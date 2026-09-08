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
