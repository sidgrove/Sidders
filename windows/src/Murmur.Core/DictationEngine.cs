using Murmur.Abstractions;
using Murmur.Dictionary;

namespace Murmur.Core;

/// <summary>What the engine is doing right now.</summary>
public enum DictationState
{
    /// <summary>Waiting for the hotkey.</summary>
    Idle,

    /// <summary>The key is held; audio is being captured.</summary>
    Recording,

    /// <summary>The key is released; the utterance is being transcribed.</summary>
    Transcribing,
}

/// <summary>One completed dictation.</summary>
public sealed record DictationResult(
    DateTimeOffset At,
    TimeSpan AudioDuration,
    TimeSpan ProcessingTime,
    string Text,
    IReadOnlyList<AppliedCorrection> Corrections,
    string? CleanedBy = null);

/// <summary>
/// The whole dictation flow: hotkey down, capture, hotkey up, transcribe, correct, inject.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately platform-neutral. It targets plain <c>net10.0</c>, so <c>CA1416</c> turns any
/// accidental Windows API call in here into a build error. Everything platform-specific
/// arrives through the interfaces it is constructed with.
/// </para>
/// <para>
/// That is what makes the interesting behaviour testable without Windows: hand it fakes and
/// the entire path — including chunking, the correction pass and the "nothing was said"
/// case — runs on any machine, in milliseconds.
/// </para>
/// <para>
/// <b>Every failure becomes a sentence.</b> The first real-hardware run showed why: both
/// async paths were fired with <c>_ = ...</c>, so a missing assembly, a blocked microphone or
/// a model that would not load simply vanished and the panel did nothing. Now each path
/// catches, logs, raises <see cref="Faulted"/>, and always returns to <see cref="DictationState.Idle"/>.
/// </para>
/// </remarks>
public sealed class DictationEngine : IAsyncDisposable
{
    /// <summary>The message shown when the OS is feeding silence instead of the microphone.</summary>
    public const string BlockedMicrophoneMessage =
        "Windows is blocking microphone access. Turn on “Let desktop apps access your microphone” "
        + "in Settings → Privacy & security → Microphone.";

    /// <summary>The message shown when there is no model to transcribe with.</summary>
    public const string ModelMissingMessage =
        "Speech model not installed. Download it from Settings → Model.";

    private readonly IAudioCapture _capture;
    private readonly IHotkeySource _hotkey;
    private readonly ITranscriber _transcriber;
    private readonly ITextInjector _injector;
    private readonly IClock _clock;
    private readonly Func<IReadOnlyList<DictionaryEntry>> _dictionary;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _recording;
    private List<float>? _buffer;
    private DateTimeOffset _startedAt;

    /// <summary>Current state.</summary>
    public DictationState State { get; private set; } = DictationState.Idle;

    /// <summary>Most recent input level, 0…1. Drives the meter.</summary>
    public float Level { get; private set; }

    /// <summary>Whether the push-to-talk hook is installed and listening.</summary>
    public bool IsHotkeyArmed { get; private set; }

    /// <summary>The most recent fault, or null once a dictation has since succeeded.</summary>
    public string? LastFault { get; private set; }

    /// <summary>Whether text should be typed into the focused app after a dictation.</summary>
    public bool InjectText { get; set; } = true;

    /// <summary>Whether a lone sentence loses its trailing full stop before typing.</summary>
    public bool DropSingleSentenceFullStop { get; set; } = true;

    /// <summary>
    /// Tap to start, tap to stop, instead of hold. The release event is ignored.
    /// </summary>
    public bool TapToToggle { get; set; }

    /// <summary>The generative clean-up, or null when none is configured.</summary>
    public ITranscriptCleaner? Cleaner { get; set; }

    /// <summary>Whether <see cref="Cleaner"/> is used. Off means the raw path, always.</summary>
    public bool AiCleanup { get; set; }

    /// <summary>
    /// Whether the key does anything. Off leaves the hook installed but ignores it, so
    /// switching back on is instant and nothing is re-registered.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The push-to-talk key, as a virtual-key code. Applies to the next press.</summary>
    public int HotkeyVirtualKey
    {
        get => _hotkey.VirtualKey;
        set { _hotkey.VirtualKey = value; Log.Info($"hotkey changed to 0x{value:X2}"); }
    }

    /// <summary>Raised when a dictation completes and produced text.</summary>
    public event EventHandler<DictationResult>? Completed;

    /// <summary>Raised whenever <see cref="State"/> or <see cref="Level"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised with a human-readable message when something went wrong.</summary>
    public event EventHandler<string>? Faulted;

