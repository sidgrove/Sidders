#!/bin/sh
# Preflight for building Acapella on a Mac. Run with `make doctor` or `sh Tools/mac-doctor.sh`.
# Prints what is present, what is missing, and what to do about it. Changes nothing.

ok()   { printf '  \033[32m[ok]\033[0m   %s\n' "$1"; }
warn() { printf '  \033[33m[warn]\033[0m %s\n' "$1"; }
bad()  { printf '  \033[31m[no]\033[0m   %s\n' "$1"; FAILED=1; }

FAILED=0
echo "Acapella on macOS: preflight"
echo

# macOS 26: the app uses Apple's SpeechAnalyzer, which ships with 26, and the on-device
# Foundation Models for smart clean-up.
os=$(sw_vers -productVersion 2>/dev/null || echo 0)
major=${os%%.*}
if [ "$major" -ge 26 ] 2>/dev/null; then
  ok "macOS $os"
else
  bad "macOS $os — Acapella needs macOS 26 or later (Package.swift declares .macOS(.v26))"
fi

# Apple silicon is where the optional Parakeet engine runs on the Neural Engine.
arch=$(uname -m)
if [ "$arch" = "arm64" ]; then
  ok "Apple silicon ($arch)"
else
  warn "$arch — Apple's engine should work; the optional Parakeet engine is untested on Intel"
fi

# Xcode with the macOS 26 SDK, selected with xcode-select.
if command -v xcode-select >/dev/null 2>&1 && xcode-select -p >/dev/null 2>&1; then
  dev=$(xcode-select -p)
  sdk=$(xcrun --sdk macosx --show-sdk-version 2>/dev/null || echo 0)
  if [ "${sdk%%.*}" -ge 26 ] 2>/dev/null; then
    ok "Xcode at $dev (macOS SDK $sdk)"
  else
    bad "Xcode at $dev has macOS SDK $sdk — install Xcode 26 and run: sudo xcode-select -s /Applications/Xcode.app"
  fi
else
  bad "Xcode is not selected — install Xcode 26 from the App Store, open it once, then: sudo xcode-select -s /Applications/Xcode.app"
fi

# Swift 6.2 or later, as the manifest's swift-tools-version demands.
if command -v swift >/dev/null 2>&1; then
  sv=$(swift --version 2>/dev/null | sed -n 's/.*Swift version \([0-9][0-9.]*\).*/\1/p' | head -1)
  smajor=${sv%%.*}
  sminor=$(echo "$sv" | cut -d. -f2)
  if [ "${smajor:-0}" -gt 6 ] 2>/dev/null || { [ "${smajor:-0}" -eq 6 ] && [ "${sminor:-0}" -ge 2 ]; }; then
    ok "Swift $sv"
  else
    bad "Swift ${sv:-unknown} — 6.2 or later is required (comes with Xcode 26)"
  fi
else
  bad "swift is not on the PATH"
fi

# A Developer ID keeps the Accessibility grant stable across rebuilds. Optional for
# building; required for handing the app to anyone else.
if security find-identity -v -p codesigning 2>/dev/null | grep -q "Developer ID Application"; then
  ok "Developer ID Application certificate found — builds will be signed with it"
else
  warn "No Developer ID certificate — builds are ad-hoc signed. Fine for you; macOS will re-ask for Accessibility after each rebuild, and other Macs will refuse the app. See docs/MAC.md."
fi

# Building inside a synced folder corrupts builds and signatures. The Makefile stages
# outside the tree, but a clone in iCloud Drive still slows every build.
case "$(pwd)" in
  *"/Library/Mobile Documents/"*|*"/iCloud Drive"*|*"/Dropbox"*|*"/OneDrive"*|*"/Google Drive"*)
    warn "This clone is inside a synced folder. Builds are staged in ~/Library/Caches, but expect random 'input file was modified' errors; a clone under ~/Developer is smoother." ;;
  *)
    ok "Clone is not in a synced folder" ;;
esac

echo
if [ "$FAILED" -eq 0 ]; then
  echo "Ready. Next:  make CONFIG=release install"
else
  echo "Fix the [no] items above, then run this again."
  exit 1
fi
