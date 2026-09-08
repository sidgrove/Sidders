using System.Runtime.InteropServices;
using Murmur.Abstractions;

namespace Murmur.Platform.Windows;

/// <summary>
/// Extended-style tweaks through <c>SetWindowLongPtr</c>.
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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    /// <inheritdoc />
    public void MakeNonActivating(nint handle)
    {
        if (handle == 0) return;

        var style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));
    }
}
