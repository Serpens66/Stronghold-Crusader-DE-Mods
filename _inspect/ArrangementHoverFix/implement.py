from pathlib import Path
import shutil
root=Path.cwd(); out=root/'_inspect/ArrangementHoverFix'
paths=['BugfixesAndQoL/src/FormationMenuViewModel.cs','BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml','BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationStartupTests.cs']
for rel in paths:
    dst=out/'before'/rel; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(root/rel,dst)
def save(rel,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); (root/rel).write_bytes(data); assert (root/rel).read_bytes()==data
p=paths[0]; s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('private MainViewModel buttonTooltipOwner;', 'private MainViewModel buttonTooltipOwner;\n        private HUD_Troops buttonTooltipPanel;\n        private HUD_Troops hookedButtonPanel;\n        private Button hookedButton;')
for line in ['            ShowButtonTooltipCommand = new ParameterCommand(_ => ShowButtonTooltip());\n','            HideButtonTooltipCommand = new ParameterCommand(_ => HideButtonTooltip());\n','        public ICommand ShowButtonTooltipCommand { get; }\n','        public ICommand HideButtonTooltipCommand { get; }\n']:
    assert line in s; s=s.replace(line,'')
s=s.replace('                HideRollover();\n                HideButtonTooltip();','                HideRollover();\n                DetachButtonEvents();')
start=s.index('            if (buttonTooltipOwner != null &&'); end=s.index('            if (menuVisible &&',start)
s=s[:start]+'''            bool hostAvailable = FeatureAvailable && current != null && current.Show_HUD_Troops &&
                FatControler.currentScene == Enums.SceneIDS.ActualMainGame;
            RefreshButtonEvents(hostAvailable ? current.HUDTroopPanel : null);
            if (!ReferenceEquals(current, subscribedMainViewModel))
            {
                if (subscribedMainViewModel != null)
                    subscribedMainViewModel.PropertyChanged -= MainViewModelPropertyChanged;
                subscribedMainViewModel = current;
                if (current != null)
                    current.PropertyChanged += MainViewModelPropertyChanged;
            }
'''+s[end:]
start=s.index('        private void ShowButtonTooltip()'); end=s.index('        private void ShowRollover(',start)
s=s[:start]+'''        private void RefreshButtonEvents(HUD_Troops panel)
        {
            Button button = panel?.FindName("BugfixesAndQoLFormationOpenButton") as Button;
            if (ReferenceEquals(button, hookedButton) && ReferenceEquals(panel, hookedButtonPanel))
                return;
            DetachButtonEvents();
            if (button == null) return;
            hookedButtonPanel = panel;
            hookedButton = button;
            button.MouseEnter += OnButtonMouseEnter;
            button.MouseLeave += OnButtonMouseLeave;
        }

        private void DetachButtonEvents()
        {
            HideButtonTooltip();
            if (hookedButton != null)
            {
                hookedButton.MouseEnter -= OnButtonMouseEnter;
                hookedButton.MouseLeave -= OnButtonMouseLeave;
            }
            hookedButton = null;
            hookedButtonPanel = null;
        }

        private void OnButtonMouseEnter(object sender, MouseEventArgs args)
        {
            if (!ReferenceEquals(sender, hookedButton)) return;
            MainViewModel current = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
            if (!FeatureAvailable || current == null || !current.Show_HUD_Troops ||
                !ReferenceEquals(current.HUDTroopPanel, hookedButtonPanel) ||
                FatControler.currentScene != Enums.SceneIDS.ActualMainGame) return;
            HideButtonTooltip();
            try
            {
                HUD_Troops panel = hookedButtonPanel;
                if (panel.RefTroopsPanelRollover == null) return;
                string text = SerpLocalization.Get("BugfixesAndQoL.ArrangementTooltip");
                current.TroopsPanelRollover = text;
                current.TroopsPanelRollover_AmountReq1 = string.Empty;
                current.TroopsPanelRollover_AmountGot1 = string.Empty;
                current.TroopsPanelRollover_GoodsImage1 = null;
                panel.RefTroopsPanelRollover.Visibility = Visibility.Visible;
                if (panel.RefTroopsPanelRollover2 != null)
                    panel.RefTroopsPanelRollover2.Visibility = Visibility.Hidden;
                buttonTooltipOwner = current;
                buttonTooltipPanel = panel;
                buttonTooltipText = text;
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogWarning(log, "Arrangement hover unavailable: " + ex.Message); }
        }

        private void OnButtonMouseLeave(object sender, MouseEventArgs args)
        {
            if (ReferenceEquals(sender, hookedButton)) HideButtonTooltip();
        }

        private void HideButtonTooltip()
        {
            MainViewModel owner = buttonTooltipOwner;
            HUD_Troops panel = buttonTooltipPanel;
            string text = buttonTooltipText;
            buttonTooltipOwner = null;
            buttonTooltipPanel = null;
            buttonTooltipText = null;
            // Leave a rollover that another button or a replacement HUD has taken over.
            if (owner == null || panel == null || !ReferenceEquals(owner.HUDTroopPanel, panel) ||
                !string.Equals(owner.TroopsPanelRollover, text, StringComparison.Ordinal)) return;
            try
            {
                if (panel.RefTroopsPanelRollover != null)
                    panel.RefTroopsPanelRollover.Visibility = Visibility.Hidden;
                if (panel.RefTroopsPanelRollover2 != null)
                    panel.RefTroopsPanelRollover2.Visibility = Visibility.Hidden;
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogWarning(log, "Arrangement hover close failed: " + ex.Message); }
        }

'''+s[end:]
s=s.replace('                HideButtonTooltip();\n            if (!menuVisible)', '                DetachButtonEvents();\n            if (!menuVisible)')
save(p,s)
p=paths[1]; s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('<Button Width="35" Height="35" Padding="0" Command="{Binding ToggleMenuCommand}">','<Button x:Name="BugfixesAndQoLFormationOpenButton" Width="35" Height="35" Padding="0" Command="{Binding ToggleMenuCommand}">')
line='          <b:Interaction.Triggers><b:EventTrigger EventName="MouseEnter"><b:InvokeCommandAction Command="{Binding ShowButtonTooltipCommand}"/></b:EventTrigger><b:EventTrigger EventName="MouseLeave"><b:InvokeCommandAction Command="{Binding HideButtonTooltipCommand}"/></b:EventTrigger></b:Interaction.Triggers>\n'
assert line in s; save(p,s.replace(line,''))
