// Feature: Register the controlled Lord with APIShared's process-wide HUD pipeline.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal sealed class LordUnitHudRegistration
    {
        private const string CategoryId = "controlled-lord";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TroopHudMiddleClickCameraFeature camera;

        internal LordUnitHudRegistration(ManualLogSource log, BugfixesAndQoLViewModel settings, TroopHudMiddleClickCameraFeature camera)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            ApiShared.WhenReady(Register);
        }

        internal IUnitHudPresentationCapability Capability { get; private set; }

        private void Register(IApiShared api)
        {
            if (!api.TryGetUnitHudPresentation(BugfixesAndQoLPlugin.PluginGuid, out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                Shared.DebugLogHelper.LogError(log, $"Shared Lord HUD capability unavailable: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            var definition = new UnitHudCategoryDefinition(
                CategoryId,
                "Lord",
                (int)eChimps.CHIMP_TYPE_LORD,
                UnitHudSurface.All,
                ResolveLordIcon);
            if (!capability.TryRegisterCategory(definition, IsControlledLord, out diagnostic) ||
                !capability.TryRegisterInteraction("lord-middle-click", OnInteraction, out diagnostic))
            {
                Shared.DebugLogHelper.LogError(log, $"Shared Lord HUD registration failed: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            foreach (UnitHudImageSlot slot in Enum.GetValues(typeof(UnitHudImageSlot)))
            {
                var imageOverride = new UnitHudImageOverrideDefinition("european-lord-" + slot, slot);
                if (!capability.TryRegisterImageOverride(imageOverride, context =>
                    !context.Arabic && HasControlledLord() ? ResolveLordIcon() : null, out diagnostic))
                {
                    Shared.DebugLogHelper.LogError(log, $"Lord image override registration failed for {slot}: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                    return;
                }
            }
            Capability = capability;
            capability.RequestRefresh();
            Shared.DebugLogHelper.LogInfo(log, "Controlled Lord registered with APIShared unit-HUD presentation.");
        }

        private bool IsControlledLord(UnitHudUnitSnapshot unit)
        {
            if (!settings.EnableMod || !settings.EnableLordUnitControls || !unit.IsAlive || unit.VanillaType != (int)eChimps.CHIMP_TYPE_LORD)
                return false;
            int playerId = Shared.GameModeHelper.IsMapEditor()
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);
            return playerId > 0 && unit.OwnerPlayerId == playerId &&
                GamePlayerManagerAPI.Instance?.GetLordUnitId(playerId) == unit.GameId;
        }

        private bool HasControlledLord()
        {
            if (!settings.EnableMod || !settings.EnableLordUnitControls) return false;
            int playerId = Shared.GameModeHelper.IsMapEditor()
                ? (EditorDirector.instance?.ActivePlayerID ?? -1)
                : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);
            return playerId > 0 && (GamePlayerManagerAPI.Instance?.GetLordUnitId(playerId) ?? -1) > 0;
        }

        private static ImageSource ResolveLordIcon() =>
            Noesis.GUI.GetApplicationResources()["BugfixesAndQoL-LordIcon"] as ImageSource;

        private void OnInteraction(UnitHudInteractionContext context)
        {
            if (context?.Button != UnitHudMouseButton.Middle || context.Category?.OwnerGuid != BugfixesAndQoLPlugin.PluginGuid || context.Category.CategoryId != CategoryId || context.Category.Units.Count == 0)
                return;
            camera.JumpToUnit(context.Category.Units[0].GameId);
        }
    }
}
