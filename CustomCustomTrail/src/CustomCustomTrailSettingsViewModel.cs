using CustomCustomTrail.Core;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace CustomCustomTrail
{
    public sealed class CustomCustomTrailSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        internal const string ErrorStatusPrefix = "ERROR|";
        internal const string MissingStatus = "ERROR|MISSING";
        internal const string MismatchStatus = "ERROR|MISMATCH";
        internal const string InvalidStatusPrefix = "ERROR|INVALID|";
        internal const string DisabledStatus = "ERROR|DISABLED";
        internal const string WaitingStatus = "WAITING";

        private bool enableClientFeatures = true;
        private bool enableMod = true;
        private string activeCoopPackageId = string.Empty;
        private string activeCoopPackageFingerprint = string.Empty;
        private int activeCoopPackageMissionCount;
        private string activeCoopPackageDescriptor = string.Empty;
        private ComboBoxItem[] coopPackageOptions = Array.Empty<ComboBoxItem>();
        private string[] coopPackageIds = Array.Empty<string>();
        private string[] disabledTrailModIds = Array.Empty<string>();
        private TrailModSelectionItem[] compatibleTrailMods = Array.Empty<TrailModSelectionItem>();
        private string incompatibleTrailModsText = string.Empty;
        private string coopPackageStatus = string.Empty;

        public CustomCustomTrailSettingsViewModel()
        {
            coopPackageOptions = new[]
            {
                new ComboBoxItem { Content = SerpLocalization.Get("CustomCustomTrail.VanillaPackage") },
            };
            coopPackageIds = new[] { string.Empty };
            OpenCompatibilityGuideCommand = new ActionCommand(OpenCompatibilityGuide);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        protected override void ConfigurePerPlayerLobbySettings(
            Shared.PerPlayerLobbySettingsBuilder settings)
        {
            settings
                .ResetSlotsWith(nameof(CoopPackageStatus), () => null)
                .RequireReport(
                    nameof(CoopPackageStatus),
                    value => !string.IsNullOrEmpty(value as string));
        }

        public event Action<bool> RuntimeActivationChanged;
        public event Action ActiveCoopPackageChanged;

        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableClientFeaturesText => SerpLocalization.Get("CustomCustomTrail.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("CustomCustomTrail.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("CustomCustomTrail.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("CustomCustomTrail.EnableHostFeaturesHelp");
        public string PracticalEffectsText => SerpLocalization.Get("CustomCustomTrail.PracticalEffects");
        public string CoopPackageText => SerpLocalization.Get("CustomCustomTrail.CoopPackage");
        public string CoopPackageHelpText => SerpLocalization.Get("CustomCustomTrail.CoopPackageHelp");
        public string CoopPackageStatusLabel => SerpLocalization.Get("CustomCustomTrail.CoopPackageStatusLabel");
        public string SupportedTrailSettingsTitle => SerpLocalization.Get("CustomCustomTrail.SupportedTrailSettings");
        public string SupportedTrailSettingsHelpText => SerpLocalization.Get("CustomCustomTrail.SupportedTrailSettingsHelp");
        public string IncompatibleTrailModsLabel => SerpLocalization.Get("CustomCustomTrail.IncompatibleTrailMods");
        public string CompatibilityGuideText => SerpLocalization.Get("CustomCustomTrail.CompatibilityGuide");
        public string CompatibilityGuideHelpText => SerpLocalization.Get("CustomCustomTrail.CompatibilityGuideHelp");
        public TrailModSelectionItem[] CompatibleTrailMods => compatibleTrailMods;
        public string IncompatibleTrailModsText => incompatibleTrailModsText;
        public Visibility IncompatibleTrailModsVisibility => string.IsNullOrEmpty(incompatibleTrailModsText)
            ? Visibility.Collapsed
            : Visibility.Visible;
        public ICommand OpenCompatibilityGuideCommand { get; }
        public bool CanEditCoopPackage => CanEditHostSettings && EnableMod;
        public bool IsRuntimeEnabled => EnableClientFeatures && EnableMod;
        public ComboBoxItem[] CoopPackageOptions => coopPackageOptions;

        public string CoopPackageStatusText
        {
            get
            {
                string status = GetLocalStatus();
                if (string.IsNullOrEmpty(ActiveCoopPackageId))
                    return SerpLocalization.Get("CustomCustomTrail.StatusVanilla");
                if (status.StartsWith("OK|", StringComparison.Ordinal))
                    return SerpLocalization.Get("CustomCustomTrail.StatusReady");
                if (string.Equals(status, MissingStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("CustomCustomTrail.ErrorPackageMissing") + " " + ActiveCoopPackageId;
                if (string.Equals(status, MismatchStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("CustomCustomTrail.ErrorFingerprintMismatch");
                if (status.StartsWith(InvalidStatusPrefix, StringComparison.Ordinal))
                    return SerpLocalization.Get("CustomCustomTrail.ErrorPackageInvalid") + " " + status.Substring(InvalidStatusPrefix.Length);
                if (string.Equals(status, DisabledStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled");
                return SerpLocalization.Get("CustomCustomTrail.StatusChecking");
            }
        }

        [Shared.PresetLocal]
        public bool EnableClientFeatures
        {
            get => enableClientFeatures;
            set
            {
                if (!CanMutateSetting(nameof(EnableClientFeatures)) || enableClientFeatures == value)
                    return;
                enableClientFeatures = value;
                OnPropertyChanged(nameof(EnableClientFeatures));
                OnPropertyChanged(nameof(IsRuntimeEnabled));
                RuntimeActivationChanged?.Invoke(IsRuntimeEnabled);
            }
        }

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (!CanMutateSetting(nameof(EnableMod)) || enableMod == value)
                    return;
                enableMod = value;
                OnPropertyChanged(nameof(EnableMod));
                OnPropertyChanged(nameof(CanEditCoopPackage));
                OnPropertyChanged(nameof(IsRuntimeEnabled));
                RuntimeActivationChanged?.Invoke(IsRuntimeEnabled);
            }
        }

        [Shared.PresetLocal]
        public string[] DisabledTrailModIds
        {
            get => disabledTrailModIds;
            set
            {
                string[] normalized = TrailModCompatibilityContract.NormalizeDisabledModIds(
                    value,
                    CustomCustomTrailPlugin.PluginGuid);
                if (disabledTrailModIds.SequenceEqual(normalized, StringComparer.Ordinal))
                    return;
                disabledTrailModIds = normalized;
                OnPropertyChanged(nameof(DisabledTrailModIds));
            }
        }

        [SyncHostOnly]
        public string ActiveCoopPackageId
        {
            get => activeCoopPackageId;
            set
            {
                value = value ?? string.Empty;
                if (!CanMutateSetting(nameof(ActiveCoopPackageId)) || string.Equals(activeCoopPackageId, value, StringComparison.OrdinalIgnoreCase))
                    return;
                activeCoopPackageId = value;
                OnPropertyChanged(nameof(ActiveCoopPackageId));
                OnPropertyChanged(nameof(SelectedCoopPackage));
                ActiveCoopPackageChanged?.Invoke();
            }
        }

        [SyncHostOnly, DoNotPersist]
        public string ActiveCoopPackageFingerprint
        {
            get => activeCoopPackageFingerprint;
            set
            {
                value = value ?? string.Empty;
                if (!CanMutateSetting(nameof(ActiveCoopPackageFingerprint)) || string.Equals(activeCoopPackageFingerprint, value, StringComparison.OrdinalIgnoreCase))
                    return;
                activeCoopPackageFingerprint = value;
                OnPropertyChanged(nameof(ActiveCoopPackageFingerprint));
                ActiveCoopPackageChanged?.Invoke();
            }
        }

        [SyncHostOnly, DoNotPersist]
        public int ActiveCoopPackageMissionCount
        {
            get => activeCoopPackageMissionCount;
            set
            {
                value = Math.Max(0, Math.Min(40, value));
                if (!CanMutateSetting(nameof(ActiveCoopPackageMissionCount)) || activeCoopPackageMissionCount == value)
                    return;
                activeCoopPackageMissionCount = value;
                OnPropertyChanged(nameof(ActiveCoopPackageMissionCount));
                ActiveCoopPackageChanged?.Invoke();
            }
        }

        [SyncHostOnly, DoNotPersist]
        public string ActiveCoopPackageDescriptor
        {
            get => activeCoopPackageDescriptor;
            set
            {
                value = value ?? string.Empty;
                if (!CanMutateSetting(nameof(ActiveCoopPackageDescriptor)) || string.Equals(activeCoopPackageDescriptor, value, StringComparison.Ordinal))
                    return;
                activeCoopPackageDescriptor = value;
                OnPropertyChanged(nameof(ActiveCoopPackageDescriptor));
                ActiveCoopPackageChanged?.Invoke();
            }
        }

        [SyncPerPlayer, DoNotPersist]
        public string CoopPackageStatus
        {
            get => coopPackageStatus;
            set
            {
                value = value ?? string.Empty;
                if (string.Equals(coopPackageStatus, value, StringComparison.Ordinal))
                    return;
                coopPackageStatus = value;
                OnPropertyChanged(nameof(CoopPackageStatus));
                OnPropertyChanged(nameof(CoopPackageStatusText));
            }
        }

        [DoNotPersist]
        public string[] CoopPackageStatusData { get; } = new string[9];

        public ComboBoxItem SelectedCoopPackage
        {
            get
            {
                int index = Array.FindIndex(coopPackageIds, id => string.Equals(id, activeCoopPackageId, StringComparison.OrdinalIgnoreCase));
                return coopPackageOptions[index >= 0 ? index : 0];
            }
            set
            {
                if (value == null)
                    return;
                int index = Array.IndexOf(coopPackageOptions, value);
                if (index >= 0 && index < coopPackageIds.Length)
                    ActiveCoopPackageId = coopPackageIds[index];
            }
        }

        public void RefreshPackages(IEnumerable<CoopTrailPackage> packages)
        {
            var options = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = SerpLocalization.Get("CustomCustomTrail.VanillaPackage") },
            };
            var ids = new List<string> { string.Empty };
            foreach (CoopTrailPackage package in (packages ?? Enumerable.Empty<CoopTrailPackage>())
                .OrderBy(item => item.Manifest.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                options.Add(new ComboBoxItem { Content = package.Manifest.DisplayName + " (" + package.Manifest.MissionCount + ")" });
                ids.Add(package.Manifest.PackageId);
            }
            ComboBoxItem[] refreshedOptions = options.ToArray();
            string[] refreshedIds = ids.ToArray();
            bool unchanged = coopPackageIds.SequenceEqual(refreshedIds, StringComparer.OrdinalIgnoreCase) &&
                coopPackageOptions.Select(option => option.Content?.ToString() ?? string.Empty)
                    .SequenceEqual(refreshedOptions.Select(option => option.Content?.ToString() ?? string.Empty), StringComparer.Ordinal);
            if (unchanged)
            {
                OnPropertyChanged(nameof(SelectedCoopPackage));
                return;
            }

            coopPackageOptions = refreshedOptions;
            coopPackageIds = refreshedIds;
            OnPropertyChanged(nameof(CoopPackageOptions));
            OnPropertyChanged(nameof(SelectedCoopPackage));
        }

        public void SetLocalPackageStatus(string value) => CoopPackageStatus = value;

        internal bool IsTrailModEnabled(string modId) =>
            !disabledTrailModIds.Contains(modId, StringComparer.Ordinal);

        internal void RefreshModCompatibility(IEnumerable<TrailModCompatibilityInfo> entries)
        {
            TrailModCompatibilityInfo[] catalog = (entries ?? Enumerable.Empty<TrailModCompatibilityInfo>()).ToArray();
            compatibleTrailMods = catalog
                .Where(entry => entry.IsCompatible)
                .Select(entry => new TrailModSelectionItem(
                    entry.ModId,
                    entry.DisplayName,
                    IsTrailModEnabled(entry.ModId),
                    SupportedTrailSettingsHelpText,
                    SetTrailModEnabled))
                .ToArray();
            incompatibleTrailModsText = string.Join(", ", catalog
                .Where(entry => !entry.IsCompatible)
                .Select(entry => entry.DisplayName));
            OnPropertyChanged(nameof(CompatibleTrailMods));
            OnPropertyChanged(nameof(IncompatibleTrailModsText));
            OnPropertyChanged(nameof(IncompatibleTrailModsVisibility));
        }

        private void SetTrailModEnabled(string modId, bool value)
        {
            var disabled = new HashSet<string>(disabledTrailModIds, StringComparer.Ordinal);
            if (value)
                disabled.Remove(modId);
            else
                disabled.Add(modId);
            DisabledTrailModIds = disabled.ToArray();
        }

        private static void OpenCompatibilityGuide()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/main/Mod%20Compatibilty%20CustomCustomTrail.md",
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Could not open Custom Custom Trail compatibility guide: " + exception.Message);
            }
        }

        public void RefreshRoleState()
        {
            System_RefreshSettingsAccess();
            OnPropertyChanged(nameof(CanEditCoopPackage));
        }

        private void ResetToDefault()
        {
            EnableClientFeatures = true;
            DisabledTrailModIds = Array.Empty<string>();
            if (CanEditHostSettings)
            {
                EnableMod = true;
                ActiveCoopPackageId = string.Empty;
            }
        }

        private string GetLocalStatus() => coopPackageStatus;
    }

    public sealed class TrailModSelectionItem : INotifyPropertyChanged
    {
        private readonly Action<string, bool> changed;
        private bool isEnabled;

        public TrailModSelectionItem(string modId, string displayName, bool isEnabled, string helpText, Action<string, bool> changed)
        {
            ModId = modId;
            DisplayName = displayName;
            this.isEnabled = isEnabled;
            HelpText = helpText;
            this.changed = changed;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string ModId { get; }
        public string DisplayName { get; }
        public string HelpText { get; }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled == value)
                    return;
                isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                changed?.Invoke(ModId, value);
            }
        }
    }

    internal sealed class ActionCommand : ICommand
    {
        private readonly Action execute;
        public ActionCommand(Action execute) => this.execute = execute;
        public event System.EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute();
    }
}
