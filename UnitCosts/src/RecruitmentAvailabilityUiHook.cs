using System;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.Interop;
using APIShared.Presentation;
using APIShared.Recruitment;

namespace UnitCosts
{
    // The shared IL policy and GUI publisher stay installed; predicates provide logical activation.
    internal sealed class RecruitmentAvailabilityUiHook
    {
        public RecruitmentAvailabilityUiHook(ManualLogSource log, Func<bool> isActive,
            Func<eChimps, bool> noWeapons, Action refreshAvailability)
        {
            int published = 0;
            if (!RecruitmentMaterialUi.TryRegister(UnitCostsPlugin.PluginGuid, "NoWeapons",
                unitType => Volatile.Read(ref published) != 0 && isActive() && noWeapons(unitType), out string reason))
                throw new InvalidOperationException(reason);
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, UnitCostsPlugin.PluginGuid,
                "RecruitmentAvailability", null, args => {
                    if (Volatile.Read(ref published) != 0 && args.OriginalCompleted && isActive()) refreshAvailability();
                }, out reason)) throw new InvalidOperationException(reason);
            Shared.DebugLogHelper.LogDebug(log, "Recruitment material UI and availability registered with APIShared.");
            Volatile.Write(ref published, 1);
        }
    }
}
