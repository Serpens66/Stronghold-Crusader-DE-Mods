"""Dirty decision blocker and lossless compact command/search transport."""
from pathlib import Path
import hashlib,importlib.util,json,collections
base=Path(__file__).parent
spec=importlib.util.spec_from_file_location('analysis',base/'bridge-log-analysis.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
raw=(base/'bridge-dirty-20261007-141153.log').read_bytes()
assert hashlib.sha256(raw).hexdigest().upper()=='6CDB31CAAE0C7BF454ED2B25F7C1455E4FCE75E86DABD7EC031808ADDE660F51'
assert hashlib.sha256((base/'bridge-dirty-20261007-full.log').read_bytes()).hexdigest().upper()=='D1472AF541E187C9ECDA96AAC9228ECDDB79426A1FD463FCBE7CBF60279992AC'
r=a.analyze(raw);assert r['bridgeComplete'] and not r['missing'] and not r['reconstructionErrors']
end=next(f for _,f in r['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='1673891' and end['commandPre']==end['commandPost']=='34009'
assert r['virtualSummary']['rejected']==100 and r['virtualSummary']['cancelled']==21 and not r['coverage']['shadowCoverageComplete']
assert r['coverage']['coherentTopologyCaptures']==97
keeps=[f for _,f in r['records'] if f['kind']=='virtual-shadow-input' and f.get('stage')=='keep-access' and f.get('player')=='8']
assert len(keeps)==4 and all(f['captureAvailable']=='False' for f in keeps)
decisions=[f for _,f in r['records'] if f['kind']=='decision-state' and f['player']=='8']
assert [f['entryPlan'].split('/')[1] for f in decisions[:4]]==['0','2','3','4']
assert decisions[3]['consumedPlan'].split('/')[1]=='6'
# Exercise identical transported definitions/envelopes while preserving all original other records.
prefix='[Info   :Enemy Bridge Path Test] [2026-10-07 14:13:14.000] bridge trace '
commands=[];searches=[];payloads={};output=[];serial=2000000
seen=set()
for line,f in r['records']:
 if f['kind'] in ('command-pre','command-post'):
  start=line.index(',op=')+1;detail=line[start:];parts=detail.split(',',2);assert parts[0].startswith('op=') and parts[1].startswith('parentEvent=')
  payload=parts[2]
  if payload not in payloads:
   payloads[payload]=str(len(payloads)+9000)
   output.append(prefix+'seq='+str(serial)+',session='+f['session']+',kind=text-definition,definition='+payloads[payload]+',category=command-payload,text=['+payload+']');serial+=1
  row=[f[k] for k in ('seq','session','thread','tick','clock','physical','topology')]+['0' if f['kind']=='command-pre' else '1',f['op'],f['parentEvent'],f['commandContext'],payloads[payload]]
  commands.append('/'.join(row))
 elif f['kind']=='native-enter' or f['kind']=='native-exit':continue # keep original batch below
 else:
  # Expanded parser records sharing one physical batch must be included exactly once.
  if line not in seen:output.append(line);seen.add(line)

# Native transport is retained unchanged, not synthesized from expanded records.
for line in raw.decode('utf-8-sig').splitlines():
 if 'kind=native-frame-batch,' in line:output.append(line)
for i in range(0,len(commands),32):output.append(prefix+'seq='+str(serial+i)+',session=1,kind=command-frame-batch,rows=['+';'.join(commands[i:i+32])+';]')
compact=('\r\n'.join(output)+'\r\n').encode();c=a.analyze(compact)
assert not c['missing'] and not c['reconstructionErrors'],(c['missing'][:3],c['reconstructionErrors'][:3])
originalCommands=[f for _,f in r['records'] if f['kind'] in ('command-pre','command-post')]
newCommands=[f for _,f in c['records'] if f['kind'] in ('command-pre','command-post')]
columns=('session','kind','op','parentEvent','commandContext','parent','state','commandKind','id','command','input','return','stage')
assert sorted(tuple(f.get(k,'') for k in columns) for f in originalCommands)==sorted(tuple(f.get(k,'') for k in columns) for f in newCommands)
# A definition omitted or a row cut must be detected, rather than treated as complete.
broken=compact.replace(b'category=command-payload',b'category=missing-payload',1);assert not a.analyze(broken)['bridgeComplete']
assert not a.analyze(compact.replace(b'kind=command-frame-batch,rows=[',b'kind=command-frame-batch,rows=[broken;',1))['bridgeComplete']
# The following number measures serialization of this trace only, not live-frame costs.
report=dict(processHash=hashlib.sha256(raw).hexdigest().upper(),originalBridgeBytes=sum(r['volumes'].values()),compactBridgeBytes=sum(c['volumes'].values()),pairedCommands=34009,transportedCommandObservations=len(commands),nativeCalls=1673891,rejected=100,cancelled=21,behaviorFix=False)
(base/'bridge-dirty-regression-result.json').write_bytes((json.dumps(report,indent=2).replace(chr(10),chr(13)+chr(10))+chr(13)+chr(10)).encode())
print('PASS dirty blocker, phase4 selection, exact command transport; '+json.dumps(report))

# Fixed search schema roundtrip: nullable native result, signed values and full64 calls.
p='[Info   :Enemy Bridge Path Test] [2026-10-07 14:13:14.000] bridge trace '
search=((p+'seq=1,session=1,kind=text-definition,definition=8,category=search-source,text=[builder]')+chr(13)+chr(10)+(p+'seq=2,session=1,kind=search-result-batch,rows=[3/1/15/1492/123456/1/15/210/667836/8/0/0/-1/4294967297/1;4/1/15/1492/123457/1/15/211/667836/8/1/-2/0/1/0;]')+chr(13)+chr(10)).encode()
s=a.analyze(search);values=[f for _,f in s['records'] if f['kind']=='search-result'];assert len(values)==2 and values[0]['native']=='unobserved' and values[0]['nativeCalls']=='4294967297' and values[1]['native']=='-2' and values[1]['completed']=='False'
print('PASS fixed search transport reconstructs nullable and signed results with full64 call counts')
