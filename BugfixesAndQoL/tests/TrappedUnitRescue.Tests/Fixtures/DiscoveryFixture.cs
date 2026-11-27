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
            SharedGraph();DenseGraph();
            Console.WriteLine("PASS: ordinary Current/Next discovery checks=" + checks + "; production pure discovery, no native calls.");
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
            Require(RescueDiscovery.TryCollect(changed,Tile,t=>{headReads++;return heads(t);},read,exact,out var units,out var reason) &&
                units.Count==2 && units[0].UnitId==1 && units[1].UnitId==3,"shared-tail diamond uses actual own-head ancestor membership");
            Require(reads==8 && headReads==9,"shared-tail records read twice and every halo head once");
            reads=0;
            Require(!RescueDiscovery.TryCollect(changed,Tile,heads,read,new RescueDiscoveryBudget(9,7,8,9999,1000),out units,out reason) &&
                units.Count==0 && reason=="occupancy-walk-budget" && reads==7,"budget counts actual reads including second validation pass");
            // A late first-pass record can expose a change to an earlier node even
            // when no other head ever repeats that earlier node.
            var original=records[1];reads=0;
            RescueDiscoveryUnitReader lastChangesFirst=delegate(int id,out RescueDiscoveryUnit unit){
                reads++;bool found=records.TryGetValue(id,out unit);
                if(reads==4)records[1]=new RescueDiscoveryUnit(1,999,center,center,3,false);return found;};
            Require(!RescueDiscovery.TryCollect(changed,Tile,heads,lastChangesFirst,exact,out units,out reason) && units.Count==0 &&
                reason=="occupant-changed-during-discovery","last first-pass record mutation invalidates earlier unique node");records[1]=original;
            foreach(var mutated in new[]{
                new RescueDiscoveryUnit(4,99,other,center,0,true),new RescueDiscoveryUnit(4,14,center,center,0,true),
                new RescueDiscoveryUnit(4,14,other,adjacent,0,true),new RescueDiscoveryUnit(4,14,other,center,1,true),
                new RescueDiscoveryUnit(4,14,other,center,0,false)})
            {
                int lastReads=0;
                RescueDiscoveryUnitReader change=delegate(int id,out RescueDiscoveryUnit unit){bool found=records.TryGetValue(id,out unit);if(id==4 && ++lastReads==2)unit=mutated;return found;};
                Require(!RescueDiscovery.TryCollect(changed,Tile,heads,change,exact,out units,out reason) && units.Count==0 &&
                    reason=="occupant-changed-during-discovery","all record fields revalidated including last unique node");
            }
            records[4]=new RescueDiscoveryUnit(4,14,other,center,3,true);
            Require(!RescueDiscovery.TryCollect(changed,Tile,heads,read,exact,out units,out reason) && units.Count==0 &&
                reason=="invalid-or-cyclic-occupancy-chain","shared suffix cycle is not accepted as a known valid tail");
            records[4]=new RescueDiscoveryUnit(4,14,other,center,0,true);
            reads=0;headReads=0;
            Require(RescueDiscovery.TryCollect(changed,Tile,t=>{headReads++;return 0;},read,exact,out units,out _) &&
                reads==0 && headReads==9 && units.Count==0,"empty heads cause no record reads or second-pass work");
            Require(!RescueDiscovery.TryCollect(changed,Tile,_=>-1,read,exact,out units,out reason) &&
                reason=="invalid-or-cyclic-occupancy-chain","negative head rejected before record access");
            RescueDiscoveryUnitReader throwing=delegate(int id,out RescueDiscoveryUnit unit){unit=default;throw new InvalidOperationException();};
            Require(!RescueDiscovery.TryCollect(changed,Tile,heads,throwing,exact,out units,out reason) && units.Count==0 &&
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
            Require(RescueDiscovery.TryCollect(changed,Tile,_=>{headReads++;return 1;},read,exact,out var units,out var reason) &&
                units.Count==maximum && units[0].UnitId==1 && units[maximum-1].UnitId==maximum,"full native ID domain with nine shared heads remains complete");
            Require(reads==2*maximum && headReads==9,"9999 density costs exactly19998 record reads rather than89991 repeated mentions");
            reads=0;
            Require(!RescueDiscovery.TryCollect(changed,Tile,_=>1,read,new RescueDiscoveryBudget(9,2*maximum-1,maximum,maximum,1000),out units,out reason) &&
                units.Count==0 && reads==2*maximum-1 && reason=="occupancy-walk-budget","full domain respects exact smaller reader budget");
            reads=0;
            Require(!RescueDiscovery.TryCollect(changed,Tile,_=>1,read,new RescueDiscoveryBudget(9,2*maximum,512,maximum,1000),out units,out reason) &&
                units.Count==0 && reads==2*maximum && reason=="occupant-snapshot-budget","all graph validity closes before bounded selected snapshot admission");
            // Deep chains must use an explicit traversal stack, never managed recursion.
            reads=0;
            RescueDiscoveryUnitReader cycle=delegate(int id,out RescueDiscoveryUnit record){reads++;record=new RescueDiscoveryUnit(id,(uint)id,center,center,id<maximum?id+1:maximum-1,false);return true;};
            Require(!RescueDiscovery.TryCollect(changed,Tile,_=>1,cycle,exact,out units,out reason) && units.Count==0 && reads==maximum &&
                reason=="invalid-or-cyclic-occupancy-chain","late deep cycle fails after one read per unique node");
        }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
