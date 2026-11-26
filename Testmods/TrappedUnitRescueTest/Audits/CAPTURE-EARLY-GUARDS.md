# Capture early-guard audit

Read-only implementation audit,2026-11-26, develop working tree at HEAD333d51ef8. No runtime/script/test edits, build, game, timing experiment or optimization acceptance.139 shows ON tails;145 has no ON data. Neither establishes the cause of those tails or the benefit of any candidate below. Quantities here are code bounds, not measured cost.

## Full capture path and current guards

GeneralRescueObserver.TryBegin rejects inactive/recovering/foreign-session/missing-world calls, checks actual current world, resets changed world, and observes failed-batch overflow before capture. NativeRescueMutationWorld.TryCapture obtains a complete NativeMutationFootprints plan first. Moat nonterminal progress already has constant-cost task identity/owner/progress rejection before Session/terrain/BFS work. Known-empty envelope returns without discovery. Unknown/failed plan remains unknown to historical-geometry retirement, not an empty success.

NativeMutationFootprints validates native manager/source/task/AIV identities, bounded tables/coordinate mappings, dynamic building cleanup/replacement closure, all relevant possible Keep orientations, projected cleanup flags and total2048 tile/131072 work bounds. Owner is source player where defined, actual task owner for dig/fill, actual removed building owner for removal, AIV owner after village validation. Final owner domain is0..8;0 is unfiltered, not local human. No native creator/enumerator is called by the planner. Session's field/table/brush caches are invocation-local. Its recursive building/cleanup relationships and required Keep replacement scan are part of complete write-envelope proof, not unit discovery.

RescueDiscovery validates all changed XY/packed aliases and constructs the clipped one-cell halo using native.Tile (fixed800 domain AND actual active diamond). It reads EVERY halo Current-chain with link/cycle/range/duplicate-consistency checks. ReadDiscoveryUnit already calls native.TryUnit: valid one-based ID1..9999, actual manager lookup and UnitAccess.IsReallyAlive (AliveState.IsAlive plus death-marker0). Its ordinary accepted Next predicate already excludes movement-class/route-permission/hidden/portal/transport/transition modes and requires a neighbouring distinct Next. Selected records must occur in their actual Current chain and satisfy changedCurrent OR ordinary changedNext. Off-halo/unaffected actors never reach snapshot. All chains must succeed before ANY full snapshot/BFS. Limits remain4096 halo cells,16384 links,512 selected units, sorted unit-ID output.

TryCaptureMutation then calls SnapshotUnit(id,true) per selected record. SnapshotUnit builds the scalar/crew view, HasOrdinaryStep, ClassifyState and tribe stamp; full immutable WorkerJourneySnapshot/external vegetation global or phase-specific CommandJourneySnapshot; coordinate/packed coherence and old Current/Next flags. ClassifyState rejects unsupported movement modes, in-flight nonordinary poses, dying/health0, special paths/AI, unsupported role/phase BEFORE candidate BFS. Typed job/command capture can also reject before BFS. BFS happens only if Unsupported==null and Current is ordinary. It is a read-only local radius8 field (at most289 visited cells including origin), using table-derived packed tiles and ordinary edge checks, not y*800+x or a full-map scan.

After that BFS, TryCaptureMutation verifies the original discovery Global/Current and applicable Next coherence, sets AffectedCurrent/Next, inserts Distance0 Current for an ordinary step, and adds snapshots. The outer NativeRescueMutationWorld finally RemoveAlls Unsupported snapshots AND every owner mismatch when source owner>0. General then retires historical geometry using the complete envelope, even on zero occupants/failure, handles failed-first/child batch tombstones and shared capacities, and either merges endpoints into earliest same-batch proof or stores a new snapshot. Only completed successful current batches use post-original HasChangedTrapCandidate and publish bounded pending records. Actual rescue/crew alias scans occur later, not in this capture path.

## Proven candidate1: owner-gate only candidate BFS

Concrete case: source owner2 has a valid affected Current-chain occupant owner1, supported Spearman idle/AI101 or another qualified role on ordinary Current. SnapshotUnit currently executes its radius8 BFS; afterwards outer RemoveAll discards that whole snapshot because owner1!=2. Its candidate list is never published or used by General. AI source owners1..8 follow exactly the same equality; no human-specific assumption is involved. This demonstrates unused performed work by source flow, not that this case caused139's tails.

Smallest behavior-preserving boundary is to pass validated source owner down ONLY as a candidate-collection gate (default0 retains generic SnapshotUnit semantics): run the complete existing discovery and full typed snapshot/coherence/failure logic, but suppress CaptureCandidates when owner>0 and captured snapshot.Owner!=owner. Keep final RemoveAll. All retained same-owner snapshots get the exact original BFS/order/distances/Distance0 candidate; owner0 collects all. Unsupported still follows its current early rejection. No new native field, role heuristic, callback, global scan or admission budget is needed.

