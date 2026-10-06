# Drawbridge AI decision audit

Native full SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2

# Feature audit: drawbridge decision chain (2026-10-04)

Native full SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. All 115 retained bodies/xrefs were selected by this full hash and decisive prologues checked against installed CrusaderDE.dll. NativeX64 backend SHA-256: 0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0. Companion evidence/contract files are retained, not disposable scratch.

## Physical chain

739C0 and 69850 establish ordered 5x5 occupancy. Four mapper tables at 2D1A30 + orientation/2*100 select 15 cells. A5E20 animation invokes raise645C0/lower64460 (void(tileManager, buildingId)). Raising requires a moat record69560, sets bit40000000 and record state2 via725A0. Lowering removes that bit/restores record state0 and height0. 725E0 -> D90D0 -> D86F0 rebuilds directions under surface mask4A5014B1; E3B90 invalidates regions. Dirty=pathManager60AD660+6C. E49D0 returns0 for no rebuild or1 after rebuilding, increments +74 revision, resets PCL/connection data (C5040), temporarily closes gates viaC4BF0 while filling from direction grid51890D0, then restores gates. Animation state is not evidence of completed topology.

## Strategic planning and candidate consumption

10C340 ->10B870/10C7C0 ->10D9F0(pointer, mode, slot). Planner resolves active player88E3D70 and raw AI value2E97E94; byte2EA7810 selects177BC-byte player record and owner2EA70DC. It clears seed535EF90 and plan53AD4B0 grids. 10F1F0/10F630 update troops/quotas. D95E0 floods from keep using actual directions/PCL and raised bit weighting; D9190 constructs distance grid5759230. 10F150 writes target region2E97D10/home2E97D14 and component count2E9CA14. 10DF60 scans tiles and fills candidate lists; ordinary unit/building paths can use direct PCL equality, while gate/wall/moat paths include neighboring E2610 queries. Raised moat cells can become work candidates. Hence direction-only late search filtering cannot reproduce the early physical distinction.

115B10 filters/weights candidates;112A00/C80/B90 expand the plan viaE6AE0;1127D0 clears plan cells outside target PCL;112810/D60/EF0 generate approach work. 116000 selects/sorts tribes by action. 1150E0 and1151E0 write state/action+50, epoch+620, index+638, delay+54. Their final delay/spacing parameters are short; 1151E0 third parameter is unused64-bit and forwarded at full width.

117520 ->10AA20 dispatches tribe state. Tactical419 ->113BC0 has an existing Gate owner and is observed through command/search adapters, not a competing detour. States418/41E use113F60/E00 ->EEC80 radius12/30/50 and11B520. Ordinary11A980 may retain target+63C/global+640 and reissue command4 based on remembered positions. New unit candidates require direct PCL equality and fine-coordinate squared distance<250000 before command32; EDF30 radius15 fallback produces37/38, or11B520 patrol movement. A later failed route does not retroactively cancel target choice.

122B40(pointer, tribeId, action) consumes lists per eligible unit, calls selectors110EC0/111060/111330/111620/111960/111AF0/111C00/111D90/111F20, records output (x,y,task tile) at2E76F10 and issues unit commands65/67 plus196280 movement. These can occur without a new group order. Selector return0 means no selected task; nonzero output is read synchronously. Lists are bounded and logged as raw observed rows: membership is not an invented rejection reason.

## Movement and work access

11B520 skips ordinary regions for identical PCLs or Assassin-only117820; otherwise117C70 supplies the fifth mode argument toE2610. Failed regions can reach E9D90/E9FF0 structure/exit checks, not a presumed leader check. E2610 itself also accepts equal regions before gate policy. 11E960 command7 invokes E7F60 if leader PCL nonzero: failure returns early; success chooses a frontier with reachable neighboring region and closest squared Euclidean leader distance, then falls through command6 and nested11B520. Leader PCL0 skips the selector. Output writes and subsequent issued command/context values are separate from actual execution.

## Gate policy and hook ownership

Gate uses validated player context, owner/allied/capturer policy and both early/tactical and late direction/search filters. The bridge test records these roles without publishing a mask. Bridge parent ID can be0; unique same-owner adjacent footprint is only a candidate. Building ID + GlobalId distinguishes reuse. Equal-PCL bridge crossings explain why gate connection filtering alone does not cover the deck.

