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
        // Public destruction/allocation of the real selected object;
        // no worker, cargo, target, timer or vegetation-record writes by this file.
        private sealed class AppleLifetimeProof
        {
            internal AppleTreeTransit Transit;
            internal AutomaticWorkerMutation Mutation;
            internal long Session,World;
            internal int SourceTick,LastTick,Reads,LogLines;
            internal uint ReplacementGlobal;
            internal bool Active,Reuse,HasTick,ReplacementAliveGlobalChecked,SawDeletionMarker,OriginalTerminated,MoveRetired;
            internal bool OriginalCandidateCaptured,OriginalCandidateRetired;
            internal int ExactOriginalOwnPublications;
            internal uint AmbientGlobal;
            internal VegetationType AmbientType;
            internal int AmbientX,AmbientY,AmbientFirstTick,AmbientLives,AmbientAliveLives;
            internal bool AmbientPending;
            internal readonly HashSet<uint> AmbientSeenGlobals=new HashSet<uint>();
            internal string Fault,Outcome="not-observed";
            internal int PreviousAI=-1,PreviousFlags=-1,PreviousCounter=-1,PreviousX=-1,PreviousY=-1;
        }
        private AppleLifetimeProof appleLifetime;
        private static volatile Fixture appleLifetimeFixture;
        private static bool appleLifetimeRegistered;

        private IEnumerable<AutomationStep> AppleLifetimeSteps()
        {yield return new AutomationStep("selected-apple-lifetime-negative",TimeSpan.FromSeconds(1200),AppleLifetime,true);}

        private void ArmAppleLifetime(AppleLifetimeProof proof)
        {
            if(!appleLifetimeRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-apple-lifetime-negative",
                    ObserveAppleLifetime,out string reason,order:-1000),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-apple-lifetime-original-moves",
                    new AppleLifetimeMoveObserver(),out string moveReason,
                    enabled:()=>appleLifetimeFixture!=null && !appleLifetimeFixture.stopped &&
                        appleLifetimeFixture.appleLifetime!=null && appleLifetimeFixture.appleLifetime.Active),moveReason);
                appleLifetimeRegistered=true;
            }
            Require(WorldMutationEvents.TryGetCurrentWorld(out proof.Session,out proof.World) && proof.Session==session,
                "Apple lifetime negative has no exact native world");
            Require(SimulationTickEvents.TryGetCurrentTick(out proof.SourceTick),"Apple lifetime source has no native tick");
            proof.LastTick=proof.SourceTick;proof.HasTick=true;proof.Active=true;
            appleLifetime=proof;appleLifetimeFixture=this;
        }
        private sealed class AppleLifetimeMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=appleLifetimeFixture;var p=f?.appleLifetime;var actor=p?.Transit?.Step?.Actor;
                if(f==null || f.stopped || p==null || !p.Active || p.MoveRetired || actor==null || context==null ||
                    context.SessionId!=p.Session || context.WorldGeneration!=p.World || context.UnitId!=actor.Id)return null;
                if(!WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) || sid!=p.Session || world!=p.World)
                {p.MoveRetired=true;return null;}
                // Resolve BEFORE counting any call/fault. A legitimate new life
                // in the old numeric slot is outside this original proof even
                // if it commands a move before the next tick callback.
                if(!UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) || unit==null ||
                    unit->r_GlobalId!=actor.Global || unit->r_UnitChimp!=actor.Type ||
                    unit->r_ControllableForPlayerId!=f.owner || !UnitAccess.IsReallyAlive(unit))
                {
                    p.MoveRetired=true;
                    if(f.workerCycle!=null && ReferenceEquals(f.workerCycle.Actor,actor))f.workerCycle.Active=false;
                    return null;
                }
                if(context.OriginOwnerGuid==Plugin.RescueOwnerGuid)p.ExactOriginalOwnPublications++;
                return null;
            }
        }
        private bool HasOriginalAppleLifetimePending(AppleLifetimeProof p)
        {
            Require(Plugin.GeneralObserver.TryCapturePending(out var records),"Cannot read the real pending candidate boundary");
            int count=0;var t=p.Transit;var before=t.Step.Before;
            foreach(var record in records)
            {
                var snapshot=record.Snapshot;
                if(snapshot==null || snapshot.Id!=before.Id || snapshot.GlobalId!=before.GlobalId)continue;
                Require(snapshot.AIState==before.AIState && snapshot.RequestedX==before.RequestedX &&
                    snapshot.RequestedY==before.RequestedY && snapshot.Job!=null && snapshot.HasCapturedVegetation &&
                    snapshot.CapturedVegetationGlobal==t.TreeGlobal && snapshot.TileId==before.TileId &&
                    snapshot.NextTileId==before.NextTileId && snapshot.AffectedNext && !string.IsNullOrEmpty(record.Source),
                    "Original queued candidate does not carry its exact pre-mutation intent/tree/geometry");
                count++;
            }
            Require(count<=1,"Duplicate original-lifetime candidate in production queue");
            return count==1;
        }
        private static void ObserveAppleLifetime(int tick)
        {
            var f=appleLifetimeFixture;var p=f?.appleLifetime;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try{f.ObserveAppleLifetimeCore(p,tick);}
            catch(Exception error){p.Fault=error.ToString();}
        }
        private unsafe void ObserveAppleLifetimeCore(AppleLifetimeProof p,int tick)
        {
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World,
                "Apple lifetime diagnostic crossed its original world");
            Require(!p.HasTick || unchecked((uint)(tick-p.LastTick))<=1,"Apple lifetime diagnostic missed a native tick");
            p.LastTick=tick;p.HasTick=true;p.Reads++;
            var t=p.Transit;
            Require(NativeVegetationAccess.TryGet(t.TreeId,out var tree) && tree!=null,"Original tree slot cannot be read safely");
            if(p.Reuse)
            {
                Require(tree->r_GlobalId==p.ReplacementGlobal && tree->r_VegetationType==VegetationType.AppleTree &&
                    tree->r_TilePositionX==t.TreeX && tree->r_TilePositionY==t.TreeY,
                    "The genuinely reused selected slot changed its replacement lifetime");
                if(!p.ReplacementAliveGlobalChecked && tree->r_AliveState==AliveState.IsAlive)
                {
                    // A NeedsInit rejection alone cannot establish a global-ID
                    // guard. Read the ACTUAL alive replacement through the same
                    // production helper, with an independently successful control.
                    Require(NativeVegetationAccess.TryReadCapturedAliveGlobal(tree,true,p.ReplacementGlobal,out uint live) &&
                        live==p.ReplacementGlobal &&
                        !NativeVegetationAccess.TryReadCapturedAliveGlobal(tree,true,t.TreeGlobal,out _),
                        "Actual alive reused slot did not independently reject the captured old global");
                    p.ReplacementAliveGlobalChecked=true;
                    log("apple-lifetime-alive-reuse tick="+tick+" nativeIndex="+t.TreeId+" oldGlobal="+t.TreeGlobal+
                        " actualNewGlobal="+live+" actualLife=IsAlive newGlobalControl=true oldGlobalRejected=true");
                }
                Require(p.ReplacementAliveGlobalChecked || p.Reads<=32,"Replacement never genuinely initialized within its bounded window");
            }
            else ObserveDeletedAppleSlot(p,tree,tick);

            var actor=t.Step.Actor;
            bool original=UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==actor.Global;
            if(!original || !UnitAccess.IsReallyAlive(unit))
            {
                p.OriginalTerminated=true;
                p.MoveRetired=true;
                if(automaticWorkerMoves!=null && ReferenceEquals(automaticWorkerMoves.Actor,actor))automaticWorkerMoves.Active=false;
                if(original && unit->r_AliveState==AliveState.MarkedForDeletion)p.SawDeletionMarker=true;
                p.Outcome=original?"original-life-"+unit->r_AliveState:"original-slot-no-longer-present";
                // The original native unit may disappear; that is an observed
                // vanilla outcome, not permission to resolve a recycled actor.
                workerCycle.Active=false;
                return;
            }
            Require(!p.OriginalTerminated,"Original actor unexpectedly revived after an observed terminal lifetime transition");
            CheckActor(actor,true);
            int counter=runtime.Native.Word(unit,0x9EC);
            if(p.PreviousAI!=unit->r_AIState || p.PreviousFlags!=unit->r_PathPlanStateBitFlags ||
                p.PreviousCounter!=counter || p.PreviousX!=unit->r_CurrentTilePositionX || p.PreviousY!=unit->r_CurrentTilePositionY)
            {
                Require(p.LogLines++<96,"Apple lifetime transition log budget exhausted");
                p.PreviousAI=unit->r_AIState;p.PreviousFlags=unit->r_PathPlanStateBitFlags;p.PreviousCounter=counter;
                p.PreviousX=unit->r_CurrentTilePositionX;p.PreviousY=unit->r_CurrentTilePositionY;
                log("apple-lifetime-native-followup tick="+tick+" ai="+p.PreviousAI+" flags="+p.PreviousFlags+
                    " current="+p.PreviousX+","+p.PreviousY+" counter="+counter+" carry="+unit->r_CarryGoodAmount+
                    " selectedNativeIndex="+unit->r_TargetVegetationId+" originalGlobal="+t.TreeGlobal+
                    " replacementGlobal="+p.ReplacementGlobal);
            }
            p.Outcome="original-alive-native-ai-"+unit->r_AIState;
        }
        private unsafe void ObserveDeletedAppleSlot(AppleLifetimeProof p,GameVegetation* tree,int tick)
        {
            var original=p.Transit;
            uint global=tree->r_GlobalId;var life=tree->r_AliveState;
            Require(!NativeVegetationAccess.TryReadCapturedAliveGlobal(tree,true,original.TreeGlobal,out _),
                "Deleted original vegetation lifetime became readable as alive again");
            if(global==0 && (int)life==0)
            {
                Require(!p.AmbientPending,"Observed replacement disappeared before its independent alive-global control");
                p.AmbientGlobal=0;return;
            }
            var type=tree->r_VegetationType;
            int x=tree->r_TilePositionX,y=tree->r_TilePositionY;
            // Tile validates the fixed grid AND the active playable diamond.
            int tile=runtime.Native.Tile(x,y);
            bool nativeTileValid=tile>=0 && tile<NativeRelocation.TileCapacity;
            int binding=nativeTileValid?GameTileManagerAPI.Instance.GetTileVegetationId(tile):-1;
            bool boundNewLife=global!=0 && global!=original.TreeGlobal &&
                (life==AliveState.NeedsInit || life==AliveState.IsAlive) &&
                type!=VegetationType.None && Enum.IsDefined(typeof(VegetationType),type) && nativeTileValid && binding==original.TreeId;
            if(!boundNewLife)
                log("apple-lifetime-slot-invalid tick="+tick+" sourceTick="+p.SourceTick+" readOrdinal="+p.Reads+
                    " nativeIndex="+original.TreeId+" capturedGlobal="+original.TreeGlobal+" actualGlobal="+global+
                    " life="+(int)life+" type="+(int)type+" x="+x+" y="+y+" tile="+tile+" binding="+binding);
            Require(boundNewLife,"Nonempty deleted slot has no independently valid new identity/life/coordinate/tile binding");
            if(global!=p.AmbientGlobal)
            {
                Require(!p.AmbientPending,"Observed replacement changed identity before its independent alive-global control");
                Require(p.AmbientLives<16 && p.AmbientSeenGlobals.Add(global),
                    "Replacement lifetime transition budget exhausted or a completed global reappeared");
                p.AmbientGlobal=global;p.AmbientType=type;p.AmbientX=x;p.AmbientY=y;p.AmbientFirstTick=tick;p.AmbientLives++;p.AmbientPending=true;
                log("apple-lifetime-natural-slot-reuse tick="+tick+" sourceTick="+p.SourceTick+" readOrdinal="+p.Reads+
                    " nativeIndex="+original.TreeId+" capturedGlobal="+original.TreeGlobal+" actualNewGlobal="+global+
                    " actualLife="+life+" actualType="+type+" x="+x+" y="+y+" tile="+tile+" binding="+binding+
                    " capturedOriginalX="+original.TreeX+" capturedOriginalY="+original.TreeY+
                    " originalProofUnchanged=true aliveControlPending=true");
            }
            Require(type==p.AmbientType && x==p.AmbientX && y==p.AmbientY,
                "One observed replacement global changed its captured type or physical position");
            Require(p.AmbientPending || life==AliveState.IsAlive,
                "One observed initialized replacement regressed to NeedsInit without a new identity");
            if(life==AliveState.IsAlive && p.AmbientPending)
            {
                Require(NativeVegetationAccess.TryReadCapturedAliveGlobal(tree,true,global,out uint live) && live==global &&
                    !NativeVegetationAccess.TryReadCapturedAliveGlobal(tree,true,original.TreeGlobal,out _),
                    "Actual alive natural replacement did not independently accept new global and reject captured old global");
                p.AmbientPending=false;p.AmbientAliveLives++;
                log("apple-lifetime-natural-alive-control tick="+tick+" nativeIndex="+original.TreeId+
                    " capturedGlobal="+original.TreeGlobal+" actualNewGlobal="+live+" x="+x+" y="+y+
                    " actualLife=IsAlive newGlobalControl=true oldGlobalRejected=true originalProofUnchanged=true");
            }
            Require(!p.AmbientPending || unchecked((uint)(tick-p.AmbientFirstTick))<=32,
                "Natural replacement exceeded the fixture's bounded independent initialization window");
        }
        private unsafe void InvalidateAppleLifetime(AppleLifetimeProof p,int farm)
        {
            var t=p.Transit;var before=t.Step.Before;
            ArmAppleLifetime(p);
            // The normal causal writer captures the genuine old object first.
            // Do not arm OriginalTreeContact/AppleNativeTrace: deliberately
            // destroying that identity would correctly invalidate those proofs.
            p.Mutation=ApplyAppleTreeNextOnly(t,farm);
            // The shared fixture observer owns only an ID/session filter. This
            // deliberately destructive case needs the exact-life observer above,
            // including the same-tick interval before the next native tick.
            automaticWorkerMoves.Active=false;
            p.OriginalCandidateCaptured=HasOriginalAppleLifetimePending(p);
            Require(p.OriginalCandidateCaptured,"Actual original lifetime was not captured by the real source writer");
            Require(OriginalAppleTransitTreeAlive(t),"Selected original tree vanished before the controlled public deletion");
            GameVegetationManagerAPI.Instance.Delete(t.TreeId);
            Require(NativeVegetationAccess.TryGet(t.TreeId,out var removed) && removed!=null &&
                removed->r_GlobalId==0 && (int)removed->r_AliveState==0 &&
                GameTileManagerAPI.Instance.GetTileVegetationId(runtime.Native.Tile(t.TreeX,t.TreeY))==0 &&
                (runtime.Native.Flags(runtime.Native.Tile(t.TreeX,t.TreeY))&(uint)TilePropertyFlag.IsTree)==0,
                "Real public native deletion did not clear the selected slot and physical tree binding");
            if(p.Reuse)
            {
                long created=GameVegetationManagerAPI.Instance.CreateEx(checked((ushort)t.TreeX),checked((ushort)t.TreeY),
                    VegetationType.AppleTree,growthStage:3);
                Require(created==t.TreeId,"Public allocator did not actually reuse the original selected slot; reuse unproved");
                Require(NativeVegetationAccess.TryGet(t.TreeId,out var replacement) && replacement!=null &&
                    replacement->r_GlobalId!=0 && replacement->r_GlobalId!=t.TreeGlobal &&
                    replacement->r_AliveState==AliveState.NeedsInit && replacement->r_VegetationType==VegetationType.AppleTree &&
                    replacement->r_TilePositionX==t.TreeX && replacement->r_TilePositionY==t.TreeY &&
                    GameTileManagerAPI.Instance.GetTileVegetationId(runtime.Native.Tile(t.TreeX,t.TreeY))==t.TreeId,
                    "Actual same-slot allocation has no independent new global/lifetime/tile binding");
                p.ReplacementGlobal=replacement->r_GlobalId;
            }
            CheckActor(t.Step.Actor,true);var unit=Resolve(t.Step.Actor);IntPtr pointer=(IntPtr)unit;
            Require(before.Job.Matches(unit->r_UnitChimp,unit->r_AIState,o=>runtime.Native.JobWord((GameUnit*)pointer,o),
                o=>runtime.Native.JobDword((GameUnit*)pointer,o)) && unit->r_TargetTilePositionX2==before.RequestedX &&
                unit->r_TargetTilePositionY2==before.RequestedY && unit->r_CurrentPositionTileId==(uint)before.TileId &&
                unit->r_NextPositionTileId2==(uint)before.NextTileId && unit->r_MovementSubstep==before.Substep &&
                unit->r_CarryGoodAmount==0,"Public tree lifetime operations changed the original actor job/goal/pose/cargo");
            Require(!new NativeWorkerResumeWorld(runtime.Native).TryCapture(before,out _,out _),
                "Own live worker capture accepted an invalidated original external lifetime");
            Require(p.ExactOriginalOwnPublications==0,
                "Own original-lifetime publication ran before controlled lifetime invalidation completed");
            log("apple-lifetime-source state="+t.State+" reuse="+p.Reuse+" originalTree="+t.TreeId+"/"+t.TreeGlobal+
                " replacementGlobal="+p.ReplacementGlobal+" actualPublicDelete=true actualSameSlotReuse="+p.Reuse+
                " nativeMutationCapturedBeforeDeletion=true rawWorkerTreeWrites=0");
        }
        private IEnumerable<AutomationWait> AppleLifetime(AutomationRun run)
        {
            Require(expectedEnabled,"Apple lifetime safety negative requires enabled rescue");
            var p=new AppleLifetimeProof{Reuse=scenario.Contains("-reuse-")};
            try
            {
                using(var worldProof=ObserveAppleWorld())
                {
                    int state=scenario.Contains("appletree8-")?8:2;
                    run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                    foreach(var wait in SetupNaturalOuterAppleWorld(run))yield return wait;
                    for(int sample=0;sample<5000 && p.Transit==null;sample++)
                    {
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,1+(sample&1));
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        run.InSimulation(run.CurrentMission,()=>{
                            p.Transit=FindNaturalAppleTreeTransit(appleWorld.Farm,state);
                            if(sample%128==0 || sample==4999){LogAppleAcquisition(appleWorld.Farm,sample);LogAppleTreeRejectSummary(sample);}return true;
                        });
                    }
                    Require(p.Transit!=null,"No genuine original ripe selected-tree route for the isolated lifetime negative");
                    run.InSimulation(run.CurrentMission,()=>{InvalidateAppleLifetime(p,appleWorld.Farm);return true;});
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    // Follow the native owner beyond the original positive
                    // contact deadlines; do not mistake an80-tick pre-arrival
                    // observation for proof of the subsequent reader behavior.
                    for(int sample=0;sample<256;sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,2);
                        run.InSimulation(run.CurrentMission,()=>{
                            Require(p.Fault==null && workerCycle.Fault==null,p.Fault??workerCycle.Fault);return true;
                        });
                    }
                    // A late genuine allocation may still be NeedsInit at tick512.
                    // Finish only its bounded read-only control, without accepting
                    // an unproved lifetime or extending observation indefinitely.
                    for(int sample=0;sample<16 && p.AmbientPending;sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,2);
                        run.InSimulation(run.CurrentMission,()=>{
                            Require(p.Fault==null && workerCycle.Fault==null,p.Fault??workerCycle.Fault);return true;
                        });
                    }
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        p.OriginalCandidateRetired=!HasOriginalAppleLifetimePending(p);
                        bool oldReceipt=runtime.Workers.Ledger.TryGetOwnedByUnit(p.Transit.Step.Actor.Id,out var ticket) &&
                            ticket.UnitGlobalId==p.Transit.Step.Actor.Global;
                        Require(p.Fault==null && p.Reads>=512 && (!p.Reuse || p.ReplacementAliveGlobalChecked) &&
                            !p.AmbientPending && p.AmbientLives==p.AmbientAliveLives &&
                            p.OriginalCandidateCaptured && p.OriginalCandidateRetired && p.ExactOriginalOwnPublications==0 && !oldReceipt &&
                            workerCycle.Fault==null && workerCycle.RemotePicking==0 &&
                            workerCycle.DropEvents==workerCycle.DropsWithStockIncrease,
                            "Lifetime negative lacks exact original candidate retirement or produced old-life publication/receipt/unproved work");
                        log("apple-lifetime-result state="+state+" reuse="+p.Reuse+" actualReplacementAliveGlobalGuard="+
                            p.ReplacementAliveGlobalChecked+" exactOriginalOwnPublications=0 oldLifetimeReceipt=false"+
                            " naturalReplacementLives="+p.AmbientLives+" naturalReplacementAliveControls="+p.AmbientAliveLives+
                            " permanentVacantSlotRequired=false originalProofRebound=false"+
                            " capturedOldCandidateRetired="+p.OriginalCandidateRetired+
                            " processingReasonUnobserved=true rescueOutcomePerLifetimeUnobserved=true remotePicking=0 physicalDeposits="+
                            workerCycle.DropsWithStockIncrease+" followupTickCallbacks="+p.Reads+" minimumWaitedNativeTicks=512"+
                            " nativeOutcome="+p.Outcome+" actualDeletionMarkerObserved="+p.SawDeletionMarker+
                            " rescueSuccessClaim=false fullNaturalProductionCycleClaim=false");
                        Plugin.SetGeneralFixtureSession(false);return true;
                    });
                }
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(appleLifetimeFixture,this))appleLifetimeFixture=null;
                if(workerCycle!=null)workerCycle.Active=false;if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
    }
}
