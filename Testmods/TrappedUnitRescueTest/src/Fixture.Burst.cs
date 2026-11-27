using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class BurstSite
        {internal int X,Y,Height,Count,Bridge;}
        private sealed class BurstGroup
        {internal int Tribe,Leader,TargetX,TargetY,SourceX,SourceY;internal uint Global;internal readonly List<Actor> Members=new List<Actor>();}
        private sealed class BurstProof
        {
            internal long Session,World,Captures,Attempts,Rescues,BeforeTick;
            internal int Population,LastTick,BeginTick,ObservedTicks,MaximumActions,ExternalMoves;
            internal readonly HashSet<int> ActorIds=new HashSet<int>();
            internal bool Active=true,Written,GroundControl,OverlapTargets,PassiveControl;internal string Fault;
            internal readonly HashSet<int> PassiveObserved=new HashSet<int>(),PassiveBound=new HashSet<int>(),PassiveEscaped=new HashSet<int>();
            internal readonly HashSet<int> OriginallyActive=new HashSet<int>(),LeftSource=new HashSet<int>();
            internal int OwnedMoves,HoldBeginTick,HoldReleaseTick;internal bool HoldStarted,HoldReleased;
            internal readonly List<BurstSite> Sites=new List<BurstSite>();
            internal readonly List<BurstGroup> Groups=new List<BurstGroup>();
            internal readonly List<GroupMemberGoal> Goals=new List<GroupMemberGoal>();
        }
        private BurstProof burst;
        private static volatile Fixture burstFixture;
        private static bool burstRegistered;
        private IEnumerable<AutomationStep> BurstSteps()
        {yield return new AutomationStep("real-construction-complete-burst",TimeSpan.FromSeconds(1200),BurstCase,true);}
        private bool BurstWorld()=>!stopped && burst!=null && burst.Active && session==burst.Session &&
            WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==burst.Session && world==burst.World;
        private void RequireBurstWorld()=>Require(BurstWorld(),"Burst crossed its exact original session/native world");
        private void ArmBurst()
        {
            if(!burstRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-burst-before",BurstBeforeTick,out string before,order:-100),before);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-burst-after",BurstAfterTick,out string after,order:100),after);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-burst-external-move",new BurstExternalMoveObserver(),out string move,
                    order:-200,enabled:()=>burstFixture!=null && !burstFixture.stopped && burstFixture.burst!=null && burstFixture.burst.Active &&
                        burstFixture.burst.Written && (burstFixture.burst.PassiveControl || burstFixture.burst.ExternalMoves<16)),move);
                burstRegistered=true;
            }
            burstFixture=this;
        }
        private sealed class BurstExternalMoveObserver : IUnitMoveObserver
        {
            public IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var f=burstFixture;var p=f?.burst;
                if(f==null || p==null || !p.Active || !p.Written || f.stopped || c==null || !p.ActorIds.Contains(c.UnitId) ||
                    c.SessionId!=p.Session || c.WorldGeneration!=p.World || !f.BurstWorld())return null;
                var goal=f.BurstOwnedGoal(c.UnitId);if(goal==null)return null;
                bool own=c.OriginOwnerGuid==Plugin.Guid && Plugin.GeneralObserver.IsRecovering;
                if(p.PassiveControl)
                {
                    if(own)
                    {
                        p.OwnedMoves++;
                        if(p.OwnedMoves<=16)f.log("burst-owned-move sample="+p.OwnedMoves+" unit="+goal.Actor.Id+"/"+goal.Actor.Global+
                            " target="+c.X+","+c.Y+" originalCapturedGoal="+goal.X+","+goal.Y+" boundaryKnownPassive="+p.PassiveBound.Contains(c.UnitId));
                        if(c.X!=goal.X || c.Y!=goal.Y || p.PassiveBound.Contains(c.UnitId))
                            p.Fault="Passive burst own Move changed original captured goal or replayed boundary-known passive actor: "+c.UnitId;
                    }
                    else p.PassiveBound.Remove(c.UnitId); // A new external order owns subsequent intent.
                }
                if(own || p.ExternalMoves>=16)return null;
                var snapshot=f.runtime.SnapshotUnit(c.UnitId,true,false);
                if(snapshot==null || snapshot.GlobalId!=goal.Actor.Global || (uint)snapshot.TileId>=NativeRelocation.TileCapacity ||
                    f.runtime.Native.OrdinaryGround(snapshot.TileId))return null;
                int number=++p.ExternalMoves;
                f.log("burst-external-move-pre sample="+number+" invocation="+c.InvocationId+" unit="+goal.Actor.Id+"/"+goal.Actor.Global+
                    " target="+c.X+","+c.Y+" extra="+c.Extra+" origin="+(c.OriginOwnerGuid??"native")+
                    " originalGoal="+goal.X+","+goal.Y+" current="+snapshot?.X+","+snapshot?.Y+" next="+snapshot?.NextX+","+snapshot?.NextY+
                    " actualGlobal="+snapshot?.GlobalId+" ai="+snapshot?.AIState+" flags="+snapshot?.Flags+" ss="+snapshot?.Substep+" requested="+snapshot?.RequestedX+","+snapshot?.RequestedY+
                    " classify="+(snapshot==null?"unavailable":snapshot.Unsupported??"supported")+" orderSerialBefore="+Plugin.GeneralObserver.GetOrderSerial(c.UnitId));
                return new BurstExternalMoveCompletion(f,p,c,number);
            }
        }
        private sealed class BurstExternalMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture fixture;private readonly BurstProof proof;private readonly UnitMoveContext context;private readonly int sample;private int completed;
            internal BurstExternalMoveCompletion(Fixture f,BurstProof p,UnitMoveContext c,int number){fixture=f;proof=p;context=c;sample=number;}
            public void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || result==null || !ReferenceEquals(result.Context,context))return;
                UnitSnapshot snapshot=null;
                if(proof.Active && !fixture.stopped && fixture.BurstWorld())snapshot=fixture.runtime.SnapshotUnit(context.UnitId,true,false);
                fixture.log("burst-external-move-post sample="+sample+" invocation="+context.InvocationId+" unit="+context.UnitId+
                    " currentWorld="+result.IsCurrentWorld+" nativeInvoked="+result.NativeInvoked+" nativeReturned="+result.NativeReturned+
                    " nativeResult="+result.NativeResult+" failure="+(result.Failure?.GetType().Name??"none")+
                    " current="+snapshot?.X+","+snapshot?.Y+" next="+snapshot?.NextX+","+snapshot?.NextY+" actualGlobal="+snapshot?.GlobalId+" ai="+snapshot?.AIState+
                    " flags="+snapshot?.Flags+" ss="+snapshot?.Substep+" requested="+snapshot?.RequestedX+","+snapshot?.RequestedY+
                    " classify="+(snapshot==null?"unavailable":snapshot.Unsupported??"supported")+" orderSerialAfter="+Plugin.GeneralObserver.GetOrderSerial(context.UnitId));
            }
        }
        // Test-only scheduling gate. Capture, native execution and P7 observation stay live;
        // only our deferred drain waits for a genuine Vanilla active-to-idle transition.
        internal static bool HoldRecoveryTick(int tick)
        {
            var f=burstFixture;var p=f?.burst;
            if(f==null || p==null || !p.Active || !p.Written || !p.PassiveControl || p.HoldReleased || p.Fault!=null || !f.BurstWorld())return false;
            if(!p.HoldStarted)
            {
                p.HoldStarted=true;p.HoldBeginTick=tick;
                f.log("burst-passive-hold-begin tick="+tick+" session="+p.Session+" world="+p.World+
                    " originallyActive="+p.OriginallyActive.Count+" pending="+Plugin.GeneralObserver.PendingCount+" maximumNativeTicks=4096 captureAndP7RemainActive=true");
            }
            uint elapsed=unchecked((uint)(tick-p.HoldBeginTick));
            if(p.PassiveObserved.Count>0)
            {
                p.HoldReleased=true;p.HoldReleaseTick=tick;
                f.log("burst-passive-hold-release tick="+tick+" elapsedNativeTicks="+elapsed+" passiveObserved="+p.PassiveObserved.Count+
                    " pending="+Plugin.GeneralObserver.PendingCount+" proofAgeNotRenewed=true");
                return false;
            }
            if(elapsed>=4096)
            {
                p.HoldReleased=true;p.Fault="Passive burst setup produced no original-active-to-passive illegal-source witness within 4096 native ticks";
                f.log("burst-passive-hold-timeout tick="+tick+" elapsedNativeTicks="+elapsed+" originallyActive="+p.OriginallyActive.Count+
                    " pending="+Plugin.GeneralObserver.PendingCount+" setupFailed=true");
                return false;
            }
            return true;
        }
        private static void BurstBeforeTick(int tick)
        {
            var f=burstFixture;var p=f?.burst;if(f==null || p==null || !p.Active || f.stopped)return;
            try
            {
                f.RequireBurstWorld();p.LastTick=tick;
                if(p.Written){p.BeginTick=tick;p.BeforeTick=Plugin.GeneralObserver.AttemptCount;if(p.PassiveControl)f.ObserveBurstPassiveBoundary(tick);}
            }
            catch(Exception e){p.Fault=e.Message;}
        }
        private unsafe void ObserveBurstPassiveBoundary(int tick)
        {
            foreach(var goal in burst.Goals)
            {
                var a=goal.Actor;var u=Resolve(a);
                Require(UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth==a.Health && u->r_TribeId==a.Tribe &&
                    runtime.Native.TryTribeGlobal(a.Tribe,out uint stamp) && runtime.Native.JobDword(u,0x93C)==stamp,
                    "Passive burst boundary lost original identity/health/tribe: "+a.Id);
                if(u->r_CurrentTilePositionX!=a.SourceX || u->r_CurrentTilePositionY!=a.SourceY)burst.LeftSource.Add(a.Id);
                bool passive=u->r_AIState==0 && u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 &&
                    runtime.Native.JobDword(u,0x740)==0 && u->r_CurrentTilePositionX==a.SourceX && u->r_CurrentTilePositionY==a.SourceY &&
                    (int)u->r_CurrentPositionTileId==runtime.Native.Tile(a.SourceX,a.SourceY) &&
                    u->r_NextTilePositionX2==a.SourceX && u->r_NextTilePositionY2==a.SourceY &&
                    u->r_TargetTilePositionX2==a.SourceX && u->r_TargetTilePositionY2==a.SourceY;
                if(!passive)continue;
                burst.PassiveBound.Add(a.Id);
                if(!burst.OriginallyActive.Contains(a.Id) || burst.LeftSource.Contains(a.Id) || runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId))continue;
                if(burst.PassiveObserved.Add(a.Id))
                {
                    log("burst-passive-before tick="+tick+" unit="+a.Id+"/"+a.Global+
                        " originallyActive=true stillOriginalIllegalSource=true beforeFirstEscape=true ai=0 flags=0 substep=8 marker740=0 current="+a.SourceX+","+a.SourceY+" requestedIsCurrent=true originalHealth="+a.Health+" tribe="+a.Tribe);
                    if(burst.PassiveObserved.Count<=8)LogBurstPassiveRaw("first-passive",goal,tick);
                }
            }
        }
        private unsafe void LogBurstPassiveRaw(string boundary,GroupMemberGoal goal,int tick)
        {
            var a=goal.Actor;var u=Resolve(a);IntPtr pointer=(IntPtr)u;
            bool command=CommandJourneySnapshot.TryCaptureStationary(u->r_UnitChimp,u->r_AIState,
                offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),offset=>runtime.Native.JobDword((GameUnit*)pointer,offset),out _);
            var text=new StringBuilder();
            foreach(int offset in new[]{0x8FC,0x708,0x6EC,0x8EC,0x8F6,0x8F8,0x9B0,0x9C8,0x9F4,0x996,0x99C,0x9F6})
                text.Append(" word").Append(offset.ToString("X")).Append('=').Append(runtime.Native.JobWord(u,offset));
            foreach(int offset in new[]{0x740,0x998,0x6F8,0x9F8,0xA00})
                text.Append(" dword").Append(offset.ToString("X")).Append('=').Append(runtime.Native.JobDword(u,offset));
            log("burst-passive-raw boundary="+boundary+" tick="+tick+" unit="+a.Id+"/"+a.Global+" type="+u->r_UnitChimp+
                " owner="+u->r_ControllableForPlayerId+" life="+u->r_AliveState+" health="+u->r_CurrentHealth+" tribe="+u->r_TribeId+
                " ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+" ss="+u->r_MovementSubstep+
                " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" currentTile="+u->r_CurrentPositionTileId+
                " next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+" nextTile="+u->r_NextPositionTileId2+
                " requested="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+" originalGoal="+goal.X+","+goal.Y+
                " ordinary="+runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId)+" stationaryCommandCapture="+command+
                " independentIdlePhase="+CommandJourneySnapshot.IsIndependentIdlePhase(u->r_UnitChimp,u->r_AIState)+text);
        }
        private unsafe void CheckBurstPassiveExhausted()
        {
            if(!burst.PassiveControl || !burst.HoldReleased || burst.PassiveObserved.Count==0 || Plugin.GeneralObserver.PendingCount!=0 ||
                unchecked((uint)(burst.LastTick-burst.HoldReleaseTick))<1 || BurstPhysicallyFree())return;
            LogBurstBacklog("terminal-work-exhausted");int logged=0;
            foreach(var goal in burst.Goals)
            {
                if(BurstCurrentPhysicallyFree(goal.Actor))continue;
                LogBurstPendingActor("terminal-work-exhausted","no-bank-original",goal,-1,-1,-1,-1,false,"original-manifest");
                LogBurstPassiveRaw("terminal-work-exhausted",goal,burst.LastTick);if(++logged==4)break;
            }
            Require(false,"Passive burst terminal work exhausted with original actors still on illegal ground; no future unrelated event assumed");
        }
        private static void BurstAfterTick(int tick)
        {
            var f=burstFixture;var p=f?.burst;if(f==null || p==null || !p.Active || !p.Written || f.stopped || p.Fault!=null)return;
            try
            {
                f.RequireBurstWorld();Require(p.BeginTick==tick,"Burst tick observers did not bracket the same native tick");
                long count=Plugin.GeneralObserver.AttemptCount-p.BeforeTick;
                Require(count>=0 && count<=8,"Burst exceeded eight General actions in one actual native tick: "+count);
                p.MaximumActions=Math.Max(p.MaximumActions,(int)count);p.ObservedTicks++;
                if(p.PassiveControl)f.ObserveBurstPassiveEscape(tick);
            }
            catch(Exception e){p.Fault=e.Message;}
        }
        private unsafe void ObserveBurstPassiveEscape(int tick)
        {
            foreach(var goal in burst.Goals)
            {
                var a=goal.Actor;
                if(!burst.PassiveObserved.Contains(a.Id) || !burst.PassiveBound.Contains(a.Id) || burst.PassiveEscaped.Contains(a.Id))continue;
                var u=Resolve(a);
                if(!BurstCurrentPhysicallyFree(a))continue;
                Require(UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth==a.Health && u->r_TribeId==a.Tribe &&
                    runtime.Native.TryTribeGlobal(a.Tribe,out uint stamp) && runtime.Native.JobDword(u,0x93C)==stamp,
                    "Passive burst escaped life lost original identity/health/tribe: "+a.Id);
                burst.PassiveEscaped.Add(a.Id);
                log("burst-passive-escaped tick="+tick+" unit="+a.Id+"/"+a.Global+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    " ordinary="+runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId)+
                    " legalExit="+(!runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId) && HasStructuralExit(a))+
                    " interveningExternalMove=false boundaryKnownPassive=true");
            }
        }
        private void FindBurstSites(int count)
        {
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            for(int radius=24;radius<=160 && burst.Sites.Count<count;radius+=8)
                for(int dy=-radius;dy<=radius && burst.Sites.Count<count;dy+=8)
                    for(int dx=-radius;dx<=radius && burst.Sites.Count<count;dx+=8)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                        int x=keep.X+dx,y=keep.Y+dy;bool separated=true;
                        foreach(var prior in burst.Sites)if(Math.Abs(prior.X-x)<=44 && Math.Abs(prior.Y-y)<=44){separated=false;break;}
                        if(!separated || !FlatSquare(x+2,y+2,20,out int height))continue;
                        for(int yy=y-18;yy<=y+22 && separated;yy++)for(int xx=x-18;xx<=x+22;xx++)
                        {int tile=runtime.Native.Tile(xx,yy);if(tile<0 || !runtime.Native.OrdinaryGround(tile)){separated=false;break;}}
                        if(!separated)continue;
                        burst.Sites.Add(new BurstSite{X=x,Y=y,Height=height});
                        log("burst-site index="+(burst.Sites.Count-1)+" origin="+x+","+y+" radius=20 flat=true ordinary=true empty=true");
                    }
            Require(burst.Sites.Count==count,"No complete disjoint41x41 ordinary flat site set for burst; found="+burst.Sites.Count+" required="+count);
        }
        private unsafe void SpawnBurst()
        {
            RequireBurstWorld();Require(actors.Count==0,"Burst cannot adopt previous fixture actors");
            var ids=new HashSet<int>();int groupSize=32;
            foreach(var site in burst.Sites)
            {
                int population=burst.Sites.Count==1?burst.Population:200;site.Count=population;BurstGroup group=null;
                for(int i=0;i<population;i++)
                {
                    if(i%groupSize==0)
                    {
                        int tribe=checked((int)GameTribeManagerAPI.Instance.Create(owner));Require(tribe>0,"Burst tribe allocation failed");
                        group=new BurstGroup{Tribe=tribe,SourceX=site.X+2,SourceY=site.Y+2,TargetX=site.X+2,TargetY=site.Y-10};burst.Groups.Add(group);
                    }
                    int x=site.X+((i&1)==0?0:4),y=site.Y+((i&2)==0?0:4);
                    int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,x,y,site.Height,eChimps.CHIMP_TYPE_SPEARMAN));
                    GameUnit* u=null;
                    Require(id>=1 && id<=NativeRelocation.MaximumUnitId && ids.Add(id) && UnitAccess.TryGetById(id,out u,out _) && u!=null &&
                        u->r_GlobalId!=0 && u->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && u->r_ControllableForPlayerId==owner &&
                        u->r_CurrentTilePositionX==x && u->r_CurrentTilePositionY==y,"Burst native spawn refused/projected/reused an actor");
                    Require(GameTribeManagerAPI.Instance.AssignUnit(group.Tribe,id) && u->r_TribeId==group.Tribe,"Burst native tribe assignment failed");
                    var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=eChimps.CHIMP_TYPE_SPEARMAN,Tribe=group.Tribe,X=x,Y=y,SourceX=x,SourceY=y};
                    actors.Add(actor);group.Members.Add(actor);burst.ActorIds.Add(id);
                }
            }
            Require(actors.Count==burst.Population,"Burst cannot accept a partially spawned population");
        }
        private void PlanBurstTargets()
        {
            if(burst.OverlapTargets)return;var planned=new List<BurstGroup>();
            foreach(var group in burst.Groups)
            {
                bool found=false;int checkedOrigins=0;
                for(int radius=24;radius<=96 && !found;radius+=12)
                    for(int dy=-radius;dy<=radius && !found;dy+=12)
                        for(int dx=-radius;dx<=radius && !found;dx+=12)
                        {
                            if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;checkedOrigins++;
                            int x=group.SourceX+dx,y=group.SourceY+dy;bool usable=true;
                            foreach(var prior in planned)
                                if(Math.Abs(x-prior.TargetX)<16 && Math.Abs(y-prior.TargetY)<16){usable=false;break;}
                            if(!usable)continue;
                            foreach(var site in burst.Sites)
                                if(x+6>=site.X && x-6<=site.X+4 && y+6>=site.Y && y-6<=site.Y+4){usable=false;break;}
                            if(!usable || !FlatSquare(x,y,6,out _))continue;
                            for(int yy=y-6;yy<=y+6 && usable;yy++)for(int xx=x-6;xx<=x+6;xx++)
                            {int tile=runtime.Native.Tile(xx,yy);if(tile<0 || !runtime.Native.OrdinaryGround(tile)){usable=false;break;}}
                            if(!usable)continue;
                            group.TargetX=x;group.TargetY=y;found=true;
                        }
                log("burst-target-plan layout=SPREAD tribe="+group.Tribe+" sourceCenter="+group.SourceX+","+group.SourceY+
                    " targetCenter="+group.TargetX+","+group.TargetY+" radius=6 separation=16 checkedOrigins="+checkedOrigins+" found="+found);
                Require(found,"No complete deterministic ordinary target patch for burst tribe "+group.Tribe);planned.Add(group);
            }
        }
        private unsafe void CommandBurst()
        {
            RequireBurstWorld();VerifyLiveAndRemember();PlanBurstTargets();var actualGoals=new HashSet<int>();
            foreach(var group in burst.Groups)
            {
                var members=new List<int>();
                Require(GameTribeManagerAPI.Instance.GetUnits(group.Tribe,members) && members.Count==group.Members.Count,"Burst tribe member count differs");
                var expected=new HashSet<int>();foreach(var a in group.Members)expected.Add(a.Id);
                Require(expected.SetEquals(members),"Burst tribe contains foreign/missing actor");
                group.Leader=GameTribeManagerAPI.Instance.GetLeaderUnitId(group.Tribe);Require(expected.Contains(group.Leader),"Burst leader is not an original member");
                Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(group.Tribe,group.TargetX,group.TargetY),"Burst native group command refused");
                foreach(var actor in group.Members)
                {
                    var before=runtime.SnapshotUnit(actor.Id,true,false);var u=Resolve(actor);
                    log("burst-command unit="+actor.Id+"/"+actor.Global+" ai="+u->r_AIState+" flags="+before?.Flags+" substep="+before?.Substep+
                        " requested="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+" pathLength="+u->r_PathPlanLength+" unsupported="+before?.Unsupported);
                    bool active=before!=null && before.Unsupported==null && before.Command!=null &&
                        (before.Flags==2 || before.Flags==4) && before.AIState==101;
                    bool passive=burst.PassiveControl && before!=null && before.Unsupported==null && before.AIState==0 && before.Flags==0 && before.Substep==8 &&
                        runtime.Native.JobDword(u,0x740)==0 && u->r_NextTilePositionX2==before.X && u->r_NextTilePositionY2==before.Y &&
                        u->r_TargetTilePositionX2==before.X && u->r_TargetTilePositionY2==before.Y;
                    Require(before!=null && before.CurrentWasOrdinary && before.X==actor.SourceX && before.Y==actor.SourceY && (active || passive),
                        "Burst did not publish genuine supported original command/passive source for "+actor.Id);
                    if(burst.PassiveControl && active)burst.OriginallyActive.Add(actor.Id);
                    if(group.Global==0)group.Global=before.TribeGlobalId;
                    Require(group.Global!=0 && before.TribeGlobalId==group.Global,"Burst tribe lifetime differs");
                    int x=u->r_TargetTilePositionX2,y=u->r_TargetTilePositionY2,tile=runtime.Native.Tile(x,y);
                    Require(tile>=0 && runtime.Native.OrdinaryGround(tile) && (passive || Math.Max(Math.Abs(x-actor.X),Math.Abs(y-actor.Y))>=3),
                        "Burst native individual destination is not separated ordinary ground");
                    if(!passive)foreach(var site in burst.Sites)Require(x<site.X || x>=site.X+5 || y<site.Y || y>=site.Y+5,"Burst native goal falls inside a future bridge");
                    if(!burst.OverlapTargets)
                        Require(Math.Abs(x-group.TargetX)<=6 && Math.Abs(y-group.TargetY)<=6 && actualGoals.Add(tile) && u->r_PathPlanLength>0,
                            "Burst SPREAD native goal is outside its prequalified patch, duplicated, or has no positive original path: "+actor.Id);
                    burst.Goals.Add(new GroupMemberGoal{Actor=actor,X=x,Y=y,Assigned=true});
                }
            }
            log("burst-target-layout layout="+(burst.OverlapTargets?"OVERLAP":"SPREAD")+" groups="+burst.Groups.Count+
                " population="+burst.Population+" distinctIndividualGoals="+(burst.OverlapTargets?-1:actualGoals.Count));
            // Full population, in bounded log batches. No hash stands in for an actor.
            for(int first=0;first<burst.Goals.Count;first+=32)
            {
                var text=new StringBuilder();int end=Math.Min(first+32,burst.Goals.Count);
                for(int i=first;i<end;i++)
                {
                    var g=burst.Goals[i];var a=g.Actor;if(text.Length!=0)text.Append(';');
                    text.Append(a.Id).Append('/').Append(a.Global).Append(':').Append(a.Health).Append(':').Append(a.Tribe)
                        .Append('@').Append(a.SourceX).Append(',').Append(a.SourceY).Append('>').Append(g.X).Append(',').Append(g.Y);
                }
                log("burst-manifest first="+first+" count="+(end-first)+" population="+burst.Population+" actors="+text);
            }
        }
        private unsafe void WriteBurst()
        {
            RequireBurstWorld();var observer=Plugin.GeneralObserver;Plugin.SetGeneralFixtureSession(expectedEnabled);
            Require(observer!=null && observer.IsActive==expectedEnabled && observer.PendingCount==0,"Burst initial activation/pending state differs");
            burst.Captures=observer.CaptureCount;burst.Attempts=observer.AttemptCount;burst.Rescues=observer.RescueCount;
            foreach(var site in burst.Sites)
            {
                // Each later footprint is outside every earlier original radius8.
                foreach(var actor in actors)
                    if(actor.SourceX!=site.X && actor.SourceX!=site.X+4 || actor.SourceY!=site.Y && actor.SourceY!=site.Y+4)
                        Require(Math.Abs(actor.SourceX-(site.X+2))>10 || Math.Abs(actor.SourceY-(site.Y+2))>10,"Burst writer overlaps another original field");
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,site.X,site.Y,eMappers.MAPPER_DRAWBRIDGE,
                    BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                site.Bridge=GameTileManagerAPI.Instance.GetTileBuildingId(runtime.Native.Tile(site.X,site.Y));
                Require(site.Bridge>0 && BridgeType(site.Bridge),"Burst original writer did not produce the real bridge");
                for(int y=site.Y;y<site.Y+5;y++)for(int x=site.X;x<site.X+5;x++)
                    Require(GameTileManagerAPI.Instance.GetTileBuildingId(runtime.Native.Tile(x,y))==site.Bridge,"Burst native bridge25-cell footprint differs");
            }
            if(burst.PassiveControl)
            {
                // Every one of this site's200 actors can consume a different local bank
                // cell. The three source fields are disjoint; no distant escape is assumed.
                foreach(var site in burst.Sites)for(int corner=0;corner<4;corner++)
                {
                    int x=site.X+((corner&1)==0?0:4),y=site.Y+((corner&2)==0?0:4),emptyBanks=0;
                    for(int yy=y-8;yy<=y+8;yy++)for(int xx=x-8;xx<=x+8;xx++)
                    {
                        int tile=runtime.Native.Tile(xx,yy);
                        if((uint)tile>=NativeRelocation.TileCapacity || !runtime.Native.OrdinaryGround(tile) || runtime.Native.Head(tile)!=0)continue;
                        if(NativeIdleEscapeWorld.HasPhysicalExit(new RescueCandidate{X=xx,Y=yy,Tile=tile},
                            runtime.Native.Tile,runtime.Native.OrdinaryGround,runtime.Native.Connections))emptyBanks++;
                    }
                    log("burst-passive-bank-precondition site="+site.X+","+site.Y+" corner="+x+","+y+
                        " radius=8 required="+site.Count+" actualEmptyOrdinaryPhysicalExit="+emptyBanks+" beforeRecovery=true");
                    Require(emptyBanks>=site.Count,"Passive burst site lacks a complete free local bank for its original population: "+x+","+y);
                }
            }
            int trapped=0,acceptedLegalNext=0;
            foreach(var actor in actors)
            {
                CheckActor(actor,true);Require(AtSource(actor),"Burst performed early recovery/movement inside paused writer");
                int tile=runtime.Native.Tile(actor.X,actor.Y);Require(!runtime.Native.OrdinaryGround(tile) && !HasStructuralExit(actor),"Burst preknown bridge corner did not actually trap Current");
                trapped++;var u=Resolve(actor);
                if(RescueRuntime.HasOrdinaryStep(u) && runtime.Native.OrdinaryGround((int)u->r_NextPositionTileId2))acceptedLegalNext++;
                if(expectedEnabled)Require(observer.IsInterestedInOrder(actor.Id),"Burst lost an original actor during automatic admission: "+actor.Id);
            }
            Require(observer.AttemptCount==burst.Attempts && observer.RescueCount==burst.Rescues,"Burst ran recovery before a later native tick");
            Require(trapped==burst.Population,"Burst did not trap the full original population");
            if(expectedEnabled)Require(observer.CaptureCount>burst.Captures && observer.PendingCount==burst.Population,"Burst failed complete513/600 automatic admission");
            else Require(observer.CaptureCount==burst.Captures && observer.PendingCount==0,"OFF burst captured unexpected recovery work");
            burst.Written=true;
            log("burst-source population="+burst.Population+" events="+burst.Sites.Count+" pending="+observer.PendingCount+
                " captures="+(observer.CaptureCount-burst.Captures)+" actualTraps="+trapped+" acceptedLegalNext="+acceptedLegalNext+
                " sourceTick="+burst.LastTick+" originalUnsuppressed=true directRescue=false explicitPlacementBypass=true");
        }
        private void MarkBurstGroundControl()
        {
            RequireBurstWorld();Require(burst.GroundControl && !expectedEnabled,"Ground control must remain genuinely OFF");
            Plugin.SetGeneralFixtureSession(false);var observer=Plugin.GeneralObserver;
            Require(observer!=null && !observer.IsActive && observer.PendingCount==0,"Ground control has active rescue evidence");
            burst.Captures=observer.CaptureCount;burst.Attempts=observer.AttemptCount;burst.Rescues=observer.RescueCount;
            foreach(var actor in actors)
            {CheckActor(actor,true);Require(AtSource(actor) && runtime.Native.OrdinaryGround(runtime.Native.Tile(actor.X,actor.Y)),"Ground control source is not original ordinary terrain");}
            burst.Written=true; // Observation phase only: deliberately no world writer.
            log("burst-ground-control population="+burst.Population+" groups="+burst.Groups.Count+" sourceTick="+burst.LastTick+
                " bridgeWrites=0 rescueEnabled=false targetLayout="+(burst.OverlapTargets?"OVERLAP":"SPREAD")+
                " originalOverlappingDestinations="+burst.OverlapTargets+" expectedArrivalQuota="+(burst.OverlapTargets?"none":"all"));
        }
        private unsafe bool BurstProgress(bool fullBoundary)
        {
            RequireBurstWorld();Require(burst.Fault==null,"Burst tick proof: "+burst.Fault);bool all=true;int arrived=0;
            foreach(var goal in burst.Goals)
            {
                var actor=goal.Actor;var u=Resolve(actor);
                Require(UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth==actor.Health && u->r_TribeId==actor.Tribe &&
                    runtime.Native.TryTribeGlobal(actor.Tribe,out uint stamp) && runtime.Native.JobDword(u,0x93C)==stamp,"Burst lost original identity/health/tribe binding: "+actor.Id);
                goal.Arrived=u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 &&
                    Math.Abs(u->r_CurrentTilePositionX-goal.X)<=1 && Math.Abs(u->r_CurrentTilePositionY-goal.Y)<=1;
                if(goal.Arrived)arrived++;all&=goal.Arrived;
                if(fullBoundary)
                {
                    CheckActor(actor,true);
                    bool ordinary=runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId);
                    bool legalExit=burst.PassiveControl && !ordinary && BurstCurrentPhysicallyFree(actor);
                    if(expectedEnabled || burst.GroundControl)Require(ordinary || legalExit,"Burst actor remains on invalid current ground without a legal physical exit");
                    log("burst-endpoint unit="+actor.Id+"/"+actor.Global+" current="+actor.X+","+actor.Y+" originalGoal="+goal.X+","+goal.Y+
                        " arrived="+goal.Arrived+" flags="+u->r_PathPlanStateBitFlags+" substep="+u->r_MovementSubstep+" originalHealth="+actor.Health+
                        " ordinary="+ordinary+" legalExit="+legalExit);
                }
            }
            foreach(var group in burst.Groups)
                Require(GameTribeManagerAPI.Instance.GetLeaderUnitId(group.Tribe)==group.Leader && runtime.Native.TryTribeGlobal(group.Tribe,out uint global) && global==group.Global,"Burst changed original tribe leader/lifetime");
            if(fullBoundary)log("burst-result population="+burst.Population+" enabled="+expectedEnabled+" arrived="+arrived+
                " pending="+Plugin.GeneralObserver.PendingCount+" attempts="+(Plugin.GeneralObserver.AttemptCount-burst.Attempts)+
                " rescues="+(Plugin.GeneralObserver.RescueCount-burst.Rescues)+" ticks="+burst.ObservedTicks+" maximumActionsPerTick="+burst.MaximumActions+
                " passiveControl="+burst.PassiveControl+" originallyActive="+burst.OriginallyActive.Count+" passiveObserved="+burst.PassiveObserved.Count+" passiveEscaped="+burst.PassiveEscaped.Count+" ownedMoves="+burst.OwnedMoves+" groundControl="+burst.GroundControl+" targetLayout="+(burst.OverlapTargets?"OVERLAP":"SPREAD")+" originalManifestComplete=true identityHealthTribeOccupancy=true ownRescuePerActorNotAssumed=true");
            return all;
        }
        private unsafe bool BurstCurrentPhysicallyFree(Actor actor)
        {
            var u=Resolve(actor);int tile=runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY);
            return (uint)tile<NativeRelocation.TileCapacity && tile==(int)u->r_CurrentPositionTileId &&
                (runtime.Native.OrdinaryGround(tile) || burst.PassiveControl && HasStructuralExit(actor));
        }
        private bool BurstPhysicallyFree()
        {
            foreach(var goal in burst.Goals)if(!BurstCurrentPhysicallyFree(goal.Actor))return false;
            return true;
        }
        private unsafe void LogBurstBacklog(string boundary)
        {
            RequireBurstWorld();var observer=Plugin.GeneralObserver;
            // Pair these in the same monitor scope, just like the production save
            // service. A failed pending export must not masquerade as an empty bank.
            bool pendingOk=observer.TryCapturePending(out var pending);
            RescuePendingGeometry[] geometry=null;bool geometryOk=pendingOk && observer.TryCaptureGeometry(out geometry);
            log("burst-backlog boundary="+boundary+" pendingCount="+observer.PendingCount+" pendingExport="+pendingOk+
                " geometryExport="+geometryOk+" queued="+(pendingOk?pending.Length:-1)+" geometry="+(geometryOk?geometry.Length:-1)+
                " unsettled="+observer.HasUnsettledReplans+" recovering="+observer.IsRecovering+" active="+observer.IsActive+
                " captures="+(observer.CaptureCount-burst.Captures)+" attempts="+(observer.AttemptCount-burst.Attempts)+
                " rescues="+(observer.RescueCount-burst.Rescues)+" tick="+burst.LastTick+" observedTicks="+burst.ObservedTicks);
            int shown=0;
            int pendingLimit=geometryOk && geometry.Length>0?2:4;
            if(pendingOk)foreach(var r in pending)
            {
                if(shown>=pendingLimit)break;var goal=BurstOwnedGoal(r.Snapshot.Id);if(goal==null)continue;
                LogBurstPendingActor(boundary,"pending",goal,r.Age,r.ActiveAge,r.Attempts,r.RetryIn,r.AwaitingResources,r.Source);shown++;
            }
            if(geometryOk)foreach(var r in geometry)
            {
                if(shown>=4)break;var goal=BurstOwnedGoal(r.Witness.UnitId);if(goal==null)continue;
                LogBurstPendingActor(boundary,"geometry",goal,r.Age,r.ActiveAge,r.Attempts,r.RetryIn,r.AwaitingResources,r.Source);shown++;
            }
            if(shown==0 && (!pendingOk || !geometryOk))
                for(int i=0;i<Math.Min(4,burst.Goals.Count);i++)LogBurstPendingActor(boundary,"export-unavailable",burst.Goals[i],-1,-1,-1,-1,false,"unknown");
        }
        private GroupMemberGoal BurstOwnedGoal(int id)
        {foreach(var goal in burst.Goals)if(goal.Actor.Id==id)return goal;return null;}
        private unsafe void LogBurstPendingActor(string boundary,string bank,GroupMemberGoal goal,int age,int activeAge,int attempts,int retry,bool waiting,string source)
        {
            var a=goal.Actor;GameUnit* u=null;bool found=UnitAccess.TryGetById(a.Id,out u,out _) && u!=null;
            bool same=found && u->r_GlobalId==a.Global && u->r_UnitChimp==a.Type && u->r_ControllableForPlayerId==owner;
            bool coherent=same && runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY)==(int)u->r_CurrentPositionTileId &&
                (uint)u->r_CurrentPositionTileId<NativeRelocation.TileCapacity;
            string live=found?" actualGlobal="+u->r_GlobalId+" type="+u->r_UnitChimp+" owner="+u->r_ControllableForPlayerId+
                " life="+u->r_AliveState+" hp="+u->r_CurrentHealth+" ai="+u->r_AIState+
                " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" currentTile="+u->r_CurrentPositionTileId+
                " next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+" nextTile="+u->r_NextPositionTileId2+
                " flags="+u->r_PathPlanStateBitFlags+" ss="+u->r_MovementSubstep+
                " requested="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2:"";
            log("burst-backlog-actor boundary="+boundary+" bank="+bank+" unit="+a.Id+"/"+a.Global+" source="+source+
                " sourceAge="+age+" activeAge="+activeAge+" attempts="+attempts+" retryIn="+retry+" awaitingResources="+waiting+
                " orderSerial="+Plugin.GeneralObserver.GetOrderSerial(a.Id)+" originalGoal="+goal.X+","+goal.Y+
                " readable="+found+" sameLifetime="+same+" coherentCurrent="+coherent+
                " ordinary="+(coherent?runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId).ToString():"unavailable")+
                " structuralExit="+(coherent?HasStructuralExit(a).ToString():"unavailable")+live);
        }
        private IEnumerable<AutomationWait> BurstCase(AutomationRun run)
        {
            bool multiple=scenario.Contains("-multi600-"),ground=scenario.Contains("-ground513-"),passive=scenario=="rescue-burst-passive600-on";int population=scenario.Contains("-single513-") || ground?513:600;
            Require(scenario.Contains("-single513-") || scenario.Contains("-single600-") || multiple || ground || passive,"Unknown burst case");
            burst=new BurstProof{Session=session,Population=population,GroundControl=ground,OverlapTargets=scenario=="rescue-burst-ground513-off" || passive,PassiveControl=passive};
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out burst.World) && sid==session,"Burst native world unavailable");
                    RequireBurstWorld();Plugin.SetGeneralFixtureSession(false);ArmBurst();FindBurstSites(multiple || passive?3:1);SpawnBurst();return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{CommandBurst();if(ground)MarkBurstGroundControl();else WriteBurst();return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                bool all=false;
                for(int sample=0;sample<(expectedEnabled || ground?320:8);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,32);
                    int boundary=sample+1;
                    all=run.InSimulation(run.CurrentMission,()=>{
                        if(boundary==1 || boundary==2 || boundary==4 || boundary==8)LogBurstBacklog("sample"+boundary);
                        CheckBurstPassiveExhausted();
                        bool arrived=BurstProgress(false);return arrived || passive && BurstPhysicallyFree();
                    });
                    if(all && (!expectedEnabled || Plugin.GeneralObserver.PendingCount==0))break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    LogBurstBacklog("final-before-assert");
                    bool reached=BurstProgress(true);Require(burst.ObservedTicks>0,"Burst observed no actual recovery ticks");
                    if(expectedEnabled && !passive || ground && !burst.OverlapTargets)Require(reached && Plugin.GeneralObserver.PendingCount==0,"Burst did not deliver every original actor to its native original goal and drain all work");
                    if(passive)Require(burst.HoldReleased && burst.PassiveObserved.Count>0 && burst.PassiveEscaped.Count>0 && BurstPhysicallyFree() && Plugin.GeneralObserver.PendingCount==0,
                        "Passive burst lacks real passive source, complete physical freedom, or drained work");
                    if(!expectedEnabled)Require(Plugin.GeneralObserver.AttemptCount==burst.Attempts && Plugin.GeneralObserver.RescueCount==burst.Rescues,"OFF burst performed rescue work");
                    Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally
            {
                // Iterator disposal can follow world retirement. Only local state.
                if(burst!=null)burst.Active=false;if(ReferenceEquals(burstFixture,this))burstFixture=null;
            }
        }
    }
}
