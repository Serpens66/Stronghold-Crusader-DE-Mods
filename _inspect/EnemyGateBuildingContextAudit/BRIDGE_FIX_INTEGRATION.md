# Drawbridge decision fix: integration contract, not activated

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

The captured keep op670225 and group op828296 now have a production no-cut witness
and no route after removing only703. This does not authorize a CF-only patch.

| Decision boundary | Actual inputs / output and side effects | Existing owner / required continuation |
| --- | --- | --- |
| CF360 / CF400 | Player-root pointer, attacker and target player; native Int64 boolean. Inactive keep branches return1. Nonzero equal PCL bypasses E2610. Otherwise native query is target -> attacker, mode0/1. | Bridge permanent L3 wrappers. Run original exactly once. Preserve inactive/zero/Unknown branches. A later authorized NoRoute can narrow a positive result before its caller consumes it. No new E2610 detour. |
| 2D250 -> D95E0 | Attack player selects stored target; CF360 supplies expansion boolean. Seed arguments are TARGET player, access boolean, extra observed argument1; target player is not requester. D95E0 temporarily closes/restores gates and mutates seed/queue grids. | Bridge permanent V3 / V4 wrappers. Keep attacker in the parent context; preserve native close/restore and all scratch side effects. A valid early fix must rebuild the seed flood on copied virtual transitions and reproduce its output contract, not just remove final target cells. |
| D9190 / 2C5A0 -> 2C480 | Distance builder consumes seeds, count/limit/region and actual attack-player output bank. Target-region globals and weights influence candidate formation. | Bridge permanent V5 / V3 / V2 wrappers. Preserve sentinel/cost/tie-break and candidate/reservation semantics. Reachable alternatives must retain valid candidate ranking. Copied snapshots currently do not include the whole decision-time distance/weight contract. |
| Phase6 -> 3B450 -> 11B520 | Stored attacker/target decision state supplies tribe and formation target. Leader Unit-ID is1-based. Equal start/target PCL bypasses region search; command then changes tribe/unit states, formation and plans. | 11B520 is the Script Extender tribe MoveHere detour/event publisher; no competing function hook. Pre event has SkipOriginalFunction and ReturnValue, Post follows original. A command-only veto is insufficient: planning must already exclude the invalid formation choice. Preserve callers' side effects and return contract. |
| Per-unit builders / next-tile checks | Unit-specific profiles, registered macro access and effective Gate masks remain authoritative. Existing stored paths may predate a new decision. | Existing Gate/SE search owners and event observation only. Do not install a second builder hook or block movement of a previously accepted path as the primary fix. |

Implementation prerequisite: prepare immutable per-player virtual topology and
endpoint mappings after completed rebuild. Cache by full content, identity, roles,
Gate policy and mode; same native PCL never substitutes for tile reachability.
Return Reachable for a fully known witness, NoRoute only for exhausted complete
admissible topology, otherwise Unknown and preserve Vanilla. No search per attack.
A bounded synchronous lookup may use previously completed cached answers; a queued
answer cannot retrospectively authorize rewriting a caller's decision.

For early seed/distance integration, a Boolean oracle alone cannot reproduce
Vanilla planning weights. Complete and verify the copied seed/distance output
contract before activating the already owned wrappers. This is an explicitly
unmet implementation prerequisite, not an invented approved hook point. Matching
CF results and the saved paths do not prove all candidate ranking semantics.

Coupling and permissions: use native spatial selection, not r_GatehouseId or AIV
frame order. Owner/ally/rightful-capturer behavior follows the Gate policy. Retain
alternative terrain and macro routes and first-pass/structure-required distinction.
Unsupported permissions, geometry, stale inputs or budget exhaustion remain Unknown.

Regression acceptance before activation: both original immutable fixtures; an
alternative route surviving only703; class3/4 C and unknown classes; equal native
regions; both directed query orders and modes; capture/role/identity/map changes;
actual original-once forwarding and candidate/formation side effects. Physical
raised-state equivalence and remaining Gate unit-table coverage are separate gates.


## Shared drawbridge coupling (2026-10-07)

