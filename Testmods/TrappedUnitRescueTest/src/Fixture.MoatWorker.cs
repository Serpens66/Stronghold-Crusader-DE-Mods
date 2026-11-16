using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // This case exercises the real Fixes worker-deletion branch at the native move publisher.
        // It does not manufacture a worker AI state, cargo, workplace link or deletion marker.
        private IEnumerable<AutomationWait> MoatWorker(AutomationRun run)
        {
            using(var proof=ObserveAppleWorld())
            {
            int farm = 0;
            bool fixesPresent = Chainloader.PluginInfos.ContainsKey("fixes");
            bool fixesDeletionInstalled = workerDeletionInstalled();
            Execute(run, () => {
                farm = SetupNaturalAppleFarm();
                log("moat-worker-setup nativeFarm=" + farm + " fixesPresent=" + fixesPresent +
                    " fixesDeletionInstalled=" + fixesDeletionInstalled + " enabled=" + expectedEnabled +
                    " manualWorkerState=false manualCargo=false");
            });

            Actor worker = null;
            for (int sample = 0; sample < 4000 && worker == null; sample++)
            {
                if(sample==256)Director.instance.SetEngineFrameRate(50);
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, sample < 256 ? 16 : 1+(sample&1));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                Execute(run, () => { worker = FindMoatWorker(farm); if(sample==0 || sample==31 || sample==127 || sample==255 || sample==599 || sample==1199)LogAppleAcquisition(farm,sample); });
            }
            Require(worker != null, "No naturally employed apple farmer acquired a supported travel segment with a distinct requested target");
            Director.instance.SetEngineFrameRate(250);

            int targetX = 0, targetY = 0, carryBefore = 0, lifeAfter = -1;
            uint workplaceGlobal = 0;
            bool accepted = false;
            Execute(run, () => {
                actors.Add(worker);
                var before = runtime.SnapshotUnit(worker.Id);
                Require(before != null && before.Unsupported == null && (before.Flags == 2 || before.Flags == 4),
                    "Moat-worker travel segment became unavailable before mutation");
                targetX = before.RequestedX; targetY = before.RequestedY;
                Require(targetX != before.X || targetY != before.Y, "Moat-worker target equals its current tile");
                Require((GameTileManagerAPI.Instance.GetTilePropertyFlag(GameTileManagerAPI.Instance.GetTileId(targetX, targetY)) & TilePropertyFlag.IsMoat) == 0,
                    "Moat-worker target must be outside completed moat before mutation");
                worker.SourceX = worker.X = before.X; worker.SourceY = worker.Y = before.Y; worker.Health = before.Health;
                carryBefore = WorkerCarry(worker);
                workplaceGlobal = WorkerWorkplaceGlobal(worker);
                var tiles = GameTileManagerAPI.Instance;
                int tile = tiles.GetTileId(before.X, before.Y);
                Require(MoatWorkerSourceEligible(tile),"Natural delivery/return source changed before native moat placement");
                log("moat-worker-before-brush unit="+worker.Id+"/"+worker.Global+" ai="+before.AIState+" tile="+tile+
                    " xy="+before.X+","+before.Y+" flags="+((uint)tiles.GetTilePropertyFlag(tile)).ToString("X8")+
                    " building="+tiles.GetTileBuildingId(tile)+" vegetation="+tiles.GetTileVegetationId(tile)+" height="+tiles.GetTileHeight(tile));
                EditorBrush.Call(tiles.GetTileManager(), owner, tile, before.Y, 1,
                    unchecked((int)(uint)TilePropertyFlag.IsMoat), 2, 0);
                Require((tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.IsMoat) != 0,
                    "Real completed-moat writer rejected the actual occupied delivery/return tile; source="+before.X+","+before.Y+
                    " flagsAfter="+((uint)tiles.GetTilePropertyFlag(tile)).ToString("X8")+" building="+tiles.GetTileBuildingId(tile)+
                    " vegetation="+tiles.GetTileVegetationId(tile)+" height="+tiles.GetTileHeight(tile));
                // One synchronous native call, then inspect the original slot before any cleanup tick.
                InvokeMoatWorkerMove(worker, targetX, targetY, out accepted, out lifeAfter, out string moveState);
                log("moat-worker-native-result unit=" + worker.Id + "/" + worker.Global + " " + moveState);
                if (!expectedEnabled && fixesDeletionInstalled)
                {
                    Require(lifeAfter == (int)AliveState.MarkedForDeletion,
                        "Fixes-enabled baseline did not reach its actual worker deletion marker; " + moveState);
                    log("moat-worker-baseline deletionObserved=true unit=" + worker.Id + "/" + worker.Global +
                        " rawLife=" + lifeAfter + " nativeMoveAccepted=" + accepted +
                        " target=" + targetX + "," + targetY + " source=" + worker.SourceX + "," + worker.SourceY);
                }
                else
                {
                    // A rejected fresh publication preserves the old native route. Physical
                    // exit and arrival below remain mandatory, independently of this return value.
                    Require(lifeAfter == (int)AliveState.IsAlive,
                        "Native worker move deleted the original worker; " + moveState);
                    CheckActor(worker, true);
                    Require(WorkerCarry(worker) == carryBefore && WorkerWorkplaceGlobal(worker) == workplaceGlobal,
                        "Moat move changed actual employee cargo or workplace identity immediately");
                }
            });

            if (!expectedEnabled && fixesDeletionInstalled)
            {
                // The marker itself is the assertion; no resurrecting/deleting/reusing the slot in this fixture.
                log("moat-worker-result enabled=false fixesDeletionInstalled=true expectedFailureClassObserved=true " +
                    "nativeMoatVerified=true deletionBeforeCleanup=true syntheticUnitState=false");
                yield break;
            }

            bool reached = false, leftMoat = false;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int sample = 0; sample < 160; sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 2);
                reached |= run.InSimulation(run.CurrentMission, () => {
                    CheckActor(worker, true);
                    Require(WorkerWorkplaceGlobal(worker) == workplaceGlobal, "Moat escape changed employee workplace global identity");
                    bool atTarget = WorkerAtRecordedTarget(worker, farm, targetX, targetY);
                    leftMoat |= !WorkerOnCompletedMoat(worker);
                    return atTarget;
                });
                if (reached && leftMoat) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(reached && leftMoat, "Natural worker did not physically exit the real moat and reach its original native job segment target");
                CheckActor(worker, true);
                log("moat-worker-result enabled=" + expectedEnabled + " fixesPresent=" + fixesPresent +
                    " fixesDeletionInstalled=" + fixesDeletionInstalled + " sameIdentity=true sameHealth=true sameWorkplace=true " +
                    "cargoBefore=" + carryBefore + " cargoAfter=" + WorkerCarry(worker) +
                    " physicallyLeftMoat=" + leftMoat + " originalSegmentReached=" + reached +
                    " nativeMoatVerified=true syntheticUnitState=false fullHarvestDeliveryCycleVerified=false" +
                    " fixesGuardAcceptance=" + (fixesDeletionInstalled && expectedEnabled ? "executed" : "not-executed") +
                    " coverage=" + (fixesDeletionInstalled ? "installed-fixes-hook" : "vanilla-control-no-installed-worker-hook"));
            });
            }
        }

        private unsafe Actor FindMoatWorker(int farm)
        {
            ValidateAppleWorld();
            var native=runtime.Native;
            var tiles=GameTileManagerAPI.Instance;
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_ControllableForPlayerId!=owner ||
                    unit->r_LinkedProductionBuildingId!=farm || unit->r_LinkedProductionBuildingGlobalId!=appleWorld.FarmGlobal ||
                    // Hash-bound135E30: 5 returns to the workplace through196280; 7 delivers to a granary through196280.
                    (unit->r_AIState!=5 && unit->r_AIState!=7) || unit->r_MovementSubstep!=8 ||
                    (unit->r_PathPlanStateBitFlags!=2 && unit->r_PathPlanStateBitFlags!=4))continue;
                UnitSnapshot state=runtime.SnapshotUnit(id);
                if(state==null || state.Unsupported!=null || state.Candidates.Count==0 ||
                    !MoatWorkerSourceEligible(state.TileId) ||
                    Math.Max(Math.Abs(state.RequestedX-state.X),Math.Abs(state.RequestedY-state.Y))<2)continue;
                int destination=native.Tile(state.RequestedX,state.RequestedY);
                if(destination<0 || (tiles.GetTilePropertyFlag(destination)&(TilePropertyFlag.IsMoat|TilePropertyFlag.Sea|TilePropertyFlag.River|TilePropertyFlag.Ford))!=0)continue;
                log("moat-worker-selected unit="+id+"/"+unit->r_GlobalId+" ai="+unit->r_AIState+" cargo="+unit->r_CarryGoodAmount+
                    " source="+state.X+","+state.Y+" tile="+state.TileId+" flags="+((uint)tiles.GetTilePropertyFlag(state.TileId)).ToString("X8")+
                    " building="+tiles.GetTileBuildingId(state.TileId)+" vegetation="+tiles.GetTileVegetationId(state.TileId)+" height="+tiles.GetTileHeight(state.TileId)+
                    " target="+state.RequestedX+","+state.RequestedY+" targetFlags="+((uint)tiles.GetTilePropertyFlag(destination)).ToString("X8"));
                return new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                    Tribe=unit->r_TribeId,X=state.X,Y=state.Y};
            }
            return null;
        }

        private bool MoatWorkerSourceEligible(int tile)
        {
            var tiles=GameTileManagerAPI.Instance;
            if(tile<0 || !runtime.Native.OrdinaryGround(tile))return false;
            // Hash-bound6FE90(param7=2): use the strict vegetation==0 branch and all tile rejection bits.
            // Farm/tree variants are excluded additionally to keep this seam outside the natural orchard.
            TilePropertyFlag excluded=TilePropertyFlag.RealityEdge|TilePropertyFlag.IsBuilding|TilePropertyFlag.IsElevated|
                TilePropertyFlag.IsWall|TilePropertyFlag.Sea|TilePropertyFlag.River|TilePropertyFlag.Ford|TilePropertyFlag.ImpassableEdge|
                TilePropertyFlag.HasStone|TilePropertyFlag.HasIron|TilePropertyFlag.IsSwamp|TilePropertyFlag.IsFarm|TilePropertyFlag.IsMoat|
                TilePropertyFlag.IsTree|TilePropertyFlag.IsAppleFarm|TilePropertyFlag.IsFarmFence|TilePropertyFlag.IsWheat|TilePropertyFlag.IsHops;
            return (tiles.GetTilePropertyFlag(tile)&excluded)==0 && tiles.GetTileBuildingId(tile)==0 &&
                tiles.GetTileVegetationId(tile)==0 && tiles.GetTileHeight(tile)<13;
            // Native59210 remains authoritative; the fixture still requires the real writer to accept the tile.
        }

        private unsafe void InvokeMoatWorkerMove(Actor actor, int targetX, int targetY, out bool accepted, out int lifeAfter, out string moveState)
        {
            GameUnit* original = Resolve(actor);
            uint global = original->r_GlobalId;
            // Reuse the initialized, hash/layout/ownership-validated call owner; do not register
            // a second native call site or repeat initialization during the synchronous operation.
            var native = runtime.Native;
            Require(native != null, "Initialized native move owner cannot be resolved for the fixture");
            accepted = native.Reissue(actor.Id, targetX, targetY);
            // The synchronous publisher may mark the slot for deletion, so alive-only lookup is unsuitable here.
            // It does not run the unit-dispatch cleanup tick. Verify the original allocation identity before reading life.
            Require(original->r_GlobalId == global && global == actor.Global && original->r_UnitChimp == actor.Type,
                "Original worker slot identity changed inside one synchronous move call");
            lifeAfter = (int)original->r_AliveState;
            moveState = "lifeAfter=" + lifeAfter + " accepted=" + accepted +
                " pathFlags=" + original->r_PathPlanStateBitFlags + " pathLength=" + original->r_PathPlanLength +
                " stored9C8=" + native.Word(original,0x9C8) +
                " position=" + original->r_CurrentTilePositionX + "," + original->r_CurrentTilePositionY +
                " next=" + native.Word(original,0x738) + "," + native.Word(original,0x73A) +
                " target=" + targetX + "," + targetY +
                " sourcePcl=" + GamePathingManagerAPI.Instance.GetPathComponentIdByTileId((int)original->r_CurrentPositionTileId) +
                " nextPcl=" + GamePathingManagerAPI.Instance.GetPathComponentId(native.Word(original,0x738),native.Word(original,0x73A)) +
                " targetPcl=" + GamePathingManagerAPI.Instance.GetPathComponentId(targetX,targetY) +
                " componentDirty=" + GamePathingManagerAPI.Instance.GetPathfindingContextView().ComponentGridDirty +
                " componentGeneration=" + GamePathingManagerAPI.Instance.GetComponentGeneration();
        }

        private unsafe uint WorkerWorkplaceGlobal(Actor actor) => Resolve(actor)->r_LinkedProductionBuildingGlobalId;

        private unsafe bool WorkerOnCompletedMoat(Actor actor)
        {
            GameUnit* unit = Resolve(actor);
            return (GameTileManagerAPI.Instance.GetTilePropertyFlag((int)unit->r_CurrentPositionTileId) & TilePropertyFlag.IsMoat) != 0;
        }
    }
}
