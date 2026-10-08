from pathlib import Path
import json,re
root=Path.cwd()
exclude={'.git','shcde-script-extender','tests','examples','bin','obj','BepInEx','.tools','.inspect','_inspect','.native-analysis','.release-output','before'}
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,t):
    with p.open('w',encoding='utf-8',newline='') as f:f.write(t.replace('\r\n','\n').replace('\n','\r\n'))
modified=[]
for project in root.rglob('*.csproj'):
    if exclude.intersection(project.relative_to(root).parts):continue
    sources=[s for s in project.parent.rglob('*.cs') if not exclude.intersection(s.relative_to(project.parent).parts)]
    source='\n'.join(read(s) for s in sources)
    if '[BepInPlugin(' not in source:continue
    info=project.parent/'info.json'
    if not info.exists():
        infos=list((project.parent/'BepInEx/plugins').rglob('info.json'))
        if not infos:continue
        assert len(infos)==1,(project,infos)
        info=infos[0]
    data=json.loads(read(info))
    constants=dict(re.findall(r'\bconst\s+string\s+(\w+)\s*=\s*"([^"\n]+)"',source))
    requirements=[]
    for guid,argument in re.findall(r'\[BepInDependency\(\s*("[^"\n]+"|\w+)\s*(?:,\s*("[^"\n]+"|[^)]+))?\)\]',source):
        if 'SoftDependency' in argument:continue
        guid=guid.strip('"') if guid.startswith('"') else constants[guid]
        minimum=argument.strip().strip('"') if argument.strip().startswith('"') else constants.get(argument.strip(),'')
        if guid=='000shcdese':
            if minimum and 'MinimumScriptExtenderVersion' not in data:data['MinimumScriptExtenderVersion']=minimum
            continue
        requirement={'GUID':guid}
        if minimum:requirement['MinimumVersion']=minimum
        assert not any(r['GUID']==guid for r in requirements),(project,guid)
        requirements.append(requirement)
    if requirements:data['Dependencies']=requirements
    elif 'Dependencies' in data:del data['Dependencies']
    new=json.dumps(data,ensure_ascii=False,indent=2)+'\n'
    if new!=read(info):
        write(info,new);modified.append(str(info.relative_to(root)))
write(root/'_inspect/SharedSeparation/dependency-manifests.json',json.dumps(modified,indent=2)+'\n')
p=root/'Shared/Tools/Release/Release.Common.ps1';t=read(p)
t=t.replace("    if (-not (Test-Path -LiteralPath $infoPath)) { return $null }",'''    if (-not (Test-Path -LiteralPath $infoPath)) {
        $infos = @(Get-ChildItem -LiteralPath (Join-Path (Join-Path $Config.Root $directory) 'BepInEx\\plugins') -Recurse -File -Filter 'info.json' -ErrorAction SilentlyContinue)
        if ($infos.Count -eq 0) { return $null }
        if ($infos.Count -ne 1) { throw "Ambiguous source manifest for $ModName" }
        $infoPath = $infos[0].FullName
    }''')
write(p,t)
p=root/'Shared/Tools/Validation/Test-DependencyMetadata.ps1';t=read(p)
t=t.replace("    if (-not (Test-Path -LiteralPath $manifestPath)) { continue }",'''    if (-not (Test-Path -LiteralPath $manifestPath)) {
        $infos = @(Get-ChildItem -LiteralPath (Join-Path $project.Directory.FullName 'BepInEx\\plugins') -Recurse -File -Filter 'info.json' -ErrorAction SilentlyContinue)
        if ($infos.Count -eq 0) { continue }
        if ($infos.Count -ne 1) { throw "Ambiguous source manifest for $($project.BaseName)" }
        $manifestPath = $infos[0].FullName
    }''')
write(p,t)
p=root/'_inspect/APISharedTests/WorkspaceIntegration.cs';t=read(p)
t=t.replace('            Func<string, string, bool> apiDependencyMatchesRelease', '            string releaseConfig = File.ReadAllText(Path.Combine(workspace, "Shared", "Tools", "Release", "release-projects.json"));\n            Func<string, string, bool> apiDependencyMatchesRelease')
t=t.replace('                var metadata = (Dictionary<string, object>)DependencyFreeJson.Parse(\n                    File.ReadAllText(Path.Combine(workspace, consumer == "ActiveAIVDetector" ? "Helpers/ActiveAIVDetector" : consumer, "info.json")));', '''                string directory = Path.Combine(workspace, consumer == "ActiveAIVDetector" ? "Helpers/ActiveAIVDetector" : consumer);
                string info = Path.Combine(directory, "info.json");
                if (!File.Exists(info)) info = Directory.GetFiles(Path.Combine(directory, "BepInEx", "plugins"), "info.json", SearchOption.AllDirectories).Single();
                var metadata = (Dictionary<string, object>)DependencyFreeJson.Parse(File.ReadAllText(info));''')
write(p,t)
print(f'{len(modified)} source manifests updated, including mods whose authoritative info.json is in the tracked package folder.')
