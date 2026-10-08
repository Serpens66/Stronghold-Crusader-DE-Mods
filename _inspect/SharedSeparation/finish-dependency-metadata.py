from pathlib import Path
import json,re
root=Path.cwd()
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    with p.open('w',encoding='utf-8',newline='') as f: f.write(t.replace('\r\n','\n').replace('\n','\r\n'))
exclude={'.git','shcde-script-extender','tests','examples','bin','obj','BepInEx','.tools','.inspect','_inspect','.native-analysis','.release-output','before'}
for p in root.rglob('info.json'):
    if exclude.intersection(p.relative_to(root).parts): continue
    source='\n'.join(read(s) for s in p.parent.rglob('*.cs') if not exclude.intersection(s.relative_to(p.parent).parts))
    if '[BepInPlugin(' not in source: continue
    constants=dict(re.findall(r'\bconst\s+string\s+(\w+)\s*=\s*"([^"\n]+)"',source))
    data=json.loads(read(p));changed=False
    for guid,argument in re.findall(r'\[BepInDependency\(\s*("[^"\n]+"|\w+)\s*,\s*"([^"\n]+)"\s*\)\]',source):
        guid=guid.strip('"') if guid.startswith('"') else constants[guid]
        if guid=='000shcdese' and 'MinimumScriptExtenderVersion' not in data:
            data['MinimumScriptExtenderVersion']=argument;changed=True
    if changed: write(p,json.dumps(data,ensure_ascii=False,indent=2)+'\n')
# Active preflights use the same authoritative manifest-based resolver as release packaging.
for relative in ['_inspect/Test-MissionLifecyclePreflight.ps1','_inspect/Test-EditorLifecyclePreflight.ps1','_inspect/EditorLifecycleMigration.ps1']:
    p=root/relative;t=read(p)
    t=t.replace("$release = Get-Content 'Shared\\Tools\\Release\\release-projects.json' -Raw | ConvertFrom-Json", ". (Join-Path $workspace 'Shared\\Tools\\Release\\Release.Common.ps1')\n$release = Get-ReleaseConfiguration")
    t=t.replace("$release = Get-Content -LiteralPath (Join-Path $workspace 'Shared\\Tools\\Release\\release-projects.json') -Raw | ConvertFrom-Json", ". (Join-Path $workspace 'Shared\\Tools\\Release\\Release.Common.ps1')\n$release = Get-ReleaseConfiguration")
    t=t.replace('$release.ApiShared.Consumers.($mod[0].Name)', '(Get-ApiSharedConsumerMinimum -Config $release -ModName $mod[0].Name)')
    t=t.replace('$release.ApiShared.Consumers.($matches[0].Name)', '(Get-ApiSharedConsumerMinimum -Config $release -ModName $matches[0].Name)')
    t=t.replace('[string]$release.ApiShared.Consumers.$consumer', '(Get-ApiSharedConsumerMinimum -Config $release -ModName $consumer)')
    write(p,t)
# PowerShell byte-loading avoids Mark-of-the-Web restrictions without modifying installed libraries.
p=root/'APIShared/tools/Validation/Verify-Interop.ps1';t=read(p)
t=t.replace("Add-Type -Path (Join-Path $GameDir 'BepInEx\\core\\Mono.Cecil.dll')", "[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\\core\\Mono.Cecil.dll')))")
write(p,t)
print('Existing Extender minima preserved and active preflights migrated to manifest ownership.')
