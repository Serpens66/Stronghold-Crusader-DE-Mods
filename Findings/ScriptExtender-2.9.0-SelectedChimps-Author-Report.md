# Script Extender 2.9.0: `GetSelectedChimps(playerId)` mixes two players' selections

## Problem

`GetSelectedChimps(playerId)` gets the number of selected units from the requested player's native count, but takes the unit IDs from `EngineInterface.selectedChimps`. That managed array contains the **local player's** selection. The method combines two players' data when `playerId` is not the local player.

For example, if the local player selected units A and B, while player 2 selected unit C, `GetSelectedChimps(2)` can return A instead of C. If player 2's count is larger, it can also read unused or stale entries from the local array.

## Evidence

- `src/SHCDESE.BepInEx/API/GamePlayerManagerAPI.cs`, lines 898–920: `GetSelectedChimpsCount(playerId)` indexes `r_SelectedChimpsCount[playerId]`; `GetSelectedChimps(playerId)` always reads `EngineInterface.selectedChimps`.
- Installed `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`: `DLL_RunTick` at RVA `0x86680` passes one output array to RVA `0x19D960`. That function filters selected units by the current local player (`DAT_1888e3d70`), writes unit ID/type pairs to the array, and writes their count to the local play-state result.
- `GetSelectedChimpsCount` does not validate `playerId` before indexing the fixed nine-element array.

## How to reproduce

In a multiplayer session, select different units for the local and another player. Call `GetSelectedChimps(otherPlayerId)` and compare the returned IDs with both selections. Also call `GetSelectedChimpsCount(-1)` and `GetSelectedChimpsCount(9)` in a controlled test; both IDs index outside the declared array.

## Suggested fix

Make the list API explicitly local and use the count from the same play-state snapshot, or obtain both count and IDs from a genuinely player-specific native source. Validate player IDs and cap any unsafe copy to the output-buffer capacity.

The mismatched sources and missing bounds check are confirmed by source and native analysis. An in-game reproduction has not yet been run.
