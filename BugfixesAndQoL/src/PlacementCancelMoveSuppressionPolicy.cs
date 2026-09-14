// Feature: Correlate a placement-cancel click with only the affected move orders.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct PlacementCancelUnitIdentity : IEquatable<PlacementCancelUnitIdentity>
    {
        public PlacementCancelUnitIdentity(int unitId, uint globalId)
        {
            UnitId = unitId;
            GlobalId = globalId;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }

        public bool IsValid => UnitId > 0 && GlobalId != 0;

        public bool Equals(PlacementCancelUnitIdentity other) =>
            UnitId == other.UnitId && GlobalId == other.GlobalId;

        public override bool Equals(object obj) =>
            obj is PlacementCancelUnitIdentity other && Equals(other);

        public override int GetHashCode() => unchecked((UnitId * 397) ^ (int)GlobalId);
    }

    internal sealed class PlacementCancelMoveSuppressionState
    {
        private readonly object sync = new object();
        private readonly HashSet<PlacementCancelUnitIdentity> pendingUnits =
            new HashSet<PlacementCancelUnitIdentity>();
        private int playerId = -1;

        public int PendingCount
        {
            get
            {
                lock (sync)
                    return pendingUnits.Count;
            }
        }

        public void Replace(int localPlayerId, IEnumerable<PlacementCancelUnitIdentity> selectedUnits)
        {
            lock (sync)
            {
                pendingUnits.Clear();
                playerId = -1;
                if (localPlayerId <= 0 || selectedUnits == null)
                    return;

                foreach (PlacementCancelUnitIdentity identity in selectedUnits)
                {
                    if (identity.IsValid)
                        pendingUnits.Add(identity);
                }

                if (pendingUnits.Count != 0)
                    playerId = localPlayerId;
            }
        }

        public bool TryConsumeMatchingGroup(
            int localPlayerId,
            IEnumerable<PlacementCancelUnitIdentity> groupUnits,
            out int matchedCount,
            out int remainingCount)
        {
            lock (sync)
            {
                matchedCount = 0;
                remainingCount = pendingUnits.Count;
                if (localPlayerId <= 0 || localPlayerId != playerId || groupUnits == null ||
                    pendingUnits.Count == 0)
                {
                    return false;
                }

                var matches = new List<PlacementCancelUnitIdentity>();
                foreach (PlacementCancelUnitIdentity identity in groupUnits)
                {
                    if (identity.IsValid && pendingUnits.Contains(identity))
                        matches.Add(identity);
                }

                if (matches.Count == 0)
                    return false;

                foreach (PlacementCancelUnitIdentity identity in matches)
                    pendingUnits.Remove(identity);

                matchedCount = matches.Count;
                remainingCount = pendingUnits.Count;
                if (remainingCount == 0)
                    playerId = -1;
                return true;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                pendingUnits.Clear();
                playerId = -1;
            }
        }
    }
}
