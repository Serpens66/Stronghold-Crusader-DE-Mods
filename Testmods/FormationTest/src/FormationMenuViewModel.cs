using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace FormationTest
{
    internal sealed class FormationMenuViewModel : INotifyPropertyChanged
    {
        private static SolidColorBrush selectedBrush;
        private static SolidColorBrush unselectedBrush;

        private readonly ManualLogSource log;
        private readonly ConfigFile configFile;
        private readonly ConfigEntry<FormationKind> formation;
        private readonly ConfigEntry<int> density;
        private readonly ConfigEntry<RangedPlacementMode> placementMode;
        private readonly ConfigEntry<bool> showRoleMarkers;
        private MainViewModel subscribedMainViewModel;
        private bool menuVisible;
        private bool rolloverVisible;
        private string rolloverText = string.Empty;

        internal FormationMenuViewModel(
            ManualLogSource log,
            ConfigFile configFile,
            ConfigEntry<FormationKind> formation,
            ConfigEntry<int> density,
            ConfigEntry<RangedPlacementMode> placementMode,
            ConfigEntry<bool> showRoleMarkers)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            this.density = density ?? throw new ArgumentNullException(nameof(density));
            this.placementMode = placementMode ?? throw new ArgumentNullException(nameof(placementMode));
            this.showRoleMarkers = showRoleMarkers ?? throw new ArgumentNullException(nameof(showRoleMarkers));

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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleMenuCommand { get; }
        public ICommand SelectFormationCommand { get; }
        public ICommand SelectDensityCommand { get; }
        public ICommand SelectPlacementCommand { get; }
        public ICommand SelectRoleMarkersCommand { get; }
        public ICommand ShowRolloverCommand { get; }
        public ICommand HideRolloverCommand { get; }

        public bool MenuVisible => menuVisible;
        public bool RolloverVisible => rolloverVisible;
        public string RolloverText => rolloverText;
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

        internal void RefreshHostState()
        {
            MainViewModel current = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
            if (!ReferenceEquals(current, subscribedMainViewModel) && current != null)
            {
                subscribedMainViewModel = current;
                current.PropertyChanged += MainViewModelPropertyChanged;
            }
            if (menuVisible &&
                (current == null || !current.Show_HUD_Troops ||
                 FatControler.currentScene != Enums.SceneIDS.ActualMainGame))
            {
                SetMenuVisible(false);
            }
        }

        internal void CloseMenu() => SetMenuVisible(false);

        private void ToggleMenu()
        {
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
            if (!bool.TryParse(parameter as string, out bool requested) ||
                showRoleMarkers.Value == requested)
                return;
            showRoleMarkers.Value = requested;
            FormationPreviewOverlay.SetRoleMarkersVisible(requested);
            SaveConfiguration("showRoleMarkers", requested.ToString());
        }

        private void ShowRollover(object parameter)
        {
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
