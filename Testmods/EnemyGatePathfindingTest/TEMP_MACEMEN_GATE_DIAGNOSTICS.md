# TEMP_MACEMEN_GATE_DIAGNOSTICS

Local startup option MacemenDiagnostics=false. Enable for the next short test_gates_vanilla run. When true it overrides DetailedDiagnostics; the generic AttackOrderCorrelationDiagnostics and TEMP_GATE_ROUTE_ACCEPTANCE observer are not instantiated or registered. The permanent functional gate provider remains installed.

## Evidence and cost

Existing Script Extender target-order, group-move and single-unit move events retain short-lived numeric frames. SamePclGateRouteRuntime forwards existing search entry/completion and EnemyGatePathfindingRuntime forwards existing gate comparisons. No native hook, additional search, route copy, all-gate scan, unit polling or persistent unit history is added. Arrays grow only to simultaneous nesting depth and clear references at completion; minute buckets retain first/last examples and full counts, with no event cap. Command callback time is measured; this is not a whole-game lag profile. Aggregate timings using SDK doubles do not establish actual in-game overhead.

Pre arguments may be modified by other event subscribers. The observer retains the actual mutable Pre object, refreshes observed values during synchronous nested calls, and rejects changed identity arguments. Extender Post contains original arguments, and skipped originals have no Post. Skipped frames are pruned before reuse; unclosed frames are counted and cleared after native work. Post results are not proof of eventual physical execution.

Incoming target commands 9/36 are building attacks. Command 3 is a coordinate movement; 6/7 are retained as moat-related raw orders, 23 wall-related, 4 unit-related, others unknown. Group MoveType remains a separate stage and raw value. A unit move may carry its synchronously observed parent target command; without that context incomingCommand=-1. storedCommand comes from r_AI_LastIssuedTribeCommand and is never promoted into an incoming order.

Exact gate attribution requires published Building-ID/Global-ID and matching live owner/capturer. Gate comparisons use only immutable identity data plus their existing native arguments. comparisonSite=0 is E2705 PCL comparison, 1 is E3024 builder comparison; gateRaw=-1 means not read there. They prove a gate was inspected, not selected. GroundElsewhere includes targets behind a gate but does not prove a route traverses the gate. NativeSearch kind uses the existing EnemyGateSearchKind numeric values; search return is the observed success boolean, not a recovered native integer. Raw event returns remain separate from execution evidence. Unit states remain raw; no animation interpretation is inferred.

Mixed groups are identified only by a verified leader or actual synchronous Maceman child, never by scanning all members. Global IDs/full control WORD/tribe identities are checked; unknown contexts and snapshot changes are unverified. No alternative identity or player is invented. Epoch UTC and monotonic tick anchor make observation time reconstructable; log write time remains deferred.

## Removal list

- Remove MacemenCommandDiagnostics.cs and MacemenDiagnosticAggregate.cs and their explicit runtime project links.
- Remove tests/MacemenDiagnosticTests.cs, its policy test link and Program.Run call; keep unrelated regressions.
- Remove the MacemenDiagnostics startup option/precedence/subscription conditions from the plugin.
- Remove the optional macemen field/constructor flag, map lifecycle/event/deferred forwarding and capturer forwarding from EnemyGatePathfindingRuntime.
- Remove MacemenDiagnostics forwarding at existing shared search scopes and standalone builder/building scopes in SamePclGateRouteRuntime.
- Remove _inspect/MacemenDiagnosticTests only on explicit instruction; audit/test artifacts otherwise remain.
- Public installed layout checks can stay; they validate legitimate API contracts.

The APIShared/Main early observer guards are permanent passive-API fixes, not parts of this temporary feature.
