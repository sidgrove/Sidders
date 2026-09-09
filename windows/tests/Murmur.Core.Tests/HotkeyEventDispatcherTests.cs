using Murmur.Abstractions;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>Slow recording startup must never block or reorder keyboard notifications.</summary>
public sealed class HotkeyEventDispatcherTests
{
    [Fact]
    public async Task Slow_subscriber_does_not_block_posting_and_release_stays_after_press()
    {
        using var dispatcher = new HotkeyEventDispatcher();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        try
        {
            dispatcher.Post(() =>
            {
                entered.SetResult();
                release.Wait(TimeSpan.FromSeconds(5));
                calls.Add("press");
            });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            dispatcher.Post(() => { calls.Add("release"); done.SetResult(); });
            done.Task.IsCompleted.ShouldBeFalse();
            release.Set();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
            calls.ShouldBe(["press", "release"]);
        }
        finally { release.Set(); }
    }
}
