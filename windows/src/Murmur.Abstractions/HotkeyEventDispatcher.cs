using System.Diagnostics;
using System.Threading.Channels;

namespace Murmur.Abstractions;

/// <summary>Moves hotkey subscribers off the OS callback while preserving press/release order.</summary>
public sealed class HotkeyEventDispatcher : IDisposable
{
    private readonly Channel<Action> _events = Channel.CreateUnbounded<Action>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

    /// <summary>Starts the background event consumer.</summary>
    public HotkeyEventDispatcher() => _ = Task.Run(ConsumeAsync);

    /// <summary>Queues a notification without running subscriber work on the caller.</summary>
    public void Post(Action notification) => _events.Writer.TryWrite(notification);

    private async Task ConsumeAsync()
    {
        await foreach (var notification in _events.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try { notification(); }
            catch (Exception e) { Debug.WriteLine($"Hotkey subscriber failed: {e}"); }
        }
    }

    /// <summary>Stops accepting notifications and drains those already queued.</summary>
    public void Dispose() => _events.Writer.TryComplete();
}
