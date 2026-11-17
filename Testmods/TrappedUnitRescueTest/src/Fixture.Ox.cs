using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private OxObservation oxObservation;
        private int oxQuarry,oxTether,oxQuarryPile;
        private uint oxQuarryGlobal,oxTetherGlobal,oxQuarryPileGlobal;
        private IDisposable oxStorageObserver, oxDropObserver;
        private sealed class OxObservation
        {
            internal Actor Actor;
            internal int Tether, InitialCargo, AcceptedQuantity, DropEvents, PendingStorage, PendingAmount;
            internal uint TetherGlobal, PendingGlobal, PendingStone;
            internal bool Active, Pending, PhysicallyNearStorage, CargoEmpty, Lost;
            internal string Fault;
        }

        // Natural quarry -> grunt -> tether -> loaded Ox. Only its occupied source geometry is synthetic.
        private IEnumerable<AutomationWait> Ox(AutomationRun run)
        {
            int quarry = 0, tether = 0;
            Execute(run, () => {
                EnsureOxPrerequisites();
                Require(TryCreateNaturalQuarry(out quarry, out tether),
                    "No real stone site accepted a complete native quarry plus tether; loaded-Ox coverage unavailable on this map");
                EnsureOxDeliveryYard(tether);
                CaptureOxSites(quarry,tether);
                LogOxAcquisition(-1);
                log("ox-setup quarry=" + quarry + " tether=" + tether +
                    " naturalStone=true manualCargo=false manualAIState=false syntheticPart=occupied-source-wall-only");
            });
            Actor ox = null;bool batchReady=false;string lastStage=null;
            // The quarry can be far from the keep. Let its real employees arrive and
            // produce first; elapsed ticks alone never qualify a loaded test actor.
            for(int sample=0;sample<512 && ox==null && !batchReady;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    ValidateOxSites();ox=FindLoadedOx(tether);
                    string stage=OxProductionReadiness(out batchReady);
                    if(stage!=lastStage || sample==0 || sample==31 || sample==127 || sample==255 || sample==511)
                    {lastStage=stage;log("ox-natural-stage phase=bulk sample="+sample+" "+stage);LogOxAcquisition(sample);}
                });
            }
            if(ox==null)
            {
                // Slow fine sampling so every actual step boundary can be observed.
                // This only changes test pacing; no worker/job/cargo state is injected.
                Director.instance.SetEngineFrameRate(50);
                try
                {
                    for(int sample=0;sample<4096 && ox==null;sample++)
                    {
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,1);
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        Execute(run,()=>{
                            ValidateOxSites();ox=FindLoadedOx(tether);
                            string stage=OxProductionReadiness(out batchReady);
                            if(stage!=lastStage || sample==0 || sample%512==0 || sample==4095 || ox!=null)
                            {lastStage=stage;log("ox-natural-stage phase=fine sample="+sample+" "+stage);LogOxAcquisition(512+sample);}
                        });
                    }
                }
                finally{Director.instance.SetEngineFrameRate(250);}
            }
            Require(ox != null, "No naturally loaded Ox acquired an ordinary outbound route; no cargo/AI state was fabricated");
            Execute(run, () => {
                actors.Add(ox);
                VerifyLiveAndRemember();
                int cargo = OxCargo(ox);
                Require(cargo > 0, "Natural Ox cargo disappeared before mutation");
                BeginOxObservation(ox, tether, cargo);
                var scope = runtime.BeginMutation("synthetic-wall-under-naturally-loaded-ox", ox.X, ox.Y, 1);
                try
                {
                    PaintSyntheticTrap(ox.X, ox.Y);
                    Require(!HasStructuralExit(ox), "Loaded-Ox source was not actually trapped");
                    RescueResult result = scope.Complete(true).Single(r => r.UnitId == ox.Id);
                    if (expectedEnabled)
                        Require(result.Status == RescueStatus.Rescued && result.IdentityPreserved && result.ResumeAttempted && result.ResumeSucceeded,
                            "Loaded Ox relocation/active-route restoration failed");
                    else Require(AtSource(ox), "Disabled rescue moved the loaded Ox");
                    CheckActor(ox, true);
                    Require(OxCargo(ox) == cargo, "Rescue changed existing Ox cargo");
                    log("ox-rescue " + result + " initialCargo=" + cargo + " naturalLoadedTrip=true");
                }
                finally { scope.Dispose(); }
            });
            bool delivered = false;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int sample = 0; sample < (expectedEnabled ? 2000 : 24); sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission, expectedEnabled ? 4 : 2);
                delivered = run.InSimulation(run.CurrentMission, ObserveOxOutcome);
                if (delivered || oxObservation.Lost) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                OxObservation observation = oxObservation;
                observation.Active = false;
                Require(observation.Fault == null, observation.Fault);
                if (expectedEnabled)
                {
                    Require(!observation.Lost && delivered && observation.AcceptedQuantity == observation.InitialCargo && observation.CargoEmpty,
                        "Loaded Ox did not deliver exactly its preserved batch into real storage after rescue");
                    CheckActor(ox, true);
                }
                else Require(!delivered && observation.AcceptedQuantity == 0,
                    "Baseline loaded Ox delivered; the controlled trap did not demonstrate this failure class");
                log("ox-result enabled=" + expectedEnabled + " naturalLoadedTrip=true initialCargo=" + observation.InitialCargo +
                    " acceptedStone=" + observation.AcceptedQuantity + " dropEvents=" + observation.DropEvents +
                    " physicallyNearStorage=" + observation.PhysicallyNearStorage + " cargoEmpty=" + observation.CargoEmpty +
                    " originalLost=" + observation.Lost + " sameIdentity=" + !observation.Lost +
                    " fullPreservedBatchDelivered=" + delivered + " manualCargo=false manualAIState=false");
            });
        }

        private unsafe void EnsureOxPrerequisites()
        {
            var players=GamePlayerManagerAPI.Instance;
            Require(!players.IsAIPlayer(owner),"Natural Ox fixture requires the actual human owner");
            int stores=CountOwnedStockpiles();
            Require(players.TryGetPlayerResourcesById(owner,out GamePlayerResources* resources) && resources!=null,"Ox fixture player resources unavailable");
            bool needsHousing=resources->r_CivilianHousingSpace<=resources->r_TotalCivilians;
            log("ox-prerequisites stockpiles="+stores+" controlledEmptyDestinationPlanned=true housing="+resources->r_CivilianHousingSpace+" civilians="+resources->r_TotalCivilians+
                " readyPeasants="+resources->r_ReadyPeasants+" existingPeasants="+resources->r_ExistingPeasants+" popularity="+resources->r_CurrentPopularity);
            if(needsHousing)
            {
                FindSite();
                if(needsHousing)
                    GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx-10,cy,eMappers.MAPPER_HOVEL,BuildingScales.GetScale(eMappers.MAPPER_HOVEL),0,true,true);
            }
        }
        private unsafe void EnsureOxDeliveryYard(int tether)
        {
            Require(BuildingAccess.TryGetById(tether,out GameBuilding* t,out _) && t!=null,"Actual tether unavailable while placing its delivery yard");
            int scale=BuildingScales.GetScale(eMappers.MAPPER_STORES);
            Require(TryNearbyOrdinarySite(t->r_TilePositionXBegin,t->r_TilePositionYBegin,scale,out int x,out int y),
                "No clear ordinary native stockpile footprint near the tether; stone capacity prerequisite unavailable");
            int yardId=0;
            using(var observer=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase==EventHookPhase.Post && args.PlayerId==owner && args.Building==eStructs.STRUCT_GOODS_YARD && args.ReturnValue>0)
                    yardId=checked((int)args.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,x,y,eMappers.MAPPER_STORES,scale,0,true,true);
            GameBuilding* yard=null;
            Require(yardId>0 && BuildingAccess.TryGetById(yardId,out yard,out _) && yard!=null && yard->r_PlayerIdOwner==owner &&
                yard->r_BuildingType==eStructs.STRUCT_GOODS_YARD && yard->r_GlobalId!=0 && yard->r_CurrentGoodStackAmount==0,
                "Actual new empty goods yard was not materialized for native8stone handoff");
            log("ox-delivery-yard id="+yardId+"/"+yard->r_GlobalId+" anchor="+x+","+y+" tether="+tether+
                " actualStack="+yard->r_CurrentGoodStackAmount+" life="+yard->r_AliveState+" setup=controlled-real-empty-destination noGoodsWrites=true");
        }
        private unsafe int CountOwnedStockpiles()
        {
            int count=0,capacity=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
            for(int id=1;id<=capacity;id++)
                if(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    (b->r_AliveState==AliveState.IsAlive || b->r_AliveState==AliveState.NeedsInit))count++;
            return count;
        }
        private unsafe void CaptureOxSites(int quarry,int tether)
        {
            Require(BuildingAccess.TryGetById(quarry,out GameBuilding* q,out _) && q!=null && q->r_PlayerIdOwner==owner,"Native quarry identity unavailable");
            Require(BuildingAccess.TryGetById(tether,out GameBuilding* t,out _) && t!=null && t->r_PlayerIdOwner==owner,"Native tether identity unavailable");
            oxQuarry=quarry;oxTether=tether;oxQuarryGlobal=q->r_GlobalId;oxTetherGlobal=t->r_GlobalId;
            oxQuarryPile=q->r_StoneQuarry_StockPileBuildingId;
            Require(BuildingAccess.TryGetById(oxQuarryPile,out GameBuilding* pile,out _) && pile!=null && pile->r_PlayerIdOwner==owner &&
                pile->r_BuildingType==eStructs.STRUCT_QUARRYPILE,"Native quarry did not create its actual loading pile");
            oxQuarryPileGlobal=pile->r_GlobalId;
        }
        private unsafe void ValidateOxSites()
        {
            Require(BuildingAccess.TryGetById(oxQuarry,out GameBuilding* q,out _) && q!=null && q->r_GlobalId==oxQuarryGlobal &&
                q->r_PlayerIdOwner==owner && q->r_BuildingType==eStructs.STRUCT_QUARRY && q->r_AliveState==AliveState.IsAlive,
                "Natural quarry disappeared or its slot was reused during Ox acquisition");
            Require(BuildingAccess.TryGetById(oxTether,out GameBuilding* t,out _) && t!=null && t->r_GlobalId==oxTetherGlobal &&
                t->r_PlayerIdOwner==owner && t->r_BuildingType==eStructs.STRUCT_OXEN_BASE && t->r_AliveState==AliveState.IsAlive,
                "Natural tether disappeared or its slot was reused during Ox acquisition");
            Require(BuildingAccess.TryGetById(oxQuarryPile,out GameBuilding* pile,out _) && pile!=null && pile->r_GlobalId==oxQuarryPileGlobal &&
                pile->r_PlayerIdOwner==owner && pile->r_BuildingType==eStructs.STRUCT_QUARRYPILE && pile->r_AliveState==AliveState.IsAlive,
                "Natural quarry loading pile disappeared or its slot was reused during Ox acquisition");
        }
        private unsafe string OxProductionReadiness(out bool batchReady)
        {
            GameBuilding* quarry=null;GameBuilding* tether=null;GameBuilding* pile=null;
            Require(BuildingAccess.TryGetById(oxQuarry,out quarry,out _) && quarry!=null &&
                BuildingAccess.TryGetById(oxTether,out tether,out _) && tether!=null &&
                BuildingAccess.TryGetById(oxQuarryPile,out pile,out _) && pile!=null,
                "Original natural Ox production sites unavailable during readiness observation");
            bool registered=*(ushort*)((byte*)quarry+0x1B8)!=0 && *(ushort*)((byte*)quarry+0x1BA)!=0 && *(ushort*)((byte*)quarry+0x1BC)!=0;
            // Hash-bound132910 state4 waits for the real8-stone tether batch.
            batchReady=tether->r_StoneBlocksAmount>=8;
            return "quarryStaff="+quarry->r_TotalCurrentWorkers+"/"+quarry->r_TotalWorkersRequired+
                " tetherStaff="+tether->r_TotalCurrentWorkers+"/"+tether->r_TotalWorkersRequired+
                " allMasonsRegistered="+registered+" pileStone="+pile->r_StoneBlocksAmount+
                " tetherStone="+tether->r_StoneBlocksAmount+" realBatchReady="+batchReady+
                " workerIds="+quarry->r_WorkerId_1+","+quarry->r_WorkerId_2+","+quarry->r_WorkerId_3+","+tether->r_WorkerId_1;
        }
        private unsafe void LogOxAcquisition(int sample)
        {
            string buildings="";
            foreach(int id in new[]{oxQuarry,oxTether,oxQuarryPile})
                if(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null)
                    buildings+=" building="+id+"/"+b->r_GlobalId+" type="+b->r_BuildingType+" owner="+b->r_PlayerIdOwner+" life="+b->r_AliveState+
                        " workers="+b->r_TotalCurrentWorkers+"/"+b->r_TotalWorkersRequired+" missing="+b->r_TotalMissingWorkers+
                        " stockpile="+b->r_StoneQuarry_StockPileBuildingId+" access="+b->r_TileAccessPositionX+","+b->r_TileAccessPositionY+
                        " stone="+b->r_StoneBlocksAmount+" stack="+b->r_CurrentGoodStackAmount+"/"+b->r_MaxGoodStackAmount+
                        " workerIds="+b->r_WorkerId_1+","+b->r_WorkerId_2+","+b->r_WorkerId_3+
                        " production="+b->r_ProductionProgress+"/"+b->r_ProductionCycleActive+" mode="+b->r_ProductionCycleMode+
                        " ticksLive="+b->r_TicksWhenProductionLive+" sleeping="+b->r_IsSleeping+
                        // Hash-boundB2B30 requires all3 physically registered mason slots before its coordinated production cycle.
                        " activeMasons="+(*(ushort*)((byte*)b+0x1B8))+","+(*(ushort*)((byte*)b+0x1BA))+","+(*(ushort*)((byte*)b+0x1BC))+
                        " quarryPhases="+(*(ushort*)((byte*)b+0x118))+","+(*(ushort*)((byte*)b+0x11A))+","+(*(ushort*)((byte*)b+0x11C))+","+(*(ushort*)((byte*)b+0x11E));
            int total=0,linked=0,loaded=0,travel=0,boundary=0;string first="";
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
                if(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && UnitAccess.IsReallyAlive(u) &&
                    u->r_UnitChimp==eChimps.CHIMP_TYPE_QUARRY_OX && u->r_ControllableForPlayerId==owner)
                {
                    total++;if(u->r_LinkedProductionBuildingId==oxTether)linked++;
                    if(u->r_CarryGoodAmount>0)loaded++;
                    bool route=u->r_AIState==1 && (u->r_PathPlanStateBitFlags==2 || u->r_PathPlanStateBitFlags==4);
                    if(route)travel++;if(route && u->r_MovementSubstep==8)boundary++;
                    if(total<=3)
                    {
                        UnitSnapshot snapshot=runtime.SnapshotUnit(id);
                        first+=" ox="+id+"/"+u->r_GlobalId+" type="+u->r_UnitChimp+" ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+
                            " substep="+u->r_MovementSubstep+" cargo="+u->r_CarryGoodAmount+
                            // Hash-bound132910: native unit+A4A must be0 before state4 can leave with8cargo.
                            " loadLatchA4A="+runtime.Native.Word(u,0xA4A)+" production="+u->r_LinkedProductionBuildingId+"/"+u->r_LinkedProductionBuildingGlobalId+
                            " tile="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" unsupported="+(snapshot==null?"snapshot-null":snapshot.Unsupported??"none")+
                            " candidates="+(snapshot==null?0:snapshot.Candidates.Count);
                    }
                }
            int yardLogs=0;string storageFacts="";
            int buildingCapacity=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
            for(int id=1;id<=buildingCapacity;id++)
                if(BuildingAccess.TryGetById(id,out GameBuilding* yard,out _) && yard!=null && yard->r_AliveState==AliveState.IsAlive &&
                    yard->r_PlayerIdOwner==owner && yard->r_BuildingType==eStructs.STRUCT_GOODS_YARD)
                {
                    if(yardLogs++<8)storageFacts+=" yard="+id+"/"+yard->r_GlobalId+" stone="+yard->r_StoneBlocksAmount+
                        " stack="+yard->r_CurrentGoodStackAmount+"/"+yard->r_MaxGoodStackAmount+" good="+yard->r_LocalStorageGoodType+
                        " access="+yard->r_TileAccessPositionX+","+yard->r_TileAccessPositionY;
                }
            int employees=0;string workers="";
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
                if(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && UnitAccess.IsReallyAlive(u) && u->r_ControllableForPlayerId==owner &&
                    (u->r_UnitChimp==eChimps.CHIMP_TYPE_QUARRY_MASON || u->r_UnitChimp==eChimps.CHIMP_TYPE_QUARRY_GRUNT) &&
                    (u->r_LinkedProductionBuildingId==oxQuarry || u->r_LinkedProductionBuildingId==oxTether))
                {
                    employees++;
                    if(employees<=5)
                        workers+=" employee="+id+"/"+u->r_GlobalId+" type="+u->r_UnitChimp+" ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+
                            " substep="+u->r_MovementSubstep+" cargo="+u->r_CarryGoodAmount+" production="+u->r_LinkedProductionBuildingId+
                            " tile="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                            // Hash-bound1319A0: native98E is the grunt's selected quarrypile/storage ID.
                            " selectedStorage98E="+runtime.Native.Word(u,0x98E);
                }
            log("ox-acquisition sample="+sample+buildings+" totalOx="+total+" linkedOx="+linked+" loadedOx="+loaded+
                " outboundRoute="+travel+" routeAtBoundary="+boundary+" stockpiles="+CountOwnedStockpiles()+first+" linkedEmployees="+employees+workers+storageFacts);
        }

        private unsafe bool TryCreateNaturalQuarry(out int quarry, out int tether)
        {
            quarry = tether = 0;
            var tiles = GameTileManagerAPI.Instance;
            var buildings = GameBuildingManagerAPI.Instance;
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int scale = BuildingScales.GetScale(eMappers.MAPPER_QUARRY);
            var candidates = new List<Tuple<int, int, int>>();
            for (int y = 1; y < 799 - scale; y++)
                for (int x = 1; x < 799 - scale; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    int tile = tiles.GetTileId(x, y);
                    if ((tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.HasStone) == 0 || tiles.GetTileBuildingId(tile) != 0) continue;
                    candidates.Add(Tuple.Create(Math.Max(Math.Abs(x - keep.X), Math.Abs(y - keep.Y)), x, y));
                }
            candidates.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) :
                a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3) : a.Item2.CompareTo(b.Item2));
            foreach (var candidate in candidates)
            {
                int x = candidate.Item2, y = candidate.Item3;
                bool stone = true;
                for (int dy = 0; dy < scale && stone; dy++) for (int dx = 0; dx < scale; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (!tiles.IsTileInsideMapBounds(xx, yy)) { stone = false; break; }
                    int tile = tiles.GetTileId(xx, yy);
                    if ((tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.HasStone) == 0 || tiles.GetTileBuildingId(tile) != 0 || tiles.GetTileUnitId(tile) != 0)
                    { stone = false; break; }
                }
                if (!stone || !TryNearbyOrdinarySite(x, y, 2, out int tx, out int ty)) continue;
                // Explicit placement bypass is confined to the disposable fixture; the source stone is real.
                buildings.CreatePrefab(owner, x, y, eMappers.MAPPER_QUARRY, scale, 0, true, true);
                int q = tiles.GetTileBuildingId(tiles.GetTileId(x, y));
                if (q <= 0 || !BuildingAccess.TryGetById(q, out GameBuilding* qb, out _) || qb == null || qb->r_BuildingType != eMappers.MAPPER_QUARRY.ConvertToEStructs()) continue;
                buildings.CreatePrefab(owner, tx, ty, eMappers.MAPPER_OXENBASE, BuildingScales.GetScale(eMappers.MAPPER_OXENBASE), 0, true, true);
                int t = tiles.GetTileBuildingId(tiles.GetTileId(tx, ty));
                if (t <= 0 || !BuildingAccess.TryGetById(t, out GameBuilding* tb, out _) || tb == null || tb->r_BuildingType != eMappers.MAPPER_OXENBASE.ConvertToEStructs())
                    throw new InvalidOperationException("Native quarry exists, but its planned tether failed; setup must not masquerade as coverage");
                quarry = q; tether = t;
                log("ox-real-site stoneAnchor=" + x + "," + y + " tetherAnchor=" + tx + "," + ty + " distanceToKeep=" + candidate.Item1);
                return true;
            }
            log("ox-real-site candidates=" + candidates.Count + " accepted=false");
            return false;
        }

        private static bool TryNearbyOrdinarySite(int x, int y, int scale, out int targetX, out int targetY)
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int radius = 8; radius <= 20; radius += 2)
                for (int d = 0; d < 8; d++)
                {
                    int tx = x + Dx[d] * radius, ty = y + Dy[d] * radius;
                    bool valid = true;
                    for (int yy = ty - 1; yy <= ty + scale && valid; yy++) for (int xx = tx - 1; xx <= tx + scale; xx++)
                    {
                        if (!tiles.IsTileInsideMapBounds(xx, yy)) { valid = false; break; }
                        int tile = tiles.GetTileId(xx, yy);
                        if (!tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetTileBuildingId(tile) != 0 || tiles.GetTileUnitId(tile) != 0 ||
                            (tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.HasStone | TilePropertyFlag.IsMoat)) != 0)
                        { valid = false; break; }
                    }
                    if (valid) { targetX = tx; targetY = ty; return true; }
                }
            targetX = targetY = 0; return false;
        }

        private unsafe Actor FindLoadedOx(int tether)
        {
            for (int id = 1; id <= NativeRelocation.MaximumUnitId; id++)
                if (UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null && UnitAccess.IsReallyAlive(unit) &&
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_QUARRY_OX && unit->r_ControllableForPlayerId == owner &&
                    unit->r_LinkedProductionBuildingId == tether && unit->r_CarryGoodAmount > 0 && unit->r_AIState == 1 &&
                    unit->r_MovementSubstep == 8 && (unit->r_PathPlanStateBitFlags == 2 || unit->r_PathPlanStateBitFlags == 4))
                {
                    UnitSnapshot state = runtime.SnapshotUnit(id);
                    if (state == null || state.Unsupported != null || state.Candidates.Count == 0 ||
                        Math.Max(Math.Abs(state.RequestedX - state.X), Math.Abs(state.RequestedY - state.Y)) < 3) continue;
                    return new Actor { Id = id, Global = unit->r_GlobalId, Health = unit->r_CurrentHealth, Type = unit->r_UnitChimp,
                        Tribe = unit->r_TribeId, X = unit->r_CurrentTilePositionX, Y = unit->r_CurrentTilePositionY };
                }
            return null;
        }

        private unsafe void BeginOxObservation(Actor actor, int tether, int cargo)
        {
            GameUnit* unit = Resolve(actor);
            oxObservation = new OxObservation { Actor = actor, Tether = tether, TetherGlobal = unit->r_LinkedProductionBuildingGlobalId,
                InitialCargo = cargo, Active = true };
            if (oxStorageObserver == null)
            {
                oxStorageObserver = BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveOxStorage);
                oxDropObserver = UnitR3EventHooks.OnQuarryOxDropOffStone.Observable.Subscribe(args => {
                    var value = oxObservation;
                    if (!stopped && value != null && value.Active && value.Actor.Id == args.UnitId) value.DropEvents++;
                });
            }
        }

        private unsafe void ObserveOxStorage(AddGoodToGoodsyardEventArgs args)
        {
            OxObservation value = oxObservation;
            if (stopped || value == null || !value.Active || value.Fault != null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId() != value.Actor.Id ||
                args.Good != eGoods.STORED_STONE_BLOCKS || !args.Add || args.AddAmount <= 0) return;
            try
            {
                GameUnit* ox = Resolve(value.Actor);
                Require(BuildingAccess.TryGetById(args.BuildingId, out GameBuilding* storage, out _) && storage != null &&
                    storage->r_GlobalId == unchecked((uint)args.BuildingGlobalId) && storage->r_PlayerIdOwner == owner,
                    "Ox storage call lacks matching native building identity");
                Require(Math.Abs(ox->r_CurrentTilePositionX - storage->r_TileAccessPositionX) < 3 &&
                    Math.Abs(ox->r_CurrentTilePositionY - storage->r_TileAccessPositionY) < 3,
                    "Ox attempted a remote delivery without native C9080 proximity");
                value.PhysicallyNearStorage = true;
                if (args.Phase == EventHookPhase.Pre)
                {
                    Require(!value.Pending, "Nested Ox deposit invalidated the simple matched call observation");
                    value.Pending = true; value.PendingStorage = args.BuildingId; value.PendingGlobal = storage->r_GlobalId;
                    value.PendingStone = storage->r_StoneBlocksAmount; value.PendingAmount = args.AddAmount;
                }
                else
                {
                    Require(value.Pending && value.PendingStorage == args.BuildingId && value.PendingGlobal == storage->r_GlobalId,
                        "Ox deposit Post lacks its matching Pre observation");
                    Require(storage->r_StoneBlocksAmount == value.PendingStone + value.PendingAmount,
                        "Ox drop event did not correspond to the actual accepted positive stone storage write");
                    value.AcceptedQuantity += value.PendingAmount; value.Pending = false;
                }
            }
            catch (Exception error) { value.Fault = error.ToString(); }
        }

        private unsafe bool ObserveOxOutcome()
        {
            OxObservation value = oxObservation;
            Require(value != null && value.Fault == null, value?.Fault ?? "No Ox observation");
            if (!UnitAccess.TryGetById(value.Actor.Id, out GameUnit* unit, out _) || unit == null ||
                unit->r_GlobalId != value.Actor.Global || !UnitAccess.IsReallyAlive(unit))
            { value.Lost = true; Require(!expectedEnabled, "Rescued loaded Ox was lost during its real delivery"); return false; }
            CheckActor(value.Actor, true);
            Require(unit->r_LinkedProductionBuildingId == value.Tether && unit->r_LinkedProductionBuildingGlobalId == value.TetherGlobal,
                "Ox tether identity changed during continuation");
            value.CargoEmpty = unit->r_CarryGoodAmount == 0;
            return value.CargoEmpty && value.AcceptedQuantity == value.InitialCargo && value.DropEvents > 0 && value.PhysicallyNearStorage;
        }

        private unsafe int OxCargo(Actor actor) => Resolve(actor)->r_CarryGoodAmount;

    }
}
