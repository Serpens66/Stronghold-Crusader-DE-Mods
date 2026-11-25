# Failed-move candidate source audit [Scope: Mod work, not Vanilla]

Snapshot: 2026-11-25. P7 of RESCUE-PLAN; read-only source/native analysis, no installation, runtime change or game execution. Freshly rehashed canonical CrusaderDE.dll matches CURRENT.json: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Existing complete movement/worker/special-state audits are MOVEMENT.md, TERRAIN-WORKERS.md and COMMAND-RESUME.md. The final UnitMove API examined is the isolated integration source under `.work/a/rescue-api/001/src/Units/Movement`; the ordinary APIShared checkout does not contain those files at this snapshot.

## Result

The final UnitMove API closes **invocation identification**, including effective arguments and actual native refusal. It does not close **trapped-source or safe-bank identification**. An arbitrary failed move cannot presently authorize a teleport, a flags5 hold or a job/order reset. No independent correct-side proof for a unit already trapped before observation exists in the examined code.

Consequently, there is no currently justified new functional P7 teleport path for failures outside the observed mutation coverage. This is a missing static contract, not a game-test-only blocker. Adding a failure callback that never gains spatial evidence would add runtime cost without rescuing another case. The current causal observer remains the functional owner; a false-negative coverage boundary is preferable to a newly invented bank.

## Closed native chain [Vanilla facts]

Native196280 takes manager, one-based unit ID, unsigned destinationXY and selector. The full body and existing callee/consumer audit were rechecked against the current-hash baseline:

1. Invalid/unavailable destination returns0 before an identified route failure; it says nothing about source mobility.
2.19B1B0 can normalize Current/Next/fine position. Start is Current only when flags0 and substep8; otherwise it is Next. A substep8 test alone does not identify the real planning start.
3. The function records requested744/746, clears portal8EC and retryA4C. Start==destination returns1 without builder publication.
4. Surface/region gates, moat mode196840/E7C40, PCL search, DF720 portal selection and F4630 participant binding can refuse before a builder is entered.
5. At196585 it copies the working unit record, clears its1000-byte path slot, binds that output slot, then calls F4930 for selector0 or E32B0 for other selectors. E32B0 reconstructs existing search data rather than performing an independent search.
6. A builder failure clears selected temporary state and invokes199CD0, but does not restore the old path bytes. Old active flags/length/cursor may survive.199CD0 can choose native AI110 under its wall/type/state conditions. Return0 is neither an unchanged unit nor a safe parked actor.
7. A successful result publishes route length, cursor0, flags2, actual path end/start and stored moat9C8; requested destination and actual intermediate path end need not be the same.

Downstream182B00→1855A0 is a different signal. A zero return there includes ordinary idle, owned flags5, waiting and other legitimate gates. DCE60/DCD60 test new physical transitions, not arbitrary failed requests. Accepted substeps do not rerun those transition checks.182B00 also executes a speculative1855A0 call with parameter4==1 and restores the copied record; it is not a real failed step or unit relocation event.

DCE60 accepts normal class0 source-forward **or destination-reverse** connections and can set waiting/climbing state; calling it as a read-only probe is invalid. DCD60 restricts the destination, not whether the current tile may be left. Stored moat mode, Assassin climbing, class1, portals and transport provide additional legitimate movement. Thus source mask0, PCL0, a raised/moat flag or eight unaudited neighbour checks do not independently prove a trapped unit.

## Existing owners and available evidence

