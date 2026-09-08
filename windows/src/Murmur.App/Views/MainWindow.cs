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
/// The main window: a listening card, then the history or the dictionary.
/// </summary>
/// <remarks>
/// Built in code, deliberately: every value comes from <see cref="Tokens"/>, and XAML makes
/// it far too easy to type a literal margin that escapes the design system.
/// </remarks>
public sealed class MainWindow : ShellWindow
{
    private readonly Composition? _composition;
    private readonly StatusDot _dot;
    private readonly TextBlock _state;
    private readonly TextBlock _hint;
    private readonly TextBlock _counter;
    private readonly LevelBars _bars;
    private readonly SgButton _record;
    private readonly Segmented _tabs;
    private readonly ContentControl _sectionHost;
    private readonly Border _fault;
    private readonly TextBlock _faultText;
    private readonly DispatcherTimer _poll;
    private readonly OverlayWindow? _overlay;

    private TranscriptionsView? _transcriptionsView;
    private DictionaryView? _dictionaryView;
    private DateTimeOffset? _startedAt;

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

        _dot = new StatusDot { VerticalAlignment = VerticalAlignment.Center };
        _state = Text.BodyStrong("Ready");
        _hint = Text.Muted(string.Empty);
        _counter = Text.Number("00:00", Tokens.Fonts.Counter, Tokens.Brushes.Ink);
        _counter.IsVisible = false;
        _bars = new LevelBars(Tokens.Layout.BarsCount, Tokens.Layout.BarsHeight) { HorizontalAlignment = HorizontalAlignment.Center };

        _record = new SgButton("Start recording", SgButton.Kind.Primary);
        _record.Click += (_, _) => ToggleRecording();

        _tabs = new Segmented(["Transcriptions", "Dictionary"]);
        _tabs.Selected += (_, index) => ShowSection(transcriptions: index == 0);

        _faultText = Text.Body(string.Empty);
        _faultText.Foreground = Tokens.Brushes.Rose;
        _fault = BuildFault();

        _sectionHost = new ContentControl();

        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = Tokens.Motion.PanelPoll };
        _poll.Tick += (_, _) => SyncFromEngine();
        _poll.Start();

        if (_composition is not null) _overlay = new OverlayWindow(PlatformFactory.CreateWindowTweaks());

        Content = Frame(AppPaths.ProductName, BuildBody(), BuildHeaderTrailing());
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

    private StackPanel BuildHeaderTrailing()
    {
        var settings = new SgButton("Settings", SgButton.Kind.Quiet, compact: true);
        settings.Click += (_, _) => ShowSettings();
        return Panels.Row(Tokens.Space.Base, _tabs, settings);
    }

    private Border BuildBody()
    {
        var body = new DockPanel { Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide) };
        body.Children.Add(Panels.Docked(BuildListenCard(), Dock.Top));
        body.Children.Add(Panels.Docked(_fault, Dock.Top));
        body.Children.Add(_sectionHost);
        return Panels.Column(body);
    }

    /// <summary>The listening card: state, bars, the one primary action.</summary>
    private Border BuildListenCard()
    {
        var status = Panels.Row(Tokens.Space.Snug, _dot, Panels.Column(Tokens.Space.Hair, _state, _hint));
        var right = Panels.Row(Tokens.Space.Roomy, _counter, _record);

        var content = Panels.Column(Tokens.Space.Roomy, Panels.Split(status, right), _bars);
        var card = Card.Standard(content);
        card.Margin = new Thickness(0, 0, 0, Tokens.Space.Roomy);
        return card;
    }

    private Border BuildFault()
    {
        var dismiss = new SgButton("Dismiss", SgButton.Kind.Quiet, compact: true);
        dismiss.Click += (_, _) => _fault.IsVisible = false;

        var notice = Card.Notice(Panels.Split(_faultText, dismiss), Tokens.Brushes.RoseLight, new Avalonia.Media.SolidColorBrush(Tokens.Colors.Rose, Tokens.Opacity.Edge));
        notice.IsVisible = false;
        notice.Margin = new Thickness(0, 0, 0, Tokens.Space.Roomy);
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
        _tabs.Select(transcriptions ? 0 : 1);

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

    /// <summary>The idle hint: which key, which mode.</summary>
    private void RefreshHint()
    {
        if (_composition is null) { _hint.Text = "Press record to start"; return; }

        var key = _composition.Settings.Data.PushToTalkKey switch
        {
            0xA3 => "Right Ctrl",
            0xA1 => "Right Shift",
            0x14 => "Caps Lock",
            0x7C => "F13",
            0xA5 => "Right Alt",
            _ => "the key",
        };
        var mode = _composition.Settings.Data.TapToToggle ? $"Tap {key} to start, tap again to stop" : $"Hold {key} and talk";
        var ai = _composition.Settings.Data.AiCleanup ? "  ·  AI clean-up on" : string.Empty;
        _hint.Text = mode + ai;
    }

    /// <summary>Pulls state from the engine onto the card.</summary>
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
        _dot.IsLive = busy;
        _dot.Fill = recording ? Tokens.Brushes.Brand : transcribing ? Tokens.Brushes.AmberMid : Tokens.Brushes.BrandMid;
        _state.Text = recording ? "Listening" : transcribing ? "Working on it" : "Ready";
        _record.Content = busy ? "Stop" : "Start recording";
        _counter.IsVisible = busy;

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
            _record.Content = IsRecording ? "Stop" : "Start recording";
            _bars.IsLive = IsRecording;
            _dot.IsLive = IsRecording;
            _state.Text = IsRecording ? "Listening" : "Ready";
            _counter.IsVisible = IsRecording;
            _startedAt = IsRecording ? DateTimeOffset.Now : null;
            return;
        }

        _composition.Engine.TogglePushToTalk();
        SyncFromEngine();
    }

    /// <summary>Whether the transport is engaged. Exposed for headless tests.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>The state label. Exposed for headless tests.</summary>
    public string StateText => _state.Text ?? string.Empty;

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
        // Hiding rather than closing also matters mechanically: a closed window cannot be
        // shown again.
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
