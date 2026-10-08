# APIShared refactor verification

Baseline: `987cc5abf2ddb7b8f6b70cca2fa163bed913fd70`. The production tree was clean at the start; the only untracked entry in the snapshot is this task's audit directory. No commit or staging was performed.

## Implemented boundaries

- Existing capability contracts retain namespace `APIShared`; the assembly and plugin GUID are unchanged.
- Public settings, general game modes and fixed Serps profiles are separated into `APIShared.ModSettings`, `APIShared.GameModes` and `APIShared.SerpsMods`. Consumer source, XAML, reflection expectations, source links and packaging checks are migrated together. No old public namespace facade remains.
- `ApiShared.ForMod` provides an owner-bound client. Managed availability, terminal global readiness and exclusive-owner conflicts remain distinct. Early and late readiness callbacks share error isolation, including failing log listeners.
- Preset/UI/persistence/registration/lobby code is extracted into focused files without changing stored keys or executable preset member bodies. Native command/formation and MoatMove internals remain private to their existing friend integrations.
- English architecture, catalog and third-party guide cover integration without SerpsModsHost. The example assembly uses public contracts and `Private=false`; its output contains only its own DLL. Example XAML bindings are checked against compiled inherited members.

## Git review and machine checks

- AST comparison with Git: 86 unchanged API feature files and 86 migrated consumer files; 224 preset methods preserved. Fields, properties and events are also compared. Zero unexpected executable-member differences. Deliberate behavior changes are limited to the owner-bound facade, callback error boundary and caller-defined multiplayer permission.
- The expanded 60-project source inventory compiles with zero errors against the real installed Assembly-CSharp.dll and installed Script Extender. No new game-member access, native target, patch range, game algorithm or runtime teardown is introduced.
- JSON/lifecycle/plugin scheduling, permanent-hook mutation checks, installed interop contracts, CRLF and XAML Content-root checks passed. Modsettings audit passed for all 14 mods. Mission-lifecycle preflight passed with current inventories and explicitly retained native transient-state unload publishers.
- APIShared's complete build regression suite passed, including missions, lobby, player defeat, repair/presets, HUD, ownership, independent failures and real native gate/bridge stub execution. New tests cover foreign GUIDs, early managed acquisition, early/late callbacks and log-listener failure.
- Host/client preset and serialization regression passed; modoptions/ledger/save/runtime checks: 8,046. ExtraFeatures session callbacks: 4,554 checks, with 10,000 settled Lord ticks without repeated player/mode reads.
- BugfixesAndQoL's complete build tests passed, including formations, command initialization, Assassin paths and actual installed RedBird contracts.
- The old MoatMove harness is fully adapted to current production sources. Deleted spacing/preview source requirements are removed. Optional formation integration uses a test boundary double; productive formation behavior remains covered by Formations.Tests. The traversal harness retains 201 actual runtime members, 250,548 runtime assertions, 84,031 independent search assertions, 289,388 Fast assertions and 1,469,340 cursor comparisons, plus native emitter/selection-adapter checks.

## Build and installation

APIShared and all 30 affected consumer build drivers completed through their respective elevated `build.bat /nopause`. A game-start interruption was resolved after the user closed the game; the final BugfixesAndQoL and MoatMove builds succeeded. All 37 package DLLs from those 31 drivers match exactly one installed DLL by SHA-256; see `installed-hashes.txt`.

Compilation still reports source-link/friend-assembly name-conflict and other pre-existing warnings. Build success is not treated as an in-game behavior proof. Detailed logs remain in this directory.

## Outstanding acceptance

APIShared remains 0.4.12 during testing. Consumer minimum versions remain on their test values. The APIShared version and migrated consumer minima must be raised together after final acceptance, before publishing the breaking namespace migration.

In-game checks remain: startup cleanup and subsequent callbacks; all mod settings tabs and preset save/load; host/client joining, rejoining, local preferences and per-player convergence; mission/save/editor/Trail transitions; HUD selections/recruitment; gate/bridge behavior; manual commands, formations and MoatMove enabled/disabled. Native semantics and persistence are statically/regression-tested, but this task has not executed these interactive game sessions.
