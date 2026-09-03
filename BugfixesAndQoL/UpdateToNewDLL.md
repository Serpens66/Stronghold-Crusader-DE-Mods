# Updating Bugfixes and QoL for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

For this exact hash the mod validates each pattern only at its audited RVA and
then registers or patches the direct address. A full scan must never run on this
path. On another hash, independently validated features search executable PE
sections for exactly one semantic pattern match. Fixed tribe/unit layouts remain
inactive until a new DLL has been audited. The same rule now explicitly covers
the plague player/projectile layouts and the AI recruitment result structure.

## Complete feature audit for Steam build 24816905

The installed native DLL, the managed assembly, and the Script Extender sources
were compared with the semantic baseline before this audit:

- installed `CrusaderDE.dll`: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
- installed `Assembly-CSharp.dll`: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`;
- Script Extender commit: `171d68e155a8f98c5f8c4ee154d9af154c9a2443`.

The README currently describes 28 independently configurable features (13 fixes
and 15 quality-of-life features). Every entry was traced from its setting through
its runtime implementation to the relevant native, managed, Script Extender, XAML,
network, or filesystem contract. The result is:

| Feature | Audited surface | Result |
| --- | --- | --- |
| Demolition cursor near enemies | Managed `Director.setCursor`, Script Extender repair-proximity function, building begin position, and hash-gated `ChoreManager +0x870` mode flag | Valid; fixed layout remains fail-closed on other hashes |
| HD-style minimap controls | Managed `FatControler` radar fields/methods and Vanilla camera `GameAction` calls | Valid against the managed baseline |
| Market-hotkey main-menu return | Script Extender key event, selected-market validation, and managed market-panel state | Valid |
| Display-resolution persistence | Managed `FatControler`, Unity display/focus state, and explicit apply guards | Valid |
| Synchronized mixed-group movement | Three unique native signatures at `0x143BD9`, `0x19B506`, `0x18410C`/`0x184203`; unit stride `0x490`, manager header `0x65C`, tribe fields and cadence states | Valid; fixed layouts remain hash-gated |
| Plague and apothecary fixes | Seven unique creation, popularity, search, treatment, healer-exit, and state-transition signatures plus projectile/unit identities | Valid for the audited hash; fixed player/unit/projectile layouts are now fail-closed on unknown hashes |
| Unrestricted rally points | Seven unique rejection sites inside `0x90CD0-0x92F31`, with original conditional-jump bytes verified before every write | Valid |
| Custom Trail extreme-gold fix | Managed Trail Maker/customize load and save flow | Valid; no native address dependency |
| AI knight horse-demand fix | Unique recruitment entry `0x190CA0`, result fields `+0x650/+0x654`, ordered equipment/horse checks, and AI demand consumers | Valid for the audited hash; `+0x654` is now fail-closed on unknown hashes |
| AI tower rebuilding | Unique broad/narrow classifiers `0x5D025/0x5D055`, complete instruction spans, stack inputs, runtime building identity, and Vanilla cleanup flow | Valid |
| Better AI overbuild rules | Unique mapper/blocker sites `0x5CEAB/0x5D016/0x5D045`, complete spans, stack inputs, protected-yard policy, and conflict guard | Valid |
| AI stone reserve | Unique seller hook and six AIV layout/lifecycle signatures; slot/step strides, player mapping, and first-build states | Valid |
| Autotrade sell threshold zero | Managed HUD slider callback, state fields, and `Autotrade_SetSell` action | Valid |
| Map-origin sorting | Managed map header/list contracts and stable malformed-entry fallback | Valid |
| Vanilla maps in the editor | Managed load/save requester flow and canonical-path containment policy | Valid; source Vanilla and Workshop files remain protected |
| Detailed-market goods order | Managed/XAML bindings, local preset storage, and visual refresh | Valid |
| Ctrl single-unit market trade | Four unique validator, packet, storage, and statistic signatures in their expected functions | Valid; future hashes require all four signatures together |
| Ally goods-transfer modifiers | Managed allies-panel button/update hooks and displayed amount bindings | Valid |
| Steam lobby invitations | Managed Steam callback/friend/lobby validation and Vanilla leave/join flow | Valid; local blacklist does not alter Steam handling |
| Camera movement with modifiers | Managed camera input hook and key-state filtering | Valid |
| Custom-lord/random-opponent selection | Managed lobby lists, slot caps, lord metadata, and random-button refresh methods | Valid |
| AIV/AIC selection | Managed AI-settings fields/methods, bounded codec/presets, Script Extender `ImportAIV`, and validated multiplayer manifest/Chore flow | Valid |
| Game-speed controls | Managed key/slider hooks, permission policy, pause action, and validated Chore transport | Valid |
| Surrender and spectator features | Managed lord resolution/statistics UI/Chore flow plus local-only native `SpectatorMode` dispatch and player-ID restore sequence | Valid |
| Resync host kick | Managed authenticated lobby members, heartbeat age, host authority, and forced-kick method | Valid |
| Multiplayer lobby return | Managed lobby identity/replacement/join lifecycle | Valid |
| Selected-unit health | Script Extender unit fields, managed troop paging, and current health table at RVA `0x322820` | Valid; all inspected defaults are divisible by display scale 10 |
| Assassin climbing/control | Unique builder/reconstruction signatures, coordinate/global layers, states `126-129`, unit fields `+0x40F/+0x414/+0x416`, per-player Chore, and Script Extender selected-unit Pre event | Valid; the redundant mod-owned selected-unit detour was removed |
| Lord troop-HUD controls | Managed lord identity, troop-HUD methods, command routing, and synchronized surrender replacement | Valid |

The focused native regression test now checks 46 production and supporting
semantic signatures. Every signature matched exactly once in an executable PE
section at its declared reference RVA. It also verifies 23 contiguous function
hashes and the complete critical overwrite spans; the current run completed
with 320 assertions.
The PE runtime-function table placed each match in the expected containing
function. Baseline Xrefs show no incoming branch into the interior of the eight
fixed-length inline overwrite spans used by movement, AI stone reserve, tower
repair, and overbuild. Context callbacks preserve every register they read or
modify, and their before/after placement retains the original instruction data
flow. Direct byte patches validate current bytes, transition atomically, and
restore only from a known state.

Direct span loops use explicitly named zero-based indices. Calls to
`TryGetUnitById`, `TryGetBuildingById`, and tribe APIs retain one-based game IDs.
The mod does not use the exceptional zero-based `BuildingR3EventHooks.OnTogglePause`
ID or the defective `BuildingExtensions.UpdateLocalGoodsResourceVisuals` helper.

The Assassin Stop callback now subscribes to the Script Extender's existing
`OnTribeIssueOrderWithTarget` event and acts only in its `Pre` phase. The former
mod-owned detour at `0x199C70`, its trampoline, and its selected-command pattern
were removed. The field mutation itself remains gated by the audited native hash
because offsets `+0x40F/+0x414/+0x416` are fixed unit-layout contracts. After the
callback, the Script Extender invokes Vanilla unchanged so it can clear the
tribe's paths and orders normally.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `ConstructingFailureStatusPattern` | `0x9129E` | preview patch at `+22` |
| `EuropeanPlacementRejectPattern` | `0x929D3` | rejection patch at `+2` |
| `MercenaryPlacementRejectPattern` | `0x928E0` | rejection patch at `+2` |
| `EngineerPlacementRejectPattern` | `0x926FA` | rejection patch at `+2` |
| `TunnelerPlacementRejectPattern` | `0x912E0` | rejection patch at `+2` |
| `KnightPlacementRejectPattern` | `0x913CF` | rejection patch at `+2` |
| `BedouinPlacementRejectPattern` | `0x927ED` | rejection patch at `+2` |
| `CreateHerdPattern` | `0xD17D0` | plague-herd function detour |
| `PopularityExitPattern` | `0xCB55C` | popularity hook at `+32` (`0xCB57C`) |
| `AreaTreatmentPattern` | `0xA0470` | plague area-treatment detour |
| `DiseaseSearchPattern` | `0x9F700` | nearest-disease detour |
| `HealerUpdateExitPattern` | `0x1501A7` | healer common-exit context hook |
| `PeriodicDiseaseFoundPattern` | `0x14F8CC` | state-transition context hook |
| `WorkingBuildingExitReferencePattern` | `0x14F768` | semantic reference only |
| `SpearmanMovementDecisionPattern` | `0x143BD9` | inline movement-decision hook |
| `PreTerrainSpeedAdjustmentPattern` | `0x19B506` | context hook after base/group speed and before late terrain/status modifiers; containing function `0x19B260-0x19B626` |
| `UnitTypeUpdateDispatchPattern` | `0x18410C` | dispatch-table reference |
| `MovementCadencePattern` | `0x184203` | cadence context hook |
| `MarketValidatorPattern` | `0xD7080` | Ctrl single-unit market validator detour |
| `MarketPacketTailPattern` | `0xD7324` | market packet globals and sender |
| `MarketStorageCallPattern` | `0xD7119` | available-storage delegate |
| `AutoMarketSellStatisticPattern` | `0xD0484` | market-sell statistic table |
| `RecruitEuropeanUnitPattern` | `0x190CA0` | European troop-recruitment detour; missing-good output at manager `+0x654` |
| `SellerReservePattern` | `0x3F14F` | AI stone seller-reserve hook at `+0x07` |
| `AivSlotLayoutPattern` | `0x5068A` | validates AIV slot stride and player-state derivation |
| `AivStepLayoutPattern` | `0x517C2` | validates step layout/state fields |
| `AivHighestFramePattern` | `0x55F64` | validates highest-frame/player-state access |
| `AivResourceShortageReturnPattern` | `0x51842` | validates resource-shortage control flow |
| `AivFirstBuildSuccessPattern` | `0x5216D` | validates normal-building success state |
| `AivPlacementRetryPattern` | `0x5217A` | validates retry/farm state writers |

The named constants in `src` contain the complete authoritative wildcard byte
patterns. Every reference above was checked as one match in the baseline DLL.

## AI recruitment horse-demand audit

The European recruitment function starts at RVA `0x190CA0` and clears its result code at
manager `+0x650`, then checks gold, three market-good requirements and finally
the special horse requirement (`-1`). A missing market good writes result code
`2` and the good id to `+0x654`. The missing-horse branch at RVA `0x190DA6`
writes the same result code but leaves `+0x654` unchanged. AI bodyguard and
economy-protection recruitment at RVAs `0x40330` and `0x40430` interprets that
stale id as a market good and writes `TradeAmountEquipment` into its demand
array at player offset `+0x131630`. The buyer at RVA `0x3ECA0` consumes that
demand, while the independent seller at RVA `0x3EE10` applies the common
`MaxEquipment` threshold to all weapons.

The detour restores the missing-output invariant by setting `+0x654` to
`STORED_NULL` before Vanilla runs when AI Fixes are enabled. A real resource
failure overwrites it again; a horse-only failure cannot create a market-good
demand. The entry signature itself proves `+0x650`, but not the later `+0x654`
writes. Consequently a unique entry match is no longer sufficient on an
unknown hash. For a new DLL, validate the complete ABI, the two result fields, the
ordered horse check and both AI consumers before accepting the signature.

## Deep detour, hook, and RVA audit (2026-09-02)

The deeper pass treated `candidate` and `probable` semantic baseline entries as
deviation-reporting evidence while still requiring bytes, control flow, and
data flow before changing behavior. The native hash and every audited dataset
match `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

