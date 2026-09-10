# APIShared architecture

APIShared complements Script Extender 2.4.0 with three typed process-wide capabilities that the extender does not provide directly. Consumers cannot request arbitrary addresses, scans, writes or detours.

Initialization occurs once from `CrusaderLibrary.LibraryLoaded`. Each capability has an independent error boundary; `NativeApiState.Unavailable` is reserved for failure of global publication. Registrations, hooks, loggers and native state remain rooted for the process lifetime.

## Gatehouse capabilities

`gatehouse-distance-origin` owns `[0xB7B70, 0xB7BBB)` and switches between Vanilla's begin coordinate and the exact center of the complete building bounds while preserving the Chebyshev metric. `BugfixesAndQoL` is its consumer.

`gatehouse-timing` owns the four immediate values at RVAs `0xB7BC3`, `0xB7BCA`, `0xB7BD3` and `0xB7C35`. `ExtraFeatures` supplies seconds and tile values through the typed API and contains no local timing patch.

Both capabilities validate the complete native function and their own instructions, have separate owners and rollback state, and share one mutation lock because their intervals occupy the same memory page. Neither changes gameplay before a consumer explicitly applies a value.

## Unit HUD capability

`unit-hud-presentation` is the single owner of the managed hooks for selected troop categories, category interactions, control groups and approved HUD image slots. Owner GUID and registration ID provide deterministic ordering. Immutable snapshots isolate consumer callbacks, callback exceptions do not stop later consumers, and ambiguous category matches fall back to Vanilla.

Current consumers are `BugfixesAndQoL`, `Testmods/SkinTest` and `Testmods/VirtualUnitsPrototype`. Registrations are process-lifetime publications and request a deferred HUD refresh only after the Vanilla view model is ready.

## Explicit non-goals

Selected-unit commands use `TribeR3EventHooks.OnTribeIssueOrderWithTarget` directly. Path-component, connection, moat and packed-plan access uses `GamePathingManagerAPI` directly. `PathConnectionRecord`, route queries and `GamePlayerManagerAPI.PlayMessage` are not wrapped.

Only a demonstrated identical process-wide target shared by independently loadable mods justifies a new capability. Gameplay policy, settings, networking, save data, localization and one-off diagnostics remain mod-local.

## Compatibility basis

The active native catalog targets SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Its semantic evidence is tied to Script Extender 2.4.0 commit `5d5719c1002aec043d331162d72b2e7f3111b34b`. APIShared itself retains minimum Script Extender 2.3.0 because its current public and runtime contracts require no 2.4.0-only API.
