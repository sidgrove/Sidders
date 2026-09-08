namespace Murmur.Abstractions;

/// <summary>
/// A block of captured audio: mono, 32-bit float, 16 kHz, samples in [-1, 1].
/// </summary>
/// <remarks>
/// The format is fixed at this boundary on purpose. Every speech model this app will use
/// wants 16 kHz mono float, and resampling is a device concern — so the platform layer deals
/// with whatever the hardware offers and nothing above it ever has to ask.
/// </remarks>
/// <param name="Samples">Owned by the receiver. The producer must not reuse the buffer.</param>
public readonly record struct AudioChunk(ReadOnlyMemory<float> Samples)
{
    /// <summary>The sample rate every implementation must deliver.</summary>
    public const int SampleRate = 16000;

    /// <summary>Duration of this chunk.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

    /// <summary>
    /// Root-mean-square amplitude, 0…1. Drives the level meter.
    /// </summary>
    public float Rms()
    {
        var span = Samples.Span;
        if (span.Length == 0) return 0;

        double sum = 0;
        foreach (var sample in span) sum += (double)sample * sample;
        return (float)Math.Sqrt(sum / span.Length);
    }
}

/// <summary>Captures microphone audio.</summary>
/// <remarks>
/// Implementations must deliver <see cref="AudioChunk"/>s already converted to 16 kHz mono
/// float, and must hand over buffers they will not touch again — the single most common
/// audio bug on every platform is a capture callback reusing one array.
/// </remarks>
public interface IAudioCapture : IAsyncDisposable
{
    /// <summary>Whether capture is currently running.</summary>
    bool IsCapturing { get; }

    /// <summary>
    /// True when the device appears to be delivering digital silence.
    /// </summary>
    /// <remarks>
    /// On Windows, when "Let desktop apps access your microphone" is off, capture does not
    /// fail — it yields exact zeros. That has to reach the user as a sentence about the
    /// privacy setting rather than as an empty transcript, so implementations report it here.
    /// </remarks>
    bool LooksLikeBlockedMicrophone { get; }

    /// <summary>Starts capture and yields chunks until cancelled.</summary>
    IAsyncEnumerable<AudioChunk> CaptureAsync(CancellationToken cancellationToken);
}

/// <summary>One microphone the OS knows about.</summary>
/// <param name="Id">Stable device identifier, stored in settings.</param>
/// <param name="Name">What to show the user.</param>
/// <param name="IsDefault">Whether this is the device Windows would pick on its own.</param>
public sealed record AudioDevice(string Id, string Name, bool IsDefault);

/// <summary>Lists microphones, so the user can pick one rather than trust the OS default.</summary>
public interface IAudioDeviceCatalog
{
    /// <summary>Every active capture device, default first.</summary>
    IReadOnlyList<AudioDevice> ListCaptureDevices();
}

/// <summary>Raised when the push-to-talk key goes down or comes up.</summary>
public interface IHotkeySource : IDisposable
{
    /// <summary>The key is held.</summary>
    event EventHandler? Pressed;

    /// <summary>The key was released.</summary>
    event EventHandler? Released;

    /// <summary>Begins listening.</summary>
    /// <returns>False if the hook could not be installed.</returns>
    bool Start();

    /// <summary>Stops listening.</summary>
    /// <remarks>Not named <c>Stop</c>: that is a reserved word in VB, which CA1716 flags
    /// on any interface member.</remarks>
    void StopListening();
}

/// <summary>Types text into whatever application currently has focus.</summary>
public interface ITextInjector
{
    /// <summary>Inserts <paramref name="text"/> at the caret of the focused control.</summary>
    /// <returns>False if the text could not be delivered.</returns>
    ValueTask<bool> InjectAsync(string text, CancellationToken cancellationToken);
}

/// <summary>Turns audio into text.</summary>
public interface ITranscriber : IAsyncDisposable
{
    /// <summary>Whether the model is loaded and ready.</summary>
    bool IsReady { get; }

    /// <summary>Loads the model. Slow — call once, at startup or first use.</summary>
    ValueTask<bool> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Transcribes one utterance.</summary>
    /// <param name="samples">16 kHz mono float, in [-1, 1].</param>
    /// <param name="biasPhrases">
    /// Dictionary terms to bias the recogniser toward. May be ignored by engines that don't
    /// support it — the correction pass is what actually guarantees spelling.
    /// </param>
    /// <param name="cancellationToken">Cancels a transcription in flight.</param>
    ValueTask<string> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        IReadOnlyList<string> biasPhrases,
        CancellationToken cancellationToken);
}

/// <summary>
/// Registers the app to start when the user signs in.
/// </summary>
/// <remarks>
/// A tray-resident dictation app that has to be launched by hand every morning is not
/// really resident. Implemented per platform; where no implementation is found the toggle
/// is simply not offered.
/// </remarks>
public interface IStartupRegistration
{
    /// <summary>Whether the app is currently registered to start at sign-in.</summary>
    bool IsEnabled { get; }

    /// <summary>Registers or unregisters the running executable.</summary>
    /// <returns>False if the registration could not be changed.</returns>
    bool SetEnabled(bool enabled);
}

/// <summary>
/// Small adjustments to a native window that the UI framework does not expose.
/// </summary>
public interface IWindowTweaks
{
    /// <summary>
    /// Stops a window from ever taking keyboard focus, even when clicked.
    /// </summary>
    /// <remarks>
    /// The overlay readout shows while the user is dictating into <i>another</i> app. If it
    /// could be activated, a stray click would move focus and the text would have nowhere to
    /// go — the same load-bearing rule as the macOS HUD panel.
    /// </remarks>
    /// <param name="handle">The platform window handle.</param>
    void MakeNonActivating(nint handle);

    /// <summary>
    /// The centre of the window the user is working in, in screen pixels, or null.
    /// </summary>
    /// <remarks>
    /// The overlay must appear on the monitor where the text is going, which is the one
    /// holding the foreground window — not the primary monitor, and not wherever the app's
    /// own window happens to be.
    /// </remarks>
    (int X, int Y)? ActiveWindowCentre();
}

/// <summary>
/// Rewrites a transcript into the text the speaker meant to type.
/// </summary>
/// <remarks>
/// The optional generative tier: fillers out, spoken corrections applied, punctuation and
/// case fixed, meaning untouched. The raw path never depends on it — a cleaner that fails
/// returns null and the local text is typed instead.
/// </remarks>
public interface ITranscriptCleaner
{
    /// <summary>A short name for the history and the log, e.g. "gemini-2.5-flash".</summary>
    string Name { get; }

    /// <summary>Cleans <paramref name="text"/>, or returns null if it could not.</summary>
    Task<string?> CleanAsync(string text, CancellationToken cancellationToken);
}

/// <summary>
/// Wall-clock time, behind an interface so timing logic is testable.
/// </summary>
/// <remarks>
/// Anything that measures a duration takes one of these. A test that depends on the real
/// clock is a test that fails on a slow CI runner.
/// </remarks>
public interface IClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset Now { get; }
}

/// <inheritdoc cref="IClock"/>
public sealed class SystemClock : IClock
{
    /// <summary>The shared instance.</summary>
    public static SystemClock Instance { get; } = new();

    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;
}