BugfixesAndQoL owns E2610,F4930,DBC60,DA020,123090,195E30,D9C40 observations; reuse APIShared observer registrations. Gate owns113BC0 and its direction/tactical/capture sites. Mainmod AiWallTargetingFix patches10ECC3..10ECD1 inside10DF60, beyond the diagnostic entry. Canonical local Fixes patches rebuild internalsE4AA3/E4B61/E4DF4 and10F1B3..10F1CE target component logic, beyond entry span10F150..10F164; therefore function outputs with Fixes enabled are effective outputs, not pristine Vanilla. Existing extended component-count behavior must be compared with each active setup. SE attack-evaluation events are diplomatic requests, not the common strategic planner; no suitable event replaces these probes.

## Instrumentation contract and open questions

30 entries are complete instruction spans at least14 bytes, with no known DB xref into their displaced interiors. Full functions and continuation are retained; runtime checks live bytes/backend hash, restricts Absolute, exact length, target, entry/trampoline and pointer slot, then verifies installed patch form. Actual backend copied-byte tests validate the same schema. Ordinary function ABI preserves originals/returns; no context-hook flags assumptions. Published hooks have process lifetime and no teardown.

The passive milestone audit is complete. Still requiring game evidence: earliest divergence in the reproduction; exact internal candidate rejection if list differences are insufficient; ambiguous bridge association; cached target versus fresh decision; topology timing with Fixes; startup event boundary classification; capture cost. No final behavior hook is selected here. Synchronous captures do not claim atomicity across unrelated simulation threads or prove a route uses any observed bridge.

## Reproduction-driven extension: player siege planning

The 18:24:53â€“18:25:35 session matches 2D250 -> D95E0(target player, CF360 result!=0, 1) -> D9190(110, selected radius or0, attacking keep PCL, attacking player). The strategic10D9F0 had zero calls. The actual common caller is2AE40 ->3C2E0(ctx, playerId), operating1-based player records stride583C, with phase at379D974, target player379D9A8, target tile379D968, targetY379D96C and targetX379D970. Player-resource native origin379AE2C must not be confused with the Extender root pointer's header adjustment. Global88E3D70 is not this invocation's attacker.

3C2E0 phases1..9 include preparation, candidate rebuilding, active attack, access reassessment and retreat. 3BD50 resets ctx+EBA0/EBA4 candidate count/rows, invokes2D250(mode1), weights potential positions, writes the retained target tile/coordinates and prepares formation positions. 2C480 clears the plan grid, invokes2C5A0(ctx,attacker,targetPlayer),1126B0,10DF60(ctx,attacker,targetPlayer),115B10(ctx,targetPlayer,attacker), then112370/1123E0/112200/112450/112190(ctx,1,attacker) andCF020(targetPlayer). 2C5A0 takes the selected tile's signed seed and PCL, its component count, and the target player's keep PCL. Fixes replaces component-count loading at2C5E1..2C5EF; the entry2C5A0..2C5B1 is disjoint. Probe returns/global results with internal Fixes patches are effective function outputs, not pristine Vanilla results.

The five availability helpers write table header+4: 112370/112200/112450/112190 count rows with signed weight<5999 and (reservation==0 or forced);1123E0 tests reservation only. Forced=1 means availability counts do not establish which individual unit will later reserve a row. Weight thresholds differ in the terminal selectors; preserve raw observed headers/rows and do not invent rejection branches from absent rows. Selector return/output task/approach tiles are distinct values.

