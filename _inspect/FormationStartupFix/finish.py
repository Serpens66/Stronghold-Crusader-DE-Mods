from pathlib import Path
import shutil
root=Path.cwd()
def read(p):
    f=root/p; backup=root/'_inspect/FormationStartupFix/before'/p
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(f,backup)
    return f.read_text(encoding='utf-8-sig')
def write(p,s):
    f=root/p
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    f.write_bytes(data)
    assert f.read_bytes()==data
p='BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/CadenceSnapshotTests.cs'
s=(root/p).read_text()
start=s.index('    internal readonly struct NativeResolution')
end=s.index('    internal static class DebugLogHelper',start)
s=s[:start]+s[end:]
s=s.replace('internal sealed class OptionalFixture','internal sealed unsafe class OptionalFixture')
write(p,s)
p='BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/Program.cs';s=read(p)
s=s.replace('RallyTerrainGeneratorTests.Validate(root);','RallyTerrainGeneratorTests.Validate(root);\nCadenceSnapshotTests.Validate(root);\nFormationStartupTests.Validate(root);')
write(p,s)
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
s=s.replace('CommandParameter="Formations"','CommandParameter="Aufstellung"').replace('Text="Formation"','Text="Aufstellung"')
write(p,s)
for f in (root/'BugfixesAndQoL/Locales').glob('*.txt'):
    p=str(f.relative_to(root));s=read(p)
    if f.name=='de-DE.txt':
        s=s.replace('Move-Formation und vollstaendige Zielmarker aktivieren','Aufstellung und vollstaendige Zielmarker aktivieren')
        s=s.replace('Waehle Formation, Dichte und Rollenplatzierung','Waehle Aufstellung, Dichte und Rollenplatzierung')
    else:
        s=s.replace('Enable Move formation and complete target markers','Enable unit arrangement and complete target markers')
        s=s.replace('Choose formation, density and role placement','Choose unit arrangement, density and role placement')
    write(p,s)