### Function detours and ABI

| Detour | Native contract on the canonical DLL | Result |
| --- | --- | --- |
| AI European recruitment `0x190CA0` | `int(manager, unitType, spawnContext, playerId, validationOnly)`; RCX/RDX/R8/R9 plus stack argument 5 | Correct; function start in the earlier text was corrected from `0x190C50` to `0x190CA0` |
| Plague herd creation `0xD17D0` | `void(diseaseManager, buildingId)` in RCX/EDX | Correct |
| Plague area treatment `0xA0470` | `void(projectileManager, unitId)` in RCX/EDX | Correct |
| Disease selection `0x9F700` | `int(projectileManager, unitId)` in RCX/EDX | Correct |
| Market validator `0xD7080` | `void(selling, tradeMode, good)` in ECX/EDX/R8D; storage helper receives manager/player/good in RCX/EDX/R8D | Correct |
| Assassin cost builder `0xD9C40` | seven arguments: context, start X/Y, target X/Y, node limit, continuation; arguments 5-7 retain the Windows x64 stack contract | Correct |
| Assassin special-tile predicate `0x107160` | predicate context and tile ID in RCX/EDX, low-byte result | Correct |

### Inline/context hooks and overwrite contracts

- The popularity callback at `0xCB57C` runs before the displaced report-field
  write. `R14D` is the one-based player loop value, `RBP = player * 0x583C`,
  `R12` is the player-manager base, `EDX` is the current accumulated popularity,
  and `AX` is the signed Vanilla modifier. The callback updates all three
  mutually dependent outputs before Vanilla stores `AX`.