    /// <summary>Wires the engine to its platform implementations.</summary>
    /// <param name="capture">Microphone source.</param>
    /// <param name="hotkey">Push-to-talk source.</param>
    /// <param name="transcriber">Speech engine.</param>
    /// <param name="injector">Where finished text goes.</param>
    /// <param name="dictionary">
    /// Read fresh on every utterance rather than captured once, so edits take effect without
    /// a restart.
    /// </param>
    /// <param name="clock">Time source; defaults to the system clock.</param>
    public DictationEngine(
        IAudioCapture capture,
        IHotkeySource hotkey,
        ITranscriber transcriber,
        ITextInjector injector,
        Func<IReadOnlyList<DictionaryEntry>> dictionary,
        IClock? clock = null)
    {
        _capture = capture;
        _hotkey = hotkey;
        _transcriber = transcriber;
        _injector = injector;
        _dictionary = dictionary;
        _clock = clock ?? SystemClock.Instance;

        _hotkey.Pressed += OnPressed;
        _hotkey.Released += OnReleased;
    }

    /// <summary>Arms the hotkey.</summary>
    /// <returns>False if the hook could not be installed.</returns>
    public bool Start()
    {
        IsHotkeyArmed = _hotkey.Start();
        Log.Info(IsHotkeyArmed ? "hotkey armed" : "hotkey could NOT be installed");

        if (!IsHotkeyArmed) Fault("The push-to-talk key could not be hooked. Try restarting Sidders.");
        return IsHotkeyArmed;
    }

    /// <summary>
    /// Loads the speech model ahead of the first utterance.
    /// </summary>
    /// <remarks>
    /// The model takes a couple of seconds to load, and paying that on the first dictation
    /// reads as the app being broken. Called at startup, and again after a download.
    /// </remarks>
    /// <returns>True if a model is loaded.</returns>
    public async Task<bool> PreloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var started = _clock.Now;
            var ready = await _transcriber.LoadAsync(cancellationToken).ConfigureAwait(false);
            Log.Info(ready
                ? $"model loaded in {(_clock.Now - started).TotalMilliseconds:0} ms"
                : "model not available");
            return ready;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.Error("model load failed", e);
            Fault($"The speech model failed to load: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts or stops recording from a button rather than the hotkey.
    /// </summary>
    /// <remarks>
    /// Routed through the same state machine as the hotkey, deliberately. Two independent
    /// paths into recording would eventually disagree about whether it is running.
    /// </remarks>
    public void TogglePushToTalk()
    {
        if (State == DictationState.Idle) _ = BeginAsync();
        else if (State == DictationState.Recording) _ = EndAsync();
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        if (!IsEnabled) return;
        if (TapToToggle) TogglePushToTalk();
        else _ = BeginAsync();
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        if (!IsEnabled && State == DictationState.Idle) return;
        if (!TapToToggle) _ = EndAsync();
    }

    private async Task BeginAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State != DictationState.Idle) return;

            _buffer = [];
            _startedAt = _clock.Now;
            _recording = new CancellationTokenSource();
            SetState(DictationState.Recording);
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await foreach (var chunk in _capture.CaptureAsync(_recording!.Token).ConfigureAwait(false))
            {
                // Stop consuming the moment recording ends. Cancellation is cooperative, so
                // chunks already queued still arrive after EndAsync has moved on — and
                // without this guard one of them sets Level back to a reading that has
                // already been zeroed.
                if (State != DictationState.Recording) break;

                // Copied, not referenced: capture implementations are entitled to reuse
                // their buffer the moment this returns.
                _buffer?.AddRange(chunk.Samples.Span);
                Level = chunk.Rms();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the key was released.
        }
        catch (Exception e)
        {
            // The device vanished, the assembly did not load, the format was refused. The
            // recording that was in flight is over; say so and get back to Idle.
            Log.Error("audio capture failed", e);
            Fault($"The microphone could not be opened: {e.Message}");
            await AbandonRecordingAsync().ConfigureAwait(false);
        }
        finally
        {
            // Authoritative: this runs only once the capture loop has genuinely finished, so
            // nothing can raise the level afterwards and leave the meter stuck.
            Level = 0;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task AbandonRecordingAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State != DictationState.Recording) return;
            _buffer = null;
            _recording?.Dispose();
            _recording = null;
            SetState(DictationState.Idle);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EndAsync()
    {
        List<float>? samples;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State != DictationState.Recording) return;

