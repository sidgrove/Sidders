using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;

namespace Murmur.App.Views;

/// <summary>
/// The main window — the front panel of the unit.
/// </summary>
/// <remarks>
/// <para>
/// Laid out the way a deck is: caption strip and menu row on the chassis, the transport,
/// meter, counter and indicator cluster on a brushed panel, then a recessed well below
/// holding whichever section is selected.
/// </para>
/// <para>
/// Built in code rather than XAML, deliberately. Every value comes from <see cref="Tokens"/>,
/// and XAML makes it far too easy to type a literal <c>Margin="12,8"</c> that silently escapes
/// the design system. In C# a stray number is visible in review.
/// </para>
/// </remarks>
public sealed class MainWindow : UnitWindow
{
    private const string ModelGuideUrl = "https://github.com/per-simmons/murmur-youtube/blob/main/docs/PARAKEET-WINDOWS.md";

    private readonly Composition? _composition;
    private readonly TransportKey _recordKey;
    private readonly Lamp _recordLamp;
    private readonly VuMeter _meter;
    private readonly SegmentReadout _counter;
    private readonly LevelTrace _trace;
    private readonly Silkscreen _mode;
    private readonly ContentControl _sectionHost;
    private readonly TransportKey _transcriptionsKey;
    private readonly TransportKey _dictionaryKey;
    private readonly Lamp _hookLamp;
    private readonly Lamp _micLamp;
    private readonly Lamp _modelLamp;
    private readonly Border _faultStrip;
    private readonly TextBlock _faultText;
    private readonly DispatcherTimer _poll;
    private readonly OverlayWindow? _overlay;

    private TranscriptionsView? _transcriptionsView;
    private DictionaryView? _dictionaryView;
    private DateTimeOffset? _startedAt;
    private bool _modelReady;

    /// <summary>Builds a window with no engine behind it. Used by headless tests.</summary>
    public MainWindow() : this(null) { }

    /// <summary>Builds the window over <paramref name="composition"/>.</summary>
    public MainWindow(Composition? composition)
    {
        _composition = composition;

        Title = "Murmur";
        ModelNumber = "PD-26  ·  PORTABLE DICTATION UNIT";
        MinWidth = Tokens.Layout.MainMinWidth;
        MinHeight = Tokens.Layout.MainMinHeight;
        Width = Tokens.Layout.MainWidth;
        Height = Tokens.Layout.MainHeight;

        _recordKey = new TransportKey { Content = "RECORD", MinWidth = Tokens.Material.RecordKeyMinWidth };
        _recordKey.Click += (_, _) => ToggleRecording();

        _recordLamp = new Lamp { LampColor = Tokens.Colors.Record };
        _meter = new VuMeter();
        _counter = new SegmentReadout { Text = "00:00" };
        _trace = new LevelTrace();
        _mode = new Silkscreen { Text = "STOP", Foreground = Tokens.Brushes.InkOnDeckDim };

        _hookLamp = new Lamp { LampColor = Tokens.Colors.MeterGreen, Width = Tokens.Material.LampSizeSmall, Height = Tokens.Material.LampSizeSmall };
        _micLamp = new Lamp { LampColor = Tokens.Colors.MeterGreen, Width = Tokens.Material.LampSizeSmall, Height = Tokens.Material.LampSizeSmall };
        _modelLamp = new Lamp { LampColor = Tokens.Colors.MeterAmber, Width = Tokens.Material.LampSizeSmall, Height = Tokens.Material.LampSizeSmall };

        _transcriptionsKey = new TransportKey { Content = "TRANSCRIPTIONS", IsEngaged = true, EngagedColor = Tokens.Colors.Ink };
        _dictionaryKey = new TransportKey { Content = "DICTIONARY", EngagedColor = Tokens.Colors.Ink };
        _transcriptionsKey.Click += (_, _) => ShowSection(transcriptions: true);
        _dictionaryKey.Click += (_, _) => ShowSection(transcriptions: false);

        _faultText = new TextBlock
        {
            FontFamily = Tokens.Fonts.Grotesque,
            FontSize = Tokens.Fonts.Label,
            Foreground = Tokens.Brushes.InkOnDeck,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _faultStrip = BuildFaultStrip();

        _sectionHost = new ContentControl();

        // The meter and counter are polled rather than pushed. The engine raises Changed on
        // a background thread at buffer rate, and marshalling every one of those to the UI
        // thread would be far more traffic than a display refresh needs.
        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = Tokens.Motion.PanelPoll };
        _poll.Tick += (_, _) => SyncFromEngine();
        _poll.Start();

        if (_composition is not null)
        {
            _overlay = new OverlayWindow(PlatformFactory.CreateWindowTweaks());
        }

        Content = Frame("Murmur", BuildBody());
        ShowSection(transcriptions: true);

        if (_composition?.Engine is { } engine)
        {
            engine.Faulted += (_, message) => Dispatcher.UIThread.Post(() => ShowFault(message));
            engine.Start();
            _modelLamp.IsLit = Composition.IsModelInstalled;
            _ = PreloadAsync(engine);
        }
        else
        {
            _modelLamp.IsLit = false;
        }
    }