Player group slots are parallel short IDs379F670 and int GlobalIDs379F8C8, each player offset583C. Installed constants2C8040 (11 pairs) and2C80E0 (11 pairs) prove used ranges including192..199; capture slots0..199 with exact GlobalID checks, never a194-slot guessed limit. 2B080 creates groups410;2B340 creates415;3D260 assigns formation movement via11B520. 3CCD0 moves selected support groups. 3B980 consumes3F7 moat tasks only if the moat availability count is positive. 3CDC0 consumes3FB;3BFA0/3C0A0/3B820 consume3F2 or420 depending AIC/global flags. 3FE50 assigns artillery413/414/3F6 from available tables and support links. 3CE40/3C150/3CAC0 use113F60/F2CB0/retained patrol alternatives then11B520. Retained IDs are checked against recorded GlobalIDs before use. Follow into122B40 selectors, unit commands65/67 and196280 movement; these may bypass a new group-order event. Group movement11B520 skips regions for equal PCL or Assassin-only profile, otherwise passes117C70 mode toE2610 before structure/exit fallbacks. 3B450's access/retreat branches may emit movement, set state8 or keep structure targets. CF360/CF400 accept equal nonzero keep regions without E2610 and otherwise call it with mode0/1. These facts explain why absence of a region callback cannot stand for a positive query.

New entry ABIs:3C2E0/2C480/3BD50 void(pointer,int);2D250/2C5A0 void(pointer,int,int). All five entries were decoded against the installed DLL:15/15/15/17/14 bytes respectively. Complete bodies, continuations, database xrefs and independently decoded direct branch targets have no displaced-interior entry. Existing SE/Fixes/main/Gate owners were searched; only the disjoint2C5E1 internal Fixes hook was found. The installed NativeX64 Absolute backend/copy tests remain required; no Context-hook flags assumptions apply.

New hash-bound data: topology countdown37ED4CC (E49D0); player fields above plus lord/AIC379D0D0 and keep tile379AFB0; target seed2E97EA8/mode2E97EAC; signed-short distance grids5759230 + player*320800*2 + tile*2. Native E49D0 decrements countdown, resets it to200, and returns0 while positive or clean; forced calls set dirty and countdown0 before decrement. A persistent dirty bit alone does not mean a rebuild. Signed int wrap must match native arithmetic. These native data offsets have no independent layout fallback and remain full-hash/module-bound.

Database role confidence for newly inspected functions is candidate. Complete byte-checked dataflow establishes the passive observation contract; names are descriptive derived roles, not a claimed public native API. Runtime uncertainty remains: first differing candidate/selection branch, actual route attribution, relationship between retained tasks and earlier plans, and physical changes from concurrent own moat work. No behavior fix is selected.

## Latest log and focused transport

Latest retained source is BepInEx LogOutput.log, process diagnostic window18:24:53.516..18:25:35.291:27,886,887 bridge bytes/85,978 lines;839,753 traced entries/exits,47,576 captures,61 index builds,37,513 drops. Zero errors/invalid IDs; physical calls raise3/lower2 and actual rebuilds47/5082. Own player1 slaves issue command6 at bridge fields, and subsequent down-state fields differ; do not compare those epochs as terrain-identical A/B evidence. Most trace volume was unit commands and dirty-but-delayed topology captures.

Focused capture now counts all commands/searches numerically, keeps changed group orders, bridge-near changed unit orders and orders inside detailed native decisions, and samples unresolved unscoped commands without a full bridge capture. Ordinary unscoped unit movement remains background, regardless of presumed unit role; this is an explicit coverage boundary. Nearby geometry is a relevance hint, never route evidence. Relevant scopes deduplicate region tuples locally; exact region totals include individual detailed observations. Background region rows are packed32 per record. Important definitions/decisions have a reserved4096-record queue, independent of4096 background records. Output prioritizes important records within64/2ms; any overflow/failure invalidates captured completeness, and pending delivery is separately reported. Native-session crossing emits a boundary rather than applying old inputs to a new session's cells.

Markers distinguish physical call completion, actual topology rebuild, fresh candidate completion, and observed following orders. Prior-plan links are per player and explicitly chronological, not proof of causality. Summary marks missing fresh planning and current generation coverage. Internal rejected branches remain unobserved unless separately audited/instrumented; effective output alone must not be relabeled a pristine native result.

Relevant synchronous cell captures also include the one-tile coordinate ring around each cached footprint: PCL, flags, direction, moat, seed/plan and attacker distance. This observes both sides of lateral deck transitions without a native search. The ring is coordinate/module/span validated; geometric relevance remains separate from actual route attribution.

## Keep-access follow-through evidence