Refutation attempts: foreign snapshot may fail identity/coherence and currently invalidate entire capture; therefore do NOT skip its full snapshot or validation. Foreign actors count toward512 selected capacity today; do NOT filter them out of discovery/counting. Source owner0/editor must not become owner1. Allied players are not equal-owner under current policy; do not replace equality with alliance/human checks. Full write envelope/historical retirement must survive even if every selected unit is foreign. Generic fixture snapshots and fresh intent snapshots have no source-owner filter and must retain current behavior. This narrow BFS-only gate survives all those counterexamples under the existing synchronous read-only capture contract; a raw early-owner drop of whole snapshots is NOT proved equivalent here.

## Proven candidate2: later same-batch BFS is unused

After capture, General's same-batch loop never takes the NEW candidate list when that unit ID already has an earliest Batch.Units entry. Same global/order/Current/Next only ORs AffectedCurrent/Next into previous.Before. Mismatch retires the old entry and marks the ID retired; it also does not adopt the new candidates. Already retired IDs skip altogether. Thus later nested captures can repeat BFS whose result is not consumed. This is common-boundary nesting, not permission to cache after arbitrary geometry writes.

A scoped candidate is to supply a read-only per-current-batch candidate-needed decision for IDs already present/retired, and skip only their later BFS. Still run COMPLETE footprint/discovery/full typed snapshot/identity checks and normal endpoint merge/conflict/failed-child behavior. The batch must be the exact current session/world/BatchId and its state cannot change during this read-only capture. The earliest candidate field must remain untouched; fresh first IDs still need full BFS. No cache may cross an original writer/batch/tick or substitute fresh geometry into earliest proof. Historical retirement before later writers, unknown-footprint invalidation, active-compound preservation and capacity tombstones remain mandatory.

Refutation attempts: dropping later entire snapshots would miss role/intent rejection or identity/pose conflicts; this proposal does not do that. Filtering a different/previous batch would discard new independent spatial proof; prohibited. Later retirement can remove an earlier batch entry, but it also marks that unit RetiredUnits, so the newly captured BFS still is not adopted. Reentrant arbitrary callbacks during capture would invalidate a precomputed decision; current real capture uses bounded managed/native-memory readers and no Original/creator/move, and source batch state changes only outside access.TryCapture. Any future callback addition requires rechecking this assumption. Existing diagnostics count foreign BFS but not all same-batch redundant BFS; actual frequency/importance is still unmeasured. Candidate1 is simpler and should precede this broader interface work if measurements justify either.

## Two smaller exact allocation candidates

- RescueDiscovery creates a new empty HashSet<int> per halo tile BEFORE reading head(tile). With head0, the set is never touched or retained. Move its creation after the SAME one-time head read and skip allocation for head0. All nonempty chains, duplicate/cycle/visit budgets, failure catch and sorted output remain identical. This can avoid up to one allocation per empty halo cell without changing scans. Do not replace this with an unchecked chain skip or hide a throwing head reader. Reusing a large set across chains is a separate unmeasured tradeoff (Clear may cost capacity-sized work), not this proven candidate.
- CaptureCrew returns new uint[0] on EVERY nonsiege snapshot. All source consumers inspected use length/value loops/shape/clone/serialization; none relies on distinct reference identity and a length0 array has no writable element. Array.Empty<uint>() is a compatible shared representation. This avoids a small allocation even for later unsupported/foreign snapshots, with no crew/ID weakening. It is not evidence of a relevant tail reduction. Eight-value siege crew capture and later initial alias proof remain unchanged.

## Guards already early or unsafe to move

| Proposed shortcut | Audit conclusion |
| --- | --- |
| Life filter before snapshot | Already in ReadDiscoveryUnit/native.TryUnit. Dead chain record is a capture failure today; silently skipping it changes full-chain proof. Keep the later second lookup/identity validation. |
| Cheap supported type/role before BFS | Already ClassifyState plus typed Job/Command capture BEFORE BFS. Unsupported actors have no candidate BFS. Crew/scalar allocations precede classification, but moving generic SnapshotUnit rejection before those fields changes diagnostic/full snapshot shape; not a proved generic rewrite. |
| Affected Current/Next before expensive snapshot | Already exact Discovery selection. Checking Current only loses genuine Next-only recovery. Do not infer HasOrdinaryNext for nonordinary/special modes. |
| Drop snapshots with neither late affected bit | No new ordinary-case gain: original discovery already requires one endpoint. Under a changed mid-capture pose this also interacts with earliest nested proof; preserve full snapshot/coherence/conflict behavior rather than inventing a new skip. |
| Skip BFS because Current currently has an exit | Incorrect pre-writer test: that ordinary old topology is the required proof before construction removes access. Whether a trap was created is intentionally post-original. |
| Owner-filter before selected-unit capacity | Changes currently visible512 overload into accepted partial capture; not equivalent. |
| No owned actors nearby, skip plan entirely | Incorrect: later writes can invalidate retained radius8 paths away from actors/endpoints/candidates. Complete envelope and failure/unknown semantics must remain. |
| Skip unknown/failing child when parent already captured | Incorrect: existing failed-batch tombstones prevent partial compound authority. |
| Global keep/building cleanup scan is unnecessary in no-attempt run | Incorrect: it closes native compound/removal writes before unit selection. Later rescue attempts0 does not make required source planning optional. |
| Full siege crew alias scan before capture to avoid BFS | Alias scan currently belongs to admission and can visit9999 slots. Moving it into every source is extra work and changes phase; not a cheap role guard. |
| Optimize per-tick census | Production capture uses local chains, not a permanent global-unit scan. Legacy MutationScope loops are explicit test-only callers and must not be confused with this runtime path or migrated into the main mod. |

