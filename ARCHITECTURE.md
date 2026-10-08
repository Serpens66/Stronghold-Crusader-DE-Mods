# Architecture

APIShared is one process-wide BepInEx service library. Assembly identity `APIShared`
and plugin GUID `APIShared_Serp` are stable. Its public entry point binds a caller's
GUID without installing hooks or reserving a capability.

## Responsibilities

| Area | Purpose |
|---|---|
| Core | Initialization, capability diagnostics, ownership and native infrastructure |
| Missions, Lobby, Players | Shared observations and immutable state notifications |
| Presentation, Buildings | HUD/briefing extensions, building repair and gatehouse services |
| GameModes | Context capture and optional caller-defined permission profiles |
| ModSettings, Savegames | Settings UI integration, presets, convergence and persistence |
| Units, Pathfinding, Diagnostics | Queries and advanced integrations |
| UnitCommands | Required internal command, formation and MoatMove runtime |
| SerpsMods | Explicit compatibility profiles for existing Serps consumers |

Directories help navigation; public namespaces define the contracts. Capabilities
use `APIShared`; settings and general profiles use `APIShared.ModSettings` and
`APIShared.GameModes`. Serps profiles are opt-in and never constrain an unrelated mod.
Internal command/formation integration is retained for existing friend-assembly
consumers; it is not an additional public entry point.

## Publication

`APISharedPlugin.Awake` publishes managed services. `CrusaderLibrary.LibraryLoaded`
initializes native services. `ApiSharedRuntime.ProcessInstance`, static registries
and persistent publishers retain services after startup cleanup. Global completion
and individual capability availability are separate; one native failure must not
disable independent managed services.

Readiness callbacks run outside the initialization lock with exception isolation.
There is no implicit thread dispatch. Registration ownership, ordering, replay and
callback contracts are described in the [API catalog](docs/API_CATALOG.md).

## Settings and dependencies

The preset view model integrates source selection, UI commands and persistence.
Per-player state owns lobby convergence. Settings, presets and saved-game data keep
their existing formats. Internal JSON parsing and atomic publication are owned by
APIShared; no source links or automatic synchronization connect them to a mod workspace.

The compiled runtime uses the real installed game and Extender assemblies.
[Native compatibility](docs/NATIVE_COMPATIBILITY.md) describes native ownership and
update checks. Native catalogs and tests express supported contracts, not a universal
promise that an arbitrary game build is supported.

## Development

The solution contains the runtime, two local integration suites, a game-independent
core suite and public consumer examples. Test-only packages never become plugin
dependencies. The standalone build uses no neighboring repositories. Additional
mod integration tests belong in their consumer repositories.
