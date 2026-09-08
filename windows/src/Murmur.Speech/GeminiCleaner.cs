using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Murmur.Abstractions;

namespace Murmur.Speech;

/// <summary>
/// The generative clean-up tier, on Gemini.
/// </summary>
/// <remarks>
/// <para>
/// Calls the Generative Language REST endpoint directly rather than through an SDK — the
/// same way the Sidgrove Intelligence app does — so there is one small request shape to
/// understand and nothing to trim out of the single-file bundle. <c>gemini-2.5-flash</c>
/// with thinking switched off: this is a rewrite, not a reasoning task, and every hundred
/// milliseconds is felt between key-up and text.
/// </para>
/// <para>
/// The prompt is the whole product here. It is written to <i>subtract</i> — fillers, false
/// starts, spoken editing commands — and never to add, summarise or answer. Proper nouns
/// arrive already corrected by the dictionary and are to be preserved verbatim.
/// </para>
/// <para>
/// Any failure — no key, network, a refusal, an empty reply — returns null and the caller
/// types the local text. The user never loses a dictation to the cloud being down.
/// </para>
/// </remarks>
public sealed class GeminiCleaner : ITranscriptCleaner, IDisposable
{
    /// <summary>The model used unless settings say otherwise.</summary>
    public const string DefaultModel = "gemini-2.5-flash";

    /// <summary>Environment variable consulted when no key is set in the app.</summary>
    public const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    /// <summary>How long a clean-up may take before the raw text is typed instead.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(8);

    /// <summary>What the model is asked to do. Public so Settings can show it.</summary>
    public const string Instructions =
        "You turn dictated speech into the text the speaker meant to type. "
        + "Remove filler words (um, uh, er, you know, sort of, like — when used as filler), "
        + "false starts, stutters and immediately repeated words. "
        + "Apply spoken editing commands: \"scratch that\", \"delete that\" or \"no wait\" removes the "
        + "clause or sentence just before it; \"new line\" and \"new paragraph\" become line breaks; "
        + "\"bullet points\" or \"number one, number two\" become a list; \"full stop\", \"comma\", "
        + "\"question mark\" and \"open/close brackets\" become the punctuation named. "
        + "Fix punctuation, capitalisation and obvious homophones from context. "
        + "Use British English spelling. "
        + "Keep the speaker's own words, tone and meaning: do not add content, do not summarise, "
        + "do not answer questions in the text, do not turn a casual message formal. "
        + "Proper nouns, product names and people's names are already spelled correctly — keep them exactly. "
        + "If the text is a single short sentence or fragment, do not end it with a full stop. "
        + "Output only the cleaned text: no quotation marks, no preamble, no explanation.";

    private static readonly Uri BaseUri = new("https://generativelanguage.googleapis.com/v1beta/models/");

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKey;
    private readonly string _model;

    /// <summary>Creates a cleaner that reads the key each call, so a key pasted into Settings works immediately.</summary>
    /// <param name="apiKey">Returns the key, or null/empty when none is configured.</param>
    /// <param name="model">Model id; defaults to <see cref="DefaultModel"/>.</param>
    /// <param name="handler">Transport, for tests.</param>
    public GeminiCleaner(Func<string?> apiKey, string? model = null, HttpMessageHandler? handler = null)
    {
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = Deadline;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppPaths.ProductName, "1.0"));
    }

    /// <inheritdoc />
    public string Name => _model;

    /// <summary>The key in use: the configured one, else the environment variable.</summary>
    public static string? ResolveKey(string? configured) =>
        !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) is { Length: > 0 } fromEnv
                ? fromEnv.Trim()
                : null;

    /// <inheritdoc />
    public async Task<string?> CleanAsync(string text, CancellationToken cancellationToken)
    {
        var key = ResolveKey(_apiKey());
        if (key is null || string.IsNullOrWhiteSpace(text)) return null;

        var request = new GenerateRequest(
            SystemInstruction: new Content([new Part(Instructions)]),
            Contents: [new Content([new Part(text)])],
            GenerationConfig: new GenerationConfig(
                Temperature: 0.2,
                MaxOutputTokens: 2048,
                ThinkingConfig: new ThinkingConfig(0)));

        var body = JsonSerializer.Serialize(request, GeminiJsonContext.Default.GenerateRequest);

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, $"{_model}:generateContent"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-goog-api-key", key);

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"{(int)response.StatusCode} {response.ReasonPhrase}: {Excerpt(payload)}";
                return null;
            }

            var parsed = JsonSerializer.Deserialize(payload, GeminiJsonContext.Default.GenerateResponse);
            var candidates = parsed?.Candidates;
            var parts = candidates is { Count: > 0 } ? candidates[0].Content?.Parts : null;
            var reply = parts is { Count: > 0 } ? parts[0].Text : null;
            if (string.IsNullOrWhiteSpace(reply))
            {
                LastError = "empty reply";
                return null;
            }

            LastError = null;
            return reply.Trim();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            LastError = e is TaskCanceledException && !cancellationToken.IsCancellationRequested
                ? $"no reply within {Deadline.TotalSeconds:0} s"
                : e.Message;
            return null;
        }
    }

    /// <summary>Why the most recent call returned null, for the log and Settings.</summary>
    public string? LastError { get; private set; }

    private static string Excerpt(string payload)
    {
        // The API's error JSON carries a "message"; surface that rather than the whole blob.
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var msg))
            {
                return msg.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through.
        }

        return payload.Length > 200 ? payload[..200] : payload;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    // ---- Wire shapes. Source-generated so they survive single-file publishing. ----

    internal sealed record GenerateRequest(
        [property: JsonPropertyName("systemInstruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<Content> Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfig GenerationConfig);

    internal sealed record Content([property: JsonPropertyName("parts")] IReadOnlyList<Part> Parts);

    internal sealed record Part([property: JsonPropertyName("text")] string Text);

    internal sealed record GenerationConfig(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("thinkingConfig")] ThinkingConfig ThinkingConfig);

    internal sealed record ThinkingConfig([property: JsonPropertyName("thinkingBudget")] int ThinkingBudget);

    internal sealed record GenerateResponse([property: JsonPropertyName("candidates")] IReadOnlyList<Candidate>? Candidates);

    internal sealed record Candidate([property: JsonPropertyName("content")] Content? Content);
}

/// <summary>Source-generated JSON for the Gemini wire shapes.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GeminiCleaner.GenerateRequest))]
[JsonSerializable(typeof(GeminiCleaner.GenerateResponse))]
internal sealed partial class GeminiJsonContext : JsonSerializerContext;
