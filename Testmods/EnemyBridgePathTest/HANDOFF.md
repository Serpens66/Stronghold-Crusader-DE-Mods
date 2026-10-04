# EnemyBridgePathTest — handoff (2026-10-02)

## Goal and current boundary

Make an enemy drawbridge look physically raised to AI planning even when a lateral deck crossing does not pass through its gatehouse. First find the earliest different Vanilla decision. This build is **read-only**: its closure mask is hypothetical and is never registered as the gate policy. Since 2026-10-04, 25 permanent passive native function probes supplement the existing observers. It co-loads with EnemyGatePathfindingTest and works with APIShared/BugfixesAndQoL without that testmod. Later gate integration must preserve the independent observer contract.

## Reproduction and historical logs

Use `test_gates.sav`. Demolish/rebuild a gate next to moat so the lowered deck can be crossed sideways around the gatehouse. Finish the layout before the attack begins. Compare bridge down versus physically raised in separate save epochs; record unit behavior as well as logs.

Historical first shifted example: gate 578 / bridge 539, portals PCL 1 on both sides. Gate/bridge association was present; the old center seam missed lateral crossing. A later experimental 15-cell active mask was built at 22:46:31. Epoch 22:50:55.459–22:53:11.085 built gate 834/global 2433338 at 22:51:19 and bridge 839/global 2433362 at 22:51:21. It counted 24,098 AI searches and 5,712 NoRoute; 4,805 command-3 MACEMAN failures. Failures continued after publication. Integrity passed. The user saw initial approaches, then no further approaches and no moat filling. These are historical experimental-mask results, not results of this diagnostic build.

## Confirmed native contract

Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; SE SHA-256 `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`. Source commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree `657af449e1397c58e6c5ec054977d83198b68e66`. Installed RedBird 1.5.0.0. Verify identities again before changing native behavior.

- Creation `0x739C0` stores occupied IDs in mapper order; `0x69850` produces coordinates. Game IDs are 1-based; spans are 0-based.
- Animation `0xA5E20` calls raise `0x645C0` / lower `0x64460`. Mapper `0x2D1A30 + orientation/2 * 100` contains 25 Int32 entries; 15 are nonzero. Orientations 0/4 select middle three rows, 2/6 middle three columns.
- Raising requires a nonzero moat-record index (`0x69560`, tile manager `+0x1EA23F0`); selected cells receive `0x40000000` and record state 2 through `0x725A0`. Lowering removes the flag and restores state 0. `0x725E0 → 0xD90D0 → 0xD86F0` updates surface edges; `0xE3B90` invalidates regions. Blocking mask `0x4A5014B1` includes that flag.
- Drawbridge type is `eStructs.STRUCT_DRAWBRIDGE`, 49. `0xE0770` is stairs (type 0x45), not a bridge graph builder.
- `0xE2610` accepts equal PCLs before gate comparisons. Ordinary group `0x11B520` can skip that call entirely for equal PCLs. Assassin-only groups also bypass ordinary region checks (`0x117820`). A missing observed region call is not an observed E2610 acceptance.
- `0x11E960`, command 7 (`TribeAICommand.Unknown7`), uses `0xE7F60` when leader PCL is nonzero. Zero selector result returns early; a positive result selects work-access coordinates and falls through command 6. Both enter nested `0x11B520` and write the issued tribe command/context coordinates for eligible units. The event captures this path without a new callsite hook. Leader PCL zero skips the selector. Missing/invalid leader context remains unclassified.
- Installed public unit fields: issued command `+0x398`, context X/Y `+0x3E4/+0x3E6`, moat task index `+0x3B4`. Task presence is not proof of a new assignment or of executed work.

Full retained feature evidence: `_inspect/EnemyGateBuildingContextAudit/bridge-native-evidence.txt`; baseline `knowledge/ENEMY_GATE_DRAWBRIDGE_POLICY.md`. Gate adapters and mainmod hooks have their existing owners; no hook address changes in this split.

## Code entry points