At the user's explicit request, APIShared now exposes GatehouseDrawbridgeCoupling and GatehouseFootprintCandidate. BuildOrderedFootprintCandidates reproduces B9330 order; CollectFirstDistinctBuildingIds selects at most two different positive Game-IDs using the caller's live drawbridge predicate. Callers validate footprint/map bounds and identities. No ownership, access permission, parent ID from r_GatehouseId, cache or hook is inferred by this pure API. BugfixesAndQoL's existing approach policy delegates to this shared core; EnemyBridgePathTest calls it directly. Offline projects source-link the same API implementation. CastlePlanner's AIV geometry remains separate. This explicit request supersedes the earlier restriction on adding a public API for this helper; the behavior fix remains disabled.

## Offline planning prerequisite update (2026-10-07)

The dormant production seed/distance core now passes69 complete native differential
cases, including48 actual-raised planning cases; physical direction/PCL equivalence
passes144 cases. Details and limitations: `../BridgePlanningTests/RESULTS.md`.
The previous *unverified copied output algorithm* prerequisite is satisfied for
these audited synthetic inputs. Historical decision-time inputs are still absent
from the two old artifacts; no historical candidate reproduction is claimed.

Raised-state planning must use transformed flags/edges and correctly rebuilt PCL,
not remove deck cells from every weight/distance expansion. Special107160 fields
may survive the raised flag. D95E0's target resolution, R8 queue cap and temporary
gate roundtrip are independently tested. D9190's region callback must use immutable
prepared component answers; no additional Vanilla query is permissible.

The internal90/540 table-copy adapter is prepared and tested but remains dormant.
Installed2.13 still uses89 for API row stride. Effective capturer authorization
must remain the Gate mod's rightful-capturer rule, not Vanilla's any-capturer test.
No live wrapper, original call, output, executable page or public API changed.

## Copied topology and decision-time preparation (2026-10-07)

Current full native hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The private reference manifest now retains 30 complete bodies (including 2D250,
2C5A0 and CF360), plus the two previously audited table readers. The unchanged
122-function decision audit and 42-function topology audit remain the provenance.

VirtualTopologyRebuild is a resumable E49D0 flood over copied, temporarily gate-
closed inputs. It preserves scan order, native packed row ends, cardinal/diagonal
transition order, special107160 seed admission, 999-component limit, full counts
and total. Missing special input, invalid transitions and the component limit
prevent a negative proof. VirtualRaisedPlanning publishes flags, edges, labels
and counts together only after a proven completion; macro endpoints are concrete
fields and therefore read labels from that same grid, including verified C fields.
VirtualPlanningBuildings reconstructs C4BF0's live types45..47 and D8CE0's forced
state2 aperture closure from copied native records, retaining separate type and
blocking-byte inputs. No physical grid or original path is modified.

VirtualPlanningCaller reproduces 2D250 target/bank resolution and threshold90/120
(radius50/70/98), cleared seeds, CF360 seed cap and D9190 parameters. 2C5A0's five
outputs are separately compared. The private Fixes-equivalent 2C5E1 replacement
uses an explicit current count table and preserves its continuation. A separate
installed NativeX64-backend test validates Bridge's entry on a copied body with
that interior jump. This preserves Fixes1.25.0's interior ownership; no competing
hook is installed. Synthetic effective counts are not historical Fixes values.

101 planning cases, 144 native anhebefunction cases, special seeds, alternating
400/402 packed rows and the 999-component boundary compare productive kernels
against native private-memory execution. Whole grids/count arrays are checked.
The passive collector and schema2 adapter replay captured native seed and distance
output. Original calls are performed once outside the observer; capture failures,
nesting, identity changes and map end are isolated and explicitly marked.

