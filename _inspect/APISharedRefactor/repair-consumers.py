from pathlib import Path
import re, subprocess
ROOT=Path(__file__).resolve().parents[2]
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    p.write_bytes(data)
    assert p.read_bytes()==data and not re.search(rb'(?<!\r)\n',data)
tracked=subprocess.check_output(['git','ls-files'],cwd=ROOT,text=True).splitlines()
for rel in tracked:
    p=ROOT/rel
    if not p.exists() or p.suffix not in ['.cs','.ps1','.csproj']: continue
    if rel.startswith(('shcde-script-extender/','_inspect/CrusaderDE-Native-Baseline/','.native-analysis/','.tools/')): continue
    if any(x in p.parts for x in ['obj','bin','BepInEx']): continue
    s=old=read(p)
    s=s.replace('Shared.PresetLocal','APIShared.ModSettings.PresetLocal')
    if p.suffix=='.cs':
        # C# source assertions inspect the whole subsystem rather than its former single file.
        pat=r'File\.ReadAllText\((Path\.Combine\([^;]*?"PresetLobbyModSettingsViewModel\.cs"\))\)'
        s=re.sub(pat,lambda m:'string.Join("\\n", Directory.GetFiles(Path.GetDirectoryName('+m.group(1)+'), "*.cs").Select(File.ReadAllText))',s)
        if s!=old and '.Select(File.ReadAllText)' in s and 'using System.Linq;' not in s:
            s='using System.Linq;\n'+s
        if 'string policySource = File.ReadAllText(' in s:
            s=s.replace('"GameModes", "GameplayModModePolicy.cs"','"SerpsMods", "SerpsModProfiles.cs"')
    if p.suffix=='.ps1':
        s=re.sub(r'Get-Content -LiteralPath ([^\n]*PresetLobbyModSettingsViewModel\.cs[^\n]*) -Raw',r'Get-Content -LiteralPath \1 -Raw',s)
    if s!=old: write(p,s)
# Offline fixtures provide the same types as the moved real assembly.
for rel in ['_inspect/MissionStateTests/Program.cs','_inspect/MissionServiceTests/Environment.cs']:
    p=ROOT/rel; write(p,read(p).replace('namespace Shared','namespace APIShared.GameModes'))
p=ROOT/'APIShared/src/Core/ModApiClient.cs';s=read(p).replace('private readonly IApiShared api;','private readonly ApiSharedRuntime api;').replace('string ownerGuid, IApiShared api','string ownerGuid, ApiSharedRuntime api').replace('ApiSharedRuntime.ProcessInstance.WhenReady','api.WhenReady');write(p,s)
p=ROOT/'APIShared/src/Core/Contracts.cs';write(p,read(p).replace('new ModApiClient(ownerGuid, Current)','new ModApiClient(ownerGuid, ApiSharedRuntime.ProcessInstance)'))
# Existing historical source snapshots are evidence, not consumers.
for rel in ['_inspect/StatsTweakerApiTests/review-installed-apishared.cs']:
    original=subprocess.check_output(['git','show','HEAD:'+rel],cwd=ROOT)
    (ROOT/rel).write_bytes(original)
# Choose runtime and classic framework tests with an actual APIShared dependency.
projects=[]
for rel in tracked:
    p=ROOT/rel
    if p.suffix!='.csproj' or not p.exists() or rel.startswith(('shcde-script-extender/','_inspect/CrusaderDE-Native-Baseline/')): continue
    s=read(p)
    if '<Project Sdk=' in s: continue
    if 'Reference Include="APIShared"' in s or 'APIShared\\src\\' in s or 'APIShared.csproj' in s:
        projects.append(rel)
projects=sorted(set(projects)-{'APIShared/APIShared.csproj'})
write(ROOT/'_inspect/APISharedRefactor/projects.txt','APIShared/APIShared.csproj\n'+'\n'.join(projects)+'\n')
p=ROOT/'Shared/UnitCommandSourceChecks/Program.cs';s=read(p)
marker='var compilations = new Dictionary<string, CSharpCompilation>();'
if '--projects-file' not in s:
    s=s.replace(marker,'if (args.Contains("--projects-file"))\n    projects = File.ReadAllLines(Path.Combine(root, args[Array.IndexOf(args, "--projects-file") + 1])).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();\n'+marker)
write(p,s)
print('Consumer fixtures repaired; compilation inventory contains',len(projects)+1,'projects.')