- `src/BridgeSnapshot.cs`: deferred building/tile snapshot, parent gate identity/link source, role labels, raw flags/PCLs and hypothetical incident edges.
- `src/DrawbridgeClosurePolicy.cs`, `GateEdgeOwnership.cs`: migrated pure experiment; never published as active policy.
- `src/BridgeDiagnostics.cs`: short-lived Pre/Post command contexts, nested work access, synchronous before/after unit fields, existing search outcomes and Assassin route inspection.
- `APIShared/src/EnemyBridgeDiagnosticBridge.cs`: independent passive registration, exception-isolated paired search and region forwarding.
- Mainmod `MovementPathPublication.cs`, `MovementSearchContext.cs`, `FriendlyMoatMovementRuntime.cs`, `AssassinPathfindingRuntime.cs`: reports from existing detours. No observer means no added context, state capture or route inspection.
- `verify.ps1`, `tests/Program.cs`, `tests/DrawbridgeClosureTests.cs`, `build.bat`.

## Reading the new diagnosis

Ten-second active aggregates retain exact event counts, first/last concrete values and target changes. State definitions are output before references; no 32-event/pattern limit. `gate=` in the shared aggregate denotes the bridge ID in this mod, not a selected gate. Bridge/PCL states are candidates or snapshot facts, not proof of a chosen bridge. Unknown identity, association and player roles are explicit.

`same-pcl-with-no-region-call` separates the group bypass case from `region-query-executed`. It describes observed calls in the whole short-lived context, not a disassembly trace of an individual branch. `work-access` records the nested selected-access movement; `work-unit-fields` compares command and context, preserving task values separately. Actual work remains a game observation.

## Remaining gaps and next comparison

Region/builder/Assassin hooks can be absent depending on mainmod feature installation. No callback is reported as missing coverage, never as a negative search. The final physical bridge animation and the earliest AI planner acceptance still need paired evidence. A context with no region callback can also take an Assassin/other branch; do not infer a precise branch from absence alone. Snapshot reads are deferred, not an atomic view of all simulation memory. Unexpected identity/shape must remain unknown.

1. Bridge below/raised with already finished layout, same save and attack setup.
2. Compare the first group decision, executed region calls, later route and work-access entry.
3. Normal soldiers and Assassins with improvement on/off; genuine side access and own/captured bridge.
4. Only then select a player-/bridge-specific intervention at the first false acceptance. Do not block whole PCLs, change global tile flags or synthesize alternative orders.

Event coverage: Script Extender emits no Post after SkipOriginalFunction. Retained Pre args are checked for suppression before nested observations and at deferred cleanup; unsuppressed missing Post is an error. Event subscribers can alter arguments or issue nested replacements. Therefore work-access is an observed nested movement, never direct proof of E7F60 success.

## 2026-10-03: completed technical verification

All four affected build.bat drivers completed successfully (APIShared, BugfixesAndQoL, EnemyGatePathfindingTest, EnemyBridgePathTest). Local and installed DLL SHA-256 values match. Existing versions and README files are unchanged; the new bridge mod is 0.1.0. Gate: 3,830 policy assertions and real installed-RedBird adapter execution. Bridge: 4,849 geometry/observer/aggregate assertions, including independent registrations, native-call counts, observer failures, nesting, map reset and over-32 state coverage. Mainmod: 15,494 weighted Assassin assertions; 269,682 unit-plan assertions, 6,480 building-distance checks, 18,262 independent search assertions and 1,469,340 cursor comparisons. Full gate/bridge source compilation was checked against installed assemblies before building.

JSON/lifecycle, enduring callback ownership, installed public members/layout, permanent hook mutation, XAML and CRLF checks passed. All builds had zero errors; both testmod runtime builds had zero warnings. BugfixesAndQoL retained an MSB3277 Mono.Cecil reference-version warning; test dependencies also report framework/reference warnings. No new runtime JSON dependency was introduced.

