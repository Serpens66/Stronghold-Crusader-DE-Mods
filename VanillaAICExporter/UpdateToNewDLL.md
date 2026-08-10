# Updating Vanilla AIC Exporter for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

The exporter writes a timestamped Error and performs no export for every other
DLL.

## What must be checked after an update

1. Update the Script Extender/AIC decoder first and revalidate the complete
   native `InternalAIC` size, packing, field order, arrays, and conversion to
   `PublicAIC`.
2. Verify `GameAIManagerAPI.GetAICArray()` still returns the first native AIC
   record and that enum value multiplied by `Marshal.SizeOf<InternalAIC>()`
   selects the intended lord.
3. Compare the current managed `Enums.AILords` with the Script Extender enum and
   update `OfficialLordSlots`. Numeric slots formerly exposed as `SK_DLC4A/B`
   are `Surgeon/Baibars` in Steam build `24530188`.
4. Export every nonempty official slot, round-trip the JSON through the editor,
   and compare representative values with the game.
5. Verify reserved/custom slots are still excluded and `HasAicData` does not
   discard a valid new lord.
6. Generate a new manifest carrying the new DLL hash; do not reuse bundled
   exports whose manifest belongs to an older DLL.
7. Only then update the shared current hash.

## Audit for Steam build 24651686

The updated game initialized `GameAIManagerAPI` and its AIC array without a
signature or layout error. The local Script-Extender decoder, `InternalAIC`,
`PublicAIC` conversion and `AILords` enum are unchanged; no new official lord
slot appeared in the updated managed assembly. A full export/editor round trip
remains a live tool smoke test. The exporter is not installed by the normal
update build set, so no stale manifest is generated or deployed here.
