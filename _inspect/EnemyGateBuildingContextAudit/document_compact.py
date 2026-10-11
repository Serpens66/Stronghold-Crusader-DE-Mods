from pathlib import Path
root=Path('Testmods/EnemyGatePathfindingTest')
for relative in ['src/FunctionalGatePolicyAdapter.cs','src/DeferredGateDiagnosticErrors.cs','tests/CompactDiagnosticTests.cs','src/EnemyGatePathfindingRuntime.cs']:
    p=root/relative
    t=p.read_text(encoding='utf-8')
    p.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
note='''

## 2026-10-11: compact startup-selected diagnostics

Latest isolated test_gates.sav epoch 00:37:42--00:39:08: SE 2.14.1, APIShared 0.7.0, Main 1.0.184 and Fixes 1.26.4. 11,809 AI queries, zero AI NoRoute, 104,180 rejected edges and 83 verified building argument-role differences. Scope/context/exception/snapshot counters pass; all recorded order pairs match. No complete Raid/Assassin route inspected: 2,918 unit-buffer mismatches remain unclear, and the two Assassin searches are flood fields. These counts are historical evidence, not newly passed route acceptance.

Local BepInEx config [Diagnostics] DetailedDiagnostics defaults to false and is sampled once in Awake. Change BepInEx/config/EnemyGatePathfindingTest_Serp.cfg and restart for detailed investigations. No lobby/network/gameplay option or live settings handler. Standard mode registers a private functional IEnemyGatePathPolicy/IEnemyGateRoutePolicyProvider facade; it forwards exact resolver arguments, scope tokens, outcomes and immutable snapshot objects. It exposes none of the optional region/Assassin/temporary observer interfaces. The functional climb identity remains available on snapshots. Main/APIShared are unchanged.

Only detailed mode constructs AttackOrderCorrelationDiagnostics/TemporaryGateRouteAcceptance, registers the temporary bridge and subscribes the three order events. Standard mode neither creates those contexts nor inspects/copies diagnostic routes or records edge histories/live gate-state aggregates. Functional capture subscription, refresh requests, identity checks, search/integrity counters, immutable publications, native hooks and persistent tick/deferred callbacks remain intact. Static rooting/permanent publication unchanged. Geometry text/sample collection is skipped; mask construction and cache identity are unchanged.

Standard output: startup mode/owner, map start, one final map report with search/AI NoRoute/rejected-edge/cursor counters, capture proof counts and integrity. Raid/Assassin routes explicitly NOT_INSPECTED. The existing process-once table coverage warning remains. Errors are deferred and grouped by source/type, with all repetitions counted and first/last detail in the final report; each new category warns once. Identity/scope errors remain visible; unresolved capture identities/generations are emitted at map end. No event cap. Detailed minute windows and TEMP_GATE_ROUTE_ACCEPTANCE removal inventory remain available.

Verification includes an 8-player, 8,000-search functional facade comparison with exact scope/capture counts, original snapshot/cache identity, explicit unmasked/mismatched-player scopes and publication invalidation; 40 error causes with 1,000 repetitions each, no repeated warning output and exact map-reset totals. Existing topology/cursor/capture/building/Assassin/RedBird and 576,000-event regressions remain. No new game access/hook or policy/AI change. Current 60+21 native audit and 18-vs-35 actual hook-span/layout checks are unchanged. Runtime version 0.1.6, minimum dependencies and README untouched.

Game acceptance still requires the short known-save run: blocked foreign ground passage, true alternate access, own/captured access and reload; leave the map normally to flush the final report. Raid/Assassin comprehensive acceptance remains incomplete and can later use detailed mode; no additional long random run is required for this logging change. Build result is recorded separately.
'''
for p in [root/'PROJECT_FINDINGS.md',Path('_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md')]:
    old=p.read_bytes()
    p.write_bytes(old+note.replace('\n','\r\n').encode('utf-8'))
    assert p.read_bytes().startswith(old)
p=root/'TEMP_GATE_ROUTE_ACCEPTANCE.md'
old=p.read_bytes()
extra='''

## Startup mode guard (2026-10-11)

The temporary acceptance subsystem is now opt-in via local BepInEx [Diagnostics] DetailedDiagnostics=true, requiring restart. Standard false does not create acceptance/order objects, subscribe order events, register temporary observers or expose optional observer interfaces on the Gatepolicy provider. The private FunctionalGatePolicyAdapter forwards only functional scopes/resolvers/snapshots with unchanged token/cache identity. Keep this facade and functional capture refresh when removing temporary diagnosis later. Guarded Anschlussstellen: Plugin Awake order subscriptions; Runtime InitializeNative diagnostic creation/temporary registration; Runtime deferred/checkpoint/sample paths; SamePcl optional observers and sample capture; Topology optional text output. Existing minute windows/observer removal list remain. Standard NOT_INSPECTED is no Raid/Assassin acceptance claim.
'''
p.write_bytes(old+extra.replace('\n','\r\n').encode('utf-8'));assert p.read_bytes().startswith(old)
