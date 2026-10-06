using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
namespace EnemyBridgePathTest
{
    // Accessed under the trace capture gate; event publishers only invalidate it.
    internal sealed unsafe class BridgeBuildingIndex
    {
        internal sealed class Entry
        {
            internal int Id, ParentId, Owner, NativeParent, Grid, Orientation, OriginX, OriginY;
            internal uint Global, ParentGlobal;
            internal int MinX=800,MinY=800,MaxX=-1,MaxY=-1;
            internal string Link;
        }
        internal static int SpanIndex(int gameId,int capacity) => gameId>0&&gameId<=capacity?gameId-1:-1;
        internal static bool IsCurrent(Entry entry,uint global,int owner,int parentRaw,int grid,int orientation,int originX=-1,int originY=-1) =>
            entry.Global==global&&entry.Owner==owner&&entry.NativeParent==parentRaw&&entry.Grid==grid&&entry.Orientation==orientation&&(originX<0||entry.OriginX==originX)&&(originY<0||entry.OriginY==originY);
        private long generation=1, built, nextCheck;
        internal long Builds;
        internal long BuildTicks;
        internal Entry[] Entries=Array.Empty<Entry>();
        internal void Invalidate() => Interlocked.Increment(ref generation);
        internal void Refresh()
        {
            long now=Stopwatch.GetTimestamp(), version=Interlocked.Read(ref generation);
            if(built==version&&now<nextCheck) return;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            var gates=new List<Entry>(); var bridges=new List<Entry>(); var footprints=new Dictionary<int,HashSet<int>>();
            for(int i=0;i<buildings.Length;i++)
            {
                ref GameBuilding b=ref buildings[i];
                if(!BridgeSnapshot.Active(b.r_AliveState)||(!BridgeSnapshot.IsGate(b.r_BuildingType)&&b.r_BuildingType!=eStructs.STRUCT_DRAWBRIDGE)) continue;
                var entry=new Entry {Id=i+1,Global=b.r_GlobalId,Owner=b.r_PlayerIdOwner,NativeParent=b.r_GatehouseId,Grid=(int)b.r_OccupyTileGridSize,Orientation=b.r_SpriteVariationIndex,OriginX=b.r_TilePositionXBegin,OriginY=b.r_TilePositionYBegin};
                fixed(GameBuilding* pointer=&b) footprints.Add(entry.Id,BridgeSnapshot.Footprint(pointer));
                foreach(int tile in footprints[entry.Id])
                {
                    if((uint)tile>=320800)continue;
                    var pos=GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
                    entry.MinX=Math.Min(entry.MinX,pos.X);entry.MaxX=Math.Max(entry.MaxX,pos.X);
                    entry.MinY=Math.Min(entry.MinY,pos.Y);entry.MaxY=Math.Max(entry.MaxY,pos.Y);
                }
                if(b.r_BuildingType==eStructs.STRUCT_DRAWBRIDGE) bridges.Add(entry); else gates.Add(entry);
            }
            // r_GatehouseId is a connection-record field, never a parent Building-ID.
            // Reuse the mainmod's exact B9330 perimeter order and first-two rule.
            var eligible=new HashSet<int>();foreach(var bridge in bridges)if(buildings[bridge.Id-1].r_AliveState==AliveState.IsAlive)eligible.Add(bridge.Id);
            var links=new Dictionary<int,List<Entry>>();var tileApi=GameTileManagerAPI.Instance;
            int BuildingAt(int x,int y)
            {if(!tileApi.IsTileInsideMapBounds(x,y))return 0;int tile=tileApi.GetTileId(x,y);return tileApi.IsValidTileId(tile)?tileApi.GetTileBuildingId(tile):0;}
            foreach(var gate in gates)
            {
                if(gate.Grid<=0||gate.Grid>7||buildings[gate.Id-1].r_AliveState!=AliveState.IsAlive)continue;
                var candidates=GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(gate.OriginX,gate.OriginY,gate.Grid);
                foreach(int id in GatehouseDrawbridgeCoupling.CollectFirstDistinctBuildingIds(candidates,BuildingAt,eligible.Contains))
                {if(!links.TryGetValue(id,out List<Entry> parents))links[id]=parents=new List<Entry>();parents.Add(gate);}
            }
            foreach(var bridge in bridges)
            {
                if(links.TryGetValue(bridge.Id,out List<Entry> parents)&&parents.Count==1&&parents[0].Global!=0)
                {bridge.ParentId=parents[0].Id;bridge.ParentGlobal=parents[0].Global;bridge.Link="native-ordered-footprint-coupling";}
                else bridge.Link=parents!=null&&parents.Count>1?"ambiguous-native-coupling":"unlinked";
            }
            Entries=bridges.ToArray();built=version;nextCheck=now+Stopwatch.Frequency;Builds++;
            BuildTicks+=Stopwatch.GetTimestamp()-now;
        }
        internal string Parent(Entry entry)
        {
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            if(SpanIndex(entry.ParentId,buildings.Length)<0) return ",parentLink="+entry.Link;
            ref GameBuilding b=ref buildings[entry.ParentId-1];
            if(b.r_GlobalId!=entry.ParentGlobal||!BridgeSnapshot.Active(b.r_AliveState)||!BridgeSnapshot.IsGate(b.r_BuildingType))
            { Invalidate();return ",parentLink=identity-mismatch"; }
            var text=new StringBuilder(",parentLink=").Append(entry.Link).Append(",parentCandidate=").Append(entry.ParentId)
                .Append("/g").Append(b.r_GlobalId).Append(",parentOwner=").Append(b.r_PlayerIdOwner).Append(",parentCapturer=").Append(b.r_CapturedByPlayerId).Append(",roles=[");
            for(int p=1;p<=8;p++) text.Append(p).Append(':').Append(BridgeSnapshot.Allied(p,b.r_PlayerIdOwner)?"owner-or-ally":"enemy")
                .Append('/').Append(BridgeSnapshot.Allied(p,b.r_CapturedByPlayerId)?"capturer-or-ally":"no-capture-access").Append(';');
            return text.Append(']').ToString();
        }
    }
}
