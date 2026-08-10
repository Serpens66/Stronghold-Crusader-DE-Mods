# Updating Active AIV Detector for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The detector is strictly hash-gated. On this hash it validates only the bytes at
the RVAs below and installs direct-address hooks; it performs no full pattern
scan. Every other DLL logs a timestamped Error and receives no native hooks.

## Native address map

| Source pattern | Reference RVA | Use |
| --- | ---: | --- |
| `PrepareLayoutPattern` | `0x53CB0` | selected-layout detour |
| `SelectBestFitPattern` | `0x54F10` | oracle detour |
| `TestSpecificCandidatePattern` | `0x54D90` | oracle detour |
| `LoadCandidatePattern` | `0x552D0` | oracle detour |
| `ApplyRotationPattern` | `0x56620` | oracle detour |
| `EvaluateCandidateFitPattern` | `0x57030` | oracle detour |
| `BuildingPlacementValidatorPattern` | `0x7B010` | validator detour |
| `ExecuteBuildStepPattern` | `0x51740` | optional prebuild trace detour |
| `OrganismRecordTableReferencePattern` | `0x15A27` | RIP-relative organism table |
| `ActiveLayoutIndexReferencePattern` | `0x55F14` | RIP-relative layout-index table (`LEA` at `+3`) |

The named source constants contain the complete patterns. Matching function
signatures alone are not sufficient to approve a changed native layout.

## Required update audit

1. Require exactly one semantic match for every entry and verify its function,
   ABI, instruction boundary and RIP-relative target bounds.
2. Revalidate AIV spec stride `0x6D98` and fields `+0x04`, `+0x0C`, `+0x10`,
   `+0x14`, `+0x28`, `+0x2C`, `+0x30` and `+0x34`.
3. Revalidate placement grids/counters `+0x3DA6C`, `+0x4288C`, `+0x5B4F8`,
   `+0x5B4FC`, `+0x1B9844` and all map-grid offsets in the source.
4. Revalidate organism stride `0x9C`, class `+0x46`, player stride `0x583C`
   and prepared-entry layout `+0x38`/`0x0C`.
5. Run cell and prebuild traces on known maps and compare them with Vanilla.
6. Update all RVAs, then update the shared hash only after every layout passes.
