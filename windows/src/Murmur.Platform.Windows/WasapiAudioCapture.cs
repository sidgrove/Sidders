using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Murmur.Abstractions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Murmur.Platform.Windows;

/// <summary>
/// Microphone capture through WASAPI, delivering 16 kHz mono float.
/// </summary>
/// <remarks>
/// <para>
/// WASAPI shared mode normally pins you to the engine mix format (typically 48 kHz stereo),
/// but <c>AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM</c> inserts a channel matrixer and a sample-rate
/// converter, and NAudio's <see cref="WasapiCapture"/> sets that flag unconditionally. So the
/// first attempt simply asks the OS for the format the model wants.
/// </para>
/// <para>
/// The flag is named AUTOCONVERT<b>PCM</b> and the docs do not say whether IEEE float is
/// accepted as a client format. Since this cannot be tested here, the code tries three
/// formats in order and falls back to converting in managed code. All three paths produce the
/// same thing, so callers never see the difference.
/// </para>
/// </remarks>
public sealed class WasapiAudioCapture : IAudioCapture
{
    private const int BufferMilliseconds = 50;

    private readonly Func<string?> _deviceId;
    private WasapiCapture? _capture;
    private MMDevice? _device;

    private Channel<float[]>? _channel;
    private BufferedWaveProvider? _rawSink;
    private WdlResamplingSampleProvider? _pipeline;
    private bool _nativeTargetFormat;
    private float[] _pullBuffer = [];

    /// <summary>Captures from a specific device, or the default when null.</summary>
    /// <param name="deviceId">
    /// Returns an <c>MMDevice.ID</c>, or null for the system default. A function rather than
    /// a value so a microphone chosen in Settings is used on the very next recording.
    /// </param>
    public WasapiAudioCapture(Func<string?>? deviceId = null) => _deviceId = deviceId ?? (static () => null);

    /// <inheritdoc />
    public bool IsCapturing { get; private set; }

    /// <summary>
    /// True when the microphone appears to be muted at the OS level.
    /// </summary>
    /// <remarks>
    /// When "Let desktop apps access your microphone" is off, WASAPI does not fail — it
    /// returns a stream of digital silence. There is no documented way for an unpackaged app
    /// to query that setting, so exact-zero samples are the only available signal. Real
    /// microphones have a noise floor, so a run of *precisely* zero is a reliable tell.
    /// </remarks>
    public bool LooksLikeBlockedMicrophone { get; private set; }

    private int _consecutiveSilentChunks;

    /// <inheritdoc />
    public async IAsyncEnumerable<AudioChunk> CaptureAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        StartCapture();

        try
        {
            await foreach (var buffer in _channel!.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return new AudioChunk(buffer);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    private void StartCapture()
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = OpenDevice(enumerator, _deviceId());
        // Kept for the life of the stream and released in StopCapture: WasapiCapture only
        // disposes the AudioClient it made from the device, never the device itself.
        _device = device;

        // Bounded and drop-oldest so a slow consumer can never block the capture thread.
        // Losing the oldest audio is bad; stalling the audio engine is worse.
        _channel = Channel.CreateBounded<float[]>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        _consecutiveSilentChunks = 0;
        LooksLikeBlockedMicrophone = false;

        foreach (var attempt in Formats(device))
        {
            var capture = new WasapiCapture(device, useEventSync: true, BufferMilliseconds)
            {
                ShareMode = AudioClientShareMode.Shared,
            };

            try
            {
                capture.WaveFormat = attempt;
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();

                _nativeTargetFormat =
                    capture.WaveFormat.SampleRate == AudioChunk.SampleRate &&
                    capture.WaveFormat.Channels == 1 &&
                    capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat;

                if (!_nativeTargetFormat) BuildManagedConverter(capture.WaveFormat);

                _capture = capture;
                IsCapturing = true;
                return;
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or NotSupportedException)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                capture.Dispose();
            }
        }

        _device = null;
        device.Dispose();
        throw new InvalidOperationException("Could not open the microphone in any supported format.");
    }

    /// <summary>
    /// The chosen device, or the default when none is chosen or the chosen one has gone.
    /// </summary>
    /// <remarks>
    /// A remembered id can point at a headset that is no longer plugged in. Falling back to
    /// the default is the right call: the user pressed the key to dictate, not to be told
    /// about a device they already know is in a drawer.
    /// </remarks>
    private static MMDevice OpenDevice(MMDeviceEnumerator enumerator, string? chosenId)
    {
        if (chosenId is not null)
        {
            try
            {
                var chosen = enumerator.GetDevice(chosenId);
                if (chosen.State == DeviceState.Active) return chosen;
                chosen.Dispose();
            }
            catch (COMException)
            {
                // Not present any more; fall through.
            }
        }

        // Communications, not Console: this follows the device the user chose as their
        // default *communication* device, which is what headset users expect.
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
    }

    /// <summary>Formats to try, best first.</summary>
    private static IEnumerable<WaveFormat> Formats(MMDevice device)
    {
        // Exactly what the model wants — the OS resamples for us if it accepts this.
        yield return WaveFormat.CreateIeeeFloatWaveFormat(AudioChunk.SampleRate, 1);

        // Right rate and channel count, integer samples. Cheap to convert.
        yield return new WaveFormat(AudioChunk.SampleRate, 16, 1);

        // Whatever the engine is already running at; we convert in managed code. The
        // AudioClient property builds a new COM object on every get, so hold and dispose it.
        using var client = device.AudioClient;
        yield return client.MixFormat;
    }

