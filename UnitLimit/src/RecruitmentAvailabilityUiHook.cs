using System;
using BepInEx.Logging;
using APIShared.Presentation;

namespace UnitLimit
{
    internal sealed class RecruitmentAvailabilityUiHook
    {
        public RecruitmentAvailabilityUiHook(ManualLogSource log, Action refreshAvailability)
        {
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, UnitLimitPlugin.PluginGuid,
                "RecruitmentAvailability", null, args => {
                    if (args.OriginalCompleted) refreshAvailability();
                }, out string reason)) throw new InvalidOperationException(reason);
        }
    }
}