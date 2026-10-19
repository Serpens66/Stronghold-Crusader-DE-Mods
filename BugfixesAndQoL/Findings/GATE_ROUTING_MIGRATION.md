# Gate routing migration — 2026-10-13

Status: technical migration; integrated game acceptance pending. No release or removal of the old project yet.

## Reference and ownership

Frozen accepted source/package: `_inspect/GateMigration/accepted`; hashes: `accepted-hashes.json`. Accepted DLL SHA256: `869dbe3eca9d9bcad0f03571b638fae9e8fa928ef3ea9d8e80118de809813438`.
Every source maps to its productive owner in `source-map.json`. Seven emitter/definition files retain functional text after namespace/public-delegate documentation changes (`emitter-parity.json`). Per-file differences are retained in the same audit directory. Productive tests compile current sources, not the old testmod.

| Concern | Owner | Verification |
|---|---|---|
| Host Fixes setting, default true, reset, whole-mod disable | Main ViewModel / GateRoutingFeature | actual feature orchestration; actual preset infrastructure with setting double |
| Owner/alliance/capture rule, complete footprint, masks, refresh | Main GateRouting | migrated geometry, identity and capture tests |
| Shared native comparisons, edges, cursor, descent, scopes | APIShared GateRoutes/Native | actual RedBird machine tests and full native contracts |
| Existing search function detours | Main UnitCommands | original owners and original-call paths preserved |
| Assassin weighting, route publication, formation, Raid retarget | existing Main/API owners | actual route/observer fixtures; no second retarget implementation |
| Ordered access contributions | APIShared GateAccessRules | allow/deny/unchanged, exception isolation, identity and invalidation |
| Optional migration diagnostics | Main GateRouting and existing passive API observer points | minute aggregates, owner-bound logical demand, inactive guards |

The three remaining migrated function detours use NativeX64 Indirect, no jump/call following and no chaining: 0x1232E0/6 bytes, 0xDC3C0/6, 0x113BC0/10. Complete functions and incoming edges: `native-detour-audit.txt`. Runtime validates actual committed scheme/span/entry/pointer slot/padding. Existing builder hooks retain their owner.

Persistent publishers are static session events, GameTime OnTick and Application.onBeforeRender, never plugin Update. API registration itself starts no publisher. Disabling clears logical policy; enabling reuses hooks. Rule revisions invalidate snapshots and caches. The legacy observer Current identity is unchanged; additive CurrentRequested suppresses optional work while disabled.

## Alt/new review

Preserved: baseline rule, footprint algorithm, cursor reference/policy decision, native emitters, descent predicate, original native arguments and calls, nested scope restoration. Changes: ownership, host activation, optional access contributions, revision freshness and inactive guards. Duplicate standalone function wrappers were removed from Main; the tested common Main wrappers remain sole owners. The three migrated detours now enforce the audited concrete backend instead of permitting unaudited alternatives.

Controlled native/SDK fixtures do not establish a new game result. Accepted Same-PCL detour, small/large gate, capture/recapture, Raid and defended closed-gate descent observations remain regressions. Historical unknown native outputs remain unknown. Drawbridge masking is excluded.

## Diagnostic removal inventory

Removable additions use TEMP_GATE_MIGRATION_DIAGNOSTICS alongside TEMP_GATE_ROUTE_ACCEPTANCE / TEMP_MACEMEN_GATE_DIAGNOSTICS.

- GateRoutingFeature: local detail options and optional order-event subscriptions.
- TemporaryGateRouteAcceptance/Aggregate/CaptureTimeline: route/capture evidence and minute summaries.
- NativeAssassinDiagnosticHooks/Trace/MovementDiagnostics(.Read): reconstruction/execution probes; after publication only deactivate, never unpatch.
- AttackOrderCorrelationDiagnostics, GateSearchEvidence, gate-state definitions: optional correlation/detail.
- MacemenCommandDiagnostics/Aggregate: optional command evidence.
- SamePclGateRouteRuntime / EnemyGatePathfindingRuntime: optional forwarding and verbose reports; FunctionalGatePolicyAdapter separates functional demand.
- Existing AssassinPathfindingRuntime, AiRaidRetargetFixRuntime and UnitCommands/Movement/TemporaryGateRouteReporting: requested-observer producer points.
- APIShared TemporaryGateRouteAcceptanceBridge and AssassinRouteHandoff: preserve public contracts when reducing Main diagnostics.

2026-10-19: the removable diagnosis now uses the DEVDIAG system (AGENTS/build-release.md). The pure diagnostic types live in `src/GateRouting/*.DevDiag.cs` and are compiled only by `build.bat /devdiag`; EnemyGatePathfindingRuntime and SamePclGateRouteRuntime reach them through `partial void Diag…` hooks implemented in their own `*.DevDiag.cs` parts. Normal and release builds contain none of these types, never bind the Macemen option and keep detailed/Macemen diagnosis off (`GateDiagnosticConfiguration.BuildAvailable`). Complete removal list: `Shared/Tools/Validation/Test-DevDiagnostics.ps1 -ModDirectory BugfixesAndQoL`. The remaining requested-observer producer points (TemporaryGateRouteReporting, AssassinPathfindingRuntime, AiRaidRetargetFixRuntime) and the public APIShared bridge stay unchanged; without a registered observer they only check `CurrentRequested`.

