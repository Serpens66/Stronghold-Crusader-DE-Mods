using APIShared;
using BepInEx.Logging;
using System;

namespace StartConditions
{
    internal sealed class StartConditionsBriefingGoldRegistration
    {
        private const string RegistrationId = "start-conditions";
        private readonly ManualLogSource log;
        private readonly BriefingGoldAdjuster adjust;

        internal StartConditionsBriefingGoldRegistration(
            ManualLogSource log,
            BriefingGoldAdjuster adjust)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.adjust = adjust ?? throw new ArgumentNullException(nameof(adjust));
            ApiShared.WhenReady(Register);
        }

        private void Register(IApiShared api)
        {
            if (!api.TryGetBriefingGoldPresentation(
                    StartConditionsPlugin.PluginGuid,
                    out IBriefingGoldPresentationCapability capability,
                    out NativeCapabilityDiagnostic diagnostic) ||
                !capability.TryRegisterAdjustment(
                    RegistrationId,
                    BriefingGoldAdjustmentStage.ModAdjustment,
                    adjust,
                    out diagnostic))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Start Conditions briefing-gold adjustment is unavailable: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Start Conditions briefing-gold adjustment registered with APIShared.");
        }
    }
}
