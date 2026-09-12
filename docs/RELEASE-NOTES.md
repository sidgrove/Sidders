Preview builds for Windows and Mac: local dictation, live transcript while you speak, spoken commands, dictionary, and optional AI clean-up. Speech never leaves your machine unless you turn the AI tier on.

**Windows (x64):** download **Acapella-Windows-x64.zip**, extract it, then double-click **Install.cmd**. Keep the whole extracted folder together. Windows 10 or 11, a microphone, and a one-time speech-model download (about 600 MB). No developer tools needed. [Windows installation guide](https://github.com/sidgrove/acapella/blob/main/docs/INSTALL-WINDOWS.md).

**Mac (Apple silicon, macOS 26):** download **Acapella-macOS.zip**, unzip it, drag **Acapella.app** to Applications. It is not yet notarised, so the first time you open it, **right-click the app and choose Open**, then Open again in the dialog. Grant Microphone, Speech Recognition and Accessibility when asked; Accessibility is what lets it type into other apps. Uses Apple's built-in speech engine, so there is no model download. [Mac guide](https://github.com/sidgrove/acapella/blob/main/docs/MAC.md).

Both builds are unsigned previews from a public build server. Check `SHA256SUMS.txt` against your download if you want to be sure. The two apps share the dictionary format but not every feature: the Windows app has the history window, spoken sending and the Gemini tier; the Mac app has a compare mode and on-device smart clean-up. The feature matrix is in the Mac guide.

Optional Gemini clean-up (Windows) sends transcript text to Google's API using your own key and is off by default.
