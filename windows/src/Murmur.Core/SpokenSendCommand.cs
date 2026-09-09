using System.Text.RegularExpressions;

namespace Murmur.Core;

/// <summary>Separates an explicit terminal send command from dictated content.</summary>
public static class SpokenSendCommand
{
    /// <summary>Recognises a command only when it is the entire utterance, apart from punctuation.</summary>
    public static bool IsStandalone(string text, string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return false;
        var pattern = @"\A\s*" + Regex.Escape(phrase.Trim()) + @"[\s.!?,;:]*\z";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }
    /// <summary>Matches the send word or a configured alternative only at the end of dictation.</summary>
    public static (string Text, bool Send) Extract(string text, string? sendWord, string? aliases = null)
    {
        if (string.IsNullOrWhiteSpace(sendWord)) return (text, false);
        var words = (aliases ?? string.Empty).Split([',', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Append(sendWord.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(word => word.Length).Select(Regex.Escape);
        var command = "(?:" + string.Join('|', words) + ")";
        var pattern = @"(?<![\p{L}\p{N}_])" + command + @"[\s.!?,;:]*\z";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        if (!match.Success) return (text, false);
        return (text[..match.Index].TrimEnd(' ', '\t', '\r', '\n', ',', ';', ':', '-', '—', '–'), true);
    }
}