- The healer-exit callback at `0x1501A7` runs before the first epilogue restore;
  therefore `EBP` still contains the healer game ID. The branch at `0x14F8CC`
  likewise follows the successful `0x9F700` call while `EBP` remains the ID.
- The movement span at `0x19B506` is exactly 14 bytes (`7,3,2,2`) and ends before
  late terrain/status modifiers. The cadence hook at `0x184203` observes the
  unit-relative cadence fields only after its displaced loads. The Spearman
  replacement at `0x143BD9` consumes exactly three complete instructions and
  branches only to the audited walking/running blocks.
- AI stone reserve starts at `0x3F156` and overwrites exactly 20 bytes. Its
  callback executes before the displaced threshold calculation, preserves all
  registers, and changes only `R9D`, which is the later reserve surcharge.
- Tower and overbuild hooks remain inside `0x5CD90-0x5D1C5`. Their 10/15/16/20
  byte spans end on instruction boundaries. Stack arguments `+0x98..+0xB8`
  follow directly from the eight nonvolatile pushes and `sub rsp,0x48`; these
  paths remain exact-hash-only.
- The Assassin combat-resume hook at `0x197716` covers exactly 14 bytes and runs
  before the saved state write at `0x197724`. `RDI` remains the unit ID, and the
  caller return address is at `RSP+0x38` after the helper prologue. Both success
  and failure exits of `0x196280` clear the same context global.
