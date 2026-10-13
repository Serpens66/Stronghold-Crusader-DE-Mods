# Gatehouse acceptance after bridge split

> Current interpretation (2026-10-13): historical PASS observation and detour labels do not establish a Vanilla improvement. See the final evidence-correction section; functional gatehouse acceptance requires the distinct planning/route evidence described there.

EnemyGatePathfindingTest 0.1.5 now limits its edge masks to gatehouse entry/exit boundaries. Linked bridges still establish identity/axis and appear in gate state diagnostics. All independent bridge edge masks, including the former center seam, are removed. EnemyBridgePathTest is independently registered read-only diagnosis. Versions and README remain unchanged; no gate behavior is integrated into BugfixesAndQoL yet.

## Evidence matrix

| Case | Existing evidence | Required before integration |
|---|---|---|
| Open/closed enemy gate, moat alternative | User observed moat filling in both; 8,888 / 8,612 AI searches, zero NoRoute | Repeat known gate layout after removing bridge masks |
| Genuine access beside gate | User observed correct use; 9,554 searches, zero NoRoute | Repeat with pure gate mask |
| Building movement player vs native search player | Three resolver runs: 11,100 / 14,151 / 8,888 searches; zero NoRoute, 13 / 14 / 30 verified role differences; integrity passed | Normal building attack and Raid attack |
| Weighted Assassin compatibility | User confirmed improvement after policy integration; weighted route/caches statically checked | Improvement enabled on pure-gate layout |
| Own keep/gate and cursor/formation | User confirmed corrected cursor and preview | Blocked enemy cursor target then own keep/gate |
| Captured gate | User observed use; older 16,835-search run had four constant early NoRoute | Capture and recapture on final scope |
| Capturer's ally | Observed in existing capture path | No dedicated new map needed |
| Original owner's ally | Statically checked, not observed in game | No user test required, limitation retained |
| Map replacement | Two epochs in one process without integrity errors | Include reload after longer test |
| Multiple attack waves | Not yet final acceptance | No growing failed movement series |
| Mainmods without testmods | Static/regression tests; short game comparison awaiting user confirmation | Normal short run without both testmods |
| Independent lateral bridge crossing | Separate experimental failure: 5,712 NoRoute | Out of gatehouse acceptance; bridge investigation |

Historical scope conflicts are not retroactively cleared. Prior working gate runs used the broader bridge mask; removal therefore requires the specific regression above. Successful technical tests alone do not settle the game behavior.

## Integration decision

Gate integration can be prepared once pure-gate regressions confirm the known alternatives, real access, building/Raid and Assassin paths, capture/cursor behavior, longer run and mainmod comparison. It is not yet released or marked finally accepted. Vanilla continues to choose alternatives and orders.

## 2026-10-04: gate-only game regression after bridge split

LogOutput.log contains one process start and one test_gates.sav map epoch, 16:12:12.928–16:13:53.811. Loaded APIShared 0.4.7, BugfixesAndQoL 1.0.174 and EnemyGatePathfindingTest 0.1.5; no EnemyBridgePathTest loading/runtime lines. User reports that everything appeared to work in game; specific scenarios were not individually identified, so this does not certify every outstanding acceptance case.

Final counters: 21,204 AI builder searches, zero AI NoRoute; 23,953 total queries, 866 rejected edges, 673 AI vanilla detours and zero policy NoRoute. Building approach: 35 calls and 35 validated argument-role differences, zero building-context failures. Scope mismatches, exceptions, thread-slot conflicts, snapshot-pool exhaustion and snapshot errors all zero. Hook execution/runtime integrity/thread-slot/lifecycle verdicts PASS. Final command events are paired: target 263/263, tribe movement 1,010/1,010, unit movement 50,930/50,930. Sum of every decision aggregate count is exactly 978,565, matching the final observation counter.

