from pathlib import Path
import xml.etree.ElementTree as ET

root = Path('Testmods/SpectatorEditorBuildTest')
path = root/'Patches/Assets/GUI/XAML/IngameUIScreens.xaml'
text = path.read_text(encoding='utf-8-sig')
text = text.replace('Width="195" Height="28"', 'Width="145" Height="28"')
tab = '''<Button Width="145" Height="28" Margin="3,0" Content="{Binding Text[PowersTab]}" Command="{Binding ActionCommand}" CommandParameter="Powers" ToolTip="{Binding Text[PowersTabHelp]}" ToolTipService.ShowDuration="60000"><Button.Style><Style TargetType="{x:Type Button}" BasedOn="{StaticResource ToolsButton}"><Style.Triggers><DataTrigger Binding="{Binding ActiveViewKey}" Value="Powers"><Setter Property="Background" Value="#FF875020"/><Setter Property="BorderBrush" Value="#FFFFE6A8"/></DataTrigger></Style.Triggers></Style></Button.Style></Button>'''
anchor = '</Button></StackPanel>\n            <TextBlock Text="{Binding SelectedPlayer}"'
assert anchor in text and 'CommandParameter="Powers"' not in text
text = text.replace(anchor, '</Button>'+tab+'</StackPanel>\n            <TextBlock Text="{Binding SelectedPlayer}"', 1)
page = '''<StackPanel Visibility="{Binding PowersVisibility}" Margin="0,4,0,0">
              <CheckBox Content="{Binding Text[PowersEnabledTitle]}" IsChecked="{Binding PowersEnabled, Mode=TwoWay}" Foreground="White" FontSize="15" Margin="5,4" ToolTip="{Binding Text[PowersEnabledHelp]}" ToolTipService.ShowDuration="60000"/>
              <WrapPanel Width="608" ItemWidth="152" ItemHeight="60" HorizontalAlignment="Center">'''
for i in range(8):
    page += f'''<Button Width="148" Height="56" Margin="2" Style="{{StaticResource ToolsButton}}" Command="{{Binding ActionCommand}}" CommandParameter="Power{i}" IsEnabled="{{Binding Powers[{i}].Enabled}}" ToolTip="{{Binding Powers[{i}].Help}}" ToolTipService.ShowDuration="60000"><StackPanel Orientation="Horizontal"><Image Source="{{Binding Powers[{i}].Icon}}" Width="30" Height="30" Margin="3,0"/><TextBlock Text="{{Binding Powers[{i}].Name}}" Width="104" MaxHeight="40" FontSize="13" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/></StackPanel></Button>'''
page += '''</WrapPanel>
              <Button Width="200" Height="26" Margin="3,4" Style="{StaticResource ToolsButton}" Content="{Binding Text[PowerCancel]}" Command="{Binding ActionCommand}" CommandParameter="CancelPower" ToolTip="{Binding Text[PowerCancelHelp]}" ToolTipService.ShowDuration="60000"/>
            </StackPanel>
            '''
anchor = '<TextBlock Text="{Binding Status}"'
assert anchor in text
text = text.replace(anchor, page+anchor)
ET.fromstring(text)
path.write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))

en = dict(
    PowersTab='Extreme powers', PowersTabHelp='Use extreme powers as the selected AI. Test casts are free and retain configured effects.',
    PowersEnabledTitle='Extreme powers for this match', PowersEnabledHelp='Change the actual match setting, including the human player. CheatMod receives the regular Script Extender setter. Human casts keep their usual costs.',
    PowerHelp='Free AI test cast. Choose a map target when requested; right-click or Escape cancels. Normal target restrictions still apply.',
    Power0='Arrow volley', Power1='Heal', Power2='Spearmen', Power3='Engineers', Power4='Macemen', Power5='Gold', Power6='Knights', Power7='Rock volley',
    PowerUnavailable='This power is disabled or its backend/target type is unavailable.',
    PowerChooseTarget='Choose a valid map target. Right-click or Escape cancels.',
    PowerCancelled='Power targeting cancelled.', PowerInvalidTarget='This target is not allowed for the selected AI. Check terrain, distance and enemy proximity.',
    PowerChanged='The power configuration changed. Select the power again.', PowerRejected='The configured power rejected this target; see the log.',
    PowerExecuted='Power invoked as the selected AI. Observe its effect on the map.',
    PowersOn='Extreme powers enabled for this match.', PowersOff='Extreme powers disabled for this match.',
    PowerCancel='Cancel targeting', PowerCancelHelp='Cancel the pending extreme-power map target without casting.',
    GateRequested='Building command applied. Animation and later AI decisions remain authoritative.'
)
de = dict(
    PowersTab='Extremkräfte', PowersTabHelp='Extremkräfte als ausgewählte KI einsetzen. Testaktionen sind kostenlos; konfigurierte Wirkungen bleiben erhalten.',
    PowersEnabledTitle='Extremkräfte für diese Partie', PowersEnabledHelp='Ändert die tatsächliche Freigabe einschließlich des menschlichen Spielers. CheatMod erhält den normalen Extender-Setter. Menschliche Aktionen behalten ihre üblichen Kosten.',
    PowerHelp='Kostenlose KI-Testaktion. Bei Aufforderung ein Kartenziel wählen; Rechtsklick oder Escape bricht ab. Normale Zielbeschränkungen gelten weiterhin.',
    Power0='Pfeilsalve', Power1='Heilung', Power2='Lanzenträger', Power3='Ingenieure', Power4='Streitkolbenkämpfer', Power5='Gold', Power6='Ritter', Power7='Steinsalve',
    PowerUnavailable='Diese Kraft ist ausgeschaltet oder ihr Backend/Zieltyp ist nicht verfügbar.',
    PowerChooseTarget='Ein gültiges Kartenziel wählen. Rechtsklick oder Escape bricht ab.', PowerCancelled='Zielwahl der Extremkraft abgebrochen.',
    PowerInvalidTarget='Dieses Ziel ist für die gewählte KI nicht zulässig. Gelände, Entfernung und Gegnernähe prüfen.',
    PowerChanged='Die Kraft wurde umkonfiguriert. Bitte erneut auswählen.', PowerRejected='Die konfigurierte Kraft hat das Ziel abgelehnt. Details im Log.',
    PowerExecuted='Kraft als gewählte KI aufgerufen. Wirkung auf der Karte beobachten.',
    PowersOn='Extremkräfte für diese Partie eingeschaltet.', PowersOff='Extremkräfte für diese Partie ausgeschaltet.',
    PowerCancel='Zielwahl abbrechen', PowerCancelHelp='Die ausstehende Kartenzielwahl ohne Einsatz der Extremkraft abbrechen.',
    GateRequested='Gebäudebefehl angewendet. Animation und spätere KI-Entscheidungen bleiben maßgeblich.'
)
for locale in (root/'Locales').glob('*.txt'):
    lines=locale.read_text(encoding='utf-8-sig').splitlines()
    existing={line.split('=',1)[0]:line.split('=',1)[1] for line in lines if '=' in line}
    existing.update({'Tools.'+k:v for k,v in (de if locale.stem=='de-DE' else en).items()})
    locale.write_bytes(('\r\n'.join(k+'='+existing[k] for k in sorted(existing))+'\r\n').encode('utf-8'))
print('Updated four tabs, eight power cards and all locales.')
