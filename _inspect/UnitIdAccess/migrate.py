from pathlib import Path
import re, json, hashlib
ROOT = Path(__file__).resolve().parents[2]
inventory = json.loads((Path(__file__).parent/'inventory.json').read_text())
RX = re.compile(r'\b(GameUnitManagerAPI\.Instance|api|unitApi|units)\s*\.TryGetUnitById\s*\(')
def write(p, text):
    text=text.replace('\r\n','\n').replace('\n','\r\n')
    p.write_bytes(text.encode('utf-8'))
    assert p.read_bytes().decode('utf-8') == text
    assert not re.search(r'(?<!\r)\n', text)

for f in inventory['files']:
    p=ROOT/f['path']
    assert hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'], f'Changed since inventory: {p}'
    text=p.read_text(encoding='utf-8-sig')
    changes=[]
    for m in RX.finditer(text):
        # Runtime calls contain only ID expressions and an out pointer, no string literals.
        depth=1; end=m.end()
        while depth:
            ch=text[end]
            if ch=='(': depth+=1
            if ch==')': depth-=1
            end+=1
        arguments=text[m.end():end-1]
        assert ', out ' in arguments or re.search(r',\s*out\s',arguments), (p,arguments)
        receiver=m.group(1)
        prefix='' if receiver=='GameUnitManagerAPI.Instance' else receiver+', '
        changes.append((m.start(),end,'APIShared.UnitAccess.TryGetById('+prefix+arguments+', out _)'))
    assert len(changes)==f['calls'], (p,len(changes),f['calls'])
    for start,end,replacement in reversed(changes): text=text[:start]+replacement+text[end:]
    write(p,text)

for key in inventory['projects']:
    p=ROOT/key
    if p.parent.name=='APIShared': continue
    text=p.read_text(encoding='utf-8-sig')
    if '<Reference Include="APIShared"' not in text:
        item='''  <PropertyGroup Condition="'$(ApiSharedDir)' == ''"><ApiSharedDir>$(GameDir)\\BepInEx\\plugins\\APIShared_Serp</ApiSharedDir></PropertyGroup>
  <ItemGroup><Reference Include="APIShared"><HintPath>$(ApiSharedDir)\\APIShared.dll</HintPath><Private>false</Private></Reference></ItemGroup>
'''
        # Both existing projects are old-style projects.
        text=text.replace('  <Import Project="$(MSBuildToolsPath)\\Microsoft.CSharp.targets"',item+'  <Import Project="$(MSBuildToolsPath)\\Microsoft.CSharp.targets"')
        assert '<Reference Include="APIShared"' in text
        write(p,text)
    for plugin in (p.parent/'src').glob('*Plugin.cs'):
        text=plugin.read_text(encoding='utf-8-sig')
        if '[BepInPlugin(' not in text or 'BepInDependency("APIShared_Serp"' in text: continue
        text=text.replace('    [BepInPlugin(', '    [BepInDependency("APIShared_Serp", "0.4.10")]\n    [BepInPlugin(')
        write(plugin,text)
    driver=p.parent/'build.bat'
    text=driver.read_text(encoding='utf-8-sig')
    if 'set "API_SHARED_DIR=' not in text and 'set "APISHARED_DIR=' not in text:
        print('CHECK BUILD API PATH',str(driver.relative_to(ROOT)))
print('Migrated',sum(f['calls'] for f in inventory['files']),'lookups in',len(inventory['files']),'files.')
