using ExtendedData.Core;
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
using System.Text;
using System.Windows.Input;

namespace ExtendedData
{
    public sealed class ExtendedDataSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
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
        private string[] playerTrailPropertyIds = Array.Empty<string>();
        private string[] fixedTrailPropertyIds = Array.Empty<string>();
        private TrailModSelectionItem[] compatibleTrailMods = Array.Empty<TrailModSelectionItem>();
        private string incompatibleTrailModsText = string.Empty;
        private string coopPackageStatus = string.Empty;

        public ExtendedDataSettingsViewModel()
        {
            coopPackageOptions = new[]
            {
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.VanillaPackage") },
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
        public string EnableClientFeaturesText => SerpLocalization.Get("ExtendedData.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("ExtendedData.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("ExtendedData.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("ExtendedData.EnableHostFeaturesHelp");
        public string PracticalEffectsText => SerpLocalization.Get("ExtendedData.PracticalEffects");
        public string CoopPackageText => SerpLocalization.Get("ExtendedData.CoopPackage");
        public string CoopPackageHelpText => SerpLocalization.Get("ExtendedData.CoopPackageHelp");
        public string CoopPackageStatusLabel => SerpLocalization.Get("ExtendedData.CoopPackageStatusLabel");
        public string SupportedTrailSettingsTitle => SerpLocalization.Get("ExtendedData.SupportedTrailSettings");
        public string SupportedTrailSettingsHelpText => SerpLocalization.Get("ExtendedData.SupportedTrailSettingsHelp");
        public string TrailSettingModeHelpText => SerpLocalization.Get("ExtendedData.TrailSettingModeHelp");
        public string IncompatibleTrailModsLabel => SerpLocalization.Get("ExtendedData.IncompatibleTrailMods");
        public string CompatibilityGuideText => SerpLocalization.Get("ExtendedData.CompatibilityGuide");
        public string CompatibilityGuideHelpText => SerpLocalization.Get("ExtendedData.CompatibilityGuideHelp");
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
                    return SerpLocalization.Get("ExtendedData.StatusVanilla");
                if (status.StartsWith("OK|", StringComparison.Ordinal))
                    return SerpLocalization.Get("ExtendedData.StatusReady");
                if (string.Equals(status, MissingStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("ExtendedData.ErrorPackageMissing") + " " + ActiveCoopPackageId;
                if (string.Equals(status, MismatchStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("ExtendedData.ErrorFingerprintMismatch");
                if (status.StartsWith(InvalidStatusPrefix, StringComparison.Ordinal))
                    return SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + status.Substring(InvalidStatusPrefix.Length);
                if (string.Equals(status, DisabledStatus, StringComparison.Ordinal))
                    return SerpLocalization.Get("ExtendedData.ErrorModDisabled");
                return SerpLocalization.Get("ExtendedData.StatusChecking");
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
        public string[] PlayerTrailPropertyIds
        {
            get => playerTrailPropertyIds;
            set
            {
                string[] normalized = NormalizePropertyIds(value)
                    .Where(id => !fixedTrailPropertyIds.Contains(id, StringComparer.Ordinal))
                    .ToArray();
                SetTrailPropertyModeIds(normalized, fixedTrailPropertyIds);
            }
        }

        [Shared.PresetLocal]
        public string[] FixedTrailPropertyIds
        {
            get => fixedTrailPropertyIds;
            set
            {
                string[] normalized = NormalizePropertyIds(value);
                SetTrailPropertyModeIds(
                    playerTrailPropertyIds.Where(id => !normalized.Contains(id, StringComparer.Ordinal)).ToArray(),
                    normalized);
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
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.VanillaPackage") },
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

        internal TrailSettingMode GetTrailPropertyMode(string modId, string propertyName)
        {
            string id = BuildPropertyId(modId, propertyName);
            if (fixedTrailPropertyIds.Contains(id, StringComparer.Ordinal))
                return TrailSettingMode.Fixed;
            if (playerTrailPropertyIds.Contains(id, StringComparer.Ordinal))
                return TrailSettingMode.Player;
            return TrailSettingMode.ModDefault;
        }

        internal void ApplyTrailSettingModes(ModSettingsDefinition document)
        {
            var player = new List<string>();
            var fixedValues = new List<string>();
            foreach (KeyValuePair<string, ModSettingsEntry> mod in
                document?.Mods ?? new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal))
            {
                ModSettingsEntry entry = mod.Value;
                if (entry == null)
                    continue;
                player.AddRange((entry.PlayerSettings ?? Array.Empty<string>())
                    .Select(propertyName => BuildPropertyId(mod.Key, propertyName)));
                fixedValues.AddRange((entry.Overrides == null
                        ? Enumerable.Empty<string>()
                        : entry.Overrides.Keys)
                    .Select(propertyName => BuildPropertyId(mod.Key, propertyName)));
            }
            SetTrailPropertyModeIds(NormalizePropertyIds(player), NormalizePropertyIds(fixedValues));
        }

        internal void ApplyTrailSettingModesForMod(string modId, ModSettingsDefinition document)
        {
            if (string.IsNullOrWhiteSpace(modId)) return;
            string prefix = modId + ".";
            var player = playerTrailPropertyIds.Where(id => !id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            var fixedValues = fixedTrailPropertyIds.Where(id => !id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (document?.Mods != null && document.Mods.TryGetValue(modId, out ModSettingsEntry entry) && entry != null)
            {
                player.AddRange((entry.PlayerSettings ?? Array.Empty<string>()).Select(name => BuildPropertyId(modId, name)));
                fixedValues.AddRange((entry.Overrides ?? new Dictionary<string, object>(StringComparer.Ordinal)).Keys.Select(name => BuildPropertyId(modId, name)));
            }
            SetTrailPropertyModeIds(NormalizePropertyIds(player), NormalizePropertyIds(fixedValues));
        }

        internal void RefreshModCompatibility(IEnumerable<TrailModCompatibilityInfo> entries)
        {
            TrailModCompatibilityInfo[] catalog = (entries ?? Enumerable.Empty<TrailModCompatibilityInfo>()).ToArray();
            compatibleTrailMods = catalog
                .Where(entry => entry.IsCompatible)
                .Select(entry => new TrailModSelectionItem(
                    entry.ModId,
                    entry.DisplayName,
                    BuildSettingGroups(entry),
                    GetTrailPropertyMode,
                    SetTrailPropertiesMode,
                    TrailSettingModeHelpText))
                .ToArray();
            incompatibleTrailModsText = string.Join(", ", catalog
                .Where(entry => !entry.IsCompatible)
                .Select(entry => entry.DisplayName));
            OnPropertyChanged(nameof(CompatibleTrailMods));
            OnPropertyChanged(nameof(IncompatibleTrailModsText));
            OnPropertyChanged(nameof(IncompatibleTrailModsVisibility));
        }

        private void SetTrailPropertiesMode(
            string modId,
            IEnumerable<string> propertyNames,
            TrailSettingMode mode)
        {
            var player = new HashSet<string>(playerTrailPropertyIds, StringComparer.Ordinal);
            var fixedValues = new HashSet<string>(fixedTrailPropertyIds, StringComparer.Ordinal);
            foreach (string propertyName in propertyNames ?? Enumerable.Empty<string>())
            {
                string id = BuildPropertyId(modId, propertyName);
                player.Remove(id);
                fixedValues.Remove(id);
                if (mode == TrailSettingMode.Player)
                    player.Add(id);
                else if (mode == TrailSettingMode.Fixed)
                    fixedValues.Add(id);
            }
            SetTrailPropertyModeIds(NormalizePropertyIds(player), NormalizePropertyIds(fixedValues));
        }

        private void SetTrailPropertyModeIds(string[] player, string[] fixedValues)
        {
            player = NormalizePropertyIds(player);
            fixedValues = NormalizePropertyIds(fixedValues);
            player = player.Where(id => !fixedValues.Contains(id, StringComparer.Ordinal)).ToArray();
            bool playerChanged = !playerTrailPropertyIds.SequenceEqual(player, StringComparer.Ordinal);
            bool fixedChanged = !fixedTrailPropertyIds.SequenceEqual(fixedValues, StringComparer.Ordinal);
            if (!playerChanged && !fixedChanged)
                return;
            playerTrailPropertyIds = player;
            fixedTrailPropertyIds = fixedValues;
            if (playerChanged)
                OnPropertyChanged(nameof(PlayerTrailPropertyIds));
            if (fixedChanged)
                OnPropertyChanged(nameof(FixedTrailPropertyIds));
            foreach (TrailModSelectionItem item in compatibleTrailMods)
                item.RefreshState();
        }

        private static string[] NormalizePropertyIds(IEnumerable<string> values) =>
            (values ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

        private static TrailSettingGroupDefinition[] BuildSettingGroups(TrailModCompatibilityInfo entry)
        {
            var remaining = new HashSet<string>(entry.Properties.Select(property => property.Name), StringComparer.Ordinal);
            var groups = new List<TrailSettingGroupDefinition>();
            AddKnownGroup(
                groups,
                remaining,
                entry.ModId,
                "UnitCosts_Serp",
                "ExtendedData.Group.UnitCosts",
                "UnitCosts",
                "HumanExtraUnitCosts");
            AddKnownGroup(
                groups,
                remaining,
                entry.ModId,
                "ExtraFeatures_Serp",
                "ExtendedData.Group.MarketGoodMultipliers",
                "MarketGoodBuyPriceMultipliers",
                "MarketGoodSellPriceMultipliers");

            foreach (string propertyName in remaining.OrderBy(PropertyOrder).ThenBy(name => name, StringComparer.Ordinal))
            {
                groups.Add(new TrailSettingGroupDefinition(
                    propertyName,
                    HumanizePropertyName(propertyName),
                    new[] { propertyName }));
            }
            return groups
                .OrderBy(group => group.PropertyNames.Contains("EnableMod", StringComparer.Ordinal) ? 0 : 1)
                .ThenBy(group => group.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static void AddKnownGroup(
            ICollection<TrailSettingGroupDefinition> groups,
            ISet<string> remaining,
            string actualModId,
            string expectedModId,
            string localizationKey,
            params string[] propertyNames)
        {
            if (!string.Equals(actualModId, expectedModId, StringComparison.Ordinal) ||
                propertyNames.Any(propertyName => !remaining.Contains(propertyName)))
            {
                return;
            }
            groups.Add(new TrailSettingGroupDefinition(
                localizationKey,
                SerpLocalization.Get(localizationKey),
                propertyNames));
            foreach (string propertyName in propertyNames)
                remaining.Remove(propertyName);
        }

        private static int PropertyOrder(string propertyName) =>
            string.Equals(propertyName, "EnableMod", StringComparison.Ordinal) ? 0 : 1;

        private static string BuildPropertyId(string modId, string propertyName) =>
            (modId ?? string.Empty) + "\u001f" + (propertyName ?? string.Empty);

        private static string HumanizePropertyName(string value)
        {
            if (string.Equals(value, "EnableMod", StringComparison.Ordinal))
                return SerpLocalization.Get("ExtendedData.Group.EnableMod");
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var result = new StringBuilder(value.Length + 8);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (index > 0 && char.IsUpper(current) &&
                    (char.IsLower(value[index - 1]) ||
                    (index + 1 < value.Length && char.IsLower(value[index + 1]))))
                {
                    result.Append(' ');
                }
                result.Append(current);
            }
            return result.ToString();
        }

        private static void OpenCompatibilityGuide()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main/Guides/ExtendedData",
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Could not open Extended Data compatibility guide: " + exception.Message);
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
            SetTrailPropertyModeIds(Array.Empty<string>(), Array.Empty<string>());
            if (CanEditHostSettings)
            {
                EnableMod = true;
                ActiveCoopPackageId = string.Empty;
            }
        }

        private string GetLocalStatus() => coopPackageStatus;
    }

    internal sealed class TrailSettingGroupDefinition
    {
        internal TrailSettingGroupDefinition(string key, string displayName, string[] propertyNames)
        {
            Key = key;
            DisplayName = displayName;
            PropertyNames = propertyNames ?? Array.Empty<string>();
        }

        internal string Key { get; }
        internal string DisplayName { get; }
        internal string[] PropertyNames { get; }
    }

    public sealed class TrailModSelectionItem : INotifyPropertyChanged
    {
        private readonly Action<string, IEnumerable<string>, TrailSettingMode> changed;
        private bool isExpanded;

        internal TrailModSelectionItem(
            string modId,
            string displayName,
            IEnumerable<TrailSettingGroupDefinition> definitions,
            Func<string, string, TrailSettingMode> getMode,
            Action<string, IEnumerable<string>, TrailSettingMode> changed,
            string helpText)
        {
            ModId = modId;
            DisplayName = displayName;
            HelpText = helpText;
            this.changed = changed;
            ModeOptions = CreateModeOptions(includeMixed: true);
            Settings = (definitions ?? Enumerable.Empty<TrailSettingGroupDefinition>())
                .Select(definition => new TrailSettingSelectionItem(
                    modId,
                    definition.Key,
                    definition.DisplayName,
                    definition.PropertyNames,
                    getMode,
                    changed,
                    OnSettingChanged,
                    helpText))
                .ToArray();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string ModId { get; }
        public string DisplayName { get; }
        public string HelpText { get; }
        public ComboBoxItem[] ModeOptions { get; }
        public TrailSettingSelectionItem[] Settings { get; }
        public string SearchText => string.Join(" ",
            new[] { DisplayName }.Concat(Settings.Select(item => item.DisplayName)));
        public string SummaryText => string.Format(
            SerpLocalization.Get("ExtendedData.TrailSettingModeSummary"),
            Settings.Count(item => item.SelectedModeIndex == (int)TrailSettingMode.ModDefault),
            Settings.Count(item => item.SelectedModeIndex == (int)TrailSettingMode.Player),
            Settings.Count(item => item.SelectedModeIndex == (int)TrailSettingMode.Fixed));

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded == value)
                    return;
                isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public int SelectedModeIndex
        {
            get
            {
                int[] modes = Settings.Select(item => item.SelectedModeIndex).Distinct().ToArray();
                return modes.Length == 1 && modes[0] <= (int)TrailSettingMode.Fixed
                    ? modes[0]
                    : 3;
            }
            set
            {
                if (value < 0 || value > (int)TrailSettingMode.Fixed)
                    return;
                changed?.Invoke(
                    ModId,
                    Settings.SelectMany(item => item.PropertyNames),
                    (TrailSettingMode)value);
            }
        }

        internal void RefreshState()
        {
            foreach (TrailSettingSelectionItem setting in Settings)
                setting.RefreshState();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SummaryText)));
        }

        private void OnSettingChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SummaryText)));
        }

        internal static ComboBoxItem[] CreateModeOptions(bool includeMixed)
        {
            var options = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.ModDefault") },
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.Player") },
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.Fixed") },
            };
            if (includeMixed)
            {
                options.Add(new ComboBoxItem
                {
                    Content = SerpLocalization.Get("ExtendedData.Mode.Mixed"),
                    IsEnabled = false,
                });
            }
            return options.ToArray();
        }
    }

    public sealed class TrailSettingSelectionItem : INotifyPropertyChanged
    {
        private readonly string modId;
        private readonly Func<string, string, TrailSettingMode> getMode;
        private readonly Action<string, IEnumerable<string>, TrailSettingMode> changed;
        private readonly Action parentChanged;

        internal TrailSettingSelectionItem(
            string modId,
            string key,
            string displayName,
            string[] propertyNames,
            Func<string, string, TrailSettingMode> getMode,
            Action<string, IEnumerable<string>, TrailSettingMode> changed,
            Action parentChanged,
            string helpText)
        {
            this.modId = modId;
            this.getMode = getMode;
            this.changed = changed;
            this.parentChanged = parentChanged;
            Key = key;
            DisplayName = displayName;
            PropertyNames = propertyNames ?? Array.Empty<string>();
            HelpText = helpText;
            ModeOptions = TrailModSelectionItem.CreateModeOptions(includeMixed: true);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string Key { get; }
        public string DisplayName { get; }
        public string HelpText { get; }
        public string[] PropertyNames { get; }
        public ComboBoxItem[] ModeOptions { get; }

        public int SelectedModeIndex
        {
            get
            {
                TrailSettingMode[] modes = PropertyNames
                    .Select(propertyName => getMode(modId, propertyName))
                    .Distinct()
                    .ToArray();
                return modes.Length == 1 ? (int)modes[0] : 3;
            }
            set
            {
                if (value < 0 || value > (int)TrailSettingMode.Fixed)
                    return;
                changed?.Invoke(modId, PropertyNames, (TrailSettingMode)value);
                parentChanged?.Invoke();
            }
        }

        internal void RefreshState() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
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
