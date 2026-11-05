using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.API;

namespace UiAcceptance
{
    // Ingame menu actions, confirmation popups, APIShared UI work snapshot, Harmony call counters
    // and the hash-bound BugfixesAndQoL 196100/+56C probe.
    internal static unsafe class ProbeSteps
    {
        internal static void Register()
        {
            Runner.Register("IngameMenuOpen", s => GameSteps.Do(() => MainViewModel.Instance.HUDmain.InGameOptions(null, null)));
            Runner.Register("MenuAction", MenuAction);
            Runner.Register("Confirm", Confirm);
            Runner.Register("UiWork.ExpectIdle", UiWorkExpectIdle);
            Runner.Register("UiWork.Dump", s => GameSteps.Do(() => AcceptLog.Info("UIWORK " + string.Join(", ", ActiveUiWork()))));
            Runner.Register("CountCalls", CountCalls);
            Runner.Register("ExpectCalls", ExpectCalls);
            Runner.Register("SelectOwnUnits", SelectOwnUnits);
            Runner.Register("PlayerMove", PlayerMove);
            Runner.Register("ExpectMoveMode", ExpectMoveMode);
            Runner.Register("RegionScan", RegionScan);
            Runner.Register("WaitGroup", WaitGroup);
        }

        private static int GroupCount(int tribe) =>
            GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out SHCDESE.Interop.GameTribe* record) && record != null ? record->r_UnitsInGroup : -1;

        // args: tribe, minimum — the Vanilla selection reaches the tribe group only through the
        // running simulation; a probe on an empty group proves nothing.
        private static IEnumerable<Wait> WaitGroup(Step step)
        {
            int tribe = step.Int(0), minimum = step.Int(1);
            yield return new Until("tribe " + tribe + " group with >= " + minimum + " units", () => GroupCount(tribe) >= minimum);
            AcceptLog.Info("GROUP tribe=" + tribe + " units=" + GroupCount(tribe));
        }

        private const int PathRegionGridRva = 0x50EC690, TargetAvailabilityRva = 0x3A11EA4, AvailabilityWidth = 800;
        private static List<int> lastSelection = new List<int>();
        private static readonly List<KeyValuePair<int, int>> candidates = new List<KeyValuePair<int, int>>();

        // Targets of the tribe group's actual members (Vanilla 0x119F90, the same reader BFQoL's
        // admission uses); the Vanilla selection does not necessarily equal the tribe group.
        private const int GetGroupUnitIdRva = 0x119F90;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GroupUnitDelegate(IntPtr tribeManager, int tribe, int ordinal);
        private static GroupUnitDelegate groupUnit;
        private static string Targets(int tribe)
        {
            if (groupUnit == null) groupUnit = Marshal.GetDelegateForFunctionPointer<GroupUnitDelegate>((IntPtr)(long)(image + GetGroupUnitIdRva));
            var parts = new List<string>();
            int count = Math.Min(GroupCount(tribe), 12);
            for (int ordinal = 0; ordinal < count; ordinal++)
                parts.Add(UnitTarget(groupUnit((IntPtr)(long)(image + TribeManagerRva), tribe, ordinal)));
            return string.Join(" ", parts);
        }

        private static string UnitTarget(int id) =>
            APIShared.UnitAccess.TryGetById(id, out SHCDESE.Interop.GameUnit* unit, out _)
                // Vanilla's group order store (118F50) writes native unit+0x934 = GameUnit+0x2D8.
                ? id + "@" + unit->r_AttackMoveToTargetTileX + "," + unit->r_AttackMoveToTargetTileY : id + "@?";

        private static short Region(int x, int y) =>
            unchecked((short)((ushort*)(image + PathRegionGridRva))[GameTileManagerAPI.Instance.GetTileId(x, y)]);
        private static byte Available(int x, int y) => ((byte*)(image + TargetAvailabilityRva))[y * AvailabilityWidth + x];

        // args: x0, y0, x1, y1, step — logs the region of the first selected unit and walkable target
        // tiles of other regions (candidates for a rejected manual move; BFQoL decides the final rejection).
        private static IEnumerable<Wait> RegionScan(Step step)
        {
            Bind();
            AcceptLog.Info(ScanRegions(step));
            yield break;
        }

