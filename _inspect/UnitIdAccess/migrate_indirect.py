from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[2]
rows=[r for r in json.loads((Path(__file__).parent/'indirect.json').read_text()) if r['method']!='IsValid'] # IsValid itself checks IsValidId before its lookup.
methods={r['method'] for r in rows}
rx=re.compile(r'\b(GameUnitManagerAPI\.Instance|api|unitApi|units)\.('+ '|'.join(sorted(methods))+r')\s*\(')
numeric={'GetGlobalId':'-1','GetOwner':'-1','GetCurrentHealth':'0','GetMaxHealth':'0','GetSpeed':'0','GetType':'eChimps.CHIMP_TYPE_NULL'}
boolean={'DeleteUnitSafe','IsValid'}
void={'DeleteUnit','KillUnit','SetMaxHealth','SetCurrentHealth','SetSpeed','MoveToTile','SetCurrentLocalTilePosition'}
assert methods <= set(numeric)|boolean|void, methods
for rel in sorted({r['path'] for r in rows}):
    p=ROOT/rel; text=p.read_text(encoding='utf-8-sig'); changes=[]
    for m in rx.finditer(text):
        depth=1;end=m.end()
        while depth:
            if text[end]=='(':depth+=1
            if text[end]==')':depth-=1
            end+=1
        args=text[m.end():end-1]; unit_id=args.split(',')[0].strip()
        original=text[m.start():end]; receiver=m.group(1); method=m.group(2)
        prefix='' if receiver=='GameUnitManagerAPI.Instance' else receiver+', '
        guard='APIShared.UnitAccess.TryGetById('+prefix+unit_id+', out _, out _)'
        if method in void: replacement='if ('+guard+') '+original
        elif method in boolean: replacement='('+guard+' && '+original+')'
        else: replacement='('+guard+' ? '+original+' : '+numeric[method]+')'
        changes.append((m.start(),end,replacement))
    for start,end,replacement in reversed(changes):text=text[:start]+replacement+text[end:]
    text=text.replace('\n','\r\n');p.write_bytes(text.encode('utf-8'))
    assert p.read_bytes().decode('utf-8')==text
print('Guarded',len(rows),'indirect unit-ID calls; SDK defaults and valid-call semantics preserved.')
