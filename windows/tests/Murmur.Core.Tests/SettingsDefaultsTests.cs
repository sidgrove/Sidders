using System.Text.Json;
using Murmur.Core;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>A settings file written by an older build must not switch new features off.</summary>
public sealed class SettingsDefaultsTests
{
    [Fact]
    public void Missing_properties_take_the_record_defaults()
    {
        const string old = """{"PushToTalkKey":124,"ModelDirectory":null,"InjectText":true,"KeepHistory":true}""";
        var data = JsonSerializer.Deserialize(old, SettingsJsonContext.Default.SettingsData);

        data.ShouldNotBeNull();
        data.IsEnabled.ShouldBeTrue("IsEnabled must default on");
        data.DropSingleSentenceFullStop.ShouldBeTrue();
        data.PushToTalkKey.ShouldBe(124);
    }
}