Final review corrected occupied-array capacity to 36 cells and records ordered raw footprint values. Bridge contexts distinguish suppressed events (SE has no Post in that case) from unsuppressed missing Post; retained mutable Pre arguments and original-input Post arguments are identified separately. Nested work movement is observed evidence, not a direct E7F60 return or proof of execution. Assassin diagnostic failures are counted without changing results. No extra native hooks, active bridge masks or persistent unit monitoring were added. Pure-gate in-game regression remains required before integration.
## 2026-10-04: early-decision diagnosis

See ACCEPTANCE.md for the fresh-order comparison matrix and log interpretation. Version and README remain unchanged. This is instrumentation, not the final decision fix.

The complete feature audit retains 69 current-hash function bodies/references in `_inspect/EnemyGateBuildingContextAudit/bridge-decision-evidence.txt`. `bridge-decision-contracts.json` records all 25 entry spans; `BRIDGE_DECISION_AUDIT.md` documents physical topology, strategic planning, ordinary attack, tactical dispatch, task selection and hook ownership. Earlier claims of "no extra native hooks" above describe the previous milestone.

`BridgeNativeHooks` uses the installed NativeX64 backend with Absolute only, full-function scan limits, exact displaced lengths and live entry validation. Originals are called exactly once and all return values are forwarded unchanged. The static runtime permanently roots hooks and delegates; duplicate library initialization cannot replace them. Published hooks have no teardown path. Backend mismatch or another entry owner leaves coverage explicitly incomplete.

`BridgeDecisionTrace` captures all live bridge cells synchronously at function entry/exit and command Pre/Post, interns complete states, records dirty/revision counters, raw planner arguments/globals, candidate tables, selected task output and assigned tribe states. Capture includes ordered occupied cells, PCL/moat/flags/direction, seed/plan bytes and parent linkage/roles. An adjacency candidate is not authoritative parent proof. Global IDs distinguish reused building IDs. Parent operation, session, thread, tick and monotonic clock join observations. No extra Vanilla search is performed.

Physical-generation counts calls, not completed animations; topology-generation increments only after a completed rebuild returning 1. Compare dirty/revision and actual cells, not gate animation alone. Native probes observe effective function outputs, including existing internal patches. Existing region/search adapters separately report native/effective results. Candidate list deltas establish membership changes, not the internal rejection reason of every scanned tile. No observed region call is never proof of the equal-PCL branch: compare audited code, raw regions, retained target and Assassin context.

Transport is bounded to 32768 records; overflow/capture failures invalidate completeness and appear in summaries. Counters are process cumulative; session IDs and scopeSession identify map boundaries. All synchronous capture has diagnostic overhead, which must be assessed in the first game run. Deferred snapshots remain context only.

Map start can occur inside a native/event call: retain the Pre frame with its original epoch, classify the crossing Post as event-boundary, and keep it out of current-map Pre/Post totals. Suppressed originals still have no Post by the installed event contract. Unsuppressed missing Post remains an error. Native scopes retain their entry session through exit. No lifecycle callback is used to undo hooks.

Pending evidence: controlled fresh decisions down versus fully raised; branch-specific per-candidate rejection when list deltas alone remain ambiguous; effective internal patches with Fixes active; startup boundary behavior in a new log. Do not infer any of these from the historical failing run.

### Build/installation evidence

2026-10-04 17:30 local: the prescribed elevated build.bat /nopause completed with exit0. Policy/geometry/observer tests and actual installed NativeX64 copied-function/production-wrapper tests passed. Runtime compilation: zero warnings, zero errors. Installed package contains only EnemyBridgePathTest.dll, its PDB and info.json; no additional runtime serializer/dependency package was deployed. Installed and local DLL SHA-256 both D6AC6A80E77A0504D55B91E29494199BA1FB4A1511F4B27E1F57D09A6489CF0E. Version remains0.1.0; README unchanged.

Before build: mod/workspace permanent-hook checks, runtime JSON/lifecycle/XAML/CRLF checks, installed public fields/layout and OnTick signature checks, and regenerated current-hash69-function/25-entry audit all passed. git diff --check passed afterward. In-game A/B and post-startup new-hook execution are pending; offline success does not establish game decision behavior or capture overhead.

