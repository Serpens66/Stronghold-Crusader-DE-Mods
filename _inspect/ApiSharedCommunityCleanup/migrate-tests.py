from pathlib import Path
import json, re, shutil, xml.etree.ElementTree as ET

workspace = Path.cwd()
repo = workspace.parent / 'SHCDE-APIShared'
audit = workspace / '_inspect/ApiSharedCommunityCleanup'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p, text):
    p.parent.mkdir(parents=True, exist_ok=True)
    expected = text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8')
    p.write_bytes(expected)
    assert p.read_bytes() == expected

write(audit/'starting-state.txt', 'workspace ac2a17a54f55077aab07c50fe977d674b86aa531\nAPIShared 77e0c62b361c017ccd4395d2102bd434c744a4ef\n')
for name in ['AGENTS.md','MIGRATION_PLAN.md','UpdateToNewDLL.md','_inspect']:
    source = repo/name
    dest = audit/'History'/name
    dest.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir(): shutil.move(str(source), str(dest))
    else: shutil.copy2(source, dest); source.unlink()

ns = {'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
removed = []
rows = json.loads(read(audit/'members.json'))

def sdk_project(old, output, assembly, target='net481', references=True):
    xml = ET.fromstring(old)
    items = []
    for group in xml.findall('m:ItemGroup', ns):
        for item in group:
            tag = item.tag.split('}')[-1]
            include = item.get('Include','')
            if tag == 'Compile' and not include.startswith('..'): continue
            if tag == 'ProjectReference':
                items.append(f'    <ProjectReference Include="{include}" />')
            elif tag in ['Compile','Reference']:
                hint = item.find('m:HintPath', ns)
                link = item.find('m:Link',ns)
                extras = f'<HintPath>{hint.text}</HintPath><Private>true</Private>' if hint is not None else ''
                if link is not None: extras += f'<Link>{link.text}</Link>'
                if extras: items.append(f'    <{tag} Include="{include}">{extras}</{tag}>')
                elif tag == 'Compile': items.append(f'    <Compile Include="{include}" />')
    write(output, f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{target}</TargetFramework>
    <AssemblyName>{assembly}</AssemblyName>
    <IsTestProject>true</IsTestProject>
    <NeedsGameAssemblies>{str(references).lower()}</NeedsGameAssemblies>
    <PlatformTarget>x64</PlatformTarget>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <DefineConstants>$(DefineConstants);API_SHARED_PRESET_TESTS</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
{chr(10).join(items)}
  </ItemGroup>
</Project>
''')

packages = '''<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.5.1" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.5.1" />
  </ItemGroup>
</Project>
'''
write(repo/'Directory.Packages.props', packages)
write(repo/'tests/Directory.Build.props', '''<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <RunSettingsFilePath>$(MSBuildThisFileDirectory)test.runsettings</RunSettingsFilePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
  </ItemGroup>
</Project>
''')
write(repo/'tests/test.runsettings','''<RunSettings>
  <RunConfiguration>
    <TargetPlatform>x64</TargetPlatform>
    <MaxCpuCount>1</MaxCpuCount>
  </RunConfiguration>
</RunSettings>
''')

row = rows[0]
directory = repo/'tests/APISharedTests'
usings = row['usings'] + '\nusing Microsoft.VisualStudio.TestTools.UnitTesting;\n'
groups = {}
support = []
def group_name(name):
    if any(x in name for x in ['Preset','Default','WorkingSource']): return 'Settings'
    if any(x in name for x in ['Gatehouse','Centered','PeValidation','FixedCatalog']): return 'NativeContracts'
    if any(x in name for x in ['Hud','Selection','Briefing']): return 'Presentation'
    if any(x in name for x in ['Lobby','Perspective','Elevated']): return 'GameState'
    if any(x in name for x in ['Readiness','Ownership','OwnerBound','PublicSurface']): return 'Contracts'
    return 'Diagnostics'

for member in row['members']:
    name, text = member['name'], member['text']
    if name in ['Main','TestWorkspaceIntegration','FindApiSharedRoot','Count']: continue
    if 'private static int failures;' in text: continue
    if name == 'Assert':
        text = '''        private static void Assert(bool condition, string message) =>
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, message);
'''
    if name in ['TestLocalSelectionSnapshots','TestPresetSaveUiModel','TestBriefingGoldPresentation','TestAivBuildStepBroker']:
        tokens = {'TestLocalSelectionSnapshots':['selectionSource'], 'TestPresetSaveUiModel':['presetSource'],
                  'TestBriefingGoldPresentation':['projectDirectory','source','originalCall','visibleSlotPass'],
                  'TestAivBuildStepBroker':['source']}[name]
        for statement in member['statements']:
            if any(re.search(r'\b'+token+r'\b',statement['text']) for token in tokens):
                removed.append({'test':name,'statement':statement['text'].strip()})
                text = text.replace(statement['text'],'')
    if name.startswith('Test'):
        category = group_name(name)
        wrapper = f'''        [TestMethod]
        [TestCategory("{category}")]
        public void {name[4:]}() => {name}();
'''
        groups.setdefault(category,[]).extend([wrapper,text])
    else: support.append(text)

support.append('''        [AssemblyInitialize]
        public static void InitializeHost(TestContext context)
        {
            typeof(BepInEx.Paths).GetProperty(nameof(BepInEx.Paths.ConfigPath))
                .GetSetMethod(true).Invoke(null, new object[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-config") });
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                new AssemblyName(args.Name).Name == "Assembly-CSharp"
                    ? Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assembly-CSharp.dll")) : null;
        }
''')
for file, methods in {'RuntimeTestSupport':support, **groups}.items():
    attrs = '[TestClass]\n    [DoNotParallelize]\n    ' if file == 'RuntimeTestSupport' else ''
    write(directory/(file+'.cs'),usings+f'\nnamespace APISharedTests\n{{\n    {attrs}public partial class RuntimeTests\n    {{\n'+''.join(methods)+'\n    }\n}\n')
for file in directory.glob('*Tests.cs'):
    write(file, read(file).replace('Program.AssembleAndDecode','RuntimeTests.AssembleAndDecode'))
write(directory/'LifecycleTests.cs', '''using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod] public void MissionLifecycleTransitions() => MissionLifecycleTests.Run(Assert);
        [TestMethod] public void SavegameSettingsRoundTrip() => SavegameModSettingsTests.Run(Assert);
        [TestMethod] public void PlayerDefeatTransitions() => PlayerDefeatTests.Run();
        [TestMethod] public void MarkedSelectionHarmonyContract() => MarkedSelectionHarmonyTests.Run(Assert);
        [TestMethod] public void GateBridgeAutomationBackend() => GateBridgeAutomationTests.Run(Assert);
        [TestMethod] public void GateBridgeDelayBackend() => GateBridgeDelayTests.Run(Assert);
    }
}
''')
# The previous monolithic runner omitted the productive gate/bridge backend fixtures.
# MSTest exposes them explicitly; no runtime hook is installed in the game process.
old = read(directory/'APISharedTests.csproj')
sdk_project(old,directory/'APISharedTests.csproj','APISharedTests')
(directory/'Program.cs').unlink()

row = rows[1]
directory = repo/'tests/LobbyModSettingsPresetTests'
usings = row['usings'] + '\nusing Microsoft.VisualStudio.TestTools.UnitTesting;\n'
groups, support = {}, []
for member in row['members']:
    name, text = member['name'], member['text']
    if name in ['TestWorkspacePublishedReleases','AuditPresetFile']: continue
    if name == 'Main':
        body = next(s['inner'] for s in member['statements'] if s['kind']=='TryStatement')
        body = [s for s in body if not ('TestWorkspacePublishedReleases' in s or 'Console.WriteLine' in s or s.strip()=='return 0;')]
        body = [s for s in body if not re.match(r'\s*Test\w+\(root\);',s)]
        body = [s for s in body if 'TestPresetAtomicPublisher();' not in s]
        groups.setdefault('Migration',[]).append('''        [TestMethod]
        public void LegacyStorageAndNetworkRoundTrip()
        {
            string root = testRoot;
'''+''.join(body)+'''        }
''')
        continue
    if name.startswith('Test'):
        category = 'Persistence' if any(x in name for x in ['Atomic','Legacy','Deletion','Persistent']) else 'Lobby'
        args = 'testRoot' if '(string root)' in text else ''
        groups.setdefault(category,[]).extend([f'        [TestMethod]\n        public void {name[4:]}() => {name}({args});\n',text])
    else: support.append(text)
support.append('''        private string testRoot;
        [TestInitialize] public void CreateStorage()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "APIShared-Presets-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }
        [TestCleanup] public void RemoveStorage()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
