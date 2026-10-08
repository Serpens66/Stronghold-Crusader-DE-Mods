from pathlib import Path
import subprocess, difflib
root=Path.cwd();out=root/'_inspect/FormationTooltips';count=0
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');p.write_bytes(data);assert p.read_bytes()==data
for before in (out/'before').rglob('*'):
    if not before.is_file():continue
    rel=before.relative_to(out/'before');old=before.read_text(encoding='utf-8-sig');new=(root/rel).read_text(encoding='utf-8-sig')
    head=subprocess.check_output(['git','show','HEAD:'+rel.as_posix()],cwd=root).decode('utf-8-sig')
    assert old.replace('\r\n','\n')==head.replace('\r\n','\n'),str(rel)
    p=out/'diff'/rel.with_suffix(rel.suffix+'.diff');p.parent.mkdir(parents=True,exist_ok=True)
    write(p,''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='starting:'+str(rel),tofile=str(rel))));count+=1
assert subprocess.check_output(['git','diff','HEAD','--','APIShared','BugfixesAndQoL/info.json','BugfixesAndQoL/README.md'],cwd=root)==b''
write(out/'Review.md','''# Arrangement tooltip review

Starting snapshots match HEAD. APIShared, versions, protocol and README remain
unchanged. New public game accesses are MainViewModel's existing public enter/
leave command getters and public TroopsPanelRollover getter/setter, validated
against the installed true Assembly-CSharp.dll. The installed Noesis converter
contract was checked; the converter returns float for FontSize.

The original complete managed ToggleControlGroups hover path clears resource
labels/icons, shows TroopsPanelRollover and hides TroopsPanelRollover2. The new
button invokes that public command and replaces only its localized text. Leave
checks owner identity and text before invoking the original public leave command.
Menu close, unavailable runtime, troop deselection and scene/host changes clear
ownership. Existing camera/presentation callbacks and bindings remain rooted.

Executable tests compile production menu and converter sources, and verify
localized original-command dispatch, owned close, foreign takeover, failed
runtime, scene changes, deselection and float 16/20/36 -> 32/40/72. Existing
formation initialization, remembered rows and native command regressions pass.
Runtime JSON, lifecycle, real assembly, permanent hooks, XAML and CRLF checks pass.
No Script Extender or Fixes edits, additional hooks or plugin callbacks.

Visible tooltip placement, readability and screen-edge behavior await playtest.
''')
write(out/'review.py',Path(__file__).read_text())
print('PASS: '+str(count)+' starting snapshots match HEAD; reviewed diffs saved; APIShared, versions and README unchanged.')
