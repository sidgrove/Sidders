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

/// <summary>The clean-up guard: a model that summarises never reaches the text field.</summary>
public sealed class CleanupGuardTests
{
    [Fact]
    public void Short_utterances_are_not_worth_a_round_trip()
    {
        CleanupGuard.IsWorthCleaning("one two three four").ShouldBeFalse();
        CleanupGuard.IsWorthCleaning("one two three four five").ShouldBeTrue();
    }

    [Theory]
    [InlineData("um so can you uh send me the the Q2 numbers by friday", "Can you send me the Q2 numbers by Friday", true)]
    [InlineData("um so hello there", "Hello there", true)]
    [InlineData("I think this is fine and we should go ahead with the plan as discussed", "Go ahead.", false)]
    [InlineData("one two one two", "", false)]
    [InlineData("one two one two", null, false)]
    [InlineData("sounds good", "Sounds good, I will send it over to you first thing tomorrow morning", false)]
    public void Rewrites_are_rejected(string raw, string? cleaned, bool plausible) =>
        CleanupGuard.IsPlausible(raw, cleaned).ShouldBe(plausible);
}