Current full hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Full CF360/CF400 bodies accept equal nonzero signed keep PCL without E2610; differing PCL calls use mode0/1 and argument order attacker,targetKeep,attackerKeep. Inactive-player raw fields accept early; zero active keep PCL rejects. Passed manager +player*583C+12EC54/+12EDA0 are active/keep. 3C2E0 phase4 accepts into6 or rejects into5; phase5 accepts either mode into6; phase6 can fall back to5 after3B450 and both failed modes. D95E0 receives CF360 result via2D250. 2C5A0 global2E97D14 refers to TARGET-player keep PCL. Details and122-function provenance: BRIDGE_DECISION_AUDIT.md in the feature audit folder.

Runtime19:38:07..19:39:47 provides player8 retained target232127 and target-keep31/1/31 with bridge703 raised/down/raised, stable15 moat identities and changing candidate tables. This is evidence of correlated topology/planning changes; differing phases and progressing game prevent an isolated causal claim or selected behavior patch. No route attribution is proven.

## 2026-10-04: assignment repair and numeric capture

The20:35:52.943..20:37:31.132 session has1559559 balanced native calls and32410 command pairs, zero queue overflows and zero ID0 lookups. Three capture failures abort consumer-return assignment evidence. The old raw assignment RVA contained an extra leading6; the correct native RVA is67E8E00. Canonical Unit manager67E8400 + sentinel-array offset65C + Game-ID*490 + public task offset3A4 exactly reproduces the installed122B40 write. Unit IDs remain1-based. AIState at2BC maps to native67E8D18; its65/67 assignments are distinct from AI_LastIssuedTribeCommand at398/native67E8DF4. Installed SHCDESE fields are public UInt32/UInt16/UInt16. Runtime now uses these public fields, verifies GlobalID and isolates each selected unit's capture failure. No new native address or hook is introduced.

At20:37:12.687, player8's unequal keep PCLs109/1 invoke mode0 E2610(targetKeep,attackerKeep): native=effective=109; CF360 accepts and enclosing3C2E0 phase5 becomes6. At20:37:18.959, keep PCLs116/31 invoke modes0 and1: native=effective=0; CF360/CF400 reject and enclosing phase6 becomes5. All63 emitted keep-access observations use unequal-region query branches. This proves the consumed access branch/phase difference, not a route through a particular bridge, and does not test equal-PCL bypass. There is one map session, so no save-identical reload experiment. All26 reservation-linked movement events refer to tasks/approaches outside observed bridge footprints/rings. Bridge703/720/828 footprint moat values are unchanged; four player5 work orders are outside bridge proximity. Missing own work markers do not prove absence.

Trace has6863 records/3546622 bytes including prefixes in98.189seconds. Measured full capture time5856.022ms versus538.476ms in the preceding run; text formatting followed by compiled-regex reparsing was present on every capture. All examined definitions resolve, but final summary reports167 queued records with6799 already delivered; only64 subsequent records appear, leaving104 undelivered. Capture and delivery completeness must both be checked.

Production captures now read reusable numeric arrays, compare numeric keys and format only new physical/planning/state definitions. Immutable dictionary keys are copied only for new definitions. Physical cells use columns tile/pcl/moat/flags/direction (flags hex; directions decimal), first grid*grid rows are occupied fields and remaining rows the immediate coordinate ring described by bounds. Invalid tile raw values remain visible without dereference. Planning columns remain tile/seed/field/distance16; missing distance is not-captured. Definition captureClock is original definition observation; referring operation Clock is its current synchronous observation. Separate interval read/compare/format costs and definition counts are emitted. These timers cover bridge fragments and candidate observations, not all wrapper cost; interval outputBytes excludes logger prefixes.

Unchanged candidate-table references are batched32 per record, preserving parent/rva/slot/definition/plan/phase/count. Exact repetitions coalesce before enqueueing. New planning provenance and reservation changes still produce evidence. Native entry contracts, original-once forwarding, public APIs and owners remain unchanged. Session-end is one immediate bounded record with captureComplete/deliveryComplete and critical/background pending; queued records continue through the existing process-rooted render publisher at64 records/2ms target. Immediate process exit can still leave output pending and must never be described as complete.
## Final verification and installation (2026-10-04 21:14)

