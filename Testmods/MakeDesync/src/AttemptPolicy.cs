namespace MakeDesync
{
    internal static class AttemptPolicy
    {
        internal static bool CanQueue(bool multiplayer, bool? host, int? playerId, bool resync, bool busy) =>
            multiplayer && host == false && playerId >= 1 && playerId <= 8 && !resync && !busy;

        internal static bool CanApply(bool depositMode, bool lordReady, bool depositReady) =>
            lordReady && (!depositMode || depositReady);
    }
}
