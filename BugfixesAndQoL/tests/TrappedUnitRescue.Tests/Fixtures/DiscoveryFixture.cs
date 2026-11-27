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
        private static RescueDiscovery.ScratchPool activePool;
        internal static void Run()
        {
            checks = 0;
            RunMatrix();
            activePool = new RescueDiscovery.ScratchPool();
            try { RunMatrix(); PoolLifecycle(); }
            finally { activePool = null; }
            Console.WriteLine("PASS: ordinary Current/Next discovery checks=" + checks + "; full unpooled+pooled production matrix, no native calls.");
        }
        private static bool Collect(IList<RescueTile> changed,Func<int,int,int> tileAt,Func<int,int> head,
            RescueDiscoveryUnitReader read,RescueDiscoveryBudget budget,out List<RescueDiscoveryUnit> units,out string reason) =>
            activePool==null?RescueDiscovery.TryCollect(changed,tileAt,head,read,budget,out units,out reason):
                activePool.TryCollect(changed,tileAt,head,read,budget,out units,out reason);
        private static void RunMatrix()
        {
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
            Require(Collect(changed, Tile, head, read, Budget, out var units, out string reason), "ordinary discovery");
            Require(units.Count == 3 && units[0].UnitId == 2 && units[1].UnitId == 7 && units[2].UnitId == 9, "current+next-only candidates, sorted IDs");
            Require(units[1].GlobalId == 17 && units[1].CurrentTile == adjacent, "next-only records retain actual current/identity");
            Require(Collect(changed, Tile, head, read, new RescueDiscoveryBudget(9, 50, 8, 9999, 1000), out units, out reason),
                "exact nine-cell ordinary halo budget accepted");
            int earlyHeadReads = 0;
            Require(!Collect(changed, Tile, tile => { earlyHeadReads++; return head(tile); }, read,
                new RescueDiscoveryBudget(8, 50, 8, 9999, 1000), out units, out reason) && earlyHeadReads == 0,
                "over-budget halo rejected before any chain/snapshot reads");
            // A genuine midpoint changes which chain owns the actor; the same discovery
            // contract finds it before (Next only) and after (Current) without duplicates.
            heads[center] = 7;
            heads[adjacent] = 4;
            records[7] = new RescueDiscoveryUnit(7, 17, center, center, 9, true);
            Require(Collect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 3 &&
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
            Require(!Collect(changed, Tile, head, changing, Budget, out units, out reason) && units.Count == 0 &&
                reason == "occupant-changed-during-discovery", "ID reuse during chain enumeration rejects whole proof");
            heads.Remove(Tile(3, 3));
            records[7] = new RescueDiscoveryUnit(7, 17, adjacent, center, 4, false);
            Collect(changed, Tile, head, read, Budget, out units, out reason);
            Require(units.Count == 2, "nonordinary/portal Next is not admitted by ordinary halo");

            // An auxiliary chain mention is not a Current occupant; still traverse its link.
            records[9] = new RescueDiscoveryUnit(9, 19, outside, center, 2, true);
            Collect(changed, Tile, head, read, Budget, out units, out reason);
            Require(units.Count == 1 && units[0].UnitId == 2, "auxiliary chain cannot fabricate halo proof");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 2, false);

            heads[Tile(3, 3)] = 5;
            records[5] = new RescueDiscoveryUnit(5, 15, Tile(3, 3), center, 5, true);
            Require(!Collect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 0 &&
                reason == "invalid-or-cyclic-occupancy-chain", "late invalid chain publishes no partial candidates");
            heads.Remove(Tile(3, 3)); records.Remove(5);
            Require(!Collect(changed, Tile, head, read, new RescueDiscoveryBudget(40, 1, 8, 9999, 1000), out units, out reason) &&
                units.Count == 0 && reason == "occupancy-walk-budget", "link budget rejects whole receipt");
            Require(!Collect(changed, Tile, head, read, new RescueDiscoveryBudget(40, 50, 1, 9999, 1000), out units, out reason) &&
                units.Count == 0 && reason == "occupant-snapshot-budget", "unit budget rejects whole receipt");
            Require(!Collect(changed, Tile, head, read, new RescueDiscoveryBudget(1, 50, 8, 9999, 1000), out units, out reason) &&
                reason == "occupancy-cell-budget", "halo work bounded before chain reads");

            int readCalls = 0;
            Require(Collect(new List<RescueTile>(), Tile, tile => { readCalls++; return 0; }, read, Budget, out units, out reason) &&
                readCalls == 0 && units.Count == 0, "no changed cells means no occupancy reads");
            var edge = new List<RescueTile> { new RescueTile(0, 1, Tile(0, 1)) };
            Require(Collect(edge, Tile, head, read, Budget, out units, out reason), "active-map edge clips halo instead of rejecting valid change");
            Require(!Collect(new[] { new RescueTile(0, 0, -1) }, Tile, head, read, Budget, out units, out reason) &&
                reason == "invalid-mutation-tile", "inactive actual mutation is invalid");
            Require(!Collect(new[] { new RescueTile(2, 2, 12) }, Tile, head, read, Budget, out units, out reason),
                "packed ID cannot be guessed from coordinates");
            Require(!Collect(changed, (x, y) => x == 1 && y == 1 ? 1000 : Tile(x, y), head, read, Budget, out units, out reason) &&
                reason == "invalid-halo-tile", "bad native packed bound rejects capture");

            records[9] = new RescueDiscoveryUnit(9, 0, center, center, 2, false);
            Require(!Collect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 0, "missing global identity");
            records[9] = new RescueDiscoveryUnit(8, 19, center, center, 2, false);
            Require(!Collect(changed, Tile, head, read, Budget, out units, out reason), "off-by-one lookup does not silently hit adjacent unit");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 10000, false);
            Require(!Collect(changed, Tile, head, read, Budget, out units, out reason), "link outside native ID domain");
            records[9] = new RescueDiscoveryUnit(9, 19, center, center, 2, false);
            Require(!Collect(changed, Tile, tile => throw new InvalidOperationException("reader"), read, Budget, out units, out reason) &&
                units.Count == 0 && reason == "discovery-reader-failed", "reader exception publishes no partial candidate");

            changed.Add(changed[0]);
            Require(Collect(changed, Tile, head, read, Budget, out units, out reason) && units.Count == 2, "duplicate delta cells deduplicate");
            // Return the same packed tile for a different valid coordinate: this is not a coherent map view.
            var alias = new[] { new RescueTile(2, 2, center), new RescueTile(3, 2, center) };
            Require(!Collect(alias, (x, y) => x == 3 && y == 2 ? center : Tile(x, y), head, read, Budget, out units, out reason) &&
                reason == "aliased-mutation-coordinates", "coordinate alias rejected");
            SharedGraph();DenseGraph();
        }
        private static void SharedGraph()
        {
            int center=Tile(2,2),adjacent=Tile(1,2),other=Tile(4,4);
            var changed=new[]{new RescueTile(2,2,center)};
            var records=new Dictionary<int,RescueDiscoveryUnit>{
                {1,new RescueDiscoveryUnit(1,11,center,center,3,false)},
                // Mentioned from adjacent head, but NOT reachable from head(Current=center).
                {2,new RescueDiscoveryUnit(2,12,center,center,3,false)},
                {3,new RescueDiscoveryUnit(3,13,adjacent,center,4,true)},
                {4,new RescueDiscoveryUnit(4,14,other,center,0,true)}};
            Func<int,int> heads=t=>t==center?1:t==adjacent?2:0;
            int reads=0,headReads=0;
            RescueDiscoveryUnitReader read=delegate(int id,out RescueDiscoveryUnit unit){reads++;return records.TryGetValue(id,out unit);};
            var exact=new RescueDiscoveryBudget(9,8,8,9999,1000);
            Require(Collect(changed,Tile,t=>{headReads++;return heads(t);},read,exact,out var units,out var reason) &&
                units.Count==2 && units[0].UnitId==1 && units[1].UnitId==3,"shared-tail diamond uses actual own-head ancestor membership");
            Require(reads==8 && headReads==9,"shared-tail records read twice and every halo head once");
            reads=0;
            Require(!Collect(changed,Tile,heads,read,new RescueDiscoveryBudget(9,7,8,9999,1000),out units,out reason) &&
                units.Count==0 && reason=="occupancy-walk-budget" && reads==7,"budget counts actual reads including second validation pass");
            // A late first-pass record can expose a change to an earlier node even
            // when no other head ever repeats that earlier node.
            var original=records[1];reads=0;
            RescueDiscoveryUnitReader lastChangesFirst=delegate(int id,out RescueDiscoveryUnit unit){
                reads++;bool found=records.TryGetValue(id,out unit);
                if(reads==4)records[1]=new RescueDiscoveryUnit(1,999,center,center,3,false);return found;};
            Require(!Collect(changed,Tile,heads,lastChangesFirst,exact,out units,out reason) && units.Count==0 &&
                reason=="occupant-changed-during-discovery","last first-pass record mutation invalidates earlier unique node");records[1]=original;
            foreach(var mutated in new[]{
                new RescueDiscoveryUnit(4,99,other,center,0,true),new RescueDiscoveryUnit(4,14,center,center,0,true),
                new RescueDiscoveryUnit(4,14,other,adjacent,0,true),new RescueDiscoveryUnit(4,14,other,center,1,true),
                new RescueDiscoveryUnit(4,14,other,center,0,false)})
            {
                int lastReads=0;
                RescueDiscoveryUnitReader change=delegate(int id,out RescueDiscoveryUnit unit){bool found=records.TryGetValue(id,out unit);if(id==4 && ++lastReads==2)unit=mutated;return found;};
                Require(!Collect(changed,Tile,heads,change,exact,out units,out reason) && units.Count==0 &&
                    reason=="occupant-changed-during-discovery","all record fields revalidated including last unique node");
            }
            records[4]=new RescueDiscoveryUnit(4,14,other,center,3,true);
            Require(!Collect(changed,Tile,heads,read,exact,out units,out reason) && units.Count==0 &&
                reason=="invalid-or-cyclic-occupancy-chain","shared suffix cycle is not accepted as a known valid tail");
            records[4]=new RescueDiscoveryUnit(4,14,other,center,0,true);
            reads=0;headReads=0;
            Require(Collect(changed,Tile,t=>{headReads++;return 0;},read,exact,out units,out _) &&
                reads==0 && headReads==9 && units.Count==0,"empty heads cause no record reads or second-pass work");
            Require(!Collect(changed,Tile,_=>-1,read,exact,out units,out reason) &&
                reason=="invalid-or-cyclic-occupancy-chain","negative head rejected before record access");
            RescueDiscoveryUnitReader throwing=delegate(int id,out RescueDiscoveryUnit unit){unit=default;throw new InvalidOperationException();};
            Require(!Collect(changed,Tile,heads,throwing,exact,out units,out reason) && units.Count==0 &&
                reason=="discovery-reader-failed","record reader exceptions remain atomic");
        }
        private static void DenseGraph()
        {
            int center=Tile(2,2),reads=0,headReads=0;
            var changed=new[]{new RescueTile(2,2,center)};
            const int maximum=9999;
            RescueDiscoveryUnitReader read=delegate(int id,out RescueDiscoveryUnit record){
                reads++;record=new RescueDiscoveryUnit(id,(uint)(10000+id),center,center,id<maximum?id+1:0,false);return true;};
            var exact=new RescueDiscoveryBudget(9,2*maximum,maximum,maximum,1000);
            Require(Collect(changed,Tile,_=>{headReads++;return 1;},read,exact,out var units,out var reason) &&
                units.Count==maximum && units[0].UnitId==1 && units[maximum-1].UnitId==maximum,"full native ID domain with nine shared heads remains complete");
            Require(reads==2*maximum && headReads==9,"9999 density costs exactly19998 record reads rather than89991 repeated mentions");
            reads=0;
            Require(!Collect(changed,Tile,_=>1,read,new RescueDiscoveryBudget(9,2*maximum-1,maximum,maximum,1000),out units,out reason) &&
                units.Count==0 && reads==2*maximum-1 && reason=="occupancy-walk-budget","full domain respects exact smaller reader budget");
            reads=0;
            Require(!Collect(changed,Tile,_=>1,read,new RescueDiscoveryBudget(9,2*maximum,512,maximum,1000),out units,out reason) &&
                units.Count==0 && reads==2*maximum && reason=="occupant-snapshot-budget","all graph validity closes before bounded selected snapshot admission");
            // Deep chains must use an explicit traversal stack, never managed recursion.
            reads=0;
            RescueDiscoveryUnitReader cycle=delegate(int id,out RescueDiscoveryUnit record){reads++;record=new RescueDiscoveryUnit(id,(uint)id,center,center,id<maximum?id+1:maximum-1,false);return true;};
            Require(!Collect(changed,Tile,_=>1,cycle,exact,out units,out reason) && units.Count==0 && reads==maximum &&
                reason=="invalid-or-cyclic-occupancy-chain","late deep cycle fails after one read per unique node");
        }
        private static void PoolLifecycle()
        {
            var pool = new RescueDiscovery.ScratchPool();
            int center = Tile(2, 2), neighbor = Tile(3, 2), other = Tile(4, 4);
            var changed = new[] { new RescueTile(2, 2, center) };
            var heads = new Dictionary<int, int> { { center, 9 } };
            var records = new Dictionary<int, RescueDiscoveryUnit> {
                { 9, new RescueDiscoveryUnit(9, 19, center, center, 2, false) },
                { 2, new RescueDiscoveryUnit(2, 12, center, center, 0, false) } };
            Func<int, int> head = tile => heads.TryGetValue(tile, out int id) ? id : 0;
            RescueDiscoveryUnitReader read = delegate(int id, out RescueDiscoveryUnit unit) { return records.TryGetValue(id, out unit); };
            Require(pool.TryCollect(changed, Tile, head, read, Budget, out var original, out var reason) && original.Count == 2,
                "pooled first graph is complete: " + reason);
            object scratch = FreeScratch(pool);
            Require(scratch != null, "runtime-owned pool retains at most one empty scratch");
            RequireEmptyScratch(scratch);
            heads[center] = 7; records.Clear();
            records[7] = new RescueDiscoveryUnit(7, 77, center, center, 0, false);
            Require(pool.TryCollect(changed, Tile, head, read, Budget, out var next, out reason) && next.Count == 1 &&
                next[0].UnitId == 7 && next[0].GlobalId == 77 && !ReferenceEquals(original, next) && ReferenceEquals(scratch, FreeScratch(pool)),
                "new heads, links and GIDs replace all prior graph data: " + reason);
            var nextChanged = new[] { new RescueTile(3, 2, neighbor) };
            Require(pool.TryCollect(nextChanged, Tile, head, read, Budget, out next, out reason) && next.Count == 0,
                "changed delta does not inherit the preceding Current tile: " + reason);
            heads.Clear(); heads[neighbor] = 7;
            records[7] = new RescueDiscoveryUnit(7, 777, neighbor, center, 0, true);
            Require(pool.TryCollect(changed, Tile, head, read, Budget, out next, out reason) && next.Count == 1 &&
                next[0].CurrentTile == neighbor && next[0].GlobalId == 777 && next[0].HasOrdinaryNext,
                "new Current/Next and lifetime values are read and revalidated afresh: " + reason);
            Require(original.Count == 2 && original[0].UnitId == 2 && original[0].GlobalId == 12 &&
                original[1].UnitId == 9 && original[1].GlobalId == 19,
                "previous output remains a fresh independent value-record list");
            int calls = 0;
            RescueDiscoveryUnitReader throwing = delegate(int id, out RescueDiscoveryUnit unit) {
                unit = default; if (++calls == 2) throw new InvalidOperationException("second pass");
                return records.TryGetValue(id, out unit); };
            Require(!pool.TryCollect(changed, Tile, head, throwing, Budget, out next, out reason) && next.Count == 0 &&
                reason == "discovery-reader-failed", "reader failure cannot publish a partial pooled capture");
            RequireEmptyScratch(FreeScratch(pool));
            Require(pool.TryCollect(changed, Tile, head, read, Budget, out next, out reason) && next.Count == 1 && next[0].GlobalId == 777,
                "next successful invocation is clean after a failed reader: " + reason);
            Require(pool.TryCollect(changed, Tile, _ => 0, read, Budget, out next, out reason) && next.Count == 0,
                "empty capture cannot inherit heads or graph from a nonempty predecessor: " + reason);
            RequireEmptyScratch(FreeScratch(pool));

            // Reentry is triggered by the reader delegate after the outer lease and
            // captures a different map view through the same runtime-owned pool.
            object outerScratch = FreeScratch(pool), nestedScratch = null;
            List<RescueDiscoveryUnit> nestedResult = null;
            bool entered = false;
            RescueDiscoveryUnitReader nestedRead = delegate(int id, out RescueDiscoveryUnit unit) {
                unit = new RescueDiscoveryUnit(3, 333, other, other, 0, false); return id == 3; };
            Func<int, int> reentryHead = tile =>
            {
                if (!entered)
                {
                    entered = true;
                    Require(FreeScratch(pool) == null, "active outer scratch is not in the free slot");
                    Require(pool.TryCollect(new[] { new RescueTile(4, 4, other) }, Tile, t => t == other ? 3 : 0,
                        nestedRead, Budget, out nestedResult, out var nestedReason) && nestedResult.Count == 1 && nestedResult[0].GlobalId == 333,
                        "nested discovery owns an isolated graph: " + nestedReason);
                    nestedScratch = FreeScratch(pool);
                    Require(nestedScratch != null && !ReferenceEquals(outerScratch, nestedScratch), "nested scratch cannot alias the outer lease");
                }
                return head(tile);
            };
            Require(pool.TryCollect(changed, Tile, reentryHead, read, Budget, out next, out reason) && next.Count == 1 && next[0].GlobalId == 777,
                "outer two-pass capture survives nested discovery: " + reason);
            Require(entered && nestedResult.Count == 1 && nestedResult[0].GlobalId == 333 && ReferenceEquals(nestedScratch, FreeScratch(pool)),
                "first returned empty scratch occupies the sole free slot and neither output is recycled");
            RequireEmptyScratch(FreeScratch(pool));
            // Retention follows the caller's existing work domain. This does not
            // weaken the exact halo rejection or add an arbitrary new size cutoff.
            Require(!pool.TryCollect(changed, Tile, head, read, new RescueDiscoveryBudget(8, 50, 8, 9999, 1000), out next, out reason) &&
                next.Count == 0 && reason == "occupancy-cell-budget" && FreeScratch(pool) == null,
                "over-budget scratch is cleared and discarded without changing admission");
            Require(pool.TryCollect(changed, Tile, head, read, Budget, out next, out reason) && next.Count == 1 && FreeScratch(pool) != null,
                "valid capture obtains fresh scratch after an over-budget failure: " + reason);
            RequireEmptyScratch(FreeScratch(pool));
        }
        // Reflection checks only the explicit private resource contract (one empty
        // free slot). All map/identity/graph correctness runs through production code.
        private static object FreeScratch(RescueDiscovery.ScratchPool pool) => typeof(RescueDiscovery.ScratchPool)
            .GetField("free", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(pool);
        private static void RequireEmptyScratch(object scratch)
        {
            Require(scratch != null, "empty discovery scratch is available");
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public;
            var type = scratch.GetType();
            foreach (string name in new[] { "Delta", "Halo", "Heads", "Records", "OccupiedTiles", "Path", "Ids", "Roots", "Stack" })
            {
                object value = type.GetField(name, fields).GetValue(scratch);
                Require((int)value.GetType().GetProperty("Count").GetValue(value, null) == 0,
                    "returned discovery scratch clears all values in " + name);
            }
        }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
