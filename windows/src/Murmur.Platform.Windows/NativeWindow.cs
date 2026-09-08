using System.Runtime.InteropServices;
using Murmur.Abstractions;

namespace Murmur.Platform.Windows;

/// <summary>
/// Extended-style tweaks through <c>SetWindowLongPtr</c>, and where the user is working.
/// </summary>
/// <remarks>
/// <c>WS_EX_NOACTIVATE</c> is the whole trick: the window can be shown, drawn and even
/// clicked, and the foreground window never changes. <c>WS_EX_TOOLWINDOW</c> keeps it out of
/// the taskbar and Alt-Tab, where a status readout has no business appearing.
/// </remarks>
public sealed class NativeWindow : IWindowTweaks
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    /// <inheritdoc />
    public void MakeNonActivating(nint handle)
    {
        if (handle == 0) return;

        var style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));
    }

    /// <inheritdoc />
    public (int X, int Y)? ActiveWindowCentre()
    {
        var window = GetForegroundWindow();
        if (window != IntPtr.Zero && GetWindowRect(window, out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
        {
            return ((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        }

        return GetCursorPos(out var point) ? (point.X, point.Y) : null;
    }
}
