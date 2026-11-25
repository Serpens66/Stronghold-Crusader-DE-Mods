using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest.Tests
{
    internal static class DiscoveryFixture
    {
        private static int checks;
        private static readonly RescueDiscoveryBudget Budget = new RescueDiscoveryBudget(40, 50, 8, 9999, 1000);
        // Deliberately non-row-major IDs with inactive diamond-like corners.
        private static int Tile(int x, int y) => x >= 0 && x < 5 && y >= 0 && y < 5 && !(x == 0 && y == 0) ? 100 + y * 17 + x : -1;
        internal static void Run()
        {
            checks = 0;
            var changed = new List<RescueTile> { new RescueTile(2, 2, Tile(2, 2)) };
            var heads = new Dictionary<int, int>();
            var records = new Dictionary<int, RescueDiscoveryUnit>();
            int center = Tile(2, 2), adjacent = Tile(1, 2), outside = Tile(4, 4);
            heads[center] = 9; heads[adjacent] = 7;
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 2, false);
            records[2] = new RescueDiscoveryUnit(2, 12, center, center, 0, false);
            records[7] = new RescueDiscoveryUnit(7, 17, adjacent, center, 4, true);
            records[4] = new RescueDiscoveryUnit(4, 14, adjacent, outside, 0, true);
            Func<int, int> head = tile => heads.TryGetValue(tile, out int id) ? id : 0;
            RescueDiscoveryUnitReader read = delegate(int id, out RescueDiscoveryUnit unit) { return records.TryGetValue(id, out unit); };
            Require(RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out var units, out string reason), "ordinary discovery");
            Require(units.Count == 3 && units[0].UnitId == 2 && units[1].UnitId == 7 && units[2].UnitId == 9, "current+next-only candidates, sorted IDs");
            Require(units[1].GlobalId == 17 && units[1].CurrentTile == adjacent, "next-only records retain actual current/identity");
            Require(RescueDiscovery.TryCollect(changed, Tile, head, read, new RescueDiscoveryBudget(9, 50, 8, 9999, 1000), out units, out reason),
                "exact nine-cell ordinary halo budget accepted");
            int earlyHeadReads = 0;
            Require(!RescueDiscovery.TryCollect(changed, Tile, tile => { earlyHeadReads++; return head(tile); }, read,
                new RescueDiscoveryBudget(8, 50, 8, 9999, 1000), out units, out reason) && earlyHeadReads == 0,
                "over-budget halo rejected before any chain/snapshot reads");
            // A genuine midpoint changes which chain owns the actor; the same discovery
            // contract finds it before (Next only) and after (Current) without duplicates.
            heads[center] = 7;
            heads[adjacent] = 4;
            records[7] = new RescueDiscoveryUnit(7, 17, center, center, 9, true);
            Require(RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 3 &&
                units[1].UnitId == 7 && units[1].CurrentTile == center, "post-midpoint Current candidate found exactly once");
            heads[center] = 9;
            heads[adjacent] = 7;
            records[7] = new RescueDiscoveryUnit(7, 17, adjacent, center, 4, true);
            heads[Tile(3, 3)] = 9;
            int repeatedReads = 0;
            RescueDiscoveryUnitReader changing = delegate(int id, out RescueDiscoveryUnit unit) {
                bool found = records.TryGetValue(id, out unit);
                if (id == 9 && ++repeatedReads == 2) unit = new RescueDiscoveryUnit(9, 999, center, center, 2, false);
                return found;
            };
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, changing, Budget, out units, out reason) && units.Count == 0 &&
                reason == "occupant-changed-during-discovery", "ID reuse during chain enumeration rejects whole proof");
            heads.Remove(Tile(3, 3));
            records[7] = new RescueDiscoveryUnit(7, 17, adjacent, center, 4, false);
            RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason);
            Require(units.Count == 2, "nonordinary/portal Next is not admitted by ordinary halo");

            // An auxiliary chain mention is not a Current occupant; still traverse its link.
            records[9] = new RescueDiscoveryUnit(9, 19, outside, center, 2, true);
            RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason);
            Require(units.Count == 1 && units[0].UnitId == 2, "auxiliary chain cannot fabricate halo proof");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 2, false);

            heads[Tile(3, 3)] = 5;
            records[5] = new RescueDiscoveryUnit(5, 15, Tile(3, 3), center, 5, true);
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 0 &&
                reason == "invalid-or-cyclic-occupancy-chain", "late invalid chain publishes no partial candidates");
            heads.Remove(Tile(3, 3)); records.Remove(5);
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, new RescueDiscoveryBudget(40, 1, 8, 9999, 1000), out units, out reason) &&
                units.Count == 0 && reason == "occupancy-walk-budget", "link budget rejects whole receipt");
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, new RescueDiscoveryBudget(40, 50, 1, 9999, 1000), out units, out reason) &&
                units.Count == 0 && reason == "occupant-snapshot-budget", "unit budget rejects whole receipt");
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, new RescueDiscoveryBudget(1, 50, 8, 9999, 1000), out units, out reason) &&
                reason == "occupancy-cell-budget", "halo work bounded before chain reads");

            int readCalls = 0;
            Require(RescueDiscovery.TryCollect(new List<RescueTile>(), Tile, tile => { readCalls++; return 0; }, read, Budget, out units, out reason) &&
                readCalls == 0 && units.Count == 0, "no changed cells means no occupancy reads");
            var edge = new List<RescueTile> { new RescueTile(0, 1, Tile(0, 1)) };
            Require(RescueDiscovery.TryCollect(edge, Tile, head, read, Budget, out units, out reason), "active-map edge clips halo instead of rejecting valid change");
            Require(!RescueDiscovery.TryCollect(new[] { new RescueTile(0, 0, -1) }, Tile, head, read, Budget, out units, out reason) &&
                reason == "invalid-mutation-tile", "inactive actual mutation is invalid");
            Require(!RescueDiscovery.TryCollect(new[] { new RescueTile(2, 2, 12) }, Tile, head, read, Budget, out units, out reason),
                "packed ID cannot be guessed from coordinates");
            Require(!RescueDiscovery.TryCollect(changed, (x, y) => x == 1 && y == 1 ? 1000 : Tile(x, y), head, read, Budget, out units, out reason) &&
                reason == "invalid-halo-tile", "bad native packed bound rejects capture");

            records[9] = new RescueDiscoveryUnit(9, 0, center, center, 2, false);
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 0, "missing global identity");
            records[9] = new RescueDiscoveryUnit(8, 19, center, center, 2, false);
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason), "off-by-one lookup does not silently hit adjacent unit");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 10000, false);
            Require(!RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason), "link outside native ID domain");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 2, false);
            Require(!RescueDiscovery.TryCollect(changed, Tile, tile => throw new InvalidOperationException("reader"), read, Budget, out units, out reason) &&
                units.Count == 0 && reason == "discovery-reader-failed", "reader exception publishes no partial candidate");

            changed.Add(changed[0]);
            Require(RescueDiscovery.TryCollect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 2, "duplicate delta cells deduplicate");
            // Return the same packed tile for a different valid coordinate: this is not a coherent map view.
            var alias = new[] { new RescueTile(2, 2, center), new RescueTile(3, 2, center) };
            Require(!RescueDiscovery.TryCollect(alias, (x, y) => x == 3 && y == 2 ? center : Tile(x, y), head, read, Budget, out units, out reason) &&
                reason == "aliased-mutation-coordinates", "coordinate alias rejected");
            Console.WriteLine("PASS: ordinary Current/Next discovery checks=" + checks + "; production pure discovery, no native calls.");
        }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
