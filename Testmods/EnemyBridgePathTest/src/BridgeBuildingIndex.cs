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
            internal int Id, ParentId, Owner, NativeParent, Grid, Orientation;
            internal uint Global, ParentGlobal;
            internal int MinX=800,MinY=800,MaxX=-1,MaxY=-1;
            internal string Link;
        }
        internal static int SpanIndex(int gameId,int capacity) => gameId>0&&gameId<=capacity?gameId-1:-1;
        internal static bool IsCurrent(Entry entry,uint global,int owner,int parentRaw,int grid,int orientation) =>
            entry.Global==global&&entry.Owner==owner&&entry.NativeParent==parentRaw&&entry.Grid==grid&&entry.Orientation==orientation;
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
                var entry=new Entry {Id=i+1,Global=b.r_GlobalId,ParentId=b.r_GatehouseId,Owner=b.r_PlayerIdOwner,NativeParent=b.r_GatehouseId,Grid=(int)b.r_OccupyTileGridSize,Orientation=b.r_SpriteVariationIndex};
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
            foreach(var bridge in bridges)
            {
                ref GameBuilding b=ref buildings[bridge.Id-1]; Entry parent=null;
                foreach(var gate in gates) if(gate.Id==bridge.ParentId&&buildings[gate.Id-1].r_PlayerIdOwner==b.r_PlayerIdOwner) {parent=gate;break;}
                bridge.Link="native-building-id";
                if(parent==null)
                {
                    int count=0;
                    foreach(var gate in gates)
                        if(buildings[gate.Id-1].r_PlayerIdOwner==b.r_PlayerIdOwner&&BridgeSnapshot.Adjacent(footprints[bridge.Id],footprints[gate.Id])) {parent=gate;count++;}
                    bridge.Link=count==1?"unique-footprint-adjacency-candidate":count==0?"unlinked":"ambiguous";
                    if(count!=1) parent=null;
                }
                bridge.ParentId=parent?.Id??0;bridge.ParentGlobal=parent?.Global??0;
            }
            Entries=bridges.ToArray();built=version;nextCheck=now+Stopwatch.Frequency;Builds++;
            BuildTicks+=Stopwatch.GetTimestamp()-now;
        }
        internal string Parent(Entry entry)
        {
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            if(SpanIndex(entry.ParentId,buildings.Length)<0) return ",parentLink="+entry.Link;
            ref GameBuilding b=ref buildings[entry.ParentId-1];
            if(b.r_GlobalId!=entry.ParentGlobal||!BridgeSnapshot.Active(b.r_AliveState)||!BridgeSnapshot.IsGate(b.r_BuildingType)||b.r_PlayerIdOwner!=buildings[entry.Id-1].r_PlayerIdOwner)
            { Invalidate();return ",parentLink=identity-mismatch"; }
            var text=new StringBuilder(",parentLink=").Append(entry.Link).Append(",parentCandidate=").Append(entry.ParentId)
                .Append("/g").Append(b.r_GlobalId).Append(",parentOwner=").Append(b.r_PlayerIdOwner).Append(",parentCapturer=").Append(b.r_CapturedByPlayerId).Append(",roles=[");
            for(int p=1;p<=8;p++) text.Append(p).Append(':').Append(BridgeSnapshot.Allied(p,b.r_PlayerIdOwner)?"owner-or-ally":"enemy")
                .Append('/').Append(BridgeSnapshot.Allied(p,b.r_CapturedByPlayerId)?"capturer-or-ally":"no-capture-access").Append(';');
            return text.Append(']').ToString();
        }
    }
}
