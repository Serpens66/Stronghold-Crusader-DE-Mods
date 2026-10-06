"""Frozen failed-install run: complete event delivery is not Native coverage."""
from pathlib import Path
import hashlib, importlib.util
base=Path(__file__).parent
spec=importlib.util.spec_from_file_location('bridge_analysis',base/'bridge-log-analysis.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
raw=(base/'bridge-20261006-202236.log').read_bytes()
assert hashlib.sha256(raw).hexdigest()=='2265cfea8d18ed975fac1c74b4245e4d01b08add2060bbec75ba7c899c41332d'
run=a.analyze(raw)
assert run['bridgeComplete'] and not run['fileComplete']
assert run['captureComplete'] and run['deliveryComplete'] and not run['missing']
assert not run['coverage']['nativeReady'] and not run['coverage']['nativeCallsObserved']
assert not run['coverage']['nativeCoverageComplete'] and not run['coverage']['shadowCoverageComplete']
assert run['virtualSummary']['inputs']==run['virtualSummary']['results']==0
end=next(f for _,f in run['records'] if f['kind']=='session-end')
assert end['commandPre']==end['commandPost']=='37258'
assert end['entered']==end['exited']==end['installedEntries']=='0'
assert b'NativeX64 contract mismatch: attack-phases' in raw
stable=a.analyze((base/'bridge-20261006-133913.log').read_bytes())
def states(r):return [f for _,f in r['records'] if f['kind']=='bridge-physical' and f['bridge'].startswith('703/')]
def cells(f):return {int(row.split('/')[0]):row.split('/') for row in f['cells'].strip('[]').split(';') if row}
old=states(stable)
deck={tile for tile,v in cells(old[0]).items() if any(tile in cells(f) and int(v[3],16)^int(cells(f)[tile][3],16)==0x40000000 for f in old[1:])}
assert len(deck)==15
settled=[cells(f) for f in states(run) if all(cells(f)[tile][1]=='1' and not int(cells(f)[tile][3],16)&0x40000000 for tile in deck)]
assert settled and not run['routeChains']
assert sum(run['volumes'].values())==704197
print('PASS unavailable Native regression:37258 paired commands, complete Bridge delivery, no Native/shadow coverage, settled lowered deck')
