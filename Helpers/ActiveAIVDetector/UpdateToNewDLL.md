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
| APIShared `AivBuildStep` target | `0x51790` | optional prebuild trace observer; APIShared owns the only detour |
| `OrganismRecordTableReferencePattern` | `0x15A27` | RIP-relative organism table |
| `ActiveLayoutIndexReferencePattern` | `0x55F64` | RIP-relative layout-index table (`LEA` at `+3`) |

The named source constants contain the complete patterns. Resolved RVAs, not
the reference constants, are used for hook installation. Matching function
signatures alone are not sufficient to approve a changed native layout.

## Sofortspawn diagnostic data reads

The opt-in observer is owned by ActiveAIVDetector but uses APIShared's single,
hash- and function-hash-gated `0x51790` detour. It reads the TileManager
pointer observed by the existing `0x7B060` validator callback; the pointer
must be consistent before and after each frame. The current-hash TileManager
offsets are `Logic +0x898400` (int32), `Logic2 +0x9D2500` (byte),
`Organism +0xA6F260` (uint16), `Structure +0xB0BCA0` (uint16),
`TileUnit +0xBF6C00` (uint16), `Height +0xD7E5A0` (byte),
`DefaultHeight +0xDCCAC0` (byte), and `WallOwner +0xE1AFE0`
(byte), each with 320800 packed tiles. These offsets agree with the
canonical local Script Extender's `GameTileManagerView`; the feature is disabled on
any other native hash because layout signatures alone cannot validate them.
The associated building metadata comes through the installed Extender's
`GetBuildingsAsSpan()` view, with `buildingId = spanIndex + 1`.

At AIV selector entry the diagnostic reads raw global values at current-hash
RVA `0x8574B90` (game mode, int32), `0x87EE2F0` and `0x87EE2F4`
(start options, int32), and `0x87EE2F8` (raw 64-bit option state). These
addresses are derived from the audited `0x94350` branch and its writers in
the current semantic baseline. The read is bounded by the loaded image
length; an out-of-image address is reported as unavailable. There is no
version-independent signature for the data layout: the existing full-DLL
hash gate is mandatory, and changed hashes disable this native feature.
The values are observations, not a claimed managed-to-native option mapping.

## Required update audit

1. Require exactly one semantic match for every entry and verify its function,
   ABI, instruction boundary and RIP-relative target bounds.
2. Revalidate AIV spec stride `0x6D98` and fields `+0x04`, `+0x0C`, `+0x10`,
   `+0x14`, `+0x28`, `+0x2C`, `+0x30` and `+0x34`.
3. Revalidate placement grids/counters `+0x3DA6C`, `+0x4288C`, `+0x5B4F8`,
   `+0x5B4FC`, `+0x1B9844` and all map-grid offsets in the source.
4. Revalidate organism stride `0x9C`, class `+0x46`, player stride `0x583C`
   and prepared-entry layout `+0x38`/`0x0C`.
   Revalidate the eight TileManager layer offsets and the four raw start-option
   globals above against both native readers/writers and Extender views.
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