Gate 819 retains Global-ID 2423576 during repeated capturer changes: one initial capture and sixteen subsequent capture transitions. All 17 callback CaptureMismatch observations (14 graph, 3 builder) were confirmed recovered by later matching publication, none unresolved at map end. Owner and capture relations remain separate: own + captured-by-other is recorded; capturer/self and capturer/ally are recorded as permitted. These observations do not by themselves prove recapture by the original owner.

Cursor: 781 requests, 172 refreshes, 609 exact cache hits, no validation deferrals or exceptions; average 0.205 ms, maximum 3.177 ms. No proven cursor-policy block was exercised in this run. Seven Assassin observations were native flood-field queries without a materialized route; this is not a new weighted Assassin route/cache acceptance test. Raid-specific retargeting and a genuine alternative access are not separately proven by the supplied observation. Duration about 101 seconds does not replace a multi-wave endurance test.

Known path-table coverage warning remains 89/90 profiles and 534/540 permissions; all compared values match, no mutation evidence. No Error/Fatal log lines. The legacy hookOwnerConflict=True label denotes shared mainmod ownership, not a failing integrity verdict. No runtime code or installed DLL was changed for this analysis. This run supports the gate-only regression and independence from the bridge observer; remaining targeted acceptance cases retain their documented limits.
Source log SHA-256: 53FCE6F47F5086030DCB0334CAA84393A421933DB4950A526A1C984BBAFA893C.

## 2026-10-11: captured Assassin gatehouse accepted, diagnostic limits retained

| Case | Current evidence | Assessment |
|---|---|---|
| Assassin ground passage after capture | Gate134/global6658 published capturer2 at16:57:28.379; permitted weighted Ground edges and final outputs afterwards; user saw later Assassins use the gate normally | Confirmed release and observed use |
| Legal gatehouse climb before capture |22weighted routes with permitted climb, including a cached route; no confirmed weighted violation | Confirmed evaluated climb; older physical observations retained |
| Weighted search/publication |131checked routes including1cache,58validator checks passed;38staged=19published+19discards, output-mode94 reported | Existing guard preserved; no reproduced validation failure |
| Native packed overlaps |237checked outputs;10suspect,17other movement-unproven overlaps | Actual native movement remains unproven; no confirmed ground violation |
| Raid alternative target | Role3 twice retargeted to a fresh valid attack point; user saw sensible overall behavior | Target/point confirmed; complete following Raid route not certified |
| Negative AI builder series | All675negative AI builders are slaves with single-unit command3, not Assassins | Do not attribute to Assassin routing or assume a gate caused failure |
| Lifecycle/diagnosis | Twoepochs, all paired events, counterAgreementPASS, no adapter/scope/snapshot errors | Confirmed for this run |
| Resources lookup errors |24SDK errors with negative player IDs; no direct resource lookup in installed Gate/Bridge IL | Separate unresolved caller, no gatehouse-policy fix justified |

Saves: test_gates_bridge2.sav16:55:38--16:56:48 (15,463AIqueries, zero negative AI builders, no checked Assassin routes), then test_Assassins_stuck_at_drawbridge.sav16:56:58--16:58:18 (3,427AIqueries). Both had the passive Bridge observer enabled, SE2.14.1, APIShared0.7.0, Main1.0.185 and Fixes1.26.4. User reports that later arrivals correctly walked through the captured gate; earlier climb may reflect an already assigned command, not a new error after release. This observation does not by itself prove that timing for every old climbing unit.

The diagnostic transition label after16:57:28.393 was wrong: Extender Post is delayed100timerunits, and the old helper rearmed Pending after a matching publication. Historical transition labels are not proof of a functionally pending gate. The private helper now preserves the confirmed publication across Post and validates owner/capturer/global identity. Gatepolicy/weighted climbing/native mode94 behavior unchanged. Technical regression:66,660Gate assertions plus89actual diagnostic-path assertions; elevated gate-only build/install successful. DetailedDiagnostics is now false; standard end-of-map counters remain. No new game test required for this correction. Final integration/release still requires the separately listed mainmod-without-testmod and other unobserved cases; bridge behavior is excluded.

