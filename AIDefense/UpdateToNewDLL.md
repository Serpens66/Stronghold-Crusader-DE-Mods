# Updating AI Defense for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

AI Defense intentionally remains inactive on every other DLL. The shared version
check writes a timestamped Error before any runtime hooks are subscribed.

## What must be checked after an update

1. Update the shared current DLL hash only after completing this checklist.
2. Rebuild or update the Script Extender first and verify the layouts of
   `GameUnit`, `GameBuilding`, and `GameTribe` used by `AIDefenseRuntime`.
3. Revalidate the fields for alive state, unit/building type, owner, global ID,
   current tile, occupied building tiles, tribe ID, AI tribe role, and the
   related AI-role value.
4. Revalidate the unit/building query convention. The mod currently converts
   zero-based query indices to one-based game IDs.
5. Verify the Tribe and Unit event argument layouts and that setting
   `SkipOriginalFunction` plus `ReturnValue` still cancels the native order.
6. Test tower discovery, defender creation, private-tribe creation, order
   blocking, map unload, and a second map in the same process.

If any item cannot be proven, keep `requireCurrentVersion: true` and do not add
the new hash.
