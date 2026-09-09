using Murmur.Abstractions;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>Win+Ctrl must work whichever key lands first, and never fire twice.</summary>
public sealed class ChordDetectorTests
{
    private const int LeftControl = 0xA2;
    private const int Control = 0x11;
    private const int LeftWin = 0x5B;

    /// <summary>A keyboard whose physical state the test drives by hand.</summary>
    private sealed class Keyboard
    {
        private readonly HashSet<int> _down = [];

        public ChordDetector Detector { get; }

        public Keyboard(int trigger, HotkeyModifiers modifiers)
        {
            Detector = new ChordDetector(IsDown) { TriggerKey = trigger, Modifiers = (int)modifiers };
        }

        private bool IsDown(int vk) => vk switch
        {
            Control => _down.Contains(0xA2) || _down.Contains(0xA3),
            0x10 => _down.Contains(0xA0) || _down.Contains(0xA1),
            0x12 => _down.Contains(0xA4) || _down.Contains(0xA5),
            _ => _down.Contains(vk),
        };

        public ChordEvent Press(int vk) { _down.Add(vk); return Detector.Feed(vk, true); }
        public ChordEvent Repeat(int vk) => Detector.Feed(vk, true);
        public ChordEvent Release(int vk) { _down.Remove(vk); return Detector.Feed(vk, false); }
    }

    [Fact]
    public void Modifier_then_trigger_presses_on_the_trigger()
    {
        var kb = new Keyboard(LeftControl, HotkeyModifiers.Windows);

        kb.Press(LeftWin).ShouldBe(ChordEvent.None);
        kb.Press(LeftControl).ShouldBe(ChordEvent.Pressed);
        kb.Release(LeftControl).ShouldBe(ChordEvent.Released);
        kb.Release(LeftWin).ShouldBe(ChordEvent.None);
    }

    [Fact]
    public void Trigger_then_modifier_presses_on_the_modifier()
    {
        var kb = new Keyboard(LeftControl, HotkeyModifiers.Windows);

        kb.Press(LeftControl).ShouldBe(ChordEvent.None, "Win is not held yet");
        kb.Press(LeftWin).ShouldBe(ChordEvent.Pressed, "the chord completed on the modifier");
        kb.Release(LeftWin).ShouldBe(ChordEvent.None, "the trigger is still held");
        kb.Release(LeftControl).ShouldBe(ChordEvent.Released);
    }

    [Fact]
    public void Autorepeat_never_presses_twice()
    {
        var kb = new Keyboard(LeftControl, HotkeyModifiers.Windows);

        kb.Press(LeftWin);
        kb.Press(LeftControl).ShouldBe(ChordEvent.Pressed);
        kb.Repeat(LeftControl).ShouldBe(ChordEvent.None);
        kb.Repeat(LeftWin).ShouldBe(ChordEvent.None);
        kb.Release(LeftControl).ShouldBe(ChordEvent.Released);
    }

    [Fact]
    public void The_trigger_alone_does_nothing_when_a_modifier_is_required()
    {
        var kb = new Keyboard(LeftControl, HotkeyModifiers.Windows);

        kb.Press(LeftControl).ShouldBe(ChordEvent.None);
        kb.Repeat(LeftControl).ShouldBe(ChordEvent.None);
        kb.Release(LeftControl).ShouldBe(ChordEvent.None, "nothing was pressed, so nothing is released");
    }

    [Fact]
    public void A_modifier_press_that_is_not_part_of_the_chord_is_ignored()
    {
        var kb = new Keyboard(LeftControl, HotkeyModifiers.Windows);

        kb.Press(LeftControl);
        kb.Press(0xA0).ShouldBe(ChordEvent.None, "Shift is not in the chord");
    }

    [Fact]
    public void A_bare_key_needs_no_modifier()
    {
        var kb = new Keyboard(0xA3, HotkeyModifiers.None);

        kb.Press(0xA3).ShouldBe(ChordEvent.Pressed);
        kb.Release(0xA3).ShouldBe(ChordEvent.Released);
    }

    [Fact]
    public void Two_modifiers_complete_in_any_order()
    {
        var kb = new Keyboard(0x20, HotkeyModifiers.Control | HotkeyModifiers.Windows);

        kb.Press(LeftWin).ShouldBe(ChordEvent.None);
        kb.Press(0x20).ShouldBe(ChordEvent.None, "Ctrl is missing");
        kb.Press(LeftControl).ShouldBe(ChordEvent.Pressed);
        kb.Release(0x20).ShouldBe(ChordEvent.Released);
    }
}
