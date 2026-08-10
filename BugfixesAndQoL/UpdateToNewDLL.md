# Updating Bugfixes and QoL for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

For this exact hash the mod validates each pattern only at its audited RVA and
then registers or patches the direct address. A full scan must never run on this
path. On another hash, independently validated features search executable PE
sections for exactly one semantic pattern match. Fixed tribe/unit layouts remain
inactive until a new DLL has been audited.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `ConstructingFailureStatusPattern` | `0x9124E` | preview patch at `+22` |
| `EuropeanPlacementRejectPattern` | `0x92983` | rejection patch at `+2` |
| `MercenaryPlacementRejectPattern` | `0x92890` | rejection patch at `+2` |
| `EngineerPlacementRejectPattern` | `0x926AA` | rejection patch at `+2` |
| `TunnelerPlacementRejectPattern` | `0x91290` | rejection patch at `+2` |
| `KnightPlacementRejectPattern` | `0x9137F` | rejection patch at `+2` |
| `BedouinPlacementRejectPattern` | `0x9279D` | rejection patch at `+2` |
| `CreateHerdPattern` | `0xD1780` | plague-herd function detour |
| `PopularityExitPattern` | `0xCB50C` | popularity hook at `+32` (`0xCB52C`) |
| `AreaTreatmentPattern` | `0xA0420` | plague area-treatment detour |
| `DiseaseSearchPattern` | `0x9F6B0` | nearest-disease detour |
| `HealerUpdateExitPattern` | `0x150107` | healer common-exit context hook |
| `PeriodicDiseaseFoundPattern` | `0x14F82C` | state-transition context hook |
| `WorkingBuildingExitReferencePattern` | `0x14F6C8` | semantic reference only |
| `SpearmanMovementDecisionPattern` | `0x143B39` | inline movement-decision hook |
| `CalculateMovementSpeedPattern` | `0x19B1C0` | movement-speed detour |
| `UnitTypeUpdateDispatchPattern` | `0x18406C` | dispatch-table reference |
| `MovementCadencePattern` | `0x184163` | cadence context hook |

The named constants in `src` contain the complete authoritative wildcard byte
patterns. Every reference above was checked as one match in the baseline DLL.

## Required update audit

1. Hash the canonical installed DLL and record its Steam build ID and size.
2. Resolve every table entry with `.tools/find_pe_pattern.py`; require exactly
   one match and confirm the function, instruction boundary, ABI, registers and
   pattern offset in the disassembly.
3. Revalidate every original opcode before assembly-point writes and confirm
   restoration remains safe.
4. Revalidate plague manager/player/projectile fields and the popularity
   accumulator at `+0x12EC20`.
5. Revalidate fixed tribe and unit layouts, including tribe `+0x542/+0x54E` and
   movement fields `+0x582`, `+0x65C`, `+0x660`, `+0x688`, `+0x914`, `+0x916`,
   `+0x930`, `+0x99E` and `+0xA64`, before approving a new shared hash.
6. Test each setting enabled and disabled, patch restoration, map reloads,
   plague treatment/popularity, assembly points and synchronized movement.
7. Update the RVAs first and the shared SHA-256 only after all fixed layouts pass.

Missing, ambiguous or locally mismatching signatures must log a timestamped
Error and leave only the affected feature inactive.
