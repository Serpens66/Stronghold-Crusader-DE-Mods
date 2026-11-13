using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AssassinKeepTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> WallConnectedKeep(AutomationRun run)
        {
            Simulation(run,()=>{ResetControl();BuildWallConnectedKeep();return 0;});
            Drain(run);log("CONTROL_CONNECTED_BEGIN improved="+Improved+" actors=Assassin,Spearman");
            foreach(var wait in Exercise(run))yield return wait;
            Simulation(run,()=>{Require(AllAtDestination(false),"Wall-connected Assassin/Spearman did not both arrive");RequireNoClimb();return 0;});
            log("CONTROL_CONNECTED_PASS improved="+Improved+" actors=2 allArrived=true climbStates=0");
        }
        private unsafe void BuildWallConnectedKeep()
        {
            connectedControl=true;
            keepId=GamePlayerManagerAPI.Instance.GetPlayerKeepId(owner);keepOwner=owner;
            Require(BuildingAccess.TryGetById(keepId,out GameBuilding* keep,out _) && keep!=null && IsKeep(keep->r_BuildingType),"Original player Keep missing");
            keepGlobal=keep->r_GlobalId;minX=keep->r_TilePositionXBegin;minY=keep->r_TilePositionYBegin;
            maxX=minX+(int)keep->r_OccupyTileGridSize-1;maxY=minY+(int)keep->r_OccupyTileGridSize-1;
            tx=(minX+maxX)/2;ty=(minY+maxY)/2;
            var tiles=GameTileManagerAPI.Instance;
            for(int x=minX-5;x<minX;x++)
            {
                Require(tiles.IsTileInsideMapBounds(x,ty),"West wall control leaves map");int tile=tiles.GetTileId(x,ty);
                Require(tiles.GetTileBuildingId(tile)==0 && tiles.GetChimpLayer()[tile]==0 && tiles.IsTileWalkableAndUnoccupied(tile) &&
                    (tiles.GetTilePropertyFlag(tile)&(TilePropertyFlag.IsWall|TilePropertyFlag.IsElevated|TilePropertyFlag.IsBuilding|TilePropertyFlag.GoodsyardRelated))==0,"West Keep wall fixture is occupied");
            }
            int stone=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_STONE_BLOCKS);
            if(stone<20)GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_STONE_BLOCKS,20-stone);
            Require(GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_STONE_BLOCKS)>=20,"No Stone for west wall control");
            GameBuildingManagerAPI.Instance.CreateWall(owner,minX-5,ty,minX-1,ty,eMappers.MAPPER_WALL);
            for(int x=minX-5;x<minX;x++)Require((tiles.GetTilePropertyFlag(tiles.GetTileId(x,ty))&TilePropertyFlag.IsWall)!=0,"West wall not constructed");
            bool forward=GamePathingManagerAPI.Instance.HasPathEdge(minX-1,ty,PackedPathDirection.East);
            bool reverse=GamePathingManagerAPI.Instance.HasPathEdge(minX,ty,PackedPathDirection.West);
            pending.Add("CONNECTED_EDGE forward="+forward+" reverse="+reverse+" source={"+Tile(minX-1,ty)+"} roof={"+Tile(minX,ty)+"}");
            Require(forward||reverse,"Constructed wall has no actual ordinary native edge into Keep; cannot prove unchanged Vanilla access");
            sx=minX-4;sy=ty;
            SpawnTypedWallActor(eChimps.CHIMP_TYPE_ARAB_ASSASIN,minX-4,ty);
            SpawnTypedWallActor(eChimps.CHIMP_TYPE_SPEARMAN,minX-3,ty);
            actor=originalActors[0].Id;actorGlobal=originalActors[0].Global;actorType=originalActors[0].Type;
        }
        private unsafe void SpawnTypedWallActor(eChimps type,int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;int tile=tiles.GetTileId(x,y);GameUnit* unit=null;
            long id=GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,x,y,tiles.GetTileHeight(tile),type);
            Require(id>0 && id<=int.MaxValue && UnitAccess.TryGetById((int)id,out unit,out _) && unit!=null && unit->r_UnitChimp==type && unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y,"Wall actor spawn failed or projected");
            originalActors.Add(new OriginalActor{Id=(int)id,Global=unit->r_GlobalId,Type=type,StartX=x,StartY=y});
            pending.Add("CONNECTED_ACTOR id="+id+":"+unit->r_GlobalId+" type="+type+" tile="+x+","+y+" height="+unit->r_HeightElevation);
        }
        private IEnumerable<AutomationWait> Control(AutomationRun run,string kind)
        {
            Simulation(run,()=>{ResetControl();return 0;});
            int x=0,y=0;bool found=false;
            var origin=Simulation(run,()=>GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner));
            for(int dy=-60;dy<=60 && !found;dy+=6)for(int dx=-60;dx<=60 && !found;dx+=6)
            {
                int px=origin.X+dx,py=origin.Y+dy;
                if(Simulation(run,()=>ControlSiteFree(px,py))){x=px;y=py;found=true;}
            }
            Require(found,"No vacant flat control site near original Keep");
            if(kind=="wall")Simulation(run,()=>{BuildControlWall(x,y);return 0;});
            else
            {
                eMappers mapper=kind=="gate-main"?eMappers.MAPPER_GATE_MAIN:eMappers.MAPPER_GATE_INNER;
                // The native gate constructor accepts direction0 and writes the ordinary gate
                // topology. Public spawn confirmation verifies the actual type and footprint.
                Require(run.TrySpawnBuilding(run.CurrentMission,owner,x,y,mapper,0,out keepId,out keepGlobal),"Normal gate construction failed: "+kind);
                Simulation(run,()=>{ConfigureGateControl();return 0;});
            }
            Simulation(run,()=>{SpawnControlActor();return 0;});
            if(kind!="wall")
                foreach(var wait in CloseControlGate(run))yield return wait;
            Drain(run);log("CONTROL_BEGIN kind="+kind+" source="+sx+","+sy+" roof="+tx+","+ty);
            foreach(var wait in Exercise(run))yield return wait;
            Simulation(run,()=>
            {
                Require(AllAtDestination(false),"Control Assassin did not stop on the requested roof: "+kind);
                Require(hookTicks>0 && ascentTicks>1 && climbStarts>0,"No actual timed ascent observed: "+kind+" hook="+hookTicks+" ascent="+ascentTicks);
                pending.Add("CONTROL_ASCENT_PASS kind="+kind+" hookTicks="+hookTicks+" ascentTicks="+ascentTicks+" climbStarts="+climbStarts);
                return 0;
            });
            Drain(run);
            if(kind!="wall")
            {
                foreach(var wait in CloseControlGate(run))yield return wait;
                foreach(var wait in ReturnBehindKeep(run))yield return wait;
            }
        }
        private IEnumerable<AutomationWait> CloseControlGate(AutomationRun run)
        {
            // Remove the cheaper ordinary passage before testing either physical direction.
            EngineInterface.GameAction(Enums.GameActionCommand.GateHouseState,keepId,(int)Enums.GameActionValues.CLOSE_GATE);
            foreach(var wait in Pause(run,false))yield return wait;
            yield return AutomationWait.Until("control gate physically closed",()=>Simulation(run,GateClosed));
            foreach(var wait in Pause(run,true))yield return wait;
            Simulation(run,()=>{pending.Add("CONTROL_GATE_CLOSED id="+keepId+":"+keepGlobal+" roof={"+Tile(tx,ty)+"}");return 0;});
        }
        private unsafe bool GateClosed()
        {
            Require(BuildingAccess.TryGetById(keepId,out GameBuilding* gate,out _) && gate!=null && gate->r_GlobalId==keepGlobal,"Closing control gate identity changed");
            return gate->r_GateState==2;
        }
        private unsafe void ResetControl()
        {
            capture=false;
            foreach(var old in originalActors)
            {
                if(!UnitAccess.TryGetById(old.Id,out GameUnit* unit,out _) || unit==null || unit->r_GlobalId!=old.Global)continue;
                // Keep the SDK mutation immediately behind the checked public lookup contract.
                if(APIShared.UnitAccess.TryGetById(old.Id,out unit,out _))GameUnitManagerAPI.Instance.KillUnit(old.Id);
            }
            originalActors.Clear();pending.Clear();failure=null;returning=false;connectedControl=false;actorType=eChimps.CHIMP_TYPE_ARAB_ASSASIN;
            hookTicks=ascentTicks=descentTicks=climbStarts=arrivals=moveCalls=0;lastMoveResult=-1;keepId=0;keepGlobal=0;keepOwner=owner;
            descentStartTicks=descentBodyTicks=descentPositiveHeightTicks=0;
        }
        private bool ControlSiteFree(int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;int first=-1;
            var buildings=GameBuildingManagerAPI.Instance;
            var origin=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int range=buildings.GetKeepProximityRange(tiles.GetCurrentMapSize());
            int overrideRange=buildings.KeepProximityOverride.GetValue();if(overrideRange>0)range=Math.Min(range,overrideRange);
            if(Math.Max(Math.Abs(x-origin.X),Math.Abs(x+7-origin.X))>=range || Math.Max(Math.Abs(y-origin.Y),Math.Abs(y+7-origin.Y))>=range)return false;
            for(int yy=y-7;yy<=y+12;yy++)for(int xx=x-7;xx<=x+12;xx++)
            {
                if(!tiles.IsTileInsideMapBounds(xx,yy))return false;
                int tile=tiles.GetTileId(xx,yy);
                if(!tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetTileBuildingId(tile)!=0 || tiles.GetChimpLayer()[tile]!=0 ||
                    (tiles.GetTilePropertyFlag(tile)&(TilePropertyFlag.IsBuilding|TilePropertyFlag.IsWall|TilePropertyFlag.IsElevated))!=0)return false;
                int height=tiles.GetTileHeight(tile);if(first>=0 && height!=first)return false;first=height;
            }
            return true;
        }
        private void BuildControlWall(int x,int y)
        {
            int stone=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_STONE_BLOCKS);
            if(stone<20)GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_STONE_BLOCKS,20-stone);
            Require(GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_STONE_BLOCKS)>=20,"No actual stored Stone for wall control");
            GameBuildingManagerAPI.Instance.CreateWall(owner,x,y-2,x,y+2,eMappers.MAPPER_WALL);
            var tiles=GameTileManagerAPI.Instance;
            for(int yy=y-2;yy<=y+2;yy++)Require((tiles.GetTilePropertyFlag(tiles.GetTileId(x,yy))&TilePropertyFlag.IsWall)!=0,"Control wall was not actually constructed");
            minX=maxX=tx=x;minY=y-2;maxY=y+2;ty=y;sx=x-3;sy=y;
            pending.Add("wall-control source={"+Tile(sx,sy)+"} target={"+Tile(tx,ty)+"}");
        }
        private unsafe void ConfigureGateControl()
        {
            Require(BuildingAccess.TryGetById(keepId,out GameBuilding* gate,out _) && gate!=null && gate->r_GlobalId==keepGlobal,"Gate control identity lost");
            Require(gate->r_BuildingType==eStructs.STRUCT_GATE_MAIN || gate->r_BuildingType==eStructs.STRUCT_GATE_INNER,"Wrong gate type");
            minX=gate->r_TilePositionXBegin;minY=gate->r_TilePositionYBegin;maxX=minX+(int)gate->r_OccupyTileGridSize-1;maxY=minY+(int)gate->r_OccupyTileGridSize-1;
            tx=(minX+maxX)/2;ty=(minY+maxY)/2;sx=minX-3;sy=ty;
            var tiles=GameTileManagerAPI.Instance;
            Require((tiles.GetTilePropertyFlag(tiles.GetTileId(tx,ty))&TilePropertyFlag.IsWall)!=0,"Gate roof is not a wall surface");
            pending.Add("gate-control type="+gate->r_BuildingType+" id="+keepId+":"+keepGlobal+" source={"+Tile(sx,sy)+"} target={"+Tile(tx,ty)+"}");
        }
        private unsafe void SpawnControlActor()
        {
            var tiles=GameTileManagerAPI.Instance;int source=tiles.GetTileId(sx,sy);
            Require(tiles.IsTileWalkableAndUnoccupied(source) && tiles.GetTileBuildingId(source)==0,"Control source not free ground");
            long id=GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,sx,sy,tiles.GetTileHeight(source),eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            GameUnit* unit=null;
            Require(id>0 && id<=int.MaxValue && UnitAccess.TryGetById((int)id,out unit,out _) && unit!=null,"Control Assassin missing");
            actor=(int)id;actorGlobal=unit->r_GlobalId;
            Require(unit->r_CurrentTilePositionX==sx && unit->r_CurrentTilePositionY==sy,"Control Assassin spawn projected");
            originalActors.Add(new OriginalActor{Id=actor,Global=actorGlobal,StartX=sx,StartY=sy});
        }
    }
}
