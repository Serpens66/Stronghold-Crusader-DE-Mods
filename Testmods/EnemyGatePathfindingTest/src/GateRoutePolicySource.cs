using System;
using System.Threading;
using APIShared;

namespace EnemyGatePathfindingTest
{
    // Publications and query scopes are managed data only. No hooks or game reads.
    internal sealed class GateRoutePolicySource : IEnemyGateRoutePolicyProvider
    {
        private Publication current;
        [ThreadStatic] private static Query activeQuery;

        internal GateRoutePolicySource() { Publish(RouteTilePolicySnapshot.Empty); }

        internal void Publish(RouteTilePolicySnapshot policy) =>
            Volatile.Write(ref current, new Publication(this, policy));

        internal Publication Current => Volatile.Read(ref current);

        internal Query Enter(int player, bool unmasked, bool valid, Publication publication = null)
        {
            var query = new Query(this, publication ?? Current, player, unmasked, valid, activeQuery);
            activeQuery = query;
            return query;
        }

        internal void Leave(Query query)
        {
            if (!ReferenceEquals(activeQuery, query))
                throw new InvalidOperationException("Gate route query scopes are not paired.");
            activeQuery = query.Previous;
        }

        public bool TryCaptureRoutePolicy(int playerId, out IEnemyGateRoutePolicySnapshot snapshot)
        {
            snapshot = null;
            if (playerId < 1 || playerId > 8) return false;
            Publication publication = Volatile.Read(ref current);
            bool unmasked = false;
            Query query = activeQuery;
            if (query != null)
            {
                if (!ReferenceEquals(query.Source, this) || !query.Valid || query.Player != playerId ||
                    !ReferenceEquals(query.Publication, publication)) return false;
                unmasked = query.Unmasked;
            }
            snapshot = (unmasked ? publication.Unmasked : publication.Filtered)[playerId];
            return true;
        }

        internal sealed class Query
        {
            internal Query(GateRoutePolicySource source, Publication publication, int player,
                bool unmasked, bool valid, Query previous)
            { Source = source; Publication = publication; Player = player;
              Unmasked = unmasked; Valid = valid; Previous = previous; }
            internal readonly GateRoutePolicySource Source;
            internal readonly Publication Publication;
            internal readonly int Player;
            internal readonly bool Unmasked, Valid;
            internal readonly Query Previous;
        }

        internal sealed class Publication
        {
            internal Publication(GateRoutePolicySource source, RouteTilePolicySnapshot policy)
            {
                Policy = policy;
                for (int player = 1; player <= 8; player++)
                {
                    Filtered[player] = new Snapshot(source, this, player, false);
                    Unmasked[player] = new Snapshot(source, this, player, true);
                }
            }
            internal readonly RouteTilePolicySnapshot Policy;
            internal readonly IEnemyGateRoutePolicySnapshot[] Filtered = new IEnemyGateRoutePolicySnapshot[9];
            internal readonly IEnemyGateRoutePolicySnapshot[] Unmasked = new IEnemyGateRoutePolicySnapshot[9];
        }

        private sealed class Snapshot : IEnemyGateRoutePolicySnapshot, IEnemyGateClimbRoutePolicySnapshot
        {
            private readonly GateRoutePolicySource source;
            private readonly Publication publication;
            private readonly bool unmasked;
            internal Snapshot(GateRoutePolicySource source, Publication publication, int player, bool unmasked)
            { this.source = source; this.publication = publication; PlayerId = player; this.unmasked = unmasked; }
            public bool TryGetBlockedGateIdentity(int tileId, int direction, out int buildingId,
                out uint globalId, out int owner, out int capturer)
            {
                buildingId = 0; globalId = 0; owner = capturer = 0;
                if (unmasked || !IsCurrent || (uint)direction > 7 || tileId < 0 ||
                    publication.Policy.IsDirectionAllowed(PlayerId, tileId, direction)) return false;
                if (publication.Policy.EdgeOwners == null || PlayerId >= publication.Policy.EdgeOwners.Length) return false;
                int gate = publication.Policy.EdgeOwners[PlayerId]?.Resolve(tileId, direction) ?? 0;
                if (gate <= 0 || !publication.Policy.GateIdentities.TryGetValue(gate, out RouteTilePolicySnapshot.GateIdentity identity) || identity.Global == 0) return false;
                buildingId = gate; globalId = identity.Global; owner = identity.Owner; capturer = identity.Capturer;
                return true;
            }
            public int PlayerId { get; }
            public bool IsCurrent => ReferenceEquals(Volatile.Read(ref source.current), publication);
            public bool IsDirectionAllowed(int tileId, int direction) =>
                unmasked || publication.Policy.IsDirectionAllowed(PlayerId, tileId, direction);
        }
    }
}