The first eligible fresh player8 2D250 family is copied before/after its D95E0 and
D9190 children. It records real players/arguments, future and selected bank, seeds,
visits, queues, controls, candidates, directions, row records, coarse records,
building records/IDs, castle scalars and current hash-bound90/540 tables. Stable
roles/building identities and physical inputs are checked again before sealing.
Exact byte repeats reference prior definitions. The schema2 planning artifact
contains the historical keep input; a matching bridge group references its native
military ancestor, never a merely chronological planning association. At most two
artifacts/session, one64KiB block/render,64 records/render and target2ms are retained.
No permanent callback or new hook/public API is added; Trace and its render publisher
root the collector across startup cleanup and bounded post-map delivery.

Schema1 remains readable; missing historical planning arrays are Unknown. Both
original artifacts retain their hashes and uncut Reachable/single703 NoRoute/all-
hostile NoRoute results in both directions/modes. The complete decision-time arrays
were absent from those original captures, so historical candidate reproduction
is still not claimed. First-family copy cost is measured offline, not a gameplay
performance guarantee; unchanged background callbacks make no additional full
copies. The behavior fix remains disabled, APIShared0.4.10 and Bridge0.1.0 unchanged.
The final group association also accepts the exact retained DecisionState.Root
when a later Phase6 frame differs from the original planning ancestor. Both frame
IDs are preserved; an unrelated retained decision cannot claim the bundle. Two
additional source-root regressions pass. Live capture requires a matching military
parent/player before making any full copy; its absence is separately marked.
The corrected prescribed build passed and installed the verified package.

## Schema2 disk replay and real-capture readiness (2026-10-07)

The productive binary importer validates magic/schema, exact section extents,
payload length/SHA256, unique definitions, references, installed provenance,
source session, actual distance bank and target-selection frame identities.
Schema1 remains readable; it supplies no invented planning state. Both frozen
original artifact hashes remain unchanged. Truncated, partial, modified and
validly hashed but wrongly associated selection artifacts are rejected.

A written synthetic Schema2 fixture now feeds the productive importer, copied
geometry adapter, native temporary-closure oracle and planning kernels. Entire
seed/weight, distance, visit and queue arrays, control blocks and candidate order
match the recorded native outputs. Missing or differing unmodelled controls block
baseline proof. This is synthetic evidence, not a historical real-plan replay.

C4BF0(1) disables the specific connection ID of each live class45..47 gate;
restoration is distinct. The collector copies native alliance IDs, connection IDs
and per-stage macro records. Native E2610 eligibility retains its any-nonzero
capturer semantics; this is explicitly separate from effective Gate authorization.
Unknown endpoints or native local-capacity excess leave the oracle unknown.
Six native gate orientation/state fixtures check temporary and restored results.

The existing 2C5A0 observer records its actual attacker/target, selected tile,
target castle tile, five post outputs and the separately read native tile count.
No earlier global value is attributed to this invocation. The bundle is sealed
for publication after its original military root finishes; target frames cannot
mutate an already queued artifact. At most two capture attempts and one complete
planning plus one group artifact are allowed per session. A failed artifact
association permits the second attempt. A readiness marker names prerequisites.

Already observed bridge groups are retained until bundle selection. Group
artifacts freeze their definition, session and current/retained source root before
lazy output, so map changes cannot rewrite those references. A regression covers
a later military frame linked through its exact retained decision root and output
after map change. Publisher limits remain 64 records or one64KiB block/render,
with the existing target2ms budget and delivery markers.

The behavior fix remains disabled. APIShared0.4.10, Bridge0.1.0, all public APIs,
32 hook owners, Script Extender fork and README files remain unchanged. One real
player8 bundle is still required: permanently lowered bridge, closed gatehouse,
Gate/Fixes/manual bridge feature active, no own moat work. Keep the process open
after map end until session-delivered and both artifact completion messages.
No further general raised comparison is requested. Real uncut replay must match
before only703/all-ineligible virtual variants can authorize the decision fix.
Latest prescribed elevated build: PASS, zero warnings/errors; package equals installed DLL.
Bridge SHA256: FF2C12906037A5A9EF9BF9FE5944C3578ADBE4538249C615EBAA2354B7512306.
Checks:9516 private Native assertions,101 planning cases,144 raised cases,38088 Bridge
assertions,3840 Gate assertions and eight frozen regressions. APIShared0.4.10 hash
unchanged. Native fixture copy40.760ms is offline-only; actual gameplay costs remain
to be measured. The full1000-slot candidate table now preserves/checks both columns.
Offline tool: EnemyBridgePathTest.PolicyTests.exe --planning-replay <planning.bin>;
--planning-group-check <planning.bin> <group.bin> checks exact source identity only.
No game started; no behavior fix enabled.

