using Murmur.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Murmur.Core;

/// <summary>User preferences.</summary>
/// <remarks>
/// Plain setters, not <c>init</c>: with source-generated JSON an <c>init</c> property that is
/// absent from an older settings file came back as <c>false</c>, not its declared default —
/// which silently switched the app off for anyone upgrading. <c>with</c> expressions work
/// either way.
/// </remarks>
public sealed record SettingsData
{
    /// <summary>
    /// Virtual-key code of the push-to-talk key. Defaults to Right Ctrl (0xA3).
    /// </summary>
    /// <remarks>
    /// <b>Not Right Alt.</b> On German, Polish, UK, Nordic and most Latin-American layouts
    /// Right Alt is AltGr — it is how those users type <c>@</c>, <c>€</c>, <c>\</c> and
    /// <c>|</c>. Right Ctrl produces no character on any layout.
    /// </remarks>
    public int PushToTalkKey { get; set; } = 0xA3;

    /// <summary>
    /// Modifiers that must be held with <see cref="PushToTalkKey"/>, as
    /// <see cref="Murmur.Abstractions.HotkeyModifiers"/> flags. Zero for a bare key.
    /// </summary>
    public int PushToTalkModifiers { get; set; }

    /// <summary>Where the speech model lives, or null to search the default locations.</summary>
    public string? ModelDirectory { get; set; }

    /// <summary>
    /// The microphone to record from, as an OS device id, or null for the system's default
    /// communications device.
    /// </summary>
    /// <remarks>
    /// Read on every recording rather than once at startup, so picking a different
    /// microphone in Settings takes effect on the next key press.
    /// </remarks>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>Whether to type the transcript into the focused app.</summary>
    public bool InjectText { get; set; } = true;

    /// <summary>Whether to keep a transcript history.</summary>
    public bool KeepHistory { get; set; } = true;

    /// <summary>
    /// Whether a lone sentence loses its trailing full stop. See
    /// <see cref="TranscriptPolish.DropTrailingFullStopIfSingleSentence"/>.
    /// </summary>
    public bool DropSingleSentenceFullStop { get; set; } = true;

    /// <summary>
    /// Tap the key once to start and once to stop, instead of holding it.
    /// </summary>
    /// <remarks>
    /// Holding a modifier is also how Windows accessibility shortcuts are armed: eight
    /// seconds on Right Shift opens the Filter Keys prompt, five taps opens Sticky Keys.
    /// Tap-to-toggle sidesteps the first entirely.
    /// </remarks>
    public bool TapToToggle { get; set; }

    /// <summary>Whether transcripts go through the generative clean-up before typing.</summary>
    public bool AiCleanup { get; set; }

    /// <summary>Gemini API key, or null to use the <c>GEMINI_API_KEY</c> environment variable.</summary>
    public string? GeminiApiKey { get; set; }

    /// <summary>Gemini model id, or null for the default.</summary>
    public string? GeminiModel { get; set; }

    /// <summary>Whether the push-to-talk key does anything. Off pauses the app without quitting.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>How the key works. <see cref="TapToToggle"/> from older files maps onto <see cref="ActivationMode.Tap"/>.</summary>
    public ActivationMode Mode { get; set; } = ActivationMode.Automatic;

    /// <summary>What happens to a full stop at the very end of a dictation.</summary>
    public TrailingFullStop FullStops { get; set; } = TrailingFullStop.DropAfterSingleSentence;

    /// <summary>Whether "new line", "full stop", "scratch that" and so on are applied locally.</summary>
    public bool SpokenCommands { get; set; } = true;

    /// <summary>Whether "um", "er" and friends are removed locally.</summary>
    public bool RemoveFillers { get; set; } = true;

    /// <summary>The user's own rules for the AI clean-up, appended to the prompt. Null for none.</summary>
    public string? CustomInstructions { get; set; }

    /// <summary>Whether the first-run walkthrough has been completed or dismissed.</summary>
    public bool HasOnboarded { get; set; }
}

/// <summary>Settings, persisted as JSON.</summary>
public sealed class AppSettings
{
    private readonly string _path;

    /// <summary>Loads settings from <paramref name="path"/>, or defaults if absent.</summary>
    public AppSettings(string path)
    {
        _path = path;
        Data = Load(path);
    }

    /// <summary>The default location.</summary>
    public static string DefaultPath => Path.Combine(AppPaths.Root, "settings.json");

    /// <summary>Current values.</summary>
    public SettingsData Data { get; private set; }

    /// <summary>Raised after a successful save.</summary>
    public event EventHandler? Changed;

    /// <summary>Replaces and persists the settings.</summary>
    public void Update(SettingsData data)
    {
        Data = data;

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(data, SettingsJsonContext.Default.SettingsData));

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static SettingsData Load(string path)
    {
        // Corrupt or unreadable settings must never stop the app launching — defaults are
        // always a working configuration.
        try
        {
            if (!File.Exists(path)) return new SettingsData();

            var data = JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.SettingsData)
                       ?? new SettingsData();

            // Files written before activation modes existed carry only the tap switch.
            if (data.TapToToggle && data.Mode == ActivationMode.Automatic) data.Mode = ActivationMode.Tap;
            if (!data.DropSingleSentenceFullStop && data.FullStops == TrailingFullStop.DropAfterSingleSentence) data.FullStops = TrailingFullStop.Keep;
            return data;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new SettingsData();
        }
    }
}

/// <summary>Source-generated JSON for settings.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsData))]
public sealed partial class SettingsJsonContext : JsonSerializerContext;
