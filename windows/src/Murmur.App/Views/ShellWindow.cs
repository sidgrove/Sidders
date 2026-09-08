using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>
/// A Sidgrove window: the wash background, a slim caption strip with the mark and a
/// Very Vogue title in place of the OS title bar, thin glyphs for the window controls.
/// </summary>
/// <remarks>
/// The client area is extended over the title bar so the whole window is one surface.
/// Dragging the strip moves the window; double-clicking it toggles maximise; Escape closes
/// a sheet.
/// </remarks>
public abstract class ShellWindow : Window
{
    /// <summary>Whether this is a sheet: close only, Escape closes.</summary>
    protected bool IsSheet { get; init; }

    /// <summary>Configures the chrome. Call before setting content.</summary>
    protected ShellWindow()
    {
        Background = Tokens.Brushes.Wash;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = Tokens.Layout.CaptionHeight;
        TransparencyLevelHint = [WindowTransparencyLevel.None];
        FontFamily = Tokens.Fonts.Sans;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && IsSheet) { Close(); e.Handled = true; }
        };
    }

    /// <summary>Wraps <paramref name="body"/> beneath the caption strip, on the wash.</summary>
    protected Control Frame(string title, Control body, Control? trailing = null)
    {
        var root = new DockPanel();
        root.Children.Add(Panels.Docked(BuildCaption(title, trailing), Dock.Top));
        root.Children.Add(body);
        return new WashPanel { Child = root };
    }

    private Border BuildCaption(string title, Control? trailing)
    {
        // The site's header: the wordmark alone on the left, nav on the right.
        var left = Wordmark.Make(title);

        var glyphs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Hair,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (!IsSheet)
        {
            var minimise = new CaptionGlyph(CaptionGlyph.Glyph.Minimise);
            minimise.Click += (_, _) => WindowState = WindowState.Minimized;
            var maximise = new CaptionGlyph(CaptionGlyph.Glyph.Maximise);
            maximise.Click += (_, _) => ToggleMaximise();
            glyphs.Children.Add(minimise);
            glyphs.Children.Add(maximise);
        }

        var close = new CaptionGlyph(CaptionGlyph.Glyph.Close);
        close.Click += (_, _) => Close();
        glyphs.Children.Add(close);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Tokens.Space.Section,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (trailing is not null) right.Children.Add(trailing);
        right.Children.Add(glyphs);
        DockPanel.SetDock(right, Dock.Right);

        var strip = new Border
        {
            Height = Tokens.Layout.CaptionHeight,
            Padding = new Thickness(Tokens.Space.Section, 0, Tokens.Space.Base, 0),
            Background = Tokens.Brushes.None,
            Child = new DockPanel { Children = { right, left } },
        };

        strip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(strip).Properties.IsLeftButtonPressed) return;
            if (e.ClickCount == 2 && !IsSheet) ToggleMaximise();
            else BeginMoveDrag(e);
        };

        return strip;
    }

    private void ToggleMaximise() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
