namespace Spectator {
    internal static class ToolSessionPolicy {
        // The map editor never shows the Spectator toolbar, so it has no tool session either.
        internal static bool Enabled(bool spectatorTools, bool singleplayerTest, bool editor, bool spectator, bool activePlayer) =>
            !editor && (spectator ? spectatorTools : activePlayer && singleplayerTest);

        internal static bool CanSelect(bool spectator, bool test, bool occupied, bool ai, bool self) =>
            occupied && (spectator || test && (ai || self));

        internal static bool CanMutate(bool enabled, bool authorizedSession, bool model, bool state, bool network, bool director, bool multiplayer) =>
            enabled && authorizedSession && model && state && !network && director && !multiplayer;
    }
}