## 2026-10-04: automatic lean diagnosis

The first detailed run (17:51:26-17:52:18) exposed the diagnostic cost: 75,117 ordinary-attack calls, 1,109 topology calls, only 21 distinct live states, approximately70.5MB Bridge logs, and434,224 Script-Extender errors for Building-ID0. The invalid parent lookup in DescribeLiveParent caused that error flood. No raise/lower or early-planner callbacks appeared in that loaded-save run; it cannot establish the down/raised decision difference.

The previous full-per-call trace is replaced, without a parallel fallback. Fixed argument values and per-thread recycled scopes avoid hot callback allocations. Ordinary attacks compare exact numeric tribe identity/state/retained-target data; unchanged calls are counted rather than captured/logged. Changed attacks and new commands promote their contexts. A promoted entry is labelled promoted-later and never claims its bridge capture occurred at native entry. Rare planning/task/physical probes retain their synchronous observations.

BridgeBuildingIndex discovers bridge/gate slots and footprints together, resolves parent candidates without invalid ID lookups, and refreshes after relevant spawn/delete notifications or at the next capture after one second. Live captures revalidate identities and read current bridge fields and parent roles; stale associations are explicitly unknown. Static R3 subscriptions are rooted, and no hook owner/backend/ABI/address changes. Deferred snapshots remain separate context.

Regions outside detailed scopes are numerically aggregated with exact counts, native/effective values and first/last parent IDs. Detailed scopes also emit linked region observations. The repeat aggregate includes those detailed observations: do not sum both streams. Candidate definitions retain exact raw rows, with unchanged table references. State definitions contain their first capture clock; the referring record clock identifies a new synchronous equality check.

Transport capacity4096; at most64 records/2ms target per render drain. A single logger call can exceed the target, and cumulative drain/capture time is measured. No synchronous backlog flush at map end. Remaining queued records retain their recorded session/thread/tick/clock and drain through the permanently rooted render publisher. Counter snapshots preserve entered=exited+active. Overflow is explicit missing coverage, distinct from exact coalescing.

Acceptance: tests use the actual production trace with independent capture/stamp adapters and the actual production native wrapper. Repeated75117-call input must create only two initial captures and under4KB trace; additionally test changed targets/global reuse, promoted commands, nesting, capture exceptions, session crossings, bounded drain and queue overflow. Game performance, complete index invalidation and the95%/<1MB-per-minute targets still require the next controlled run.

### Final lean verification

2026-10-04 18:19: build.bat /nopause completed and installed the final package after the additional index identity checks. All5293 assertions passed. Actual production trace steady-load fixture:75117 identical ordinary attacks,2 captures,2 output records,600 bytes of trace payload,13.966ms on this test run. The fixture supplies simulated scalar/capture inputs; it measures the managed trace path, not native game time or full live capture cost. Building tests cover ID0/negative/out-of-range, exact1-based boundaries, multiple entries, slot reuse, owner/parent/grid/orientation changes.

Runtime compilation:0 warnings/0 errors. The offline context fixture has3 CS0649 warnings for default-valued fields; these are absent from the runtime assembly. The actual installed NativeX64 backend copied-function and native production-wrapper execution tests remain passing. Full build transcript is retained at `_inspect/EnemyGateBuildingContextAudit/bridge-lean-build.log`. Local/installed DLL hashes match. Version0.1.0 and README unchanged. New controlled game comparison is pending.

## Focused alternate planning diagnosis (2026-10-04)

Latest log analysis:27.9MB/42s with37513 lost records despite zero ID0 errors. Physical raise/lower and47 completed topology builds are observed; own moat work is confirmed and invalidates a terrain-identical A/B interpretation. The strategic root had no calls; actual parameters match player siege planning2D250/2C480.

