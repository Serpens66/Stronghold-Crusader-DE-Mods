# Unified mission lifecycle audit

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Installed Script Extender 2.6.0; canonical fork commit 2cee24e33b5a5d81d1c275efabc714ac59917b7b,
tree 9cfb59b7b531553b23709b90b3cc3f10b0615cc1. No fork changes or additional native detours.

## Audited boundaries

- `DLL_LoadMapToPlay` RVA 0x84AC0 (confirmed export) dispatches campaign initialization (0x87C40), ordinary mission initialization (0x8AE80), skirmish initialization (0x94350), trail routing (0x28960), and tutorial initialization (0x100530). The three initializer addresses match the installed Extender's OnStartMap targets; internal implementation names remain candidate-level where the database has not confirmed semantics.
- Successful native initializers return low 32 bits 0xFFFFFFFF. Campaign errors -11/-21, ordinary reader failure 1, and skirmish reader failure 0 are not success. The enclosing loader can normalize negative results to errorCode=1; a failed inner initializer therefore vetoes an otherwise successful outer result. Tutorial uses its separate route and is not required to emit OnStartMap.
- Save loader 0x853F0 succeeds with native return 1 and output errorCode=1. It restores native state without normal new-game startup. Multiplayer restoration also enters the skirmish initializer with the save byte set; consumers must not infer a new game from the existence of an OnStartMap callback.
- Reset 0x87D10 and unload 0x5BC70 are repeatedly nested in preinitializers/loaders. The unload target is independently confirmed by the installed Extender pattern; internal function names remain candidate confidence. Reader 0x87D0 returns zero for invalid input. Repeated unloads do not constitute repeated completed sessions.
- `DLL_PreInitMap_Multiplayer` RVA 0x86270 (confirmed export/PInvoke) receives byte skirmish flag, signed Int32 restart byte length and byte pointer, signed Int32 coop trail/mission, and byte test/custom/extreme flags. It resets via 0x87D10/0x8ADC0 and stores coop trail at RVA 0x366A090 and mission at 0x366A094 unchanged. Frontend selection 21..24 maps to native trail 1..4; the UI's one-based mission is decremented in `CoopMissionChanged`. StartGame and the local StartSkirmishGame pass the resulting zero-based mission unchanged. Capture the managed preinitializer arguments; the shared skirmish entry alone cannot distinguish standard local Coop from custom games.
- `LoadMapReturnData.siege_or_invasion` is serialized 0=Siege, 1=Invasion, 2=Economy, 3=FreeBuild. `GameData.Init` explicitly converts this to `Enums.GameModes`; these enum numeric values are not interchangeable. `coopMissionID`, `skirmishTrailLevel` and campaign `mission_level` retain native zero-based indices. Game player IDs are one-based.

## Managed completion and retirement

Interactive roots: EditorDirector creation/editor load/campaign/trail/custom trail/save, FRONT_StandaloneMission.StartMap, FRONT_Multiplayer.StartSkirmishGame/RestartSkirmishGame, HUD_Tutorial.StartTutorial, Platform_Multiplayer.StartGame/StartSave. Nested custom-trail/skirmish calls belong to one attempt. Editor creation observes newMapEditor output, editor loading requires both native success and the outer bool success. Lower LoadMapFile exports without an interactive root create no mission.

`postLoading` returns after GameData/GameMap/UI initialization but the enclosing root still performs player assignment, colour mapping and other setup. OnStart waits for the outer normal return. Clients waiting for the random seed complete in the main-thread `processMessage` packet-2 path after postLoading and its subsequent setup. Repeated messages have no waiting operation to complete. An operation ID retired by replacement cannot become current again. Simulation start is later (packet 4) and is not guaranteed by OnStart.

`MainViewModel.GoToScreen` and direct `FatControler.NewScene` are observed after their actual screen state changes. Options bypass scene replacement. HUD win/lose presentation is not departure. The Extender's HUD exit hook publishes a synthetic Unload Pre with a null pointer; this is logical retirement even without a native Unload Post. An editor flag can survive departure and is not session evidence.

## Public and Shared contract

`IMissionLifecycleCapability` supersedes the editor-specific API completely. Context and notifications are immutable; process-local attempt IDs are monotonic. Initialization phases never replay. Only a current, fully managed-ready session replays. End includes last captured context, last reached phase, reason and whether Start occurred. Observer and diagnostic exceptions are isolated; Vanilla delegates execute once. Unpublished failed hook candidates are rolled back; published services remain process-rooted.

Mode classification and the existing regular-mod/feature permission tables are defined once in APIShared. Shared's per-assembly adapter updates the gate before feature subscribers. Lobby network authority remains available outside a mission without permitting gameplay. Startup resources exclude save/editor/replay. Save persistence, player switching and actual building/HUD/render readiness remain separate contracts.

Legacy Customize-origin fields retain their provider schema: Custom Trail IDs 81..92 and one-based mission IDs; Coop provider trail IDs 0..3 and mission IDs 1..10; built-in Vanilla/Sands mission IDs are zero-based. These fields describe provenance, whereas `MissionContext.MissionIndex` consistently uses a known zero-based position. Absent evidence does not invent an index.

## Evidence and limits

The user's prior ElevatedMoat editor test and the last inspected log establish one Created session, one End and one Loaded session in the preceding editor implementation (19:26:24.494, 19:26:40.977, 19:26:44.671). They do not validate this later unified implementation. Two preceding Ally-Goods/ExtendedData errors are outside this lifecycle change.

Automated validation and final build results are recorded in `_inspect/MissionLifecycleValidation/RESULTS.md`. Game validation of the new implementation, particularly real multiplayer host/client and mission-specific features, remains a separate required observation. No live coverage is inferred from state-machine or boundary-double tests.
