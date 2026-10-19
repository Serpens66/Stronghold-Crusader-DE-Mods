using System;
using System.Threading;
using APIShared;

namespace BugfixesAndQoL.GateRouting
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

        // Every native search enters one scope. Scopes are strictly nested per thread, so a
        // per-thread free list replaces one allocation per search. A scope returns to the
        // list only after its paired Leave; abandoned scopes are simply never reused.
        [ThreadStatic] private static Query freeQueries;

        internal Query Enter(int player, bool unmasked, bool valid, Publication publication = null)
        {
            Query query = freeQueries;
            if (query != null) freeQueries = query.NextFree;
            else query = new Query();
            query.Set(this, publication ?? Current, player, unmasked, valid, activeQuery);
            activeQuery = query;
            return query;
        }

        internal void Leave(Query query)
        {
            if (!ReferenceEquals(activeQuery, query))
                throw new InvalidOperationException("Gate route query scopes are not paired.");
            activeQuery = query.Previous;
            query.Set(null, null, 0, false, false, null);
            query.NextFree = freeQueries;
            freeQueries = query;
        }

        public bool TryCaptureRoutePolicy(int playerId, out IEnemyGateRoutePolicySnapshot snapshot)
        {
            snapshot = null;
            if (playerId < 1 || playerId > 8) return false;
            Publication publication = Volatile.Read(ref current);
            if (publication.RulesVersion != GateAccessRules.Version) return false;
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
            internal void Set(GateRoutePolicySource source, Publication publication, int player,
                bool unmasked, bool valid, Query previous)
            { Source = source; Publication = publication; Player = player;
              Unmasked = unmasked; Valid = valid; Previous = previous; }
            internal GateRoutePolicySource Source { get; private set; }
            internal Publication Publication { get; private set; }
            internal int Player { get; private set; }
            internal bool Unmasked { get; private set; }
            internal bool Valid { get; private set; }
            internal Query Previous { get; private set; }
            internal Query NextFree;
        }

        internal sealed class Publication
        {
            internal Publication(GateRoutePolicySource source, RouteTilePolicySnapshot policy)
            {
                Policy = policy;
                RulesVersion = ReferenceEquals(policy, RouteTilePolicySnapshot.Empty) ? GateAccessRules.Version : policy.RulesVersion;
                for (int player = 1; player <= 8; player++)
                {
                    Filtered[player] = new Snapshot(source, this, player, false);
                    Unmasked[player] = new Snapshot(source, this, player, true);
                }
            }
            internal readonly long RulesVersion;
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
            public bool IsCurrent => publication.RulesVersion == GateAccessRules.Version && ReferenceEquals(Volatile.Read(ref source.current), publication);
            public bool IsDirectionAllowed(int tileId, int direction) =>
                unmasked || publication.Policy.IsDirectionAllowed(PlayerId, tileId, direction);
        }
    }
}
