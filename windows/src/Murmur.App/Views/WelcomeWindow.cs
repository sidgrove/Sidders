using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;

namespace Murmur.App.Views;

/// <summary>
/// The first-run walkthrough: get the model, pick the key, try it.
/// </summary>
/// <remarks>
/// Three steps and out. The playground is a real dictation into a real text box, so the
/// user has seen the whole loop work before they are on their own.
/// </remarks>
public sealed class WelcomeWindow : ShellWindow
{
    private readonly Composition _composition;
    private readonly ContentControl _host;
    private readonly TextBlock _stepLabel;
    private readonly SgButton _next;
    private readonly SgButton _back;
    private readonly ModelPart _model;
    private readonly KeyPart _key;
    private readonly TextBox _playground;
    private readonly TextBlock _playgroundHint;
    private readonly Control[] _cards;
    private int _step;

    /// <summary>Raised after the model is downloaded.</summary>
    public event EventHandler? ModelChanged;

    /// <summary>Builds the walkthrough.</summary>
    public WelcomeWindow(Composition composition)
    {
        _composition = composition;

        Title = "Welcome";
        IsSheet = true;
        Width = Tokens.Layout.SettingsWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _model = new ModelPart(composition);
        _model.ModelChanged += (_, _) => { ModelChanged?.Invoke(this, EventArgs.Empty); RefreshButtons(); };
        _key = new KeyPart(composition);

        _playground = Field.Multiline("Press your key and say something. It will land here.");
        _playgroundHint = Text.Muted(string.Empty);

        _stepLabel = Text.Eyebrow(string.Empty);
        _host = new ContentControl();
        _back = new SgButton("Back", SgButton.Kind.Ghost);
        _back.Click += (_, _) => Go(_step - 1);
        _next = new SgButton("Next", SgButton.Kind.Primary);
        _next.Click += (_, _) => { if (_step == 2) Finish(); else Go(_step + 1); };

        var skip = new SgButton("Skip for now", SgButton.Kind.Quiet, compact: true);
        skip.Click += (_, _) => Finish();

        var footer = Panels.Split(skip, Panels.Row(Tokens.Space.Snug, _back, _next));

        var body = Panels.Column(Tokens.Space.Wide, _stepLabel, _host, footer);
        body.Margin = new Thickness(Tokens.Space.Wide, Tokens.Space.Snug, Tokens.Space.Wide, Tokens.Space.Wide);
        Content = Frame("Welcome", body);

        // Built once: a control may only ever have one parent, and the parts are shared
        // between steps.
        _cards =
        [
            Card.Standard(Panels.Section("Get the speech model",
                "Everything is transcribed on this machine. One download, then no internet needed.", _model)),
            Card.Standard(Panels.Section("Choose your key",
                "Pick something you never use for typing. Right Ctrl is the safe default.", _key)),
            Card.Standard(Panels.Section("Try it",
                "Click in the box, then use your key and speak. Escape cancels a recording.",
                Panels.Column(Tokens.Space.Base, _playground, _playgroundHint))),
        ];

        _composition.Engine?.Completed += OnCompleted;
        Closed += (_, _) => { if (_composition.Engine is { } e) e.Completed -= OnCompleted; };

        // On the playground step Escape means "cancel the recording", as the card says. A
        // sheet closes on Escape, and that closed the walkthrough mid-dictation without
        // marking it done — so it came straight back on the next launch. Tunnelled so it
        // runs before the sheet's own handler.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && _step == 2) e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Go(0);
    }

    private void OnCompleted(object? sender, DictationResult result) => Dispatcher.UIThread.Post(() =>
    {
        if (_step != 2) return;
        _playgroundHint.Text = $"That took {result.ProcessingTime.TotalMilliseconds:0} ms after you let go. {AppPaths.ProductName} types into whatever has focus, so it works anywhere.";
    });

    private void Go(int step)
    {
        _step = Math.Clamp(step, 0, 2);
        _stepLabel.Text = $"Step {_step + 1} of 3";

        _host.Content = _cards[_step];

        if (_step == 2) _playground.Focus();
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        _back.IsVisible = _step > 0;
        _next.Content = _step == 2 ? "Done" : "Next";
        _next.IsEnabled = _step != 0 || ModelPart.IsInstalled;
    }

    private void Finish()
    {
        _composition.Settings.Update(_composition.Settings.Data with { HasOnboarded = true });
        Close();
    }
}
