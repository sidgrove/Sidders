using Avalonia.Headless.XUnit;
using Murmur.Abstractions;
using Murmur.App.Views;
using Shouldly;

namespace Murmur.AppTests;

/// <summary>Device selection preserves saved choices and writes only on user selection.</summary>
public sealed class MicrophonePickerTests
{
    [AvaloniaFact]
    public void Initial_selection_is_collapsed_and_does_not_change_settings()
    {
        var saved = new List<string?>();
        var picker = new MicrophonePicker([new AudioDevice("mic-a", "Desk microphone", true), new AudioDevice("mic-b", "Headset", false)], "mic-b", saved.Add);
        var window = new Avalonia.Controls.Window { Content = picker };
        window.Show();
        try { picker.Template.ShouldNotBeNull("the dropdown must inherit the ComboBox theme"); }
        finally { window.Close(); }
        picker.IsDropDownOpen.ShouldBeFalse();
        picker.SelectedIndex.ShouldBe(2);
        saved.ShouldBeEmpty();
        picker.SelectedIndex = 1;
        saved.ShouldBe(["mic-a"]);
        picker.SelectedIndex = 0;
        saved.ShouldBe(["mic-a", null]);
    }

    [AvaloniaFact]
    public void Disconnected_saved_device_is_preserved_until_another_is_selected()
    {
        var saved = new List<string?>();
        var picker = new MicrophonePicker([], "offline-mic", saved.Add);
        picker.SelectedIndex.ShouldBe(1);
        saved.ShouldBeEmpty();
        picker.SelectedIndex = 0;
        saved.Count.ShouldBe(1);
        saved[0].ShouldBeNull();
    }
}
