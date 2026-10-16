import json, pathlib
root=pathlib.Path('_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319')
targets={0x2e1e0,0x2dba0,0x3b360,0xcc420,0xccbb0,0x3f270,0x3ec90,0x29650,0x29700,0x2d810,0x40740}
def number(x):
    if x is None: return 0
    if isinstance(x,dict): return number(x.get('target',x.get('rva',0)))
    return int(x,16) if isinstance(x,str) else int(x)
inst={}; headers={}; incoming=[]
for p in (root/'analysis/g1E2FBFF53E33').glob('facts-*.jsonl'):
    for line in p.open(encoding='utf-8-sig'):
        if '"recordType":"instructions"' in line:
            x=json.loads(line); r=number(x['functionRva'])
            if r in targets: inst.setdefault(r,[]).extend(x['items'])
            for i in x['items']:
                for t in i['targets']:
                    n=number(t)
                    if any(k<n<k+16 for k in targets): incoming.append((r,i['rva'],n))
        elif '"recordType":"header"' in line:
            x=json.loads(line)['payload']; r=number(x['functionRva'])
            if r in targets: headers[r]=x
report={}
for r in sorted(targets):
    first=[];n=0
    for i in inst[r]:
        first.append(i);n+=i['length']
        if n>=6:break
    inside=[x for x in incoming if r<x[2]<r+n]
    assert not inside,(hex(r),inside)
    report[hex(r)]={'length':n,'prefix':''.join(i['bytes'] for i in first),'functionEnd':hex(max(number(i['rva'])+i['length'] for i in inst[r])),'instructions':first,'incomingInside':inside,'header':headers[r]}
    print(hex(r),n,report[hex(r)]['prefix'])
pathlib.Path('_inspect/AIEconomyDiagnostic-20261016/native-entry-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
