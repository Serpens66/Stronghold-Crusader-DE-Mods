from pathlib import Path
root=Path.cwd()
files=['APIShared/src/Pathfinding/Assassin/AssassinRouteHandoff.cs','APIShared/tests/APISharedTests/AssassinRouteHandoffTests.cs','BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs','Testmods/EnemyGatePathfindingTest/src/MacemenCommandDiagnostics.cs','Testmods/EnemyGatePathfindingTest/src/MacemenDiagnosticAggregate.cs','Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingRuntime.cs','Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingTestPlugin.cs','Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs','Testmods/EnemyGatePathfindingTest/Test-InstalledContracts.ps1']
for f in files:
 p=root/f;s=p.read_text(encoding='utf-8-sig');expected=s.replace('\r\n','\n').replace('\n','\r\n').encode();p.write_bytes(expected);assert p.read_bytes()==expected;assert b'\n' not in expected.replace(b'\r\n',b'')
print('PASS: explicit CRLF normalization and ordinal verification',len(files),'files.')
notes={
'Testmods/EnemyGatePathfindingTest/TEMP_MACEMEN_GATE_DIAGNOSTICS.md':'''# TEMP_MACEMEN_GATE_DIAGNOSTICS

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
''',
}
for f,s in notes.items():p=root/f;p.write_bytes(s.replace('\n','\r\n').encode())
append={
'Testmods/EnemyGatePathfindingTest/PROJECT_FINDINGS.md':'''\n## 2026-10-11: compact Macemen command evidence and passive handoff\n\nLatest baseline comparison run 19:21:51--19:24:04 loaded test_gates_vanilla.sav: 16,494 AI searches, zero NoRoute and no functional scope/snapshot/exception failures. 1,755 positive Maceman searches carried stored unit command3; this is not proof of a newly issued movement-through-gate order. User observed approach followed by gate attack in both modded and Vanilla saves. New TEMP_MACEMEN_GATE_DIAGNOSTICS separates incoming target command, inherited synchronous parent context, stored unit command, search outcome and exact target gate. Minute aggregates replace broad detail collection in this profile. Physical attack execution still needs observation.\n\nSource and installed APIShared0.7.0 IL confirmed a handoff reporter constructed a diagnostic string before Main discarded it for an absent observer. The internal API reporter and Main delegate creation now gate on the explicit optional Assassin observer. Functional identity/validation/publication remains unchanged. This small passive-diagnosis correction is not a demonstrated cause of strong whole-game lag. No global logsetting changed.\n''',
'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md':'''\n## 2026-10-11: Maceman observation contract\n\nReconfirmed full60 search/climb bodies,16,960 installed instruction ranges and21 planning/output bodies against current FBCB9319. The installed Extender Maceman speed signature uniquely locates the complete0x146A70 update body (6306 bytes); _inspect/EnemyGateBuildingContextAudit/macemen-native-update-evidence.txt preserves its full body and incoming table references. r_AIState is read at unit+2BC; r_AI_LastIssuedTribeCommand at+398 and context target tile at+3A4 are distinct. The update includes tile-damage execution via0x7EB00, but a group Post/positive movement result does not prove that later branch executed. The extra observation does not hook the update or damage function.\n\nExtender target/group/unit Pre events are mutable; Post is recreated with original input arguments. SkipOriginalFunction suppresses the Original and Post. Exact synchronous contexts must retain the Pre object and treat identity mutation/missing Post as unverified instead of fabricating a new order. All new reads use installed public SDK members; native queries/results are unchanged.\n'''
}
for f,s in append.items():p=root/f;old=p.read_text(encoding='utf-8-sig');b=(old+s).replace('\r\n','\n').replace('\n','\r\n').encode();p.write_bytes(b);assert p.read_bytes()==b
print('Documented provenance, limits and removal points without changing README/version/savegame.')