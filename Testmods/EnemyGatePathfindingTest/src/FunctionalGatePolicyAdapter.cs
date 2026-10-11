using System;
using APIShared;

namespace EnemyGatePathfindingTest
{
    // Deliberately exposes only functional contracts. Optional diagnostic interfaces
    // must not be discoverable by APIShared/Main when detailed diagnostics are off.
    internal sealed class FunctionalGatePolicyAdapter : IEnemyGatePathPolicy, IEnemyGateRoutePolicyProvider
    {
        private readonly IEnemyGatePathPolicy policy;
        private readonly IEnemyGateRoutePolicyProvider routes;
        internal FunctionalGatePolicyAdapter(IEnemyGatePathPolicy policy, IEnemyGateRoutePolicyProvider routes)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
        }
        public bool HasPublishedMask => policy.HasPublishedMask;
        public bool IsDirectionAllowed(int playerId, int tileId, int direction) => policy.IsDirectionAllowed(playerId, tileId, direction);
        public int ResolveTribePlayer(int tribeId) => policy.ResolveTribePlayer(tribeId);
        public int ResolveBuildingPlayer(int explicitPlayerId, int tribeId) => policy.ResolveBuildingPlayer(explicitPlayerId, tribeId);
        public int ResolveCursorPlayer(int tribeId) => policy.ResolveCursorPlayer(tribeId);
        public object EnterNativeSearch(int playerId, EnemyGateSearchKind kind) => policy.EnterNativeSearch(playerId, kind);
        public void ExitNativeSearch(object scope, EnemyGateSearchKind kind, bool completed, bool success) => policy.ExitNativeSearch(scope, kind, completed, success);
        public bool TryCaptureRoutePolicy(int playerId, out IEnemyGateRoutePolicySnapshot snapshot) => routes.TryCaptureRoutePolicy(playerId, out snapshot);
    }
}
