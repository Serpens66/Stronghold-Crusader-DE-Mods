using APIShared;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private IPlagueHeightCapability plagueHeights;
        // ApplySettings is already called by the durable mission/mode/settings publishers.
        // Failed acquisition disables only this feature and is retried at the next settings pass.
        private void ApplyPlagueHeightSettings()
        {
            if (plagueHeights == null && !ApiShared.ForMod(ExtraFeaturesPlugin.PluginGuid)
                .TryGetPlagueHeight(out plagueHeights, out var unavailable))
            {
                if (unavailable.State != NativeCapabilityState.Pending)
                    Shared.DebugLogHelper.LogErrorLimited(log, "PlagueHeightUnavailable", "Plague heights unavailable: " + unavailable.Reason);
                return;
            }
            var categories = PlagueSurfaceCategory.None;
            if (settings.PlagueDamageWalls) categories |= PlagueSurfaceCategory.Walls;
            if (settings.PlagueDamageGates) categories |= PlagueSurfaceCategory.Gates;
            if (settings.PlagueDamageTowers) categories |= PlagueSurfaceCategory.Towers;
            if (settings.PlagueDamageKeeps) categories |= PlagueSurfaceCategory.Keeps;
            if (!plagueHeights.TrySetPolicy(Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod), categories, 20, out var diagnostic))
                Shared.DebugLogHelper.LogErrorLimited(log, "PlagueHeightPolicy", "Plague height policy rejected: " + diagnostic.Reason);
        }
    }
}