| Source | What it proves | What it does not prove |
| --- | --- | --- |
| Final `UnitMoveContext` / `UnitMoveCompletion` | Final UnitId/XY/Extra after mutable Pre; wrapper/parent/world identities; NativeInvoked/Returned/Result before mutable Post | Global/lifetime identity, typed job/intent, physical failure reason, safe exit bank, final route after later Post |
| Existing rescue order observer | Stable matching receipt and actual result; own-origin filtering; foreign nested orders remain authoritative | A failure is not new causal geometry; no arbitrary failed-unit queue is created |
| `GeneralRescueObserver` | Before-writer Current/acceptedNext and genuinely old reachable local candidates; current world/order; post-writer changed trap | Old-save geometry or unobserved historical origins. New external orders intentionally invalidate its queued old intent |
| Pathfinding pre-builder recovery19664B | Exact matching UnitMoveFrame, start/mode/target, qualified encoded friendly moat route | General teleport permission; it repairs a route in its own owner and does not eject an arbitrary source |
| Pathfinding `GroundFailureProofs` | Current policy-qualified closed direction set and failed search from a member to a destination outside it | The member is trapped rather than standing in a perfectly legal component; which wall/bank may be crossed |
| Pathfinding `MoatFailureProofs` | Exhaustive DAFD0 failure set under mode/profile and unchanged boundary inputs; DAAC0 remains separately executed | Permission to leave the closed set by teleport or to ignore current type/mode rules |
| `RouteSearchEvents` | Additional participating weighted search outcome and endpoints | Stable unit identity or a global native failure event. Its contract forbids commands/world mutation in callbacks |
| `PathReachability` | Player/mode-aware coarse connected/unreachable components | Detailed executable route, trapped source or correct bank. Native bookkeeping forbids careless nested use |

The final API uses the existing SE native owner; no second196280/F4930/DCD60/1855A0 hook is justified. Begin receipts are private and complete in reverse registration order. No original/veto/exception and current-world mismatch remain distinct from actual0. Completion occurs before legacy Post; foreign later Post can change goals, pose or world. Therefore a new candidate may only be queued as immutable evidence and revalidated after the entire wrapper, never acted on inside these callbacks. The current order observer's bounded-interest filter intentionally observes pending/owned actors rather than making every failed unit a candidate.

## Refutations: why the tempting substitutes fail

- **Stable identity + actual native0:** a healthy soldier on ordinary ground can receive a destination on another disconnected island. The correct outcome is refusal; relocating him would bypass a real obstacle. Native0 is identical in the relevant API fields to a source-side obstruction.
- **Failure set closed around source:** a normal castle courtyard surrounded by walls is a closed set. An unreachable destination outside does not grant permission to cross a wall. A singleton set also requires current type-specific exit proof; it is not historical-side evidence.
- **Choose the nearest legal empty tile:** the same invalid source can border two legal banks separated by a moat/wall. Current geometry and the failed goal cannot establish which side belonged to the unit before the obstruction. Choosing the goal-facing bank would turn an unreachable-target request into a crossing mechanism.
- **Recompute old candidates in failure Post:** by then construction may already have made the origin invalid. BFS from the current topology creates no old reachability. Even a known previous successful move proves only that that command was published, not perpetual validity of a cached bank after motion and later geometry changes.
- **Reuse a construction snapshot after another order:** the current observer explicitly invalidates it. Retaining its old goal or job would undo a newer command. Geometry could only be transferred through a new explicit, independently validated witness contract; the present queue does not provide one.
- **Clear the path or park every failure until recovery:** normal unreachable destinations and native job-specific failures are legitimate.196280 may also leave a partial live route. A flags5 write or clearing AI/job/path without a positively admitted rescue source changes gameplay without a trapped-source proof.
- **Call the native movement oracle to find an exit:** DCE60 has writes and1855A0 can advance occupancy/animation; speculative calls have their own scratch-copy semantics. This is not a read-only qualification strategy.

## Implementable boundary and missing next contract

A future failure-triggered functional path can use the final API **only after** obtaining one of these independent authorities:

1. **Retained causal witness:** same session/world/unit lifetime; a genuinely observed earlier ordinary Current/acceptedNext and old local reachable candidate set; no intervening invalidating source transition; a current exact typed job/command and authoritative new goal captured separately from the old geometry. Transfer to a newer external order must revoke the old intent while preserving only proven spatial evidence. The existing construction queue must not silently be reinterpreted this way.
2. **Independent current escape proof:** a complete read-only physical exit predicate for the admitted type/mode, plus a positively proved legal ejection relation and correct-side rule. A native failed search or negative reachability query supplies neither the relation nor the side. A building-specific ejection contract can supply both for its exact family, but cannot become a general rule.

When such authority exists: reserve bounded capacity before mutation; deduplicate by world/unit global/current order; retain actual Context.Extra and real start/goal; queue outside search/wrapper callbacks; revalidate unit/typed state/crew/pose/world and current candidate occupancy; use the existing owned resume/idle owner. No job, stock, crop, input, output or old-goal replay is allowed. The failed event is a trigger; it never becomes the spatial proof.

