using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;

namespace Murmur.App.Views;

/// <summary>
/// The main window, in the site's voice: a hero with a badge, a serif headline and its
/// italic accent, the one pill button with its coin, and a readout card; then the history
/// or the dictionary as 20px cards.
/// </summary>
public sealed class MainWindow : ShellWindow
{
    private readonly Composition? _composition;
    private readonly Badge _badge;
    private readonly TextBlock _headline;
    private readonly TextBlock _accent;
    private readonly TextBlock _subtitle;
    private readonly TextBlock _counter;
    private readonly TextBlock _readoutLabel;
    private readonly LevelBars _bars;
    private readonly SgButton _record;
    private readonly TextBlock _recordLabel;
    private readonly Coin _coin;
    private readonly NavLink _transcriptionsLink;
    private readonly NavLink _dictionaryLink;
    private readonly ContentControl _sectionHost;
    private readonly Border _fault;
    private readonly TextBlock _faultText;
    private readonly DispatcherTimer _poll;
    private readonly OverlayWindow? _overlay;
    private Controls.Switch? _enabled;
    private TextBlock? _enabledLabel;

    private TranscriptionsView? _transcriptionsView;
    private DictionaryView? _dictionaryView;
    private DateTimeOffset? _startedAt;
    private string _lastState = string.Empty;

    /// <summary>Builds a window with no engine behind it. Used by headless tests.</summary>
    public MainWindow() : this(null) { }

    /// <summary>Builds the window over <paramref name="composition"/>.</summary>
    public MainWindow(Composition? composition)
    {
        _composition = composition;

        Title = AppPaths.ProductName;
        MinWidth = Tokens.Layout.MainMinWidth;
        MinHeight = Tokens.Layout.MainMinHeight;
        Width = Tokens.Layout.MainWidth;
        Height = Tokens.Layout.MainHeight;

        _badge = new Badge("Ready");
        _headline = Headline.Line("Say it,");
        _accent = Headline.Accent("and it's typed.");
        _subtitle = Headline.Subtitle(string.Empty);

        _counter = Text.Hero("00:00");
        _readoutLabel = Text.Eyebrow("Idle");
        _bars = new LevelBars(Tokens.Layout.BarsCount, Tokens.Layout.BarsHeight) { HorizontalAlignment = HorizontalAlignment.Center };

        _recordLabel = new TextBlock { Text = "Start recording", VerticalAlignment = VerticalAlignment.Center };
        _coin = new Coin { VerticalAlignment = VerticalAlignment.Center };
        _record = new SgButton(Panels.Row(Tokens.Space.Base, _recordLabel, _coin), SgButton.Kind.Hero);
        _record.Click += (_, _) => ToggleRecording();

        _transcriptionsLink = new NavLink("Transcriptions") { IsActive = true };
        _dictionaryLink = new NavLink("Dictionary");
        _transcriptionsLink.Click += (_, _) => ShowSection(transcriptions: true);
        _dictionaryLink.Click += (_, _) => ShowSection(transcriptions: false);

        _faultText = Text.Body(string.Empty);
        _faultText.Foreground = Tokens.Brushes.Rose;
        _fault = BuildFault();

        _sectionHost = new ContentControl();

        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = Tokens.Motion.PanelPoll };
        _poll.Tick += (_, _) => SyncFromEngine();
        _poll.Start();

        if (_composition is not null) _overlay = new OverlayWindow(PlatformFactory.CreateWindowTweaks());

        Content = Frame(AppPaths.ProductName, BuildBody(), BuildNav());
        BindShortcuts();
        ShowSection(transcriptions: true);
        RefreshHint();

