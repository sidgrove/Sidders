namespace Murmur.Core;

/// <summary>
/// Small, deterministic tidying of a transcript before it is typed.
/// </summary>
/// <remarks>
/// <para>
/// Parakeet was trained on complete sentences, so it closes every utterance with a full
/// stop — including "can you send me the Q2 numbers", which the user is about to follow with
/// more typing in a chat box. This is the rules layer that knows the difference. It is not
/// a language model: it never rewords, never removes a word, and every rule here is
/// something a person can predict.
/// </para>
/// <para>Shared contract in spirit with the macOS <c>RuleBasedFormatter</c>.</para>
/// </remarks>
public static class TranscriptPolish
{
    /// <summary>
    /// Drops a trailing full stop when the transcript is a single sentence.
    /// </summary>
    /// <remarks>
    /// A single sentence is a fragment or a chat message; a full stop there is noise the
    /// user has to delete. Two or more sentences read as prose and keep their punctuation.
    /// Question marks and exclamation marks always stay — they carry meaning.
    /// </remarks>
    public static string DropTrailingFullStopIfSingleSentence(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0 || trimmed[^1] != '.') return text;

        // "..." is deliberate; leave it.
        if (trimmed.Length >= 2 && trimmed[^2] == '.') return text;

        // Any earlier sentence terminator means this is prose.
        var body = trimmed[..^1];
        if (body.Any(static c => c is '.' or '?' or '!')) return text;

        return body;
    }

    /// <summary>Applies the enabled rules.</summary>
    public static string Apply(string text, bool dropSingleSentenceFullStop) =>
        dropSingleSentenceFullStop ? DropTrailingFullStopIfSingleSentence(text) : text;
}

/// <summary>
/// Sanity checks on what a generative clean-up returns, so a model that summarises or
/// invents never reaches the user's text field.
/// </summary>
public static class CleanupGuard
{
    /// <summary>Utterances with fewer words than this are not worth a round trip.</summary>
    public const int MinimumWords = 3;

    /// <summary>A result with fewer than this share of the input's words was summarised.</summary>
    public const double MinimumRatio = 0.55;

    /// <summary>A result with more than this share of the input's words had things added.</summary>
    public const double MaximumRatio = 1.6;

    /// <summary>
    /// Words a tidy may drop or add regardless of ratio, so "um so hello there" can
    /// become "Hello there" without tripping the summarising check.
    /// </summary>
    public const int Slack = 2;

    /// <summary>Whether the raw text is long enough to be worth cleaning.</summary>
    public static bool IsWorthCleaning(string raw) => Words(raw) >= MinimumWords;

    /// <summary>
    /// Whether <paramref name="cleaned"/> is a plausible tidy of <paramref name="raw"/>
    /// rather than a rewrite. Null or blank is never accepted.
    /// </summary>
    public static bool IsPlausible(string raw, string? cleaned)
    {
        if (string.IsNullOrWhiteSpace(cleaned)) return false;

        var before = Words(raw);
        var after = Words(cleaned);

        var floor = Math.Min(before - Slack, (int)Math.Ceiling(before * MinimumRatio));
        var ceiling = Math.Max(before + Slack, (int)Math.Floor(before * MaximumRatio));
        return after >= Math.Max(1, floor) && after <= ceiling;
    }

    private static int Words(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
