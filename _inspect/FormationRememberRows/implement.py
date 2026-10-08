from pathlib import Path
import shutil
root=Path.cwd()
def read(p):
    f=root/p; backup=root/'_inspect/FormationRememberRows/before'/p
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(f,backup)
    return f.read_text(encoding='utf-8-sig')
def write(p,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');f=root/p;f.write_bytes(data);assert f.read_bytes()==data
def rep(s,a,b):
    assert a in s,a
    return s.replace(a,b)
p='APIShared/src/UnitCommands/FormationPresentation.cs';s=read(p)
s=rep(s,'        void CloseMenu();','        int GetRememberedRows(FormationKind kind);\n        void RememberSelectedRows(FormationKind kind, int rows);\n        void CloseMenu();');write(p,s)
p='APIShared/src/UnitCommands/FormationModel.cs';s=read(p)
s=rep(s,'FormationGestureState(FormationKind kind, int count, int direction)','FormationGestureState(FormationKind kind, int count, int direction, int rememberedRows = 0)')
s=rep(s,'            Rows = FormationModel.ResolveAutomaticRows(kind, count);','''            int selectedRows = FormationModel.NormalizeRememberedRows(rememberedRows);
            Rows = kind == FormationKind.Circle || kind == FormationKind.Vanilla || selectedRows == 0
                ? FormationModel.ResolveAutomaticRows(kind, count)
                : FormationModel.NormalizeRows(selectedRows, count);''')
s=rep(s,'        internal static int NormalizeDensity(int value) =>','''        internal static int NormalizeRememberedRows(int value) =>
            value >= 1 && value <= 10000 ? value : 0;

        internal static int NormalizeDensity(int value) =>''');write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
s=rep(s,'            else if (changedRows != 0)\n                LogDebugNoThrow(','            else if (changedRows != 0)\n            {\n                menuViewModel.RememberSelectedRows(state.Kind, changedRows);\n                LogDebugNoThrow(')
s=rep(s,'$"thread={Environment.CurrentManagedThreadId}.");\n        }\n\n        private void TryStartDrag','$"thread={Environment.CurrentManagedThreadId}.");\n            }\n        }\n\n        private void TryStartDrag')
s=rep(s,'FormationModel.NormalizePlacementMode((int)placementModeConfig.Value));','FormationModel.NormalizePlacementMode((int)placementModeConfig.Value),\n                menuViewModel.GetRememberedRows(FormationModel.NormalizeKind((int)formationConfig.Value)));')
s=rep(s,'                RangedPlacementMode placementMode)\n            {\n                CommandButton','                RangedPlacementMode placementMode,\n                int rememberedRows)\n            {\n                CommandButton')
s=rep(s,'new FormationGestureState(kind, selection.Length, DefaultDirectionSector);','new FormationGestureState(kind, selection.Length, DefaultDirectionSector, rememberedRows);');write(p,s)
p='BugfixesAndQoL/src/FormationFeature.cs';s=read(p)
s=rep(s,'            FormationPreviewOverlay.Initialize(log, roles.Value);','''            var rememberRows = config.Bind("Formation", "RememberRows", true,
                "Remember wheel-selected row counts separately for each arrangement across restarts.");
            var rememberedRows = new ConfigEntry<int>[6];
            foreach (FormationKind rowKind in new[] { FormationKind.Block, FormationKind.Line, FormationKind.Column, FormationKind.Wedge })
            {
                var entry = config.Bind("Formation", "Remembered" + rowKind + "Rows", 0,
                    "Local remembered row count; 0 selects automatic rows. Valid range: 1..10000.");
                entry.Value = FormationModel.NormalizeRememberedRows(entry.Value);
                rememberedRows[(int)rowKind] = entry;
            }
            FormationPreviewOverlay.Initialize(log, roles.Value);''')
s=rep(s,'kind, density, placement, roles);','kind, density, placement, roles, rememberRows, rememberedRows);');write(p,s)
p='BugfixesAndQoL/src/FormationMenuViewModel.cs';s=read(p)
s=rep(s,'        private readonly ConfigEntry<bool> showRoleMarkers;','''        private readonly ConfigEntry<bool> showRoleMarkers;
        private readonly ConfigEntry<bool> rememberRows;
        private readonly ConfigEntry<int>[] rememberedRows;''')
s=rep(s,'            ConfigEntry<bool> showRoleMarkers)','            ConfigEntry<bool> showRoleMarkers,\n            ConfigEntry<bool> rememberRows,\n            ConfigEntry<int>[] rememberedRows)')
s=rep(s,'            ToggleMenuCommand =','''            this.rememberRows = rememberRows ?? throw new ArgumentNullException(nameof(rememberRows));
            this.rememberedRows = rememberedRows ?? throw new ArgumentNullException(nameof(rememberedRows));

            ToggleMenuCommand =''')
s=rep(s,'            showRoleMarkers.SettingChanged += ConfigurationChanged;','            showRoleMarkers.SettingChanged += ConfigurationChanged;\n            rememberRows.SettingChanged += ConfigurationChanged;')
s=rep(s,'        public void SetPreviewAuthorization', '''        public string RememberRowsText => Shared.SerpLocalization.Get("BugfixesAndQoL.RememberRows");
        public string RememberRowsHelp => Shared.SerpLocalization.Get("BugfixesAndQoL.RememberRowsHelp");
        public bool RememberRows
        {
            get => rememberRows.Value;
            set => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (!FeatureAvailable || rememberRows.Value == value) return;
                try { rememberRows.Value = value; SaveConfiguration("rememberRows", value.ToString()); }
                catch (Exception ex) { Shared.DebugLogHelper.LogWarning(log, "Arrangement preference save failed: " + ex.Message); }
            });
        }

        public int GetRememberedRows(FormationKind kind)
        {
            int index = (int)kind;
            return rememberRows.Value && index >= (int)FormationKind.Block && index <= (int)FormationKind.Wedge
                ? FormationModel.NormalizeRememberedRows(rememberedRows[index].Value) : 0;
        }

        public void RememberSelectedRows(FormationKind kind, int rows)
        {
            int index = (int)kind;
            int selectedRows = FormationModel.NormalizeRememberedRows(rows);
            if (!rememberRows.Value || index < (int)FormationKind.Block || index > (int)FormationKind.Wedge || selectedRows == 0) return;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (!rememberRows.Value || rememberedRows[index].Value == selectedRows) return;
                try { rememberedRows[index].Value = selectedRows; SaveConfiguration("remembered" + kind + "Rows", selectedRows.ToString()); }
                catch (Exception ex) { Shared.DebugLogHelper.LogWarning(log, "Arrangement preference save failed: " + ex.Message); }
            });
        }

        public void SetPreviewAuthorization''')
s=rep(s,'        private void NotifySelectionsChanged()\n        {','        private void NotifySelectionsChanged()\n        {\n            OnChanged(nameof(RememberRows));');write(p,s)
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p)
s=rep(s,'       xmlns:local="clr-namespace:CrusaderDE"','       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"\n       xmlns:local="clr-namespace:CrusaderDE"')
style='''
        <Grid.Resources>
          <Style TargetType="{x:Type ToolTip}">
            <Setter Property="Background" Value="#FF1D1710"/><Setter Property="Foreground" Value="White"/>
            <Setter Property="seui:ToolTipResolutionScale.Enabled" Value="True"/>
            <Setter Property="BorderBrush" Value="#FFF2D48A"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="MaxWidth" Value="450"/><Setter Property="Padding" Value="10,8"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type ToolTip}">
              <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="2" Padding="{TemplateBinding Padding}" MaxWidth="{TemplateBinding MaxWidth}">
                <TextBlock Text="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" FontSize="{TemplateBinding FontSize}" TextWrapping="Wrap"/>
              </Border>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
        </Grid.Resources>'''
