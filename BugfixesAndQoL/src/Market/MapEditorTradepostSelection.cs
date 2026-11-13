// Whether the selected trading post belongs to the player being edited in the map editor.
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    internal static class MapEditorTradepostSelection
    {
        internal static unsafe bool IsControlled()
        {
            if (!APIShared.GameModes.GameModeHelper.IsMapEditor())
                return true;

            int activePlayerId = EditorDirector.instance?.ActivePlayerID ?? -1;
            int buildingId = GamePlayerManagerAPI.Instance.GetSelectedBuildingId();
            return activePlayerId > 0 && buildingId > 0 &&
                APIShared.BuildingAccess.TryGetById(buildingId, out GameBuilding* building, out _) &&
                building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_BuildingType == eStructs.STRUCT_TRADEPOST &&
                building->r_PlayerIdOwner == activePlayerId;
        }
    }
}
