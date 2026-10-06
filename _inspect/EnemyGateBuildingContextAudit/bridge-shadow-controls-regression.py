"""Freeze the repaired Native run independently of later appended game processes."""
from pathlib import Path
import hashlib, importlib.util
base=Path(__file__).parent
spec=importlib.util.spec_from_file_location('analysis',base/'bridge-log-analysis.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
raw=(base/'bridge-20261006-204752.log').read_bytes()
assert hashlib.sha256(raw).hexdigest()=='463a8978b48bfe188947ea2481432b1938b20b366a75a887280bac56daa4c1c3'
r=a.analyze(raw)
assert r['captureComplete'] and r['fileComplete'] and not r['deliveryComplete']
assert not r['missing'] and not r['reconstructionErrors']
assert r['coverage']['nativeReady'] and r['coverage']['coherentTopologyCaptures']==31
assert r['virtualSummary']['inputs']==r['virtualSummary']['results']==171
assert r['virtualSummary']['geometricResults']==dict(Unknown=84,Reachable=74,NoRoute=13)
assert r['virtualSummary']['policyUnknown']==171
end=next(f for _,f in r['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='1136667'
assert end['commandPre']==end['commandPost']=='23246'
assert end['queue']=='90'
case=next(f for _,f in r['records'] if f['kind']=='virtual-shadow' and f['op']=='676422')
assert case['player']=='8' and case['geometricResult']=='NoRoute' and case['candidateCutCells']=='45'
assert case['nativeQueryOrder']=='keep-target-to-attacker' and case['comparisonDirection']=='attacker-to-target'
assert next(f for f in r['movementSummary'] if f['player']=='8')['unitsWithEntryAndExit']==25
assert any(c.get('decisionMatches') and c.get('rootRecorded') and c.get('groupRecorded') for c in r['routeChains'])
a.self_test()
print('PASS repaired run:1136667 calls,23246 pairs,171 shadows,25 moving player8 units; 90 pending delivery; cut45 is not validated')
