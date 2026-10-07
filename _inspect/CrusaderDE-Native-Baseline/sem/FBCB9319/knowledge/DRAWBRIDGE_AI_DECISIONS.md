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

The 18:24:53ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œ18:25:35 session matches 2D250 -> D95E0(target player, CF360 result!=0, 1) -> D9190(110, selected radius or0, attacking keep PCL, attacking player). The strategic10D9F0 had zero calls. The actual common caller is2AE40 ->3C2E0(ctx, playerId), operating1-based player records stride583C, with phase at379D974, target player379D9A8, target tile379D968, targetY379D96C and targetX379D970. Player-resource native origin379AE2C must not be confused with the Extender root pointer's header adjustment. Global88E3D70 is not this invocation's attacker.

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
## 2026-10-06 native installation failure and repair

Frozen bridge-20261006-202236.log:59587638 bytes, SHA256
2265CFEA8D18ED975FAC1C74B4245E4D01B08ADD2060BBEC75BA7C899C41332D.
The20:22:36.457..20:24:41.505 session delivered37258 matching command pairs
and a complete Bridge delivery marker. The final non-Bridge SE line is torn.
There are no Native hooks/calls, coherent captures or shadow queries; balanced0/0
and event captureComplete do not establish Native coverage. Bridge703 lowers,
then all15 deck fields belong to PCL1. Bridge trace704197 bytes is not a valid
full Native diagnostic volume benchmark. AI Build Diagnose Test contributes
55959965 bytes to the59587638-byte file. Duplicate plugins were removed by the
user; only the canonical Bridge DLL remains in the plugin search path.

The copied live-body replay with actual V2 and installed NativeX64 reproduces
attack-phases displacement57 instead of15. Fixes enabled its two inline patches
at3C30F (18 displaced bytes) and3C3D9 (36 bytes). The first stub pointer in the
failed run was0x00007FFDC7716490. Its inline bytes90 64 71 C7 FD 7F 00 00 include
a spurious FS-prefixed JNO when scanned as code: branch from function+0x36 to
function entry. The backend widens its prologue to include this data branch.
This is an ASLR-dependent parsing artifact, not a genuine incoming Vanilla edge.
The canonical full122-function/32-entry and42-function topology audits were
renewed against the unchanged Native and backend hashes before this repair.

Only attack-phases now uses scanLimit15. Its full baseline function has no incoming
interior edges into this straight-line prefix. The trampoline resumes at3C2EF.
Before preparation the entire1753-byte live body must match the immutable load-time
snapshot except the two audited Fixes windows, each exactlyFF25/zero displacement,
a nonzero eight-byte pointer and NOP tail. Unknown modifications fail closed.
This does not relax scheme, displaced count, target, pointer-slot, entry or
trampoline checks. The installed backend test also validates the bounded hook
before/after Enable on a private copy. No game function or Fixes hook is executed
by the reproduction; existing V2/V3 original-call fixtures retain ABI/return tests.

Preparation failure releases only unpublished candidates after checking both the
publicationStarted guard and every hook's uninstalled state. Enable starts the
permanent publication boundary; no failure beyond it rolls back code. Detailed
contract errors report expected/actual fields and phase. Startup logs the loaded
managed DLL path/hash, and the first durable render publisher emits readiness.
Native counters for the current map reset independently of process totals, so
old-map calls cannot establish new-map coverage. The analyzer separates event
capture, Native installation/calls, coherent shadow capture/results and delivery.
Unknown shadow results remain explicit; this repair activates no behavior policy.

Prebuild checks:37895 Bridge assertions,3840 Gate assertions, frozen unavailable,
positive/negative planning, route, topology and lossless transport regressions,
installed snapshot/public members, JSON/lifecycle, permanent hooks and CRLF pass.

