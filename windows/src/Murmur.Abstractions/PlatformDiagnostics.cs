namespace Murmur.Abstractions;

/// <summary>
/// The one way the platform layer can say something went wrong.
/// </summary>
/// <remarks>
/// <c>Murmur.Platform.Windows</c> cannot reference <c>Murmur.Core</c>, so it had no route to
/// the log. It used <c>Debug.WriteLine</c>, which is compiled out of a Release build — so a
/// ducker that had quietly given up for good, or a session mute that failed, left no trace
/// anywhere. The app wires <see cref="Sink"/> to the real log at startup.
/// </remarks>
public static class PlatformDiagnostics
{
    /// <summary>Where messages go. Null until the app installs the log.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>Reports a problem worth a line in the log.</summary>
    public static void Warn(string message) => Sink?.Invoke(message);
}