- Assembly-point and Assassin reconstruction patches still validate every
  original branch byte before writes, reject foreign mutations, verify writes,
  and roll back already attempted sites on a partial transition.

### Corrected compatibility policy

The deeper pass found one real policy defect rather than a wrong canonical RVA
or argument order. The plague hooks and AI horse-demand detour previously
accepted an unknown DLL after a unique code-pattern match. Their callbacks also
depend on fixed fields that those patterns do not completely prove:

- plague: unit `+0x2BE/+0x39A/+0x39C`, projectile phase/layout, player stride
  `0x583C`, popularity accumulator `+0x12EC20`, and related identity fields;
- recruitment: the entry pattern proves result code `+0x650`, but the
  missing-good output `+0x654` occurs only in later branches.

Both groups now abort before hook construction unless the audited fixed-layout
hash matches. Vanilla remains active. Assembly-point placement, Ctrl market,
and AI stone reserve retain their unknown-hash paths because they validate all
used native instructions/targets as a complete feature set and do not assume an
unvalidated fixed callback layout. Movement, tower/overbuild, Assassin, enemy
proximity, and the selected-health table were already hash-gated where needed.

The focused test lives in `_inspect/BugfixesAndQoLNativeTests`. Besides the DLL
hash, function hashes, signatures, executable-section membership and full
overwrite bytes, it enforces both new unknown-hash gates so this distinction
cannot silently regress.

