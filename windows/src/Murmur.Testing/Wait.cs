namespace Murmur.Testing;

/// <summary>Condition-based waiting for engine tests.</summary>
/// <remarks>
/// A wait counted in <c>Task.Yield</c>s is a wait measured in scheduler luck: on a loaded CI
/// runner twenty thousand yields can pass before the engine has moved at all, and the test
/// then releases a key nobody is holding. A deadline in wall-clock time is what the intent
/// actually is.
/// </remarks>
public static class Wait
{
    /// <summary>How long a condition is given before the test is allowed to fail on its own assertion.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Polls until <paramref name="condition"/> is true or <see cref="Timeout"/> passes.</summary>
    /// <returns>True if the condition was met.</returns>
    public static async Task<bool> UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) return false;
            await Task.Delay(1);
        }

        return true;
    }
}
