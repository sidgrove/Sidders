# Acapella

Press a key, say it, and it's typed. Sidders is a dictation app for Windows from [Sidgrove](https://sidgrove.com): hold or tap a key anywhere, speak, and the words land in whatever has focus.

- **On this machine.** Speech is transcribed locally with NVIDIA's Parakeet model through sherpa-onnx. Nothing leaves your PC unless you turn on AI clean-up.
- **Live as you speak.** The overlay shows the running transcript while you talk.
- **Spoken commands that just work.** "New line", "full stop", "comma", "question mark", "scratch that". Ums and ers are dropped. All local, all predictable.
- **Optional AI clean-up.** Gemini tidies punctuation, applies self-corrections and writes numbers as figures, with a guard that refuses anything that summarises or rewrites your words. Add your own rules in Settings.
- **Your key, your way.** Record any key or chord (Ctrl, Alt, Shift, Win combinations included). Hold to talk, tap to toggle, or both. Escape cancels.
- **Dictionary.** Teach it the names and terms it gets wrong.
- **History.** Every dictation, with what was heard and what was typed.

## Install

Grab the latest `Sidders.exe` from the releases page, or build it yourself:

```powershell
cd windows
.\publish.ps1
.\install.ps1
```

`install.ps1` puts Sidders in `%LOCALAPPDATA%\Programs\Sidders`, adds a Start menu entry and an Apps & features entry. On first run it walks you through downloading the speech model (about 600 MB, once), picking your key and trying a dictation.

## Requirements

- Windows 10 or 11, x64
- A microphone
- For AI clean-up: a Gemini API key, entered in Settings or set as `GEMINI_API_KEY`

## Building

- .NET 10 SDK
- `dotnet build windows/Murmur.sln`
- `dotnet test windows/Murmur.sln`

The engine is platform-neutral and fully tested against fakes; only the thin `Murmur.Platform.Windows` layer touches Win32. See [`windows/README.md`](windows/README.md) for the layout and [`AGENTS.md`](AGENTS.md) for the conventions.

## Data

Settings, dictionary, history, log and the speech model live under `%LOCALAPPDATA%\Sidders`.

## Licence

Proprietary. © Sidgrove.
