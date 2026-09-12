# Acapella on a Mac

This repository contains a native Swift/SwiftUI dictation app for macOS. It is a separate
implementation from the Windows app, sharing the dictionary format and the correction
contract but not the code. It works and is in daily use on the maintainer's machine.

Read this whole page before building. It says what you get, what you do not, how to build
it, what will go wrong with permissions, and where to change things if you want to adapt it.

**There is no notarised download yet.** Building from source takes one command, but macOS
treats an app without a Developer ID as untrusted, and the Accessibility permission the app
needs behaves badly with ad-hoc signatures. Both are solvable; see [Signing](#signing).

---

## Requirements

| Need | Why |
|---|---|
| **macOS 26 or later** | Apple's `SpeechAnalyzer` (the default engine) and the on-device Foundation Models (smart clean-up) ship with 26. Declared in `Package.swift` and `Info.plist`. |
| **Xcode 26** with the macOS 26 SDK, selected with `xcode-select` | Swift 6.2 or later; the manifest will not even parse on older toolchains. |
| **Apple silicon** recommended | The optional Parakeet engine runs on the Neural Engine via CoreML. Apple's engine should work on Intel; Parakeet there is untested. |
| A microphone, and internet on first build | Swift Package Manager fetches FluidAudio; Parakeet's ~470 MB models are downloaded on demand. |

Check all of that in one go:

```sh
make doctor
```

---

## Build and install

```sh
git clone https://github.com/sidgrove/acapella.git
cd acapella
make doctor
make CONFIG=release install
```

`install` builds, assembles and signs `Acapella.app`, copies it to `/Applications`, and
launches it. Use `make CONFIG=release` alone to build without installing; the bundle lands in
`~/Library/Caches/AcapellaBuild/Acapella.app`.

Always build with `make`, never with a bare `swift build`. The Makefile stages every build
product outside the source tree because iCloud-synced folders corrupt Swift builds and
code signatures mid-flight (the reasons are in the Makefile's comments). A raw SwiftPM
binary also cannot be used directly: macOS keys permissions on a signed bundle.

### First run

1. **Microphone** and **Speech Recognition** prompts appear; allow both.
2. **Accessibility** does not prompt. The app opens System Settings → Privacy & Security →
   Accessibility; switch Acapella on there. This grant is what allows the global hotkey
   (a `CGEventTap`) and typing into other apps.
3. Open TextEdit, hold **Right ⌥** (Option), speak, release. Text lands at the caret.
4. The menu bar item (a waveform) is where the key, engine and clean-up options live.
   ⌘, opens the same settings in a window.

### If typing does nothing after a rebuild

macOS ties the Accessibility grant to the app's code signature. An ad-hoc signature changes
on every build, so after `make install` the old grant no longer matches, and **the toggle
still shows as on**. Reset that one entry and grant again:

```sh
tccutil reset Accessibility com.sidgrove.acapella
```

Then quit System Settings completely (⌘Q) before reopening the Privacy pane; it caches the
list. Never run `tccutil reset Accessibility` without the bundle identifier: that wipes every
app on the machine. A Developer ID certificate makes this go away entirely.

### Troubleshooting

- **"input file was modified during the build"**: the clone is in a synced folder. Wait a
  few seconds and retry, or clone under `~/Developer`.
- **Build log says `log: command not found` or similar shell oddities**: `log` may be
  shadowed in your shell; use `/usr/bin/log`.
- **Nothing happens on the hotkey and the menu shows "Grant Accessibility…"**: the tap
  could not be created. Grant Accessibility; the app polls and arms itself once granted.
- **Where the app writes**: `~/Library/Application Support/Acapella/` holds
  `dictionary.txt` (the same format as Windows; copy it between machines) and the run log
  behind the engine-comparison dashboard. An older install's `MurmurYouTube` folder is moved here automatically on first launch.
- **Logs**: `/usr/bin/log stream --predicate 'subsystem == "com.sidgrove.acapella"'`.

---

## What the Mac app is, and how it differs from Windows

| | Windows | Mac |
|---|---|---|
| Speech engine | Parakeet via sherpa-onnx, CPU. Model downloaded in-app (~660 MB). | **Apple `SpeechAnalyzer` by default**: no download, live text while speaking. Parakeet via FluidAudio (CoreML, Neural Engine) optional, ~470 MB. |
| Push-to-talk key | Any key or chord, recorded in Settings. Right Ctrl default. Hold, tap, or both. | Right ⌥, fn, or Right ⌘ from a menu. Hold only. The key is consumed (except fn). |
| Live preview | Overlay pill shows the running transcript. | HUD panel shows Apple's streaming text; Parakeet resolves on release. |
| Clean-up | Local rules (fillers, spoken punctuation, "scratch that"), then optional Gemini with a plausibility guard and custom rules. | Local rules (fillers, "new line", "open paren"), then optional **on-device Apple Foundation Model** "smart clean-up". No cloud tier. |
| Dictionary | `dictionary.txt`, corrections and terms. | Same file format, same behaviour; verified by the shared test vectors. |
| Text insertion | `SendInput`, with a clipboard-paste path for long text. | Accessibility API write, verified by watching the caret move; pasteboard + ⌘V fallback for Electron and Chrome, clipboard restored afterwards. |
| Spoken send ("send it", send word) | Yes. | No. |
| History window, search, copy, retype | Yes. | An engine-comparison dashboard (HTML) with per-run timings; no history UI. |
| Compare mode | No. | Yes: runs every engine on one recording and shows them side by side. **Types nothing** in this mode, by design. |
| Ducking other audio, sound cues | Both. | Sound cues. No ducking. |
| Start at login, tray/menu bar | Tray icon; start-at-sign-in toggle. | Menu bar item; regular app with a dock icon. No start-at-login. |
| Onboarding | First-run walkthrough. | None; the permission prompts are the onboarding. |

The direction of the design is the same on both: 1980s field recorders, red only for
recording, amber and green only for meters, no gradients. `Sources/Acapella/UI/DesignSystem.swift`
holds every token; views must not contain literal values.

---

## Adapting it: where things live

Everything is in `Sources/Acapella/`, about 5,000 lines. The module, executable and
bundle are all `Acapella`; the dictionary library is `MurmurDictionary`, a name shared with
the test-vector contract and left alone on purpose. On first launch a build from after the
rename moves `~/Library/Application Support/MurmurYouTube` to `.../Acapella`
(`Support/AppPaths.swift`), so an existing dictionary comes along.

| To change… | Look in |
|---|---|
| The dictation flow: press, capture, transcribe, format, inject | `Core/DictationController.swift` |
| Which keys can be push-to-talk, and how they are detected | `Core/HotkeyMonitor.swift` (`PushToTalkKey` enum; the event tap; device-specific left/right modifier masks) |
| How text is typed into the focused app | `Core/TextInjector.swift` |
| Microphone capture | `Core/AudioCapture.swift` (fresh buffer per chunk; do not reuse) |
| Speech engines | `Transcription/TranscriptionEngine.swift` is the protocol; `AppleSpeechEngine.swift` and `ParakeetEngine.swift` implement it. A new engine is one more actor conforming to the protocol. |
| Clean-up rules | `Formatting/TextFormatter.swift` (rule-based) and `Formatting/FoundationModelFormatter.swift` (on-device LLM, with the prompt) |
| Dictionary corrections | `Sources/MurmurDictionary/` — shared contract; change `shared/dictionary-test-vectors.json` first (see AGENTS.md) |
| Settings and their defaults | `Support/Settings.swift` |
| Permissions | `Support/Permissions.swift` |
| Windows, HUD, menu bar | `UI/MainWindow.swift`, `UI/HUDPanel.swift`, `UI/HUDView.swift`, `UI/SettingsWindow.swift`, `AcapellaApp.swift` (menu bar contents) |
| Colours, sizes, typography | `UI/DesignSystem.swift` |
| Bundle name, identifier, usage strings, minimum OS | `Resources/Info.plist` |
| Build, sign, install, package | `Makefile` |

Things that look wrong and are not, all documented in `AGENTS.md`: `MainActor.assumeIsolated`
in the event tap callback, the VU meter keeping its physics outside `@State`, compare mode
typing nothing, and the timing column in the comparison window not being like-for-like.

There are no unit tests for the app target itself, only for the dictionary. CI builds the
full app on a macOS runner and attaches the ad-hoc-signed bundle as an artifact (`macOS`
workflow, "Full Mac app" job), which proves it compiles and bundles; it cannot prove
microphone, hotkey or insertion behaviour. Test those by hand in TextEdit, then in an
Electron app such as Slack or VS Code, which exercise the pasteboard fallback.

---

## <a id="signing"></a>Signing, notarisation and sharing a build

For yourself, ad-hoc signing is fine apart from the Accessibility re-grant after each
rebuild. To give the app to anyone else it must be signed with a **Developer ID
Application** certificate and notarised, or their Mac will refuse to open it.

One-time setup on the building machine:

1. Join the Apple Developer Program and create a Developer ID Application certificate in
   Xcode (Settings → Accounts → Manage Certificates). The Makefile finds it automatically;
   `make doctor` confirms.
2. Create an app-specific password at appleid.apple.com and store a notarytool profile:

   ```sh
   xcrun notarytool store-credentials acapella \
       --apple-id you@example.com --team-id TEAMID --password <app-specific password>
   ```

Then, for every release:

```sh
make CONFIG=release notarize
```

That builds, signs with the certificate (with a secure timestamp), zips with `ditto`,
submits to Apple, waits, staples the ticket to the bundle, and re-zips. The result,
`dist/Acapella-macOS.zip`, can be attached to a GitHub release and will open on any Mac
running macOS 26. `make dist` alone produces the zip without notarising, for passing to
someone who is happy to right-click → Open.

Nobody has run `make notarize` yet; the targets follow Apple's documented `notarytool`
flow but are untested. If Apple rejects the submission, `xcrun notarytool log <id>
--keychain-profile acapella` explains why; the usual causes are a missing hardened runtime
(the Makefile sets `--options runtime`) or an entitlement Apple wants justified.

---

## Remaining work for a proper Mac release

1. Run `make notarize` once with a real certificate and confirm the zip opens cleanly on a
   second Mac.
2. Add a `release.yml` job for macOS mirroring the Windows one, with the certificate and
   notary credentials as repository secrets.
3. Bring the Mac app up to the Windows feature set where it matters: recorded chords and
   tap-to-toggle, a history window, spoken send, start at login, first-run onboarding.
4. Decide whether the Wispr Flow comparison tooling (`Core/WisprTrigger.swift`,
   `Transcription/WisprReader.swift`, the comparison window) stays in a product build or
   moves to `bench/`.
