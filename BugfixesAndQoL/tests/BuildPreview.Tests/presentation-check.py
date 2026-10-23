import copy
import pathlib
import re
import sys
from lxml import etree as E

root = pathlib.Path(sys.argv[1])
mod = root / 'BugfixesAndQoL'
locales = list((mod / 'Locales').glob('*.txt'))
assert locales, 'No build-preview translations found'
assert {'de-DE.txt', 'en-US.txt'} <= {p.name for p in locales}
keys = {'Ground.Coverage', 'Ground.Green', 'Ground.Stone', 'Ground.Iron', 'Ground.Pitch'}
for locale in locales:
    text = locale.read_text(encoding='utf-8')
    rows = [line.split('=', 1) for line in text.splitlines() if line.startswith('Ground.')]
    assert len(rows) == 5 and {k for k,v in rows} == keys
    assert all(0 < len(v) <= 24 and '\ufffd' not in v and '\u00c3' not in v for k,v in rows)
    assert b'\n' not in locale.read_bytes().replace(bytes([13,10]), b'')
assert dict(line.split('=',1) for line in (mod/'Locales/de-DE.txt').read_text(encoding='utf-8').splitlines() if '=' in line)['Ground.Green'] == 'Grün'
ns = {'n':'http://schemas.microsoft.com/winfx/2006/xaml/presentation', 'x':'http://schemas.microsoft.com/winfx/2006/xaml'}
vanilla = E.parse(str(root / '_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/resources/xaml/Assets/GUI/XAML/MainHUD.xaml'))

ours = E.parse(str(root/'APIShared/Patches/Assets/GUI/XAML/MainHUD.xaml'))
for content in ours.findall('.//Content'): assert len(content) == 1
# APIShared 0.13 tooltip contract (docs/TOOLTIPS.md): the patch only marks three Vanilla elements with
# the attached TooltipAnchor.Anchor property; the runtime creates the contributed lines. No XAML hosts.
apt = 'clr-namespace:APIShared.Presentation;assembly=APIShared'
assert [(op.get('AttributeName'), op.get('Value')) for op in ours.findall('Operation') if op.get('Type') == 'AddNamespace'] == [('apt', apt)]
anchors = {op.get('Value'): op.get('XPath') for op in ours.findall('Operation')
           if op.get('Type') == 'SetAttribute' and op.get('AttributeName') == 'apt:TooltipAnchor.Anchor'}
assert set(anchors) == {'BuildingCompact', 'BuildingDetailed', 'BuildingDescription'}, anchors
assert 'GroundTooltipHost' not in (mod/'Patches/Assets/GUI/XAML/MainHUD.xaml').read_text(encoding='utf-8')

def apply(tree, patch):
    prefixes = {}
    for op in patch.findall('Operation'):
        kind = op.get('Type')
        if kind == 'AddNamespace':
            prefixes[op.get('AttributeName')] = op.get('Value'); continue
        targets = tree.xpath(op.get('XPath'),namespaces=ns)
        assert len(targets) == 1, (kind, op.get('XPath'), len(targets))
        node = targets[0]
        content = op.find('Content')
        if content is None:
            assert kind == 'SetAttribute'
            name = op.get('AttributeName')
            if ':' in name:
                prefix, local = name.split(':', 1)
                name = '{'+prefixes[prefix]+'}'+local
            node.set(name,op.get('Value'));continue
        assert len(content) == 1
        child = copy.deepcopy(content[0])
        for element in child.iter():
            if isinstance(element.tag,str): element.tag = '{'+ns['n']+'}'+element.tag
        if kind=='Add':node.append(child)
        elif kind=='Replace':node.getparent().replace(node,child)
        elif kind=='InsertBefore':node.addprevious(child)
        elif kind=='InsertAfter':node.addnext(child)
        else:raise AssertionError(kind)

