"""Stable lowered bridge: exact decision/order/path/execution and transport round trip."""
from pathlib import Path
import hashlib,importlib.util,collections

root=Path(__file__).parent
spec=importlib.util.spec_from_file_location('analysis',root/'bridge-log-analysis.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
raw=(root/'bridge-20261006-133913.log').read_bytes()
assert hashlib.sha256(raw).hexdigest().upper()=='26AD1EC083B8A65835E77C012ADC5E2E463DEE64AF35DC0D7797AF282C79C4FD'
r=a.analyze(raw);assert r['bridgeComplete'] and r['fileComplete'] and not r['missing'] and not r['reconstructionErrors']
end=next(f for _,f in r['records'] if f['kind']=='session-end')
assert end['entered']==end['exited']=='2118706' and end['commandPre']==end['commandPost']=='36923'
assert 'lower:1;raise:0;' in end['siteCalls'] and end['invalidIds']=='0'
decisions={f['definition']:f for _,f in r['records'] if f['kind']=='decision-state'}
assert decisions['5']['entryPlan']=='[4/0/1/224222/543/489]' and decisions['5']['consumedPlan']=='[4/2/1/225464/545/491]'
assert decisions['12']['entryPlan']=='[4/4/1/225464/545/491]' and decisions['12']['consumedPlan']=='[4/6/1/225464/545/491]'
assert '/102/1/1/1/102/102/1;' in decisions['12']['accesses']
chain=next(c for c in r['routeChainGroups'] if c['planningRoot']=='710831')
assert (chain['decision'],chain['groups'],chain['commands'],chain['units'],chain['observations'],chain['linkedObservations'],chain['positiveAccessObservations'])==('12',4,25,25,83,83,83)
assert len(r['routeChains'])==535 and sum(c['linkedObservations'] for c in r['routeChainGroups'])==511
expected={'3':24,'4':23,'5':1,'7':23,'8':20}
assert {v['player']:v['unitsWithEntryAndExit'] for v in r['movementSummary']}==expected
assert sum(v['observations'] for v in r['movementSummary'])==364
assert all(v['unmatchedEntries']==v['unmatchedExits']==0 for v in r['movementSummary'])
for _,f in r['records']:
    if f['kind']=='bridge-physical' and f['bridge']=='703/g2432893':
        cells=[v.split('/') for v in f['cells'].strip('[]').split(';') if v]
        assert [v[2] for v in cells[5:20]]==list(map(str,[1141,1145,1146,1147,1164,1165,1169,1927,1928,2034,2044,2041,2037,2038,2035]))

# Re-encode exactly the production fixed numeric native frame and shared command
# context. Compare semantic records and on-disk bytes, not synthetic row counts.
lines=[];contexts={};context_id=0
for line,f in r['records']:
    # Expanded background path rows already have a transport record in the source.
    if f['kind']=='stored-route' and 'kind=stored-route-background-batch' in line:continue
    prefix=line.split('bridge trace ',1)[0]+'bridge trace '
    envelope=','.join(k+'='+f[k] for k in ('seq','session','thread','tick','clock','physical','topology'))
    if f['kind'] in ('native-enter','native-exit'):
        stamp=[f.get(k,'0') for k in ('global','owner','leader','state16','phase16','retainedTarget16','targetGlobal')]
        values=[f['op'],f['parent'],*f['args'].strip('[]').split('/'),f['scopeSession'],f['state'],*stamp,
                '1' if f.get('completed')=='True' else '0','v' if f.get('observedReturn','void')=='void' else f['observedReturn'],f.get('regions','0'),
                *f.get('retainedEntryPlanStamp','[0/0/0/0/0/0]').strip('[]').split('/')]
        assert len(values)==26
        line=prefix+envelope+',kind=native-frame,phase='+('pre' if f['kind']=='native-enter' else 'post')+',site='+f['site']+',rva='+f['rva']+',values=['+'/'.join(values)+'],contextDefinition='+f['contextDefinition']
    elif f['kind'] in ('command-pre','command-post'):
        names=('parent','priorPlayerPlan','priorPlanPhysical','priorPlanTopology','state')
        key=tuple(f[n] for n in names)
        if key not in contexts:
            context_id+=1;contexts[key]=str(context_id)
            lines.append(prefix+envelope+',kind=command-context,definition='+str(context_id)+',values=['+'/'.join(key)+'],priorLink=chronological-not-proven-causality')
        # Preserve field contents, including promoted frozen Pre fields.
        fields={k:v for k,v in f.items() if k not in names and k not in ('seq','session','thread','tick','clock','physical','topology','kind','priorLink')}
        line=prefix+envelope+',kind='+f['kind']+',commandContext='+contexts[key]+','+','.join(k+'='+v for k,v in fields.items())
    lines.append(line)
encoded=('\r\n'.join(lines)+'\r\n').encode();compact=a.analyze(encoded)
assert compact['bridgeComplete'] and compact['routeChainGroups']==r['routeChainGroups'] and compact['movementSummary']==r['movementSummary']
assert compact['uniqueBridgeCommands']==r['uniqueBridgeCommands'] and compact['uniqueBridgeUnits']==r['uniqueBridgeUnits']
old=sum(r['volumes'].values());new=sum(compact['volumes'].values());assert new<old

# A retained plan with accesses=[] is not positive access evidence, even with a
# perfect command/path association. The old fixture's synthetic link tests this.
a.self_test()
print('PASS stable: 2118706 calls; 36923 commands; 535 route observations/511 linked; 91 executed crossing units/364 movement observations')
print('Fixed numeric transport including prefixes:',old,'->',new,'saved',old-new,'; active-run serialization is not the quiet-volume acceptance test')
