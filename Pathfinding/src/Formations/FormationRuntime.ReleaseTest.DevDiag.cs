using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Pathfinding.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private ReleaseTrial releaseTrial;

        internal void TryStartFormationReleaseTest()
        {
            string path = Path.Combine(BepInEx.Paths.ConfigPath, "SerpAutomation", PathfindingPlugin.PluginGuid + ".run");
            if (!File.Exists(path))
                return;

            try
            {
                string[] request = File.ReadAllLines(path);
                File.Delete(path);
                if (request.Length != 2 || request[1] != "formation-release" ||
                    !Guid.TryParseExact(request[0], "N", out Guid runId) || runId == Guid.Empty)
                    throw new InvalidDataException("Expected a run GUID and formation-release.");

                ApiShared.ForMod(PathfindingPlugin.PluginGuid).Automation.Start(runId, new[]
                {
                    new AutomationStep("start-crater-lake", TimeSpan.FromMinutes(2), StartFormationTestMap),
                    new AutomationStep("formation-release", TimeSpan.FromMinutes(3), ExerciseFormationRelease, missionBound: true)
                }, log, quitWhenDone: true);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "FORMATION_TEST_START_FAILED: " + ex);
            }
        }

        private static IEnumerable<AutomationWait> StartFormationTestMap(AutomationRun run)
        {
            foreach (var wait in run.StartSkirmish("Crater Lake.map", new[] { AILords.SK_RAT }))
                yield return wait;
            var mission = run.CurrentMission;
            if (mission.Mode.IsRealMultiplayer || !mission.Mode.IsSingleplayerSkirmish ||
                !string.Equals(Path.GetFileNameWithoutExtension(mission.FilePath), "Crater Lake", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The host-only test requires a local Crater Lake skirmish.");
            yield return AutomationWait.Until("skirmish briefing", () => MainViewModel.Instance.Show_HUD_Briefing);
            MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return run.WaitForPlayState(mission);
            yield return run.WaitForTicks(mission, 8);
        }

        private IEnumerable<AutomationWait> ExerciseFormationRelease(AutomationRun run)
        {
            var mission = run.CurrentMission;
            if (!LocalInputEnabled || !HasValidMap())
                throw new InvalidOperationException("Formation input is unavailable.");
            IntPtr window;
            using (var process = Process.GetCurrentProcess())
                window = process.MainWindowHandle;
            RequireForeground(window);
            if (!GetClientRect(window, out ClientRect bounds))
                throw new InvalidOperationException("Cannot read the game client bounds.");
            var center = new ScreenPoint { X = bounds.Right / 2, Y = bounds.Bottom / 2 };
            if (!ClientToScreen(window, ref center))
                throw new InvalidOperationException("Cannot map the game client to desktop coordinates.");

            var anchors = new List<(ScreenPoint Cursor, GroundTarget Target)>(3);
            ushort component = 0;
            // Probe real game cursor coordinates so camera rotation, zoom and DPI are reflected in the map hit.
            for (int row = -2; row <= 2 && anchors.Count < 3; row++)
            for (int column = -2; column <= 2 && anchors.Count < 3; column++)
            {
                var point = center.Offset(column * bounds.Right / 7, row * bounds.Bottom / 7);
                SetTestCursor(window, point);
                yield return AutomationWait.NextFrame;
                if (FatControler.instance.overNoesisGUI() || !TryCaptureCommandTarget(out var target, out _))
                    continue;
                ushort found = run.InSimulation(mission, () => ReadFreeTestTileComponent(target));
                if (found == 0 || (component != 0 && found != component) ||
                    anchors.Any(anchor => anchor.Target.Equals(target)))
                    continue;
                component = found;
                anchors.Add((point, target));
            }
            if (anchors.Count < 3)
                throw new InvalidOperationException("Could not find three distinct visible free tiles on one path component.");

            var unitIds = run.InSimulation(mission, () => CreateReleaseTestGroup(
                anchors[0].Target, component, anchors.Select(anchor => anchor.Target).ToArray()));
            yield return run.WaitForTicks(mission, 8);
            GamePlayerManagerAPI.Instance.SetSelectedChimps(unitIds);
            SelectionIdentity[] selection = null;
            uint tribeGlobalId = 0;
            int tribeId = 0;
            yield return AutomationWait.Until("the three test swordsmen selected in one tribe", () =>
            {
                RequireForeground(window);
                return run.InSimulation(mission, () =>
                {
                    if (!TryCaptureReleaseTestGroup(unitIds, out var units, out int selectedTribe, out uint globalId))
                        return false;
                    selection = units;
                    tribeId = selectedTribe;
                    tribeGlobalId = globalId;
                    return true;
                });
            });

            Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_SETUP tribe={tribeId}/{tribeGlobalId} " +
                $"units={string.Join(",", unitIds)} ground={string.Join(" ", anchors.Select(a => $"{a.Target.NativeX},{a.Target.NativeY}"))}");

            foreach (bool followup in new[] { false, true })
            {
                var trial = new ReleaseTrial(tribeId);
                string phase = followup ? "explicit-followup" : "single-release";
                bool left = GetCommandMouseButton() == 0;
                int button = left ? 0 : 1;
                bool pressed = false;
                try
                {
                    var anchor = anchors[followup ? 1 : 0];
                    SetTestCursor(window, anchor.Cursor);
                    yield return AutomationWait.NextFrame;
                    GroundTarget start = ConfirmTestAnchor(run, mission, anchor.Target, component);
                    Volatile.Write(ref releaseTrial, trial);
                    long priorInputAttempt;
                    lock (stateSync) priorInputAttempt = inputAttempt;
                    SendTestMouse(window, left, down: true);
                    pressed = true;
                    Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_INPUT phase={phase} down button={button} tile={start.NativeX},{start.NativeY}");
                    yield return WaitForTestInput(window, "Unity reports the command mouse button down", () => Input.GetMouseButton(button));
                    yield return WaitForTestInput(window, "formation drag starts", () =>
                    {
                        lock (stateSync)
                        {
                            if (drag == null || !drag.Selection.Select(unit => unit.GlobalId)
                                .SequenceEqual(selection.Select(unit => unit.GlobalId)))
                                return false;
                            Volatile.Write(ref trial.TribeId, drag.TribeId);
                            return true;
                        }
                    }, () => DescribeTestDragState(priorInputAttempt));
                    tribeId = Volatile.Read(ref trial.TribeId);
                    int actualTribe = tribeId;
                    tribeGlobalId = run.InSimulation(mission, () => GetReleaseTestTribeIdentity(actualTribe));
                    Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_DRAG phase={phase} tribe={tribeId}/{tribeGlobalId}");
                    // Vanilla can authorize a new gesture only while the cursor still hits its pressed tile.
                    yield return WaitForTestInput(window, "native feedback authorizes the pressed tile", () =>
                    {
                        lock (stateSync)
                            return drag != null && drag.TribeId == tribeId && drag.Authorization.IsConfirmed;
                    }, () => DescribeTestDragState(priorInputAttempt));
                    SetTestCursor(window, anchors[followup ? 2 : 1].Cursor);
                    yield return WaitForTestInput(window, "authorized explicit formation preview", () =>
                    {
                        lock (stateSync)
                            return drag != null && drag.TribeId == tribeId && drag.Authorization.IsConfirmed &&
                                drag.HasPreviewPlan && HasExplicitDirection(drag);
                    }, () => DescribeTestDragState(priorInputAttempt));
                    SendTestMouse(window, left, down: false);
                    pressed = false;
                    Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_INPUT phase={phase} drag={start.NativeX},{start.NativeY}");
                    yield return WaitForTestInput(window, "formation release consumed", () => Volatile.Read(ref trial.OperationId) > 0);
                    GroundTarget next = default;
                    if (followup)
                    {
                        SetTestCursor(window, anchors[2].Cursor);
                        yield return AutomationWait.NextFrame;
                        next = ConfirmTestAnchor(run, mission, anchors[2].Target, component);
                        SendTestMouse(window, left, down: true);
                        pressed = true;
                        yield return WaitForTestInput(window, "Unity reports the follow-up button down", () => Input.GetMouseButton(button));
                        SendTestMouse(window, left, down: false);
                        pressed = false;
                        Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_INPUT phase={phase} followup={next.NativeX},{next.NativeY}");
                    }
                    yield return run.WaitForTicks(mission, 24);
                    RequireForeground(window);
                    var orders = trial.Orders.ToArray();
                    var posts = orders.Where(order => order.Phase == EventHookPhase.Post && order.Result == 1).ToArray();
                    foreach (var order in orders)
                        Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_ORDER phase={phase} operation={trial.OperationId} {order}");
                    var targets = run.InSimulation(mission, () => DescribeTestTargets(selection));
                    Shared.DebugLogHelper.LogInfo(log,
                        $"FORMATION_TEST_TARGETS phase={phase} operation={trial.OperationId} tribe={tribeId}/{tribeGlobalId} {targets}");
                    if (Volatile.Read(ref trial.SampleCount) > ReleaseTrial.SampleLimit ||
                        !posts.Any(order => order.X == start.NativeX && order.Y == start.NativeY))
                        throw new InvalidOperationException("Incomplete group-order evidence.");
                    if (followup && (posts.Length < 2 || !posts.Any(order => order.X == next.NativeX && order.Y == next.NativeY)))
                        throw new InvalidOperationException("The deliberate follow-up did not produce a distinct successful order.");
                    Shared.DebugLogHelper.LogInfo(log, $"FORMATION_TEST_RESULT phase={phase} operation={trial.OperationId} " +
                        $"successfulPosts={posts.Length} result={(followup ? "followup-observed" : posts.Length > 1 ? "second-execution-observed" : "not-reproduced")}");
                }
                finally
                {
                    Volatile.Write(ref releaseTrial, null);
                    if (pressed)
                        SendMouse(left, down: false);
                }
            }
        }

        private static AutomationWait WaitForTestInput(IntPtr window, string stage, Func<bool> observed,
            Func<string> describeFailure = null)
        {
            var timer = Stopwatch.StartNew();
            return AutomationWait.Until(stage, () =>
            {
                RequireForeground(window);
                if (observed()) return true;
                if (timer.Elapsed.TotalSeconds >= 6)
                    throw new InvalidOperationException("Missing formation test input or feedback: " + stage +
                        (describeFailure == null ? string.Empty : ". " + describeFailure()));
                return false;
            });
        }

        private string DescribeTestDragState(long priorInputAttempt)
        {
            lock (stateSync)
                return $"keyDownAttempts={inputAttempt - priorInputAttempt} enabled={LocalInputEnabled} " +
                    $"mode={EvaluateCommandMode()} marker={markerRenderer?.ReplacementAvailable} " +
                    $"overUi={FatControler.instance.overNoesisGUI()} shift={IsShiftHeld()} pending={pendingStart != null} " +
                    $"dragTribe={drag?.TribeId} authorized={drag?.Authorization.IsConfirmed} " +
                    $"preview={drag?.HasPreviewPlan} direction={drag?.Geometry.ExplicitDirection}";
        }

        private static bool TryCaptureReleaseTestGroup(List<int> expected, out SelectionIdentity[] units,
            out int tribeId, out uint globalId)
        {
            globalId = 0;
            if (!TryCaptureSelection(out units, out tribeId) ||
                !units.Select(unit => unit.UnitId).SequenceEqual(expected))
                return false;
            globalId = GetReleaseTestTribeIdentity(tribeId);
            return globalId != 0;
        }

        private static uint GetReleaseTestTribeIdentity(int tribeId) =>
            GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null
                ? tribe->r_GlobalId : 0;

        private static List<int> CreateReleaseTestGroup(GroundTarget nearby, ushort component, GroundTarget[] reserved)
        {
            var players = GamePlayerManagerAPI.Instance;
            int owner = players.GetLocalPlayerId();
            if (!players.IsPlayerIdValid(owner) || players.IsAIPlayer(owner))
                throw new InvalidOperationException("No active local human player for the test group.");

            var tiles = GameTileManagerAPI.Instance;
            var positions = new List<GroundTarget>(3);
            for (int dy = -8; dy <= 8 && positions.Count < 3; dy++)
            for (int dx = -8; dx <= 8 && positions.Count < 3; dx++)
            {
                var candidate = new GroundTarget(nearby.NativeX + dx, nearby.NativeY + dy);
                if (ReadFreeTestTileComponent(candidate) == component && !reserved.Contains(candidate))
                    positions.Add(candidate);
            }
            if (positions.Count != 3)
                throw new InvalidOperationException("No room for three test swordsmen near the first drag target.");

            var tribes = GameTribeManagerAPI.Instance;
            int tribeId = checked((int)tribes.Create(owner));
            if (!tribes.IsValidId(tribeId))
                throw new InvalidOperationException("Could not create the test tribe.");
            var units = new List<int>(positions.Count);
            foreach (var position in positions)
            {
                int height = tiles.GetTileHeight(tiles.GetTileId(position.NativeX, position.NativeY));
                int unitId = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner,
                    position.NativeX, position.NativeY, height, eChimps.CHIMP_TYPE_SWORDSMAN));
                if (unitId <= 0 || !tribes.AssignUnit(tribeId, unitId))
                    throw new InvalidOperationException("Could not create or assign a test swordsman.");
                units.Add(unitId);
            }
            units.Sort();
            return units;
        }

        private static ushort ReadFreeTestTileComponent(GroundTarget target)
        {
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(target.NativeX, target.NativeY))
                return 0;
            int tileId = tiles.GetTileId(target.NativeX, target.NativeY);
            var grid = tiles.TileManager;
            return IsTargetInsideNativeMap(target, tileId, grid) &&
                grid.TileUnitIdGrid[tileId] == 0 && grid.StructureGrid[tileId] == 0
                ? grid.PathConnectionGrid[tileId] : (ushort)0;
        }

        private GroundTarget ConfirmTestAnchor(AutomationRun run, MissionContext mission, GroundTarget expected, ushort component)
        {
            if (FatControler.instance.overNoesisGUI() || !TryCaptureCommandTarget(out var actual, out _) ||
                !actual.Equals(expected) || run.InSimulation(mission, () => ReadFreeTestTileComponent(actual)) != component)
                throw new InvalidOperationException($"The expected test tile {expected.NativeX},{expected.NativeY} is no longer under the cursor or free.");
            return expected;
        }

        private static string DescribeTestTargets(SelectionIdentity[] selection)
        {
            var targets = new List<string>(selection.Length);
            foreach (var identity in selection)
            {
                if (!UnitAccess.TryGetById(identity.UnitId, out GameUnit* unit, out _) ||
                    unit == null || unit->r_GlobalId != identity.GlobalId || !UnitAccess.IsReallyAlive(unit))
                    throw new InvalidOperationException("The selected test units changed before the target snapshot.");
                targets.Add($"{identity.GlobalId}:{unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}");
            }
            return string.Join(" ", targets);
        }

        private void RecordTestRelease(int tribeId, int operationId)
        {
            var trial = Volatile.Read(ref releaseTrial);
            if (trial != null && Volatile.Read(ref trial.TribeId) == tribeId)
                Interlocked.CompareExchange(ref trial.OperationId, operationId, 0);
        }

        private void RecordTestMove(TribeIssueOrderMoveHereEventArgs args)
        {
            var trial = Volatile.Read(ref releaseTrial);
            if (trial == null || args.TribeId != Volatile.Read(ref trial.TribeId) ||
                Interlocked.Increment(ref trial.SampleCount) > ReleaseTrial.SampleLimit)
                return;
            bool hasTick = SimulationTickEvents.TryGetCurrentTick(out int tick);
            trial.Orders.Enqueue(new OrderSample(args.Phase, args.TileX, args.TileY,
                args.ReturnValue, args.SkipOriginalFunction, hasTick ? (int?)tick : null));
        }

        private sealed class ReleaseTrial
        {
            internal const int SampleLimit = 96;
            internal ReleaseTrial(int tribeId) { TribeId = tribeId; }
            internal int TribeId;
            internal readonly ConcurrentQueue<OrderSample> Orders = new ConcurrentQueue<OrderSample>();
            internal int OperationId;
            internal int SampleCount;
        }

        private readonly struct OrderSample
        {
            internal OrderSample(EventHookPhase phase, int x, int y, long result, bool skip, int? tick)
            { Phase = phase; X = x; Y = y; Result = result; Skip = skip; Tick = tick; }
            internal readonly EventHookPhase Phase;
            internal readonly int X, Y;
            internal readonly long Result;
            private readonly bool Skip;
            private readonly int? Tick;
            public override string ToString() => $"tick={Tick?.ToString() ?? "unknown"} event={Phase} " +
                $"target={X},{Y} return={Result} skipAtObservation={Skip}";
        }

        private static void RequireForeground(IntPtr window)
        {
            if (window == IntPtr.Zero || GetForegroundWindow() != window || !Application.isFocused)
                throw new InvalidOperationException("The game lost the foreground during the formation test.");
        }

        private static void SetTestCursor(IntPtr window, ScreenPoint destination)
        {
            RequireForeground(window);
            var client = destination;
            if (!ScreenToClient(window, ref client) || !GetClientRect(window, out ClientRect bounds) ||
                client.X < bounds.Left || client.X >= bounds.Right || client.Y < bounds.Top || client.Y >= bounds.Bottom ||
                !SetCursorPos(destination.X, destination.Y))
                throw new InvalidOperationException("The test cursor destination is outside the game client or could not be set.");
        }

        private static void SendTestMouse(IntPtr window, bool left, bool down)
        {
            RequireForeground(window);
            if (!SendMouse(left, down))
                throw new InvalidOperationException("Windows rejected the test mouse input.");
        }

        private static bool SendMouse(bool left, bool down)
        {
            const uint LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008, RightUp = 0x0010;
            var input = new MouseInputPacket
            {
                Type = 0, // Win32 INPUT_MOUSE.
                Mouse = new MouseInput { Flags = left ? (down ? LeftDown : LeftUp) : (down ? RightDown : RightUp) }
            };
            return SendInput(1, ref input, Marshal.SizeOf<MouseInputPacket>()) == 1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInputPacket { internal uint Type; internal MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenPoint
        {
            internal int X, Y;
            internal ScreenPoint Offset(int x, int y) => new ScreenPoint { X = X + x, Y = Y + y };
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ClientRect { internal int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref ScreenPoint point);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref ScreenPoint point);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out ClientRect bounds);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern uint SendInput(uint count, ref MouseInputPacket input, int size);
    }
}