The practical next offline task is to choose and audit one narrow ejection relation or a geometry-witness producer with a bounded lifetime. A generic historical snapshot cache is not already proven safe and a continuous10,000-unit monitor is not required by this design. Until that contract is supplied, P7 remains a documented functional coverage limit rather than an implementation waiting only for game tests.

## Verification scope

Read: final UnitMove Contracts/Events/Broker/Wrapper/NativeOwner and root order observer; complete GeneralRescueObserver capture/order/batch/queue/lifecycle flow; native196280 and existing closed movement/helper audit; Pathfinding UnitMovementContext, NativeMovementRecovery pre-builder branch, builder publication/ownership, Ground/MoatFailureProofs, RouteSearch contracts and PathReachability. Counterexamples above survive the existing guards because those guards establish identity/search validity, not historical escape authority. No production/API/runtime edits or new tests were needed for this evidence-only task; no game behaviour is claimed as newly reproduced.

## Bounded retained-geometry replan contract: design follow-up

The subsequent requested design narrows P7 to **previously observed causal geometry**, retained briefly across a new external order. This can rescue an existing proved trap whose original queued intent was correctly invalidated by that order. It still supplies no permission for a failure lacking an original geometry witness. The following is an implementable proposed contract, not a claim that the P7 source is already wired.

### Separate immutable records

`GeometryWitness` contains only session/world, unit ID/global and original type/owner, source mutation identity, original capture tick/age and cumulative attempt budget, original Current, the optional one originally accepted Next, affected/previously-ordinary endpoint flags, and a deep-copied old reachable candidate list. No old AI/job, cargo, requested destination, tribe assignment or crew references become executable intent. A newer order may retain this spatial record; it cannot refresh its original80-tick expiry or reset its attempt allowance. Conflicting subsequent geometry, identity/type/owner change, lifecycle invalidation or an exceeded bound discards it. There is one bounded record per unit, not a unit scanner.

`FreshReplanIntent` is captured read-only at final-original Begin, **after** the canonical effective-order serial has advanced. It contains the actual context identity, fresh order serial, exact typed worker/command snapshot, fresh actual tribe/stamp, fresh goal, crew and active external bindings, and its own Current/Next/substep/flags pose. It is not reconstructed from GeometryWitness. Admission requires ordinary active flags2/4, effective ContextXY exactly equal to live RequestedXY, selector Extra0, all currently admitted family/state/mode guards, and fresh position within the original witnessed anchors. Other selectors, a new destination, idle/special phases or unknown failures produce no trigger.

The existing native world's `TryCapture(freshSnapshot,...)` can supply an immutable **unreserved** typed receipt at Begin: it performs no move or park and captures the active workplace/store/vegetation lifetimes and command crew. This is stronger than Job alone. In particular, Apple9A0 has no unit-stamped tree serial: its external global must already be captured at Begin and still match at Tick. A late tree lookup cannot turn a reused slot into the same fresh intent. The same temporal requirement was independently found in the current construction pipeline and fixed there by pre-mutation vegetation evidence; P7 must retain that evidence as part of its fresh receipt as well.

### Original completion and safe Tick admission

Only the exact matching current-world original that actually returned0 may arm a candidate. Native1, unsupported result, no original/abort, world change or another external order revoke the trigger. Revocation is per effective unit, not just per matching serial: an older outer original can run after a nested order and publish a positive route even though its Begin serial is older. Such a positive publication must cancel a nested0 trigger too. Otherwise equal goals and unchanged typed fields could make the inner failure appear current despite the later successful route. Independent other-unit nesting remains separate.

No mutation occurs in Begin, original completion or legacy Post. At the next admitted simulation boundary, require the current order serial to remain the fresh serial, the same unit lifetime/type/owner and exact fresh tribe identity, the same live requested goal and immutable typed intent, valid pre-captured active external bindings, unchanged crew identities, and the geometry/pose join below. The ordinary worker continuation comparison may exempt only the already audited Ox8E6 and Wheat/Hops9D8 countdowns; refresh those harmless timer values into the exact synchronous snapshot afterwards. No old-purpose comparison is substituted for this fresh intent.