The expanded115-function full-hash audit and30 actual NativeX64 entry contracts are retained in the audit folder. Added attack-phases/attack-field/attack-candidates/attack-target-region/attack-position probes use real attacker and target-player context. The original25 hooks remain process-rooted. Fixes internal2C5E1 access is disjoint. No SE fork or APIShared interface changes, no behavior patch, no Version/README change.

Managed capture: pending dirty topology calls stay quiet; actual rebuild/physical/planning changes retain synchronous state. Fixed numeric command stamps suppress exact repeats. Unscoped ordinary unit movement and search results become exact counters; bridge-near changed commands and detailed native decisions retain their chain. Unknown contexts are sampled explicitly. Parallel player group identity slots0..199 and candidate header+4 are observed. State snapshots include the actual attacker's distance field, and per-player prior-plan links are chronological only. Important vs background queues have independent4096 capacity, still64 records/2ms per render; completeness/delivery and interval-cost markers are explicit. Fresh planning status tells the user what is still absent before quitting.

Acceptance protocol is down/up/down from the same pre-attack save within one process, with no own moat work and both Gate/Fixes enabled. No automatic in-game test or completed functional comparison is claimed. Use ACCEPTANCE.md and new markers to decide which optional role/route cases are still missing.

## Validation and installation result (2026-10-04 19:32)

All24241 assertions passed:30 copied entry contracts using the installed NativeX64 Absolute backend; real production R2/V2/V3 wrappers preserving arguments/returns and one original call under capture failure;75117 quiet attacks;834632 ordinary checks plus5082 topology calls/47 rebuilds and18835 counted commands;18835 actual diagnostic Push/Pop pairs; priority reservation, full64-bit command counters, detailed region deduplication, session replacement and unavailable native pointer guards.

Mixed trace emitted61062 bytes with96 full captures and zero queue drops (offline population, not an in-game measurement). Installed member/signature, JSON/lifecycle, XAML, permanent-runtime and CRLF checks passed. The prescribed elevated build.bat ran once, built/installed successfully with0 warnings/0 errors. Local and installed DLL SHA256 both9A5781B7F4950792E096BEE275264E50AEC6D1631C2F379ED1B445530B866E58. AssemblyVersion0.1.0.0 and mod Version0.1.0 remain unchanged; README untouched. Retained build output:_inspect/EnemyGateBuildingContextAudit/bridge-focused-build.log.

Game acceptance remains pending: compare down/up/down from the same prepared pre-attack save, without own moat work, within one process. Verify30 installed entries, post-cleanup runtime marker, fresh planning markers, zero failures/drops and drained output before closing. No final bridge decision fix or achieved in-game FPS/log budget is claimed by the offline tests.

## Keep-access decision milestone

Implemented passive CF360/CF400 observation with exact attacker/target keep inputs, effective return and entry-data branch reconstruction. Expanded full-hash audit122 functions/32 Absolute contracts. The existing30 entries remain permanently rooted; no public API/fork/behavior changes. Public members unchanged.

Physical bridge and per-player planning fields have separate definitions; candidate tables share numeric versions and emit row deltas. Counter tuples are batched32 per row group. Per-bridge topology/fresh-plan status and unique reservation -> unit assignment -> same-consumer movement observations distinguish actual evidence from chronological guesses. Standalone or ambiguous selection and absent movement are explicit gaps. The latest fluent100-second run provides a31/1/31 target-keep comparison for player8 but differing phases; the next test must reload the same pre-attack save raised/down/raised. Version0.1.0 and README unchanged.

## Verification and installation (2026-10-04 20:31)

The prescribed elevated build.bat ran once and installed successfully with0 warnings/0 errors. All24300 Bridge assertions passed, including32 copied Absolute contracts, actual production L3 argument/full-return/one-original execution under capture failures, latest1661352 attacks+10518 topology calls(95 actual builds)+35588 command pairs, reservation ambiguity, shared table versions/deltas, bridge identity reuse and unrelated topology revision. Existing3830 Gate assertions and actual RedBird adapters also passed. Installed member, JSON/lifecycle/permanent hook, XAML/CRLF checks passed. Local/installed DLL SHA256: 34114A4CBAF78C710616C05A003208CB44FE56135AD84DBBF5E45B223F304494. Version0.1.0 remains unchanged. No README modification.

