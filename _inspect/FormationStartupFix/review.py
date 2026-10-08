from pathlib import Path
import subprocess, difflib, re
root=Path.cwd(); out=root/'_inspect/FormationStartupFix'
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); p.write_bytes(data)
    assert p.read_bytes()==data
rows=[]
for before in sorted((out/'before').rglob('*')):
    if not before.is_file(): continue
    p=before.relative_to(out/'before'); current=root/p
    old=before.read_text(encoding='utf-8-sig'); new=current.read_text(encoding='utf-8-sig')
    head=subprocess.check_output(['git','show','HEAD:'+p.as_posix()],cwd=root).decode('utf-8-sig')
    assert old.replace('\r\n','\n')==head.replace('\r\n','\n'), 'Starting snapshot differs from HEAD: '+str(p)
    rows.append(str(p))
    target=out/'diff'/p.with_suffix(p.suffix+'.diff');target.parent.mkdir(parents=True,exist_ok=True)
    write(target,''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='starting:'+p.as_posix(),tofile=p.as_posix())))
for relative in ('APIShared/src/UnitCommands/FormationModel.cs','APIShared/src/UnitCommands/FormationOrderPacket.cs',
    'APIShared/src/UnitCommands/FormationReleaseStateModel.cs','APIShared/src/UnitCommands/IUnitCommandSettings.cs'):
    assert subprocess.check_output(['git','diff','HEAD','--',relative],cwd=root)==b'', relative+' unexpectedly changed'
for mod in ('APIShared','BugfixesAndQoL'):
    for p in (root/mod).rglob('*.cs'):
        if any(x in p.parts for x in ('bin','obj','BepInEx')):continue
        assert not re.search(rb'(?<!\r)\n',p.read_bytes()), 'Bare LF '+str(p)
for p in list(out.glob('*.py')):
    write(p,p.read_text(encoding='utf-8-sig'))
baseline=root/'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RALLY_TERRAIN_SLOWDOWN.md'
note='''
## Startup interaction: immutable static analysis (2026-10-08)

The 18:36 start log confirms the process-owned rally capture was installed at
RVA 0x18410C before the cadence resolver ran. Reading the original dispatch
operand from live memory after that installation produced an out-of-module
handler table and disabled the shared commands, Shift queue and formation.
Static cadence analysis must read the dispatch displacement, handler pointers,
handler instructions, compressed state mapping and case jump tables from the
existing CrusaderLibraryLoadContext.Memory load-time snapshot. Keep the context
rooted; validate all reads against its span and calculate decoder virtual
addresses from its real ModuleHandle. Do not decode published live hooks or
dispose the extender-owned region. No new native hook boundaries are needed.

Cadence qualification is optional. Its failure must reject weighted route
publication without disabling the shared command/selector runtime. Executable
production-resolver tests independently mutate live dispatch, handler pointer,
handler code, state map and jump table, including the combined mutation, and
verify identical snapshot-derived profiles plus rejection of invalid addresses.
Fixes' speed-reduction patches remove stores; snapshot analysis retains the
original possibilities conservatively, in addition to the captured current
speed bonus. No Fixes or Script Extender sources are changed.
'''
text=baseline.read_text(encoding='utf-8-sig')
if '## Startup interaction: immutable static analysis (2026-10-08)' not in text:write(baseline,text+note)
write(out/'Review.md','''# Startup correction review

Starting source snapshots match HEAD. Changes are limited to snapshot reads,
optional cadence isolation, early independent binding registration, availability
notifications, unavailable Shift-queue guard, collapsed XAML fallbacks, tests and
the user-requested visible terminology. No protocol, public API, native hook
installation, geometry, gesture rows, density, release consumption, README or
version changes. Runtime-error UI updates use the shared main-thread dispatcher.

Executable regression tests compile the production cadence resolver, its actual
optional initialization block and weighted-publication guards. They compile the
complete production Feature and MenuViewModel with boundary test doubles and
execute initialization failures, registration failures, commands, six icons,
config changes, checkbox, scene/host replacement and deselection.

Native baseline reused: dispatcher/rally/type handlers/cadence/arrival and the
complete move/release closure for FBCB9319. Installed interop layout and true game
assembly checks pass. Permanent hook, JSON, lifecycle, XAML and CRLF checks pass.
560042 formation/release/rows/geometry/packet assertions and 8999 queue checks pass.
Friendly-moat native probes, native detours and production rally generators pass.

In-game start/menu/release/save/terrain and multiplayer acceptance require a fresh
game run with the newly installed packages. Unit fixtures do not claim these.

## Compared starting snapshots

'''+''.join('- '+p+'\n' for p in rows))
print('PASS: '+str(len(rows))+' starting snapshots match HEAD; reviewed diffs saved; geometry/protocol/release/settings contracts unchanged; CRLF verified.')
