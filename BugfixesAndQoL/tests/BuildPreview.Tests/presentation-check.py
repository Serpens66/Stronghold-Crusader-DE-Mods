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
for name in ('APISharedBuildingTooltipDetailed','APISharedBuildingTooltipCompact'):
    host = ours.xpath('//*[@x:Name="'+name+'"]',namespaces=ns)[0]
    assert host.tag=='ItemsControl' and host.get('MaxWidth')=='{Binding MaxWidth}'
    assert 'GlobalTextFlowAL2R' in host.get('FlowDirection')
    assert all(t.get('Background') is None for t in host.findall('.//TextBlock'))
    assert all(t.get('TextWrapping')=='Wrap' for t in host.findall('.//TextBlock'))
assert 'GroundTooltipHost' not in (mod/'Patches/Assets/GUI/XAML/MainHUD.xaml').read_text(encoding='utf-8')

def apply(tree, patch):
    for op in patch.findall('Operation'):
        targets = tree.xpath(op.get('XPath'),namespaces=ns)
        assert len(targets) == 1, (op.get('Type'), op.get('XPath'), len(targets))
        node = targets[0]
        content = op.find('Content')
        if content is None:
            assert op.get('Type') == 'SetAttribute'
            node.set(op.get('AttributeName'),op.get('Value'));continue
        assert len(content) == 1
        child = copy.deepcopy(content[0])
        for element in child.iter():
            if isinstance(element.tag,str): element.tag = '{'+ns['n']+'}'+element.tag
        kind=op.get('Type')
        if kind=='Add':node.append(child)
        elif kind=='Replace':node.getparent().replace(node,child)
        elif kind=='InsertBefore':node.addprevious(child)
        elif kind=='InsertAfter':node.addnext(child)
        else:raise AssertionError(kind)

# Presence matrix: central hosts remain usable for arbitrary foreign contributions
# with or without each of our consumers; disabled contributors return no lines.
import itertools
original = vanilla.xpath('//*[@x:Name="GameHoverText"]',namespaces=ns)[0]
for costs,limit in itertools.product((False,True),repeat=2):
    tree=copy.deepcopy(vanilla)
    apply(tree,ours)
    for name,present in [('BuildingCosts',costs),('BuildingLimit',limit)]:
        if present:apply(tree,E.parse(str(root/name/'BepInEx/plugins'/f'{name}_Serp/Patches/Assets/GUI/XAML/MainHUD.xaml')))
    assert not tree.xpath('//*[@x:Name="GroundTooltipHost" or @x:Name="GroundTooltipHostCompact" or @x:Name="BuildingLimitTooltipHost" or @x:Name="BuildingLimitTooltipHostCompact"]',namespaces=ns)
    hover=tree.xpath('//*[@x:Name="GameHoverText"]',namespaces=ns)[0]
    for attribute in ('HorizontalAlignment','FontSize','Foreground','FlowDirection','Tag'):
        assert hover.get(attribute)==original.get(attribute)
    assert [run.get('Text') for run in hover.findall('n:Run',namespaces=ns)]==[run.get('Text') for run in original.findall('n:Run',namespaces=ns)]
    compact=tree.xpath('//n:StackPanel[@Visibility="{Binding RolloverBuilding_TooltipVisNot, Converter={StaticResource booleanToVisibilityConverter}}"]',namespaces=ns)[0]
    assert compact[0].get('{'+ns['x']+'}Name')=='APISharedBuildingTooltipCompact'
    outer=compact.getparent()
    assert outer.get('Width')=='550' and outer.get('Margin')=='{Binding RollOverText_Margin}'
    assert outer.get('HorizontalAlignment')=='Right' and outer.get('VerticalAlignment')=='Bottom'
    detailed=tree.xpath('//n:DockPanel[@Visibility="{Binding RolloverBuilding_TooltipVis, Converter={StaticResource booleanToVisibilityConverter}}"]',namespaces=ns)[0]
    assert detailed.get('HorizontalAlignment')=='Left'
    stack=detailed.find('n:Grid/n:StackPanel',namespaces=ns)
    assert stack[0].get('{'+ns['x']+'}Name')=='APISharedBuildingTooltipDetailed'
    assert detailed.find('n:Grid/n:Border',namespaces=ns).get('Background')=='#88000000'
    if costs:
        cost_container=compact[1]
        assert cost_container.get('HorizontalAlignment')=='Left'
        assert cost_container.find('n:Border',namespaces=ns).get('Background')==original.get('Background')
        assert cost_container.xpath('.//n:TranslateTransform[@X="-250"]',namespaces=ns)
        for name in ('BuildingCostsTooltipHost','BuildingCostsTooltipHostCompact','APISharedRepairExtrasHost'):
            assert len(tree.xpath('//*[@x:Name="'+name+'"]',namespaces=ns))==1
    else:
        assert hover.get('Background')==original.get('Background')
        assert len(tree.xpath('//*[@x:Name="APISharedRepairExtrasHost"]',namespaces=ns))==1
print(f'PASS: {len(locales)} locales; four distinct patch-composition combinations; preserved Vanilla anchors/style/background, shared hosts and cost templates. No rendered-layout claim.')

new_keys={'BugfixesAndQoL.ImprovedBuildPreview','BugfixesAndQoL.ImprovedBuildPreviewHelp'}
for locale in locales:
    rows=[line.split('=',1) for line in locale.read_text(encoding='utf-8').splitlines() if '=' in line]
    values=dict(rows)
    assert all(k in values and values[k] for k in new_keys)
    assert all(sum(k==key for k,v in rows)==1 for key in keys|new_keys)
print(f'PASS: local checkbox title/help in {len(locales)} locales; no duplicate ground/setting keys.')
