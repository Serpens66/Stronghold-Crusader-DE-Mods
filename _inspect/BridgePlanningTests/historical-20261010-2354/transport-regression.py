from pathlib import Path
import runpy,collections
parser=runpy.run_path('_inspect/EnemyGateBuildingContextAudit/bridge-log-analysis.py')
analyze,fields=parser['analyze'],parser['fields']
root=Path('_inspect/BridgePlanningTests/historical-20261010-2354');raw=(root/'LogOutput.log').read_bytes();output=[];pre={};paths=[]
def flush():
    if not paths:return
    envelope=paths[0][0].split('kind=stored-route',1)[0]
    output.append(envelope+'kind=stored-route-definition-batch,schema=1,rows=['+'|'.join(v for _,v in paths)+'|],complete=True,format=2')
    paths.clear()
for line in raw.decode('utf-8-sig').splitlines():
    if 'bridge trace ' not in line:output.append(line);continue
    f=fields(line.split('bridge trace ',1)[1])
    if f.get('kind')=='stored-route' and f.get('complete')=='True':
        row=[f['definition'],f['captureClock'],*f['origin'].split('/'),f['length'],f['cursor'],f['flags'],f['substep'],*f['decodedEndpoint'].split('/'),*f['nativeSegmentTarget'].split('/'),f['bridges'][1:-1],f['packedHex']]
        assert len(row)==14;paths.append((line,':'.join(row)))
        if len(paths)==32:flush()
        continue
    if f.get('kind')=='command-frame-batch':
        rows=[]
        for row in f['rows'][1:-1].split(';'):
            if not row:continue
            v=row.split('/');assert len(v)==31
            key=(v[1],v[8]);prior=pre.get(key)
            if v[7]=='0':pre[key]=v[:]
            elif prior is not None and all(v[i]==prior[i] for i in [9,11,12,13,14,15,16,17,18,19,20,21,22,27,29,30]):
                v=v[:11]+[prior[0]]+v[23:27]+[v[28]];assert len(v)==17
            if v[7]=='1':pre.pop(key,None)
            rows.append('/'.join(v))
        line=line[:line.index('rows=[')]+'schema=2,rows=['+';'.join(rows)+';],envelopeTiming=batch-flush'
    # Path definitions always precede their following observations/bindings.
    if paths and f.get('kind') in ('route-observations','session-delivered'):flush()
    output.append(line)
flush();converted=('\r\n'.join(output)+'\r\n').encode('utf-8');(root/'transport-converted.log').write_bytes(converted)
a,b=analyze(raw),analyze(converted)
for key in ('bridgeComplete','captureComplete','deliveryComplete','movementSummary','uniqueBridgeCommands','uniqueBridgeUnits','virtualSummary','routeChainGroups','missing','reconstructionErrors'):
    assert a[key]==b[key],(key,a[key],b[key])
assert not b['torn']
# Explicitly broken references and truncated path bytes cannot be complete.
ref='[Info: Enemy Bridge Path Test] bridge trace seq=9999999,session=1,thread=1,tick=1,clock=1,physical=1,topology=1,kind=command-frame-batch,schema=2,rows=[9999999/1/1/1/1/1/1/1/9999999/0/0/99999999/0/0/0/0/0;]\r\n'
assert not analyze(converted+ref.encode())['bridgeComplete']
bad='[Info: Enemy Bridge Path Test] bridge trace seq=9999999,session=1,thread=1,tick=1,clock=1,physical=1,topology=1,kind=stored-route-definition-batch,schema=1,rows=[9999999:1:1:1:4:0:2:0:2:2:2:2::22|],complete=True\r\n'
assert not analyze(converted+bad.encode())['bridgeComplete']
print('PASS frozen transport reconstruction, originalBytes='+str(len(raw))+',convertedBytes='+str(len(converted))+',reduction='+str(round(100*(1-len(converted)/len(raw)),2))+'%; no live-cost or steady-volume claim')
