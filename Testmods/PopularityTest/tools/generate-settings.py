"""Generate declared popularity settings and rows inside the maintained main-mod shell."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
workspace = root.parents[1]

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
settings = '''using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PopularityTest
{
    public sealed class PopularitySettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private volatile bool missionLocked;
        private readonly SynchronizationContext uiContext = SynchronizationContext.Current;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        public PopularitySettings()
        {
            PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CanEditHostSettings)) RefreshEditors(); };
        }
        public bool BetweenMissions => !missionLocked;
        public bool CanConfigure => BetweenMissions && CanEditHostSettings;
        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (Change(ref enableMod, value)) RefreshEditors(); }
        }
        public string ScopeText => SerpLocalization.Get("Popularity.Scope");
        public string RuleText => SerpLocalization.Get("Popularity.Rule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        { string text = SerpLocalization.Get(key); return text == key ? fallback : text; }
        internal void SetMissionLocked(bool locked)
        {
            missionLocked = locked;
            // Never raise Noesis-bound notifications on a simulation publisher thread.
            if (Thread.CurrentThread.ManagedThreadId == uiThread) RefreshEditors();
            else if (uiContext != null) uiContext.Post(_ => RefreshEditors(), null);
        }
        private bool Change<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!BetweenMissions || !CanMutateSetting(name) || Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }
        private void RefreshEditors()
        {
            OnPropertyChanged(nameof(BetweenMissions)); OnPropertyChanged(nameof(CanConfigure));
'''
for _,_,key,_,_,_ in items + fear_items:
    settings += f'            OnPropertyChanged(nameof({key}Editable));\n'
settings += '        }\n'
for group, _, _, _ in fear_groups:
    field = group[0].lower() + group[1:] + 'Enabled'
    settings += f'''        private bool {field};
        [SyncHostOnly]
        public bool {group}Enabled
        {{
            get => {field};
            set {{ if (Change(ref {field}, value)) RefreshEditors(); }}
        }}
        public string {group}Title => SerpLocalization.Get("Popularity.{group}");
        public string {group}Help => SerpLocalization.Get("Popularity.{group}.Help");
'''
settings += '''        // Capture at mission checkpoints; APIShared copies this table before
        // publishing it to simulation and report readers.
        internal Dictionary<int, int> CaptureFearContributions()
        {
            var values = new Dictionary<int, int>();
            if (!EnableMod) return values;
'''
for group, _, _, _ in fear_groups:
    settings += f'            if ({group}Enabled)\n            {{\n'
    for row_group, _, key, level, _, _ in fear_items:
        if row_group == group:
            settings += f'                values.Add({level}, {key});\n'
    settings += '            }\n'
settings += '            return values;\n        }\n'
for group,limit,key,default,en,de in items + fear_items:
    field = key[0].lower() + key[1:]
    is_fear = group in ('NegativeFear', 'PositiveFear')
    enabled = group + 'Enabled' if is_fear else key + 'Enabled'
    if not is_fear:
        settings += f'''        private bool {field}Enabled;
        [SyncHostOnly]
        public bool {key}Enabled
        {{
            get => {field}Enabled;
            set {{ if (Change(ref {field}Enabled, value)) OnPropertyChanged(nameof({key}Editable)); }}
        }}
'''
    settings += f'''        private int {field} = {default};
        [SyncHostOnly]
        public int {key}
        {{
            get => {field};
            set {{ if (Change(ref {field}, Math.Max(-{limit}, Math.Min({limit}, value)))) OnPropertyChanged(nameof({key}ValueText)); }}
        }}
        public bool {key}Editable => CanConfigure && EnableMod && {enabled};
        public string {key}ValueText
        {{
            get => {key}.ToString(CultureInfo.InvariantCulture);
            set {{ if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {key} = parsed; OnPropertyChanged(nameof({key}ValueText)); }}
        }}
        public string {key}Title => SerpLocalization.Get("Popularity.{key}");
        public string {key}Help => SerpLocalization.Get("Popularity.{key}.Help");
'''
for group,_,_,_,_ in groups:
    settings += f'        public string {group}Title => SerpLocalization.Get("Popularity.{group}");\n'
settings += '        public string FearTitle => SerpLocalization.Get("Popularity.Fear");\n    }\n}\n'
write('src/PopularitySettings.cs', settings)

catalog = '''using RedBird.Core.Memory.Managed;
using SHCDESE.GameGlobals;
using System;

namespace PopularityTest
{
    internal sealed class PopularityParameter
    {
        internal readonly string Name, Group;
        internal readonly int Vanilla, Limit;
        internal readonly Func<GameGlobalsManager, ManagedValue<int>> Resolve;
        internal readonly Func<PopularitySettings, bool> Enabled;
        internal readonly Func<PopularitySettings, int> Read;
        internal PopularityParameter(string name, string group, int vanilla, int limit,
            Func<GameGlobalsManager, ManagedValue<int>> resolve,
            Func<PopularitySettings, bool> enabled, Func<PopularitySettings, int> read)
        { Name = name; Group = group; Vanilla = vanilla; Limit = limit; Resolve = resolve; Enabled = enabled; Read = read; }
    }
    internal static class PopularityCatalog
    {
        // Component reports divide by 25; total popularity instead divides by 100.
        internal const int UnitsPerPoint = 25;
        internal static readonly PopularityParameter[] All = {
'''
for group,limit,key,default,_,_ in items:
    catalog += f'            new PopularityParameter("{key}", "{group}", {default}, {limit}, g => g.{key}, s => s.{key}Enabled, s => s.{key}),\n'
catalog += '        };\n    }\n}\n'
write('src/PopularityCatalog.cs', catalog)


def value_row(key, limit, checkbox):
    title = (f'<CheckBox Content="{{Binding {key}Title}}" IsChecked="{{Binding {key}Enabled, Mode=TwoWay}}" IsEnabled="{{Binding CanConfigure}}" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"' if checkbox
             else f'<TextBlock Text="{{Binding {key}Title}}"')
    return f'''      <Grid Style="{{StaticResource ModSettingsSearchTargetGrid}}" Margin="0,3" shared:ModSettingsSearch.Key="popularity.{key}" shared:ModSettingsSearch.Title="{{Binding {key}Title}}" shared:ModSettingsSearch.ToolTipText="{{Binding {key}Help}}">
        <Grid.ColumnDefinitions><ColumnDefinition Width="350"/><ColumnDefinition Width="220"/><ColumnDefinition Width="70"/></Grid.ColumnDefinitions>
        {title} Grid.Column="0" Foreground="White" VerticalAlignment="Center"/>
        <Slider Grid.Column="1" Minimum="-{limit}" Maximum="{limit}" Value="{{Binding {key}, Mode=TwoWay}}" IsEnabled="{{Binding {key}Editable}}" TickFrequency="1" SmallChange="1" LargeChange="1" IsSnapToTickEnabled="True" Margin="6,0" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"/>
        <TextBox Grid.Column="2" Text="{{Binding {key}ValueText, Mode=TwoWay, UpdateSourceTrigger=LostFocus}}" IsEnabled="{{Binding {key}Editable}}" seui:KeyboardCaptureBinding.Enabled="True" Foreground="White" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1" Padding="4,1" TextAlignment="Center" VerticalAlignment="Center" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"/>
      </Grid>'''


rows = []
for group,_,_,limit,entries in groups:
    rows.append(f'''      <StackPanel shared:ModSettingsSearch.SectionKey="popularity.section.{group}" shared:ModSettingsSearch.SectionTitle="{{Binding {group}Title}}">
      <Grid Style="{{StaticResource ModSettingsSearchSectionGrid}}" shared:ModSettingsSearch.IsSection="True"><TextBlock Text="{{Binding {group}Title}}" Style="{{StaticResource SectionHeader}}" Margin="0,14,0,6"/></Grid>''')
    for key,_,_,_ in entries:
        rows.append(value_row(key, limit, True))
    rows.append('      </StackPanel>')
rows.append('''      <StackPanel shared:ModSettingsSearch.SectionKey="popularity.section.Fear" shared:ModSettingsSearch.SectionTitle="{Binding FearTitle}">
      <Grid Style="{StaticResource ModSettingsSearchSectionGrid}" shared:ModSettingsSearch.IsSection="True"><TextBlock Text="{Binding FearTitle}" Style="{StaticResource SectionHeader}" Margin="0,14,0,6"/></Grid>''')
for group,_,_,_ in fear_groups:
    rows.append(f'''      <StackPanel shared:ModSettingsSearch.SectionKey="popularity.section.{group}" shared:ModSettingsSearch.SectionTitle="{{Binding {group}Title}}">
      <Grid Style="{{StaticResource ModSettingsSearchTargetGrid}}" shared:ModSettingsSearch.Key="popularity.{group}" shared:ModSettingsSearch.Title="{{Binding {group}Title}}" shared:ModSettingsSearch.ToolTipText="{{Binding {group}Help}}" Margin="0,6,0,3">
        <CheckBox Content="{{Binding {group}Title}}" IsChecked="{{Binding {group}Enabled, Mode=TwoWay}}" IsEnabled="{{Binding CanConfigure}}" Foreground="White" FontWeight="Bold" ToolTip="{{Binding {group}Help}}" ToolTipService.ShowDuration="60000"/>
      </Grid>''')
    for row_group,limit,key,_,_,_ in fear_items:
        if row_group == group:
            rows.append(value_row(key, limit, False))
    rows.append('      </StackPanel>')
rows.append('      </StackPanel>')
template = (root/'tools/SettingsShell.xaml').read_text(encoding='utf-8-sig')
# This marker is the generator's explicit input contract, not a UI-layout assertion.
assert template.count('<!-- POPULARITY_SETTINGS -->') == 1, 'The generation slot must be unique'
write('Override/ScriptExtenderUI/PopularitySettings.xaml', template.replace('<!-- POPULARITY_SETTINGS -->', '\n'.join(rows)))

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
    base = '\n'.join(line for line in locale.read_text(encoding='utf-8-sig').splitlines() if line.startswith('Common.'))
    extra = {
     'Popularity.Scope': 'Beliebtheitsbeiträge für alle Spieler und KI. Änderungen gelten ab der nächsten Karte; während einer Partie sind die Regler gesperrt.' if german else 'Popularity contributions for all players and AI. Changes apply from the next map; controls are locked during a mission.',
     'Popularity.Rule': 'Checkbox aus: kein Override durch diesen Mod. Quelle „Mod-Standards“ laden: alle Overrides aus, Werte auf Vanilla. Einheit: Zufriedenheitspunkte der Spielberichte.' if german else 'Checkbox off: this mod supplies no override. Load the Mod defaults source to disable all overrides and restore Vanilla values. Unit: popularity points in the game reports.',
     'Popularity.Fear': 'Furchtfaktor' if german else 'Fear factor'}
    for group,en,de,_,_ in groups:
        extra['Popularity.'+group] = de if german else en
    for group,limit,key,default,en,de in items:
        extra['Popularity.'+key] = de if german else en
        explanation = (help_de if german else help_en)[group]
        suffix = f' Vanilla: {default:+d}. ' + ('Ausgeschaltet bleibt dieser Teil unverändert. Aktiviert ersetzt der Regler den absoluten Beitrag in Zufriedenheitspunkten.' if german else 'When unchecked, this part is untouched. When checked, the slider replaces the absolute contribution in popularity points.')
        extra['Popularity.'+key+'.Help'] = explanation+suffix
    for group,_,en,de in fear_groups:
        extra['Popularity.'+group] = de if german else en
        extra['Popularity.'+group+'.Help'] = ('Aktiviert die fünf festen Beiträge für diese Furchtfaktorseite. Ausgeschaltet bleibt die vorhandene Spiel-/Extender-Berechnung erhalten; eingetragene Werte bleiben gespeichert. Stufe 0 bleibt neutral. Gebäude, Produktivität und Kampfeffekte bleiben unverändert.' if german else 'Enables the five fixed contributions on this side of the fear factor. When disabled, existing game/Extender calculation is retained and entered values remain saved. Level 0 stays neutral. Buildings, productivity and combat effects are unchanged.')
    for group,limit,key,level,en,de in fear_items:
        extra['Popularity.'+key] = (de + ' (Punkte)') if german else (en + ' (points)')
        extra['Popularity.'+key+'.Help'] = (f'Fester Beliebtheitsbeitrag bei Furchtfaktorstufe {level:+d}, von -25 bis +25 in ganzen Berichtspunkten. Vanilla: {level:+d}. Gilt nur bei aktivierter zugehöriger Furchtfaktorseite. Null und umgekehrte Vorzeichen sind möglich. Der tatsächliche Furchtfaktor und seine anderen Wirkungen bleiben unverändert.' if german else f'Absolute popularity contribution at fear level {level:+d}, from -25 to +25 in whole report points. Vanilla: {level:+d}. Applies only when its fear-side switch is enabled. Zero and reversed signs are allowed. The actual fear level and its other effects are unchanged.')
    write('Locales/'+locale.name, base+'\n'+'\n'.join(k+'='+v for k,v in extra.items())+'\n')

print(f'Generated {len(items)} SE parameters and {len(fear_items)} fixed fear contributions, settings, XAML rows and locales.')
