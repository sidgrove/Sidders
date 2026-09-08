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

    /// <summary>A label above a control.</summary>
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

    /// <summary>Centred "nothing here yet" copy.</summary>
    public static Control EmptyState(string emoji, string label, string detail) => new StackPanel
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Spacing = Tokens.Space.Snug,
        Margin = new Thickness(0, Tokens.Space.Page),
        Children =
        {
            new TextBlock { Text = emoji, FontSize = Tokens.Fonts.Counter, HorizontalAlignment = HorizontalAlignment.Center },
            Centre(Text.BodyStrong(label)),
            Centre(Text.Muted(detail)),
        },
    };

    private static TextBlock Centre(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Center;
        block.TextAlignment = Avalonia.Media.TextAlignment.Center;
        return block;
    }

    /// <summary>Constrains content to the reading column and centres it.</summary>
    public static Border Column(Control content) => new()
    {
        MaxWidth = Tokens.Layout.ContentMaxWidth,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Child = content,
    };
}
