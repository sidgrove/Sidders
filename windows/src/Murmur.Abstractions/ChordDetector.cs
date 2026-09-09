namespace Murmur.Abstractions;

/// <summary>What one key event meant for the push-to-talk chord.</summary>
public enum ChordEvent
{
    /// <summary>Nothing changed.</summary>
    None,

    /// <summary>The chord is now held.</summary>
    Pressed,

    /// <summary>The trigger key came up.</summary>
    Released,
}

/// <summary>
/// Turns raw key events into chord presses and releases, in any key order.
/// </summary>
/// <remarks>
/// <para>
/// A chord such as Win+Ctrl is a <i>set</i> of held keys, not a sequence. Users press the
/// two keys within a few milliseconds of each other in whichever order their fingers land,
/// so the chord is complete the moment the last of its keys goes down — whether that was
/// the trigger or a modifier. Requiring the modifier first meant a Ctrl-then-Win press was
/// missed, and then fired late off Ctrl's key autorepeat, which felt like "sometimes".
/// </para>
/// <para>
/// Release is tied to the trigger key alone: letting go of a modifier while the trigger is
/// still held keeps the recording going, and the trigger coming up always ends it, so a
/// recording can never be left running.
/// </para>
/// <para>
/// Key state is read through a delegate rather than tracked from events, because the hook
/// is installed after the app starts and a key already held at that moment would otherwise
/// be invisible until released.
/// </para>
/// </remarks>
public sealed class ChordDetector
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private readonly Func<int, bool> _isKeyDown;
    private bool _active;

    /// <param name="isKeyDown">Whether a virtual key is physically down right now.</param>
    public ChordDetector(Func<int, bool> isKeyDown) => _isKeyDown = isKeyDown;

    /// <summary>The key whose press and release bracket the recording.</summary>
    public int TriggerKey { get; set; }

    /// <summary>Modifiers that must also be held, as <see cref="HotkeyModifiers"/> flags.</summary>
    public int Modifiers { get; set; }

    /// <summary>Whether the chord is currently held.</summary>
    public bool IsActive => _active;

    /// <summary>Feeds one key event.</summary>
    /// <param name="key">A left/right-specific virtual key.</param>
    /// <param name="isDown">True for key-down, including autorepeat; false for key-up.</param>
    public ChordEvent Feed(int key, bool isDown)
    {
        if (key == TriggerKey)
        {
            if (isDown)
            {
                // Autorepeat re-fires key-down while held; only the first is a press.
                if (_active || !ModifiersHeld()) return ChordEvent.None;
                _active = true;
                return ChordEvent.Pressed;
            }

            if (!_active) return ChordEvent.None;
            _active = false;
            return ChordEvent.Released;
        }

        // A required modifier arriving after the trigger completes the chord too.
        if (isDown && !_active && (FlagOf(key) & Modifiers) != 0 && _isKeyDown(TriggerKey) && ModifiersHeld())
        {
            _active = true;
            return ChordEvent.Pressed;
        }

        return ChordEvent.None;
    }

    /// <summary>Forgets a held chord, e.g. when the hook is reinstalled.</summary>
    public void Reset() => _active = false;

    private bool ModifiersHeld()
    {
        var required = (HotkeyModifiers)Modifiers;
        if (required == HotkeyModifiers.None) return true;

        if (required.HasFlag(HotkeyModifiers.Control) && !_isKeyDown(VK_CONTROL)) return false;
        if (required.HasFlag(HotkeyModifiers.Shift) && !_isKeyDown(VK_SHIFT)) return false;
        if (required.HasFlag(HotkeyModifiers.Alt) && !_isKeyDown(VK_MENU)) return false;
        if (required.HasFlag(HotkeyModifiers.Windows) && !_isKeyDown(VK_LWIN) && !_isKeyDown(VK_RWIN)) return false;
        return true;
    }

    private static int FlagOf(int vk) => vk switch
    {
        VK_LCONTROL or VK_RCONTROL or VK_CONTROL => (int)HotkeyModifiers.Control,
        VK_LSHIFT or VK_RSHIFT or VK_SHIFT => (int)HotkeyModifiers.Shift,
        VK_LMENU or VK_RMENU or VK_MENU => (int)HotkeyModifiers.Alt,
        VK_LWIN or VK_RWIN => (int)HotkeyModifiers.Windows,
        _ => 0,
    };
}
