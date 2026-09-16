# Editor map lifecycle audit

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Script Extender: 2.6.0, commit 2cee24e33b5a5d81d1c275efabc714ac59917b7b,
tree 9cfb59b7b531553b23709b90b3cc3f10b0615cc1. Installed assembly independently inspected.

## Native and managed contract

- `DLL_PreInitMap_Editor`, RVA 0x85E90, confirmed export/PInvoke: `(int mapSize, int mapType, byte siegeThat, byte multiplayerMap, byte* retData)`. Initializes a blank native editor map and sets the output errorCode to 1. `EngineInterface.newMapEditor` passes siegeThat=false, gets colour mapping and increments the returned mapRotation. `EditorDirector.createNewMap` then initializes GameData, GameMap, scenario UI, simulation and one-based player ID 1 before returning. Its managed success branch is `!siege_that || initData.errorCode == 1`; the current native export always initializes errorCode=1.
- `EngineInterface.LoadMapFile` first calls that SAME preinitializer with `(160,0,false,false)`, then `DLL_LoadSaveGame` at RVA 0x853F0 (confirmed export/PInvoke). Its arguments are UTF-16 bytes, signed int BYTE length, output struct pointer, and editor byte flag. Native failure returns 0; success returns 1 and sets output errorCode=1. `EditorDirector.loadMapIntoEditor` only performs subsequent managed editor initialization when that errorCode equals 1, returning true afterwards. `SaveCustomTrailMap` can call the lower EngineInterface loader without starting an interactive editor session.
- Reset functions RVA 0x87D10 and 0x5BC70 and archive reader RVA 0x87D0 have baseline confidence candidate. Their pseudocode was followed for reset/load/abort flow. 0x5BC70 is independently confirmed as the installed Extender unload pattern target. Both exported editor paths call it repeatedly (also through 0x87D10); these are internal teardown notifications, not completed editor sessions.
- The installed Extender's OnStartMap patterns uniquely resolve to RVA 0x94350 (skirmish), 0x8AE80 (non-skirmish), 0x87C40 (campaign). None is reachable in the baseline direct-call graph from the two editor exports. OnLoadMap wraps DLL_LoadMapToPlay. OnLoadSave wraps DLL_LoadSaveGame; Post occurs BEFORE the outer managed editor initialization.
- MapEditorR3EventHooks contains editing/placement events only, not a session-ready event. Current managed Extender hooks have no equivalent editor lifecycle completion signal.
- MainViewModel.InitNewScene(MapEditor) sets IsMapEditorMode and calls GoToScreen(ActualMainGame). Options return before changing screens. Other scene transitions can leave IsMapEditorMode stale: actual screen and flag must be considered together. Native editor preinit alone, a mode-flag edge, and simulation start alone do not establish a complete interactive editor session.
- The first GameData.lastGameState arrives later through setGameState; it is explicitly cleared inside editor initialization. Complete managed initialization does not imply first PlayState/render/building readiness.

## Workspace integration

The editor-only public contract below is historical. It has been superseded by the [unified mission lifecycle](MISSION_LIFECYCLE.md); the native/editor findings above remain applicable.

APIShared owns managed hooks around createNewMap/loadMapIntoEditor and captures newMapEditor's output for creation success. It observes actual GoToScreen transitions and external unloads, suppressing nested unloads within its operation scope. It publishes one Ready per successful operation and one Ended per retired ready session. A failed replacement leaves no active session; late observers replay only a currently ready session.

Shared.GameplaySessionLifecycle translates those notifications into EditorCreated/EditorLoaded, with no fabricated MapStartEventArgs or save restoration. It updates the per-assembly activation gate before feature callbacks. Feature-specific readiness and editor-player changes remain local. Runtime evidence from an actual game session is still required; this document records static analysis, not a live test.
