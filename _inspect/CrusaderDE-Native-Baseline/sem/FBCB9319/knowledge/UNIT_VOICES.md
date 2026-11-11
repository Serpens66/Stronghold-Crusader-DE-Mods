# Unit speech sources and shared filename transport [Scope: Vanilla + mod integration]

Observed 2026-11-11 against installed CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Evidence: current semantic function/decompiler exports, direct native table reads,
installed Assembly-CSharp member/IL inspection. These are static findings, not
in-game or multiplayer acceptance. The sound-manager symbol at RVA `0xEDF0` is
confirmed; other descriptive function roles below are statically reconstructed
and need not have confirmed symbol names in the semantic database.

## Voice production [Vanilla facts]

- `0x3BC0` maps the requested troop type and situation to one of 27 voice banks.
  Variant counters and original-path/busy suppression precede `0xEDF0`.
  Shared banks must not be treated as the originally requested unit type.
- `0x3770` selects engineer/siege voices, including Arabic alternatives and
  multi-variant oil/cow groups. Calls can concern siege devices, not engineers.
- `0x3BC0` also delegates healer/sapper/demolisher actions to `0x38A0`, whose
  indices 65 through 76 select the corresponding unit speech. Most other calls
  to that helper are advisor speech and cannot be included by function alone.
- Recruitment confirmations use `0x39A0` from success paths in `0xD5B40`,
  `0xD5DE0`, `0xD6290`, `0xD6030` and `0xD6830`. Their error paths also emit advisor
  or Genie speech. Throttled confirmations can retain a previous voice-bank choice.
- `0x97C70 -> 0x97DA0 -> 0x3B20` selects civilian comments using the actual selected
  worker type and owner. Male/female peasant banks contain 120 files each.
  The owner need not equal the native view player. LORD55 and LADY56 are excluded;
  do not invent Lord order voices from this path. Combat grunts are SFX instead.
- Monk type37 chooses Monk versus TempleGuard voice banks by religion.
- Silent placeholders, busy rejection and recruitment throttling must remain
  silent. All aliases used for transport must be introduced after the original
  generators' filename comparison and variant selection.

## Last-writer transport [Vanilla facts]

The shared 128-byte filename starts at SoundManager `+0x41A6` (global
`0x1888EB476`). Four functions write it: `0xEDF0`, `0xEF20`, `0xF070`, `0xF0F0`.
The latter three include unrelated message paths; an unmarked last-unit sidecar
cannot distinguish their overwrites, even when they write the same filename.

`0xEDF0` can reject before writing: negative manager `+0x28` with busy channel3,
or game mode4. Its accepted path cycles the internal channel counter through
3/4, sets manager `+0x41A4/5`, copies the filename, and calls record writer
`0x1A4270` with counter minus two: managed speech channels1/2, volume100.
The writer appends record18 with channel and volume, no filename or player ID.

`0x19D960` copies all 128 filename bytes to return-struct `+0x600` and zeroes the
native filename buffer. `EngineInterface.CopyPlayStateStruct` converts the
bounded ASCII string to `PlayState.speechFileName`. Every record18 in that state
uses this single last filename in `GameMap.processTestMap`. Preserve this
coalescing; there is no independent per-record source filename to recover.

EngineInterface.run, GameAction and TroopSelection serialize their native calls
under the private static `threadLock`. Capture may read native player/worker
data only in that verified context, with initialized managers and identity checks.

## Managed playback and lifetime [Vanilla facts]

SFXManager resolves its speech-folder table and calls public
`MyAudioManager.PlaySpeech`. The table has troop and peasant categories;
EnglishSpeech is an asset branch, not proof of the spoken content's language.
Some native table filenames are absent from the managed folder table; their
presence in native tables alone does not prove managed playback availability.

PlaySpeech accepts channel requests and starts private async LoadClip. There are
three path-loader and three PlayOneShot callsites in the state machine.
Channel volume uses either UnitSpeechVolume or SpeechVolume times master.
Settings changes rewrite channel volume. A per-OneShot multiplier therefore
preserves later settings changes without a persistent channel factor.

Speech clips are unloaded when replaced, naturally finished, stopped or when
StopAllGameSounds runs. MyAudioManager.OnDestroy calls StopAllGameSounds.
These are audio-manager lifetime paths, not BepInEx plugin startup teardown.

## APIShared integration [mod integration]

The Unit Voices extension captures primitive source snapshots, tags accepted
native unit output with a short catalog-backed token, restores the original path
while copying PlayState, and associates the immutable snapshot with that exact
state. Public callbacks run on Unity. No concrete unit ID is promised.

The common GameMap speech adapter cooperates with LordMessageRuntime's grouped
audio ownership. The original group takes priority; other attributed unit speech
goes through UnitVoiceEvents. Existing SFX sites remain independent.

Prepared resources own PCM data, not reusable speech AudioClips. Each replacement
owns a fresh clip whose release follows the existing speech cleanup sites.
Async request frames carry OneShot gains, including neutral frames for ordinary
or nested speech. Current implementation and validation results belong to the
workspace's unit-voices run evidence; no game success is asserted here.
