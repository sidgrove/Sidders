using Murmur.Core;
using Shouldly;
using Xunit;

namespace Murmur.Core.Tests;

public sealed class FeedbackSoundsTests
{
    [Fact]
    public void Cues_follow_transitions_and_live_preferences()
    {
        var settings = new SettingsData { RecordingSounds = true, SendSound = false };
        var played = new List<byte[]>();
        var feedback = new FeedbackSounds(() => settings, played.Add);
        feedback.Observe(DictationState.Idle);
        feedback.Observe(DictationState.Recording);
        feedback.Observe(DictationState.Recording);
        feedback.Observe(DictationState.Transcribing);
        feedback.Observe(DictationState.Idle);
        feedback.Sent();
        played.Count.ShouldBe(2);
        played[0].ShouldNotBe(played[1]);
        settings.RecordingSounds = false;
        feedback.Observe(DictationState.Recording);
        feedback.Observe(DictationState.Idle);
        played.Count.ShouldBe(2);
        settings.SendSound = true;
        feedback.Sent();
        played.Count.ShouldBe(3);
        System.Text.Encoding.ASCII.GetString(played[2], 0, 4).ShouldBe("RIFF");
    }
}
