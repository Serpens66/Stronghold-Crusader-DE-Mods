# APIShared migration plan and current state

Status: 10 September 2026. Development and compatibility checks target Script Extender 2.4.0, tag `v2.4.0`, commit `5d5719c1002aec043d331162d72b2e7f3111b34b`. APIShared retains Script Extender 2.3.0 as its minimum because its four current contracts do not require a 2.4.0 API. Consumers that use 2.4.0 pathing or other new contracts require 2.4.0 themselves.

## Implemented API boundary

APIShared exposes exactly four process-wide capabilities:

- `IGatehouseDistanceOriginCapability`
- `IGatehouseTimingCapability`
- `IUnitHudPresentationCapability`
- `IAivBuildStepCapability`

The former selected-unit broker was removed. Consumers subscribe directly to `TribeR3EventHooks.OnTribeIssueOrderWithTarget` and choose the required Pre or Post phase. APIShared does not wrap `GamePathingManagerAPI`, `PathConnectionRecord`, `GamePlayerManagerAPI.PlayMessage`, or another public Script Extender 2.4.0 contract.

Native addresses, patterns, memory writers, detours, concrete services and ownership state remain internal. APIShared initializes once from `CrusaderLibrary.LibraryLoaded`; process-wide hooks and callbacks remain rooted for the process lifetime. Capability failures are isolated and fail closed.

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

`Testmods/APITest` was removed after its assertions were transferred to the APIShared test suite and both production pilots stopped using local gatehouse mutations.

## 2.4.0 capability classification

Before adding any APIShared surface, audit the canonical local Script Extender source first. Use this classification:

- Direct Script Extender: path-component grid, edge/connection/moat/packed-plan data, `PathConnectionRecord`, route-component queries, selected-unit events and `PlayMessage`.
- APIShared: only identical process-wide hook or mutation targets shared by multiple independently loadable mods, plus stable typed callback/data contracts.
- Mod-local: gameplay policy, settings, UI, networking, save formats, diagnostics and single-consumer native behavior.

Current classification after the final overlap audit:

- The production conflict at `0x51790` is resolved: only APIShared detours the AIV build step. `ExtraFeatures` and the optional `Helpers/ActiveAIVDetector` prebuild trace register observers.
- `CastlePlanner` only binds and calls `0x53D00`, `0x54DE0` and `0x54F60`; it does not detour those functions and therefore needs no APIShared dependency.
- `Helpers/HunterQueryTargetDiagnostic` currently observes the State-7 writer at `0x12FEC1`; no identical target exists in `ImprovedHunters`, so no Hunter API capability is justified by the current code.
- The ExtraFeatures/Bugfixes troop-action implementations share layout concepts but do not currently prove an identical hook entrypoint. Keep them local until an exact-target audit demonstrates overlap.
- Recruitment, player/readiness, AI-economy and other single-consumer candidates remain local unless a second identical owner is demonstrated.

## Release and compatibility policy

- Workspace release projects live at root, under `Testmods`, or under `Helpers` according to `Shared/Release/release-projects.json` and `Shared/ScriptExtenderUpdate/mods.json`; do not assume a fixed mod count.
- Runtime projects compile against the installed `BepInEx/plugins/000shcdese/SHCDESE.dll`. `ExtenderDir` is the explicit override; local Script Extender output is not an implicit fallback.
- APIShared is referenced with `<Private>false>` and must never be copied privately beside a consumer DLL.
- Consumers that call a surface first introduced in APIShared 0.3.0 require 0.3.0 themselves. This includes `BugfixesAndQoL` because control-group removal is new in 0.3.0; older Gatehouse/HUD consumers that use only the 0.2.0 surface may retain 0.2.0.
- Release metadata identifies APIShared consumers and pins bundle contents to APIShared 0.3.3. Thin archives exclude APIShared; bundle archives contain one validated APIShared copy. The SerpsMods package stages APIShared once as infrastructure and keeps consumers thin.
- Version changes are atomic across active plugin and manifest metadata. Minimum Script Extender versions remain based on actual API use rather than the workspace-wide target.

## Required verification

Before every runtime build, scan source and project files for forbidden JSON serializers and Unity teardown paths. JSON remains source-linked through `Shared.DependencyFreeJson`. Verify CRLF for every changed text file, run the relevant static/unit tests, and only then invoke each changed mod's `build.bat /nopause` once.

Native evidence is valid only when the installed `CrusaderDE.dll` matches SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` and the semantic baseline identifies Script Extender commit `5d5719c1002aec043d331162d72b2e7f3111b34b`. An unchanged game hash does not replace the managed-contract audit.

Automated acceptance covers public surface, observer identity and ordering, reverse completion, reentrancy, callback isolation, one Original call, unknown hashes, resolver failure and unpublished transaction rollback. Manual acceptance still covers both plugin load orders, the opt-in prebuild trace alone and alongside ExtraFeatures, multiple HUD consumers, gatehouse combinations, and host/client equality. `Testmods/PreplacedTest` remains an intentionally isolated overlapping diagnostic. README files intentionally remain unchanged; their stale version and capability descriptions require separate user approval.