# Presence matrix: the shared anchors stay usable for arbitrary foreign contributions with or
# without each of our consumers, and every patch still targets exactly one Vanilla element.
import itertools
anchor_attribute = '{'+apt+'}TooltipAnchor.Anchor'
original = vanilla.xpath('//*[@x:Name="GameHoverText"]',namespaces=ns)[0]
compact_xpath = '//n:StackPanel[@Visibility="{Binding RolloverBuilding_TooltipVisNot, Converter={StaticResource booleanToVisibilityConverter}}"]'
detailed_xpath = '//n:DockPanel[@Visibility="{Binding RolloverBuilding_TooltipVis, Converter={StaticResource booleanToVisibilityConverter}}"]'
legacy_hosts = ('GroundTooltipHost','GroundTooltipHostCompact','BuildingLimitTooltipHost','BuildingLimitTooltipHostCompact',
                'APISharedBuildingTooltipDetailed','APISharedBuildingTooltipCompact','APISharedRepairExtrasHost',
                'BuildingCostsTooltipHost','BuildingCostsTooltipHostCompact')
for costs,limit in itertools.product((False,True),repeat=2):
    tree=copy.deepcopy(vanilla)
    apply(tree,ours)
    for name,present in [('BuildingCosts',costs),('BuildingLimit',limit)]:
        if present:apply(tree,E.parse(str(root/name/'BepInEx/plugins'/f'{name}_Serp/Patches/Assets/GUI/XAML/MainHUD.xaml')))
    assert not tree.xpath('//*['+' or '.join('@x:Name="'+h+'"' for h in legacy_hosts)+']',namespaces=ns)
    hover=tree.xpath('//*[@x:Name="GameHoverText"]',namespaces=ns)[0]
    for attribute in ('HorizontalAlignment','FontSize','Foreground','FlowDirection','Tag','Background'):
        assert hover.get(attribute)==original.get(attribute)
    assert [run.get('Text') for run in hover.findall('n:Run',namespaces=ns)]==[run.get('Text') for run in original.findall('n:Run',namespaces=ns)]
    marked = {element.get(anchor_attribute): element for element in tree.iter() if isinstance(element.tag,str) and element.get(anchor_attribute) is not None}
    assert sorted(marked) == ['BuildingCompact','BuildingDescription','BuildingDetailed'] and \
        sum(1 for element in tree.iter() if isinstance(element.tag,str) and element.get(anchor_attribute) is not None) == 3
    assert marked['BuildingCompact'] is hover
    detailed=tree.xpath(detailed_xpath,namespaces=ns)[0]
    assert marked['BuildingDetailed'] in list(detailed.iter()) and \
        '{Binding RollOverText_AmountReq1}' in [run.get('Text') for run in marked['BuildingDetailed'].findall('n:Run',namespaces=ns)]
    assert marked['BuildingDescription'].get('Text') == '{Binding RolloverBuilding_TooltipBody}'
    # Vanilla layout of both tooltip variants is unchanged.
    for xpath in (compact_xpath, detailed_xpath):
        before = vanilla.xpath(xpath,namespaces=ns)[0]; after = tree.xpath(xpath,namespaces=ns)[0]
        assert len(after) == len(before) and dict(after.attrib) == dict(before.attrib)
        assert dict(after.getparent().attrib) == dict(before.getparent().attrib)
print(f'PASS: {len(locales)} locales; four distinct patch-composition combinations; unique patch targets, three shared tooltip anchors, preserved Vanilla anchors/style/background/layout and no legacy XAML hosts. No rendered-layout claim.')

new_keys={'BugfixesAndQoL.ImprovedBuildPreview','BugfixesAndQoL.ImprovedBuildPreviewHelp'}
for locale in locales:
    rows=[line.split('=',1) for line in locale.read_text(encoding='utf-8').splitlines() if '=' in line]
    values=dict(rows)
    assert all(k in values and values[k] for k in new_keys)
    assert all(sum(k==key for k,v in rows)==1 for key in keys|new_keys)
print(f'PASS: local checkbox title/help in {len(locales)} locales; no duplicate ground/setting keys.')
