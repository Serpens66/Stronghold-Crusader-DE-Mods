from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path.cwd()
ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}

def read(p):
    return p.read_text(encoding='utf-8-sig')

def write(p, text):
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open('w', encoding='utf-8', newline='') as f:
        f.write(text.replace('\r\n', '\n').replace('\n', '\r\n'))

def take_method(text, name):
    start = re.search(r'^        private static [^\n]*\b' + re.escape(name) + r'\(', text, re.M).start()
    match = re.search(r'^        private static ', text[start + 1:], re.M)
    end = start + 1 + match.start() if match else text.rindex('\n    }')
    return text[:start] + text[end:], text[start:end]

def convert(kind, names, hook):
    old = root / '_inspect' / kind
    new = root / 'APIShared/tests' / kind
    program = read(old / 'Program.cs')
    header = program[:program.index('namespace ')]
    members = []
    for name in names:
        program, member = take_method(program, name)
        members.append(member)
    program = program.replace('internal static class Program', 'internal static partial class Program')
    program = program.replace(names[0] + '();', hook + '();')
    program = program.replace('    internal static partial class Program\n    {', '    internal static partial class Program\n    {\n        static partial void ' + hook + '();')
    workspace = header + 'namespace ' + kind + '\n{\n    internal static partial class Program\n    {\n' + ''.join(members) + '    }\n}\n'
    workspace = workspace.replace('private static void ' + names[0] + '()', 'static partial void ' + hook + '()')
    write(old / 'WorkspaceIntegration.cs', workspace)
    if kind == 'APISharedTests':
        program = program.replace('Assembly-CSharp-publicized.dll', 'Assembly-CSharp.dll')
        program = program.replace('FindWorkspaceRoot()', 'FindApiSharedRoot()')
        program = re.sub(r'FindApiSharedRoot\(\),\s*"APIShared",\s*', 'FindApiSharedRoot(), ', program)
        program = program.replace('Path.Combine(FindApiSharedRoot(), "APIShared")', 'FindApiSharedRoot()')
        finder = '''        private static string FindApiSharedRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "APIShared.csproj"))) return directory.FullName;
                if (File.Exists(Path.Combine(directory.FullName, "APIShared", "APIShared.csproj")))
                    return Path.Combine(directory.FullName, "APIShared");
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("APIShared source root was not found.");
        }

'''
        pos = program.index('        private static int Count(')
        program = program[:pos] + finder + program[pos:]
    else:
        program = program.replace('using Shared;', 'using APIShared.Internal;')
    write(new / 'Program.cs', program)
    (old / 'Program.cs').unlink()
    project = read(old / (kind + '.csproj'))
    # Core sources have one owner; the workspace project links those sources plus its integration partial.
    core_files = []
    for file in old.glob('*.cs'):
        if file.name == 'WorkspaceIntegration.cs':
            continue
        write(new / file.name, read(file))
        file.unlink()
        core_files.append(file.name)
    core_files.append('Program.cs')
    project = project.replace('..\\..\\APIShared\\', '..\\..\\')
    project = project.replace('<PropertyGroup><ExtenderDir>$(MSBuildThisFileDirectory)..\\..\\shcde-script-extender\\src\\SHCDESE.BepInEx\\bin\\net481</ExtenderDir></PropertyGroup>', '<PropertyGroup Condition="\'$(ExtenderDir)\' == \'\'"><ExtenderDir>$(GameDir)\\BepInEx\\plugins\\000shcdese</ExtenderDir></PropertyGroup>')
    project = project.replace('<ExtenderDir>$(MSBuildThisFileDirectory)..\\..\\shcde-script-extender\\src\\SHCDESE.BepInEx\\bin\\net481</ExtenderDir>', '<ExtenderDir Condition="\'$(ExtenderDir)\' == \'\'">$(GameDir)\\BepInEx\\plugins\\000shcdese</ExtenderDir>')
    project = project.replace('$(MSBuildThisFileDirectory)..\\..\\shcde-script-extender\\deps\\Assembly-CSharp-publicized.dll', '$(GameDir)\\Stronghold Crusader Definitive Edition_Data\\Managed\\Assembly-CSharp.dll')
    if kind != 'APISharedTests':
        project = re.sub(r'\s*<Compile Include="..\\..\\Shared\\[^\"]+"[^>]*>.*?</Compile>', '', project)
        project = project.replace('..\\HostClientPresetTests\\MissionTestEnvironment.cs', 'MissionTestEnvironment.cs')
        write(new / 'MissionTestEnvironment.cs', read(root / '_inspect/HostClientPresetTests/MissionTestEnvironment.cs'))
        core_files.append('MissionTestEnvironment.cs')
        project = project.replace('E:\\ProgrammeE\\Steam\\steamapps\\common\\Stronghold Crusader Definitive Edition\\BepInEx\\plugins\\000shcdese\\R3.dll', '$(ExtenderDir)\\R3.dll')
    write(new / (kind + '.csproj'), project)
    # Reuse core project contents but make every local source link explicit for the workspace-only harness.
    workspace_project = project.replace('..\\..\\src\\', '..\\..\\APIShared\\src\\').replace('Include="..\\..\\APIShared.csproj"', 'Include="..\\..\\APIShared\\APIShared.csproj"')
    for name in core_files:
        workspace_project = workspace_project.replace('Compile Include="' + name + '"', 'Compile Include="..\\..\\APIShared\\tests\\' + kind + '\\' + name + '"')
    workspace_project = workspace_project.replace('<Import Project="$(MSBuildToolsPath)\\Microsoft.CSharp.targets" />', '<ItemGroup><Compile Include="WorkspaceIntegration.cs" /></ItemGroup>\n  <Import Project="$(MSBuildToolsPath)\\Microsoft.CSharp.targets" />')
    write(old / (kind + '.csproj'), workspace_project)

convert('APISharedTests', ['TestMigrationContracts', 'FindWorkspaceRoot', 'FindRuntimeProjectFiles'], 'TestWorkspaceIntegration')
convert('LobbyModSettingsPresetTests', ['ValidatePublishedReleaseSchemaContracts', 'FindWorkspaceRoot', 'ReadGitFile'], 'TestWorkspacePublishedReleases')

old = root / '_inspect/APISharedPresetConsumerTests'
new = root / 'APIShared/tests/PublicPresetConsumer'
for file in old.glob('*.cs'):
    write(new / file.name, read(file))
    file.unlink()
project = read(old / 'APISharedPresetConsumerTests.csproj')
write(new / 'APISharedPresetConsumerTests.csproj', project.replace('..\\..\\APIShared\\BepInEx', '..\\..\\BepInEx'))
write(old / 'APISharedPresetConsumerTests.csproj', project.replace('Compile Include="ConsumerSettings.cs"', 'Compile Include="..\\..\\APIShared\\tests\\PublicPresetConsumer\\ConsumerSettings.cs"'))
print('API-owned test sources moved; workspace integration checks remain linked to the same core sources.')
