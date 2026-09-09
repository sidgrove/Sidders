# Audio ducking while dictating — design

Date: 2026-09-09. Scope: the Windows app (Sidders).

## Goal

Other audio on the machine drops to a whisper the moment a recording starts and comes back
the moment it ends, so the microphone hears the speaker rather than the speaker's music,
video or call. A single switch in Settings turns it on or off.

## Decision: duck, not mute

Other applications' audio sessions are lowered to 15 % of their current volume rather than
silenced. Ducking keeps a call or a video intelligible while a short dictation goes in;
outright muting would be a nasty surprise mid-meeting. The master volume is never touched,
so a crash during a hold cannot leave the machine quiet: session volumes live with the
session and die with it, and the app restores them anyway on every exit from Recording and
on dispose.

Skipping the duck when a meeting app owns the microphone is out of scope for this pass.

## Components

- `IAudioDucker` (Abstractions): `Duck()` lowers every other application's playback
  session on the default output device and remembers each original level; `Restore()`
  puts them back. Both are best-effort and never throw. `Restore()` is idempotent.
- `DictationEngine` (Core): gains `IAudioDucker? Ducker` and `bool DuckAudio` (default
  true). On the transition into `Recording` it ducks; on any transition out of `Recording`
  (finish, cancel, fault, dispose) it restores. Nothing else in the engine changes.
- `SessionDucker` (Platform.Windows): NAudio `MMDeviceEnumerator` default render endpoint,
  `AudioSessionManager` sessions, `SimpleAudioVolume` per session. Skips this process's own
  sessions. Each call takes a fresh snapshot; sessions that appear mid-hold are not ducked.
- `FakeAudioDucker` (Testing): records the sequence of calls.
- Settings: `DuckOtherAudio` (bool, default true), switch row in Behaviour:
  "Turn other audio down while I talk".
- Wiring: `PlatformFactory.CreateAudioDucker()`, assigned in `Composition`.

## Tests

- Recording ducks; finishing restores.
- Cancelling restores.
- Switch off: no calls at all.
- Old settings files default the switch on.
