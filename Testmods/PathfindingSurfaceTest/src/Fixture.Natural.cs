using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Units;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingSurfaceTest
{
    internal sealed partial class Fixture
    {
        // Coordinates and bounds belong to the captured test_diagonal.sav fixture,
        // not to a general claim that every obstacle can be bypassed this closely.
        private readonly HashSet<uint> naturalInitialGlobals = new HashSet<uint>();
        private readonly HashSet<uint> naturalInitialFarmerGlobals = new HashSet<uint>();
        private readonly HashSet<uint> naturalReportedGlobals = new HashSet<uint>();
        private readonly Dictionary<int, NaturalPendingMove> naturalPending = new Dictionary<int, NaturalPendingMove>();
        private sealed class NaturalPendingMove
        {
            internal uint Global;
            internal long SingleRoutes;
            internal NaturalMovementSnapshot Before;
            internal UnitMoveHereEventArgs Invocation;
        }
        private struct NaturalMovementSnapshot
        {
            internal int CurrentX, CurrentY, NextX, NextY, PreviousX, PreviousY, Flags, Substep, Index, Length;
            internal int TargetX, TargetY, TargetX2, TargetY2, AiState;
            internal int StartX => Flags == 0 && Substep == 8 ? CurrentX : NextX;
            internal int StartY => Flags == 0 && Substep == 8 ? CurrentY : NextY;
            public override string ToString() => "current=" + CurrentX + "," + CurrentY + " next=" + NextX + "," + NextY +
                " previous=" + PreviousX + "," + PreviousY + " flags=" + Flags + " substep=" + Substep + " path=" + Index + "/" + Length +
                " target=" + TargetX + "," + TargetY + " target2=" + TargetX2 + "," + TargetY2 + " aiState=" + AiState + " nativeStart=" + StartX + "," + StartY;
        }
        private struct NaturalPoint
        {
            internal int X, Y;
            internal NaturalPoint(int x, int y) { X = x; Y = y; }
        }
        private volatile bool naturalCapture, naturalNewWorkerPassed;
        private bool naturalControlOwned;
        private int naturalFarmId, naturalFreshRoutes, naturalLoadedRoutes;
        private uint naturalFarmGlobal;
        private uint corridorTribeGlobal;
        private IAiMilitaryCapability corridorMilitary;
        private int corridorDx, corridorDy, corridorBound;
        // The reported save's dairy economy belongs to player 7. Forced test
        // spearmen retain the locally controlled owner used by other scenarios.
        private const int NaturalOwner = 7;

        private struct CorridorSpawnSite
        {
            internal int X, Y, Tile, Chimp, Building, Height;
            internal bool Inside, Valid, GenericWalkable, Raised, Dry, SurfaceAllowed;
            internal PathfindingProfile Profile;
            internal PathEdgeMask Outgoing;
            internal TilePropertyFlag Flags;
            internal bool Ready => Inside && Valid && SurfaceAllowed && Chimp == 0;
            public override string ToString() => "tile=" + X + "," + Y + " nativeTile=" + Tile + " inside=" + Inside + " valid=" + Valid +
                " genericWalkable=" + GenericWalkable + " chimp=" + Chimp + " building=" + Building + " flags=0x" + ((uint)Flags).ToString("X8") +
                " namedFlags=" + Flags + " raised=" + Raised + " dry=" + Dry + " height=" + Height + " outgoing=" + Outgoing +
                " profile=" + Profile + " surfaceAllowed=" + SurfaceAllowed;
        }

        private CorridorSpawnSite ReadCorridorSpawnSite(bool group)
        {
            var site = new CorridorSpawnSite { X = group ? 299 : 355, Y = group ? 363 : 332, Tile = -1 };
            var tiles = GameTileManagerAPI.Instance;
            site.Inside = tiles.IsTileInsideMapBounds(site.X, site.Y);
            if (!site.Inside) return site;
            site.Tile = tiles.GetTileId(site.X, site.Y); site.Valid = tiles.IsValidTileId(site.Tile);
            if (!site.Valid) return site;
            var profiles = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance;
            Require(profiles.IsAvailable, "Public test-spearman profile source unavailable");
            site.Profile = profiles.GetUnitTypePathfindingProfile(eChimps.CHIMP_TYPE_SPEARMAN);
            site.GenericWalkable = tiles.IsTileWalkableAndUnoccupied(site.Tile);
            site.Chimp = tiles.GetChimpLayer()[site.Tile]; site.Building = tiles.GetTileBuildingId(site.Tile);
            site.Flags = tiles.GetTilePropertyFlag(site.Tile); site.Height = tiles.GetTileHeight(site.Tile);
            site.Raised = (site.Flags & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated)) != 0;
            site.Dry = (site.Flags & TilePropertyFlag.IsMoat) == 0;
            site.Outgoing = GamePathingManagerAPI.Instance.GetPathEdgeMask(site.X, site.Y);
            // The 89-step fixture deliberately starts on flat empty terrain.
            // The captured 201-step leg is FootDefault/Any: generic building and
            // wall/elevated exclusions are not the native permission contract.
            site.SurfaceAllowed = site.Dry && site.Outgoing != PathEdgeMask.None &&
                (group ? site.Profile == PathfindingProfile.Default : site.GenericWalkable && !site.Raised);
            return site;
        }

        private IEnumerable<AutomationWait> SpawnAtFixedCorridorStart(AutomationRun run, bool group)
        {
            string last = null; bool spawned = false;
            for (int attempt = 0; attempt <= 32 && !spawned; attempt++)
            {
                spawned = run.InSimulation(run.CurrentMission, () => {
                    CorridorSpawnSite site = ReadCorridorSpawnSite(group);
                    string detail = site.ToString();
                    if (detail != last) { log("corridor-spawn-site kind=" + (group ? "group" : "individual") + " " + detail); last = detail; }
                    Require(site.Inside && site.Valid && site.SurfaceAllowed, "Fixed corridor start is not permitted for the fixture: " + detail);
                    if (site.Chimp != 0) return false;
                    // Check and Create share the public engine monitor, avoiding
                    // a new occupancy race between a successful wait and spawn.
                    SpawnCorridor(group); return true;
                });
                if (!spawned && attempt < 32)
                {
                    foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission, 16);
                }
            }
            Require(spawned, "Fixed corridor start remained occupied after 512 simulation ticks: " + last);
        }

        private IEnumerable<AutomationWait> LoadNatural(AutomationRun run)
        {
            Require(AttackTargetRules.TryRegister(Plugin.Guid, "corridor-truce", out corridorTruce, out string truceReason),
                "Public attack-target exclusion registration failed: " + truceReason);
            yield return AutomationWait.NextFrame;
            Require(AttackTargetRules.IsAvailable, "Public attack-target exclusions did not pass native publication before loading");
            Require(PlayerPerspectiveAPI.TryRegisterSpectatorControl(Plugin.Guid, out string controlReason),
                "Spectator-control registration before save load failed: " + controlReason);
            object settings = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance.GetType().GetProperty("Settings").GetValue(
                BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance);
            Set(settings, "EnableMod", true); Set(settings, "EnablePathfindingCore", true); Set(settings, "AnchorSingleMoves", true);
            Set(settings, "EnableFriendlyMoatTraversal", false); Set(settings, "AvoidFire", false); Set(settings, "AvoidPlague", false);
            Set(settings, "EnableMoveFormationEnhancements", false);
            string save = Path.Combine(ConfigSettings.GetSavesPath(), "test_diagonal.sav");
            SavegameModSettings.PrepareLoad(save, true);
            foreach (var wait in run.LoadSave(save)) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, RecordNaturalBaseline);
        }

        private void ReleaseNaturalControl()
        {
            if (!naturalControlOwned) return;
            naturalControlOwned = false;
            if (PlayerPerspectiveAPI.GetInputControlledPlayerId() == NaturalOwner)
                PlayerPerspectiveAPI.ClearSpectatorControl();
            log("corridor-spectator-control released=true");
        }

        private unsafe void RecordNaturalBaseline()
        {
            naturalInitialGlobals.Clear(); naturalInitialFarmerGlobals.Clear();
            foreach (int id in GameUnitManagerAPI.Instance.GetAllAliveUnits())
                if (UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId != 0)
                {
                    naturalInitialGlobals.Add(unit->r_GlobalId);
                    if (unit->r_ControllableForPlayerId == NaturalOwner && unit->r_UnitChimp == eChimps.CHIMP_TYPE_FARMER_CATTLE)
                        naturalInitialFarmerGlobals.Add(unit->r_GlobalId);
                }
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(NaturalOwner);
            log("natural-save-loaded initialAliveGlobals=" + naturalInitialGlobals.Count + " initialDairyFarmerGlobals=" + naturalInitialFarmerGlobals.Count +
                " naturalOwner=" + NaturalOwner + " keep=" + keep.X + "," + keep.Y + " source=test_diagonal.sav");
        }

        private void VerifyCorridorOptions()
        {
            var host = provider.GetType().Assembly.GetType("Pathfinding.Engine.EngineHost", true);
            object options = host.GetField("options", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (string name in new[] { "EnablePathfindingCore", "AnchorSingleMoves" })
                Require((bool)options.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(options),
                    "Loaded mission did not freeze the required host option: " + name);
        }

        private unsafe void SpawnCorridor(bool group)
        {
            VerifyCorridorOptions();
            Require(GamePlayerManagerAPI.Instance.GetLocalPlayerId() == owner &&
                (!naturalControlOwned || PlayerPerspectiveAPI.GetInputControlledPlayerId() == owner),
                "Controlled test player changed before the new corridor order");
            cx = group ? 299 : 355; cy = group ? 363 : 332;
            corridorDx = group ? 201 : 89; corridorDy = group ? 4 : 36; corridorBound = group ? 12 : 4;
            var tiles = GameTileManagerAPI.Instance;
            Require(tiles.IsTileInsideMapBounds(cx, cy) && tiles.IsTileInsideMapBounds(cx + corridorDx, cy + corridorDy), "Corridor fixture outside the loaded map");
            CorridorSpawnSite site = ReadCorridorSpawnSite(group);
            Require(site.Ready, "Corridor start changed before exact spawn: " + site);
            elevation = site.Height;
            actors.Clear(); capture = false;
            Require(ApiShared.ForMod(Plugin.Guid).TryGetAiMilitary(out corridorMilitary, out var diagnostic),
                "Public AI military protection unavailable: " + diagnostic.Reason);
            var assignment = new AiMilitaryAssignment(AiMilitaryRole.None, AiRecruitmentMode.Regular);
            Require(corridorMilitary.TryBeginSpawnScope(owner, eChimps.CHIMP_TYPE_SPEARMAN, assignment, false, false,
                result => { if (!stopped && result.Status == AiMilitaryAssignmentStatus.Rejected) error = "Public actor protection rejected: " + result.Reason + ": " + result.Detail; },
                out IAiMilitarySpawnScope scope, out string reason), "Public protected spawn scope rejected: " + reason);
            using (scope)
            {
                long id = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, cx, cy, elevation, eChimps.CHIMP_TYPE_SPEARMAN);
                Require(id > 0 && id <= int.MaxValue, "Corridor actor spawn failed");
                Require(UnitAccess.TryGetById((int)id, out GameUnit* unit, out _) && unit->r_GlobalId != 0 &&
                    unit->r_CurrentTilePositionX == cx && unit->r_CurrentTilePositionY == cy, "Corridor actor projected away from the verified start");
                actors.Add(new Actor { Id = (int)id, Global = unit->r_GlobalId, Type = eChimps.CHIMP_TYPE_SPEARMAN,
                    X = cx, Y = cy, StartX = cx, StartY = cy, TargetX = cx + corridorDx, TargetY = cy + corridorDy });
                var submitted = scope.Complete();
                Require(submitted.Count == 1 && submitted[0].Unit.Equals(new AiUnitRef((int)id, actors[0].Global)) &&
                    submitted[0].Status != AiMilitaryAssignmentStatus.Rejected, "Protected spawn did not capture exactly the new actor");
            }
        }

        private IEnumerable<AutomationWait> ExerciseCorridor(AutomationRun run, bool group)
        {
            log("corridor-phase kind=" + (group ? "group" : "individual") + " phase=spawn owner=" + owner);
            foreach (var wait in SpawnAtFixedCorridorStart(run, group)) yield return wait;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            yield return AutomationWait.Until("corridor public AI protection applied", () => run.InSimulation(run.CurrentMission, CorridorProtectionReady));
            log("corridor-phase kind=" + (group ? "group" : "individual") + " phase=protection-applied role=None actor=" + actors[0].Id + " global=" + actors[0].Global + " tribe=" + tribe);
            yield return AutomationWait.Until("corridor actor live", () => run.InSimulation(run.CurrentMission, AllLive));
            log("corridor-phase kind=" + (group ? "group" : "individual") + " phase=live actor=" + actors[0].Id + " global=" + actors[0].Global);
            yield return AutomationWait.Until("corridor owner mask ready", () => run.InSimulation(run.CurrentMission, AnchorMaskReady));
            log("corridor-phase kind=" + (group ? "group" : "individual") + " phase=mask-ready owner=" + owner);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            if (group) Execute(run, () => {
                if (GamePlayerManagerAPI.Instance.IsAIPlayer(owner)) VerifyCorridorGroup();
                else CreateCorridorGroup();
            });
            else log("corridor-phase kind=individual phase=ready groupRequired=false");
            foreach (var wait in RunOnSimulationTick(run, () => IssueCorridor(group))) yield return wait;
            for (int i = 0; i < 600; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 32);
                if (run.InSimulation(run.CurrentMission, () => { Require(error == null, error); ObserveActor(actors[0]); return actors[0].Arrived; })) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(error == null, error); Require(actors[0].Arrived, "Corridor actor did not arrive");
                log("corridor-arrived kind=" + (group ? "group" : "individual") + " actor=" + actors[0].Id + " tile=" + actors[0].X + "," + actors[0].Y);
                capture = false; KillActor(actors[0]); actors.Clear();
            });
        }

        private bool CorridorProtectionReady()
        {
            Require(error == null, error);
            Actor actor = actors[0];
            Require(corridorMilitary.TryGetState(new AiUnitRef(actor.Id, actor.Global), out AiMilitaryAssignmentResult state),
                "Public protected actor state is missing");
            Require(state.Assignment.Role == AiMilitaryRole.None && state.Unit.Equals(new AiUnitRef(actor.Id, actor.Global)),
                "Public protected actor identity or requested role changed");
            Require(state.Status != AiMilitaryAssignmentStatus.Rejected, "Public actor protection rejected: " + state.Reason + ": " + state.Detail);
            if (state.Status != AiMilitaryAssignmentStatus.Applied) return false;
            if (GamePlayerManagerAPI.Instance.IsAIPlayer(owner))
            {
                Require(state.Group.TribeId > 0 && state.Group.GlobalId != 0 && state.Group.PlayerId == owner,
                    "Applied None role did not provide an owned protected group");
                tribe = state.Group.TribeId; corridorTribeGlobal = state.Group.GlobalId;
                VerifyCorridorGroup();
            }
            return true;
        }

        private unsafe void CreateCorridorGroup()
        {
            Actor actor = actors[0]; GameUnit* unit = ActorUnit(actor);
            Require(UnitAccess.IsReallyAlive(unit), "Cannot group an actor which is not really alive");
            var tribes = GameTribeManagerAPI.Instance;
            long created = tribes.Create(owner);
            Require(created > 0 && created <= int.MaxValue, "Public tribe creation failed");
            tribe = (int)created;
            Require(tribes.TryGetTribeById(tribe, out GameTribe* createdTribe) && createdTribe != null &&
                createdTribe->r_GlobalId != 0 && createdTribe->r_PlayerIdOwner == owner && createdTribe->r_UnitsInGroup == 0,
                "Created tribe has invalid identity, owner or initial membership");
            corridorTribeGlobal = createdTribe->r_GlobalId;
            Require(APIShared.UnitAccess.TryGetById(actor.Id, out GameUnit* current, out _) &&
                current->r_GlobalId == actor.Global && current->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN &&
                current->r_ControllableForPlayerId == owner && UnitAccess.IsReallyAlive(current), "Actor identity changed before public tribe assignment");
            Require(tribes.AssignUnit(tribe, actor.Id), "Public tribe assignment was rejected");
            VerifyCorridorGroup();
            log("corridor-phase kind=group phase=ready tribe=" + tribe + " tribeGlobal=" + corridorTribeGlobal +
                " owner=" + owner + " actor=" + actor.Id + " actorGlobal=" + actor.Global + " members=1 publicCreateAssign=true");
        }

        private unsafe void VerifyCorridorGroup()
        {
            Actor actor = actors[0]; GameUnit* unit = ActorUnit(actor);
            var tribes = GameTribeManagerAPI.Instance;
            Require(unit->r_TribeId == tribe && tribes.TryGetTribeById(tribe, out GameTribe* current) && current != null &&
                current->r_GlobalId == corridorTribeGlobal && current->r_PlayerIdOwner == owner && current->r_UnitsInGroup == 1 &&
                (current->r_AliveState == AliveState.NeedsInit || current->r_AliveState == AliveState.IsAlive),
                "Corridor tribe identity, owner, state or unit back-link changed");
            var members = new List<int>();
            Require(tribes.GetUnits(tribe, members) && members.Count == 1 && members[0] == actor.Id,
                "Corridor tribe does not contain exactly its own test actor");
        }

        private unsafe void IssueCorridor(bool group)
        {
            Actor actor = actors[0]; GameUnit* unit = ActorUnit(actor);
            Require(unit->r_CurrentTilePositionX == cx && unit->r_CurrentTilePositionY == cy && !Walking(unit), "Corridor actor moved before the new order");
            Require(CorridorProtectionReady(), "Public actor protection is not applied before the new order");
            if (group) VerifyCorridorGroup();
            log("corridor-phase kind=" + (group ? "group" : "individual") + " phase=issuing actor=" + actor.Id +
                " global=" + actor.Global + " owner=" + owner + " actualTribe=" + unit->r_TribeId);
            capture = true; phase++; CaptureMetrics();
            if (group) Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, actor.TargetX, actor.TargetY), "Corridor group command rejected");
            else
            {
                if (APIShared.UnitAccess.TryGetById(actor.Id, out var movingUnit, out _))
                    GameUnitManagerAPI.Instance.MoveToTile(actor.Id, actor.TargetX, actor.TargetY);
                else throw new InvalidOperationException("Corridor actor disappeared before command");
            }
            Require(error == null, error);
            Require(actor.LastResult > 0 && actor.AcceptedRoutes > 0, "No fresh corridor route observed");
            string counter = group ? "anchorPublished" : "anchorSingleRoutes";
            Require(Metric(counter) > metrics[counter], "Corridor command bypassed accelerated publisher");
            double maximum; int turns;
            ValidateFreshCorridor(actor.Id, actor.Global, cx, cy, actor.TargetX, actor.TargetY, out maximum, out turns, flatGroundFixture: !group);
            Require(maximum <= corridorBound, "Fresh corridor route retains the old detour: maximum deviation=" + maximum + " bound=" + corridorBound);
            log("corridor-route kind=" + (group ? "group" : "individual") + " actor=" + actor.Id + " global=" + actor.Global +
                " steps=" + corridorDx + " maxDeviation=" + maximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " turns=" + turns + " baselineMaxDeviation=" + (group ? "54.751" : "27.989"));
        }

        private unsafe void ValidateFreshCorridor(int id, uint global, int sx, int sy, int tx, int ty, out double maximum, out int turns,
            bool minimumSteps = true, bool flatGroundFixture = true)
        {
            Require(UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == global, "Fresh corridor identity changed");
            if (!flatGroundFixture)
            {
                var tables = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance;
                Require(tables.IsAvailable, "Natural surface profile table is unavailable");
                PathfindingProfile profile = tables.GetUnitTypePathfindingProfile(unit->r_UnitChimp);
                // FarmerCattle and Spearman are absent from DCD60's restricted-type switch;
                // Default therefore resolves to Any, including legal keep,
                // stockpile and stair tiles. A modified Large profile requires
                // its own whitelist-aware fixture rather than granting Any.
                Require((unit->r_UnitChimp == eChimps.CHIMP_TYPE_FARMER_CATTLE || unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN) && profile == PathfindingProfile.Default,
                    "FootAny fixture requires a Default-profile farmer or test spearman; actual type=" + unit->r_UnitChimp + " profile=" + profile);
            }
            Require(GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(id, out GameUnitPathPlanView path) && path.IsLengthValid && path.CurrentIndex == 0,
                "Fresh route missing, invalid or already consumed");
            int dx = tx - sx, dy = ty - sy, distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            Require(distance > 0 && (!minimumSteps || path.Length == distance), "Fresh corridor route does not attain the minimum step count");
            var tiles = GameTileManagerAPI.Instance;
            Require(tiles.IsTileInsideMapBounds(sx, sy), "Fresh corridor start outside map: " + sx + "," + sy);
            int x = sx, y = sy, previous = -1; long maxCross = 0; turns = 0; bool reportedRaised = false;
            for (int i = 0; i < path.Length; i++)
            {
                Require(path.TryGetDirection(i, out PackedPathDirection direction), "Invalid fresh corridor direction");
                if (!GamePathingManagerAPI.Instance.HasPathEdge(x, y, direction))
                    throw new InvalidOperationException("Fresh corridor uses a forbidden directed edge: actor=" + id +
                        " global=" + global + " index=" + i + "/" + path.Length + " from=" + x + "," + y +
                        " to=" + (x + Dx[(int)direction]) + "," + (y + Dy[(int)direction]) + " direction=" + direction + " start=" + sx + "," + sy + " target=" + tx + "," + ty);
                int d = (int)direction; x += Dx[d]; y += Dy[d];
                if (!tiles.IsTileInsideMapBounds(x, y))
                    throw new InvalidOperationException("Fresh corridor leaves map bounds: actor=" + id + " index=" + i + " tile=" + x + "," + y);
                int tile = tiles.GetTileId(x, y);
                if (!tiles.IsValidTileId(tile))
                    throw new InvalidOperationException("Fresh corridor entered an invalid native tile: actor=" + id + " index=" + i + " tile=" + x + "," + y);
                TilePropertyFlag flags = tiles.GetTilePropertyFlag(tile);
                bool raised = (flags & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated)) != 0;
                if ((flatGroundFixture && raised) || (flags & TilePropertyFlag.IsMoat) != 0)
                    throw new InvalidOperationException("Fresh corridor surface rejected: actor=" + id + " index=" + i + " tile=" + x + "," + y +
                        " flags=0x" + ((uint)flags).ToString("X8") + " namedFlags=" + flags + " expected=" + (flatGroundFixture ? "flat-dry-ground" : "FootAny-dry"));
                if (!flatGroundFixture && raised && !reportedRaised)
                {
                    reportedRaised = true;
                    log("natural-permitted-surface actor=" + id + " global=" + global + " index=" + i + " tile=" + x + "," + y +
                        " flags=0x" + ((uint)flags).ToString("X8") + " namedFlags=" + flags + " profile=Default surface=Any directedEdge=true");
                }
                maxCross = Math.Max(maxCross, Math.Abs((long)(x - sx) * dy - (long)(y - sy) * dx));
                if (previous >= 0 && previous != d) turns++; previous = d;
            }
            Require(x == tx && y == ty && unit->r_TargetTilePositionX == tx && unit->r_TargetTilePositionY == ty,
                "Fresh corridor endpoint disagrees with requested or owned target: actor=" + id + " global=" + global +
                " decoded=" + x + "," + y + " expected=" + tx + "," + ty + " owned=" + unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY +
                " target2=" + unit->r_TargetTilePositionX2 + "," + unit->r_TargetTilePositionY2);
            maximum = (double)maxCross / distance;
        }

        private IEnumerable<AutomationWait> ExerciseNaturalWorker(AutomationRun run)
        {
            Execute(run, () => { naturalCapture = true; naturalNewWorkerPassed = false; naturalReportedGlobals.Clear(); naturalPending.Clear(); });
            bool built = false; int farmX = 0, farmY = 0;
            // A normal dairy farm creates native employment demand. Never assign a
            // worker, AI state, production link or movement order through private data.
            // Try the captured destination's center first, then symmetric rings.
            for (int radius = 0; radius <= 20 && !built; radius += 10)
                for (int dy = -radius; dy <= radius && !built; dy += 10)
                    for (int dx = -radius; dx <= radius && !built; dx += 10)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        built = run.TrySpawnBuilding(run.CurrentMission, NaturalOwner, 500 + dx, 370 + dy, eMappers.MAPPER_CATTLEFARM, 0,
                            out naturalFarmId, out naturalFarmGlobal);
                        if (built) { farmX = 500 + dx; farmY = 370 + dy; }
                        yield return AutomationWait.NextFrame;
                    }
            Require(built, "Natural worker setup: normal dairy-farm placement near the reproducer was not confirmed; no natural-worker acceptance");
            log("natural-farm-created id=" + naturalFarmId + " global=" + naturalFarmGlobal + " owner=" + NaturalOwner + " tile=" + farmX + "," + farmY + " normalPlacement=true");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int i = 0; i < 320; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 64);
                if (run.InSimulation(run.CurrentMission, () => { Require(error == null, error); return naturalNewWorkerPassed; })) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                naturalCapture = false; Require(error == null, error);
                log("natural-worker-summary newOccupationPostRoutes=" + naturalFreshRoutes + " loadedFarmerPostRoutes=" + naturalLoadedRoutes + " newWorkerJobPassed=" + naturalNewWorkerPassed);
                Require(naturalNewWorkerPassed, "No newly employed dairy farmer acquired the new normal farm job and published a fresh complete route across the reproducer after arming; no natural-worker acceptance");
            });
        }

        private bool MeasureNaturalLeg(int id, int sx, int sy, out double maximum, out string leg)
        {
            Require(GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(id, out GameUnitPathPlanView path) && path.IsLengthValid && path.CurrentIndex == 0,
                "Natural route changed before local measurement");
            var points = new List<NaturalPoint>();
            int x = sx, y = sy;
            for (int i = 0; i < path.Length; i++)
            {
                Require(path.TryGetDirection(i, out PackedPathDirection direction), "Invalid local natural direction");
                int d = (int)direction;
                if (Dx[d] == 1)
                {
                    if (points.Count == 0) points.Add(new NaturalPoint(x, y));
                    points.Add(new NaturalPoint(x + Dx[d], y + Dy[d]));
                }
                else
                {
                    if (ComparableNaturalLeg(points, out maximum, out leg)) return true;
                    points.Clear();
                }
                x += Dx[d]; y += Dy[d];
            }
            return ComparableNaturalLeg(points, out maximum, out leg);
        }

        private static bool ComparableNaturalLeg(List<NaturalPoint> points, out double maximum, out string leg)
        {
            maximum = double.NaN; leg = "not-comparable";
            if (points.Count < 2) return false;
            NaturalPoint start = points[0], end = points[points.Count - 1];
            // Compare the complete monotone leg after the saved castle exit,
            // not a new global chord across the unavoidable walls. Merely
            // selecting a straight subsection of an old detour proves nothing.
            if (start.X < 290 || start.X > 325 || start.Y < 350 || start.Y > 380 ||
                start.X > 355 || end.X < 444 || end.Y < 350 || end.Y > 390) return false;
            int dx = end.X - start.X, dy = end.Y - start.Y;
            long maxCross = 0;
            foreach (NaturalPoint point in points)
                maxCross = Math.Max(maxCross, Math.Abs((long)(point.X - start.X) * dy - (long)(point.Y - start.Y) * dx));
            maximum = (double)maxCross / dx;
            leg = start.X + "," + start.Y + "->" + end.X + "," + end.Y + ":steps=" + (points.Count - 1);
            return true;
        }

        private unsafe void ObserveNaturalMove(UnitMoveHereEventArgs args)
        {
            if (!naturalCapture || stopped || missions?.Current?.SessionId != session) return;
            NaturalPendingMove pending = null;
            NaturalMovementSnapshot after = default;
            try
            {
                if (!UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _) || unit->r_UnitChimp != eChimps.CHIMP_TYPE_FARMER_CATTLE ||
                    unit->r_ControllableForPlayerId != NaturalOwner || unit->r_GlobalId == 0 || !UnitAccess.IsReallyAlive(unit)) return;
                if (args.Phase == EventHookPhase.Pre)
                {
                    naturalPending[args.UnitId] = new NaturalPendingMove { Global = unit->r_GlobalId, SingleRoutes = Metric("anchorSingleRoutes"),
                        Before = CaptureNaturalMovement(unit), Invocation = args };
                    return;
                }
                if (args.Phase != EventHookPhase.Post || !naturalPending.TryGetValue(args.UnitId, out pending)) return;
                naturalPending.Remove(args.UnitId);
                if (args.ReturnValue <= 0 || pending.Global != unit->r_GlobalId) return;
                after = CaptureNaturalMovement(unit);
                bool accelerated = Metric("anchorSingleRoutes") > pending.SingleRoutes;
                // State 2 is the observed native job-route state of this saved
                // reproducer, not a new public enum or a state written by the test.
                if (unit->r_AIState != 2) return;
                uint global = unit->r_GlobalId;
                // FBCB9319: 196305/19630F choose Current only for flags0/substep8,
                // otherwise Next2. Native +C8 stores Current even during a step
                // and therefore cannot serve as the new packed route's origin.
                int sx = pending.Before.StartX, sy = pending.Before.StartY;
                int tx = unit->r_TargetTilePositionX, ty = unit->r_TargetTilePositionY;
                if (Math.Min(sx, tx) > 355 || Math.Max(sx, tx) < 444 || sy < 300 || sy > 400 || ty < 300 || ty > 400) return;
                if (!GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(args.UnitId, out GameUnitPathPlanView path) || !path.IsLengthValid || path.CurrentIndex != 0) return;
                Require(tx == pending.Invocation.TileX && ty == pending.Invocation.TileY,
                    "Fresh natural owned target disagrees with its correlated Pre request: owned=" + tx + "," + ty +
                    " preRequest=" + pending.Invocation.TileX + "," + pending.Invocation.TileY);
                double maximum; int turns;
                ValidateFreshCorridor(args.UnitId, global, sx, sy, tx, ty, out maximum, out turns, minimumSteps: false, flatGroundFixture: false);
                bool comparableLeg = MeasureNaturalLeg(args.UnitId, sx, sy, out double localMaximum, out string leg);
                bool birthNewGlobal = !naturalInitialGlobals.Contains(global);
                bool newOccupation = !naturalInitialFarmerGlobals.Contains(global);
                if (newOccupation) naturalFreshRoutes++; else naturalLoadedRoutes++;
                if (naturalReportedGlobals.Add(global))
                    log("natural-worker-route actor=" + args.UnitId + " global=" + global + " birthNewGlobal=" + birthNewGlobal + " newOccupation=" + newOccupation + " owner=" + NaturalOwner + " accelerated=" + accelerated + " job=" + unit->r_LinkedProductionBuildingId +
                        " start=" + sx + "," + sy + " target=" + tx + "," + ty + " steps=" + path.Length + " wholeMaxDeviation=" + maximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " localLeg=" + leg + " localMaxDeviation=" + localMaximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " turns=" + turns);
                if (newOccupation)
                    log("natural-worker-movement actor=" + args.UnitId + " global=" + global + " pre={" + pending.Before + "} post={" + after + "}");
                bool newFarmJob = unit->r_LinkedProductionBuildingId == naturalFarmId &&
                    BuildingAccess.TryGetById(GameBuildingManagerAPI.Instance, naturalFarmId, out GameBuilding* farm, out _) &&
                    farm->r_GlobalId == naturalFarmGlobal && farm->r_BuildingType == eStructs.STRUCT_CATTLEFARM && farm->r_PlayerIdOwner == NaturalOwner;
                if (newOccupation && newFarmJob && accelerated && comparableLeg && localMaximum <= 12 && !naturalNewWorkerPassed)
                {
                    naturalNewWorkerPassed = true;
                    log("natural-new-worker-accepted actor=" + args.UnitId + " global=" + global + " birthNewGlobal=" + birthNewGlobal + " newOccupation=" + newOccupation + " owner=" + NaturalOwner + " farm=" + naturalFarmId + " farmGlobal=" + naturalFarmGlobal +
                        " start=" + sx + "," + sy + " target=" + tx + "," + ty + " steps=" + path.Length + " wholeMaxDeviation=" + maximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " localLeg=" + leg + " localMaxDeviation=" + localMaximum.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " turns=" + turns + " freshPrePost=true accelerated=true");
                }
            }
            catch (Exception failure)
            {
                log("natural-worker-validation-failed actor=" + args.UnitId + " global=" + (pending?.Global ?? 0) +
                    " pre={" + (pending == null ? "missing" : pending.Before.ToString()) + "} post={" + after +
                    "} postArguments=" + args.TileX + "," + args.TileY + " reason=" + failure.Message);
                error = "Natural worker route: " + failure.Message;
            }
        }

        private static unsafe NaturalMovementSnapshot CaptureNaturalMovement(GameUnit* unit) => new NaturalMovementSnapshot {
            CurrentX = unit->r_CurrentTilePositionX, CurrentY = unit->r_CurrentTilePositionY,
            NextX = unit->r_NextTilePositionX2, NextY = unit->r_NextTilePositionY2,
            PreviousX = unit->r_PreviousTilePositionX, PreviousY = unit->r_PreviousTilePositionY,
            Flags = unit->r_PathPlanStateBitFlags, Substep = unit->r_MovementSubstep,
            Index = unit->r_CurrentPathPlanIndex, Length = unit->r_PathPlanLength,
            TargetX = unit->r_TargetTilePositionX, TargetY = unit->r_TargetTilePositionY,
            TargetX2 = unit->r_TargetTilePositionX2, TargetY2 = unit->r_TargetTilePositionY2, AiState = unit->r_AIState
        };
    }
}
