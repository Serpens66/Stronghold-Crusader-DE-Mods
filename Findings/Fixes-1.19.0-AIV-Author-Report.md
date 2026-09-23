# Fixes 1.19.0: saved games omit the active AIV tile buffers

## Problem

Fixes 1.19.0 adds save/load support for the enlarged AIV ordered-tile buffer, addressing the omission in 1.18.0. However, its save and load loops always copy the **first** 40,000 entries. That first section belongs to reserved village slot 0. Active AI villages use slots 1–8, so their sections are not saved or restored.

This affects even one AI village. With two villages, the save handler creates two entries but fills both from the same first section.

## Evidence

- `src/shcde-fixes/Detours/AIDetours.cs`, lines 1008–1042: the loops iterate player `i` and entry `j`, but both access `_newOrderedMapTileIdsAddress[j]`. They never add a village-slot offset. This code was added in commit `dd1ab11468b90c3a6eeba78b2aa671418c90b1a5`.
- The same file allocates `9 × 40,000` entries and uses `villageSlot × 40,000 + entry` in its relocation hooks. `GameAIVManagerAPI.TryGetVillageByPlayerId(i)` finds a village by owner and can return a physical slot different from `i`; the save/load loops ignore the returned village pointer.
- In the installed game's native DLL (SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`), the AIV reader at RVA `0x51790` uses the mapped village slot and the writer at RVA `0x53D00` uses a village-slot argument. Both keep sections separate. RVA `0x56600` resets the nine native village records.
- Runtime test with Fixes 1.19.0 and Script Extender 2.9.0: a diagnostic mod recorded independent SHA-256 hashes of the seven active sections while saving `test_aivbug.sav`. All seven hashes differed from each other and from slot 0. For example, player 2's physical slot 1 hashed `9992A083...EB795CF4`, and player 3's slot 2 hashed `F0B271FB...BD42943`.
- The saved game's actual Fixes MessagePack entry contains seven player records, but every record's 40,000 entries have the same hash, `B9CE164D...77FABE8`. This was checked directly in the save archive, separately from the diagnostic mod's record.
- After closing and restarting the game, loading that save put `B9CE164D...77FABE8` in all seven active sections. The first game tick confirmed the same result. A same-process reload had appeared to match because the old buffer contents remained in memory; the cold restart exposed the loss.

## How to reproduce

With `RelocateAIVOrderedMapTileIds` enabled, let at least two AI villages run until their ordered-tile sections differ. Record each section's hash, save the game, close the game entirely, restart it, and load that save. Compare the recorded hashes with the player entries in `_SE_ModData_extendedaivbuffer-savedata.msgpack` and with the sections after loading. In the reproduced case, the saved entries and loaded sections were all identical, despite distinct sections at save time.

## Suggested fix

Resolve each player's physical village slot and copy from `bufferBase + slotIndex * 40000`. Restore to the corresponding slot, and check the saved array length before indexing it. A round-trip test should verify that distinct values in two active sections survive a save/load.

This was reproduced in a real saved game, including a cold restart and load. Fixes 1.19.0 does write and read an AIV archive entry, addressing the missing-entry problem reported for 1.18.0; the entries currently contain the wrong buffer section.
