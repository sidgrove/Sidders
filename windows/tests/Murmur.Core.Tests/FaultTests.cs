using Xunit;
using System.Runtime.CompilerServices;
using Murmur.Abstractions;
using Murmur.Core;
using Murmur.Testing;
using Shouldly;

namespace Murmur.CoreTests;

/// <summary>Keeps the test run's log out of the user's real log file.</summary>
internal static class TestLog
{
    [ModuleInitializer]
    internal static void Redirect() =>
        Log.Path = Path.Combine(Path.GetTempPath(), $"murmur-tests-{Environment.ProcessId}.log");
}

/// <summary>
/// The engine's failure paths. Every one of these was silent before the first real-hardware
/// run; now each must produce a sentence and return the engine to Idle.
/// </summary>
public sealed class FaultTests
{
    private static async Task SettleAsync(DictationEngine engine)
    {
        for (var i = 0; i < 20000 && engine.State != DictationState.Idle; i++) await Task.Yield();
    }

    [Fact]
    public async Task A_capture_that_throws_becomes_a_fault_and_the_engine_returns_to_idle()
    {
        var hotkey = new FakeHotkeySource();
        var faults = new List<string>();
        await using var engine = new DictationEngine(
            new ThrowingCapture(), hotkey, new FakeTranscriber("x"), new RecordingTextInjector(), () => []);
        engine.Faulted += (_, m) => faults.Add(m);

        hotkey.Press();
        await SettleAsync(engine);

        engine.State.ShouldBe(DictationState.Idle);
        faults.ShouldHaveSingleItem().ShouldContain("microphone");
        engine.LastFault.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_blocked_microphone_is_reported_in_words_rather_than_as_empty_text()
    {
        var hotkey = new FakeHotkeySource();
        var capture = FakeAudioCapture.Silence(0.8);
        capture.LooksLikeBlockedMicrophone = true;
        var injector = new RecordingTextInjector();
        var faults = new List<string>();

        await using var engine = new DictationEngine(capture, hotkey, new FakeTranscriber("hello"), injector, () => []);
        engine.Faulted += (_, m) => faults.Add(m);

        hotkey.Press();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        await SettleAsync(engine);

        faults.ShouldHaveSingleItem().ShouldBe(DictationEngine.BlockedMicrophoneMessage);
        injector.Injected.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_model_is_reported_and_nothing_is_typed()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        var faults = new List<string>();

        await using var engine = new DictationEngine(
            FakeAudioCapture.Tone(0.6), hotkey, new NeverReadyTranscriber(), injector, () => []);
        engine.Faulted += (_, m) => faults.Add(m);

        hotkey.Press();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        await SettleAsync(engine);

        faults.ShouldHaveSingleItem().ShouldBe(DictationEngine.ModelMissingMessage);
        injector.Injected.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_model_is_loaded_before_the_first_transcription()
    {
        // Found on hardware: nothing called LoadAsync, so every transcript was empty.
        var hotkey = new FakeHotkeySource();
        var transcriber = new FakeTranscriber("loaded fine");
        var injector = new RecordingTextInjector();

        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, transcriber, injector, () => []);
        transcriber.IsReady.ShouldBeFalse();

        hotkey.Press();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        await SettleAsync(engine);

        transcriber.IsReady.ShouldBeTrue();
        injector.Injected.ShouldBe(["loaded fine"]);
    }

    [Fact]
    public async Task InjectText_off_keeps_the_history_but_types_nothing()
    {
        var hotkey = new FakeHotkeySource();
        var injector = new RecordingTextInjector();
        DictationResult? completed = null;

        await using var engine = new DictationEngine(FakeAudioCapture.Tone(0.6), hotkey, new FakeTranscriber("kept"), injector, () => [])
        {
            InjectText = false,
        };
        engine.Completed += (_, r) => completed = r;

        hotkey.Press();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        await SettleAsync(engine);

        completed.ShouldNotBeNull().Text.ShouldBe("kept");
        injector.Injected.ShouldBeEmpty();
    }

    [Fact]
    public void A_hook_that_will_not_install_is_a_fault()
    {
        var faults = new List<string>();
        var engine = new DictationEngine(
            FakeAudioCapture.Silence(0.1), new DeadHotkey(), new FakeTranscriber("x"), new RecordingTextInjector(), () => []);
        engine.Faulted += (_, m) => faults.Add(m);

        engine.Start().ShouldBeFalse();
        engine.IsHotkeyArmed.ShouldBeFalse();
        faults.ShouldHaveSingleItem().ShouldContain("hook");
    }

    private sealed class ThrowingCapture : IAudioCapture
    {
        public bool IsCapturing => false;
        public bool LooksLikeBlockedMicrophone => false;

        public async IAsyncEnumerable<AudioChunk> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("Could not open the microphone in any supported format.");
#pragma warning disable CS0162 // Unreachable — the iterator needs a yield to be an iterator.
            yield break;
#pragma warning restore CS0162
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NeverReadyTranscriber : ITranscriber
    {
        public bool IsReady => false;
        public ValueTask<bool> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);
        public ValueTask<string> TranscribeAsync(ReadOnlyMemory<float> samples, IReadOnlyList<string> biasPhrases, CancellationToken cancellationToken) =>
            ValueTask.FromResult("should never be called");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DeadHotkey : IHotkeySource
    {
        public event EventHandler? Pressed { add { } remove { } }
        public event EventHandler? Released { add { } remove { } }
        public event EventHandler? CancelPressed { add { } remove { } }
        public int VirtualKey { get; set; }
        public int Modifiers { get; set; }
        public event EventHandler<(int VirtualKey, int Modifiers)>? Captured { add { } remove { } }
        public void BeginCapture() { }
        public void CancelCapture() { }
        public bool Start() => false;
        public void StopListening() { }
        public void Dispose() { }
    }
}

/// <summary>The lazily-resolved transcriber that lets a model arrive after startup.</summary>
public sealed class ReloadableTranscriberTests
{
    [Fact]
    public async Task Reports_not_ready_until_a_model_directory_exists_then_loads_it()
    {
        string? directory = null;
        var created = 0;
        await using var transcriber = new ReloadableTranscriber(() => directory, _ => { created++; return new FakeTranscriber("ok"); });

        (await transcriber.LoadAsync(CancellationToken.None)).ShouldBeFalse();
        transcriber.IsReady.ShouldBeFalse();
        created.ShouldBe(0);

        directory = "C:/models/parakeet";
        (await transcriber.LoadAsync(CancellationToken.None)).ShouldBeTrue();
        transcriber.IsReady.ShouldBeTrue();
        transcriber.ModelDirectory.ShouldBe(directory);
        created.ShouldBe(1);

        // Loading again does not build a second engine.
        (await transcriber.LoadAsync(CancellationToken.None)).ShouldBeTrue();
        created.ShouldBe(1);
    }

    [Fact]
    public async Task Transcribe_returns_empty_when_there_is_no_model()
    {
        await using var transcriber = new ReloadableTranscriber(() => null, _ => new FakeTranscriber("never"));
        var text = await transcriber.TranscribeAsync(new float[100], [], CancellationToken.None);
        text.ShouldBe(string.Empty);
    }
}

/// <summary>Taps and silence never reach the model — seen on hardware as typed "Mm-hmm."</summary>
public sealed class TapGuardTests
{
    private static async Task RunAsync(FakeAudioCapture capture, FakeTranscriber transcriber, RecordingTextInjector injector)
    {
        var hotkey = new FakeHotkeySource();
        await using var engine = new DictationEngine(capture, hotkey, transcriber, injector, () => []);
        hotkey.Press();
        for (var i = 0; i < 20000 && engine.State == DictationState.Recording && !capture.IsCapturing; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level == 0 && engine.State == DictationState.Recording; i++) await Task.Yield();
        for (var i = 0; i < 20000 && engine.Level > 0; i++) await Task.Yield();
        hotkey.Release();
        for (var i = 0; i < 20000 && engine.State != DictationState.Idle; i++) await Task.Yield();
    }

    [Fact]
    public async Task A_key_tap_is_not_transcribed()
    {
        var transcriber = new FakeTranscriber("Mm-hmm.");
        var injector = new RecordingTextInjector();
        await RunAsync(FakeAudioCapture.Tone(0.2), transcriber, injector);

        transcriber.SegmentLengths.ShouldBeEmpty();
        injector.Injected.ShouldBeEmpty();
    }

    [Fact]
    public async Task Long_silence_is_not_transcribed()
    {
        var transcriber = new FakeTranscriber("Mm-hmm.");
        var injector = new RecordingTextInjector();
        await RunAsync(FakeAudioCapture.Silence(2.0), transcriber, injector);

        transcriber.SegmentLengths.ShouldBeEmpty();
        injector.Injected.ShouldBeEmpty();
    }
}
