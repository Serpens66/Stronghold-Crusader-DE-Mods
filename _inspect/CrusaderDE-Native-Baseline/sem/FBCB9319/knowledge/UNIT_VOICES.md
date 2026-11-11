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

## Troop dispatcher table and unrouted intents [Vanilla facts]

Static audit of 2026-11-11 (same hash): semantic decompile of `0x3BC0`, raw table
reads from the DLL, a direct call/jump scan of `.text`, and ilspycmd output of
`SFXManager` and `MyAudioManager`. Not run in the game.

- **Type to column:** a switch maps the requested type to one of 27 columns, for example
  archer 0, crossbow 1, spearman 2, maceman 3, pikeman 4, swordsman 5, knight 6, monk 7
  (or 26 by religion), tunneler 8, ladderman 9, engineer 10, Arabian/Bedouin types 11-25.
  Unlisted types return silently. Siege devices go to the engineer helper `0x3770` or to
  column 10.
- **Slot:** situation 42 becomes 21 (sapper/demolisher instead use `0x38A0`). Valid range
  `0 <= situation < 42`, `column < 27`. The file is `0x2AD298[((situation + variant) * 42) + column]`;
  the variant count is at `0x2AD290 + situation * 0x150`, a counter per situation and column
  at `0x2AD370 + (situation * 0x54 + column) * 4`. Start rows and counts: 0 (6), 6 (3),
  9 (1), 10 (1), 11 (5), 16 (1), 17 (4), 21 (4), 25 (4), 29-32 (1 each), 33 (3), 36 (3),
  39 (2), 41 (1). In column 10 a switch (`DAT_183665f28 == 0 && DAT_183665f0c == 2`) takes
  the file from `0x2B0EB0[situation]` instead; the meaning of these globals is unknown.
- **Callers:** 18 direct call/jump sites, no pointer reference. Situation 33 comes only from
  `0x143B16` in the spearman handler `0x143400` with the type fixed to spearman. It fires
  when `0x18CD10` finds an adjacent enemy ladderman in state 3 (pushing the ladder off),
  for the viewing player's units, with a 4-second throttle. Rows 33-35 hold
  `spear_ladder1-3.wav` in both the spearman and the pikeman column; the pikeman entries
  are never requested. Managed `troopsSpeech` lists `Pike_Ladder1-3.wav`, which native code
  never emits. No other file is shared between columns apart from `Null.wav`.
- **Unrouted intents:** 29 native file names have no entry in the managed speech lists.
  `knight_atks1-4` and `sword_atks1-4` are never requested (attack-unit commands for
  types 0x1B/0x1C always pass situation 21). `ladder_atkw1-4` has a native path; whether
  laddermen can receive that order was not checked. The Arabian engineer lines
  `aengineer_equip1-3`, `launchcow2-6`, `pouroil2-9` and `sshield` are reachable under
  the column-10 switch above. `sshield` is misspelled in the managed list
  (`AEngineer_Ssheild.wav`).
- **Managed side:** `SFXManager.playSpeech` plays a path only if it is in `speechFolders`
  (built from the speech arrays); anything else is dropped before any load, without a log.
  `MyAudioManager.PlaySpeech` checks neither the file nor the lists: it applies
  busy/force rules, and `LoadClip` only builds the path (`folder == "*"` means an absolute
  path). Only `LoadClip(string)` touches the file; a missing file logs and yields a null clip.
  `folder` forms only the path; `unitsSpeech` chooses UnitSpeechVolume and is derived from
  `folder == "troops"` in `SFXManager`.

## Civilian comment repetition [Vanilla facts]

`0x97DA0` runs from `DLL_RunTick` (via `0x97C70`) on every tick while a worker panel is
open (panel state `0x46`). It returns at once when the selected civilian
(`0x67E839C`) is zero. Its only speech output is `0x3B20` at the end, which it reaches only
when the selected civilian differs from the previous one at `0x2D9E58` (compare at
`0x97E09`, store at `0x97E11`). `0x4660`, called on every run, is empty; the helpers
`0x18B880` and `0x18CE60` (idle reason, Hunter query `0x18AF50`) emit no speech. An
unchanged selection therefore never speaks.

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
or nested speech.

The civilian source's native guard skips the managed callback for a zero or unchanged
selection, the exact Vanilla condition above. A replacement of an unrouted intent
plays through its voice bank's folder; `UnitVoiceContext.OriginalIsSilent` marks such
requests. All IL edits run through `SafeIlHook` and cannot remove other patches of the
same method. Current implementation and validation results belong to the
workspace's unit-voices run evidence; no game success is asserted here.