            await _recording!.CancelAsync().ConfigureAwait(false);
            samples = _buffer;
            _buffer = null;
            Level = 0;
            SetState(DictationState.Transcribing);
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await ProcessAsync(samples).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("dictation failed", e);
            Fault($"Transcription failed: {e.Message}");
        }
        finally
        {
            _recording?.Dispose();
            _recording = null;
            SetState(DictationState.Idle);
        }
    }

    /// <summary>
    /// Recordings shorter than this are dropped without transcribing.
    /// </summary>
    /// <remarks>
    /// Observed on real hardware: a brief tap of the key — often a Shift pressed for a
    /// capital letter — yields 30 to 400 ms of room tone, and Parakeet hallucinates
    /// "Mm-hmm." onto it, which then gets typed. No word fits in less than this.
    /// </remarks>
    public static readonly TimeSpan MinimumUtterance = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Recordings whose overall level is below this are dropped as silence, whatever their
    /// length. Speech into any working microphone measures well above it.
    /// </summary>
    public const float SilenceFloor = 0.002f;

    private async Task ProcessAsync(List<float>? samples)
    {
        if (samples is null || samples.Count == 0) return;

        var seconds = (double)samples.Count / AudioChunk.SampleRate;
        if (seconds < MinimumUtterance.TotalSeconds)
        {
            Log.Info($"ignored a {seconds * 1000:0} ms tap of the key");
            return;
        }

        if (new AudioChunk(samples.ToArray()).Rms() < SilenceFloor && !_capture.LooksLikeBlockedMicrophone)
        {
            Log.Info($"ignored {seconds:0.0}s of silence");
            return;
        }

        if (_capture.LooksLikeBlockedMicrophone)
        {
            Log.Warn("capture delivered only digital silence — microphone looks blocked");
            Fault(BlockedMicrophoneMessage);
            return;
        }

        // Found on the first real-hardware run: nothing ever loaded the model, so with the
        // files on disk every transcript still came back empty.
        if (!_transcriber.IsReady && !await _transcriber.LoadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            Log.Warn("no model to transcribe with");
            Fault(ModelMissingMessage);
            return;
        }

        // Measured from key release, because that is the wait the user actually feels — and
        // it is the only figure on which a streaming and a batch engine compare honestly.
        var releasedAt = _clock.Now;
        var audio = new ReadOnlyMemory<float>(samples.ToArray());

        var entries = _dictionary();
        var bias = DictionaryCorrector.BiasPhrases(entries);

        var pieces = AudioSegmenter.Split(audio);
        var transcripts = new List<string>(pieces.Count);

        foreach (var piece in pieces)
        {
            var text = await _transcriber
                .TranscribeAsync(piece, bias, CancellationToken.None)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(text)) transcripts.Add(text.Trim());
        }

        var raw = string.Join(' ', transcripts);
        Log.Info($"transcribed {audio.Length / (double)AudioChunk.SampleRate:0.0}s of audio "
               + $"in {(_clock.Now - releasedAt).TotalMilliseconds:0} ms: {raw.Length} chars");

        if (string.IsNullOrWhiteSpace(raw)) return;

        // The dictionary runs last and unconditionally. Biasing only raises the odds of the
        // right word; this is the pass that guarantees it.
        var (dictionaryText, applied) = new DictionaryCorrector(entries).Apply(raw);

        // The generative tier sits between the dictionary and the polish: names arrive
        // already corrected, and the single-sentence rule still applies to what comes back.
        // If it fails for any reason the local text is used — a dictation is never lost to
        // the cloud being down.
        string? cleanedBy = null;
        var candidate = dictionaryText;
        if (AiCleanup && Cleaner is { } cleaner)
        {
            var cleaned = await cleaner.CleanAsync(dictionaryText, CancellationToken.None).ConfigureAwait(false);
            if (cleaned is not null)
            {
                candidate = cleaned;
                cleanedBy = cleaner.Name;
            }
            else
            {
                Log.Warn($"AI clean-up ({cleaner.Name}) returned nothing; typed the raw transcript");
                Fault($"AI clean-up did not respond, so the raw transcript was typed. Check the key and connection in Settings.");
            }
        }

        // Polish runs last so a correction or a clean-up that ends a sentence is treated
        // the same as one the engine produced itself.
        var corrected = TranscriptPolish.Apply(candidate, DropSingleSentenceFullStop);

        var result = new DictationResult(
            At: releasedAt,
            AudioDuration: TimeSpan.FromSeconds((double)audio.Length / AudioChunk.SampleRate),
            ProcessingTime: _clock.Now - releasedAt,
            Text: corrected,
            Corrections: applied,
            CleanedBy: cleanedBy);

        if (cleanedBy is not null || !AiCleanup) LastFault = null;
        Completed?.Invoke(this, result);

        if (!InjectText) return;

        var delivered = await _injector.InjectAsync(corrected, CancellationToken.None).ConfigureAwait(false);
        if (!delivered)
        {
            Log.Warn("text could not be delivered to the focused app");
            Fault("The text could not be typed into the focused app. It is in the history — press COPY.");
        }
    }

    private void Fault(string message)
    {
        LastFault = message;
        Faulted?.Invoke(this, message);
    }

    private void SetState(DictationState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _hotkey.Pressed -= OnPressed;
        _hotkey.Released -= OnReleased;
        _hotkey.Dispose();

        if (_recording is not null)
        {
            await _recording.CancelAsync().ConfigureAwait(false);
            _recording.Dispose();
        }

        await _capture.DisposeAsync().ConfigureAwait(false);
        await _transcriber.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
