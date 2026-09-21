using APIShared;
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    internal sealed class BriefingNoStartingGoldFixRegistration
    {
        private const string RegistrationId = "no-starting-gold";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;

        internal BriefingNoStartingGoldFixRegistration(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ApiShared.WhenReady(Register);
        }

        private void Register(IApiShared api)
        {
            if (!api.TryGetBriefingGoldPresentation(
                    BugfixesAndQoLPlugin.PluginGuid,
                    out IBriefingGoldPresentationCapability capability,
                    out NativeCapabilityDiagnostic diagnostic) ||
                !capability.TryRegisterAdjustment(
                    RegistrationId,
                    BriefingGoldAdjustmentStage.VanillaCorrection,
                    Adjust,
                    out diagnostic))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Briefing No Starting Gold correction is unavailable: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Briefing No Starting Gold correction registered with APIShared.");
        }

        private int Adjust(BriefingGoldContext context)
        {
            if (!BriefingNoStartingGoldFixPolicy.IsEnabled(
                    settings.EnableMod,
                    settings.EnableClientFeatures,
                    settings.EnableBriefingNoStartingGoldFix))
            {
                return context.CurrentGold;
            }

            return BriefingNoStartingGoldFixPolicy.Apply(
                context.CurrentGold,
                context.EffectiveVanillaGold,
                context.IsHuman,
                context.HasNoStartingGoldState,
                context.NoStartingGoldEnabled);
        }
    }
}