Installation20:43:55: the single elevated build.bat completed with zero warnings/errors.
Installed and local DLL SHA256 both 3F24297AFC201783DCC775DBCCBE18B9BE4EF31AA6323C60EBAAE54F18D9E651.
Transcript: _inspect/EnemyGateBuildingContextAudit/bridge-native-repair-build.log.
A new game start is still required to confirm installation in the real process
and obtain the first coherent shadow comparison. Version and README are unchanged.


## 2026-10-06: controlled shadow inputs and offline artifacts

Frozen process: bridge-20261006-204752.log, 4,924,654 bytes, SHA256
463A8978B48BFE188947EA2481432B1938B20B366A75A887280BAC56DAA4C1C3.
It is selected by the BepInEx process boundary, not by the newest appended process.
The regression retains 1,136,667 completed Native calls, 23,246 paired commands,
31 coherent captures and 171 shadow results (74 Reachable, 13 NoRoute, 84 Unknown).
All 171 effective policies remain Unknown. Player8 has 25 units with observed
entry and exit. Query676422/definition96 returns geometric NoRoute after cutting
45 tiles across three bridges, whereas CF returns positive. This is not a validated
negative: query direction differed and no uncut control existed. Capture is complete;
90 queued records and the missing delivery marker make delivery incomplete.

The full122-function decision audit and42-function topology audit were renewed
against the entire installed Native hash. E2610 is a component query: equality
returns the component (including zero), otherwise it expands from destination,
first excluding class1, then with class1. Mode0 excludes class1 entirely; mode2
excludes other classes. Eligibility is active==1, enabled!=0 and native team access
or nonzero building capturer. CF360/CF400 pass target keep component as current,
attacker keep component as destination, modes0/1. The copied component control
reproduces boolean reachability only, not the exact next-component return or
native counters/structure flags. Raw component control and Gate-filtered directed
tile geometry are separate evidence bases. Gate ownership/capture restrictions
remain in the geometric adapter; Gate policy publication is frozen per request.
No new native entry, incoming edge, ABI, backend or hook owner is introduced.

Each request runs six controlled geometric variants on one immutable capture:
uncut/only703/all-hostile in attacker-to-target and target-to-attacker directions.
CF comparison uses its observed mode and direction. Group modes remain hypotheses.
A CF cut interpretation is blocked unless the raw macro boolean and the uncut
native-direction tile result both match the observed CF boolean; a direction's
uncut path must also be reachable. This is conditional diagnostic parity, not
permission to change AI behavior. Unresolved actual endpoints, notably componentC,
never obtain a physical endpoint from an arbitrary PCL anchor. A positive known
alternative remains a witness; unresolved transitions prevent negative proof.
Parent geometry and the Gate permission-table coverage warning remain unresolved.

Every successful E49D0 rebuild now attempts a coherent snapshot without the
one-second throttle. Dirty/revision/identity checks remain. Coordinate arrays are
shared only after comparing all800 native row starts and all320,800 packed row
coordinates; X is exactly tile-rowStart[Y]. Other live topology grids are copied.
The map adopts only these already-owned immutable arrays, avoiding a second copy.
A valid input/policy is retained for its historical request even if publication
changes later. Missing input at the decision, dirty topology, changed identities/
roles/alliances and a policy already stale at capture have distinct markers.
Map end cancels pending calculations explicitly; it does not discard file output.

At most two schema1 binary input artifacts are selected per map: the first valid
player8 keep check and a player8 group proven relevant by stored deck-route evidence.
The latter retains its original Pre input, group identity, planning root, decision
and phase where actually linked; a coalesced source query is separately identified.
The files contain copied grids, signed special records, all consumed connection
members including raw third components, building/unit globals, owner/capturer and
alliance inputs, bridge identities/decks/parent authorization, query parameters and
the complete immutable player-specific Gate direction mask. Unknown input stays
explicit. No path search or native function is called by the producer or replay.

