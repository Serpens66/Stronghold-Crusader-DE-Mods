using BepInEx.Logging;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace SpawnCastle
{
    public sealed class SpawnCastleSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        public const string DisabledOption = "disabled";

        private readonly ManualLogSource log;
        private readonly AivFileCatalog catalog = new AivFileCatalog();
        private string selectedCastle = DisabledOption;
        private bool storageNeedsRewrite;

        public SpawnCastleSettingsViewModel(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            CastleOptions.Add(DisabledOption);
            foreach (string option in catalog.Discover())
                CastleOptions.Add(option);
        }

        public ObservableCollection<string> CastleOptions { get; } =
            new ObservableCollection<string>();

        public int AvailableFileCount => CastleOptions.Count - 1;

        public string InventoryText =>
            $"{AvailableFileCount} AIVJSON files found. Selection applies to new games only.";

        // The Script Extender persists attributed properties in LobbyModSettings.
        [SyncHostOnly]
        public string SelectedCastle
        {
            get => selectedCastle;
            set
            {
                string candidate = string.IsNullOrWhiteSpace(value)
                    ? DisabledOption
                    : value.Trim();
                string normalized = TryGetCanonicalOption(
                    candidate,
                    out string canonical)
                    ? canonical
                    : DisabledOption;
                if (normalized == DisabledOption &&
                    !string.Equals(
                        candidate,
                        DisabledOption,
                        StringComparison.OrdinalIgnoreCase))
                {
                    storageNeedsRewrite = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Stored or selected AIVJSON is no longer available: " +
                        $"'{candidate}'. Falling back to '{DisabledOption}'.");
                }

                if (selectedCastle == normalized)
                    return;

                selectedCastle = normalized;
                OnPropertyChanged(nameof(SelectedCastle));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"SpawnCastle selection applied: '{selectedCastle}'.");
            }
        }

        public bool IsDisabled =>
            string.Equals(selectedCastle, DisabledOption, System.StringComparison.OrdinalIgnoreCase);

        internal bool TryResolveSelectedFile(out string fullPath)
        {
            return catalog.TryResolve(selectedCastle, out fullPath);
        }

        internal void RewriteInvalidPersistedSelectionIfNeeded()
        {
            if (!storageNeedsRewrite)
                return;

            storageNeedsRewrite = false;
            // Registration has attached the Script Extender storage handler now.
            OnPropertyChanged(nameof(SelectedCastle));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Normalized SpawnCastle selection persisted as '{selectedCastle}'.");
        }

        private bool TryGetCanonicalOption(
            string value,
            out string canonical)
        {
            foreach (string option in CastleOptions)
            {
                if (!string.Equals(
                        option,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                canonical = option;
                return true;
            }

            canonical = null;
            return false;
        }
    }
}
