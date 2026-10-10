using System;
using System.Threading;
using BepInEx.Logging;
using CrusaderDE;
using APIShared.Presentation;

namespace UnitCosts
{
    // Consumer behavior remains private; APIShared owns the two hover publishers.
    internal sealed class CreateTroopHoverHook
    {
        public CreateTroopHoverHook(ManualLogSource log, Func<bool> isActive, Action<MainViewModel> onEnter, Action onLeave)
        {
            int published = 0;
            if (!PresentationEvents.TryRegister(PresentationOperation.RecruitmentEnter, UnitCostsPlugin.PluginGuid,
                "RecruitmentHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted && isActive()) onEnter(args.ViewModel); }, out string reason))
                throw new InvalidOperationException(reason);
            if (!PresentationEvents.TryRegister(PresentationOperation.RecruitmentLeave, UnitCostsPlugin.PluginGuid,
                "RecruitmentHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted && isActive()) onLeave(); }, out reason))
                throw new InvalidOperationException(reason);
            Volatile.Write(ref published, 1);
        }
    }
}
