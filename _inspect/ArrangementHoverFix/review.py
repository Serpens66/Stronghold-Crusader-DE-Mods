from pathlib import Path
import subprocess, difflib, hashlib, xml.etree.ElementTree as ET
root=Path.cwd(); out=root/'_inspect/ArrangementHoverFix'
def write(p,s):
    p.parent.mkdir(parents=True,exist_ok=True)
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');p.write_bytes(data);assert p.read_bytes()==data
for before in (out/'before').rglob('*'):
    if not before.is_file():continue
    rel=before.relative_to(out/'before'); current=root/rel
    old=before.read_text(encoding='utf-8-sig');new=current.read_text(encoding='utf-8-sig')
    head=subprocess.check_output(['git','show','HEAD:'+rel.as_posix()]).decode('utf-8-sig')
    print(str(rel)+': starting snapshot '+('matches HEAD' if old==head else 'has preserved prior changes'))
    data=current.read_bytes();assert b'\n' not in data.replace(b'\r\n',b'');assert b'\\r\\n' not in data
    write(out/'diff'/rel.with_suffix(rel.suffix+'.diff'),''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='starting:'+str(rel),tofile=str(rel))))
    print('CRLF',data.count(b'\r\n'),'sha256',hashlib.sha256(data).hexdigest())
patch=ET.parse(root/'BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml')
for content in patch.iter('Content'):assert len(list(content))==1
buttons=[el for el in patch.iter() if el.attrib.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name')=='BugfixesAndQoLFormationOpenButton'];assert len(buttons)==1
text=(root/'BugfixesAndQoL/src/FormationMenuViewModel.cs').read_text()
assert 'ShowButtonTooltipCommand' not in text and 'ButtonTroopPanelMouseEnterCommand' not in text
assert 'MouseEnter += OnButtonMouseEnter' in text and 'MouseEnter -= OnButtonMouseEnter' in text
assert 'current.PropertyChanged += MainViewModelPropertyChanged' in text and 'subscribedMainViewModel.PropertyChanged -= MainViewModelPropertyChanged' in text
write(out/'Review.md','''# Arrangement hover correction

Opening hover now attaches directly to the named Noesis button and writes the existing short Vanilla rollover, following the working Knight event/visibility path. It clears stale cost fields, hides the long rollover, and keeps foreign hover text intact on leave. Replacement buttons and viewmodels detach obsolete UI handlers. Feature disable, HUD hide and scene changes clean up logically; no native hooks are added or disposed.

The menu remains statically rooted by FormationFeature; the existing rooted FormationRuntime camera presentation callback invokes RefreshHostState. APIShared contracts, configuration, protocol, icons, row selection, checkbox font and texts remain unchanged. The three changed files were compared with current Git and their starting snapshots; diffs are retained here.

Executable fixtures compile the production menu and exercise direct event dispatch, localized short rollover visibility, cleared costs, repeated refresh, foreign short/long hover, missing runtime, scene/selection changes, HUD and viewmodel replacement. Existing formation, packet, wheel, remembered-row and queue regressions are run separately. Runtime JSON/lifecycle, installed interop, real assembly, permanent-hook, XAML and CRLF checks pass.

Visible hover placement and actual game mouse dispatch still require playtest; fixture success is not a visible in-game acceptance result. Versions and README are unchanged.
''')
for name in ['implement.py','tests.py','review.py']:
    p=out/name;write(p,p.read_text())
print('PASS: targeted diff review, XAML root/button contract, removed old hover commands, UI event pairing and CRLF.')
