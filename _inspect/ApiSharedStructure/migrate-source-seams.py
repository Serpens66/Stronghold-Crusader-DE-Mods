import pathlib,re
root=pathlib.Path.cwd()
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode())
friendly=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests'
for p in friendly.glob('*.cs'):
 s=p.read_text(encoding='utf-8-sig')
 s=re.sub(r'File.ReadAllText\(Path.Combine\(\s*root,\s*"APIShared/src/UnitCommands/(?:Formation/)?(FormationRuntime|UnitCommandPathRuntime)\.cs"\)\)',lambda m:'FeatureSourceModel.Read(root, "'+m[1]+'")',s)
 write(p,s)
p=root/'BugfixesAndQoL/tests/Program.cs';s=p.read_text(encoding='utf-8-sig')
for name,part,pattern in [('FormationRuntime','Formation','FormationRuntime.*.cs'),('UnitCommandPathRuntime','','UnitCommandPathRuntime.*.cs'),('MoatWorkTargetSelection','Moat','UnitCommandPathRuntime.WorkTarget*.cs')]:
 regex=r'File.ReadAllText\(Path.Combine\([^;]*?"APIShared",\s*"src",\s*"UnitCommands",\s*(?:"Formation",\s*|"Moat",\s*)?"'+name+r'\.cs"\)\)'
 s=re.sub(regex,'ReadApiFeature("UnitCommands", "'+part+'", "'+pattern+'")',s)
index=s.index('private static int ')
s=s[:index]+'''private static string ReadApiFeature(string area, string subject, string pattern) => string.Join("\\n",
            Array.ConvertAll(Directory.GetFiles(Path.Combine(FindProjectDirectory(), "..", "APIShared", "src", area, subject),
                pattern, SearchOption.AllDirectories), File.ReadAllText));

        '''+s[index:]
write(p,s)
p=root/'BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=p.read_text(encoding='utf-8-sig')
anchor='string shared = Path.Combine(Directory.GetParent(main).FullName, "APIShared", "src", "UnitCommands");'
s=s.replace(anchor,anchor+'''
        string ReadSharedSource(string filename) => File.ReadAllText(Directory.GetFiles(shared, filename, SearchOption.AllDirectories).Single());''')
s=s.replace('File.ReadAllText(Path.Combine(shared, "FormationRuntime.cs"))','string.Join("\\n", Array.ConvertAll(Directory.GetFiles(shared, "FormationRuntime.*.cs", SearchOption.AllDirectories), File.ReadAllText))')
s=re.sub(r'File.ReadAllText\(Path.Combine\(shared,\s*"([^"\n]+)"\)\)',r'ReadSharedSource("\1")',s)
write(p,s)
p=root/'_inspect/APISharedTests/WorkspaceIntegration.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('File.ReadAllText(Path.Combine(workspace, "APIShared", "src", "Presentation", "UnitHud", "UnitHudPresentationCapability.cs"))','string.Join("\\n", Array.ConvertAll(Directory.GetFiles(Path.Combine(workspace, "APIShared", "src", "Presentation", "UnitHud"), "*.cs"), File.ReadAllText))')
write(p,s)
