using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest
{
    internal readonly struct RescueTile
    {
        internal readonly int X, Y, TileId;
        internal RescueTile(int x, int y, int tileId) { X = x; Y = y; TileId = tileId; }
    }
    internal readonly struct RescueDiscoveryUnit
    {
        internal readonly int UnitId, CurrentTile, NextTile, ChainNextId;
        internal readonly uint GlobalId;
        internal readonly bool HasOrdinaryNext;
        internal RescueDiscoveryUnit(int unitId, uint globalId, int currentTile, int nextTile, int chainNextId, bool hasOrdinaryNext)
        { UnitId = unitId; GlobalId = globalId; CurrentTile = currentTile; NextTile = nextTile; ChainNextId = chainNextId; HasOrdinaryNext = hasOrdinaryNext; }
    }
    internal delegate bool RescueDiscoveryUnitReader(int unitId, out RescueDiscoveryUnit record);
    internal readonly struct RescueDiscoveryBudget
    {
        internal readonly int MaximumCells, MaximumLinks, MaximumUnits, MaximumUnitId, TileCapacity;
        internal RescueDiscoveryBudget(int maximumCells, int maximumLinks, int maximumUnits, int maximumUnitId, int tileCapacity)
        {
            if (maximumCells < 1 || maximumLinks < 1 || maximumUnits < 1 || maximumUnitId < 1 || tileCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumCells));
            MaximumCells = maximumCells; MaximumLinks = maximumLinks; MaximumUnits = maximumUnits;
            MaximumUnitId = maximumUnitId; TileCapacity = tileCapacity;
        }
    }

    // Pure, event-local discovery. tileAt owns active-map/packed-coordinate validation.
    // The one-cell halo is valid only for ordinary eight-neighbour Next production.
    internal static class RescueDiscovery
    {
        internal static bool TryCollect(IList<RescueTile> changed, Func<int, int, int> tileAt,
            Func<int, int> head, RescueDiscoveryUnitReader read, RescueDiscoveryBudget budget,
            out List<RescueDiscoveryUnit> units, out string reason)
        {
            units = new List<RescueDiscoveryUnit>(); reason = null;
            if (changed == null || tileAt == null || head == null || read == null || budget.MaximumCells < 1 || budget.MaximumLinks < 1 ||
                budget.MaximumUnits < 1 || budget.MaximumUnitId < 1 || budget.TileCapacity < 1)
            { reason = "invalid-discovery-input"; return false; }
            if (changed.Count > budget.MaximumCells) { reason = "mutation-cell-budget"; return false; }
            var delta = new Dictionary<int, RescueTile>();
            var halo = new SortedSet<int>();
            var records = new Dictionary<int, RescueDiscoveryUnit>();
            var selected = new Dictionary<int, RescueDiscoveryUnit>();
            try
            {
                foreach (var tile in changed)
                {
                    if (!ValidTile(tile.TileId, budget) || tileAt(tile.X, tile.Y) != tile.TileId)
                    { reason = "invalid-mutation-tile"; return false; }
                    if (delta.TryGetValue(tile.TileId, out var old) && (old.X != tile.X || old.Y != tile.Y))
                    { reason = "aliased-mutation-coordinates"; return false; }
                    delta[tile.TileId] = tile;
                }
                foreach (var tile in delta.Values)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            long x = (long)tile.X + dx, y = (long)tile.Y + dy;
                            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) continue;
                            int id = tileAt((int)x, (int)y);
                            if (id < 0) continue; // Clip a halo at active-map/diamond boundaries.
                            if (!ValidTile(id, budget)) { reason = "invalid-halo-tile"; return false; }
                            halo.Add(id);
                            if (halo.Count > budget.MaximumCells) { reason = "occupancy-cell-budget"; return false; }
                        }
                int visits = 0;
                foreach (int tile in halo)
                {
                    var chain = new HashSet<int>();
                    int id = head(tile);
                    while (id != 0)
                    {
                        if (++visits > budget.MaximumLinks) { reason = "occupancy-walk-budget"; return false; }
                        if (id < 1 || id > budget.MaximumUnitId || !chain.Add(id))
                        { reason = "invalid-or-cyclic-occupancy-chain"; return false; }
                        if (!read(id, out var record) || record.UnitId != id || record.GlobalId == 0 || !ValidTile(record.CurrentTile, budget) ||
                            record.ChainNextId < 0 || record.ChainNextId > budget.MaximumUnitId ||
                            record.HasOrdinaryNext && !ValidTile(record.NextTile, budget))
                        { reason = "invalid-occupant-record"; return false; }
                        if (records.TryGetValue(id, out var previous) && !Same(previous, record))
                        { reason = "occupant-changed-during-discovery"; return false; }
                        records[id] = record;
                        // Auxiliary links can legitimately mention a different current tile.
                        // Only the actual Current chain establishes the ordinary halo proof.
                        if (record.CurrentTile == tile && (delta.ContainsKey(record.CurrentTile) ||
                            record.HasOrdinaryNext && delta.ContainsKey(record.NextTile)))
                        {
                            selected[id] = record;
                            if (selected.Count > budget.MaximumUnits) { reason = "occupant-snapshot-budget"; return false; }
                        }
                        id = record.ChainNextId;
                    }
                }
                // No expensive unit snapshots or mutations occur until ALL chains succeeded.
                var ids = new List<int>(selected.Keys); ids.Sort();
                foreach (int id in ids) units.Add(selected[id]);
                return true;
            }
            catch
            {
                units.Clear(); reason = "discovery-reader-failed"; return false;
            }
        }
        private static bool ValidTile(int tile, RescueDiscoveryBudget budget) => tile >= 0 && tile < budget.TileCapacity;
        private static bool Same(RescueDiscoveryUnit a, RescueDiscoveryUnit b) => a.UnitId == b.UnitId && a.GlobalId == b.GlobalId &&
            a.CurrentTile == b.CurrentTile && a.NextTile == b.NextTile && a.ChainNextId == b.ChainNextId && a.HasOrdinaryNext == b.HasOrdinaryNext;
    }
}
