using SHCDESE.Interop.Enums;

namespace CastlePlanner
{
    internal static class DecorationVerificationPolicy
    {
        internal static bool IsStableMatch(
            int projectileId,
            int flyGridProjectileId,
            AliveState aliveState,
            ProjectileType actualType,
            ProjectileType expectedType,
            uint actualPlayerId,
            int expectedPlayerId,
            int sourceX,
            int sourceY,
            int expectedSourceX,
            int expectedSourceY,
            int currentTileX,
            int currentTileY,
            int expectedTileX,
            int expectedTileY,
            uint currentTileId,
            int expectedTileId)
        {
            return projectileId > 0 &&
                projectileId <= short.MaxValue &&
                flyGridProjectileId == projectileId &&
                (aliveState == AliveState.NeedsInit || aliveState == AliveState.IsAlive) &&
                actualType == expectedType &&
                actualPlayerId == (uint)expectedPlayerId &&
                sourceX == expectedSourceX &&
                sourceY == expectedSourceY &&
                currentTileX == expectedTileX &&
                currentTileY == expectedTileY &&
                currentTileId == (uint)expectedTileId;
        }
    }
}
