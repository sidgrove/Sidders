using Avalonia.Input;

namespace Murmur.App.Views;

/// <summary>
/// Windows virtual-key codes for the keys a person might record as a push-to-talk key,
/// and the names to show for them.
/// </summary>
/// <remarks>
/// Avalonia reports a <see cref="Key"/>; the hook compares virtual-key codes. This is the
/// bridge, covering everything sensible to hold or tap. Keys that type a character are
/// allowed but the hook passes them through, so a letter will also be typed — Settings
/// says so.
/// </remarks>
public static class KeyNames
{
    private static readonly Dictionary<Key, (int Code, string Name)> Map = Build();

    /// <summary>The virtual-key code for an Avalonia key, or null if it cannot be a hotkey.</summary>
    public static int? ToVirtualKey(Key key) => Map.TryGetValue(key, out var v) ? v.Code : null;

    /// <summary>A display name for a virtual-key code.</summary>
    public static string Describe(int virtualKey)
    {
        foreach (var (_, value) in Map)
        {
            if (value.Code == virtualKey) return value.Name;
        }

        return $"Key 0x{virtualKey:X2}";
    }

    /// <summary>Whether holding or tapping this key also types something.</summary>
    public static bool TypesACharacter(int virtualKey) =>
        virtualKey is (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) or (>= 0x60 and <= 0x6F) or (>= 0xBA and <= 0xE2) or 0x20 or 0x09;

    private static Dictionary<Key, (int, string)> Build()
    {
        var map = new Dictionary<Key, (int, string)>
        {
            [Key.LeftCtrl] = (0xA2, "Left Ctrl"),
            [Key.RightCtrl] = (0xA3, "Right Ctrl"),
            [Key.LeftShift] = (0xA0, "Left Shift"),
            [Key.RightShift] = (0xA1, "Right Shift"),
            [Key.LeftAlt] = (0xA4, "Left Alt"),
            [Key.RightAlt] = (0xA5, "Right Alt"),
            [Key.LWin] = (0x5B, "Left Windows"),
            [Key.RWin] = (0x5C, "Right Windows"),
            [Key.Apps] = (0x5D, "Menu"),
            [Key.CapsLock] = (0x14, "Caps Lock"),
            [Key.Scroll] = (0x91, "Scroll Lock"),
            [Key.Pause] = (0x13, "Pause"),
            [Key.Insert] = (0x2D, "Insert"),
            [Key.Delete] = (0x2E, "Delete"),
            [Key.Home] = (0x24, "Home"),
            [Key.End] = (0x23, "End"),
            [Key.PageUp] = (0x21, "Page Up"),
            [Key.PageDown] = (0x22, "Page Down"),
            [Key.Space] = (0x20, "Space"),
            [Key.Tab] = (0x09, "Tab"),
            [Key.OemTilde] = (0xC0, "` (backtick)"),
            [Key.OemMinus] = (0xBD, "-"),
            [Key.OemPlus] = (0xBB, "="),
            [Key.OemOpenBrackets] = (0xDB, "["),
            [Key.OemCloseBrackets] = (0xDD, "]"),
            [Key.OemPipe] = (0xDC, "\\"),
            [Key.OemSemicolon] = (0xBA, ";"),
            [Key.OemQuotes] = (0xDE, "'"),
            [Key.OemComma] = (0xBC, ","),
            [Key.OemPeriod] = (0xBE, "."),
            [Key.OemQuestion] = (0xBF, "/"),
            [Key.NumLock] = (0x90, "Num Lock"),
            [Key.Multiply] = (0x6A, "Numpad *"),
            [Key.Add] = (0x6B, "Numpad +"),
            [Key.Subtract] = (0x6D, "Numpad -"),
            [Key.Decimal] = (0x6E, "Numpad ."),
            [Key.Divide] = (0x6F, "Numpad /"),
        };

        for (var i = 0; i < 24; i++) map[Key.F1 + i] = (0x70 + i, $"F{i + 1}");
        for (var i = 0; i < 26; i++) map[Key.A + i] = (0x41 + i, ((char)('A' + i)).ToString());
        for (var i = 0; i < 10; i++) map[Key.D0 + i] = (0x30 + i, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        for (var i = 0; i < 10; i++) map[Key.NumPad0 + i] = (0x60 + i, $"Numpad {i}");

        return map;
    }
}
