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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The token source lives for one download and is disposed in that download's finally; a Window has its own lifecycle and OnClosed cancels any download in flight.")]
public sealed class SettingsWindow : ShellWindow
{
    /// <summary>Quick picks, in recommendation order. Any other key can be recorded.</summary>
    private static readonly (int Key, string Label)[] Keys =
    [
        (0xA3, "Right Ctrl"),
        (0xA1, "Right Shift"),
        (0x14, "Caps Lock"),
        (0x7C, "F13"),
        (0x91, "Scroll Lock"),
    ];

    private static string? WarningFor(int key) => key switch
    {
        0xA1 => "Right Shift also fires when you type a capital with your right hand. Taps under half a second are ignored, but Right Ctrl is quieter.",
        0xA5 => "Right Alt is AltGr on many European layouts and will interfere with typing @, €, \\ and |.",
        _ when KeyNames.TypesACharacter(key) => $"{KeyNames.Describe(key)} also types a character. The key is passed through, so you will get that character as well as a recording.",
        _ => null,
    };

    private readonly Composition _composition;
    private readonly AppSettings _settings;
    private readonly Border _keyCapture;
    private readonly TextBlock _keyName;
    private bool _capturing;
    private readonly Border _keyWarning;
    private readonly TextBlock _keyWarningText;

    private readonly StatusDot _modelDot;
    private readonly TextBlock _modelStatus;
    private readonly TextBlock _modelDetail;
    private readonly SgButton _download;
    private readonly Gauge _gauge;
    private readonly TextBlock _gaugeText;
    private CancellationTokenSource? _downloading;

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

        _keyName = Text.BodyStrong(KeyNames.Describe(_settings.Data.PushToTalkKey, _settings.Data.PushToTalkModifiers));
        _keyCapture = BuildKeyCapture();
        _keyWarningText = Text.Body(string.Empty);
        _keyWarningText.Foreground = Tokens.Brushes.Amber;
        _keyWarning = Card.Notice(_keyWarningText, Tokens.Brushes.AmberLight, new Avalonia.Media.SolidColorBrush(Tokens.Colors.AmberMid, Tokens.Opacity.FocusBorder));
        _keyWarning.IsVisible = false;

        _modelDot = new StatusDot { VerticalAlignment = VerticalAlignment.Center };
        _modelStatus = Text.BodyStrong(string.Empty);
        _modelDetail = Text.Muted(string.Empty);
        _download = new SgButton("Download model", SgButton.Kind.Primary);
        _download.Click += (_, _) => _ = ToggleDownloadAsync();
        _gauge = new Gauge { IsVisible = false };
        _gaugeText = Text.Caption(string.Empty);
        _gaugeText.IsVisible = false;

