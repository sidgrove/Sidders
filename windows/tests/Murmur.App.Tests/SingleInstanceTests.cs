using Murmur.App;
using Shouldly;
using Xunit;

namespace Murmur.AppTests;

/// <summary>A second launch must raise the first instance's window, not vanish.</summary>
public sealed class SingleInstanceTests
{
    [Fact]
    public void Second_claim_fails_and_asks_the_first_to_show()
    {
        var name = $"SiddersTest.{Guid.NewGuid():N}";
        using var shown = new ManualResetEventSlim();

        using var first = SingleInstance.TryClaim(name);
        first.ShouldNotBeNull();
        first.ShowRequested += (_, _) => shown.Set();

        using var second = SingleInstance.TryClaim(name);
        second.ShouldBeNull("only one instance may run");

        shown.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue("the running instance must be asked to show its window");
    }

    [Fact]
    public void Releasing_the_first_lets_a_later_launch_claim()
    {
        var name = $"SiddersTest.{Guid.NewGuid():N}";

        var first = SingleInstance.TryClaim(name);
        first.ShouldNotBeNull();
        first.Dispose();

        using var later = SingleInstance.TryClaim(name);
        later.ShouldNotBeNull();
    }
}
