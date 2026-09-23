# Script Extender 2.9.0: `GetSelectedChimps(playerId)` mixes two players' selections

## Problem

`GetSelectedChimps(playerId)` gets the number of selected units from the requested player's native count, but takes the unit IDs from `EngineInterface.selectedChimps`. That managed array contains the **local player's** selection. The method combines two players' data when `playerId` is not the local player.

For example, if the local player selected units A and B, while player 2 selected unit C, `GetSelectedChimps(2)` can return A instead of C. If player 2's count is larger, it can also read unused or stale entries from the local array.

## Evidence

- `src/SHCDESE.BepInEx/API/GamePlayerManagerAPI.cs`, lines 898–920: `GetSelectedChimpsCount(playerId)` indexes `r_SelectedChimpsCount[playerId]`; `GetSelectedChimps(playerId)` always reads `EngineInterface.selectedChimps`.
- Installed `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`: `DLL_RunTick` at RVA `0x86680` passes one output array to RVA `0x19D960`. That function filters selected units by the current local player (`DAT_1888e3d70`), writes unit ID/type pairs to the array, and writes their count to the local play-state result.
- `GetSelectedChimpsCount` does not validate `playerId` before indexing the fixed nine-element array.
- A two-client multiplayer test reproduced the mixed result at game tick 1856. On client 1 (`localPlayerId=1`), `GetSelectedChimps(1)` returned `[46,47,48]`, while `GetSelectedChimps(2)` returned `[46,47]`. On client 2 (`localPlayerId=2`) at the same tick, `GetSelectedChimps(2)` returned `[49,50]`, while `GetSelectedChimps(1)` returned `[49,50,51]`. Each requested player's count was correct (3 or 2), but the IDs came from the calling client's own local output array.

## How to reproduce

In a two-client multiplayer session, select three units on player 1's client and two different units on player 2's client. At the same game tick, call `GetSelectedChimps(1)` and `GetSelectedChimps(2)` on both clients. The returned length follows the requested player, but the IDs follow the local client. The missing bounds check for invalid player IDs is a separate source-level finding; invalid IDs were not called in the live test because they can read outside the array.

## Suggested fix

Make the list API explicitly local and use the count from the same play-state snapshot, or obtain both count and IDs from a genuinely player-specific native source. Validate player IDs and cap any unsafe copy to the output-buffer capacity.

The mixed-source result was reproduced in game on both clients. The invalid-ID issue remains a source-level finding, not a live-tested result.
