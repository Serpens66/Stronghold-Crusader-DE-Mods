# TEMP_GATE_ROUTE_ACCEPTANCE

Temporary read-only acceptance diagnosis, 2026-10-07. Remove after the Raid/Assassin acceptance run. No new native hooks, additional searches, or persistent unit tracking.

## Evidence and limits

### Correction after the 2026-10-07 15:19:55–15:24:06 run

That run had 52,070 AI queries and six NoRoute results with integrity PASS, but its acceptance diagnosis was incomplete: 456 invalid Tribe-ID-0 SDK lookups, 399 weighted routes falsely rejected by a node/edge mismatch, 73 target requests with no materialized weighted route, and no packed-publication observations. Two Raid replacements (players 2/4, role 3) reached fresh valid attack points; their actual paths were not established. Historical zero violations are not a route pass.

Format 2 guards every added Tribe lookup with installed IsValidId and counts missing context by source. Weighted node count includes the start: expectedEdges=nodeCount-1; a one-node route passes only for identical endpoints. Packed lengths remain directed-edge counts. Negative searches are separate from positive results lacking a path; floods are never route failures. Entry/exit identities and map/publication changes remain unverified.

The packed-publication helper no longer calls GetBuilderPlan or the mutating frame accessor. The old GetBuilderPlan was moat-specific and could qualify intermediate targets through extra searches; the earlier no-extra-search marker therefore was not sufficient proof. The helper now reads the existing valid frame or derives the one-based Unit-ID from the audited owned buffer slot, validates full control WORD and endpoints, and checks Unit-/Tribe-Global-ID again on return. It neither constructs a moat plan nor invokes a search. Missing context is counted; it is not replaced with a guessed player or Tribe.

Runtime fixture tests execute the actual diagnostic classes, packed-publication helper and the production ObservePreparedAssassinRoute method (extracted without modification), with isolated memory/SDK fixtures. They cover invalid IDs, stationary/multi-node routes, cache/climb, exact violations, missing output, nested calls, reused identities, full control WORD, frame-less/no-moat publication, partial buffers, skipped frames and diagnostic exceptions. They do not replace installed-assembly source compilation or the subsequent game test.

- Raid roles are the six installed `HarassmentCombat0..5` storage roles, checked against player, Tribe-ID and Global-ID. Command classification reuses the retarget fix's paired search evidence and freshness validation. Candidate coordinates, search sequence, native return and replacement outcome remain separate from published routes.
- Both existing unit-builder publication paths inspect their final packed output only when an observer is registered. Manager, output pointer, unit/control identity, length, endpoints and every nibble must match. Partial output is **unclear**, never checked. Native packed directions do not encode a reliable climb classification: `packed-directions-climb-unknown` is explicit.
- The existing Assassin observer reports target searches, flood fills, continuations, native/weighted results and cache origins. Materialized weighted routes include the existing per-edge ground/climb classification. Flood fills are not treated as failed target routes.
- A checked route requires all edges and the same nonempty publication identity through completion. Every directed edge uses that player's published gate mask, including own/allied/captured access. Invalid player, edge, identity, partial decoding and publication changes are not a pass.
- Each active combination is emitted every 60 seconds and at map end. Variable units, coordinates, Tribes and target buildings appear only in first/last samples. `targetChanges` counts successive targets within that result bucket, not a permanent per-Tribe history. Exact gate IDs are additional violation keys. There is no event cap.
- Final coverage distinguishes six Raid roles, target searches, cache paths, climbs, flood fills, continuations and native publications. Zero coverage means **not observed**. A positive command, fresh attack point, or selected replacement building does not prove a successful route. The played behavior still needs observation.
- Generic decision details use a counts-only profile; integrity/error rows and live gate-state definitions survive. Capture refresh evidence is unchanged. Neither bridge policy nor bridge diagnosis belongs to this acceptance.

## Removal checklist / attachment points

All additions carry `TEMP_GATE_ROUTE_ACCEPTANCE` or use a class named `TemporaryGate...`.

1. Remove APIShared `src/TemporaryGateRouteAcceptanceBridge.cs` and its project include/public-test allowlist additions. This is a passive independent observer, not the gate policy registration.
2. Remove BugfixesAndQoL `src/TemporaryGateRouteReporting.cs`, its project include and the linked `Shared/TemporaryPackedRouteInspection.cs` include. Remove `BeginTemporaryRouteReport`/`EndTemporaryRouteReport` from `MovementPathPublication.cs` and `MovementSearchContext.cs` only.
3. Remove the optional `ReportRaid` blocks from `AiRaidRetargetFixRuntime.On...` command classification and `LogRetry`. Preserve their existing evaluations, freshness checks and retarget decisions.
4. Remove Gate `src/TemporaryGateRouteAcceptance.cs`, `src/TemporaryGateAcceptanceAggregate.cs`, project includes and `tests/TemporaryGateAcceptanceTests.cs` plus its invocation/includes. Remove APIShared temporary source include from policy tests.
5. Remove Gate runtime construction/registration/map begin/end/deferred calls and `SamePclGateRouteRuntime.TemporaryAcceptanceSnapshot`. Remove temporary Raid/Assassin attachments and exception wrappers in `AttackOrderCorrelationDiagnostics`.
6. Restore the generic 10-second cadence and original cadence test after removing the temporary counts-only profile. Remove `TemporaryCountsOnly` from `Shared/PathDecisionAggregate.cs`; do not change unrelated aggregation logic.
7. Remove the temporary decoder from Shared only after removing its production and test includes. Remove temporary test-source injection and no-observer fixture stubs in the mainmod source-contract tests and `_inspect/EnemyBridgeCompileTests`, plus the isolated `AiRaidRetarget.Tests/Run.ps1` bridge source include. These fixtures exercise the old no-observer path; real runtime sources compile separately against installed assemblies.

