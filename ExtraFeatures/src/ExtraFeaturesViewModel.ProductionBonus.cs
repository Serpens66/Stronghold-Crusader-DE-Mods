using APIShared.ModSettings;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using System;
using System.Globalization;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel
    {
        private int fixedProductionBonusPercent = 50;
        private volatile bool fixedProductionBonusMissionLocked;
        private string workerProductionBonusCompatibilityKey;
        private int workerProductionBonusExpectedReports;

        public bool CanConfigureFixedProductionBonus => CanEditHostSettings && !fixedProductionBonusMissionLocked;
        public string FixedProductionBonusTitle => SerpLocalization.Get("ExtraFeatures.ProductionBonus.Title");
        public string FixedProductionBonusHelp => SerpLocalization.Get("ExtraFeatures.ProductionBonus.Help");

        [SyncHostOnly]
        public int FixedProductionBonusPercent
        {
            get => fixedProductionBonusPercent;
            set
            {
                if (!fixedProductionBonusMissionLocked || IsApplyingMissionSettingsSnapshot)
                    SetIntSetting(ref fixedProductionBonusPercent, value, -100, 1000,
                        nameof(FixedProductionBonusPercent), nameof(FixedProductionBonusValueText));
            }
        }

        public string FixedProductionBonusValueText
        {
            get => FixedProductionBonusPercent.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    FixedProductionBonusPercent = parsed;
                OnPropertyChanged(nameof(FixedProductionBonusValueText));
            }
        }

        // Runtime admission data: never part of personal presets or savegame settings.
        // null means no report yet. The versioned report separates shared native
        // history protection from this consumer's ability to publish an override.
        [SyncPerPlayer, DoNotPersist]
        public string WorkerProductionBonusCompatibilityKey
        {
            get => workerProductionBonusCompatibilityKey;
            set
            {
                if (string.Equals(workerProductionBonusCompatibilityKey, value, StringComparison.Ordinal)) return;
                workerProductionBonusCompatibilityKey = value;
                OnPropertyChanged(nameof(WorkerProductionBonusCompatibilityKey));
                WorkerProductionBonusPeerReportsChanged?.Invoke();
            }
        }

        [DoNotPersist]
        public string[] WorkerProductionBonusCompatibilityKeyData { get; } = new string[9];

        internal event Action WorkerProductionBonusPeerReportsChanged;

        protected override void ConfigurePerPlayerLobbySettings(PerPlayerLobbySettingsBuilder options)
        {
            options.ResetSlotsWith(nameof(WorkerProductionBonusCompatibilityKey), () => null)
                .RequireReport(nameof(WorkerProductionBonusCompatibilityKey), value => value != null)
                .WhenLobbyChanged(snapshot =>
                {
                    // The shared coordinator owns identity, roster validation and map
                    // transition preservation. A known solo MP-save needs one report.
                    workerProductionBonusExpectedReports = snapshot.LobbyId.HasValue &&
                        !snapshot.HasUnresolvedPlayers && snapshot.LocalPlayerId > 0
                        ? snapshot.Players.Count : 0;
                    WorkerProductionBonusPeerReportsChanged?.Invoke();
                })
                .WhenRemoteDataChanged(property =>
                {
                    if (property == nameof(WorkerProductionBonusCompatibilityKeyData))
                        WorkerProductionBonusPeerReportsChanged?.Invoke();
                });
        }

        // Uses only the coordinator's authenticated, roster-bound companion slots.
        // It must not infer readiness from a local service or an old lobby report alone.
        private const string ProductionBonusReportPrefix = "worker-production-peer/v1/";

        internal static string CreateWorkerProductionBonusReport(string nativeKey, bool canPublish) =>
            ProductionBonusReportPrefix + (canPublish ? "1/" : "0/") + (nativeKey ?? string.Empty);

        private static bool TryReadWorkerProductionBonusReport(string report, out string nativeKey, out bool canPublish)
        {
            nativeKey = null; canPublish = false;
            if (report == null || !report.StartsWith(ProductionBonusReportPrefix, StringComparison.Ordinal) ||
                report.Length < ProductionBonusReportPrefix.Length + 2) return false;
            int offset = ProductionBonusReportPrefix.Length;
            if ((report[offset] != '0' && report[offset] != '1') || report[offset + 1] != '/') return false;
            canPublish = report[offset] == '1';
            nativeKey = report.Substring(offset + 2);
            return !canPublish || nativeKey.Length != 0;
        }

        internal bool TryGetWorkerProductionBonusPeerReadiness(out string reason, bool requirePublishing = true)
        {
            if (!IsPerPlayerLobbySettingsReady)
            {
                reason = PerPlayerLobbySettingsReadinessError;
                return false;
            }
            if (workerProductionBonusExpectedReports < 1)
            {
                reason = SerpLocalization.Get("ExtraFeatures.ProductionBonus.PeerWaiting");
                return false;
            }
            if (!TryReadWorkerProductionBonusReport(WorkerProductionBonusCompatibilityKey,
                out string expectedNative, out bool localCanPublish) ||
                (requirePublishing && (!localCanPublish || expectedNative.Length == 0)))
            {
                reason = SerpLocalization.Get("ExtraFeatures.ProductionBonus.Unavailable");
                return false;
            }
            int reported = 0;
            for (int id = 1; id < WorkerProductionBonusCompatibilityKeyData.Length; id++)
            {
                string value = WorkerProductionBonusCompatibilityKeyData[id];
                if (value == null) continue;
                reported++;
                if (!TryReadWorkerProductionBonusReport(value, out string nativeKey, out bool canPublish) ||
                    !string.Equals(nativeKey, expectedNative, StringComparison.Ordinal) ||
                    (requirePublishing && !canPublish))
                {
                    reason = SerpLocalization.Get("ExtraFeatures.ProductionBonus.PeerMismatch");
                    return false;
                }
            }
            if (reported < workerProductionBonusExpectedReports)
            {
                reason = SerpLocalization.Get("ExtraFeatures.ProductionBonus.PeerWaiting");
                return false;
            }
            reason = string.Empty;
            return true;
        }

        internal void SetFixedProductionBonusMissionLocked(bool locked)
        {
            if (fixedProductionBonusMissionLocked == locked) return;
            fixedProductionBonusMissionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread)
                OnPropertyChanged(nameof(CanConfigureFixedProductionBonus));
            else if (uiContext != null)
                uiContext.Post(_ => OnPropertyChanged(nameof(CanConfigureFixedProductionBonus)), null);
        }
    }
}
