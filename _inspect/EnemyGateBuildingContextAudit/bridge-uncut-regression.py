"""Hash-bound production replay of the fully delivered October 6 process."""
from pathlib import Path
import sys,hashlib,importlib.util,subprocess,json
sys.dont_write_bytecode=True
base=Path(__file__).resolve().parent
def module(name,file):
    spec=importlib.util.spec_from_file_location(name,base/file)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
a=module('logs','bridge-log-analysis.py');binary=module('binary','bridge-artifact-analysis.py')
raw=(base/'bridge-20261006-235642.log').read_bytes()
assert hashlib.sha256(raw).hexdigest().upper()=='A6DD8FB7EE22F4F144F0FA1D82499C7C8DB4E739FCEBE0E8F2FFAA0F40549675'
r=a.analyze(raw);a.self_test()
assert r['bridgeComplete'] and r['fileComplete'] and not r['missing'] and not r['reconstructionErrors']
assert r['coverage']['nativeCoverageComplete'] and r['coverage']['coherentTopologyCaptures']==57
assert r['coverage']['shadowResultsDelivered'] and not r['coverage']['shadowCoverageComplete']
assert not r['coverage']['selectedShadowComputationComplete']
assert r['virtualSummary']['results']==257 and r['virtualSummary']['rejected']==154 and r['virtualSummary']['cancelled']==3
assert next(v for v in r['movementSummary'] if v['player']=='8')['unitsWithEntryAndExit']==25
end=next(f for _,f in r['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='982868' and end['commandPre']==end['commandPost']=='21456'
assert any(c['planningRoot']=='828295' and c['decision']=='12' and c['linkedObservations']==30 for c in r['routeChainGroups'])
exe=base.parents[1]/'Testmods/EnemyBridgePathTest/tests/bin/EnemyBridgePathTest.PolicyTests.exe'
report=dict(logHash=hashlib.sha256(raw).hexdigest(),coverage=r['coverage'],virtual=r['virtualSummary'],movement=r['movementSummary'],routeChains=r['routeChainGroups'],replays=[])
for definition,digest in [(174,'C1765553075387D23183D792726DAC98BF1DEA8E6B444C908EE6C8325C0283FC'),(204,'3E5D974B7684C5F00490E2DE1D0BCBC388BB50A37EFEA7EACD798BF1A83C6121')]:
    path=base/'bridge-inputs-20261006-235642'/f'bridge-1-{definition}.bin'
    meta,sections,actual=binary.decode(path);assert actual.upper()==digest
    assert meta['op']==str(670225 if definition==174 else 828296)
    for flag in ['--replay','--replay-alternate-mode']:
        replay=subprocess.run([str(exe),flag,str(path)],check=True,capture_output=True,text=True)
        controls=[line for line in replay.stdout.splitlines() if line.startswith('virtual-control:')]
        assert len(controls)==2 and all('noCut=Reachable,only703=NoRoute,allHostile=NoRoute' in line for line in controls)
        assert all('policyResult=Unknown' in line and 'cutCells=0/15/45' in line for line in controls)
        if definition==174 and flag=='--replay':assert all('macroMatchesNative=True,geometryMatchesNative=True,cutAssessment=blocked-missing-validity-metadata' in line for line in controls)
        if flag=='--replay-alternate-mode':assert all('modeEvidence=hypothesis' in line for line in controls)
        (base/f'bridge-replay-{definition}-{flag[2:]}.txt').write_bytes(replay.stdout.replace('\r\n','\n').replace('\n','\r\n').encode())
        cost=next(line for line in replay.stdout.splitlines() if line.startswith('offline-cost:'))
        assert 'preparationHits=3' in cost
        report['replays'].append(dict(definition=definition,hash=actual,mode=flag,cost=cost,controls=controls))
(base/'bridge-uncut-results.json').write_bytes(json.dumps(report,indent=2).replace('\n','\r\n').encode())
print('PASS frozen 982868 calls/21456 pairs; delivered is not computed coverage; both original inputs reachable uncut in both directions/modes, single703/all-hostile cuts NoRoute with native C endpoints, policy remains conservative')
