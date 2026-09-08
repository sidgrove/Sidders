namespace Murmur.Abstractions;

/// <summary>
/// The product name and where its files live.
/// </summary>
/// <remarks>
/// <para>
/// The product is <b>Sidders</b>; the code namespaces still say Murmur because the repo is
/// shared with the macOS app and renaming a dozen projects buys nothing the user can see.
/// Everything a user <i>does</i> see — window titles, the tray, the Start menu, the data
/// folder — goes through here.
/// </para>
/// <para>
/// The data folder moved from <c>%LOCALAPPDATA%\Murmur</c> to <c>%LOCALAPPDATA%\Sidders</c>
/// with the rename. <see cref="MigrateLegacyFolder"/> moves an old folder across once, so
/// the model, dictionary and history survive.
/// </para>
/// </remarks>
public static class AppPaths
{
    /// <summary>The name on the tin.</summary>
    public const string ProductName = "Sidders";

    /// <summary>The folder name used before the rename.</summary>
    public const string LegacyFolderName = "Murmur";

    /// <summary>Where settings, dictionary, history, log and models live.</summary>
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);

    /// <summary>The pre-rename folder, if it exists.</summary>
    public static string LegacyRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyFolderName);

    /// <summary>
    /// Moves a pre-rename data folder into place. Safe to call every launch: it does
    /// nothing once the new folder exists.
    /// </summary>
    /// <returns>True if a folder was moved.</returns>
    public static bool MigrateLegacyFolder()
    {
        try
        {
            if (Directory.Exists(Root) || !Directory.Exists(LegacyRoot)) return false;
            Directory.Move(LegacyRoot, Root);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Leave both where they are; the app will simply start fresh in the new folder.
            return false;
        }
    }
}
