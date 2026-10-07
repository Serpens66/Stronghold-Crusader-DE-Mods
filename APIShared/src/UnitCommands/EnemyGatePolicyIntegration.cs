using APIShared;
using System;

namespace APIShared.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        // The normal BugfixesAndQoL path does no gate work. The optional provider is
        // published only after the test mod has installed its disjoint inline adapters.
        internal IEnemyGatePathPolicy BeginEnemyGateSearch(
            int playerId, EnemyGateSearchKind kind, out object scope)
        {
            scope = null;
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            if (policy == null || !policy.HasPublishedMask ||
                playerId <= 0 || playerId > 8)
                return null;
            try
            {
                scope = policy.EnterNativeSearch(playerId, kind);
                return policy;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("enemy-gate-enter", ex);
                return null;
            }
        }

        internal void ObserveEnemyGateAssassinBuilding(IEnemyGatePathPolicy policy,
            int tribeId, int buildingId, int sourceRegion, int rawSearchPlayer)
        {
            try { EnemyBridgeDiagnosticBridge.Current?.ObserveAssassinBuildingSearch(tribeId, buildingId, sourceRegion, rawSearchPlayer); }
            catch (Exception ex) { TryLogDiagnosticFailure("bridge-building", ex); }
            if (!(policy is IEnemyGateAssassinObserver observer)) return;
            try { observer.ObserveAssassinBuildingSearch(tribeId, buildingId, sourceRegion, rawSearchPlayer); }
            catch (Exception ex) { TryLogDiagnosticFailure("enemy-gate-assassin-building", ex); }
        }

        internal void EndEnemyGateSearch(
            IEnemyGatePathPolicy policy, object scope, EnemyGateSearchKind kind,
            bool completed, bool success)
        {
            if (policy == null) return;
            try { policy.ExitNativeSearch(scope, kind, completed, success); }
            catch (Exception ex) { TryLogDiagnosticFailure("enemy-gate-exit", ex); }
        }

        internal static int ResolveEnemyGateTribePlayer(int tribeId)
        {
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            return policy != null && policy.HasPublishedMask
                ? policy.ResolveTribePlayer(tribeId) : -1;
        }

        internal static int ResolveEnemyGateBuildingPlayer(int explicitPlayerId, int tribeId)
        {
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            return policy != null && policy.HasPublishedMask
                ? policy.ResolveBuildingPlayer(explicitPlayerId, tribeId) : -1;
        }

        internal static int ResolveEnemyGateCursorPlayer(int tribeId)
        {
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            return policy != null && policy.HasPublishedMask
                ? policy.ResolveCursorPlayer(tribeId) : -1;
        }
    }
}
