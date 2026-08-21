# Updating Bugfixes and QoL for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

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
| `HealerUpdateExitPattern` | `0x150157` | healer common-exit context hook |
| `PeriodicDiseaseFoundPattern` | `0x14F87C` | state-transition context hook |
| `WorkingBuildingExitReferencePattern` | `0x14F718` | semantic reference only |
| `SpearmanMovementDecisionPattern` | `0x143B89` | inline movement-decision hook |
| `PreTerrainSpeedAdjustmentPattern` | `0x19B4B6` | context hook after base/group speed and before late terrain/status modifiers; containing function `0x19B210-0x19B5D6` |
| `UnitTypeUpdateDispatchPattern` | `0x1840BC` | dispatch-table reference |
| `MovementCadencePattern` | `0x1841B3` | cadence context hook |

The named constants in `src` contain the complete authoritative wildcard byte
patterns. Every reference above was checked as one match in the baseline DLL.

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
6. Test each setting enabled and disabled, patch restoration, map reloads,
   plague treatment/popularity, assembly points and synchronized movement.
7. Update the RVAs first and the shared SHA-256 only after all fixed layouts pass.

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
