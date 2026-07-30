# SpawnCastle

`SpawnCastle` adds a Script Extender Mod Settings dropdown for selecting an
`.aivjson` castle blueprint. On a newly started singleplayer map, the blueprint is
spawned for the local human player relative to the player's existing keep.

The dropdown scans:

- `BepInEx/plugins/SpawnCastle_Serp/AIV`
- the game's `CustomLords` and `ExtendedLords` directories
- the official Castle & CPU Lord Editor's `StreamingAssets/Villages` directory

Choose `disabled` to leave new maps unchanged. Loading a savegame never spawns a
second copy. The host selection uses the Script Extender's normal mod-settings
storage and is restored before the settings UI is bound:

`BepInEx/plugins/SpawnCastle_Serp/LobbyModSettings/SpawnCastle_Serp.msgpack`

If the selected file no longer exists, the setting safely falls back to `disabled`.

The existing keep frame and AIV misc/unit slots are not spawned. The castle is
anchored to the real keep building footprint, and buildings, stairs, traps, moat
frames, and pitch ditches are processed in their original AIV frame order.

Wall and crenellation spawning is temporarily deferred while building placement is
being verified. SpawnCastle does not modify wall-cost multipliers or grant stone.

## Verified building placement

The player resource returned by `GetPlayerKeepPosition(...)` is useful diagnostic
data, but it is not used as the world anchor. SpawnCastle scans
`GameBuildingManagerAPI.Instance.GetBuildingsAsSpan()` for an initializing or alive
keep owned by the local player. It prefers the reported keep ID when that entry is
valid and otherwise uses the matching real keep building.

The anchor passed to the AIV conversion is the keep building's
`r_TilePositionXBegin/r_TilePositionYBegin`. This is the same top-left placement
coordinate expected by `GameBuildingManagerAPI.CreatePrefab(...)`. For an
unrotated AIV, the confirmed conversion is:

    delta = AivGridTransform.GetAnchorDelta(point, aivKeep, Degrees0)
    tileX = realKeepBeginX + delta.Column
    tileY = realKeepBeginY - delta.Row

AIV editor rows therefore map to inverted world-tile Y. Before spawning, the mod
checks every building rectangle for map bounds and overlaps, including the real
keep's actual `r_OccupyTileGridSize`. Normal buildings are then restored with
`CreatePrefab(..., bIsFree: true, bypassPlacementRules: true)` so the result gets
the game's complete prefab, tile, visual, and interaction initialization.