        private static string ScanRegions(Step step)
        {
            int reference = ReferenceUnit();
            Runner.Require(APIShared.UnitAccess.TryGetById(reference, out SHCDESE.Interop.GameUnit* unit, out _), "reference unit vanished");
            int start = (int)unit->r_CurrentPositionTileId;
            short own = unchecked((short)((ushort*)(image + PathRegionGridRva))[start]);
            var regions = new Dictionary<short, string>();
            for (int y = step.Int(1); y <= step.Int(3); y += step.Int(4))
                for (int x = step.Int(0); x <= step.Int(2); x += step.Int(4))
                {
                    short region = Region(x, y);
                    if (region > 0 && region != own && Available(x, y) != 0 && !regions.ContainsKey(region)) regions[region] = x + "," + y;
                }
            var shown = new List<string>();
            candidates.Clear();
            foreach (var pair in regions)
            {
                string[] xy = pair.Value.Split(',');
                candidates.Add(new KeyValuePair<int, int>(int.Parse(xy[0]), int.Parse(xy[1])));
                if (shown.Count < 20) shown.Add("c" + (candidates.Count - 1) + "=r" + pair.Key + "@" + pair.Value);
            }
            return "REGION_SCAN unit=" + reference + " startTile=" + start + " ownRegion=" + own +
                " candidates=" + regions.Count + " " + string.Join(" ", shown);
        }

        // ---- APIShared ingame menu actions (internal registry; test-only reflection) ----
        private static IEnumerable<Wait> MenuAction(Step step)
        {
            var view = MainViewModel.Instance;
            if (!view.Show_HUD_IngameMenu) view.HUDmain.InGameOptions(null, null);
            yield return new Until("ingame menu open", () => view.Show_HUD_IngameMenu);
            yield return new Delay(200);
            Type actions = GameSteps.FindType("APIShared.Presentation.IngameMenuActions") ?? throw new StepFailure("APIShared IngameMenuActions missing");
            object registry = GameSteps.Read(actions, null, "registry", BindingFlags.Static);
            var entries = (IEnumerable)GameSteps.Read(registry.GetType(), registry, "Entries", BindingFlags.Instance);
            foreach (object entry in entries)
            {
                Type type = entry.GetType();
                if ((string)GameSteps.Read(type, entry, "Owner", BindingFlags.Instance) != step.Arg(0) ||
                    (string)GameSteps.Read(type, entry, "Id", BindingFlags.Instance) != step.Arg(1)) continue;
                var execute = (Action)GameSteps.Read(type, entry, "Execute", BindingFlags.Instance);
                AcceptLog.Info("MENU_ACTION " + step.Arg(0) + "/" + step.Arg(1));
                execute();
                yield return new Delay(300);
                yield break;
            }
            throw new StepFailure("menu action " + step.Arg(0) + "/" + step.Arg(1) + " not registered");
        }

        private static IEnumerable<Wait> Confirm(Step step)
        {
            var view = MainViewModel.Instance;
            yield return new Until("confirmation popup", GameSteps.ConfirmationVisible);
            view.ButtonConfirmationCommand.Execute(step.Arg(0, "1"));
            yield return new Delay(300);
        }

        // ---- APIShared UI work snapshot: active = continuous or immediately requested ----
        internal static List<string> ActiveUiWork()
        {
            var active = new List<string>();
            Type runtime = GameSteps.FindType("APIShared.Presentation.UiWorkRuntime") ?? throw new StepFailure("UiWorkRuntime missing");
            object scheduler = GameSteps.Read(runtime, null, "scheduler", BindingFlags.Static) ?? throw new StepFailure("UI work not initialized");
            object snapshot = GameSteps.Read(scheduler.GetType(), scheduler, "snapshot", BindingFlags.Instance);
            foreach (object entry in (IEnumerable)GameSteps.Read(snapshot.GetType(), snapshot, "Active", BindingFlags.Instance))
                active.Add(GameSteps.Read(entry.GetType(), entry, "Owner", BindingFlags.Instance) + "/" +
                    GameSteps.Read(entry.GetType(), entry, "Id", BindingFlags.Instance) +
                    ((bool)GameSteps.Read(entry.GetType(), entry, "Continuous", BindingFlags.Instance) ? "(continuous)" : "(requested)"));
            return active;
        }

