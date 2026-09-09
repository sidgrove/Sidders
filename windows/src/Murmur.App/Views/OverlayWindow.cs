using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;
using Murmur.Core;

namespace Murmur.App.Views;

/// <summary>
/// The pill shown over other applications while you dictate.
/// </summary>
/// <remarks>
/// <para>
/// It appears on the monitor holding the window the text is going into, found through
/// <see cref="IWindowTweaks.ActiveWindowCentre"/> — not on the primary monitor, which is
/// where the first version put it.
/// </para>
/// <para>
/// <b>It must never take focus.</b> <c>ShowActivated</c> is off, it is not hit-testable,
/// and on Windows <see cref="IWindowTweaks.MakeNonActivating"/> sets <c>WS_EX_NOACTIVATE</c>.
/// </para>
/// <para>
/// While recording it shows the tail of the running transcript, so words appear as they
/// are spoken. That is the single biggest thing that makes dictation feel responsive.
/// </para>
/// </remarks>
public sealed class OverlayWindow : Window
{
    private readonly IWindowTweaks? _tweaks;
    private readonly StatusDot _dot;
    private readonly LevelBars _bars;
    private readonly TextBlock _state;
    private readonly TextBlock _counter;
    private readonly TextBlock _preview;

    /// <summary>Builds the overlay. Not shown until <see cref="Present"/>.</summary>
    public OverlayWindow(IWindowTweaks? tweaks)
    {
        _tweaks = tweaks;

        SizeToContent = SizeToContent.Width;
        Height = Tokens.Layout.OverlayHeight + Tokens.Layout.OverlayShadowRoom * 2;
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

        _dot = new StatusDot { Fill = Tokens.Brushes.BrandStrong, IsLive = true, VerticalAlignment = VerticalAlignment.Center };
        _bars = new LevelBars(Tokens.Layout.BarsCountSmall, Tokens.Layout.BarsHeightSmall) { VerticalAlignment = VerticalAlignment.Center };
        _state = Text.BodyStrong("Listening");
        _state.TextWrapping = TextWrapping.NoWrap;
        _state.VerticalAlignment = VerticalAlignment.Center;
        _counter = Text.Number("00:00", Tokens.Fonts.Body, Tokens.Brushes.Muted);
        _counter.VerticalAlignment = VerticalAlignment.Center;
        _preview = Text.Muted(string.Empty);
        _preview.TextWrapping = TextWrapping.NoWrap;
        _preview.TextTrimming = TextTrimming.None;
        _preview.MaxWidth = Tokens.Layout.OverlayPreviewWidth;
        _preview.VerticalAlignment = VerticalAlignment.Center;
        _preview.IsVisible = false;

        var pill = new Border
        {
            Background = Tokens.Brushes.Card,
            BorderBrush = Tokens.Brushes.CardBorder,
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            CornerRadius = new CornerRadius(Tokens.Radius.Pill),
            BoxShadow = Tokens.Shadow.Lift,
            Height = Tokens.Layout.OverlayHeight,
            Padding = new Thickness(Tokens.Space.Roomy, 0, Tokens.Space.Card, 0),
            Margin = new Thickness(Tokens.Layout.OverlayShadowRoom),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Panels.Row(Tokens.Space.Base, _dot, _state, _bars, _preview, _counter),
        };

        Content = pill;

        Opened += (_, _) => tweaks?.MakeNonActivating(TryGetPlatformHandle()?.Handle ?? 0);
    }

    /// <summary>Shows the pill at the bottom centre of the screen the user is working on.</summary>
    public void Present()
    {
        var anchor = _tweaks?.ActiveWindowCentre();
        var screen = anchor is { } a ? Screens.ScreenFromPoint(new PixelPoint(a.X, a.Y)) : null;
        screen ??= Screens.Primary;

        if (screen is not null)
        {
            var work = screen.WorkingArea;
            var scale = screen.Scaling;
            var width = (Bounds.Width > 0 ? Bounds.Width : Tokens.Layout.MainMinWidth / 2) * scale;
            Position = new PixelPoint(
                (int)(work.X + (work.Width - width) / 2),
                (int)(work.Bottom - (Height + Tokens.Layout.OverlayBottomMargin - Tokens.Layout.OverlayShadowRoom) * scale));
        }

        if (!IsVisible) Show();
    }

    /// <summary>Pushes the current state onto the pill.</summary>
    /// <param name="recording">The key is down.</param>
    /// <param name="transcribing">The key is up and the model is working.</param>
    /// <param name="cleaning">The AI tier is on, so the wait after transcribing is the network.</param>
    /// <param name="level">Input level, 0…1.</param>
    /// <param name="counter">Elapsed time, formatted.</param>
    /// <param name="preview">The running transcript, or empty.</param>
    public void Sync(bool recording, bool transcribing, bool cleaning, double level, string counter, string preview)
    {
        _bars.IsLive = recording;
        _bars.Level = level;
        _dot.Fill = recording ? Tokens.Brushes.BrandStrong : Tokens.Brushes.AmberMid;
        _state.Text = recording ? "Listening" : cleaning ? "Cleaning up" : "Working on it";
        _counter.Text = counter;

        var tail = Tail(preview);
        _preview.Text = tail;
        _preview.IsVisible = tail.Length > 0;

        // The pill sizes to content, so a growing preview re-centres it.
        if (IsVisible && tail.Length > 0) Present();
    }

    /// <summary>The last few words, so the newest speech is always in view.</summary>
    public static string Tail(string text)
    {
        var flat = text.Replace('\n', ' ').Trim();
        if (flat.Length <= Tokens.Layout.OverlayPreviewChars) return flat;

        var cut = flat[^Tokens.Layout.OverlayPreviewChars..];
        var space = cut.IndexOf(' ', StringComparison.Ordinal);
        return "…" + (space > 0 ? cut[(space + 1)..] : cut);
    }
}