    private async Task PreloadAsync(DictationEngine engine)
    {
        _modelReady = await engine.PreloadAsync(CancellationToken.None).ConfigureAwait(true);
        _modelLamp.LampColor = _modelReady ? Tokens.Colors.MeterGreen : Tokens.Colors.MeterAmber;
        _modelLamp.IsLit = true;
    }

    /// <summary>Re-checks the model after a download from Settings.</summary>
    public void ModelChanged()
    {
        if (_composition?.Engine is { } engine) _ = PreloadAsync(engine);
    }

    private DockPanel BuildBody()
    {
        var root = new DockPanel();

        root.Children.Add(Panels.Docked(BuildMenuRow(), Dock.Top));

        var panelArea = new DockPanel { Margin = new Thickness(Tokens.Space.Roomy, Tokens.Space.Base, Tokens.Space.Roomy, Tokens.Space.Roomy) };
        panelArea.Children.Add(Panels.Docked(BuildTransportPanel(), Dock.Top));
        panelArea.Children.Add(Panels.Docked(BuildSectionRow(), Dock.Top));
        panelArea.Children.Add(Panels.Docked(_faultStrip, Dock.Top));
        panelArea.Children.Add(BuildWell(_sectionHost));

        root.Children.Add(panelArea);
        return root;
    }

    private Border BuildMenuRow()
    {
        var menu = new PanelMenu(this,
        [
            new MenuGroup("File",
            [
                new MenuEntry("New dictionary entry", new KeyGesture(Key.N, KeyModifiers.Control), () => { ShowSection(false); _dictionaryView?.AddEntry(); }),
                new MenuEntry("Open dictionary.txt", null, () => OpenPath(_composition?.Dictionary.FilePath)),
                new MenuEntry("Open data folder", null, () => OpenPath(Path.GetDirectoryName(AppSettings.DefaultPath))),
                MenuEntry.Separator,
                new MenuEntry("Settings", new KeyGesture(Key.OemComma, KeyModifiers.Control), ShowSettings),
                MenuEntry.Separator,
                new MenuEntry("Hide to tray", new KeyGesture(Key.W, KeyModifiers.Control), Hide),
                new MenuEntry("Quit Murmur", new KeyGesture(Key.Q, KeyModifiers.Control), App.Quit),
            ]),
            new MenuGroup("Edit",
            [
                new MenuEntry("Copy last transcription", new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Shift), CopyLast),
                new MenuEntry("Find", new KeyGesture(Key.F, KeyModifiers.Control), FocusSearch),
                MenuEntry.Separator,
                new MenuEntry("Delete all transcriptions", null, () => _composition?.Transcripts.Clear()),
            ]),
            new MenuGroup("View",
            [
                new MenuEntry("Transcriptions", new KeyGesture(Key.D1, KeyModifiers.Control), () => ShowSection(true)),
                new MenuEntry("Dictionary", new KeyGesture(Key.D2, KeyModifiers.Control), () => ShowSection(false)),
            ]),
            new MenuGroup("Transport",
            [
                new MenuEntry("Record / Stop", new KeyGesture(Key.R, KeyModifiers.Control), ToggleRecording),
                MenuEntry.Separator,
                new MenuEntry("Reload speech model", null, ModelChanged),
            ]),
            new MenuGroup("Help",
            [
                new MenuEntry("Speech model guide", null, () => OpenPath(ModelGuideUrl)),
                new MenuEntry("Open log", null, () => OpenPath(Log.Path)),
                MenuEntry.Separator,
                new MenuEntry("About Murmur", new KeyGesture(Key.F1), ShowAbout),
            ]),
        ]);

