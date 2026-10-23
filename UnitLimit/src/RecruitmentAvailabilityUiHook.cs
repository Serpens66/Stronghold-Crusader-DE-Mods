using System;
using BepInEx.Logging;
using APIShared.Presentation;
using APIShared.Recruitment;

namespace UnitLimit
{
    internal sealed class RecruitmentAvailabilityUiHook
    {
        public RecruitmentAvailabilityUiHook(ManualLogSource log, UnitLimitRuntime runtime)
        {
            // Order -1 keeps pending expiry ahead of APIShared's shared availability pass (order 0).
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, UnitLimitPlugin.PluginGuid,
                "RecruitmentAvailability", null, args => {
                    if (args.OriginalCompleted) runtime.ExpirePendingRecruitmentsForGuiCheck();
                }, out string reason, -1)) throw new InvalidOperationException(reason);
            if (!RecruitmentAvailability.TryRegister(UnitLimitPlugin.PluginGuid, "UnitLimit",
                runtime.IsRecruitmentBlockedByLimit, runtime.IsRecruitmentAvailabilityActive, out reason))
                throw new InvalidOperationException(reason);
        }
    }
}