Native0's known movement bookkeeping writes (requested744/746, portal/retry/output cleanup, search slot clearing) do not authorize a new job.199CD0's AI110 transition fails the fresh AI check and is left to its native owner. Skirmisher's native failure sets A7E=100;178350 AI101 increments/consumes that natural counter and can choose a combat target/AI4. A7E is not command identity and must not be restored; an ensuing actual AI/adversary/goal change wins and rejects this candidate. Caller-tail198620 shortening, legacy Post role/job/goal changes, death, target slot reuse or group reassignment similarly invalidate the fresh proof through their specific checks. Same-goal **new purpose** is allowed only when that purpose was itself the freshly captured valid Pre intent and remains current; the original geometry job is never restored.

### Two coordinate frames must not be merged blindly

First require fresh Pre Current to equal original Current, or the exact original accepted Next. A fresh in-progress Next can inherit spatial authority only when it is exactly that original accepted step; no newly planned second step inherits it. At substep8 require coherent Current==Next and raw740zero; do not admit an unrelated Next merely because substep8 was reached. After the failed call and at Tick, admit only the same fresh Current or completion of that same witnessed step. Coherent packed coordinates, Next, substep and motion mode are revalidated. An observed departure or any unproved successor pose retires the witness rather than creating fresh evidence. These coordinate checks are not claimed to reconstruct a complete unobserved movement history; positive intervening native publications should conservatively retire the retained witness as well as its trigger.

The runtime input must keep **fresh Pre pose/Flags/Substep/OrdinaryStep** for the supervisor's admissible-capture guard. Separately map the historical `WasOrdinary`/`Affected` evidence onto those fresh anchors and supply the original candidate list. For example, if fresh Pre already stands on original Next, the original Next evidence maps to fresh Current; it does not create another accepted Next. Reusing original OrdinaryStep together with a newly rebased X/Next would incorrectly authorize a second step. A small pure `TryJoin(GeometryWitness,FreshReplanIntent)` should enforce these cases before producing a runtime snapshot; it must not mutate either input.

Before joining, validate the unreserved fresh receipt through the native world's existing identity/job/crew/binding inspection. Only then may the runtime recapture a new exact synchronous receipt and use its existing reserve-before-relocate/park/probe/resume path. This reuses the audited continuation for all currently admitted Worker R and Command families without admitting their contact/native-owner states. Old bank candidates remain independently checked for live ordinary surface, physical exit and occupancy. Missing candidate or changed proof means no mutation.

### Lifecycle, queue and save integration required before implementation

Use a canonical order serial shared with GeneralRescueObserver, explicit observer priority after its Pre invalidation, single-use context-bound completions and per-unit deduplication. Cancel the original queued intent while transferring only its immutable geometry; never run both sources for the same actor. Share the bounded512 candidate/retention budget and preserve the original attempt/age values. Repeated failed replans must not extend lifetime or multiply work. No geometry witness means immediate NoAction and no path/AI/flags writes.

A new source also needs an explicit save phase: Begin through the first admitted Tick is unsettled because the public completion precedes legacy Post and the native caller's tail. Mark such interested receipts transient and reject atomic save capture until that boundary, rather than serialize half-observed fresh intent as a completed operation. Once Tick has validated and joined a candidate, a strict pending record can preserve its original age and fresh typed/binding evidence. Geometry-only retention needs its own explicit record kind; do not serialize the invalidated old job as a placeholder. Load/disable/session/world policies must be coordinated with the existing causal queue and receipt owner. These are necessary integration tasks, not additional native detours.

### Offline closure and remaining scope

The native safety argument is conditional on the explicit guards above and reuses the closed active-family consumers; it requires no inference from native0 to source entrapment. Implementation should first test the pure join and transfer kernel: originalCurrent, originalNext and accepted completion; second-step rejection; same-goal changed purpose; Native1 after nested0; caller/legacy Post mutations; actual0 AI110 and A7E progression; pre-captured vegetation reuse; original age/attempt retention; capacity, save/load and order priority. Then exercise real observer/runtime wiring. Native movement and gameplay accounting still require game acceptance, but the model separation, callback ordering, lifetime guards, budgets and codec can all be implemented and tested offline.

