using System;
using APIShared;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class AssassinPathfindingRuntime
    {
        private readonly struct RouteCacheKey : IEquatable<RouteCacheKey>
        {
            public RouteCacheKey(
                int startX,
                int startY,
                int targetX,
                int targetY,
                int maximumNodes,
                int speedDelay,
                int playerId,
                bool allowClimbing,
                bool allowWalkableReservedClimbEndpoints, IEnemyGateRoutePolicySnapshot gatePolicy)
            {
                GatePolicy = gatePolicy;
                StartX = startX;
                StartY = startY;
                TargetX = targetX;
                TargetY = targetY;
                MaximumNodes = maximumNodes;
                SpeedDelay = speedDelay;
                PlayerId = playerId;
                AllowClimbing = allowClimbing;
                AllowWalkableReservedClimbEndpoints = allowWalkableReservedClimbEndpoints;
            }

            public int StartX { get; }
            public int StartY { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int MaximumNodes { get; }
            public int SpeedDelay { get; }
            public int PlayerId { get; }
            public bool AllowClimbing { get; }
            public bool AllowWalkableReservedClimbEndpoints { get; }
            public IEnemyGateRoutePolicySnapshot GatePolicy { get; }

            public bool Equals(RouteCacheKey other) =>
                StartX == other.StartX && StartY == other.StartY &&
                TargetX == other.TargetX && TargetY == other.TargetY &&
                MaximumNodes == other.MaximumNodes && SpeedDelay == other.SpeedDelay &&
                PlayerId == other.PlayerId && AllowClimbing == other.AllowClimbing &&
                AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints &&
                ReferenceEquals(GatePolicy, other.GatePolicy);

            public override bool Equals(object obj) =>
                obj is RouteCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StartX;
                    hash = hash * 397 ^ StartY;
                    hash = hash * 397 ^ TargetX;
                    hash = hash * 397 ^ TargetY;
                    hash = hash * 397 ^ MaximumNodes;
                    hash = hash * 397 ^ SpeedDelay;
                    hash = hash * 397 ^ PlayerId;
                    hash = hash * 397 ^ (AllowClimbing ? 1 : 0);
                    hash = hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);
                    return hash * 397 ^ AssassinGateRoutePolicy.IdentityHash(GatePolicy);
                }
            }
        }

        private readonly struct SuffixCacheKey : IEquatable<SuffixCacheKey>
        {
            public SuffixCacheKey(
                int targetX,
                int targetY,
                int speedDelay,
                bool allowClimbing,
                bool allowWalkableReservedClimbEndpoints, int policyPlayer, IEnemyGateRoutePolicySnapshot gatePolicy)
            {
                GatePolicy = gatePolicy;
                PolicyPlayer = policyPlayer;
                TargetX = targetX;
                TargetY = targetY;
                SpeedDelay = speedDelay;
                AllowClimbing = allowClimbing;
                AllowWalkableReservedClimbEndpoints = allowWalkableReservedClimbEndpoints;
            }

            public int TargetX { get; }
            public int TargetY { get; }
            public int SpeedDelay { get; }
            public bool AllowClimbing { get; }
            public bool AllowWalkableReservedClimbEndpoints { get; }
            public IEnemyGateRoutePolicySnapshot GatePolicy { get; }

            public int PolicyPlayer { get; }

            public bool Equals(SuffixCacheKey other) =>
                PolicyPlayer == other.PolicyPlayer &&
                TargetX == other.TargetX && TargetY == other.TargetY &&
                SpeedDelay == other.SpeedDelay && AllowClimbing == other.AllowClimbing &&
                AllowWalkableReservedClimbEndpoints == other.AllowWalkableReservedClimbEndpoints &&
                ReferenceEquals(GatePolicy, other.GatePolicy);

            public override bool Equals(object obj) =>
                obj is SuffixCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = TargetX ^ PolicyPlayer;
                    hash = hash * 397 ^ TargetY;
                    hash = hash * 397 ^ SpeedDelay;
                    hash = hash * 397 ^ (AllowClimbing ? 1 : 0);
                    hash = hash * 397 ^ (AllowWalkableReservedClimbEndpoints ? 1 : 0);
                    return hash * 397 ^ AssassinGateRoutePolicy.IdentityHash(GatePolicy);
                }
            }
        }

    }
}