Offline previous mixed fixture:839714 native calls,96 captures,58109 trace bytes; this is not a game log-volume measurement. The32-entry game run and controlled raised/down/raised decision/command comparison remain pending. Retained build log: _inspect/EnemyGateBuildingContextAudit/bridge-access-build.log.

## Numeric-capture comparison protocol (2026-10-04)

The20:35..20:37 run proves player8 access-driven phase5->6 and6->5, but assignment capture failed and104 records remained undelivered. The repaired build must show zero captureFailures/overflow/invalidIds;32 installed entries; post-startup runtime marker. Inspect task-assignment aiState separately from command and nativeAssignedTaskRaw. Missing movement and reused GlobalID remain explicit gaps.26 older selected tasks do not touch a observed bridge; do not treat them as bridge execution.

Physical cells now use tile/pcl/moat/flags/direction: first grid*grid rows footprint, remaining rows ring; bounds preserve the observed rectangle. Unchanged references retain original definition captureClock while the decision has its current Clock. Candidate-table-reference-batch rows are parent/rva/slot/definition/plan/phase/count (RVA decimal). Table definitions/deltas are unchanged. interval-capture-cost separates numeric reads, compares, formatting and counts; interval outputBytes excludes logger prefixes, so measure actual log bytes for1MB/min acceptance.

Within one game process, reload the SAME prepared pre-attack save for raised -> lowered -> raised. Gate/Fixes stay enabled; no own ongoing moat work. For each arm wait for settled topology, fresh attacker planning, observed access and actual consumed phase, then reservation/assignment/movement or an explicit missing evidence marker. Check both lateral deck crossing and gatehouse passage. Old retained tasks and geometric proximity do not prove a new bridge route. Before leaving the map, wait for pending=0 where possible. Immediate session-end always reports captureComplete,deliveryComplete and pending priority counts; a pending tail at process exit remains incomplete. No unbounded drain is introduced.

Offline regressions exercise the real public task getter/AIState test, per-unit exception isolation, reused identities, numeric repeated captures, exact table repeats, current hot-load subset and the observed player8 unequal-PCL mode/phase sequence. In-game performance and complete bridge-relevant following decision still need a new run; no behavior fix, version change or README update is part of this milestone.
## Final verification and installation (2026-10-04 21:14)

Final prescribed elevated build.bat completed with0 warnings/0 errors and24316 Bridge assertions. Existing3830 Gate assertions passed separately. Runtime/project JSON/lifecycle/XAML/permanent-patch checks, real installed members/signatures, CRLF and C# semantic analysis passed. No Script Extender fork changes, public API additions, new native hooks, version changes or README edits.

The exact current native-call population1559559 (1548253 ordinary attacks,9704 topology calls including84 actual rebuilds,1602 less frequent calls) and32410 command pairs is replayed with balanced counters and zero capture failures/overflows. Its offline trace is283157 bytes including a conservative95-byte-per-line logger-prefix allowance. This is a synthetic trace-population regression with fixture field reads, not an in-game volume or latency claim.10000 unchanged numeric fragment publications take2.840ms and emit no extra definitions; this measures comparison/publication, not installed tile reads. The real public assignment getter/state check, per-unit capture exception/reused identity handling, logger failure at immediate session-end and ended-session deactivation are tested.32 copied entry contracts and real production wrapper forwarding preserve the installed Absolute backend's original-once contract.

Final local and installed DLL SHA256 both9FFD2DC85155E2A60DBDD6754BAF9348B6F4B7802E10FD49E3B3734CE0E92045; AssemblyVersion0.1.0.0 and mod0.1.0 unchanged. First successful build log bridge-numeric-build.log is retained; final log bridge-numeric-isolation-build.log covers the later bounded-summary exception guard and complete population replay. In-game budget below1MB/minute, actual capture latency and a complete bridge-relevant following decision remain pending the controlled raised/lowered/raised save comparison.