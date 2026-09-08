using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>
/// The pill shown over other applications while you dictate.
/// </summary>
/// <remarks>
/// <b>It must never take focus.</b> <c>ShowActivated</c> is off, it is not hit-testable,
/// and on Windows <see cref="IWindowTweaks.MakeNonActivating"/> sets <c>WS_EX_NOACTIVATE</c>
/// so even a click cannot move the foreground. If it ever took focus the user's text field
/// would lose it and the transcript would have nowhere to go.
/// </remarks>
public sealed class OverlayWindow : Window
{
    private readonly StatusDot _dot;
    private readonly LevelBars _bars;
    private readonly TextBlock _state;
    private readonly TextBlock _counter;

    /// <summary>Builds the overlay. Not shown until <see cref="Present"/>.</summary>
    public OverlayWindow(IWindowTweaks? tweaks)
    {
        Width = Tokens.Layout.OverlayWidth;
        Height = Tokens.Layout.OverlayHeight + Tokens.Space.Wide;   // room for the shadow
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        IsHitTestVisible = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None];
        FontFamily = Tokens.Fonts.Sans;

        _dot = new StatusDot { Fill = Tokens.Brushes.Brand, IsLive = true, VerticalAlignment = VerticalAlignment.Center };
        _bars = new LevelBars(Tokens.Layout.BarsCountSmall, Tokens.Layout.BarsHeightSmall) { VerticalAlignment = VerticalAlignment.Center };
        _state = Text.BodyStrong("Listening");
        _counter = Text.Number("00:00", Tokens.Fonts.Body, Tokens.Brushes.Muted);

        var pill = new Border
        {
            Background = new SolidColorBrush(Tokens.Colors.Card, Tokens.Opacity.Glass),
            BorderBrush = Tokens.Brushes.CardBorder,
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            CornerRadius = new CornerRadius(Tokens.Radius.Pill),
            BoxShadow = Tokens.Shadow.Lift,
            Height = Tokens.Layout.OverlayHeight,
            Padding = new Thickness(Tokens.Space.Roomy, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = Panels.Row(Tokens.Space.Base, _dot, _state, _bars, _counter),
        };

        Content = pill;

        Opened += (_, _) => tweaks?.MakeNonActivating(TryGetPlatformHandle()?.Handle ?? 0);
    }

    /// <summary>Shows the pill at the bottom centre of the primary screen.</summary>
    public void Present()
    {
        if (Screens.Primary?.WorkingArea is { } work)
        {
            var scale = Screens.Primary?.Scaling ?? 1;
            Position = new PixelPoint(
                (int)(work.X + (work.Width - Width * scale) / 2),
                (int)(work.Bottom - (Height + Tokens.Layout.OverlayBottomMargin) * scale));
        }

        if (!IsVisible) Show();
    }

    /// <summary>Pushes the current state onto the pill.</summary>
    public void Sync(bool recording, bool transcribing, double level, string counter)
    {
        _bars.IsLive = recording;
        _bars.Level = level;
        _dot.Fill = recording ? Tokens.Brushes.Brand : Tokens.Brushes.AmberMid;
        _state.Text = transcribing ? "Working on it" : "Listening";
        _counter.Text = counter;
    }
}
