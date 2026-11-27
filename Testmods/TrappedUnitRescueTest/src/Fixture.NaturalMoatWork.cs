using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // Genuine Spearman143400 -> DigMoat639C0, canonical FBCB9319.
        // The existing SE brush's variant0 plans work (59210); it neither
        // completes the moat nor supplies a fabricated worker/progress state.
        // This controlled planning path is not a human UI click acceptance.
        // Bank is the normal work lifecycle control. Task-standing separately
        // measures the completed caller boundary; neither claims Fill/passer
        // coverage or replacement of a completed native work instruction.
        private sealed class NaturalMoatProof
        {
            internal Actor Actor;
            internal int X,Y,Tile,Slot,Calls,Completed,TerminalTick,OwnMoves;
            internal long Session,World,Attempts,Rescues,Batch;
            internal uint TribeGlobal;
            internal bool Active,TaskStanding,Terminal,TailObserved,FirstCallLogged;
            internal bool FollowMoveArmed;
            internal int FollowX,FollowY,FollowCalls,FollowNative0,FollowNative1,FollowNoOriginal;
            internal string Fault,Results="";
        }
        private NaturalMoatProof naturalMoat;
        private static volatile Fixture naturalMoatFixture;
        private static bool naturalMoatObserversRegistered;

        private IEnumerable<AutomationStep> NaturalMoatWorkSteps()
        {
            yield return new AutomationStep("natural-moat-command6-four-steps-and-caller-tail",
                TimeSpan.FromSeconds(420),NaturalMoatWork,true);
        }
        private static bool NaturalMoatObserverEnabled()
        {
            var f=naturalMoatFixture;
            return f!=null && !f.stopped && f.naturalMoat!=null && f.naturalMoat.Active;
        }
        private void ArmNaturalMoat(NaturalMoatProof p)
        {
            Require((WorldMutationEvents.AvailableSources&WorldMutationSource.MoatDig)!=0,
                "Actual native MoatDig publisher is unavailable");
            if(!naturalMoatObserversRegistered)
            {
                var observer=new NaturalMoatObserver();
                Require(WorldMutationEvents.TryRegister(Plugin.Guid,"acceptance-natural-moat-work",observer,
                    out string reason,sources:WorldMutationSource.MoatDig,enabled:NaturalMoatObserverEnabled),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-natural-moat-moves",observer,
                    out reason,enabled:NaturalMoatObserverEnabled),reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-moat-tail",
                    ObserveNaturalMoatTail,out reason,order:-100),reason);
                naturalMoatObserversRegistered=true;
            }
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,
                "Natural moat fixture has no current identified world");
            naturalMoat=p;p.Active=true;naturalMoatFixture=this;
        }
        private sealed class NaturalMoatObserver : IWorldMutationObserver,IUnitMoveObserver
        {
            public IWorldMutationInvocation TryBegin(WorldMutationContext context)
            {
                var f=naturalMoatFixture;var p=f?.naturalMoat;
                if(f==null || f.stopped || p==null || !p.Active || context==null ||
                    context.Source!=WorldMutationSource.MoatDig || context.TaskSlot!=p.Slot)return null;
                try
                {
                    Require(context.SessionId==p.Session && context.WorldGeneration==p.World,
                        "Moat work crossed the original native world");
                    f.LogNaturalMoatFirstCall(p);
                    f.CaptureNaturalMoatBegin(p);p.Batch=context.BatchId;
                    return new NaturalMoatCompletion(f,p,context);
                }
                catch(Exception error){p.Fault=error.Message;return null;}
            }
            public void OnBatchCompleted(WorldMutationBatchCompletion value)
            {
                var f=naturalMoatFixture;var p=f?.naturalMoat;
                if(f==null || f.stopped || p==null || !p.Active || value==null ||
                    value.BoundarySource!=WorldMutationSource.MoatDig || value.BatchId!=p.Batch)return;
                if(value.SessionId!=p.Session || value.WorldGeneration!=p.World ||
                    !value.IsCurrentWorld || !value.ReturnedNormally || value.ContainsOriginalFailure)
                    p.Fault="Natural dig batch did not return in its original world";
            }
            IUnitMoveInvocation IUnitMoveObserver.TryBegin(UnitMoveContext context)
            {
                var f=naturalMoatFixture;var p=f?.naturalMoat;
                if(f==null || f.stopped || p==null || !p.Active || context==null || context.UnitId!=p.Actor.Id)return null;
                if(context.OriginOwnerGuid==Plugin.RescueOwnerGuid){p.OwnMoves++;return null;}
                if(!p.FollowMoveArmed)return null;
                try
                {
                    f.CheckNaturalFollowMove(p,context);p.FollowCalls++;
                    return new NaturalMoatMoveCompletion(f,p,context);
                }
                catch(Exception error){p.Fault=error.Message;return null;}
            }
        }
        private sealed class NaturalMoatMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture fixture;private readonly NaturalMoatProof proof;
            private readonly UnitMoveContext context;private bool completed;
            internal NaturalMoatMoveCompletion(Fixture f,NaturalMoatProof p,UnitMoveContext c)
            {fixture=f;proof=p;context=c;}
            public void Complete(UnitMoveCompletion value)
            {
                if(completed)return;completed=true;
                try
                {
                    Require(value!=null && ReferenceEquals(value.Context,context) && value.IsCurrentWorld && value.Failure==null,
                        "New player move completion lost its exact invocation/world");
                    fixture.CheckNaturalFollowMove(proof,context);
                    if(!value.NativeInvoked)proof.FollowNoOriginal++;
                    else
                    {
                        Require(value.NativeReturned,"New player move actual original did not return");
                        if(value.NativeResult==0)proof.FollowNative0++;
                        else if(value.NativeResult==1)proof.FollowNative1++;
                        else throw new InvalidOperationException("Unexpected actual player-move native result");
                    }
                    fixture.log("natural-moat-new-player-original unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                        " invocation="+context.InvocationId+" effectiveGoal="+context.X+","+context.Y+
                        " nativeInvoked="+value.NativeInvoked+" nativeReturned="+value.NativeReturned+
                        " actualNative="+(value.NativeInvoked && value.NativeReturned?value.NativeResult.ToString():"not-observed")+
                        " sameWorldAndLifetime=true workContinuationClaim=false");
                }
                catch(Exception error){proof.Fault=error.Message;}
            }
        }
        private unsafe void CheckNaturalFollowMove(NaturalMoatProof p,UnitMoveContext context)
        {
            Require(p.Active && p.FollowMoveArmed && context.SessionId==p.Session && context.WorldGeneration==p.World &&
                WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World &&
                context.X==p.FollowX && context.Y==p.FollowY && context.Extra==0,
                "New player move changed its original world/effective goal");
            var unit=CheckNaturalMoatActor(p);
            if(p.FollowCalls<8)log("natural-moat-new-player-pose current="+unit->r_CurrentTilePositionX+","+
                unit->r_CurrentTilePositionY+" next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                " flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                " ai="+unit->r_AIState+" command="+unit->r_AI_LastIssuedTribeCommand+
                " effectiveGoal="+context.X+","+context.Y);
        }
        private sealed class NaturalMoatCompletion : IWorldMutationInvocation
        {
            private readonly Fixture fixture;
            private readonly NaturalMoatProof proof;
            private readonly WorldMutationContext context;
            private bool completed;
            internal NaturalMoatCompletion(Fixture f,NaturalMoatProof p,WorldMutationContext c)
            {fixture=f;proof=p;context=c;}
            public void Complete(WorldMutationCompletion value)
            {
                if(completed)return;completed=true;
                try
                {
                    Require(value!=null && ReferenceEquals(value.Context,context) && value.OriginalInvoked &&
                        value.OriginalReturned && value.Failure==null,"Actual dig original did not complete normally");
                    fixture.CaptureNaturalMoatCompletion(proof,value.ReturnValue);
                }
                catch(Exception error){proof.Fault=error.Message;}
            }
        }
        private unsafe MoatWorkTask ReadNaturalMoatTask(NaturalMoatProof p)
        {
            var tiles=GameTileManagerAPI.Instance;
            Require(tiles.GetMoatWorkTaskIndexAtTile(p.X,p.Y)==p.Slot,
                "Original native moat task tile changed slots");
            Require(tiles.TryGetMoatWorkTaskByIndex(p.Slot,out var view) && view.Pointer!=null,
                "Original native moat task slot disappeared");
            // Copy only the public, typed task record inside this qualified call.
            // No view/native pointer survives into a later tick.
            var task=*view.Pointer;
            Require(task.r_TileId==p.Tile && task.r_TileX==p.X && task.r_TileY==p.Y &&
                task.r_OwnerPlayerId==owner && task.r_TargetMode==MoatTargetMode.AdjacentReachableTile,
                "Original native moat task identity/owner/target mode changed");
            return task;
        }
        private unsafe GameUnit* CheckNaturalMoatActor(NaturalMoatProof p)
        {
            var unit=Resolve(p.Actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==p.Actor.Health &&
                unit->r_TribeId==p.Actor.Tribe && runtime.Native.TryTribeGlobal(p.Actor.Tribe,out uint global) &&
                global==p.TribeGlobal && runtime.Native.JobDword(unit,0x93C)==global,
                "Original digger health or tribe lifetime changed");
            int tile=runtime.Native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY);
            Require(tile>=0 && unit->r_CurrentPositionTileId==(uint)tile &&
                runtime.Native.HasOccupancyExactlyOnce(tile,p.Actor.Id),"Digger occupancy is incoherent");
            return unit;
        }
        private unsafe void CaptureNaturalMoatBegin(NaturalMoatProof p)
        {
            Require(p.Fault==null && p.Calls==p.Completed && p.Calls<4 && !p.Terminal,
                "Unexpected duplicate or extra native dig work call");
            var task=ReadNaturalMoatTask(p);var unit=CheckNaturalMoatActor(p);
            var flags=GameTileManagerAPI.Instance.GetTilePropertyFlag(p.Tile);
            Require(task.r_WorkType==MoatWorkType.DigPlannedMoat && task.r_Progress==p.Completed &&
                (flags&TilePropertyFlag.PlannedMoat)!=0 && (flags&TilePropertyFlag.IsMoat)==0,
                "Dig work did not begin at its actual expected progress/planned geometry");
            Require(GameUnitManagerAPI.Instance.GetCurrentContextUnitId()==p.Actor.Id && unit->r_AIState==124 &&
                unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.DigMoatTileId &&
                unit->r_MoatWorkTaskIndex==(uint)p.Slot && runtime.Native.Word(unit,0xA0C)==p.X &&
                runtime.Native.Word(unit,0xA0E)==p.Y && runtime.Native.Word(unit,0x9C6)==1,
                "Moat writer was not called by the original naturally commanded capable Spearman");
            bool standing=unit->r_CurrentPositionTileId==(uint)p.Tile;
            Require(standing==p.TaskStanding,"Actual dig standingMismatch: expectedTaskStanding="+
                p.TaskStanding+" actualTaskStanding="+standing);
            int distance=Math.Max(Math.Abs(unit->r_CurrentTilePositionX-p.X),Math.Abs(unit->r_CurrentTilePositionY-p.Y));
            Require(distance<=1,"Actual dig bank/task distance exceeds one tile: actualDistance="+distance);
            p.Calls++;
        }
        private unsafe void LogNaturalMoatFirstCall(NaturalMoatProof p)
        {
            if(p.FirstCallLogged)return;p.FirstCallLogged=true;
            var unit=Resolve(p.Actor);var tiles=GameTileManagerAPI.Instance;
            string task="missing";
            if(tiles.TryGetMoatWorkTaskByIndex(p.Slot,out var view) && view.Pointer!=null)
            {
                var value=*view.Pointer;
                task="tile:"+value.r_TileId+" xy:"+value.r_TileX+","+value.r_TileY+
                    " owner:"+value.r_OwnerPlayerId+" progress:"+value.r_Progress+
                    " workType:"+value.r_WorkType+" targetMode:"+value.r_TargetMode;
            }
            int distance=Math.Max(Math.Abs(unit->r_CurrentTilePositionX-p.X),Math.Abs(unit->r_CurrentTilePositionY-p.Y));
            log("natural-moat-first-dig-pre unit="+p.Actor.Id+"/"+p.Actor.Global+
                " nativeContextUnit="+GameUnitManagerAPI.Instance.GetCurrentContextUnitId()+
                " current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                " currentTile="+unit->r_CurrentPositionTileId+
                " next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                " substep="+unit->r_MovementSubstep+" flags="+unit->r_PathPlanStateBitFlags+
                " requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+
                " routeEnd="+runtime.Native.Word(unit,0x720)+","+runtime.Native.Word(unit,0x722)+
                " routeStart="+runtime.Native.Word(unit,0x724)+","+runtime.Native.Word(unit,0x726)+
                " pathCursor="+runtime.Native.Word(unit,0x752)+" pathLength="+runtime.Native.Word(unit,0x754)+
                " ai="+unit->r_AIState+" command="+unit->r_AI_LastIssuedTribeCommand+
                " actorTask="+unit->r_MoatWorkTaskIndex+
                " actorWorkTarget="+runtime.Native.Word(unit,0xA0C)+","+runtime.Native.Word(unit,0xA0E)+
                " taskPre={"+task+"}"+
                " expectedTaskStanding="+p.TaskStanding+" actualTaskStanding="+(unit->r_CurrentPositionTileId==(uint)p.Tile)+
                " actualDistance="+distance+" beforePoseGuard=true");
        }
        private unsafe void CaptureNaturalMoatCompletion(NaturalMoatProof p,long result)
        {
            Require(p.Calls==p.Completed+1,"Natural work completion has no matching begin");
            int next=p.Completed+1;bool terminal=next==4;
            var task=ReadNaturalMoatTask(p);var unit=CheckNaturalMoatActor(p);
            var tiles=GameTileManagerAPI.Instance;var flags=tiles.GetTilePropertyFlag(p.Tile);
            Require(result==(terminal?1:0) && task.r_Progress==next &&
                task.r_WorkType==(terminal?MoatWorkType.FillDugMoat:MoatWorkType.DigPlannedMoat) &&
                ((flags&TilePropertyFlag.IsMoat)!=0)==terminal &&
                ((flags&TilePropertyFlag.PlannedMoat)!=0)==!terminal,
                "Actual dig result/progress/task mode/geometry differs from the audited four-step transition");
            Require(unit->r_AIState==124 && unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.DigMoatTileId,
                "Writer completion is not the original pre-caller-tail AI124 boundary");
            if(terminal)Require(tiles.GetTileHeight(p.Tile)==0,"Terminal native dig did not lower the actual task tile");
            p.Completed=next;p.Results+=(next==1?"":",")+result;
            if(terminal)
            {
                Require(SimulationTickEvents.TryGetCurrentTick(out p.TerminalTick),"Terminal dig has no native map tick");
                p.Terminal=true;
            }
            log("natural-moat-dig-original unit="+p.Actor.Id+"/"+p.Actor.Global+" slot="+p.Slot+
                " step="+next+" actualNative="+result+" progress="+task.r_Progress+" writerPostAI="+unit->r_AIState+
                " actualTaskStanding="+p.TaskStanding+" callerTailClaim=false");
        }
        private static void ObserveNaturalMoatTail(int tick)
        {
            var f=naturalMoatFixture;var p=f?.naturalMoat;
            if(f==null || f.stopped || p==null || !p.Active || !p.Terminal || p.TailObserved || p.Fault!=null)return;
            uint elapsed=unchecked((uint)(tick-p.TerminalTick));if(elapsed==0)return;
            try{Require(elapsed==1,"Missed the first native tick after terminal dig");f.CaptureNaturalMoatTail(p,tick);}
            catch(Exception error){p.Fault=error.Message;}
        }
        private unsafe void CaptureNaturalMoatTail(NaturalMoatProof p,int tick)
        {
            Require(WorldMutationEvents.TryGetCurrentWorld(out long currentSession,out long currentWorld) &&
                currentSession==p.Session && currentWorld==p.World,"Digger caller tail crossed its native world");
            var unit=CheckNaturalMoatActor(p);
            Require(unit->r_AIState==0 && runtime.Native.JobDword(unit,0x908)==0 &&
                unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.DigMoatTileId,
                "Full native dig caller tail did not publish its actual AI0/animation0/unchanged command6");
            p.TailObserved=true;
            log("natural-moat-dig-caller-tail unit="+p.Actor.Id+"/"+p.Actor.Global+" tick="+tick+
                " actualAI="+unit->r_AIState+" actualCommand="+unit->r_AI_LastIssuedTribeCommand+
                " actualPose="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                " onCompletedMoat="+(unit->r_CurrentPositionTileId==(uint)p.Tile)+
                " fullCallerObserved=true workIntentRestored=false");
        }
        private unsafe NaturalMoatProof PrepareNaturalMoat(bool standing)
        {
            FindSite();var tiles=GameTileManagerAPI.Instance;int tile=tiles.GetTileId(cx,cy);
            EditorBrush.Call(tiles.GetTileManager(),owner,tile,cy,1,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),0,0);
            var p=new NaturalMoatProof{X=cx,Y=cy,Tile=tile,TaskStanding=standing,
                Slot=tiles.GetMoatWorkTaskIndexAtTile(cx,cy)};
            Require(p.Slot>0,"Real planning brush did not allocate a native moat task");
            var task=ReadNaturalMoatTask(p);var flags=tiles.GetTilePropertyFlag(tile);
            Require(task.r_Progress==0 && task.r_WorkType==MoatWorkType.DigPlannedMoat &&
                (flags&TilePropertyFlag.PlannedMoat)!=0 && (flags&TilePropertyFlag.IsMoat)==0 &&
                tiles.GetTileHeight(tile)==elevation,"Planning unexpectedly completed/altered the moat geometry");
            // The lifecycle control starts on the genuine adjacent bank. Do not
            // infer physical adjacency from an arbitrary approach route: AI124
            // calls188340 (Flags0), which itself has no physical distance test.
            // The command also uses this bank as its initial move/search anchor:
            // 11E960 calls11B520 before publishing6. The later native task
            // approach is a distinct196280 route shortened by its own198620(1).
            SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,standing?cx:cx-1,cy);
            p.Actor=actors[actors.Count-1];return p;
        }
        private unsafe void BeginNaturalMoat(NaturalMoatProof p)
        {
            VerifyLiveAndRemember();Require(runtime.Native.TryTribeGlobal(p.Actor.Tribe,out p.TribeGlobal),
                "Original natural digger tribe lifetime unavailable");
            var unit=CheckNaturalMoatActor(p);
            Require(runtime.Native.Word(unit,0x9C6)==1 && unit->r_PathPlanStateBitFlags==0 &&
                unit->r_MovementSubstep==8 && (unit->r_CurrentPositionTileId==(uint)p.Tile)==p.TaskStanding,
                "Actual spawned digger does not qualify for the intended natural work placement");
            Require(unit->r_CurrentTilePositionX==(p.TaskStanding?p.X:p.X-1) && unit->r_CurrentTilePositionY==p.Y &&
                unit->r_NextTilePositionX2==unit->r_CurrentTilePositionX &&
                unit->r_NextTilePositionY2==unit->r_CurrentTilePositionY &&
                runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),
                "Natural digger did not retain its exact original ordinary bank/task start before command6");
            ArmNaturalMoat(p);Plugin.SetGeneralFixtureSession(expectedEnabled);
            p.Attempts=Plugin.GeneralObserver.AttemptCount;p.Rescues=Plugin.GeneralObserver.RescueCount;
            // Command6 first orders a normal group move, then the unit selects
            // a nearby task via69D60. A bank lifecycle control must not order the
            // initial group movement onto the task it intends to work beside.
            int commandX=p.TaskStanding?p.X:p.X-1;
            Require(GameTribeManagerAPI.Instance.DigMoat(p.Actor.Tribe,commandX,p.Y),
                "Public native tribe command6 was rejected");
            log("natural-moat-dig-begin enabled="+expectedEnabled+" unit="+p.Actor.Id+"/"+p.Actor.Global+
                " task="+p.Slot+" tile="+p.X+","+p.Y+" taskStanding="+p.TaskStanding+
                " commandMoveAndSearchAnchor="+commandX+","+p.Y+" taskSelection=await-native69D60"+
                " planner=SE-SetTileType-variant0 publicCommand=DigMoat artificialAIJobProgressWrites=0");
        }
        private unsafe void LogNaturalMoatSettled(NaturalMoatProof p)
        {
            var unit=CheckNaturalMoatActor(p);
            log("natural-moat-dig-settled unit="+p.Actor.Id+"/"+p.Actor.Global+
                " actualAI="+unit->r_AIState+" actualCommand="+unit->r_AI_LastIssuedTribeCommand+
                " actualFlags="+unit->r_PathPlanStateBitFlags+
                " actualPose="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                " actualOnTask="+(unit->r_CurrentPositionTileId==(uint)p.Tile)+" noForcedContinuation=true");
        }
        private IEnumerable<AutomationWait> NaturalMoatWork(AutomationRun run)
        {
            NaturalMoatProof p=null;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                Execute(run,()=>p=PrepareNaturalMoat(scenario.Contains("-dig-task-")));
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{BeginNaturalMoat(p);return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                bool completed=false;
                for(int sample=0;sample<1024 && !completed;sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    completed=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,"Natural moat observation failed: "+p.Fault);
                        if(sample%64==0)log("natural-moat-progress sample="+sample+" calls="+p.Calls+
                            " completed="+p.Completed+" terminal="+p.Terminal+" callerTail="+p.TailObserved);
                        return p.Completed==4 && p.TailObserved;
                    });
                }
                Require(completed,"Natural command6 did not execute all four actual work steps and caller tail");
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    LogNaturalMoatSettled(p);Require(p.Fault==null && p.Completed==4 && p.Results=="0,0,0,1",
                        "Native work lifecycle changed after its terminal caller boundary");
                    if(!p.TaskStanding || !expectedEnabled)Require(p.OwnMoves==0 &&
                        Plugin.GeneralObserver.AttemptCount==p.Attempts && Plugin.GeneralObserver.RescueCount==p.Rescues,
                        "Normal bank or disabled moat work spuriously triggered own recovery");
                    log("natural-moat-lifecycle-result enabled="+expectedEnabled+" taskStanding="+p.TaskStanding+
                        " actualResults="+p.Results+" fullCallerTail="+p.TailObserved+" ownRecoveryCalls="+p.OwnMoves+
                        " workRescueCovered=false fillCovered=false currentNextPasserCovered=false");
                    return true;
                });
                // A real NEW player command, never replay of finished digging.
                // Task-standing refusal is a qualified gap observation, not rescue
                // acceptance or evidence that an owned Native0 park was persisted.
                {
                    int x=p.X-8,y=p.Y+4;bool arrived=false,issued=false;
                    run.InSimulation(run.CurrentMission,()=>{
                        p.FollowX=x;p.FollowY=y;p.FollowMoveArmed=true;
                        issued=GameTribeManagerAPI.Instance.IssueMoveHereCommand(p.Actor.Tribe,x,y);
                        log("natural-moat-new-player-issued taskStanding="+p.TaskStanding+" groupAccepted="+issued+
                            " requestedGoal="+x+","+y+" oldWorkReissued=false");return true;
                    });
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    for(int sample=0;sample<80 && !arrived;sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,8);
                        arrived=run.InSimulation(run.CurrentMission,()=>MovementOutcome(p.Actor,x,y));
                    }
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        CheckActor(p.Actor,true);p.FollowMoveArmed=false;
                        Require(p.Fault==null,"New player move observation failed: "+p.Fault);
                        if(!p.TaskStanding)Require(issued && p.FollowNative1>0 && arrived && p.OwnMoves==0,
                            "Original bank digger did not physically execute its subsequent real player command");
                        else if(p.FollowNative1>0)Require(arrived,
                            "Task-standing actor published a real Native1 route but did not physically reach its new player goal");
                        else Require(!issued || p.FollowNative0>0 || p.FollowNoOriginal>0,
                            "Accepted task-standing group order has no qualified native result or arrival");
                        if(!expectedEnabled)Require(p.OwnMoves==0,"Disabled task-standing case executed own recovery");
                        log("natural-moat-new-player-result taskStanding="+p.TaskStanding+" groupAccepted="+issued+
                            " actualNative0="+p.FollowNative0+" actualNative1="+p.FollowNative1+
                            " managedNoOriginal="+p.FollowNoOriginal+" physicalGoal="+arrived+
                            " qualifiedMovementGap="+(!arrived && p.TaskStanding)+
                            " originalIdentityHealthTribeOccupancy=true ownRecoveryCalls="+p.OwnMoves+
                            " completedDigJobReplayed=false rescueAcceptanceClaim=false native0ParkSaveCovered=false");return true;
                    });
                }
                run.InSimulation(run.CurrentMission,()=>{p.Active=false;Plugin.SetGeneralFixtureSession(false);return true;});
            }
            finally
            {
                if(p!=null)p.Active=false;
                if(ReferenceEquals(naturalMoatFixture,this))naturalMoatFixture=null;
            }
        }
    }
}
