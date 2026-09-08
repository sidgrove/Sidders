using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Murmur.App.Design;

namespace Murmur.App.Controls;

/// <summary>One line in a menu.</summary>
/// <param name="Label">Shown uppercase, silkscreen style.</param>
/// <param name="Shortcut">Key gesture, or null for none. Also bound on the owning window.</param>
/// <param name="Action">What happens.</param>
public sealed record MenuEntry(string Label, KeyGesture? Shortcut, Action Action)
{
    /// <summary>A divider line.</summary>
    public static MenuEntry Separator { get; } = new(string.Empty, null, static () => { });

    /// <summary>Whether this entry is a divider.</summary>
    public bool IsSeparator => ReferenceEquals(this, Separator);
}

/// <summary>A titled group of entries — one heading on the menu row.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Entries">Its items, in order.</param>
public sealed record MenuGroup(string Title, IReadOnlyList<MenuEntry> Entries);

/// <summary>
/// The menu row: silkscreen headings on the chassis that open a popup of entries.
/// </summary>
/// <remarks>
/// <para>
/// Built from primitives rather than Avalonia's <c>Menu</c>, because <c>Menu</c> is themed
/// by the Fluent resource dictionary and re-skinning it means guessing at forty resource
/// keys. A <see cref="Popup"/> over a <see cref="Border"/> is styled by nothing but this
/// file, and every value in it is a token.
/// </para>
/// <para>
/// Shortcuts are registered on the owning window's <c>KeyBindings</c> so a
/// gesture works whether or not the menu is open, and the same entry drives both.
/// </para>
/// </remarks>
public sealed class PanelMenu : StackPanel
{
    private Popup? _open;

    /// <summary>Builds the row and binds every shortcut on <paramref name="owner"/>.</summary>
    public PanelMenu(Window owner, IEnumerable<MenuGroup> groups)
    {
        Orientation = Orientation.Horizontal;
        Height = Tokens.Material.MenuHeight;
        Spacing = Tokens.Space.Hair;

        foreach (var group in groups)
        {
            var heading = Heading(group.Title);
            var popup = BuildPopup(group, heading);
            heading.Click += (_, _) => Toggle(popup);
            Children.Add(heading);

            foreach (var entry in group.Entries.Where(e => e.Shortcut is not null))
            {
                owner.KeyBindings.Add(new KeyBinding
                {
                    Gesture = entry.Shortcut!,
                    Command = new Relay(entry.Action),
                });
            }
        }
    }

    private void Toggle(Popup popup)
    {
        if (_open is not null && !ReferenceEquals(_open, popup)) _open.IsOpen = false;
        popup.IsOpen = !popup.IsOpen;
        _open = popup.IsOpen ? popup : null;
    }

    private static Button Heading(string title) => new()
    {
        Content = new Silkscreen
        {
            Text = title,
            Foreground = Tokens.Brushes.InkOnDeck,
            VerticalAlignment = VerticalAlignment.Center,
        },
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(Tokens.Space.Snug, 0),
        VerticalAlignment = VerticalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private Popup BuildPopup(MenuGroup group, Control target)
    {
        var list = new StackPanel { Spacing = 0, MinWidth = Tokens.Material.KeyMinWidth * 4 };
        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                Background = Tokens.Brushes.Deck,
                BorderBrush = new SolidColorBrush(Tokens.Colors.Seam),
                BorderThickness = new Thickness(Tokens.Border.Hairline),
                CornerRadius = new CornerRadius(Tokens.Radius.Control),
                BoxShadow = Tokens.Shadow.Popup,
                Padding = new Thickness(Tokens.Space.Tight),
                Child = list,
            },
        };
        popup.Closed += (_, _) => { if (ReferenceEquals(_open, popup)) _open = null; };

        foreach (var entry in group.Entries)
        {
            if (entry.IsSeparator)
            {
                list.Children.Add(new Border
                {
                    Height = Tokens.Border.Seam,
                    Background = new SolidColorBrush(Tokens.Colors.InkOnDeck, Tokens.Material.SegmentGhostOpacity),
                    Margin = new Thickness(Tokens.Space.Snug, Tokens.Space.Tight),
                });
                continue;
            }

            var row = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(Tokens.Space.Snug, Tokens.Space.Tight + Tokens.Space.Hair),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(Tokens.Radius.Chip),
                Content = new DockPanel
                {
                    Children =
                    {
                        Shortcut(entry.Shortcut),
                        new Silkscreen
                        {
                            Text = entry.Label,
                            Foreground = Tokens.Brushes.InkOnDeck,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            };
            row.PointerEntered += (_, _) => row.Background = new SolidColorBrush(Tokens.Colors.DeckHover);
            row.PointerExited += (_, _) => row.Background = Brushes.Transparent;
            row.Click += (_, _) => { popup.IsOpen = false; entry.Action(); };
            list.Children.Add(row);
        }

        return popup;
    }

    private static TextBlock Shortcut(KeyGesture? gesture)
    {
        var text = new TextBlock
        {
            Text = gesture is null ? string.Empty : Describe(gesture),
            FontFamily = Tokens.Fonts.Mono,
            FontSize = Tokens.Fonts.Caption,
            Foreground = Tokens.Brushes.InkOnDeckDim,
            Margin = new Thickness(Tokens.Space.Wide, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(text, Dock.Right);
        return text;
    }

    private static string Describe(KeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        parts.Add(gesture.Key switch
        {
            Key.OemComma => ",",
            Key.D1 => "1",
            Key.D2 => "2",
            _ => gesture.Key.ToString(),
        });
        return string.Join('+', parts);
    }
}

/// <summary>The smallest possible <see cref="ICommand"/>, for key bindings.</summary>
public sealed class Relay : ICommand
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
