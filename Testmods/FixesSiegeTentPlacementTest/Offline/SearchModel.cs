using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
namespace Offline;
internal sealed unsafe class SearchModel : IDisposable
{
    internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    internal int[] Rows, Neighbors, Logic;
    internal ushort[] Columns, Structures, Units;
    internal byte[] Edges, Heights, Active, TreeAllowed;
    internal short[] Metric;
    internal Dictionary<string,object> Document;
    private GCHandle rowsPin, columnsPin;
    internal SearchModel(Dictionary<string,object> data)
    {
        Document=data;
        if ((string)data["nativeHash"] != Hash) throw new InvalidDataException("Native hash mismatch.");
        Rows=Array<int>(data,"rows"); Columns=Array<ushort>(data,"columns"); Neighbors=Array<int>(data,"neighbors");
        Logic=Array<int>(data,"logic"); Structures=Array<ushort>(data,"structures"); Units=Array<ushort>(data,"units");
        Edges=Array<byte>(data,"edges"); Heights=Array<byte>(data,"heights"); Active=Array<byte>(data,"activeCoordinates");
        Metric=Array<short>(data,"metric"); TreeAllowed=Array<byte>(data,"treeAllowed");
        if(Rows.Length!=2400 || Columns.Length!=320800 || Neighbors.Length!=6400 || Active.Length!=640000 ||
            new[]{Logic.Length,Structures.Length,Units.Length,Edges.Length,Heights.Length,Metric.Length,TreeAllowed.Length}.Any(n=>n!=320800))
            throw new InvalidDataException("Capture capacities mismatch.");
        rowsPin=GCHandle.Alloc(Rows,GCHandleType.Pinned); columnsPin=GCHandle.Alloc(Columns,GCHandleType.Pinned);
        GameTileManagerAPI.Instance=new GameTileManagerAPI { MapRowLookupTable=(int*)rowsPin.AddrOfPinnedObject(),
            MapColumnLookupTable=(ushort*)columnsPin.AddrOfPinnedObject(), TileManager=new TileView{StructureGrid=Structures} };
        var tents=(List<object>)data["tents"];
        int max=tents.Count==0 ? 0 : tents.Max(v=>Convert.ToInt32(((Dictionary<string,object>)v)["id"]));
        GameBuildingManagerAPI.Instance.Buildings=new GameBuilding[max];
        foreach(Dictionary<string,object> tent in tents)
            GameBuildingManagerAPI.Instance.Buildings[Convert.ToInt32(tent["id"])-1]=new GameBuilding { r_PlayerIdOwner=Convert.ToInt32(tent["owner"]),
                r_BuildingType=(eStructs)Convert.ToInt32(tent["type"]), r_TilePositionXBegin=Convert.ToInt32(tent["x"]), r_TilePositionYBegin=Convert.ToInt32(tent["y"]) };
        var player=(Dictionary<string,object>)data["player"]; int id=Convert.ToInt32(player["playerId"]), target=Convert.ToInt32(player["targetPlayerId"]);
        if(id<1 || id>8 || target<1 || target>8 || target==id) throw new InvalidDataException("Attacker/target missing.");
        GamePlayerManagerAPI.Instance.Players[id].r_SiegeAttackTargetPlayerId=target;
        GamePlayerManagerAPI.Instance.Players[target].r_KeepDoorTilePositionX=Convert.ToInt32(player["targetKeepX"]);
        GamePlayerManagerAPI.Instance.Players[target].r_KeepDoorTilePositionY=Convert.ToInt32(player["targetKeepY"]);
    }
    private static T[] Array<T>(Dictionary<string,object> data,string name) => ((List<object>)data[name]).Select(v=>(T)Convert.ChangeType(v,typeof(T))).ToArray();
    internal int Tile(int x,int y)
    { if((uint)x>=800 || (uint)y>=800 || Active[y*800+x]==0) return -1; int tile=Rows[y*3]+x; return (uint)tile<320800 ? tile : -1; }
    internal (int x,int y) Position(int tile) { int y=Columns[tile]; if((uint)y>=800) throw new InvalidDataException("Column lookup invalid."); return (tile-Rows[y*3],y); }
    // Exact producer search, including first 100-depth attempt then 200. No RNG.
    internal int Find(int x,int y,int playerId,bool counterfactual,int filter=60)
    { int result=FindRange(x,y,playerId,counterfactual,100,filter); return result!=0 ? result : FindRange(x,y,playerId,counterfactual,200,filter); }
    private int FindRange(int x,int y,int playerId,bool corrected,int maxDepth,int filter)
    {
        int start=Tile(x,y); if(start<0) return 0;
        var visited=new bool[320800]; var queue=new Queue<(int tile,int y,int depth)>();
        visited[start]=true; queue.Enqueue((start,y,1));
        while(queue.Count!=0)
        {
            var current=queue.Dequeue(); if(current.depth>maxDepth) return 0;
            for(int direction=0;direction<8;direction++)
            {
                if((Edges[current.tile]&NativeDirections.Bits[direction])==0) continue;
                int next=current.tile+Neighbors[current.y*8+direction];
                if((uint)next>=320800) throw new InvalidDataException("Native neighbor points outside captured image.");
                if(visited[next]) continue;
                int nextY=current.y+NativeDirections.Y[direction];
                if((uint)nextY>=800) throw new InvalidDataException("Native neighbor Y outside capture.");
                if(current.depth>4 && Structures[next]==0 && (filter==0 || Metric[next]==filter))
                {
                    int min=Heights[next],max=min; bool accepted=true;
                    for(int footprint=0;footprint<8;footprint++)
                    {
                        int cell=next+Neighbors[nextY*8+footprint];
                        if((uint)cell>=320800) throw new InvalidDataException("Footprint outside captured image.");
                        uint flags=unchecked((uint)Logic[cell]);
                        if((flags&0x4a7014b1)!=0 && ((flags&0x1000)==0 || TreeAllowed[cell]==0) ||
                            (flags&4)!=0 || Structures[cell]!=0 || Units[cell]!=0 ||
                            (corrected ? CounterfactualFixesCallback.Evaluate(cell,next,playerId) : OriginalFixesCallback.Evaluate(cell,next,playerId))!=1)
                        { accepted=false; break; }
                        min=Math.Min(min,Heights[cell]); max=Math.Max(max,Heights[cell]);
                    }
                    if(accepted && max-min<12) return next;
                }
                visited[next]=true; queue.Enqueue((next,nextY,current.depth+1));
            }
        }
        return 0;
    }
    internal bool ProducerAccepts(int center)
    {
        if(center==0) return false; var p=Position(center); int origin=Tile(p.x-1,p.y-1);
        return origin>=0 && (unchecked((uint)Logic[origin])&0x4a5014b1)==0;
    }
    internal void AddHypotheticalTent(int center, int player)
    {
        var position=Position(center);
        var old=GameBuildingManagerAPI.Instance.Buildings;
        var buildings=new GameBuilding[old.Length+1]; old.CopyTo(buildings,0);
        buildings[old.Length]=new GameBuilding { r_PlayerIdOwner=player,r_BuildingType=eStructs.STRUCT_SIEGE_TENT_CATAPULT,
            r_TilePositionXBegin=position.x-1,r_TilePositionYBegin=position.y-1 };
        GameBuildingManagerAPI.Instance.Buildings=buildings;
        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
        { int cell=Tile(position.x+dx,position.y+dy); if(cell<0)throw new InvalidDataException("Hypothetical footprint outside map."); Structures[cell]=checked((ushort)buildings.Length); }
    }
    public void Dispose() { if(rowsPin.IsAllocated)rowsPin.Free(); if(columnsPin.IsAllocated)columnsPin.Free(); }
}
