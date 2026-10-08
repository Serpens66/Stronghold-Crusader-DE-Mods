from pathlib import Path
import os,json,re,subprocess
root=Path(__file__).resolve().parents[2];audit=Path(__file__).resolve().parent
data=json.loads((audit/'migration-map.json').read_text())
def write(p,t):
    t=t.replace('\r\n','\n').replace('\n','\r\n');p.write_bytes(t.encode());assert p.read_bytes().decode()==t
renames={
 'Shared/Adapters/APIShared/GameModeHelper.cs':'Shared/Adapters/APIShared/PlayerIdentityHelper.cs',
 'Shared/Adapters/APIShared/GameplaySessionLifecycle.cs':'Shared/Adapters/APIShared/MissionEventsAdapter.cs'
}
for base,dirs,names in os.walk(root):
    dirs[:]=[d for d in dirs if d not in {'.git','.tools','.inspect','.native-analysis','bin','obj','BepInEx','shcde-script-extender','x86_64','.release-output','CrusaderDE-Native-Baseline','before'}]
    for name in names:
        p=Path(base)/name
        if p.suffix not in {'.csproj','.props','.targets','.cs','.ps1','.bat','.md','.json','.yml'}:continue
        if p.is_relative_to(audit) or 'APISharedRefactor' in p.parts:continue
        try:t=p.read_bytes().decode('utf-8-sig')
        except UnicodeDecodeError:continue
        orig=t
        for old,new in renames.items():
            for sep in ['/','\\','\\\\']:
                t=t.replace(old.replace('/',sep),new.replace('/',sep))
            oldparts=old.split('/');newparts=new.split('/')
            t=re.sub(',\\s*'.join(re.escape('"'+s+'"') for s in oldparts), ', '.join('"'+s+'"' for s in newparts),t)
        if t!=orig:write(p,t)
for old,new in renames.items():(root/old).rename(root/new)
for old,new in list(data['moves'].items()):data['moves'][old]=renames.get(new,new)
write(audit/'migration-map.json',json.dumps(data,indent=2)+'\n')
legacy={k:v for k,v in data['moves'].items() if k.endswith('.cs')}
legacy.update({f'Shared/{d}/':f'Shared/Tools/{d}/' for d in ['Release','Steam','ScriptExtenderUpdate']})
write(root/'Shared/Tools/Validation/legacy-shared-paths.json',json.dumps(legacy,indent=2)+'\n')
drivers=[]
for p in root.rglob('build.bat'):
    rel=p.relative_to(root).as_posix()
    if any(s in p.parts for s in ['bin','obj','BepInEx','_inspect','.inspect','shcde-script-extender','.tools','.native-analysis']):continue
    # Only mod drivers with projects that use Shared or APIShared.
    projects=list(p.parent.glob('*.csproj'))
    if not any('Shared' in q.read_bytes().decode('utf-8-sig') for q in projects):continue
    t=p.read_bytes().decode('utf-8-sig')
    check=os.path.relpath(root/'Shared/Tools/Validation/Test-SharedBoundaries.ps1',p.parent).replace('/','\\')
    if rel=='APIShared/build.bat':
        insertion='if exist "%~dp0'+check+'" (\n  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0'+check+'"\n  if errorlevel 1 exit /b 1\n)\n'
    else:
        insertion='powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0'+check+'"\nif errorlevel 1 exit /b 1\n'
    t=t.replace('@echo off\r\n','@echo off\n'+insertion,1)
    if rel=='Testmods/EnemyBridgePathTest/build.bat':
        t=t.replace('"%PROJECT_DIR%..\\..\\_inspect\\BridgePlanningTests\\bin\\BridgePlanningTests.exe"\r\nif errorlevel 1 goto build_failed', '"%PROJECT_DIR%..\\..\\_inspect\\BridgePlanningTests\\bin\\BridgePlanningTests.exe"\r\nif not "%ERRORLEVEL%"=="0" goto build_failed')
    write(p,t);drivers.append(rel)
write(audit/'build-drivers.txt','\n'.join(sorted(drivers))+'\n')
print(f'Wired shared-boundary gate into {len(drivers)} mod drivers.')
