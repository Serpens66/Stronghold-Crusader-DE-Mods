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
