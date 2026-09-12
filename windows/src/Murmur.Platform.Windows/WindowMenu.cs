using System.Runtime.InteropServices;
using Murmur.Abstractions;

namespace Murmur.Platform.Windows;

/// <summary>Restores the native Alt+Space menu for custom title bars.</summary>
/// <remarks>
/// <para>
/// The window draws its own caption, so Windows never sees a title bar to hang the system
/// menu on and Alt+Space does nothing by itself. The first version asked
/// <c>DefWindowProc</c> to behave as if the key had been pressed (<c>SC_KEYMENU</c>), which
/// is what a stock window does internally — but on a window whose caption has been
/// extended into the client area that path did not reliably show anything, so
/// Alt+Space, N (minimise) was dead.
/// </para>
/// <para>
/// This is the way custom-chrome apps (Windows Terminal, Visual Studio) do it: take the
/// window's real system menu, let Windows refresh which items are enabled for the current
/// state, track it as a popup at the caption's top-left, and post the chosen command back
/// to the window. Mnemonics work inside the popup, so Alt+Space then N minimises, X
/// maximises, R restores, C closes.
/// </para>
/// </remarks>
public sealed class WindowMenu : IWindowMenu
{
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint WM_INITMENU = 0x0116;
    private const int SC_KEYMENU = 0xF100;
    private const int VK_SPACE = 0x20;
    private const uint TPM_LEFTBUTTON = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const int SM_CYCAPTION = 4;
    private const int SM_CXFRAME = 32;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetSystemMenu(nint window, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);

    /// <inheritdoc />
    public void Show(nint handle)
    {
        if (handle == 0) return;

        var menu = GetSystemMenu(handle, revert: false);
        if (menu == 0)
        {
            // No system menu at all (no WS_SYSMENU). Fall back to the old behaviour.
            DefWindowProc(handle, WM_SYSCOMMAND, SC_KEYMENU, VK_SPACE);
            return;
        }

        // Windows enables and disables Restore/Move/Size/Minimize/Maximize for the
        // window's current state when it handles WM_INITMENU for the system menu.
        SendMessage(handle, WM_INITMENU, menu, 0);

        // Where the keyboard-opened menu appears on a stock window: just inside the
        // frame, below the caption.
        var x = 0;
        var y = 0;
        if (GetWindowRect(handle, out var rect))
        {
            x = rect.Left + GetSystemMetrics(SM_CXFRAME);
            y = rect.Top + GetSystemMetrics(SM_CYCAPTION);
        }

        // TPM_RETURNCMD: the popup runs its own modal loop (arrow keys and mnemonics
        // included) and hands back the command rather than sending WM_COMMAND, which the
        // UI framework's window procedure would not understand.
        var command = TrackPopupMenuEx(menu, TPM_LEFTBUTTON | TPM_RETURNCMD, x, y, handle, 0);
        if (command != 0) PostMessage(handle, WM_SYSCOMMAND, command, 0);
    }
}
