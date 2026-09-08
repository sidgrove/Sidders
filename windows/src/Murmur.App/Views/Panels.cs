using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Murmur.App.Controls;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>Shared layout helpers. Every value comes from <see cref="Tokens"/>.</summary>
internal static class Panels
{
    /// <summary>Docks a control and returns it, so it reads inline in a Children list.</summary>
    public static Control Docked(Control control, Dock side)
    {
        DockPanel.SetDock(control, side);
        return control;
    }

    /// <summary>An eyebrow above a control.</summary>
    public static StackPanel Labelled(string label, Control content) => new()
    {
        Spacing = Tokens.Space.Chip,
        Children = { Text.Eyebrow(label), content },
    };

    /// <summary>A row with content on the left and actions on the right.</summary>
    public static DockPanel Split(Control leading, Control trailing)
    {
        DockPanel.SetDock(trailing, Dock.Right);
        trailing.VerticalAlignment = VerticalAlignment.Center;
        leading.VerticalAlignment = VerticalAlignment.Center;
        return new DockPanel { Children = { trailing, leading } };
    }

    /// <summary>A horizontal run of controls.</summary>
    public static StackPanel Row(double spacing, params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    /// <summary>A vertical run of controls.</summary>
    public static StackPanel Column(double spacing, params Control[] children)
    {
        var column = new StackPanel { Spacing = spacing };
        foreach (var child in children) column.Children.Add(child);
        return column;
    }

    /// <summary>A section: heading, optional description, then the body.</summary>
    public static StackPanel Section(string heading, string? description, Control body)
    {
        var column = Column(Tokens.Space.Roomy, Column(Tokens.Space.Tight, Text.Heading(heading)));
        if (description is not null) ((StackPanel)column.Children[0]).Children.Add(Text.Muted(description));
        column.Children.Add(body);
        return column;
    }

    /// <summary>A setting row: label and helper on the left, a switch on the right.</summary>
    public static DockPanel SwitchRow(string label, string? helper, bool value, Action<bool> onChange)
    {
        var toggle = new Switch { IsChecked = value };
        toggle.IsCheckedChanged += (_, _) => onChange(toggle.IsChecked == true);

        var text = Column(Tokens.Space.Hair, Text.Body(label));
        if (helper is not null) text.Children.Add(Text.Muted(helper));
        text.Margin = new Thickness(0, 0, Tokens.Space.Wide, 0);

        return Split(text, toggle);
    }

    /// <summary>The empty-state card. <c>.sg-empty</c>: white, centred, a title in the serif.</summary>
    public static Control EmptyState(string emoji, string label, string detail)
    {
        var title = Text.Title(label);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextAlignment = Avalonia.Media.TextAlignment.Center;

        var body = Text.Muted(detail);
        body.HorizontalAlignment = HorizontalAlignment.Center;
        body.TextAlignment = Avalonia.Media.TextAlignment.Center;

        var card = Card.Empty(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = Tokens.Space.Grid,
            Children =
            {
                new TextBlock { Text = emoji, FontSize = Tokens.Fonts.Hero, HorizontalAlignment = HorizontalAlignment.Center },
                title,
                body,
            },
        });
        card.CornerRadius = new CornerRadius(Tokens.Radius.CardLarge);
        card.Margin = new Thickness(Tokens.Layout.ScrollGutter, Tokens.Space.Roomy);
        return card;
    }

    /// <summary>Constrains content to the reading column and centres it.</summary>
    public static Border Column(Control content) => new()
    {
        MaxWidth = Tokens.Layout.ContentMaxWidth,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Child = content,
    };
}
