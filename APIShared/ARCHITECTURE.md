# APIShared architecture

APIShared complements Script Extender with typed process-wide capabilities that the extender does not provide directly. Consumers cannot request arbitrary addresses, scans, writes or detours.

The managed `lobby-state`, `mission-lifecycle`, and `player-defeat` capabilities initialize once from `APISharedPlugin.Awake()` and are therefore available independently of native library initialization. Native capabilities initialize once from `CrusaderLibrary.LibraryLoaded`. Each capability has an independent error boundary; `NativeApiState.Unavailable` is reserved for failure of global native publication. Registrations, hooks, loggers and runtime state remain rooted for the process lifetime.

## Player-defeat capability

The managed `player-defeat` capability observes simulation ticks and publishes two independent, one-shot transitions per player and mission: disappearance or death of a previously confirmed living lord, and entry into Vanilla's official `WinLossState.Loss`. Initial save state is baseline-only. Owner-local registrations are delivered in deterministic owner/registration order; reentrant publications are queued and callback failures are isolated.

## Lobby-state capability

`lobby-state` owns the single managed observation path for Vanilla's active multiplayer lobby. It captures once during startup, immediately after the central `Platform_Multiplayer.GetActiveLobbyMembers(bool)` writer and `LeaveLobby(bool)`, at map transitions, and otherwise every 15 render frames. Observation is suppressed while a map is running. The map-start Pre event synchronously captures the last lobby state before mod-local slot finalization.

Snapshots defensively copy the one-based player-slot-to-Steam-ID mapping and include lobby ID, local slot, resolution/error state and map-transition preservation. Equal values are not republished. Observer order is ordinal owner GUID followed by registration ID; reentrant publications are queued and callback failures are isolated.

`BugfixesAndQoL`, `CastlePlanner` and `ExtendedData` are the only current consumers. Their source-linked coordinators retain settings publication, readiness, host/client policy and final in-game slot remapping. They have hard APIShared dependencies and no local polling fallback.

## Gatehouse capabilities

`gatehouse-distance-origin` owns `[0xB7B70, 0xB7BBB)` and switches between Vanilla's begin coordinate and the exact center of the complete building bounds while preserving the Chebyshev metric. `BugfixesAndQoL` is its consumer.

`gatehouse-timing` owns the four immediate values at RVAs `0xB7BC3`, `0xB7BCA`, `0xB7BD3` and `0xB7C35`. `ExtraFeatures` supplies seconds and tile values through the typed API and contains no local timing patch.

Both capabilities validate the complete native function and their own instructions, have separate owners and rollback state, and share one mutation lock because their intervals occupy the same memory page. Neither changes gameplay before a consumer explicitly applies a value.

## Unit HUD capability

`unit-hud-presentation` is the single owner of the managed hooks for selected troop categories, category interactions, control groups and approved HUD image slots. It also owns the native control-group storage resolver and exposes a one-based, validated removal operation. Owner GUID and registration ID provide deterministic ordering. Immutable snapshots isolate consumer callbacks, callback exceptions do not stop later consumers, and ambiguous category matches fall back to Vanilla.

Current consumers are `BugfixesAndQoL`, `Testmods/SkinTest` and `Testmods/VirtualUnitsPrototype`. Registrations are process-lifetime publications and request a deferred HUD refresh only after the Vanilla view model is ready.

## AIV build-step capability

`aiv-build-step` owns the single permanent RedBird detour at RVA `0x51790`. It validates the full native SHA-256, executable range, unique prolog and full function hash before publication. Observers begin in ordinal owner-GUID and registration-ID order; successful per-call invocations complete in reverse order after exactly one unchanged Vanilla call. Exceptions are isolated, while a Vanilla exception is reported to completions and then propagates normally.

`ExtraFeatures` uses the broker for AI defense rebuild timing. `Helpers/ActiveAIVDetector` uses it only when its optional prebuild trace is enabled. Their gameplay and diagnostic policies remain local. `CastlePlanner` merely binds and calls its AIV functions and therefore is not a consumer.

## Explicit non-goals

Selected-unit commands use `TribeR3EventHooks.OnTribeIssueOrderWithTarget` directly. Path-component, connection, moat and packed-plan access uses `GamePathingManagerAPI` directly. `PathConnectionRecord`, route queries and `GamePlayerManagerAPI.PlayMessage` are not wrapped.

Only a demonstrated identical process-wide target shared by independently loadable mods justifies a new capability. Gameplay policy, settings, networking, save data, localization and one-off diagnostics remain mod-local.

## Compatibility basis

The active native catalog targets SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The managed lobby audit targets installed `Assembly-CSharp.dll` SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` and Script Extender 2.6.0 commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`. APIShared itself retains minimum Script Extender 2.3.0 because its current public and runtime contracts require no newer API.
