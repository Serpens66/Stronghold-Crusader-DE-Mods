"""Generate declared popularity settings and rows inside the maintained main-mod shell."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]

def write(path, text):
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('w', encoding='utf-8', newline='\r\n') as stream:
        stream.write(text.replace('\r\n', '\n'))

groups = [
 ('Rations', 'Rations', 'Rationen', 16, [
 ('RationsPopModifierNone', -8, 'No rations', 'Keine Rationen'),
 ('RationsPopModifierHalf', -4, 'Half rations', 'Halbe Rationen'),
 ('RationsPopModifierFull', 0, 'Normal rations', 'Normale Rationen'),
 ('RationsPopModifierExtra', 4, 'Extra rations', 'Zusätzliche Rationen'),
 ('RationsPopModifierDouble', 8, 'Double rations', 'Doppelte Rationen'),
 ('RationsPopModifierNoPreferredFood', -8, 'No available food', 'Keine verfügbare Nahrung'),
 ('RationsPopModifierNoPopulation', 8, 'No population', 'Keine Bevölkerung')]),
 ('Variety', 'Food variety', 'Nahrungsvielfalt', 6, [
 ('FoodVarietyPopModifierTwoTypes', 1, 'Two food types', 'Zwei Nahrungssorten'),
 ('FoodVarietyPopModifierThreeTypes', 2, 'Three food types', 'Drei Nahrungssorten'),
 ('FoodVarietyPopModifierFourTypes', 3, 'Four food types', 'Vier Nahrungssorten')]),
 ('Crowding', 'Overpopulation', 'Überbevölkerung', 20, [
 ('OverpopulationPopModifierTier'+tier, value, f'Population {label}', f'Bevölkerung {label}')
 for tier, value, label in [('One',-2,'101–120 %'),('Two',-4,'121–140 %'),('Three',-6,'141–160 %'),('Four',-8,'161–180 %'),('Five',-10,'>180 %')]]),
 ('Taxes', 'Taxes and donations', 'Steuern und Bestechung', 48, [
 ('TaxPopModifier'+key, value, en, de) for key,value,en,de in [
 ('HugeDonation',7,'Huge donation','Großzügige Bestechung'),('BigDonation',5,'Big donation','Große Bestechung'),
 ('SmallDonation',3,'Small donation','Kleine Bestechung'),('None',1,'No taxes','Keine Steuern'),
 ('Low',-2,'Low taxes','Niedrige Steuern'),('Moderate',-4,'Moderate taxes','Mäßige Steuern'),
 ('High',-6,'High taxes','Hohe Steuern'),('Mean',-8,'Mean taxes','Gemeine Steuern'),
 ('Usurious',-12,'Usurious taxes','Wucherische Steuern'),('Cruel',-16,'Cruel taxes','Grausame Steuern'),
 ('MostCruel',-20,'Most cruel taxes','Sehr grausame Steuern'),('Cruellest',-24,'Cruellest taxes','Äußerst grausame Steuern'),
 ('UnaffordableDonation',1,'Donation without gold','Bestechung ohne Gold'),
 ('UnknownMode',-24,'Unknown tax mode (fallback)','Unbekannte Steuerstufe (Sonderfall)')]]),
 ('Religion', 'Religion', 'Religion', 16, [
 ('ReligionPopModifierTier'+tier, value, f'Blessed population {label}', f'Gesegnete Bevölkerung {label}')
 for tier,value,label in [('One',2,'25–49 %'),('Two',4,'50–74 %'),('Three',6,'75–94 %'),('Four',8,'≥95 %')]] + [
 ('ChurchPopModifier',1,'Church exists','Kirche vorhanden'),('CathedralPopModifier',2,'Cathedral exists','Kathedrale vorhanden')]),
 ('Ale', 'Ale coverage', 'Bierversorgung', 16, [
 ('AlePopModifierTier'+tier, value, f'Ale coverage {label}', f'Bierversorgung {label}')
 for tier,value,label in [('One',2,'25–49 %'),('Two',4,'50–74 %'),('Three',6,'75–99 %'),('Four',8,'100 %')]]),
 ('Fairs', 'Fair event', 'Jahrmarkt', 32, [('FairsPopularityEventModifier',16,'Active fair event','Aktiver Jahrmarkt')])]

items = [(group, limit, key, default, en, de) for group,_,_,limit,rows in groups for key,default,en,de in rows]
fear_groups = [('NegativeFear', -1, 'Negative fear levels', 'Negative Furchtfaktorstufen'),
               ('PositiveFear', 1, 'Positive fear levels', 'Positive Furchtfaktorstufen')]
fear_items = [(group, 25, f'Fear{"Negative" if sign < 0 else "Positive"}{level}', sign * level,
               f'Contribution at level {sign * level:+d}', f'Beitrag bei Stufe {sign * level:+d}')
              for group, sign, _, _ in fear_groups for level in range(1, 6)]
settings = '''using APIShared.Economy;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private volatile bool missionLocked;
        private readonly SynchronizationContext uiContext = SynchronizationContext.Current;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        private void InitializePopularitySettings()
        {
            PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CanEditHostSettings) || e.PropertyName == nameof(EnableMod)) RefreshPopularityEditors(); };
        }
        public bool PopularityBetweenMissions => !missionLocked;
        public bool CanConfigurePopularity => PopularityBetweenMissions && CanEditHostSettings;
        public string PopularityScopeText => SerpLocalization.Get("ExtraFeatures.Popularity.Scope");
        public string PopularityRuleText => SerpLocalization.Get("ExtraFeatures.Popularity.Rule");
        internal void SetPopularityMissionLocked(bool locked)
        {
            missionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread) RefreshPopularityEditors();
            else if (uiContext != null) uiContext.Post(_ => RefreshPopularityEditors(), null);
        }
        private bool ChangePopularity<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!PopularityBetweenMissions || !CanMutateSetting(name) || Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }
        private void RefreshPopularityEditors()
        {
            OnPropertyChanged(nameof(PopularityBetweenMissions)); OnPropertyChanged(nameof(CanConfigurePopularity));
'''
roles=('Human','AI')
activation_groups=[g[0] for g in groups]+[g[0] for g in fear_groups]
for _,_,key,_,_,_ in items+fear_items:
    for role in roles: settings+=f'            OnPropertyChanged(nameof({key}{role}Editable));\n'
settings+='        }\n'
for key in activation_groups:
    for role in roles:
        name=key+role; field=name[0].lower()+name[1:]+'Enabled'
        settings+=f'''        private bool {field};
        [SyncHostOnly]
        public bool {name}Enabled
        {{
            get => {field};
            set {{ if (ChangePopularity(ref {field}, value)) RefreshPopularityEditors(); }}
        }}
'''
for group,limit,key,default,en,de in items+fear_items:
    for role in roles:
        name=key+role; field=name[0].lower()+name[1:]; enabled=group+role+'Enabled'
        settings+=f'''        private int {field} = {default};
        [SyncHostOnly]
        public int {name}
        {{
            get => {field};
            set {{ if (ChangePopularity(ref {field}, Math.Max(-{limit}, Math.Min({limit}, value)))) OnPropertyChanged(nameof({name}ValueText)); }}
        }}
        public bool {name}Editable => CanConfigurePopularity && EnableMod && {enabled};
        public string {name}ValueText
        {{
            get => {name}.ToString(CultureInfo.InvariantCulture);
            set {{ if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {name} = parsed; OnPropertyChanged(nameof({name}ValueText)); }}
        }}
'''
for key in [i[2] for i in items+fear_items]+activation_groups:
    settings+=f'        public string {key}Title => SerpLocalization.Get("ExtraFeatures.Popularity.{key}");\n'
    for role in roles:
        for suffix in ('Title','Help'):
            settings+=f'        public string {key}{role}{suffix} => SerpLocalization.Get("ExtraFeatures.Popularity.{key}.{role}.{suffix}");\n'
settings+='        public string FearTitle => SerpLocalization.Get("ExtraFeatures.Popularity.Fear");\n'
for kind,keytype,source in [('Ordinary','PopularitySource',items),('Fear','int',fear_items)]:
    settings+=f'        internal Dictionary<{keytype}, PopularityOverrideValues> Capture{kind}Contributions()\n        {{\n            var values = new Dictionary<{keytype}, PopularityOverrideValues>();\n            if (!EnableMod) return values;\n'
    for group,_,key,level,_,_ in source:
        enable=group
        index=str(level) if kind=='Fear' else 'PopularitySource.'+key
        settings+=f'            if ({enable}HumanEnabled || {enable}AIEnabled) values.Add({index}, new PopularityOverrideValues({enable}HumanEnabled ? (int?){key}Human : null, {enable}AIEnabled ? (int?){key}AI : null));\n'
    settings+='            return values;\n        }\n'
settings+='        internal void ResetPopularityDefaults()\n        {\n'
for key in activation_groups:
    for role in roles: settings+=f'            {key}{role}Enabled = false;\n'
for _,_,key,default,_,_ in items+fear_items:
    for role in roles: settings+=f'            {key}{role} = {default};\n'
settings+='        }\n    }\n}\n'
write('src/ExtraFeaturesViewModel.Popularity.cs',settings)

catalog='''namespace ExtraFeatures
{
    internal sealed class PopularityParameter
    {
        internal readonly string Name, Group;
        internal readonly int Vanilla, Limit;
        internal PopularityParameter(string name,string group,int vanilla,int limit)
        {Name=name;Group=group;Vanilla=vanilla;Limit=limit;}
    }
    internal static class PopularityCatalog
    {
        internal static readonly PopularityParameter[] All = {
'''
for group,limit,key,default,_,_ in items: catalog+=f'            new PopularityParameter("{key}","{group}",{default},{limit}),\n'
catalog+='        };\n    }\n}\n';write('src/PopularityCatalog.cs',catalog)

# Each player block has its own search entry; row unions keep captions attached.
search_groups={}
for group,*_ in groups:
    search_groups['Popularity'+group+'Options']=[group]+[i[2] for i in items if i[0]==group]
for group,*_ in fear_groups:
    search_groups['Popularity'+group+'Options']=[group]+[i[2] for i in fear_items if i[0]==group]
search_groups['PopularityFearOptions']=[g[0] for g in fear_groups]+[i[2] for i in fear_items]
for key in [i[2] for i in items+fear_items]: search_groups['PopularityRow'+key]=[key]
role_groups={}
for group in activation_groups:
    members=search_groups['Popularity'+group+'Options']
    for role in roles: role_groups['Popularity'+group+role+'Options']=[(key,role) for key in members]
presentation='using System.Collections.Generic;\nnamespace ExtraFeatures\n{\n    public sealed partial class ModSettingsPresentation\n    {\n'
for name,keys in search_groups.items():
    suffixes=', '.join('"popularity.'+k+'.'+r.lower()+'"' for k in keys for r in roles)
    presentation+=f'        public SettingsGroupPresentation {name} {{ get; }} = new SettingsGroupPresentation({suffixes});\n'
for name,pairs in role_groups.items():
    suffixes=', '.join('"popularity.'+key+'.'+role.lower()+'"' for key,role in pairs)
    presentation+=f'        public SettingsGroupPresentation {name} {{ get; }} = new SettingsGroupPresentation({suffixes});\n'
presentation+='        private IEnumerable<SettingsGroupPresentation> PopularityGroups => new[] { '+', '.join(list(search_groups)+list(role_groups))+' };\n    }\n}\n';write('src/ModSettingsPresentation.Popularity.cs',presentation)

def table_columns():
    return '<Grid.ColumnDefinitions><ColumnDefinition Width="180"/><ColumnDefinition Width="16"/><ColumnDefinition Width="*" MinWidth="152" MaxWidth="192"/><ColumnDefinition Width="24"/><ColumnDefinition Width="*" MinWidth="152" MaxWidth="192"/></Grid.ColumnDefinitions>'

def group_header(group):
    row=[f'      <Grid Margin="0,12,0,8" MinHeight="28">',
         '        '+table_columns(),
         f'        <TextBlock Text="{{Binding {group}Title}}" Foreground="#DDDDDD" FontWeight="Bold" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" ToolTip="{{Binding {group}Title}}" ToolTipService.ShowDuration="60000"/>']
    for role,column,label in [('Human',2,'HumanText'),('AI',4,'AiText')]:
        name=group+role
        # The context union includes child values, so filtering for a value never
        # hides the switch needed to enable that player's whole group.
        row.append(f'        <Grid Grid.Column="{column}" MinHeight="28" Margin="0" HorizontalAlignment="Stretch" Style="{{StaticResource ModSettingsSearchTargetGrid}}" Visibility="{{Binding SettingsPresentation.Popularity{group}{role}Options.Visibility}}" shared:ModSettingsSearch.Key="extra.popularity.{group}.{role.lower()}" shared:ModSettingsSearch.Title="{{Binding {name}Title}}" shared:ModSettingsSearch.ToolTipText="{{Binding {name}Help}}">')
        row.append(f'          <CheckBox Content="{{Binding {label}}}" MinHeight="28" HorizontalAlignment="Left" VerticalAlignment="Center" Margin="0" IsChecked="{{Binding {name}Enabled, Mode=TwoWay}}" IsEnabled="{{Binding CanConfigurePopularity}}" Foreground="White" ToolTip="{{Binding {name}Help}}" ToolTipService.ShowDuration="60000"/>')
        row.append('        </Grid>')
    row.append('      </Grid>');return '\n'.join(row)

def value_row(key,limit):
    row=[f'      <Grid Margin="0,0,0,8" MinHeight="28" Visibility="{{Binding SettingsPresentation.PopularityRow{key}.Visibility}}">',
         '        '+table_columns(),
         f'        <TextBlock Text="{{Binding {key}Title}}" Foreground="White" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" ToolTip="{{Binding {key}Title}}" ToolTipService.ShowDuration="60000"/>']
    neutral=next(default for _,_,name,default,_,_ in items+fear_items if name==key)
    for role,column in [('Human',2),('AI',4)]:
        name=key+role
        row.append(f'        <Grid Grid.Column="{column}" MinHeight="28" HorizontalAlignment="Stretch" Margin="0" Style="{{StaticResource ModSettingsSearchTargetGrid}}" shared:ModSettingsSearch.Key="extra.popularity.{key}.{role.lower()}" shared:ModSettingsSearch.Title="{{Binding {name}Title}}" shared:ModSettingsSearch.ToolTipText="{{Binding {name}Help}}">')
        row.append('          <Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="80" MaxWidth="120"/><ColumnDefinition Width="8"/><ColumnDefinition Width="64"/></Grid.ColumnDefinitions>')
        row.append(f'          <Slider shared:ModSettingsSearch.InactiveValues="{neutral}" MinWidth="80" MaxWidth="120" HorizontalAlignment="Stretch" Margin="0" Minimum="-{limit}" Maximum="{limit}" Value="{{Binding {name}, Mode=TwoWay}}" IsEnabled="{{Binding {name}Editable}}" TickFrequency="1" SmallChange="1" LargeChange="1" IsSnapToTickEnabled="True" ToolTip="{{Binding {name}Help}}" ToolTipService.ShowDuration="60000"/>')
        row.append(f'          <TextBox Grid.Column="2" Width="64" Text="{{Binding {name}ValueText, Mode=TwoWay, UpdateSourceTrigger=LostFocus}}" IsEnabled="{{Binding {name}Editable}}" seui:KeyboardCaptureBinding.Enabled="True" Foreground="White" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1" Padding="4,1" TextAlignment="Center" VerticalAlignment="Center" ToolTip="{{Binding {name}Help}}" ToolTipService.ShowDuration="60000"/>')
        row.append('        </Grid>')
    row.append('      </Grid>');return '\n'.join(row)

rows=[]
for group,_,_,limit,entries in groups:
    rows.append(f'          <StackPanel Visibility="{{Binding SettingsPresentation.Popularity{group}Options.Visibility}}">')
    rows.append(group_header(group))
    for key,*_ in entries: rows.append(value_row(key,limit))
    rows.append('          </StackPanel>')
rows.append('          <StackPanel Visibility="{Binding SettingsPresentation.PopularityFearOptions.Visibility}">')
rows.append('            <TextBlock Text="{Binding FearTitle}" Style="{StaticResource SubsectionHeader}"/>')
for group,*_ in fear_groups:
    rows.append(f'          <StackPanel Visibility="{{Binding SettingsPresentation.Popularity{group}Options.Visibility}}">')
    rows.append(group_header(group))
    for row_group,limit,key,*_ in fear_items:
        if row_group==group: rows.append(value_row(key,limit))
    rows.append('          </StackPanel>')
rows.append('          </StackPanel>')
path=root/'Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml';template=path.read_text(encoding='utf-8-sig')
start,end='<!-- POPULARITY_SETTINGS_BEGIN -->','<!-- POPULARITY_SETTINGS_END -->'
assert template.count(start)==template.count(end)==1,'The owned generation slot must be unique'
before,rest=template.split(start);_,after=rest.split(end)
write(path.relative_to(root),before+start+'\n'+'\n'.join(rows)+'\n          '+end+after)

help_en = {
 'Rations':'Applies to the selected ration level when population and available food are present. The no-food and no-population cases replace the ration-level contribution.',
 'Variety':'Added to the ration contribution when this exact number of food types is consumed.',
 'Crowding':'Applies at the indicated population-to-housing ratio; the existing thresholds are unchanged.',
 'Taxes':'Applies to this tax/donation mode. Donations require positive gold; without gold the separate fallback applies. Gold income and expenditure are unchanged.',
 'Religion':'Applies to this blessing-coverage tier or, for buildings, once when at least one such building exists. Church and cathedral bonuses are additive.',
 'Ale':'Applies to this inn coverage tier. Existing coverage thresholds and ale consumption are unchanged.',
 'Fairs':'Applies while the game flags a fair event as active. Other event bonuses and penalties are unchanged.'}
help_de = {
 'Rations':'Gilt für die gewählte Rationsstufe bei vorhandener Bevölkerung und Nahrung. Ohne verfügbare Nahrung bzw. ohne Bevölkerung gilt stattdessen der jeweilige Sonderwert.',
 'Variety':'Wird bei genau dieser Anzahl verzehrter Nahrungssorten zum Rationsbeitrag addiert.',
 'Crowding':'Gilt beim angegebenen Verhältnis von Bevölkerung zu Wohnraum; die vorhandenen Schwellen bleiben unverändert.',
 'Taxes':'Gilt für diese Steuer-/Bestechungsstufe. Bestechung benötigt positives Gold; ohne Gold gilt der eigene Sonderwert. Goldeinnahmen und Ausgaben bleiben unverändert.',
 'Religion':'Gilt für die angegebene Segnungsstufe bzw. bei Gebäuden einmalig, sobald mindestens eines vorhanden ist. Kirche und Kathedrale werden addiert.',
 'Ale':'Gilt für diese Wirtshausabdeckung. Abdeckungsschwellen und Bierverbrauch bleiben unverändert.',
 'Fairs':'Gilt, solange das Spiel einen aktiven Jahrmarkt meldet. Andere Ereignisboni und -mali bleiben unverändert.'}
for locale in (root/'Locales').glob('*.txt'):
    german = locale.stem == 'de-DE'
    base = '\n'.join(line for line in locale.read_text(encoding='utf-8-sig').splitlines() if not line.startswith('ExtraFeatures.Popularity.')).rstrip('\n')
    extra = {
     'ExtraFeatures.Popularity.Scope': 'Getrennte Beliebtheitsbeiträge für menschliche Spieler und KI. Änderungen gelten ab der nächsten Karte; während einer Partie sind die Regler gesperrt.' if german else 'Independent popularity contributions for human players and AI. Changes apply from the next map; controls are locked during a mission.',
     'ExtraFeatures.Popularity.Rule': 'Gruppenhäkchen aus: kein Override dieser Gruppe durch den Mod. Quelle „Mod-Standards“ laden: alle Overrides aus, Werte auf Vanilla. Einheit: Zufriedenheitspunkte der Spielberichte.' if german else 'Group checkbox off: this mod supplies no override for that group. Load the Mod defaults source to disable all overrides and restore Vanilla values. Unit: popularity points in the game reports.',
     'ExtraFeatures.Popularity.Fear': 'Furchtfaktor' if german else 'Fear factor'}
    for group,en,de,_,_ in groups:
        extra['ExtraFeatures.Popularity.'+group] = de if german else en
        extra['ExtraFeatures.Popularity.'+group+'.Help'] = ('Aktiviert alle Beiträge dieser Gruppe für die gewählte Spielerart. Ausgeschaltet gelten die vorhandenen Spiel-/Extender-Werte; eingetragene Zahlen bleiben gespeichert.' if german else 'Enables every contribution in this group for the selected player type. When disabled, existing game/Extender values remain effective and entered numbers stay saved.')
    for group,limit,key,default,en,de in items:
        extra['ExtraFeatures.Popularity.'+key] = de if german else en
        explanation = (help_de if german else help_en)[group]
        suffix = f' Vanilla: {default:+d}. ' + ('Gilt nur bei aktivierter zugehöriger Gruppe für diese Spielerart. Der Regler ersetzt den absoluten Beitrag in Zufriedenheitspunkten; ausgeschaltet bleibt der eingetragene Wert gespeichert.' if german else 'Applies only when its group is enabled for this player type. The slider replaces the absolute contribution in popularity points; disabling the group retains the entered value.')
        extra['ExtraFeatures.Popularity.'+key+'.Help'] = explanation+suffix
    for group,_,en,de in fear_groups:
        extra['ExtraFeatures.Popularity.'+group] = de if german else en
        extra['ExtraFeatures.Popularity.'+group+'.Help'] = ('Aktiviert die fünf festen Beiträge für diese Furchtfaktorseite. Ausgeschaltet bleibt die vorhandene Spiel-/Extender-Berechnung erhalten; eingetragene Werte bleiben gespeichert. Stufe 0 bleibt neutral. Gebäude, Produktivität und Kampfeffekte bleiben unverändert.' if german else 'Enables the five fixed contributions on this side of the fear factor. When disabled, existing game/Extender calculation is retained and entered values remain saved. Level 0 stays neutral. Buildings, productivity and combat effects are unchanged.')
    for group,limit,key,level,en,de in fear_items:
        extra['ExtraFeatures.Popularity.'+key] = (de + ' (Punkte)') if german else (en + ' (points)')
        extra['ExtraFeatures.Popularity.'+key+'.Help'] = (f'Fester Beliebtheitsbeitrag bei Furchtfaktorstufe {level:+d}, von -25 bis +25 in ganzen Berichtspunkten. Vanilla: {level:+d}. Gilt nur bei aktivierter zugehöriger Furchtfaktorseite. Null und umgekehrte Vorzeichen sind möglich. Der tatsächliche Furchtfaktor und seine anderen Wirkungen bleiben unverändert.' if german else f'Absolute popularity contribution at fear level {level:+d}, from -25 to +25 in whole report points. Vanilla: {level:+d}. Applies only when its fear-side switch is enabled. Zero and reversed signs are allowed. The actual fear level and its other effects are unchanged.')
    for key in [i[2] for i in items+fear_items]+activation_groups:
        for role in roles:
            label = ('Mensch' if role=='Human' else 'KI') if german else role
            extra['ExtraFeatures.Popularity.'+key+'.'+role+'.Title'] = extra['ExtraFeatures.Popularity.'+key]+' — '+label
            extra['ExtraFeatures.Popularity.'+key+'.'+role+'.Help'] = extra['ExtraFeatures.Popularity.'+key+'.Help']+' '+(('Gilt ausschließlich für '+label+'.') if german else ('Applies only to '+label+' players.'))
    write('Locales/'+locale.name, base+'\n'+'\n'.join(k+'='+v for k,v in extra.items())+'\n')

print(f'Generated {len(items)} SE parameters and {len(fear_items)} fixed fear contributions, settings, XAML rows and locales.')
