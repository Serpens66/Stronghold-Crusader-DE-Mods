using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
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
        // Fixed-map natural workplace setup. No AI, job, cargo, animation or
        // resource writes: all Pitch output comes from ADF70/1332B0 normally.
        private sealed class PitchProof
        {
            internal int Worksite,InitWait,Remaining,Batches,Deposits,Yields,HiddenTicks,NoopX,NoopY,WorkX,WorkY,ProductionEnds;
            internal uint WorksiteGlobal,TribeGlobal,LastProduction;
            internal long Session,World;
            internal Actor Actor;internal UnitSnapshot Before;internal AutomaticWorkerMutation Mutation;
            internal bool Active=true,Ready,Mutated,Retired,Complete,YieldPending,OwnResume;
            internal int YieldEpoch,DeliveryStore,DeliveryX,DeliveryY,Handoffs;
            internal uint DeliveryGlobal;internal string Fault,SourceReject;
            internal readonly long[] YieldAmounts=new long[2];
            internal readonly bool[] Produced=new bool[2],WasHidden=new bool[2],Unhidden=new bool[2];
            internal readonly PitchHiddenPose[] PreHide=new PitchHiddenPose[2];
            internal readonly Stack<PitchGoodsFrame> Frames=new Stack<PitchGoodsFrame>();
        }
        private sealed class PitchHiddenPose
        {internal int X,Y,Tile,HomeX,HomeY,GoalX,GoalY;}
        private sealed class PitchGoodsFrame
        {internal int Building,Before,Cargo;internal uint Global;}
        private PitchProof pitchProof;
        private static volatile Fixture pitchFixture;private static bool pitchRegistered;
        private IEnumerable<AutomationStep> AutomaticPitchSteps()
        {
            yield return new AutomationStep("natural-pitch-r3-two-physical-pitch-charges",TimeSpan.FromSeconds(1200),AutomaticPitch,true);
            yield return new AutomationStep("natural-pitch-settled-real-moat-noop",TimeSpan.FromSeconds(90),PitchNoop,true);
        }
        private void PitchOwnedWorld()
        {
            var p=pitchProof;
            Require(p!=null && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==session && sid==p.Session && world==p.World,"Pitch fixture crossed its exact native world");
        }
        private void PitchFault(string reason)
        {var p=pitchProof;if(p!=null && p.Fault==null){p.Fault=reason;log("automatic-pitch-first-failure "+reason);}}
        private void ArmPitch()
        {
            if(!pitchRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-pitch-r3",ObservePitch,out string ticks,order:100),ticks);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-pitch-original-r3",new PitchMoveObserver(),out string moves,order:-100,
                    enabled:()=>pitchFixture!=null && !pitchFixture.stopped && pitchFixture.pitchProof!=null &&
                        pitchFixture.pitchProof.Active && pitchFixture.pitchProof.Mutated && !pitchFixture.pitchProof.Retired),moves);
                pitchRegistered=true;
            }
            pitchFixture=this;
        }
        private unsafe GameBuilding* PitchWorksite()
        {
            var p=pitchProof;
            Require(BuildingAccess.TryGetById(p.Worksite,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.WorksiteGlobal &&
                b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_PITCH_DIGGER,
                "Original Pitch worksite lifetime changed");return b;
        }
        private unsafe void SetupPitch()
        {
            var p=pitchProof;PitchOwnedWorld();
            Require(string.Equals(System.IO.Path.GetFileName(map),"4spielerkarte.map",StringComparison.OrdinalIgnoreCase),
                "Pitch stage requires explicit fixed4spielerkarte.map; another map needs its own qualification");
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Pitch fixture requires actual human owner");
            FindSite();CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            millerWorld=new MillerWorld();ExtendMillerStorage();
            FindPitchSite(out p.WorkX,out p.WorkY);
            p.Worksite=CreateBakerPrefab(p.WorkX,p.WorkY,eMappers.MAPPER_PITCH_WORKINGS,eStructs.STRUCT_PITCH_DIGGER);
            Require(BuildingAccess.TryGetById(p.Worksite,out GameBuilding* b,out _) && b!=null,"Pitch constructor returned no worksite");
            p.WorksiteGlobal=b->r_GlobalId;p.LastProduction=b->N0001200A;
            // Explicit controlled free prefab setup; the real IsPitch resource
            // requirement and native initialized footprint/access are still proved.
            log("automatic-pitch-world worksite="+p.Worksite+"/"+p.WorksiteGlobal+" at="+p.WorkX+","+p.WorkY+
                " map="+map+" placementBypass=true humanPlacementClaim=false rawWorkerWrites=false resourceWrites=false");
        }
        private unsafe void FindPitchSite(out int x,out int y)
        {
            var tiles=GameTileManagerAPI.Instance;var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int size=tiles.GetCurrentMapSize();Require(size>0 && size<=800,"Pitch finder has invalid native map bounds");
            int candidates=0,resource=0,blocked=0,floor=0,access=0,best=int.MaxValue;x=0;y=0;bool found=false;
            // One global bounded setup scan, independent of Keep construction
            // range. Only the later actual R3 trap source must be Keep-near.
            // Minimum Chebyshev distance, then row/column order, fixes selection.
            for(int yy=2;yy<size-5;yy++)
            for(int xx=2;xx<size-5;xx++)
            {
                int distance=Math.Max(Math.Abs(xx-keep.X),Math.Abs(yy-keep.Y));if(distance<20 || distance>=best)continue;
                candidates++;bool any=false;
                for(int dy=0;dy<4;dy++)for(int dx=0;dx<4;dx++)
                {int tile=runtime.Native.Tile(xx+dx,yy+dy);if(tile>=0 && (tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.IsPitch)!=0)any=true;}
                if(!any){resource++;continue;}bool free=true,flat=true;int height=-1;
                for(int dy=-2;dy<=5;dy++)for(int dx=-2;dx<=5;dx++)
                {
                    int tile=runtime.Native.Tile(xx+dx,yy+dy);if(tile<0){free=false;continue;}
                    var flags=tiles.GetTilePropertyFlag(tile);
                    TilePropertyFlag forbidden=TilePropertyFlag.RealityEdge|TilePropertyFlag.IsBuilding|TilePropertyFlag.IsWall|
                        TilePropertyFlag.IsElevated|TilePropertyFlag.Sea|TilePropertyFlag.River|TilePropertyFlag.Ford|
                        TilePropertyFlag.ImpassableEdge|TilePropertyFlag.HasStone|TilePropertyFlag.HasIron|TilePropertyFlag.IsFarm|
                        TilePropertyFlag.IsMoat|TilePropertyFlag.IsTree|TilePropertyFlag.IsAppleFarm|TilePropertyFlag.IsFarmFence|
                        TilePropertyFlag.IsWheat|TilePropertyFlag.IsHops;
                    if((flags&forbidden)!=0 || tiles.GetTileBuildingId(tile)!=0 || tiles.GetTileVegetationId(tile)!=0 || runtime.Native.Head(tile)!=0)free=false;
                    int h=tiles.GetTileHeight(tile);if(height<0)height=h;if(h!=height || h!=8 || tiles.GetTileDefaultHeight(tile)!=8)flat=false;
                }
                if(!free){blocked++;continue;}if(!flat){floor++;continue;}
                // Conservative complete resource footprint and free surrounding
                // envelope; actual constructor access is rechecked after init.
                if(!AppleStepAccessHasExit(xx-1,yy+1)){access++;continue;}
                x=xx;y=yy;best=distance;found=true;
            }
            if(found){log("automatic-pitch-site candidates="+candidates+" noPitch="+resource+" blocked="+blocked+
                " floor="+floor+" access="+access+" chosen="+x+","+y+" keepDistance="+best+" realPitch4x4=true globalSearch=true");return;}
            throw new InvalidOperationException("Fixed-map Pitch site missing: candidates="+candidates+" noPitch="+resource+
                " blocked="+blocked+" floor="+floor+" access="+access+"; no terrain/AI/range changes or random retry");
        }
        private unsafe bool PitchAlive()
        {
            PitchOwnedWorld();var p=pitchProof;if(p.Actor==null || p.Retired)return false;
            if(!UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=p.Actor.Global)
            {p.Retired=true;if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;return false;}
            if(u->r_AliveState==AliveState.MarkedForDeletion)
            {if(automaticWorkerMoves!=null)automaticWorkerMoves.DeletionObserved=true;p.Retired=true;return false;}
            var worksite=PitchWorksite();
            Require(UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==eChimps.CHIMP_TYPE_PITCHMAN && u->r_ControllableForPlayerId==owner &&
                worksite->r_WorkerId_1==p.Actor.Id && worksite->N0000176A==p.Actor.Global &&
                u->r_LinkedProductionBuildingId==p.Worksite && u->r_LinkedProductionBuildingGlobalId==p.WorksiteGlobal &&
                u->r_CurrentHealth==p.Actor.Health && u->r_TribeId==p.Actor.Tribe &&
                runtime.Native.TryTribeGlobal(p.Actor.Tribe,out uint tribe) && tribe==p.TribeGlobal,"Original Pitch identity/HP/tribe/workplace changed");
            int epoch=p.Yields;
            if(runtime.Native.Word(u,0x9B0)!=0)
            {
                var pose=epoch<2?p.PreHide[epoch]:null;
                Require(pose!=null && runtime.Native.Word(u,0x9B0)==1 && (u->r_AIState==1 || u->r_AIState==109 && runtime.Native.Word(u,0x91A)==1) &&
                    PitchSameSettledPose(u,pose) && CountInTileChain(pose.Tile,p.Actor.Id)==0,
                    "Pitch hidden production lost its observed pre-hide109/saved1 pose/home/goal/unlink proof");
            }
            else
            {
                CheckActor(p.Actor,true);
                // R4 commits109/saved1 and exposes16 real updates before Hide.
                // AI1 exit instead uses109/saved2 and immediately UNHIDES; it
                // must never be mistaken for the incoming production witness.
                if(u->r_AIState==109 && runtime.Native.Word(u,0x91A)==1)
                {
                    Require(epoch<2 && u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 &&
                        runtime.Native.JobDword(u,0x740)==0,"Pitch pre-hide phase lacks settled original home return");
                    if(p.PreHide[epoch]==null)
                    {p.PreHide[epoch]=new PitchHiddenPose{X=u->r_CurrentTilePositionX,Y=u->r_CurrentTilePositionY,
                        Tile=(int)u->r_CurrentPositionTileId,HomeX=runtime.Native.Word(u,0x992),HomeY=runtime.Native.Word(u,0x994),
                        GoalX=u->r_TargetTilePositionX2,GoalY=u->r_TargetTilePositionY2};
                        log("automatic-pitch-prehide epoch="+epoch+" unit="+p.Actor.Id+"/"+p.Actor.Global+
                            " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" home="+runtime.Native.Word(u,0x992)+","+runtime.Native.Word(u,0x994)+
                            " ai=109 saved=1 visibleOccupancy=1");}
                    Require(PitchSameSettledPose(u,p.PreHide[epoch]),"Pitch settled pre-hide production pose changed");
                }
            }
            return true;
        }
        private unsafe bool PitchSameSettledPose(GameUnit* u,PitchHiddenPose p)=>
            u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 && runtime.Native.JobDword(u,0x740)==0 &&
            u->r_CurrentTilePositionX==p.X && u->r_CurrentTilePositionY==p.Y && u->r_CurrentPositionTileId==(uint)p.Tile &&
            p.Tile==runtime.Native.Tile(p.X,p.Y) && runtime.Native.Word(u,0x992)==p.HomeX && runtime.Native.Word(u,0x994)==p.HomeY &&
            u->r_TargetTilePositionX2==p.GoalX && u->r_TargetTilePositionY2==p.GoalY;
        private static unsafe void ObservePitch(int tick)
        {
            var f=pitchFixture;var p=f?.pitchProof;if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                f.PitchOwnedWorld();
                if(!p.Ready)
                {
                    Require(++p.InitWait<=64,"Pitch setup initialization exceeded64 ticks");
                    Require(BuildingAccess.TryGetById(p.Worksite,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.WorksiteGlobal,
                        "Pitch setup building lifetime changed");
                    if(b->r_AliveState==AliveState.NeedsInit || !f.MillerAddedYardsReady())return;
                    int size=checked((int)b->r_OccupyTileGridSize);
                    Require(b->r_AliveState==AliveState.IsAlive && size>0 && size<=8 &&
                        b->r_TilePositionXBegin>=p.WorkX-2 && b->r_TilePositionXBegin+size-1<=p.WorkX+5 &&
                        b->r_TilePositionYBegin>=p.WorkY-2 && b->r_TilePositionYBegin+size-1<=p.WorkY+5 &&
                        AppleStepAccessHasExit(b->r_TileAccessPositionX,b->r_TileAccessPositionY),"Pitch native worksite/access did not initialize");
                    p.Ready=true;
                }
                if(p.Actor==null)
                {
                    var h=f.PitchWorksite();int id=h->r_WorkerId_1;if(id==0)return;
                    Require(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==h->N0000176A,
                        "Worksite employee identity is unresolved");
                    // A natural employee may still await native type conversion.
                    // Bind only its positively completed Pitch/workplace identity.
                    if(!UnitAccess.IsReallyAlive(u) || u->r_UnitChimp!=eChimps.CHIMP_TYPE_PITCHMAN || u->r_ControllableForPlayerId!=f.owner ||
                        u->r_LinkedProductionBuildingId!=p.Worksite || u->r_LinkedProductionBuildingGlobalId!=p.WorksiteGlobal)return;
                    p.Actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                        X=u->r_CurrentTilePositionX,Y=u->r_CurrentTilePositionY,SourceX=u->r_CurrentTilePositionX,SourceY=u->r_CurrentTilePositionY};
                    Require(f.runtime.Native.TryTribeGlobal(p.Actor.Tribe,out p.TribeGlobal),"Pitch tribe unavailable");
                    f.log("automatic-pitch-natural-employee unit="+id+"/"+p.Actor.Global+" worksite="+p.Worksite+"/"+p.WorksiteGlobal);
                }
                if(!f.PitchAlive())return;var actor=f.Resolve(p.Actor);var worksite=f.PitchWorksite();
                int epoch=p.Yields;
                if(f.runtime.Native.Word(actor,0x9B0)!=0)
                {p.HiddenTicks++;if(epoch<2)p.WasHidden[epoch]=true;}
                if(worksite->N0001200A!=p.LastProduction)
                {
                    Require(epoch<2 && worksite->N0001200A==p.LastProduction+1 && p.PreHide[epoch]!=null && p.WasHidden[epoch] &&
                        !p.Produced[epoch],"Pitch animation completion lacks its exact observed hidden work cycle");
                    p.LastProduction=worksite->N0001200A;p.Produced[epoch]=true;p.ProductionEnds++;
                    f.log("automatic-pitch-production-end epoch="+epoch+" actualCounter="+p.LastProduction+" tick="+tick);
                }
                if(epoch<2 && actor->r_AIState==109 && f.runtime.Native.Word(actor,0x91A)==2)
                {
                    Require(p.Produced[epoch] && p.WasHidden[epoch] && p.PreHide[epoch]!=null && actor->r_CarryGoodAmount==1 &&
                        f.runtime.Native.Word(actor,0x9B0)==0 && f.PitchSameSettledPose(actor,p.PreHide[epoch]),
                        "Pitch natural output lacks completed hidden work, native cargo1 and physical unhide");p.Unhidden[epoch]=true;
                }
                if(p.Batches==2 && !p.Complete)
                {Require(actor->r_CarryGoodAmount==0 && p.Yields==2 && p.ProductionEnds==2 && p.WasHidden[0] && p.WasHidden[1] &&
                    p.Unhidden[0] && p.Unhidden[1] && !p.YieldPending && p.Frames.Count==0,
                    "Pitch final deposit did not complete two genuine production chains");p.Complete=true;}
            }
            catch(Exception e){f.PitchFault(e.Message);}
        }
        // Exact BD490 storage rule: all24 actual goods buffers, excluding
        // Pitch itself, must be empty. A stale visual/local-storage type is
        // not the native capacity predicate and cannot prove a handoff.
        private static unsafe int PitchStoreFree(GameBuilding* b)
        {
            int* goods=(int*)&b->r_NullAmount;
            for(int good=1;good<(int)eGoods.Count;good++)
                if(good!=(int)eGoods.STORED_PITCH_RAW && goods[good]!=0)return 0;
            return Math.Max(0,16-goods[(int)eGoods.STORED_PITCH_RAW]);
        }
        private unsafe void ObservePitchGoods(AddGoodToGoodsyardEventArgs a)
        {
            var p=pitchProof;if(stopped || p==null || !p.Active || p.Actor==null || p.Fault!=null || p.Batches>=2 || a==null ||
                a.Good!=eGoods.STORED_PITCH_RAW || GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                Require(PitchAlive(),"Pitch lifetime ended during storage operation");var u=Resolve(p.Actor);
                Require(BuildingAccess.TryGetById(a.BuildingId,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==unchecked((uint)a.BuildingGlobalId) &&
                    b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD,
                    "Pitch credit lost actual Store identity");int amount=checked((int)b->r_PitchRawAmount);
                if(a.Phase==EventHookPhase.Pre)
                {
                    Require(a.Add && a.AddAmount==1 && a.Capacity==16 && u->r_AIState==3 && p.Remaining>0 && u->r_CarryGoodAmount==p.Remaining &&
                        runtime.Native.Word(u,0x98E)==a.BuildingId && runtime.Native.JobDword(u,0x6F8)==b->r_GlobalId && PitchStoreFree(b)>0 &&
                        Math.Abs(u->r_CurrentTilePositionX-b->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-b->r_TileAccessPositionY)<3,
                        "Pitch+1 lacks R3 cargo, exact Store/global, physical contact or capacity16");
                    if(expectedEnabled && p.Mutated && p.Batches==0)Require(p.OwnResume && a.BuildingId==p.DeliveryStore && b->r_GlobalId==p.DeliveryGlobal &&
                        u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,"Original Pitch charge lacks owned resume/native Store handoff");
                    p.Frames.Push(new PitchGoodsFrame{Building=a.BuildingId,Global=b->r_GlobalId,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(a.Phase==EventHookPhase.Post)
                {
                    Require(p.Frames.Count>0,"Pitch goods Post lacks Pre");var old=p.Frames.Pop();
                    Require(old.Building==a.BuildingId && old.Global==b->r_GlobalId && a.AddAmount==1 && a.Capacity==16 &&
                        amount==old.Before+1 && u->r_CarryGoodAmount==old.Cargo,"Pitch native storage delta/lifetime/cargo changed");
                    p.Deposits++;if(--p.Remaining==0)p.Batches++;
                    log("automatic-pitch-native-credit building="+a.BuildingId+"/"+b->r_GlobalId+" actual="+old.Before+"->"+amount+
                        " batches="+p.Batches+" remaining="+p.Remaining);
                }
            }
            catch(Exception e){PitchFault(e.Message);}
        }
        private unsafe void ObservePitchYield(UnitCalculateBonusYieldEventArgs a)
        {
            var p=pitchProof;if(stopped || p==null || !p.Active || p.Actor==null || p.Fault!=null || a==null || a.UnitId!=p.Actor.Id)return;
            try
            {
                Require(PitchAlive(),"Pitch yield lost original lifetime");var u=Resolve(p.Actor);
                Require(u->r_AIState==3 && u->r_CarryGoodAmount==1 && a.GoodAmount==1 && a.b50PercentBonus==1 && p.Yields<2 &&
                    p.Produced[p.Yields] && p.WasHidden[p.Yields] && p.Unhidden[p.Yields],"Pitch yield is not actual AI3 natural Pitch1 branch");
                if(a.Phase==EventHookPhase.Pre){Require(!p.YieldPending,"Nested Pitch yield");p.YieldPending=true;p.YieldEpoch=p.Yields;}
                else if(a.Phase==EventHookPhase.Post)
                {Require(p.YieldPending && p.YieldEpoch==p.Yields && a.ReturnValue>0 && a.ReturnValue<=16,"Pitch natural yield lacks bounded positive result");
                    p.YieldAmounts[p.Yields]=a.ReturnValue;p.Remaining=checked((int)a.ReturnValue);p.Yields++;p.YieldPending=false;
                    log("automatic-pitch-natural-yield epoch="+(p.Yields-1)+" amount="+a.ReturnValue+" productionEnds="+p.ProductionEnds);}
            }
            catch(Exception e){PitchFault(e.Message);}
        }
        private unsafe bool PitchFutureSafe(UnitSnapshot before,int x,int y)
        {
            var h=PitchWorksite();var u=Resolve(pitchProof.Actor);
            if(x>=pitchProof.WorkX-3 && x<=pitchProof.WorkX+6 && y>=pitchProof.WorkY-3 && y<=pitchProof.WorkY+6)return false;
            if(AppleStepContains(before.RequestedX,before.RequestedY,x,y,1) || AppleStepContains(h->r_TileAccessPositionX,h->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(u,0x992),runtime.Native.Word(u,0x994),x,y,1))return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int i=0;i<buildings.Length;i++){var b=buildings.GetValuePointer(i);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1))return false;}
            return true;
        }
        private bool RejectPitchSource(string reason){pitchProof.SourceReject=reason;return false;}
        private unsafe bool FindPitchSource()
        {
            var p=pitchProof;if(!p.Ready || p.Actor==null)return RejectPitchSource("world-or-natural-employee-pending");
            if(!PitchAlive())return RejectPitchSource("original-life-retired");var u=Resolve(p.Actor);
            if(u->r_AIState!=3 || u->r_CarryGoodAmount==0 || p.Yields!=1 || !p.Produced[0] || p.Batches!=0 || p.Deposits!=0 || u->r_CarryGoodAmount!=p.Remaining)return RejectPitchSource("not-first-natural-loaded-R3-before-deposit");
            var before=runtime.SnapshotUnit(p.Actor.Id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null)return RejectPitchSource("snapshot:"+(before?.Unsupported??"missing-job"));
            if(!TryStartedMoatAnchor(before,out int x,out int y))return RejectPitchSource("no-ordinary-started-free-2x2-envelope");
            if(!PitchFutureSafe(before,x,y))return RejectPitchSource("2x2-halo-overlaps-future-home-or-yard-access");
            int store=runtime.Native.Word(u,0x98E);uint global=runtime.Native.JobDword(u,0x6F8);
            if(!BuildingAccess.TryGetById(store,out GameBuilding* b,out _) || b==null || b->r_GlobalId!=global ||
                b->r_AliveState!=AliveState.IsAlive || b->r_PlayerIdOwner!=owner || b->r_BuildingType!=eStructs.STRUCT_GOODS_YARD || PitchStoreFree(b)<4)return RejectPitchSource("source-yard-lifetime-or-free4-pitch-capacity");
            if(!new NativeWorkerResumeWorld(runtime.Native).TryCapture(before,out var ticket,out string rejected))return RejectPitchSource("native-ticket:"+rejected);
            if(ticket.Bindings.WorkplaceId!=p.Worksite || ticket.Bindings.WorkplaceGlobal!=p.WorksiteGlobal || ticket.Bindings.StoreId!=store ||
                ticket.Bindings.StoreGlobal!=global)return RejectPitchSource("ticket-home-store-binding-mismatch");
            p.Before=before;p.NoopX=x;p.NoopY=y;p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;p.SourceReject=null;return true;
        }
        private sealed class PitchMoveObserver:IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var f=pitchFixture;var p=f?.pitchProof;
                if(f==null || p==null || c==null || p.Actor==null || c.UnitId!=p.Actor.Id || c.SessionId!=p.Session || c.WorldGeneration!=p.World)return null;
                try
                {
                    if(!f.PitchAlive())return null;var u=f.Resolve(p.Actor);if(u->r_AIState!=3 || p.Batches!=0)return null;
                    bool own=c.OriginOwnerGuid==Plugin.Guid;
                    // OFF classifies native movement/refusal/deletion, without
                    // imposing ON's owned-continuation handoff prerequisite.
                    if(!own && !f.expectedEnabled)return null;
                    Require(c.Extra==0 && u->r_CarryGoodAmount==p.Remaining,"R3 move changed cargo/mode");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD,"R3 move lost live Yard");
                    if(own){IntPtr ptr=(IntPtr)u;Require(store==p.DeliveryStore && global==p.DeliveryGlobal && c.X==p.Before.RequestedX && c.Y==p.Before.RequestedY &&
                        p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,o=>f.runtime.Native.JobWord((GameUnit*)ptr,o),o=>f.runtime.Native.JobDword((GameUnit*)ptr,o)),
                        "Own Pitch move changed original job/store/goal");}
                    else
                    {
                        Require(p.OwnResume && string.IsNullOrEmpty(c.OriginOwnerGuid) && c.ParentInvocationId==0 && p.Handoffs<16 &&
                            c.X==b->r_TileAccessPositionX && c.Y==b->r_TileAccessPositionY && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "Yard handoff lacks original continuation/new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null && old->r_GlobalId==p.DeliveryGlobal &&
                            old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner && old->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 || Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 ||
                             PitchStoreFree(old)<=0),"Yard handoff lacks native contact/capacity rejection");
                    }
                    return new PitchMoveResult(f,p,c,own,store,global);
                }
                catch(Exception e){f.PitchFault(e.Message);return null;}
            }
        }
        private sealed class PitchMoveResult:IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly PitchProof p;private readonly UnitMoveContext c;private readonly bool own;
            private readonly int store;private readonly uint global;private int done;
            internal PitchMoveResult(Fixture f,PitchProof p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion r)
            {
                if(Interlocked.Exchange(ref done,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(r!=null && ReferenceEquals(r.Context,c) && r.IsCurrentWorld && r.NativeInvoked && r.NativeReturned && r.Failure==null && f.PitchAlive(),
                        "Pitch move lacks coherent native completion");var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==3 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store && f.runtime.Native.JobDword(u,0x6F8)==global,
                        "Pitch move changed original cargo/store");if(own && r.NativeResult==0)return;
                    Require(r.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,"Pitch continuation/handoff lacks Native1");
                    if(own)p.OwnResume=true;else p.Handoffs++;p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception e){f.PitchFault(e.Message);}
            }
        }
        private unsafe string PitchFacts()
        {
            var p=pitchProof;string unit="unassigned";
            if(p.Actor!=null && UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null)
                unit=p.Actor.Id+"/"+u->r_GlobalId+" life="+u->r_AliveState+" ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+
                    " ss="+u->r_MovementSubstep+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    " goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+" cargo="+u->r_CarryGoodAmount;
            return unit+" productionEnds="+p.ProductionEnds+" yields="+p.Yields+" batches="+p.Batches+" sourceReject="+p.SourceReject+" fault="+p.Fault;
        }
        private IEnumerable<AutomationWait> AutomaticPitch(AutomationRun run)
        {
            pitchProof=new PitchProof();var p=pitchProof;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,"Pitch has no initial native world");
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObservePitchGoods))
            using(var yields=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObservePitchYield))
            try
            {
                ArmPitch();Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupPitch();});Director.instance.SetEngineFrameRate(50);
                for(int n=0;n<8000 && p.Before==null;n++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{PitchOwnedWorld();Require(p.Fault==null,p.Fault);FindPitchSource();
                        if(n%128==0)log("automatic-pitch-acquire sample="+n+" "+PitchFacts());return true;});
                }
                Require(p.Before!=null,"No natural first R3 ordinary source before any original pitch deposit: "+PitchFacts());
                run.InSimulation(run.CurrentMission,()=>{
                    PitchOwnedWorld();Require(PitchAlive() && p.Fault==null && PitchFutureSafe(p.Before,p.NoopX,p.NoopY),p.Fault??"Pitch source changed");
                    actors.Add(p.Actor);p.Mutated=true;p.Mutation=ApplyAutomaticStartedWorkerMoat(p.Actor,p.Before,p.NoopX,p.NoopY);return true;});
                Director.instance.SetEngineFrameRate(250);
                for(int n=0;n<(expectedEnabled?8000:32);n++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    bool stop=run.InSimulation(run.CurrentMission,()=>{PitchOwnedWorld();Require(p.Fault==null,p.Fault);
                        if(n%128==0)log("automatic-pitch-progress sample="+n+" "+PitchFacts());return p.Retired || p.Complete;});if(stop)break;
                }
                run.InSimulation(run.CurrentMission,()=>{
                    PitchOwnedWorld();Require(p.Fault==null && p.Frames.Count==0 && !p.YieldPending,p.Fault??"Pitch native event pair incomplete");
                    if(expectedEnabled)Require(p.Complete && p.OwnResume && p.Batches==2 && p.Deposits==p.YieldAmounts[0]+p.YieldAmounts[1],
                        "Pitch did not complete full original and naturally produced second batch");
                    EndAutomaticWorkerCase(p.Mutation,p.Complete);
                    log("automatic-pitch-result enabled="+expectedEnabled+" rescuePhase=3 fullOriginalBatch="+(p.Batches>=1)+" fullSecondBatch="+p.Complete+
                        " yield0="+p.YieldAmounts[0]+" yield1="+p.YieldAmounts[1]+" physicalCredits="+p.Deposits+" hiddenTicks="+p.HiddenTicks+" productionEnds="+p.ProductionEnds+" handoffs="+p.Handoffs);return true;});
            }
            finally
            {p.Active=false;if(ReferenceEquals(pitchFixture,this))pitchFixture=null;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;}
        }
        private uint[] PitchNoopGeometry()
        {
            var p=pitchProof;var t=GameTileManagerAPI.Instance;var result=new uint[150];int i=0;
            for(int y=p.NoopY-2;y<=p.NoopY+2;y++)for(int x=p.NoopX-2;x<=p.NoopX+2;x++)
            {int tile=runtime.Native.Tile(x,y);Require(tile>=0,"Pitch no-op halo outside active map");
                result[i++]=runtime.Native.Flags(tile);result[i++]=t.GetTileHeight(tile);result[i++]=(uint)t.GetTileType(tile);
                result[i++]=runtime.Native.Connections(tile);result[i++]=unchecked((uint)runtime.Native.Building(tile));result[i++]=unchecked((uint)t.GetTileVegetationId(tile));}
            return result;
        }
        private static bool PitchSameGeometry(uint[] a,uint[] b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private IEnumerable<AutomationWait> PitchNoop(AutomationRun run)
        {
            var p=pitchProof;Require(p!=null && p.Mutated,"Pitch no-op lacks settled original source");uint[] geometry=null;long attempts=0,rescues=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{ApplyPitchNoop(out geometry,out attempts,out rescues);return true;});
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{
                    PitchOwnedWorld();Require(PitchSameGeometry(geometry,PitchNoopGeometry()) && Plugin.GeneralObserver.AttemptCount==attempts &&
                        Plugin.GeneralObserver.RescueCount==rescues && Plugin.GeneralObserver.PendingCount==0 && automaticWorkerMoves.OwnBegins==0 &&
                        automaticWorkerMoves.Fault==null,"Pitch no-op produced additional recovery");
                    if(AutomaticActorAlive(p.Actor))Require(PitchAlive(),"No-op lost exact original Pitch life");
                    log("automatic-pitch-noop enabled="+expectedEnabled+" actualRepeatedWriter=true geometryUnchanged=true advancedTicks=8 additionalOwnCalls=0");
                    Plugin.SetGeneralFixtureSession(false);return true;});
            }
            finally{if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;}
        }
        private unsafe void ApplyPitchNoop(out uint[] geometry,out long attempts,out long rescues)
        {
            var p=pitchProof;PitchOwnedWorld();Require(p.Fault==null && Plugin.GeneralObserver.PendingCount==0,"Pitch no-op has unsettled original recovery");
            bool alive=AutomaticActorAlive(p.Actor);UnitSnapshot before=null;ushort cargo=0;int ai=0;
            if(alive){Require(PitchAlive(),"No-op lost exact Pitch life");before=runtime.SnapshotUnit(p.Actor.Id,true,false);var u=Resolve(p.Actor);cargo=u->r_CarryGoodAmount;ai=u->r_AIState;
                BeginAutomaticWorkerMoves(p.Actor,p.Before.RequestedX,p.Before.RequestedY);}
            else {Require(automaticWorkerMoves!=null && automaticWorkerMoves.DeletionObserved,"OFF lifetime disappeared without native deletion");
                BeginAutomaticWorkerMoves(p.Actor,p.Before.RequestedX,p.Before.RequestedY);automaticWorkerMoves.DeletionObserved=true;}
            Plugin.SetGeneralFixtureSession(expectedEnabled);var o=Plugin.GeneralObserver;
            attempts=o.AttemptCount;rescues=o.RescueCount;geometry=PitchNoopGeometry();var tiles=GameTileManagerAPI.Instance;
            for(int y=p.NoopY-1;y<=p.NoopY;y++)for(int x=p.NoopX-1;x<=p.NoopX;x++)
            {int tile=runtime.Native.Tile(x,y);Require((tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.IsMoat)!=0 && runtime.Native.Head(tile)==0,
                "Pitch repeated footprint is not complete empty Moat");}
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.NoopX,p.NoopY),p.NoopY,2,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(PitchSameGeometry(geometry,PitchNoopGeometry()) && o.AttemptCount==attempts && o.RescueCount==rescues && o.PendingCount==0,
                "Repeated native Pitch moat changed geometry/evidence");
            if(alive)
            {var after=runtime.SnapshotUnit(p.Actor.Id,true,false);var u=Resolve(p.Actor);IntPtr ptr=(IntPtr)u;
                Require(before!=null && after!=null && before.GlobalId==after.GlobalId && before.X==after.X && before.Y==after.Y &&
                    before.NextX==after.NextX && before.NextY==after.NextY && before.RequestedX==after.RequestedX && before.RequestedY==after.RequestedY &&
                    before.Flags==after.Flags && before.Substep==after.Substep && ai==u->r_AIState && cargo==u->r_CarryGoodAmount &&
                    (before.Job==null || before.Job.Matches(u->r_UnitChimp,u->r_AIState,o2=>runtime.Native.JobWord((GameUnit*)ptr,o2),o2=>runtime.Native.JobDword((GameUnit*)ptr,o2))),
                    "Repeated Pitch writer changed live pose/intent/job/cargo");Require(PitchAlive(),"No-op lost exact Pitch life");}
        }
    }
}
