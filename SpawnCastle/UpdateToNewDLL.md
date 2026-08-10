# Updating Spawn Castle for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

Native Spawn is strictly hash-gated. The supported hash uses direct RVAs after
local pattern validation and never performs a full DLL scan. On another DLL,
native Spawn remains inactive while managed Blueprint mode stays available.

## Native address map

| Source pattern | Reference RVA | Use |
| --- | ---: | --- |
| `AllocateSpecPattern` | `0x50630` | `AllocateSpecDelegate` |
| `SetPlacementPattern` | `0x54E70` | `SetPlacementDelegate` |
| `SelectBestFitPattern` | `0x54F10` | `SelectBestFitDelegate` |
| `TestSpecificCandidatePattern` | `0x54D90` | `TestSpecificCandidateDelegate` |
| `PrepareLayoutPattern` | `0x53CB0` | `PrepareLayoutDelegate` |
| `ExecuteToPercentagePattern` | `0x55F00` | `ExecuteToPercentageDelegate` |
| `AivStateReferencePattern` | `0x95C4F` | RIP-relative AIV state (`LEA` at `+4`) |
| `PrebuiltPlayersReferencePattern` | `0x95FA8` | RIP-relative bit field |
| `PreparedKeepCoordinatesReferencePattern` | `0x95E53` | RIP-relative Keep X/Y references |

The named source constants contain the complete authoritative byte patterns.

## Required update audit

1. Require one match for every entry and revalidate delegate ABIs, instruction
   offsets and all resolved image bounds.
2. Revalidate AIV spec stride `0x6D98` and fields `+0x08`, `+0x0C`, `+0x10`,
   `+0x14` and `+0x24`.
3. Revalidate player state stride `0x583C`, imported candidate pointers,
   prebuilt-player bits, active-spec index and prepared Keep coordinates.
4. Recheck the `AllocateSpec +0x5F` player-state reference and expected bytes.
5. Test default/custom candidates, rotations, partial/no-fit failures, repeated
   map starts and the multiplayer block without any manual-spawn fallback.
6. Update all RVAs before approving the new shared hash.

Historical RVAs in `AISpawnCastle.md` belong to an older DLL and are not a
source for the current table without a new audit.
