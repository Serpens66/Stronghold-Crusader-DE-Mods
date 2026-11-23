using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        private bool camelHealthResyncReady;

        // Per-animal compatibility required by the corresponding Hunt setting. Corpse lifetimes of rabbits,
        // camels and chickens are part of their HuntablePrey registrations (HuntablePreyIntegration).

        // APIShared applies the maximum once per camel at mission start, spawn and save load, and keeps
        // the original basis in the save; this mod neither writes HP nor clamps live camels.
        private void RegisterCamelHealthPolicy()
        {
            if (camelHealthPolicy != null)
                return;

            camelArrowDamage = GameUnitManagerAPI.Instance.GetRangedArrowDamageTo(eChimps.CHIMP_TYPE_CAMEL);
            if (!APIShared.Health.HealthModifiers.TryRegister(
                ImprovedHuntersPlugin.PluginGuid,
                "camel-one-arrow",
                SelectCamelMaximumHealth,
                out APIShared.Health.HealthModifierHandle handle,
                out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            camelHealthPolicy = handle;
            camelHealthResyncReady = APIShared.Health.HealthModifiers.TryEnableMultiplayerResync(out reason);
            if (!camelHealthResyncReady)
                Shared.DebugLogHelper.LogError(log, "Improved Hunters multiplayer camel health repair unavailable: " + reason);
            UpdateCamelHealthPolicy();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters camel health policy registered: arrowDamage={camelArrowDamage}, " +
                $"enabled={handle.IsEnabled}.");
        }

        // Settings only change between matches; the provider re-checks the mission gate itself.
        // Enabled with the mod, not only with camel hunting: APIShared then keeps a health archive in every
        // save, so camels are adjusted after a later load that has camel hunting on. The provider decides.
        private void UpdateCamelHealthPolicy() =>
            camelHealthPolicy?.SetEnabled(settings.EnableMod);

        // Simulation thread: one arrow kills a camel. Deterministic from host settings and mission mode.
        private APIShared.Modifiers.ValueModifierContribution SelectCamelMaximumHealth(APIShared.Health.HealthModifierContext context)
        {
            if (context.Kind != APIShared.Health.HealthObjectKind.Unit ||
                context.UnitType != eChimps.CHIMP_TYPE_CAMEL ||
                (Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer && !camelHealthResyncReady) ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !settings.HuntCamel)
            {
                return APIShared.Modifiers.ValueModifierContribution.None;
            }

            uint oneShotHealth = (uint)Math.Max(1, camelArrowDamage - 1);
            return context.OriginalMaximumHealth <= oneShotHealth
                ? APIShared.Modifiers.ValueModifierContribution.None
                : APIShared.Modifiers.ValueModifierContribution.FixedValue(oneShotHealth);
        }

    }
}
