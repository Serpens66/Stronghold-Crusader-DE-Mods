using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Memory.Managed;

namespace SomeSettings
{
    public sealed class SomeSettingsRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, DateTime> recentlyKeptStorageBuildingIds = new Dictionary<int, DateTime>();

        private bool hooksSubscribed;
        private bool settingsSubscribed;

        public SomeSettingsRuntime(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SubscribeSettingsChanges();
        }

        public void SubscribeHooks()
        {
            if (hooksSubscribed)
                return;

            subscriptions.Add(BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(OnBuildingRefund));
            hooksSubscribed = true;
            log.LogDebug("SomeSettings hooks subscribed.");
        }

        public void ApplySettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;

            ApplyRefundPercent(buildingApi.WoodRefundMultiplier, settings.WoodRefundPercent, "wood");
            ApplyRefundPercent(buildingApi.StoneRefundMultiplier, settings.StoneRefundPercent, "stone");
            ApplyRefundPercent(buildingApi.IronRefundMultiplier, settings.IronRefundPercent, "iron");
            ApplyRefundPercent(buildingApi.PitchRefundMultiplier, settings.PitchRefundPercent, "pitch");
            ApplyRefundPercent(buildingApi.GoldRefundMultiplier, settings.GoldRefundPercent, "gold");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            recentlyKeptStorageBuildingIds.Clear();
            hooksSubscribed = false;

            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void SubscribeSettingsChanges()
        {
            if (settingsSubscribed)
                return;

            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(SomeSettingsViewModel.KeepStorageContent))
            {
                log.LogDebug($"SomeSettings changed: KeepStorageContent={settings.KeepStorageContent}.");
                return;
            }

            ApplySettings();
        }

        private static void ApplyRefundPercent(ManagedValue<float> refundMultiplier, int percent, string label)
        {
            if (percent < 0)
                return;

            refundMultiplier.SetValue(percent / 100f);
        }

        private unsafe void OnBuildingRefund(BuildingRefundEventArgs args)
        {
            try
            {
                if (args.Phase != EventHookPhase.Pre || !settings.KeepStorageContent)
                    return;

                PruneRecentlyKeptStorageIds();
                if (recentlyKeptStorageBuildingIds.ContainsKey(args.BuildingId))
                {
                    log.LogDebug($"Skipped duplicate keep-storage refund event for buildingId={args.BuildingId}, playerId={args.PlayerId}, percentage={args.Percentage}.");
                    return;
                }

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                    return;

                recentlyKeptStorageBuildingIds[args.BuildingId] = DateTime.UtcNow;
                eStructs structure = building->r_BuildingType;
                int total = 0;
                List<string> parts = new List<string>();
                int* localStorage = (int*)&building->r_NullAmount;

                for (int i = 0; i < GoodsCount; i++)
                {
                    int amount = localStorage[i];
                    if (amount <= 0)
                        continue;

                    eGoods good = (eGoods)i;
                    GamePlayerManagerAPI.Instance.AddIncomingGood(args.PlayerId, good, amount);
                    total += amount;
                    parts.Add($"{good}={amount}");
                }

                if (total > 0)
                {
                    log.LogDebug($"Kept storage content for refunded {structure} buildingId={args.BuildingId}, playerId={args.PlayerId}, percentage={args.Percentage}, total={total}, goods={string.Join(", ", parts)}.");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings refund storage hook failed: {ex}");
            }
        }

        private void PruneRecentlyKeptStorageIds()
        {
            if (recentlyKeptStorageBuildingIds.Count == 0)
                return;

            DateTime cutoff = DateTime.UtcNow.AddSeconds(-2);
            List<int> expired = null;
            foreach (KeyValuePair<int, DateTime> entry in recentlyKeptStorageBuildingIds)
            {
                if (entry.Value < cutoff)
                {
                    if (expired == null)
                        expired = new List<int>();

                    expired.Add(entry.Key);
                }
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                recentlyKeptStorageBuildingIds.Remove(expired[i]);
        }
    }
}
