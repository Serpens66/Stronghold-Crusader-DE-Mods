using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteAivFixtureFrame(ulong system,int player,int frame,int restricted,byte freeOrForced);
        private int aivBridgeX,aivBridgeY,aivGate;
        private uint aivGateGlobal;
        private IEnumerable<AutomationStep> AivBridgeSteps()
        {
            yield return new AutomationStep("actual-ai-aiv-drawbridge-observer",TimeSpan.FromSeconds(180),AivBridgePilot,true);
        }
        private IEnumerable<AutomationWait> AivBridgePilot(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
            Execute(run,()=>PrepareAivBridgeWorld());
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,64);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            foreach(var wait in WaitMountedIdleBoundary(run))yield return wait;
            Execute(run,()=>{
                PrepareMountedActors();
                for(int i=0;i<actors.Count;i++)
                {
                    var actor=actors[i];
                    Require(runtime.Native.Relocate(actor.Id,aivBridgeX+i*4,aivBridgeY+4),"Native AIV actor staging failed");
                    CheckActor(actor,true);actor.SourceX=actor.X;actor.SourceY=actor.Y;
                }
            });
            // Do not use Fixture.Execute here: original Vanilla/Fixes construction is unsuppressed.
            run.InSimulation(run.CurrentMission,()=>{
                Plugin.SetGeneralFixtureSession(expectedEnabled);
                var observer=Plugin.GeneralObserver;
                Require(observer!=null && observer.IsActive==expectedEnabled,"General observer session did not match fixture mode");
                long captures=observer.CaptureCount,attempts=observer.AttemptCount,rescues=observer.RescueCount;
                int noOp=InvokeAivBridgeFrame(1);
                Require(noOp==0,"Restricted native drawbridge call was not a no-op");
                Require(observer.RescueCount==rescues,"No-op AIV frame caused an observer relocation");
                foreach(var actor in actors){CheckActor(actor,true);Require(AtSource(actor),"No-op AIV frame moved a real actor");}
                Require(!AivBridgeAtAnchor(),"Restricted native AIV call unexpectedly created a bridge");
                if(expectedEnabled)Require(observer.CaptureCount==captures+1 && observer.AttemptCount>=attempts+actors.Count,
                    "Real no-op AIV call did not traverse the installed observer broker");
                else Require(observer.CaptureCount==captures && observer.AttemptCount==attempts,"Disabled observer processed a no-op frame");
                log("aiv-bridge-noop realNative51790=true restricted=1 unchangedActors=true relocated=0 enabled="+expectedEnabled);
                captures=observer.CaptureCount;attempts=observer.AttemptCount;rescues=observer.RescueCount;
                int result=InvokeAivBridgeFrame(0);
                Require(result==1 && AivBridgeAtAnchor(),"Actual AIV frame did not construct a native drawbridge beside the real gate");
                foreach(var actor in actors)
                {
                    CheckActor(actor,true);
                    if(expectedEnabled)Require(!AtSource(actor) && HasStructuralExit(actor),"General observer did not rescue an actual newly trapped current occupant");
                    else Require(AtSource(actor) && !HasStructuralExit(actor),"Disabled AIV observer did not preserve the actual vanilla trap");
                }
                if(expectedEnabled)Require(observer.CaptureCount==captures+1 && observer.AttemptCount>=attempts+actors.Count && observer.RescueCount==rescues+actors.Count,
                    "Actual AIV observer did not account for every original rescued actor");
                else Require(observer.CaptureCount==captures && observer.AttemptCount==attempts && observer.RescueCount==rescues,
                    "Disabled general observer executed construction rescue");
                log("aiv-bridge-construction result="+result+" realNative51790=true originalConstructionUnsuppressed=true enabled="+expectedEnabled+
                    " actors="+actors.Count+" actualObserverRescues="+(observer.RescueCount-rescues)+" identityHealthTribeOccupancy=true");
                return true;
            });
            if(!expectedEnabled)
            {
                run.InSimulation(run.CurrentMission,()=>{log("aiv-bridge-result enabled=false actualObserverInactive=true actualVanillaTrap=true identityHealthTribeOccupancy=true");Plugin.SetGeneralFixtureSession(false);RetireActors();return true;});
                yield break;
            }
            int targetX=aivBridgeX+8,targetY=aivBridgeY+8;
            run.InSimulation(run.CurrentMission,()=>{
                foreach(var actor in actors)Require(runtime.Native.Reissue(actor.Id,targetX,targetY),"Rescued original actor rejected a real subsequent movement order");
                return true;
            });
            bool reached=false;var movementRetries=new Dictionary<int,int>();
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<256 && !reached;sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,2);
                int observation=sample;
                reached=run.InSimulation(run.CurrentMission,()=>AivBridgeActorsReached(targetX,targetY,observation,movementRetries));
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            run.InSimulation(run.CurrentMission,()=>{
                Require(reached,"Rescued AIV current occupants did not physically reach their subsequent real movement endpoint");
                foreach(var actor in actors)CheckActor(actor,true);
                log("aiv-bridge-result enabled=true actualObserver=true currentOccupantsOnly=true physicallyReached="+targetX+","+targetY+
                    " actors="+actors.Count+" identityHealthTribeOccupancy=true pendingNextOnlyCoverage=false");
                Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
            });
        }
        private unsafe void PrepareAivBridgeWorld()
        {
            Require(actors.Count==0,"Previous actors were not retired");SelectMountedAiOwner();FindMountedSite();
            GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_GATE_MAIN,BuildingScales.GetScale(eMappers.MAPPER_GATE_MAIN),81,true,true);
            aivGate=GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(cx,cy));
            GameBuilding* gate=null;
            Require(aivGate>0 && BuildingAccess.TryGetById(aivGate,out gate,out _) && gate!=null && gate->r_PlayerIdOwner==owner &&
                gate->r_BuildingType==eStructs.STRUCT_GATE_MAIN && gate->r_SpriteVariationIndex==81,
                "Native gate orientation/identity does not satisfy the audited AIV drawbridge preflight");
            aivGateGlobal=gate->r_GlobalId;aivBridgeX=cx+1;aivBridgeY=cy+7;
            //7A170 direction0 table2D1540's first7 offsets cover the exact7-wide gate row above this anchor.
            foreach(int dx in new[]{2,1,3,0,4,-1,5})
                Require(GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(aivBridgeX+dx,aivBridgeY-1))==aivGate,
                    "Real gate footprint does not match the native bridge support samples");
            SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,aivBridgeX,aivBridgeY+4);
            SpawnSingle(eChimps.CHIMP_TYPE_ARAB_ASSASIN,aivBridgeX+4,aivBridgeY+4);
            log("aiv-bridge-setup owner="+owner+" gate="+aivGate+"/"+aivGateGlobal+" gateSprite=81 anchor="+aivBridgeX+","+aivBridgeY+
                " realGateSupport=true syntheticUnitState=false controlledAivFrame=true");
        }
        private unsafe int InvokeAivBridgeFrame(int restricted)
        {
            var aiv=GameAIVManagerAPI.Instance;GamePlayerResources* player=null;AivVillageState* village=null;AivBuildStep* frame=null;
            Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,out player) && player!=null,"Actual AI resources unavailable");
            int slot=checked((int)player->r_AIVVillageSlotIndex),index=AivVillageState.BUILD_STEP_CAPACITY-1;
            Require(slot>=AivSystem.FIRST_LIVE_VILLAGE_SLOT && slot<=AivSystem.LAST_LIVE_VILLAGE_SLOT && aiv.TryGetVillageBySlot(slot,out village) && village!=null && village->OwnerPlayerId==owner &&
                aiv.TryGetBuildStep(slot,index,out frame) && frame!=null,"Actual AI AIV slot/frame unavailable");
            GameBuilding* gate=null;
            Require(BuildingAccess.TryGetById(aivGate,out gate,out _) && gate!=null && gate->r_GlobalId==aivGateGlobal,
                "Original native bridge support gate disappeared before AIV execution");
            AivBuildStep saved=*frame;string header=AivFixtureHeader(village);int orderedCount=village->OrderedMapTileCount;int result;
            try
            {
                *frame=new AivBuildStep{State=AivBuildStepState.Pending,BuildingType=eMappers.MAPPER_DRAWBRIDGE,TileCount=1,
                    MapTileIdOrBufferIndex=GameTileManagerAPI.Instance.GetTileId(aivBridgeX,aivBridgeY)};
                var execute=Marshal.GetDelegateForFunctionPointer<ExecuteAivFixtureFrame>(runtime.Native.ImageBase+0x51790);
                result=execute((ulong)aiv.GetAIVSystemPointer(),owner,index,restricted,1);
            }
            finally{*frame=saved;}
            Require(AivFixtureHeader(village)==header && village->OrderedMapTileCount==orderedCount,
                "Native single-building AIV call unexpectedly changed its village header/cursor or ordered-map count");
            log("aiv-bridge-frame-restored slot="+slot+" frame="+index+" headerCursorUnchanged=true orderedMapCountUnchanged=true originalStepRestored=true");
            return result;
        }
        private unsafe string AivFixtureHeader(AivVillageState* village)
        {
            int size=Marshal.OffsetOf<AivVillageState>(nameof(AivVillageState.BuildStepsBuffer)).ToInt32();
            var bytes=new byte[size];byte* source=(byte*)village;
            for(int i=0;i<size;i++)bytes[i]=source[i];
            return Convert.ToBase64String(bytes);
        }
        private unsafe bool AivBridgeAtAnchor()
        {
            int id=GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(aivBridgeX,aivBridgeY));
            return id>0 && BridgeType(id);
        }
        private unsafe bool AivBridgeActorsReached(int x,int y,int sample,Dictionary<int,int> retries)
        {
            bool reached=true;
            foreach(var actor in actors)
            {
                CheckActor(actor,true);var unit=Resolve(actor);
                bool arrived=actor.X==x && actor.Y==y && unit->r_MovementSubstep==8;
                reached &= arrived;
                if(sample%16==0 || sample==255)
                    log("aiv-bridge-movement unit="+actor.Id+"/"+actor.Global+" sample="+sample+" arrived="+arrived+
                        " current="+actor.X+","+actor.Y+" requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+
                        " expected="+x+","+y+" flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep);
                retries.TryGetValue(actor.Id,out int count);
                if(!arrived && sample>0 && sample%16==0 && count<4 &&
                    (unit->r_TargetTilePositionX2!=x || unit->r_TargetTilePositionY2!=y || unit->r_PathPlanStateBitFlags==0))
                {
                    Require(runtime.Native.Reissue(actor.Id,x,y),"Rescued original AIV actor rejected a bounded real movement retry");
                    retries[actor.Id]=count+1;
                    log("aiv-bridge-native-movement-retry unit="+actor.Id+" retry="+(count+1)+" manualUnitState=false");
                }
            }
            return reached;
        }
    }
}
