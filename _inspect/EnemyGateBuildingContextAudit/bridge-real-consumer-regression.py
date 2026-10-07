"""Frozen real consumer evidence and lossless route-format transport regression."""
from pathlib import Path
import hashlib,importlib.util,json,subprocess,re
base=Path(__file__).resolve().parent;workspace=base.parents[1];frozen=base/'real-consumer-20261007-171558'
spec=importlib.util.spec_from_file_location('analysis',base/'bridge-log-analysis.py');analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
for row in json.loads((frozen/'SHA256.json').read_text()):
 assert hashlib.sha256((frozen/row['name']).read_bytes()).hexdigest().lower()==row['sha256'].lower()
raw=(frozen/'process-section.log').read_bytes();result=analysis.analyze(raw)
assert result['bridgeComplete'] and not result['missing'] and not result['reconstructionErrors']
end=next(f for _,f in result['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='3776002' and end['commandPre']==end['commandPost']=='66689'
exe=workspace/'Testmods/EnemyBridgePathTest/tests/bin/EnemyBridgePathTest.PolicyTests.exe'
for switch,code,needle in [('--planning-replay',0,'planningBaselineMatched=True'),('--consumer-replay',0,'fullMilitaryConsumer=Unknown'),('--consumer-closure',2,'missing-native-inputs')]:
 c=subprocess.run([str(exe),switch,str(frozen/'planning.bin')],cwd=exe.parents[2],capture_output=True,text=True)
 assert c.returncode==code and needle in c.stdout,(switch,c.returncode,c.stdout,c.stderr)
# Model only the production format change. This is not a measured game run.
contract='format=2,completeness=decoded-stored-transitions,consistency=single-copy-header-stability-only,bridgeColumns=building/global/firstStep/lastStep/entryX/entryY/exitX/exitY/classification,packedEncoding=low-nibble-first,parentAssociation=candidate-unless-native-link,execution=not-proven,coverage=stored-plan-only'
constants=contract.split(',')[1:];lines=[];sessions=set()
for line in raw.decode('utf-8-sig').splitlines():
 if 'kind=stored-route,' in line:
  session=re.search(r'session=(\d+)',line).group(1)
  if session not in sessions:
   sessions.add(session);prefix=line[:line.index('bridge trace')];lines.append(prefix+'bridge trace seq=0,session='+session+',kind=route-format,'+contract)
  line=line.replace('kind=stored-route,','kind=stored-route,format=2,')
  for value in constants:line=line.replace(','+value,'')
 lines.append(line)
compact=analysis.analyze(('\r\n'.join(lines)+'\r\n').encode())
for key in ('routeChainGroups','movementSummary','uniqueBridgeCommands','uniqueBridgeUnits','definitions','missing','reconstructionErrors','bridgeComplete'):
 assert compact[key]==result[key],key
before=sum(result['volumes'].values());after=sum(compact['volumes'].values());assert after<before
print('PASS frozen3776002 calls/66689 commands, hashes and explicit missing historical inputs')
print('PASS lossless route-format wire model with prefixes:',before,'->',after,'saved',before-after,'; active replay is not quiet background acceptance')
