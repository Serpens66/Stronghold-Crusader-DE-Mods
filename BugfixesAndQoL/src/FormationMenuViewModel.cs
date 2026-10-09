using BugfixesAndQoL.UnitCommands;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace BugfixesAndQoL
{
    internal sealed class FormationMenuViewModel : INotifyPropertyChanged, IFormationPresentation
    {
        private static SolidColorBrush selectedBrush;
        private static SolidColorBrush unselectedBrush;

        private readonly ManualLogSource log;
        internal Func<bool> Enabled = () => false;
        private readonly ConfigFile configFile;
        private readonly ConfigEntry<FormationKind> formation;
        private readonly ConfigEntry<int> density;
        private readonly ConfigEntry<RangedPlacementMode> placementMode;
        private readonly ConfigEntry<bool> showRoleMarkers;
        private readonly ConfigEntry<bool> rememberRows;
        private readonly ConfigEntry<int>[] rememberedRows;
        private MainViewModel subscribedMainViewModel;
        private MainViewModel buttonTooltipOwner;
        private HUD_Troops buttonTooltipPanel;
        private HUD_Troops hookedButtonPanel;
        private Button hookedButton;
        private string buttonTooltipText;
        private bool menuVisible;
        private bool rolloverVisible;
        private string rolloverText = string.Empty;

        internal FormationMenuViewModel(
            ManualLogSource log,
            ConfigFile configFile,
            ConfigEntry<FormationKind> formation,
            ConfigEntry<int> density,
            ConfigEntry<RangedPlacementMode> placementMode,
            ConfigEntry<bool> showRoleMarkers,
            ConfigEntry<bool> rememberRows,
            ConfigEntry<int>[] rememberedRows)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            this.density = density ?? throw new ArgumentNullException(nameof(density));
            this.placementMode = placementMode ?? throw new ArgumentNullException(nameof(placementMode));
            this.showRoleMarkers = showRoleMarkers ?? throw new ArgumentNullException(nameof(showRoleMarkers));

            this.rememberRows = rememberRows ?? throw new ArgumentNullException(nameof(rememberRows));
            this.rememberedRows = rememberedRows ?? throw new ArgumentNullException(nameof(rememberedRows));

            ToggleMenuCommand = new ParameterCommand(_ => ToggleMenu());
            SelectFormationCommand = new ParameterCommand(SelectFormation);
            SelectDensityCommand = new ParameterCommand(SelectDensity);
            SelectPlacementCommand = new ParameterCommand(SelectPlacement);
            SelectRoleMarkersCommand = new ParameterCommand(SelectRoleMarkers);
            ShowRolloverCommand = new ParameterCommand(ShowRollover);
            HideRolloverCommand = new ParameterCommand(_ => HideRollover());

            formation.SettingChanged += ConfigurationChanged;
            density.SettingChanged += ConfigurationChanged;
            placementMode.SettingChanged += ConfigurationChanged;
            showRoleMarkers.SettingChanged += ConfigurationChanged;
            rememberRows.SettingChanged += ConfigurationChanged;
        }

        public string RememberRowsText => SerpLocalization.Get("BugfixesAndQoL.RememberRows");
        public string RememberRowsHelp => SerpLocalization.Get("BugfixesAndQoL.RememberRowsHelp");
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

        public void SetPreviewAuthorization(bool allowed) => FormationPreviewOverlay.SetNativeAllowed(allowed);
        public bool FeatureAvailable => Enabled();
        private bool lastAvailability;
        public void RefreshPreview() => FormationPreviewOverlay.Refresh();
        public void ClearPreview() => FormationPreviewOverlay.Clear();
        public void PublishPreview(FormationPreviewPoint[] points, FormationDirectionIndicator direction) =>
            FormationPreviewOverlay.Publish(points, direction);

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleMenuCommand { get; }
        public ICommand SelectFormationCommand { get; }
        public ICommand SelectDensityCommand { get; }
        public ICommand SelectPlacementCommand { get; }
        public ICommand SelectRoleMarkersCommand { get; }
        public ICommand ShowRolloverCommand { get; }
        public ICommand HideRolloverCommand { get; }

        public bool MenuVisible => menuVisible && FeatureAvailable;
        public bool RolloverVisible => rolloverVisible && FeatureAvailable;
        public string RolloverText => rolloverText;
        public bool IsVanilla => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Vanilla;
        public bool IsBlock => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Block;
        public bool IsLine => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Line;
        public bool IsColumn => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Column;
        public bool IsWedge => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Wedge;
        public bool IsCircle => FormationModel.NormalizeKind((int)formation.Value) == FormationKind.Circle;
        public SolidColorBrush VanillaBackground => FormationBrush(FormationKind.Vanilla);
        public SolidColorBrush BlockBackground => FormationBrush(FormationKind.Block);
        public SolidColorBrush LineBackground => FormationBrush(FormationKind.Line);
        public SolidColorBrush ColumnBackground => FormationBrush(FormationKind.Column);
        public SolidColorBrush WedgeBackground => FormationBrush(FormationKind.Wedge);
        public SolidColorBrush CircleBackground => FormationBrush(FormationKind.Circle);
        public SolidColorBrush TightBackground => DensityBrush(1);
        public SolidColorBrush NormalBackground => DensityBrush(2);
        public SolidColorBrush FarBackground => DensityBrush(3);
        public SolidColorBrush VeryFarBackground => DensityBrush(4);
        public SolidColorBrush PlacementOffBackground => PlacementBrush(RangedPlacementMode.Off);
        public SolidColorBrush PlacementRearBackground => PlacementBrush(RangedPlacementMode.Rear);
        public SolidColorBrush PlacementCenterBackground => PlacementBrush(RangedPlacementMode.Center);
        public SolidColorBrush RoleMarkersOnBackground => BooleanBrush(showRoleMarkers.Value);
        public SolidColorBrush RoleMarkersOffBackground => BooleanBrush(!showRoleMarkers.Value);

        internal void RefreshAvailability()
        {
            bool available = FeatureAvailable;
            if (!available)
            {
                SetMenuVisible(false);
                HideRollover();
                DetachButtonEvents();
            }
            if (lastAvailability != available)
            {
                lastAvailability = available;
                OnChanged(nameof(FeatureAvailable));
                OnChanged(nameof(MenuVisible));
                OnChanged(nameof(RolloverVisible));
            }
        }

        public void RefreshHostState()
        {
            RefreshAvailability();
            MainViewModel current = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
            bool hostAvailable = FeatureAvailable && current != null && current.Show_HUD_Troops &&
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
            if (menuVisible && (!Enabled() ||
                (current == null || !current.Show_HUD_Troops ||
                 FatControler.currentScene != Enums.SceneIDS.ActualMainGame)))
            {
                SetMenuVisible(false);
            }
        }

        public void CloseMenu() => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
        {
            SetMenuVisible(false);
            HideButtonTooltip();
            RefreshAvailability();
        });

        private void ToggleMenu()
        {
            if (!Enabled()) return;
            RefreshHostState();
            if (!MainViewModel.viewModelLoaded)
                return;
            MainViewModel main = MainViewModel.Instance;
            if (menuVisible)
            {
                SetMenuVisible(false);
                return;
            }
            if (main == null || !main.Show_HUD_Troops ||
                FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
                return;
            main.Show_HUD_ControlGroups = false;
            SetMenuVisible(true);
        }

        private void SelectFormation(object parameter)
        {
            if (!FeatureAvailable) return;
            if (!Enum.TryParse(parameter as string, true, out FormationKind requested))
                return;
            FormationKind normalized = FormationModel.NormalizeKind((int)requested);
            if (formation.Value == normalized)
                return;
            formation.Value = normalized;
            SaveConfiguration("formation", normalized.ToString());
        }

        private void SelectDensity(object parameter)
        {
            if (!FeatureAvailable) return;
            if (!int.TryParse(parameter as string, out int requested))
                return;
            int normalized = FormationModel.NormalizeDensity(requested);
            if (density.Value == normalized)
                return;
            density.Value = normalized;
            SaveConfiguration("density", normalized.ToString());
        }

        private void SelectPlacement(object parameter)
        {
            if (!FeatureAvailable) return;
            if (!Enum.TryParse(parameter as string, true, out RangedPlacementMode requested))
                return;
            RangedPlacementMode normalized = FormationModel.NormalizePlacementMode((int)requested);
            if (placementMode.Value == normalized)
                return;
            placementMode.Value = normalized;
            SaveConfiguration("rangedPlacement", normalized.ToString());
        }

        private void SelectRoleMarkers(object parameter)
        {
            if (!FeatureAvailable) return;
            if (!bool.TryParse(parameter as string, out bool requested) ||
                showRoleMarkers.Value == requested)
                return;
            showRoleMarkers.Value = requested;
            FormationPreviewOverlay.SetRoleMarkersVisible(requested);
            SaveConfiguration("showRoleMarkers", requested.ToString());
        }

        private void RefreshButtonEvents(HUD_Troops panel)
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

        private void ShowRollover(object parameter)
        {
            if (!FeatureAvailable) return;
            string text = parameter as string;
            if (string.IsNullOrWhiteSpace(text))
                return;
            rolloverText = text;
            rolloverVisible = true;
            OnChanged(nameof(RolloverText));
            OnChanged(nameof(RolloverVisible));
        }

        private void HideRollover()
        {
            if (!rolloverVisible)
                return;
            rolloverVisible = false;
            OnChanged(nameof(RolloverVisible));
        }

        private void SaveConfiguration(string setting, string value)
        {
            configFile.Save();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"FORMATION_MENU_CHANGED: setting={setting}, value={value}.");
        }

        private void ConfigurationChanged(object sender, System.EventArgs args)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(NotifySelectionsChanged);
        }

        private void MainViewModelPropertyChanged(
            object sender,
            PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(MainViewModel.Show_HUD_Troops) &&
                sender is MainViewModel changedMain && !changedMain.Show_HUD_Troops)
                DetachButtonEvents();
            if (!menuVisible)
                return;
            MainViewModel source = sender as MainViewModel;
            if (source == null)
                return;
            if ((args.PropertyName == nameof(MainViewModel.Show_HUD_ControlGroups) &&
                 source.Show_HUD_ControlGroups) ||
                (args.PropertyName == nameof(MainViewModel.Show_HUD_Troops) &&
                 !source.Show_HUD_Troops))
            {
                SetMenuVisible(false);
            }
        }

        private SolidColorBrush FormationBrush(FormationKind kind) =>
            FormationModel.NormalizeKind((int)formation.Value) == kind
                ? SelectedBrush
                : UnselectedBrush;

        private SolidColorBrush DensityBrush(int value) =>
            FormationModel.NormalizeDensity(density.Value) == value
                ? SelectedBrush
                : UnselectedBrush;

        private static SolidColorBrush SelectedBrush => selectedBrush ??
            (selectedBrush = new SolidColorBrush(Color.FromArgb(210, 91, 117, 44)));

        private static SolidColorBrush UnselectedBrush => unselectedBrush ??
            (unselectedBrush = new SolidColorBrush(Color.FromArgb(145, 0, 0, 0)));

        private void SetMenuVisible(bool value)
        {
            if (menuVisible == value)
                return;
            menuVisible = value;
            if (!value)
                HideRollover();
            OnChanged(nameof(MenuVisible));
            Shared.DebugLogHelper.LogDebug(
                log, $"FORMATION_MENU_VISIBILITY: visible={value}.");
        }

        private void NotifySelectionsChanged()
        {
            OnChanged(nameof(RememberRows));
            OnChanged(nameof(IsVanilla));
            OnChanged(nameof(IsBlock));
            OnChanged(nameof(IsLine));
            OnChanged(nameof(IsColumn));
            OnChanged(nameof(IsWedge));
            OnChanged(nameof(IsCircle));
            OnChanged(nameof(VanillaBackground));
            OnChanged(nameof(BlockBackground));
            OnChanged(nameof(LineBackground));
            OnChanged(nameof(ColumnBackground));
            OnChanged(nameof(WedgeBackground));
            OnChanged(nameof(CircleBackground));
            OnChanged(nameof(TightBackground));
            OnChanged(nameof(NormalBackground));
            OnChanged(nameof(FarBackground));
            OnChanged(nameof(VeryFarBackground));
            OnChanged(nameof(PlacementOffBackground));
            OnChanged(nameof(PlacementRearBackground));
            OnChanged(nameof(PlacementCenterBackground));
            OnChanged(nameof(RoleMarkersOnBackground));
            OnChanged(nameof(RoleMarkersOffBackground));
            FormationPreviewOverlay.SetRoleMarkersVisible(showRoleMarkers.Value);
        }

        private SolidColorBrush PlacementBrush(RangedPlacementMode mode) =>
            FormationModel.NormalizePlacementMode((int)placementMode.Value) == mode
                ? SelectedBrush
                : UnselectedBrush;

        private static SolidColorBrush BooleanBrush(bool selected) =>
            selected ? SelectedBrush : UnselectedBrush;

        private void OnChanged(string property) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        private sealed class ParameterCommand : ICommand
        {
            private readonly Action<object> execute;

            internal ParameterCommand(Action<object> execute)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public event System.EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => execute(parameter);
        }
    }
}
