using CustomCustomTrail.Core;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomCustomTrail
{
    public sealed class CustomCustomTrailSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private bool enableMod = true;
        private string activeCoopPackageId = string.Empty;
        private string activeCoopPackageFingerprint = string.Empty;
        private int activeCoopPackageMissionCount;
        private ComboBoxItem[] coopPackageOptions = Array.Empty<ComboBoxItem>();
        private string[] coopPackageIds = Array.Empty<string>();

        public CustomCustomTrailSettingsViewModel()
        {
            coopPackageOptions = new[]
            {
                new ComboBoxItem { Content = SerpLocalization.Get("CustomCustomTrail.VanillaPackage") },
            };
            coopPackageIds = new[] { string.Empty };
        }

        public event Action<bool> EnableModChanged;
        public event Action ActiveCoopPackageChanged;

        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string EnableModHelpText => SerpLocalization.Get(SerpLocalization.EnableModHelp);
        public string PracticalEffectsText => SerpLocalization.Get("CustomCustomTrail.PracticalEffects");
        public string HostOptionsText => SerpLocalization.Get("CustomCustomTrail.HostOptions");
        public string CoopPackageText => SerpLocalization.Get("CustomCustomTrail.CoopPackage");
        public string CoopPackageHelpText => SerpLocalization.Get("CustomCustomTrail.CoopPackageHelp");
        public string CoopPackageStatusLabel => SerpLocalization.Get("CustomCustomTrail.CoopPackageStatusLabel");
        public string HostReadOnlyNoticeText => SerpLocalization.Get("CustomCustomTrail.HostReadOnlyNotice");
        public Visibility HostReadOnlyNoticeVisibility => IsHost ? Visibility.Collapsed : Visibility.Visible;
        public bool CanEditCoopPackage => IsHost && EnableMod;
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
                if (status.StartsWith("ERROR|", StringComparison.Ordinal))
                    return status.Substring("ERROR|".Length);
                return SerpLocalization.Get("CustomCustomTrail.StatusChecking");
            }
        }

        // This local safety switch deliberately remains outside host synchronisation.
        [PersistLocal]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (enableMod == value)
                    return;
                enableMod = value;
                OnPropertyChanged(nameof(EnableMod));
                OnPropertyChanged(nameof(CanEditCoopPackage));
                EnableModChanged?.Invoke(value);
            }
        }

        [SyncHostOnly]
        public string ActiveCoopPackageId
        {
            get => activeCoopPackageId;
            set
            {
                value = value ?? string.Empty;
                if (!CanEdit() || string.Equals(activeCoopPackageId, value, StringComparison.OrdinalIgnoreCase))
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
                if (!CanEdit() || string.Equals(activeCoopPackageFingerprint, value, StringComparison.OrdinalIgnoreCase))
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
                if (!CanEdit() || activeCoopPackageMissionCount == value)
                    return;
                activeCoopPackageMissionCount = value;
                OnPropertyChanged(nameof(ActiveCoopPackageMissionCount));
                ActiveCoopPackageChanged?.Invoke();
            }
        }

        [SyncPerPlayer, DoNotPersist]
        public string CoopPackageStatus
        {
            get => GetLocalStatus();
            set
            {
                int playerId = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
                value = value ?? string.Empty;
                if (string.Equals(CoopPackageStatusData[playerId], value, StringComparison.Ordinal))
                    return;
                CoopPackageStatusData[playerId] = value;
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

        public void RefreshRoleState()
        {
            System_RefreshHostState();
            OnPropertyChanged(nameof(CanEditCoopPackage));
            OnPropertyChanged(nameof(HostReadOnlyNoticeVisibility));
        }

        private string GetLocalStatus()
        {
            int playerId = Math.Max(1, GameNetworkAPI.GetLocalPlayerId());
            return CoopPackageStatusData[playerId] ?? string.Empty;
        }
    }
}
