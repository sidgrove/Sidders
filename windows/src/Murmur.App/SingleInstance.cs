using Murmur.Abstractions;

namespace Murmur.App;

/// <summary>
/// One running copy per user session, and a way for a second launch to reach it.
/// </summary>
/// <remarks>
/// <para>
/// Two copies would both hook the keyboard and both open the transcript log, so the second
/// must not run. But it must not simply vanish either: the app lives in the tray, and a
/// user who has closed the window and clicks the Start menu again expects the window back.
/// So the second launch pulses a named event and exits, and the first, waiting on that
/// event from a background thread, raises <see cref="ShowRequested"/>.
/// </para>
/// <para>
/// <c>Local\</c> names scope both handles to the session, so two signed-in users can each
/// run their own copy.
/// </para>
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _listener;

    private SingleInstance(Mutex mutex, EventWaitHandle show)
    {
        _mutex = mutex;
        _show = show;
        _listener = new Thread(Listen) { IsBackground = true, Name = "SingleInstance.Listen" };
        _listener.Start();
    }

    /// <summary>Raised, on a background thread, when another launch asked for the window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>
    /// Claims the instance for this process.
    /// </summary>
    /// <param name="name">A base name; defaults to the product name.</param>
    /// <returns>
    /// The claim, or null when another copy already holds it — in which case that copy has
    /// been asked to show its window and the caller should exit.
    /// </returns>
    public static SingleInstance? TryClaim(string? name = null)
    {
        name ??= AppPaths.ProductName;
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.SingleInstance", out var first);
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");

        if (first) return new SingleInstance(mutex, show);

        show.Set();
        show.Dispose();
        mutex.Dispose();
        return null;
    }

    private void Listen()
    {
        WaitHandle[] handles = [_show, _stop];
        while (WaitHandle.WaitAny(handles) == 0)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Set();
        _listener.Join(TimeSpan.FromSeconds(1));
        _show.Dispose();
        _stop.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