needle='Visibility="{Binding MenuVisible, Converter={StaticResource booleanToVisibilityConverter}, FallbackValue=Collapsed}">'
s=rep(s,needle,needle+style)
needle='Command="{Binding SelectRoleMarkersCommand}" CommandParameter="False"/></StackPanel></Grid>'
checkbox='''Command="{Binding SelectRoleMarkersCommand}" CommandParameter="False"/><CheckBox Width="150" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12" Content="{Binding RememberRowsText}" IsChecked="{Binding RememberRows, Mode=TwoWay}" ToolTip="{Binding RememberRowsHelp}" ToolTipService.ShowDuration="60000" Style="{StaticResource CheckBox_Template}"/></StackPanel></Grid>'''
s=rep(s,needle,checkbox);write(p,s)
for f in (root/'BugfixesAndQoL/Locales').glob('*.txt'):
    p=str(f.relative_to(root));s=read(p)
    if f.name=='de-DE.txt':
        label='Breite/LÃƒÂ¤nge merken'
        help='Merkt sich die zuletzt per Mausrad gewÃƒÂ¤hlte Reihenanzahl getrennt fÃƒÂ¼r Block, Linie, Kolonne und Keil, auch nach einem Spielneustart. Kleinere Gruppen verwenden hÃƒÂ¶chstens eine Reihe pro Einheit; der gemerkte Wert bleibt erhalten. Ohne gemerkten Wert oder bei ausgeschalteter Option beginnt jeder Befehl mit der automatischen Reihenanzahl. Kreis und Vanilla bleiben unverÃƒÂ¤ndert.'
    else:
        label='Remember rows'
        help='Remembers the last wheel-selected row count separately for Block, Line, Column and Wedge, including across game restarts. Smaller groups use at most one row per unit without overwriting the remembered value. Without a remembered value, or with this option off, each command starts with automatic rows. Circle and Vanilla remain unchanged.'
    s+='\nBugfixesAndQoL.RememberRows='+label+'\nBugfixesAndQoL.RememberRowsHelp='+help+'\n';write(p,s)
write('_inspect/FormationRememberRows/implement.py',Path(__file__).read_text())
print('Implemented remembered rows, local config and compact styled checkbox; starting copies saved.')
