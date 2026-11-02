// Feature: Treat an original spectator who controls a player's input (APIShared) like that player.
namespace BugfixesAndQoL
{
    internal static class SpectatorInputControlPolicy
    {
        // Only a spectator without input control keeps observer behavior (foreign HUD, no Lord controls).
        internal static bool IsObservingSpectator(int spectatorMode, int inputControlledPlayerId) =>
            spectatorMode != 0 && !IsPlayer(inputControlledPlayerId);

        // Own player whose units are not foreign: the input-controlled player for a spectator, otherwise the
        // ordinary perspective. 0 means no own player.
        internal static int ResolveOwnPlayerId(int spectatorMode, int inputControlledPlayerId, int ordinaryPlayerId)
        {
            int playerId = spectatorMode != 0 ? inputControlledPlayerId : ordinaryPlayerId;
            return IsPlayer(playerId) ? playerId : 0;
        }

        private static bool IsPlayer(int playerId) => playerId >= 1 && playerId <= 8;
    }
}
