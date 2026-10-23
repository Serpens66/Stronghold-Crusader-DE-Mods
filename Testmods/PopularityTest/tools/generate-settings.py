"""Regenerate the declared 41 settings, catalog, UI rows and locale additions."""
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
 ('Fairs', 'Fair event', 'Jahrmarkt', 32, [('FairsPopularityEventModifier',16,'Active fair event','Aktiver Jahrmarkt')]),
 ('Fear', 'Fear factor', 'Furchtfaktor', 5, [('GoodBadThingPopModifierMultiplier',1,'Contribution per fear-factor level','Beitrag je Furchtfaktorstufe')])]

items = [(group, limit, key, default, en, de) for group,_,_,limit,rows in groups for key,default,en,de in rows]
settings = '''using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;
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
            ResetToDefaultCommand = new RelayCommand(ResetDefaults);
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
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
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
for _,_,key,_,_,_ in items:
    settings += f'            OnPropertyChanged(nameof({key}Editable));\n'
settings += '''        }
        private void ResetDefaults()
        {
            if (!CanConfigure) return;
            EnableMod = true;
'''
for _,_,key,default,_,_ in items:
    settings += f'            {key}Enabled = false; {key} = {default};\n'
settings += '        }\n'
for group,limit,key,default,en,de in items:
    kind = 'double' if group == 'Fear' else 'int'
    clamp = 'Math.Round(Math.Max(-5.0, Math.Min(5.0, value)), 1, MidpointRounding.AwayFromZero)' if group == 'Fear' else f'Math.Max(-{limit}, Math.Min({limit}, value))'
    finite = 'if (double.IsNaN(value) || double.IsInfinity(value)) return; ' if group == 'Fear' else ''
    formatting = '"0.0", CultureInfo.InvariantCulture' if group == 'Fear' else 'CultureInfo.InvariantCulture'
    parser = 'double' if group == 'Fear' else 'int'
    style = 'NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint' if group == 'Fear' else 'NumberStyles.Integer'
    settings += f'''        private bool {key[0].lower()+key[1:]}Enabled;
        private {kind} {key[0].lower()+key[1:]} = {default};
        [SyncHostOnly]
        public bool {key}Enabled
        {{
            get => {key[0].lower()+key[1:]}Enabled;
            set {{ if (Change(ref {key[0].lower()+key[1:]}Enabled, value)) OnPropertyChanged(nameof({key}Editable)); }}
        }}
        [SyncHostOnly]
        public {kind} {key}
        {{
            get => {key[0].lower()+key[1:]};
            set {{ {finite}if (Change(ref {key[0].lower()+key[1:]}, {clamp})) OnPropertyChanged(nameof({key}ValueText)); }}
        }}
        public bool {key}Editable => CanConfigure && EnableMod && {key}Enabled;
        public string {key}ValueText
        {{
            get => {key}.ToString({formatting});
            set {{ if ({parser}.TryParse(value.Replace(',', '.'), {style}, CultureInfo.InvariantCulture, out {kind} parsed)) {key} = parsed; OnPropertyChanged(nameof({key}ValueText)); }}
        }}
        public string {key}Title => SerpLocalization.Get("Popularity.{key}");
        public string {key}Help => SerpLocalization.Get("Popularity.{key}.Help");
'''
for group,_,_,_,_ in groups:
    settings += f'        public string {group}Title => SerpLocalization.Get("Popularity.{group}");\n'
settings += '    }\n}\n'
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
        // The game's component reports divide by 25; total popularity instead divides by 100.
        internal const int UnitsPerPoint = 25;
        internal static readonly PopularityParameter[] All = {
'''
for group,limit,key,default,_,_ in items:
    if group == 'Fear': continue
    catalog += f'            new PopularityParameter("{key}", "{group}", {default}, {limit}, g => g.{key}, s => s.{key}Enabled, s => s.{key}),\n'
catalog += '        };\n    }\n}\n'
write('src/PopularityCatalog.cs', catalog)

template = (root/'Override/ScriptExtenderUI/PopularitySettings.xaml').read_text(encoding='utf-8-sig')
start = template.index('      <TextBlock Text="{Binding '+groups[0][0]+'Title}"')
end = template.index('      <TextBlock Text="{Binding RuleText}"', start)
template = template[:start] + template[end:]

rows = []
for group,en,de,limit,entries in groups:
    rows.append(f'      <TextBlock Text="{{Binding {group}Title}}" Foreground="#FFF2D48A" FontWeight="Bold" Margin="0,14,0,6"/>')
    for key,default,_,_ in entries:
        rows.append(f'''      <Grid Margin="0,3" shared:ModSettingsSearch.Key="popularity.{key}" shared:ModSettingsSearch.Title="{{Binding {key}Title}}" shared:ModSettingsSearch.ToolTipText="{{Binding {key}Help}}">
        <Grid.ColumnDefinitions><ColumnDefinition Width="350"/><ColumnDefinition Width="220"/><ColumnDefinition Width="70"/></Grid.ColumnDefinitions>
        <CheckBox Grid.Column="0" Content="{{Binding {key}Title}}" IsChecked="{{Binding {key}Enabled, Mode=TwoWay}}" IsEnabled="{{Binding CanConfigure}}" Foreground="White" VerticalAlignment="Center" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"/>
        <Slider Grid.Column="1" Minimum="-{limit}" Maximum="{limit}" Value="{{Binding {key}, Mode=TwoWay}}" IsEnabled="{{Binding {key}Editable}}" TickFrequency="{0.1 if group == 'Fear' else 1}" SmallChange="{0.1 if group == 'Fear' else 1}" LargeChange="{0.1 if group == 'Fear' else 1}" IsSnapToTickEnabled="True" Margin="6,0" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"/>
        <TextBox Grid.Column="2" Text="{{Binding {key}ValueText, Mode=TwoWay, UpdateSourceTrigger=LostFocus}}" IsEnabled="{{Binding {key}Editable}}" seui:KeyboardCaptureBinding.Enabled="True" Foreground="White" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1" Padding="4,1" TextAlignment="Center" VerticalAlignment="Center" ToolTip="{{Binding {key}Help}}" ToolTipService.ShowDuration="60000"/>
      </Grid>''')
template = template.replace('      <TextBlock Text="{Binding RuleText}"', '\n'.join(rows)+'\n      <TextBlock Text="{Binding RuleText}"')
write('Override/ScriptExtenderUI/PopularitySettings.xaml', template)

help_en = {
 'Rations':'Applies to the selected ration level when population and available food are present. The no-food and no-population cases replace the ration-level contribution.',
 'Variety':'Added to the ration contribution when this exact number of food types is consumed.',
 'Crowding':'Applies at the indicated population-to-housing ratio; the existing thresholds are unchanged.',
 'Taxes':'Applies to this tax/donation mode. Donations require positive gold; without gold the separate fallback applies. Gold income and expenditure are unchanged.',
 'Religion':'Applies to this blessing-coverage tier or, for buildings, once when at least one such building exists. Church and cathedral bonuses are additive.',
 'Ale':'Applies to this inn coverage tier. Existing coverage thresholds and ale consumption are unchanged.',
 'Fairs':'Applies while the game flags a fair event as active. Other event bonuses and penalties are unchanged.',
 'Fear':'Multiplier from -5 to +5 in steps of 0.1. Multiply the signed fear level first, then round the result to whole popularity points (midpoint ties away from zero). Productivity and combat effects are unchanged.'}
help_de = {
 'Rations':'Gilt für die gewählte Rationsstufe bei vorhandener Bevölkerung und Nahrung. Ohne verfügbare Nahrung bzw. ohne Bevölkerung gilt stattdessen der jeweilige Sonderwert.',
 'Variety':'Wird bei genau dieser Anzahl verzehrter Nahrungssorten zum Rationsbeitrag addiert.',
 'Crowding':'Gilt beim angegebenen Verhältnis von Bevölkerung zu Wohnraum; die vorhandenen Schwellen bleiben unverändert.',
 'Taxes':'Gilt für diese Steuer-/Bestechungsstufe. Bestechung benötigt positives Gold; ohne Gold gilt der eigene Sonderwert. Goldeinnahmen und Ausgaben bleiben unverändert.',
 'Religion':'Gilt für die angegebene Segnungsstufe bzw. bei Gebäuden einmalig, sobald mindestens eines vorhanden ist. Kirche und Kathedrale werden addiert.',
 'Ale':'Gilt für diese Wirtshausabdeckung. Abdeckungsschwellen und Bierverbrauch bleiben unverändert.',
 'Fairs':'Gilt, solange das Spiel einen aktiven Jahrmarkt meldet. Andere Ereignisboni und -mali bleiben unverändert.',
 'Fear':'Faktor von -5 bis +5 in 0,1-Schritten. Erst mit der vorzeichenbehafteten Furchtfaktorstufe multiplizieren, dann auf ganze Zufriedenheitspunkte runden (halbe Werte von null weg). Produktivität und Kampfeffekte bleiben unverändert.'}
for locale in (root/'Locales').glob('*.txt'):
    german = locale.stem == 'de-DE'
    base = '\n'.join(line for line in locale.read_text(encoding='utf-8-sig').splitlines() if line.startswith('Common.'))
    extra = {
     'Popularity.Scope': 'Alle 41 direkten SE-Beiträge. Änderungen gelten für alle Spieler und KI ab der nächsten Karte; während einer Partie sind die Regler gesperrt.' if german else 'All 41 direct SE contributions. Changes apply to all players and AI from the next map; controls are locked during a mission.',
     'Popularity.Rule': 'Checkbox aus: kein Override durch diesen Mod. Zurücksetzen: alle Checkboxen aus, Werte auf Vanilla. Einheit: Zufriedenheitspunkte der Spielberichte; Furchtfaktor je Stufe.' if german else 'Checkbox off: this mod supplies no override. Reset disables all checkboxes and restores Vanilla defaults. Unit: popularity points in the game reports; fear factor is per level.'}
    for group,en,de,_,_ in groups:
        extra['Popularity.'+group] = de if german else en
    for group,limit,key,default,en,de in items:
        extra['Popularity.'+key] = de if german else en
        explanation = (help_de if german else help_en)[group]
        suffix = f' Vanilla: {default:+d}. ' + ('Ausgeschaltet bleibt dieser Teil unverändert. Aktiviert ersetzt der Regler den absoluten Beitrag in Zufriedenheitspunkten.' if german else 'When unchecked, this part is untouched. When checked, the slider replaces the absolute contribution in popularity points.')
        extra['Popularity.'+key+'.Help'] = explanation+suffix
    write('Locales/'+locale.name, base+'\n'+'\n'.join(k+'='+v for k,v in extra.items())+'\n')

print(f'Generated {len(items)} settings, catalog bindings, XAML rows and locale additions.')