Runtime diagnostics log the resolution method and active setting state. For
knights they report the first horse-only result per player, every occurrence
that actually discarded a stale sword or metal-armour id, and a periodic
summary after each 100 horse blocks. The first genuine sword and metal-armour
shortage per player is also logged, as is the first error-free knight check
after a horse block. These messages allow the fix path and the unchanged
Vanilla equipment path to be distinguished without logging every recruitment
attempt.

## Eliminated-player spectator audit

This feature calls Vanilla's managed `EngineInterface.GameAction` and does not
patch or directly access an RVA. Its multiplayer safety nevertheless depends on
the following audited native semantics for the baseline hash:

- `DLL_GameAction` is exported at RVA `0x81870`. Command `1073`
  (`SpectatorMode`) dispatches to RVA `0x823A9` and only stores `1` in the
  process-local spectator flag (global RVA `0x3665080`). It does not enqueue a
  network command or accept a target player.
- `DLL_RunTick` reads that flag at RVA `0x868DC`. When set, it temporarily
  replaces the process-local player ID with `0` for the tick/output pass and
  restores the original ID at RVA `0x86927` immediately after the call.
- Managed Vanilla enables this path only for an all-AI local skirmish. Using it
  after elimination in real multiplayer is therefore a mod extension, not a
  Vanilla-supported multiplayer spectator slot. The mod must retain the human
  player slot, require an authenticated local human member, never send a packet
  or Chore for this transition, and verify that both native and managed local
  player IDs remain unchanged after the flag becomes visible in `PlayState`.

These RVAs document why the feature is considered local-only on the audited
baseline; the runtime does not address them and is therefore not hash-gated.
For a new DLL, recheck both command dispatch and the complete save/set/call/
restore sequence as a compatibility audit. Runtime mode, local-human identity,
lord transition, and post-transition identity checks remain fail-closed.

## Features transferred from Extra Features in 1.0.118

BugfixesAndQoL now owns single-building pause, fast recruit rally movement,
reachability-aware gate closing, both quarry-pile features, and all four AI
economy-protection settings. There is no runtime or settings fallback to
ExtraFeatures.

Like the other BugfixesAndQoL features, these transferred features are not
restricted by `GameplayModActivationGate` or `GameplayFeatureModePolicy` and
may run in every game mode when the mod and their individual settings are enabled.

| Feature | Native or managed contract |
| --- | --- |
| Single-building pause | One-based building IDs, `r_IsSleeping`, managed pause UI hooks, synchronized Chore transport, and sleep-state resynchronization |
| Fast recruit rally movement | Script Extender unit events plus the mod-internal synchronized movement-cadence callbacks; no reflective cross-mod bridge |
| Reachability-aware gate closing | `GatehouseQueryEventArgs`, the Script Extender 1.42.0 zero-based UnitId exception, gate entries and PCL reachability; exact-hash-only and fail-open |
| Quarry-pile relocation | Helper `0xC0270..0xC04BE`, manager globals, `GameBuilding` size `0x32C`, pile link `0x192`, structure group `0x2A8`, and one-based building IDs; exact-hash-only |
| AI economy protection | Handlers containing `0xC7DCB`, `0x2F454`, `0x3B1D0`, and `0x3B2FF`; owner/sleep fields, demolition callers, and the single-building manual override |

The moved native address map is:

| Source pattern | Reference RVA | Unknown-hash behavior |
| --- | ---: | --- |
| `SleepStateComparisonPattern` | `0xC7DCB` | Bounded scan; AI-owner suppression still requires the audited fixed layout |
| `SleepStateSynchronizationFunctionPattern` | `0xC7D50` | Bounded scan and validated delegate |
| `EmergencyDemolitionComparisonPattern` | `0x2F454` | Bounded context-hook scan |
| `AIHovelDemolitionFunctionPattern` | `0x3B1D0` | Bounded detour scan at the AI-only decision point |
| `InaccessibleBuildingComparisonPattern` | `0x3B2FF` | Audited-hash-only context hook |
| `SetupBuildingEntrancesOffsetPattern` | `0xC0270` | Audited-hash-only fixed manager/candidate layout |

