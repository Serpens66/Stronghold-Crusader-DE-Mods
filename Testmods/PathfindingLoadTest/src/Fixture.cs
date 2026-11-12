using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.IO;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingLoadTest
{
    // All engine memory access stays inside AutomationRun.InSimulation; no pointer survives a scope.
    internal sealed partial class Fixture
    {
        private readonly string map;
        private readonly bool startupProbe;
        private readonly bool watchBenchmark;
        private readonly bool wall;
        private readonly int count, width, height;
        private readonly Action<string> log;
        private readonly List<Identity> units = new List<Identity>();
        private int owner, tribe, originX, originY, elevation, targetX, targetY;
        private long commandTicks;
        private int acceptedTargets, acceptedPaths;
        private int maxFreeCandidate;
        private int keepId, keepX, keepY, keepRadius;
        private readonly Dictionary<string, int> regionRejections = new Dictionary<string, int>();
        private struct Identity { internal int Id, X, Y, BeforeTargetX, BeforeTargetY; internal uint Global; }
        private struct Outcome
        {
            internal int Live, Moved, Arrived, TargetChanged, HasPath, Dead, Invalid;
            internal int LookupFailures, GlobalMismatch, OwnerMismatch, TypeMismatch, DetailCount;
            internal string Details;
        }
        private const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void PlayerMove(IntPtr manager, int tribe, int x, int y, int patrol, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        private static PlayerMove move;
        private static IntPtr unitManager;

        internal Fixture(string map, string scenario, Action<string> log)
        {
            this.map = map; this.log = log;
            startupProbe = scenario == "startup-probe";
            watchBenchmark = scenario == "watch-bench";
            wall = scenario.StartsWith("wall-", StringComparison.Ordinal);
            count = scenario.EndsWith("5000", StringComparison.Ordinal) ? 5000 : 1000;
            width = count == 5000 ? 100 : 40; height = count == 5000 ? 60 : 30;
        }
        internal static bool Owns(string scenario) => scenario == "wall-1000" || scenario == "wall-5000" ||
            scenario == "open-1000" || scenario == "open-5000" || scenario == "watch-bench" || scenario == "startup-probe";
        internal IEnumerable<AutomationStep> Steps()
        {
            if (startupProbe)
            {
                yield return new AutomationStep("ordinary-skirmish-start", TimeSpan.FromSeconds(180),
                    run => run.StartSkirmish(map, new[] { AILords.SK_RAT }));
                yield return new AutomationStep("observe-start", TimeSpan.FromSeconds(30), ObserveStartup, true);
                yield break;
            }
            if (watchBenchmark)
            {
                foreach (var step in WatchBenchmark.Steps(log)) yield return step;
                yield break;
            }
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("ready", TimeSpan.FromSeconds(90), Ready, true);
            yield return new AutomationStep("prepare-region", TimeSpan.FromSeconds(120), Region, true);
            yield return new AutomationStep("spawn", TimeSpan.FromSeconds(120), Spawn, true);
            yield return new AutomationStep("initialize-select", TimeSpan.FromSeconds(120), Select, true);
            yield return new AutomationStep("move-observe", TimeSpan.FromSeconds(180), Move, true);
        }
        private IEnumerable<AutomationWait> ObserveStartup(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed and simulation ready",
                () => !MainViewModel.Instance.Show_HUD_Briefing && Director.instance.SimRunning);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            run.InSimulation(run.CurrentMission, () => { StartupProbe.VerifyBuildingSnapshots(log); return 0; });
            log("startup-probe ordinary map start completed; no unit/wall/pinning/ExtremeTroops actions");
        }
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            BindMove(); // Fail before preparing a world if the feature audit does not match.
            // Use the public starter unchanged. After each yielded lobby wait, configure Vanilla
            // before its iterator resumes Play. Reflection observes private settings; it writes none.
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT }))
            {
                yield return wait;
                if (MainViewModel.viewModelLoaded && FRONT_Multiplayer.skirmishGame &&
                    MainViewModel.Instance.Show_MPGameCreation)
                {
                    var lobby = MainViewModel.Instance.FRONTMultiplayer;
                    if (lobby?.currentLobby == null || !lobby.currentLobby.isHost) continue;
                    var field = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
                    var setup = field?.GetValue(lobby) as EngineInterface.MultiplayerSetupData;
                    Require(setup != null, "Vanilla Skirmish setup observation unavailable");
                    if (setup.extreme_troops == 0) lobby.ButtonClicked("Settings_ExtremeTroops");
                    if (MainViewModel.Instance.Show_MP_ExtremeWarning) lobby.ButtonClicked("CloseExtremeWarning");
                    Require(setup.extreme_troops != 0, "Vanilla declined Extreme Troops");
                    TryPinLobbyKeeps(lobby, setup);
                }
            }
        }
        private IEnumerable<AutomationWait> Ready(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            Require(GameData.Instance.lastGameState.chimps_limit == 9999, "Expected Vanilla large allocator (chimps_limit=9999)");
            Director.instance.SetEngineFrameRate(250);
            owner = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetLocalPlayerId());
            Require(owner >= 1 && owner <= GamePlayerManagerAPI.MAX_PLAYERS, "No local player");
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { VerifyPinnedKeeps(); ObserveKeepGuide(); return 0; });
            string peace = run.InSimulation(run.CurrentMission, () => {
                bool available = APIShared.Simulation.PeaceTimeState.TryGetActive(out bool active);
                return "peace-time requested=none nativeAvailable=" + available + " active=" + active;
            });
            log(peace);
            EnableDeathObservation();
            log("ready requested=" + count + " wall=" + wall + " owner=" + owner + " chimps_limit=9999 map=" + map);
        }
        private IEnumerable<AutomationWait> Region(AutomationRun run)
        {
            // At most one candidate per frame. Bounds/corners reject most unsuitable candidates
            // before the complete contiguous-region check; no unbounded search under the monitor.
            bool found = false;
            for (int y = 48; y < 752 && !found; y += 8)
                for (int x = 48; x < 752 && !found; x += 8)
                {
                    int cx = x, cy = y;
                    found = run.InSimulation(run.CurrentMission, () => CheckRegion(cx, cy));
                    if (found) { originX = x; originY = y; }
                    yield return AutomationWait.NextFrame;
                }
            string rejectionSummary = string.Join(",", regionRejections.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            log("region search maxFreeCandidate=" + maxFreeCandidate + " required=" + count + " rejected=[" + rejectionSummary + "]");
            Require(found, "No legal flat terrain region with sufficient vacant spawn sites; required=" + count +
                " maxFreeCandidate=" + maxFreeCandidate + " rejected=[" + rejectionSummary + "]");
            targetX = originX + width + 24; targetY = originY + height / 2;
            run.InSimulation(run.CurrentMission, () => { PrepareWall(); return 0; });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 48);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => { VerifyWall(); RequireTargetFree(); return 0; });
            log("region x=" + originX + " y=" + originY + " w=" + width + " h=" + height + " elevation=" + elevation +
                " target=" + targetX + "," + targetY + " wallVerified=" + wall);
        }
        private bool RejectRegion(string reason)
        {
            regionRejections.TryGetValue(reason, out int previous);
            regionRejections[reason] = previous + 1;
            return false;
        }
        private void ObserveKeepGuide()
        {
            var players = GamePlayerManagerAPI.Instance;
            keepId = players.GetPlayerKeepId(owner);
            Require(keepId > 0, "Own keep reference unavailable");
            var position = players.GetPlayerKeepPosition(owner);
            keepX = position.X; keepY = position.Y;
            int mapSize = GameTileManagerAPI.Instance.GetCurrentMapSize();
            var buildings = GameBuildingManagerAPI.Instance;
            int tableRange = buildings.GetKeepProximityRange(mapSize);
            int overrideRange = buildings.KeepProximityOverride.GetValue();
            keepRadius = overrideRange > 0 ? Math.Min(tableRange, overrideRange) : tableRange;
            Require(keepRadius > 0, "No positive conservative own-keep build range");
            log("keep guide id=" + keepId + " position=" + keepX + "," + keepY + " mapSize=" + mapSize +
                " tableRange=" + tableRange + " overrideRange=" + overrideRange + " conservativeRadius=" + keepRadius +
                " guideOnly=true nativePlacementStillRequired=true");
        }
        private bool WallWithinKeepGuide(int x, int y)
        {
            // Same filter for open/wall: the entire proposed column fits the owner's strict
            // EEF90 range. This does not establish terrain/proximity/resource permission.
            int wallX = x + width + 10;
            int farthest = Math.Max(Math.Abs(wallX - keepX),
                Math.Max(Math.Abs(y - 12 - keepY), Math.Abs(y + height + 12 - keepY)));
            return keepId > 0 && keepRadius > 0 && farthest < keepRadius;
        }
        private bool CheckRegion(int x, int y)
        {
            if (!WallWithinKeepGuide(x, y)) return RejectRegion("keep-range");
            int right = x + width + 28, bottom = y + height + 12, top = y - 12;
            if (!Free(x, top, out int h, false) || !Free(right, top, out int h2, false) ||
                !Free(x, bottom, out int h3, false) || !Free(right, bottom, out int h4, false))
                return RejectRegion("corner-terrain-or-bounds");
            if (h != h2 || h != h3 || h != h4) return RejectRegion("height");
            int available = 0;
            for (int yy = top; yy <= bottom; yy++)
                for (int xx = x; xx <= right; xx++)
                {
                    if (!Free(xx, yy, out h2, false)) return RejectRegion("terrain-or-bounds");
                    if (h != h2) return RejectRegion("height");
                    if (xx < x + width && yy >= y && yy < y + height && Free(xx, yy, out h2)) available++;
                }
            maxFreeCandidate = Math.Max(maxFreeCandidate, available);
            if (available < count) return RejectRegion("spawn-capacity");
            // Keep the control and wall variants' reserved column and target equally vacant.
            for (int yy = top; yy <= bottom; yy++)
                if (!Free(x + width + 10, yy, out h2)) return RejectRegion("occupied-wall-column");
            if (!Free(x + width + 24, y + height / 2, out h2)) return RejectRegion("occupied-target");
            elevation = h;
            return true;
        }
        private static bool Free(int x, int y, out int h, bool requireVacant = true)
        {
            var tiles = GameTileManagerAPI.Instance; h = 0;
            if (!tiles.IsTileInsideMapBounds(x, y)) return false;
            int tile = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(tile) || !tiles.IsTileWalkableAndUnoccupied(tile)) return false;
            // The public walkable helper tests buildings/terrain, not the separate unit layer.
            if (requireVacant && tiles.GetChimpLayer()[tile] != 0) return false;
            if ((tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsBuilding)) != 0) return false;
            h = tiles.GetTileHeight(tile); return true;
        }
        private bool TryNextSpawnSite(ref int cursor, out int x, out int y, out int h)
        {
            while (cursor < width * height)
            {
                int ordinal = cursor++;
                x = originX + ordinal % width; y = originY + ordinal / width;
                Require(Free(x, y, out h, false) && h == elevation, "Spawn terrain changed at " + x + "," + y);
                if (Free(x, y, out h)) return true;
            }
            x = y = h = 0;
            return false;
        }
        private void RequireTargetFree()
        {
            Require(Free(targetX, targetY, out int h) && h == elevation,
                "Destination changed or occupied at " + targetX + "," + targetY);
        }
        private static void ValidateSpawnPosition(Identity identity, int x, int y, HashSet<int> positions)
        {
            Require(identity.X == x && identity.Y == y, "Created unit was projected: id=" + identity.Id +
                " requested=" + x + "," + y + " actual=" + identity.X + "," + identity.Y);
            Require(positions.Add(identity.Y * GameTileManagerAPI.MAX_WIDTH + identity.X), "Created units overlap at " + identity.X + "," + identity.Y);
        }
        private void PrepareWall()
        {
            if (!wall) return;
            var player = GamePlayerManagerAPI.Instance;
            int beforeStone = player.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            int requiredStone = height + 20; // Upper bound: one stone for each verified wall cell.
            int missingStone = Math.Max(0, requiredStone - beforeStone);
            bool added = missingStone == 0 || player.TryAddGood(owner, eGoods.STORED_STONE_BLOCKS, missingStone);
            int afterStone = player.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            log("wall-stock before=" + beforeStone + " after=" + afterStone + " required=" + requiredStone +
                " requestedAdd=" + missingStone + " accepted=" + added);
            Require(afterStone >= requiredStone, "Insufficient actual native stone storage for walls");
            int wx = originX + width + 10, middle = originY + height / 2;
            var api = GameBuildingManagerAPI.Instance;
            api.CreateWall(owner, wx, originY - 12, wx, middle - 3, eMappers.MAPPER_WALL);
            LogWallOutcome("upper", wx, originY - 12, middle - 3);
            api.CreateWall(owner, wx, middle + 3, wx, originY + height + 12, eMappers.MAPPER_WALL);
            LogWallOutcome("lower", wx, middle + 3, originY + height + 12);
        }
        private void VerifyWall()
        {
            var api = GameTileManagerAPI.Instance;
            int wx = originX + width + 10, middle = originY + height / 2, walls = 0;
            for (int y = originY - 12; y <= originY + height + 12; y++)
                for (int x = originX; x <= originX + width + 28; x++)
                {
                    int tile = api.GetTileId(x, y);
                    bool expected = wall && x == wx && (y <= middle - 3 || y >= middle + 3);
                    bool actual = (api.GetTilePropertyFlag(tile) & TilePropertyFlag.IsWall) != 0;
                    Require(actual == expected, "Wall topology differs at " + x + "," + y);
                    if (expected)
                    {
                        Require((api.GetTilePlayerOwnerId(tile) & 7) + 1 == owner && api.GetTileHeight(tile) > elevation,
                            "Wall ownership/height not confirmed"); walls++;
                    }
                }
            log("terrain verified wallCells=" + walls + " gap=5");
        }
        private IEnumerable<AutomationWait> Spawn(AutomationRun run)
        {
            var seen = new HashSet<int>();
            var positions = new HashSet<int>();
            int spawnCursor = 0;
            run.InSimulation(run.CurrentMission, () => {
                int occupied = 0;
                var slots = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                    if (slots[slotIndex].r_AliveState != AliveState.None) occupied++;
                Require(occupied + count <= GameData.Instance.lastGameState.chimps_limit,
                    "Insufficient native slot headroom: occupied=" + occupied + " requested=" + count);
                log("headroom occupied=" + occupied + " requested=" + count + " allocatorIds=9999");
                return 0;
            });
            while (units.Count < count)
            {
                run.InSimulation(run.CurrentMission, () => {
                    int end = Math.Min(count, units.Count + 100);
                    while (units.Count < end)
                    {
                        Require(TryNextSpawnSite(ref spawnCursor, out int x, out int y, out int h),
                            "Spawn sites exhausted: scanned=" + spawnCursor + " created=" + units.Count + " required=" + count);
                        long result = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, h, eChimps.CHIMP_TYPE_SPEARMAN);
                        Require(result > 0 && result <= GameData.Instance.lastGameState.chimps_limit && seen.Add((int)result),
                            "Spawn failed/duplicate; created=" + units.Count);
                        Identity identity = Capture((int)result);
                        ValidateSpawnPosition(identity, x, y, positions);
                        units.Add(identity);
                        TrackCreated(identity);
                    }
                    return 0;
                });
                log("created=" + units.Count + "/" + count + " scannedSites=" + spawnCursor + "/" + (width * height));
                yield return AutomationWait.NextFrame;
            }
            Require(units.Count == count, "Incomplete creation");
            LogInitialHealth();
        }
        private unsafe Identity Capture(int id)
        {
            Require(UnitAccess.TryGetById(id, out GameUnit* u, out _) && u->r_GlobalId != 0 &&
                u->r_ControllableForPlayerId == owner && u->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN, "Created identity/type/owner invalid");
            return new Identity { Id = id, Global = u->r_GlobalId, X = u->r_CurrentTilePositionX, Y = u->r_CurrentTilePositionY,
                BeforeTargetX = u->r_TargetTilePositionX, BeforeTargetY = u->r_TargetTilePositionY };
        }
        private IEnumerable<AutomationWait> Select(AutomationRun run)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("all created units initialized", () => run.InSimulation(run.CurrentMission, AllLive));
            int[] requested = units.Select(u => u.Id).ToArray();
            run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.SetSelectedChimps(requested.ToList()));
            yield return run.WaitForTicks(run.CurrentMission, 4);
            yield return AutomationWait.Until("exact native selection and tribe", () => run.InSimulation(run.CurrentMission, SelectionMatches));
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("selection/group verified count=" + count + " tribe=" + tribe);
        }
        private unsafe bool AllLive()
        {
            foreach (var identity in units)
            {
                if (!UnitAccess.TryGetById(identity.Id, out GameUnit* unit, out _) || unit->r_GlobalId != identity.Global)
                    throw new InvalidOperationException("Spawn identity lost");
                Require(unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN && unit->r_ControllableForPlayerId == owner, "Ownership/type changed");
                if (!UnitAccess.IsReallyAlive(unit)) return false;
            }
            return true;
        }
        private unsafe bool SelectionMatches()
        {
            if (!AllLive()) return false;
            var expected = new HashSet<int>(units.Select(u => u.Id));
            // Completed local PlayState, not the Extender's Steam-roster-based getter:
            // an ordinary local Skirmish need not have a Steam participant slot.
            if (!LocalSelectionAPI.TryCapture(owner, out LocalSelectionSnapshot selected) || selected.Count != count) return false;
            var selectedIds = new HashSet<int>();
            for (int index = 0; index < selected.Count; index++) selectedIds.Add(selected[index].UnitId);
            if (!expected.SetEquals(selectedIds)) return false;
            if (!UnitAccess.TryGetById(units[0].Id, out GameUnit* first, out _)) return false;
            int candidate = first->r_TribeId;
            if (candidate <= 0 || !GameTribeManagerAPI.Instance.TryGetTribeById(candidate, out GameTribe* record) ||
                record->r_AliveState != AliveState.IsAlive || record->r_PlayerIdOwner != owner || record->r_UnitsInGroup != count) return false;
            var members = new List<int>();
            if (!GameTribeManagerAPI.Instance.GetUnits(candidate, members) || members.Count != count || !expected.SetEquals(members)) return false;
            foreach (var identity in units)
            {
                if (!UnitAccess.TryGetById(identity.Id, out GameUnit* u, out _)) return false;
                if (u->r_TribeId != candidate) return false;
            }
            tribe = candidate; return true;
        }
        private IEnumerable<AutomationWait> Move(AutomationRun run)
        {
            BindMove();
            run.InSimulation(run.CurrentMission, () => {
                RequireTargetFree();
                Require(SelectionMatches(), "Selection/group changed before command");
                for (int i = 0; i < units.Count; i++) units[i] = Capture(units[i].Id);
                var clock = Stopwatch.StartNew();
                move(unitManager, tribe, targetX, targetY, 0, 0);
                clock.Stop(); commandTicks = clock.ElapsedTicks;
                var immediate = Observe(); acceptedTargets = immediate.TargetChanged; acceptedPaths = immediate.HasPath;
                log("command count=" + count + " elapsedMs=" + clock.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    " changedTargets=" + acceptedTargets + " paths=" + acceptedPaths + " skippedOrUnchanged=" + (count - acceptedTargets));
                return 0;
            });
            Require(acceptedTargets > 0, "No retained member changed target after the command");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            for (int sample = 1; sample <= 6; sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 100);
                var state = run.InSimulation(run.CurrentMission, Observe);
                log("outcome sample=" + sample + " live=" + state.Live + " moved=" + state.Moved + " arrived=" + state.Arrived +
                    " targetChanged=" + state.TargetChanged + " hasPath=" + state.HasPath + " dead=" + state.Dead + " invalid=" + state.Invalid +
                    " lookupFailures=" + state.LookupFailures + " globalMismatch=" + state.GlobalMismatch +
                    " ownerMismatch=" + state.OwnerMismatch + " typeMismatch=" + state.TypeMismatch +
                    " details=[" + state.Details + "]");
                Require(state.Invalid == 0 && state.Dead == 0 && state.Live == count, "Fixture units died or identities changed");
                if (sample == 6) Require(state.Moved > 0, "No retained unit progressed after 600 observed ticks");
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("complete count=" + count + " wall=" + wall + " commandStopwatchTicks=" + commandTicks);
        }
        private unsafe Outcome Observe()
        {
            var result = new Outcome();
            foreach (var identity in units)
            {
                if (!UnitAccess.TryGetById(identity.Id, out GameUnit* u, out var lookupFailure))
                {
                    result.Invalid++; result.LookupFailures++;
                    RecordLookupFailure(ref result, identity, "lookup:" + lookupFailure);
                    continue;
                }
                bool globalMismatch = u->r_GlobalId != identity.Global;
                bool ownerMismatch = u->r_ControllableForPlayerId != owner;
                bool typeMismatch = u->r_UnitChimp != eChimps.CHIMP_TYPE_SPEARMAN;
                if (globalMismatch || ownerMismatch || typeMismatch)
                {
                    result.Invalid++;
                    if (globalMismatch) result.GlobalMismatch++;
                    if (ownerMismatch) result.OwnerMismatch++;
                    if (typeMismatch) result.TypeMismatch++;
                    RecordFailure(ref result, identity, u, "identity(global=" + globalMismatch + ",owner=" + ownerMismatch + ",type=" + typeMismatch + ")");
                    continue;
                }
                if (!UnitAccess.IsReallyAlive(u))
                { result.Dead++; RecordFailure(ref result, identity, u, "not-really-alive"); continue; }
                result.Live++;
                if (u->r_CurrentTilePositionX != identity.X || u->r_CurrentTilePositionY != identity.Y) result.Moved++;
                if (Math.Abs(u->r_CurrentTilePositionX - targetX) <= 3 && Math.Abs(u->r_CurrentTilePositionY - targetY) <= 3) result.Arrived++;
                if (u->r_TargetTilePositionX != identity.BeforeTargetX || u->r_TargetTilePositionY != identity.BeforeTargetY) result.TargetChanged++;
                if (u->r_PathPlanLength > 0) result.HasPath++;
            }
            return result;
        }
        private unsafe void RecordFailure(ref Outcome result, Identity identity, GameUnit* unit, string reason)
        {
            // Per-sample bounded diagnostic, after the command stopwatch; it must not hide
            // identity changes behind a generic death label or retain a native pointer.
            if (result.DetailCount >= 8) return;
            result.DetailCount++;
            string actual = unit == null ? "actual=unavailable" :
                "actualGlobal=" + unit->r_GlobalId + " actualOwner=" + unit->r_ControllableForPlayerId +
                " actualType=" + unit->r_UnitChimp + " alive=" + unit->r_AliveState +
                " killed=" + unit->r_IsKilledByProjectile + " tribe=" + unit->r_TribeId +
                " pos=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY +
                " target=" + unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY +
                " pathLength=" + unit->r_PathPlanLength;
            result.Details += (result.DetailCount == 1 ? "" : " ; ") + "id=" + identity.Id + " reason=" + reason +
                " expectedGlobal=" + identity.Global + " expectedOwner=" + owner +
                " expectedType=" + eChimps.CHIMP_TYPE_SPEARMAN + " start=" + identity.X + "," + identity.Y + " " + actual;
        }
        private unsafe void RecordLookupFailure(ref Outcome result, Identity identity, string reason)
        {
            if (result.DetailCount >= 8) return;
            try
            {
                var slots = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                int slotIndex = identity.Id - 1; // Game IDs are one-based; this view is zero-based.
                if ((uint)slotIndex < (uint)slots.Length)
                {
                    // Diagnostic snapshot only; it never grants identity/life/lookup validity.
                    GameUnit raw = slots[slotIndex];
                    RecordFailure(ref result, identity, &raw, reason + ",raw-slot");
                    return;
                }
            }
            catch (Exception error) { reason += ",raw-slot-unavailable:" + error.GetType().Name; }
            RecordFailure(ref result, identity, null, reason);
        }
        private static void BindMove()
        {
            if (move != null) return;
            string file = Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var stream = File.OpenRead(file)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == Hash, "Unknown native image");
            IntPtr image = GetModuleHandle("CrusaderDE.dll");
            Require(image != IntPtr.Zero, "Native image not loaded");
            // Same hash-bound, six-argument ordinary order entry as UiAcceptance.ProbeSteps.
            // Call through Pathfinding's installed detour; never compare/overwrite its live entry.
            unitManager = new IntPtr(image.ToInt64() + 0x67E8400);
            move = Marshal.GetDelegateForFunctionPointer<PlayerMove>(new IntPtr(image.ToInt64() + 0x196100));
        }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }
}
