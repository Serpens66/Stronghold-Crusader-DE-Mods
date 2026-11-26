using System;
using System.Collections.Generic;
using APIShared;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class Native0YardLayout
        {
            internal int X,Y,InnerMinX,InnerMinY,InnerMaxX,InnerMaxY,MinX,MinY,MaxX,MaxY;
            internal bool TilesBound;
            internal readonly Dictionary<int,Native0YardTile> Tiles=new Dictionary<int,Native0YardTile>();
        }
        private sealed class Native0YardTile
        {internal int Building,Height,DefaultHeight,Owner;internal uint Flags;}
        private Native0YardLayout native0YardLayout;
        private static bool Native0Inside(int x,int y,int minX,int minY,int maxX,int maxY)=>
            x>=minX && x<=maxX && y>=minY && y<=maxY;
        private static bool Native0Intersects(int minX,int minY,int maxX,int maxY,
            int otherMinX,int otherMinY,int otherMaxX,int otherMaxY)=>
            minX<=otherMaxX && maxX>=otherMinX && minY<=otherMaxY && maxY>=otherMinY;

        private Native0YardLayout PlanNative0YardLayout()
        {
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int dx=cx-keep.X,dy=cy-keep.Y;
            int ax=Math.Abs(dx)>=Math.Abs(dy)?Math.Sign(dx):0,ay=ax==0?Math.Sign(dy):0;
            Require(ax!=0 || ay!=0,"Native0 Brewery cannot coincide with its Keep");
            int px=-ay,py=ax,scale=BuildingScales.GetScale(eMappers.MAPPER_STORES);
            Require(scale==5,"Existing dedicated-yard prefab scale contract changed");
            // Fixed bounded WORLD selection before worker acquisition. Never
            // revisit the chosen layout while waiting for a clear source moment.
            int[] distances={28,24,24,20,20},offsets={0,-12,12,-12,12};
            for(int i=0;i<distances.Length;i++)
            {
                int x=keep.X+ax*distances[i]+px*offsets[i],y=keep.Y+ay*distances[i]+py*offsets[i];
                var p=new Native0YardLayout{X=x,Y=y,InnerMinX=x,InnerMinY=y,InnerMaxX=x+scale,InnerMaxY=y+scale,
                    MinX=x-3,MinY=y-3,MaxX=x+scale+3,MaxY=y+scale+3};
                // Conservative future helper-built Workshop/Hovel envelopes.
                // Actual native footprints/accesses are checked again after spawn.
                if(Native0Intersects(p.MinX,p.MinY,p.MaxX,p.MaxY,cx-2,cy-2,cx+10,cy+10) ||
                    Native0Intersects(p.MinX,p.MinY,p.MaxX,p.MaxY,cx-11,cy+6,cx-5,cy+13))continue;
                if(!Native0LayoutClear(p,false,out string reason))
                {log("native0-yard-layout-candidate ordinal="+i+" rejected="+reason);continue;}
                log("native0-yard-layout-fixed ordinal="+i+" yard="+x+","+y+" plannedFullArea="+
                    p.MinX+","+p.MinY+".."+p.MaxX+","+p.MaxY+" brewery="+cx+","+cy+
                    " keep="+keep.X+","+keep.Y+" noLaterClearMomentSearch=true nativeStoreSelectionUnclaimed=true");
                return p;
            }
            throw new InvalidOperationException("No bounded pre-work yard/ring/interior layout is genuinely empty and protected-access-free");
        }
        private bool Native0KnownYard(int id)
        {
            if(millerWorld==null)return false;
            foreach(var identity in millerWorld.AddedYards)if(identity.Id==id)return true;
            return false;
        }
        private unsafe bool Native0LayoutClear(Native0YardLayout p,bool allowDedicatedYards,out string reason)
        {
            reason=null;var tiles=GameTileManagerAPI.Instance;
            for(int y=p.MinY;y<=p.MaxY;y++)for(int x=p.MinX;x<=p.MaxX;x++)
            {
                int tile=runtime.Native.Tile(x,y);int building=tile>=0?tiles.GetTileBuildingId(tile):0;
                Native0YardTile bound=null;
                bool yard=allowDedicatedYards && p.TilesBound && p.Tiles.TryGetValue(tile,out bound);
                if(tile<0 || !MoatWorkerWithinOwnKeepRange(tile) || !yard && tiles.GetTileHeight(tile)!=8 ||
                    tiles.GetTileDefaultHeight(tile)!=8 || runtime.Native.Head(tile)!=0 || tiles.GetTileVegetationId(tile)!=0 ||
                    (!yard && (building!=0 || !runtime.Native.OrdinaryGround(tile) ||
                        (runtime.Native.Flags(tile)&(uint)TilePropertyFlag.IsFarm)!=0)))
                {reason="full-area-tile "+x+","+y+" "+Native0TileFacts(tile);return false;}
                if(yard && (building!=bound.Building || tiles.GetTileHeight(tile)!=bound.Height ||
                    tiles.GetTileDefaultHeight(tile)!=bound.DefaultHeight || runtime.Native.Flags(tile)!=bound.Flags ||
                    tiles.GetWallOwnerLayer()[tile]!=bound.Owner))
                {reason="original-bound-yard-tile-changed "+x+","+y+" "+Native0TileFacts(tile);return false;}
            }
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_AliveState!=AliveState.IsAlive && b->r_AliveState!=AliveState.NeedsInit)continue;
                if(allowDedicatedYards && Native0KnownYard(index+1))continue;
                int size=checked((int)b->r_OccupyTileGridSize);
                if(size>0 && Native0Intersects(p.MinX,p.MinY,p.MaxX,p.MaxY,
                    b->r_TilePositionXBegin-1,b->r_TilePositionYBegin-1,
                    b->r_TilePositionXBegin+size,b->r_TilePositionYBegin+size) ||
                    Native0Intersects(p.MinX,p.MinY,p.MaxX,p.MaxY,b->r_TileAccessPositionX-1,b->r_TileAccessPositionY-1,
                        b->r_TileAccessPositionX+1,b->r_TileAccessPositionY+1))
                {reason="protected-native-building-or-access id="+(index+1)+" type="+b->r_BuildingType;return false;}
            }
            // Once per candidate during setup; once at the FIRST qualified source.
            // Include the whole interior and real accepted Next, not just Head
            // chains or the boundary. No actor is deleted or repositioned.
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null)continue;
                bool current=Native0Inside(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY,p.MinX,p.MinY,p.MaxX,p.MaxY);
                bool next=(u->r_PathPlanStateBitFlags==2 || u->r_PathPlanStateBitFlags==4) && u->r_MovementSubstep<8 &&
                    Native0Inside(u->r_NextTilePositionX2,u->r_NextTilePositionY2,p.MinX,p.MinY,p.MaxX,p.MaxY);
                if(current || next){reason="foreign-current-or-accepted-next id="+id+" global="+u->r_GlobalId;return false;}
            }
            return true;
        }
        private unsafe bool Native0CellInActualYard(int id,int x,int y)
        {
            if(!Native0KnownYard(id) || !BuildingAccess.TryGetById(id,out GameBuilding* b,out _) || b==null)return false;
            int size=checked((int)b->r_OccupyTileGridSize);
            return Native0Inside(x,y,b->r_TilePositionXBegin,b->r_TilePositionYBegin,
                b->r_TilePositionXBegin+size-1,b->r_TilePositionYBegin+size-1);
        }
        private void BindNative0PostInitYardTiles(Native0YardLayout p)
        {
            Require(!p.TilesBound && p.Tiles.Count==0,"Native0 yard tile bindings may not be recaptured");
            var tiles=GameTileManagerAPI.Instance;
            // Full76E80: four size2 records at (0,0),(3,0),(0,3),(3,3),
            // 2D2FB8; then nine building-ID0 cross cells from2D3060, all within
            // offset0..4. Scale5's extra row/column is only a free envelope.
            for(int y=p.Y;y<=p.Y+4;y++)for(int x=p.X;x<=p.X+4;x++)
            {
                int tile=runtime.Native.Tile(x,y),id=tiles.GetTileBuildingId(tile);
                bool core=Native0CellInActualYard(id,x,y);
                bool cross=id==0 && (x==p.X+2 || y==p.Y+2);
                uint flags=runtime.Native.Flags(tile);uint mask=core?0x502u:0x102u;
                Require((core || cross) && (flags&mask)==mask && tiles.GetTileDefaultHeight(tile)==8 &&
                    tiles.GetTileVegetationId(tile)==0 && (tiles.GetWallOwnerLayer()[tile]&7)+1==owner &&
                    (flags&(uint)(TilePropertyFlag.IsTree|TilePropertyFlag.IsMoat|TilePropertyFlag.IsFarm))==0,
                    "Post-init yard cell is outside the exact native core/cross ownership contract");
                p.Tiles.Add(tile,new Native0YardTile{Building=id,Flags=flags,Height=tiles.GetTileHeight(tile),
                    DefaultHeight=tiles.GetTileDefaultHeight(tile),Owner=tiles.GetWallOwnerLayer()[tile]});
                log("native0-yard-tile-bound xy="+x+","+y+" building="+id+" flags="+flags.ToString("X8")+
                    " currentHeight="+tiles.GetTileHeight(tile)+" defaultHeight="+tiles.GetTileDefaultHeight(tile)+
                    " nativeCore="+core+" nativeCross="+cross);
            }
            Require(p.Tiles.Count==25,"Incomplete exact native stockpile tile binding");p.TilesBound=true;
        }
        private unsafe void ValidateNative0BuiltYardLayout(bool requireAlive,int savedHomeX=-1,int savedHomeY=-1)
        {
            var p=native0YardLayout;Require(p!=null && millerWorld.AddedYards.Count==4,"Native0 fixed four-yard plan missing");
            int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
            foreach(var identity in millerWorld.AddedYards)
            {
                Require(BuildingAccess.TryGetById(identity.Id,out GameBuilding* b,out _) && b!=null &&
                    b->r_GlobalId==identity.Global && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    (b->r_AliveState==AliveState.IsAlive || !requireAlive && b->r_AliveState==AliveState.NeedsInit),
                    "Native0 exact planned yard lifetime changed");
                int size=checked((int)b->r_OccupyTileGridSize);Require(size>0 && size<=13,"Unexpected dedicated yard footprint");
                minX=Math.Min(minX,b->r_TilePositionXBegin);minY=Math.Min(minY,b->r_TilePositionYBegin);
                maxX=Math.Max(maxX,b->r_TilePositionXBegin+size-1);maxY=Math.Max(maxY,b->r_TilePositionYBegin+size-1);
                Require(Native0Inside(b->r_TilePositionXBegin,b->r_TilePositionYBegin,
                    p.InnerMinX,p.InnerMinY,p.InnerMaxX,p.InnerMaxY) &&
                    Native0Inside(b->r_TilePositionXBegin+size-1,b->r_TilePositionYBegin+size-1,
                    p.InnerMinX,p.InnerMinY,p.InnerMaxX,p.InnerMaxY) &&
                    Native0Inside(b->r_TileAccessPositionX,b->r_TileAccessPositionY,
                    p.InnerMinX,p.InnerMinY,p.InnerMaxX,p.InnerMaxY),"Actual yard access escaped its preplanned interior");
            }
            if(!p.TilesBound)
            {
                Require(requireAlive,"Tile bindings need genuinely initialized yard records");
                BindNative0PostInitYardTiles(p);
                log("native0-yard-actual-union contained=true actual="+minX+","+minY+".."+maxX+","+maxY+
                    " conservativeEnvelope="+p.InnerMinX+","+p.InnerMinY+".."+p.InnerMaxX+","+p.InnerMaxY);
            }
            Require(savedHomeX<0 && savedHomeY<0 || savedHomeX>=0 && savedHomeY>=0 &&
                !Native0Intersects(p.MinX,p.MinY,p.MaxX,p.MaxY,savedHomeX-1,savedHomeY-1,savedHomeX+1,savedHomeY+1),
                "Fixed stockpile ring/interior intersects the worker's actual saved home access");
            Require(Native0LayoutClear(p,true,out string reason),"Chosen fixed ring/interior became occupied/protected: "+reason);
        }
    }
}
