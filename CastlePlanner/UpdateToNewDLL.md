# Updating CastlePlanner for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Native Spawn remains hash-gated because its AIV/player layouts and several
field offsets cannot be proven by function signatures alone. On the audited
hash each target first validates its reference RVA and otherwise falls back to
the named unique `.text` pattern; the resolved RVA is used. A failure disables
only native Spawn with a timestamped Error, while managed Blueprint and other
independent features remain available. An unaudited layout is not enabled just
because its code patterns match.

## Native address map

| Source pattern | Reference RVA | Use |
| --- | ---: | --- |
| `AllocateSpecPattern` | `0x50680` | `AllocateSpecDelegate` |
| `SetPlacementPattern` | `0x54EC0` | `SetPlacementDelegate` |
| `SelectBestFitPattern` | `0x54F60` | `SelectBestFitDelegate` |
| `TestSpecificCandidatePattern` | `0x54DE0` | `TestSpecificCandidateDelegate` |
| `PrepareLayoutPattern` | `0x53D00` | `PrepareLayoutDelegate` |
| `ExecuteToPercentagePattern` | `0x55F50` | `ExecuteToPercentageDelegate` |
| `AivStateReferencePattern` | `0x95C9F` | RIP-relative AIV state (`LEA` at `+4`) |
| `PrebuiltPlayersReferencePattern` | `0x95FF8` | RIP-relative bit field |
| `PreparedKeepCoordinatesReferencePattern` | `0x95EA3` | RIP-relative Keep X/Y references |
| `HumanKeepCoordinateLoadPattern` | `0x95B3C` | earliest human Keep X/Y load and start-data hook |

The named source constants contain the complete authoritative byte patterns.

## Required update audit

1. Require one match for every entry and revalidate delegate ABIs, instruction
   offsets and all resolved image bounds.
2. Revalidate AIV spec stride `0x6D98` and fields `+0x08`, `+0x0C`, `+0x10`,
   `+0x14` and `+0x24`.
3. Revalidate player state stride `0x583C`, imported candidate pointers,
   prebuilt-player bits, active-spec index and prepared Keep coordinates.
4. Recheck the `AllocateSpec +0x5F` player-state reference and expected bytes.
5. Recheck that the human-start hook still runs after Vanilla resolves the
   start index but before it first loads Keep X/Y, and that its 16 overwritten
   bytes are exactly the two coordinate-load instructions.
6. Recheck the Lord-initialization diagnostic entry and its player-manager
   offsets `0x130DB8` (Lord unit ID) and `0x130DC0` (Lady unit ID).
7. Test default/custom candidates, rotations, partial/no-fit failures, repeated
   map starts and deterministic per-player multiplayer spawning without any manual-spawn fallback.
8. Update all RVAs before approving the new shared hash.

Historical RVAs in `AICastlePlanner.md` belong to an older DLL and are not a
source for the current table without a new audit.

## Audit for Steam build 24651686

All ten patterns match exactly once. Targeted disassembly reconfirmed every
delegate ABI, AIV stride `0x6D98`, player stride `0x583C`, the spec fields,
prebuilt bit field, prepared Keep references and the unchanged
`AllocateSpec +0x5F` LEA contract. The human-start hook at `0x95B3C` precedes
the first coordinate load and preserves both overwritten instructions. Native singleplayer and synchronized
per-player multiplayer spawning remain post-build game smoke tests.

## Audit for Steam build 24816905

All ten patterns remain at the documented RVAs and match exactly once. AIV spec
stride `0x6D98`, player stride `0x583C`, placement fields, prebuilt state and
Keep-coordinate loads retain their prior semantics. No CastlePlanner source RVA
changes are needed. The resolver now always uses its returned RVA and can use
the unique pattern if local reference bytes were changed by an earlier hook.
The fixed layouts remain the explicit reason for the hash gate. Blueprint mode
was not version-sensitive. Native singleplayer and multiplayer spawning still
need a live smoke test.
