using System;
using APIShared.Units;
using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal sealed class VanillaPeaceTimeGameplayPatch
    {
        private static readonly AttackTargetRule[] WildlifeRules = CreateWildlifeRules();
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ManualLogSource log;
        private readonly PermanentNativeSwitch native;
        private readonly AttackTargetRuleRegistration wildlifeRegistration;
        private bool enabled;

        internal VanillaPeaceTimeGameplayPatch(ManualLogSource log, IntPtr libraryHandle, ScanRegion region,
            ReadOnlySpan<byte> memory, bool referenceHashMatches, BugfixesAndQoLViewModel settings,
            AttackTargetRuleRegistration wildlifeRegistration)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.wildlifeRegistration = wildlifeRegistration ?? throw new ArgumentNullException(nameof(wildlifeRegistration));
            if (libraryHandle == IntPtr.Zero || region == null || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");
            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
            VanillaPeaceTimeNativeContract.Validate(memory, imageBase, referenceHashMatches);
            native = new PermanentNativeSwitch(log, imageBase, NativeSwitchCatalog.CreatePlans(memory, imageBase, false),
                VanillaPeaceTimeNativeContract.ReferenceSha256);
            if (!TryApply(DesiredEnabled, out string reason))
                throw new InvalidOperationException("Peace Time wildlife rules could not be configured: " + reason);
            settings.SettingChanged += Refresh;
        }

        internal static AttackTargetRuleRegistration RegisterWildlifeRules()
        {
            if (!AttackTargetRules.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "peace-time-wildlife",
                out var registration, out string reason))
                throw new InvalidOperationException("Peace Time wildlife rule registration failed: " + reason);
            if (!AttackTargetRules.IsAvailable)
                throw new InvalidOperationException("The shared attack target entry is unavailable.");
            return registration;
        }

        private bool DesiredEnabled => settings.EnableMod && settings.EnablePeaceTimeGameplayFixes;

        private void Refresh(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnablePeaceTimeGameplayFixes)) return;
            bool desired = DesiredEnabled;
            if (desired != enabled && !TryApply(desired, out string reason))
                Shared.DebugLogHelper.LogErrorLimited(log, "peace-time-wildlife-update",
                    "PEACE_TIME_WILDLIFE_UPDATE_REJECTED: previous native Peace Time state retained; " + reason);
        }

        private bool TryApply(bool desired, out string reason)
        {
            // Both options are host-synced and applied before simulation. Keep the old cohort state on rejection.
            if (!wildlifeRegistration.TrySetConfiguredRules(desired ? WildlifeRules : Array.Empty<AttackTargetRule>(),
                out reason)) return false;
            native.SetEnabled(desired);
            enabled = desired;
            return true;
        }

        private static AttackTargetRule[] CreateWildlifeRules()
        {
            eChimps[] predators = { eChimps.CHIMP_TYPE_LION, eChimps.CHIMP_TYPE_HYENA, eChimps.CHIMP_TYPE_CROCODILE };
            var rules = new AttackTargetRule[predators.Length * GamePlayerManagerAPI.MAX_PLAYERS];
            int index = 0;
            foreach (eChimps predator in predators)
                for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
                    rules[index++] = new AttackTargetRule(new AttackTargetSelector(unitType: predator),
                        new AttackTargetSelector(playerId: playerId), condition: AttackTargetCondition.WhilePeaceTimeActive);
            return rules;
        }
    }
}
