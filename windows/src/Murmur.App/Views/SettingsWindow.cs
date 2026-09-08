using Murmur.Abstractions;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;
using Murmur.Speech;

namespace Murmur.App.Views;

/// <summary>Settings: the hotkey, the model, behaviour, start-up.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The token source lives for one download and is disposed in that download's finally; a Window has its own lifecycle and OnClosed cancels any download in flight.")]
public sealed class SettingsWindow : UnitWindow
{
    /// <summary>
    /// The keys offered, in recommendation order.
    /// </summary>
    /// <remarks>
    /// Right Alt is included but listed last and carries a warning: on German, Polish, UK,
    /// Nordic and most Latin-American layouts it is AltGr, and binding push-to-talk there
    /// breaks typing <c>@</c>, <c>€</c>, <c>\</c> and <c>|</c>.
    /// </remarks>
    private static readonly (int Key, string Label, string? Warning)[] Keys =
    [
        (0xA3, "RIGHT CTRL", null),
        (0xA1, "RIGHT SHIFT", "Right Shift also fires every time you type a capital letter with your "
                            + "right hand. Taps under half a second are ignored, but Right Ctrl is quieter."),
        (0x14, "CAPS LOCK", null),
        (0x7C, "F13", null),
        (0xA5, "RIGHT ALT", "Right Alt is AltGr on many European layouts — binding it here "
                          + "will interfere with typing @, €, \\ and |."),
    ];

    private readonly Composition _composition;
    private readonly AppSettings _settings;
    private readonly StackPanel _keyRow;
    private readonly TextBlock _keyWarning;

    private readonly Lamp _modelLamp;
    private readonly TextBlock _modelStatus;
    private readonly TextBlock _modelDetail;
    private readonly TransportKey _download;
    private readonly ProgressGauge _gauge;
    private readonly TextBlock _gaugeText;
    // Owned for the life of one download and disposed in its finally; the class is not
    // IDisposable because a Window already has a lifecycle, and OnClosed cancels it.
    private CancellationTokenSource? _downloading;

    /// <summary>Raised after a model download completes, so the panel can reload it.</summary>
    public event EventHandler? ModelChanged;

    /// <summary>Builds the settings window.</summary>
    public SettingsWindow(Composition composition)
    {
        _composition = composition;
        _settings = composition.Settings;

        Title = "Sidders Settings";
        ModelNumber = "SETTINGS";
        IsResizableUnit = false;
        Width = Tokens.Layout.SettingsWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _keyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Tokens.Space.Snug };

        _keyWarning = new TextBlock
        {
            FontFamily = Tokens.Fonts.Grotesque,
            FontSize = Tokens.Fonts.Label,
            Foreground = Tokens.Brushes.MeterAmber,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        foreach (var (key, label, warning) in Keys)
        {
            var button = new TransportKey { Content = label, EngagedColor = Tokens.Colors.Ink };
            button.Click += (_, _) => SelectKey(key, warning);
            _keyRow.Children.Add(button);
        }

        _modelLamp = new Lamp { VerticalAlignment = VerticalAlignment.Center };
        _modelStatus = Body(string.Empty);
        _modelDetail = Note(string.Empty);
        _download = new TransportKey { Content = "DOWNLOAD", EngagedColor = Tokens.Colors.Ink };
        _download.Click += (_, _) => _ = ToggleDownloadAsync();
        _gauge = new ProgressGauge { IsVisible = false };
        _gaugeText = new TextBlock
        {
            FontFamily = Tokens.Fonts.Mono,
            FontSize = Tokens.Fonts.Caption,
            Foreground = Tokens.Brushes.InkSecondary,
            IsVisible = false,
        };

        Content = Frame(AppPaths.ProductName, BuildContent());
        SelectKey(_settings.Data.PushToTalkKey, WarningFor(_settings.Data.PushToTalkKey));
        RefreshModel();
    }

    private static string? WarningFor(int key) => Keys.FirstOrDefault(k => k.Key == key).Warning;

