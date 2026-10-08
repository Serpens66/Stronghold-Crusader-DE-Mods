using System;
using APIShared;
using Noesis;

namespace ThirdPartyMod
{
    internal static class HudExample
    {
        // Load a mod-owned image through your normal asset pipeline on the Unity thread.
        // Null deliberately preserves the current image until an asset is assigned.
        internal static ImageSource Icon;

        internal static void Register(IUnitHudPresentationCapability hud, Action<string> log)
        {
            var definition = new UnitHudImageOverrideDefinition("swordsman-icon", UnitHudImageSlot.UIButtonsK007);
            if (!hud.TryRegisterImageOverride(definition, ResolveIcon, out var diagnostic))
                log(diagnostic.Reason);
        }

        private static ImageSource ResolveIcon(UnitHudImageOverrideContext context) => Icon;
    }
}
