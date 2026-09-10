# Acapella on a Mac: current status

This repository already contains a separate native Swift/SwiftUI dictation app. It is not the Windows executable ported to macOS, and it does not yet have the same branding, interface or all of the latest Windows features. It currently identifies itself as **Murmur YouTube**.

**There is no tested, signed Acapella Mac download in this release.** The following is a developer build path, not a one-click installation for the accounting community. These steps have been checked against the repository, but have not been executed on a Mac during this release.

## Requirements

- macOS **26 or later**, as declared by `Package.swift` and `Resources/Info.plist`.
- Xcode with the macOS 26 SDK and **Swift 6.2 or later**, selected with `xcode-select`.
- A microphone and internet access to fetch build dependencies and any speech assets.
- Apple silicon is recommended for the optional FluidAudio/Parakeet engine; this release makes no Intel compatibility claim.

## Build and run

In Terminal:

```sh
git clone https://github.com/sidgrove/acapella.git
cd acapella
swift --version
make CONFIG=release
open "$HOME/Library/Caches/MurmurYouTubeBuild/Murmur YouTube.app"
```

To install to Applications, use `make CONFIG=release install`. This replaces an existing `/Applications/Murmur YouTube.app` and launches the new copy. Use the cached build above first if you need to preserve an existing installation.

Use the Makefile: it stages builds outside cloud-synced folders and creates a signed application bundle. Do not launch a raw Swift executable and expect microphone permissions to work identically.

Grant Microphone, Speech Recognition and Accessibility permissions when requested, and check System Settings → Privacy & Security if a permission is missing. The app uses Apple's speech recognition by default, with an optional Parakeet engine. Select the shortcut in the Mac app and test in TextEdit.

Without a Developer ID certificate, builds are ad-hoc signed; rebuilding can invalidate Accessibility permission. Keep the installed path stable and re-grant permission to the specific app if necessary. Do not clear permissions for other applications or disable Gatekeeper globally.

## Work needed for an ordinary Mac download

1. Align product branding and document Windows/Mac feature differences.
2. Build and test the complete app on macOS 26, including microphone capture, insertion, shortcuts and permissions onboarding.
3. Configure Developer ID signing and Apple notarization using the maintainer's Apple Developer account.
4. Publish a notarized DMG or ZIP, with an Applications installation flow and tested update behaviour.

The existing macOS CI focuses on the shared dictionary contract; it does not establish that the full desktop app is ready for distribution. Windows UI tests also do not prove Mac functionality.