The permanent render publisher writes at most one64-KiB block per render and aims
at2ms including lazy encoding/hashing and IO. This is a soft time target, not a hard
filesystem latency guarantee. Files live under BepInEx/diagnostics/EnemyBridgePathTest/
<UTC capture instance>/, outside the plugin directory removed by build.bat.
Only after footer/payload SHA256, close and rename from .partial to .bin is the full
file hash/completion emitted. Session-delivered waits for pending artifacts. Failed
artifact status and log/file delivery are evaluated independently. Abrupt process
exit cannot finish either queue. Do not analyze .partial files.

Offline validation: _inspect/EnemyGateBuildingContextAudit/bridge-artifact-analysis.py
<absolute .bin path> --replay. It validates schema/extents/footer/hash and invokes
the PolicyTests --replay mode, which reconstructs inputs and uses production
Prepare/Pump rather than a separate graph implementation. Versioned inputs permit
further core tests without restarting the game. Completion records alone do not
prove that an artifact exists intact; the offline decoder verifies its bytes.

Player8 strategic references remain explicit; other repeated shadow references are
batched with exact repeat counts and explicitly summarized intermediate parents.
All Native/event counters and existing relevant route chains remain. The64-record/
2ms log drain and bounded queue remain. Existing quiet-volume regressions are not
a live-volume claim for this new full-shadow build; the previous active trace was
3,276,971 bytes in47.271s. Live capture/compute/output cost and volume still require
the next targeted run. No behavior policy, public API, SE fork, version or README
is changed. Test next with a permanently lowered bridge and closed gatehouse, then
end the map and await both artifact completion and session-delivered before exit.


Build/installation 2026-10-06 21:30:20: the prescribed elevated build.bat ran once successfully, with zero warnings/errors. Bridge assertions: 38012; Gate assertions: 3840; all seven frozen log/transport/topology regressions passed. Installed and local DLL SHA256: 3CBEC1AC3B0FDBF6542B67FEC3DD773D4D2BAEE821CD84C3E99308E2674E44F5. Native SHA256 was rechecked unchanged after installation. Build transcript: _inspect/EnemyGateBuildingContextAudit/bridge-shadow-controls-build.log. Binary fixture integrity and replay were independently checked by the Python decoder and production C# adapter. The real uncut/cut comparison and live costs remain pending the next targeted game run.


## 2026-10-07: independently verified A/B endpoints and offline parity

Frozen process: bridge-20261006-235642.log (5,108,252 bytes), SHA256
A6DD8FB7EE22F4F144F0FA1D82499C7C8DB4E739FCEBE0E8F2FFAA0F40549675.
Map session 2026-10-06 23:56:42.346--23:57:44.170: 982,868 completed
Native calls, 21,456 paired commands, 57 rebuilds/captures. Full Bridge and
file delivery are verified, including session-delivered and two completed inputs.
154 rejected shadow requests and three explicitly cancelled definitions mean
calculation coverage is incomplete despite successful delivery. The analyzer now
separates delivered results, selected calculations and exhaustive shadow coverage.
Player8 has 25 observed entry/exit units, 100 movement observations and 32 distinct
movement command identities; observations and unique commands are not conflated.

Retained original schema1 inputs in bridge-inputs-20261006-235642:
- bridge-1-174.bin: 4,619,815 bytes; SHA256
  C1765553075387D23183D792726DAC98BF1DEA8E6B444C908EE6C8325C0283FC.
  Keep op670225/parent670149, raw native components101/1, CF mode0,
  native order target1 -> attacker101, native boolean1.
- bridge-1-204.bin: 4,619,839 bytes; SHA256
  3E5D974B7684C5F00490E2DE1D0BCBC388BB50A37EFEA7EACD798BF1A83C6121.
  Group op828296, planning root828295, stored decision12, phase6,
  tribe4364/global2416650. Its mode remains a hypothesis. The original process
  links this root/decision with 30 stored-route observations, 26 distinct commands,
  23 units and six groups, not 30 new commands.

