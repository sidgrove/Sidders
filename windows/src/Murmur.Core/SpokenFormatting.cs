using System.Text;
using System.Text.RegularExpressions;

namespace Murmur.Core;

/// <summary>
/// Deterministic spoken commands and filler removal, applied before any generative tier.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes local-only mode feel finished: "new line", "full stop" and "scratch
/// that" work without a network, and "um" never reaches the page. It also shrinks the job
/// the AI clean-up has left to do, which makes that tier more predictable.
/// </para>
/// <para>
/// Every rule is a plain pattern a person can predict. Parakeet already punctuates and
/// capitalises, so each command tolerates a comma or full stop on either side of it and the
/// result is re-capitalised after the breaks it introduces.
/// </para>
/// </remarks>
public static partial class SpokenFormatting
{
    /// <summary>Words dropped when they stand alone as hesitation.</summary>
    public static readonly IReadOnlyList<string> DefaultFillers = ["um", "umm", "uh", "uhh", "er", "erm", "hmm", "mm"];

    /// <summary>Applies everything.</summary>
    public static string Apply(string text, bool commands = true, bool fillers = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var result = text;
        if (fillers) result = RemoveFillers(result);
        if (commands)
        {
            result = ApplyScratchThat(result);
            result = ApplyPunctuationCommands(result);
            result = ApplyBreaks(result);
        }

        // Untouched text goes back exactly as it came: the speech model already capitalises,
        // and this layer only mends what its own edits disturbed.
        return result == text ? text : Tidy(result);
    }

    /// <summary>Removes standalone hesitation words and the punctuation that clung to them.</summary>
    public static string RemoveFillers(string text) => Filler().Replace(text, " ");

    /// <summary>
    /// "scratch that" and friends remove the clause spoken just before them, back to the
    /// previous sentence end or the start of the text.
    /// </summary>
    public static string ApplyScratchThat(string text)
    {
        var result = text;
        Match match;
        while ((match = Scratch().Match(result)).Success)
        {
            var before = result[..match.Index];
            var cut = LastClauseStart(before);
            result = string.Concat(before.AsSpan(0, cut), " ", result.AsSpan(match.Index + match.Length));
        }

        return result;
    }

    /// <summary>"full stop", "comma", "question mark" and so on become the mark itself.</summary>
    public static string ApplyPunctuationCommands(string text)
    {
        var result = PunctuationWord().Replace(text, m => MarkFor(m.Groups["word"].Value));
        return Spacing().Replace(result, "$1");
    }

    /// <summary>"new line" and "new paragraph" become breaks.</summary>
    public static string ApplyBreaks(string text)
    {
        var result = Paragraph().Replace(text, "\n\n");
        return Line().Replace(result, "\n");
    }

    private static int LastClauseStart(string before)
    {
        var trimmed = before.TrimEnd();
        for (var i = trimmed.Length - 1; i >= 0; i--)
        {
            if (trimmed[i] is '.' or '?' or '!' or '\n') return i + 1;
        }

        return 0;
    }

    private static string MarkFor(string word) => word.ToLowerInvariant() switch
    {
        "full stop" or "period" => ".",
        "comma" => ",",
        "question mark" => "?",
        "exclamation mark" or "exclamation point" => "!",
        "colon" => ":",
        "semicolon" or "semi colon" => ";",
        "open bracket" or "open parenthesis" => " (",
        "close bracket" or "close parenthesis" => ")",
        "open quote" or "open quotes" => " “",
        "close quote" or "close quotes" => "”",
        "dash" or "hyphen" => " – ",
        "ellipsis" or "dot dot dot" => "…",
        _ => word,
    };

    /// <summary>Collapses the spacing the substitutions leave and re-capitalises after breaks and sentence ends.</summary>
    private static string Tidy(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = MultiSpace().Replace(lines[i], " ").Trim();
            line = SpaceBeforeMark().Replace(line, "$1");
            line = DoubleMark().Replace(line, "$1");
            line = SpaceAfterOpener().Replace(line, "$1");
            lines[i] = Capitalise(line);
        }

        var joined = string.Join('\n', lines);
        return TripleBreak().Replace(joined, "\n\n").Trim();
    }

    private static string Capitalise(string line)
    {
        if (line.Length == 0) return line;

        var builder = new StringBuilder(line.Length);
        var atStart = true;
        foreach (var c in line)
        {
            builder.Append(atStart && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
            if (char.IsLetterOrDigit(c)) atStart = false;
            else if (c is '.' or '?' or '!') atStart = true;
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"(?<=^|[\s,.])(?:um+|uh+|er|erm|hmm+|mm+)\b[,.]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Filler();

    [GeneratedRegex(@"[,.]?\s*\b(?:scratch that|delete that|strike that)\b[,.]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Scratch();

    [GeneratedRegex(@"[,.]?\s*\b(?<word>full stop|period|comma|question mark|exclamation mark|exclamation point|colon|semicolon|semi colon|open bracket|close bracket|open parenthesis|close parenthesis|open quotes?|close quotes?|dash|hyphen|ellipsis|dot dot dot)\b[,.]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PunctuationWord();

    [GeneratedRegex(@"\s+([.,?!:;)”])", RegexOptions.CultureInvariant)]
    private static partial Regex Spacing();

    [GeneratedRegex(@",?\s*\b(?:new paragraph|next paragraph)\b[,.]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Paragraph();

    [GeneratedRegex(@",?\s*\b(?:new line|newline|next line|line break)\b[,.]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Line();

    [GeneratedRegex(@"([(“])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceAfterOpener();

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"\s+([.,?!:;])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforeMark();

    [GeneratedRegex(@"([.,?!:;])(?:\s*[.,])+", RegexOptions.CultureInvariant)]
    private static partial Regex DoubleMark();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex TripleBreak();
}