Final prescribed elevated build.bat completed with0 warnings/0 errors and24316 Bridge assertions. Existing3830 Gate assertions passed separately. Runtime/project JSON/lifecycle/XAML/permanent-patch checks, real installed members/signatures, CRLF and C# semantic analysis passed. No Script Extender fork changes, public API additions, new native hooks, version changes or README edits.

The exact current native-call population1559559 (1548253 ordinary attacks,9704 topology calls including84 actual rebuilds,1602 less frequent calls) and32410 command pairs is replayed with balanced counters and zero capture failures/overflows. Its offline trace is283157 bytes including a conservative95-byte-per-line logger-prefix allowance. This is a synthetic trace-population regression with fixture field reads, not an in-game volume or latency claim.10000 unchanged numeric fragment publications take2.840ms and emit no extra definitions; this measures comparison/publication, not installed tile reads. The real public assignment getter/state check, per-unit capture exception/reused identity handling, logger failure at immediate session-end and ended-session deactivation are tested.32 copied entry contracts and real production wrapper forwarding preserve the installed Absolute backend's original-once contract.

Final local and installed DLL SHA256 both9FFD2DC85155E2A60DBDD6754BAF9348B6F4B7802E10FD49E3B3734CE0E92045; AssemblyVersion0.1.0.0 and mod0.1.0 unchanged. First successful build log bridge-numeric-build.log is retained; final log bridge-numeric-isolation-build.log covers the later bounded-summary exception guard and complete population replay. In-game budget below1MB/minute, actual capture latency and a complete bridge-relevant following decision remain pending the controlled raised/lowered/raised save comparison.
## 2026-10-04 23:22 decision-chain completion and compact background transport

Frozen evidence: _inspect/EnemyGateBuildingContextAudit/bridge-232200.log, SHA256 5AEF0B8E216C0E42E2E5AE63EB26A34AEC440E7762DFE221E7F0CC37CDCAF9D8. 1695766 balanced native calls;34661 command pairs;zero capture/ID/overflow/reference/reconstruction errors. Bridge capture and delivery are complete; the whole file ends in an unrelated partial UU-ImGUI line. Analyzer now separates bridgeFileComplete/bridgeComplete from fileComplete/complete.

Player8 decision28 consumes CF360 positive103/1, native=effective103, phase5->6. Later root1336380 has the identical retained plan and four group orders/25 unit orders/25 units/52 route observations. Path523, unit1143/g2425970, command1336382/group1336381, crosses703/g2432893 at steps64..66,603/477->605/473, without the candidate parent footprint. Group PCL1/1 bypasses regions. Decision31 after raising consumes negative mode0/1,107/31, phase6->5. Total105 observations have matching decisions and recorded root/group/unit commands;10 are unresolved. No executed deck movement and only one map session. Retained-state linkage is established; save-identical counterfactual causality remains pending.

The renewed full122-function/32-entry Absolute audit matches CURRENT.json and the installed complete Native/NativeX64 hashes; existing27-function stored-path audit remains applicable. JSON/lifecycle/project preflight passes. Lifetime remains static subscriptions/native hooks/Application.onBeforeRender; no plugin teardown, new native hooks, public API, game-member access, Vanilla search, behavior write, version or README change. Native originals remain exactly once. No new assembly members require validation; all existing installed public field/view/publisher contracts remain machine-checked.

Complete no-deck path definitions now use stored-route-background-batch rows: definition/captureClock/originX/originY/length/cursor/flags/substep/decodedX/decodedY/segmentX/segmentY. These paths previously had no packed bytes either; relevant or undecodable paths still retain full original definitions and bytes. Envelope metadata belongs to batch flush; physicalAtCapture is explicitly not recorded for background rows. Decoder reconstructs original numeric fields and does not assign flush physical/topology to their capture. Repeated no-deck observations are exact player totals, not per-binding details or movement evidence. Relevant repetitions retain binding/path IDs and cursor/time ranges. Replacements use unit/global/oldCommand/newCommand/observationClock rows. All batches flush across session boundaries. Critical definitions and route evidence retain priority; background repetition totals use the bounded background queue.

