"""Frozen same-save counterrun; read only fixtures, never select the newest process."""
from pathlib import Path
import hashlib, importlib.util
base=Path(__file__).parent
spec=importlib.util.spec_from_file_location('bridge_analysis',base/'bridge-log-analysis.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def fixture(name,sha):
    raw=(base/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==sha
    return a.analyze(raw)
down=fixture('bridge-20261006-133913.log','26ad1ec083b8a65835e77c012adc5e2e463dee64af35dc0d7797af282c79c4fd')
up=fixture('bridge-20261006-155247.log','d0de38d7bea5c0fdd41cdce44d8557106598236930805038f0cca5fc06098cbc')
assert up['captureComplete'] and not up['deliveryComplete'] and not up['bridgeFileComplete']
assert len(up['torn'])==1 and not up['missing'] and not up['reconstructionErrors']
assert not up['routeChains'] and not up['movementSummary']
def rows(run,kind):return [f for _,f in run['records'] if f.get('kind')==kind]
def plans(run):return [f for f in rows(run,'decision-state') if f.get('player')=='8']
u,d=plans(up),plans(down)
assert u[0]['entryPlan']==d[0]['entryPlan']=='[4/0/1/224222/543/489]'
assert u[0]['consumedPlan']=='[4/2/1/232127/531/502]' and d[0]['consumedPlan']=='[4/2/1/225464/545/491]'
assert u[3]['entryPlan'].startswith('[4/4/') and u[3]['consumedPlan'].startswith('[4/5/')
assert all(f['consumedPlan'].startswith('[4/5/') for f in u[3:])
assert any(f['entryPlan'].startswith('[4/4/') and f['consumedPlan'].startswith('[4/6/') for f in d)
for f in u:
    for row in f['accesses'].strip('[]').split(';'):
        if row:assert [int(x) for x in row.split('/')][8:13]==[0,1,0,0,1]
def seed(run,root):
    return next(f['args'] for f in rows(run,'native-enter') if f.get('site')=='seed-field' and int(root)<int(f['op'])<int(root)+220)
assert seed(up,u[0]['planningRoot'])=='[1/0/1/0/0/0]'
assert seed(down,d[0]['planningRoot'])=='[1/1/1/0/0/0]'
end=rows(up,'session-end')[0]
assert end['entered']==end['exited']=='2500812' and end['commandPre']==end['commandPost']=='40729'
assert end['criticalPending']=='53' and end['backgroundPending']=='14'
assert any(f.get('player')=='5' and f['consumedPlan'].startswith('[4/6/') for f in rows(up,'decision-state'))
assert sum(int(x['unitsWithEntryAndExit']) for x in down['movementSummary'] if str(x['player'])=='8')==20
print('PASS paired counterrun: first planning input/target, phase4->6 versus4->5, transient player5, capture/delivery/file boundaries')
