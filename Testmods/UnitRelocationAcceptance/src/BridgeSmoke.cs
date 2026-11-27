using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Automation;
using APIShared.NativeSites;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UnitRelocationAcceptance
{
    internal static partial class Fixture
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteAivFrame(ulong system, int player, int index, int restricted, byte freeOrForced);
        private static int bridgeOwner, bridgeActor, bridgeX, bridgeY, gateId, goalX, goalY;
        private static uint gateGlobal;
        private static Actor bridgeOriginal, bridgeRecovered;
        private static volatile bool waitBridgeOutcome;
        private static object observer;
        private static long capturedBefore, attemptsBefore, rescuedBefore, bridgeCapturedDelta, bridgeAttemptsDelta, bridgeRescuedDelta;
        private static bool bridgeOccupancyVerified;

        private static IEnumerable<AutomationWait> AutomaticBridge(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => { PrepareBridge(); return true; });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 20);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            foreach (var wait in Stage(run, BuildBridgeOverOriginal)) yield return wait;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("first qualified automatic bridge recovery tick", () =>
            { if (error != null) throw error; return !waitBridgeOutcome; });
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () =>
            {
                Actor after = bridgeRecovered; RequireSame(bridgeOriginal, after, false, "automatic bridge rescue");
                Require(after.Tile != bridgeOriginal.Tile && PhysicalExit(after.X, after.Y), "automatic observer did not free original bridge occupant");
                Require(bridgeCapturedDelta > 0 && bridgeAttemptsDelta > 0 && bridgeRescuedDelta == 1,
                    "production observer counters did not qualify this deferred original rescue");
                Require(bridgeOccupancyVerified, "first recovery tick did not verify original source/destination occupancy");
                goalX = bridgeX + 8; goalY = bridgeY + 8;
                Require(Ground(goalX, goalY), "post-rescue movement endpoint is no longer ordinary and empty");
                OrderBridgeActor(goalX, goalY);
                Case("automatic-aiv-rescue", "same original, health, tribe; moved by production observer only");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            var deadline = Stopwatch.StartNew(); bool arrived = false;
            while (!arrived)
            {
                yield return run.WaitForTicks(run.CurrentMission, 4);
                arrived = run.InSimulation(run.CurrentMission, () =>
                {
                    Actor now = Read(bridgeActor); RequireSame(bridgeOriginal, now, false, "post-rescue native movement");
                    return now.X == goalX && now.Y == goalY;
                });
                Require(deadline.Elapsed.TotalSeconds < 45, "rescued original did not reach the ordinary movement endpoint");
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Case("automatic-rescue-followup-move", "physically reached original native command endpoint");
        }

        private static unsafe void PrepareBridge()
        {
            Require(api.TryGetAivBuildStep(out _, out var diagnostic), "audited shared AIV owner unavailable: " + diagnostic);
            Require(NativeSiteRegistry.TryGetWriter(0x51790, out var writer) && writer.OwnerGuid == "APIShared_Serp",
                "AIV call does not enter the expected shared owner");
            var claim = NativeSiteRegistry.Register(Plugin.Guid, "acceptance.aiv-frame-call", 0x51790, 16, NativeSiteMode.CallOnly);
            Require(claim.Succeeded, "AIV call-only registration refused: " + claim.Reason);
            var players = GamePlayerManagerAPI.Instance;
            bridgeOwner = 0;
            for (int candidate = 1; candidate <= GamePlayerManagerAPI.MAX_PLAYERS; candidate++)
                if (players.IsAIPlayer(candidate) && players.GetPlayerKeepId(candidate) > 0) { bridgeOwner = candidate; break; }
            Require(bridgeOwner != 0, "actual AI owner unavailable");
            var center = players.GetPlayerKeepPosition(bridgeOwner);
            int cx = 0, cy = 0; bool found = false;
            for (int radius = 24; radius < 180 && !found; radius += 16)
                for (int dy = -radius; dy <= radius && !found; dy += 16)
                    for (int dx = -radius; dx <= radius && !found; dx += 16)
                    {
                        int x = center.X + dx, y = center.Y + dy;
                        if (!ClearBridgeSite(x, y)) continue;
                        cx = x; cy = y; found = true;
                    }
            Require(found, "no verified clear flat gate/bridge site");
            var tiles = GameTileManagerAPI.Instance;
            GameBuildingManagerAPI.Instance.CreatePrefab(bridgeOwner, cx, cy, eMappers.MAPPER_GATE_MAIN,
                BuildingScales.GetScale(eMappers.MAPPER_GATE_MAIN), 81, true, true);
            gateId = tiles.GetTileBuildingId(tiles.GetTileId(cx, cy));
            Require(BuildingAccess.TryGetById(gateId, out GameBuilding* gate, out _) && gate != null &&
                gate->r_PlayerIdOwner == bridgeOwner && gate->r_BuildingType == eStructs.STRUCT_GATE_MAIN && gate->r_SpriteVariationIndex == 81,
                "actual support gate was not built with audited sprite81");
            gateGlobal = gate->r_GlobalId; bridgeX = cx + 1; bridgeY = cy + 7;
            foreach (int dx in new[] { 2, 1, 3, 0, 4, -1, 5 })
                Require(tiles.GetTileBuildingId(tiles.GetTileId(bridgeX + dx, bridgeY - 1)) == gateId,
                    "native bridge preflight gate support does not match");
            int actorY = bridgeY + 4;
            bridgeActor = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(bridgeOwner, bridgeOwner, bridgeX, actorY,
                tiles.GetTileHeight(tiles.GetTileId(bridgeX, actorY)), eChimps.CHIMP_TYPE_SPEARMAN));
            Require(bridgeActor >= GameUnitManager.FirstLiveUnitId && bridgeActor <= GameUnitManager.LastLiveUnitId, "bridge actor spawn refused");
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(bridgeOwner));
            Require(tribe > 0 && GameTribeManagerAPI.Instance.AssignUnit(tribe, bridgeActor), "bridge actor tribe assignment failed");
            var main = BepInEx.Bootstrap.Chainloader.PluginInfos[MainGuid].Instance.GetType().Assembly;
            var hostType = main.GetType("BugfixesAndQoL.TrappedUnitRescue.TrappedUnitRescueFeature", true);
            object host = hostType.GetField("processOwner", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Require(host != null && (bool)hostType.GetProperty("Active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host, null),
                "actual production rescue host is inactive");
            observer = hostType.GetProperty("GeneralObserver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host, null);
            Require(observer != null, "actual general observer unavailable");
            Info("AIV_SMOKE_SETUP owner=" + bridgeOwner + " gate=" + gateId + "/" + gateGlobal + " anchor=" + bridgeX + "," + bridgeY +
                " actor=" + bridgeActor + " realAI=true realGate=true frameRestored=true noDirectActorStateWrites=true");
        }

        private static bool ClearBridgeSite(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance; int height = -1;
            for (int yy = y - 2; yy <= y + 17; yy++) for (int xx = x - 2; xx <= x + 13; xx++)
            {
                if (!Ground(xx, yy)) return false;
                int h = tiles.GetTileHeight(tiles.GetTileId(xx, yy));
                if (height < 0) height = h; else if (h != height) return false;
            }
            return true;
        }

        private static unsafe void BuildBridgeOverOriginal()
        {
            bridgeOriginal = Read(bridgeActor);
            Require(UnitAccess.TryGetById(bridgeActor, out GameUnit* u, out _) && u != null && u->r_PathPlanStateBitFlags == 0 &&
                u->r_MovementSubstep == 8 && (u->r_AIState == 0 || u->r_AIState == 1 || u->r_AIState == 2),
                "bridge actor has no audited stationary idle precondition");
            VerifyRelocation(bridgeOriginal, bridgeX, bridgeY + 4, "bridge-actor-staging");
            bridgeOriginal = Read(bridgeActor);
            Require(PhysicalExit(bridgeOriginal.X, bridgeOriginal.Y), "bridge source was not physically passable before write");
            capturedBefore = Count("CaptureCount"); attemptsBefore = Count("AttemptCount"); rescuedBefore = Count("RescueCount");
            Require(InvokeBridgeFrame(1) == 0, "restricted AIV control did not reject drawbridge");
            RequireSame(bridgeOriginal, Read(bridgeActor), true, "restricted AIV control");
            Require(InvokeBridgeFrame(0) == 1, "real unrestricted AIV frame did not construct drawbridge");
            Actor trapped = Read(bridgeActor); RequireSame(bridgeOriginal, trapped, true, "unsuppressed AIV construction");
            int b = GameTileManagerAPI.Instance.GetTileBuildingId(trapped.Tile);
            Require(BuildingAccess.TryGetById(b, out GameBuilding* bridge, out _) && bridge != null &&
                bridge->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE && !PhysicalExit(trapped.X, trapped.Y),
                "actual drawbridge did not produce the expected physical trap");
            Require(Count("RescueCount") == rescuedBefore && Count("AttemptCount") == attemptsBefore,
                "construction performed recovery synchronously instead of the deferred tick");
            waitBridgeOutcome = true;
            Info("AIV_ACTUAL_TRAP actor=" + trapped.Id + "/" + trapped.Global + " at=" + trapped.X + "," + trapped.Y +
                " realNative51790=true nativeReturned1=true noSyntheticQueue=true originalConstructionUnsuppressed=true");
        }

        private static unsafe int InvokeBridgeFrame(int restricted)
        {
            var aiv = GameAIVManagerAPI.Instance;
            Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(bridgeOwner, out GamePlayerResources* player) && player != null,
                "AI resources unavailable");
            int slot = checked((int)player->r_AIVVillageSlotIndex), index = AivVillageState.BUILD_STEP_CAPACITY - 1;
            AivVillageState* village = null; AivBuildStep* frame = null;
            Require(slot >= AivSystem.FIRST_LIVE_VILLAGE_SLOT && slot <= AivSystem.LAST_LIVE_VILLAGE_SLOT &&
                aiv.TryGetVillageBySlot(slot, out village) && village != null && village->OwnerPlayerId == bridgeOwner &&
                aiv.TryGetBuildStep(slot, index, out frame) && frame != null,
                "actual AI village/frame unavailable");
            Require(BuildingAccess.TryGetById(gateId, out GameBuilding* gate, out _) && gate != null && gate->r_GlobalId == gateGlobal,
                "support gate lifetime replaced");
            Require(index > village->MaximumBuildStep && frame->State == AivBuildStepState.Inactive, "reserved last AIV frame is not unused");
            AivBuildStep saved = *frame;
            int headerSize = Marshal.OffsetOf(typeof(AivVillageState), nameof(AivVillageState.BuildStepsBuffer)).ToInt32();
            var header = new byte[headerSize]; Marshal.Copy((IntPtr)village, header, 0, headerSize);
            int orderedCount = village->OrderedMapTileCount; int result;
            try
            {
                *frame = new AivBuildStep { State = AivBuildStepState.Pending, BuildingType = eMappers.MAPPER_DRAWBRIDGE, TileCount = 1,
                    MapTileIdOrBufferIndex = GameTileManagerAPI.Instance.GetTileId(bridgeX, bridgeY) };
                IntPtr module;
                using (var process = Process.GetCurrentProcess())
                    module = process.Modules.Cast<ProcessModule>().Single(m => m.ModuleName.Equals("CrusaderDE.dll", StringComparison.OrdinalIgnoreCase)).BaseAddress;
                var invoke = Marshal.GetDelegateForFunctionPointer<ExecuteAivFrame>(module + 0x51790);
                result = invoke((ulong)aiv.GetAIVSystemPointer(), bridgeOwner, index, restricted, 1);
            }
            finally { *frame = saved; }
            var after = new byte[headerSize]; Marshal.Copy((IntPtr)village, after, 0, headerSize);
            Require(header.SequenceEqual(after) && village->OrderedMapTileCount == orderedCount,
                "AIV frame invocation changed village header/cursor/count");
            return result;
        }

        private static void ObserveBridgeOutcome()
        {
            if (!waitBridgeOutcome) return;
            Actor after = Read(bridgeActor);
            if (after.Tile == bridgeOriginal.Tile) return;
            // The production callback runs earlier in this very tick. Capture the first
            // post-recovery identity before unrelated later AI updates can reassign a tribe.
            Require(Count("RescueCount") == rescuedBefore + 1 && PhysicalExit(after.X, after.Y),
                "original escaped without the expected production recovery accounting");
            RequireSame(bridgeOriginal, after, false, "first automatic recovery tick");
            Require(Occurrences(after.Tile, bridgeActor) == 1 && Occurrences(bridgeOriginal.Tile, bridgeActor) == 0,
                "first automatic recovery tick retained invalid tile linkage");
            bridgeCapturedDelta = Count("CaptureCount") - capturedBefore;
            bridgeAttemptsDelta = Count("AttemptCount") - attemptsBefore;
            bridgeRescuedDelta = Count("RescueCount") - rescuedBefore;
            bridgeOccupancyVerified = true; bridgeRecovered = after; waitBridgeOutcome = false;
            Info("AIV_FIRST_RECOVERY actor=" + after.Id + "/" + after.Global + " at=" + after.X + "," + after.Y +
                " tribe=" + after.Tribe + "/" + after.TribeGlobal + " actualProductionRescues=1");
        }

        private static unsafe void OrderBridgeActor(int x, int y)
        {
            // The surrounding monitored step has already checked the original GID/health/tribe.
            if (APIShared.UnitAccess.TryGetById(bridgeActor, out GameUnit* live, out _))
                GameUnitManagerAPI.Instance.MoveToTile(bridgeActor, x, y);
            else throw new InvalidOperationException("Original bridge actor vanished before its command");
        }

        private static long Count(string property) => (long)observer.GetType().GetProperty(property,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(observer, null);

        private static bool PhysicalExit(int x, int y)
        {
            var p = GamePathingManagerAPI.Instance;
            var directions = new[] {
                Tuple.Create(PackedPathDirection.North,0,-1,PackedPathDirection.South),
                Tuple.Create(PackedPathDirection.NorthEast,1,-1,PackedPathDirection.SouthWest),
                Tuple.Create(PackedPathDirection.East,1,0,PackedPathDirection.West),
                Tuple.Create(PackedPathDirection.SouthEast,1,1,PackedPathDirection.NorthWest),
                Tuple.Create(PackedPathDirection.South,0,1,PackedPathDirection.North),
                Tuple.Create(PackedPathDirection.SouthWest,-1,1,PackedPathDirection.NorthEast),
                Tuple.Create(PackedPathDirection.West,-1,0,PackedPathDirection.East),
                Tuple.Create(PackedPathDirection.NorthWest,-1,-1,PackedPathDirection.SouthEast) };
            foreach (var d in directions)
                if (p.HasPathEdge(x, y, d.Item1) || p.HasPathEdge(x + d.Item2, y + d.Item3, d.Item4)) return true;
            return false;
        }
    }
}
