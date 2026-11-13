// Feature: The player whose units and buildings local HUD commands act on.
using SHCDESE.API;

namespace ExtraFeatures
{
    internal static class ControlledPlayer
    {
        // In the map editor this is the player selected in the editor toolbar; otherwise the
        // local player. -1 when none is known yet.
        internal static int GetId()
        {
            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : -1;
        }
    }
}
