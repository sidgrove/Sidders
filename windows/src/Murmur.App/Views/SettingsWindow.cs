using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;
using Murmur.Speech;

namespace Murmur.App.Views;

/// <summary>Settings: the key, the microphone, the model, AI clean-up, behaviour.</summary>
public sealed class SettingsWindow : ShellWindow
{
    private readonly Composition _composition;
    private readonly AppSettings _settings;
    private readonly ModelPart _model;

    /// <summary>Raised after a model download completes, so the panel can reload it.</summary>
    public event EventHandler? ModelChanged;

    /// <summary>Builds the settings window.</summary>
    public SettingsWindow(Composition composition)
    {
        _composition = composition;
        _settings = composition.Settings;

        Title = "Settings";
        IsSheet = true;
        Width = Tokens.Layout.SettingsWidth;
        SizeToContent = SizeToContent.Height;
        // Never taller than the screen: a dialog that runs off the bottom hides its own
        // footer and cannot be scrolled.
        MaxHeight = (Screens.Primary?.WorkingArea.Height ?? 900) / (Screens.Primary?.Scaling ?? 1) - Tokens.Space.Empty * 2;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _model = new ModelPart(composition);
        _model.ModelChanged += (_, _) => ModelChanged?.Invoke(this, EventArgs.Empty);

        var body = Panels.Column(Tokens.Space.Wide,
            Card.Standard(Panels.Section("Push to talk", "Which key, and how it works.", new KeyPart(composition))),
            Card.Standard(Panels.Section("Microphone", "Applies to the next recording.", BuildMicrophoneSection())),
            Card.Standard(Panels.Section("Speech model", "Runs on this machine. Nothing is sent anywhere.", _model)),
            Card.Standard(Panels.Section("Writing", "Rules applied on this machine, before anything else.", BuildWritingSection())),
            Card.Standard(Panels.Section("AI clean-up", "Optional. Tidies the transcript with Gemini before typing.", BuildAiSection())),
            Card.Standard(Panels.Section("Behaviour", null, BuildBehaviourSection())));
        body.Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide);

