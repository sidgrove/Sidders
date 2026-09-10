# Install Acapella on Windows

You need a Windows 10 or 11 x64 PC, a microphone, and an internet connection for the initial speech-model download. You do not need Python, .NET, a paid AI account or developer tools to use the packaged app. Windows ARM and Mac need different builds; do not use this package there.

## Download and install

1. Open [Acapella releases](https://github.com/sidgrove/acapella/releases). Preview releases are labelled **Pre-release**.
2. Under **Assets**, download **Acapella-Windows-x64.zip**. The GitHub **Source code** archives are for developers, not installation.
3. Right-click the downloaded ZIP and choose **Extract All**. Open the extracted folder.
4. Double-click **Install.cmd**. Keep the `dist` folder and `install.ps1` beside it. This installs for your Windows account, adds a Start menu shortcut, and opens Acapella.
5. If Windows displays an unsigned-app warning, check that the download came from the repository above. Continue only if you trust that source. On a work-managed device, ask your IT team if policy blocks installation; do not disable security protections.

The app is currently unsigned. If you prefer to try it without installation, open `dist/Acapella.exe` and keep all files in that folder together.

## Your first dictation

1. Follow the app's setup to download the speech model (approximately 600 MB, once).
2. Pick your microphone and shortcut. Read the shortcut shown in the app; do not assume it matches another person's setup.
3. Open Notepad and click in a blank document. Activate recording, wait for the listening cue, and say a short sentence. Stop recording using your chosen hold or tap mode.
4. The words should appear in Notepad. Try other applications after that works.

Speech recognition runs locally. Optional **AI clean-up** sends the transcript to Google's Gemini API and requires your own API key. You can use Acapella without enabling it. Avoid testing with confidential client information; follow your organisation's data policy when enabling cloud processing.

## Daily use

- The floating indicator shows recording and processing. Sound effects can be disabled in Settings.
- Closing the window with **×** keeps dictation running in the tray. **Quit Acapella** stops it.
- Say **send it** at the end to insert your text and press Enter, or say it alone to submit text already in the focused app. Enter's effect depends on the app. Disable the send phrase in Settings → Writing if unwanted.
- Add unusual names and accounting terms to Dictionary.
- Do not run an older Sidders copy with Acapella; overlapping shortcuts can cause duplicate text.

## Update or uninstall

Finish your current dictation, extract the newer package and run **Install.cmd** again. Settings, dictionary, model and history are kept. Uninstall through Windows **Settings → Apps → Installed apps → Acapella**. Uninstallation keeps your data in `%LOCALAPPDATA%\Acapella`; remove that folder separately only if you want to erase it.

## If something goes wrong

- **No audio:** check the selected microphone and Windows Settings → Privacy & security → Microphone → Let desktop apps access your microphone.
- **Nothing is typed:** test in Notepad, confirm the app is enabled and the model is downloaded, and check that typing into the focused app is enabled. Elevated applications can reject input from a normal app.
- **Missing DLL:** extract the complete ZIP again. Moving just the EXE is not supported.
- **Slow text:** turn off AI clean-up to compare local-only speed with cloud refinement.
- The main window's **Instant / Polished** control switches between local-only processing and optional Gemini clean-up. Polished needs a configured API key; your existing mode is preserved when updating.
- **Report a problem:** use [GitHub Issues](https://github.com/sidgrove/acapella/issues), including Windows version, steps and any error. Review logs for private information before sharing; never post API keys, client transcripts or settings files.

Settings and history live in `%LOCALAPPDATA%\Acapella`. Help → Open log opens the diagnostic log.
