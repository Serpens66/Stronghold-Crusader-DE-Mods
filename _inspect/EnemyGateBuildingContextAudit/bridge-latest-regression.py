"""Frozen 23:22 trace: counts, provenance and compact transport round trip."""
from pathlib import Path
import hashlib, importlib.util, collections

root=Path(__file__).parent
spec=importlib.util.spec_from_file_location('bridge_analysis',root/'bridge-log-analysis.py')
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
raw=(root/'bridge-232200.log').read_bytes()
assert hashlib.sha256(raw).hexdigest().upper()=='5AEF0B8E216C0E42E2E5AE63EB26A34AEC440E7762DFE221E7F0CC37CDCAF9D8'
result=analysis.analyze(raw)
assert result['bridgeComplete'] and not result['fileComplete']
assert not result['missing'] and not result['reconstructionErrors']
end=next(f for _,f in result['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='1695766'
assert end['commandPre']==end['commandPost']=='34661'
assert sum(c['linkedObservations'] for c in result['routeChainGroups'])==105
p8=next(c for c in result['routeChainGroups'] if c['planningRoot']=='1336380')
assert (p8['decision'],p8['groups'],p8['commands'],p8['units'],p8['observations'])==('28',4,25,25,52)
decisions={f['definition']:f for _,f in result['records'] if f['kind']=='decision-state'}
assert decisions['28']['entryPlan']=='[4/5/1/232127/531/502]' and decisions['28']['consumedPlan']=='[4/6/1/232127/531/502]'
assert '/103/1/1/1/103/103/1;' in decisions['28']['accesses']
assert decisions['31']['entryPlan']=='[4/6/1/232127/531/502]' and decisions['31']['consumedPlan']=='[4/5/1/232127/531/502]'
assert '/107/31/0/1/0/0/1;' in decisions['31']['accesses']
path=next(f for _,f in result['records'] if f['kind']=='stored-route' and f['definition']=='523')
assert '703/2432893/64/66/603/477/605/473/deck-without-parent-footprint;' in path['bridges']
assert not any(f['kind'] in ('bridge-movement','route-command-boundary') for _,f in result['records'])

# Replay only the changed transport; decisions and relevant raw paths stay byte
# identical. This measures serialization, not a simulated native workload.
lines=[];paths=[];replaced=[];repeat_totals=collections.Counter()
path_records={f['definition']:f for _,f in result['records'] if f['kind']=='stored-route'}
bindings={}
for _,f in result['records']:
    if f['kind']=='route-bindings':
        for row in f['rows'].strip('[]').split(';'):
            if row:
                v=row.split('/');bindings[v[0]]=v
def batch(kind,rows,extra=''):
    for i in range(0,len(rows),32):
        lines.append('[Info   :Enemy Bridge Path Test] [2026-10-04 23:23:12.192] bridge trace seq=9999999,session=1,kind='+kind+',rows=['+';'.join(rows[i:i+32])+';]'+extra)
for line,f in result['records']:
    kind=f['kind']
    if kind=='stored-route' and f['complete']=='True' and f['bridges']=='[]':
        paths.append('/'.join([f['definition'],f['captureClock'],*f['origin'].split('/'),f['length'],f['cursor'],f['flags'],f['substep'],*f['decodedEndpoint'].split('/'),*f['nativeSegmentTarget'].split('/')]))
    elif kind=='route-command-replaced':
        unit,global_id=f['unit'].split('/g');replaced.append('/'.join([unit,global_id,f['oldCommand'],f['newCommand'],f['clock']]))
    elif kind=='route-repeat-batch':
        retained=[]
        for row in f['rows'].strip('[]').split(';'):
            if not row:continue
            v=row.split('/');p=path_records[v[1]]
            if p['complete']=='True' and p['bridges']=='[]':repeat_totals[bindings[v[0]][3]]+=int(v[2])
            else:retained.append(row)
        if retained:batch(kind,retained)
    else:lines.append(line)
batch('stored-route-background-batch',paths,',complete=True,bridges=[],packedHex=,execution=not-proven,envelopeTiming=batch-flush,physicalAtCapture=not-recorded')
batch('route-replacement-batch',replaced,',samePlanMustBeObserved=True,envelopeTiming=batch-flush')
batch('route-background-repeat-batch',[p+'/'+str(n) for p,n in repeat_totals.items()],',coverage=complete-decoded-no-deck-observation-attempts-not-movement')
compact_raw=('\r\n'.join(lines)+'\r\n').encode()
compact=analysis.analyze(compact_raw)
assert compact['bridgeComplete']
assert compact['routeChainGroups']==result['routeChainGroups']
assert compact['definitions']==result['definitions']
assert len(compact['routeReplacements'])==len(replaced)
assert compact['backgroundRouteRepeats']=={int(p):n for p,n in repeat_totals.items()}
old=sum(result['volumes'].values());new=sum(compact['volumes'].values())
assert new<old
print('PASS frozen latest: 1695766 calls, 34661 commands, 105 linked observations; player8 four groups/25 commands/52 observations; exact definitions and relevant paths survive compact transport')
print('Transport-only replay bytes including prefixes:',old,'->',new,'saved',old-new,'; changed-run replay is not the quiet-volume acceptance test')
