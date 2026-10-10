using System;
using System.Threading;
using BepInEx.Logging;
using CrusaderDE;
using APIShared.Presentation;

namespace UnitLimit
{
    // Consumer behavior remains private; APIShared owns the two hover publishers.
    internal sealed class CreateTroopHoverHook
    {
        public CreateTroopHoverHook(ManualLogSource log, Action<MainViewModel> onEnter, Action onLeave)
        {
            int published = 0;
            if (!PresentationEvents.TryRegister(PresentationOperation.RecruitmentEnter, UnitLimitPlugin.PluginGuid,
                "RecruitmentHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted) onEnter(args.ViewModel); }, out string reason))
                throw new InvalidOperationException(reason);
            if (!PresentationEvents.TryRegister(PresentationOperation.RecruitmentLeave, UnitLimitPlugin.PluginGuid,
                "RecruitmentHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted) onLeave(); }, out reason))
                throw new InvalidOperationException(reason);
            Volatile.Write(ref published, 1);
        }
    }
}
