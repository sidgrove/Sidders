using System.Net;
using System.Text;
using System.Text.Json;
using Murmur.Abstractions;
using Murmur.Core;
using Murmur.Speech;
using Murmur.Testing;
using Shouldly;
using Xunit;

namespace Murmur.CoreTests;

/// <summary>The generative tier, against a fake Gemini, and the engine's use of it.</summary>
public sealed class GeminiCleanerTests
{
    private static GeminiCleaner Build(FakeGemini server, string? key = "test-key") =>
        new(() => key, null, server);

    [Fact]
    public async Task Sends_the_instructions_and_text_and_returns_the_reply()
    {
        var server = new FakeGemini(reply: "Can you send me the Q2 numbers by Thursday");
        using var cleaner = Build(server);

        var cleaned = await cleaner.CleanAsync("um can you uh send me the the Q2 numbers by friday scratch that by thursday", CancellationToken.None);

        cleaned.ShouldBe("Can you send me the Q2 numbers by Thursday");
        // The full URL, not a substring: the old (base, relative) construction produced a URI
        // whose *scheme* was "gemini-2.5-flash", which contained the substring and passed.
        server.LastRequestUri!.ToString().ShouldBe("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
        server.LastRequestUri.Scheme.ShouldBe("https");
        server.LastKeyHeader.ShouldBe("test-key");
        server.LastBody.ShouldContain("systemInstruction");
        server.LastBody.ShouldContain("scratch that");
        server.LastBody.ShouldContain("\"thinkingBudget\":0");
    }

    [Fact]
    public async Task No_key_means_no_call_and_null()
    {
        var server = new FakeGemini(reply: "never");
        using var cleaner = Build(server, key: null);

        // Environment may carry a key on a developer machine; only assert when it does not.
        if (Environment.GetEnvironmentVariable(GeminiCleaner.ApiKeyEnvironmentVariable) is { Length: > 0 }) return;

        (await cleaner.CleanAsync("hello", CancellationToken.None)).ShouldBeNull();
        server.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task An_error_status_returns_null_and_keeps_the_message()
    {
        var server = new FakeGemini(status: HttpStatusCode.BadRequest, rawBody: "{\"error\":{\"message\":\"API key not valid\"}}");
        using var cleaner = Build(server);

        (await cleaner.CleanAsync("hello", CancellationToken.None)).ShouldBeNull();
        cleaner.LastError.ShouldNotBeNull().ShouldContain("API key not valid");
    }

    [Fact]
    public async Task An_empty_candidate_list_returns_null()
    {
        var server = new FakeGemini(rawBody: "{\"candidates\":[]}");
        using var cleaner = Build(server);

        (await cleaner.CleanAsync("hello", CancellationToken.None)).ShouldBeNull();
    }

    /// <summary>A stand-in for the Generative Language endpoint.</summary>
    private sealed class FakeGemini : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public FakeGemini(string reply) : this(HttpStatusCode.OK, Wrap(reply)) { }

        public FakeGemini(HttpStatusCode status = HttpStatusCode.OK, string rawBody = "{}")
        {
            _status = status;
            _body = rawBody;
        }

        public int Requests { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastKeyHeader { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        private static string Wrap(string reply) =>
            JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = reply } } } } } });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequestUri = request.RequestUri;
            LastKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.First() : null;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }
}

/// <summary>How the engine uses a cleaner, and the tap-to-toggle mode.</summary>
public sealed class EngineCleanupAndToggleTests
{
    private static async Task DrainAndReleaseAsync(FakeHotkeySource hotkey, DictationEngine engine)
    {
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        for (var i = 0; i < 20000 && engine.State != DictationState.Idle; i++) await Task.Yield();
    }

