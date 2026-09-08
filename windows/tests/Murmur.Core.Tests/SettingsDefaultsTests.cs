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

/// <summary>Chord settings survive a round trip and default to a bare key.</summary>
public sealed class ChordSettingsTests
{
    [Fact]
    public void Modifiers_default_to_none_and_round_trip()
    {
        new SettingsData().PushToTalkModifiers.ShouldBe(0);

        var data = new SettingsData { PushToTalkKey = 0x20, PushToTalkModifiers = (int)(Murmur.Abstractions.HotkeyModifiers.Control | Murmur.Abstractions.HotkeyModifiers.Shift) };
        var json = JsonSerializer.Serialize(data, SettingsJsonContext.Default.SettingsData);
        var back = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsData);
        back.ShouldNotBeNull().PushToTalkModifiers.ShouldBe(3);
        back.PushToTalkKey.ShouldBe(0x20);
    }
}
