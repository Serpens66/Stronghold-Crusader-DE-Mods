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
using CrusaderDE;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class AppleTreeTransit
        {
            internal AppleStepPlacement Step;
            internal int State,TreeId,TreeX,TreeY,InitialCounter,SourceTick;
            internal uint TreeGlobal,TreeFlags,SourceGrowthProgress;
            internal bool Active,OriginalTreeContact,FirstNormalized,OriginalJobChecked;
            internal int OwnMoves,ActualNative1;
            internal string Fault;
        }
        private AppleTreeTransit appleTreeTransit;
        private static volatile Fixture appleTreeFixture;
        private static bool appleTreeTickRegistered;
        private readonly Dictionary<string,int> appleTreeRejects=new Dictionary<string,int>();
        private int appleTreeRejectWitnesses;
        // Native107390 seeds phase1 progress500, then calls106A70 synchronously:
        // actual readback is phase2/progress0. After200 natural updates it reaches
        // ripe phase3 lasting1000. These are fixture admission/deadline
        // bounds, not a claim that arbitrary native paths finish in these times.
        // R8 follows a genuine completed R2/pick, so admit its later source in
        // a separate300/250 window. Both require50 actual updates of reserve.
        private const uint AppleRipeDuration=1000,AppleRipeReserve=50;
        private static int AppleOriginalContactMaximumTicks(int state)=>state==8?250:450;
        private static uint AppleMinimumRipeRemaining(int state)=>(uint)AppleOriginalContactMaximumTicks(state)+AppleRipeReserve;
        private sealed class OuterAppleTreeSetup
        {
            internal int NativeIndex,X,Y,Tile,FarmX,FarmY;
            internal uint Global;
            internal bool ObservedAlive;
        }
        private OuterAppleTreeSetup outerAppleTree;

        private IEnumerable<AutomationWait> SetupNaturalOuterAppleWorld(AutomationRun run)
        {
            int startTick=0;
            Execute(run,()=>{
                SetupNaturalAppleFarm();
                Require(SimulationTickEvents.TryGetCurrentTick(out startTick),"Original farm setup has no native tick");
            });
            // Offset the real constructor rather than editing maturity later.
            // The farm must still have no naturally acquired employee afterwards.
            Director.instance.SetEngineFrameRate(50);
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,128);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                Require(SimulationTickEvents.TryGetCurrentTick(out int tick),"Delayed outer tree has no native tick");
                uint elapsed=unchecked((uint)(tick-startTick));
                Require(elapsed>=128 && elapsed<=144,"Delayed constructor missed its bounded natural setup window");
                PrepareOuterAppleTree(appleWorld.Farm);
                log("automatic-apple-outer-tree-natural-delay farmTick="+startTick+" constructorTick="+tick+
                    " actualElapsedTicks="+elapsed+" workerNotAcquired=true rawMaturityWrites=0");
            });
        }

        private bool OutsideOriginalOrchard(int x,int y)=>outerAppleTree!=null &&
            (x<outerAppleTree.FarmX || x>=outerAppleTree.FarmX+10 ||
             y<outerAppleTree.FarmY || y>=outerAppleTree.FarmY+10);
        private void CheckOuterAppleAccess(int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;int tile=runtime.Native.Tile(x,y);
            Require(OutsideOriginalOrchard(x,y) && tile>=0 && runtime.Native.OrdinaryGround(tile) &&
                tiles.GetTileBuildingId(tile)==0 && tiles.GetTileVegetationId(tile)==0 && runtime.Native.Head(tile)==0 &&
                (runtime.Native.Flags(tile)&(uint)TilePropertyFlag.IsFarm)==0 && AppleStepAccessHasExit(x,y),
                "Outer apple tree access is not genuinely free ordinary non-farm ground outside the original orchard");
        }
        private unsafe void PrepareOuterAppleTree(int farmId)
        {
            Require(BuildingAccess.TryGetById(farmId,out GameBuilding* farm,out _) && farm!=null &&
                farm->r_GlobalId==appleWorld.FarmGlobal && farm->r_WorkerId_1==0 &&
                farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_APPLEFARM &&
                farm->r_AliveState==AliveState.IsAlive,
                "Outer tree setup must precede the farm's natural worker acquisition");
            var tiles=GameTileManagerAPI.Instance;
            // Native72A50 calls72490 with the original placement origin and10.
            // Verify its actual field envelope rather than assuming the smaller
            // house-core occupancy rectangle describes the whole orchard.
            for(int y=cy;y<cy+10;y++)for(int x=cx;x<cx+10;x++)
                Require((tiles.GetTilePropertyFlag(tiles.GetTileId(x,y))&TilePropertyFlag.IsFarm)!=0,
                    "Actual original orchard does not match the checked native10x10 field envelope");
            outerAppleTree=new OuterAppleTreeSetup{FarmX=cx,FarmY=cy,X=cx-4,Y=cy+4};
            var setup=outerAppleTree;setup.Tile=runtime.Native.Tile(setup.X,setup.Y);
            CheckOuterAppleAccess(setup.X,setup.Y);
            for(int d=0;d<8;d+=2)CheckOuterAppleAccess(setup.X+Dx[d]*2,setup.Y+Dy[d]*2);
            // This is the public native object constructor's normal initial
            // growthStage, not a later maturity/counter/job write. The native
            // constructor normalizes input1 to actual2/0 synchronously; it then
            // grows naturally into ripe3. Starting immediately ripe expired
            // before original arrival in historical run134. Actual remaining
            // maturity is independently qualified below. Original farm trees
            // remain untouched and continue growing normally.
            // Explicit public level2 agrees with the normalized real phase2.
            //59520 recomputes proximity from the actual stage after construction;
            // automatic proximity would therefore produce the same five-cell cross.
            long created=GameVegetationManagerAPI.Instance.CreateEx(checked((ushort)setup.X),checked((ushort)setup.Y),
                VegetationType.AppleTree,treeProximityAreaLevel:2,growthStage:1);
            Require(created>0 && created<int.MaxValue,"Public external apple tree creation failed");
            setup.NativeIndex=checked((int)created);
            Require(NativeVegetationAccess.TryGet(setup.NativeIndex,out var tree) && tree!=null,
                "Created outer tree cannot be resolved in its actual native index domain");
            log("automatic-apple-outer-constructor-readback nativeIndex="+setup.NativeIndex+" global="+tree->r_GlobalId+
                " actualLife="+tree->r_AliveState+" actualType="+tree->r_VegetationType+" actualGrowthStage="+tree->r_GrowthStage+
                " actualGrowthProgress="+tree->r_GrowthProgress+" actualXY="+tree->r_TilePositionX+","+tree->r_TilePositionY+
                " actualTile="+tree->r_TileId+" inputGrowthStage=1 explicitProximityLevel=2");
            Require(tree->r_AliveState==AliveState.NeedsInit && tree->r_VegetationType==VegetationType.AppleTree &&
                tree->r_GrowthStage==2 && tree->r_GrowthProgress==0 && tree->r_TilePositionX==setup.X && tree->r_TilePositionY==setup.Y &&
                tree->r_TileId==(uint)setup.Tile && tree->r_GlobalId!=0,
                "Created native tree record/type/lifetime/initial stage differs from its real public constructor");
            setup.Global=tree->r_GlobalId;
            Require(tiles.GetTileVegetationId(setup.Tile)==setup.NativeIndex &&
                (runtime.Native.Flags(setup.Tile)&(uint)(TilePropertyFlag.IsTree|TilePropertyFlag.TreeProximity))==
                    (uint)(TilePropertyFlag.IsTree|TilePropertyFlag.TreeProximity),
                "Public tree creation did not publish its actual native tile/proximity binding");
            for(int d=0;d<8;d+=2)
            {
                Require((runtime.Native.Flags(runtime.Native.Tile(setup.X+Dx[d],setup.Y+Dy[d]))&
                    (uint)TilePropertyFlag.TreeProximity)!=0,"Actual native tree proximity cross missing");
                CheckOuterAppleAccess(setup.X+Dx[d]*2,setup.Y+Dy[d]*2);
            }
            log("automatic-apple-outer-tree-setup nativeIndex="+setup.NativeIndex+" sdkId="+(setup.NativeIndex+1)+
                " global="+setup.Global+" actualTile="+setup.X+","+setup.Y+" futureOrchard="+cx+","+cy+",10"+
                " initialLife="+tree->r_AliveState+" initialGrowthStage="+tree->r_GrowthStage+
                " constructorInputPhase=1 actualProgress="+tree->r_GrowthProgress+" explicitProximityLevel=2"+
                " realProximityCross=true fourApproachesFree=true nativeFarmTreesUnchanged=true workerNotAcquired=true");
        }
        private unsafe bool OuterAppleTreeReady()
        {
            var setup=outerAppleTree;
            Require(setup!=null,"Outer apple tree setup was not captured");
            Require(NativeVegetationAccess.TryGet(setup.NativeIndex,out var tree) && tree!=null &&
                tree->r_GlobalId==setup.Global && tree->r_VegetationType==VegetationType.AppleTree &&
                tree->r_TilePositionX==setup.X && tree->r_TilePositionY==setup.Y && tree->r_TileId==(uint)setup.Tile &&
                GameTileManagerAPI.Instance.GetTileVegetationId(setup.Tile)==setup.NativeIndex,
                "Original outer tree slot/global/physical tile binding changed before native selection");
            if(tree->r_AliveState==AliveState.NeedsInit && !setup.ObservedAlive)return false;
            Require(tree->r_AliveState==AliveState.IsAlive,"Original outer tree stopped being alive before acquisition");
            if(!setup.ObservedAlive)
            {
                setup.ObservedAlive=true;
                log("automatic-apple-outer-tree-initialized nativeIndex="+setup.NativeIndex+" global="+setup.Global+
                    " actualLife="+tree->r_AliveState+" naturalGrowthStage="+tree->r_GrowthStage+" observedNativeInit=true");
            }
            return true;
        }

        private AppleTreeTransit RejectAppleTree(string reason,UnitSnapshot before=null,string detail=null)
        {
            appleTreeRejects.TryGetValue(reason,out int count);appleTreeRejects[reason]=count+1;
            if(count==0 && appleTreeRejectWitnesses++<24)
                log("automatic-apple-tree-reject reason="+reason+" detail="+(detail??"none")+
                    " actualIncludeStepSnapshot=true unit="+(before==null?"none":before.Id+"/"+before.GlobalId)+
                    " ai="+before?.AIState+" flags="+before?.Flags+" ss="+before?.Substep+" ordinaryStep="+before?.OrdinaryStep+
                    " unsupported="+(before?.Unsupported??"none")+" hasVegetation="+before?.HasCapturedVegetation+
                    " capturedVegGlobal="+before?.CapturedVegetationGlobal+
                    " current="+(before==null?"none":before.X+","+before.Y+":"+AppleTreeTileFacts(before.TileId))+
                    " next="+(before==null?"none":before.NextX+","+before.NextY+":"+AppleTreeTileFacts(before.NextTileId)));
            return null;
        }
        private string AppleTreeTileFacts(int tile)
        {
            if(tile<0 || tile>=NativeRelocation.TileCapacity)return "invalid";
            var tiles=GameTileManagerAPI.Instance;
            return "flags="+runtime.Native.Flags(tile).ToString("X8")+",ordinary="+runtime.Native.OrdinaryGround(tile)+
                ",moatEligible="+MoatWorkerSourceEligible(tile)+",keepRange="+MoatWorkerWithinOwnKeepRange(tile)+
                ",building="+tiles.GetTileBuildingId(tile)+",vegetation="+tiles.GetTileVegetationId(tile)+",height="+tiles.GetTileHeight(tile);
        }
        private void LogAppleTreeRejectSummary(int sample)
        {
            var parts=new List<string>();foreach(var pair in appleTreeRejects)parts.Add(pair.Key+":"+pair.Value);
            log("automatic-apple-tree-reject-summary sample="+sample+" counts="+string.Join(";",parts.ToArray()));
        }

        private IEnumerable<AutomationStep> AutomaticAppleTreeSteps()
        {yield return new AutomationStep("automatic-natural-apple-tree-"+(scenario.Contains("-appletree8-")?8:2),
            TimeSpan.FromSeconds(1200),AutomaticAppleTree,true);}

        private unsafe AppleTreeTransit FindNaturalAppleTreeTransit(int farmId,int state)
        {
            ValidateAppleWorld();
            if(!OuterAppleTreeReady())return RejectAppleTree("outer-tree-awaiting-native-init");
            GameBuilding* farm=null;GameBuilding* granary=null;
            Require(BuildingAccess.TryGetById(farmId,out farm,out _) && farm!=null &&
                BuildingAccess.TryGetById(appleWorld.Granary,out granary,out _) && granary!=null,
                "Apple tree transit lacks its original live farm/granary");
            int id=farm->r_WorkerId_1;
            if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                unit->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_ControllableForPlayerId!=owner ||
                unit->r_LinkedProductionBuildingId!=farmId || unit->r_LinkedProductionBuildingGlobalId!=appleWorld.FarmGlobal ||
                unit->r_AIState!=state || unit->r_CarryGoodAmount!=0)return RejectAppleTree("not-requested-natural-role-phase");
            var before=runtime.SnapshotUnit(id,true,false);
            if(before==null)return RejectAppleTree("missing-snapshot");
            if(before.Unsupported!=null)return RejectAppleTree("snapshot-unsupported:"+before.Unsupported,before);
            if(before.Job==null)return RejectAppleTree("no-typed-job",before);
            if(!before.OrdinaryStep || before.Substep>=8)return RejectAppleTree("not-started-ordinary-step",before);
            if(!before.HasCapturedVegetation)return RejectAppleTree("no-pre-vegetation-lifetime",before);
            if(before.Flags!=2 && before.Flags!=4)return RejectAppleTree("no-active-route",before);
            if(!runtime.Native.OrdinaryGround(before.TileId))return RejectAppleTree("current-not-ordinary",before);
            if(!MoatWorkerSourceEligible(before.NextTileId))return RejectAppleTree("next-not-moat-eligible",before);
            if(runtime.Native.Head(before.NextTileId)!=0)return RejectAppleTree("next-occupied",before);
            int x=before.NextX,y=before.NextY;
            int treeId=unit->r_TargetVegetationId;
            if(treeId!=outerAppleTree.NativeIndex || before.CapturedVegetationGlobal!=outerAppleTree.Global)
                return RejectAppleTree("native-selected-another-tree",before,"selected="+treeId+" expectedOuter="+
                    outerAppleTree.NativeIndex+"/"+outerAppleTree.Global);
            if(!TryGetNativeAppleTree(treeId,out GameVegetation* tree) || tree->r_AliveState!=AliveState.IsAlive ||
                tree->r_GlobalId!=before.CapturedVegetationGlobal || tree->r_GrowthStage!=3)
                return RejectAppleTree("selected-tree-not-live-ripe-captured-lifetime",before,"nativeIndex="+treeId);
            if(tree->r_GrowthProgress>AppleRipeDuration-AppleMinimumRipeRemaining(state))
                return RejectAppleTree("selected-tree-insufficient-natural-ripe-window",before,
                    "growthProgress="+tree->r_GrowthProgress+" ripeDuration="+AppleRipeDuration+
                    " minimumRemaining="+AppleMinimumRipeRemaining(state));
            int treeX=tree->r_TilePositionX,treeY=tree->r_TilePositionY;
            // Natural tree-approach legs can be only two tiles long. Protecting
            // every approach plus an artificial halo excludes every intermediate
            // tile. Use real width1 on acceptedNext only: exact accesses stay
            // ordinary and retain real exits before/after the original writer.
            if(farm->r_TileAccessPositionX==x && farm->r_TileAccessPositionY==y ||
                granary->r_TileAccessPositionX==x && granary->r_TileAccessPositionY==y ||
                before.RequestedX==x && before.RequestedY==y || treeX==x && treeY==y)
                return RejectAppleTree("next-is-protected-job-or-tree-field",before,"tree="+treeX+","+treeY+" goal="+before.RequestedX+","+before.RequestedY);
            for(int d=0;d<8;d+=2)
            {
                int ax=treeX+Dx[d]*2,ay=treeY+Dy[d]*2;
                if(ax==x && ay==y)return RejectAppleTree("next-is-native-approach",before,"approach="+ax+","+ay);
                if(!AppleTreeAccessOpen(ax,ay))return RejectAppleTree("native-approach-not-open",before,"approach="+ax+","+ay+
                    " "+AppleTreeTileFacts(runtime.Native.Tile(ax,ay))+",physicalExit="+AppleStepAccessHasExit(ax,ay));
            }
            if(!AppleTreeAccessOpen(farm->r_TileAccessPositionX,farm->r_TileAccessPositionY))return RejectAppleTree("farm-access-not-open",before,
                "access="+farm->r_TileAccessPositionX+","+farm->r_TileAccessPositionY+" "+
                AppleTreeTileFacts(runtime.Native.Tile(farm->r_TileAccessPositionX,farm->r_TileAccessPositionY)));
            if(!AppleTreeAccessOpen(granary->r_TileAccessPositionX,granary->r_TileAccessPositionY))return RejectAppleTree("granary-access-not-open",before,
                "access="+granary->r_TileAccessPositionX+","+granary->r_TileAccessPositionY+" "+
                AppleTreeTileFacts(runtime.Native.Tile(granary->r_TileAccessPositionX,granary->r_TileAccessPositionY)));
            if(!AppleTreeAccessOpen(before.RequestedX,before.RequestedY))return RejectAppleTree("goal-access-not-open",before,
                "goal="+before.RequestedX+","+before.RequestedY+" "+AppleTreeTileFacts(runtime.Native.Tile(before.RequestedX,before.RequestedY)));
            int counter=runtime.Native.Word(unit,0x9EC);
            if(state==2 && counter!=0 || state==8 && (counter<1 || counter>3))return RejectAppleTree("counter-does-not-match-natural-phase",before,"counter="+counter);
            if(Math.Abs(before.RequestedX-treeX)+Math.Abs(before.RequestedY-treeY)!=2 ||
                before.RequestedX!=treeX && before.RequestedY!=treeY)return RejectAppleTree("goal-not-native-tree-approach",before,
                    "tree="+treeX+","+treeY+" goal="+before.RequestedX+","+before.RequestedY);
            var world=new NativeWorkerResumeWorld(runtime.Native);
            if(!world.TryCapture(before,out var receipt,out string captureReason))return RejectAppleTree("native-world-capture-rejected",before,captureReason);
            if((receipt.Bindings.Required&WorkerExternalBindings.Vegetation)==0 || receipt.Bindings.VegetationId!=treeId ||
                receipt.Bindings.VegetationGlobal!=tree->r_GlobalId)return RejectAppleTree("native-world-tree-binding-mismatch",before);
            Require(SimulationTickEvents.TryGetCurrentTick(out int sourceTick),"Apple ripe source has no actual native tick");
            return new AppleTreeTransit{State=state,TreeId=treeId,TreeGlobal=tree->r_GlobalId,TreeX=treeX,TreeY=treeY,
                SourceTick=sourceTick,SourceGrowthProgress=tree->r_GrowthProgress,
                TreeFlags=runtime.Native.Flags(runtime.Native.Tile(treeX,treeY)),
                InitialCounter=counter,Step=new AppleStepPlacement{Before=before,X=x,Y=y,
                    FarmX=farm->r_TileAccessPositionX,FarmY=farm->r_TileAccessPositionY,
                    GranaryX=granary->r_TileAccessPositionX,GranaryY=granary->r_TileAccessPositionY,
                    Actor=new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                        Tribe=unit->r_TribeId,X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y}}};
        }
        private static unsafe bool OriginalAppleTransitTreeAlive(AppleTreeTransit proof)=>
            TryGetNativeAppleTree(proof.TreeId,out GameVegetation* tree) && tree->r_AliveState==AliveState.IsAlive &&
            tree->r_GlobalId==proof.TreeGlobal && tree->r_TilePositionX==proof.TreeX && tree->r_TilePositionY==proof.TreeY;

        private bool AppleTreeAccessOpen(int x,int y)
        {
            int tile=runtime.Native.Tile(x,y);
            return tile>=0 && runtime.Native.OrdinaryGround(tile) && NativeIdleEscapeWorld.HasPhysicalExit(
                new RescueCandidate{X=x,Y=y,Tile=tile},runtime.Native.Tile,runtime.Native.OrdinaryGround,runtime.Native.Connections);
        }
        private void ValidateAppleTreeAccesses(AppleTreeTransit proof)
        {
            ValidateAppleStepAccess(proof.Step);
            Require(OriginalAppleTransitTreeAlive(proof) && runtime.Native.Flags(runtime.Native.Tile(proof.TreeX,proof.TreeY))==proof.TreeFlags,
                "Next-only mutation changed the original actual tree record or its tile flags");
            var step=proof.Step;
            Require(AppleTreeAccessOpen(step.FarmX,step.FarmY) && AppleTreeAccessOpen(step.GranaryX,step.GranaryY) &&
                AppleTreeAccessOpen(step.Before.RequestedX,step.Before.RequestedY),"Apple future job access lost ordinary ground or its physical exit");
            for(int d=0;d<8;d+=2)Require(AppleTreeAccessOpen(proof.TreeX+Dx[d]*2,proof.TreeY+Dy[d]*2),
                "Apple native tree approach lost ordinary ground or its physical exit");
        }
        private unsafe AutomaticWorkerMutation ApplyAppleTreeNextOnly(AppleTreeTransit proof,int farm)
        {
            ValidateAppleTreeAccesses(proof);
            var before=proof.Step.Before;var actor=proof.Step.Actor;
            var current=runtime.SnapshotUnit(actor.Id,true,false);
            Require(current!=null && current.Unsupported==null && current.Job!=null && current.GlobalId==before.GlobalId &&
                current.AIState==before.AIState && current.Substep==before.Substep && current.Flags==before.Flags &&
                current.X==before.X && current.Y==before.Y && current.NextX==before.NextX && current.NextY==before.NextY &&
                current.HasCapturedVegetation && current.CapturedVegetationGlobal==before.CapturedVegetationGlobal,
                "Natural original tree route changed before Next-only construction");
            var world=new NativeWorkerResumeWorld(runtime.Native);
            Require(world.TryCapture(before,out _,out string reason),"Original vegetation job unavailable before Next-only writer: "+reason);
            actors.Add(actor);BeginWorkerCycle(actor,farm,before.RequestedX,before.RequestedY);
            BeginAutomaticWorkerMoves(actor,before.RequestedX,before.RequestedY);
            Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
            Require(observer!=null && observer.IsActive==expectedEnabled,"Apple tree source activation differs from fixture option");
            var mutation=new AutomaticWorkerMutation{Actor=actor,GoalX=before.RequestedX,GoalY=before.RequestedY,Cargo=WorkerCarry(actor),
                Captures=observer.CaptureCount,Attempts=observer.AttemptCount,Rescues=observer.RescueCount};
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,before.NextTileId,before.NextY,1,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(runtime.Native.OrdinaryGround(before.TileId) && (runtime.Native.Flags(before.NextTileId)&(uint)TilePropertyFlag.IsMoat)!=0,
                "Actual width1 writer must preserve Current and create acceptedNext moat");
            CheckActor(actor,true);var unit=Resolve(actor);IntPtr pointer=(IntPtr)unit;
            Require(unit->r_CurrentPositionTileId==(uint)before.TileId && unit->r_NextPositionTileId2==(uint)before.NextTileId &&
                unit->r_MovementSubstep==before.Substep && WorkerCarry(actor)==mutation.Cargo &&
                unit->r_TargetTilePositionX2==before.RequestedX && unit->r_TargetTilePositionY2==before.RequestedY &&
                before.Job.Matches(unit->r_UnitChimp,unit->r_AIState,offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),
                    offset=>runtime.Native.JobDword((GameUnit*)pointer,offset)),"Original Next-only writer changed actual pose, immutable tree job, cargo or goal");
            ValidateAppleTreeAccesses(proof);
            Require(observer.AttemptCount==mutation.Attempts && observer.RescueCount==mutation.Rescues,"Apple recovery ran inside original Next-only writer");
            if(expectedEnabled)Require(observer.CaptureCount>mutation.Captures && observer.PendingCount>0,"Real Next-only writer did not capture the original tree route");
            else Require(observer.CaptureCount==mutation.Captures && observer.PendingCount==0,"Disabled Next-only writer captured an owned route");
            log("automatic-apple-tree-next-only-source current="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+
                " substep="+before.Substep+" actualBrushWidth=1 currentStillOrdinary=true allFourApproachesOpen=true directRescue=false");
            return mutation;
        }

        private void ArmAppleTreeContact(AppleTreeTransit proof)
        {
            if(!appleTreeTickRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-apple-original-tree-contact",ObserveAppleTreeContact,
                    out string reason,order:100),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-apple-original-tree-resume",new AppleTreeResumeObserver(),
                    out string moveReason,enabled:()=>appleTreeFixture!=null && !appleTreeFixture.stopped &&
                        appleTreeFixture.appleTreeTransit!=null && appleTreeFixture.appleTreeTransit.Active),moveReason);
                appleTreeTickRegistered=true;
            }
            appleTreeTransit=proof;proof.Active=true;appleTreeFixture=this;
        }
        private sealed class AppleTreeResumeObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var fixture=appleTreeFixture;var proof=fixture?.appleTreeTransit;
                if(fixture==null || fixture.stopped || proof==null || !proof.Active || context==null ||
                    context.OriginOwnerGuid!=Plugin.RescueOwnerGuid || context.SessionId!=fixture.session || context.UnitId!=proof.Step.Actor.Id)return null;
                try
                {
                    if(proof.OwnMoves++==0)
                    {
                        var unit=fixture.Resolve(proof.Step.Actor);var before=proof.Step.Before;IntPtr pointer=(IntPtr)unit;
                        proof.FirstNormalized=unit->r_PathPlanStateBitFlags==5 && unit->r_MovementSubstep==8 &&
                            unit->r_CurrentTilePositionX==unit->r_NextTilePositionX2 && unit->r_CurrentTilePositionY==unit->r_NextTilePositionY2 &&
                            fixture.runtime.Native.JobDword(unit,0x740)==0 &&
                            (unit->r_CurrentPositionTileId==(uint)before.TileId && unit->r_CurrentTilePositionX==before.X && unit->r_CurrentTilePositionY==before.Y ||
                             unit->r_CurrentPositionTileId==(uint)before.NextTileId && unit->r_CurrentTilePositionX==before.NextX && unit->r_CurrentTilePositionY==before.NextY);
                        proof.OriginalJobChecked=context.X==before.RequestedX && context.Y==before.RequestedY && context.Extra==0 &&
                            OriginalAppleTransitTreeAlive(proof) && unit->r_TargetVegetationId==proof.TreeId &&
                            before.Job.Matches(unit->r_UnitChimp,unit->r_AIState,offset=>fixture.runtime.Native.JobWord((GameUnit*)pointer,offset),
                                offset=>fixture.runtime.Native.JobDword((GameUnit*)pointer,offset));
                        Require(proof.FirstNormalized && proof.OriginalJobChecked,"First own original-goal native call lacks normalized pose and original vegetation job");
                    }
                }
                catch(Exception error){proof.Fault=error.Message;}
                return new AppleTreeResumeCompletion(proof,context);
            }
        }
        private sealed class AppleTreeResumeCompletion : IUnitMoveInvocation
        {
            private readonly AppleTreeTransit proof;private readonly UnitMoveContext context;
            private int completed;
            internal AppleTreeResumeCompletion(AppleTreeTransit proof,UnitMoveContext context){this.proof=proof;this.context=context;}
            public void Complete(UnitMoveCompletion value)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || value==null || !ReferenceEquals(value.Context,context))return;
                if(value.IsCurrentWorld && value.NativeInvoked && value.NativeReturned && value.Failure==null && value.NativeResult==1)proof.ActualNative1++;
                else if(!value.IsCurrentWorld || !value.NativeInvoked || !value.NativeReturned || value.Failure!=null)
                    proof.Fault="Own original tree move did not complete an actual current-world native original";
            }
        }
        private static unsafe void ObserveAppleTreeContact(int tick)
        {
            var fixture=appleTreeFixture;var proof=fixture?.appleTreeTransit;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.OriginalTreeContact || proof.Fault!=null)return;
            try
            {
                if(!fixture.AutomaticActorAlive(proof.Step.Actor))return;
                var unit=fixture.Resolve(proof.Step.Actor);
                Require(OriginalAppleTransitTreeAlive(proof),"Original selected apple tree died, moved or changed lifetime before first post-rescue pick");
                Require(unchecked((uint)(tick-proof.SourceTick))<=AppleOriginalContactMaximumTicks(proof.State),
                    "Fixture original-tree contact exceeded its admitted"+AppleOriginalContactMaximumTicks(proof.State)+
                    "-tick natural ripe window; no stable-tree success claimed");
                int counter=fixture.runtime.Native.Word(unit,0x9EC);
                if(counter==proof.InitialCounter)return;
                Require(TryGetNativeAppleTree(proof.TreeId,out var tree) && tree->r_GrowthStage==3,
                    "Original contact outlived the actual natural ripe stage despite the fixture window");
                Require(counter==proof.InitialCounter+1,"First tree contact was not the next natural original picking-counter increment");
                Require(Math.Abs(unit->r_CurrentTilePositionX-proof.TreeX)<=2 && Math.Abs(unit->r_CurrentTilePositionY-proof.TreeY)<=2,
                    "First new picking-counter increment occurred away from the originally selected tree");
                fixture.CheckActor(proof.Step.Actor,true);
                proof.OriginalTreeContact=true;
                fixture.log("automatic-apple-original-tree-contact tick="+tick+" state="+proof.State+
                    " unit="+proof.Step.Actor.Id+"/"+proof.Step.Actor.Global+" tree="+proof.TreeId+"/"+proof.TreeGlobal+
                    " treeTile="+proof.TreeX+","+proof.TreeY+" counter="+proof.InitialCounter+"->"+counter+
                    " actorTile="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+" originalTreeAlive=true"+
                    " sourceGrowthProgress="+proof.SourceGrowthProgress+" elapsedActualTicks="+unchecked((uint)(tick-proof.SourceTick)));
            }
            catch(Exception error){proof.Fault=error.Message;}
        }

        private IEnumerable<AutomationWait> AutomaticAppleTree(AutomationRun run)
        {
            AppleTreeTransit selected=null;
            try
            {
                using(var worldProof=ObserveAppleWorld())
                {
                    int farm=0,state=scenario.Contains("-appletree8-")?8:2;
                    Require(scenario.Contains("-appletree2-") || scenario.Contains("-appletree8-"),"Missing explicit natural Apple2/8 scenario");
                    run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                    foreach(var wait in SetupNaturalOuterAppleWorld(run))yield return wait;
                    farm=appleWorld.Farm;
                    // The first genuine external-tree journey precedes maturity
                    // of the unchanged native farm trees. Observe actual ticks
                    // from the start instead of skipping short R2/R8 legs.
                    Director.instance.SetEngineFrameRate(50);
                    for(int sample=0;sample<5000 && selected==null;sample++)
                    {
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,1+(sample&1));
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        run.InSimulation(run.CurrentMission,()=>{
                            selected=FindNaturalAppleTreeTransit(farm,state);
                            if(sample%128==0 || sample==4999){LogAppleAcquisition(farm,sample);LogAppleTreeRejectSummary(sample);}return true;
                        });
                    }
                    Require(selected!=null,"No natural Apple"+state+" started transit preserves farm/granary/tree and every native tree approach");
                    AutomaticWorkerMutation mutation=null;
                    run.InSimulation(run.CurrentMission,()=>{
                        Require(OriginalAppleTransitTreeAlive(selected),"Original tree lifetime changed before actual construction");
                        if(expectedEnabled)ArmAppleNativeTrace(selected,false);
                        ArmAppleTreeContact(selected);
                        mutation=ApplyAppleTreeNextOnly(selected,farm);
                        Require(OriginalAppleTransitTreeAlive(selected),"Original construction changed the selected tree lifetime");
                        log("automatic-apple-tree-source state="+state+" tree="+selected.TreeId+"/"+selected.TreeGlobal+
                            " treeTile="+selected.TreeX+","+selected.TreeY+" originalCounter="+selected.InitialCounter+
                            " sourceTick="+selected.SourceTick+" actualGrowthStage=3 growthProgress="+selected.SourceGrowthProgress+
                            " ripeRemaining="+(AppleRipeDuration-selected.SourceGrowthProgress)+
                            " minimumRipeRemaining="+AppleMinimumRipeRemaining(state)+
                            " originalContactDeadlineTicks="+AppleOriginalContactMaximumTicks(state)+
                            " capturedExternalGlobal="+selected.Step.Before.CapturedVegetationGlobal+" syntheticAIJobCargoWrites=0");return true;
                    });
                    Director.instance.SetEngineFrameRate(250);
                    bool delivered=false;
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    for(int sample=0;sample<(expectedEnabled?5000:24);sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,4);
                        bool stop=run.InSimulation(run.CurrentMission,()=>{
                            ValidateAppleStepAccess(selected.Step);
                            if(!AutomaticActorAlive(selected.Step.Actor))return true;
                            CheckActor(selected.Step.Actor,true);
                            if(expectedEnabled)Require(selected.Fault==null && workerCycle.Fault==null && appleNativeTrace.Fault==null,
                                "Apple original-tree/physical-cycle observation failed: "+(selected.Fault??workerCycle.Fault??appleNativeTrace.Fault));
                            delivered=selected.OriginalTreeContact && workerCycle.Delivered && workerCycle.CounterIncrements>0 &&
                                workerCycle.PickupEvents>0 && workerCycle.DropsWithStockIncrease>0 && WorkerCarry(selected.Step.Actor)==0;
                            if(sample%128==0)log("automatic-apple-tree-progress state="+state+" sample="+sample+
                                " originalContact="+selected.OriginalTreeContact+" deposits="+workerCycle.DropsWithStockIncrease+
                                " cycleFault="+(workerCycle.Fault??"none")+" treeFault="+(selected.Fault??"none"));
                            return expectedEnabled && delivered;
                        });
                        if(stop)break;
                    }
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        selected.Active=false;workerCycle.Active=false;ValidateAppleStepAccess(selected.Step);
                        if(expectedEnabled)Require(selected.Fault==null && workerCycle.Fault==null && appleNativeTrace.Fault==null && delivered && workerCycle.RemotePicking==0 &&
                            selected.FirstNormalized && selected.OriginalJobChecked && selected.ActualNative1>0,
                            "Apple tree approach did not preserve the original tree contact and complete a real harvest/physical delivery cycle");
                        log("automatic-apple-tree-result enabled="+expectedEnabled+" state="+state+" originalTree="+selected.TreeId+"/"+selected.TreeGlobal+
                            " originalTreeContact="+selected.OriginalTreeContact+" naturalHarvestDelivery="+delivered+
                            " nativePickups="+workerCycle.PickupEvents+" physicalDeposits="+workerCycle.DropsWithStockIncrease+
                            " remotePicking="+workerCycle.RemotePicking+" nextOnly=true firstNormalized="+selected.FirstNormalized+
                            " originalVegetationJobChecked="+selected.OriginalJobChecked+" actualOwnNative1="+selected.ActualNative1+
                            " destroyedReusedTreeNegativeCovered=false");
                        EndAutomaticWorkerCase(mutation,delivered);return true;
                    });
                }
            }
            finally
            {
                StopAppleNativeTrace();
                if(selected!=null)selected.Active=false;
                if(workerCycle!=null)workerCycle.Active=false;
                if(ReferenceEquals(appleTreeFixture,this))appleTreeFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
    }
}
