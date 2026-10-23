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
## Current text-owned presentation, 2026-10-18

This supersedes the consumer's speech-generation completion policy above; the
historical audit and its verification limits remain unchanged. Native SHA-256 is
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; installed
Assembly-CSharp.dll SHA-256 is
BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Both installed files were rechecked against the referenced evidence.

- Presentation 0xFEE50 emits text through 0x1A2850 (command 20) before optional
  audio and video. GameMap.processTestMap processes these records on Director's
  rendered-buffer caller, then GameData.setGameState/updateOST applies text state
  before the subsequent GuiChecks callback. Function labels remain candidate-level;
  the record order is supported by the exported pseudocode and managed consumers.
- OnScreenText.addOSTEntry(OST_MESSAGE_BAR, ...) refreshes Vanilla's text expiry.
  updateOST applies expiry only when allowExpire is true, refreshes the expiry
  while a briefing is visible, and drives MainViewModel.OST_Message_Bar_Vis.
  Its setter emits the public System.ComponentModel.PropertyChanged event.
  No consumer expiry calculation is required. The Extender's updateOST IL hook
  replaces custom-Lord text resolution, not this visibility signal.
- A nonlooping ** media source can be paused in the MediaEnded callback while
  leaving requestBinkPlayState=1 and binkIsPlaying=true. FatControler neither
  restarts that state nor executes its speech-dependent state-3 completion branch.
  Its briefing-resume path can call Play again; the GUI consumer must pause a held
  frame afterwards without restoring opacity after a left-click hide.
- BugfixesAndQoL now associates each message-bar write with the next accepted
  video in the rendered batch. Identical text/file names do not identify ownership.
  A visible-to-hidden text notification closes only its matching current video via
  the public parameterless RadarME_Ended; GuiChecks handles missing initial text,
  logical disable, source/HUD replacement and deferred mission invalidation.
  This may end a portrait while speech is still playing, as explicitly requested.
- Queue update 0xFE570 waits for the video flag after its age gate. Closing the
  picture when Vanilla hides text releases that dependency; waiting for the native
  finalizer first would create a circular lifetime dependency. No native patch is
  introduced. The separate skip feature retains its audio-load cancellation.

The new production-source fixture covers text-driven completion, silent messages,
independent audio replacement/end/pause, pending text/source, repeated filenames,
left-click preservation, explicit close, briefing resume, replacement HUD/ViewModel
and mission invalidation. The fixture does not prove real decoder timing or a live
in-game acceptance result; no installation is authorized for this change.

## HD-bounded speech presentation, 2026-10-23

This supersedes the 2026-10-18 text-owned consumer policy. The preceding audits
remain historical evidence. Current DE native SHA-256 was rechecked as
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The installed managed assembly still matches BC8B6A39 above (preflight validates
actual member signatures and the Noesis player identity independently).

### HD evidence and closed feature flow

Source: the local `HD sources/Stronghold Crusader HD reversed/Stronghold Crusader.exe.gzf`
archive, SHA-256 9069E32DD2B6FEFFEE39E0B12EA9CF17CE68B7CF93AA8B4C7291969298D4E2A0.
Its program metadata identifies original executable SHA-256
3BB0A8C1E72331B3A30A5AA93ED94BECA0081B476B04C1960E26D5B45387AC5A.
This identifies the archived HD program, not an independently checked installed HD
executable or a verified game version. Function names are archive annotations;
the following control/data-flow statements are confirmed-static against its code.

- `playVideoOverMap` VA 004B7AF0 opens nonlooping Bink slot 1, starts speech
  stream 3, and binds that stream to the video only if it is reported playing.
  `setBinkSndStreamAndStartTime` VA 00408E70 writes the stream index at
  BinkControlClass+40+slot*4 and the current timeGetTime DWORD at +48+slot*4;
  binding is skipped if the sound system is unavailable. Only this caller binds
  a speech stream through that setter in the archived program.
- `playSpeechOnStream` VA 0047B830 delegates to `playSoundStreamOnSlot`
  VA 0047B060. An unavailable sound system, focus restrictions or failure to
  open the WAV leaves no playing stream. `isSampleOrStreamPlaying` VA 00479A60
  requires an active sound slot, a nonnull stream and Miles status 4.
- Both `processBinkFrames` VA 00409200 and `copyBinkToVideoBuffer` VA 00408FC0
  permanently clear a nonzero video speech binding when that stream is no
  longer playing OR more than 20000 ms have elapsed since binding. At 0040922E
  the assembler compares elapsed DWORD against 0x4E20 and uses unsigned JBE
  at 00409233 to retain the binding at equality. Clearing does not restart the
  clock, stop speech, or bind a subsequent stream anew.
- For a nonlooping minimap video, reaching the last Bink frame advances no
  further while the binding remains active. Once cleared, the same video-end
  branch calls `stopBinkPlayback` VA 00408E30, closes the Bink object and clears
  isBinkVideoPlaying. The 20-second rule limits speech waiting, not video length.
  A voice replaced on the same occupied stream can extend the still frame only
  within the original deadline; HD does not identify individual voice requests.
- `playAiLordMessage` VA 004B7C90 draws message text separately and waits for
  the video pointer to clear after its 10000-ms age gate. Modal completion reaches
  `playNextStoredAIMessage` VA 004BE960, which clears current presentation and
  promotes a queued entry. This is distinct from the speech-wait bound.

Reproducible local exports and the small reader are retained under
`.work/a/lastframe-hd/001/hd-verified.stdout.log` and `HdRead.java` (workspace
relative). The reader imports the archive into a fresh temporary Ghidra project
and opens a read-only program; Ghidra rejects `.work` as a project path element.
The export includes both video paths, the binding writes and the exact timeout
assembler. The original archive and DE raw baseline were not modified.

### DE implementation contract

- `NotificationLastFrameFeature` uses nonlooping `**` sources for accepted
  nonlooping minimap videos and captures Vanilla's wait request plus the current
  audio manager. No message text subscription/hook or speech-generation identity
  controls this feature. `NotificationSpeechPlayback` remains unchanged for skip
  load cancellation, including the Extender single-path loader override.
- A Stopwatch timestamp is captured once after each accepted video request.
  The original wait predicate is permanently cleared after channel-1 idle,
  audio-manager replacement or elapsed >20 seconds, including while the video
  is still playing. Loading/paused states count as channel occupancy, as in DE
  Vanilla, but cannot renew or bypass the fixed bound. Time is monotonic and is
  not suspended for game pause/briefing. The precision improvement over HD's
  DWORD clock does not change its strict greater-than threshold.
- On MediaEnded, an active binding holds the frame paused in state 1; otherwise
  the existing public close method releases it. GuiChecks checks completion and
  re-pauses held media if Vanilla resumed it after a briefing. The end marker is
  recorded before Pause, so a decoder-Pause failure falling back to Vanilla's
  state 3 still has bounded completion. No UI work is done from simulation ticks.
- Existing logical disable, deferred mission invalidation and stale HUD/request/
  source checks remain. A foreign current media source is never closed by old
  owner cleanup. Text visibility cannot close or extend the video; audio is never
  stopped by picture completion. The native queue observes the released video
  flag through DLL_RunTick and can finalize normally, without a new native hook.

Production-source fixtures cover strict 19999/20000/20001-ms boundaries, hung,
failed and null speech loads, long videos/voices, replacement voices and files,
observed-idle latching, pause/briefing, left/right clicks, stale HUD/media/manager,
mission end, logical disable, and decoder Pause failure. These isolated adapters
validate the consumer, not live Noesis decoder timing. Actual game acceptance is
still required; this task requests no game installation.