## 2026-10-07: Dirty decision capture is independent of shadow publication

Frozen process:07.10.14:11:53-14:13:14. Whole appended log SHA256
D1472AF541E187C9ECDA96AAC9228ECDDB79426A1FD463FCBE7CBF60279992AC;
byte-exact final process SHA256
6CDB31CAAE0C7BF454ED2B25F7C1455E4FCE75E86DABD7EC031808ADDE660F51.
1,673,891 completed native calls;34,009 paired commands;97 coherent rebuild
captures;119 completed shadow queries;100 rejected and21 cancelled. Delivery
is complete, selected computation coverage is not. Four player8 plans saw Dirty1
and were previously skipped by our own collector. Native2D250 does not rebuild
or reject its cached component inputs merely because Dirty1 is set.

The collector now owns a session/map identity independently of the invalidatable
shadow publication. It copies the actual decision-time native fields even Dirty1,
records Dirty/native revision/copy time, validates identity, thread, bank, tables,
physical arrays and buildings, and rejects real input/revision/Dirty transitions.
Capture completeness, pending topology and negative-policy eligibility are
separate: negative policy is always disabled. No cached shadow fields replace
missing actual inputs. The existing copy owner supplies a separate immutable
physical/role/connection input for this bundle, without publishing it as coherent
negative topology or changing live fields. Original calls and32 hooks are unchanged.

The selected real target is the first suitable fresh player8 entry phase4.
Phases0/2/3 are preparation and do not consume the maximum two attempts.
This is essential in the frozen process: only root667836 consumes phase4->6;
the earlier three roots consume0->2,2->3,3->4 and must not occupy the only
planning artifact. Observed native/effective keep responses and actual consumed
military state are stored alongside the existing target selection records.
Target recording errors reject the bundle and permit the second eligible attempt.

Planning publication uses its own copied inputs, not keep-access's potentially
missing shadow input. Later groups still require the exact current or retained
decision origin. If command-Pre shadow data were absent, the first matching
bridge group takes a fresh copy at stored-path observation; metadata explicitly
says this is later evidence, never fabricated command-Pre data. Retained actual
XY arguments are mapped against that own copy; no old plan snapshot supplies
group physics or roles. Dirty group copies are artifact evidence only and do not
run six expensive virtual variants. Critical keep requests precede group work.

Command transport uses exact numeric envelopes and reusable frozen payload
and context definitions; searches preserve nullable native/signed effective
results, full64 call counts, event and native parent IDs in bounded numeric rows.
The analyzer reconstructs both, rejects missing definitions and broken rows.
Both command and search partial batches flush before map-end delivery. Existing
64-record/target2ms render and one64KiB artifact block limits remain unchanged.
Input-copy, coherent-copy, compute, format and output costs are separately named.

Offline verification:103 native planning cases,144 native raised cases,
10,068 private-image assertions; productive capture/write/import/replay passes
Dirty0 and Dirty1 with full fields, controls and both candidate columns. Map,
revision and Dirty changes, discarded shadow publication, nested frames,
exceptions, two attempts, missing target data and bounded post-map delivery are
covered.38,092 Bridge assertions pass, including own Dirty1 artifact publication
without a coherent shadow snapshot,100 rejections and21 explicit cancellations.
Nine frozen log regressions pass. Unchanged workload fixtures remain below1MB
including prefix allowances. The latest command-transport-only replay changes
7,128,970 to6,695,933 bytes; this is only6.1percent and excludes the new search
serialization saving. It does not establish the full real-run volume target or
in-game timing. Large active bursts and computation cost remain measured limits.

