# Acapella for Windows

Acapella is push-to-talk dictation for Windows, on-device. It began as a port of an open-source macOS app, and the project namespaces still carry that name (`Murmur.*`); the product is Acapella.

> **Status: running on real hardware since 2026-09-08.** Built, installed and driven on a
> Windows 11 machine: the hook arms, the model loads in ~1.3 s, the front panel, tray,
> Settings and model download all work. The first hardware session found three bugs CI
> could never see — see [What real hardware found](#hardware). See [Honesty](#honesty) for
> what is still unverified.

---

## Running it

```powershell
cd windows
.\publish.ps1      # self-contained win-x64 build into windows\dist, runs the self-test
.\install.ps1      # copies to %LOCALAPPDATA%\Programs\Acapella, Start menu, Apps entry, launches
```

No administrator rights at any point. `install.ps1 -Uninstall` removes it again and keeps the
model and history.

First run: open **Settings** (Ctrl+,), press **DOWNLOAD** under Model. It fetches the
~660 MB Parakeet model from Hugging Face once and loads it without a restart. Then hold
**Right Ctrl**, talk, release. Pick a microphone in the same window if the Windows default
is not the one you dictate into.

Everything the app writes lives in `%LOCALAPPDATA%\Acapella`: `settings.json`,
`dictionary.txt` (edit it by hand if you like), `transcripts.jsonl`, `acapella.log`, and
`models\`.

---

## Clipboard and spoken sending

Each completed dictation copies its final text to the clipboard as well as inserting it
into the focused app. Turning off **Type into the focused app** keeps clipboard copying
and optional history, but suppresses typing and Enter.

In **Settings → Writing**:

- **Send word**: a word at the end of the complete dictation removes itself and presses
  Enter after text insertion. The default is `blob`; choose `send` if preferred.
- **Also send if you hear**: comma-separated recognition alternatives, such as `sand`.
  These match only at the end of the complete dictation, not between sentences or paragraphs.
- **Send phrase**: the default phrase `send it` at the end of a dictation removes itself
  and sends the preceding text. Spoken on its own, it presses Enter without adding text
  or replacing the clipboard. Leave either command field blank to disable it.

A brief multicoloured wave confirms that Enter was sent. This is a keypress confirmation;
what Enter does depends on the focused application.

The recording overlay appears even when the main window is active. **Settings → Sound
effects** has separate switches for quiet recording start/stop notes and a send note.
The start note waits for microphone audio to arrive. Capture starts before other apps
are muted; the log records time to the first audio chunk for hardware diagnosis.

Closing the main window with **×** leaves Acapella running in the tray. **Quit Acapella**
stops it completely. Avoid running the older Sidders app alongside Acapella: both can
respond to the same dictation shortcut and insert overlapping text.

On 2026-09-09, the user verified automatic insertion, send-word removal, the send animation,
standalone `send it`, and dictation with the main window closed on Windows. The Windows
solution currently has 234 passing tests. The latest capture-start change still needs
a spoken hardware check; microphone privacy and unplugging a microphone remain separate checks.

---

## What the 2026-09-12 review changed

A full review of the engine, the Win32 layer and the UI, with every confirmed finding fixed
and pinned by a test in `tests/Murmur.Core.Tests/ResilienceTests.cs`:

- **Blocked-microphone flag was sticky.** Set once by 1.5 s of digital silence and never
  cleared, so a headset muted for a call faulted every later dictation. The flag now
  describes the stream *now*, and only a silent recording can raise the fault.
- **Preview decoded the whole buffer every 700 ms.** Past a minute that is gigabytes per
  tick, and past 400 s it throws. The preview now freezes text older than
  `DictationEngine.PreviewWindow` and re-decodes only the tail.
- **A microphone chosen in Settings was ignored** while the warm stream held the old one.
  `WarmAudioCapture.ReopenDevice` closes it at once when idle, or as the recording ends.
- **The warm stream's idle release raced a new recording** and could complete the new
  session with the dying pump. Pumps now carry an identity and never touch a successor.
- **The managed audio fallback spun forever.** `BufferedWaveProvider.ReadFully` defaults to
  true, so the drain loop never saw an empty sink. Latent on this machine (the native
  16 kHz float path is accepted) and fatal on any device that refuses it.
- **Lifting a held modifier used neutral key codes**, which Windows delivers as the *left*
  key: Right Ctrl stayed held through Enter and a phantom Left Ctrl stuck afterwards.
  Modifiers are now probed and re-sent side-specifically.
- **A surrogate pair at a chunk boundary was re-sent** as a lone low surrogate.
- **A Gemini reply cut off by the 2048-token cap was typed** as if complete. The cap is
  gone and any `finishReason` other than `STOP` falls back to the local text.
- **Settings, dictionary and history were written non-atomically**, on every keystroke.
  `AtomicFile` writes beside and renames; a corrupt settings file is set aside as
  `.corrupt` rather than silently replaced by defaults.
- **The two legacy settings switches re-migrated on every launch**, undoing a later choice.
  Migration now runs once. The duplicate full-stop switch in Behaviour is gone.
- **Stores were mutable lists shared across threads.** `TranscriptStore` and
  `DictionaryFile` are copy-on-write; readers get a snapshot.
- **Escape in the key recorder became the hotkey**, and a recorder left open swallowed the
  first key typed into another app. Escape now cancels at the hook, and leaving the window
  cancels too. The recorder also resubscribes when Settings is shown a second time.
- **Navigating away from Settings cancelled a model download.** It now cancels only when
  the window closes. Model status refreshes once the engine has loaded it.
- **The Gemini key saved only on focus loss**, so paste-then-Ctrl+Q lost it. Text fields
  save on a short debounce and are flushed at quit.
- **A missed key-up** (delivered to an elevated window) left Hold mode recording. The
  engine polls the physical key while recording in Hold mode.
- **Quitting mid-transcription disposed the model under a running decode.** Disposal now
  waits up to `DictationEngine.DisposeGrace`.
- **A stale ducking-recovery record disabled ducking forever**, silently. Records for a
  vanished output are dropped, records age out after a day, and the platform layer can
  finally log through `PlatformDiagnostics`.
- **A press during transcription was ignored.** Each recording is now its own session
  (buffer, token, preview), so the next one can start while the previous is still in the
  model or waiting on Gemini. Transcriptions are queued and typed in spoken order.
- Smaller: "mm" after a figure and "ER" are no longer removed as fillers; the overlay is
  click-through; the pill says "Nothing heard" instead of vanishing; "Clear all" takes two
  clicks; a paused app says so; the tray can retype the last transcript.

---
## <a id="hardware"></a>What real hardware found

Three bugs, none of which 63 green tests and a passing self-test could have caught:

1. **NAudio never shipped.** A class library does not copy its package assemblies to `bin`,
   and both the copy step and the publish step globbed `NAudio*.dll` from exactly that empty
   folder. Fixed with `CopyLocalLockFileAssemblies` in `Murmur.Platform.Windows.csproj`.
2. **Then NAudio could not be loaded even when present.** A host with a `deps.json` probes
   only the assemblies listed in it, and NAudio is deliberately unknown to `Murmur.App`. The
   resolver in `PlatformFactory` now loads any assembly that sits beside the executable.
3. **Nothing ever loaded the model.** No caller of `ITranscriber.LoadAsync` existed, so with
   the files on disk every transcript was empty. The engine now loads on first use and
   preloads at startup.

And one design gap: every one of those failed *silently*, inside `_ = BeginAsync()`. The
engine now logs to `acapella.log`, raises `Faulted`, and the front panel shows the message.

---

## Why this is a rewrite, not a port

Almost every layer of the macOS app is Apple-specific:

| Layer | macOS | Windows |
|---|---|---|
| UI | SwiftUI | Avalonia |
| Audio capture | `AVAudioEngine` | WASAPI via NAudio |
| **Default speech engine** | `SpeechAnalyzer` (ships with macOS 26) | **nothing equivalent exists** |
| Parakeet | FluidAudio → CoreML | sherpa-onnx → ONNX Runtime |
| Hotkey | `CGEventTap` | `SetWindowsHookEx(WH_KEYBOARD_LL)` |
| Text injection | Accessibility API | `SendInput` |

The consequence that shapes everything: **Windows has no counterpart to Apple's
`SpeechAnalyzer`.** On macOS, Parakeet is the optional upgrade. On Windows it is the only
engine, and the app cannot transcribe until the model is downloaded —
see [`docs/PARAKEET-WINDOWS.md`](../docs/PARAKEET-WINDOWS.md).

What *is* genuinely shared is the dictionary's behaviour, and it is shared as a contract
rather than as code: [`shared/dictionary-test-vectors.json`](../shared/dictionary-test-vectors.json).
Both implementations run those vectors in CI. Changing correction semantics starts there.

---

## Decisions, and why

**Avalonia, not WPF or WinUI 3.** WPF cannot be run or UI-tested on macOS, so every mistake
would cost a full CI round-trip. Avalonia's headless test platform runs on macOS in ~100 ms,
including simulated keyboard input and real pixel capture. Win32 interop is unaffected —
hooks, `SendInput` and WASAPI are P/Invoke, not UI-framework code. WinUI 3 was rejected
outright: Microsoft's own docs contradict each other on whether unpackaged single-file
publishing works, with open bugs reporting an exe that won't launch.

**.NET 10, not .NET 8.** .NET 8 reaches end-of-life on **2026-11-10**.

**Right Ctrl is the default hotkey, not Right Alt.** Right Alt is AltGr on German, Polish,
UK, Nordic and most Latin-American layouts — it is how those users type `@`, `€`, `\`, `|`.
Binding push-to-talk there would break basic typing for a large fraction of users. Right
Ctrl produces no character on any layout.

**The hotkey is observed, never swallowed.** The macOS build consumes Right Option because
on macOS that key types characters. On Windows, suppression buys nothing and risks a much
worse failure: if the key-down is swallowed but the key-up escapes — a hook that timed out
mid-gesture, or focus crossing into an elevated window — the target app believes Ctrl is
held down forever.

**CPU-only inference.** sherpa-onnx ships no GPU package; DirectML is five versions behind
and forbids the variable tensor shapes this model requires; CUDA would force every user to
install a toolkit. On CPU with int8 weights, transcription runs ~40× faster than real time.

**Three pinned versions that would break at "latest":**

| Package | Pinned | Why |
|---|---|---|
| `NAudio` | **2.3.0** | 3.x targets .NET 9+ and will not restore |
| `Avalonia.Headless.XUnit` | **11.3.20** | 12.x requires xUnit **v3**, a different package line |
| `org.k2fsa.sherpa.onnx` | 1.13.5 | Bundles ONNX Runtime; never also reference `Microsoft.ML.OnnxRuntime` |

---

## Layout

```
windows/
├─ Directory.Build.props          strict analysis, applied to every project
├─ Directory.Packages.props       central version pinning
├─ global.json                    SDK pin
├─ src/
│  ├─ Murmur.Dictionary/          corrections + biasing          net10.0
│  ├─ Murmur.Abstractions/        the four platform interfaces   net10.0
│  ├─ Murmur.Core/                engine, segmenter, storage     net10.0
│  ├─ Murmur.Speech/              Parakeet via sherpa-onnx       net10.0
│  ├─ Murmur.Testing/             fakes for the interfaces       net10.0
│  ├─ Murmur.App/                 Avalonia UI                    net10.0
│  └─ Murmur.Platform.Windows/    the ONLY Win32 code            net10.0-windows
└─ tests/
   ├─ Murmur.Dictionary.Tests/    the shared vectors             24 tests
   ├─ Murmur.Core.Tests/          engine, chunking, storage      26 tests
   └─ Murmur.App.Tests/           headless Avalonia UI           13 tests
```

**Only one project targets `-windows`.** Everything else is platform-neutral, so `CA1416`
turns an accidental Win32 call into a build error — and, more usefully, the whole app
builds, runs and tests on macOS.

`Murmur.App` loads the platform layer **by reflection** rather than referencing it. A direct
reference would drag the UI onto `net10.0-windows` and destroy the local loop. The published
self-test verifies that reflection works from inside the single-file bundle, because that is
where the arrangement would otherwise fail — silently, at the moment the user first pressed
the key.

Keeping the platform layer logic-free is deliberate: anything that lives there is code CI
cannot exercise. Retries, debouncing, device-change handling all belong in the neutral
projects, behind an interface.

---

## Building

**On Windows** — everything, including the platform layer:

```bash
cd windows
dotnet build Murmur.sln --no-incremental -warnaserror
dotnet test  Murmur.sln
```

**On macOS or Linux** — use the solution filter. `Murmur.Platform.Windows` targets
`net10.0-windows` and cannot compile off Windows; the filter omits it and everything else
builds and tests normally, including the full UI suite:

```bash
cd windows
dotnet test Murmur.CrossPlatform.slnf -c Release      # ~0.5s, 63 tests
```

`--no-incremental` is not optional in CI. Roslyn does not re-emit analyzer warnings on an
incremental build, so `-warnaserror` would pass on cached results and prove nothing.

---

## <a id="honesty"></a>Honesty about what is verified

**Verified, on Windows, every push:** 63 tests pass — 24 dictionary (the shared vectors),
26 core (dictation state machine, audio chunking, all three storage formats), 13 headless
Avalonia UI. CI then publishes a self-contained ~116 MB executable, **runs it**, and the
binary reports back that the dictionary works, the source-generated JSON round-trips, and
the Windows platform layer loads and constructs out of the bundle.

**Verified on macOS, in ~0.5s:** the same 63 tests. The UI genuinely runs headless here,
which is why bugs like a `Render` method mutating a property get caught while writing them
rather than three CI round-trips later.

**Known divergences between the two regex engines**, measured across 30 cases — 9 differed.
The two that affect this code are both handled: culture-sensitive case-insensitive matching
(fixed by `CultureInvariant`) and NFC/NFD mismatch (fixed by normalizing both sides). Two
that are *not* fixable are simply avoided: ICU folds `ß` to `ss` and .NET does not, and
.NET's `.` splits surrogate pairs. Neither is reachable from the patterns this code builds.

**Verified on a real Windows 11 machine (2026-09-08):** the app launches, installs its
low-level keyboard hook, enumerates microphones, loads Parakeet (~1.3 s, ~850 MB working
set at idle), renders the front panel, tray, Settings and dialogs, and reports faults on the
panel. The three bugs above were found and fixed that way.

**Still unverified by anyone but a person at the keyboard:**

- Text landing in a foreground app after a spoken dictation, and the transcript quality.
- The OS microphone-privacy block (the detection exists; the message has not been seen live).
- Unplugging a microphone mid-capture.

Everything those depend on is behind an interface and exercised with fakes, so the logic
around them is tested. The bindings themselves are only as tested as the last person who
held the key.
