# Pitch/stairs fix: pitch keeps the ground height (variant B)

Owner: BugfixesAndQoL, migrated from the former `PitchStairsTest_Serp` testmod. Version, dependencies and `NetworkMode` are in [info.json](../info.json). Native sites, register contracts and the update procedure are in [UpdateToNewDLL.md](../UpdateToNewDLL.md#pitchstairs-variant-b-integration). The Vanilla audit is [PITCH_STAIRS_HEIGHT.md](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md) in the native baseline. Implementation: [NativeCode.cs](../src/PitchStairs/NativeCode.cs), [PitchStairsRuntime.cs](../src/PitchStairs/PitchStairsRuntime.cs) and [PitchStairsActivation.cs](../src/PitchStairs/PitchStairsActivation.cs).

## Problem

Vanilla stores a pitch tile 4 lower than its ground (`max(0, h - 4)`). In front of an AIV stair foot (Default 8 + 16 = 24) the step then grows from 16 to 20. A full transition rebuild, which every start and load runs, drops that connection; the AI then marks reachable buildings behind the stair for demolition. While a game runs, the transition from before the pitch can survive until such a rebuild, so the effect typically appears after save and load. Overbuilding existing pitch with AIV or manual stairs/walls additionally adds a redundant +4.

## What the mod does

Variant B removes the drop at its source: HeightGrid keeps the ground height under pitch, so pathing, building access and generic height consumers share one ground plane.

- The pitch-lowering store, the two overbuild +4 restores, the start/load re-lowering, the two +4 compensations for hidden pitch (render and placement cap) are adjusted.
- Start/load normalization also raises pitch that Vanilla saves or maps still hold lowered, on new games, save loads and editor map loads.
- Pitch created through the Extender (`GamePitchManagerAPI.CreatePitch`) uses the native pitch build and is covered by the same site.
- General fire spread and the pitch-to-pitch chain keep Vanilla's virtual lowered height, including the terminal restore window after burn-out.
- Ordinary projectile damage, ranged targeting and disease use the actual (B) ground height.
- Visible pitch is drawn at ground level instead of as a 4-deep ditch; hidden enemy pitch looks as in Vanilla.

User decisions: variant B (initially as a test mod, now integrated into BugfixesAndQoL); Vanilla fire behavior is kept; changed unit stand height over pitch is accepted; visible pitch at ground level is accepted (checked in game on 02.11.2026); scope is the native corrections only. A broader multiplayer start guard is not part of the mod.

## Activation and settings

- Host option `EnablePitchStairsFix`, `[SyncHostOnly]`, default/reset true, under Host / Gates and siege rules / Fixes (`HostSiege`, `SettingsCategory.Fixes`). The native rule requires both `EnableMod` and this option; it runs on every simulation participant, including clients and the editor. Existing host presets, save/map/trail settings and synchronization own the value. No legacy testmod setting needs importing (the testmod had none).
- Installation runs once at `CrusaderLibrary.LibraryLoaded`, before any map or save is loaded. All fourteen sites must install on the audited native hash. Successful installation leaves the native gate at zero until the first `BeforeLoad` for a new session. Failure keeps it zero and Vanilla runs (reported through the persistent-feature health log); unrelated BugfixesAndQoL features remain independent.
- At that first `BeforeLoad`, after saved/received settings restoration and before native placement or load normalization, one snapshot under `EngineInterface.threadLock` selects `ready && EnableMod && EnablePitchStairsFix`. Duplicate/nested notifications do not republish. The selected rule stays unchanged for the entire native world, including load failure, mission end, diagnostic failure and settings restoration after the mission. Only the next session's first `BeforeLoad` may select another rule. `Started` is too late to select it; `Ended` releases the settings lock but never flips the native gate. Published hooks remain installed for the process.
- `NetworkMode` 1: stored heights change pathing and fire, so every player in a lobby needs the same gameplay mod. The ordinary shared host-setting transport is used; an extra per-peer native-readiness/start guard is deferred.
- Loads after the Fixes mod when present (soft dependency). Fixes patches none of the fourteen sites. The BepInEx incompatibility declaration for `PitchStairsTest_Serp` prevents BugfixesAndQoL from loading alongside a residual old testmod; remove that obsolete copy before use. There must never be two owners of these sites.

## Log markers

| Marker | Meaning |
|---|---|
| `PITCH_STAIRS_SITE` | One per site with resolution method and RVA. |
| `PITCH_STAIRS_READY` | All sites published and validated; activation awaits `BeforeLoad`. |
| `PITCH_STAIRS_ACTIVATION` | First `BeforeLoad` selected the session's native enabled word. |
| Persistent-feature health error for `pitch/stairs height fix` | Installation refused; Vanilla heights remain active. |
| `PITCH_STAIRS_SESSION` / `PITCH_STAIRS_SESSION_END` | Mission start/end: `enabled`, `ready`, counters as delta/total. |
| `DEVDIAG PITCH_STAIRS_SCAN` | DEVDIAG builds, mission start: `pitchTiles`, `stillLowered` (pitch still at Vanilla's lowered height), `firstLoweredTile`, grid lengths. |
| `DEVDIAG PITCH_STAIRS_FIRE` | DEVDIAG builds, start and end: `fireSourceCalls`, `fireSourcePitch`, `fireSourcePitchVisible`, `fireNeighborCalls`. |

Counters: `skippedLowerings`, `skippedAivRestores`, `skippedManualRestores`, `loadPitchTiles`, `loadRaisedTiles`, `fireSourceAdjusted`, `fireNeighborAdjusted`.

The standalone plugin previously emitted `PITCH_STAIRS_SESSION_FAILED`, `PITCH_STAIRS_SESSION_END_FAILED` and `PITCH_STAIRS_SESSION_LOG_UNAVAILABLE` for reporting errors. After migration the shared mission adapter isolates and reports callback exceptions; those legacy plugin markers are no longer emitted. Reporting errors do not change the native rule. With the feature disabled, the DEVDIAG scan may legitimately find Vanilla-lowered pitch; interpret `stillLowered` together with the session's `enabled` value.

- They are process totals and are not reset per mission. Delta means "since the previous start or end report", including the end of the previous game and a following load.
- `loadPitchTiles` counts pitch tiles reached by the active load stub; `loadRaisedTiles` counts only actual writes with a different height.
- `skippedLowerings` counts events, not distinct tiles.
- A terminal (burnt-out, restored) pitch tile stays in `fireSourcePitch` but does not increment `fireSourceAdjusted`, so equality of the two is no invariant.
- There are no counters for render and placement cap; `PITCH_STAIRS_READY` proves installation, not the visible result.

## Tests

- The integrated build invokes the migrated [PitchStairs.Tests](../tests/PitchStairs.Tests/PitchStairs.Tests.csproj) suite (native stub tests plus activation tests). The former testmod driver ran `verify.ps1` (installed hash, baseline hash, manifest) and `PitchStairsTest.StubTests.exe`; this is historical provenance, not a remaining driver dependency. The stub tests run Vanilla's block and the stub for every site in one harness (disabled: bit-identical; enabled: only the intended difference), check the installed site bytes, and execute complete original load, pitch-chain, burn-out, cleanup, replacement, projectile, disease and ranged paths with the production generators in a private PE image.
- Static Vanilla audit tools: [PitchStairsAudit.py](../../_inspect/PitchStairs/PitchStairsAudit.py) pins the original-code inconsistencies; [PitchStairsFixCandidates.py](../../_inspect/PitchStairs/PitchStairsFixCandidates.py) compares variant A (logical height in the readers) with variant B (`--mode logical` / `--mode producer`). Both verify the DLL hash and work on private images only.

Game test procedure (save `BepInEx/LogOutput.log` after every run; evaluate `PITCH_STAIRS_SESSION`, `PITCH_STAIRS_SESSION_END` and `DEVDIAG PITCH_STAIRS_SCAN`):

1. **Affected castle:** new game with `Castillo de Burgalimar Pitch Test.aivjson`, let the castle finish, save, load, play on. Buildings behind the pitch stay; units reach the stair in both directions; `stillLowered=0`.
2. **Manual overbuild:** lay pitch, build a wall piece and a stair over it. Expect `skippedManualRestores > 0`; wall and stair have the height of their neighbours; units climb the stair.
3. **AIV overbuild:** an AI castle that places walls/stairs on existing pitch. Expect `skippedAivRestores > 0`.
4. **Save and load with the mod:** `loadPitchTiles > 0`, `loadRaisedTiles` 0 or small, `stillLowered=0`, buildings stay. Repeat over several cycles.
5. **Editor:** load a Vanilla map with pre-placed pitch into the editor (`loadPitchTiles > 0`, `stillLowered=0`), build a wall over pitch, save, start as a game; the wall has normal height.
6. **Fire:** lay pitch, ignite, let it burn out completely. `fireSourceAdjusted` counts non-terminal pitch sources; check spread at height boundaries (pitch in front of stairs or walls).
7. **Low Default heights:** maps with Default 0..3 under pitch (custom or imported maps; the editor brush clamps to 8..156).
8. **Multiplayer:** host and client with identical mod lists, own and enemy pitch, both ignited. Expect no desync. Client log: `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`.

## Multiplayer

Every gameplay stub decides only on simulation data, never on MiscDisplay `0x2000`, which differs per client. With identical live bytes the deterministic install checks give the same result on every peer. An asymmetric install failure (for example RedBird's executable slab within +/-1.75 GiB of a hook target) is technically possible but has not been observed; there is no start guard against it. A start-guard draft with its isolated checks is stored under `.work/a/pitch-fix/001/deferred-mp` and has no runtime wiring.

## Limits

- Stairs already raised by +4 in old saves carry no pitch flag and are not repaired.
- Buildings that a Vanilla save has already demolished or marked (state 3) are not restored; the mod prevents new marks.
- The fire-neighbor stubs call `0x6B910` from a stub without unwind information (usual context-hook practice; relevant only for an SEH exception inside the call).

## Acceptance status

Before migration, the fourteen-site testmod source state passed 11361 native machine checks. That result is not an integrated BugfixesAndQoL build or game acceptance. The integrated fourteen-site implementation still needs game, editor, save and genuine host/client acceptance. The runs below used earlier testmod builds with eleven sites and remain valid for those builds only.

### Dated evidence (02.11.2026, eleven-site builds)

All runs: canonical native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, local single player, host log `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`.

**10:57-11:03, log SHA-256 `725265D7…5D97`, DEVDIAG build `.work/b/working/637.build.json` (PitchStairsTest 0.1.0 with MoatMove 0.1.6, BugfixesAndQoL 1.0.208, APIShared 0.23.0, Script Extender 2.14.3.0).**
- All eleven `PITCH_STAIRS_SITE` markers with `method=reference-rva`, then `PITCH_STAIRS_READY`; no error markers.
- Session 1 NewGame: no pitch, all counters 0.
- Session 2 LoadedSave `test2_pitchstrais2.sav` (written by Vanilla on 01.11.2026 20:44:40, an affected save): `loadPitchTiles=173`, `loadRaisedTiles=173`, scan 173 pitch tiles, `stillLowered=0`. User: buildings behind the pitch stayed, units reached the stair.
- `fireSourceAdjusted`/`fireNeighborAdjusted` stayed 0 although pitch burned; explained by the next run.
- MoatMove's movement findings of the same run: [PathfindingFinalReport.md, section 8](../../PathfindingFinalReport.md#8-feuerbewegung-und-rückkehr--nachtrag-vom-02112026).

**12:00-12:02, log SHA-256 `5D8F4AEE…FC1F`, DEVDIAG build `.work/b/working/638.build.json`.** New game, pitch ignited: `fireSourceCalls=648`, `fireSourcePitch=305`, all 305 with MiscDisplay `0x2000`, `fireSourceAdjusted=0`. The stubs of that build excluded `0x2000`; on pitch this bit means "visible to the local player" (see the baseline), so the stubs left own, allied and burning pitch unadjusted and would have made fire spread client-dependent. The following builds decide on PitchTrap only.

**12:15-12:16, log SHA-256 `DD66B6AA…CE8C`, DEVDIAG build `.work/b/working/643.build.json`.** New game, pitch ignited: 173 lowerings skipped at map start (`loadPitchTiles=0`), scan `stillLowered=0`; at end `skippedLowerings` 212 total, `fireSourcePitch=363`, `fireSourceAdjusted=363`, `fireNeighborCalls=284`, `fireNeighborAdjusted=125`. User: visuals correct.

**12:53-12:58, log SHA-256 `6C87DA93…810D`, same build, Fixes and BugfixesAndQoL loaded.**

| Session | Run | Result |
|---|---|---|
| 1 | `test2_pitchstrais3.sav` (written 12:48:11 with the mod) loaded, played on | 1211 pitch tiles normalized, 0 raised, `stillLowered=0`; then 26 lowerings, 7 manual overbuilds skipped; fire 332/332 sources, 100 neighbours adjusted |
| 2 | New editor map, pitch laid and overbuilt, saved as `Maps\test_pitch.map` | 93 lowerings and 9 manual overbuilds skipped |
| 3 | `test_pitch.map` loaded into the editor (`EditorLoaded`) | `loadPitchTiles=84` (93 - 9), 0 raised, `stillLowered=0` |
| 4 | `test_pitch.map` started as a game (`DLL_LoadMapToPlay`) | 84 normalized, 3 AIV overbuilds skipped, scan 77 pitch tiles, `stillLowered=0` |

Accepted for those builds: save/load with the mod, manual overbuild in game and editor, AIV overbuild, pitch in the editor, editor map load and start, fire counters, visuals (user). The four tiles between 81 and 77 in session 4 are not attributable from the log (plausibly pitch removal by AI construction through `0x62EB0`).