## 2026-10-13: evidence correction, functional gate rule retained

The feature audit confirms that ordinary native E2610/E2F60 traversal already excludes hostile uncaptured gatehouses. Same-component shortcuts, special Assassin paths and unrelated capturers remain distinct policy cases. A gate building-approach order38 is not a through-gate movement order. Policy, permitted climbing, cursor validation and native order selection are unchanged.

Historical observation PASS and aiDetour/vanillaDetour labels are superseded: activity is OBSERVED; successful searches with rejected edges do not prove a counterfactual detour. The original-call result under installed hooks is labeled nativeWithActiveHooks. Negative searches with/without rejected edges and incomplete calls are now separate. The old filtered aiNoRoute counter was not an all-negative-search counter. The October1 adapter fix repaired our flags transport and is not, by itself, proof of a Vanilla improvement.

Detailed evidence uses existing synchronous order/search frames. Gate comparisons from both graph and builder sites, actual region calls, search outcomes and inclusive parent order summaries are counted. Native comparisons already rejecting an uncaptured gate are preservation, not a new block. Early policy rejections and individual-route-only filtering are separate; no region callback remains unobserved. Building approach, stored unit command, group move type, complete allowed route, confirmed ground violation and unproven packed movement remain distinct. An order post is not proof of physical execution. No extra native calls or searches are introduced.

New attachment points: GateSearchEvidence.cs (pure classifications, ephemeral frame counts, window aggregates); AttackOrderCorrelationDiagnostics comparison/scope/region/Finish/checkpoint; SamePclGateRouteRuntime completion counters; EnemyGatePathfindingRuntime both comparison sites and summaries. TEMP_GATE_ROUTE_ACCEPTANCE detailed observations are optional; standard counters do not register a route observer. Windows retain counts and first/last details, no per-unit history or event cap. Existing route validation and capture timing remain unchanged.

Abnahme: historical moat work, captured access and climbing remain valid observations/regressions, not generic proof of a new Vanilla fix. A demonstrated upstream policy effect and a fully inspected allowed route are separate evidence. Same-PCL planning coverage and native packed movement gaps remain open unless directly measured. Mainmod integration remains pending; the bridge experiment is separate. See baseline knowledge/ENEMY_GATE_VANILLA_EVIDENCE.md.
Technical status 2026-10-13: 66,830 policy/adapter/diagnostic assertions passed, including installed RedBird machine execution. The additional isolated fixture executes the actual order-correlation and unsafe scope-completion methods: 30 assertions passed, including diagnostics on/off, exact restoration and negative/incomplete counters. All 35 evaluated runtime sources compile in memory against installed APIShared; no production DLL was emitted. Runtime JSON/lifecycle/scheduling, actual installed layouts, 18 gate spans against 35 Extender spans, permanent hooks, XAML, minimum dependencies and CRLF checks passed. The Macemen SDK fixture rebuild and production build/install await a fresh APIShared package: its source/package provenance check reported stale, and the user explicitly requested waiting for parallel changes. No mainmod, setting, version, README or save was changed.

Build completion 2026-10-13: after user clearance, APIShared 0.8.0 package provenance passed. Repeated installed-API compile check, all 96 actual Macemen observer checks and 30 actual correlation/scope checks passed. Elevated EnemyGatePathfindingTest/build.bat /nopause completed with exit0, 66,830 assertions, zero compiler warnings/errors and successful installation. Installed/local DLL SHA256=5dcf8c3306a26190c8154a75beafbb448b1b94fafe24ff032329f8ba88f0b67c. Version0.1.6 and minimum dependencies unchanged. DetailedDiagnostics=true and MacemenDiagnostics=true retained. No APIShared/Main build or gameplay changes. Next game check: short known gatehouse-without-bridge scenario, open then closed, plus permitted own/captured passage; leave map for final report. Identify gate and whether observed action was attack, ground traversal or climbing. This measures current behavior; a pristine Vanilla benefit remains a separate comparison.
