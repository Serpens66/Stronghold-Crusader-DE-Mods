using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
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
        // Canonical FBCB9319: public command7 -> 11E960/E7F60/11B520,
        // natural143400 AI125 -> 69470 -> 61E70 -> caller122800.
        // Scope: the local HUMAN bank filler, not successful AI retargeting,
        // not a trapped passer. Finished work is never replayed/restored.
        private sealed class NaturalFillProof
        {
            internal NaturalMoatProof Identity;
            internal int TaskOwner,ActorTeam,TaskTeam,ActiveBefore;
            internal bool FirstLogged;
        }
        private NaturalFillProof naturalFill;
        private static volatile Fixture naturalFillFixture;
        private static bool naturalFillObserversRegistered;
        private IEnumerable<AutomationStep> NaturalMoatFillSteps()
        {yield return new AutomationStep("natural-fill-command7-four-steps-task-retirement-and-caller-tail",
            TimeSpan.FromSeconds(420),NaturalMoatFill,true);}
        private static bool NaturalFillEnabled()
        {
            var f=naturalFillFixture;var p=f?.naturalFill?.Identity;
            return f!=null && !f.stopped && p!=null && p.Active;
        }
        private void ArmNaturalFill(NaturalFillProof proof)
        {
            Require((WorldMutationEvents.AvailableSources&WorldMutationSource.MoatFill)!=0,
                "Actual native MoatFill publisher unavailable");
            if(!naturalFillObserversRegistered)
            {
                var observer=new NaturalFillObserver();
                Require(WorldMutationEvents.TryRegister(Plugin.Guid,"acceptance-natural-fill",observer,out string reason,
                    sources:WorldMutationSource.MoatFill,enabled:NaturalFillEnabled),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-natural-fill-moves",observer,
                    out reason,enabled:NaturalFillEnabled),reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-fill-tail",
                    ObserveNaturalFillTail,out reason,order:-100),reason);
                naturalFillObserversRegistered=true;
            }
            var p=proof.Identity;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,
                "Natural filler has no original identified world");
            naturalFill=proof;p.Active=true;naturalFillFixture=this;
        }
        private sealed class NaturalFillObserver : IWorldMutationObserver,IUnitMoveObserver
        {
            public IWorldMutationInvocation TryBegin(WorldMutationContext context)
            {
                var f=naturalFillFixture;var proof=f?.naturalFill;var p=proof?.Identity;
                if(f==null || f.stopped || p==null || !p.Active || context==null ||
                    context.Source!=WorldMutationSource.MoatFill || context.TaskSlot!=p.Slot)return null;
                try
                {
                    Require(context.SessionId==p.Session && context.WorldGeneration==p.World,"Fill crossed native world");
                    f.CaptureNaturalFillBegin(proof);p.Batch=context.BatchId;
                    return new NaturalFillCompletion(f,proof,context);
                }
                catch(Exception error){p.Fault=error.Message;return null;}
            }
            public void OnBatchCompleted(WorldMutationBatchCompletion value)
            {
                var f=naturalFillFixture;var p=f?.naturalFill?.Identity;
                if(f==null || f.stopped || p==null || !p.Active || value==null || value.BatchId!=p.Batch)return;
                if(value.SessionId!=p.Session || value.WorldGeneration!=p.World || !value.IsCurrentWorld ||
                    !value.ReturnedNormally || value.ContainsOriginalFailure)p.Fault="Actual fill batch failed or crossed world";
            }
            IUnitMoveInvocation IUnitMoveObserver.TryBegin(UnitMoveContext context)
            {
                var f=naturalFillFixture;var p=f?.naturalFill?.Identity;
                if(f!=null && !f.stopped && p!=null && p.Active && context!=null &&
                    context.SessionId==p.Session && context.UnitId==p.Actor.Id && context.OriginOwnerGuid==Plugin.RescueOwnerGuid)p.OwnMoves++;
                return null;
            }
        }
        private sealed class NaturalFillCompletion : IWorldMutationInvocation
        {
            private readonly Fixture fixture;private readonly NaturalFillProof proof;
            private readonly WorldMutationContext context;private bool completed;
            internal NaturalFillCompletion(Fixture f,NaturalFillProof p,WorldMutationContext c)
            {fixture=f;proof=p;context=c;}
            public void Complete(WorldMutationCompletion value)
            {
                if(completed)return;completed=true;
                try
                {
                    Require(value!=null && ReferenceEquals(value.Context,context) && value.OriginalInvoked &&
                        value.OriginalReturned && value.Failure==null,"Actual fill original did not return normally");
                    fixture.CaptureNaturalFillCompletion(proof,value.ReturnValue);
                }
                catch(Exception error){proof.Identity.Fault=error.Message;}
            }
        }
        private void CheckNaturalFillTeams(NaturalFillProof proof)
        {
            var players=GamePlayerManagerAPI.Instance;
            Require(owner==deterministicHuman && owner==players.GetLocalPlayerId() && proof.TaskOwner==deterministicAi &&
                players.GetPlayerTeam(owner)==proof.ActorTeam && players.GetPlayerTeam(proof.TaskOwner)==proof.TaskTeam &&
                proof.ActorTeam!=proof.TaskTeam,"Original human filler/opponent task team contract changed");
        }
        private unsafe MoatWorkTask ReadNaturalFillTask(NaturalFillProof proof)
        {
            var p=proof.Identity;var tiles=GameTileManagerAPI.Instance;CheckNaturalFillTeams(proof);
            Require(tiles.GetMoatWorkTaskIndexAtTile(p.X,p.Y)==p.Slot,"Original fill task tile lost its slot");
            Require(tiles.TryGetMoatWorkTaskByIndex(p.Slot,out var view) && view.Pointer!=null,"Original fill task unavailable");
            var task=*view.Pointer;
            Require(task.r_TileId==p.Tile && task.r_TileX==p.X && task.r_TileY==p.Y &&
                task.r_OwnerPlayerId==proof.TaskOwner && task.r_TargetMode==MoatTargetMode.AdjacentReachableTile &&
                task.r_WorkType==MoatWorkType.FillDugMoat,"Original opposing fill task identity/mode changed");
            return task;
        }
        private unsafe void CaptureNaturalFillBegin(NaturalFillProof proof)
        {
            var p=proof.Identity;var task=ReadNaturalFillTask(proof);var unit=CheckNaturalMoatActor(p);
            int distance=Math.Max(Math.Abs(unit->r_CurrentTilePositionX-p.X),Math.Abs(unit->r_CurrentTilePositionY-p.Y));
            if(!proof.FirstLogged)
            {
                proof.FirstLogged=true;
                log("natural-fill-first-pre unit="+p.Actor.Id+"/"+p.Actor.Global+" current="+unit->r_CurrentTilePositionX+","+
                    unit->r_CurrentTilePositionY+" next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                    " substep="+unit->r_MovementSubstep+" flags="+unit->r_PathPlanStateBitFlags+
                    " requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+
                    " ai="+unit->r_AIState+" command="+unit->r_AI_LastIssuedTribeCommand+
                    " actorTask="+unit->r_MoatWorkTaskIndex+" task="+p.Slot+" taskOwner="+proof.TaskOwner+
                    " actorTeam="+proof.ActorTeam+" taskTeam="+proof.TaskTeam+" taskProgress="+task.r_Progress+
                    " actualDistance="+distance+" beforeWorkPoseGuard=true");
            }
            Require(p.Fault==null && p.Calls==p.Completed && p.Calls<4 && !p.Terminal && task.r_Progress==4-p.Completed,
                "Fill original has unexpected progress/duplicate invocation");
            var tiles=GameTileManagerAPI.Instance;
            Require((tiles.GetTilePropertyFlag(p.Tile)&TilePropertyFlag.IsMoat)!=0 && tiles.GetTileHeight(p.Tile)==0,
                "Nonterminal fill task lost its completed moat geometry");
            Require(GameUnitManagerAPI.Instance.GetCurrentContextUnitId()==p.Actor.Id && unit->r_AIState==125 &&
                unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.Unknown7 &&
                unit->r_MoatWorkTaskIndex==(uint)p.Slot && runtime.Native.Word(unit,0xA0C)==p.X &&
                runtime.Native.Word(unit,0xA0E)==p.Y,"Original filler did not naturally acquire this exact opposing task");
            Require(unit->r_CurrentPositionTileId!=(uint)p.Tile && distance==1 &&
                runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),"Actual filler is not on the ordinary adjacent bank");
            proof.ActiveBefore=tiles.GetMoatWorkTaskActiveCount();p.Calls++;
        }
        private unsafe void CaptureNaturalFillCompletion(NaturalFillProof proof,long result)
        {
            var p=proof.Identity;Require(p.Calls==p.Completed+1,"Fill completion missing its actual begin");
            int next=p.Completed+1;bool terminal=next==4;var unit=CheckNaturalMoatActor(p);
            var tiles=GameTileManagerAPI.Instance;CheckNaturalFillTeams(proof);
            Require(result==(terminal?1:0) && unit->r_AIState==125 &&
                unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.Unknown7,
                "Actual fill result/pre-caller AI125 boundary differs from contract");
            if(terminal)
            {
                // 61E70 retires this task inside69470. Never dereference the old
                // record or require the stale unitA10 reference to remain live.
                Require(tiles.GetMoatWorkTaskIndexAtTile(p.X,p.Y)==0 &&
                    tiles.GetMoatWorkTaskActiveCount()==proof.ActiveBefore-1 &&
                    (tiles.GetTilePropertyFlag(p.Tile)&(TilePropertyFlag.IsMoat|TilePropertyFlag.PlannedMoat))==0 &&
                    tiles.GetTileHeight(p.Tile)==8 && runtime.Native.OrdinaryGround(p.Tile),
                    "Terminal fill did not retire the task and restore actual ordinary height8 land");
                Require(SimulationTickEvents.TryGetCurrentTick(out p.TerminalTick),"Terminal fill has no native tick");
                p.Terminal=true;
            }
            else
            {
                var task=ReadNaturalFillTask(proof);
                Require(task.r_Progress==4-next && tiles.GetMoatWorkTaskActiveCount()==proof.ActiveBefore &&
                    (tiles.GetTilePropertyFlag(p.Tile)&TilePropertyFlag.IsMoat)!=0 && tiles.GetTileHeight(p.Tile)==0,
                    "Nonterminal fill changed task lifetime/geometry prematurely");
            }
            p.Completed=next;p.Results+=(next==1?"":",")+result;
            log("natural-fill-original step="+next+" actualNative="+result+" remainingProgress="+(4-next)+
                " taskRetired="+terminal+" writerPostAI="+unit->r_AIState+" callerTailClaim=false");
        }
        private static void ObserveNaturalFillTail(int tick)
        {
            var f=naturalFillFixture;var proof=f?.naturalFill;var p=proof?.Identity;
            if(f==null || f.stopped || p==null || !p.Active || !p.Terminal || p.TailObserved || p.Fault!=null)return;
            uint elapsed=unchecked((uint)(tick-p.TerminalTick));if(elapsed==0)return;
            try{Require(elapsed==1,"Missed first native tick after actual fill");f.CaptureNaturalFillTail(proof,tick);}
            catch(Exception error){p.Fault=error.Message;}
        }
        private unsafe void CaptureNaturalFillTail(NaturalFillProof proof,int tick)
        {
            var p=proof.Identity;CheckNaturalFillTeams(proof);
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World,
                "Fill caller tail crossed original world");
            var unit=CheckNaturalMoatActor(p);
            // This case has a verified local HUMAN actor.122800's AI-owner gate
            // refuses retargeting; AI-controlled successful101 is separate scope.
            Require(unit->r_AIState==0 && runtime.Native.JobDword(unit,0x908)==0 && runtime.Native.JobDword(unit,0xA00)==0 &&
                unit->r_AI_LastIssuedTribeCommand==(ushort)TribeAICommand.Unknown7,
                "Human fill caller tail did not complete its native AI0/animation0/target0 handoff");
            p.TailObserved=true;
            log("natural-fill-caller-tail tick="+tick+" actualAI=0 actualCommand="+unit->r_AI_LastIssuedTribeCommand+
                " oldTaskGrid=0 staleActorTask="+unit->r_MoatWorkTaskIndex+
                " originalIdentityHealthTribeOccupancy=true finishedWorkRestored=false aiRetargetCovered=false");
        }
        private unsafe NaturalFillProof PrepareNaturalFill()
        {
            Require(owner==deterministicHuman && deterministicAi>0 && deterministicAi!=owner,"Fill fixture needs its deterministic human/opponent");
            var players=GamePlayerManagerAPI.Instance;var tiles=GameTileManagerAPI.Instance;
            var keep=players.GetPlayerKeepPosition(deterministicAi);bool found=false;
            for(int radius=28;radius<=124 && !found;radius+=8)
                for(int dy=-radius;dy<=radius && !found;dy+=8)
                    for(int dx=-radius;dx<=radius && !found;dx+=8)
                    {
                        int x=keep.X+dx,y=keep.Y+dy;
                        if(!FlatSquare(x,y,12,out int height) || height!=8)continue;
                        cx=x;cy=y;elevation=height;found=true;
                    }
            Require(found,"No genuine empty height8 site near the opposing Keep for fill lifecycle");
            var p=new NaturalMoatProof{X=cx,Y=cy,Tile=tiles.GetTileId(cx,cy)};
            var proof=new NaturalFillProof{Identity=p,TaskOwner=deterministicAi,
                ActorTeam=players.GetPlayerTeam(owner),TaskTeam=players.GetPlayerTeam(deterministicAi)};
            CheckNaturalFillTeams(proof);
            EditorBrush.Call(tiles.GetTileManager(),proof.TaskOwner,p.Tile,p.Y,1,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            p.Slot=tiles.GetMoatWorkTaskIndexAtTile(p.X,p.Y);
            Require(p.Slot>0,"Actual opposing-owner brush did not allocate a completed moat task");
            var task=ReadNaturalFillTask(proof);
            Require(task.r_Progress==4 && (tiles.GetTilePropertyFlag(p.Tile)&TilePropertyFlag.IsMoat)!=0 &&
                (tiles.GetTilePropertyFlag(p.Tile)&TilePropertyFlag.PlannedMoat)==0 && tiles.GetTileHeight(p.Tile)==0,
                "Real fill setup is not an original completed opposing moat");
            SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,p.X-1,p.Y);p.Actor=actors[actors.Count-1];return proof;
        }
        private unsafe void BeginNaturalFill(NaturalFillProof proof)
        {
            var p=proof.Identity;VerifyLiveAndRemember();CheckNaturalFillTeams(proof);
            Require(runtime.Native.TryTribeGlobal(p.Actor.Tribe,out p.TribeGlobal),"Original filler tribe lifetime unavailable");
            var unit=CheckNaturalMoatActor(p);
            Require(unit->r_CurrentTilePositionX==p.X-1 && unit->r_CurrentTilePositionY==p.Y &&
                unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8 &&
                runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),"Filler did not retain actual original ordinary bank");
            ArmNaturalFill(proof);Plugin.SetGeneralFixtureSession(expectedEnabled);
            p.Attempts=Plugin.GeneralObserver.AttemptCount;p.Rescues=Plugin.GeneralObserver.RescueCount;
            Require(GameTribeManagerAPI.Instance.IssueTargettedCommand(p.Actor.Tribe,TribeAICommand.Unknown7,p.X,p.Y,1000),
                "Public native command7 was rejected");
            log("natural-fill-begin enabled="+expectedEnabled+" unit="+p.Actor.Id+"/"+p.Actor.Global+" task="+p.Slot+
                " taskXY="+p.X+","+p.Y+" actorOwner="+owner+" taskOwner="+proof.TaskOwner+
                " actorTeam="+proof.ActorTeam+" taskTeam="+proof.TaskTeam+
                " setup=SE-brush-variant2 command=public7 nativeAcquisitionAwaited=true rawAIJobProgressWrites=0");
        }
        private unsafe void CheckNaturalFillActor(NaturalMoatProof proof) { CheckNaturalMoatActor(proof); }
        private IEnumerable<AutomationWait> NaturalMoatFill(AutomationRun run)
        {
            NaturalFillProof proof=null;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                Execute(run,()=>proof=PrepareNaturalFill());
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{BeginNaturalFill(proof);return true;});
                var p=proof.Identity;bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<1024 && !complete;sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    complete=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,"Natural fill observation failed: "+p.Fault);
                        if(sample%64==0)log("natural-fill-progress sample="+sample+" calls="+p.Calls+
                            " completed="+p.Completed+" callerTail="+p.TailObserved);
                        return p.Completed==4 && p.TailObserved;
                    });
                }
                Require(complete,"Public fill command did not naturally acquire/complete the exact opposing task");
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                int targetX=p.X-8,targetY=p.Y+4;bool arrived=false;
                run.InSimulation(run.CurrentMission,()=>{
                    CheckNaturalFillActor(p);Require(p.Fault==null && p.Results=="0,0,0,1" && p.OwnMoves==0 &&
                        Plugin.GeneralObserver.AttemptCount==p.Attempts && Plugin.GeneralObserver.RescueCount==p.Rescues,
                        "Normal bank filling triggered own recovery or altered completed work evidence");
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(p.Actor.Tribe,targetX,targetY),
                        "Original human filler rejected its genuine subsequent player move");return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<80 && !arrived;sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,8);
                    arrived=run.InSimulation(run.CurrentMission,()=>MovementOutcome(p.Actor,targetX,targetY));
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    CheckActor(p.Actor,true);Require(arrived && p.Fault==null && p.OwnMoves==0,
                        "Original filler did not physically execute its subsequent real player command");
                    log("natural-fill-result enabled="+expectedEnabled+" actualResults="+p.Results+
                        " oldTaskRetired=true fullHumanCallerTail=true physicalNextPlayerGoal=true ownRecoveryCalls=0"+
                        " finishedWorkReplayed=false passerCovered=false aiRetargetCovered=false");
                    p.Active=false;Plugin.SetGeneralFixtureSession(false);return true;
                });
            }
            finally
            {
                if(proof!=null)proof.Identity.Active=false;
                if(ReferenceEquals(naturalFillFixture,this))naturalFillFixture=null;
            }
        }
    }
}
