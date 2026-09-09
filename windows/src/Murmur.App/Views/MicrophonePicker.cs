using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Murmur.Abstractions;
using Murmur.App.Design;

namespace Murmur.App.Views;

/// <summary>A collapsed device chooser that preserves unavailable saved microphones.</summary>
public sealed class MicrophonePicker : ComboBox
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ComboBox);
    /// <summary>Lists devices and reports deliberate selection changes, never initialization.</summary>
    public MicrophonePicker(IReadOnlyList<AudioDevice> devices, string? selectedId, Action<string?> select)
    {
        var choices = new List<(string? Id, string Label)> { (null, "Follow Windows default") };
        choices.AddRange(devices.Select(d => ((string?)d.Id, d.Name + (d.IsDefault ? " (Windows default)" : string.Empty))));
        if (selectedId is not null && choices.All(c => c.Id != selectedId))
            choices.Add((selectedId, "Saved microphone (disconnected)"));

        ItemsSource = choices.Select(c => c.Label).ToArray();
        SelectedIndex = choices.FindIndex(c => c.Id == selectedId);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        MinHeight = Tokens.Layout.HeroButtonHeight;
        MaxDropDownHeight = Tokens.Layout.DeviceDropdownHeight;
        Background = Tokens.Brushes.Card;
        BorderBrush = Tokens.Brushes.PanelBorder;
        CornerRadius = new CornerRadius(Tokens.Radius.Inner);
        FontFamily = Tokens.Fonts.Sans;
        FontSize = Tokens.Fonts.Base;
        SelectionChanged += (_, _) =>
        {
            if (SelectedIndex >= 0 && SelectedIndex < choices.Count) select(choices[SelectedIndex].Id);
        };
    }
}
