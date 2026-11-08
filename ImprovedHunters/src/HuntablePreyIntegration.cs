using System;
using System.Collections.Generic;
using APIShared.Wildlife;
using SHCDESE.Interop;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // APIShared owns the native parts of hunting: admission of non-Vanilla prey, the release of the
        // Hunter's reservation, own-player chickens, corpse removal when butchering starts, the base meat per
        // butchered animal and the correct Hunter id of target queries. This mod registers its animals, its
        // target selection and its meat settings there once at startup; registrations are process-lived and
        // follow the settings logically (APIShared freezes them per mission and agrees on them in multiplayer).
        private readonly Dictionary<eChimps, HuntablePreyRegistration> preyRegistrations = new Dictionary<eChimps, HuntablePreyRegistration>();
        private bool targetPolicyRegistered, meatHandlerRegistered;

        private void RegisterHuntingWithApiShared()
        {
            RegisterPrey(eChimps.CHIMP_TYPE_RABBIT, definition => definition.WithCorpseRemovedWhenButchered());
            RegisterPrey(eChimps.CHIMP_TYPE_CAMEL, definition => definition.WithCorpseRemovedWhenButchered());
            // Chickens keep their owner, so Hunters may take those of their own player only.
            RegisterPrey(eChimps.CHIMP_TYPE_CHICKEN, definition => definition.WithOwnPlayerPrey().WithCorpseRemovedWhenButchered());
            targetPolicyRegistered = HunterTargetPolicies.TryRegister(ImprovedHuntersPlugin.PluginGuid, "target-selection",
                OnHunterTargetCandidate, out string policyReason);
            if (!targetPolicyRegistered)
                Shared.DebugLogHelper.LogError(log, "Improved Hunters target selection is unavailable; Hunters choose like Vanilla: " + policyReason);
            meatHandlerRegistered = HunterMeatYieldEvents.TryRegister(ImprovedHuntersPlugin.PluginGuid, "meat-settings",
                OnHunterMeatYield, out string meatReason);
            if (!meatHandlerRegistered)
                Shared.DebugLogHelper.LogError(log, "Improved Hunters meat settings are unavailable; Hunters get Vanilla's meat: " + meatReason);
            ApplyPreyRegistrations();
        }

        private void RegisterPrey(eChimps type, Func<HuntablePreyDefinition, HuntablePreyDefinition> options)
        {
            var definition = options(new HuntablePreyDefinition((int)type, GetPreyHandlingCost(type)));
            if (!HuntablePrey.TryRegister(ImprovedHuntersPlugin.PluginGuid, type.ToString(), definition,
                    out HuntablePreyRegistration registration, out string reason))
            {
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters cannot make {type} huntable: {reason}");
                return;
            }
            preyRegistrations[type] = registration;
            if (registration.Status == HuntablePreyRegistrationStatus.Unsupported)
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters cannot make {type} huntable: {registration.Diagnostic}");
        }

        // Before or after a mission, from the settings only; APIShared applies it at the next mission start.
        // The mode gate is deliberately not part of it: it is closed outside missions and only opens during
        // mission initialization, after APIShared has frozen the catalog, and APIShared's catalog follows the
        // mission mode itself. Where this mod's mode policy is closed, its target policy refuses these types.
        private void ApplyPreyRegistrations()
        {
            foreach (var pair in preyRegistrations)
            {
                bool wanted = settings.IsHuntingEnabled(pair.Key);
                if (pair.Value.IsEnabled != wanted && !pair.Value.TrySetEnabled(wanted, out string reason))
                    Shared.DebugLogHelper.LogError(log, $"Improved Hunters could not update {pair.Key} hunting: {reason}");
            }
        }

        private bool IsRegisteredPrey(eChimps type) => preyRegistrations.ContainsKey(type);

        // Whether APIShared admits the type in the running mission (outside one: for the next mission).
        private static bool IsPreyAdmitted(eChimps type) => HuntablePrey.Current.IsHuntable((int)type);

        // The setting replaces Vanilla's base amount; Vanilla still adds its bonus, as for all meat.
        private void OnHunterMeatYield(HunterMeatYield yield)
        {
            var type = (eChimps)yield.PreyType;
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || !settings.IsKnownAnimal(type) ||
                !IsRuntimeHuntingEnabled(type))
                return;
            yield.BaseAmount = settings.GetMeatAmount(type);
        }
    }
}
