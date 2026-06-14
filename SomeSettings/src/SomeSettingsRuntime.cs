using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using Zhuqiaomon.Memory.Managed;

namespace SomeSettings
{
    // Storage refunds need both OnBuildingRefund and OnBuildingBulldoze:
    // OnBuildingRefund fires once for a stockpile refund, even though the game removes all four stockpile parts at once.
    // OnBuildingBulldoze fires for each of those four parts, but it also fires for buildings destroyed by enemies.
    public sealed class SomeSettingsRuntime : IDisposable
    {
        private static readonly int GoodsCount = (int)eGoods.Count;

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly HashSet<string> resourceAddReentryGuards = new HashSet<string>();
        private readonly Dictionary<string, ResourceEventCountGuard> marketBuyResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private readonly Dictionary<string, ResourceEventCountGuard> refundResourceGuards = new Dictionary<string, ResourceEventCountGuard>();
        private PendingStockpileRefund pendingStockpileRefund;

        private bool hooksSubscribed;
        private bool settingsSubscribed;
        private const int MarketBuyAmount = 5;
        private const int MarketBuyShiftAmount = 25;
        private static readonly TimeSpan MarketBuyGuardLifetime = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RefundGuardLifetime = TimeSpan.FromSeconds(2);

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
            subscriptions.Add(BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(OnBuildingRefund));
            subscriptions.Add(BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(OnGoodsyardAddGood));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(OnPlayerMarketInteraction));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));
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
            ClearResourceEventGuards();
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

                AddResourceRefundGuards(args);

                if (args.Phase != EventHookPhase.Pre || !settings.KeepStorageContent)
                    return;

                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                    return;

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

        private void OnGoodsyardAddGood(AddGoodToGoodsyardEventArgs args)
        {
            int playerId = GameBuildingManagerAPI.Instance.GetOwner(args.BuildingId);
            string key = BuildResourceEventKey(playerId, args.Good);
            bool reentryGuardActive = resourceAddReentryGuards.Contains(key);

            LogDebugForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood:",
                "phase", args.Phase,
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "add", args.Add,
                "buildingId", args.BuildingId,
                "buildingGlobalId", args.BuildingGlobalId,
                "capacity", args.Capacity,
                "reentryGuard", reentryGuardActive);

            if (args.Phase != EventHookPhase.Post)
                return;

            if (!args.Add)
                return;

            if (args.AddAmount <= 0)
                return;

            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return;

            PruneExpiredResourceGuards();

            bool marketGuardActive = marketBuyResourceGuards.TryGetValue(key, out ResourceEventCountGuard marketGuard);
            bool refundGuardActive = refundResourceGuards.TryGetValue(key, out ResourceEventCountGuard refundGuard);

            if (reentryGuardActive)
            {
                LogDebugForResourceEventPlayer(playerId, "OnGoodsyardAddGood ignored own TryAddGood event:", "player", playerId, "good", args.Good, "addAmount", args.AddAmount);
                return;
            }

            if (marketGuardActive)
            {
                marketGuard.RemainingAmount -= args.AddAmount;
                if (marketGuard.RemainingAmount <= 0)
                    marketBuyResourceGuards.Remove(key);

                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored market buy resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingMarketGuardAmount", marketGuard.RemainingAmount);
                return;
            }

            if (refundGuardActive)
            {
                refundGuard.RemainingAmount -= args.AddAmount;
                if (refundGuard.RemainingAmount <= 0)
                    refundResourceGuards.Remove(key);

                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored building refund resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingRefundGuardAmount", refundGuard.RemainingAmount);
                return;
            }

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int multiplyGoods = isAI ? settings.MultiplyGoodsGainAI : settings.MultiplyGoodsGainHuman;
            int multiplyMoney = isAI ? settings.MultiplyGoodsGainInMoneyAI : settings.MultiplyGoodsGainInMoneyHuman;
            LogDebugForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood processing:",
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "marketBuyGuard", marketGuardActive,
                "refundGuard", refundGuardActive,
                "isAI", isAI,
                "multiplyGoods", multiplyGoods,
                "multiplyMoney", multiplyMoney);

            if (multiplyGoods > 1)
            {
                int bonusAmount = args.AddAmount * (multiplyGoods - 1);
                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood TryAddGood bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "sourceAmount", args.AddAmount,
                    "bonusAmount", bonusAmount,
                    "multiplyGoods", multiplyGoods);
                resourceAddReentryGuards.Add(key);
                try
                {
                    GamePlayerManagerAPI.Instance.TryAddGood(playerId, args.Good, bonusAmount);
                }
                finally
                {
                    resourceAddReentryGuards.Remove(key);
                }
            }

            if (multiplyMoney > 0)
            {
                PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetTradeBasePrice(args.Good);
                int sellPricePerItem = price.SellPrice / 5;
                int money = args.AddAmount * sellPricePerItem * multiplyMoney;
                LogDebugForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood money bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "amount", args.AddAmount,
                    "sellPricePerItem", sellPricePerItem,
                    "money", money,
                    "multiplyMoney", multiplyMoney);
                if (money != 0)
                    GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, money);
            }
        }

        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            string key = BuildResourceEventKey(args.PlayerId, args.Good);
            LogDebugForResourceEventPlayer(
                args.PlayerId,
                "OnPlayerMarketInteraction:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "selling", args.Selling,
                "good", args.Good,
                "shiftModifier", args.ShiftModifier,
                "skipOriginal", args.SkipOriginalFunction,
                "key", key);

            if (args.Selling)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                PruneExpiredMarketBuyResourceGuards();
                int expectedAmount = GetMarketInteractionAmount(args);
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = expectedAmount,
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
                LogDebugForResourceEventPlayer(
                    args.PlayerId,
                    "OnPlayerMarketInteraction market buy guard added:",
                    "player", args.PlayerId,
                    "good", args.Good,
                    "key", key,
                    "expectedAmount", expectedAmount);
                return;
            }

            if (args.Phase == EventHookPhase.Post)
            {
                marketBuyResourceGuards.Remove(key);
                LogDebugForResourceEventPlayer(args.PlayerId, "OnPlayerMarketInteraction market buy guard removed on post:", "player", args.PlayerId, "good", args.Good, "key", key);
            }
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            ClearResourceEventGuards();
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

        private static string BuildProcessedBuildingIdSummary(HashSet<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return "none";

            List<int> sorted = new List<int>(ids);
            sorted.Sort();
            return string.Join(", ", sorted);
        }

        private void AddResourceRefundGuards(BuildingRefundEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre)
                return;

            if (args.PlayerId <= 0)
                return;

            PruneExpiredResourceGuards();
            AddBuildingRefundGuards(args);
            LogDebugForResourceEventPlayer(
                args.PlayerId,
                "OnBuildingRefund resource guard added:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "buildingId", args.BuildingId,
                "percentage", args.Percentage,
                "skipOriginal", args.SkipOriginalFunction);
        }

        private static string BuildResourceEventKey(int playerId, eGoods good)
        {
            return playerId + ":" + (int)good;
        }

        private void ClearResourceEventGuards()
        {
            resourceAddReentryGuards.Clear();
            marketBuyResourceGuards.Clear();
            refundResourceGuards.Clear();
        }

        private void PruneExpiredResourceGuards()
        {
            PruneExpiredMarketBuyResourceGuards();
            PruneExpiredRefundResourceGuards();
        }

        private void PruneExpiredMarketBuyResourceGuards()
        {
            if (marketBuyResourceGuards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in marketBuyResourceGuards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                marketBuyResourceGuards.Remove(expiredKeys[i]);
        }

        private void PruneExpiredRefundResourceGuards()
        {
            PruneExpiredCountGuardKeys(refundResourceGuards);
        }

        private static void PruneExpiredCountGuardKeys(Dictionary<string, ResourceEventCountGuard> guards)
        {
            if (guards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in guards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                guards.Remove(expiredKeys[i]);
        }

        private static int GetMarketInteractionAmount(PlayerMarketInteractionEventArgs args)
        {
            return args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount;
        }

        private void AddBuildingRefundGuards(BuildingRefundEventArgs args)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(args.PlayerId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            eStructs buildingType = buildingApi.GetType(args.BuildingId);
            DateTime expiresAt = DateTime.UtcNow + RefundGuardLifetime;

            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_WOOD_PLANKS, GetRefundAmount(buildingApi.GetWoodCost(buildingType), buildingApi.WoodRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_STONE_BLOCKS, GetRefundAmount(buildingApi.GetStoneCost(buildingType), buildingApi.StoneRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_IRON_INGOTS, GetRefundAmount(buildingApi.GetIronIngotCost(buildingType), buildingApi.IronRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_PITCH_RAW, GetRefundAmount(buildingApi.GetRawPitchCost(buildingType), buildingApi.PitchRefundMultiplier), expiresAt);
        }

        private void AddBuildingRefundGuard(int playerId, eGoods good, int amount, DateTime expiresAt)
        {
            if (amount <= 0)
                return;

            string key = BuildResourceEventKey(playerId, good);
            refundResourceGuards[key] = new ResourceEventCountGuard
            {
                RemainingAmount = amount,
                ExpiresAt = expiresAt
            };

            LogDebugForResourceEventPlayer(
                playerId,
                "OnBuildingRefund resource good guard added:",
                "player", playerId,
                "good", good,
                "expectedAmount", amount,
                "key", key);
        }

        private static int GetRefundAmount(int cost, float refundMultiplier)
        {
            if (cost <= 0 || refundMultiplier <= 0)
                return 0;

            return (int)(cost * refundMultiplier);
        }

        private void LogDebugForResourceEventPlayer(int playerId, params object[] parts)
        {
            if (ShouldLogResourceEventPlayer(playerId))
                log.LogDebug(string.Join(" ", parts));
        }

        private static bool ShouldLogResourceEventPlayer(int playerId)
        {
            try
            {
                return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            }
            catch
            {
                return false;
            }
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

        private sealed class ResourceEventCountGuard
        {
            public int RemainingAmount { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