APIShared0.4.10, versions, README, public APIs and Script Extender fork are
unchanged. Behavior remains Vanilla. After the prescribed build, one focused
real run is required: bridge permanently lowered, coupled gate closed, Gate/Fixes
and manual bridge feature active, no own moat work. Keep the process open after
map end until session-delivered and both artifact completion messages. Replay
uncut real planning first; only then assess only703/all-hostile variants, with
pending topology explicitly accounted for. No general raised counterrun needed.

Prescribed elevated build07.10.2026 14:43:52:PASS; installed/package SHA256 DF3C2C0F32B791A43E1E19EB6042D3233DA5EDE5A696B1AFB492B35D94AADA7E.
Runtime:0 errors,1 CS0649 warning in shared TemporaryCountsOnly (not modified here).
APIShared0.4.10 SHA256 unchanged; no game was started.

## 2026-10-07: real continuous planning and conditional raised replay

Frozen regression: `_inspect/EnemyGateBuildingContextAudit/real-plan-20261007-151016`.
The full archived Log_264 SHA256 is 598639F9EB2DBB7FEB7D9705AE2CFA392BE447556A1E277404B3623F5991D3CB.
Planning SHA256 48EB96C1902815BC062B4C87777679A49FFCF6B38997AEA3F72FE5BCA373A552;
linked group SHA256 6E0437D565FCD4CA77D4F1CD1A56DA04F465EE3103D46FD3958893390C0BB461.
Later LogOutput processes are excluded. Original binary/log files are byte-exact;
manifest and productive replay reports retain full hashes and source paths.

Full installed native audit remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
122 decision/path bodies,42 topology bodies,30 private reference bodies plus2
native table readers. Installed members/backend, runtime JSON/lifecycle and
permanent publisher/hook checks passed. Runtime work remains rooted by static
Application.onBeforeRender and permanent native/event owners after startup cleanup.
No new hook, public API, game-member access, JSON dependency or executable mutation.

The productive baseline now starts at caller-pre, validates CF360-derived R8,
its mode0 scratch controls C0/C4 and target-bank selection, clears seed state as
2D250 does, then carries its calculated Seed output through Distance and caller
completion. It never reloads recorded seed-post/distance-pre as working state.
Full field/queue/visit/controls/candidate-column comparisons remain. Tests separate
shared immutable section references before corruption checks, so mutating one
input cannot accidentally mutate its expected Post evidence too. Private capture
fixtures were corrected to put 2D250's bank write BEFORE D95E0, matching native order.

Real uncut replay matches the observed fields and 2C5A0 projection. Native count0
is kept separate from effective Fixes count132415. The copied PCL histogram and
zero-cut rebuilt count both confirm132415. The canonical Fixes1.25.0 owner at
2C5E1 reads its extended table; it is not replaced. Supported virtual labels remain
bounded to999; unsupported extended labels return unknown rather than being guessed.

Topology states are now explicit: captured physical directions; physical raised
overlay; E49D0's C4BF0(1) closure for the global flood; restored physical publication;
then an independent temporary closure for planning. The captured physical map
supplies special107160 data. Active macro-subject globals and coupling identities
are validated; A/B use native offsets36/48 and are mapped through concrete tiles.
Unknown C preserves independently known A/B witnesses, but cannot prove a negative.
Coupling comes from APIShared; bridge/gate owner, allied owner, capturer and allied
capturer permission remain separate. No native/default permission table is invented.
Native D9190 permission semantics are explicitly not effective Gate unit permission.

Controls on the same historical inputs:
- Original: CF Reachable, seedShortLimit=True, origin103/target1, seed/distance changes0/0.
- Global rebuild WITHOUT deck cut: CF Reachable; seed/distance changes0/0 and count132415.
  However one tile is added and one removed. The first removed tile123127 (326,350)
  has flags0000B000, old/new PCL1/0. No split or merge is observed. Therefore Dirty1
  is not silently promoted to a current, complete negative topology publication.
- Only703 (15 fields): CF NoRoute, R8 True->False, origin105/target31;
  4000 changed seed fields,34878 changed distance fields; observed-target count116982.
- All unallowed confirmed coupled bridges (45 fields): same first R8 change and
  same field-change counts; origin107/target31, observed-target count116964.