        if (_composition?.Engine is { } engine)
        {
            engine.Faulted += (_, message) => Dispatcher.UIThread.Post(() => ShowFault(message));
            engine.Start();
            _ = PreloadAsync(engine);
        }
    }

    private async Task PreloadAsync(DictationEngine engine)
    {
        var ready = await engine.PreloadAsync(CancellationToken.None).ConfigureAwait(true);
        if (!ready) ShowFault(DictationEngine.ModelMissingMessage);
        RefreshHint();
    }

    /// <summary>Re-checks the model after a download from Settings.</summary>
    public void ModelChanged()
    {
        if (_composition?.Engine is { } engine) _ = PreloadAsync(engine);
        RefreshHint();
    }

    private StackPanel BuildNav()
    {
        var settings = new NavLink("Settings");
        settings.Click += (_, _) => ShowSettings();
        return Panels.Row(Tokens.Space.Section, _transcriptionsLink, _dictionaryLink, settings);
    }

    private Border BuildBody()
    {
        var body = new DockPanel { ClipToBounds = false };
        body.Children.Add(Panels.Docked(BuildHero(), Dock.Top));
        body.Children.Add(Panels.Docked(_fault, Dock.Top));
        body.Children.Add(_sectionHost);
        return new Border { Child = body, ClipToBounds = false, Padding = new Thickness(Tokens.Space.Section, 0, Tokens.Space.Section, Tokens.Space.Wide) };
    }

    /// <summary>The hero: badge, headline, subtitle, the pill; and the readout card beside it.</summary>
    private Grid BuildHero()
    {
        _enabled = new Controls.Switch { IsChecked = _composition?.Settings.Data.IsEnabled ?? true, VerticalAlignment = VerticalAlignment.Center };
        _enabled.IsCheckedChanged += (_, _) => SetEnabled(_enabled.IsChecked == true);
        var badgeRow = Panels.Row(Tokens.Space.Base, _badge, _enabled, Text.Eyebrow("On"));
        _enabledLabel = (TextBlock)badgeRow.Children[2];

        var copy = Panels.Column(Tokens.Space.Roomy,
            badgeRow,
            Panels.Column(0, _headline, _accent),
            _subtitle,
            _record);
        _record.HorizontalAlignment = HorizontalAlignment.Left;
        _record.Margin = new Thickness(0, Tokens.Space.Snug, 0, 0);
        copy.VerticalAlignment = VerticalAlignment.Center;

        var readout = Card.Standard(Panels.Column(Tokens.Space.Roomy,
            Panels.Split(_readoutLabel, Text.Eyebrow(AppPaths.ProductName)),
            _counter,
            _bars), Tokens.Space.Wide);
        readout.CornerRadius = new CornerRadius(Tokens.Radius.CardLarge);
        readout.BoxShadow = Tokens.Shadow.Soft;
        readout.BorderBrush = Tokens.Brushes.Line;
        readout.VerticalAlignment = VerticalAlignment.Center;
        readout.MinWidth = Tokens.Layout.BarsCount * (Tokens.Layout.BarWidth + Tokens.Layout.BarGap) + Tokens.Space.Wide * 2;

        var hero = new Grid
        {
            ClipToBounds = false,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(Tokens.Layout.ScrollGutter, Tokens.Space.Wide, Tokens.Layout.ScrollGutter, Tokens.Space.Section),
        };
        Grid.SetColumn(copy, 0);
        Grid.SetColumn(readout, 1);
        readout.Margin = new Thickness(Tokens.Space.Section, 0, 0, 0);
        hero.Children.Add(copy);
        hero.Children.Add(readout);
        return hero;
    }

    private Border BuildFault()
    {
        var dismiss = new SgButton("Dismiss", SgButton.Kind.Quiet, compact: true);
        dismiss.Click += (_, _) => _fault.IsVisible = false;

        var notice = Card.Notice(Panels.Split(_faultText, dismiss), Tokens.Brushes.RoseLight, Tokens.Brushes.RoseTint);
        notice.IsVisible = false;
        notice.Margin = new Thickness(Tokens.Layout.ScrollGutter, 0, Tokens.Layout.ScrollGutter, Tokens.Space.Roomy);
        return notice;
    }

    private void BindShortcuts()
    {
        void Bind(Key key, KeyModifiers modifiers, Action action) =>
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, modifiers), Command = new Relay(action) });

        Bind(Key.R, KeyModifiers.Control, ToggleRecording);
        Bind(Key.OemComma, KeyModifiers.Control, ShowSettings);
        Bind(Key.F, KeyModifiers.Control, FocusSearch);
        Bind(Key.N, KeyModifiers.Control, () => { ShowSection(false); _dictionaryView?.AddEntry(); });
        Bind(Key.D1, KeyModifiers.Control, () => ShowSection(true));
        Bind(Key.D2, KeyModifiers.Control, () => ShowSection(false));
        Bind(Key.C, KeyModifiers.Control | KeyModifiers.Shift, CopyLast);
        Bind(Key.W, KeyModifiers.Control, Hide);
        Bind(Key.Q, KeyModifiers.Control, App.Quit);
        Bind(Key.F1, KeyModifiers.None, ShowAbout);
        Bind(Key.L, KeyModifiers.Control | KeyModifiers.Shift, () => OpenPath(Log.Path));
    }

    private void ShowSection(bool transcriptions)
    {
        _transcriptionsLink.IsActive = transcriptions;
        _dictionaryLink.IsActive = !transcriptions;

        if (_composition is null)
        {
            _sectionHost.Content = Panels.EmptyState("🎙️", transcriptions ? "No recordings yet" : "Dictionary is empty",
                transcriptions ? "Press record to start." : "Add the words it keeps getting wrong.");
            return;
        }

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
        if (records is not { Count: > 0 } || Clipboard is null) return;
        await Clipboard.SetTextAsync(records[0].Text).ConfigureAwait(true);
    }

    private void ShowSettings()
    {
        if (_composition is null) return;
        var settings = new SettingsWindow(_composition);
        settings.ModelChanged += (_, _) => ModelChanged();
        settings.Closed += (_, _) => RefreshHint();
        _ = settings.ShowDialog(this);
    }

    private void ShowAbout() => _ = new AboutWindow().ShowDialog(this);

    private void ShowFault(string message)
    {
        _faultText.Text = message;
        _fault.IsVisible = true;
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

    private string KeyName => KeyNames.Describe(_composition?.Settings.Data.PushToTalkKey ?? 0xA3, _composition?.Settings.Data.PushToTalkModifiers ?? 0);

    /// <summary>The idle subtitle: which key, which mode, whether AI is on.</summary>
    private void RefreshHint()
    {
        if (_composition is null) { _subtitle.Text = "Press the button to start."; return; }

        var mode = _composition.Settings.Data.TapToToggle
            ? $"Tap {KeyName} anywhere to start, tap again to stop."
            : $"Hold {KeyName} anywhere and talk.";
        var ai = _composition.Settings.Data.AiCleanup ? " Cleaned up by Gemini before it lands." : " Typed exactly as you said it.";
        _subtitle.Text = mode + ai;
    }

    /// <summary>Pulls state from the engine onto the hero.</summary>
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

        _bars.Level = engine.Level;
        _bars.IsLive = recording;

        var state = recording ? "Listening" : transcribing ? "Working" : "Ready";
        if (state != _lastState)
        {
            _lastState = state;
            SetState(recording, transcribing);
        }

        if (busy && _startedAt is null) _startedAt = DateTimeOffset.Now;
        else if (!busy) _startedAt = null;
        UpdateCounter();

        App.SetTrayRecording(recording);

        if (_overlay is not null)
        {
            if (busy && !IsActive) { _overlay.Present(); _overlay.Sync(recording, transcribing, engine.Level, _counter.Text ?? string.Empty); }
            else if (_overlay.IsVisible) _overlay.Hide();
        }
    }

    /// <summary>Pauses or resumes the key without quitting.</summary>
    private void SetEnabled(bool on)
    {
        if (_enabledLabel is not null) _enabledLabel.Text = on ? "ON" : "OFF";
        if (_composition is not null && _composition.Settings.Data.IsEnabled != on)
        {
            _composition.Settings.Update(_composition.Settings.Data with { IsEnabled = on });
        }
        _lastState = string.Empty;
        SetState(recording: false, transcribing: false);
    }

    private void SetState(bool recording, bool transcribing)
    {
        var off = _composition is not null && !_composition.Settings.Data.IsEnabled;
        if (off && !recording && !transcribing)
        {
            _badge.Set("Off", Tokens.Brushes.Faint, live: false);
            _readoutLabel.Text = "OFF";
            _recordLabel.Text = "Start recording";
            _coin.IsStop = false;
            return;
        }

        if (recording)
        {
            _badge.Set("Listening", Tokens.Brushes.Rose, live: true);
            _readoutLabel.Text = "RECORDING";
            _recordLabel.Text = "Stop";
            _coin.IsStop = true;
        }
        else if (transcribing)
        {
            _badge.Set("Working", Tokens.Brushes.AmberMid, live: true);
            _readoutLabel.Text = "TRANSCRIBING";
            _recordLabel.Text = "Stop";
            _coin.IsStop = true;
        }
        else
        {
            _badge.Set("Ready", Tokens.Brushes.Brand, live: false);
            _readoutLabel.Text = "IDLE";
            _recordLabel.Text = "Start recording";
            _coin.IsStop = false;
        }
    }

    private void UpdateCounter()
    {
        var elapsed = _startedAt is null ? TimeSpan.Zero : DateTimeOffset.Now - _startedAt.Value;
        _counter.Text = string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}");
    }

    /// <summary>Toggles the transport. Exposed for headless tests.</summary>
    public void ToggleRecording()
    {
        if (_composition?.Engine is null)
        {
            IsRecording = !IsRecording;
            _bars.IsLive = IsRecording;
            SetState(IsRecording, transcribing: false);
            _startedAt = IsRecording ? DateTimeOffset.Now : null;
            return;
        }

        _composition.Engine.TogglePushToTalk();
        SyncFromEngine();
    }

    /// <summary>Whether the transport is engaged. Exposed for headless tests.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>The state the readout reflects. Exposed for headless tests.</summary>
    public string StateText => _readoutLabel.Text switch { "RECORDING" => "Listening", "TRANSCRIBING" => "Working", _ => "Ready" };

    /// <summary>The bars. Exposed for headless tests.</summary>
    public LevelBars Bars => _bars;

    /// <summary>The fault notice. Exposed for headless tests.</summary>
    public Border FaultNotice => _fault;

    /// <summary>Shows a fault. Exposed for headless tests.</summary>
    public void ReportFault(string message) => ShowFault(message);

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Closing leaves the app in the tray; the hotkey still works. Quit is explicit.
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

/// <summary>The smallest possible command, for key bindings.</summary>
public sealed class Relay : System.Windows.Input.ICommand
{
    private readonly Action _action;

    /// <summary>Wraps an action.</summary>
    public Relay(Action action) => _action = action;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _action();
}