Audit renewed: all122 decision functions, all32 full Absolute contracts, all42
physical/virtual topology functions against installed Native SHA256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
No new Native entry/ABI/backend/owner is introduced. E2610 directly tests A/B
before considering C, including symmetric A/B branches in each applicable pass.
Eligibility/mode gates are unchanged: mode0 excludes class1; mode1 first excludes
class1, then permits it. Raw native access permits native-team ownership or nonzero
building capturer. The geometric adapter additionally retains the Gate-mod rule
that owner or capturer must be allied to the actual requesting player. Immutable
Gate direction filters remain distinct from raw native component control. The
89/90 and534/540 permission warning is still not full unit permission coverage.
Fixes1.24/SE2.13 topology changes are already included in the installed input;
no shared owner, Extender source, public interface or executable patch is changed.

The old adapter incorrectly made known A/B endpoints unknown whenever C>0.
Now EndpointsKnown independently validates A/B tiles, matching PCLs and subject
Global IDs. ThirdEndpointUnknown records unresolved C without discarding these
edges. Known routes can prove Reachable; unresolved transitions still prevent
NoRoute. No arbitrary component anchor is used for C. In the retained keep input,
records8/9 (buildings466/472, owner8) connect101/1 with correct endpoint PCLs;
their C100 remains physically unresolved. Gate adjacency is not accepted as a
confirmed bridge-parent assignment.

Production Prepare/Pump replay of BOTH ORIGINAL inputs now yields:
             uncut           only703         all-hostile
forward      Reachable       Unknown         Unknown
reverse      Reachable       Unknown         Unknown
The same results hold in the alternate-mode replay, explicitly marked hypothesis
instead of reusing the observed mode0 CF result as a mode1 observation. Original
keep replay matches both raw macro boolean and native-direction uncut geometry.
This is conditional parity, not permission for a behavioral policy. Effective
policy stays Unknown for every result. Missing actual C endpoints and uncertain
bridge-parent/boundary authorization still block a negative cut interpretation.
The next evidence task is to trace physical C-endpoint production against the
native connection layout using these offline inputs, not request another broad
raised/lowered game comparison or invent anchors.

Cost/coverage changes: ordinary group hypotheses are counted, but group Pre
inputs for every player are retained in a bounded group index and scheduled in both modes only
after stored deck-route evidence. Keep checks and selected-target comparisons
remain scheduled; keep and proven groups are prioritized. Unresolved bindings,
retention evictions, queue rejections and cancelled definitions are explicit.
Historical Pre input/clock and planning/decision context survive promotion. Exact
query definitions are reused with separate current observation references.

All consumed copied grids, special predicates, connection fields, identities,
roles, alliances and deck contracts are compared before sharing a content token.
Tokens contain no map arrays; result keys cannot keep all historical snapshots
alive. At most eight prepared content/player/Gate-publication entries are held.
Prepared maps/connections/decks are shared by both directions and modes; the
permanent shadow runtime serially reuses one private traversal workspace. Cut/
visited state is cleared before reuse. Where no known class1 transition exists,
the second traversal has identical known adjacency and is omitted; uncertainty
still produces Unknown. Class1 second-pass and structure-required semantics remain.

Capture, content comparison, live input validation, preparation, search and result
formatting costs are reported separately. Capture includes content comparison;
compute includes preparation/search/result formatting; formatting scope is result
and control records, so the totals must not be added as independent measurements.
Writer costs remain separate. Existing64-record/2ms and64-KiB/2ms output bounds and
post-map delivery markers are unchanged. Offline quiet-load prefix allowances
are not a measured live-volume claim. Real changed planning/command bursts may
still exceed1MB/min; no general new game run is required for this offline step.

Regression entry: bridge-uncut-regression.py; report bridge-uncut-results.json and
four production replay transcripts retain hashes, directions/modes and outcomes.
Runtime preflight covers all19 compiled sources, installed public members, JSON,
MonoBehaviour callbacks, permanently rooted publishers/hooks, workspace mutation
checks, XAML and CRLF. No new game field is used. Version and README unchanged.

