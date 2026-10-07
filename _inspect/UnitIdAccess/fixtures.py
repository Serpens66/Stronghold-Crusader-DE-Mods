from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
for project,namespace in [('BugfixesAndQoL','BugfixesAndQoL'),('Testmods/MoatMove','MoatMove')]:
    testdir=ROOT/project/'tests'
    if project=='BugfixesAndQoL':testdir=testdir/'FriendlyMoatMovement.Tests'
    p=testdir/'Program.cs';s=p.read_text(encoding='utf-8-sig')
    marker='var compilation = CSharpCompilation.Create('
    assert marker in s
    # Each grid fixture models GameUnit locally. Alias only the fixture types; compile the
    # production helper body unchanged, with internal visibility for the internal fixture records.
    tree=f'''CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared", "src", "UnitAccess.cs"))
        .Replace("using SHCDESE.API;", "using GameUnitManagerAPI = {namespace}.GameUnitManagerAPI;")
        .Replace("using SHCDESE.Interop;", "using GameUnit = {namespace}.GameUnit;")
        .Replace("public static unsafe class UnitAccess", "internal static unsafe class UnitAccess")),
    CSharpSyntaxTree.ParseText("namespace BepInEx.Logging {{ public class ManualLogSource {{ public void LogDebug(object message) {{ }} }} }}"),
    '''
    # Locate the syntax-tree initializer passed to the runtime compilation.
    start=s.index(marker); initializer=s.index('new[]',start); open_brace=s.index('{',initializer)
    s=s[:open_brace+1]+'\n    '+tree+s[open_brace+1:]
    write(p,s)
p=ROOT/'_inspect/Test-ExtraFeaturesSessionCallbacks.ps1';s=p.read_text(encoding='utf-8-sig')
s=s.replace('public class ManualLogSource {}','public class ManualLogSource { public void LogDebug(object message){} }')
s=s.replace('public uint GetDefaultHealth(eChimps type)=>BaseHealth;','public bool IsValidId(int id)=>id>0&&id<16;\n        public uint GetDefaultHealth(eChimps type)=>BaseHealth;')
write(p,s)
p=ROOT/'_inspect/ExtraFeaturesSessionTests/ExtraFeaturesSessionTests.csproj';s=p.read_text(encoding='utf-8-sig')
marker='<Compile Include="GeneratedHarness.cs"'
assert marker in s
s=s.replace(marker,'<Compile Include="..\\..\\APIShared\\src\\UnitAccess.cs" Link="UnitAccess.cs" /><Compile Include="GeneratedHarness.cs"',1)
write(p,s)
print('Grid and session fixtures compile the productive helper with their isolated memory types.')