These are conditional calculations on captured physics, NOT live policy results.
Revision43, Dirty1, planning clock788991761, physical capture789011290 and query
observation789073802 are retained. Group input independently retains native
revision46/Dirty0 and its actual capture timing. Legacy artifacts missing validity
metadata remain readable but their cut assessment is explicitly blocked.

Important integration limit: this real 2D250 uses mode0/candidateDistance0. D9190
preserves the434 prior candidate entries, including both table columns. 2C5A0
projects the stored selection225464; it does not select a new target. Therefore
reports do NOT call the unchanged candidate table a fresh candidate choice or
invent a new virtual target/phase. First changed planning parameter is proven;
a fully replayed subsequent target consumer and pending update semantics remain
release gates. A future decision patch must use the same valid virtual publication
for early fields, castle checks and late formation/equal-PCL decisions. Unknown
preserves Vanilla; successful alternatives stay allowed. No late movement block.

Runtime diagnosis: changing command values are fixed31-column numeric records;
only constant kind/id/global/type identities are defined. Identity comparisons are
numeric before formatting. Original envelopes, parent/event/context, full64 returns,
retained arguments/a6, positions/PCL, search counters, promotion and timing survive.
Duplicated Post aggregate details point to the command envelope and matching Pre.
The reader handles both old12-column frozen traces and new31-column transport;
missing identities/torn rows are rejected. Full-row delivery remains64/target2ms.

Fresh or changed strategic keep decisions are prioritized. Unchanged keeps and
selected-target hypotheses are counted separately without duplicate six-variant
searches. In production, full group comparisons focus on the retained planning
artifact's exact decision root. Other group calculations are marked skipped,
without dropping their route/command evidence. Queue rejection, cancellation,
intentional skips, completed calculation and file delivery remain separate facts.
Groups/alternative modes remain hypotheses; native castle direction stays target
->attacker. Historical immutable inputs are never replaced by a newer observation.
Artifact output stays one64KiB block/render; session-delivered remains unchanged.

Frozen population:1837071 native completions,36747 command pairs,105 topology
copies,239 rejected computations and10 cancellations. Full capture/delivery is not
full calculation coverage. Command-only transport replay reduces Bridge bytes
including prefixes7894389->6453084 (18.3percent):6420 detailed observations through
352 identity definitions. This is OFFLINE serialization, not an in-game timing or
under1MB/min acceptance. Existing unchanged-population volume fixtures pass;
active burst volume and game compute cost remain open measurements.

Behavior fix remains disabled. APIShared stays0.4.10; all versions, README and
Script-Extender fork remain unchanged. No new game start was performed or requested.

## 2026-10-07: bounded consumer acquisition and historical flood reference

The completed Log264 plan lacks the inputs of2C480's downstream consumers.
No new virtual target or full phase replay is claimed. Historical replay explicitly
returns Unknown for missing consumer inputs; captured434 D9190 mode0 candidates
remain inherited. Recorded Post fields are never substituted for full computation.

The private reference now retains41 hash-checked complete bodies (including3C2E0,
2C480,10DF60 and115B10), plus2 readers. Unsupported outbound branches trap; full
3C2E0/2C480 execution is NOT validated merely by including their bodies. Executed
native comparisons validate1126B0's one-based unit target selection and all five
availability consumers:112370/1123E0/112200/112450/112190, signed5999 boundary and
both reservation modes. Productive byte-copy kernels agree on complete buffers.

A hash-bound replay of the original real plan supplies the separately validated
native temporary gate-closure input to E49D0's full component flood. In this narrow
reference the building update list is empty; it does NOT claim replay of live
C5040 building updates. Entire320800 component grid,1000 field-count entries,
component ceiling and total agree with the productive flood. Changes versus the
old Dirty1 publication are exactly:
- tile123127,326/350,flags0000B000,edges0: oldPCL1 ->0.
- tile290885,414/627,flags00008000,edges249: oldPCL0 ->1.
The former cannot seed the ordinary flood and has no incoming flood inclusion;
the latter is included by the copied directions. These differences show disagreement between captured labels and the copied
physical rebuild; they do not authorize a negative live decision. Dirty/revision and
old versus rebuilt labels stay distinct. Policy remains Unknown.