    private void BuildManagedConverter(WaveFormat source)
    {
        _rawSink = new BufferedWaveProvider(source)
        {
            BufferDuration = TimeSpan.FromSeconds(3),
            DiscardOnBufferOverflow = true,
            // ReadFully defaults to TRUE, which pads a short read with silence to the full
            // request. Left on, the drain loop below never sees the empty sink and spins
            // forever on NAudio's capture thread — WASAPI is never read again, the channel
            // fills with zeros, and Dispose blocks joining that thread.
            ReadFully = false,
        };

        ISampleProvider provider = _rawSink.ToSampleProvider();
        if (source.Channels == 2)
        {
            provider = new StereoToMonoSampleProvider(provider) { LeftVolume = 0.5f, RightVolume = 0.5f };
        }
        else if (source.Channels > 2)
        {
            provider = new DownmixSampleProvider(provider);
        }

        // WDL, not MediaFoundationResampler: pure managed, so no COM apartment concerns and
        // nothing extra to extract from a single-file bundle.
        _pipeline = new WdlResamplingSampleProvider(provider, AudioChunk.SampleRate);
        _pullBuffer = new float[AudioChunk.SampleRate / 10];
    }

    /// <summary>
    /// Runs on NAudio's dedicated capture thread — never the UI thread.
    /// </summary>
    /// <remarks>
    /// <b>The buffer is reused on every callback.</b> <c>WasapiCapture</c> allocates one array
    /// in <c>InitializeCaptureDevice</c> and hands out the same reference every time, and
    /// <c>WaveInEventArgs</c> stores it without cloning. Anything retained past the end of
    /// this method is overwritten in place by the next callback. Copy, always.
    /// </remarks>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        if (_nativeTargetFormat)
        {
            var count = e.BytesRecorded / sizeof(float);
            var owned = new float[count];
            MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded)).CopyTo(owned);
            Publish(owned);
            return;
        }

        _rawSink!.AddSamples(e.Buffer, 0, e.BytesRecorded);   // AddSamples copies internally

        // Drain while the sink still holds input. Bounded by what was just added: every
        // read consumes sink bytes, so this cannot spin. The resampler may hand back a
        // sample or two of residue on an empty sink; a short read is not an error.
        while (_rawSink.BufferedBytes > 0)
        {
            var read = _pipeline!.Read(_pullBuffer, 0, _pullBuffer.Length);
            if (read <= 0) return;

            var owned = new float[read];
            Array.Copy(_pullBuffer, owned, read);
            Publish(owned);
        }
    }

    private void Publish(float[] samples)
    {
        DetectBlockedMicrophone(samples);
        _channel?.Writer.TryWrite(samples);   // TryWrite never blocks
    }

    private void DetectBlockedMicrophone(float[] samples)
    {
        var allZero = true;
        foreach (var sample in samples)
        {
            if (sample != 0f) { allZero = false; break; }
        }

        // ~1.5s of exactly-zero samples. A live microphone always has a noise floor, so this
        // means the OS is feeding us silence rather than the room being quiet. The flag
        // describes the stream *now*: the first real sample clears it, so a headset muted
        // for a call and unmuted since does not keep reporting itself as blocked for the
        // rest of a warm stream.
        if (!allZero)
        {
            _consecutiveSilentChunks = 0;
            LooksLikeBlockedMicrophone = false;
            return;
        }

        _consecutiveSilentChunks++;
        if (_consecutiveSilentChunks > 1500 / BufferMilliseconds) LooksLikeBlockedMicrophone = true;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // A non-null exception here is usually AUDCLNT_E_DEVICE_INVALIDATED — the device was
        // unplugged or reconfigured mid-capture.
        _channel?.Writer.TryComplete(e.Exception);
        IsCapturing = false;
    }

    private void StopCapture()
    {
        if (_capture is null) return;

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        try { _capture.StopRecording(); } catch (COMException) { /* already gone */ }
        _capture.Dispose();
        _capture = null;
        _device?.Dispose();
        _device = null;

        _channel?.Writer.TryComplete();
        IsCapturing = false;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        StopCapture();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Averages an arbitrary number of channels down to one.
/// </summary>
/// <remarks>
/// NAudio's <see cref="StereoToMonoSampleProvider"/> handles exactly two channels. USB audio
/// interfaces routinely present four or eight.
/// </remarks>
internal sealed class DownmixSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private float[] _buffer = [];

    public DownmixSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var needed = count * _channels;
        if (_buffer.Length < needed) _buffer = new float[needed];

        var read = _source.Read(_buffer, 0, needed);
        var frames = read / _channels;

        for (var frame = 0; frame < frames; frame++)
        {
            float sum = 0;
            for (var channel = 0; channel < _channels; channel++)
            {
                sum += _buffer[(frame * _channels) + channel];
            }
            buffer[offset + frame] = sum / _channels;
        }

        return frames;
    }
}
