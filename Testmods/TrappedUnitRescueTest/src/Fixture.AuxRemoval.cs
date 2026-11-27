using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private enum AuxRemovalRole { Miller,Miner1,Miner2,TannerCow }
        private sealed class AuxRemovalProof
        {
            internal AuxRemovalRole Role;internal eChimps Type;internal eStructs Kind;
            internal long Session,World,BeforeRemovals;internal int Building,Slot,X,Y,Width=1,Ticks,LastCowAI=-1,RejectLogs;
            internal uint BuildingGlobal;internal Actor Actor,Cow;internal UnitSnapshot Before;
            internal int CowSlot,ReplacementId;internal uint ReplacementGlobal;
            internal bool Active=true,Written,SawLife3,Gone,EmployeeCleared,HeartbeatCleared,ChainsChecked,Replacement,CowGone;
            internal bool CowTimeout,CowTerminal,CowFarmSlotCleared,ReplacementHeartbeat,AccessesCaptured,MillerInputReady;internal string Fault,LastReject;
            internal bool WheatCapacityPending=true;internal AllyTransferCapacity WheatCapacity;
            internal readonly Dictionary<int,uint> NaturalCows=new Dictionary<int,uint>();
            internal readonly List<int> ProtectedAccesses=new List<int>();
        }
        private AuxRemovalProof auxRemoval;
        private static volatile Fixture auxRemovalFixture;
        private static bool auxRemovalRegistered;
        private IEnumerable<AutomationStep> AuxRemovalSteps()
        {yield return new AutomationStep("natural-civilian-auxiliary-removal-and-reemployment",TimeSpan.FromSeconds(1200),AuxRemovalCase,true);}
        private bool IsAuxRemovalWorld()
        {
            var p=auxRemoval;return !stopped && p!=null && p.Active && session==p.Session &&
                WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World;
        }
        private void RequireAuxRemovalWorld()=>Require(IsAuxRemovalWorld(),"Aux removal crossed its exact original session/native world");
        // CURRENT native GameBuilding starts at5C; IDs1FA+2slot/globals204+4slot
        // are employment.214+2slot is a per-pass heartbeat, NOT another stamped employee.
        private static unsafe int AuxEmployeeId(GameBuilding* b,int slot)=>Marshal.ReadInt16((IntPtr)b,0x1FA-0x5C+slot*2);
        private static unsafe uint AuxEmployeeGlobal(GameBuilding* b,int slot)=>unchecked((uint)Marshal.ReadInt32((IntPtr)b,0x204-0x5C+slot*4));
        private static unsafe int AuxHeartbeat(GameBuilding* b,int slot)=>Marshal.ReadInt16((IntPtr)b,0x214-0x5C+slot*2);
        private unsafe GameBuilding* AuxWorkplace()
        {
            RequireAuxRemovalWorld();var p=auxRemoval;
            Require(BuildingAccess.TryGetById(p.Building,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.BuildingGlobal &&
                b->r_PlayerIdOwner==owner && b->r_BuildingType==p.Kind &&
                (b->r_AliveState==AliveState.IsAlive || !p.Written && b->r_AliveState==AliveState.NeedsInit),"Original auxiliary workplace lifetime changed");return b;
        }
        private unsafe void SetupAuxRemoval()
        {
            RequireAuxRemovalWorld();var p=auxRemoval;Plugin.SetGeneralFixtureSession(false);
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Aux removal requires its real human employee owner");
            Require(Marshal.OffsetOf(typeof(GameBuilding),nameof(GameBuilding.r_WorkerId_1)).ToInt32()==0x1FA-0x5C &&
                Marshal.OffsetOf(typeof(GameBuilding),nameof(GameBuilding.N0000176A)).ToInt32()==0x204-0x5C &&
                Marshal.OffsetOf(typeof(GameBuilding),nameof(GameBuilding.N0000176C)).ToInt32()==0x214-0x5C,"Auxiliary building layout changed");
            if(p.Role==AuxRemovalRole.Miller)
            {
                // Reuse construction only. No old Miller job/goods/move proof is armed.
                millerWorld=new MillerWorld{Active=false};SetupMillerWorld();p.Building=millerWorld.Mill;
                p.BuildingGlobal=millerWorld.MillGlobal;ExtendMillerStorage();
            }
            else if(p.Role==AuxRemovalRole.TannerCow)
            {
                farmerOutput=new FarmerOutputWorld{Active=false,Type=eChimps.CHIMP_TYPE_FARMER_CATTLE,Mapper=eMappers.MAPPER_CATTLEFARM,
                    Kind=eStructs.STRUCT_CATTLEFARM,Good=eGoods.STORED_FOOD_CHEESE,AI=5,Capacity=250,Extent=10};
                SetupAuxCattleFarm();FindSite();p.Building=CreateBakerPrefab(cx,cy,eMappers.MAPPER_TANNER,eStructs.STRUCT_TANNERS_WORKSHOP);
                CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_ARMOURY,eStructs.STRUCT_ARMOURY);
            }
            else
            {
                FindSite();CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
                p.Building=CreateBakerPrefab(cx,cy,eMappers.MAPPER_IRON_MINE,eStructs.STRUCT_IRON_MINE);
            }
            Require(BuildingAccess.TryGetById(p.Building,out GameBuilding* b,out _) && b!=null,"Auxiliary prefab has no original identity");
            p.BuildingGlobal=b->r_GlobalId;AuxWorkplace();
            if(!auxRemovalRegistered)
            {Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-civilian-aux-removal",ObserveAuxRemoval,out string reason,order:100),reason);auxRemovalRegistered=true;}
            auxRemovalFixture=this;
            log("aux-removal-world role="+p.Role+" building="+p.Building+"/"+p.BuildingGlobal+
                " naturalEmployment=true nativeCowSpawnOnly=true oldContinuationObservers=false");
        }
        private bool AuxCattleGround(int x,int y,out int coverage,out int green)
        {
            coverage=0;green=0;var tiles=GameTileManagerAPI.Instance;
            //77E60/787C7..78878: overlapping bits contribute independently;
            // River contributes to neither counter. No square-of-all-green rule.
            for(int yy=y;yy<y+10;yy++)for(int xx=x;xx<x+10;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);if(tile<0)return false;
                if((tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.River)!=0)continue;
                var terrain=tiles.GetTileType(tile);
                int value=((terrain&TileType.OasisGrass)!=0?1:0)+((terrain&TileType.ThickFoliage)!=0?1:0);
                green+=value;coverage+=value+((terrain&TileType.Foliage)!=0?1:0);
            }
            return coverage>=100 && green>=50;
        }
        private unsafe void SetupAuxCattleFarm()
        {
            RequireAuxRemovalWorld();var p=farmerOutput;p.Session=auxRemoval.Session;p.World=auxRemoval.World;
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);int visited=0,safe=0,ground=0,bestCoverage=0,bestGreen=0;
            // Preserve the complete10x10+one-cell empty, level, ordinary safety
            // envelope. Fertility is Vanilla's coverage/green predicate instead.
            // All integer origins in these rings: sum(8*r,r=12..64)=16112.
            // One-off setup search, never a simulation-tick scan.
            for(int r=12;r<=64 && p.Farm==0;r++)for(int dy=-r;dy<=r && p.Farm==0;dy++)for(int dx=-r;dx<=r && p.Farm==0;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r)continue;visited++;int x=keep.X+dx,y=keep.Y+dy;
                if(!FarmerPatch(x,y,10,false) || !MoatWorkerWithinOwnKeepRange(runtime.Native.Tile(x,y)))continue;
                safe++;bool suitable=AuxCattleGround(x,y,out int coverage,out int green);
                bestCoverage=Math.Max(bestCoverage,coverage);bestGreen=Math.Max(bestGreen,green);if(!suitable)continue;ground++;
                // This remains the real public construction/77E60 validation path,
                // bypassPlacementRules=false. Counters are only a read-only prefilter.
                int id=FarmerPrefab(x,y,eMappers.MAPPER_CATTLEFARM,eStructs.STRUCT_CATTLEFARM,false);
                if(id==0)continue;
                Require(BuildingAccess.TryGetById(id,out GameBuilding* farm,out _) && farm!=null && farm->r_GlobalId!=0 &&
                    farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_CATTLEFARM &&
                    (farm->r_AliveState==AliveState.NeedsInit || farm->r_AliveState==AliveState.IsAlive) &&
                    farm->r_TilePositionXBegin==x && farm->r_TilePositionYBegin==y,"Native Cattle constructor identity/origin readback differs");
                p.Farm=id;p.FarmGlobal=farm->r_GlobalId;p.FarmX=x;p.FarmY=y;
                log("aux-removal-cattle-placement farm="+id+"/"+p.FarmGlobal+" origin="+x+","+y+" coverage="+coverage+
                    " green="+green+" requiredCoverage=100 requiredGreen=50 extent=10 emptyFlatHalo=1 bypass=false syntheticTerrainWrites=false");
            }
            log("aux-removal-cattle-preflight visited="+visited+" maxOrigins=16112 ringMin=12 ringMax=64 step=1 safeCandidates="+safe+" nativeGroundCandidates="+ground+" bestCoverage="+bestCoverage+
                " bestGreen="+bestGreen+" actualFarm="+p.Farm+" owner="+owner);
            Require(p.Farm>0,"No native-approved auxiliary Cattle farm: visited="+visited+"/16112 safe="+safe+" ground="+ground+" bestCoverage="+bestCoverage+" bestGreen="+bestGreen);
            FarmerService(eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            p.Granary=FarmerService(eMappers.MAPPER_GRANARY,eStructs.STRUCT_GRANARY);
            Require(BuildingAccess.TryGetById(p.Granary,out GameBuilding* granary,out _) && granary!=null && granary->r_GlobalId!=0 &&
                granary->r_PlayerIdOwner==owner && granary->r_BuildingType==eStructs.STRUCT_GRANARY,"Aux Cattle Granary readback missing");
            p.GranaryGlobal=granary->r_GlobalId;
        }
        private unsafe void CacheAuxNaturalCows()
        {
            var p=auxRemoval;if(p.Role!=AuxRemovalRole.TannerCow || p.Written)return;
            Require(BuildingAccess.TryGetById(farmerOutput.Farm,out GameBuilding* farm,out _) && farm!=null && farm->r_GlobalId==farmerOutput.FarmGlobal &&
                farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_CATTLEFARM,"Natural Cow farm lifetime changed");
            for(int slot=1;slot<=3;slot++)
            {
                int id=AuxEmployeeId(farm,slot);uint global=AuxEmployeeGlobal(farm,slot);
                if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* cow,out _) || cow==null || cow->r_GlobalId!=global ||
                    cow->r_UnitChimp!=eChimps.CHIMP_TYPE_COW || cow->r_ControllableForPlayerId!=owner ||
                    (cow->r_AliveState!=AliveState.NeedsInit && cow->r_AliveState!=AliveState.IsAlive))continue;
                Require(cow->r_LinkedProductionBuildingId==farmerOutput.Farm && cow->r_LinkedProductionBuildingGlobalId==farmerOutput.FarmGlobal &&
                    runtime.Native.Word(cow,0x9EE)==slot && runtime.Native.Word(cow,0x9F0)==farmerOutput.Farm,"Natural Cow birth has inconsistent farm binding");
                if(p.NaturalCows.TryGetValue(id,out uint known) && known==global)continue;
                Require(p.NaturalCows.Count<32 || p.NaturalCows.ContainsKey(id),"Natural Cow birth cache exceeds explicit32 identities");
                p.NaturalCows[id]=global;log("aux-removal-natural-cow cow="+id+"/"+global+" slot="+slot+" life="+cow->r_AliveState);
            }
        }
        private unsafe bool AuxTannerCow(GameUnit* tanner,out Actor actor,out int farmSlot)
        {
            actor=null;farmSlot=0;var p=auxRemoval;
            if(tanner->r_AIState!=3)return false;
            int id=runtime.Native.Word(tanner,0x9F6);uint global=runtime.Native.JobDword(tanner,0x9F8);
            if(id==0 || !p.NaturalCows.TryGetValue(id,out uint original) || original!=global ||
                !UnitAccess.TryGetById(id,out GameUnit* cow,out _) || cow==null || cow->r_GlobalId!=global || !UnitAccess.IsReallyAlive(cow) ||
                cow->r_UnitChimp!=eChimps.CHIMP_TYPE_COW || cow->r_ControllableForPlayerId!=owner || cow->r_CurrentHealth<=0 ||
                runtime.Native.Word(cow,0x8F8)!=0 || (cow->r_AIState!=4 && cow->r_AIState!=5))return false;
            Require(runtime.Native.Word(cow,0x9F6)==AuxEmployeeId(AuxWorkplace(),0) && runtime.Native.JobDword(cow,0x9F8)==tanner->r_GlobalId &&
                cow->r_LinkedProductionBuildingId==farmerOutput.Farm && cow->r_LinkedProductionBuildingGlobalId==farmerOutput.FarmGlobal &&
                runtime.Native.Word(cow,0x9F0)==farmerOutput.Farm,"Natural Tanner/Cow reservation is not reciprocal");
            farmSlot=runtime.Native.Word(cow,0x9EE);Require(farmSlot>=1 && farmSlot<=3,"Cow animal slot outside audited domain");
            actor=CivilianActor(cow,id);return true;
        }
        private bool RejectAuxSource(string reason,UnitSnapshot before=null)
        {
            var p=auxRemoval;if(p.LastReject!=reason)
            {
                p.LastReject=reason;
                if(p.RejectLogs++<128)log("aux-removal-acquire role="+p.Role+" reason="+reason+
                    " ai="+before?.AIState+" flags="+before?.Flags+" substep="+before?.Substep+
                    " current="+(before==null?"none":before.X+","+before.Y+":"+AppleTreeTileFacts(before.TileId))+
                    " next="+(before==null?"none":before.NextX+","+before.NextY+":"+AppleTreeTileFacts(before.NextTileId)));
                else if(p.RejectLogs==129)log("aux-removal-acquire-diagnostic-limit count=128 furtherChangesSuppressed=true");
            }
            return false;
        }
        private bool PrepareAuxMillerInput()
        {
            var p=auxRemoval;if(p.Role!=AuxRemovalRole.Miller || p.MillerInputReady)return true;
            if(p.WheatCapacityPending)return false;
            var capacity=p.WheatCapacity;Require(capacity!=null,"Miller Wheat capacity tick produced no result");
            if(!capacity.HasStorage || capacity.FreeAmount<30)
            {ExtendMillerStorage();p.WheatCapacity=null;p.WheatCapacityPending=true;return false;}
            Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_RAW_WHEAT,30) &&
                GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_WHEAT)>=30,"Real Miller input stockpile rejected controlled Wheat supply");
            p.MillerInputReady=true;
            log("aux-removal-miller-input wheat="+GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_WHEAT)+
                " physicalStockpile=true workerCargoWrites=false");return true;
        }
        private bool TryAuxStartedAnchor(UnitSnapshot before,out int x,out int y)
        {
            x=0;y=0;
            // Geometry only. The older continuation helper requires Job!=null,
            // which is deliberately absent from early CivilianRemoval captures.
            if(before==null || !before.CivilianRemoval || before.Unsupported!=null || !before.OrdinaryStep ||
                before.Substep<0 || before.Substep>=8 || (before.Flags!=2 && before.Flags!=4) ||
                Math.Max(Math.Abs(before.NextX-before.X),Math.Abs(before.NextY-before.Y))!=1)return false;
            int centerX=Math.Max(before.X,before.NextX),centerY=Math.Max(before.Y,before.NextY);
            var tiles=GameTileManagerAPI.Instance;
            // Audited6FE90/69850/619C0 width2: four unique centered-square cells.
            for(int yy=centerY-1;yy<=centerY;yy++)for(int xx=centerX-1;xx<=centerX;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);
                if(!MoatWorkerSourceEligible(tile))return false;
                int occupant=tiles.GetTileUnitId(tile);
                if(occupant!=0 && occupant!=before.Id)return false;
            }
            x=centerX;y=centerY;return true;
        }
        private unsafe bool TryPrepareAuxRemoval()
        {
            var p=auxRemoval;var b=AuxWorkplace();Require(p.Fault==null,p.Fault);CacheAuxNaturalCows();
            if(b->r_AliveState!=AliveState.IsAlive || !PrepareAuxMillerInput())return false;
            int slots=p.Role==AuxRemovalRole.Miller?3:1;
            for(int index=0;index<slots;index++)
            {
                int slot=p.Role==AuxRemovalRole.Miner2?1:index,id=AuxEmployeeId(b,slot);uint global=AuxEmployeeGlobal(b,slot);
                if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=global ||
                    !UnitAccess.IsReallyAlive(u) || u->r_UnitChimp!=p.Type || u->r_ControllableForPlayerId!=owner)continue;
                Require(u->r_LinkedProductionBuildingId==p.Building && u->r_LinkedProductionBuildingGlobalId==p.BuildingGlobal &&
                    runtime.Native.Word(u,0x9EE)==slot && runtime.Native.Word(u,0x9F0)==p.Building,"Natural auxiliary employee binding changed");
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || !before.CivilianRemoval || before.Unsupported!=null){RejectAuxSource("admission:"+u->r_AIState+":"+before?.Unsupported);continue;}
                int heartbeat=p.Role==AuxRemovalRole.TannerCow?0:AuxHeartbeat(b,slot);
                if(p.Role==AuxRemovalRole.Miner1)
                {if(u->r_AIState!=2 || heartbeat!=0){RejectAuxSource("Miner1-needs-visible-R2-without-hidden-heartbeat");continue;}}
                else if(p.Role!=AuxRemovalRole.TannerCow && heartbeat!=id){RejectAuxSource("no-original-visible-heartbeat");continue;}
                if(p.Role==AuxRemovalRole.Miller && u->r_AIState!=3 && u->r_AIState!=8)
                {RejectAuxSource("Miller-needs-natural-3-or8-transit:"+u->r_AIState);continue;}
                // R3 may retain the last AI1 heartbeat, but has no renewed
                // physical Mine contact before withdrawal. Never stage that phase.
                if(p.Role==AuxRemovalRole.Miner2 && u->r_AIState!=1)
                {RejectAuxSource("Miner2-needs-visible-AI1-before-contact:"+u->r_AIState);continue;}
                Actor cow=null;int cowSlot=0;
                if(p.Role==AuxRemovalRole.TannerCow && !AuxTannerCow(u,out cow,out cowSlot)){RejectAuxSource("no-natural-reciprocal-cow4-or5");continue;}
                p.Slot=slot;p.Actor=CivilianActor(u,id);p.Cow=cow;p.CowSlot=cowSlot;
                if(p.Role==AuxRemovalRole.TannerCow || p.Role==AuxRemovalRole.Miller)
                {
                    if(!before.OrdinaryStep){RejectAuxSource("natural-transit-pose:"+p.Role,before);continue;}
                    if(!TryAuxStartedAnchor(before,out int x,out int y))
                    {RejectAuxSource("natural-moat-eligibility:"+p.Role,before);continue;}
                    if(!AuxSourceAccessSafe(x,y,2)){RejectAuxSource("natural-protected-access-or-cow:"+p.Role,before);continue;}
                    p.X=x;p.Y=y;p.Width=2;
                }
                else StageAuxRemovalSource(before);
                p.Before=runtime.SnapshotUnit(id,true,false);
                Require(p.Before!=null && p.Before.GlobalId==global && p.Before.CivilianRemoval && p.Before.Unsupported==null &&
                    (p.Role==AuxRemovalRole.TannerCow || p.Role==AuxRemovalRole.Miller?p.Before.OrdinaryStep:p.Before.Flags==0 && p.Before.Substep==8 && p.Before.X==p.Before.NextX && p.Before.Y==p.Before.NextY) &&
                    p.Before.CurrentHadExit && MoatWorkerSourceEligible(p.Before.TileId) && CountInTileChain(p.Before.TileId,id)==1,
                    "Audited auxiliary setup did not produce a qualified ordinary source");
                p.Actor.X=p.Actor.SourceX=p.Before.X;p.Actor.Y=p.Actor.SourceY=p.Before.Y;
                return true;
            }
            return false;
        }
        private unsafe bool AuxSourceAccessSafe(int x,int y,int width)
        {
            var p=auxRemoval;
            // The tested moat must not itself destroy future hiring/workplace access.
            if(!p.AccessesCaptured)
            {
                foreach(int id in GameBuildingManagerAPI.Instance.GetAllAliveBuildings())
                    if(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null && b->r_PlayerIdOwner==owner)
                    {
                        int tile=runtime.Native.Tile(b->r_TileAccessPositionX,b->r_TileAccessPositionY);
                        if(tile>=0)p.ProtectedAccesses.Add(tile);
                    }
                Require(p.ProtectedAccesses.Count>0,"No initialized actual own building accesses for auxiliary source protection");p.AccessesCaptured=true;
            }
            foreach(int tile in p.ProtectedAccesses)
            {var q=GameTileManagerAPI.Instance.GetTileVectorFromId(tile);if(q.X>=x-width/2-2 && q.X<=x+2 && q.Y>=y-width/2-2 && q.Y<=y+2)return false;}
            if(p.Role==AuxRemovalRole.TannerCow)
            {
                if(x>=farmerOutput.FarmX-1 && x-width/2<=farmerOutput.FarmX+farmerOutput.Extent &&
                    y>=farmerOutput.FarmY-1 && y-width/2<=farmerOutput.FarmY+farmerOutput.Extent)return false;
                Require(UnitAccess.TryGetById(p.Cow.Id,out GameUnit* cow,out _) && cow!=null && cow->r_GlobalId==p.Cow.Global,"Original selected Cow disappeared before source");
                if(AuxBrushContains(cow->r_CurrentTilePositionX,cow->r_CurrentTilePositionY,x,y,width) ||
                    AuxBrushContains(cow->r_NextTilePositionX2,cow->r_NextTilePositionY2,x,y,width))return false;
            }
            return true;
        }
        private static bool AuxBrushContains(int tx,int ty,int x,int y,int width)=>tx>=x-width/2 && tx<=x && ty>=y-width/2 && ty<=y;
        private unsafe void StageAuxRemovalSource(UnitSnapshot original)
        {
            var p=auxRemoval;Require(!Plugin.GeneralObserver.IsActive,"Auxiliary native setup requires General OFF");
            for(int dy=-18;dy<=18;dy++)for(int dx=-18;dx<=18;dx++)
            {
                int x=cx+dx,y=cy+dy;bool clear=true;
                for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)
                {int tile=runtime.Native.Tile(x+ox,y+oy);if(!MoatWorkerSourceEligible(tile) || GameTileManagerAPI.Instance.GetTileUnitId(tile)!=0)clear=false;}
                if(!clear || !AuxSourceAccessSafe(x,y,1))continue;
                log("aux-removal-native-setup role="+p.Role+" unit="+p.Actor.Id+"/"+p.Actor.Global+" ai="+original.AIState+
                    " from="+original.X+","+original.Y+" target="+x+","+y+" originalHeartbeat="+AuxHeartbeat(AuxWorkplace(),p.Slot));
                Require(runtime.Native.Relocate(p.Actor.Id,x,y),"Audited auxiliary test-position normalization failed");
                Require(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==p.Actor.Global &&
                    u->r_CurrentHealth==p.Actor.Health && u->r_AIState==original.AIState && u->r_TribeId==p.Actor.Tribe &&
                    u->r_UnitChimp==p.Type && u->r_ControllableForPlayerId==owner &&
                    u->r_LinkedProductionBuildingId==p.Building && u->r_LinkedProductionBuildingGlobalId==p.BuildingGlobal &&
                    runtime.Native.Word(u,0x9EE)==p.Slot && runtime.Native.Word(u,0x9F0)==p.Building &&
                    AuxEmployeeId(AuxWorkplace(),p.Slot)==p.Actor.Id && AuxEmployeeGlobal(AuxWorkplace(),p.Slot)==p.Actor.Global &&
                    (p.Role==AuxRemovalRole.Miner1?AuxHeartbeat(AuxWorkplace(),p.Slot)==0:AuxHeartbeat(AuxWorkplace(),p.Slot)==p.Actor.Id),
                    "Native setup changed original civilian identity/health/AI/employment/heartbeat");
                p.X=x;p.Y=y;p.Width=1;return;
            }
            throw new InvalidOperationException("No ordinary empty controlled auxiliary source outside protected accesses");
        }
        private unsafe void ApplyAuxRemovalSource()
        {
            RequireAuxRemovalWorld();var p=auxRemoval;var before=runtime.SnapshotUnit(p.Actor.Id,true,false);
            Require(before!=null && before.CivilianRemoval && before.Unsupported==null && before.GlobalId==p.Actor.Global &&
                before.X==p.Before.X && before.Y==p.Before.Y && before.NextX==p.Before.NextX && before.NextY==p.Before.NextY &&
                before.Flags==p.Before.Flags && before.Substep==p.Before.Substep && AuxSourceAccessSafe(p.X,p.Y,p.Width),"Natural auxiliary source changed before writer");
            Require(before.Job==null && !before.HasCapturedVegetation && before.Candidates.Count==0,
                "Aux removal source unexpectedly depended on legacy job/vegetation/BFS capture");
            if(p.Role==AuxRemovalRole.TannerCow)
            {
                Require(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && AuxTannerCow(u,out Actor cow,out int slot) &&
                    cow.Id==p.Cow.Id && cow.Global==p.Cow.Global && slot==p.CowSlot,"Reciprocal natural Cow reservation changed before writer");
            }
            p.Before=before;p.BeforeRemovals=Plugin.GeneralObserver.RemovalCount;
            Plugin.SetGeneralFixtureSession(expectedEnabled);long captures=Plugin.GeneralObserver.CaptureCount;
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.X,p.Y),p.Y,p.Width,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(Plugin.GeneralObserver.RemovalCount==p.BeforeRemovals,"Aux removal ran synchronously inside construction");
            bool readAfter=UnitAccess.TryGetById(p.Actor.Id,out GameUnit* after,out _) && after!=null;
            bool currentMoat=(tiles.GetTilePropertyFlag(before.TileId)&TilePropertyFlag.IsMoat)!=0;
            bool nextCacheValid=before.NextTileId>=0 && before.NextTileId<NativeRelocation.TileCapacity;
            bool nextCacheMoat=before.OrdinaryStep && nextCacheValid && (tiles.GetTilePropertyFlag(before.NextTileId)&TilePropertyFlag.IsMoat)!=0;
            log("aux-removal-source-after role="+p.Role+" expected="+p.Actor.Id+"/"+p.Actor.Global+" readable="+readAfter+
                " expectedCurrent="+before.X+","+before.Y+":"+before.TileId+" ordinaryStep="+before.OrdinaryStep+
                " nextCache="+before.NextTileId+" currentMoat="+currentMoat+" nextCacheValid="+nextCacheValid+" nextCacheMoat="+nextCacheMoat+
                " nextCacheRequired="+before.OrdinaryStep+" removalDelta="+(Plugin.GeneralObserver.RemovalCount-p.BeforeRemovals)+
                (readAfter?" actualGlobal="+after->r_GlobalId+" type="+after->r_UnitChimp+" owner="+after->r_ControllableForPlayerId+
                    " life="+after->r_AliveState+" hp="+after->r_CurrentHealth+" tribe="+after->r_TribeId+" ai="+after->r_AIState+
                    " current="+after->r_CurrentTilePositionX+","+after->r_CurrentTilePositionY+":"+after->r_CurrentPositionTileId+
                    " next="+after->r_NextTilePositionX2+","+after->r_NextTilePositionY2+":"+after->r_NextPositionTileId2+
                    " flags="+after->r_PathPlanStateBitFlags+" substep="+after->r_MovementSubstep:" unavailable=true"));
            Require(readAfter && after->r_GlobalId==p.Actor.Global && UnitAccess.IsReallyAlive(after) &&
                after->r_CurrentHealth==p.Actor.Health && after->r_UnitChimp==p.Type && after->r_ControllableForPlayerId==owner && after->r_TribeId==p.Actor.Tribe &&
                after->r_CurrentTilePositionX==before.X && after->r_CurrentTilePositionY==before.Y && (int)after->r_CurrentPositionTileId==before.TileId &&
                after->r_NextTilePositionX2==before.NextX && after->r_NextTilePositionY2==before.NextY &&
                after->r_PathPlanStateBitFlags==before.Flags && after->r_MovementSubstep==before.Substep &&
                currentMoat && (!before.OrdinaryStep || nextCacheMoat) && !HasStructuralExit(p.Actor),"Actual auxiliary writer did not create the genuine unchanged-body trap");
            Require(expectedEnabled?Plugin.GeneralObserver.CaptureCount>captures:Plugin.GeneralObserver.CaptureCount==captures,"Aux source observer activation differs");
            p.Written=true;
            log("aux-removal-source role="+p.Role+" unit="+p.Actor.Id+"/"+p.Actor.Global+" enabled="+expectedEnabled+
                " slot="+p.Slot+" heartbeat="+(p.Role==AuxRemovalRole.TannerCow?"not-applicable":AuxHeartbeat(AuxWorkplace(),p.Slot).ToString())+" activeHeartbeatClaim="+(p.Role==AuxRemovalRole.Miller || p.Role==AuxRemovalRole.Miner2)+
                " hiddenMiner1Active214Claim=false hiddenMillerActive21CClaim=false cow="+p.Cow?.Id+"/"+p.Cow?.Global+
                " current="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+" brush="+p.X+","+p.Y+":"+p.Width+" unsuppressed=true");
        }
        private static void ObserveAuxRemoval(int tick)
        {
            var f=auxRemovalFixture;var p=f?.auxRemoval;if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                f.RequireAuxRemovalWorld();
                if(!p.Written)
                {
                    f.CacheAuxNaturalCows();
                    if(p.Role==AuxRemovalRole.Miller && !p.MillerInputReady && p.WheatCapacityPending && f.MillerAddedYardsReady())
                    {
                        // AllyTransfers requires the public synchronous callback's
                        // mutation scope. The UI Engine monitor alone is insufficient.
                        Require(AllyTransfers.TryGetCapacity(f.owner,eGoods.STORED_RAW_WHEAT,out p.WheatCapacity,out string reason),reason);
                        p.WheatCapacityPending=false;
                        f.log("aux-removal-wheat-capacity tick="+tick+" free="+p.WheatCapacity.FreeAmount+" hasStorage="+p.WheatCapacity.HasStorage);
                    }
                    return;
                }
                p.Ticks++;f.ObserveAuxCleanup();
            }
            catch(Exception e){p.Fault=e.Message;f.log("aux-removal-first-failure "+e.Message);}
        }
        private unsafe void ObserveAuxCleanup()
        {
            var p=auxRemoval;var b=AuxWorkplace();
            bool same=UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==p.Actor.Global;
            if(same && u->r_AliveState==AliveState.MarkedForDeletion)p.SawLife3=true;
            if(!same || u->r_AliveState==AliveState.None)p.Gone=true;
            int employee=AuxEmployeeId(b,p.Slot);uint global=AuxEmployeeGlobal(b,p.Slot);
            if(employee!=p.Actor.Id || global!=p.Actor.Global)p.EmployeeCleared=true;
            if(p.Role!=AuxRemovalRole.TannerCow && AuxHeartbeat(b,p.Slot)!=p.Actor.Id)p.HeartbeatCleared=true;
            if(p.Gone && !p.ChainsChecked){CheckAuxOldChains();p.ChainsChecked=true;}
            p.Replacement=false;
            if(p.EmployeeCleared && employee>0 && global!=p.Actor.Global &&
                UnitAccess.TryGetById(employee,out GameUnit* replacement,out _) && replacement!=null && replacement->r_GlobalId==global &&
                UnitAccess.IsReallyAlive(replacement) && replacement->r_UnitChimp==p.Type && replacement->r_ControllableForPlayerId==owner &&
                replacement->r_LinkedProductionBuildingId==p.Building && replacement->r_LinkedProductionBuildingGlobalId==p.BuildingGlobal &&
                runtime.Native.Word(replacement,0x9EE)==p.Slot && runtime.Native.Word(replacement,0x9F0)==p.Building)
            {
                p.Replacement=true;
                if(employee!=p.ReplacementId || global!=p.ReplacementGlobal)
                {
                    p.ReplacementHeartbeat=false;
                    log("aux-removal-natural-replacement role="+p.Role+" original="+p.Actor.Id+"/"+p.Actor.Global+
                        " replacement="+employee+"/"+global+" type="+replacement->r_UnitChimp);
                }
                p.ReplacementId=employee;p.ReplacementGlobal=global;
                if(p.Role!=AuxRemovalRole.TannerCow && AuxHeartbeat(b,p.Slot)==employee)p.ReplacementHeartbeat=true;
            }
            if(p.Cow!=null)ObserveAuxCow();
        }
        private unsafe void ObserveAuxCow()
        {
            var p=auxRemoval;
            Require(BuildingAccess.TryGetById(farmerOutput.Farm,out GameBuilding* farm,out _) && farm!=null && farm->r_GlobalId==farmerOutput.FarmGlobal &&
                farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_CATTLEFARM && farm->r_AliveState==AliveState.IsAlive,"Original Cow farm changed during cleanup");
            if(AuxEmployeeId(farm,p.CowSlot)!=p.Cow.Id || AuxEmployeeGlobal(farm,p.CowSlot)!=p.Cow.Global)p.CowFarmSlotCleared=true;
            bool same=UnitAccess.TryGetById(p.Cow.Id,out GameUnit* cow,out _) && cow!=null && cow->r_GlobalId==p.Cow.Global;
            if(!same || cow->r_AliveState==AliveState.None){p.CowGone=true;return;}
            Require(cow->r_UnitChimp==eChimps.CHIMP_TYPE_COW && cow->r_ControllableForPlayerId==owner,"Original reserved Cow changed type/owner");
            // Complete CURRENT1587804/5/6/110 retires without HP/8F8 writes.
            // Unrelated damage is not evidence of the tested reservation cleanup.
            Require(cow->r_CurrentHealth==p.Cow.Health && runtime.Native.Word(cow,0x8F8)==0,
                "Reserved Cow suffered unrelated health/death-marker change during auxiliary cleanup");
            if(cow->r_AIState==5)p.CowTimeout=true;
            if(cow->r_AIState==6 || cow->r_AIState==110 || cow->r_AliveState==AliveState.MarkedForDeletion)p.CowTerminal=true;
            if(p.LastCowAI!=cow->r_AIState)
            {
                p.LastCowAI=cow->r_AIState;log("aux-removal-cow-followup cow="+p.Cow.Id+"/"+p.Cow.Global+" life="+cow->r_AliveState+
                    " ai="+cow->r_AIState+" timer920="+runtime.Native.Word(cow,0x920)+" tanner="+runtime.Native.Word(cow,0x9F6)+"/"+runtime.Native.JobDword(cow,0x9F8));
            }
            // Cow5 has no Tanner-life recheck; its1001-update timeout is legitimate.
            // Stale reference bytes alone do not prove an active reservation.
            Require(cow->r_AIState==4 || cow->r_AIState==5 || cow->r_AIState==6 || cow->r_AIState==110 || cow->r_AliveState==AliveState.MarkedForDeletion,
                "Reserved Cow entered an unaudited post-removal state");
        }
        private unsafe void CheckAuxOldChains()
        {
            var p=auxRemoval;
            //19BA40 updates Current72C and both XY pairs, not inactive packedNext73C.
            // Only an accepted ordinary step makes that cache a physical Next witness.
            foreach(int tile in !p.Before.OrdinaryStep || p.Before.TileId==p.Before.NextTileId?new[]{p.Before.TileId}:new[]{p.Before.TileId,p.Before.NextTileId})
            {
                int id=GameTileManagerAPI.Instance.GetTileUnitId(tile);var seen=new HashSet<int>();
                while(id!=0)
                {
                    Require(id>0 && id<=NativeRelocation.MaximumUnitId && seen.Add(id),"Aux old source has an invalid/cyclic complete chain");
                    Require(UnitAccess.TryGetById(id,out GameUnit* member,out _) && member!=null,"Aux old source chain is unreadable");
                    if(id==p.Actor.Id)
                        Require(member->r_GlobalId!=p.Actor.Global && member->r_GlobalId!=0 && UnitAccess.IsReallyAlive(member) &&
                            runtime.Native.OrdinaryGround(tile) && member->r_CurrentPositionTileId==(uint)tile &&
                            runtime.Native.Tile(member->r_CurrentTilePositionX,member->r_CurrentTilePositionY)==tile,"Old auxiliary lifetime/stale reused-ID remains on completed Moat");
                    id=member->r_InterestingTodo4MaybeRandomPathing;
                }
            }
            log("aux-removal-old-chains role="+p.Role+" original="+p.Actor.Id+"/"+p.Actor.Global+" current="+p.Before.TileId+
                " nextCache="+p.Before.NextTileId+" activeNext="+p.Before.OrdinaryStep+" complete=true originalEntries=0");
        }
        private void CheckAuxRemovalResult()
        {
            RequireAuxRemovalWorld();var p=auxRemoval;Require(p.Fault==null,p.Fault);ObserveAuxCleanup();
            long own=Plugin.GeneralObserver.RemovalCount-p.BeforeRemovals;
            Require(own==(expectedEnabled?1:0),"Auxiliary source own removal count differs: "+own);
            if(expectedEnabled)
            {
                Require(p.SawLife3 && p.Gone && p.ChainsChecked && p.EmployeeCleared && (p.Role==AuxRemovalRole.TannerCow || p.HeartbeatCleared) && p.Replacement && (p.Role==AuxRemovalRole.TannerCow || p.ReplacementHeartbeat),
                    "Auxiliary native teardown/recount/natural completed replacement heartbeat is incomplete");
                if(p.Cow!=null)Require(p.CowFarmSlotCleared && p.CowTerminal && p.CowGone,
                    "Original reserved Cow has not completed its allowed native timeout/retirement/removal");
            }
            if(p.Gone)CheckAuxOldChains();
            log("aux-removal-result role="+p.Role+" enabled="+expectedEnabled+" original="+p.Actor.Id+"/"+p.Actor.Global+
                " heartbeatRequired="+(p.Role!=AuxRemovalRole.TannerCow)+" ownRemoval="+own+" life3="+p.SawLife3+" gone="+p.Gone+" employeeCleared="+p.EmployeeCleared+" heartbeatCleared="+p.HeartbeatCleared+
                " replacement="+p.ReplacementId+"/"+p.ReplacementGlobal+" replacementLive="+p.Replacement+" replacementHeartbeat="+p.ReplacementHeartbeat+
                " cowFarmSlotCleared="+p.CowFarmSlotCleared+" cowTimeout="+p.CowTimeout+" cowTerminal="+p.CowTerminal+" cowGone="+p.CowGone+
                " activeMiner1RemovalClaim=false activeMiller21CRemovalClaim=false observedPrePassTicks="+p.Ticks);
        }
        private uint[] AuxRemovalGeometry()
        {
            var p=auxRemoval;var tiles=GameTileManagerAPI.Instance;var result=new List<uint>();
            for(int yy=p.Y-p.Width/2;yy<=p.Y;yy++)for(int xx=p.X-p.Width/2;xx<=p.X;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);Require(tile>=0,"Aux source geometry outside map");
                result.Add(runtime.Native.Flags(tile));result.Add(tiles.GetTileHeight(tile));result.Add((uint)tiles.GetTileType(tile));
                result.Add(runtime.Native.Connections(tile));result.Add(unchecked((uint)runtime.Native.Building(tile)));result.Add(unchecked((uint)tiles.GetTileVegetationId(tile)));
            }
            return result.ToArray();
        }
        private void RepeatAuxRemovalNoop()
        {
            RequireAuxRemovalWorld();var p=auxRemoval;var before=AuxRemovalGeometry();long removals=Plugin.GeneralObserver.RemovalCount;
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.X,p.Y),p.Y,p.Width,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            var after=AuxRemovalGeometry();Require(before.Length==after.Length,"Aux no-op geometry length changed");
            for(int i=0;i<before.Length;i++)Require(before[i]==after[i],"Settled auxiliary moat no-op changed actual geometry");
            Require(Plugin.GeneralObserver.RemovalCount==removals,"Aux no-op synchronously removed another unit");
        }
        private unsafe void LogAuxAcquisition(int sample)
        {
            var p=auxRemoval;var b=AuxWorkplace();int slots=p.Role==AuxRemovalRole.Miller?3:1;
            log("aux-removal-acquire-progress role="+p.Role+" sample="+sample+" cows="+p.NaturalCows.Count+" rejection="+p.LastReject+
                " millerInput="+p.MillerInputReady+" capacityPending="+p.WheatCapacityPending);
            for(int index=0;index<slots;index++)
            {
                int slot=p.Role==AuxRemovalRole.Miner2?1:index,id=AuxEmployeeId(b,slot);uint global=AuxEmployeeGlobal(b,slot);
                bool read=UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null;
                log("aux-removal-acquire-employee slot="+slot+" pair="+id+"/"+global+" readable="+read+
                    (read?" actualGlobal="+u->r_GlobalId+" type="+u->r_UnitChimp+" life="+u->r_AliveState+" ai="+u->r_AIState+
                        " flags="+u->r_PathPlanStateBitFlags+" substep="+u->r_MovementSubstep+" hidden="+runtime.Native.Word(u,0x9B0)+
                        " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY:" unavailable=true")+
                    " heartbeat="+(p.Role==AuxRemovalRole.TannerCow?"not-applicable":AuxHeartbeat(b,slot).ToString()));
            }
            if(p.Role==AuxRemovalRole.TannerCow)
            {
                Require(BuildingAccess.TryGetById(farmerOutput.Farm,out GameBuilding* farm,out _) && farm!=null && farm->r_GlobalId==farmerOutput.FarmGlobal,
                    "Cow acquisition farm identity changed");
                log("aux-removal-acquire-cattle-farm farm="+farmerOutput.Farm+"/"+farmerOutput.FarmGlobal+" life="+farm->r_AliveState+
                    " worker="+AuxEmployeeId(farm,0)+"/"+AuxEmployeeGlobal(farm,0)+" phase="+FarmerPhase(farm)+" ready="+FarmerReady(farm));
            }
        }
        private IEnumerable<AutomationWait> AuxRemovalCase(AutomationRun run)
        {
            AuxRemovalRole role=scenario.Contains("-miller-")?AuxRemovalRole.Miller:scenario.Contains("-miner1-")?AuxRemovalRole.Miner1:
                scenario.Contains("-miner2-")?AuxRemovalRole.Miner2:AuxRemovalRole.TannerCow;
            auxRemoval=new AuxRemovalProof{Role=role,Type=role==AuxRemovalRole.Miller?eChimps.CHIMP_TYPE_MILLER:
                role==AuxRemovalRole.Miner1?eChimps.CHIMP_TYPE_MINER1:role==AuxRemovalRole.Miner2?eChimps.CHIMP_TYPE_MINER2:eChimps.CHIMP_TYPE_TANNER,
                Kind=role==AuxRemovalRole.Miller?eStructs.STRUCT_MILL:role==AuxRemovalRole.TannerCow?eStructs.STRUCT_TANNERS_WORKSHOP:eStructs.STRUCT_IRON_MINE};
            var p=auxRemoval;Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,"Aux removal has no exact initial world");
            try
            {
                Execute(run,SetupAuxRemoval);Director.instance.SetEngineFrameRate(250);
                for(int sample=0;sample<8000 && p.Before==null;sample++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{
                        RequireAuxRemovalWorld();Require(p.Fault==null,p.Fault);TryPrepareAuxRemoval();
                        if(sample%128==0)LogAuxAcquisition(sample);
                        return true;
                    });
                }
                Require(p.Before!=null,"No genuine admissible natural auxiliary source; no hidden/AI/job/cargo state was manufactured");
                run.InSimulation(run.CurrentMission,()=>{ApplyAuxRemovalSource();return true;});
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,64);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                // Marker != teardown. Permit ordinary C60F0/182B00 passes and
                // conditional hiring. Cow5 may need its genuine1001 actor updates.
                for(int window=0;expectedEnabled && window<16;window++)
                {
                    bool complete=run.InSimulation(run.CurrentMission,()=>{
                        RequireAuxRemovalWorld();Require(p.Fault==null,p.Fault);ObserveAuxCleanup();
                        log("aux-removal-cleanup-progress role="+p.Role+" window="+window+" gone="+p.Gone+" employeeClear="+p.EmployeeCleared+
                            " heartbeatClear="+p.HeartbeatCleared+" replacement="+p.Replacement+" replacementHeartbeat="+p.ReplacementHeartbeat+" cowGone="+p.CowGone);
                        return p.Gone && p.EmployeeCleared && (p.Role==AuxRemovalRole.TannerCow || p.HeartbeatCleared) && p.Replacement && (p.Role==AuxRemovalRole.TannerCow || p.ReplacementHeartbeat) && (p.Cow==null || p.CowGone && p.CowFarmSlotCleared);
                    });
                    if(complete)break;
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,200);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                }
                run.InSimulation(run.CurrentMission,()=>{CheckAuxRemovalResult();RepeatAuxRemovalNoop();return true;});
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{CheckAuxRemovalResult();RequireAuxRemovalWorld();Plugin.SetGeneralFixtureSession(false);Director.instance.SetEngineFrameRate(250);return true;});
            }
            finally
            {
                // Iterator disposal can follow SDK Finish/world replacement.
                // Retire local observations only; never access a replacement world.
                p.Active=false;if(ReferenceEquals(auxRemovalFixture,this))auxRemovalFixture=null;
                if(millerWorld!=null)millerWorld.Active=false;if(farmerOutput!=null)farmerOutput.Active=false;
            }
        }
    }
}
