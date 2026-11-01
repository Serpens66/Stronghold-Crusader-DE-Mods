namespace Spectator {
    internal static class ToolSessionPolicy {
        internal static bool Enabled(bool spectatorTools, bool singleplayerTest, bool editor, bool spectator, bool activePlayer) =>
            editor || spectator ? spectatorTools : activePlayer && singleplayerTest;

        internal static bool CanSelect(bool spectator, bool test, bool occupied, bool ai, bool self) =>
            occupied && (spectator || test && (ai || self));

        internal static bool CanMutate(bool enabled, bool session, bool model, bool state, bool network, bool director, bool multiplayer) =>
            enabled && session && model && state && !network && director && !multiplayer;
    }
}
