"""Refresh this mod's declared production sources; retain its maintained tests and SDK policy."""
from pathlib import Path
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[1]
ns = 'http://schemas.microsoft.com/developer/msbuild/2003'
ET.register_namespace('', ns)
def tag(name): return '{'+ns+'}'+name
project = ET.parse(root/'PopularityTest.csproj').getroot()
for group in project.findall(tag('ItemGroup')):
    for entry in list(group.findall(tag('Compile'))):
        group.remove(entry)
group = ET.SubElement(project,tag('ItemGroup'))
for file in sorted((root/'src').glob('*.cs')):
    ET.SubElement(group,tag('Compile'),Include='src/'+file.name)
for name in ('Localization/SerpLocalization.cs','Diagnostics/DebugLogHelper.cs'):
    ET.SubElement(group,tag('Compile'),Include='../../Shared/Runtime/'+name)
ET.indent(project,space='  ')
text = ET.tostring(project,encoding='unicode')+'\n'
(root/'PopularityTest.csproj').write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
print('Refreshed production source links; maintained tests, SDK selection and historical evidence retained.')