Acquisition uses existing3C2E0,2C480,10DF60 and115B10 observers, no new hooks.
Only the selected player's own phase4 military entry and planning-root consumer
are captured. Boundaries carry session,root,op,parent,actual players,clock,Dirty and
native revision. Capture2C480 Pre/Post and builder/weight Pre/Post; retain own
military exit. Required child stages are checked before artifact publication.

Concrete new inputs at2C480 entry, not replaceable from Log264:
- unit occupancy4C559B0 (320800 ushort),height4DDD350/base-height4E2B870 and
  terrain owner4E79D90 (320800 bytes each),selection mask53AD4B0 and seed field;
- unit manager67E8400: bounded next-ID1..10000,header65C and490-byte records,
  including reserved storage without public ID0 lookups;
- tribe manager7CC6720:2A-byte prefix and4500 raw688-byte slots;
- candidate working span2E76F10..2F91E24,derived from the last eight-player
  candidate table extent (11AF14 bytes),and nine player records starting379AE00;
- actual10DF60 cost branch byte8574B90; current consumer flags/components/edges,
  building-ID grid and bounded live building records.
All are copied immutable byte storage; no large interop structs constructed.
Unchanged definitions reuse exact own bytes. Additive consumer capture version1
uses the existing schema2 artifact; old schemas1/2 remain readable with unknown
consumer coverage. Import checks complete stages,frame association and capacities.
Local unit/availability handoff replay is labelled partial: it is not a computed
candidate-builder/weight or full military counterfactual.

Limits remain two capture attempts,one planning/one linked group artifact,64 log
records and one64KiB artifact block per render with target2ms. Existing tests with
dummy native pointers do not dereference them; actual capture is tested on the
private native image,including file write/import and mismatched-frame rejection.
APIShared0.4.10,versions,README,public APIs,SE fork and permanent32 owners unchanged.

Next acquisition: same prepared save,bridge permanently DOWN and gate CLOSED,
Gate/Fixes/manual bridge feature active,no own ongoing moat work. Capture first
fresh player8 phase4 planning and linked bridge-relevant group. Wait after map end
for session-delivered and both complete artifact/hash markers. Look for
planning-consumer-captured and planning-consumer-coverage with hasConsumer=True
and hasMilitaryEntry=True. No raised counterrun requested. Fix remains disabled.


Final review/build 2026-10-07: the second review added explicit consumer building
count/record and Post player-buffer extent checks. Native capture/write/import
fixtures Dirty0/1 pass including child-frame corruption rejection. Full historical
consumer replay remains Unknown; a successful local handoff is not full-chain proof.
Final prescribed elevated build.bat /nopause completed once, after preflight and
review:110 native differential cases/10095 checks,144 raising cases,38104 Bridge
assertions; existing Gate regression9894 assertions and frozen-log regressions pass.
Only existing Shared CS0649 warning; zero build errors. Package and installed DLL
SHA256 both68CE104F5FCBBCC833B158B3A4C766525034324AE22C39E8674550311856570C.
Installed APIShared0.4.10 unchanged. Behavior fix disabled. No version,README,
public API,SE fork or competing hook changes. Runtime/game cost of the new bounded
consumer acquisition still requires the targeted run; synthetic copy timings are
not game-cost measurements. Build report:consumer-final-build-20261007.log.


Offline consumer closure review 2026-10-07 (17:15:58 real session)

Frozen inputs: real-consumer-20261007-171558/Log_267.log SHA256
71120296E01A098ED7BF45E8DE94A4D36D4C8DADC84238E79FF0051FF782BE2C;
planning.bin 3C5290A9081930B4E0B3D55A537217DA2CDD04A497034403585D29ED059DB7AB;
group.bin CDBB2B69FAEA1895A0FA963597A051D64E6D02ADB4F13E78301F4F142CB607D7.
Process section and sources retained in SHA256.json. No original bytes changed.