Before accepting a new DLL, revalidate the complete hook spans, ABI/register
contracts, building layouts, quarry manager fields `+0x31B7D0/+0x31B7D4`,
candidate semantics, AI-only hovel caller, PCL query context, and the UnitId
contract of the installed Script Extender. Unknown or ambiguous evidence must
leave the affected behavior inactive and preserve Vanilla.

## Required update audit

### Selected-unit HP display audit

The display reads live `GameUnit.r_CurrentHealth` and `r_MaxHealth` through the
Script Extender and has no production RVA access. For the canonical DLL with
SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
the Script Extender resolves `gUnitHealthTable` at RVA `0x322820` (`.data`, raw
file offset `0x320E20`). All 34 unit types represented by
`HUD_Troops.SetSelectedTroopVisible` have default HP divisible by 10: the range
is 5,000 (ladderman/engineer) through 240,000 (siege tower). The HUD therefore
displays current and maximum sums divided by 10 and rounded to the nearest
integer, with midpoint values rounded away from zero. Recheck this table and
the HUD-supported type list before retaining that scale for a new DLL.

1. Hash the canonical installed DLL and record its Steam build ID and size.
2. Resolve every table entry with `.tools/find_pe_pattern.py`; require exactly
   one match and confirm the function, instruction boundary, ABI, registers and
   pattern offset in the disassembly.
3. Revalidate every original opcode before assembly-point writes and confirm
   restoration remains safe.
4. Revalidate plague manager/player/projectile fields and the popularity
   accumulator at `+0x12EC20`.
5. Revalidate the pre-terrain speed hook's complete 14-byte span
   (`7,3,2,2`, end `0x19B4C4`), the following instruction, every direct branch
   in `0x19B210-0x19B5D6`, and confirm that no control flow enters the span's
   interior. Revalidate fixed tribe and unit layouts, including tribe
   `+0x542/+0x54E` and
   movement fields `+0x582`, `+0x65C`, `+0x660`, `+0x688`, `+0x914`, `+0x916`,
   `+0x930`, `+0x99E` and `+0xA64`, before approving a new shared hash.
6. Revalidate the Ctrl-market validator, packet globals, sender/storage calls
   and statistic table together before enabling the single-unit trade feature.
7. Test each setting enabled and disabled, patch restoration, map reloads,
   AI knight recruitment with and without horses, genuine equipment shortages,
   market buys/sales, plague treatment/popularity, assembly points and synchronized movement.
8. Update the RVAs first and the shared SHA-256 only after all fixed layouts pass.

Missing, ambiguous or locally mismatching signatures must log a timestamped
Error and leave only the affected feature inactive.

## Audit for Steam build 24651686

Every table signature matched exactly once. The pre-terrain speed signature is
unique at `0x19B4B6`; its 14-byte overwrite span is `7,3,2,2` bytes, ends at
`0x19B4C4`, has no incoming branch into its interior and precedes Vanilla's
late terrain/status modifiers. The original assembly-point bytes, plague hook
boundaries and movement ABIs were checked at the new RVAs. Native
unit access still uses manager header `0x65C`, unit stride `0x490` and all fixed
movement fields listed above. The tribe free-speed fields still map from the
native tribe record to Script-Extender offsets `+0x542/+0x54E`. The enemy
proximity caller still reads `ChoreManager +0x870` and selects Vanilla ranges
`30`/`15`. Functional enabled/disabled and reload testing remains a post-build
game smoke test.

The reference-hash AIState-101 cadence audit covers all 15 recruit types with
a native positive run bonus. `CHIMP_TYPE_ARAB_HORSEMAN` uses the normal fast
pair `bonus=2/state=0x1`; state `0x111` belongs to a conditional later branch
and must not be promoted to the general rally-running state. The Sapper's
positive running pair is `bonus=1/state=0x81`; its state `0x1` path carries no
positive run bonus. Healer (`0x5C1`) and Skirmisher (`0x101/0x181`) retain their
audited type-specific conditional states.