        var body = Panels.Column(Tokens.Space.Wide,
            Card.Standard(Panels.Section("Push to talk", "Which key, and whether you hold it or tap it.", BuildKeySection())),
            Card.Standard(Panels.Section("Microphone", "Applies to the next recording.", BuildMicrophoneSection())),
            Card.Standard(Panels.Section("Speech model", "Runs on this machine. Nothing is sent anywhere.", BuildModelSection())),
            Card.Standard(Panels.Section("AI clean-up", "Optional. Rewrites the transcript with Gemini before typing.", BuildAiSection())),
            Card.Standard(Panels.Section("Behaviour", null, BuildBehaviourSection())));
        body.Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide);

        Content = Frame("Settings", new ScrollViewer { Content = body });
        SelectKey(_settings.Data.PushToTalkKey, _settings.Data.PushToTalkModifiers, initial: true);
        RefreshModel();
    }

    private StackPanel BuildKeySection()
    {
        var mode = new Segmented(["Hold to talk", "Tap to start, tap to stop"], _settings.Data.TapToToggle ? 1 : 0);
        mode.Selected += (_, i) => { if (_settings.Data.TapToToggle != (i == 1)) Save(_settings.Data with { TapToToggle = i == 1 }); };

        var picks = Panels.Row(Tokens.Space.Snug);
        foreach (var (key, label) in Keys)
        {
            var pick = new SgButton(label, SgButton.Kind.Ghost, compact: true);
            pick.Click += (_, _) => SelectKey(key, 0);
            picks.Children.Add(pick);
        }

        return Panels.Column(Tokens.Space.Base,
            _keyCapture,
            picks,
            _keyWarning,
            mode,
            Text.Muted("The key is passed through, never swallowed, so it can't get stuck down. Tap mode avoids the Windows Filter Keys prompt that appears when Right Shift is held for eight seconds."));
    }

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

    private StackPanel BuildModelSection()
    {
        var openFolder = new SgButton("Open folder", SgButton.Kind.Ghost);
        openFolder.Click += (_, _) => OpenFolder(ModelDownloader.DefaultTarget);

        return Panels.Column(Tokens.Space.Base,
            Panels.Row(Tokens.Space.Snug, _modelDot, _modelStatus),
            _modelDetail,
            Panels.Row(Tokens.Space.Snug, _download, openFolder),
            _gauge,
            _gaugeText);
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

        var result = Text.Muted(string.Empty);
        var test = new SgButton("Test with a sample", SgButton.Kind.Ghost);
        test.Click += async (_, _) =>
        {
            const string sample = "um so can you uh send me the the Q2 numbers by friday scratch that by thursday";
            test.IsEnabled = false;
            result.Text = "Sending…";
            try
            {
                using var cleaner = new GeminiCleaner(() => _settings.Data.GeminiApiKey, _settings.Data.GeminiModel);
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
                "Removes fillers and false starts, applies “scratch that” and “new line”, fixes punctuation. Your words go to Google's API — about a twentieth of a penny per dictation on Flash. If it doesn't answer in eight seconds the raw transcript is typed.",
                _settings.Data.AiCleanup, v => Save(_settings.Data with { AiCleanup = v })),
            Panels.Labelled("API key", key),
            Panels.Labelled("Model", model),
            test,
            result);
    }

    private StackPanel BuildBehaviourSection()
    {
        var column = Panels.Column(Tokens.Space.Roomy,
            Panels.SwitchRow("Type into the focused app", "Off keeps the history only.", _settings.Data.InjectText, v => Save(_settings.Data with { InjectText = v })),
            Panels.SwitchRow("Keep a history", null, _settings.Data.KeepHistory, v => Save(_settings.Data with { KeepHistory = v })),
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

    private void RefreshModel()
    {
        var located = ParakeetTranscriber.Locate();
        var loaded = _composition.Transcriber?.IsReady == true;

        _modelDot.Fill = located is null ? Tokens.Brushes.AmberMid : loaded ? Tokens.Brushes.Brand : Tokens.Brushes.BrandMid;
        _modelStatus.Text = located is null ? "Parakeet not installed" : loaded ? "Parakeet ready" : "Parakeet found, loading";

        var size = (ModelDownloader.ApproximateBytes / 1_000_000d).ToString("0", CultureInfo.CurrentCulture);
        _modelDetail.Text = located is not null
            ? $"Loaded from {located}"
            : $"Windows has no built-in speech engine, so {AppPaths.ProductName} can't transcribe until the Parakeet model is downloaded — about {size} MB, once, from Hugging Face. It runs entirely on this machine afterwards.";

        _download.IsVisible = located is null || _downloading is not null;
    }

    private async Task ToggleDownloadAsync()
    {
        if (_downloading is not null)
        {
            await _downloading.CancelAsync().ConfigureAwait(true);
            return;
        }

        _downloading = new CancellationTokenSource();
        _download.Content = "Cancel";
        _gauge.IsVisible = true;
        _gaugeText.IsVisible = true;
        _gauge.Fraction = 0;

        var progress = new Progress<DownloadProgress>(p => Dispatcher.UIThread.Post(() =>
        {
            _gauge.Fraction = p.Fraction;
            var received = (p.BytesReceived / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture);
            var total = p.TotalBytes is { } t ? (t / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture) : "?";
            _gaugeText.Text = $"{p.File}  {received} / {total} MB  ({p.FileIndex + 1} of {p.FileCount})";
        }));

        try
        {
            using var downloader = new ModelDownloader();
            Log.Info("model download started");
            await downloader.DownloadAsync(ModelDownloader.DefaultTarget, progress, _downloading.Token).ConfigureAwait(true);
            Log.Info("model download complete");
            _gaugeText.Text = "Download complete";
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            _gaugeText.Text = "Cancelled";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            Log.Error("model download failed", e);
            _gaugeText.Text = $"Failed: {e.Message}";
        }
        finally
        {
            _downloading.Dispose();
            _downloading = null;
            _download.Content = "Download model";
            _gauge.IsVisible = false;
            RefreshModel();
        }
    }

    /// <summary>The recorder: click, press any key, done.</summary>
    private Border BuildKeyCapture()
    {
        var hint = Text.Muted("Click, then press a key or a combination like Ctrl + Shift + Space");
        var box = Card.Subtle(Panels.Split(Panels.Column(Tokens.Space.Hair, _keyName, hint), Pill.Brand("Record a key")), Tokens.Space.Roomy);
        box.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        box.Focusable = true;
        void Finish()
        {
            _capturing = false;
            _composition.Engine?.CancelCapture();
            box.BorderBrush = Tokens.Brushes.PanelBorder;
            hint.Text = "Click, then press a key or a combination like Ctrl + Shift + Space";
        }

        // Recorded by the keyboard hook, not by this window: the hook sees Win, Alt and
        // Ctrl combinations that a window never receives, and it swallows them while
        // recording so the shortcut does not also fire in Windows.
        box.PointerPressed += (_, _) =>
        {
            if (_composition.Engine is null) { hint.Text = "Recording needs the keyboard hook, which is not available here."; return; }
            _capturing = true;
            box.Focus();
            box.BorderBrush = Tokens.Brushes.FocusBorder;
            hint.Text = "Listening… press a key, or hold modifiers and press a key. Escape to cancel";
            _composition.Engine.BeginCapture();
        };

        if (_composition.Engine is { } engine)
        {
            EventHandler<(int VirtualKey, int Modifiers)> onCaptured = (_, chord) => Dispatcher.UIThread.Post(() =>
            {
                if (!_capturing) return;
                Finish();
                SelectKey(chord.VirtualKey, chord.Modifiers);
            });
            engine.Captured += onCaptured;
            Closed += (_, _) => engine.Captured -= onCaptured;
        }

        box.KeyDown += (_, e) =>
        {
            if (_capturing && e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; Finish(); }
        };
        box.LostFocus += (_, _) => { if (_capturing) Finish(); };
        return box;
    }

    private void SelectKey(int key, int modifiers, bool initial = false)
    {
        var text = modifiers == 0 ? WarningFor(key) : null;
        _keyName.Text = KeyNames.Describe(key, modifiers);
        if (!initial && (_settings.Data.PushToTalkKey != key || _settings.Data.PushToTalkModifiers != modifiers))
        {
            Save(_settings.Data with { PushToTalkKey = key, PushToTalkModifiers = modifiers });
        }

        _keyWarningText.Text = text ?? string.Empty;
        _keyWarning.IsVisible = text is not null;
    }

    private void Save(SettingsData data) => _settings.Update(data);

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"could not open {path}: {e.Message}");
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _downloading?.Cancel();
        base.OnClosed(e);
    }
}
