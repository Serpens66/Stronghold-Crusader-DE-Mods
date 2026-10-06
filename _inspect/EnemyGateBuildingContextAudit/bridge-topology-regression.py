"""Empirical local boundary validation; no claim of full-map reachability."""
from pathlib import Path
import importlib.util,json
root=Path(__file__).parent
s=importlib.util.spec_from_file_location('a',root/'bridge-log-analysis.py');a=importlib.util.module_from_spec(s);s.loader.exec_module(a)
r=a.analyze((root/'bridge-20261006-133913.log').read_bytes())
physical={f['definition']:f for _,f in r['records'] if f['kind']=='bridge-physical'}
raised=physical['7'];lowered=physical['31']
assert raised['bridge']==lowered['bridge']=='703/g2432893'
assert raised['gateIdRaw']==lowered['gateIdRaw']=='0'
assert raised['parentLink']==lowered['parentLink']=='unique-footprint-adjacency-candidate'
old=[v.split('/') for v in raised['cells'].strip('[]').split(';') if v]
new=[v.split('/') for v in lowered['cells'].strip('[]').split(';') if v]
assert len(old)==len(new)==49
coords=[(600+i%5,473+i//5) for i in range(25)]+[(x,y) for y in range(472,479) for x in range(599,606) if not (600<=x<=604 and 473<=y<=477)]
closed=set(coords[5:20]);dx=(0,1,1,1,0,-1,-1,-1);dy=(-1,-1,0,1,1,1,0,-1)
for xy,before,after in zip(coords,old,new):
    assert before[0]==after[0] and before[2]==after[2]
    mask=0 if xy in closed else int(after[4])
    for direction in range(8):
        if (xy[0]+dx[direction],xy[1]+dy[direction]) in closed:mask&=~(1<<direction)
    assert mask==int(before[4]),(xy,mask,before[4])
    if xy in closed:assert int(before[3],16)&0x40000000 and not int(after[3],16)&0x40000000

# A directed synthetic tile graph documents alternative-route and equality
# requirements. It is deliberately not a native graph implementation.
def reachable(edges,start,target,blocked):
    if start in blocked or target in blocked:return False
    visited={start};pending=[start]
    while pending:
        tile=pending.pop()
        if tile==target:return True
        for other in edges.get(tile,()):
            if other not in visited and other not in blocked:visited.add(other);pending.append(other)
    return False
assert not reachable({1:[2],2:[3]},1,3,{2})
assert reachable({1:[2,4],2:[3],4:[5],5:[3]},1,3,{2})
assert not reachable({1:[2],2:[3]},3,1,set())
assert reachable({1:[2],2:[3]},1,3,set())
assert not reachable({1:[2],2:[3]},1,3,{2,4})
# 107160 consumes a signed-short record kind and its caller consumes AL only.
def seed_special(tile_record_id,kind):
    signed=kind if kind<32768 else kind-65536
    return tile_record_id!=0 and signed>4 and kind!=15
assert not seed_special(0,6) and not seed_special(1,4) and not seed_special(1,15)
assert seed_special(1,5) and seed_special(1,16) and not seed_special(1,0x8000)
contracts=json.loads((root/'bridge-virtual-contracts.json').read_text())
assert contracts['nativeHash']==json.loads(Path('_inspect/CrusaderDE-Native-Baseline/CURRENT.json').read_text())['currentNativeHash']
assert len(contracts['functions'])==42
print('PASS:49 observed boundary cells including diagonals match the deck cut; directed alternatives and signed special seeds tested')
print('LIMIT:only local boundary proven; full packed map, gate-endpoint remapping, closed-connection fallback and parent authorization remain prerequisites')
