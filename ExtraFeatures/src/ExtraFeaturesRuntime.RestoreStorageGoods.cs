// Feature: Restore goods when a stockpile, armory, or granary is bulldozed.
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private unsafe void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            try
            {
                if (args.Phase != EventHookPhase.Pre ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building) ||
                    building->r_BuildingType != eStructs.STRUCT_GOODS_YARD)
                {
                    return;
                }

                PendingStockpileRefund pending = pendingStockpileRefund;
                if (pending == null || pending.CreatedAt < DateTime.UtcNow.AddSeconds(-2))
                {
                    pendingStockpileRefund = null;
                    return;
                }

                if (building->r_PlayerIdOwner != pending.Owner ||
                    !pending.ProcessedBuildingIds.Add(args.BuildingId))
                {
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(pending.PlayerId, goods);
                pending.PartsRemaining--;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"Extra Features restored stockpile part goods: buildingId={args.BuildingId}, playerId={pending.PlayerId}, goods={BuildGoodsSummary(goods)}, partsRemaining={pending.PartsRemaining}.");

                if (pending.PartsRemaining <= 0)
                    pendingStockpileRefund = null;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features stockpile restore hook failed: {ex}");
            }
        }

        private unsafe void OnBuildingRefund(BuildingRefundEventArgs args)
        {
            try
            {
                NormalizeRefundPercentage(args);
                AddResourceRefundGuards(args);

                if (args.Phase != EventHookPhase.Pre || args.BuildingId <= 0 || !settings.KeepStorageContent ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(args.BuildingId, out GameBuilding* building))
                {
                    return;
                }

                if (building->r_BuildingType == eStructs.STRUCT_GOODS_YARD)
                {
                    pendingStockpileRefund = new PendingStockpileRefund
                    {
                        PlayerId = args.PlayerId,
                        Owner = building->r_PlayerIdOwner,
                        RefundBuildingId = args.BuildingId,
                        CreatedAt = DateTime.UtcNow,
                        PartsRemaining = 4
                    };
                    return;
                }

                int[] goods = CopyLocalGoods(building);
                RestoreGoods(args.PlayerId, goods);
                eStructs structure = building->r_BuildingType;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"Extra Features restored storage goods: type={structure}, buildingId={args.BuildingId}, playerId={args.PlayerId}, goods={BuildGoodsSummary(goods)}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features storage restore hook failed: {ex}");
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
                if (goods[i] > 0)
                    GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, (eGoods)i, goods[i]);
            }
        }

        private static string BuildGoodsSummary(int[] goods)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < goods.Length; i++)
            {
                if (goods[i] > 0)
                    parts.Add($"{(eGoods)i}={goods[i]}");
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
    }
}
