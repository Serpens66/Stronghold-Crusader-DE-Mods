using APIShared.GameModes;
// Feature: Register the controlled Lord with APIShared's process-wide HUD pipeline.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace BugfixesAndQoL
{
    internal sealed class LordUnitHudRegistration
    {
        private const string CategoryId = "controlled-lord";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TroopHudMiddleClickCameraFeature camera;
        private (int PlayerId, LordSkin Skin, int Colour)? displayedPortrait;

        internal LordUnitHudRegistration(ManualLogSource log, BugfixesAndQoLViewModel settings, TroopHudMiddleClickCameraFeature camera)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            ApiShared.WhenReady(Register);
        }

        internal IUnitHudPresentationCapability Capability { get; private set; }

        internal void RefreshSetting()
        {
            (Capability as IUnitHudActivationCapability)?.SetOwnerActive(settings.EnableMod && settings.EnableLordUnitControls);
            Capability?.RequestRefresh();
        }

        private void Register(IApiShared api)
        {
            if (!api.TryGetUnitHudPresentation(BugfixesAndQoLPlugin.PluginGuid, out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/LordUnitHudRegistration/Register.capability-unavailable-state-reason", $"Shared Lord HUD capability unavailable: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            var definition = new UnitHudCategoryDefinition(
                CategoryId,
                "Lord",
                (int)eChimps.CHIMP_TYPE_LORD,
                UnitHudSurface.All,
                ResolveLordIcon);
            ((IUnitHudActivationCapability)capability).SetOwnerActive(false);
            if (!capability.TryRegisterCategory(definition, IsControlledLord, out diagnostic) ||
                !capability.TryRegisterInteraction("lord-middle-click", OnInteraction, out diagnostic))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/LordUnitHudRegistration/Register.registration-failed-state-reason", $"Shared Lord HUD registration failed: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            Capability = capability;
            RefreshSetting();
            Shared.DebugLogHelper.LogDebug(log, "Controlled Lord registered with APIShared unit-HUD presentation.");
        }

        private bool IsControlledLord(UnitHudUnitSnapshot unit)
        {
            if (!settings.EnableMod || !settings.EnableLordUnitControls || !unit.IsAlive || unit.VanillaType != (int)eChimps.CHIMP_TYPE_LORD)
                return false;
            int playerId = GetControlledPlayerId();
            return playerId > 0 && unit.OwnerPlayerId == playerId &&
                GamePlayerManagerAPI.Instance?.GetLordUnitId(playerId) == unit.GameId;
        }

        internal void RefreshPortrait(bool visible)
        {
            if (!visible)
            {
                displayedPortrait = null;
                return;
            }
            if (!displayedPortrait.HasValue) return;

            // An already displayed portrait can change while the selection IDs stay the same.
            var current = ReadPortrait();
            if (current == displayedPortrait.Value) return;
            displayedPortrait = current;
            Capability?.RequestRefresh();
        }

        private ImageSource ResolveLordIcon()
        {
            var current = ReadPortrait();
            displayedPortrait = current;
            return LordPortraits.Get(current.Skin, current.Colour);
        }

        private static (int PlayerId, LordSkin Skin, int Colour) ReadPortrait()
        {
            int playerId = GetControlledPlayerId();
            LordSkins.TryGetForPlayer(playerId, out LordSkin skin);
            PlayerColours.TryGetUiColour(playerId, out int colour);
            return (playerId, skin, colour);
        }

        private static int GetControlledPlayerId() => GameModeHelper.IsMapEditor()
            ? (EditorDirector.instance?.ActivePlayerID ?? -1)
            : (GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1);

        private void OnInteraction(UnitHudInteractionContext context)
        {
            if (context?.Button != UnitHudMouseButton.Middle || context.Category?.OwnerGuid != BugfixesAndQoLPlugin.PluginGuid || context.Category.CategoryId != CategoryId || context.Category.Units.Count == 0)
                return;
            camera.JumpToUnit(context.Category.Units[0].GameId);
        }
    }
}
