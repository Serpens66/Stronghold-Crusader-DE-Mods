"""Structural asset checks only; compiled signatures and behavior have separate tests."""
from pathlib import Path
import re
import math
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[1]
ns = {'p': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
xaml = ET.parse(root/'Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml')
sliders = [slider for node in xaml.iter() if node.get('{clr-namespace:APIShared.ModSettings;assembly=APIShared}ModSettingsSearch.Key', '').startswith('extra.popularity.') for slider in node.findall('.//p:Slider', ns)]
assert sliders, 'No settings sliders found'
for slider in sliders:
    binding = slider.get('Value', '')
    name = re.search(r'Binding (\w+)', binding).group(1)
    assert float(slider.get('Minimum')) < float(slider.get('Maximum')), name
    step = '1'  # Declared popularity values are whole report points.
    # Tick/input precision is a user-visible contract; page increments may vary.
    for key in ('TickFrequency', 'SmallChange'):
        assert float(slider.get(key)) == float(step), (name, key)
    large = float(slider.get('LargeChange'))
    assert math.isfinite(large) and large >= float(step), (name, 'LargeChange')
    assert math.isclose(large / float(step), round(large / float(step))), (name, 'LargeChange')
    assert slider.get('IsSnapToTickEnabled') == 'True', name
for node in xaml.iter():
    if node.tag.rsplit('}', 1)[-1] in ('CheckBox', 'Slider', 'TextBox', 'Button', 'ComboBox'):
        assert node.get('ToolTipService.ShowDuration') == '60000' and node.get('ToolTip'), node.attrib
viewers = xaml.findall('.//p:ScrollViewer', ns)
assert any(v.get('HorizontalScrollBarVisibility') == v.get('VerticalScrollBarVisibility') == 'Auto' for v in viewers)
keys = None
for locale in (root/'Locales').glob('*.txt'):
    current = {line.split('=',1)[0] for line in locale.read_text(encoding='utf-8-sig').splitlines() if '=' in line}
    assert keys is None or current == keys, str(locale)
    keys = current
for file in root.rglob('*'):
    if not file.is_file() or any(part in ('obj','bin','BepInEx') for part in file.relative_to(root).parts): continue
    if file.suffix not in ('.cs','.csproj','.json','.xaml','.txt','.ps1','.py','.bat','.md'): continue
    assert not re.search(rb'(?<!\r)\n', file.read_bytes()), str(file)+' bare LF'
print('PASS: declared slider steps/ranges, tooltips, scrolling, locale parity and CRLF. Compiled tests validate bindings and settings behavior.')
