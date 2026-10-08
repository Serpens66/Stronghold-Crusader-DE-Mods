# APIShared architecture

APIShared owns typed, process-wide services and the common ModSettings integration. Its assembly identity and BepInEx GUID remain `APIShared` and `APIShared_Serp`.

## Source map

| Directory | Responsibility |
|---|---|
| `src/Core` | Public entry point, owner-bound client, initialization and internal native infrastructure |
| `src/Missions`, `src/Lobby`, `src/Players` | Shared observers and immutable lifecycle/state contracts |
| `src/Presentation`, `src/Buildings` | HUD/briefing presentation, repair and gatehouse services |
| `src/Units`, `src/Pathfinding`, `src/Diagnostics`, `src/Savegames` | Validated helpers, advanced integration and shared observations |
| `src/GameModes` | General mode snapshots and optional caller-defined permissions |
| `src/SerpsMods` | Existing Serps GUID profiles and feature exceptions |
| `src/ModSettings` | Public settings integration and its implementation |
| `src/UnitCommands` | Internal BugfixesAndQoL/MoatMove command and formation implementation |

Capability contracts retain the `APIShared` namespace. Directories organize implementation without forcing namespace churn in these contracts. Public former `Shared` types are placed in the three explicit namespaces above. Internal source-linked `Shared` utilities still provide dependency-free JSON, dispatch, logging and per-consumer adapters; they are not a third-party dependency.

## Publication and lifetime

`APISharedPlugin.Awake` establishes main-thread dispatch and managed services. Native initialization is published by `CrusaderLibrary.Instance.LibraryLoaded`. `ApiSharedRuntime.ProcessInstance`, static registries and long-lived publishers retain runtime objects after startup cleanup. There is no plugin Update/coroutine/teardown host.

Global readiness is separate from individual service diagnostics. Capabilities isolate failures; unsupported native services must not disable independent managed observation. Owner-bound clients delegate to the same service acquisition and ownership checks as direct consumers. Creating a client installs nothing.

Readiness callbacks share one exception boundary for early and late delivery, always outside the initialization lock. No dispatch or callback thread transformation is added. Native hooks, executable ranges and installation order are unchanged by this refactor.

## Settings implementation

`PresetLobbyModSettingsViewModel` is a partial class: the main file handles integration, permissions and notifications; `.Sources.cs` handles source selection and UI/search commands; `.Persistence.cs` contains the existing preset controller and stable storage schema. `PerPlayerLobbySettings.cs` owns lobby convergence and its builder contracts. `LobbyModSettingsPresetRegistration.cs` owns preparation, registration and horizontal focus-scroll handling.

The extraction preserves executable member bodies and persistence keys. JSON still uses the source-linked `Shared.DependencyFreeJson`. Personal, host, per-player and trail settings retain their existing sync and save boundaries.

## Policy and compatibility

Mode capture describes the current context. `GameplayModModePolicy` evaluates an explicit caller profile; it is optional. `SerpsModProfiles` and `GameplayFeatureModePolicy` preserve our existing permissions and exceptions. Unknown/conflicting contexts remain denied by the optional evaluator.

The installed Script Extender is the compile/runtime source of truth. APIShared currently declares 2.14.0. Native support is still controlled by existing hash-bound validators and the current native baseline, not by historical documentation version numbers. The canonical Fixes source remains the compatibility reference; this refactor installs no additional hooks and changes no native targets.

The pre-refactor migration record is retained in `MIGRATION_PLAN.md` as historical context. Current public contracts are documented in `docs/API_CATALOG.md`.
