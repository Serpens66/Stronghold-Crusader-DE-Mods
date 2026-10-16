from pathlib import Path
import re

root = Path('Testmods/SpectatorEditorBuildTest')
def edit(path, change):
    p = root / path
    text = p.read_text(encoding='utf-8-sig')
    result = change(text)
    assert result != text, path
    p.write_text(result, encoding='utf-8', newline='')

def model(s):
    s = s.replace('orders, powers, powerFlag', 'orders, powerFlag')
    s = s.replace(' && !powers', '')
    s = re.sub(r'^        public Visibility PowersVisibility.*\n', '', s, flags=re.M)
    s = s.replace(' : powers ? "Powers"', '')
    s = s.replace('        internal bool PowersOpen => expanded && powers;\n', '')
    s = s.replace('orders = powers = false', 'orders = false').replace('goods = powers = false', 'goods = false').replace('goods = orders = powers = false', 'goods = orders = false')
    s = re.sub(r'^                case "Powers":.*\n', '', s, flags=re.M)
    s = s.replace('            Changed(nameof(PowersVisibility));\n', '')
    return s
edit('src/TestToolsViewModel.cs', model)

def powers(s):
    s = s.replace('            if (!toolsModel.PowersOpen) return;\n', '')
    s = s.replace('            powerEscapeSubscription =', '            RegisterVanillaPowerActions();\n            powerEscapeSubscription =')
    # The action dispatcher remains independent of the panel being expanded.
    s = s.replace('            CancelPowerSelection();\n            MainControls', '            amountInput?.EndEditing();\n            CancelPowerSelection();\n            MainControls')
    return s
edit('src/TestToolsPowers.cs', powers)

def hud(s):
    s = s.replace('        private static void DetachHumanControls()\n        {', '        private static void DetachHumanControls()\n        {\n            RestoreVanillaPowerToolbar(true);')
    s = s.replace('            if (command == "StopCursor")', '            if (command == "StopCursor" || command == "CancelPower") amountInput?.EndEditing();\n            if (command == "StopCursor")')
    s = re.sub(r'            if \(command != null && command.StartsWith\("Power",.*\n            \{ SelectPower\(power\); return; \}\n', '', s)
    s = s.replace('if (humanEligible) { RefreshPowers(); RefreshGateState(); }', 'if (humanEligible) { RefreshPowers(); RefreshVanillaPowerToolbar(); RefreshGateState(); }')
    s = s.replace('else { CancelPowerSelection(); ClearGateSelection(); }', 'else { RestoreVanillaPowerToolbar(false); CancelPowerSelection(); ClearGateSelection(); }')
    return s
edit('src/SpectatorEditorBuildTestPlugin.Hud.cs', hud)

def xaml(s):
    s = re.sub(r'<Button Width="145" Height="28" Margin="3,0" Content="\{Binding Text\[PowersTab\]\}".*?</Button>', '', s)
    s = s.replace('Width="145" Height="28"', 'Width="196" Height="28"')
    s = re.sub(r'            <StackPanel Visibility="\{Binding PowersVisibility\}".*?</StackPanel>\s*</Button>.*?</WrapPanel>\s*</StackPanel>', '', s, flags=re.S) if False else s
    start = s.index('            <StackPanel Visibility="{Binding PowersVisibility}"')
    end = s.index('</WrapPanel>', start) + len('</WrapPanel>')
    end = s.index('</StackPanel>', end) + len('</StackPanel>')
    s = s[:start] + s[end:]
    start = s.index('<WrapPanel x:Name="SpectatorEditorHumanOwners"')
    insert = s.index('</WrapPanel>', start) + len('</WrapPanel>')
    s = s[:insert] + '\n            <CheckBox Content="{Binding Text[PowersEnabledTitle]}" IsChecked="{Binding PowersEnabled, Mode=TwoWay}" Foreground="White" FontSize="15" Margin="5,4" ToolTip="{Binding Text[PowersEnabledHelp]}" ToolTipService.ShowDuration="60000"/>' + s[insert:]
    return s
edit('Patches/Assets/GUI/XAML/IngameUIScreens.xaml', xaml)

def layout(s):
    s = s.replace("@('Placement','Goods','Orders','Powers')", "@('Placement','Goods','Orders')").replace("$tab.Width -ne '145'", "$tab.Width -ne '196'").replace('Four equal compact tabs', 'Three equal compact tabs')
    start = s.index('for ($id=0; $id -lt 8; $id++)')
    end = s.index('if (@($commands', start)
    return s[:start] + '''if (@($commands | Where-Object { $_ -match '^Power' }).Count) { throw 'Own power menu must be absent.' }
$toggle = $xml.SelectSingleNode('//CheckBox[@IsChecked="{Binding PowersEnabled, Mode=TwoWay}"]')
if (-not $toggle -or $toggle.ParentNode -ne (Named 'SpectatorEditorHumanOwners').ParentNode) { throw 'Power toggle must be in the common AI area.' }
''' + s[end:]
edit('tests/Test-CompactLayout.ps1', layout)

for p in (root / 'Locales').glob('*.txt'):
    s = p.read_text(encoding='utf-8-sig')
    s = re.sub(r'^Tools.PowersTab(?:Help)?=.*\n', '', s, flags=re.M)
    de = p.stem == 'de-DE'
    context = '{Player} · ∞ Testkräfte' if de else '{Player} · ∞ test powers'
    helptext = ('Extremkräfte für die Partie ein- oder ausschalten. Die rechte Vanilla-Leiste führt Kräfte kostenlos für die hier gewählte KI aus, auch bei minimiertem Testmenü. CheatMod und ExtremePowers werden berücksichtigt.' if de else 'Enable or disable extreme powers for this match. The right-hand Vanilla bar casts for the selected AI without cost, even while the test panel is minimized. CheatMod and ExtremePowers remain supported.')
    s = re.sub(r'^Tools.PowersEnabledHelp=.*$', 'Tools.PowersEnabledHelp=' + helptext, s, flags=re.M)
    s += '\nTools.PowerToolbarContext=' + context + '\n'
    p.write_text(s, encoding='utf-8', newline='')
