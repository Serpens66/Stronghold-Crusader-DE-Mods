// Feature: Configure custom building-demolition refund percentages.
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private void ApplyRefundSettings()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            ApplyRefundPercent(buildingApi.WoodRefundMultiplier, settings.WoodRefundPercent);
            ApplyRefundPercent(buildingApi.StoneRefundMultiplier, settings.StoneRefundPercent);
            ApplyRefundPercent(buildingApi.IronRefundMultiplier, settings.IronRefundPercent);
            ApplyRefundPercent(buildingApi.PitchRefundMultiplier, settings.PitchRefundPercent);
            ApplyRefundPercent(buildingApi.GoldRefundMultiplier, settings.GoldRefundPercent);
        }

        private static void ApplyRefundPercent(RedBird.Core.Memory.Managed.ManagedValue<float> refundMultiplier, int percent)
        {
            refundMultiplier.SetValue(percent < 0 ? VanillaRefundMultiplier : percent / 100f);
        }

        private void NormalizeRefundPercentage(BuildingRefundEventArgs args)
        {
            // Negative building IDs are native refund sentinels such as walls, where the
            // fourth argument is not a percentage. The Script Extender currently exposes no
            // supported wall-refund adjustment path, so these contexts must remain Vanilla.
            if (args.Phase != EventHookPhase.Pre || args.BuildingId <= 0 || !HasCustomRefundPercent())
                return;

            // Custom multipliers already express the final percentage, so the event contributes 100%.
            args.Percentage = 100;
        }

        private bool HasCustomRefundPercent()
        {
            return settings.WoodRefundPercent >= 0 ||
                settings.StoneRefundPercent >= 0 ||
                settings.IronRefundPercent >= 0 ||
                settings.PitchRefundPercent >= 0 ||
                settings.GoldRefundPercent >= 0;
        }

        private void AddResourceRefundGuards(BuildingRefundEventArgs args)
        {
            // The Script Extender documents -1 as a possible refund-context sentinel.
            // It cannot be resolved through GameBuildingManagerAPI and needs no resource guard.
            if (args.Phase != EventHookPhase.Pre || args.PlayerId <= 0 || args.BuildingId <= 0)
                return;

            PruneExpiredResourceGuards();
            AddBuildingRefundGuards(args);
            {
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogDebugForResourceEventPlayer(
                args.PlayerId,
                "OnBuildingRefund resource guard added:",
                "player", args.PlayerId,
                "buildingId", args.BuildingId,
                "percentage", args.Percentage);
            }
        }

        private void AddBuildingRefundGuards(BuildingRefundEventArgs args)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(args.PlayerId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            eStructs buildingType = buildingApi.GetType(args.BuildingId);
            int createdTick = CurrentGameTick();
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_WOOD_PLANKS, GetRefundAmount(buildingApi.GetWoodCost(buildingType), buildingApi.WoodRefundMultiplier, args.Percentage), createdTick);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_STONE_BLOCKS, GetRefundAmount(buildingApi.GetStoneCost(buildingType), buildingApi.StoneRefundMultiplier, args.Percentage), createdTick);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_IRON_INGOTS, GetRefundAmount(buildingApi.GetIronIngotCost(buildingType), buildingApi.IronRefundMultiplier, args.Percentage), createdTick);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_PITCH_RAW, GetRefundAmount(buildingApi.GetRawPitchCost(buildingType), buildingApi.PitchRefundMultiplier, args.Percentage), createdTick);
        }

        private void AddBuildingRefundGuard(int playerId, eGoods good, int amount, int createdTick)
        {
            if (amount <= 0)
                return;

            long key = ResourceEventKey(playerId, good);
            refundResourceGuards[key] = new ResourceEventCountGuard
            {
                RemainingAmount = amount,
                CreatedTick = createdTick
            };
            activeRefundGuardKeys.Add(key);
        }

        private void RemoveResourceRefundGuards(BuildingRefundEventArgs args)
        {
            // The Script Extender raises Post synchronously after Vanilla's refund. Every refund
            // AddGood has happened by then, so a leftover guard (e.g. differing rounding) must not
            // swallow later production gains.
            if (args.Phase != EventHookPhase.Post || activeRefundGuardKeys.Count == 0)
                return;

            for (int index = 0; index < activeRefundGuardKeys.Count; index++)
                refundResourceGuards.Remove(activeRefundGuardKeys[index]);
            activeRefundGuardKeys.Clear();
        }

        private static int GetRefundAmount(int cost, float refundMultiplier, int percentage)
        {
            if (cost <= 0 || refundMultiplier <= 0 || percentage <= 0)
                return 0;
            return (int)(cost * refundMultiplier * (percentage / 100f));
        }
    }
}