## Native/managed contracts and callers checked

Current canonical hash verified in this investigation's preceding native review: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Existing Audits/MOVEMENT.md, CONSTRUCTION.md, TERRAIN-WORKERS.md and baseline TRAPPED_UNIT_RESCUE_AUDIT.md provide the complete feature contracts reused here: class6EC/route9C8/special fields, original Current-chain and accepted-step semantics, tribe930/93C, complete creator/cleanup footprint bounds, typed role/crew preservation. No new RVA/layout/native behavior is proposed.

NativeRelocation.Tile checks uint800 plus actual IsTileInsideMapBounds and uses GetTileId's read-only row lookup; Flags/Connections/Building/Head/Next are audited immediate table/record reads. Snapshot Job/Command delegates perform bounded field reads; their capture catches invalid schema/reader input. No native relocation, path query, original writer or game mutation is performed by candidate BFS. Managed runtime lifecycle still owns admission to a current synchronous simulation world; these are immediate views, not reusable pointers across callbacks.

All runtime SnapshotUnit callers were traced: automatic mutation capture; explicit current-occupant helper/test MutationScope/direct test paths; fresh intent capture explicitly uses collectCandidates=false. Other fixture snapshot callers must retain their default candidate behavior. Candidate gates must be specific to the source/batch invocation, not a global SnapshotUnit disable or an owner-specific static cache.

## Tests and decision boundary

No implementation is authorized by this audit and no tests/build/game were executed. Before an implementation, preserve exact returned accepted snapshots/candidate order/geometry and whole-capture failure outcomes against a reference. Existing DiscoveryFixture should cover foreign/unsupported records still participating in bad-chain/duplicate/512 budgets; add the missing focused owner1/owner2/owner0 branch cases at the real native capture seam if selected. GeneralObserverFixture/ReplanCoordinatorFixture cover nested earliest proof/failed parent-child/unknown metadata/historical conflicts and must stay green. RescuePolicyFixture covers local candidate field; NativeFootprintAdapter/MutationFootprints fixtures cover complete envelopes; pending/geometry codecs and full worker/command/crew tests must remain unaffected. Check source-structure tests if signatures change. Add only focused behavior cases for a chosen change, not text expectations that merely mirror implementation.

Root should first obtain valid real ON diagnostic windows. Read the direct capture/discovery/snapshot/observer spans, source counts, ownerDroppedAfterSnapshot and foreignCandidateBfs/foreignCandidateNodes together. Allocation API was actually unavailable on Mono in142; do not replace unavailable with zero. Process GC overlap is not collection duration or proof that these allocations caused a native tail. Inclusive parent/child spans must not be summed. These source-flow candidates are actionable but have no measured saving or attribution claim.

## Source snapshot

SHA256 via shared repository .NET helper; active working sources at audit time:

| File under src | SHA256 |
| --- | --- |
| GeneralRescueObserver.cs | 7760857f11b706cfb79576609482dea25498efbdaf064d99efa00cbce4fc5b12 |
| GeneralRescueObserver.Replans.cs | a306bc3545d38268e0c5841dab6abde16003c7e131b8287975258b364f8293bf |
| NativeMutationFootprints.cs | 7a7e396e9f7943a485cee828a77021dcd42388f6f6ed94483edc414773dce788 |
| RescueMutationFootprints.cs | 4019e5073628fb05d285364267966b9fe65d524e6a216f0c47736fe50800d512 |
| RescueDiscovery.cs | 05f56fdcd44d24e2855015269be39825b7beab9d80449ffa00dc6838e8ad34e1 |
| RescueRuntime.cs | 987e19436d0591f8d32516eac8646469a682000538f72e63ac60befd5b6f40d4 |
| NativeRelocation.cs | b9a93df502c0b44abbc541f8a0a64a15a376429de6379a9e0cc9bfba54edad72 |

This snapshot is descriptive, not a permanent build/version constraint. Later changes require rereading affected flow before reusing an equivalence conclusion.