Full feature evidence now retains 58 hash-validated private reference bodies and
two readers, plus the existing decision/topology audits. Retaining a body is not
proof that its whole historical call chain has executed. The new productive
115B10 weight kernel and 193C80/193D90 helpers match all output bytes against the
private native reference for both 70 and150 limits. Eight synthetic weight cases
cover signed-distance/score boundaries, inactive targets and unit blockers.
Both real-input weight branches also match native:70 reproduces recorded output;
150 differs by100 bytes. Actual inputs3665F10/3665F28 were not recorded. Matching
Post values does not establish those inputs for a counterfactual.

The full2C480 builder/CF020/military3C2E0 consumer remains Unknown. The fullC5040
Dirty rebuild also remains Unknown:67E6424 building dirty/counter controls and
all3999 native updater slots are missing. Existing empty-updater flood comparisons
do not explain132473->132471 or authorize a negative Dirty decision.

Consumer capture version2 adds the exact opaque mode values at weight Pre/Post,
full updater slots/controls, packed coordinate validity, effective combat and
building-class tables, and consumer Pre/Post work controls/queues/coordinates/
visits/distances/targets. These are passive own-frame copies via the existing
hash/range-checked reader. No hooks, game writes or additional searches added.
Importer validates extents, stable mode inputs and frame/identity association.
Old schema1/2 and consumer-version1 artifacts remain readable; absent inputs are
explicitly Unknown. inputs-present is not full replay or fix authorization.

Private producer/write/import/replay cases pass Dirty0/1, including corrupt mode
and wrong-frame rejection. Native suite:118 differential cases/10229 checks,
144 raising cases. Bridge suite:38109 assertions, including3776002-call/66689-
command population and bounded quiet output. Costs measured offline, not in-game.

Constant stored-route contract fields now have one route-format definition per
session. Numeric observations, paths, bindings and full decision chains remain.
Frozen transport model preserves reconstruction, movement counts, definitions
and chains; saves470140 bytes from11282251, yielding10812111. This changing-run
model is not the under1MB/min quiet-operation proof or an in-game measurement.

Installed SE2.13.1/native/backend contracts remain valid. Canonical Fixes1.25.1
preserves2C5E1 counter hook;10F150 removes discarded-EAX push/pop, so later planning
must retain the effective owner distinction. Permissions remain separate from
spatial APIShared coupling; negative policy and behavior fix remain disabled.
No version, README or Script Extender fork changes by this work.

A parallel migration added APIShared.UnitAccess calls in Bridge source. Its source
is included in offline policy tests; installed APIShared currently lacks it.
User chose the other chat to finish and install APIShared first. No Bridge runtime
build/install may proceed until that dependency is available and validated.

Next necessary acquisition after verified Bridge installation: same save, bridge
permanently DOWN, gate CLOSED, Gate/Fixes/manual bridge feature active, no own
ongoing moat work. First fresh player8 phase4 planning and exact linked group;
wait after map end for session-delivered and both complete artifact/hash markers.
One bounded planning plus one group artifact, maximum two attempts. No raised
counterrun. Full candidate/Dirty replay and fix approval follow those actual inputs.

Final independent checks: all11 existing frozen-regression scripts pass, plus
new real-consumer hashes/closure/transport checks and producer v2 artifact tests.
Runtime verify.ps1 passes JSON/lifecycle/publisher/permanent-hook/real-member/CRLF
checks after preserving the parallel UnitAccess migration. Source inclusion in the
offline project is required because it does not reference the runtime APIShared.

The existing Gate test executable was also attempted from its own project folder:
its native RedBird capturer cases pass, but NormalCursorPreviewContractIsExact
fails at the reference-first/filtered-last DB650 source assertion. Gate source is
being changed in parallel; no Gate files were modified to hide this failure.
This run must not be reported as a successful current Gate regression.

No runtime build or installation executed in this work session. The prescribed
Bridge driver remains pending the user-selected APIShared installation by the
other chat; final installed signature/hash and current owner checks must run
before that build. Do not request the acquisition run until installation succeeds.
