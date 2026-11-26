using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // Genuine ordinary196280 refusals, not a managed veto.
        // Reuses the existing real Brewer production and native save helpers.
        // The controlled dedicated yard prefab has PlacementBypass=true;
        // no human stockpile-adjacency acceptance is claimed. All tested wall,
        // moat, reopening and rescue guards remain unchanged and authoritative.
        private sealed class Native0WorkerSaveProof
        {
            internal Fixture Fixture;internal Actor Actor;internal UnitSnapshot Before;
            internal string Path,Fault;internal bool Active,LoadArmed,OriginalEnded,Barrier,GateOpened,FirstBatch;
            internal int MinX,MinY,MaxX,MaxY,GateX,GateY,BankX,BankY,Cargo,SaveTick,SourceTick;
            internal int Loaded,LoadedReads,FirstLoadedTick,Native0BeforeSave;
            internal int OriginalMoatNative0,OriginalBankNative0,LoadedBankNative0,ActualNative1;
            internal long OriginalSession,LoadedSession,OriginalWorld,LoadedWorld;
            internal byte[] SavedOwned;
            internal readonly List<RescueTile> Ring=new List<RescueTile>();
            internal readonly Dictionary<string,int> RejectCounts=new Dictionary<string,int>();
            internal int RejectWitnesses;
        }
        private static Native0WorkerSaveProof native0WorkerSave;
        private static bool native0WorkerSaveRegistered;
        private IEnumerable<AutomationStep> Native0WorkerPersistenceSteps()
        {yield return new AutomationStep("genuine-native0-worker-park-save-load-reopen-delivery",
            TimeSpan.FromSeconds(1200),Native0WorkerPersistence,false);}

        private void ArmNative0WorkerSave(Native0WorkerSaveProof proof)
        {
            if(!native0WorkerSaveRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var lifecycle,out var available),available.Reason);
                Require(lifecycle.TryRegisterObserver("acceptance-native0-worker-save",Native0WorkerStarted,
                    Native0WorkerEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-native0-worker-save-tick",
                    ObserveNative0WorkerSaveTick,out string reason,order:-1000),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-native0-worker-save-results",new Native0WorkerSaveMoveObserver(),
                    out string moveReason,enabled:()=>native0WorkerSave!=null && native0WorkerSave.Active &&
                        !native0WorkerSave.Fixture.stopped && native0WorkerSave.Actor!=null && native0WorkerSave.Before!=null),moveReason);
                native0WorkerSaveRegistered=true;
            }
            Require(native0WorkerSave==null,"Another genuine Native0 worker-save case is active");native0WorkerSave=proof;
        }
        private sealed class Native0WorkerSaveMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var p=native0WorkerSave;var f=p?.Fixture;
                if(p==null || f==null || !p.Active || f.stopped || p.Before==null || context==null ||
                    context.OriginOwnerGuid!=Plugin.Guid || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==f.session &&
                        context.SessionId==sid && context.WorldGeneration==world &&
                        context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY && context.Extra==0,
                        "Native0 receipt observer crossed world or original effective goal");
                    f.CheckActor(p.Actor,true);var u=f.Resolve(p.Actor);IntPtr pointer=(IntPtr)u;
                    Require(u->r_PathPlanStateBitFlags==5 && NativeRelocation.IsNormalizedPose(u,(int)u->r_CurrentPositionTileId,
                        f.runtime.Native.JobDword(u,0x740)) && p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                        o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                        "Native0 receipt observer lacks coherent owned original typed continuation");
                    bool moat=(f.runtime.Native.Flags((int)u->r_CurrentPositionTileId)&(uint)TilePropertyFlag.IsMoat)!=0;
                    Require(moat || f.runtime.Native.OrdinaryGround((int)u->r_CurrentPositionTileId),
                        "Native0 receipt source is neither its actual moat nor an ordinary bank");
                    return new Native0WorkerSaveMoveCompletion(p,context,moat,sid==p.OriginalSession);
                }
                catch(Exception error){p.Fault=error.ToString();return null;}
            }
        }
        private sealed class Native0WorkerSaveMoveCompletion : IUnitMoveInvocation
        {
            private readonly Native0WorkerSaveProof p;private readonly UnitMoveContext context;
            private readonly bool moat,original;private int completed;
            internal Native0WorkerSaveMoveCompletion(Native0WorkerSaveProof p,UnitMoveContext context,bool moat,bool original)
            {this.p=p;this.context=context;this.moat=moat;this.original=original;}
            public void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || p.Fixture.stopped)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,context) && result.IsCurrentWorld &&
                        result.NativeInvoked && result.NativeReturned && result.Failure==null,
                        "Native0 park-save requires an actual current returned native original, never veto/exception");
                    if(result.NativeResult==1){p.ActualNative1++;return;}
                    Require(result.NativeResult==0,"Unknown native original result in worker park-save");
                    if(original){if(moat)p.OriginalMoatNative0++;else p.OriginalBankNative0++;}
                    else{Require(!moat,"Loaded worker retry left its saved free bank");p.LoadedBankNative0++;}
                }
                catch(Exception error){p.Fault=error.ToString();}
            }
        }
        private static void Native0WorkerEnded(MissionLifecycleNotification notification)
        {
            var p=native0WorkerSave;
            if(p!=null && p.Active && notification.Context.SessionId==p.OriginalSession)
            {p.OriginalEnded=true;p.Fixture.brewerWorld.Active=false;}
        }
        private static void Native0WorkerStarted(MissionLifecycleNotification notification)
        {
            var p=native0WorkerSave;var context=notification.Context;
            if(p==null || !p.Active || !p.LoadArmed || notification.IsReplay || !context.IsSave ||
                context.SessionId==p.OriginalSession || string.IsNullOrEmpty(context.FilePath))return;
            try
            {
                if(!string.Equals(System.IO.Path.GetFullPath(context.FilePath),p.Path,StringComparison.OrdinalIgnoreCase))return;
                Require(p.OriginalEnded,"Genuine Native0 worker load did not end its original mission");
                var f=p.Fixture;p.LoadArmed=false;p.LoadedSession=context.SessionId;f.session=context.SessionId;f.stopped=false;
                f.brewerWorld.Active=true;f.BeginAutomaticWorkerMoves(p.Actor,p.Before.RequestedX,p.Before.RequestedY);
                Plugin.SetGeneralFixtureSession(true);Volatile.Write(ref p.Loaded,1);
            }
            catch(Exception error){p.Fault=error.ToString();Volatile.Write(ref p.Loaded,1);}
        }
        private static void ObserveNative0WorkerSaveTick(int tick)
        {
            var p=native0WorkerSave;
            if(p==null || !p.Active || p.Fixture.stopped || Volatile.Read(ref p.Loaded)==0 || p.Fault!=null)return;
            try{p.Fixture.ObserveNative0WorkerSaveTickCore(p,tick);}
            catch(Exception error){p.Fault=error.ToString();}
        }
        private unsafe void ObserveNative0WorkerSaveTickCore(Native0WorkerSaveProof p,int tick)
        {
            if(!p.Barrier)
            {
                CheckNative0WorkerPark(p);
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sessionId,out long world) && sessionId==p.LoadedSession,
                    "Loaded native0 worker park is not in its genuine new world");p.LoadedWorld=world;
                if(p.LoadedReads==0)
                {
                    Require(runtime.Workers.Ledger.TrySerialize(out var loaded) && ParkedSameBytes(loaded,p.SavedOwned),
                        "Actual worker receipt changed before first loaded recovery tick");
                    p.FirstLoadedTick=tick;p.LoadedReads=1;
                    log("native0-worker-save-first-loaded-tick tick="+tick+" session="+sessionId+" world="+world+
                        " originalWorkerJobCargoStoreGoal=true serializedOwnedIdentical=true flags5=true");
                }
                else
                {
                    Require(unchecked((uint)(tick-p.FirstLoadedTick))==1,"Missed consecutive loaded native0 worker tick");
                    Require(p.LoadedBankNative0>0 && p.ActualNative1==0 && automaticWorkerMoves.OwnSuccesses==0,
                        "Closed saved ring did not produce an actual loaded native0 retry");
                    p.LoadedReads=2;p.Barrier=true;
                    log("native0-worker-save-loaded-barrier tick="+tick+" actualLoadedNative0="+
                        automaticWorkerMoves.OwnRefusals+" flags5=true goodsWrites=0 noManagedVeto=true");
                }
            }
            if(p.GateOpened && !p.FirstBatch && brewerWorld.Batches==1 && brewerWorld.Deposits==p.Cargo &&
                brewerWorld.Remaining==0)
            {
                CheckActor(p.Actor,true);var unit=Resolve(p.Actor);
                Require(unit->r_CarryGoodAmount==0 && brewerWorld.OwnResume && brewerWorld.Inputs==0 &&
                    brewerWorld.Yields==0 && brewerWorld.Goods.Count==0,
                    "Saved worker's first complete physical batch lacks empty cargo/original resume");
                p.FirstBatch=true;
                log("native0-worker-first-batch tick="+tick+" originalAle="+p.Cargo+" physicalCredits="+brewerWorld.Deposits+
                    " cargo=0 firstBatchLatch=true noDuplicateCredits=true");
            }
        }
        private unsafe void SetupNative0BrewerWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Native0 world requires its real human owner");
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);bool found=false;
            // Deterministic real geometry search; these radii are candidates, not
            // permission to exceed the current native/public placement range.
            for(int radius=48;radius<=72 && !found;radius+=8)
                for(int d=0;d<8 && !found;d++)
                {
                    int x=keep.X+Dx[d]*radius,y=keep.Y+Dy[d]*radius;
                    if(!FlatSquare(x,y,12,out int height) || height!=8 ||
                        !MoatWorkerWithinOwnKeepRange(runtime.Native.Tile(x,y)))continue;
                    cx=x;cy=y;elevation=height;found=true;
                }
            Require(found,"No real far Brewery site satisfies current Keep range and flat world geometry");
            native0YardLayout=PlanNative0YardLayout();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            brewerWorld.Brewery=CreateBakerPrefab(cx,cy,eMappers.MAPPER_BREWER,eStructs.STRUCT_BREWERS_WORKSHOP);
            Require(BuildingAccess.TryGetById(brewerWorld.Brewery,out GameBuilding* brewery,out _) && brewery!=null,
                "Native0 Brewery has no exact native lifetime");brewerWorld.BreweryGlobal=brewery->r_GlobalId;
            millerWorld=new MillerWorld{Active=false};
            ExtendMillerStorage(native0YardLayout.X,native0YardLayout.Y);RequestBrewerCapacity();
            log("native0-worker-world brewery="+brewerWorld.Brewery+"/"+brewerWorld.BreweryGlobal+
                " site="+cx+","+cy+" keep="+keep.X+","+keep.Y+" controlledYardPlacementBypass=true humanAdjacencyClaim=false");
        }
        private string Native0TileFacts(int tile)
        {
            if(tile<0 || tile>=NativeRelocation.TileCapacity)return "tile="+tile+" invalid=true";
            var tiles=GameTileManagerAPI.Instance;
            return "tile="+tile+" flags="+runtime.Native.Flags(tile).ToString("X8")+" ordinary="+runtime.Native.OrdinaryGround(tile)+
                " building="+tiles.GetTileBuildingId(tile)+" vegetation="+tiles.GetTileVegetationId(tile)+" head="+runtime.Native.Head(tile)+
                " height="+tiles.GetTileHeight(tile)+" defaultHeight="+tiles.GetTileDefaultHeight(tile)+
                " keepRange="+MoatWorkerWithinOwnKeepRange(tile)+" moatEligible="+MoatWorkerSourceEligible(tile);
        }
        private bool Native0Reject(Native0WorkerSaveProof p,string reason,UnitSnapshot before,string detail)
        {
            p.RejectCounts.TryGetValue(reason,out int count);p.RejectCounts[reason]=count+1;
            if(count==0 && p.RejectWitnesses++<32)
            {
                log("native0-worker-source-reject reason="+reason+" detail="+(detail??"none")+
                    " unit="+(before==null?"none":before.Id+"/"+before.GlobalId)+" ai="+before?.AIState+" ss="+before?.Substep+
                    " flags="+before?.Flags+" ordinaryStep="+before?.OrdinaryStep+" job="+(before?.Job!=null)+
                    " unsupported="+(before?.Unsupported??"none")+" current="+before?.X+","+before?.Y+
                    " next="+before?.NextX+","+before?.NextY+" goal="+before?.RequestedX+","+before?.RequestedY);
                if(before!=null)
                {
                    int x=Math.Max(before.X,before.NextX),y=Math.Max(before.Y,before.NextY);
                    for(int yy=y-1;yy<=y;yy++)for(int xx=x-1;xx<=x;xx++)
                        log("native0-worker-source-cell reason="+reason+" xy="+xx+","+yy+" "+Native0TileFacts(runtime.Native.Tile(xx,yy)));
                }
            }
            return false;
        }
        private void LogNative0RejectSummary(Native0WorkerSaveProof p,int sample)
        {
            var values=new List<string>();foreach(var item in p.RejectCounts)values.Add(item.Key+":"+item.Value);
            log("native0-worker-reject-summary sample="+sample+" counts="+string.Join(";",values));
        }
        private unsafe bool PrepareNative0Ring(Native0WorkerSaveProof p,Actor actor)
        {
            var before=brewerWorld.Before;var unit=Resolve(actor);int selected=runtime.Native.Word(unit,0x98E);
            bool ownDedicated=false;foreach(var identity in millerWorld.AddedYards)
                if(identity.Id==selected && identity.Global==runtime.Native.JobDword(unit,0x6F8))ownDedicated=true;
            if(!ownDedicated)return Native0Reject(p,"ring-selected-store-not-dedicated",before,"store="+selected+" global="+runtime.Native.JobDword(unit,0x6F8));
            if(before==null)return Native0Reject(p,"ring-missing-before",null,null);
            if(!MoatWorkerWithinOwnKeepRange(before.TileId) || !MoatWorkerWithinOwnKeepRange(before.NextTileId))
                return Native0Reject(p,"ring-current-or-next-keep-range",before,null);
            Require(native0YardLayout!=null && Native0Inside(before.RequestedX,before.RequestedY,
                native0YardLayout.InnerMinX,native0YardLayout.InnerMinY,native0YardLayout.InnerMaxX,native0YardLayout.InnerMaxY),
                "Actual native-selected store goal escaped the fixed pre-work yard interior");
            var tiles=GameTileManagerAPI.Instance;
            // Use the full pre-work qualified conservative ring, not an
            // equality between validator scale and actual record union.
            int minX=native0YardLayout.MinX,minY=native0YardLayout.MinY;
            int maxX=native0YardLayout.MaxX,maxY=native0YardLayout.MaxY;
            int Distance(int x,int y)=>Math.Max(Math.Max(minX-x,x-maxX),Math.Max(minY-y,y-maxY));
            if(Distance(before.X,before.Y)<=8 || Distance(before.NextX,before.NextY)<=8)
                return Native0Reject(p,"ring-source-too-close",before,"bounds="+minX+","+minY+".."+maxX+","+maxY+
                    " currentDistance="+Distance(before.X,before.Y)+" nextDistance="+Distance(before.NextX,before.NextY));
            ValidateNative0BuiltYardLayout(true,runtime.Native.Word(unit,0x992),runtime.Native.Word(unit,0x994));
            p.Ring.Clear();
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                if(x!=minX && x!=maxX && y!=minY && y!=maxY)continue;
                int tile=runtime.Native.Tile(x,y);if(tile<0 || !runtime.Native.OrdinaryGround(tile) ||
                    tiles.GetTileBuildingId(tile)!=0 || tiles.GetTileVegetationId(tile)!=0 || runtime.Native.Head(tile)!=0 ||
                    (runtime.Native.Flags(tile)&(uint)TilePropertyFlag.IsFarm)!=0 ||
                    tiles.GetTileHeight(tile)!=8 || tiles.GetTileDefaultHeight(tile)!=8 || !MoatWorkerWithinOwnKeepRange(tile))
                    return Native0Reject(p,"ring-tile-preflight",before,"xy="+x+","+y+" "+Native0TileFacts(tile));
                p.Ring.Add(new RescueTile(x,y,tile));
            }
            // Full fixed-ring AND interior Current/Next validation above subsumes the old boundary-only scan.
            p.MinX=minX;p.MinY=minY;p.MaxX=maxX;p.MaxY=maxY;p.GateX=minX;
            p.GateY=Math.Max(minY+1,Math.Min(maxY-1,before.RequestedY));
            return true;
        }
        private unsafe void BuildNative0RingAndMoat(Native0WorkerSaveProof p)
        {
            var actor=p.Actor;var before=brewerWorld.Before;var unit=Resolve(actor);IntPtr pointer=(IntPtr)unit;
            Require(MoatWorkerWithinOwnKeepRange(before.TileId) && MoatWorkerWithinOwnKeepRange(before.NextTileId),
                "Actual Native0 source moved outside the live Keep range");
            foreach(var point in p.Ring)
            {
                BulkAIDetours.c_game_ai_build_wall_hook_impl(GameTileManagerAPI.Instance.GetTileManager(),owner,point.X,point.Y,eMappers.MAPPER_WALL);
                int tile=runtime.Native.Tile(point.X,point.Y);
                Require((runtime.Native.Flags(tile)&(uint)TilePropertyFlag.IsWall)!=0 && runtime.Native.Head(tile)==0 &&
                    GameTileManagerAPI.Instance.GetTileBuildingId(tile)==0,"Real ring wall writer rejected/occupied a required cell");
            }
            Require(unit->r_CurrentPositionTileId==(uint)before.TileId && unit->r_NextPositionTileId2==(uint)before.NextTileId &&
                unit->r_MovementSubstep==before.Substep && unit->r_TargetTilePositionX2==before.RequestedX &&
                unit->r_TargetTilePositionY2==before.RequestedY && before.Job.MatchesContinuation(unit->r_UnitChimp,unit->r_AIState,
                    o=>runtime.Native.JobWord((GameUnit*)pointer,o),o=>runtime.Native.JobDword((GameUnit*)pointer,o)),
                "Ring construction changed the actual original travel/job before causal moat source");
            PrepareBrewerMutation(actor);p.Before=brewerWorld.Before;p.Cargo=brewerWorld.InitialCargo;
            Require(SimulationTickEvents.TryGetCurrentTick(out p.SourceTick),"Native0 source has no native tick");
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out p.OriginalWorld) && sid==session,"Native0 source world unavailable");
            var tileApi=GameTileManagerAPI.Instance;var buildingApi=GameBuildingManagerAPI.Instance;
            int mapSize=tileApi.GetCurrentMapSize(),baseRange=buildingApi.GetKeepProximityRange(mapSize);
            int overrideRange=buildingApi.KeepProximityOverride.GetValue();
            int effectiveRange=overrideRange>0?overrideRange:baseRange;
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            log("native0-worker-real-ring-source cells="+p.Ring.Count+" selectedStore="+brewerWorld.DeliveryStore+"/"+
                brewerWorld.DeliveryGlobal+" originalGoal="+p.Before.RequestedX+","+p.Before.RequestedY+
                " source="+p.Before.X+","+p.Before.Y+" next="+p.Before.NextX+","+p.Before.NextY+
                " mapSize="+mapSize+" baseKeepRange="+baseRange+" actualRangeOverride="+overrideRange+
                " sufficientSafeKeepRange="+(effectiveRange+1)+" keep="+keep.X+","+keep.Y+
                " currentKeepRangeQualified=true sourceOutsideRingBfs=true actualNative0Awaited=true artificialResults=0");
        }
        private unsafe void CheckNative0WorkerPark(Native0WorkerSaveProof p)
        {
            Require(p.Fault==null && brewerWorld.Fault==null,p.Fault??brewerWorld.Fault);
            CheckActor(p.Actor,true);ValidateBrewer(p.Actor);var unit=Resolve(p.Actor);var ledger=runtime.Workers.Ledger;
            Require(ledger.ReservedCount==0,"Native0 worker receipt still transiently reserved");
            Require(ledger.TryGetParkedByUnit(p.Actor.Id,out var ticket) && ticket.Kind==ResumeReceiptKind.Worker &&
                ticket.UnitGlobalId==p.Actor.Global && ticket.Job!=null && ticket.RequestedX==p.Before.RequestedX &&
                ticket.RequestedY==p.Before.RequestedY,"Native0 worker has no committed original worker receipt");
            Require(new NativeWorkerResumeWorld(runtime.Native).Inspect(ticket,true)==WorkerReceiptState.MatchingParked &&
                unit->r_CarryGoodAmount==p.Cargo && unit->r_AIState==8 && runtime.Native.Word(unit,0x98E)==brewerWorld.DeliveryStore &&
                runtime.Native.JobDword(unit,0x6F8)==brewerWorld.DeliveryGlobal &&
                unit->r_TargetTilePositionX2==p.Before.RequestedX && unit->r_TargetTilePositionY2==p.Before.RequestedY &&
                unit->r_PathPlanStateBitFlags==5 && brewerWorld.Deposits==0 && brewerWorld.Batches==0 &&
                brewerWorld.Inputs==0 && brewerWorld.Yields==0 && brewerWorld.Goods.Count==0,
                "Owned native0 park changed job/cargo/storage/goal or advanced production/delivery");
            if(p.BankX==0 && p.BankY==0)
            {p.BankX=unit->r_CurrentTilePositionX;p.BankY=unit->r_CurrentTilePositionY;}
            Require(unit->r_CurrentTilePositionX==p.BankX && unit->r_CurrentTilePositionY==p.BankY &&
                runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId) && HasStructuralExit(p.Actor),
                "Owned native0 receipt lost its real free local bank");
        }
        private static void RequireNative0DeleteMirrorOff()
        {
            var assembly=typeof(GameTileManagerAPI).Assembly;
            var type=assembly.GetType("SHCDESE.DebugMenu.DebugMenuManager",true);
            var instance=type.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static);
            Require(instance!=null && instance.PropertyType==type,"Native0 delete mirror manager contract changed");
            var field=type.GetField("_mirrorTool",BindingFlags.Instance|BindingFlags.NonPublic);
            Require(field!=null && field.FieldType.FullName=="SHCDESE.ImMenu.MapEditor.MirrorTool","Native0 mirror field contract changed");
            var mirror=field.GetValue(instance.GetValue(null));
            var enabled=field.FieldType.GetField("DeleteMirrorEnabled",BindingFlags.Public|BindingFlags.Instance);
            Require(mirror!=null && enabled!=null && enabled.FieldType==typeof(bool) && !(bool)enabled.GetValue(mirror),
                "Wall reopening cannot use an enabled/unknown delete mirror");
        }
        private unsafe void ReopenNative0Gate(Native0WorkerSaveProof p)
        {
            CheckNative0WorkerPark(p);Require(p.Barrier,"Loaded native0 barrier not yet verified");RequireNative0DeleteMirrorOff();
            var tiles=GameTileManagerAPI.Instance;int tile=runtime.Native.Tile(p.GateX,p.GateY);uint flags=runtime.Native.Flags(tile);
            Require((flags&(uint)TilePropertyFlag.IsWall)!=0 && (flags&0xA00u)==0 && runtime.Native.Head(tile)==0 &&
                tiles.GetTileBuildingId(tile)==0 && tiles.GetTileVegetationId(tile)==0 &&
                (tiles.GetWallOwnerLayer()[tile]&7)+1==owner && tiles.GetTileDefaultHeight(tile)==8,
                "Real gate is not an empty own ordinary wall satisfying native removal preconditions");
            BulkBuildingDetours.c_game_bulldoze_wall_hook_impl(tiles.GetTileManager(),owner,tile);
            Require(runtime.Native.OrdinaryGround(tile) && tiles.GetTileHeight(tile)==8 && runtime.Native.Head(tile)==0 &&
                tiles.GetTileBuildingId(tile)==0,"Actual wall-removal wrapper did not reopen the exact free gate");
            CheckNative0WorkerPark(p);p.GateOpened=true;
            log("native0-worker-gate-opened actualWallRemoval=true gate="+p.GateX+","+p.GateY+
                " noManagedVeto=true originalWorkerJobCargoStoreGoal=true");
        }
        private IEnumerable<AutomationWait> Native0WorkerPersistence(AutomationRun run)
        {
            Require(expectedEnabled,"Genuine Native0 worker receipt acceptance requires enabled rescue");
            var p=new Native0WorkerSaveProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run),Active=true};
            brewerWorld=new BrewerWorld();var brewer=brewerWorld;ArmNative0WorkerSave(p);
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(args=>{
                if(!stopped && brewer.Active)ObserveBrewerGoods(args);
            }))
            using(var production=SHCDESE.EventAPI.UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(args=>{
                if(!stopped && brewer.Active)ObserveBrewerYield(args);
            }))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupNative0BrewerWorld();});
                while(brewer.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                }
                Execute(run,()=>{
                    ValidateNative0BuiltYardLayout(true);
                    Require(brewer.Fault==null && brewer.Hops.FreeAmount>=12 && brewer.Ale.FreeAmount>=1,
                        "Native0 controlled yards lack actual initialized Hops/Ale capacity");
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_RAW_HOPS,12),
                        "Native0 world could not physically admit its real Hops input");
                });
                Director.instance.SetEngineFrameRate(50);
                for(int sample=0;sample<6000 && p.Actor==null;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        Require(brewer.Fault==null,brewer.Fault);var actor=FindNaturalBrewer(out string finderReason,out var rejectedBefore);
                        if(actor!=null && PrepareNative0Ring(p,actor))p.Actor=actor;
                        else
                        {
                            if(actor==null)Native0Reject(p,"finder:"+finderReason,rejectedBefore,null);
                            brewer.Actor=null;
                        }
                        if(sample%128==0 || sample==5999){LogBrewerAcquisition(sample);LogNative0RejectSummary(p,sample);}return true;
                    });
                }
                Require(p.Actor!=null,"No genuine loaded Brewer route satisfies unchanged store/ring/source/Keep constraints");
                yield return AutomationWait.Until("quiescent director before genuine native0 source",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();Director.instance.SetEngineFrameRate(10);
                run.InSimulation(run.CurrentMission,()=>{BuildNative0RingAndMoat(p);return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                yield return AutomationWait.Until("quiescent director before genuine native0 worker save",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>{
                    CheckNative0WorkerPark(p);var moves=automaticWorkerMoves;
                    Require(moves.Fault==null && p.Fault==null && p.ActualNative1==0 && moves.OwnSuccesses==0 &&
                        moves.OwnRefusals>=2 && moves.OwnRefusals<=4 && p.OriginalMoatNative0>0 && p.OriginalBankNative0>0,
                        "Real original-goal probe AND post-relocation resume did not both return actual Native0 before bounded save");
                    Require(SimulationTickEvents.TryGetCurrentTick(out p.SaveTick) && unchecked((uint)(p.SaveTick-p.SourceTick))<=4,
                        "Too many native ticks elapsed before owned Native0 park capture");
                    p.Native0BeforeSave=moves.OwnRefusals;
                    Require(runtime.Workers.Ledger.TrySerialize(out p.SavedOwned),"Actual committed worker receipt cannot serialize");
                    log("native0-worker-before-save actualNative0="+p.Native0BeforeSave+" actualNative1=0 flags5=true workerReceipt=true"+
                        " reserved=0 originalCargo="+p.Cargo+" nativeTick="+p.SaveTick+" noManagedVeto=true");return true;
                });
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int centreX=-1,centreY=-1;
                GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                Require(EngineInterface.SaveSaveGame(p.Path,screen.x,screen.y,centreX,centreY,false,false,false),"Actual Native0 worker save refused");
                Require(File.Exists(p.Path) && new FileInfo(p.Path).Length>0,"Native0 worker save was not materialized");
                run.InSimulation(run.CurrentMission,()=>{
                    CheckNative0WorkerPark(p);Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==p.SaveTick &&
                        runtime.Workers.Ledger.TrySerialize(out var unchanged) && ParkedSameBytes(unchanged,p.SavedOwned),
                        "Actual paused save changed native tick/owned worker record");return true;
                });
                foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                p.LoadArmed=true;foreach(var wait in run.LoadSave(p.Path))yield return wait;
                Require(Volatile.Read(ref p.Loaded)!=0 && p.Fault==null && run.CurrentMission?.SessionId==p.LoadedSession,
                    "Native0 worker save did not complete its actual new lifecycle: "+p.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("native0 worker loaded briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<16 && !p.Barrier;sample++)
                {yield return run.WaitForTicks(run.CurrentMission,1);Require(p.Fault==null,p.Fault);}
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{ReopenNative0Gate(p);return true;});
                Director.instance.SetEngineFrameRate(50);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<1600 && !p.FirstBatch;sample++)
                {yield return run.WaitForTicks(run.CurrentMission,4);Require(p.Fault==null && brewer.Fault==null,p.Fault??brewer.Fault);}
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    CheckActor(p.Actor,true);Require(p.FirstBatch && p.Fault==null && brewer.Fault==null && brewer.Deposits==p.Cargo &&
                        !runtime.Workers.Ledger.TryGetOwnedByUnit(p.Actor.Id,out _) && automaticWorkerMoves.OwnSuccesses==1 &&
                        p.ActualNative1==1 && p.OriginalMoatNative0>0 && p.OriginalBankNative0>0 && p.LoadedBankNative0>0,
                        "Loaded Native0 worker did not resume exactly once and physically deliver its original complete real batch");
                    log("native0-worker-save-result actualOriginalNative0="+p.Native0BeforeSave+
                        " loadedActualNative0="+automaticWorkerMoves.OwnRefusals+" releaseActualNative1="+automaticWorkerMoves.OwnSuccesses+
                        " originalCargo="+p.Cargo+" physicalAleCredits="+brewer.Deposits+
                        " firstLoadedBarrier=true oldSession="+p.OriginalSession+" newSession="+p.LoadedSession+
                        " oldWorld="+p.OriginalWorld+" newWorld="+p.LoadedWorld+
                        " workerJobSave=true actualNative0Save=true noVeto=true noDuplicateCredits=true");
                    Plugin.SetGeneralFixtureSession(false);return true;
                });
            }
            finally
            {
                p.Active=false;p.LoadArmed=false;brewer.Active=false;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(native0WorkerSave,p))native0WorkerSave=null;
                if(ReferenceEquals(brewerCapacityFixture,this))brewerCapacityFixture=null;
                if(ReferenceEquals(brewerMoveFixture,this))brewerMoveFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
    }
}
