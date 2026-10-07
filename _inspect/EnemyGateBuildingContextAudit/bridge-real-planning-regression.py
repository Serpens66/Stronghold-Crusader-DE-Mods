"""Frozen real planning, continuous product replay and numeric command transport."""
from pathlib import Path
import hashlib,importlib.util,json,re,subprocess
base=Path(__file__).parent;workspace=base.parent.parent;folder=base/'real-plan-20261007-151016'
manifest=json.loads((folder/'manifest.json').read_text())
for f in manifest['files']:assert hashlib.sha256((folder/f['file']).read_bytes()).hexdigest().upper()==f['sha256']
spec=importlib.util.spec_from_file_location('analysis',base/'bridge-log-analysis.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
r=a.analyze((folder/'Log_264.log').read_bytes());assert r['bridgeComplete'] and not r['missing'] and not r['reconstructionErrors']
end=next(f for _,f in r['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='1837071' and end['commandPre']==end['commandPost']=='36747'
assert r['artifactSummary']['completed']==2 and r['artifactSummary']['deliveryComplete']
assert r['virtualSummary']['rejected']==239 and r['virtualSummary']['cancelled']==10 and not r['coverage']['shadowCoverageComplete']
assert r['coverage']['coherentTopologyCaptures']==105
exe=(workspace/'Testmods/EnemyBridgePathTest/tests/bin/EnemyBridgePathTest.PolicyTests.exe').resolve()
mod=workspace/'Testmods/EnemyBridgePathTest'
results={}
for name,args in [('planning',['--planning-variants',str((folder/'planning.bin').resolve())]),('linked',['--planning-group-check',str((folder/'planning.bin').resolve()),str((folder/'group.bin').resolve())]),('geometry-plan',['--replay',str((folder/'planning.bin').resolve())]),('geometry-group',['--replay',str((folder/'group.bin').resolve())]),('geometry-plan-other-mode',['--replay-alternate-mode',str((folder/'planning.bin').resolve())]),('geometry-group-other-mode',['--replay-alternate-mode',str((folder/'group.bin').resolve())])]:
 c=subprocess.run([str(exe)]+args,cwd=mod,capture_output=True,text=True);assert c.returncode==0,(name,c.stdout,c.stderr);results[name]=c.stdout
 (folder/(name+'-result.txt')).write_bytes(c.stdout.replace('\r\n','\n').replace('\n','\r\n').encode())
assert 'seedChanged=0,distanceChanged=0' in results['planning'] and 'effectiveCount=132415' in results['planning']
assert 'variant=only703,cutCells=15' in results['planning'] and 'seedChanged=4000,distanceChanged=34878' in results['planning'] and 'firstChange=CF360-to-D95E0-R8' in results['planning']
assert 'dirty=1' in results['geometry-plan'] and 'blocked-pending-topology' in results['geometry-plan'] and 'nativeRevision=43' in results['geometry-plan']
assert 'dirty=0' in results['geometry-group'] and 'nativeRevision=46' in results['geometry-group']
# Encode this historical command population using the new fixed numeric shape.
# Existing native/route/decision definitions are kept byte-for-byte.
prefix='[Info   :Enemy Bridge Path Test] [2026-10-07 15:11:46.500] bridge trace '
commands=[f for _,f in r['records'] if f['kind'] in ('command-pre','command-post')]
pre={f['op']:f for f in commands if f['kind']=='command-pre'};identities={};rows=[];out=[];serial=3000000
for f in commands:
 p=pre[f['op']];kind={'target':1,'move':2,'unit':3}[p['commandKind']];identity=(p['commandKind'],p['id'],p.get('global',p.get('unit','0/g0').split('/g')[-1]),p.get('type','unknown'))
 if identity not in identities:
  identities[identity]=str(len(identities)+90000);out.append(prefix+'seq='+str(serial)+',session=1,kind=text-definition,definition='+identities[identity]+',category=command-identity,text=[commandKind='+identity[0]+',id='+identity[1]+',global='+identity[2]+',type='+identity[3]+']');serial+=1
 xy=p['input'].split('/');start=p.get('start','-1/-1').split('/');pcl=p.get('pcl','-1->-1').split('->');post=f['kind']=='command-post'
 retained=f.get('retainedPre','');extra=retained.split(':a6=')[-1] if ':a6=' in retained else '0'
 data=[f[k] for k in ('seq','session','thread','tick','clock','physical','topology')]+['1' if post else '0',f['op'],f['parentEvent'],f['commandContext'],identities[identity],str(kind),p.get('tribe','0'),p.get('player','0'),p.get('command','0')]+xy+start+pcl+[extra,f.get('return','0'),f.get('regions','0'),f.get('searches','0'),f.get('failed','0'),'1' if p.get('promotion')=='bridge-route-observed' else '0',p.get('followingUnitOp','0'),'1' if p.get('nearBridge')=='True' else '0','1' if p.get('contextAttribution')=='native-decision' else '2' if p.get('contextAttribution')=='bridge-proximity' else '3']
 assert len(data)==31;[int(v) for v in data];rows.append('/'.join(data))
for line in (folder/'Log_264.log').read_text(encoding='utf-8-sig').splitlines():
 if 'kind=command-frame-batch,' in line or 'category=command-payload,' in line:continue
 out.append(line)
for i in range(0,len(rows),32):out.append(prefix+'seq='+str(serial+i)+',session=1,kind=command-frame-batch,rows=['+';'.join(rows[i:i+32])+';]')
out=[line for line in out if 'category=command-identity,' not in line and 'kind=command-frame-batch,' not in line]+[line for line in out if 'category=command-identity,' in line]+[line for line in out if 'kind=command-frame-batch,' in line]
wire=('\r\n'.join(out)+'\r\n').encode();converted=a.analyze(wire);assert converted['bridgeComplete'],(converted['missing'][:3],converted['reconstructionErrors'][:3],len(converted['torn']),converted['captureComplete'],converted['deliveryComplete'])
c=[f for _,f in converted['records'] if f['kind'] in ('command-pre','command-post')];cols=('session','kind','op','parentEvent','commandContext','commandKind','id','return','stage')
assert sorted(tuple(f.get(k,'') for k in cols) for f in c)==sorted(tuple(f.get(k,'') for k in cols) for f in commands)
assert converted['movementSummary']==r['movementSummary'] and converted['uniqueBridgeCommands']==r['uniqueBridgeCommands']
assert not a.analyze(wire.replace(b'category=command-identity',b'category=missing-identity',1))['bridgeComplete']
# Explicit skipped computation must not count as complete coverage.
skip=(prefix+'seq=9999999,session=1,kind=virtual-computation-coverage,skipped=8\r\n').encode();assert a.analyze(wire+skip)['virtualSummary']['skippedComputations']==8
report=dict(nativeCalls=1837071,pairedCommands=36747,hashesVerified=True,baseline='continuous-original-fields-and-selection-match',firstChange='CF360 -> D95E0 R8 True to False',only703SeedChanges=4000,only703DistanceChanges=34878,dirty=1,negativeEligible=False,newTargetChoice='Unknown: D9190 mode0 preserves old candidates; unrecorded consumer',originalTraceBytes=sum(r['volumes'].values()),numericCommandTraceBytes=sum(converted['volumes'].values()),detailedCommandObservations=len(commands),identityDefinitions=len(identities),costScope='offline-serialization-not-live-game',behaviorFix='disabled')
(folder/'regression-result.json').write_bytes((json.dumps(report,indent=2)+'\n').replace('\n','\r\n').encode());print('PASS real continuous planning and numeric command population:',report)