Per-bridge decision-comparison and final comparison-coverage markers identify access observation, target, consumed phase, physical definition and settled topology; route-comparison identifies stored-route state. Expired decisions and old-physical routes cannot satisfy the current-physical coverage marker. Missing controlled reload comparison is explicit. 64-record/2ms drain and session-delivered remain unchanged.

Frozen transport replay:4243980->3396576 bytes including prefixes (847404 saved), preserving all definitions and105 linked observations. This is a serialization regression, not proof of runtime volume or performance. Updated load fixture reproduces1695766 calls/34661 command pairs/93 rebuilds; separate unchanged/background-volume fixture includes prefixes. New graph design in Testmods/EnemyBridgePathTest/DECISION_FIX_DESIGN.md specifies virtual split-components with remapped macro endpoints and Gate authorization. It is not installed; native parent0, physical-closed boundary equivalence, special unit/mode semantics and same-save raised->lowered->raised remain prerequisites.
# Stable bridge execution and virtual reachability audit (2026-10-06)

The current full Native hash remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Frozen bridge-20261006-133913.log hash26AD1EC083B8A65835E77C012ADC5E2E463DEE64AF35DC0D7797AF282C79C4FD
proves stable lowered bridge703/g2432893 (one lower/no raises), player8 positive
CF360102/1, consumed phase4->6, retained four group/25 unit commands and20 actually
executed side-deck crossings.91 units from five AI players enter/exit in364
movement observations.535 stored-route observations/511 retained-state links must
not be counted as535 orders or positive access proofs.

Supplemental42 complete function bodies, installed decoding, byte hashes and xrefs:
_inspect/EnemyGateBuildingContextAudit/bridge-virtual-evidence.txt and contracts.json.
107160's seed exception is nonzero signed-short record ID, signed kind>4 and !=15;
only AL is meaningful. E49D0 is ordered packed flood over320800 tiles, not a rectangular
or strongly-connected-components replacement. D86F0 includes height/occupancy and
diagonal corner rules. The49 observed local direction bytes match raised state after
cutting all incident deck transitions; this establishes no full-map NoRoute result.
E2610's three-endpoint macro search excludes type1 for mode0, permits it for mode1;
its second pass admits closed records and signals structure-required at60AD6F8.
Parent native ID0 remains unresolved; geometry is no authorization proof.
The A5E20 rendering lookup B9330 scans C0270 perimeter/type45/alive-state2,
ignores the passed player and writes no parent association.6C3B0's mapped unit
presence controls automatic raising independently of a gate owner's permission.
Installed public permissions are534-entry native prefix with incorrect89 per-class
stride versus native90. Do not use per-class public slices to validate virtual
permissions. See the retained English author report; no Extender fork edit.

One stable raised same-save counterrun remains. No behavior hook/virtual policy is
published. Full-map alternative routes, endpoint remapping and parent authorization
remain Unknown until validated. The latest passive transport uses numeric native
frames and shared command-context definitions with exact offline reconstruction.


## 2026-10-06 raised counterrun and shadow implementation

Frozen bridge-20261006-155247.log has9941535 bytes and SHA256
D0DE38D7BEA5C0FDD41CDCE44D8557106598236930805038F0CCA5FC06098CBC.
The exact byte boundary matters: a new BepInEx header was appended directly to
the torn final Bridge row. Cutting by whole lines loses that fragment and falsely
reports file completeness. Capture is complete:2500812 balanced native calls,
40729 paired commands, no capture/ID/queue/reference failures. At session end53
critical+14 background rows remain; no delivery marker exists. Both delivery and
Bridge file completeness are false, while complete earlier chains remain valid.

Player8 starts from the same entry plan4/0/1/224222/543/489. Lowered uses
D95E0(1,1,1), target225464 and4->6; raised usesD95E0(1,0,1), target232127,
negative modes0/1 and4->5, then remains5. No stored deck route or actual crossing
was captured in the raised run. Early transient openings and the player5 positive
5->6 decision are excluded from the settled player8 comparison. This completes
the requested counterrun; another general raised replay is unnecessary.

Installed SE2.13.0, local85ab962b342c18f663da830570884a25b85116d0, current Native
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, unchanged
NativeX64 backend0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0.
The122-function and42-function audits/32 entries have been renewed. The89/90,
534/540 warning remains; no completeness or new permission claim follows from
this update. Current byte gate-state fields are reconstructed as low|high<<8.

