using System;
using APIShared.Presentation;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
namespace BugfixesAndQoL.UnitCommands
{
    // The shared native publisher runs before Vanilla clears the selected movement marker.
    internal sealed class GroundMoveFrameObserver
    {
        private readonly Action observe;
        private VisibleMapRenderRegistration registration;
        internal GroundMoveFrameObserver(Action observe) { this.observe = observe ?? throw new ArgumentNullException(nameof(observe)); }
        internal void Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (!VisibleMapRenderEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "ground-feedback",
                _ => observe(), null, out registration, out string reason, order: 10))
                throw new InvalidOperationException(reason);
        }
        internal void SetActive(bool active) => registration?.SetEnabled(active);
    }
}
