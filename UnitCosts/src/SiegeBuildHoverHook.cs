using System;
using System.Threading;
using BepInEx.Logging;
using CrusaderDE;
using APIShared.Presentation;

namespace UnitCosts
{
    // Consumer behavior remains private; APIShared owns the two hover publishers.
    internal sealed class SiegeBuildHoverHook
    {
        public SiegeBuildHoverHook(ManualLogSource log, Func<bool> isActive, Action<object> onEnter, Action onLeave)
        {
            int published = 0;
            if (!PresentationEvents.TryRegister(PresentationOperation.TroopPanelEnter, UnitCostsPlugin.PluginGuid,
                "SiegeBuildHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted && isActive()) onEnter(args.Parameter); }, out string reason))
                throw new InvalidOperationException(reason);
            if (!PresentationEvents.TryRegister(PresentationOperation.TroopPanelLeave, UnitCostsPlugin.PluginGuid,
                "SiegeBuildHover", null, args => { if (Volatile.Read(ref published) != 0 && args.OriginalCompleted && isActive()) onLeave(); }, out reason))
                throw new InvalidOperationException(reason);
            Volatile.Write(ref published, 1);
        }
    }
}