Build/installation 2026-10-07 00:35:12: the prescribed elevated build.bat ran once successfully with zero warnings/errors. Bridge assertions:38041; Gate assertions:3840; eight frozen regressions passed. Installed/local DLL SHA256: 99E58D4687EA4F146380E8A3043686D6A799ADD77995BEF238FA54A858359E61. Transcript: bridge-uncut-build.log. Latest quiet production fixture:982868 calls,21456 command pairs,57 rebuilds,41255 bytes with prefix allowance over61.824s. Original artifact full comparisons took78.478/74.643ms offline; these are not live frame measurements. Uncut baseline parity is established; cut variants remain Unknown and behavior remains inactive. No further game start is requested for this step.

## 2026-10-07: exact third endpoints and native coupling correction

Installed Native SHA256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The existing full 122-function decision audit, 42-function physical topology audit
and 32 Absolute hook contracts were renewed before runtime edits. Eighteen complete
additional/overlapping bodies, class dispatch bytes and data references are retained
in bridge-third-endpoint-evidence.txt. Database omissions are explicit: D7D50,
D7E90, D8040, D81F0 and D83B0 have complete installed bodies but no function rows.
They are not new hooks or inferred signatures.

F4540 dispatches by connection class through table3125D0; class3 -> D7E90,
class4 -> D8040. Their endpoint geometry uses gate origin/size, orientation0
(south entry/north exit) or2 (east entry/west exit). Large size7 uses midpoint3
and separation8; small size5 uses midpoint2 and separation6. Both read C from
PCL(originX+1,originY+1). The pure adapter reconstructs this coordinate from
A/B positions only when the exact signed geometry, packed lookup, C and subject
Global-ID all agree. Unconfirmed classes retain verified A/B and unresolved C.
All11 small gates in each original input have matching C, including own gates
466/472; no arbitrary PCL anchor and no additional native search are used.

The production adapter, not only an independent probe, replays both original
inputs in both directions and both modes: noCut Reachable, only703 NoRoute,
allHostile NoRoute, cuts0/15/45, unknownRecords0. Original artifact hashes are
unchanged. Keep mode0 also matches raw native control and native-direction
geometry. Group modes remain hypotheses. Every effective policy stays Unknown;
this proves the copied-graph separation, not full raised-map/unit authorization.

Correction to earlier parent claims: gate updaters write connection record index
to native building manager+GameID*32C+32E, corresponding to public field2D2
r_GatehouseId. This is not an audited bridge-parent Building-ID. The old Bridge
index, deferred snapshot and shadow authorization incorrectly interpreted it as
one. All three consumers now use confirmed spatial coupling. Raw field values
remain opaque, including the retained schema1 slot name NativeParent.

Coupling source-links BugfixesAndQoL/SynchronizedGatehouseReachabilityPolicy.cs
into the Bridge runtime/tests: B9330's ordered perimeter, exclusion of previously
selected ID, first two distinct eligible live bridges. Native eligible alive=2;
NeedsInit is not silently accepted for this lookup. No owner filter is added to
coupling. Multiple gate sources produce an explicit ambiguous association. Parent
identity/owner/capturer are separate from bridge ownership and permission. Negative
policy authorization remains conservative, including differing parent/bridge owners.
Copied parent identity/roles participate in exact-content cache equality and live
validation. Optional schema1 coupledParents5 records those facts for new captures;
old original captures remain valid and do not acquire invented parent evidence.

