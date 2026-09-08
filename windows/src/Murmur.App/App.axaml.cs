using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Murmur.App.Views;

namespace Murmur.App;

/// <summary>The application.</summary>
public partial class App : Application
{
    /// <summary>The argument that starts the app hidden in the tray.</summary>
    public const string MinimizedArgument = "--minimized";

    private static WindowIcon? s_trayIdle;
    private static WindowIcon? s_trayRecording;
    private static bool s_trayRecordingShown;

    private Composition? _composition;
    private MainWindow? _main;

    /// <summary>True once Quit has been chosen, so the main window really closes.</summary>
    public static bool IsQuitting { get; private set; }

    /// <summary>Whether to start hidden. Set by <c>Program</c> from the command line.</summary>
    public static bool StartMinimized { get; set; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _composition = Composition.Create();
            _main = new MainWindow(_composition);

            // Assigning MainWindow makes the lifetime show it. A sign-in launch stays in the
            // tray instead — the hotkey works either way.
            if (!StartMinimized) desktop.MainWindow = _main;

            // Closing the window leaves Murmur running in the tray. Quit is explicit.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Disposing tears down the keyboard hook and releases the audio device. Leaving
            // a low-level hook installed after exit is the kind of thing that makes a
            // machine feel broken until it is rebooted.
            desktop.ShutdownRequested += (_, _) =>
            {
                _composition?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _composition = null;
            };

            s_trayIdle = LoadIcon("tray.ico");
            s_trayRecording = LoadIcon("tray-rec.ico");
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Swaps the tray icon so recording is visible without any window.</summary>
    public static void SetTrayRecording(bool recording)
    {
        if (recording == s_trayRecordingShown || Current is null) return;
        s_trayRecordingShown = recording;

        var icons = TrayIcon.GetIcons(Current);
        if (icons is null || icons.Count == 0) return;

        var icon = recording ? s_trayRecording : s_trayIdle;
        if (icon is not null) icons[0].Icon = icon;
        icons[0].ToolTipText = recording ? "Sidders — recording" : "Sidders — hold the push-to-talk key to dictate";
    }

    /// <summary>Ends the app from anywhere.</summary>
    public static void Quit()
    {
        IsQuitting = true;
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
    }

    private static WindowIcon? LoadIcon(string name)
    {
        try
        {
            return new WindowIcon(AssetLoader.Open(new Uri($"avares://Sidders/Assets/{name}")));
        }
        catch (Exception e) when (e is FileNotFoundException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private void OnTrayShow(object? sender, EventArgs e) => ShowMain();

    private void OnTraySettings(object? sender, EventArgs e)
    {
        ShowMain();
        if (_main is not null && _composition is not null)
        {
            var settings = new SettingsWindow(_composition);
            settings.ModelChanged += (_, _) => _main.ModelChanged();
            _ = settings.ShowDialog(_main);
        }
    }

    private void OnTrayQuit(object? sender, EventArgs e) => Quit();

    private void ShowMain()
    {
        if (_main is null) return;

        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }
}