    private StackPanel BuildContent()
    {
        var behaviour = new StackPanel
        {
            Spacing = Tokens.Space.Snug,
            Children =
            {
                Toggle("Type transcripts into the focused app", _settings.Data.InjectText,
                    v => Save(_settings.Data with { InjectText = v })),
                Toggle("Keep a transcript history", _settings.Data.KeepHistory,
                    v => Save(_settings.Data with { KeepHistory = v })),
                Toggle("Drop the full stop after a single sentence (chat messages, fragments)",
                    _settings.Data.DropSingleSentenceFullStop,
                    v => Save(_settings.Data with { DropSingleSentenceFullStop = v })),
            },
        };

        if (_composition.Startup is { } startup)
        {
            behaviour.Children.Add(Toggle("Start Sidders when I sign in to Windows", startup.IsEnabled,
                v => { if (!startup.SetEnabled(v)) Log.Warn("could not change start-up registration"); }));
        }

        return new StackPanel
        {
            Margin = new Thickness(Tokens.Space.Panel),
            Spacing = Tokens.Space.Wide,
            Children =
            {
                Section("PUSH TO TALK", new StackPanel
                {
                    Spacing = Tokens.Space.Snug,
                    Children =
                    {
                        _keyRow,
                        _keyWarning,
                        BuildModeRow(),
                        Note("The key is passed through to the focused app rather than swallowed, so it "
                           + "never gets stuck down. Tap mode avoids the Windows Filter Keys prompt, which "
                           + "appears when Right Shift is held for eight seconds."),
                    },
                }),
                Section("MICROPHONE", BuildMicrophoneSection()),
                Section("MODEL", BuildModelSection()),
                Section("AI CLEAN-UP", BuildAiSection()),
                Section("BEHAVIOUR", behaviour),
            },
        };
    }

