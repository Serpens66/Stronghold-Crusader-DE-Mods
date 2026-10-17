"""Independent catalog/UI/dependency checks; writes no dependency files."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
workspace = root.parents[1]
def read(path): return path.read_text(encoding='utf-8-sig')
source = read(workspace/'shcde-script-extender/src/SHCDESE.BepInEx/GameGlobals/GameGlobalsManager.cs')
se = {name:int(value) for name,value in re.findall(r'public ManagedValue<int> (\w+) \{ get; \} = new\((-?\d+)\);',source)
      if 'PopModifier' in name or name == 'FairsPopularityEventModifier'}
catalog = re.findall(r'new PopularityParameter\("(\w+)", "(\w+)", (-?\d+), (\d+), g => g\.(\w+), s => s\.(\w+)Enabled, s => s\.(\w+)\)', read(root/'src/PopularityCatalog.cs'))
assert len(catalog) == len(se) == 41
assert {entry[0] for entry in catalog} == set(se)
limits={'Rations':16,'Variety':6,'Crowding':20,'Taxes':48,'Religion':16,'Ale':16,'Fairs':32,'Fear':2}
settings=read(root/'src/PopularitySettings.cs')
for name,group,default,limit,target,toggle,value in catalog:
    assert name == target == toggle == value
    assert int(default)*25 == se[name], name+' native/SE default'
    assert int(limit) == limits[group]
    assert '[SyncHostOnly]\n        public int '+name in settings
    assert '[SyncHostOnly]\n        public bool '+name+'Enabled' in settings
    assert f'private int {name[0].lower()+name[1:]} = {default};' in settings
    assert f'{name}Enabled = false; {name} = {default};' in settings

xaml=ET.parse(root/'Override/ScriptExtenderUI/PopularitySettings.xaml')
ns={'p':'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
sliders=xaml.findall('.//p:Slider',ns)
assert len(sliders) == 41
for slider in sliders:
    assert slider.get('TickFrequency') == slider.get('SmallChange') == slider.get('LargeChange') == '1'
    assert slider.get('IsSnapToTickEnabled') == 'True'
    name=re.search(r'Binding (\w+),',slider.get('Value')).group(1)
    limit=next(int(item[3]) for item in catalog if item[0] == name)
    assert int(slider.get('Minimum')) == -limit and int(slider.get('Maximum')) == limit
    parent=next(node for node in xaml.iter() if slider in list(node))
    controls=[node for node in parent if node.tag.rsplit('}',1)[-1] in ['CheckBox','Slider','TextBox']]
    assert [node.tag.rsplit('}',1)[-1] for node in controls] == ['CheckBox','Slider','TextBox']
    assert controls[0].get('Grid.Column') == '0' and controls[1].get('Grid.Column') == '1'
    assert controls[0].get('IsChecked') == '{Binding '+name+'Enabled, Mode=TwoWay}'
    assert slider.get('IsEnabled') == controls[2].get('IsEnabled') == '{Binding '+name+'Editable}'
for node in xaml.iter():
    if node.tag.rsplit('}',1)[-1] in ['CheckBox','Slider','TextBox','Button','ComboBox']:
        assert node.get('ToolTipService.ShowDuration') == '60000' and node.get('ToolTip')
viewer=xaml.find('.//p:ScrollViewer',ns)
assert viewer.get('HorizontalScrollBarVisibility') == viewer.get('VerticalScrollBarVisibility') == 'Auto'
keys=None
for locale in (root/'Locales').glob('*.txt'):
    lines=read(locale).splitlines()
    current={line.split('=',1)[0] for line in lines if '=' in line}
    assert keys is None or current == keys, str(locale)
    keys=current
    for name,*_ in catalog:
        assert 'Popularity.'+name in current and 'Popularity.'+name+'.Help' in current
for file in root.rglob('*'):
    if not file.is_file() or any(part in ['obj','bin','BepInEx'] for part in file.relative_to(root).parts): continue
    if file.suffix not in ['.cs','.csproj','.json','.xaml','.txt','.ps1','.py','.bat','.md']: continue
    body=file.read_bytes()
    assert not re.search(rb'(?<!\r)\n',body), str(file)+' bare LF'
    if file.suffix in ['.cs','.csproj'] and 'tests' not in file.relative_to(root).parts:
        text=body.decode('utf-8-sig')
        assert not re.search(r'Newtonsoft|System\.Text\.Json|JsonUtility|JavaScriptSerializer|Assembly-CSharp-publicized|\b(?:OnDestroy|OnDisable|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(',text), str(file)
snapshot=json.loads(read(root/'tests/dependency-display-before.json'))
def state(folder):
    files={p for p in folder.iterdir() if p.is_file() and not p.name.startswith('.')}
    if folder == workspace/'APIShared':
        for name in ['src','Properties','Override','Patches','Locales','BepInEx/plugins/APIShared_Serp']:
            location=folder/name
            if location.exists(): files.update(p for p in location.rglob('*') if p.is_file())
    else: files.update(p for p in folder.rglob('*') if p.is_file())
    return {str(p.relative_to(folder)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
installed=Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/APIShared_Serp')
for name,folder in [('source',workspace/'APIShared'),('installed',installed)]:
    current=state(folder)
    changed=[key for key in set(current)|set(snapshot[name]) if current.get(key)!=snapshot[name].get(key)]
    assert not changed, name+' APIShared changed: '+', '.join(changed)
print('PASS: 41 independent SE/catalog defaults and ranges; checkbox/slider/numeric layout, XAML/tooltips/locales, lifecycle/CRLF and unchanged APIShared source/install.')