## Audit for Steam build 24816905

All native patterns were independently scanned. Unchanged RVAs cover assembly
points, market trades, plague creation/popularity/search/treatment, tower repair
and most AIV state sites. The following code moved by `0x50`: periodic disease
`0x14F8CC`, working-building exit `0x14F768`, healer exit `0x1501A7`, Spearman
decision `0x143BD9`, synchronized movement sites `0x19B506`, `0x18410C` and
`0x184203`, and European recruitment `0x190CA0`. The movement-speed function is
therefore now `0x19B260`.

Earlier diagnostics used the placement validator at `0x7B060`, including an
optional observer shared with ActiveAIVDetector. They established the failing
ruin case but are not needed by the final fix and have been removed, eliminating
that hook overlap entirely. The production fix uses separate inline classifiers
at `0x5D025` and `0x5D055`. Both are installed atomically and only for the
audited DLL hash because their callbacks rely on fixed stack slots and the
building-record layout.

The AIV placement helper at `0x5CD90` already has a complete native obstruction
cleanup. Its broad two-pass branch admits tower ruins 79 and 86-89, but its
single-pass branch outside Manhattan distance 20 from the stored keep
(`abs(dx) + abs(dy) > 20`) rejects every type above 33 before cleanup. The inline callback changes only the
temporary classifier value in the active branch to native-deletable type 3 for an exact,
runtime-tracked, same-owner AI ruin. The real building record is not changed and
Vanilla reloads its true type before running its own deletion and tile cleanup.
This mirrors UCP2's `ai_rebuildtowers` strategy of routing ruins into an existing
native demolition branch instead of treating them as empty ground.

Runtime tower ruins are captured from post-map-start building-spawn events and
validated at the classifier by building ID, global ID, owner, type and anchor.
Preplaced, human, enemy, reused-ID and unrelated ruins therefore remain Vanilla.
There is no independent 30-second deletion scan and no `DeleteBuildingSafe`
mutation in this feature. Ruin cleanup happens at Vanilla's next matching AIV
placement attempt and is independent of ExtraFeatures rebuild delay and enemy
proximity; the later tower placement itself remains governed by Vanilla and the
configured rebuild rules.

## Release-quarantine rollback

The temporary `1.0.79` DLL-update release quarantine has been removed. The
checkbox is visible again and defaults/resets to enabled. If the mod, the AI-fix
group or this checkbox is disabled, `AITowerRuinRepairFix` is not constructed
and installs no native classifier hooks. Enabling it
later retries initialization against the retained canonical DLL mapping. If it
is disabled after installation, the inline hook is disabled and restores the
original classifier path. Runtime ruin tracking is cleared and performs no
mutation or feature logging while disabled.

The first-build AIV signature stayed at `0x53F0B`, but its absolute map-row
operand moved from `0x402EF2C` to `0x402FF2C`; that relocation is now wildcarded
while the surrounding player/index/state semantics remain fully validated.
Unit stride `0x490`, tribe fields and the enemy-proximity ChoreManager contract
are unchanged. The latest pre-update log showed exactly the expected hash gates
and this AIV signature failure, with all other fallback signatures resolving.

## Tower-ruin footprint validation

The 2026-08-25 multi-target trace showed that destroyed tower records do not
always carry usable end coordinates. Observed live examples included inverted
Y bounds `(382,116)-(387,115)`, inverted X bounds `(396,117)-(395,119)` and an
end position of `(0,0)`. These records nevertheless remained registered on the
correct occupied tiles in Vanilla's `StructureGrid`.

The placement validator supplies the concrete blocked tile, and
`GameTileManagerAPI.GetTileBuildingId(tileId)` reads the building ID directly
from that exact `StructureGrid` entry. It remains useful for diagnosing whether
a tracked or untracked ruin blocks an AIV target, but no longer controls cleanup.
The later inline classifier validates the stable spawn identity again before it
lets Vanilla enter native cleanup.
