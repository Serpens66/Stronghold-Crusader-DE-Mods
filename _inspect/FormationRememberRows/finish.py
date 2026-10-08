from pathlib import Path
import json, subprocess, difflib
root=Path.cwd();out=root/'_inspect/FormationRememberRows'
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');p.write_bytes(data);assert p.read_bytes()==data
p=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationStartupTests.cs'
s=p.read_text(); line=next(x for x in s.splitlines() if x.startswith('        fixture += '))
suffix='.Replace("__PRODUCTION_UPDATE__", gestureUpdate);'
content=json.loads(line[len('        fixture += '):-len(suffix)])
s=s.replace(line,'        fixture += """\n'+content.strip('\n')+'\n"""'+suffix);write(p,s)
p=out/'tests.py';s=p.read_text();s='\n'.join(x for x in s.splitlines() if 'if False else' not in x)+'\n';write(p,s)
count=0
for before in (out/'before').rglob('*'):
    if not before.is_file():continue
    rel=before.relative_to(out/'before');old=before.read_text(encoding='utf-8-sig');new=(root/rel).read_text(encoding='utf-8-sig')
    head=subprocess.check_output(['git','show','HEAD:'+rel.as_posix()],cwd=root).decode('utf-8-sig')
    assert head.replace('\r\n','\n')==old.replace('\r\n','\n'), str(rel)
    p=out/'diff'/rel.with_suffix(rel.suffix+'.diff');p.parent.mkdir(parents=True,exist_ok=True)
    write(p,''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='starting:'+str(rel),tofile=str(rel))));count+=1
for rel in ('APIShared/info.json','BugfixesAndQoL/info.json','APIShared/src/UnitCommands/FormationOrderPacket.cs','APIShared/src/UnitCommands/FormationReleaseStateModel.cs','APIShared/README.md','BugfixesAndQoL/README.md'):
    assert subprocess.check_output(['git','diff','HEAD','--',rel],cwd=root)==b'', rel
for p in out.glob('*.py'):write(p,p.read_text())
print('PASS: '+str(count)+' starting snapshots match HEAD; protocol, release state, versions and README unchanged. Reviewed diffs saved.')
