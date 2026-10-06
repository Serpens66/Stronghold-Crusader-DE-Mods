"""Lossless replay of both frozen process sections with bounded native batches."""
from pathlib import Path
import importlib.util,re
base=Path(__file__).parent
spec=importlib.util.spec_from_file_location('a',base/'bridge-log-analysis.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def pack(tokens):
    result=[];i=0
    while i<len(tokens):
        n=0
        while i+n<len(tokens) and tokens[i+n]=='0':n+=1
        if n>=3:result.append('z'+str(n));i+=n
        else:result.append(tokens[i]);i+=1
    return '/'.join(result)
def rewrite(raw):
    output=[];batch=[];envelope=None
    def flush():
        nonlocal envelope
        if not batch:return
        f=envelope
        prefix='[Info   :Enemy Bridge Path Test] [2026-10-06 00:00:00.000] bridge trace '
        prefix+=','.join(k+'='+f[k] for k in ('seq','session','thread','tick','clock','physical','topology'))
        output.append((prefix+',kind=native-frame-batch,columns=seq/session/thread/tick/clock/physical/topology/phase/rva/site/contextDefinition/values26,zeroRuns=zN,rows=['+';'.join(batch)+';]\r\n').encode())
        batch.clear();envelope=None
    for line in raw.splitlines(keepends=True):
        if not any(k in line for k in (b'kind=native-frame,',b'kind=native-enter,',b'kind=native-exit,')):output.append(line);continue
        f=a.fields(line.decode().split('bridge trace ',1)[1].strip())
        if f['kind']=='native-frame':v=f['values'].strip('[]').split('/')
        else:
            stamp=[f.get(k,'0') for k in ('global','owner','leader','state16','phase16','retainedTarget16','targetGlobal')]
            v=[f['op'],f['parent'],*f['args'].strip('[]').split('/'),f['scopeSession'],f['state'],*stamp,'1' if f.get('completed')=='True' else '0','v' if f.get('observedReturn','void')=='void' else f['observedReturn'],f.get('regions','0'),*f.get('retainedEntryPlanStamp','[0/0/0/0/0/0]').strip('[]').split('/')]
            f['phase']='pre' if f['kind']=='native-enter' else 'post'
        assert len(v)==26
        tokens=[f[k] for k in ('seq','session','thread','tick','clock','physical','topology')]+['0' if f['phase']=='pre' else '1',f['rva'][2:],f['site'],f.get('contextDefinition','0')]+v
        if envelope is None:envelope=f
        batch.append(pack(tokens))
        if len(batch)==16:flush()
    # Retain a torn final fragment as the final fragment, not inside a valid row.
    tail=output.pop() if output and not output[-1].endswith(b'\n') else b''
    flush();output.append(tail)
    return b''.join(output)
def frames(result):
    names=('seq','session','thread','tick','clock','physical','topology','kind','op','parent','args','scopeSession','state','global','owner','leader','state16','phase16','retainedTarget16','targetGlobal','completed','observedReturn','regions','contextDefinition','retainedEntryPlanStamp')
    return sorted(tuple(f.get(k,'') if k!='retainedEntryPlanStamp' or f['kind']=='native-enter' else '' for k in names) for _,f in result['records'] if f['kind'] in ('native-enter','native-exit'))
for name in ['bridge-20261006-133913.log','bridge-20261006-155247.log']:
    raw=(base/name).read_bytes();encoded=rewrite(raw);old=a.analyze(raw);new=a.analyze(encoded)
    assert frames(old)==frames(new)
    for key in ['routeChainGroups','movementSummary','missing','reconstructionErrors','captureComplete','deliveryComplete','bridgeFileComplete','uniqueBridgeCommands','uniqueBridgeUnits']:assert old[key]==new[key],key
    assert sum(new['volumes'].values())<sum(old['volumes'].values())
    print('PASS',name,'trace bytes incl prefixes',sum(old['volumes'].values()),'->',sum(new['volumes'].values()),'native frames',len(frames(new)))
    # Truncated and invalid zero-run batches must never count as complete delivery.
    corrupt=encoded.replace(b'zeroRuns=zN,rows=[',b'zeroRuns=zN,rows=[z999/',1)
    assert a.analyze(corrupt)['torn']
