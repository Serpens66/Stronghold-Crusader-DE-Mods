using System;
using System.Threading;

namespace APIShared
{
    /// <summary>Identifies a native path search whose player context can be scoped.</summary>
    public enum EnemyGateSearchKind
    {
        /// <summary>Central tile route builder.</summary>
        Builder,
        /// <summary>Attack approach flood search.</summary>
        Attack,
        /// <summary>Building approach search.</summary>
        BuildingApproach,
        /// <summary>Building candidate consumer.</summary>
        BuildingConsumer,
        /// <summary>Direct cursor command.</summary>
        CursorCommand
    }

    /// <summary>Optional, immutable player gate policy supplied by a registered mod.</summary>
    public interface IEnemyGatePathPolicy
    {
        /// <summary>Whether at least one player has a published gate mask.</summary>
        bool HasPublishedMask { get; }
        /// <summary>Checks one directed tile edge against the published mask.</summary>
        bool IsDirectionAllowed(int playerId, int tileId, int direction);
        /// <summary>Resolves a native tribe to its player, or an unknown value.</summary>
        int ResolveTribePlayer(int tribeId);
        /// <summary>Validates the explicit building-search player against its tribe.</summary>
        int ResolveBuildingPlayer(int explicitPlayerId, int tribeId);
        /// <summary>Validates the cursor player against the active native player.</summary>
        int ResolveCursorPlayer(int tribeId);
        /// <summary>Scopes a native search and returns an opaque token for its exit.</summary>
        object EnterNativeSearch(int playerId, EnemyGateSearchKind kind);
        /// <summary>Ends the native search scope and records its outcome.</summary>
        void ExitNativeSearch(object scope, EnemyGateSearchKind kind, bool completed, bool success);
    }

    /// <summary>Optional read-only observer used by a registered gate test policy.</summary>
    public interface IEnemyGateRegionPairObserver
    {
        /// <summary>Observes one completed native region-pair query without changing its result.</summary>
        void ObserveRegionPair(int playerId, int sourceComponentId,
            int destinationComponentId, int queryMode, int vanillaResult,
            int effectiveResult, string source);
    }

    /// <summary>Passive bridge that holds an optional registered gate policy.</summary>
    public static class EnemyGatePathPolicyBridge
    {
        private static IEnemyGatePathPolicy provider;

        /// <summary>Gets the registered provider, if any.</summary>
        public static IEnemyGatePathPolicy Current => Volatile.Read(ref provider);

        /// <summary>Registers a provider once; the provider clears its masks at map end.</summary>
        public static bool TryRegister(IEnemyGatePathPolicy candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            IEnemyGatePathPolicy previous = Interlocked.CompareExchange(ref provider, candidate, null);
            return previous == null || ReferenceEquals(previous, candidate);
        }
    }
}
