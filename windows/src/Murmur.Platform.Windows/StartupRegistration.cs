using Microsoft.Win32;
using Murmur.Abstractions;

namespace Murmur.Platform.Windows;

/// <summary>
/// Start-at-sign-in through the per-user Run key.
/// </summary>
/// <remarks>
/// <para>
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>, not the machine-wide key and
/// not a scheduled task: it needs no elevation, it is what Task Manager's Startup tab lists
/// and lets the user disable, and removing it is one value delete.
/// </para>
/// <para>
/// The registered command carries <c>--minimized</c> so a sign-in launch goes straight to
/// the tray rather than opening the window over whatever the user was about to do.
/// </para>
/// </remarks>
public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Acapella";

    /// <summary>The argument that tells the app to start in the tray.</summary>
    public const string MinimizedArgument = "--minimized";

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                var value = key?.GetValue(ValueName) as string;
                return value is not null && value.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (enabled) key.SetValue(ValueName, $"\"{ExecutablePath}\" {MinimizedArgument}", RegistryValueKind.String);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The running executable. <c>Environment.ProcessPath</c> rather than
    /// <c>Assembly.Location</c>, which is empty inside a single-file bundle.
    /// </summary>
    private static string ExecutablePath => Environment.ProcessPath ?? string.Empty;
}
