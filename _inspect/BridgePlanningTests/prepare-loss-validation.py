from pathlib import Path
import hashlib,json,importlib.util
root=Path('Testmods/EnemyBridgePathTest')
files=[root/'src'/n for n in ['BridgeBuildingIndex.cs','BridgeDecisionTrace.cs','BridgePlanningCapture.cs','BridgeStructureTrace.cs','BridgeStructureCapture.cs','BridgeDiagnostics.cs','BridgeRouteTrace.cs','BridgeVirtualShadow.cs','BridgePlanningImporter.cs','CopiedStructureInput.cs','BridgeLossHistory.cs']]
files += [root/'tests'/n for n in ['Program.cs','StructureCaptureTests.cs','BridgeLossTests.cs']]
files += [root/n for n in ['EnemyBridgePathTest.csproj','EnemyBridgePathTest.PolicyTests.csproj']]
files += [root/'verify.ps1']
files += [Path('_inspect/BridgePlanningTests')/n for n in ['Program.cs','loss-preflight.ps1','apply-loss-integration.py','finish-loss-edits.py','finish-loss-association.py','prepare-loss-validation.py']]
files += [Path('_inspect/EnemyGateBuildingContextAudit')/n for n in ['bridge-loss-audit.py','bridge-log-analysis.py']]
for path in files:
 original=path.read_text(encoding='utf-8-sig'); expected=original.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');path.write_bytes(expected)
 assert path.read_bytes()==expected and b'\n' not in expected.replace(b'\r\n',b'')
print('PASS CRLF exact targeted files',len(files))
folder=Path('_inspect/BridgePlanningTests/historical-20261013-152715');folder.mkdir(exist_ok=True)
log=folder/'LogOutput.log'
raw=log.read_bytes() if log.exists() else Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log').read_bytes()
if not log.exists():log.write_bytes(raw)
s=importlib.util.spec_from_file_location('analysis','_inspect/EnemyGateBuildingContextAudit/bridge-log-analysis.py');a=importlib.util.module_from_spec(s);s.loader.exec_module(a)
text=raw.decode('utf-8-sig');starts=list(a.START.finditer(text));selected=[]
for i,start in enumerate(starts):
 section=text[start.start():starts[i+1].start() if i+1<len(starts) else len(text)].encode('utf-8');r=a.analyze(section)
 if r['bridgeComplete'] and r['sessionSummary'] and r['sessionSummary'][0]['commandAccounting']['pre']==7312:selected.append((section,r))
assert len(selected)==1,'Expected explicitly identified historical process; newer starts remain separate'
section,r=selected[0]
summary={k:r[k] for k in ['sessionSummary','coverage','lossSummary','movementSummary','artifactSummary','captureComplete','deliveryComplete','bridgeComplete']}
summary.update(logSha256=hashlib.sha256(raw).hexdigest().upper(),processSectionSha256=hashlib.sha256(section).hexdigest().upper())
(folder/'summary.json').write_bytes(json.dumps(summary,indent=2).replace('\n','\r\n').encode())
print('Frozen latest process',summary['logSha256'],summary['processSectionSha256'])
