using Murmur.Abstractions;
using System.Globalization;
using System.Text;

namespace Murmur.Core;

/// <summary>
/// A plain text log, because the first real-hardware run had nothing to read.
/// </summary>
/// <remarks>
/// <para>
/// Every failure that matters here — a hook that would not install, a microphone the OS is
/// feeding silence, a model that fails to load — happens on a background thread in a
/// process with no console. Without a file to open afterwards, "it did nothing when I
/// pressed the key" is the whole bug report.
/// </para>
/// <para>
/// Deliberately tiny: one static, synchronous, append. It rotates once at
/// <see cref="MaxBytes"/> so it can never grow without bound, and it never throws — a
/// logger that can take the app down is worse than no logger.
/// </para>
/// </remarks>
public static class Log
{
    /// <summary>Rotation threshold. One old copy is kept.</summary>
    public const long MaxBytes = 1024 * 1024;

    private static readonly Lock Gate = new();
    private static string? _path;

    /// <summary>Where the log is written. Defaults to the app data folder.</summary>
    public static string Path
    {
        get => _path ??= System.IO.Path.Combine(AppPaths.Root, "acapella.log");
        set => _path = value;
    }

    /// <summary>Appends one line.</summary>
    public static void Info(string message) => Write("info ", message);

    /// <summary>Appends one line marked as a warning.</summary>
    public static void Warn(string message) => Write("warn ", message);

    /// <summary>Appends one line marked as an error, with the exception if any.</summary>
    public static void Error(string message, Exception? exception = null) =>
        Write("error", exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}");

    private static void Write(string level, string message)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");

        try
        {
            lock (Gate)
            {
                var path = Path;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Never let logging be the thing that fails.
        }
    }
}
