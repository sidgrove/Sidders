using Murmur.Core;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>The rules layer that makes local-only dictation feel finished.</summary>
public sealed class SpokenFormattingTests
{
    [Theory]
    [InlineData("Um, so can you send me the numbers?", "So can you send me the numbers?")]
    [InlineData("I think, er, it's fine.", "I think, it's fine.")]
    [InlineData("Hmm. Let me see.", "Let me see.")]
    [InlineData("Summer is here.", "Summer is here.")]
    [InlineData("The drummer was late.", "The drummer was late.")]
    [InlineData("It's 5 mm wide.", "It's 5 mm wide.")]
    [InlineData("It's 5mm wide.", "It's 5mm wide.")]
    [InlineData("She went to the ER last night.", "She went to the ER last night.")]
    [InlineData("Mm, I think so.", "I think so.")]
    public void Fillers_go_but_real_words_stay(string input, string expected) =>
        SpokenFormatting.Apply(input).ShouldBe(expected);

    [Theory]
    [InlineData("Send it by Friday. Scratch that. Send it by Thursday.", "Send it by Thursday.")]
    [InlineData("Buy milk, scratch that, buy water", "Buy water")]
    [InlineData("First point. Second point, delete that.", "First point.")]
    public void Scratch_that_removes_the_clause_before_it(string input, string expected) =>
        SpokenFormatting.Apply(input).ShouldBe(expected);

    [Theory]
    [InlineData("Thanks Dave new line see you Monday", "Thanks Dave\nSee you Monday")]
    [InlineData("Thanks Dave. New line. See you Monday.", "Thanks Dave.\nSee you Monday.")]
    [InlineData("Intro, new paragraph, body", "Intro\n\nBody")]
    public void Breaks_become_line_breaks_and_recapitalise(string input, string expected) =>
        SpokenFormatting.Apply(input).ShouldBe(expected);

    [Theory]
    [InlineData("Are you coming question mark", "Are you coming?")]
    [InlineData("One comma two comma three full stop", "One, two, three.")]
    [InlineData("Wait dash really", "Wait – really")]
    [InlineData("He said open quote no close quote", "He said “no”")]
    public void Spoken_punctuation_becomes_marks(string input, string expected) =>
        SpokenFormatting.Apply(input).ShouldBe(expected);

    [Fact]
    public void Everything_can_be_switched_off()
    {
        const string text = "Um, new line, comma";
        SpokenFormatting.Apply(text, commands: false, fillers: false).ShouldBe(text);
    }

    [Fact]
    public void Plain_prose_is_untouched()
    {
        const string text = "Hi Dave, just checking in on the Sidders app. It's looking really good now, I think.";
        SpokenFormatting.Apply(text).ShouldBe(text);
    }
}