VirtualBridgeMap owns copied packed components, edges, coordinates, flags and
signed107160 special-record inputs. The read-only core follows directed edges,
removes candidate deck vertices, keeps actual macro A/B endpoints and preserves
the two E2610 passes. Important correction: the first-pass comparison at context
+record*0x204+0x2028 is record class+4, not an independent closed flag; class1 is
excluded in mode0 and is a structure-required second-pass connection in mode1.
No live native structure-required flag or visit table is modified.

Runtime capture occurs only in the existing successful E49D0 post, with matching
pre/post dirty/revision and identity checks. It is throttled to at most one capture
per second. Spawn/delete, physical change, capture, ownership/identity/alliance
change, incomplete publication and session replacement invalidate inputs. Queries
compare keep access, completed target choice and formation endpoints. Group/target
mode hypotheses are explicitly marked; they are not observed native group modes.
CF's actual macro query order is target-component to attacker-component, while
the shadow evaluates movement from attacker tile to target tile. Pure managed
jobs run in64-node steps under an intended1ms render budget, separately from the
existing64-record/2ms output budget. Queues are bounded32; rejected and cancelled
jobs and interval capture/compute costs are explicit. No Vanilla search is added.

This is a geometric shadow, not a validated exact replacement for E49D0. Seed
predicate data are copied and tested, but complete closed-state relabelling and
all orientation/height exceptions have not yet been compared on a full map.
ComponentC has no public exact tile field: it can use an unchanged component
anchor, but an affected third component makes a negative result Unknown. Invalid
subject globals do likewise. Geometric NoRoute remains conditional. Cut policies,
unresolved parent authorization, missing Gate snapshot and unobserved group/unit
mode permissions yield policy Unknown. No behavioral result is installed.

The pure core covers alternative routes, directed asymmetry, diagonal packed
coordinates, class/mode/access eligibility, second-pass structure status, exact
third endpoints, unknown inputs and signed seed exceptions. Live full-map parity
will be assessed from shadow output; synthetic grid tests are not that evidence.

Native output uses bounded16-row packets with each original37-field row envelope
and numeric zero runs. Definitions and per-operation metadata remain recoverable.
Both frozen logs round-trip with identical native fields, decision/route/movement
chains and completeness: prefix-inclusive bytes4765869->4339452 (lowered),
7052551->5974835 (raised). Active-run volume still exceeds1MB/min. The separate
unchanged production fixture falls to53139 bytes over113.069s (about0.028MB/min).
This is a fixture measurement, not proof of quiet in-game runtime cost.

## Shadow validation and installation (2026-10-06 16:53)

The final elevated build.bat ran once and completed with zero warnings/errors.
Local and installed EnemyBridgePathTest.dll SHA256 both equal
3353B181AF4D28991637E9829AA6A7FCFEA1BED3469DA1D4F05E6FBDF36C61B6.
The retained transcript is _inspect/EnemyGateBuildingContextAudit/bridge-shadow-build.log.
Bridge tests passed37885 assertions; the Gate compatibility suite passed3840.
Analyzer completeness, both frozen counterruns, lossless native batches, local
boundary and previous decision/route regressions passed. Installed public view,
layout, lifecycle, JSON, CRLF and workspace permanent-hook checks passed.

Every queued shadow query has a unique definition, including two modes of one
operation. Repeated observations reference it; session cancellation lists its
pending definitions. Calculation exceptions deliver Unknown. The analyzer reports
shadow inputs/results/cancelled/pending independently of Bridge delivery and
validates result/reference definitions. No behavior decision has changed.

Remaining acceptance is explicit: no full-map in-game shadow result has yet been
measured. Geometric deck-cut results are hypotheses; a cut policy remains Unknown
until complete closed-state topology, macro endpoint and authorization parity is
proven. The evidence establishes the planning difference but does not yet make
this geometric model an authoritative replacement. Active trace volume remains
above1MB/min; the lower background fixture result does not claim otherwise.
The next focused run can reuse the known save to inspect virtual-shadow output;
another broad raised baseline comparison is unnecessary.