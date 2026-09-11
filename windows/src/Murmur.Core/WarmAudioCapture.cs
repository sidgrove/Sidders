using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Murmur.Abstractions;

namespace Murmur.Core;

/// <summary>
/// Keeps the microphone open between dictations and hands each new recording the audio
/// captured just before the key was pressed.
/// </summary>
/// <remarks>
/// <para>
/// Opening a WASAPI stream on the key press means the first word is at the mercy of the
/// device: a USB interface can take a good fraction of a second before real samples arrive,
/// and anyone who starts talking as they press loses the start of the sentence. Reported on
/// 2026-09-11 as "it's not picking up the beginning".
/// </para>
/// <para>
/// So after the first recording the inner capture is left running. While no recording is in
/// progress the newest <see cref="PreRoll"/> of audio is kept in a ring; when the key is
/// pressed the ring is delivered first, then live audio, with no device start-up at all.
/// After <see cref="IdleTimeout"/> without a recording the stream is closed, so the
/// microphone indicator does not stay lit all day.
/// </para>
/// <para>
/// This lives in Core, not the platform layer, so it runs against the fake capture in CI.
/// </para>
/// </remarks>
public sealed class WarmAudioCapture : IAudioCapture
{
    /// <summary>How much audio from before the key press is included in a recording.</summary>
    public static readonly TimeSpan DefaultPreRoll = TimeSpan.FromMilliseconds(400);

    /// <summary>How long the microphone stays open after a recording with no new one.</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(5);

    private readonly IAudioCapture _inner;
    private readonly int _preRollSamples;
    private readonly TimeSpan _idleTimeout;
    private readonly Lock _lock = new();
    private readonly Queue<float[]> _ring = new();
    private int _ringSamples;

    private Channel<AudioChunk>? _session;
    private CancellationTokenSource? _pump;
    private Task? _pumpTask;
    private CancellationTokenSource? _idle;

    /// <summary>Wraps <paramref name="inner"/>.</summary>
    public WarmAudioCapture(IAudioCapture inner, TimeSpan? preRoll = null, TimeSpan? idleTimeout = null)
    {
        _inner = inner;
        PreRoll = preRoll ?? DefaultPreRoll;
        IdleTimeout = idleTimeout ?? DefaultIdleTimeout;
        _preRollSamples = (int)(PreRoll.TotalSeconds * AudioChunk.SampleRate);
        _idleTimeout = IdleTimeout;
    }

    /// <summary>Audio kept from before the key press.</summary>
    public TimeSpan PreRoll { get; }

    /// <summary>Time without a recording before the microphone is released.</summary>
    public TimeSpan IdleTimeout { get; }

    /// <summary>Whether the inner device is open, recording or not.</summary>
    public bool IsWarm => _pumpTask is { IsCompleted: false };

    /// <inheritdoc />
    public bool IsCapturing => _session is not null;

    /// <inheritdoc />
    public bool LooksLikeBlockedMicrophone => _inner.LooksLikeBlockedMicrophone;

    /// <inheritdoc />
    public TimeSpan PreRollDelivered { get; private set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<AudioChunk> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<AudioChunk> session;
        lock (_lock)
        {
            if (_session is not null) throw new InvalidOperationException("A recording is already in progress.");

            _idle?.Cancel();
            _idle = null;

            // Bounded and drop-oldest for the same reason as the device capture: a slow
            // consumer must never stall the audio thread.
            session = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(512)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true,
            });
            var delivered = 0;
            while (_ring.TryDequeue(out var kept))
            {
                session.Writer.TryWrite(new AudioChunk(kept));
                delivered += kept.Length;
            }
            PreRollDelivered = TimeSpan.FromSeconds((double)delivered / AudioChunk.SampleRate);
            _ringSamples = 0;
            _session = session;

            if (!IsWarm) StartPump();
        }

        try
        {
            await foreach (var chunk in session.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
        finally
        {
            lock (_lock)
            {
                _session = null;
                if (IsWarm) ScheduleRelease();
            }
        }
    }

    private void StartPump()
    {
        var pump = new CancellationTokenSource();
        _pump = pump;
        _pumpTask = PumpAsync(pump.Token);
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in _inner.CaptureAsync(cancellationToken).ConfigureAwait(false))
            {
                // Copied: the inner capture may reuse its buffer the moment this returns, and
                // the ring outlives that by up to PreRoll.
                var owned = chunk.Samples.ToArray();
                lock (_lock)
                {
                    if (_session is { } session)
                    {
                        session.Writer.TryWrite(new AudioChunk(owned));
                    }
                    else
                    {
                        _ring.Enqueue(owned);
                        _ringSamples += owned.Length;
                        while (_ringSamples > _preRollSamples && _ring.Count > 1)
                        {
                            _ringSamples -= _ring.Dequeue().Length;
                        }
                    }
                }
            }

            // The inner stream ended on its own (a fake ran out, or a device stopped
            // cleanly). A recording waiting on it is over.
            lock (_lock) _session?.Writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            lock (_lock) _session?.Writer.TryComplete();
        }
        catch (Exception e)
        {
            // Mid-recording this surfaces to the engine exactly as a direct capture failure
            // would. Between recordings there is no one to tell, so log it and let the next
            // key press reopen the device.
            lock (_lock)
            {
                if (_session is { } session) session.Writer.TryComplete(e);
                else Log.Warn($"warm microphone stopped: {e.Message}");
            }
        }
        finally
        {
            lock (_lock)
            {
                _ring.Clear();
                _ringSamples = 0;
            }
        }
    }

    private void ScheduleRelease()
    {
        var idle = new CancellationTokenSource();
        _idle = idle;
        _ = ReleaseAfterIdleAsync(idle.Token);
    }

    private async Task ReleaseAfterIdleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_idleTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        CancellationTokenSource? pump;
        lock (_lock)
        {
            if (_session is not null || cancellationToken.IsCancellationRequested) return;
            pump = _pump;
            _pump = null;
        }
        pump?.Cancel();
        Log.Info("microphone released after idle");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task? pumpTask;
        lock (_lock)
        {
            _idle?.Cancel();
            _pump?.Cancel();
            pumpTask = _pumpTask;
        }
        if (pumpTask is not null)
        {
            try { await pumpTask.ConfigureAwait(false); }
            catch (Exception) { /* reported through the session already */ }
        }
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}
