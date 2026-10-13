from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest/src/BridgeLossHistory.cs')
s=p.read_text(encoding='utf-8-sig')
old='physicsTiming=current-observation");h.LastState=state;'
new='physicsTiming=current-observation,captureClock="+captureClock+",fieldCaptureClock="+h.FieldsClock+",coherence=scalar-reads-not-atomic,negativePolicyEligible=False");h.LastState=state;'
if new not in s:
    assert old in s;s=s.replace(old,new);p.write_bytes(s.replace('\n','\r\n').encode())
paths=[p,Path('Testmods/EnemyBridgePathTest/src/BridgePlanningCapture.cs'),Path('Testmods/EnemyBridgePathTest/src/BridgeStructureTrace.cs'),Path('Testmods/EnemyBridgePathTest/src/BridgePlanningImporter.cs'),Path('Testmods/EnemyBridgePathTest/src/BridgeDiagnostics.cs'),Path('Testmods/EnemyBridgePathTest/tests/StructureCaptureTests.cs'),Path('_inspect/BridgePlanningTests/Program.cs'),Path('_inspect/BridgePlanningTests/MilitaryContracts.cs'),Path('_inspect/EnemyGateBuildingContextAudit/bridge-log-analysis.py'),Path('_inspect/EnemyGateBuildingContextAudit/bridge-loss-audit.py'),Path(__file__),Path('_inspect/BridgePlanningTests/repair-entry-budget.py'),Path('_inspect/BridgePlanningTests/freeze-command-run.py')]
p=Path('Testmods/EnemyBridgePathTest/src/BridgeStructureCapture.cs');s=p.read_text(encoding='utf-8-sig')
for old,new in [('pending=input;emit(', 'CheckOwnTables(input);pending=input;emit('),('input.Sections.Add("post/players9",', 'CheckOwnTables(input);input.Sections.Add("post/players9",')]:
    if new not in s:
        assert old in s;s=s.replace(old,new)
p.write_bytes(s.replace('\n','\r\n').encode());paths.append(p)
paths.append(Path('Testmods/EnemyBridgePathTest/src/CopiedStructureInput.cs'))
paths.extend([Path('_inspect/BridgePlanningTests/loss-preflight.ps1'),Path('_inspect/BridgePlanningTests/ENTRY_BUDGET_20261013.md'),Path('_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_BRIDGE_ENTRY_CAPTURE.md')])
for target in [Path('Testmods/EnemyBridgePathTest/src/BridgeStructureCapture.cs'),Path('Testmods/EnemyBridgePathTest/src/CopiedStructureInput.cs')]:
    s=target.read_text(encoding='utf-8-sig')
    for old,new in [('NativePathfindingTableCopy.ProfileRva','0x322540'),('NativePathfindingTableCopy.PermissionsRva','0x32BDB0'),('NativePathfindingTableCopy.Types*NativePathfindingTableCopy.Classes*4','90*6*4'),('NativePathfindingTableCopy.Types*4','90*4')]:s=s.replace(old,new)
    target.write_bytes(s.replace('\n','\r\n').encode())
for p in paths:
    s=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    expected=s.replace('\n','\r\n').encode('utf-8');p.write_bytes(expected)
    assert p.read_bytes()==expected and b'\n' not in expected.replace(b'\r\n',b'')
print('PASS CRLF',len(paths),'explicit files')