        // Samples the snapshot for N ms; owners listed in the arguments must never be continuously active.
        private static IEnumerable<Wait> UiWorkExpectIdle(Step step)
        {
            var violations = new HashSet<string>(StringComparer.Ordinal);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int samples = 0;
            int duration = step.Int(0);
            while (clock.ElapsedMilliseconds < duration)
            {
                samples++;
                foreach (string item in ActiveUiWork())
                    for (int i = 1; i < step.Args.Length; i++)
                        if (item.StartsWith(step.Args[i] + "/", StringComparison.Ordinal) && item.EndsWith("(continuous)", StringComparison.Ordinal))
                            violations.Add(item);
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("UIWORK_IDLE samples=" + samples + " violations=" + string.Join(", ", violations));
            Runner.Require(violations.Count == 0, "continuous UI work of inactive consumers: " + string.Join(", ", violations));
        }

        // ---- Harmony call counters (prefix, counts per method and per rendered frame) ----
        private static readonly Dictionary<string, Counter> counters = new Dictionary<string, Counter>(StringComparer.Ordinal);
        private sealed class Counter { internal long Calls; internal int MaxPerFrame, Frame = -1, InFrame; }
        private static readonly Dictionary<MethodBase, Counter> byMethod = new Dictionary<MethodBase, Counter>();

        // args: label, "Namespace.Type", "Method"
        private static IEnumerable<Wait> CountCalls(Step step)
        {
            string label = step.Arg(0);
            if (!counters.TryGetValue(label, out Counter counter))
            {
                Type type = GameSteps.FindType(step.Arg(1)) ?? throw new StepFailure("type not loaded: " + step.Arg(1));
                MethodInfo method = AccessTools.Method(type, step.Arg(2)) ?? throw new StepFailure("method not found: " + step.Arg(2));
                counter = new Counter();
                lock (byMethod) byMethod[method] = counter;
                counters[label] = counter;
                UiAcceptancePlugin.Harmony.Patch(method, prefix: new HarmonyMethod(typeof(ProbeSteps), nameof(CountPrefix)));
            }
            lock (byMethod) { counter.Calls = 0; counter.MaxPerFrame = 0; }
            yield break;
        }

        private static void CountPrefix(MethodBase __originalMethod)
        {
            lock (byMethod)
            {
                if (!byMethod.TryGetValue(__originalMethod, out Counter counter)) return;
                counter.Calls++;
                int frame = UnityEngine.Time.frameCount;
                if (counter.Frame != frame) { counter.Frame = frame; counter.InFrame = 0; }
                if (++counter.InFrame > counter.MaxPerFrame) counter.MaxPerFrame = counter.InFrame;
            }
        }

        // args: label, minCalls, maxCalls, [maxPerFrame]
        private static IEnumerable<Wait> ExpectCalls(Step step)
        {
            Runner.Require(counters.TryGetValue(step.Arg(0), out Counter counter), "no counter " + step.Arg(0));
            long calls; int perFrame;
            lock (byMethod) { calls = counter.Calls; perFrame = counter.MaxPerFrame; }
            AcceptLog.Info("CALLS " + step.Arg(0) + " calls=" + calls + " maxPerFrame=" + perFrame);
            Runner.Require(calls >= step.Int(1) && calls <= step.Int(2), step.Arg(0) + " calls=" + calls);
            if (step.Args.Length > 3) Runner.Require(perFrame <= step.Int(3), step.Arg(0) + " maxPerFrame=" + perFrame);
            yield break;
        }

        // ---- BugfixesAndQoL rejected-move probe (hash-bound, see UpdateToNewDLL.md) ----
        private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int PlayerMoveRva = 0x196100, UnitManagerRva = 0x67E8400, TribeManagerRva = 0x7CC6720, TribeStride = 0x688, MoveModeOffset = 0x56C;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void PlayerMoveDelegate(IntPtr manager, int tribe, int x, int y, int patrol, int flags);
        private static PlayerMoveDelegate playerMove;
        private static ulong image;
        private static object engineLock;
        private static bool nativeHashVerified;

        internal static void RequireNativeReferenceHash()
        {
            if (nativeHashVerified) return;
            string file = Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "") != NativeSha256)
                    throw new StepFailure("unknown CrusaderDE.dll; native fixture contracts are hash-bound");
            nativeHashVerified = true;
        }

        private static void Bind()
        {
            if (playerMove != null) return;
            RequireNativeReferenceHash();
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            Runner.Require(module != IntPtr.Zero, "CrusaderDE.dll not loaded");
            image = (ulong)module.ToInt64();
            // The full-file hash pins every RVA; the live entry is deliberately not compared because
            // BugfixesAndQoL's detour is expected there and the call must go through it.
            FieldInfo monitor = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            engineLock = monitor?.GetValue(null) ?? throw new StepFailure("EngineInterface.threadLock missing");
            playerMove = Marshal.GetDelegateForFunctionPointer<PlayerMoveDelegate>((IntPtr)(long)(image + PlayerMoveRva));
            AcceptLog.Info("PROBE_BOUND image=0x" + image.ToString("X") + " playerMove=0x" + PlayerMoveRva.ToString("X"));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        internal static ushort MoveMode(int tribe) => *(ushort*)(image + TribeManagerRva + (ulong)(tribe * TribeStride) + MoveModeOffset);

        // args: player, count — selects that many alive own units through Vanilla's selection entry.
        private static IEnumerable<Wait> SelectOwnUnits(Step step)
        {
            int player = step.Int(0), count = step.Int(1);
            List<int> selected = OwnUnits(player, count);
            Runner.Require(selected.Count == count, "player " + player + " has only " + selected.Count + " alive units");
            EngineInterface.TroopSelectionChanged(selected.ToArray());
            lastSelection = selected;
            AcceptLog.Info("SELECTED player=" + player + " units=" + string.Join(",", selected));
            yield return new Delay(300);
        }

        private static List<int> OwnUnits(int player, int count)
        {
            var selected = new List<int>();
            foreach (int id in APIShared.UnitAccess.GetAllReallyAliveUnits())
            {
                if (selected.Count >= count) break;
                if (APIShared.UnitAccess.TryGetById(id, out SHCDESE.Interop.GameUnit* unit, out _) && unit->r_ControllableForPlayerId == player)
                    selected.Add(id);
            }
            return selected;
        }

        // Target forms: "c<N>" = N-th RegionScan candidate, "rel:dx:dy" = relative to the first selected
        // unit, "x:y" = absolute tile.
        private static void ResolveTarget(string target, out int x, out int y)
        {
            string[] part = target.Split(':');
            if (target.StartsWith("c", StringComparison.Ordinal) && part.Length == 1)
            {
                int index = int.Parse(target.Substring(1));
                Runner.Require(index < candidates.Count, "RegionScan found only " + candidates.Count + " candidates");
                x = candidates[index].Key; y = candidates[index].Value; return;
            }
            if (part[0] == "rel" && part.Length == 3)
            {
                UnitTile(ReferenceUnit(), out x, out y);
                x += int.Parse(part[1]); y += int.Parse(part[2]); return;
            }
            Runner.Require(part.Length == 2, "target must be c<N>, rel:dx:dy or x:y");
            x = int.Parse(part[0]); y = int.Parse(part[1]);
        }

        // First member of the probed tribe group (tribe 1 = local player in the test saves), else the selection.
        private static int ReferenceUnit()
        {
            if (groupUnit == null) groupUnit = Marshal.GetDelegateForFunctionPointer<GroupUnitDelegate>((IntPtr)(long)(image + GetGroupUnitIdRva));
            if (GroupCount(1) > 0) return groupUnit((IntPtr)(long)(image + TribeManagerRva), 1, 0);
            Runner.Require(lastSelection.Count > 0, "no group member and no selection");
            return lastSelection[0];
        }

        private static void UnitTile(int id, out int x, out int y)
        {
            Runner.Require(APIShared.UnitAccess.TryGetById(id, out SHCDESE.Interop.GameUnit* unit, out _), "unit " + id + " vanished");
            x = unit->r_CurrentTilePositionX; y = unit->r_CurrentTilePositionY;
        }

        // args: tribe, target, flags(hex) — Vanilla's Chore-17 call shape: rcx = unit manager (10AE0/10C7C).
        private static IEnumerable<Wait> PlayerMove(Step step)
        {
            Bind();
            ResolveTarget(step.Arg(1), out int x, out int y);
            int tribe = step.Int(0), flags = Convert.ToInt32(step.Arg(2), 16);
            ushort before, after;
            string targetsBefore, targetsAfter;
            lock (engineLock)
            {
                targetsBefore = Targets(tribe);
                before = MoveMode(tribe);
                playerMove((IntPtr)(long)(image + UnitManagerRva), tribe, x, y, 0, flags);
                after = MoveMode(tribe);
                targetsAfter = Targets(tribe);
            }
            AcceptLog.Info("PLAYER_MOVE tribe=" + tribe + " group=" + GroupCount(tribe) + " target=" + x + "," + y + " targetRegion=" + Region(x, y) + " flags=0x" + flags.ToString("X") +
                " moveMode " + before + "->" + after + " unitTargets " + targetsBefore + " -> " + targetsAfter +
                " accepted=" + (targetsBefore != targetsAfter));
            // "restore": a fully rejected order (no member target changed) must keep the earlier mode.
            if (step.Arg(3, "") == "restore" && targetsBefore == targetsAfter)
                Runner.Require(after == before, "rejected order changed tribe+0x56C " + before + "->" + after);
            yield return new Delay(300);
        }

        private static IEnumerable<Wait> ExpectMoveMode(Step step)
        {
            Bind();
            ushort mode = MoveMode(step.Int(0));
            AcceptLog.Info("MOVE_MODE tribe=" + step.Arg(0) + " value=" + mode);
            Runner.Require(mode == step.Int(1), "tribe+0x56C=" + mode + ", expected " + step.Arg(1));
            yield break;
        }
    }
}
