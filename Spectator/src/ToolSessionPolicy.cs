namespace Spectator {
    internal static class ToolSessionPolicy {
        internal static bool CanMutate(bool enabled, bool session, bool model, bool state, bool network, bool director, bool multiplayer) =>
            enabled && session && model && state && !network && director && !multiplayer;
    }
}
