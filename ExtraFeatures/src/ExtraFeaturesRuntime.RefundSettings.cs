// Feature: Configure custom building-demolition refund percentages.
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;

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

    }
}
