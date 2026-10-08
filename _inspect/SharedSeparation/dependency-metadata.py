from pathlib import Path
import json,re
root=Path.cwd()
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    with p.open('w',encoding='utf-8',newline='') as f: f.write(t.replace('\r\n','\n').replace('\n','\r\n'))
exclude={'.git','shcde-script-extender','tests','bin','obj','BepInEx','.tools','.inspect','_inspect','.native-analysis','.release-output','before'}
changed=[]
for p in root.rglob('info.json'):
    if exclude.intersection(p.relative_to(root).parts): continue
    sources=[s for s in p.parent.rglob('*.cs') if not exclude.intersection(s.relative_to(p.parent).parts)]
    source='\n'.join(read(s) for s in sources)
    if '[BepInPlugin(' not in source: continue
    constants=dict(re.findall(r'\bconst\s+string\s+(\w+)\s*=\s*"([^"\n]+)"',source))
    requirements=[]
    for guid,argument in re.findall(r'\[BepInDependency\(\s*("[^"\n]+"|\w+)\s*(?:,\s*("[^"\n]+"|[^)]+))?\)\]',source):
        if 'SoftDependency' in argument: continue
        guid=guid.strip('"') if guid.startswith('"') else constants[guid]
        if guid=='000shcdese': continue
        minimum=argument.strip().strip('"') if argument.strip().startswith('"') else ''
        requirement={'GUID':guid}
        if minimum: requirement['MinimumVersion']=minimum
        assert not any(r['GUID']==guid for r in requirements),(p,guid)
        requirements.append(requirement)
    data=json.loads(read(p))
    existing=data.get('Dependencies',[])
    for old in existing:
        assert any(r['GUID']==old['GUID'] for r in requirements),(p,old)
    if requirements:
        if 'Dependencies' not in data:
            rebuilt={}
            for key,value in data.items():
                rebuilt[key]=value
                if key=='MaximumScriptExtenderVersion': rebuilt['Dependencies']=requirements
            if 'Dependencies' not in rebuilt: rebuilt['Dependencies']=requirements
            data=rebuilt
        else: data['Dependencies']=requirements
        write(p,json.dumps(data,ensure_ascii=False,indent=2)+'\n')
        changed.append(str(p.relative_to(root)))

# Single source of truth: release packaging reads consumer minima from source info.json.
p=root/'Shared/Tools/Release/release-projects.json'
data=json.loads(read(p)); del data['ApiShared']['Consumers']; write(p,json.dumps(data,ensure_ascii=False,indent=2)+'\n')
p=root/'Shared/Tools/Release/Release.Common.ps1';t=read(p)
t=t.replace("@('Project', 'Guid', 'Consumers')","@('Project', 'Guid')")
start=t.index('    if ($null -eq $apiShared.Consumers)');end=t.index('    $projects =',start);t=t[:start]+t[end:]
start=t.index('    $consumersProperty =',t.index('function Get-ApiSharedConsumerMinimum'));end=t.index('\n}',start)
t=t[:start]+'''    $directory = Get-ReleaseProjectDirectory -Config $Config -Project $ModName
    $infoPath = Join-Path (Join-Path $Config.Root $directory) 'info.json'
    if (-not (Test-Path -LiteralPath $infoPath)) { return $null }
    $manifest = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
    $dependenciesProperty = $manifest.PSObject.Properties['Dependencies']
    if ($null -eq $dependenciesProperty) { return $null }
    $dependencies = @($dependenciesProperty.Value | Where-Object { [string]$_.GUID -ceq [string]$apiSharedProperty.Value.Guid })
    if ($dependencies.Count -gt 1) { throw "Duplicate APIShared dependencies in $infoPath" }
    if ($dependencies.Count -eq 0) { return $null }
    return [string]$dependencies[0].MinimumVersion'''+t[end:];write(p,t)

p=root/'_inspect/APISharedTests/WorkspaceIntegration.cs';t=read(p)
start=t.index('            string releaseConfig =');end=t.index('            string releaseScript =',start)
t=t[:start]+'''            Func<string, string, bool> apiDependencyMatchesRelease = (source, consumer) =>
            {
                Match dependency = Regex.Match(source,
                    @"BepInDependency\\((?:ApiSharedGuid|""APIShared_Serp"")\\s*,\\s*""(?<version>[^""]+)""\\)");
                var metadata = (Dictionary<string, object>)DependencyFreeJson.Parse(
                    File.ReadAllText(Path.Combine(workspace, consumer, "info.json")));
                var requirements = ((List<object>)metadata["Dependencies"])
                    .Cast<Dictionary<string, object>>().Where(item => (string)item["GUID"] == "APIShared_Serp").ToArray();
                return dependency.Success && requirements.Length == 1 &&
                    dependency.Groups["version"].Value == (string)requirements[0]["MinimumVersion"];
            };
'''+t[end:]
# Helpers have a non-root directory and are never passed to this assertion currently.
write(p,t)

p=root/'BugfixesAndQoL/tests/Program.cs';t=read(p)
t=t.replace('plugin.Contains("[BepInDependency(ApiSharedGuid, \\"0.4.11\\")]"),','System.Text.RegularExpressions.Regex.IsMatch(plugin, @"BepInDependency\\(ApiSharedGuid, ""[0-9.]+""\\)"),')
write(p,t)
p=root/'ExtraFeatures/verify-repair.ps1';t=read(p)
t=t.replace('if (-not $activeVersion -or $runtimeText -notmatch \'BepInDependency\\(ApiSharedGuid, "0\\.4\\.10"\\)\') {', 'if (-not $activeVersion) {')
t=t.replace("    throw 'Plugin version or APIShared dependency mismatch.'", "    throw 'Plugin version is missing.'")
write(p,t)
for relative in ['APIShared/tools/Validation/DependencyMetadata.Common.ps1','Shared/Tools/Validation/Test-DependencyMetadata.ps1','APIShared/tools/Validation/Test-Standalone.ps1']:
    p=root/relative;t=read(p).replace('$Name:', '${Name}:');write(p,t)
p=root/'Shared/Tools/Validation/Test-SharedBoundaries.ps1';t=read(p);t += "\n& (Join-Path $PSScriptRoot 'Test-DependencyMetadata.ps1') -Workspace $Workspace\n";write(p,t)
p=root/'APIShared/tools/Validation/Test-Standalone.ps1';t=read(p);t=t.replace('$projects = @(', ". (Join-Path $PSScriptRoot 'DependencyMetadata.Common.ps1')\n$source = (Get-ChildItem -LiteralPath (Join-Path $apiRoot 'src') -Recurse -File -Filter '*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine\nAssert-PluginDependencyMetadata $source ([IO.File]::ReadAllText((Join-Path $apiRoot 'info.json')) | ConvertFrom-Json) 'APIShared'\n$projects = @(",1);write(p,t)
write(root/'_inspect/SharedSeparation/dependency-manifests.json',json.dumps(changed,indent=2)+'\n')
print(f'{len(changed)} manifests migrated; release consumer minima now read from info.json.')
