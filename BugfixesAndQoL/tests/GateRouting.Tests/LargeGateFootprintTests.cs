using APIShared.GateRouting.Native;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.GateRouting
{
    internal static class LargeGateFootprintTests
    {
        private static int assertions;
        private static void Check(bool value, string reason)
        { assertions++; if (!value) throw new Exception("Large gate: " + reason); }
        private static int Id(int x, int y) => x < 0 || y < 0 || x >= 100 || y >= 100 ? -1 : y * 100 + x;
        internal static int Run()
        {
            assertions = 0;
            var tiles = new Dictionary<int, int>();
            var seeds = new List<ConnectedGateFootprint.Cell>();
            for (int y = 30; y < 37; y++) for (int x = 20; x < 27; x++)
            {
                tiles.Add(Id(x, y), 2);
                if (seeds.Count < 36) seeds.Add(new ConnectedGateFootprint.Cell(Id(x, y), x, y));
            }
            // Touching foreign building must not enlarge the gate.
            tiles[Id(27, 30)] = 3;
            Func<int, int, ConnectedGateFootprint.Cell?> read = (x, y) =>
                tiles.TryGetValue(Id(x, y), out int b) && b == 2 ? new ConnectedGateFootprint.Cell(Id(x,y), x,y) : (ConnectedGateFootprint.Cell?)null;
            Check(ConnectedGateFootprint.TryCollect(2, seeds, read, out var cells) && cells.Length == 49, "36 seeds recover 49 tiles");
            var summary = SparseFootprintAccumulator.ReconstructedLargeGate();
            for (int i = 0; i < cells.Length; i++) summary.AddCell(i, (uint)cells[i].Id, true, cells[i].X, cells[i].Y);
            Check(summary.IsValid && summary.ValidTileCount == 49 && summary.MinX == 20 && summary.MaxY == 36, "full summary");
            seeds.Reverse();
            Check(ConnectedGateFootprint.TryCollect(2, seeds, read, out var reordered), "seed ordering independent");
            for (int i = 0; i < cells.Length; i++) Check(cells[i].Id == reordered[i].Id, "deterministic signature order");
            tiles.Remove(Id(26,36));
            Check(!ConnectedGateFootprint.TryCollect(2,seeds,read,out _), "missing last non-inline tile fails open");
            tiles[Id(26,36)] = 2; tiles[Id(27,30)] = 2;
            Check(!ConnectedGateFootprint.TryCollect(2,seeds,read,out _), "oversized component rejected");
            tiles[Id(27,30)] = 3;
            var stale = new List<ConnectedGateFootprint.Cell>(seeds) { new ConnectedGateFootprint.Cell(Id(60,60),60,60) };
            stale.RemoveAt(0);
            Check(!ConnectedGateFootprint.TryCollect(2,stale,read,out _), "disconnected stale seed rejected");
            int calls = 0;
            Check(!ConnectedGateFootprint.TryCollect(2,seeds,(x,y) => { calls++; return calls > 197 && x == 26 && y == 36 ? null : read(x,y); },out _), "change during final grid reread rejected");
            Check(!ConnectedGateFootprint.TryCollect(0,seeds,read,out _), "invalid building id");
            Check(!ConnectedGateFootprint.TryCollect(2,new List<ConnectedGateFootprint.Cell>(),read,out _), "no seeds");
            var identity = new GateFootprintIdentity(2,3,45,1,1,0,7,20,30);
            Check(identity.Matches(identity), "stable identity");
            Check(!identity.Matches(new GateFootprintIdentity(2,4,45,1,1,0,7,20,30)), "reused slot");
            Check(!identity.Matches(new GateFootprintIdentity(2,3,45,1,1,2,7,20,30)), "capture change");
            Check(!identity.Matches(new GateFootprintIdentity(2,3,45,1,1,0,7,21,30)), "moved building");
            Check(!identity.Matches(new GateFootprintIdentity(2,3,45,0,1,0,7,20,30)), "demolition");
            Check(!identity.Matches(new GateFootprintIdentity(2,3,46,1,1,0,5,20,30)), "replacement type");
            foreach (int size in new[] {5,7}) for (int orientation = 0; orientation < 4; orientation++)
                CheckMask(size, orientation);
            // Exactly reproduced large-gate door coordinates from the failing log.
            byte[] nativeMap = new byte[320800]; for(int i=0;i<nativeMap.Length;i++) nativeMap[i]=255;
            int changed = GatePassageMask.ClearGatehouseOuterDirections((x,y)=> y*500+x,
                408,348,414,354,411,355,411,347,false,nativeMap,new GateEdgeOwnership(),2,
                out _,out _,out _,out _,out _,out _);
            Check(changed == 84, "observed large gate receives 84 directed restrictions");
            var masks = new byte[9][];
            var identities = new Dictionary<int, RouteTilePolicySnapshot.GateIdentity> { [2] = new RouteTilePolicySnapshot.GateIdentity(3,1,0) };
            var missing = new RouteTilePolicySnapshot(masks,1,0,1,null,null,identities,null,new[]{2});
            Check(missing.DescribeGateMask(2,3,1,0)=="missing-required-mask", "coverage independent of owner policy");
            Check(missing.DescribeGateMask(2,4,1,0)=="unknown-identity", "coverage identity check");
            // An edge-of-map passage cannot publish only its surviving boundary.
            byte[] border = new byte[10000]; for(int i=0;i<border.Length;i++)border[i]=255;
            Check(GatePassageMask.ClearGatehouseOuterDirections(Id,0,10,6,16,-1,13,7,13,true,border,new GateEdgeOwnership(),2,
                out _,out _,out _,out _,out _,out _)==0,"invalid boundary rejects whole mask");
            Check(Array.TrueForAll(border, value => value == 255),"failed boundary leaves bytes untouched");
            return assertions;
        }
        private static void CheckMask(int size, int orientation)
        {
            bool horizontal = (orientation & 1) == 0;
            int lo=20, hi=lo+size-1, mid=lo+size/2;
            int ex=horizontal ? lo-1 : mid, ey=horizontal ? mid : lo-1;
            int xx=horizontal ? hi+1 : mid, xy=horizontal ? mid : hi+1;
            if (orientation>=2) { int t=ex;ex=xx;xx=t;t=ey;ey=xy;xy=t; }
            var mask=new byte[10000]; for(int i=0;i<mask.Length;i++)mask[i]=255;
            var owners=new GateEdgeOwnership();
            int changed=GatePassageMask.ClearGatehouseOuterDirections(Id,lo,lo,hi,hi,ex,ey,xx,xy,horizontal,mask,owners,2,
                out _,out _,out _,out _,out _,out _);
            Check(changed==size*12,"both boundaries covered in orientation "+orientation);
            int dir=horizontal?2:4;
            for(int offset=lo;offset<=hi;offset++)
            {
                int from=horizontal?Id(lo-1,offset):Id(offset,lo-1);
                int to=horizontal?Id(lo,offset):Id(offset,lo);
                for(int d=-1;d<=1;d++)
                {
                    Check((mask[from]&(1<<((dir+d)&7)))==0,"forward cardinal/diagonal blocked");
                    Check((mask[to]&(1<<((dir+4+d)&7)))==0,"reverse cardinal/diagonal blocked");
                }
            }
            var players=new byte[9][]; players[2]=mask;
            var snapshot=new RouteTilePolicySnapshot(players,1);
            int entry=horizontal?Id(lo-1,mid):Id(mid,lo-1);
            Check(!snapshot.IsDirectionAllowed(2,entry,dir),"hostile ground passage rejected");
            Check(snapshot.IsDirectionAllowed(1,entry,dir),"own/permitted player has no mask");
            Check(snapshot.IsDirectionAllowed(2,Id(lo-3,lo-3),dir),"real path outside gate remains allowed");
            int start = horizontal ? Id(lo-2,mid) : Id(mid,lo-2);
            int end = horizontal ? Id(hi+2,mid) : Id(mid,hi+2);
            Func<int,int,bool> walkable = (x,y) => x>=lo-3 && y>=lo-3 && x<=hi+3 && y<=hi+3 &&
                (!(x>=lo && x<=hi && y>=lo && y<=hi) || (horizontal ? y==mid : x==mid));
            int direct=Distance(snapshot,1,start,end,walkable);
            int detour=Distance(snapshot,2,start,end,walkable);
            Check(direct==size+3 && detour>direct,"actual mask forces usable longer path");
            Check(Distance(snapshot,2,start,end,(x,y)=>walkable(x,y)&&(horizontal?y==mid:x==mid))<0,
                "gate-only ground route is unreachable");
            Check(Distance(snapshot,1,start,end,(x,y)=>walkable(x,y)&&(horizontal?y==mid:x==mid))==direct,
                "permitted player retains direct route");
            // Clearing again is idempotent and preserves all unrelated tiles.
            Check(GatePassageMask.ClearGatehouseOuterDirections(Id,lo,lo,hi,hi,ex,ey,xx,xy,horizontal,mask,owners,2,
                out _,out _,out _,out _,out _,out _)==0,"idempotent");
            Check(mask[Id(hi+3,hi+3)]==255,"neighbor area unmodified");
        }
        private static int Distance(RouteTilePolicySnapshot snapshot, int player, int start, int end, Func<int,int,bool> walkable)
        {
            var pending=new Queue<int>(); var distances=new Dictionary<int,int>();
            pending.Enqueue(start); distances.Add(start,0);
            int[] dx={0,1,0,-1}, dy={-1,0,1,0}, directions={0,2,4,6};
            while(pending.Count>0)
            {
                int cell=pending.Dequeue(); if(cell==end)return distances[cell];
                for(int n=0;n<4;n++)
                {
                    int x=cell%100+dx[n], y=cell/100+dy[n], next=Id(x,y);
                    if(next<0 || !walkable(x,y) || distances.ContainsKey(next) ||
                        !snapshot.IsDirectionAllowed(player,cell,directions[n]) ||
                        !snapshot.IsDirectionAllowed(player,next,(directions[n]+4)&7))continue;
                    distances.Add(next,distances[cell]+1);pending.Enqueue(next);
                }
            }
            return -1;
        }
    }
}
