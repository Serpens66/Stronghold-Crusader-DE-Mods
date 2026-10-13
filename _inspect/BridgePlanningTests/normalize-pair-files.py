from pathlib import Path
files=['BridgeVirtualShadow.cs','BridgeDecisionTrace.cs','BridgeStructureTrace.cs','BridgeInputArtifact.cs','BridgePlanningCapture.cs','BridgePlanningImporter.cs','BridgeStructureCapture.cs','CopiedStructureInput.cs','CopiedMilitaryEntry.cs','CopiedPlanningReplay.cs']
paths=[Path('Testmods/EnemyBridgePathTest/src')/p for p in files]
paths += [Path('Testmods/EnemyBridgePathTest/tests')/p for p in ['Program.cs','ShadowControlTests.cs','StructureCaptureTests.cs','TraceTransportTests.cs']]
paths += [Path('_inspect/BridgePlanningTests')/p for p in ['BridgePlanningTests.csproj','Program.cs','HistoricalInputs.cs','OwnCommandInputs.cs','freeze-pair-run.py','finish-pair-edits.py','normalize-pair-files.py']]
paths.append(Path('_inspect/EnemyGateBuildingContextAudit/bridge-loss-audit.py'))
paths.append(Path('_inspect/BridgePlanningTests/loss-preflight.ps1'))
paths.extend([Path('Testmods/EnemyBridgePathTest/verify.ps1'),Path('_inspect/BridgePlanningTests/PAIR_CAPTURE_20261013.md')])
paths.append(Path('_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_BRIDGE_PAIR_INPUTS.md'))
for p in paths:
 s=p.read_text(encoding='utf-8-sig').replace('\r\n','\n');expected=s.replace('\n','\r\n').encode('utf-8');p.write_bytes(expected)
 assert p.read_bytes()==expected and b'\n' not in expected.replace(b'\r\n',b'')
print('PASS CRLF',len(paths),'explicit pair files')
