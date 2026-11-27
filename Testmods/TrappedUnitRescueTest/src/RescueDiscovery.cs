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
        // MaximumLinks bounds actual unit-record reader calls across BOTH passes,
        // not repeated mentions of an already captured shared chain suffix.
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
        private sealed class Node
        {
            internal readonly RescueDiscoveryUnit Record;
            internal byte State=1; // grey while on the current first-pass path
            internal int FirstChild,NextSibling,ChildCursor,Enter,Exit;
            internal Node(RescueDiscoveryUnit record){Record=record;}
        }
        internal static bool TryCollect(IList<RescueTile> changed, Func<int, int, int> tileAt,
            Func<int, int> head, RescueDiscoveryUnitReader read, RescueDiscoveryBudget budget,
            out List<RescueDiscoveryUnit> units, out string reason)
        {
            units=new List<RescueDiscoveryUnit>();reason=null;
            if(changed==null || tileAt==null || head==null || read==null || budget.MaximumCells<1 || budget.MaximumLinks<1 ||
                budget.MaximumUnits<1 || budget.MaximumUnitId<1 || budget.TileCapacity<1)
            {reason="invalid-discovery-input";return false;}
            if(changed.Count>budget.MaximumCells){reason="mutation-cell-budget";return false;}
            var delta=new Dictionary<int,RescueTile>();var halo=new SortedSet<int>();
            var heads=new Dictionary<int,int>();var records=new Dictionary<int,Node>();
            int visits=0,selectedCount=0;
            try
            {
                foreach(var tile in changed)
                {
                    if(!ValidTile(tile.TileId,budget) || tileAt(tile.X,tile.Y)!=tile.TileId)
                    {reason="invalid-mutation-tile";return false;}
                    if(delta.TryGetValue(tile.TileId,out var old) && (old.X!=tile.X || old.Y!=tile.Y))
                    {reason="aliased-mutation-coordinates";return false;}
                    delta[tile.TileId]=tile;
                }
                foreach(var tile in delta.Values)
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        long x=(long)tile.X+dx,y=(long)tile.Y+dy;
                        if(x<int.MinValue || x>int.MaxValue || y<int.MinValue || y>int.MaxValue)continue;
                        int id=tileAt((int)x,(int)y);if(id<0)continue;
                        if(!ValidTile(id,budget)){reason="invalid-halo-tile";return false;}
                        halo.Add(id);if(halo.Count>budget.MaximumCells){reason="occupancy-cell-budget";return false;}
                    }
                // Each tile head is captured once. Auxiliary heads may share any
                // suffix: native8FA is one outgoing link per physical unit record.
                foreach(int tile in halo)
                {
                    int id=head(tile);
                    if(id<0 || id>budget.MaximumUnitId){reason="invalid-or-cyclic-occupancy-chain";return false;}
                    heads.Add(tile,id);
                }
                List<int> path=null;
                foreach(int tile in halo)
                {
                    int id=heads[tile];if(id==0)continue;
                    if(path==null)path=new List<int>();else path.Clear();
                    while(id!=0)
                    {
                        if(records.TryGetValue(id,out var known))
                        {
                            if(known.State==1){reason="invalid-or-cyclic-occupancy-chain";return false;}
                            break;
                        }
                        if(!TryRead(id,read,budget,ref visits,out var record,out reason))return false;
                        records.Add(id,new Node(record));path.Add(id);id=record.ChainNextId;
                    }
                    foreach(int seen in path)records[seen].State=2;
                }
                if(records.Count==0)return true; // no capacity-sized work arrays for empty worlds
                var ids=new List<int>(records.Keys);ids.Sort();
                // Revalidate EVERY unique node once, including a suffix seen from
                // only one head. No old/new record mixture is published on failure.
                foreach(int id in ids)
                {
                    if(!TryRead(id,read,budget,ref visits,out var current,out reason))return false;
                    if(!Same(records[id].Record,current)){reason="occupant-changed-during-discovery";return false;}
                }
                // Reverse the acyclic functional graph into a forest rooted at
                // terminal Next0. u is on head's original forward chain iff u is
                // an ancestor of head in this forest (Enter[u]<=Enter[h]<Exit[u]).
                var roots=new List<int>();
                foreach(int id in ids)
                {
                    var node=records[id];int parent=node.Record.ChainNextId;
                    if(parent==0)roots.Add(id);
                    else {node.NextSibling=records[parent].FirstChild;records[parent].FirstChild=id;}
                }
                int clock=0;var stack=new Stack<int>();
                foreach(int root in roots)
                {
                    var first=records[root];first.Enter=clock++;first.ChildCursor=first.FirstChild;stack.Push(root);
                    while(stack.Count!=0)
                    {
                        var node=records[stack.Peek()];int child=node.ChildCursor;
                        if(child==0){node.Exit=clock;stack.Pop();continue;}
                        var next=records[child];node.ChildCursor=next.NextSibling;
                        next.Enter=clock++;next.ChildCursor=next.FirstChild;stack.Push(child);
                    }
                }
                var selected=new List<RescueDiscoveryUnit>();
                foreach(int id in ids)
                {
                    var node=records[id];var record=node.Record;
                    if(!(delta.ContainsKey(record.CurrentTile) || record.HasOrdinaryNext && delta.ContainsKey(record.NextTile)) ||
                        !heads.TryGetValue(record.CurrentTile,out int ownHead) || ownHead==0)continue;
                    var first=records[ownHead];
                    if(node.Enter>first.Enter || first.Enter>=node.Exit)continue;
                    selected.Add(record);selectedCount=selected.Count;
                    if(selected.Count>budget.MaximumUnits){reason="occupant-snapshot-budget";return false;}
                }
                units=selected;return true;
            }
            catch
            {units.Clear();reason="discovery-reader-failed";return false;}
#if SERP_DEVDIAG
            finally{RescueCaptureDiagnostics.Discovery(halo.Count,visits,selectedCount);}
#endif
        }
        private static bool TryRead(int id,RescueDiscoveryUnitReader read,RescueDiscoveryBudget budget,
            ref int visits,out RescueDiscoveryUnit record,out string reason)
        {
            record=default;reason=null;
            if(visits>=budget.MaximumLinks){reason="occupancy-walk-budget";return false;}
            visits++;
            if(!read(id,out record) || record.UnitId!=id || record.GlobalId==0 || !ValidTile(record.CurrentTile,budget) ||
                record.ChainNextId<0 || record.ChainNextId>budget.MaximumUnitId ||
                record.HasOrdinaryNext && !ValidTile(record.NextTile,budget))
            {reason="invalid-occupant-record";return false;}
            return true;
        }
        private static bool ValidTile(int tile, RescueDiscoveryBudget budget) => tile >= 0 && tile < budget.TileCapacity;
        private static bool Same(RescueDiscoveryUnit a, RescueDiscoveryUnit b) => a.UnitId == b.UnitId && a.GlobalId == b.GlobalId &&
            a.CurrentTile == b.CurrentTile && a.NextTile == b.NextTile && a.ChainNextId == b.ChainNextId && a.HasOrdinaryNext == b.HasOrdinaryNext;
    }
}