Do not remove functional identity checks, capture refresh/recovery, masks, scopes, route validation, cursor cache or AssassinGateDescentRule/Runtime. Descent adapter/callback helpers now have functional API ownership.

## Technical evidence

Native hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2: 60 full search/climb bodies, 16,960 installed instruction ranges, 21 planning/output bodies plus the three complete function detours.

157,367 migrated assertions; actual inline/NativeX64 execution; 50 production descent-reader checks; 273 Raid / 757 diagnostic-path assertions; actual native trace and route-publication producers with isolated SDK/native fixtures. Twelve actual GateRoutingFeature activation checks cover menu, own setting off/on, contributor demand, whole mod off and map reload without reinstallation. Host working-state persistence, missing default, explicit false, client lock and sync pass. Production setting metadata/setter/reset are separately checked. Actual 184 API / 406 Main sources compile in memory against real installed signatures/visibility.

## Build and next step

Build APIShared then Main using their drivers. Main moves the exact old DLL, hash-verified, to BepInEx/disabledmods/GateMigration and rejects duplicate/unexpected copies. Keep old source until migration game acceptance.

Versions stay unchanged for local testing. These paired unreleased packages are not a compatibility promise for old public APIShared 0.9.0. Before release, atomically advance APIShared and set Main's true new minimum version.

Prepare effective settings: host fix on; gatehouse climbing on; improved Assassin pathfinding off for native comparison; migration detail on; Macemen off; bridge test off.

1. test_gates_around: on uses long route; reload with fix off for prior Main behavior, then restore on.
2. Existing no-bridge editor: small/large enemy gate ground command, then permitted access after capture.
3. Defended closed large gate: one Assassin command behind it, full ascent/descent/goal without second command.
4. Leave/reload; check Main provider/API owner, complete masks and integrity.

After acceptance remove old testmod/build references; then reduce marked diagnosis in a separately reviewed step. README, reference saves and versions unchanged now.

## Driver result

APIShared driver completed 2026-10-13 22:28:55: 164 Core, 113 runtime and 21
preset tests passed; package installed. Main driver completed 22:31:16: integrated
gate suite and complete existing driver regressions passed, including 16,257
Assassin A*/Dijkstra assertions; package installed and exact legacy DLL preserved
outside plugins. The first API driver stopped safely on the old blanket public
pointer prohibition; explicit audited member/type/ABI checks now cover the new
advanced service while retaining the general prohibition. No runtime was manually
built or copied. Migration game acceptance remains pending.

## 2026-10-14: Assassin descent ownership and height review

The functional closed-gate descent guard now belongs to EnableAssassinGatehouseClimbFix, independently of EnableEnemyGatePathfindingFix. APIShared remains the sole owner of 0x185D55 (20 bytes). Sources: BugfixesAndQoL/src/Assassins/AssassinGatehouseDescentFeature.cs, AssassinGateDescentRuntime.cs and AssassinGateDescentRule.cs. Gate diagnosis only attaches optional observers; detaching them cannot disable descent. No new hooks or API contracts.

The live guard validates unit/global ID, full control WORD, exact native pointer and current gate/global/owner/capturer identity before and after transition classification. No gate-policy snapshot permission is substituted. All four settings combinations, session changes and observer detach are covered by the production feature harness (64 assertions together with reader fixtures).

Native height review: DCE60 initializes grid-height difference; 16CD70 consumes descent progress and commits the destination through 180A80. 184FD0 independently adds the roof/render offset and climb progress. Executing the installed 184FD0 in an isolated memory fixture demonstrates that a wall/building offset of 40 is separate from the grid-height difference. This is NOT yet proof of the actual reported visual defect: the earlier logs lack those raw heights. No speculative coordinate/height correction was applied. Existing movement observations now include world coordinates, base height, render offset, climb height and progress. Logical destination changes must not be undone merely to alter animation.

Validation: full 60 search/climb and 21 planning/output native audit retained; gate suite 157368 assertions, native adapter register/flags tests, copied native height reader, runtime preflight and shared-boundary checks passed. Installed public field layout validated for B6/B8/410/414 and full control WORD at 92h. Interop fields 410/414 are DWORD storage; native audited instructions consume their signed low WORDs. No new Assembly-CSharp member access.

Remaining acceptance: one closed, defended gate without drawbridge, one Assassin ground command behind it. Record descent appearance with climbing and gate policy enabled so the existing gate observer collects the raw heights. Then repeat with gate policy disabled to check functional independence; the separate descent summary remains available, but gate-observer detail is intentionally inactive. Height diagnosis is observational; the rendering defect remains open until the recorded values establish the exact correction. No repeat of Raid or around-route acceptance required.