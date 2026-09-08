using Murmur.Core;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>The rules that tidy a transcript without rewording it.</summary>
public sealed class TranscriptPolishTests
{
    [Theory]
    [InlineData("Can you send me the Q2 numbers.", "Can you send me the Q2 numbers")]
    [InlineData("Sounds good.", "Sounds good")]
    [InlineData("Sounds good.  ", "Sounds good")]
    public void A_lone_sentence_loses_its_full_stop(string input, string expected) =>
        TranscriptPolish.DropTrailingFullStopIfSingleSentence(input).ShouldBe(expected);

    [Theory]
    [InlineData("First thing. Second thing.")]
    [InlineData("Is that right?")]
    [InlineData("Brilliant!")]
    [InlineData("Wait for it...")]
    [InlineData("No punctuation at all")]
    [InlineData("")]
    public void Prose_questions_exclamations_and_ellipses_are_left_alone(string input) =>
        TranscriptPolish.DropTrailingFullStopIfSingleSentence(input).ShouldBe(input);

    [Fact]
    public void The_rule_can_be_switched_off()
    {
        TranscriptPolish.Apply("Sounds good.", dropSingleSentenceFullStop: false).ShouldBe("Sounds good.");
        TranscriptPolish.Apply("Sounds good.", dropSingleSentenceFullStop: true).ShouldBe("Sounds good");
    }
}