## Audit and verification

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed Script Extender: 2.13.1.0, SHA-256 `225441FC02215359AF9C70F954EF83B9B2FD60608AC867E38B51C81F3E0C1A71`; local commit `2a0e1a2ac2d4b5261ec1c27289b3d8e32eb835ec`, tree `792b0c1051ba3eccbb5cbe7b4468d1e7c863b426`.
The Raid scheduler/search/command/publication chain and Assassin-only group bypass, D9C40 target/field/continuation, reconstruction and weighted publication were audited against this native baseline and installed SDK. Existing ownership and native call sites are unchanged. Installed role APIs and full unit control WORD are checked before build. Long-lived diagnosis runs through the existing statically rooted search observers and deferred publisher, not plugin lifecycle callbacks.

Synthetic test: 576,000 changing-target events over 120 minute windows; 48 rows/window for eight players and six roles, exact counters, drained windows and no growing event history. Native packed-path decoding checks directions, endpoints, coordinates and truncation. Passive registration tests cover no observer, throwing observer, unchanged simulated return/call count and duplicate registration. Existing native-adapter execution, gate/cursor/capture/Assassin/Raid regressions remain required.

## In-game run

Enable EnemyGatePathfindingTest and the improved Assassin pathfinding in BugfixesAndQoL. Run a longer eight-AI game with foreign gatehouses, reachable outside buildings and buildings behind gates. Leave EnemyBridgePathTest disabled for this gate acceptance. End the map normally to flush the final coverage report. Only missing observed cases need targeted follow-up.

## Format 3: native Assassin publication follow-up

Publication aggregates retain source (F4930-builder/E32B0-reconstruction), raw modes, logical reconstruction relaxation and only synchronously nested Assassin results. Unknown preexisting fields and the unobserved native accepted branch stay unknown. Packed climb evidence is unknown, not false. Gate-attack order overlaps, gate endpoints and other-purpose mask intersections remain separate; none alone proves executed passage. Exact SiegeAssassins classification uses storage role 11 plus live generation/owner. Fixes presence does not prove hook causality or live preference values.

Removal additions: remove the TemporaryReconstructionRelaxation reader/root in AssassinPathfindingRuntime and its Begin/EndTemporaryAssassinSearch attachments; remove the thread-local publication correlation from TemporaryGateRouteReporting and the source argument at both existing publication attachments. Remove the compile-only sibling reader/optional source parameter from FriendlyMoatMovement test fixtures. Native flags, surface/building samples and meaning dimensions are contained in the already marked temporary modules; APIShared needs no new interface.

Next game: same castle with improved Assassin pathfinding enabled, open then closed gates, Fixes unchanged; then open gates with the improvement disabled. End each map normally. Look for format=3 and paired publication/search evidence. The 78 historical intersections remain unresolved.

## Format 4 movement assessment

Confirmed ground edges, verified weighted climbs and unresolved overlaps are separate. Unassigned queries are no-route-inspected, not failed human routes. Packed climb/execution remains unknown. Identity and capture changes invalidate completion. Minute windows have complete counts; no event cap.

Removal addition: AssassinPathAPI.ClassifyNativeTransition and marked observer assessments/counters/fixture cases. Keep the pure AssassinGateTransitionPolicy and functional weighted/cache/reconstruction guard.
## Coordinated review and installation completed (2026-10-07)

After the parallel APIShared work finished, the installed APIShared was verified current (package/install SHA256 equality and new public contract present); no redundant APIShared build was needed. The final review also required a weighted diagnostic climb endpoint to match the exact gate responsible for the blocked edge, rather than a neighboring gate. Actual diagnostic runtime tests now pass 46 assertions. The main movement test fixture was adapted to the parallel UnitAccess.IsReallyAlive addition (enum alias and low-word death-marker field); production UnitAccess was preserved.

All pre-build Native/Interop, compatibility, JSON, Lifecycle, permanent-hook, XAML and CRLF checks passed. Independent Assassin tests: 15,862 assertions; Gate tests: 9,900; installed RedBird Assassin execution tests: 1,396. The full BugfixesAndQoL build driver and all its regression suites completed successfully, with zero errors and the existing MSB3277 assembly-binding warning. EnemyGatePathfindingTest build.bat completed with zero warnings/errors. Both drivers installed successfully. Installed/local package hashes match for APIShared, BugfixesAndQoL and EnemyGatePathfindingTest; evidence: _inspect/EnemyGateBuildingContextAudit/transition-installed-hashes.json. Successful build logs: transition-main-build-complete.log and transition-gate-build-complete.log in the same directory. Earlier failed-attempt logs remain historical evidence.

AssassinGatehouseClimbTest is unchanged and needs no rebuild. Versions and README files remain unchanged. No new native hooks or changes to cursor, formation or physical climb execution were introduced. In-game acceptance remains pending: Assassin commands to the roof and behind open/closed enemy gates, ordinary soldiers denied the ground passage, improvement enabled/disabled, and AI behavior at the same setup. Bridges remain outside this acceptance. Native-only historical mask overlaps are not retroactively marked resolved.
