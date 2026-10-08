from pathlib import Path
import json,re,subprocess,os
root=Path(__file__).resolve().parents[2]
audit=Path(__file__).resolve().parent
mapping=json.loads((audit/'migration-map.json').read_text())['moves']
def write(p,t):
    t=t.replace('\r\n','\n').replace('\n','\r\n');p.write_bytes(t.encode('utf-8'));assert p.read_bytes().decode()==t
paths=set(root/p for p in subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0') if p)
paths.update(root/p for p in mapping.values())
for path in paths:
    if not path.exists() or path.suffix not in {'.cs','.csproj','.ps1','.md','.bat','.json','.yml'}:continue
    rel=path.relative_to(root).as_posix()
    if any(s in path.parts for s in ['bin','obj','BepInEx','before','CrusaderDE-Native-Baseline']):continue
    if rel.startswith(('_inspect/APISharedRefactor/','_inspect/SharedSeparation/')):continue
    try:t=path.read_bytes().decode('utf-8-sig')
    except UnicodeDecodeError:continue
    old=t
    for a,b in mapping.items():
        segments=a.split('/')
        if len(segments)!=2:continue
        pattern='"Shared"'+r'\s*,\s*'+re.escape('"'+segments[1]+'"')
        replacement=', '.join('"'+part+'"' for part in b.split('/'))
        t=re.sub(pattern,replacement,t)
    for name in ['Release','Steam','ScriptExtenderUpdate','DocumentationImages','UnitCommandSourceChecks','Threading.Tests']:
        target={'Release':['Tools','Release'],'Steam':['Tools','Steam'],'ScriptExtenderUpdate':['Tools','ScriptExtenderUpdate'],'DocumentationImages':['Tools','Documentation'],'UnitCommandSourceChecks':['Tools','Validation','UnitCommandSourceChecks'],'Threading.Tests':['Tests','Threading.Tests']}[name]
        t=re.sub('"Shared"'+r'\s*,\s*'+re.escape('"'+name+'"'), ', '.join('"'+s+'"' for s in ['Shared']+target),t)
    if rel.startswith('_inspect/APISharedTests/'):
        t=t.replace('"Shared.MissionEvents.', '"APIShared.Internal.MissionEvents.')
    if t!=old:write(path,t)
for file in ['DependencyFreeJson','ToolTipPresentation']:
    p=root/'APIShared/src/ModSettings/Internal'/f'{file}.cs';t=p.read_bytes().decode()
    t=re.sub(r'#if API_SHARED_INTERNAL_(JSON|TOOLTIP)\r?\n\s*internal static class '+file+r'\r?\n#else\r?\n\s*public static class '+file+r'\r?\n#endif', '    internal static class '+file,t)
    write(p,t)
# This file is a mod-side join publisher, not an adapter to an API capability.
old='Shared/Adapters/APIShared/LobbyLifecycle.cs';new='Shared/Runtime/Gameplay/LobbyLifecycle.cs'
for p in paths:
    if not p.exists() or p.suffix not in {'.csproj','.cs','.ps1','.md','.json'}:continue
    if any(s in p.parts for s in ['bin','obj','BepInEx','before','CrusaderDE-Native-Baseline']):continue
    if p.is_relative_to(audit):continue
    try:t=p.read_bytes().decode('utf-8-sig')
    except UnicodeDecodeError:continue
    orig=t
    for sep in ['/','\\','\\\\']:t=t.replace(old.replace('/',sep),new.replace('/',sep))
    if t!=orig:write(p,t)
(root/old).rename(root/new)
data=json.loads((audit/'migration-map.json').read_text())
data['moves']['Shared/LobbyLifecycle.cs']=new
write(audit/'migration-map.json',json.dumps(data,indent=2)+'\n')
print('Finished structured path references, internal visibility and lobby publisher placement.')