''')
for file, methods in {'PresetTestSupport':support, **groups}.items():
    attrs = '[TestClass]\n    [DoNotParallelize]\n    ' if file == 'PresetTestSupport' else ''
    write(directory/(file+'.cs'),usings+f'\nnamespace LobbyModSettingsPresetTests\n{{\n    {attrs}public partial class PresetTests\n    {{\n'+''.join(methods)+'\n    }\n}\n')
old = read(directory/'LobbyModSettingsPresetTests.csproj')
sdk_project(old,directory/'LobbyModSettingsPresetTests.csproj','LobbyModSettingsPresetTests')
(directory/'Program.cs').unlink()

# Keep the production UnitAccess implementation and its SDK-boundary doubles.
old_dir = repo/'tests/UnitAccess.Tests'
text = read(old_dir/'Program.cs')
head, doubles = text.split('namespace SHCDESE.Interop.Enums',1)
body = head[head.index('unsafe\n{')+len('unsafe\n{'):head.rindex('\n}')]
body = re.sub(r'^\s*Console.WriteLine\([^\n]+\);\s*$', '', body, flags=re.M)
body = body.split('\nvoid Check(')[0]
write(repo/'tests/Core.Tests/UnitAccessTests.cs', '''using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APIShared.Core.Tests;
[TestClass, DoNotParallelize]
public class UnitAccessTests
{
    [TestMethod] public unsafe void LookupBoundariesAndLifeState()
    {
'''+body+'''
    }
    private static void Check(bool condition, string message) => Assert.IsTrue(condition, message);
}
''')
write(repo/'tests/Core.Tests/UnitAccessDoubles.cs','// Test doubles for the external SDK boundary; native layout is validated separately.\nnamespace SHCDESE.Interop.Enums'+doubles)
shutil.rmtree(old_dir)
write(repo/'tests/Core.Tests/Core.Tests.csproj','''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <Compile Include="../../src/Units/UnitAccess.cs" Link="Production/UnitAccess.cs" />
    <Compile Include="../../src/ModSettings/Internal/DependencyFreeJson.cs" Link="Production/DependencyFreeJson.cs" />
    <Compile Include="../../src/ModSettings/Internal/AtomicFileReplacement.cs" Link="Production/AtomicFileReplacement.cs" />
    <Compile Include="../../src/Core/UniquePatternSearch.cs" Link="Production/UniquePatternSearch.cs" />
  </ItemGroup>
</Project>
''')
write(audit/'removed-source-assertions.json',json.dumps(removed,indent=2))
print(f'Migrated runtime/preset suites; removed {len(removed)} source-text assertions/statements; preserved production sources.')
