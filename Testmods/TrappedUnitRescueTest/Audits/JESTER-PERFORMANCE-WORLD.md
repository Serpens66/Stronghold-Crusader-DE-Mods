# Jester contact damage and an isolated performance test world

Audit snapshot: 2026-11-26. This document distinguishes confirmed Vanilla behavior from a proposed, artificial test-world setup. It does not describe a rescue-mod defect or a completed performance acceptance.

## Identities and reproducible sources

- Canonical installed `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, rechecked against `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` and its selected database.
- Actual `Assembly-CSharp.dll`: SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Managed visibility/order checks use this actual assembly, not the publicized build reference.
- Native function evidence: selected semantic database, exact current-hash rows at the RVAs below, plus canonical PE tables and decoded instructions. Reproduce with the baseline `tools/semantic/query.ps1 function <RVA>` and restrict evidence to the current binary hash when a query also returns historical matches.
- Saved game observation: `.work/a/rescue-game/148/b930c6f44262/LogOutput.log`, with restoration tracked by that run's `config-recovery.json`. Historical logs remain unchanged.
- Managed lifecycle source: `APIShared/src/Missions/MissionLifecycleService.cs`, `MissionLifecycleState.cs`, `MissionLifecycleContracts.cs`; public API used through `ApiShared.ForMod`.
- Managed lobby/launch evidence independently checked by the candidate-core agent: actual `FRONT_Multiplayer.Update`, `ButtonClicked`, full `StartSkirmishGame`, `Platform_Multiplayer.MPLobby`, `EngineInterface.MultiplayerSetupData` and its transfer representation. This is a dated member/order audit, not an enduring assertion that no future API can expose another option.

## Actual damage, not an inferred health-writer bug

Run148 recorded124 existing melee publications:62 Pre/Post pairs for original Spearman28/global1371674, type24, owner1, source335,143. Reported attacker4010/global1375790 was owner2/type57; the actual managed `eChimps` enum identifies57 as Jester. Native ticks3606..3667 show victim health10000→9876 in increments of minus2. There were no projectile publications in this trace. The original health boundary correctly rejected the changed actor.

This establishes the observed damage path. It does not establish which side initiated combat, why the Jester first entered that neighborhood, or an exclusive cause for unrelated health changes in earlier runs.

## Complete relevant contact and target chain [Vanilla facts]

| RVA | Checked behavior |
| --- | --- |
| `182B00` | Ordinary unit update reaches generic contact handler `195170`, independently of the Jester entertainment state machine. |
| `195170` | Requires live/nonhidden/nonterminal records and nonzero current-health percentage at native `+928`; examines same/neighborhood tile chains, height and teams. Selects enemy996/global998, faces target through197B20, can install reciprocal defender targets, enters native combat AI106 immediately or after the admitted movement step, and calls199110. `+928` is **not** an attack-permission field. |
| `188A20` | Tile-chain candidate reader requires an admissible live target, calls1867A0 and compares `37EDF3C[owner]` team values. Owner0 has separate special semantics; do not infer its behavior from the player1/2 proof. |
| `1867A0` | Target/type/animal eligibility. It reads actual PeaceTimeFlag38722DC, but the relevant non-animal branch explicitly admits network modes0 and99. Skirmish uses99, so extending configured peace cannot prevent the observed contact. |
| `19A240` / table `32B510` | Type initialization writes native `+984` from this table; both actual types24 and57 have byte0. This is the animal/type classification input, not a general military-stance switch. |
| `197B20` | Computes facing toward the selected current target; does not add a peace restriction. |
| `199110` → `180560` | Melee HP writer selects a table entry when caller damage is nonpositive, then applies owner-dependent scaling and actual health/bar/death handling. Table `323230`, stride90 types:57→24 is2,24→57 is20. The observed minus2 is compatible with this native path; the table alone does not establish runtime owner multipliers. |
| `187230` | Additional area-contact traversal also uses current neighborhood/height/team admission and199110. |
| `1853F0` → `1976C0` | After the native enemy reference clears, cleanup restores native saved continuation and reissues normally. No old rescue intent is invented by this audit. |

The full Jester handler `15DF90` distinguishes home return, entertainment-person, entertainment-building, leisure wandering, animation/delay, combat106 and terminal states. Person selector `18B260` only selects the supplied **same owner**, with real life/type/timer checks; building selector `BD320` chooses an owned building. This entertainment selection does not target the hostile Spearman as a joke recipient.

Jester leisure AI7 calls `197050(...,0,0,1)`. The full `E4FC0` planner starts at the **current** position, uses search budget80 for this mode and can publish up to2000 path steps; it does not enforce a keep/home territorial radius. Repeated leisure walks are therefore not bounded by the fixture's current Keep+16 exclusion. A larger arbitrary spawn margin is a risk reduction, not a closed exclusion proof.

## Why the two-player alliance proposal is unsuitable [Vanilla facts]

`DLL_RegisterMultiplayerUser`864C0 transfers the supplied integer team unchanged to native `37EDF3C[playerId]` and the parallel setup byte. The direct native team array uses game-player IDs1..8. Public lobby teams are keyed by member SteamID, not by roster index. `StartSkirmishGame` preserves shared positive teams and normalizes singleton assignments to0.

The actual frontend Update requires at least two distinct team values through `getEnoughTeams`. Although the existing direct automation Play path does not repeat that UI-property guard, bypassing it is not a valid workaround: full native victory function `1D160` groups active participants by team and completes the game when only one living team remains. Two allied participants therefore cannot provide the required ongoing Skirmish baseline. Setting alliance after start has the same native victory problem.

## Jester creation and exact switch layout [Vanilla facts]

`CEF00` performs per-player special-person upkeep. Its Jester branch in full `C23C0` reads original Jester ID/global at `379CFD8/379CFDC + playerId*583C`. When ID is zero, a nonzero **UInt16** at `37EDF82` admits `17FEF0(..., type57)`; successful creation records actual ID/global, initial AI2 and home data. When an existing matching ID/global is present, clearing the switch does **not** remove it: native upkeep continues for that life. The proposed fixture must reject existing records, never clear them.

`CEF00`'s callers are the ordinary scheduler `D1AD0` (slot95 of its0..199 cycle) and `15200` (Chore64/resync reconstruction). `94350` can reach scheduler maintenance through `71670→CDE60→D1AD0`; do not assume that constructor completion alone proves no earlier person was created.

Layout and direct instruction evidence:

- `37EDF80..81`: Lady UInt16. `37EDF82..83`: Jester UInt16. They are independent adjacent values, not one bitfield.
- `951C1`: Skirmish initializer94350 writes DWORD `0x10000` at37EDF80, thus Lady0/Jester1. A fixture must change only the latter **two bytes** and verify the Lady word unchanged.
- `C29AB`: Jester producer compares WORD37EDF82 against zero; `C25CC` separately compares Lady WORD37EDF80.
- Campaign helper51C0 writes Jester at51F0/5230 and Lady at5242/525B. Its verified map-loader caller passes manager3665ED0; `+40` is3665F10 (campaign mission index), **not** a MultiplayerSetupData field. Stage7 introduces Jester and stage11 Lady.
- Reset7160 writes DWORD37EDF80 at71A0 and can retire current local special-person records. It is not an acceptable fixture API: it has additional side effects and unit-state writes.
- Map/campaign/editor loader8AE80:8B30B clears both,8B562/8B5A0 clears the selected words.
- Map loader100530 preserves pre-load words at1005B3/1005C1 and restores them at100E96/100EA7; its campaign51C0 branch applies only in game mode0.
- Old-save migrationD3380 can clear both atD34C5; its campaign-only cleanup reads them atD390C/D391A. Current save version236 takes no old migration branch affecting these values.
- Explicit/random Jester event1045F0(150) sets1 at104A7D. Direct GameAction81870 action0x407 forwards its structureID to this handler. The diagnostic profile must not send this action.
- Scripted event F82B0 event0x16 sets1 atFA795. Its entire relevant event loop is gated by network mode0, excluding Skirmish99.
- Random schedulerD1E50 can select event150 through table311D68 and1045F0. Full CDE60 only calls it for game mode2 plus map type3 plus38722B8; fresh Skirmish94350 sets game mode3. CA660 is its frequency-based scheduler; no public Skirmish Jester-setting mapping was established from it.
- A decoded executable-section reference check for all operands overlapping37EDF80..83 corroborated the above direct reads/writes, including overlapping DWORD writes and the D3380 loop-end LEA. Indirect native reset/copy/save/restore operations remain world lifecycle operations, not per-Jester settings APIs.

Full InternalAIC and actual MultiplayerSetupData/TransferData contain no dedicated Jester control. Public members named Jester in the checked managed/SE assemblies concern enums/rendering/popularity reports, not a verified creation toggle. This negative result is restricted to the checked versions and paths; it is not permission to call an unrelated public reset helper.

## Native save/load and early timing [Vanilla facts]

Canonical PE section-directory table336B20 uses24-byte entries. Entry64 has pointer37ED180, length82474, flag0, compression1023 and contains the Jester word at offsetE02. Full writer52B0 copies/compresses that actual region; it is reached by DLL_SaveSaveGame86F80 and the synchronized save path. Thus zero is part of an ordinary native save, not a separate fixture serializer.

DLL_LoadSaveGame853F0→87D0 clears and restores the same region through the typed section directory, performs version migration and native post-load reconciliation. Current-version saves preserve the word; the loader does not unconditionally restore Jester1 afterwards. A fixture-owned save must already contain0. Its load path verifies0 and never silently writes0 again to hide a broken save contract.

The actual managed new-Skirmish order is loadMultiplayerMap at IL03F9, StartMultiplayerGame at0421, startSimThread at0462, return04BD. APIShared NativeLoaded is published synchronously after the enclosing native load wrapper succeeds and before that wrapper returns. Registering/arming before `Fixture.Start` launches the Skirmish therefore gives the necessary earlier callback; ordinary OnStart/ManagedReady is too late to guarantee no simulation has run. APIShared notifications are outside its internal engine critical section. The fixture must validate/take the unchanged existing engine monitor and keep its fresh-start-only timing qualification; it must not treat an arbitrary load notification as proof of quiescence.

Before either read or write: qualify exact profile, nonreplay fresh NewGame context, expected full map path, matching attempt/session/world, current native Skirmish/network mode, canonical module/hash and committed image-backed readable/writable data span. Check fixed player IDs1..8 only; every original Jester ID/global pair must be empty. Any populated or inconsistent pair is a visible setup rejection without modification.

## Isolated proposed test-world contract [testmod integration]

Two new profiles are proposed; existing profiles retain their old worlds:

- `rescue-diag-nojesters4000-on`:4000 original actors/1000 walkers, existing health-event observer enabled; diagnostic, no performance acceptance.
- `rescue-perf-4000-nojesters-passive-on`:same artificial world, no health observer/extra UnitMove observer; still an explicitly modified world and separate baseline.

Both are exclusively `/devdiag`. Normal builds contain no setter/reader/native typed references; selecting either name fails before starting a game. The scope is armed before StartSkirmish and bound to the actual AutomationRun GUID, exact map and unique protected own save path. It performs one fresh-world WORD write, verifies Lady preservation and readback, and subsequently verifies its state only at existing native-load/paused performance boundaries. No new hook, Tick subscription, per-unit scanner, damage veto, HP/AI/job write or removal is allowed.

An independent foreign setter may still reenable the flag. Any such boundary change, populated original special-person pair, other combat, health/lifetime/goal error remains a failure; the fixture does not repeatedly force the flag back to0. Restoration is allowed only while holding the existing monitor in the same still-qualified own world with the expected current0 state. If the world has been replaced/unloaded or the state changed, do not write old state into it. Native fresh initialization supplies its own default on the next start; there is no process-global settings patch to remove.

Absence of new Jesters does not prove general invulnerability. It can alter entertainment, popularity, natural population and CPU work. The world is therefore labelled `native-state-fixture=true` and `generalPerformanceClaim=false`, with default/previous profiles `native-state-fixture=false`. Four A/B members must use the same newly created native save. Historical148 remains a failed hostile-world observation. No9871 extension, runtime fix or measurement result is implied by this audit or stage.
