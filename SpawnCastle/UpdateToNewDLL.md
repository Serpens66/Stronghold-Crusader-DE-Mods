# Updating Spawn Castle for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

For every other DLL, native Spawn writes a timestamped Error and remains
inactive. Managed Blueprint mode is still available.

## What must be checked after an update

1. Require exactly one match for AllocateSpec, SetPlacement, SelectBestFit,
   TestSpecificCandidate, PrepareLayout, ExecuteToPercentage, AIV-state,
   prebuilt-player, and prepared-Keep signatures.
2. Revalidate each native delegate ABI and the RIP-relative instruction offsets
   used to resolve globals.
3. Revalidate AIV spec stride `0x6D98` and fields `+0x08`, `+0x0C`, `+0x10`,
   `+0x14`, and `+0x24`.
4. Revalidate player AIV state stride `0x583C`, imported candidate pointer table,
   prebuilt-player bit field, active-spec index, and prepared Keep coordinates.
5. Verify all expected instruction bytes used near derived references before
   calling native code.
6. Test default/custom multi-candidate AIVs, all rotations, partial/no-fit
   failures, repeated map starts, and the multiplayer block. Confirm failures do
   not fall back to another native or manual spawn path.
7. Only then update the shared current hash.

Historical RVAs in `AISpawnCastle.md` belong to the old `17F8DD4A...` DLL and
must never be promoted without new analysis.
