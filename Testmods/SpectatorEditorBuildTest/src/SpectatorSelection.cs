using APIShared;
using BepInEx.Bootstrap;

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static SpectatorSelectionAdapter spectatorSelection;
        private static bool spectatorSelectionLinked;

        private static void SyncSpectatorSelection()
        {
            spectatorSelectionLinked = false;
            if (!IsSpectatorSession()) return;
            if (spectatorSelection == null)
            {
                if (!Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out var plugin)) return;
                var provider = plugin.Instance?.GetType().Assembly.GetType("BugfixesAndQoL.SpectatorPerspectiveRuntime");
                if (provider == null) log.LogWarning("TEST_SPECTATOR_SELECTION_UNAVAILABLE: provider type missing.");
                spectatorSelection = new SpectatorSelectionAdapter(provider,
                    PlayerPerspectiveAPI.GetViewedPlayerId, message => log.LogWarning(message));
            }
            if (!spectatorSelection.TryRead(out int player)) return;
            spectatorSelectionLinked = true;
            ApplyHumanAi(IsSelectableAi(player) ? player : -1);
        }
    }
}
