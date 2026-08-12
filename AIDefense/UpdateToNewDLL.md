# Updating AI Defense for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

AI Defense intentionally remains inactive on every other DLL. The shared version
check writes a timestamped Error before any runtime hooks are subscribed.

## What must be checked after an update

1. Update the shared current DLL hash only after completing this checklist.
2. Rebuild or update the Script Extender first and verify the layouts of
   `GameUnit`, `GameBuilding`, and `GameTribe` used by `AIDefenseRuntime`.
3. Revalidate the fields for alive state, unit/building type, owner, global ID,
   current tile, occupied building tiles, tribe ID, AI tribe role, and the
   related AI-role value.
4. Revalidate the unit/building query convention. Script Extender 1.41 returns
   one-based game IDs, which the mod passes directly to the public accessors.
5. Verify the Tribe and Unit event argument layouts and that setting
   `SkipOriginalFunction` plus `ReturnValue` still cancels the native order.
6. Test tower discovery, defender creation, private-tribe creation, order
   blocking, map unload, and a second map in the same process.

If any item cannot be proven, keep `requireCurrentVersion: true` and do not add
the new hash.

## Audit for Steam build 24651686

The current Script Extender resolved the unit, building and tribe managers in
the updated game without scanner errors and logged `SizeOf(GameUnit)=1168`
(`0x490`). Targeted native disassembly reconfirmed unit stride `0x490`, building
stride `0x32C`, one-based IDs and the manager header used by the public APIs.
The managed `GameUnit`, `GameBuilding`, `GameTribe` definitions and event args
are unchanged, including every field used by `AIDefenseRuntime`. Script Extender
1.41's `GetAllUnits` and `GetAllAliveBuildings` results were verified as
one-based game IDs and are no longer adjusted by the mod. Tower/private-tribe
behavior and a second-map cycle remain live smoke tests; the mod is not
installed by the normal update build set.
