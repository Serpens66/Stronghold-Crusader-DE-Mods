"""Prepare local project files without building or altering dependencies."""
from pathlib import Path
import xml.etree.ElementTree as ET
import json
import hashlib
import os

root = Path(__file__).resolve().parents[1]
workspace = root.parents[1]
ns = 'http://schemas.microsoft.com/developer/msbuild/2003'
ET.register_namespace('', ns)
def tag(name): return '{'+ns+'}'+name
def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='\r\n') as out: out.write(data.replace('\r\n','\n'))

# Preserve the standard game/reference layout; replace all mod-specific entries.
tree = ET.parse(workspace/'Testmods/CrenelatedWallTest/CrenelatedWallTest.csproj')
project = tree.getroot()
for group in list(project.findall(tag('ItemGroup'))):
    if group.findall(tag('Compile')): project.remove(group)
for node in project.iter():
    if node.text: node.text = node.text.replace('CrenelatedWallTest','PopularityTest')
    for key,value in list(node.attrib.items()): node.attrib[key] = value.replace('CrenelatedWallTest','PopularityTest')
group = ET.SubElement(project, tag('ItemGroup'))
for file in sorted((root/'src').glob('*.cs')):
    ET.SubElement(group,tag('Compile'),Include='src/'+file.name)
ET.SubElement(group,tag('Compile'),Include='../../Shared/Runtime/Localization/SerpLocalization.cs')
for pattern in ['Override/**/*.xaml','Locales/*.txt']:
    entry = ET.SubElement(group,tag('Content'),Include=pattern)
    ET.SubElement(entry,tag('CopyToOutputDirectory')).text='PreserveNewest'
write(root/'PopularityTest.csproj',ET.tostring(project,encoding='unicode'))

# Reuse the maintained preset harness read-only, adding this mod's actual settings.
# Rewrite relative source paths; the harness remains a separate test assembly.
harness = workspace/'_inspect/HostClientPresetTests/HostClientPresetTests.csproj'
tree = ET.parse(harness)
project = tree.getroot()
for node in project.iter():
    if node.tag in (tag('Compile'),tag('ProjectReference')) and 'Include' in node.attrib:
        path = Path(node.attrib['Include'].replace('\\','/'))
        if '$(' not in str(path): node.set('Include',os.path.relpath((harness.parent/path).resolve(), root/'tests'))
    if node.tag == tag('AssemblyName'): node.text='PopularityTest.PresetTests'
    if node.tag == tag('OutputPath'): node.text='bin/PresetTests/'
group = ET.SubElement(project,tag('PropertyGroup'))
ET.SubElement(group,tag('StartupObject')).text='PopularityTest.Tests.PresetEntry'
group=ET.SubElement(project,tag('ItemGroup'))
for name in ['../src/PopularitySettings.cs','../src/OwnedOverride.cs','../src/OverrideBatch.cs','PresetIntegration.cs']:
    ET.SubElement(group,tag('Compile'),Include=name)
write(root/'tests/PopularityTest.PresetTests.csproj',ET.tostring(project,encoding='unicode'))

# First-run immutable snapshot. Re-running preparation never updates the baseline.
snapshot=root/'tests/dependency-before.json'
if not snapshot.exists():
    api=workspace/'APIShared'
    installed=Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/APIShared_Serp')
    def state(folder):
        files = {p for p in folder.iterdir() if p.is_file() and not p.name.startswith('.')}
        selected = ['src','Properties','Override','Patches','Locales','BepInEx/plugins/APIShared_Serp'] if folder == api else []
        if folder != api:
            files.update(p for p in folder.rglob('*') if p.is_file())
        for name in selected:
            location = folder/name
            if location.exists(): files.update(p for p in location.rglob('*') if p.is_file())
        return {str(p.relative_to(folder)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
    write(snapshot,json.dumps({'source':state(api),'installed':state(installed)},indent=2)+'\n')
print('Prepared runtime project, read-only preset harness integration and dependency identity snapshot.')