        Content = Frame("Settings", new ScrollViewer { Content = body });
    }

    private StackPanel BuildWritingSection()
    {
        var fullStops = new Segmented(["Drop after one sentence", "Never end with one", "Keep"], (int)FullStopIndex(_settings.Data.FullStops));
        fullStops.Selected += (_, i) =>
        {
            var chosen = i switch { 1 => TrailingFullStop.Never, 2 => TrailingFullStop.Keep, _ => TrailingFullStop.DropAfterSingleSentence };
            if (_settings.Data.FullStops != chosen) Save(_settings.Data with { FullStops = chosen });
        };

        return Panels.Column(Tokens.Space.Roomy,
            Panels.Column(Tokens.Space.Snug,
                Text.Body("Full stop at the very end"),
                fullStops,
                Text.Muted("Chat messages and fragments read better without one. Questions and exclamation marks always stay.")),
            Panels.SwitchRow("Spoken commands", "“New line”, “new paragraph”, “full stop”, “comma”, “question mark”, “scratch that” and so on become what they say.", _settings.Data.SpokenCommands, v => Save(_settings.Data with { SpokenCommands = v })),
            Panels.SwitchRow("Remove ums and ers", "Standalone hesitations are dropped before anything else sees the text.", _settings.Data.RemoveFillers, v => Save(_settings.Data with { RemoveFillers = v })));
    }

    private static int FullStopIndex(TrailingFullStop rule) => rule switch { TrailingFullStop.Never => 1, TrailingFullStop.Keep => 2, _ => 0 };
    private StackPanel BuildMicrophoneSection()
    {
        var list = Panels.Column(Tokens.Space.Snug);
        var devices = _composition.Devices?.ListCaptureDevices() ?? [];

        if (_composition.Devices is null)
        {
            list.Children.Add(Text.Muted("Microphone selection is not available on this platform."));
            return list;
        }

        var rows = new List<(string? Id, StatusDot Dot, Border Row)>();
        var chosen = _settings.Data.MicrophoneDeviceId;

        void Select(string? id)
        {
            foreach (var (rowId, dot, row) in rows)
            {
                var active = rowId == id;
                dot.Fill = active ? Tokens.Brushes.Brand : Tokens.Brushes.Line;
                row.Background = active ? Tokens.Brushes.BrandLight : Tokens.Brushes.Surface;
            }
            if (_settings.Data.MicrophoneDeviceId != id) Save(_settings.Data with { MicrophoneDeviceId = id });
        }

        Border Row(string? id, string name, bool isDefault)
        {
            var dot = new StatusDot { VerticalAlignment = VerticalAlignment.Center };
            var label = Panels.Row(Tokens.Space.Snug, Text.Body(name));
            if (isDefault) label.Children.Add(Pill.Neutral("Windows default"));

            var row = Card.Subtle(Panels.Row(Tokens.Space.Base, dot, label), Tokens.Space.Base);
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.PointerPressed += (_, _) => Select(id);
            rows.Add((id, dot, row));
            return row;
        }

        list.Children.Add(Row(null, "Follow the Windows default", isDefault: false));
        foreach (var device in devices) list.Children.Add(Row(device.Id, device.Name, device.IsDefault));

        if (devices.Count == 0)
        {
            list.Children.Add(Text.Muted("No active microphone found. Plug one in, or check Settings → Privacy & security → Microphone."));
        }

        if (chosen is not null && devices.All(d => d.Id != chosen))
        {
            list.Children.Add(Text.Muted("The microphone you chose isn't connected right now; the Windows default is used until it is."));
        }

        foreach (var (rowId, dot, row) in rows)
        {
            var active = rowId == chosen;
            dot.Fill = active ? Tokens.Brushes.Brand : Tokens.Brushes.Line;
            row.Background = active ? Tokens.Brushes.BrandLight : Tokens.Brushes.Surface;
        }

        return list;
    }

    private StackPanel BuildAiSection()
    {
        var key = Field.Text("Gemini API key, or leave blank to use GEMINI_API_KEY", _settings.Data.GeminiApiKey, secret: true);
        key.LostFocus += (_, _) =>
        {
            var value = string.IsNullOrWhiteSpace(key.Text) ? null : key.Text.Trim();
            if (_settings.Data.GeminiApiKey != value) Save(_settings.Data with { GeminiApiKey = value });
        };

        var model = Field.Text(GeminiCleaner.DefaultModel, _settings.Data.GeminiModel ?? GeminiCleaner.DefaultModel);
        model.LostFocus += (_, _) =>
        {
            var value = string.IsNullOrWhiteSpace(model.Text) || model.Text.Trim() == GeminiCleaner.DefaultModel ? null : model.Text.Trim();
            if (_settings.Data.GeminiModel != value) Save(_settings.Data with { GeminiModel = value });
        };

        var custom = Field.Multiline("Your own rules, e.g. “Never use exclamation marks”, “Write dates as 9 Sept”, “Sign off emails with Dave”", _settings.Data.CustomInstructions);
        custom.LostFocus += (_, _) =>
        {
            var value = string.IsNullOrWhiteSpace(custom.Text) ? null : custom.Text.Trim();
            if (_settings.Data.CustomInstructions != value) Save(_settings.Data with { CustomInstructions = value });
        };

        var result = Text.Muted(string.Empty);
        var test = new SgButton("Test with a sample", SgButton.Kind.Ghost);
        test.Click += async (_, _) =>
        {
            const string sample = "um so can you uh send me the the Q2 numbers by friday scratch that by thursday";
            test.IsEnabled = false;
            result.Text = "Sending…";
            try
            {
                using var cleaner = new GeminiCleaner(() => _settings.Data.GeminiApiKey, _settings.Data.GeminiModel, customInstructions: () => _settings.Data.CustomInstructions);
                var cleaned = await cleaner.CleanAsync(sample, CancellationToken.None).ConfigureAwait(true);
                result.Text = cleaned is null ? $"Failed: {cleaner.LastError ?? "no reply"}" : $"“{sample}”\n→ “{cleaned}”";
            }
            finally
            {
                test.IsEnabled = true;
            }
        };

        return Panels.Column(Tokens.Space.Base,
            Panels.SwitchRow("Clean up with Gemini before typing",
                "Tidies punctuation, applies self-corrections and writes numbers as figures, without changing your words. Your text goes to Google's API — about a twentieth of a penny per dictation on Flash. If it doesn't answer in eight seconds, or rewrites rather than tidies, the local text is typed instead.",
                _settings.Data.AiCleanup, v => Save(_settings.Data with { AiCleanup = v })),
            Panels.Labelled("API key", key),
            Panels.Labelled("Model", model),
            Panels.Labelled("Your own instructions", custom),
            test,
            result);
    }

    private StackPanel BuildBehaviourSection()
    {
        var column = Panels.Column(Tokens.Space.Roomy,
            Panels.SwitchRow("Type into the focused app", "Off keeps the history only.", _settings.Data.InjectText, v => Save(_settings.Data with { InjectText = v })),
            Panels.SwitchRow("Keep a history", null, _settings.Data.KeepHistory, v => Save(_settings.Data with { KeepHistory = v })),
            Panels.SwitchRow("Turn other audio down while I talk", "Music, video and calls drop to a whisper while the key is held and come straight back. Your volume slider is never touched.", _settings.Data.DuckOtherAudio, v => Save(_settings.Data with { DuckOtherAudio = v })),
            Panels.SwitchRow("Drop the full stop after a single sentence", "For chat messages and fragments. Questions and longer dictations keep their punctuation.", _settings.Data.DropSingleSentenceFullStop, v => Save(_settings.Data with { DropSingleSentenceFullStop = v })));

        if (_composition.Startup is { } startup)
        {
            column.Children.Add(Panels.SwitchRow("Start when I sign in to Windows", "Starts in the tray.", startup.IsEnabled,
                v => { if (!startup.SetEnabled(v)) Log.Warn("could not change start-up registration"); }));
        }

        var quit = new SgButton($"Quit {AppPaths.ProductName}", SgButton.Kind.Danger);
        quit.Click += (_, _) => App.Quit();
        column.Children.Add(Panels.Split(Text.Muted("Closing the window keeps it running in the tray. This stops it completely."), quit));

        return column;
    }

    private void Save(SettingsData data) => _settings.Update(data);
}