Workspace review: BugfixesAndQoL ReachableEnemyGatehouseRuntime already uses this
native perimeter lookup for up to2 synchronized drawbridges and tests exterior
approaches. It does not prove enemy lateral deck exclusion. AIPreplacedBuildingFix
only stores r_GatehouseId in a diagnostic snapshot; the property is not consumed
as a lookup. EnemyGatePathfindingTest logs/fingerprints the value as explicitly
opaque. CastlePlanner BlueprintLayout.TryFindAdjacentGate matches AIV footprint
axis, centered five-tile shared edge and frame/build order for blueprint/render
orientation. It is not a runtime identity or reachability provider. ExtraFeatures
and APIShared gate automation follow C5300 recipient lookup and do not interpret
r_GatehouseId as a bridge parent. No change to those mods is required by this finding.

Runtime preflight: actual public SHCDESE fields XBegin238,
YBegin240 and grid248; public tile API GetTileBuildingId(int)->ushort,
GetTileId(int,int)->int, IsTileInsideMapBounds(int,int)->bool and
IsValidTileId(int)->bool verified against installed assembly. No publicized-only
member was added. Static publishers/rooted native callbacks survive startup
cleanup; JSON/lifecycle/published-hook mutation and CRLF checks pass.

Remaining gates before behavior activation: full raised-boundary equivalence for
all supported orientations/height/special cases, authoritative Gate permission
coverage and synchronous bounded decision-time answers. Queue/offline completion
must not change an already issued command retroactively. Existing89/90,534/540
coverage warning remains an explicit limitation. No physical flags/PCL/path writes,
API additions, new hooks, version or README edits.


## Offline acceptance and installation (2026-10-07)

Production replay of both immutable original artifacts passed in both directions and modes: Reachable uncut, NoRoute after cutting only703 and after all hostile decks. Eleven gate C endpoints per original input match the reconstructed native field. Original hashes are unchanged. Bridge tests passed 38082 assertions; all eight frozen log/control regression scripts passed. APIShared baseline/preset/consumer tests and BugfixesAndQoL policy/native/queue/movement regressions passed through their prescribed drivers. Installed DLLs match workspace package hashes in bridge-coupling-installed-hashes.json. APIShared and Bridge runtime builds had no warnings/errors; BugfixesAndQoL retained the known Mono.Cecil version warning only. Runtime/JSON/lifecycle/public member/permanent hook/XAML/CRLF gates passed. No game was started. Version, README and the Script Extender fork were not changed.

This establishes conditional copied-graph reachability, not full permission or early planning equivalence. Effective policy remains Unknown and behavior unchanged. Early seed/distance output, complete effective Gate permissions and physical raised-state equivalence remain activation prerequisites described in BRIDGE_FIX_INTEGRATION.md. Quiet synthetic load produced 41255 bytes including prefix allowance for 982868 calls and21456 commands; this is not a live volume or frame-cost measurement.

## Offline native reference proof (2026-10-07)

Full current hash unchanged. Complete installed planning/raise/helper bodies are
retained in workspace `_inspect/BridgePlanningTests/native-evidence.txt`, with
full-body hashes in native-contracts.tsv.69 copied planning comparisons and144
actual raise/direction/rebuild fixtures pass; see that folder's RESULTS.md.
D95E0 R8 selects2000/6000 queue cap; R9 is unused. Building blocking byte is
independent of class. D9190 cardinal expansion mask100031 does NOT reject raised
flag40000000; a generic deck-blocking planning flood does not reproduce Vanilla.
107160 special start(kind>=5 except15) can give a component even with40001000.
Use full special inputs, not a flags-only empty-component assumption.

Installed SE2.13.0.0 hash7F7750481B392007CCCAD6D609554FBA138D8ED2781AAC54CCE0B235FC5F39F4
still has enum/storage count89 and erroneous per-class stride89; native stride90
is confirmed by full181E00 body. A dormant copied90/540 adapter is tested;
slot89 coverage does not extend valid game enum values. Native E2610 accepts any
nonzero capturer; effective Gate rules are more restrictive and remain authoritative.
These are private synthetic proofs, not newly observed runtime decisions. No
behavior patch is activated. Old artifacts lack mutable early planning inputs.

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