    /// <summary>Hold-to-talk versus tap-to-toggle, as two latching keys.</summary>
    private StackPanel BuildModeRow()
    {
        var hold = new TransportKey { Content = "HOLD TO TALK", EngagedColor = Tokens.Colors.Ink };
        var tap = new TransportKey { Content = "TAP TO START · TAP TO STOP", EngagedColor = Tokens.Colors.Ink };

        void Select(bool toggle)
        {
            hold.IsEngaged = !toggle;
            tap.IsEngaged = toggle;
            if (_settings.Data.TapToToggle != toggle) Save(_settings.Data with { TapToToggle = toggle });
        }

        hold.Click += (_, _) => Select(false);
        tap.Click += (_, _) => Select(true);
        Select(_settings.Data.TapToToggle);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Snug,
            Margin = new Thickness(0, Tokens.Space.Tight, 0, 0),
            Children = { hold, tap },
        };
    }

    /// <summary>The generative tier: a toggle, the key, the model, and a live test.</summary>
    private StackPanel BuildAiSection()
    {
        var key = new TextBox
        {
            Text = _settings.Data.GeminiApiKey ?? string.Empty,
            Watermark = "Gemini API key (or set GEMINI_API_KEY)",
            PasswordChar = '•',
            FontFamily = Tokens.Fonts.Mono,
            FontSize = Tokens.Fonts.Body,
            Foreground = Tokens.Brushes.InkOnDeck,
            Background = Tokens.Brushes.Deck,
            BorderBrush = Tokens.Brushes.Seam,
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            CornerRadius = new CornerRadius(Tokens.Radius.Chip),
            Padding = new Thickness(Tokens.Space.Snug),
        };
        key.LostFocus += (_, _) =>
        {
            var value = string.IsNullOrWhiteSpace(key.Text) ? null : key.Text.Trim();
            if (_settings.Data.GeminiApiKey != value) Save(_settings.Data with { GeminiApiKey = value });
        };

        var model = new TextBox
        {
            Text = _settings.Data.GeminiModel ?? GeminiCleaner.DefaultModel,
            Watermark = GeminiCleaner.DefaultModel,
            FontFamily = Tokens.Fonts.Mono,
            FontSize = Tokens.Fonts.Body,
            Foreground = Tokens.Brushes.InkOnDeck,
            Background = Tokens.Brushes.Deck,
            BorderBrush = Tokens.Brushes.Seam,
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            CornerRadius = new CornerRadius(Tokens.Radius.Chip),
            Padding = new Thickness(Tokens.Space.Snug),
        };
        model.LostFocus += (_, _) =>
        {
            var value = string.IsNullOrWhiteSpace(model.Text) || model.Text.Trim() == GeminiCleaner.DefaultModel ? null : model.Text.Trim();
            if (_settings.Data.GeminiModel != value) Save(_settings.Data with { GeminiModel = value });
        };

        var result = Note(string.Empty);
        var test = new TransportKey { Content = "TEST" };
        test.Click += async (_, _) =>
        {
            const string sample = "um so can you uh send me the the Q2 numbers by friday scratch that by thursday";
            test.IsEnabled = false;
            result.Text = "Sending a sample…";
            try
            {
                using var cleaner = new GeminiCleaner(() => _settings.Data.GeminiApiKey, _settings.Data.GeminiModel);
                var cleaned = await cleaner.CleanAsync(sample, CancellationToken.None).ConfigureAwait(true);
                result.Text = cleaned is null
                    ? $"Failed: {cleaner.LastError ?? "no reply"}"
                    : $"“{sample}”  →  “{cleaned}”";
            }
            finally
            {
                test.IsEnabled = true;
            }
        };

        return new StackPanel
        {
            Spacing = Tokens.Space.Snug,
            Children =
            {
                Toggle("Clean up transcripts with Gemini before typing", _settings.Data.AiCleanup,
                    v => Save(_settings.Data with { AiCleanup = v })),
                Note("Removes fillers and false starts, applies “scratch that”, “new line” and "
                   + "“bullet points”, and fixes punctuation. Your words leave this machine for "
                   + "Google's API; roughly a twentieth of a penny per dictation on Flash. If the API "
                   + "does not answer within eight seconds the raw transcript is typed instead."),
                Panels.Labelled("API KEY", key),
                Panels.Labelled("MODEL", model),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Tokens.Space.Snug, Children = { test } },
                result,
            },
        };
    }

    /// <summary>
    /// One row per microphone, plus "system default". Selecting saves immediately and takes
    /// effect on the next recording.
    /// </summary>
    private StackPanel BuildMicrophoneSection()
    {
        var list = new StackPanel { Spacing = Tokens.Space.Tight };
        var devices = _composition.Devices?.ListCaptureDevices() ?? [];

        if (_composition.Devices is null)
        {
            list.Children.Add(Note("Microphone selection is not available on this platform."));
            return list;
        }

        var rows = new List<(string? Id, Border Row, Lamp Lamp)>();

        void Select(string? id)
        {
            foreach (var (rowId, _, lamp) in rows) lamp.IsLit = rowId == id;
            if (_settings.Data.MicrophoneDeviceId != id) Save(_settings.Data with { MicrophoneDeviceId = id });
        }

        Border Row(string? id, string name, bool isDefault)
        {
            var lamp = new Lamp
            {
                LampColor = Tokens.Colors.MeterGreen,
                Width = Tokens.Material.LampSizeSmall,
                Height = Tokens.Material.LampSizeSmall,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var label = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Tokens.Space.Snug,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = name,
                        FontFamily = Tokens.Fonts.Grotesque,
                        FontSize = Tokens.Fonts.Body,
                        Foreground = Tokens.Brushes.InkOnDeck,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            };

            if (isDefault)
            {
                label.Children.Add(new Silkscreen
                {
                    Text = "WINDOWS DEFAULT",
                    Foreground = Tokens.Brushes.InkOnDeckDim,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            var row = new Border
            {
                Background = Tokens.Brushes.Deck,
                CornerRadius = new CornerRadius(Tokens.Radius.Chip),
                BorderBrush = Tokens.Brushes.Seam,
                BorderThickness = new Thickness(Tokens.Border.Hairline),
                Padding = new Thickness(Tokens.Space.Base, Tokens.Space.Snug),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Base,
                    Children = { lamp, label },
                },
            };
            row.PointerPressed += (_, _) => Select(id);
            rows.Add((id, row, lamp));
            return row;
        }

        list.Children.Add(Row(null, "Follow the Windows default", isDefault: false));
        foreach (var device in devices) list.Children.Add(Row(device.Id, device.Name, device.IsDefault));

        if (devices.Count == 0)
        {
            list.Children.Add(Note("No active microphone was found. Plug one in, or check Settings → "
                                 + "Privacy & security → Microphone."));
        }

        var chosen = _settings.Data.MicrophoneDeviceId;
        if (chosen is not null && devices.All(d => d.Id != chosen))
        {
            list.Children.Add(Note("The microphone you chose is not connected right now; the Windows "
                                 + "default will be used until it is."));
        }

        foreach (var (rowId, _, lamp) in rows) lamp.IsLit = rowId == chosen;

        list.Children.Add(Note("Applies to the next recording. Pick the microphone you actually "
                             + "dictate into — a laptop's built-in array picks up the room."));
        return list;
    }

    private StackPanel BuildModelSection()
    {
        var openFolder = new TransportKey { Content = "OPEN FOLDER" };
        openFolder.Click += (_, _) => OpenFolder(ModelDownloader.DefaultTarget);

        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Snug,
            Children = { _modelLamp, _modelStatus },
        };

        var keys = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Snug,
            Children = { _download, openFolder },
        };

        return new StackPanel
        {
            Spacing = Tokens.Space.Snug,
            Children = { status, _modelDetail, keys, _gauge, _gaugeText },
        };
    }

    private void RefreshModel()
    {
        var located = ParakeetTranscriber.Locate();
        var loaded = _composition.Transcriber?.IsReady == true;

        _modelLamp.IsLit = true;
        _modelLamp.LampColor = located is null ? Tokens.Colors.MeterAmber : loaded ? Tokens.Colors.MeterGreen : Tokens.Colors.MeterAmber;
        _modelStatus.Text = located is null
            ? "Parakeet not installed"
            : loaded ? "Parakeet ready" : "Parakeet found — loading";

        var size = (ModelDownloader.ApproximateBytes / 1_000_000d).ToString("0", CultureInfo.CurrentCulture);
        _modelDetail.Text = located is not null
            // Showing the resolved path matters: "model not found" is unactionable without
            // knowing which directory was actually checked.
            ? $"Loaded from {located}"
            : "Windows has no built-in speech engine, so Murmur cannot transcribe until the "
            + $"Parakeet model is downloaded (~{size} MB, once, from Hugging Face). It runs "
            + "entirely on this machine afterwards.";

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
        _download.Content = "CANCEL";
        _gauge.IsVisible = true;
        _gaugeText.IsVisible = true;
        _gauge.Fraction = 0;

        var progress = new Progress<DownloadProgress>(p => Dispatcher.UIThread.Post(() =>
        {
            _gauge.Fraction = p.Fraction;
            var received = (p.BytesReceived / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture);
            var total = p.TotalBytes is { } t ? (t / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture) : "?";
            _gaugeText.Text = $"{p.File.ToUpperInvariant()}  {received} / {total} MB   ({p.FileIndex + 1} OF {p.FileCount})";
        }));

        try
        {
            using var downloader = new ModelDownloader();
            Log.Info("model download started");
            await downloader.DownloadAsync(ModelDownloader.DefaultTarget, progress, _downloading.Token).ConfigureAwait(true);
            Log.Info("model download complete");
            _gaugeText.Text = "DOWNLOAD COMPLETE";
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            _gaugeText.Text = "CANCELLED";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            Log.Error("model download failed", e);
            _gaugeText.Text = $"FAILED: {e.Message}";
        }
        finally
        {
            _downloading.Dispose();
            _downloading = null;
            _download.Content = "DOWNLOAD";
            _gauge.IsVisible = false;
            RefreshModel();
        }
    }

    private void SelectKey(int key, string? warning)
    {
        for (var i = 0; i < Keys.Length; i++)
        {
            ((TransportKey)_keyRow.Children[i]).IsEngaged = Keys[i].Key == key;
        }

        _keyWarning.Text = warning ?? string.Empty;
        _keyWarning.IsVisible = warning is not null;

        if (_settings.Data.PushToTalkKey != key)
        {
            Save(_settings.Data with { PushToTalkKey = key });
            _keyWarning.Text = (warning is null ? string.Empty : warning + " ")
                             + "The new key takes effect the next time Murmur starts.";
            _keyWarning.IsVisible = true;
        }
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

    private static BrushedPanel Section(string label, Control content) => new()
    {
        Child = new StackPanel
        {
            Margin = new Thickness(Tokens.Space.Roomy),
            Spacing = Tokens.Space.Base,
            Children = { new Silkscreen { Text = label, IsLarge = true }, content },
        },
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontFamily = Tokens.Fonts.Grotesque,
        FontSize = Tokens.Fonts.Body,
        Foreground = Tokens.Brushes.Ink,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontFamily = Tokens.Fonts.Grotesque,
        FontSize = Tokens.Fonts.Label,
        Foreground = Tokens.Brushes.InkSecondary,
        TextWrapping = TextWrapping.Wrap,
    };

    private static CheckBox Toggle(string label, bool value, Action<bool> onChange)
    {
        var box = new CheckBox
        {
            IsChecked = value,
            Content = new TextBlock
            {
                Text = label,
                FontFamily = Tokens.Fonts.Grotesque,
                FontSize = Tokens.Fonts.Body,
                Foreground = Tokens.Brushes.Ink,
            },
        };

        box.IsCheckedChanged += (_, _) => onChange(box.IsChecked ?? false);
        return box;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _downloading?.Cancel();
        base.OnClosed(e);
    }
}

/// <summary>
/// A flat gauge on the deck for the model download. Not for recording — the record
/// indicator is the VU meter — but a download has an end, and a bar is the honest shape.
/// </summary>
public sealed class ProgressGauge : Control
{
    /// <summary>How far along, 0…1.</summary>
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ProgressGauge, double>(nameof(Fraction));

    /// <inheritdoc cref="FractionProperty"/>
    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    static ProgressGauge() => AffectsRender<ProgressGauge>(FractionProperty);

    /// <summary>Creates a gauge at the token height.</summary>
    public ProgressGauge() => Height = Tokens.Layout.GaugeHeight;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(Tokens.Brushes.Deck, new Pen(Tokens.Brushes.Seam, Tokens.Border.Hairline), new RoundedRect(bounds, Tokens.Radius.Chip));

        var fill = bounds.Deflate(Tokens.Space.Hair).WithWidth(Math.Max(0, (bounds.Width - 2 * Tokens.Space.Hair) * Math.Clamp(Fraction, 0, 1)));
        context.DrawRectangle(Tokens.Brushes.InkOnDeck, null, new RoundedRect(fill, Tokens.Radius.Chip));
    }
}
