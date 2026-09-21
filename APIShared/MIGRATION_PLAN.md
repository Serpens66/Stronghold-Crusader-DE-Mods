# APIShared migration plan and current state

Status: 21 September 2026. Development and compatibility checks target installed Script Extender 2.6.0, commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`. APIShared retains Script Extender 2.3.0 as its minimum because its current contracts do not require a newer API. Consumers that use newer pathing or other contracts require the corresponding version themselves.

## Implemented API boundary

APIShared exposes the following process-wide capabilities in addition to its public ModSettings preset integration:

- `IGatehouseDistanceOriginCapability`
- `IGatehouseTimingCapability`
- `IUnitHudPresentationCapability`
- `IAivBuildStepCapability`
- `ILobbyStateCapability`

The former selected-unit broker was removed. Consumers subscribe directly to `TribeR3EventHooks.OnTribeIssueOrderWithTarget` and choose the required Pre or Post phase. APIShared does not wrap `GamePathingManagerAPI`, `PathConnectionRecord`, `GamePlayerManagerAPI.PlayMessage`, or another public Script Extender 2.4.0 contract.

Native addresses, patterns, memory writers, detours, concrete services and ownership state remain internal. The managed lobby observer initializes in `Awake()`; native capabilities initialize from `CrusaderLibrary.LibraryLoaded`. Process-wide hooks and callbacks remain rooted for the process lifetime. Capability failures are isolated and fail closed.

The public preset integration in APIShared 0.4.0 owns `PresetLobbyModSettingsViewModel`, registration, property scopes, JSON discovery/export, copy commands, and the typed `IModSettingsPresetEndpoint`. Consumers reference APIShared directly; they no longer source-link the former `Shared` implementations or compile their own lobby observer. ExtendedData is an optional typed consumer of the same contract.

## Completed consumers

| Area | Consumer | State |
|---|---|---|
| Gatehouse timing | `ExtraFeatures` | Uses only `IGatehouseTimingCapability`; the local timing patch and its native target code are removed. |
| Gatehouse distance origin | `BugfixesAndQoL` | Uses `IGatehouseDistanceOriginCapability`; the synchronized setting selects centered or Vanilla distance. |
| Unit HUD | `BugfixesAndQoL` | Controlled-Lord category and interaction use APIShared. |
| Unit HUD/control groups | `BugfixesAndQoL` | The mod retains its disband hook; one-based control-group mutation is owned by APIShared. |
| Unit HUD | `Testmods/SkinTest` | HUD image overrides use APIShared. |
| Unit HUD | `Testmods/VirtualUnitsPrototype` | Virtual-unit category uses APIShared. |
| Selected-unit commands | `BugfixesAndQoL` and relevant testmods | Use Script Extender events directly. |
| Path components | `RandomEvents` | Uses `GamePathingManagerAPI.GetPathComponentGrid()` directly with a span-length guard and requires Script Extender 2.4.0. |
| Gate/path records | `ExtraFeatures`, `ImprovedHunters`, relevant `Testmods` | Use the 2.4.0 `GamePathingManagerAPI` and `PathConnectionRecord` contracts directly. |
| AIV build step | `ExtraFeatures`, `Helpers/ActiveAIVDetector` | Share the single permanent APIShared detour at `0x51790`; deterministic observers cannot modify arguments or result. |
| Lobby state | `BugfixesAndQoL`, `CastlePlanner`, `ExtendedData` | Share one managed dirty-plus-15-frame observer. Publish, readiness, host/client and map-slot remapping remain mod-local. |

`Testmods/APITest` was removed after its assertions were transferred to the APIShared test suite and both production pilots stopped using local gatehouse mutations.

## 2.4.0 capability classification

Before adding any APIShared surface, audit the canonical local Script Extender source first. Use this classification:

- Direct Script Extender: path-component grid, edge/connection/moat/packed-plan data, `PathConnectionRecord`, route-component queries, selected-unit events and `PlayMessage`.
- APIShared: only identical process-wide hook or mutation targets shared by multiple independently loadable mods, plus stable typed callback/data contracts.
- Mod-local: gameplay policy, settings, UI, networking, save formats, diagnostics and single-consumer native behavior.

Current classification after the final overlap audit:

- The production conflict at `0x51790` is resolved: only APIShared detours the AIV build step. `ExtraFeatures` and the optional `Helpers/ActiveAIVDetector` prebuild trace register observers.
- `CastlePlanner` only binds and calls AIV functions `0x53D00`, `0x54DE0` and `0x54F60`; it does not consume the AIV capability. It now depends on APIShared solely for `lobby-state`.
- `Helpers/HunterQueryTargetDiagnostic` currently observes the State-7 writer at `0x12FEC1`; no identical target exists in `ImprovedHunters`, so no Hunter API capability is justified by the current code.
- The ExtraFeatures/Bugfixes troop-action implementations share layout concepts but do not currently prove an identical hook entrypoint. Keep them local until an exact-target audit demonstrates overlap.
- Recruitment, player/readiness, AI-economy and other single-consumer candidates remain local unless a second identical owner is demonstrated.

## Release and compatibility policy

- Workspace release projects live at root, under `Testmods`, or under `Helpers` according to `Shared/Release/release-projects.json` and `Shared/ScriptExtenderUpdate/mods.json`; do not assume a fixed mod count.
- Runtime projects compile against the installed `BepInEx/plugins/000shcdese/SHCDESE.dll`. `ExtenderDir` is the explicit override; local Script Extender output is not an implicit fallback.
- APIShared is referenced with `<Private>false>` and must never be copied privately beside a consumer DLL.
- Consumers that call a surface first introduced in APIShared 0.3.0 require 0.3.0 themselves. Preset-capable ModSettings consumers require APIShared 0.4.0. Older consumers retain their actual minimum.
- Release metadata identifies APIShared consumers and their minimum versions. Thin archives exclude APIShared; bundle archives contain one validated APIShared copy. The SerpsMods package stages APIShared once as infrastructure and keeps consumers thin.
- Version changes are atomic across active plugin and manifest metadata. Minimum Script Extender versions remain based on actual API use rather than the workspace-wide target.

## Required verification

Before every runtime build, scan source and project files for forbidden JSON serializers and Unity teardown paths. JSON remains source-linked through `Shared.DependencyFreeJson`. Verify CRLF for every changed text file, run the relevant static/unit tests, and only then invoke each changed mod's `build.bat /nopause` once.

Native evidence is valid only when the installed `CrusaderDE.dll` matches SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The lobby contract additionally requires installed `Assembly-CSharp.dll` SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` and the audited Script Extender 2.6.0 managed contract. An unchanged game hash does not replace the managed-contract audit.

Automated acceptance covers the public surface, snapshot immutability/value equality, observer identity and ordering, late registration, reentrancy, callback isolation, dirty/fallback/map policy, one Original call, unknown hashes, resolver failure and unpublished transaction rollback. Manual lobby acceptance covers host/client create, join, leave, membership changes, slot remapping, map start/end and save reload. Existing AIV, HUD and gatehouse scenarios remain required. `Testmods/PreplacedTest` remains an intentionally isolated overlapping diagnostic. README files intentionally remain unchanged; their capability descriptions require separate user approval.
