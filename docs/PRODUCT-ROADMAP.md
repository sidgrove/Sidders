# Acapella product improvement plan

Approved direction: fast, reliable local dictation, optional intelligence, and a calm, polished interface for everyday professional use.

## First delivery: community preview and speed visibility

- Complete Windows x64 ZIP with a double-click installer and checksums.
- GitHub tag-driven preview release workflow; clean-extraction smoke test.
- Windows installation, updating, troubleshooting and privacy guidance.
- Accurate Mac developer instructions and explicit distribution limitations.
- Instant/Polished control on the main window. Instant uses existing local rules; Polished enables Gemini clean-up. Existing preference is preserved.
- Timing logs separating microphone startup, preview wait, local decoding, cloud clean-up and insertion. Stop-to-complete includes the full processing path; insertion acceptance does not prove the target app rendered the text.

## Next: measured speed and reliable delivery

Benchmark short and long dictations in Notepad, Codex, Outlook, Teams and a browser. Target under 300 ms for typical short Instant dictations; report median and 95th percentile, not just the fastest result. Test first/last words, microphone changes, rapid toggles, cancellation, delivery failures and send commands.

Use the measurements to reduce duplicated preview/final work. Never skip the final audio tail or silently paste a stale preview to meet a latency target. Keep retry/copy-last recovery available.

## Then: interface redesign

Audit actual rendered screens at small and large window sizes before redesigning. Evolve the recording capsule, waveform, transcript list, typography, spacing, light/dark states and settings hierarchy as one system. Preserve focus, keyboard access, reduced motion and clearly distinguish listening from processing. The preview-release work does not claim this redesign is complete.

## Then: intelligence and personalisation

Add app-specific writing styles, explicit voice snippets, reviewable learned corrections, and a separate voice-editing mode for selected text. Preserve exact amounts, dates, names and negation in accounting workflows. Keep instructions separate from dictation and offer recovery for destructive edits.

## Mac community release

Requires full macOS 26 testing, branding alignment, permissions onboarding, Developer ID signing and notarization. A Windows build or dictionary-only CI run cannot substitute for these checks. Track multilingual and Intel support separately rather than promising them without a tested engine matrix.

## Licensing

Retain the existing repository notice until the owner chooses the community licence. Do not describe the repository as open source merely because it is visible on GitHub. Review upstream and bundled dependency/model notices before selecting redistribution terms.
