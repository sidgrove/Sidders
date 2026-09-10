using System.Runtime.InteropServices;
using Murmur.Abstractions;

namespace Murmur.Platform.Windows;

/// <summary>Restores the native Alt+Space menu for custom title bars.</summary>
public sealed class WindowMenu : IWindowMenu
{
    private const uint WmSysCommand = 0x0112;
    private const int ScKeyMenu = 0xF100;
    private const int Space = 0x20;

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);

    /// <inheritdoc />
    public void Show(nint handle)
    {
        // Let Windows own menu positioning, translated mnemonics, disabled commands,
        // keyboard navigation and dispatch. Bypass the custom chrome's key handling.
        if (handle != 0) DefWindowProc(handle, WmSysCommand, ScKeyMenu, Space);
    }
}
