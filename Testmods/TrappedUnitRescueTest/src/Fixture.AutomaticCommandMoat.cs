using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class CommandMoatObservation
        {
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active,PostMidpoint,NextOnly,Written,DrainSeen,FirstOwnNormalized;
            internal int WriterTick,ArmTicks,OwnBegins,OwnSuccesses,OwnRefusals,DrainX,DrainY,DrainSubstep;
            internal int SourceCounter;
            internal long FirstOriginal=long.MinValue,Captures,Attempts,Rescues;
            internal int GoalX,GoalY;
            internal string Fault;
        }
        private CommandMoatObservation commandMoat;
        private static volatile Fixture commandMoatFixture;
        private static bool commandMoatRegistered;
        private IEnumerable<AutomationStep> AutomaticCommandMoatSteps()
        {yield return new AutomationStep("automatic-command-moat-"+(scenario.Contains("-postmidpoint-")?"post-midpoint":"pre-midpoint"),
            TimeSpan.FromSeconds(240),AutomaticCommandMoat,true);}

        private void ArmCommandMoat(CommandMoatObservation proof)
        {
            if(!commandMoatRegistered)
            {
                // Plugin's ordinary recovery registration has order0. The reader
                // observes the following native tick before recovery; the writer
                // publishes only after recovery has already had this tick's turn.
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-command-moat-read",ReadCommandMoatTick,out string readReason,order:-100),readReason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-command-moat-write",WriteCommandMoatTick,out string writeReason,order:100),writeReason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-command-moat-original",new CommandMoatMoveObserver(),out string moveReason,
                    order:200,enabled:()=>commandMoatFixture!=null && !commandMoatFixture.stopped && commandMoatFixture.commandMoat!=null &&
                        commandMoatFixture.commandMoat.Active),moveReason);
                commandMoatRegistered=true;
            }
            commandMoat=proof;proof.Active=true;commandMoatFixture=this;
        }
        private static void WriteCommandMoatTick(int tick)
        {
            var fixture=commandMoatFixture;var proof=fixture?.commandMoat;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.Written || proof.Fault!=null)return;
            try{fixture.TryWriteCommandMoat(proof,tick);}catch(Exception error){proof.Fault=error.Message;}
        }
        private unsafe void TryWriteCommandMoat(CommandMoatObservation proof,int tick)
        {
            Require(++proof.ArmTicks<=160,"No exact ordinary northbound command step reached the requested source phase");
            var unit=Resolve(proof.Actor);CheckActor(proof.Actor,true);
            var before=runtime.SnapshotUnit(proof.Actor.Id,true,false);
            if(proof.ArmTicks%16==0)log("automatic-command-moat-acquire tick="+tick+" armTicks="+proof.ArmTicks+
                " ai="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                " direction="+runtime.Native.Word(unit,0x90C)+" native916="+runtime.Native.Word(unit,0x916)+
                " nativeA2A="+runtime.Native.Word(unit,0xA2A)+" current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                " next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+" unsupported="+before?.Unsupported);
            if(before==null || before.Unsupported!=null || before.Command==null || before.Job!=null || !before.OrdinaryStep ||
                before.Substep!=(proof.PostMidpoint?3:1) || before.NextX!=before.X || before.NextY!=before.Y-1 ||
                runtime.Native.Word(unit,0x90C)!=0)return;
            //182B00 passes signed916;1855A0 adds signedA2A and executes
            // budget+1 inner iterations. The actual Spearman916=1 therefore
            // publishes SS1->SS3 or SS3->SS5, crossing direction0 threshold4.
            Require(runtime.Native.Word(unit,0xA2A)==0 && runtime.Native.Word(unit,0x916)==1 && runtime.Native.Word(unit,0x8F8)==0,
                "Exact midpoint fixture requires the actual native916=1/A2A=0/unpaused ordinary cadence");
            int animationMode=*((byte*)unit+0x985-0x65C),animationPhase=unchecked((short)runtime.Native.Word(unit,0x9D6));
            if(animationMode==1 && (animationPhase<4 || animationPhase>11) ||
                animationMode==2 && (animationPhase<5 || animationPhase>9))return;
            int clock=unchecked((int)runtime.Native.JobDword(unit,0x9AC));
            int last=unchecked((int)runtime.Native.JobDword(unit,0x9A8));
            int gate=unchecked((short)runtime.Native.Word(unit,0x74C))+unchecked((short)runtime.Native.Word(unit,0x9A2))+unchecked((short)runtime.Native.Word(unit,0x916));
            Require(clock>=0 && last>=0 && clock>=last && clock<int.MaxValue,
                "Fresh fixture unit counters do not qualify for a bounded nonwrapping next-update prediction");
            // The caller increments9AC once, then skips while elapsed<=gate.
            // Wait for an actually due update; never change clocks or movement.
            if((long)clock+1-last<=gate)return;
            Require(before.RequestedX==proof.GoalX && before.RequestedY==proof.GoalY,"Native command goal changed before the actual terrain writer");
            int x=Math.Max(before.X,before.NextX),y=Math.Max(before.Y,before.NextY);
            if(proof.GoalX>=x-2 && proof.GoalX<=x+1 && proof.GoalY>=y-2 && proof.GoalY<=y+1)return;
            var tiles=GameTileManagerAPI.Instance;
            for(int yy=y-1;yy<=y;yy++)for(int xx=x-1;xx<=x;xx++)
            {
                if(!tiles.IsTileInsideMapBounds(xx,yy))return;
                int tile=tiles.GetTileId(xx,yy),occupant=tiles.GetTileUnitId(tile);
                if(!MoatWorkerSourceEligible(tile) || occupant!=0 && occupant!=proof.Actor.Id)return;
            }
            var observer=Plugin.GeneralObserver;
            Require(observer!=null && observer.IsActive==expectedEnabled,"Command moat source activation differs from its requested setting");
            proof.Before=before;proof.WriterTick=tick;proof.Captures=observer.CaptureCount;proof.Attempts=observer.AttemptCount;proof.Rescues=observer.RescueCount;
            proof.SourceCounter=clock;
            proof.Written=true;
            // Width2 covers both accepted endpoints via69850. The independent
            // Next-only case uses the real width1 brush solely on acceptedNext.
            int writerX=proof.NextOnly?before.NextX:x,writerY=proof.NextOnly?before.NextY:y;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(writerX,writerY),writerY,proof.NextOnly?1:2,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            if(proof.NextOnly)
                Require(runtime.Native.OrdinaryGround(before.TileId) &&
                    (tiles.GetTilePropertyFlag(before.NextTileId)&TilePropertyFlag.IsMoat)!=0,
                    "Next-only command writer must leave original Current ordinary and actually moat acceptedNext");
            else for(int yy=y-1;yy<=y;yy++)for(int xx=x-1;xx<=x;xx++)
                    Require((tiles.GetTilePropertyFlag(tiles.GetTileId(xx,yy))&TilePropertyFlag.IsMoat)!=0,"Actual command moat brush rejected a required cell");
            CheckActor(proof.Actor,true);
            unit=Resolve(proof.Actor);
            if(!proof.NextOnly)Require(!HasStructuralExit(proof.Actor),"Actual command moat did not establish the Current trap");
            Require(unit->r_CurrentPositionTileId==(uint)before.TileId && unit->r_NextPositionTileId2==(uint)before.NextTileId &&
                unit->r_MovementSubstep==before.Substep && unit->r_TargetTilePositionX2==proof.GoalX && unit->r_TargetTilePositionY2==proof.GoalY,
                "Original writer synchronously changed the pending step or original command");
            Require(observer.AttemptCount==proof.Attempts && observer.RescueCount==proof.Rescues,"Command moat recovery ran inside original construction");
            if(expectedEnabled)Require(observer.CaptureCount>proof.Captures && observer.PendingCount>0,"Real command moat did not enqueue the deferred causal candidate");
            else Require(observer.CaptureCount==proof.Captures && observer.PendingCount==0,"Disabled command moat captured an owned candidate");
            log("automatic-command-moat-source tick="+tick+" enabled="+expectedEnabled+" postMidpoint="+proof.PostMidpoint+
                " unit="+proof.Actor.Id+"/"+proof.Actor.Global+" current="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+
                " substep="+before.Substep+" direction=0 nativeThreshold=4 native916=1 nativeA2A=0 innerSubsteps=2"+
                " source9AC="+clock+" source9A8="+last+" callerGate="+gate+" animation985="+animationMode+" animation9D6="+animationPhase+
                " nextMovementDue=true bothEndpointsMoat="+!proof.NextOnly+" nextOnly="+proof.NextOnly+
                " writerCallbackOrder=100 recoveryCallbackOrder=0 directRescue=false originalUnsuppressed=true");
        }
        private static void ReadCommandMoatTick(int tick)
        {
            var fixture=commandMoatFixture;var proof=fixture?.commandMoat;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || !proof.Written || proof.DrainSeen || proof.Fault!=null)return;
            try{fixture.ObserveCommandMoatDrain(proof,tick);}catch(Exception error){proof.Fault=error.Message;}
        }
        private unsafe void ObserveCommandMoatDrain(CommandMoatObservation proof,int tick)
        {
            Require(unchecked((uint)(tick-proof.WriterTick))==1,"Command moat did not observe the immediate following native tick");
            var unit=Resolve(proof.Actor);var before=proof.Before;CheckActor(proof.Actor,true);
            int x=proof.PostMidpoint?before.NextX:before.X,y=proof.PostMidpoint?before.NextY:before.Y;
            int tile=proof.PostMidpoint?before.NextTileId:before.TileId;
            Require(unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y && unit->r_CurrentPositionTileId==(uint)tile &&
                unit->r_NextTilePositionX2==before.NextX && unit->r_NextTilePositionY2==before.NextY &&
                unit->r_NextPositionTileId2==(uint)before.NextTileId && unit->r_MovementSubstep==(proof.PostMidpoint?5:3) &&
                runtime.Native.JobDword(unit,0x740)==0 && unit->r_TargetTilePositionX2==proof.GoalX && unit->r_TargetTilePositionY2==proof.GoalY,
                "Native motion did not publish the exact captured pre/post-midpoint pose before the next normal recovery drain");
            Require(runtime.Native.JobDword(unit,0x9AC)==(uint)(proof.SourceCounter+1) &&
                runtime.Native.JobDword(unit,0x9A8)==(uint)(proof.SourceCounter+1),
                "The actual unit caller did not perform exactly the predicted due movement update");
            if(proof.NextOnly)Require(runtime.Native.OrdinaryGround(before.TileId) &&
                (runtime.Native.Flags(before.NextTileId)&(uint)TilePropertyFlag.IsMoat)!=0,
                "Next-only retained proof lost the unchanged ordinary old Current or actual fresh moat Next");
            proof.DrainSeen=true;proof.DrainX=x;proof.DrainY=y;proof.DrainSubstep=unit->r_MovementSubstep;
            Require(Plugin.GeneralObserver.AttemptCount==proof.Attempts,"Recovery preceded the order-minus100 diagnostic pre-drain reader");
            log("automatic-command-moat-pre-drain tick="+tick+" enabled="+expectedEnabled+" postMidpoint="+proof.PostMidpoint+
                " current="+x+","+y+" next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                " substep="+proof.DrainSubstep+" actual9AC="+runtime.Native.JobDword(unit,0x9AC)+" actual9A8="+runtime.Native.JobDword(unit,0x9A8)+
                " raw740=0 readerOrder=-100 beforeOwnNativeMove=true originalGoalUnchanged=true");
        }
        private sealed class CommandMoatMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var fixture=commandMoatFixture;var proof=fixture?.commandMoat;
                if(fixture==null || fixture.stopped || proof==null || !proof.Active || !proof.Written || context==null ||
                    context.SessionId!=fixture.session || context.UnitId!=proof.Actor.Id || context.OriginOwnerGuid!=Plugin.RescueOwnerGuid)return null;
                try
                {
                    Require(proof.DrainSeen,"Owned command continuation preceded the observed normal next-tick drain");
                    Require(context.X==proof.GoalX && context.Y==proof.GoalY && context.Extra==0,"Own command continuation changed the original goal");
                    proof.OwnBegins++;
                    if(proof.OwnBegins==1)
                    {
                        var unit=fixture.Resolve(proof.Actor);
                        proof.FirstOwnNormalized=unit->r_CurrentTilePositionX==proof.DrainX && unit->r_CurrentTilePositionY==proof.DrainY &&
                            unit->r_NextTilePositionX2==proof.DrainX && unit->r_NextTilePositionY2==proof.DrainY &&
                            unit->r_MovementSubstep==8 && unit->r_PathPlanStateBitFlags==5 && fixture.runtime.Native.JobDword(unit,0x740)==0;
                        Require(proof.FirstOwnNormalized,"Original-goal probe was not normalized on the exact live pre-drain Current before any bank movement");
                    }
                }
                catch(Exception error){proof.Fault=error.Message;}
                return new CommandMoatMoveCompletion(proof,context);
            }
        }
        private sealed class CommandMoatMoveCompletion : IUnitMoveInvocation
        {
            private readonly CommandMoatObservation proof;private readonly UnitMoveContext context;private int done;
            internal CommandMoatMoveCompletion(CommandMoatObservation proof,UnitMoveContext context){this.proof=proof;this.context=context;}
            public void Complete(UnitMoveCompletion value)
            {
                if(Interlocked.Exchange(ref done,1)!=0 || value==null || !ReferenceEquals(value.Context,context))return;
                if(!value.IsCurrentWorld || !value.NativeInvoked || !value.NativeReturned || value.Failure!=null)
                {proof.Fault="Command continuation did not complete its actual native original";return;}
                if(proof.FirstOriginal==long.MinValue)proof.FirstOriginal=value.NativeResult;
                if(value.NativeResult==1)proof.OwnSuccesses++;else if(value.NativeResult==0)proof.OwnRefusals++;
                else proof.Fault="Command continuation returned an unsupported native result";
            }
        }
        private unsafe string CommandMoatActorOutcome(CommandMoatObservation proof)
        {
            if(!UnitAccess.TryGetById(proof.Actor.Id,out GameUnit* unit,out _) || unit==null)return "native-slot-unavailable";
            if(unit->r_GlobalId!=proof.Actor.Global)return "original-lifetime-no-longer-in-slot";
            if(!UnitAccess.IsReallyAlive(unit))return "native-not-really-alive-state-"+(int)unit->r_AliveState+"-ai-"+unit->r_AIState;
            CheckActor(proof.Actor,true);
            var current=runtime.SnapshotUnit(proof.Actor.Id,true,false);
            Require(current!=null && current.TribeGlobalId==proof.Before.TribeGlobalId,"Original native command tribe lifetime changed");
            return "alive-current-"+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+"-ai-"+unit->r_AIState;
        }
        private IEnumerable<AutomationWait> AutomaticCommandMoat(AutomationRun run)
        {
            var proof=new CommandMoatObservation{PostMidpoint=scenario.Contains("-postmidpoint-"),NextOnly=scenario.Contains("-nextonly-")};
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx,cy);proof.Actor=actors[0];});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    VerifyLiveAndRemember();int targetX=proof.Actor.X,targetY=proof.Actor.Y-10;
                    Require(FlatSquare(targetX,targetY,1,out int targetHeight) && targetHeight==elevation,"No clear northbound ordinary command endpoint");
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(proof.Actor.Tribe,targetX,targetY),"Native single-tribe command was rejected");
                    var initial=runtime.SnapshotUnit(proof.Actor.Id,true,false);
                    Require(initial!=null && initial.Unsupported==null && initial.Command!=null && initial.AIState==101 &&
                        (initial.Flags==2 || initial.Flags==4),"Native command did not publish the ordinary active soldier intent");
                    proof.GoalX=initial.RequestedX;proof.GoalY=initial.RequestedY;
                    Plugin.SetGeneralFixtureSession(expectedEnabled);ArmCommandMoat(proof);return true;
                });
                bool arrived=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?320:80);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool finished=run.InSimulation(run.CurrentMission,()=>{
                        Require(proof.Fault==null,"Command moat observation failed: "+proof.Fault);
                        if(!proof.Written || !proof.DrainSeen)return false;
                        if(!AutomaticActorAlive(proof.Actor))return true;
                        CheckActor(proof.Actor,true);arrived=MovementOutcome(proof.Actor,proof.GoalX,proof.GoalY);
                        return expectedEnabled && arrived;
                    });
                    if(finished)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    Require(proof.Fault==null && proof.Written && proof.DrainSeen,"Real command moat source or exact midpoint proof was not obtained: "+proof.Fault);
                    var observer=Plugin.GeneralObserver;bool alive=AutomaticActorAlive(proof.Actor);
                    if(expectedEnabled)
                    {
                        Require(alive && arrived && proof.FirstOwnNormalized && proof.OwnBegins>0 && proof.OwnSuccesses>0,
                            "Automatically recovered command did not retain the actual native goal and complete physical travel");
                        Require(observer.AttemptCount==proof.Attempts+1 && observer.RescueCount==proof.Rescues+1,"Command moat was not recovered by exactly one normal deferred attempt");
                        CheckActor(proof.Actor,true);
                    }
                    else Require(observer.AttemptCount==proof.Attempts && observer.RescueCount==proof.Rescues && proof.OwnBegins==0,
                        "Disabled command moat case executed its own recovery");
                    log("automatic-command-moat-result enabled="+expectedEnabled+" postMidpoint="+proof.PostMidpoint+" nextOnly="+proof.NextOnly+" alive="+alive+
                        " nativeOutcome="+CommandMoatActorOutcome(proof)+
                        " endpoint="+arrived+" preDrain="+proof.DrainX+","+proof.DrainY+" preDrainSubstep="+proof.DrainSubstep+
                        " ownMoves="+proof.OwnBegins+" ownNative1="+proof.OwnSuccesses+" ownNative0="+proof.OwnRefusals+
                        " firstActualNative="+proof.FirstOriginal+" originalGoal="+proof.GoalX+","+proof.GoalY+
                        " baselineEscapeNotAssumed=true syntheticPoseWrites=0 directRecoveryCalls=0");
                    Plugin.SetGeneralFixtureSession(false);return true;
                });
            }
            finally{proof.Active=false;if(ReferenceEquals(commandMoatFixture,this))commandMoatFixture=null;}
        }
    }
}
