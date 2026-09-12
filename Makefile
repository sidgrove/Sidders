## The Swift module, executable and bundle are all called Acapella since 2026-09-12.
EXEC     := Acapella
CONFIG   := debug

## Build products live OUTSIDE this directory, for the same reason the .app does.
##
## ~/Desktop is iCloud/file-provider synced, and the provider mutates files inside
## .build while the compiler is using them — producing "input file was modified during
## the build" on random object files, and occasionally a wedged swift-frontend stuck at
## 0% CPU. Moving the scratch path to ~/Library/Caches (never synced) removes the race.
SCRATCH  := $(HOME)/Library/Caches/AcapellaBuild/scratch
BUILD    := $(SCRATCH)/$(CONFIG)/$(EXEC)

## The bundle is assembled and signed OUTSIDE this directory on purpose.
##
## This tree lives under ~/Desktop, which is iCloud/file-provider synced. The provider
## stamps com.apple.FinderInfo onto files inside an .app faster than we can strip them,
## and codesign hard-refuses anything carrying them ("resource fork, Finder information,
## or similar detritus not allowed"). `xattr -cr` immediately before signing is not enough
## — the provider re-stamps in between. Staging in ~/Library/Caches sidesteps it entirely.
STAGE    := $(HOME)/Library/Caches/AcapellaBuild
APPNAME  := Acapella.app
BUNDLE   := $(STAGE)/$(APPNAME)
CONTENTS := $(BUNDLE)/Contents

## Where `make dist` writes the shareable zip. Git-ignored.
DIST     := dist
ZIP      := $(DIST)/Acapella-macOS.zip

## TCC keys the Accessibility grant to the code signature, so an ad-hoc signature — which
## changes on every build — makes the user re-grant after every `make`. Signing with a
## stable Developer ID keeps the identity constant and the grant sticky. Falls back to
## ad-hoc ("-") on a machine without the cert.
SIGN_ID := $(shell security find-identity -v -p codesigning 2>/dev/null \
             | grep "Developer ID Application" | head -1 | sed -E 's/.*"(.*)".*/\1/')
ifeq ($(strip $(SIGN_ID)),)
SIGN_ID := -
endif

## Notarization requires a secure timestamp on the signature; an ad-hoc signature cannot
## carry one (there is no identity for Apple to vouch for), so it is only requested when
## a real certificate is in use.
ifeq ($(SIGN_ID),-)
TIMESTAMP := --timestamp=none
else
TIMESTAMP := --timestamp
endif

## The notarytool keychain profile. Create it once with
##   xcrun notarytool store-credentials acapella --apple-id you@example.com \
##       --team-id TEAMID --password <app-specific password>
NOTARY_PROFILE ?= acapella

.PHONY: all build app run install clean icon dist notarize doctor

all: app

build:
	swift build -c $(CONFIG) --scratch-path "$(SCRATCH)"

## Regenerates AppIcon.icns from Tools/makeicon.swift. Not a dependency of `app` — the
## icon rarely changes and rendering 10 PNGs on every build is wasted time.
icon:
	@swift Tools/makeicon.swift
	@iconutil -c icns Resources/AppIcon.iconset -o Resources/AppIcon.icns
	@echo "wrote Resources/AppIcon.icns"

## Assemble a real .app bundle. TCC (microphone + Accessibility) keys on bundle identity
## and code signature, so the raw SwiftPM binary can't be used directly.
app: build
	@rm -rf "$(BUNDLE)"
	@mkdir -p "$(CONTENTS)/MacOS" "$(CONTENTS)/Resources"
	@cp $(BUILD) "$(CONTENTS)/MacOS/$(EXEC)"
	@cp Resources/Info.plist "$(CONTENTS)/Info.plist"
	@if [ -f Resources/AppIcon.icns ]; then cp Resources/AppIcon.icns "$(CONTENTS)/Resources/"; fi
	@printf 'APPL????' > "$(CONTENTS)/PkgInfo"
	@# Belt and braces: the staging dir isn't synced, but the copied binary can still carry
	@# xattrs inherited from the synced .build directory.
	@xattr -cr "$(BUNDLE)"
	@codesign --force --sign "$(SIGN_ID)" \
		--entitlements Resources/$(EXEC).entitlements \
		--options runtime \
		$(TIMESTAMP) \
		"$(BUNDLE)"
	@echo "built $(BUNDLE)  [signed: $(SIGN_ID)]"

## Only ever targets this executable — never any other app that happens to be running.
run: app
	@pkill -x $(EXEC) 2>/dev/null || true
	@open "$(BUNDLE)"

## Ad-hoc signatures change on every rebuild, which resets the Accessibility grant.
## Installing to /Applications keeps the path stable and makes re-granting a one-click fix.
install: app
	@pkill -x $(EXEC) 2>/dev/null || true
	@# $(BUNDLE) is an absolute staging path — the destination must use $(APPNAME) alone.
	@rm -rf "/Applications/$(APPNAME)"
	@cp -R "$(BUNDLE)" "/Applications/$(APPNAME)"
	@open "/Applications/$(APPNAME)"
	@echo "installed to /Applications/$(APPNAME)"

## A zip to hand to someone. `ditto` preserves the signature and bundle structure; Finder's
## Compress and plain `zip` can both break a signed bundle.
dist: app
	@mkdir -p "$(DIST)"
	@rm -f "$(ZIP)"
	@ditto -c -k --keepParent "$(BUNDLE)" "$(ZIP)"
	@echo "wrote $(ZIP)  [signed: $(SIGN_ID)]"

## Developer ID + notarization: the step that stops Gatekeeper from refusing the app on
## someone else's Mac. Needs the certificate in the keychain (SIGN_ID picks it up) and a
## notarytool profile (see NOTARY_PROFILE). Re-zips after stapling so the ticket ships.
notarize: dist
	@if [ "$(SIGN_ID)" = "-" ]; then \
		echo "No 'Developer ID Application' certificate in the keychain; cannot notarize an ad-hoc build."; \
		exit 1; \
	fi
	@xcrun notarytool submit "$(ZIP)" --keychain-profile "$(NOTARY_PROFILE)" --wait
	@xcrun stapler staple "$(BUNDLE)"
	@rm -f "$(ZIP)"
	@ditto -c -k --keepParent "$(BUNDLE)" "$(ZIP)"
	@echo "notarized and stapled: $(ZIP) is ready to share"

## Checks the machine before the first build: OS, Xcode, Swift, certificate, sync folders.
doctor:
	@sh Tools/mac-doctor.sh

clean:
	@rm -rf .build "$(STAGE)" "$(SCRATCH)" "$(DIST)"
