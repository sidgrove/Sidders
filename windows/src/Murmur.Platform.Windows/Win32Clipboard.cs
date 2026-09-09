using System.Runtime.InteropServices;

namespace Murmur.Platform.Windows;

/// <summary>
/// The Win32 clipboard, just enough to paste a dictation and put back what was there.
/// </summary>
/// <remarks>
/// <para>
/// Pasting is how the professional dictation tools deliver text, because it is one
/// message to the target rather than one per character: a 300-character dictation lands
/// in a few milliseconds instead of visibly typing itself out, and browsers and Electron
/// apps — which process each synthetic keystroke slowly — are where the difference is felt.
/// </para>
/// <para>
/// Only text is preserved. If the clipboard held an image or a file the paste replaces it
/// with the dictation, which is the trade every dictation tool makes.
/// </para>
/// </remarks>
internal static class Win32Clipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int OpenAttempts = 5;
    private static readonly TimeSpan OpenRetry = TimeSpan.FromMilliseconds(20);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr handle);

    /// <summary>The clipboard's text, or null if it holds none or could not be opened.</summary>
    public static string? GetText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT) || !Open()) return null;
        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Replaces the clipboard with <paramref name="text"/>.</summary>
    public static bool SetText(string text)
    {
        if (!Open()) return false;
        try
        {
            if (!EmptyClipboard()) return false;

            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var handle = GlobalAlloc(GMEM_MOVEABLE, bytes);
            if (handle == IntPtr.Zero) return false;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) { GlobalFree(handle); return false; }
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            // On success the system owns the handle; on failure it is still ours to free.
            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero) { GlobalFree(handle); return false; }
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Opens the clipboard, retrying briefly: another app may be holding it.</summary>
    private static bool Open()
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(OpenRetry);
        }

        return false;
    }
}
