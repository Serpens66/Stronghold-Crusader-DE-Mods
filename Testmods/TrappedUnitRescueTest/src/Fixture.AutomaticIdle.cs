using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> AutomaticIdleSteps()
        {
            bool archer=scenario.Contains("-idlearcher-");
            yield return new AutomationStep(archer?"automatic-natural-idle-archer-moat":"automatic-natural-idle-spearman-moat",TimeSpan.FromSeconds(180),
                run=>AutomaticIdle(run,archer?eChimps.CHIMP_TYPE_ARCHER:eChimps.CHIMP_TYPE_SPEARMAN),true);
        }
        private sealed class AutomaticIdleProof
        {
            internal Fixture Fixture;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active,SourceNormalized,NativeCompleted,RefusedAtSource;
            internal int Calls,GoalX,GoalY,TargetTile;
            internal long NativeResult=long.MinValue;
            internal string Fault;
        }
        private static volatile AutomaticIdleProof automaticIdleProof;
        private static bool automaticIdleObserverRegistered;
        private sealed class AutomaticIdleObserver : IUnitMoveObserver
        {
            public IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var proof=automaticIdleProof;
                if(proof==null || !proof.Active || proof.Fixture.stopped || context==null ||
                    context.SessionId!=proof.Fixture.session || context.UnitId!=proof.Actor.Id || context.OriginOwnerGuid!=Plugin.RescueOwnerGuid)return null;
                proof.Calls++;
                if(proof.Calls!=1){proof.Fault="Idle escape made more than one owned move call";return null;}
                proof.GoalX=context.X;proof.GoalY=context.Y;
                try{proof.Fixture.CaptureAutomaticIdleOriginal(proof,context);}
                catch(Exception error){proof.Fault=error.ToString();}
                return new Completion(proof,context);
            }
            private sealed class Completion : IUnitMoveInvocation
            {
                private readonly AutomaticIdleProof proof;
                private readonly UnitMoveContext context;
                private int completed;
                internal Completion(AutomaticIdleProof proof,UnitMoveContext context){this.proof=proof;this.context=context;}
                public void Complete(UnitMoveCompletion result)
                {
                    if(Interlocked.Exchange(ref completed,1)!=0 || result==null || !ReferenceEquals(result.Context,context))return;
                    if(!result.IsCurrentWorld || !result.NativeInvoked || !result.NativeReturned || result.Failure!=null)
                    {proof.Fault="Idle move lacks an actual completed current native original";return;}
                    proof.NativeCompleted=true;proof.NativeResult=result.NativeResult;
                    if(result.NativeResult==0)
                    {
                        try{proof.RefusedAtSource=proof.Fixture.AtSource(proof.Actor);
                            Require(proof.RefusedAtSource,"Idle fallback moved before the actual native refusal receipt");}
                        catch(Exception error){proof.Fault=error.ToString();}
                    }
                    else if(result.NativeResult!=1)proof.Fault="Idle move returned neither native0 nor native1";
                }
            }
        }
        private void ArmAutomaticIdle(AutomaticIdleProof proof)
        {
            if(!automaticIdleObserverRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-automatic-idle",new AutomaticIdleObserver(),out string reason,
                    order:200,enabled:()=>automaticIdleProof!=null && automaticIdleProof.Active && !automaticIdleProof.Fixture.stopped),
                    "Idle assertion observer unavailable: "+reason);
                automaticIdleObserverRegistered=true;
            }
            automaticIdleProof=proof;
        }
        private unsafe void CaptureAutomaticIdleOriginal(AutomaticIdleProof proof,UnitMoveContext context)
        {
            CheckActor(proof.Actor,true);var unit=Resolve(proof.Actor);var b=proof.Before;
            proof.SourceNormalized=unit->r_CurrentTilePositionX==b.X && unit->r_CurrentTilePositionY==b.Y &&
                unit->r_NextTilePositionX2==b.X && unit->r_NextTilePositionY2==b.Y && unit->r_PathPlanStateBitFlags==0 &&
                unit->r_MovementSubstep==8 && runtime.Native.Word(unit,0x740)==0 && runtime.Native.Word(unit,0x742)==0 &&
                unit->r_TargetTilePositionX2==b.X && unit->r_TargetTilePositionY2==b.Y && unit->r_AIState==b.AIState;
            Require(proof.SourceNormalized,"Idle move did not start at its original unchanged normalized idle source");
            Require(context.Extra==0,"Idle escape changed native movement extra");
            bool oldReachable=false;
            foreach(var candidate in b.Candidates)
                if(candidate.X==context.X && candidate.Y==context.Y)
                {proof.TargetTile=candidate.Tile;oldReachable=true;break;}
            Require(oldReachable && proof.TargetTile!=b.TileId && runtime.Native.Tile(context.X,context.Y)==proof.TargetTile &&
                runtime.Native.Head(proof.TargetTile)==0 && runtime.Native.OrdinaryGround(proof.TargetTile) &&
                AutomaticIdleBankHasExit(context.X,context.Y),"Idle target was not an original reachable free ordinary bank with a physical exit");
        }
        private bool AutomaticIdleBankHasExit(int x,int y)
        {
            var native=runtime.Native;int tile=native.Tile(x,y);if(tile<0 || !native.OrdinaryGround(tile))return false;
            int links=native.Connections(tile);
            for(int d=0;d<8;d++)
            {
                int next=native.Tile(x+Dx[d],y+Dy[d]);
                if(next>=0 && native.OrdinaryGround(next) && ((links&(1<<d))!=0 || (native.Connections(next)&(1<<(d^4)))!=0))return true;
            }
            return false;
        }
        private IEnumerable<AutomationWait> AutomaticIdle(AutomationRun run,eChimps type)
        {
            run.InSimulation(run.CurrentMission,()=>{
                Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(type,cx,cy);return true;
            });
            UnitSnapshot before=null;
            for(int sample=0;sample<64 && before==null;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                int observation=sample;
                before=run.InSimulation(run.CurrentMission,()=>{
                    var value=CaptureAutomaticIdleSource(actors[0]);
                    if(observation==0 || observation==63)LogAutomaticIdleAcquisition(actors[0],observation,value!=null);
                    return value;
                });
            }
            Require(before!=null,"Natural idle source never qualified; no AI/order fields were fabricated for "+type);
            AutomaticIdleProof proof=null;long attempts=0,rescues=0,captures=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    VerifyLiveAndRemember();var actor=actors[0];
                    proof=new AutomaticIdleProof{Fixture=this,Actor=actor,Before=before,Active=true};ArmAutomaticIdle(proof);
                    Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
                    Require(observer!=null && observer.IsActive==expectedEnabled,"Idle general source activation mismatch");
                    attempts=observer.AttemptCount;rescues=observer.RescueCount;captures=observer.CaptureCount;
                    EditorBrush.Call(SHCDESE.API.GameTileManagerAPI.Instance.GetTileManager(),owner,before.TileId,before.Y,1,
                        unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
                    Require((runtime.Native.Flags(before.TileId)&(uint)TilePropertyFlag.IsMoat)!=0,"Actual idle editor construction did not create completed moat");
                    CheckActor(actor,true);Require(AtSource(actor) && proof.Calls==0 && observer.AttemptCount==attempts && observer.RescueCount==rescues,
                        "Idle rescue ran inside the original construction wrapper");
                    if(expectedEnabled)Require(observer.CaptureCount>captures && observer.PendingCount>0,"Actual idle writer did not publish a deferred causal candidate");
                    else Require(observer.CaptureCount==captures && observer.PendingCount==0,"Disabled idle source captured construction");
                    log("automatic-idle-before type="+type+" ai="+before.AIState+" original="+before.X+","+before.Y+
                        " oldCandidates="+before.Candidates.Count+" enabled="+expectedEnabled+" actualEditorMoat=true deferred=true");return true;
                });
                bool arrived=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?160:12);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    arrived=run.InSimulation(run.CurrentMission,()=>{
                        CheckActor(proof.Actor,true);
                        if(expectedEnabled)Require(proof.Fault==null,"Idle receipt observation failed: "+proof.Fault);
                        return proof.NativeCompleted && AutomaticIdleArrived(proof);
                    });
                    if(expectedEnabled && arrived)break;
                }
                // Keep the assertion receipt armed across a following real tick;
                // native1's route observation must not issue another escape move.
                if(expectedEnabled && arrived)yield return run.WaitForTicks(run.CurrentMission,4);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    var observer=Plugin.GeneralObserver;CheckActor(proof.Actor,true);
                    if(expectedEnabled)
                    {
                        Require(proof.Fault==null && proof.Calls==1 && proof.SourceNormalized && proof.NativeCompleted && arrived,
                            "Idle escape lacks one real own original call and completed physical bank arrival");
                        Require(proof.NativeResult==1 || proof.NativeResult==0 && proof.RefusedAtSource,
                            "Local idle fallback lacks actual preceding native0 at its original source");
                        // General counts synchronous relocations. A successful
                        // native route is EscapeInProgress and is completed by the
                        // separate idle owner; physical arrival above is its oracle.
                        Require(observer.AttemptCount==attempts+1 && observer.RescueCount==rescues+(proof.NativeResult==0?1:0),
                            "Idle coordinator attempt/synchronous-relocation counts differ from its actual result path");
                    }
                    else Require(proof.Calls==0 && observer.AttemptCount==attempts && observer.RescueCount==rescues,
                        "Disabled idle scenario issued an own escape or rescue");
                    log("automatic-idle-result type="+type+" enabled="+expectedEnabled+" ownCalls="+proof.Calls+
                        " actualNativeResult="+proof.NativeResult+" physicalBankArrival="+arrived+" native0AtSource="+proof.RefusedAtSource+
                        " bank="+proof.GoalX+","+proof.GoalY+" baselineAtSource="+AtSource(proof.Actor)+
                        " baselineStructuralExit="+HasStructuralExit(proof.Actor)+" bothReturnPathsCovered=false");
                    proof.Active=false;Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally{if(proof!=null)proof.Active=false;if(ReferenceEquals(automaticIdleProof,proof))automaticIdleProof=null;}
        }
        private unsafe void LogAutomaticIdleAcquisition(Actor actor,int sample,bool qualified)
        {
            var u=Resolve(actor);
            log("automatic-idle-acquire type="+actor.Type+" unit="+actor.Id+"/"+actor.Global+" sample="+sample+" qualified="+qualified+
                " life="+u->r_AliveState+" ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+" substep="+u->r_MovementSubstep+
                " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                " requested="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+" midpoint="+runtime.Native.JobDword(u,0x740)+
                " control="+runtime.Native.Word(u,0xA9E)+" target9F6="+runtime.Native.Word(u,0x9F6)+" targetA00="+runtime.Native.JobDword(u,0xA00)+
                " command9F4="+runtime.Native.Word(u,0x9F4)+
                " ranged99C="+runtime.Native.Word(u,0x99C)+" rangedGlobal6F8="+runtime.Native.JobDword(u,0x6F8)+
                " unsupported="+(runtime.SnapshotUnit(actor.Id)?.Unsupported??"none"));
        }
        private unsafe UnitSnapshot CaptureAutomaticIdleSource(Actor actor)
        {
            var unit=Resolve(actor);if(!UnitAccess.IsReallyAlive(unit))return null;
            var b=runtime.SnapshotUnit(actor.Id);
            return b!=null && b.Unsupported==null && b.Job==null && b.Command!=null && b.Flags==0 && b.Substep==8 &&
                b.X==b.NextX && b.Y==b.NextY && b.RequestedX==b.X && b.RequestedY==b.Y &&
                CommandJourneySnapshot.IsIndependentIdlePhase(actor.Type,b.AIState) && b.Candidates.Count>0 &&
                MoatWorkerSourceEligible(b.TileId) && runtime.Native.Word(unit,0x740)==0 && runtime.Native.Word(unit,0x742)==0?b:null;
        }
        private unsafe bool AutomaticIdleArrived(AutomaticIdleProof proof)
        {
            var unit=Resolve(proof.Actor);
            return unit->r_CurrentTilePositionX==proof.GoalX && unit->r_CurrentTilePositionY==proof.GoalY &&
                unit->r_CurrentPositionTileId==(uint)proof.TargetTile &&
                NativeRelocation.IsNormalizedPose(unit,proof.TargetTile,runtime.Native.JobDword(unit,0x740)) &&
                unit->r_NextTilePositionX2==proof.GoalX && unit->r_NextTilePositionY2==proof.GoalY &&
                unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8 && HasStructuralExit(proof.Actor) &&
                AutomaticIdleBankHasExit(proof.GoalX,proof.GoalY);
        }
    }
}
