# Updating Vanilla AIC Exporter for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

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