### Fresh-intent boundary implementation checkpoint

`src/RescueReplanIntent.cs` now implements the read-only part through IRescueReplanIntentWorld. NativeRescueReplanIntentWorld uses SnapshotUnit(id,true,false), so Pre does not collect another local candidate BFS. RescueReplanIntent stores a private deep copy of the fresh Pre snapshot and an immutable unreserved typed receipt; it strips candidate geometry/witness from the intent. The generic CopySnapshot helper preserves an existing immutable GeometryWitness when used outside that constructor.

IsCurrent validates the original receipt through Inspect(receipt,true), including original active external binding lifetimes, before any new capture. Its live pose guard rejects special modes, death, incoherent packed coordinates, stationary pending740, unrelated Next, step reset and an unproved second successor. TryGetSnapshot then uses NativeWorld.TryCapture to refresh the exact synchronous job and current health, compares identity/goal/bindings/schema, and preserves all original fresh Pre pose fields for GeometryJoin. Only the explicitly audited worker timers may differ. It never reserves a ledger slot, calls a planner, parks a unit or changes geometry. The coordinator still exclusively owns actualNative0, world/session/order serial, wrapper settling, geometry join, age/budgets and save integration.

RescueReplanIntentFixture executes that same orchestration with real immutable worker/command snapshots and injected read-only access.928 offline assertions cover all20 admitted Worker families and their R states, every admitted Command phase, exact completed-Next0 handling, rejected second steps, health/timer refresh, selected-tree lifetime changes, crew/AI/owner/tribe/goal/command changes and mutable-copy isolation. The genuine-SDK isolated executable passes; an independent read-only review found no additional confirmed defect within this boundary. These are boundary proofs, not a claim that coordinator wiring or native gameplay has passed.

### Coordinator and persistence implementation checkpoint, 2026-11-25

The proposed retained-geometry contract above is now implemented in GeneralRescueObserver.Replans and wired at final Move priority100 after canonical order invalidation. Geometry-only records use a separate codec; joined candidates use pending codec4 with an exact canonical geometry join, preserving the original radius8 frame even when the fresh origin is originalNext. Both namespaces share capacity512 and original age/attempt accounting. Begin through first admitted Tick is unsettled and blocks atomic save. Late zero completions cannot arm a future boundary; a late positive publication still retires geometry.

Independent actual-source offline runs passed GeometryWitness209, FreshIntent928, GeometryPersistence637 and Coordinator853 assertions, plus all existing suites. These close managed identity/order/pose/codec orchestration at this checkpoint; they do not prove native caller-tail timing or gameplay. The final footprint-conflict regression and complete packaged build are recorded in IMPLEMENTATION.md. A failed move without a causal witness remains outside the admitted rescue source.

### Historical-bank overlap and compound failure closure

The actual coordinator regression reproduced a retained witness surviving a later
planned write at originalX+3/originalY+3 with no local actors, away from its
endpoints and chosen candidate fields. Candidate occupancy checks cannot prove
that an intermediate old path is unchanged. The coordinator now invalidates all
historical representations on overlapping original radius8 envelopes before the
new writer; disjoint complete envelopes preserve independent cases. Native
planning is done once, and its complete envelope remains available if occupant
discovery subsequently fails. No envelope is inferred from the captured actors.

A failed first or later child capture leaves an Invalid Batch tombstone until
outer completion. Zero-actor conflicts retain retired actor IDs too, preventing
later samecompound child re-admission. Unknown envelopes cannot publish a partial
proof. A bounded64-batch failure overflow retires historical state and latches
capture off until world reset. Successful complete samecompound captures retain
their existing earliest-proof rule. These corrections change metadata ownership,
not the original native writer or its execution count.

The final focused actual-source run passed Coordinator919 and General179 checks;
six live/staged/joined/unjoined stores, shiftedNext radius9, sparse disjoint
control, known-envelope discovery failure, unknown planning, failed-first/child,
conflict tombstones and overflow/reset are covered. The additional invalidation
work is bounded by2048 envelope entries plus at most512 historical records with
at most289 coordinate-hash probes each (147968 maximum), with no unit reads or
new native hooks. This is an operation bound, not a game timing measurement.
