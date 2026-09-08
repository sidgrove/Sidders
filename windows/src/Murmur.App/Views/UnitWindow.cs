using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>
/// A window that is part of the unit: the OS title bar is replaced by a caption strip on the
/// chassis, with the name plate, screws and drawn caption keys.
/// </summary>
/// <remarks>
/// <para>
/// The client area is extended over the title bar and the OS chrome hidden. Dragging the
/// strip moves the window, double-clicking it toggles maximise, and the caption keys do
/// what the OS ones would — so nothing is lost, it just stops looking like a Win32 dialog
/// sitting on top of a tape deck.
/// </para>
/// <para>
/// Every derived window calls <see cref="Frame"/> with its content and gets the same strip.
/// </para>
/// </remarks>
public abstract class UnitWindow : Window
{
    /// <summary>Whether the window offers minimise and maximise, or close only.</summary>
    protected bool IsResizableUnit { get; init; } = true;

    /// <summary>Text printed to the right of the unit name, the way a model number is.</summary>
    protected string ModelNumber { get; init; } = string.Empty;

    /// <summary>Configures the chrome. Call before setting <see cref="ContentControl.Content"/>.</summary>
    protected UnitWindow()
    {
        Background = Tokens.Brushes.Chassis;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = Tokens.Material.CaptionHeight;
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        FontFamily = Tokens.Fonts.Grotesque;
    }

    /// <summary>Wraps <paramref name="body"/> beneath the caption strip.</summary>
    protected Control Frame(string name, Control body)
    {
        var root = new DockPanel();
        root.Children.Add(Panels.Docked(BuildCaption(name), Dock.Top));
        root.Children.Add(body);
        return root;
    }

    private Border BuildCaption(string name)
    {
        var nameplate = new TextBlock
        {
            Text = name.ToUpperInvariant(),
            FontFamily = Tokens.Fonts.Grotesque,
            FontSize = Tokens.Fonts.Nameplate,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = Tokens.Fonts.NameplateTracking,
            Foreground = Tokens.Brushes.InkOnDeck,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var model = new Silkscreen
        {
            Text = ModelNumber,
            Foreground = Tokens.Brushes.SilkscreenOnChassis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Base,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new Screw { VerticalAlignment = VerticalAlignment.Center }, nameplate, model },
        };

        var keys = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Tight,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (IsResizableUnit)
        {
            var minimise = new CaptionKey { Kind = CaptionKey.Glyph.Minimise };
            minimise.Click += (_, _) => WindowState = WindowState.Minimized;
            var maximise = new CaptionKey { Kind = CaptionKey.Glyph.Maximise };
            maximise.Click += (_, _) => ToggleMaximise();
            keys.Children.Add(minimise);
            keys.Children.Add(maximise);
        }

        var close = new CaptionKey { Kind = CaptionKey.Glyph.Close };
        close.Click += (_, _) => Close();
        keys.Children.Add(close);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Base,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { keys, new Screw { VerticalAlignment = VerticalAlignment.Center } },
        };
        DockPanel.SetDock(right, Dock.Right);

        var strip = new Border
        {
            Height = Tokens.Material.CaptionHeight,
            Padding = new Thickness(Tokens.Space.Base, 0),
            Background = Tokens.Brushes.Chassis,
            BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
            BorderThickness = new Thickness(0, 0, 0, Tokens.Border.Seam),
            Child = new DockPanel { Children = { right, left } },
        };

        strip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(strip).Properties.IsLeftButtonPressed) return;

            if (e.ClickCount == 2 && IsResizableUnit) ToggleMaximise();
            else BeginMoveDrag(e);
        };

        return strip;
    }

    private void ToggleMaximise() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