        return new Border
        {
            Background = Tokens.Brushes.Chassis,
            BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
            BorderThickness = new Thickness(0, 0, 0, Tokens.Border.Seam),
            Padding = new Thickness(Tokens.Space.Snug, 0),
            Child = menu,
        };
    }

    /// <summary>Record/stop, the record lamp, the level meter, the counter and the indicator cluster.</summary>
    private BrushedPanel BuildTransportPanel()
    {
        var row = new Grid
        {
            Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Roomy),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"),
        };

        var transport = Panels.Labelled("TRANSPORT", new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Snug,
            Children =
            {
                _recordKey,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Tight,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { _recordLamp, new Silkscreen { Text = "REC" } },
                },
            },
        });
        transport.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(transport, 0);

        var level = Panels.Labelled("LEVEL", _meter);
        level.Margin = new Thickness(Tokens.Space.Wide, 0, 0, 0);
        Grid.SetColumn(level, 1);

        var counter = Panels.Labelled("COUNTER", Deck(new StackPanel
        {
            Spacing = Tokens.Space.Snug,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Base,
                    Children = { _counter, _mode },
                },
                _trace,
            },
        }));
        counter.Margin = new Thickness(Tokens.Space.Wide, 0, 0, 0);
        Grid.SetColumn(counter, 2);

        var cluster = new StackPanel
        {
            Spacing = Tokens.Space.Base,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Base,
                    Children =
                    {
                        Indicator("POWER", new Lamp { LampColor = Tokens.Colors.MeterGreen, IsLit = true, Width = Tokens.Material.LampSizeSmall, Height = Tokens.Material.LampSizeSmall }),
                        Indicator("HOOK", _hookLamp),
                        Indicator("MIC", _micLamp),
                        Indicator("MODEL", _modelLamp),
                    },
                },
                new Vents { Count = 12, HorizontalAlignment = HorizontalAlignment.Right },
            },
        };
        Grid.SetColumn(cluster, 4);

        row.Children.Add(transport);
        row.Children.Add(level);
        row.Children.Add(counter);
        row.Children.Add(cluster);

        return new BrushedPanel
        {
            HasScrews = true,
            Child = row,
            Margin = new Thickness(0, 0, 0, Tokens.Space.Base),
        };
    }

    private static StackPanel Indicator(string label, Lamp lamp) => new()
    {
        Spacing = Tokens.Space.Tight,
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            new Border { Child = lamp, HorizontalAlignment = HorizontalAlignment.Center },
            new Silkscreen { Text = label, HorizontalAlignment = HorizontalAlignment.Center },
        },
    };

    private DockPanel BuildSectionRow()
    {
        var settings = new TransportKey { Content = "SETTINGS" };
        settings.Click += (_, _) => ShowSettings();

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Snug,
            Children = { settings },
        };
        DockPanel.SetDock(right, Dock.Right);

        return new DockPanel
        {
            Margin = new Thickness(0, 0, 0, Tokens.Space.Base),
            Children =
            {
                right,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Snug,
                    Children = { _transcriptionsKey, _dictionaryKey },
                },
            },
        };
    }

    /// <summary>A strip on the deck that carries the most recent fault until dismissed.</summary>
    private Border BuildFaultStrip()
    {
        var dismiss = Panels.DeckButton("DISMISS");
        dismiss.Click += (_, _) => _faultStrip.IsVisible = false;
        DockPanel.SetDock(dismiss, Dock.Right);

        var lamp = new Lamp
        {
            IsLit = true,
            LampColor = Tokens.Colors.MeterAmber,
            Width = Tokens.Material.LampSizeSmall,
            Height = Tokens.Material.LampSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Tokens.Space.Base, 0),
        };
        DockPanel.SetDock(lamp, Dock.Left);

        return new Border
        {
            IsVisible = false,
            Background = Tokens.Brushes.Deck,
            CornerRadius = new CornerRadius(Tokens.Radius.Control),
            BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            Padding = new Thickness(Tokens.Space.Base, Tokens.Space.Snug),
            Margin = new Thickness(0, 0, 0, Tokens.Space.Base),
            Child = new DockPanel { Children = { dismiss, lamp, _faultText } },
        };
    }

    /// <summary>A recessed well cut into the panel — content sits inside it.</summary>
    private static Border BuildWell(Control content) => new()
    {
        Background = Tokens.Brushes.Well,
        CornerRadius = new CornerRadius(Tokens.Radius.Panel),
        BorderBrush = new SolidColorBrush(Tokens.Colors.Seam, Tokens.Opacity.Dim),
        BorderThickness = new Thickness(Tokens.Border.Hairline),
        Padding = new Thickness(Tokens.Space.Hair),
        Child = content,
    };

    /// <summary>The dark readout window of a tape deck.</summary>
    private static Border Deck(Control content) => new()
    {
        Background = Tokens.Brushes.Deck,
        CornerRadius = new CornerRadius(Tokens.Radius.Control),
        BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
        BorderThickness = new Thickness(Tokens.Border.Hairline),
        Padding = new Thickness(Tokens.Space.Base, Tokens.Space.Snug),
        Child = content,
    };

    private void ShowSection(bool transcriptions)
    {
        _transcriptionsKey.IsEngaged = transcriptions;
        _dictionaryKey.IsEngaged = !transcriptions;

        if (_composition is null)
        {
            _sectionHost.Content = Panels.EmptyState(
                transcriptions ? "NO RECORDINGS" : "DICTIONARY EMPTY",
                transcriptions ? "Press Record to start." : "Add words it keeps getting wrong.");
            return;
        }

        // Built once and reused: rebuilding would drop the user's search text every time
        // they switched tabs.
        if (transcriptions)
        {
            _transcriptionsView ??= new TranscriptionsView(_composition.Transcripts);
            _sectionHost.Content = _transcriptionsView;
        }
        else
        {
            _dictionaryView ??= new DictionaryView(_composition.Dictionary);
            _sectionHost.Content = _dictionaryView;
        }
    }

    private void FocusSearch()
    {
        if (_sectionHost.Content is TranscriptionsView t) t.FocusSearch();
        else if (_sectionHost.Content is DictionaryView d) d.FocusSearch();
    }

    private async void CopyLast()
    {
        var records = _composition?.Transcripts.Records;
        var last = records is { Count: > 0 } ? records[0] : null;
        if (last is null || Clipboard is null) return;
        await Clipboard.SetTextAsync(last.Text).ConfigureAwait(true);
    }

    private void ShowSettings()
    {
        if (_composition is null) return;
        var settings = new SettingsWindow(_composition);
        settings.ModelChanged += (_, _) => ModelChanged();
        _ = settings.ShowDialog(this);
    }

    private void ShowAbout() => _ = new AboutWindow().ShowDialog(this);

    private void ShowFault(string message)
    {
        _faultText.Text = message;
        _faultStrip.IsVisible = true;
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"could not open {path}: {e.Message}");
        }
    }

    /// <summary>Pulls state from the engine onto the panel.</summary>
    private void SyncFromEngine()
    {
        var engine = _composition?.Engine;
        if (engine is null)
        {
            if (_startedAt is not null) UpdateCounter();
            return;
        }

        var recording = engine.State == DictationState.Recording;
        var transcribing = engine.State == DictationState.Transcribing;
        var busy = recording || transcribing;

        _meter.Level = engine.Level;
        _meter.IsActive = recording;
        _recordLamp.IsLit = recording;
        _recordKey.IsEngaged = busy;
        _recordKey.Content = busy ? "STOP" : "RECORD";
        _mode.Text = transcribing ? "PROC" : recording ? "REC" : "STOP";
        _mode.Foreground = recording ? Tokens.Brushes.Record : Tokens.Brushes.InkOnDeckDim;

        _hookLamp.IsLit = engine.IsHotkeyArmed;
        _hookLamp.LampColor = engine.IsHotkeyArmed ? Tokens.Colors.MeterGreen : Tokens.Colors.MeterAmber;
        _micLamp.IsLit = recording;

        if (recording) _trace.Push(engine.Level);
        else if (!busy && _startedAt is not null) _trace.Clear();

        if (busy && _startedAt is null) _startedAt = DateTimeOffset.Now;
        else if (!busy) _startedAt = null;

        UpdateCounter();

        App.SetTrayRecording(recording);

        if (_overlay is not null)
        {
            if (busy && !IsActive) { _overlay.Present(); _overlay.Sync(recording, transcribing, engine.Level, _counter.Text); }
            else if (_overlay.IsVisible) _overlay.Hide();
        }
    }

    private void UpdateCounter()
    {
        var elapsed = _startedAt is null ? TimeSpan.Zero : DateTimeOffset.Now - _startedAt.Value;
        _counter.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}");
    }

    /// <summary>Toggles the transport. Exposed for headless tests.</summary>
    public void ToggleRecording()
    {
        // With no engine — a headless test, or a machine with no platform layer — the panel
        // still toggles so the visual state can be exercised.
        if (_composition?.Engine is null)
        {
            IsRecording = !IsRecording;
            _recordKey.Content = IsRecording ? "STOP" : "RECORD";
            _recordKey.IsEngaged = IsRecording;
            _recordLamp.IsLit = IsRecording;
            _meter.IsActive = IsRecording;
            _mode.Text = IsRecording ? "REC" : "STOP";
            _startedAt = IsRecording ? DateTimeOffset.Now : null;
            return;
        }

        // The button is a convenience; the hotkey is the real trigger. Both funnel through
        // the same engine so there is only ever one state machine.
        _composition.Engine.TogglePushToTalk();
        SyncFromEngine();
    }

    /// <summary>Whether the transport is engaged. Exposed for headless tests.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>The record lamp. Exposed for headless tests.</summary>
    public Lamp RecordLamp => _recordLamp;

    /// <summary>The level meter. Exposed for headless tests.</summary>
    public VuMeter Meter => _meter;

    /// <summary>The tape counter. Exposed for headless tests.</summary>
    public SegmentReadout Counter => _counter;

    /// <summary>The fault strip. Exposed for headless tests.</summary>
    public Border FaultStrip => _faultStrip;

    /// <summary>Shows a fault on the panel. Exposed for headless tests.</summary>
    public void ReportFault(string message) => ShowFault(message);

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Closing the window leaves Murmur in the tray — the hotkey still works, which is
        // the whole point. Quit is explicit, from the menu or the tray. Hiding rather than
        // closing also matters mechanically: Avalonia cannot re-show a closed window.
        if (!App.IsQuitting && _composition is not null)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _poll.Stop();
        _overlay?.Close();
        base.OnClosed(e);
    }
}
