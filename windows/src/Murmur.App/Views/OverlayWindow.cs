using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Murmur.Abstractions;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>
/// The small readout shown over other applications while the key is held.
/// </summary>
/// <remarks>
/// <para>
/// The main window is the app, but dictation happens in someone else's text field. This is
/// the record lamp and meter you glance at while doing that — the Windows counterpart of the
/// macOS HUD panel.
/// </para>
/// <para>
/// <b>It must never take focus.</b> <c>ShowActivated</c> is off, it is not hit-testable, and
/// on Windows <see cref="IWindowTweaks.MakeNonActivating"/> sets <c>WS_EX_NOACTIVATE</c> so
/// even a click cannot move the foreground. If it ever took focus the user's text field
/// would lose it and the transcript would have nowhere to go.
/// </para>
/// </remarks>
public sealed class OverlayWindow : Window
{
    private readonly Lamp _lamp;
    private readonly VuMeter _meter;
    private readonly Silkscreen _state;
    private readonly TextBlock _counter;

    /// <summary>Builds the overlay. Not shown until <see cref="Present"/>.</summary>
    public OverlayWindow(IWindowTweaks? tweaks)
    {
        Width = Tokens.Material.OverlayWidth;
        Height = Tokens.Material.OverlayHeight;
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        IsHitTestVisible = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None];

        _lamp = new Lamp { LampColor = Tokens.Colors.Record, VerticalAlignment = VerticalAlignment.Center };
        _meter = new VuMeter
        {
            Width = Tokens.Material.MeterWidthSmall,
            Height = Tokens.Material.MeterHeightSmall,
            ShowScale = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _state = new Silkscreen { Text = "REC", Foreground = Tokens.Brushes.Ink, VerticalAlignment = VerticalAlignment.Center };
        _counter = new TextBlock
        {
            Text = "00:00",
            FontFamily = Tokens.Fonts.Mono,
            FontSize = Tokens.Fonts.Body,
            Foreground = Tokens.Brushes.Ink,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Content = new Border
        {
            Background = Tokens.Brushes.Chassis,
            CornerRadius = new CornerRadius(Tokens.Radius.Panel),
            BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
            BorderThickness = new Thickness(Tokens.Border.Hairline),
            BoxShadow = Tokens.Shadow.Overlay,
            Padding = new Thickness(Tokens.Space.Tight),
            Child = new BrushedPanel
            {
                CornerRadius = Tokens.Radius.Control,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Tokens.Space.Base,
                    Margin = new Thickness(Tokens.Space.Base, Tokens.Space.Snug),
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = Tokens.Space.Tight,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = Tokens.Space.Tight,
                                    Children = { _lamp, _state },
                                },
                                _counter,
                            },
                        },
                        _meter,
                    },
                },
            },
        };

        Opened += (_, _) =>
        {
            var handle = TryGetPlatformHandle()?.Handle ?? 0;
            tweaks?.MakeNonActivating(handle);
        };
    }

    /// <summary>Shows the readout at the bottom centre of the primary screen.</summary>
    public void Present()
    {
        var area = Screens.Primary?.WorkingArea;
        if (area is { } work)
        {
            var scale = Screens.Primary?.Scaling ?? 1;
            Position = new PixelPoint(
                (int)(work.X + (work.Width - Width * scale) / 2),
                (int)(work.Bottom - (Height + Tokens.Material.OverlayBottomMargin) * scale));
        }

        if (!IsVisible) Show();
    }

    /// <summary>Pushes the current transport state onto the readout.</summary>
    public void Sync(bool recording, bool transcribing, double level, string counter)
    {
        _lamp.IsLit = recording;
        _meter.IsActive = recording;
        _meter.Level = level;
        _state.Text = transcribing ? "PROCESSING" : "REC";
        _counter.Text = counter;
    }
}
