using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // Output only: the separate Wheat field730/198620 contract is not normalized here.
        private sealed class FarmerOutputWorld
        {
            internal eChimps Type;internal eMappers Mapper;internal eStructs Kind;internal eGoods Good;
            internal int AI,Capacity,PlotCount,Extent,Farm,FarmX,FarmY,Granary;internal uint FarmGlobal,GranaryGlobal;
            internal long Session,World;internal Actor Actor;internal UnitSnapshot Before;internal int[] Plots;
            internal bool Active=true,PendingCapacity,YieldPending,OwnResume,Complete,Retired;
            internal int Wait,Extensions,Remaining,InitialCargo,SecondCargo,Batches,Deposits,Yields,YieldArg,YieldResult;
            internal int Harvests,Products,Consumes,Store,GoalX,GoalY,Handoffs;internal uint StoreGlobal;
            internal string Fault;internal AllyTransferCapacity Free;internal FarmerFrame Previous;
            internal readonly Stack<FarmerGoodsFrame> Goods=new Stack<FarmerGoodsFrame>();
            internal readonly HashSet<string> Cows=new HashSet<string>(StringComparer.Ordinal);
        }
        private sealed class FarmerFrame
        {internal int AI,Plot,Stage,Carry,Counter,Phase,Ready;}
        private sealed class FarmerGoodsFrame
        {internal int Store,Stock,Carry;internal uint Global;}
        private FarmerOutputWorld farmerOutput;
        private static bool farmerTicksRegistered,farmerMovesRegistered;
        private static volatile Fixture farmerTickFixture,farmerMoveFixture;
        private IEnumerable<AutomationStep> AutomaticFarmerOutputSteps()
        {yield return new AutomationStep("natural-farmer-output-and-two-batches",TimeSpan.FromSeconds(1200),AutomaticFarmerOutput,true);}
        private FarmerOutputWorld NewFarmerOutput()
        {
            if(scenario.Contains("-wheat10-"))return new FarmerOutputWorld{Type=eChimps.CHIMP_TYPE_FARMER_WHEAT,Mapper=eMappers.MAPPER_WHEATFARM,
                Kind=eStructs.STRUCT_WHEATFARM,Good=eGoods.STORED_RAW_WHEAT,AI=10,Capacity=32,PlotCount=36,Extent=9};
            if(scenario.Contains("-hops8-"))return new FarmerOutputWorld{Type=eChimps.CHIMP_TYPE_FARMER_HOPS,Mapper=eMappers.MAPPER_HOPSFARM,
                Kind=eStructs.STRUCT_HOPSFARM,Good=eGoods.STORED_RAW_HOPS,AI=8,Capacity=16,PlotCount=24,Extent=9};
            Require(scenario.Contains("-cattle5-"),"Unknown output farmer");
            return new FarmerOutputWorld{Type=eChimps.CHIMP_TYPE_FARMER_CATTLE,Mapper=eMappers.MAPPER_CATTLEFARM,
                Kind=eStructs.STRUCT_CATTLEFARM,Good=eGoods.STORED_FOOD_CHEESE,AI=5,Capacity=250,Extent=10};
        }
        private unsafe GameBuilding* FarmerWorld(bool actor)
        {
            var p=farmerOutput;
            Require(p!=null && p.Active && !stopped && session==p.Session && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==p.Session && world==p.World,"Farmer fixture session/native world changed");
            Require(BuildingAccess.TryGetById(p.Farm,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.FarmGlobal &&
                b->r_PlayerIdOwner==owner && b->r_BuildingType==p.Kind && (b->r_AliveState==AliveState.IsAlive || !actor && b->r_AliveState==AliveState.NeedsInit),
                "Original farm lifetime changed");
            if(actor)
            {
                var u=Resolve(p.Actor);
                Require(UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==p.Type && u->r_ControllableForPlayerId==owner &&
                    u->r_LinkedProductionBuildingId==p.Farm && u->r_LinkedProductionBuildingGlobalId==p.FarmGlobal && b->r_WorkerId_1==p.Actor.Id,
                    "Original farmer employment/lifetime changed");
            }
            return b;
        }
        // A3A70 publishes native1A8;136D40 consumes this same local Cheese DWORD.
        private static unsafe int FarmerReady(GameBuilding* b)=>checked((int)b->r_CheeseAmount);
        private static unsafe int FarmerPhase(GameBuilding* b)=>Marshal.ReadByte((IntPtr)b,0x2F4-0x5C);
        private unsafe void RequireFarmerWorld(){FarmerWorld(false);}
        private bool FarmerOriginalAvailable()
        {
            var p=farmerOutput;
            if(stopped || p==null || !p.Active || p.Retired || !WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) || sid!=p.Session || world!=p.World)return false;
            var moves=automaticWorkerMoves;
            if(moves!=null && ReferenceEquals(moves.Actor,p.Actor) && moves.Session==p.Session)
                ObserveAutomaticDeletion(moves);
            if(AutomaticActorAlive(p.Actor))return true;
            if(!expectedEnabled)p.Retired=true;
            return false; // A later allocation in the same slot is never this observation's actor.
        }
        private unsafe int FarmerCows(GameBuilding* farm)
        {
            var p=farmerOutput;int live=0;
            // A3A70 owns slots1..3 beyond the regular farmer in slot0. Validate each current allocation, never a raw slot alone.
            for(int slot=1;slot<=3;slot++)
            {
                int id=Marshal.ReadInt16((IntPtr)farm,0x1FA-0x5C+slot*2);
                uint global=unchecked((uint)Marshal.ReadInt32((IntPtr)farm,0x204-0x5C+slot*4));
                if(id<=0 || !UnitAccess.TryGetById(id,out GameUnit* cow,out _) || cow==null || cow->r_GlobalId!=global ||
                    !UnitAccess.IsReallyAlive(cow) || cow->r_UnitChimp!=eChimps.CHIMP_TYPE_COW || cow->r_ControllableForPlayerId!=owner ||
                    cow->r_LinkedProductionBuildingId!=p.Farm || cow->r_LinkedProductionBuildingGlobalId!=p.FarmGlobal)continue;
                live++;string identity=id+"/"+global;
                if(p.Cows.Count<12 && p.Cows.Add(identity))log("automatic-farmer-natural-cow farm="+p.Farm+"/"+p.FarmGlobal+" slot="+slot+" cow="+identity+" ai="+cow->r_AIState);
            }
            return live;
        }
        private bool FarmerPatch(int x,int y,int side,bool fertile)
        {
            var tiles=GameTileManagerAPI.Instance;int height=-1;
            for(int yy=y-1;yy<=y+side;yy++)for(int xx=x-1;xx<=x+side;xx++)
            {
                if(!tiles.IsTileInsideMapBounds(xx,yy))return false;int t=tiles.GetTileId(xx,yy);
                if(!runtime.Native.OrdinaryGround(t) || tiles.GetTileUnitId(t)!=0 || tiles.GetTileBuildingId(t)!=0 || tiles.GetTileVegetationId(t)!=0 ||
                    (tiles.GetTilePropertyFlag(t)&(TilePropertyFlag.IsFarm|TilePropertyFlag.IsTree|TilePropertyFlag.IsMoat|TilePropertyFlag.PlannedMoat|
                    TilePropertyFlag.HasStone|TilePropertyFlag.HasIron|TilePropertyFlag.IsSwamp|TilePropertyFlag.River))!=0)return false;
                int h=tiles.GetTileHeight(t);if(height>=0 && h!=height)return false;height=h;
                if(fertile && xx>=x && xx<x+side && yy>=y && yy<y+side && (tiles.GetTileType(t)&(TileType.OasisGrass|TileType.ThickFoliage))==0)return false;
            }
            return height>=0;
        }
        private unsafe int FarmerPrefab(int x,int y,eMappers mapper,eStructs kind,bool bypass)
        {
            int id=0;var tiles=GameTileManagerAPI.Instance.TileManager;
            Require(!tiles.UsePlacementBlockedOverride,"Farmer setup cannot nest placement override");bool old=tiles.PlacementBlockedOverrideValue;
            using(var receipt=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(a=>{
                if(a.Phase==EventHookPhase.Post && a.PlayerId==owner && a.Building==kind && a.ReturnValue>0)
                {Require(id==0,"Unexpected multiple farmer prefab records");id=checked((int)a.ReturnValue);}
            }))
            try{GameBuildingManagerAPI.Instance.CreatePrefab(owner,x,y,mapper,BuildingScales.GetScale(mapper),0,true,bypass);}
            finally{tiles.UsePlacementBlockedOverride=false;tiles.PlacementBlockedOverrideValue=old;}
            return id;
        }
        private int FarmerService(eMappers mapper,eStructs kind)
        {
            var p=farmerOutput;var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            for(int r=12;r<=48;r+=8)for(int dy=-r;dy<=r;dy+=8)for(int dx=-r;dx<=r;dx+=8)
            {
                int x=keep.X+dx,y=keep.Y+dy;
                if(x+5>=p.FarmX-2 && x<=p.FarmX+p.Extent+2 && y+5>=p.FarmY-2 && y<=p.FarmY+p.Extent+2 || !FarmerPatch(x,y,4,false))continue;
                int id=FarmerPrefab(x,y,mapper,kind,true);Require(id>0,"Free dedicated farmer service prefab failed");return id;
            }
            throw new InvalidOperationException("No free service patch outside the full farm field");
        }
        private void ExtendFarmerStorage()
        {
            var p=farmerOutput;var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            for(int radius=12;radius<=48;radius+=4)for(int d=0;d<8;d++)
            {
                int x=keep.X+Dx[d]*radius,y=keep.Y+Dy[d]*radius;
                if(x+7>=p.FarmX-2 && x-1<=p.FarmX+p.Extent+2 && y+7>=p.FarmY-2 && y-1<=p.FarmY+p.Extent+2)continue;
                if(!FarmerPatch(x,y,6,false) || !MoatWorkerWithinOwnKeepRange(GameTileManagerAPI.Instance.GetTileId(x+2,y+2)))continue;
                ExtendMillerStorage(x,y);return;
            }
            throw new InvalidOperationException("No dedicated stockpile envelope outside the original farm field");
        }
        private unsafe void SetupFarmerOutput()
        {
            var p=farmerOutput;Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Farmer requires actual human owner");
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,"Farmer world unavailable");
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);int candidates=0;
            for(int r=12;r<=64 && p.Farm==0;r+=4)for(int dy=-r;dy<=r && p.Farm==0;dy+=4)for(int dx=-r;dx<=r && p.Farm==0;dx+=4)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r)continue;int x=keep.X+dx,y=keep.Y+dy;
                if(!FarmerPatch(x,y,p.Extent,true) || !MoatWorkerWithinOwnKeepRange(GameTileManagerAPI.Instance.GetTileId(x,y)))continue;
                candidates++;int id=FarmerPrefab(x,y,p.Mapper,p.Kind,false);if(id==0)continue;
                Require(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null && b->r_GlobalId!=0 && b->r_PlayerIdOwner==owner && b->r_BuildingType==p.Kind &&
                    b->r_TilePositionXBegin==x && b->r_TilePositionYBegin==y,
                    "Farm constructor readback differs");p.Farm=id;p.FarmGlobal=b->r_GlobalId;p.FarmX=x;p.FarmY=y;
            }
            Require(p.Farm>0,"No native-approved fertile farm patch near own Keep; candidates="+candidates);
            FarmerService(eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            if(p.PlotCount==0)
            {p.Granary=FarmerService(eMappers.MAPPER_GRANARY,eStructs.STRUCT_GRANARY);Require(BuildingAccess.TryGetById(p.Granary,out GameBuilding* g,out _) && g!=null,"Farmer granary readback missing");p.GranaryGlobal=g->r_GlobalId;}
            else
            {Require(millerWorld==null || !millerWorld.Active,"Farmer cannot replace active Miller");millerWorld=new MillerWorld{Active=false};ExtendFarmerStorage();p.Extensions=1;}
            p.PendingCapacity=true;
            if(!farmerTicksRegistered){Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-farmer",FarmerTick,out string reason),reason);farmerTicksRegistered=true;}
            farmerTickFixture=this;
            log("automatic-farmer-world type="+p.Type+" farm="+p.Farm+"/"+p.FarmGlobal+" origin="+p.FarmX+","+p.FarmY+" extent="+p.Extent+
                " greenPreflight=true farmBypass=false naturalEmployment=true noCropCargoYieldWrites=true");
        }
        private unsafe void FarmerPlots(GameBuilding* b)
        {
            var p=farmerOutput;if(p.PlotCount==0 || p.Plots!=null)return;
            Require(Marshal.OffsetOf(typeof(GameBuilding),nameof(GameBuilding.r_OccupiedTileIdsArrayBegin)).ToInt32()==0x224-0x5C,"Farm plot view layout differs");
            p.Plots=new int[p.PlotCount];uint* data=&b->r_OccupiedTileIdsArrayBegin;var tiles=GameTileManagerAPI.Instance;
            for(int i=0;i<p.Plots.Length;i++)
            {
                int t=checked((int)data[i]);Require(tiles.IsValidTileId(t),"Invalid farm plot");var q=tiles.GetTileVectorFromId(t);
                Require(q.X>=p.FarmX && q.X<p.FarmX+p.Extent && q.Y>=p.FarmY && q.Y<p.FarmY+p.Extent && tiles.GetTileBuildingId(t)==p.Farm &&
                    (tiles.GetTilePropertyFlag(t)&(p.Type==eChimps.CHIMP_TYPE_FARMER_WHEAT?TilePropertyFlag.IsWheat:TilePropertyFlag.IsHops))!=0,
                    "Original crop plot membership/extent differs");p.Plots[i]=t;
            }
            Require(p.Plots.Distinct().Count()==p.Plots.Length,"Aliased native crop plots");
        }
        private unsafe bool FarmerSourceSafe(UnitSnapshot before,int x,int y)
        {
            var p=farmerOutput;var b=FarmerWorld(true);
            if(AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1) || AppleStepContains(before.RequestedX,before.RequestedY,x,y,1))return false;
            for(int yy=p.FarmY;yy<p.FarmY+p.Extent;yy++)for(int xx=p.FarmX;xx<p.FarmX+p.Extent;xx++)if(AppleStepContains(xx,yy,x,y,1))return false;
            if(p.Plots!=null)foreach(int t in p.Plots){var q=GameTileManagerAPI.Instance.GetTileVectorFromId(t);if(AppleStepContains(q.X,q.Y,x,y,1))return false;}
            foreach(int id in GameBuildingManagerAPI.Instance.GetAllAliveBuildings())
                if(BuildingAccess.TryGetById(id,out GameBuilding* store,out _) && store!=null && store->r_PlayerIdOwner==owner &&
                    (store->r_BuildingType==eStructs.STRUCT_GOODS_YARD || store->r_BuildingType==eStructs.STRUCT_GRANARY) &&
                    AppleStepContains(store->r_TileAccessPositionX,store->r_TileAccessPositionY,x,y,1))return false;
            return true;
        }
        private unsafe Actor FindNaturalOutputFarmer()
        {
            var p=farmerOutput;var farm=FarmerWorld(false);if(farm->r_AliveState!=AliveState.IsAlive)return null;FarmerPlots(farm);
            int id=farm->r_WorkerId_1;
            if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || !UnitAccess.IsReallyAlive(u) || u->r_UnitChimp!=p.Type ||
                u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=p.Farm || u->r_LinkedProductionBuildingGlobalId!=p.FarmGlobal ||
                u->r_AIState!=p.AI || u->r_CarryGoodAmount==0 || p.PlotCount==0 && FarmerReady(farm)!=0)return null;
            if(p.PlotCount==0 && FarmerCows(farm)==0)return null;
            var before=runtime.SnapshotUnit(id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 || !TryStartedMoatAnchor(before,out int x,out int y))return null;
            var a=new Actor{Id=id,Global=u->r_GlobalId,Type=p.Type,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
            p.Actor=a;if(!FarmerSourceSafe(before,x,y)){p.Actor=null;return null;}p.Before=before;return a;
        }
        private unsafe FarmerFrame ReadFarmerFrame(GameBuilding* farm)
        {
            var p=farmerOutput;var u=Resolve(p.Actor);int plot=p.PlotCount==0?-1:unchecked((int)runtime.Native.JobDword(u,0x730));
            var grid=GameTileManagerAPI.Instance.TileManager.DamageGrid;
            return new FarmerFrame{AI=u->r_AIState,Plot=plot,Stage=plot>=0 && plot<grid.Length?grid[plot]:-1,Carry=u->r_CarryGoodAmount,
                Counter=p.Type==eChimps.CHIMP_TYPE_FARMER_WHEAT?runtime.Native.JobWord(u,0x9EC):0,Phase=FarmerPhase(farm),Ready=FarmerReady(farm)};
        }
        private static unsafe void FarmerTick(int tick)
        {
            var f=farmerTickFixture;var p=f?.farmerOutput;
            if(f==null || f.stopped || p==null || !p.Active || p.Retired || p.Complete || p.Fault!=null)return;
            try
            {
                var b=f.FarmerWorld(false);
                if(p.Before!=null && !f.FarmerOriginalAvailable()){if(!f.expectedEnabled)return;throw new InvalidOperationException("Original farmer was lost");}
                if(p.Before!=null)b=f.FarmerWorld(true);
                if(b->r_AliveState==AliveState.NeedsInit){Require(++p.Wait<=64,"Farm did not initialize");return;}f.FarmerPlots(b);
                if(p.PendingCapacity)
                {
                    if(p.PlotCount>0 && !f.MillerAddedYardsReady()){Require(++p.Wait<=64,"Farmer yards did not initialize");return;}
                    if(p.PlotCount==0)
                    {Require(BuildingAccess.TryGetById(p.Granary,out GameBuilding* g,out _) && g!=null && g->r_GlobalId==p.GranaryGlobal,"Farmer granary lifetime changed");if(g->r_AliveState==AliveState.NeedsInit){Require(++p.Wait<=64,"Granary did not initialize");return;}}
                    Require(AllyTransfers.TryGetCapacity(f.owner,p.Good,out p.Free,out string reason),reason);p.PendingCapacity=false;p.Wait=0;
                    f.log("automatic-farmer-capacity tick="+tick+" type="+p.Type+" free="+p.Free.FreeAmount);
                }
                if(p.Before==null)return;var now=f.ReadFarmerFrame(b);var old=p.Previous;
                if(p.Batches==2){Require(now.Carry==0 && p.Yields==1 && p.SecondCargo>0 && p.Remaining==0,"Final native farmer cargo decrement missing");p.Complete=true;return;}
                var grid=GameTileManagerAPI.Instance.TileManager.DamageGrid;
                if(old!=null)
                {
                    if(p.Batches==1 && p.Type==eChimps.CHIMP_TYPE_FARMER_WHEAT && old.AI==7 && p.Plots.Contains(old.Plot) && old.Stage<120 &&
                        (grid[old.Plot]==120 || grid[old.Plot]==121) && now.Counter==(grid[old.Plot]==121?0:old.Counter+1))
                    {p.Harvests++;if(p.Harvests<=4)f.log("automatic-farmer-native-harvest type="+p.Type+" plot="+old.Plot+" oldStage="+old.Stage+" newStage="+grid[old.Plot]+" counter="+old.Counter+"->"+now.Counter);}
                    // BBB70's maturity test is at selection. It is NOT repeated by the later AI6 write;
                    // normal ageing while walking must not be mistaken for artificial crop production.
                    if(p.Batches==1 && p.Type==eChimps.CHIMP_TYPE_FARMER_HOPS && old.AI==6 && p.Plots.Contains(old.Plot) && old.Stage!=80 &&
                        grid[old.Plot]==80 && now.Carry==old.Carry+1)
                    {p.Harvests++;if(p.Harvests<=4)f.log("automatic-farmer-native-harvest type="+p.Type+" plot="+old.Plot+" oldStage="+old.Stage+" newStage=80 carry="+old.Carry+"->"+now.Carry);}
                    // The next milk production can finish during the first delivery. It must be new AFTER source capture.
                    if(p.PlotCount==0)
                    {
                        if(old.Phase==6 && now.Phase==0)
                        {Require(f.FarmerCows(b)>0,"New milk production has no real original-farm cow lifetime");p.Products++;if(p.Batches==1 && now.Ready==0 && now.AI==4)p.Consumes++;}
                        else if(p.Batches==1 && old.Ready==1 && now.Ready==0 && now.AI==4)p.Consumes++;
                    }
                }
                p.Previous=now;
            }
            catch(Exception e){f.FarmerFault(e.Message);}
        }
        private void FarmerFault(string text)
        {if(farmerOutput!=null && farmerOutput.Fault==null){farmerOutput.Fault=text;log("automatic-farmer-first-failure "+text);}}
        private unsafe void FarmerYield(UnitCalculateBonusYieldEventArgs a)
        {
            var p=farmerOutput;if(p==null || !p.Active || p.Retired || p.Before==null || p.Fault!=null || p.Batches>=2 || a==null ||
                a.UnitId!=p.Actor.Id || GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            if(!FarmerOriginalAvailable())return;
            try
            {
                FarmerWorld(true);var u=Resolve(p.Actor);
                Require(u->r_AIState==p.AI && p.Batches==1 && p.Remaining==0 && p.Yields==0 && (p.PlotCount>0?p.Harvests>0:p.Products>0 && p.Consumes>0),
                    "Farmer second yield lacks a new natural crop/animal production and first full delivery");
                if(a.Phase==EventHookPhase.Pre){Require(!p.YieldPending && a.GoodAmount>0,"Nested/invalid farmer yield");p.YieldPending=true;p.YieldArg=a.GoodAmount;}
                else if(a.Phase==EventHookPhase.Post)
                {Require(p.YieldPending && a.GoodAmount==p.YieldArg && a.ReturnValue>0 && a.ReturnValue<=ushort.MaxValue,"Unmatched farmer yield completion");p.YieldPending=false;p.YieldResult=checked((int)a.ReturnValue);p.Yields++;log("automatic-farmer-production type="+p.Type+" unit="+p.Actor.Id+"/"+p.Actor.Global+" yield="+p.YieldResult+" harvests="+p.Harvests+" products="+p.Products+" consumes="+p.Consumes);}
            }
            catch(Exception e){FarmerFault(e.Message);}
        }
        private unsafe GameBuilding* FarmerStore(int id,uint global)
        {
            var p=farmerOutput;Require(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global && b->r_AliveState==AliveState.IsAlive &&
                b->r_PlayerIdOwner==owner && b->r_BuildingType==(p.PlotCount==0?eStructs.STRUCT_GRANARY:eStructs.STRUCT_GOODS_YARD),"Farmer store lifetime/type changed");return b;
        }
        private unsafe bool FarmerStoreAccepts(GameUnit* u,GameBuilding* b)
        {
            var p=farmerOutput;if(Math.Abs(u->r_CurrentTilePositionX-b->r_TileAccessPositionX)>=3 || Math.Abs(u->r_CurrentTilePositionY-b->r_TileAccessPositionY)>=3)return false;
            if(p.PlotCount==0)return b->r_CurrentGoodStackAmount<250;
            int* amounts=(int*)&b->r_NullAmount;
            // Native BD490 tests exactly native goods slots1..24; this is its audited record contract.
            for(int i=1;i<=24;i++)if(i!=(int)p.Good && amounts[i]!=0)return false;
            return amounts[(int)p.Good]<p.Capacity;
        }
        private unsafe void FarmerGoods(AddGoodToGoodsyardEventArgs a)
        {
            var p=farmerOutput;if(p==null || !p.Active || p.Retired || p.Before==null || p.Fault!=null || p.Batches>=2 || a==null || a.Good!=p.Good ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            if(!FarmerOriginalAvailable())return;
            try
            {
                FarmerWorld(true);var u=Resolve(p.Actor);var b=FarmerStore(a.BuildingId,unchecked((uint)a.BuildingGlobalId));
                Require(u->r_AIState==p.AI && u->r_CarryGoodAmount>0 && a.Add && a.AddAmount==1 && a.Capacity==p.Capacity && runtime.Native.Word(u,0x98E)==a.BuildingId &&
                    runtime.Native.JobDword(u,0x6F8)==b->r_GlobalId && Math.Abs(u->r_CurrentTilePositionX-b->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-b->r_TileAccessPositionY)<3,
                    "Farmer credit lacks exact cargo, live store binding or physical contact");
                int stock=((int*)&b->r_NullAmount)[(int)p.Good];
                if(a.Phase==EventHookPhase.Pre)
                {
                    if(p.Remaining==0){Require(p.Batches==1 && p.Yields==1 && !p.YieldPending && u->r_CarryGoodAmount==p.YieldResult,"Second cargo differs from actual fresh yield");p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;}
                    Require(u->r_CarryGoodAmount==p.Remaining && (!expectedEnabled || p.Batches!=0 || p.OwnResume),"First farmer delivery lacks original native resume");
                    if(p.Batches==0)Require(a.BuildingId==p.Store && b->r_GlobalId==p.StoreGlobal,"First delivery changed store without observed native handoff");
                    p.Goods.Push(new FarmerGoodsFrame{Store=a.BuildingId,Global=b->r_GlobalId,Stock=stock,Carry=u->r_CarryGoodAmount});
                }
                else if(a.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Farmer Post lacks Pre");var old=p.Goods.Pop();
                    Require(old.Store==a.BuildingId && old.Global==b->r_GlobalId && stock==old.Stock+1 && u->r_CarryGoodAmount==old.Carry,"Farmer goods write did not create its real storage delta");
                    p.Deposits++;p.Remaining--;if(p.Remaining==0)p.Batches++;
                    log("automatic-farmer-deposit type="+p.Type+" unit="+p.Actor.Id+"/"+p.Actor.Global+" store="+a.BuildingId+"/"+b->r_GlobalId+" delta=1 remaining="+p.Remaining+" batches="+p.Batches);
                }
            }
            catch(Exception e){FarmerFault(e.Message);}
        }
        private void ArmFarmerMoves()
        {
            if(!farmerMovesRegistered)
            {Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-farmer-output",new FarmerMoveObserver(),out string reason,order:200,enabled:()=>farmerMoveFixture!=null &&
                !farmerMoveFixture.stopped && farmerMoveFixture.farmerOutput!=null && farmerMoveFixture.farmerOutput.Active && !farmerMoveFixture.farmerOutput.Retired &&
                farmerMoveFixture.farmerOutput.Before!=null && farmerMoveFixture.farmerOutput.Fault==null && farmerMoveFixture.farmerOutput.Batches==0),reason);farmerMovesRegistered=true;}
            farmerMoveFixture=this;
        }
        private sealed class FarmerMoveObserver:IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var f=farmerMoveFixture;var p=f?.farmerOutput;
                if(p==null || c==null || c.UnitId!=p.Actor.Id || c.SessionId!=p.Session || c.WorldGeneration!=p.World)return null;
                if(!f.FarmerOriginalAvailable())return null;
                try
                {
                    bool own=c.OriginOwnerGuid==Plugin.Guid;
                    // Native OFF failure/retirement may already have changed phase,
                    // cargo or its next command. Only own ON continuation is strict.
                    if(!own && !f.expectedEnabled)return null;
                    f.FarmerWorld(true);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==p.AI && u->r_CarryGoodAmount==p.Remaining && c.Extra==0,
                        "Farmer move changed phase/cargo: own="+own+" ai="+u->r_AIState+" expectedAI="+p.AI+
                        " cargo="+u->r_CarryGoodAmount+" remaining="+p.Remaining+" extra="+c.Extra);
                    int id=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);var b=f.FarmerStore(id,global);
                    if(own)
                    {IntPtr ptr=(IntPtr)u;Require(f.expectedEnabled && id==p.Store && global==p.StoreGlobal && c.X==p.Before.RequestedX && c.Y==p.Before.RequestedY &&
                        p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,o=>f.runtime.Native.JobWord((GameUnit*)ptr,o),o=>f.runtime.Native.JobDword((GameUnit*)ptr,o)),"Own farmer goal/job/store changed");}
                    else Require((!f.expectedEnabled || p.OwnResume) && p.Handoffs<16 && string.IsNullOrEmpty(c.OriginOwnerGuid) && c.ParentInvocationId==0 &&
                        u->r_TargetTilePositionX2==p.GoalX && u->r_TargetTilePositionY2==p.GoalY && c.X==b->r_TileAccessPositionX && c.Y==b->r_TileAccessPositionY &&
                        !f.FarmerStoreAccepts(u,f.FarmerStore(p.Store,p.StoreGlobal)),"Farmer reselection lacks native old-store rejection/new access");
                    return new FarmerMoveCompletion(f,p,c,own,id,global);
                }
                catch(Exception e){f.FarmerFault(e.Message);return null;}
            }
        }
        private sealed class FarmerMoveCompletion:IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly FarmerOutputWorld p;private readonly UnitMoveContext c;private readonly bool own;
            private readonly int store;private readonly uint global;private int finished;
            internal FarmerMoveCompletion(Fixture f,FarmerOutputWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion r)
            {
                if(Interlocked.Exchange(ref finished,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(r!=null && ReferenceEquals(r.Context,c) && r.IsCurrentWorld && r.NativeInvoked && r.NativeReturned && r.Failure==null,"Farmer move lacks actual native completion");
                    f.log("automatic-farmer-output-move own="+own+" result="+r.NativeResult+" unit="+p.Actor.Id+"/"+p.Actor.Global+" goal="+c.X+","+c.Y);
                    if(r.NativeResult==0){if(!f.expectedEnabled)f.FarmerOriginalAvailable();return;}
                    f.FarmerWorld(true);var u=f.Resolve(p.Actor);
                    Require(r.NativeResult==1 && u->r_AIState==p.AI && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,"Farmer native move lost original cargo/binding/goal");
                    if(own)p.OwnResume=true;else p.Handoffs++;p.Store=store;p.StoreGlobal=global;p.GoalX=c.X;p.GoalY=c.Y;
                }
                catch(Exception e){f.FarmerFault(e.Message);}
            }
        }
        private unsafe AutomaticWorkerMutation PrepareFarmerMutation(Actor actor)
        {
            var p=farmerOutput;var farm=FarmerWorld(true);var u=Resolve(actor);var old=p.Before;var b=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(b!=null && b.Unsupported==null && b.Job!=null && b.AIState==p.AI && b.GlobalId==actor.Global && b.X==old.X && b.Y==old.Y &&
                b.NextX==old.NextX && b.NextY==old.NextY && b.Substep==old.Substep && b.Flags==old.Flags && b.RequestedX==old.RequestedX && b.RequestedY==old.RequestedY &&
                TryStartedMoatAnchor(b,out x,out y) && FarmerSourceSafe(b,x,y),"Farmer source changed before native moat writer");
            p.Before=b;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;p.Store=runtime.Native.Word(u,0x98E);p.StoreGlobal=runtime.Native.JobDword(u,0x6F8);FarmerStore(p.Store,p.StoreGlobal);
            p.GoalX=b.RequestedX;p.GoalY=b.RequestedY;p.Previous=ReadFarmerFrame(farm);actors.Add(actor);ArmFarmerMoves();
            return ApplyAutomaticStartedWorkerMoat(actor,b,x,y);
        }
        private unsafe void LogFarmerAcquisition(int sample)
        {
            var p=farmerOutput;var b=FarmerWorld(false);int id=b->r_WorkerId_1;string fact="no-employee";
            if(id>0 && UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null)fact=id+"/"+u->r_GlobalId+" life="+u->r_AliveState+" type="+u->r_UnitChimp+
                " ai="+u->r_AIState+" cargo="+u->r_CarryGoodAmount+" flags="+u->r_PathPlanStateBitFlags+" ss="+u->r_MovementSubstep+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY;
            log("automatic-farmer-acquire sample="+sample+" farm="+p.Farm+"/"+p.FarmGlobal+" cropPhase="+(b->r_WorkerInteractionPhase&0xffff)+" animalPhase="+FarmerPhase(b)+" ready="+FarmerReady(b)+" free="+p.Free?.FreeAmount+" "+fact);
        }
        private IEnumerable<AutomationWait> AutomaticFarmerOutput(AutomationRun run)
        {
            farmerOutput=NewFarmerOutput();var p=farmerOutput;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(FarmerGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(FarmerYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupFarmerOutput();});Actor actor=null;
                for(int sample=0;sample<14000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(sample%256==0)LogFarmerAcquisition(sample);if(p.PendingCapacity)return true;
                        if(p.Free==null || !p.Free.HasStorage || p.Free.FreeAmount<32)
                        {if(p.PlotCount>0){Require(p.Extensions++<8,"Farmer yard extension bound reached");ExtendFarmerStorage();}p.PendingCapacity=true;return true;}
                        if(sample%256==0){p.PendingCapacity=true;return true;}
                        actor=FindNaturalOutputFarmer();return true;
                    });
                }
                Require(actor!=null,"No natural loaded output farmer reached an eligible step outside its future field/access");
                Director.instance.SetEngineFrameRate(250);AutomaticWorkerMutation mutation=null;
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareFarmerMutation(actor);return true;});
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                for(int sample=0;sample<(expectedEnabled?14000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool done=run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);RequireFarmerWorld();return p.Complete || !AutomaticActorAlive(actor);});if(done)break;
                }
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{
                    RequireFarmerWorld();
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"Farmer observations incomplete: "+p.Fault);
                    if(expectedEnabled)Require(p.Complete && p.OwnResume,"Farmer did not complete original batch, new natural production and second full delivery");
                    log("automatic-farmer-cycle type="+p.Type+" enabled="+expectedEnabled+" first="+p.InitialCargo+" second="+p.SecondCargo+" deposits="+p.Deposits+
                        " batches="+p.Batches+" harvests="+p.Harvests+" products="+p.Products+" consumes="+p.Consumes+" yield="+p.Yields+" ownResume="+p.OwnResume+" complete="+p.Complete);
                    EndAutomaticWorkerCase(mutation,p.Complete);
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World,
                        "Completed farmer case no longer owns its original native world");
                    Plugin.SetGeneralFixtureSession(false);Director.instance.SetEngineFrameRate(250);return true;
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(farmerTickFixture,this))farmerTickFixture=null;if(ReferenceEquals(farmerMoveFixture,this))farmerMoveFixture=null;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                // Managed observation cleanup only. Never retire native farmers/crops/cows in finally.
            }
        }
    }
}
