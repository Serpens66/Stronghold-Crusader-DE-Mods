# Updating Active AIV Detector for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

The detector remains hash-gated because its AIV, player, organism and map-grid
layouts cannot be proven by function signatures alone. On the audited hash it
first validates each reference RVA and otherwise uses the named unique `.text`
pattern, which also tolerates an earlier mod replacing a function prologue.
Failure rolls back and disables only the detector/oracle native feature with a
timestamped Error. No native hook is installed on an unaudited layout.

The detector owns the placement-validator detour when installed and exposes a
managed observer registration point for optional consumers. It does not require
BugfixesAndQoL. Conversely, BugfixesAndQoL declares only a soft dependency and
uses its own independently resolved detour when this detector is absent or its
native feature is unavailable. With both installed, only this single native
detour exists; the Bugfix observer runs after the unchanged Vanilla result.

## Native address map

| Source pattern | Reference RVA | Use |
| --- | ---: | --- |
| `PrepareLayoutPattern` | `0x53D00` | selected-layout detour |
| `SelectBestFitPattern` | `0x54F60` | oracle detour |
| `TestSpecificCandidatePattern` | `0x54DE0` | oracle detour |
| `LoadCandidatePattern` | `0x55320` | oracle detour |
| `ApplyRotationPattern` | `0x56670` | oracle detour |
| `EvaluateCandidateFitPattern` | `0x57080` | oracle detour |
| `BuildingPlacementValidatorInteriorPattern` | `0x7B078` | stable body signature; subtract `0x18` to derive validator entry `0x7B060` |
| `ExecuteBuildStepPattern` | `0x51790` | optional prebuild trace detour |
| `OrganismRecordTableReferencePattern` | `0x15A27` | RIP-relative organism table |
| `ActiveLayoutIndexReferencePattern` | `0x55F64` | RIP-relative layout-index table (`LEA` at `+3`) |

The named source constants contain the complete patterns. Resolved RVAs, not
the reference constants, are used for hook installation. Matching function
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

## Audit for Steam build 24651686

All ten patterns have exactly one match in `.text`. Targeted disassembly
reconfirmed AIV stride `0x6D98`, player stride `0x583C`, the documented spec
fields, placement grids/counters and `AllocateSpec +0x5F`. The organism-table
reference remains at `0x15A27`; only code after the early changed block moved
by `0x50`. Live cell/prebuild trace comparison remains a post-build smoke test.

## Audit for Steam build 24816905

All ten patterns and their audited RVAs are unchanged and each matches exactly
once. The placement-validator target now deliberately uses the unique interior
signature at `0x7B078` and derives entry `0x7B060`, because another mod may
already have detoured its prologue. The latest log resolved all ten targets and
confirmed the detector in a live skirmish. The documented AIV, player and
organism layouts remain unchanged; they are the reason unaudited hashes stay
blocked even when code patterns match.
