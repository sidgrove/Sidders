namespace Murmur.Core;

/// <summary>
/// Writes a file so that a crash or power loss mid-write leaves the previous contents intact.
/// </summary>
/// <remarks>
/// <c>File.WriteAllText</c> truncates first and writes second. Settings are saved on nearly
/// every keystroke in a text field, so a process killed at the wrong moment left an empty
/// <c>settings.json</c> — and the loader, correctly refusing to fail on a corrupt file,
/// silently returned defaults: API key gone, microphone forgotten, welcome sheet back. The
/// fix is the oldest one there is: write beside, then rename over.
/// </remarks>
public static class AtomicFile
{
    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> as UTF-8, atomically.</summary>
    public static void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllText(temp, text, System.Text.Encoding.UTF8);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Writes one line per element, atomically.</summary>
    public static void WriteAllLines(string path, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllLines(temp, lines, System.Text.Encoding.UTF8);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Moves a file that could not be read aside as <c>name.corrupt</c>, so a bad file is
    /// kept for inspection rather than overwritten by the next save.
    /// </summary>
    public static void SetAside(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do; the next save will overwrite it.
        }
    }
}
