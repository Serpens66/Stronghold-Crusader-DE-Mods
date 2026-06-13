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
    // TODO: Remove the building refund duplicate guard once the Script Extender fires Pre only once and Post again.
    public sealed class SomeSettingsRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, DateTime> recentlyKeptStorageBuildingIds = new Dictionary<int, DateTime>();
        private PendingStockpileRefund pendingStockpileRefund;

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

            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDelete));
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
            pendingStockpileRefund = null;
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

        private unsafe void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                {
                    string reason = args.Phase == EventHookPhase.Post
                        ? "building-not-readable-after-delete"
                        : "building-not-readable";
                    log.LogDebug($"OnBuildingDelete: phase={args.Phase}, buildingId={args.BuildingId}, ignored={reason}.");
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                int total = GetGoodsTotal(goods);
                string goodsSummary = BuildGoodsSummary(goods);

                log.LogDebug($"OnBuildingDelete: phase={args.Phase}, buildingId={args.BuildingId}, owner={building->r_PlayerIdOwner}, type={building->r_BuildingType}, globalId={building->r_GlobalId}, tileX={building->r_TilePositionXBegin}, tileY={building->r_TilePositionYBegin}, total={total}, goods={goodsSummary}.");
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings delete diagnostic hook failed: {ex}");
            }
        }

        private unsafe void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            try
            {
                if (args.Phase != EventHookPhase.Pre)
                    return;

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                {
                    log.LogDebug($"OnBuildingBulldoze: phase={args.Phase}, buildingId={args.BuildingId}, ignored=building-not-found.");
                    return;
                }

                eStructs structure = building->r_BuildingType;
                int owner = building->r_PlayerIdOwner;
                uint globalId = building->r_GlobalId;
                ushort tileX = building->r_TilePositionXBegin;
                ushort tileY = building->r_TilePositionYBegin;

                log.LogDebug($"OnBuildingBulldoze: phase={args.Phase}, buildingId={args.BuildingId}, owner={owner}, type={structure}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");

                if (structure != eStructs.STRUCT_GOODS_YARD)
                {
                    log.LogDebug($"OnBuildingBulldoze ignored non-stockpile buildingId={args.BuildingId}, type={structure}.");
                    return;
                }

                PendingStockpileRefund pending = pendingStockpileRefund;
                if (pending == null)
                {
                    log.LogDebug($"OnBuildingBulldoze stockpile ignored: no pending stockpile refund, buildingId={args.BuildingId}, owner={owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");
                    return;
                }

                if (pending.CreatedAt < DateTime.UtcNow.AddSeconds(-2))
                {
                    log.LogWarning($"Pending stockpile refund expired: refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, partsRemaining={pending.PartsRemaining}.");
                    pendingStockpileRefund = null;
                    return;
                }

                if (owner != pending.Owner)
                {
                    log.LogDebug($"OnBuildingBulldoze stockpile ignored: owner mismatch, buildingId={args.BuildingId}, owner={owner}, pendingOwner={pending.Owner}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");
                    return;
                }

                if (pending.ProcessedBuildingIds.Contains(args.BuildingId))
                {
                    log.LogDebug($"OnBuildingBulldoze stockpile ignored: duplicate processed buildingId={args.BuildingId}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, processedBuildingIds={BuildProcessedBuildingIdSummary(pending.ProcessedBuildingIds)}.");
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(pending.PlayerId, goods);
                int total = GetGoodsTotal(goods);
                string goodsSummary = BuildGoodsSummary(goods);
                pending.ProcessedBuildingIds.Add(args.BuildingId);
                pending.PartsRemaining--;

                log.LogDebug($"OnBuildingBulldoze restored pending stockpile part: buildingId={args.BuildingId}, refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}, total={total}, goods={goodsSummary}, partsRemaining={pending.PartsRemaining}.");

                if (pending.PartsRemaining <= 0)
                {
                    log.LogDebug($"OnBuildingBulldoze pending stockpile refund completed: refundBuildingId={pending.RefundBuildingId}, playerId={pending.PlayerId}, owner={pending.Owner}, processedBuildingIds={BuildProcessedBuildingIdSummary(pending.ProcessedBuildingIds)}.");
                    pendingStockpileRefund = null;
                }
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings bulldoze pending stockpile refund hook failed: {ex}");
            }
        }

        private unsafe void OnBuildingRefund(BuildingRefundEventArgs args)
        {
            try
            {
                log.LogDebug($"OnBuildingRefund: phase={args.Phase}, playerId={args.PlayerId}, buildingId={args.BuildingId}, percentage={args.Percentage}, skipOriginal={args.SkipOriginalFunction}.");

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
                int owner = building->r_PlayerIdOwner;
                uint globalId = building->r_GlobalId;
                ushort tileX = building->r_TilePositionXBegin;
                ushort tileY = building->r_TilePositionYBegin;

                log.LogDebug($"OnBuildingRefund resolved building: buildingId={args.BuildingId}, owner={owner}, type={structure}, globalId={globalId}, tileX={tileX}, tileY={tileY}.");

                if (structure == eStructs.STRUCT_GOODS_YARD)
                {
                    pendingStockpileRefund = new PendingStockpileRefund
                    {
                        PlayerId = args.PlayerId,
                        Owner = owner,
                        RefundBuildingId = args.BuildingId,
                        CreatedAt = DateTime.UtcNow,
                        PartsRemaining = 4
                    };

                    log.LogDebug($"OnBuildingRefund pending stockpile refund created: refundBuildingId={args.BuildingId}, playerId={args.PlayerId}, owner={owner}, globalId={globalId}, tileX={tileX}, tileY={tileY}, partsRemaining=4.");
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(args.PlayerId, goods);
                int total = GetGoodsTotal(goods);
                string goodsSummary = BuildGoodsSummary(goods);

                log.LogDebug($"Kept storage content for refunded {structure} buildingId={args.BuildingId}, playerId={args.PlayerId}, percentage={args.Percentage}, total={total}, goods={goodsSummary}.");
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings refund storage hook failed: {ex}");
            }
        }

        private unsafe static int[] CopyLocalGoods(GameBuilding* building)
        {
            int[] goods = new int[GoodsCount];
            int* localStorage = (int*)&building->r_NullAmount;
            for (int i = 0; i < GoodsCount; i++)
                goods[i] = localStorage[i];

            return goods;
        }

        private static void RestoreGoods(int playerId, int[] goods)
        {
            for (int i = 0; i < GoodsCount; i++)
            {
                int amount = goods[i];
                if (amount <= 0)
                    continue;

                GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, (eGoods)i, amount);
            }
        }

        private static int GetGoodsTotal(int[] goods)
        {
            int total = 0;
            for (int i = 0; i < goods.Length; i++)
            {
                if (goods[i] > 0)
                    total += goods[i];
            }

            return total;
        }

        private static string BuildGoodsSummary(int[] goods)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < goods.Length; i++)
            {
                int amount = goods[i];
                if (amount <= 0)
                    continue;

                parts.Add($"{(eGoods)i}={amount}");
            }

            if (parts.Count == 0)
                return "none";

            return string.Join(", ", parts);
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

        private static string BuildProcessedBuildingIdSummary(HashSet<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return "none";

            List<int> sorted = new List<int>(ids);
            sorted.Sort();
            return string.Join(", ", sorted);
        }

        private sealed class PendingStockpileRefund
        {
            public int PlayerId;
            public int Owner;
            public int RefundBuildingId;
            public DateTime CreatedAt;
            public int PartsRemaining;
            public HashSet<int> ProcessedBuildingIds = new HashSet<int>();
        }
    }
}
