using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Units;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> AutomaticNextSteps()
        {
            yield return new AutomationStep("automatic-blocked-next-normalization",TimeSpan.FromSeconds(180),AutomaticNext,true);
        }
        private IEnumerable<AutomationWait> AutomaticNext(AutomationRun run)
        {
            int targetX=0,targetY=0;UnitSnapshot captured=null;
            run.InSimulation(run.CurrentMission,()=>{
                Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx-1,cy);
                targetX=cx+9;targetY=cy;return true;
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            // Slow only the phase-acquisition window. The condition is an actual
            // accepted ordinary step, never a fabricated Next/substep/AI field.
            Director.instance.SetEngineFrameRate(10);
            run.InSimulation(run.CurrentMission,()=>{
                VerifyLiveAndRemember();Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(actors[0].Tribe,targetX,targetY),
                    "Next-only actor did not accept the original ordinary move");return true;
            });
            for(int sample=0;sample<12 && captured==null;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                captured=run.InSimulation(run.CurrentMission,()=>CaptureAcceptedBridgeNext());
            }
            Require(captured!=null,"No real first accepted step reached the future bridge corner");
            long beforeAttempts=0,beforeRescues=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    var actor=actors[0];Plugin.SetGeneralFixtureSession(expectedEnabled);
                    NextResumeProbe.Arm(this,actor,targetX,targetY);
                    var observer=Plugin.GeneralObserver;beforeAttempts=observer.AttemptCount;beforeRescues=observer.RescueCount;
                    GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_DRAWBRIDGE,
                        BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                    var tiles=GameTileManagerAPI.Instance;
                    Require(BridgeType(tiles.GetTileBuildingId(tiles.GetTileId(cx,cy))),"Next-only native bridge was not built");
                    Require(runtime.Native.OrdinaryGround(captured.TileId) && !runtime.Native.OrdinaryGround(captured.NextTileId),
                        "Next-only fixture must keep Current ordinary and invalidate only its accepted Next");
                    CheckActor(actor,true);Require(AtSource(actor),"Next-only recovery executed inside construction");
                    Require(observer.AttemptCount==beforeAttempts,"Next-only construction performed immediate rescue");
                    if(expectedEnabled)Require(observer.PendingCount>0,"Next-only mutation produced no automatic candidate");
                    log("automatic-next-before enabled="+expectedEnabled+" unit="+actor.Id+"/"+actor.Global+
                        " current="+captured.X+","+captured.Y+" next="+captured.NextX+","+captured.NextY+
                        " substep="+captured.Substep+" currentOrdinary=true nextInvalid=true realStep=true directRescueCalls=0");return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    var observer=Plugin.GeneralObserver;CheckActor(actors[0],true);
                    if(expectedEnabled)
                    {
                        Require(observer.RescueCount==beforeRescues+1,"Next-only candidate was not recovered exactly once");
                        Require(NextResumeProbe.Calls>0 && NextResumeProbe.FirstNormalized && NextResumeProbe.FirstNative==1,
                            "Next-only recovery did not normalize at original Current before publishing the continued route");
                    }
                    else Require(observer.AttemptCount==beforeAttempts && observer.RescueCount==beforeRescues && NextResumeProbe.Calls==0,
                        "Disabled Next-only source made an owned recovery call");
                    return true;
                });
                Director.instance.SetEngineFrameRate(250);
                if(expectedEnabled)
                {
                    bool arrived=false;
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    for(int sample=0;sample<80 && !arrived;sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,16);
                        arrived=run.InSimulation(run.CurrentMission,()=>MovementOutcome(actors[0],targetX,targetY));
                    }
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(arrived,"Next-only actor did not complete its unchanged original movement goal");
                }
                run.InSimulation(run.CurrentMission,()=>{
                    CheckActor(actors[0],true);
                    log("automatic-next-result enabled="+expectedEnabled+" originalCurrentNormalization="+NextResumeProbe.FirstNormalized+
                        " ownCalls="+NextResumeProbe.Calls+" firstNative="+NextResumeProbe.FirstNative+
                        " endpoint="+expectedEnabled+" originalGoal="+targetX+","+targetY+
                        " identity=true health=true tribe=true occupancy=true baselineEscapeNotAssumed=true");
                    Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally{NextResumeProbe.Release(this);}
        }
        private UnitSnapshot CaptureAcceptedBridgeNext()
        {
            var actor=actors[0];CheckActor(actor,true);
            var snapshot=runtime.SnapshotUnit(actor.Id,true,false);
            Require(snapshot!=null && snapshot.GlobalId==actor.Global,"Next-only actor lifetime changed");
            Require(snapshot.X==actor.SourceX && snapshot.Y==actor.SourceY,
                "Phase acquisition missed the first accepted step; fix the fixture sampling before retrying");
            if(!snapshot.OrdinaryStep || snapshot.Substep>=8)return null;
            Require(snapshot.Unsupported==null,"First step is not an admitted ordinary travel state: "+snapshot.Unsupported);
            Require(snapshot.NextX==cx && snapshot.NextY==cy,"First route step did not enter the intended future corner");
            return snapshot;
        }
        // A read-only assertion observer at the existing shared owner. Its first
        // owned move occurs after native normalization and before original reissue.
        private sealed class NextResumeProbe : IUnitMoveObserver
        {
            private static volatile Fixture current;
            private static Actor actor;
            private static int goalX,goalY;
            private static bool registered;
            internal static int Calls;
            internal static bool FirstNormalized;
            internal static long FirstNative;
            internal static void Arm(Fixture value,Actor original,int targetX,int targetY)
            {
                if(!registered)
                {
                    Require(UnitMoveEvents.TryRegister(Plugin.Guid,"test-next-current-normalization",new NextResumeProbe(),out string reason,
                        order:200,enabled:()=>current!=null && !current.stopped),"Next-only assertion observer unavailable: "+reason);
                    registered=true;
                }
                actor=original;goalX=targetX;goalY=targetY;Calls=0;FirstNormalized=false;FirstNative=long.MinValue;current=value;
            }
            internal static void Release(Fixture owner)
            {if(ReferenceEquals(current,owner)){current=null;actor=null;}}
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var fixture=current;var original=actor;
                if(fixture==null || fixture.stopped || original==null || context.SessionId!=fixture.session ||
                    context.UnitId!=original.Id || context.OriginOwnerGuid!=Plugin.Guid)return null;
                Calls++;if(Calls!=1)return null;
                GameUnit* unit=null;
                FirstNormalized=context.X==goalX && context.Y==goalY && context.Extra==0 &&
                    UnitAccess.TryGetById(original.Id,out unit,out _) && unit!=null && unit->r_GlobalId==original.Global &&
                    unit->r_CurrentTilePositionX==original.SourceX && unit->r_CurrentTilePositionY==original.SourceY &&
                    unit->r_NextTilePositionX2==original.SourceX && unit->r_NextTilePositionY2==original.SourceY &&
                    unit->r_CurrentPositionTileId==(uint)GameTileManagerAPI.Instance.GetTileId(original.SourceX,original.SourceY) &&
                    unit->r_NextPositionTileId2==unit->r_CurrentPositionTileId && unit->r_PathPlanStateBitFlags==5 &&
                    unit->r_TargetTilePositionX2==context.X && unit->r_TargetTilePositionY2==context.Y &&
                    unit->r_MovementSubstep==8 && *(uint*)((byte*)unit+0xE4)==0;
                // Audited native740 is typed GameUnit+E4 (record view starts65C).
                // The owned Reissue contract requires this packed next marker clear.
                return new Completion(context);
            }
            private sealed class Completion : IUnitMoveInvocation
            {
                private readonly UnitMoveContext context;private int completed;
                internal Completion(UnitMoveContext context){this.context=context;}
                public void Complete(UnitMoveCompletion result)
                {
                    if(Interlocked.Exchange(ref completed,1)!=0 || result==null || !ReferenceEquals(result.Context,context))return;
                    if(result.IsCurrentWorld && result.NativeInvoked && result.NativeReturned && result.Failure==null)FirstNative=result.NativeResult;
                }
            }
        }
    }
}
