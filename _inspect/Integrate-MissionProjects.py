from pathlib import Path
import re,os,json
root=Path.cwd()
def write(p,s): p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
affected=[]
for p in root.rglob('*.csproj'):
    parts=p.relative_to(root).parts
    if any(x.startswith('.') or x.startswith('_') or x.lower()=='tests' or x.endswith('.Tests') for x in parts) or any(x in parts for x in ['APIShared','shcde-script-extender','obj','bin']):continue
    s=p.read_text(encoding='utf-8-sig')
    if 'BepInEx' not in s and p.name!='ExtremePowers.API.csproj':continue
    sources=[]
    for inc in re.findall(r'<Compile Include="([^"]+)"',s):
        if '$' in inc or '*' in inc:continue
        f=(p.parent/inc).resolve()
        if f.exists():sources.append(f)
    if not any(any(token in f.read_text(encoding='utf-8-sig') for token in ['MissionEvents','GameplaySessionLifecycle','GameModeHelper','GameplayModModePolicy','GameplayFeatureModePolicy']) for f in sources):continue
    affected.append(str(p.relative_to(root)))
    prefix=os.path.relpath(root,p.parent).replace('/','\\')+'\\'
    if '<Reference Include="APIShared"' not in s:
        s=s.replace('  <ItemGroup>', '''  <PropertyGroup Condition="'$(ApiSharedDir)' == ''"><ApiSharedDir>$(GameDir)\\BepInEx\\plugins\\APIShared_Serp</ApiSharedDir></PropertyGroup>
  <ItemGroup>
    <Reference Include="APIShared"><HintPath>$(ApiSharedDir)\\APIShared.dll</HintPath><Private>false</Private></Reference>''',1)
    if 'Shared\\GameplaySessionLifecycle.cs' not in s:
        s=s.replace('  <ItemGroup>', '  <ItemGroup>\n    <Compile Include="'+prefix+'Shared\\GameplaySessionLifecycle.cs"><Link>Shared\\GameplaySessionLifecycle.cs</Link></Compile>',1)
    if p.name=='ExtremePowers.API.csproj':
        if '<Reference Include="BepInEx"' not in s:
            s=s.replace('<ItemGroup>','<ItemGroup><Reference Include="BepInEx"><HintPath>$(GameDir)\\BepInEx\\core\\BepInEx.dll</HintPath><Private>false</Private></Reference><Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)\\Stronghold Crusader Definitive Edition_Data\\Managed\\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>',1)
        if 'Shared\\DebugLogHelper.cs' not in s:
            s=s.replace('<ItemGroup>','<ItemGroup><Compile Include="..\\Shared\\DebugLogHelper.cs"><Link>Shared\\DebugLogHelper.cs</Link></Compile>',1)
    write(p,s)
    for f in sources:
        if f.name.endswith('Plugin.cs'):
            source=f.read_text(encoding='utf-8-sig')
            if '[BepInPlugin(' in source and 'BepInDependency("APIShared_Serp"' not in source:
                source=source.replace('[BepInPlugin(', '[BepInDependency("APIShared_Serp", "0.3.6")]\n    [BepInPlugin(',1)
                write(f,source)

write(root/'_inspect/MissionLifecycleProjects.json',json.dumps(affected,indent=2)+'\n')
# Companion assembly has no plugin attribute from which an owner could be inferred.
for rel in ['ExtremePowers/api/Networking/ExtremePowerNetworkRuntime.cs','ExtremePowers/api/Native/NativeExtremePowersRuntime.cs']:
    p=root/rel;s=p.read_text(encoding='utf-8-sig')
    m=re.search(r'^\s*mapUnloadSubscription = Shared.MissionEvents',s,re.M)
    if m:s=s[:m.start()]+'\n                Shared.MissionEvents.SetOwner("ExtremePowers_Serp");'+s[m.start():]
    write(p,s)
print('Affected runtime projects:',len(affected))
for item in affected: print(item)
