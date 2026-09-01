# Off-by-one building IDs in `OnTogglePause` and `UpdateLocalGoodsResourceVisuals`

## Affected versions

- Confirmed in v1.42.0 (`171d68e155a8f98c5f8c4ee154d9af154c9a2443`).
- Still present in the public v1.43.2 tag.

## Summary

Two building-pointer-to-ID conversions use the zero-based result of `SimpleNativeArray<T>.GetIndexByAddress(...)` as if it were a one-based game building ID:

1. `BuildingR3EventHooks.OnTogglePause` exposes a zero-based value through `BuildingTogglePauseEventArgs.BuildingId`.
2. `BuildingExtensions.UpdateLocalGoodsResourceVisuals` passes a zero-based value to two native functions that expect a one-based game building ID.

Both sites need `+ 1` after `GetIndexByAddress(...)`.

This is separate from the intentional zero-based indexing of `GetBuildingsAsSpan()` discussed in [work item 115](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/115). The bug is specifically that a raw span index crosses an API/native boundary labeled and implemented as a game `BuildingId` without conversion.

## Expected ID contract

`GetBuildingsArray()`, `GetBuildingsAsSpan()`, and `GetIndexByAddress(...)` use normal zero-based array indices. Public building IDs are one-based:

```csharp
building = &_buildingArray._array[buildingId - 1];
```

`GameBuildingManagerAPI.IsValidId(...)` consequently rejects `buildingId <= 0`. The v1.42.0 query API documentation also explicitly describes building IDs as `slot index + 1`.

This matches the native game. In the current native building-creation function, the free-slot search starts at `iVar8 = 1`, accesses buildings with `iVar8 * 0x32c`, and returns `iVar8`. Native game building IDs are therefore one-based; the managed array deliberately presents the active native slots as a zero-based array.

The interop layout confirms the mapping exactly: `GameBuildingManager.BuildingsArray` begins at manager offset `0x388`, which is `0x5c + 1 * sizeof(GameBuilding)` (`sizeof(GameBuilding) == 0x32c`). Consequently, managed array element 0 is native game building ID 1; there is no reserved ID-0 element inside the managed array that could make `GetIndexByAddress(...)` one-based here.

## Bug 1: `OnTogglePause.BuildingId`

Current code in `BulkBuildingDetours.cs`:

```csharp
GameBuilding* buildingPtr = (GameBuilding*)(ctx.Pointer->R10 - 6);
int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingPtr);

BuildingTogglePauseEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, buildingPtr->r_IsSleeping == 1);
BuildingR3EventHooks.OnTogglePause.Raise(eventArgs);
```

`GetIndexByAddress(...)` returns `(elementAddress - arrayAddress) / sizeof(GameBuilding)`, so this exposes the raw zero-based slot as `BuildingId`.

The native function containing this hook independently confirms the mismatch: its building loop initializes the current game ID to 1 while its pointer starts at the first managed-array element, then increments both together by one ID and one `GameBuilding` stride. Thus the first pointer produces array index 0 but game building ID 1.

The nearby repair-cost hooks already perform the correct conversion:

```csharp
int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingAddr + 300) + 1;
```

### Impact

- The first building produces `BuildingId == 0`, which is invalid for `TryGetBuildingById` and `IsValidId`.
- Every later building reports the ID of the preceding array slot.
- Passing the event's `BuildingId` to normal building-manager APIs therefore targets the wrong building or fails.

## Bug 2: `UpdateLocalGoodsResourceVisuals`

Current code in `BuildingExtensions.cs`:

```csharp
GameBuilding* ptr = (GameBuilding*)Unsafe.AsPointer(ref Unsafe.AsRef(in self));

int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(ptr);
GameBuildingManagerAPI.Instance.UpdateVisualResourceGoods(buildingId);
GameTileManagerAPI.Instance.TryUpdateTileResourceVisualsForBuilding(buildingId);
```

Again, `buildingId` is actually a zero-based array index. Both called API methods forward it unchanged to native functions.

The native implementations confirm that their second argument is a one-based game building ID:

- `c_game_update_visual_goodsyard_goods` addresses building data as `buildingManager + buildingId * 0x32c`.
- `c_game_update_visual_resourcetile` addresses the native building globals using the same `buildingId * 0x32c` stride.
- Native building creation reserves/starts at ID 1 and uses that same stride, so passing 0 does not select the first active building.

### Impact

- Calling the extension on game building ID 1 passes 0 to both native visual-update functions.
- Calling it on game building ID N passes N - 1, updating data/visuals for the wrong native slot.

## Suggested fix

```diff
diff --git a/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs b/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs
@@
-int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingPtr);
+int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingPtr) + 1;

diff --git a/src/SHCDESE.BepInEx/Extensions/BuildingExtensions.cs b/src/SHCDESE.BepInEx/Extensions/BuildingExtensions.cs
@@
-int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(ptr);
+int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(ptr) + 1;
```

## Suggested regression checks

1. Toggle pause on the first active building and assert `eventArgs.BuildingId == 1` and that `TryGetBuildingById(eventArgs.BuildingId, ...)` resolves the same pointer.
2. Repeat with a later building and assert pointer identity, not only a plausible numeric range.
3. Invoke `UpdateLocalGoodsResourceVisuals` on a known building and verify that both forwarded native calls receive `GetIndexByAddress(pointer) + 1`.

## Native verification context

- Canonical `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Native building creation function: VA `0x1800B47E0`, RVA `0xB47E0`
- Toggle-pause hook site: VA `0x1800C7F13`, RVA `0xC7F13`, inside function VA `0x1800C7E90`
- `c_game_update_visual_goodsyard_goods`: VA `0x1800C05B0`, RVA `0xC05B0`
- `c_game_update_visual_resourcetile`: VA `0x18006E620`, RVA `0x6E620`

