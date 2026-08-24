# Updating Bugfixes and QoL for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

For this exact hash the mod validates each pattern only at its audited RVA and
then registers or patches the direct address. A full scan must never run on this
path. On another hash, independently validated features search executable PE
sections for exactly one semantic pattern match. Fixed tribe/unit layouts remain
inactive until a new DLL has been audited.

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

The European recruitment function at RVA `0x190C50` clears its result code at
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
demand. For a new DLL, validate the complete ABI, the two result fields, the
ordered horse check and both AI consumers before accepting the signature.

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

## Required update audit

### Selected-unit HP display audit

The display reads live `GameUnit.r_CurrentHealth` and `r_MaxHealth` through the
Script Extender and has no production RVA access. For the canonical DLL with
SHA-256 `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`,
the Script Extender resolved `gUnitHealthTable` at RVA `0x321820` (`.data`, raw
file offset `0x320C20`). All 34 unit types represented by
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

The latest live log showed that `ActiveAIVDetector` can detour the tower
placement-validator prologue before this mod initializes. Tower repair now
resolves the unique stable body signature at `0x7B078` and subtracts `0x18` to
derive function entry `0x7B060` for standalone operation. When ActiveAIVDetector
is present and has installed that detour, the soft-dependency load order lets
this feature register a managed post-Vanilla observer instead of installing an
overlapping hook. Neither mod requires the other. If observer registration is
unavailable, or RVA validation and the standalone pattern fallback both fail,
only the AI tower-ruin repair feature logs Error and remains inactive; all other
fixes and Vanilla placement continue.

The 2026-08-24 finished-castle trace confirmed the complete ruin path on the
audited hash. `STRUCT_TOWER3_DESTROYED` was observed at tick 75120; at tick
77622 the validator returned `2`, the exact same-owner ruin was marked through
`DeleteBuildingSafe`, the remaining footprint saw `MarkedForDeletion`, and a
second Vanilla validation returned `0` before the replacement tower spawned in
the same tick. Validator diagnostics therefore log only the first allowed
callback per player/mapper and one representative blocked category per
five-second diagnostic window. Expected per-tile `MarkedForDeletion` follow-ups are suppressed; the
successful ruin mark itself remains fully logged.

The first-build AIV signature stayed at `0x53F0B`, but its absolute map-row
operand moved from `0x402EF2C` to `0x402FF2C`; that relocation is now wildcarded
while the surrounding player/index/state semantics remain fully validated.
Unit stride `0x490`, tribe fields and the enemy-proximity ChoreManager contract
are unchanged. The latest pre-update log showed exactly the expected hash gates
and this AIV signature failure, with all other fallback signatures resolving.
