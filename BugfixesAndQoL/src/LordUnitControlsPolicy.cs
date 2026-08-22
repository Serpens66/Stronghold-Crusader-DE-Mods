// Feature: Decide when the compact Lord troop HUD may replace Vanilla's default HUD.
namespace BugfixesAndQoL
{
    internal enum LordStanceTooltipAction
    {
        UseVanilla,
        ShowVanillaBehavior,
        UseVanillaStandGround
    }

    internal static class LordUnitControlsPolicy
    {
        internal static bool CanActivate(
            bool modEnabled,
            bool lordControlsEnabled,
            bool activeMatch,
            bool mapEditor,
            bool spectator,
            int selectedCount,
            int selectedUnitId,
            int localPlayerId,
            SurrenderLordSnapshot lord) =>
            modEnabled &&
            lordControlsEnabled &&
            (activeMatch || mapEditor) &&
            (mapEditor || !spectator) &&
            selectedCount == 1 &&
            selectedUnitId > 0 &&
            localPlayerId >= 1 &&
            localPlayerId <= 8 &&
            SurrenderPolicy.IsValidLord(lord) &&
            lord.PlayerId == localPlayerId &&
            lord.UnitId == selectedUnitId;

        internal static bool CanShowDisband(
            bool lordControlsActive,
            bool surrenderEnabled,
            bool mapEditor) =>
            lordControlsActive && surrenderEnabled && !mapEditor;

        internal static bool ShouldReturnToDefaultHud(
            bool lordModeWasActive,
            bool troopHudVisible,
            int selectedCount) =>
            lordModeWasActive && troopHudVisible && selectedCount == 0;

        internal static LordStanceTooltipAction GetStanceTooltipAction(
            bool lordModeActive,
            string buttonName)
        {
            if (!lordModeActive)
                return LordStanceTooltipAction.UseVanilla;

            switch (buttonName)
            {
                case "GuardStanceButton":
                    return LordStanceTooltipAction.ShowVanillaBehavior;
                case "DefensiveStanceButton":
                case "AggressiveStanceButton":
                    return LordStanceTooltipAction.UseVanillaStandGround;
                default:
                    return LordStanceTooltipAction.UseVanilla;
            }
        }
    }
}