    [Fact]
    public async Task Cleaned_text_is_typed_and_recorded_when_the_tier_is_on()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        DictationResult? completed = null;

        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("um so hello there."), injector, () => [])
        {
            AiCleanup = true,
            Cleaner = new StubCleaner("Hello there"),
        };
        engine.Completed += (_, r) => completed = r;

        hotkey.Press();
        await DrainAndReleaseAsync(hotkey, engine);

        injector.Injected.ShouldBe(["Hello there"]);
        completed.ShouldNotBeNull().CleanedBy.ShouldBe("stub");
    }

    [Fact]
    public async Task A_failed_cleanup_types_the_local_text_and_reports_it()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        var faults = new List<string>();

        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("hello there, how are you"), injector, () => [])
        {
            AiCleanup = true,
            Cleaner = new StubCleaner(null),
        };
        engine.Faulted += (_, m) => faults.Add(m);

        hotkey.Press();
        await DrainAndReleaseAsync(hotkey, engine);

        injector.Injected.ShouldBe(["hello there, how are you"]);
        faults.ShouldHaveSingleItem().ShouldContain("raw transcript");
    }

    [Fact]
    public async Task The_cleaner_is_not_called_when_the_tier_is_off()
    {
        var hotkey = new FakeHotkeySource();
        var cleaner = new StubCleaner("should not appear");
        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("raw"), new RecordingTextInjector(), () => [])
        {
            AiCleanup = false,
            Cleaner = cleaner,
        };

        hotkey.Press();
        await DrainAndReleaseAsync(hotkey, engine);

        cleaner.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Tap_to_toggle_starts_on_one_press_and_stops_on_the_next()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("toggled"), injector, () => [])
        {
            TapToToggle = true,
        };

        hotkey.Press();
        hotkey.Release();   // ignored in toggle mode
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        engine.State.ShouldBe(DictationState.Recording, "release must not stop a toggled recording");

        hotkey.Press();     // second tap stops
        hotkey.Release();
        for (var i = 0; i < 20000 && engine.State != DictationState.Idle; i++) await Task.Yield();

        injector.Injected.ShouldBe(["toggled"]);
    }

    private sealed class StubCleaner(string? reply) : ITranscriptCleaner
    {
        public int Calls { get; private set; }
        public string Name => "stub";
        public Task<string?> CleanAsync(string text, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(reply);
        }
    }
}

/// <summary>Recording a chord goes through the hook, not a window.</summary>
public sealed class ChordCaptureTests
{
    [Fact]
    public async Task Capture_reports_the_chord_and_the_engine_applies_it()
    {
        var hotkey = new FakeHotkeySource();
        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("x"), new RecordingTextInjector(), () => []);

        (int Key, int Mods)? got = null;
        engine.Captured += (_, chord) => got = chord;

        engine.BeginCapture();
        hotkey.IsCapturing.ShouldBeTrue();
        hotkey.Capture(0x20, (int)(Murmur.Abstractions.HotkeyModifiers.Control | Murmur.Abstractions.HotkeyModifiers.Alt));

        got.ShouldNotBeNull();
        got.Value.Key.ShouldBe(0x20);
        got.Value.Mods.ShouldBe(5);

        engine.HotkeyVirtualKey = got.Value.Key;
        engine.HotkeyModifiers = got.Value.Mods;
        hotkey.VirtualKey.ShouldBe(0x20);
        hotkey.Modifiers.ShouldBe(5);
    }
}

/// <summary>The engine keeps the raw transcript when the cleaner rewrites it.</summary>
public sealed class CleanupGuardInEngineTests
{
    [Fact]
    public async Task A_summarising_cleaner_is_ignored_without_a_fault()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        var faults = new List<string>();
        const string raw = "I think this is fine and we should go ahead with the plan as discussed";

        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber(raw), injector, () => [])
        {
            AiCleanup = true,
            Cleaner = new Summariser(),
        };
        engine.Faulted += (_, m) => faults.Add(m);
        DictationResult? completed = null;
        engine.Completed += (_, r) => completed = r;

        hotkey.Press();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        for (var i = 0; i < 20000 && engine.State != DictationState.Idle; i++) await Task.Yield();

        injector.Injected.ShouldBe([raw]);
        completed.ShouldNotBeNull().CleanedBy.ShouldBeNull();
        faults.ShouldBeEmpty();
    }

    private sealed class Summariser : ITranscriptCleaner
    {
        public string Name => "summariser";
        public Task<string?> CleanAsync(string text, CancellationToken cancellationToken) => Task.FromResult<string?>("Go ahead.");
    }
}
