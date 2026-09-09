using Avalonia;

namespace Murmur.App;

/// <summary>Entry point.</summary>
public static class Program
{
    /// <summary>Starts the app, or runs a headless self-test.</summary>
    /// <param name="args">
    /// Command line. <c>--selftest</c> exits without showing UI; <c>--minimized</c> starts
    /// in the tray, which is what the sign-in registration passes.
    /// </param>
    /// <returns>0 on success.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        PlatformFactory.InstallResolver();

        // The published single-file exe is the only artifact CI can run end to end, and a
        // GitHub runner cannot show a window. This branch exercises startup — assembly
        // loading, native library resolution out of the self-extracted bundle, model
        // discovery — and exits, which is the class of failure that only appears after
        // publishing.
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.Run();
        }

        App.StartMinimized = args.Contains(App.MinimizedArgument, StringComparer.OrdinalIgnoreCase);

        // Two copies would both hook the keyboard and both open the transcript log, and the
        // second one loses the file. One per user session; a second launch simply exits.
        using var single = new Mutex(initiallyOwned: true, $"Local\\{Murmur.Abstractions.AppPaths.ProductName}.SingleInstance", out var first);
        if (!first) return 0;

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Configures Avalonia. Also used by the headless test host.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
