# Fixes 1.19.0: saved games omit the active AIV tile buffers

## Problem

Fixes 1.19.0 adds save/load support for the enlarged AIV ordered-tile buffer, addressing the omission in 1.18.0. However, its save and load loops always copy the **first** 40,000 entries. That first section belongs to reserved village slot 0. Active AI villages use slots 1–8, so their sections are not saved or restored.

This affects even one AI village. With two villages, the save handler creates two entries but fills both from the same first section.

## Evidence

- `src/shcde-fixes/Detours/AIDetours.cs`, lines 1008–1042: the loops iterate player `i` and entry `j`, but both access `_newOrderedMapTileIdsAddress[j]`. They never add a village-slot offset. This code was added in commit `dd1ab11468b90c3a6eeba78b2aa671418c90b1a5`.
- The same file allocates `9 × 40,000` entries and uses `villageSlot × 40,000 + entry` in its relocation hooks. `GameAIVManagerAPI.TryGetVillageByPlayerId(i)` finds a village by owner and can return a physical slot different from `i`; the save/load loops ignore the returned village pointer.
- In the installed game's native DLL (SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`), the AIV reader at RVA `0x51790` uses the mapped village slot and the writer at RVA `0x53D00` uses a village-slot argument. Both keep sections separate. RVA `0x56600` resets the nine native village records.

## How to reproduce

With `RelocateAIVOrderedMapTileIds` enabled, let one AI village create ordered-tile entries, save, and reload. Compare that village's 40,000-entry section before saving and after loading. The save data contains slot 0 instead of the active village's section. A two-village test can put different sentinel values in both active sections; the serialized entries will still be identical.

## Suggested fix

Resolve each player's physical village slot and copy from `bufferBase + slotIndex * 40000`. Restore to the corresponding slot, and check the saved array length before indexing it. A round-trip test should verify that distinct values in two active sections survive a save/load.

The indexing defect is confirmed by the source and native buffer layout. Its visible effect on an actual saved game still needs a runtime test.
