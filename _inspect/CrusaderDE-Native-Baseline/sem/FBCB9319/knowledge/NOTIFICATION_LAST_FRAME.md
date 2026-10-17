# Minimap last-frame speech ownership

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The installed DLL matched CURRENT.json during this investigation. Native function
labels below are candidate names in the semantic database; the stated field and
output-record flows are visible in the exported pseudocode.

## Audited flow

- Notification presentation at RVA 0xFEE50 calls 0xEF20 for nonempty audio before
  0x14F0 for nonempty video. 0xEF20 emits command 18 through 0x1A4270 with channel 1;
  0x14F0 emits command 21 through 0x1A4100 with loop and speech-wait arguments.
  GameMap.processTestMap consumes those records in order and calls SFXManager.
- SFXManager.playSpeech resolves the resource and MyAudioManager.PlaySpeech accepts
  or rejects the request. Only an accepted request calls the five-argument private
  LoadClip overload, after setting speechMode1 to loading (1). Successful loading
  changes it to playing (2); MonitorLoadedSounds clears it when playback stops,
  except while deliberately paused. isSpeechPlaying(1) means mode != 0, including
  loading and intentional pauses; it does not identify a particular voice line.
- SFXManager.playBink constructs a fresh request URI, records whether existing
  channel-1 speech is to be awaited, and requests state 1 (once) or 2 (loop).
  Replacements negate that state until FatControler installs the new source.
- The installed NoesisMediaPlayer interprets ** as nonlooping playback with a
  MediaEnded event. MainHUD.RadarME_Ended(object, RoutedEventArgs) changes state 1
  to state 3 and opacity 0 when speech is still pending; otherwise it closes the
  media and clears playing/state. The parameterless overload always closes it.
- Director.Update invokes GUI checks for rendered buffers and also at a 100 ms
  fallback interval while simulation is running. FatControler closes state 3 only
  when the entire speech channel is idle. Briefings pause/resume the media.
- DLL_RunTick (RVA 0x86680) copies binkPlaying into the native flag checked by
  notification update 0xFE570. Even beyond its 10000 ms age gate, that update waits
  for the video flag to clear. Finalizer 0x102A70 clears the current presentation
  and promotes the next queued entry when present. The native age gate therefore
  does not limit an incorrectly retained managed video.

## Consumer defect and correction

NotificationLastFrameFeature formerly forced binkWaitForSpeech=true based on any
channel-1 occupant and retained no voice identity. An unrelated replacement could
keep an old portrait visible. A source-compiled isolated fixture confirmed retention
across 100 unrelated replacements. This establishes the code defect, not the exact
unrecorded sequence in the supplied user log.

The corrected consumer gives every accepted channel-1 load a generation, binds
eligible video requests to that generation, and closes only its own held video
when the generation changes. It never forces waiting on a video without the
original wait request. Its completion callback uses APIShared GuiChecks on the
HUD caller thread; no simulation-tick callback touches Noesis. Speech hooks and
GUI registrations remain installed for process lifetime. Mission end invalidates
pending identities through the existing central lifecycle subscription.

The shared internal speech owner also invalidates pending skip loads when an
unrelated request supersedes them. It retains the Script Extender's _SE_ path
semantics and calls the existing single-path loader through reflection, preserving
that loader's override hook. No native hook spans or public APIs change.

## Verification and limits

BugfixesAndQoL/Test-NotificationLastFramePreflight.ps1 validates installed Noesis
identity, real game member signatures and reflection fields, then runs the actual
feature sources in tests/NotificationPlayback.Tests. Tests substitute Unity/Noesis
objects and hook dispatch, retaining audited Vanilla state predicates. They cover
speech replacements, repeated filenames, rejected requests, pauses, source/HUD
replacement, disabled behavior, stale asynchronous loads and mission reset.
A real-game reproduction and live acceptance remain unavailable; these tests do
not validate decoder timing or prove the historical user's precise trigger.