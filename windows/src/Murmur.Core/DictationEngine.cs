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

/// <summary>How the key starts and stops a recording.</summary>
public enum ActivationMode
{
    /// <summary>Hold the key down while talking.</summary>
    Hold,

    /// <summary>Tap to start, tap again to stop. The release is ignored.</summary>
    Tap,

    /// <summary>
    /// Both: a quick tap toggles, a longer hold is push-to-talk. Nothing to choose.
    /// </summary>
    Automatic,
}

/// <summary>What happens to a full stop at the very end of a dictation.</summary>
public enum TrailingFullStop
{
    /// <summary>Leave it.</summary>
    Keep,

    /// <summary>Drop it after a lone sentence; prose keeps it.</summary>
    DropAfterSingleSentence,

    /// <summary>Never end with one.</summary>
    Never,
}

/// <summary>One completed dictation.</summary>
/// <param name="At">When the key was released.</param>
/// <param name="AudioDuration">How long the key was held.</param>
/// <param name="ProcessingTime">Release to finished text.</param>
/// <param name="Text">The final text, after corrections, rules, clean-up and polish.</param>
/// <param name="Corrections">Dictionary corrections that fired.</param>
/// <param name="CleanedBy">The model that cleaned the text, or null.</param>
/// <param name="RawText">What the speech model heard, before any rules or clean-up.</param>
/// <param name="CleanupFailed">The AI tier was on but its answer was unusable, so <paramref name="Text"/> is the local result.</param>
public sealed record DictationResult(
    DateTimeOffset At,
    TimeSpan AudioDuration,
    TimeSpan ProcessingTime,
    string Text,
    IReadOnlyList<AppliedCorrection> Corrections,
    string? CleanedBy = null,
    string? RawText = null,
    bool CleanupFailed = false);

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

    /// <summary>Terminal spoken command that presses Enter; blank disables it.</summary>
    public string SendWord { get; set; } = "blob";

    /// <summary>Comma-separated alternative words recognised only at the end of dictation.</summary>
    public string SendWordAliases { get; set; } = string.Empty;

    /// <summary>Phrase that presses Enter only when it is the entire dictation; blank disables it.</summary>
    public string SendOnlyPhrase { get; set; } = "send it";

    /// <summary>Copies the final transcript before delivery, even when automatic typing is off.</summary>
    public Func<string, Task>? CopyTranscriptAsync { get; set; }

    /// <summary>What happens to a full stop at the very end.</summary>
    public TrailingFullStop FullStops { get; set; } = TrailingFullStop.DropAfterSingleSentence;

    /// <summary>Shorthand for <see cref="FullStops"/> being the single-sentence rule.</summary>
    public bool DropSingleSentenceFullStop
    {
        get => FullStops == TrailingFullStop.DropAfterSingleSentence;
        set => FullStops = value ? TrailingFullStop.DropAfterSingleSentence : TrailingFullStop.Keep;
    }

    /// <summary>How the key works. See <see cref="ActivationMode"/>. Hold by default; settings choose Automatic.</summary>
    public ActivationMode Mode { get; set; } = ActivationMode.Hold;

    /// <summary>Shorthand for <see cref="Mode"/> being <see cref="ActivationMode.Tap"/>.</summary>
    public bool TapToToggle
    {
        get => Mode == ActivationMode.Tap;
        set => Mode = value ? ActivationMode.Tap : ActivationMode.Hold;
    }

    /// <summary>
    /// In <see cref="ActivationMode.Automatic"/>, a press shorter than this is a tap and
    /// leaves the recording running; anything longer was a hold and stops on release.
    /// </summary>
    public static readonly TimeSpan TapThreshold = TimeSpan.FromMilliseconds(400);

    /// <summary>Whether "new line", "full stop", "scratch that" and so on are applied locally.</summary>
    public bool SpokenCommands { get; set; } = true;

    /// <summary>Whether "um", "er" and friends are removed locally.</summary>
    public bool RemoveFillers { get; set; } = true;

    /// <summary>
    /// Turns other applications' playback down while recording. Null where the platform
    /// offers none.
    /// </summary>
    public IAudioDucker? Ducker { get; set; }

    /// <summary>Whether <see cref="Ducker"/> is used. Read at the start of each recording.</summary>
    public bool DuckAudio
    {
        get { lock (_audioLock) return _duckAudio; }
        set
        {
            lock (_audioLock)
            {
                _duckAudio = value;
                if (!value) RestoreAudio();
            }
        }
    }

    private readonly object _audioLock = new();
    private bool _duckAudio = true;

    private bool _ducked;

    /// <summary>
    /// The running transcript of the current recording, refreshed every
    /// <see cref="PreviewInterval"/> once a second of audio exists. Empty when idle.
    /// </summary>
    public string Preview { get; private set; } = string.Empty;

    /// <summary>Raised when <see cref="Preview"/> changes. Engine thread.</summary>
    public event EventHandler? PreviewChanged;

    /// <summary>How often the preview is re-decoded while recording.</summary>
    public static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(700);

    /// <summary>Preview needs at least this much audio to be worth decoding.</summary>
    public static readonly TimeSpan PreviewMinimum = TimeSpan.FromSeconds(1);

    private Task? _preview;
    private DateTimeOffset _pressedAt;

    /// <summary>The generative clean-up, or null when none is configured.</summary>
    public ITranscriptCleaner? Cleaner { get; set; }

    /// <summary>Whether <see cref="Cleaner"/> is used. Off means the raw path, always.</summary>
    public bool AiCleanup { get; set; }

    /// <summary>
    /// Whether the key does anything. Off leaves the hook installed but ignores it, so
    /// switching back on is instant and nothing is re-registered.
    /// </summary>
    public bool IsEnabled
    {
        get { lock (_audioLock) return _isEnabled; }
        set
        {
            lock (_audioLock)
            {
                _isEnabled = value;
                if (!value) RestoreAudio();
            }
            if (!value) Cancel();
        }
    }

    private bool _isEnabled = true;

    /// <summary>The push-to-talk key, as a virtual-key code. Applies to the next press.</summary>
    public int HotkeyVirtualKey
    {
        get => _hotkey.VirtualKey;
        set { _hotkey.VirtualKey = value; Log.Info($"hotkey changed to 0x{value:X2}"); }
    }

    /// <summary>Raised once after <see cref="BeginCapture"/> with what the user pressed. Hook thread.</summary>
    public event EventHandler<(int VirtualKey, int Modifiers)>? Captured
    {
        add => _hotkey.Captured += value;
        remove => _hotkey.Captured -= value;
    }

    /// <summary>Records the next chord instead of acting on it.</summary>
    public void BeginCapture() => _hotkey.BeginCapture();

    /// <summary>Abandons a capture.</summary>
    public void CancelCapture() => _hotkey.CancelCapture();

    /// <summary>Modifiers required with the key, as <see cref="HotkeyModifiers"/> flags.</summary>
    public int HotkeyModifiers
    {
        get => _hotkey.Modifiers;
        set { _hotkey.Modifiers = value; Log.Info($"hotkey modifiers changed to {(Abstractions.HotkeyModifiers)value}"); }
    }

    /// <summary>Raised when a dictation completes and produced text.</summary>
    public event EventHandler<DictationResult>? Completed;

    /// <summary>Raised only after the explicit send command successfully presses Enter.</summary>
    public event EventHandler? Sent;

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
        _hotkey.CancelPressed += OnCancelPressed;
    }

    /// <summary>Arms the hotkey.</summary>
    /// <returns>False if the hook could not be installed.</returns>
    public bool Start()
    {
        IsHotkeyArmed = _hotkey.Start();
        Log.Info(IsHotkeyArmed ? "hotkey armed" : "hotkey could NOT be installed");

        if (!IsHotkeyArmed) Fault("The push-to-talk key could not be hooked. Try restarting Acapella.");
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

    /// <summary>Discards the recording in progress, typing nothing.</summary>
    public void Cancel()
    {
        if (State == DictationState.Recording) _ = AbandonRecordingAsync();
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        if (!IsEnabled) return;
        _pressedAt = _clock.Now;

        switch (Mode)
        {
            case ActivationMode.Hold:
                _ = BeginAsync();
                break;
            case ActivationMode.Tap:
            case ActivationMode.Automatic:
                TogglePushToTalk();
                break;
        }
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        if (!IsEnabled && State == DictationState.Idle) return;

        switch (Mode)
        {
            case ActivationMode.Hold:
                _ = EndAsync();
                break;
            case ActivationMode.Automatic:
                // A quick tap leaves it running; a hold was push-to-talk and ends here.
                if (_clock.Now - _pressedAt >= TapThreshold) _ = EndAsync();
                break;
        }
    }

    private void OnCancelPressed(object? sender, EventArgs e) => Cancel();

    private async Task BeginAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsEnabled || State != DictationState.Idle) return;

            _buffer = [];
            _startedAt = _clock.Now;
            _recording = new CancellationTokenSource();
            SetPreview(string.Empty);
            SetState(DictationState.Recording);
            _preview = PreviewLoopAsync(_recording.Token);
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
            if (_recording is not null) await _recording.CancelAsync().ConfigureAwait(false);
            _buffer = null;
            _recording?.Dispose();
            _recording = null;
            SetPreview(string.Empty);
            SetState(DictationState.Idle);
            Log.Info("recording cancelled");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Re-decodes the audio so far while the key is held, so words appear as they are
    /// spoken rather than all at once on release.
    /// </summary>
    /// <remarks>
    /// The offline model decodes many times faster than real time, so a whole-buffer pass
    /// every 700 ms costs a fraction of a second even for a long dictation. A tick that
    /// finds the previous one still running is skipped, and the final pass waits for the
    /// last tick so the model is never asked to decode two streams at once.
    /// </remarks>
    private async Task PreviewLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PreviewInterval, cancellationToken).ConfigureAwait(false);
                if (!_transcriber.IsReady) continue;

                float[]? snapshot;
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (State != DictationState.Recording || _buffer is null) return;
                    if (_buffer.Count < PreviewMinimum.TotalSeconds * AudioChunk.SampleRate) continue;
                    snapshot = _buffer.ToArray();
                }
                finally
                {
                    _gate.Release();
                }

                var text = await _transcriber.TranscribeAsync(snapshot, [], cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested && State == DictationState.Recording) SetPreview(text.Trim());
            }
        }
        catch (OperationCanceledException)
        {
            // The key was released.
        }
        catch (Exception e)
        {
            // Preview is a nicety. It must never take the real transcription down with it.
            Log.Warn($"preview stopped: {e.Message}");
        }
    }

    private void SetPreview(string text)
    {
        if (text == Preview) return;
        Preview = text;
        PreviewChanged?.Invoke(this, EventArgs.Empty);
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
            if (_preview is { } preview) await preview.ConfigureAwait(false);
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
            _preview = null;
            SetPreview(string.Empty);
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

        // The dictionary runs first and unconditionally. Biasing only raises the odds of the
        // right word; this is the pass that guarantees it.
        if (SpokenSendCommand.IsStandalone(raw, SendOnlyPhrase))
        {
            if (InjectText) await SendToFocusedAppAsync().ConfigureAwait(false);
            return;
        }
        // Detect before corrections or AI can alter or invent the command.
        var (content, send) = SpokenSendCommand.Extract(raw, SendWord, SendWordAliases);
        if (send && string.IsNullOrWhiteSpace(content))
        {
            if (InjectText) await SendToFocusedAppAsync().ConfigureAwait(false);
            return;
        }
        var (dictionaryText, applied) = new DictionaryCorrector(entries).Apply(content);

        // Then the rules: spoken commands and fillers, deterministically, so local-only mode
        // is complete on its own and the AI tier has less to do.
        var local = SpokenFormatting.Apply(dictionaryText, SpokenCommands, RemoveFillers);
        if (string.IsNullOrWhiteSpace(local))
        {
            Log.Info("nothing left after rules (a filler, or a command with nothing before it)");
            return;
        }

        // The generative tier sits between the rules and the polish: names arrive already
        // corrected, and the full-stop rule still applies to what comes back. If it fails
        // for any reason the local text is used — a dictation is never lost to the cloud
        // being down.
        string? cleanedBy = null;
        var cleanupFailed = false;
        var candidate = local;
        if (AiCleanup && Cleaner is { } cleaner && CleanupGuard.IsWorthCleaning(local))
        {
            var cleaned = await cleaner.CleanAsync(local, CancellationToken.None).ConfigureAwait(false);
            if (cleaned is not null && !CleanupGuard.IsPlausible(local, cleaned))
            {
                // The model summarised or padded. Wispr-grade means never doing that to
                // someone's words; the local result wins, quietly.
                Log.Warn($"AI clean-up rewrote rather than tidied ({local.Length} -> {cleaned.Length} chars); kept the local text");
                cleanupFailed = true;
            }
            else if (cleaned is not null)
            {
                candidate = cleaned;
                cleanedBy = cleaner.Name;
            }
            else
            {
                Log.Warn($"AI clean-up ({cleaner.Name}) returned nothing; typed the local text");
                cleanupFailed = true;
                Fault("AI clean-up did not respond, so the local transcript was typed. Check the key and connection in Settings.");
            }
        }

        // Polish runs last so a correction or a clean-up that ends a sentence is treated
        // the same as one the engine produced itself.
        // A recognised command never belongs in the final text or clipboard.
        if (send) candidate = SpokenSendCommand.Extract(candidate, SendWord, SendWordAliases).Text;
        var corrected = TranscriptPolish.Apply(candidate, FullStops);

        var result = new DictationResult(
            At: releasedAt,
            AudioDuration: TimeSpan.FromSeconds((double)audio.Length / AudioChunk.SampleRate),
            ProcessingTime: _clock.Now - releasedAt,
            Text: corrected,
            Corrections: applied,
            CleanedBy: cleanedBy,
            RawText: raw,
            CleanupFailed: cleanupFailed);

        if (cleanedBy is not null || !AiCleanup) LastFault = null;
        if (CopyTranscriptAsync is { } copy)
        {
            try { await copy(corrected).ConfigureAwait(false); }
            catch (Exception e) { Log.Warn($"Could not copy transcription to clipboard: {e.Message}"); }
        }
        Completed?.Invoke(this, result);

        if (!InjectText) return;

        var delivered = await _injector.InjectAsync(corrected, CancellationToken.None).ConfigureAwait(false);
        if (!delivered)
        {
            Log.Warn("text could not be delivered to the focused app");
            Fault("The text could not be typed into the focused app. It is in the history — press COPY.");
        }
        else if (send)
        {
            await SendToFocusedAppAsync().ConfigureAwait(false);
        }
    }

    private async Task SendToFocusedAppAsync()
    {
        if (await _injector.SendAsync(CancellationToken.None).ConfigureAwait(false))
            Sent?.Invoke(this, EventArgs.Empty);
        else
            Fault("Enter could not be pressed. Send the text manually.");
    }

    private DateTimeOffset _lastFaultAt = DateTimeOffset.MinValue;

    /// <summary>Whether <see cref="Faulted"/> fired within the last second, so a follow-up notice can defer to it.</summary>
    public bool IsFaultedRecently => _clock.Now - _lastFaultAt < TimeSpan.FromSeconds(1);

    private void Fault(string message)
    {
        LastFault = message;
        _lastFaultAt = _clock.Now;
        Faulted?.Invoke(this, message);
    }

    private void SetState(DictationState state)
    {
        lock (_audioLock)
        {
            var wasRecording = State == DictationState.Recording;
            State = state;
            var isRecording = state == DictationState.Recording;
            if (isRecording && !wasRecording && _isEnabled && _duckAudio && Ducker is { } ducker)
            {
                _ducked = true;
                ducker.Duck();
                Log.Info("other audio ducked");
            }
            else if (!isRecording)
            {
                RestoreAudio();
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreAudio()
    {
        lock (_audioLock)
        {
            if (!_ducked) return;
            _ducked = false;
            Ducker?.Restore();
            Log.Info("other audio restored");
        }
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _hotkey.Pressed -= OnPressed;
        _hotkey.Released -= OnReleased;
        _hotkey.CancelPressed -= OnCancelPressed;
        _hotkey.Dispose();

        if (_recording is not null)
        {
            await _recording.CancelAsync().ConfigureAwait(false);
            _recording.Dispose();
        }

        // Belt and braces: a recording cut short by shutdown must not leave the user's
        // music at a whisper.
        RestoreAudio();

        await _capture.DisposeAsync().ConfigureAwait(false);
        await _transcriber.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
