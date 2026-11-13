using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AiSiegeCrewTest
{
    // Explicit local fixture. Every native access/call is inside InSimulation.
    // The local slot receives a disposable AI context and runs the original
    // decision. The ordinary simulation can also schedule that context.
    internal sealed class Fixture
    {
        private readonly string map, scenario;
        private readonly Action<string> log;
        private int owner, tribe, x, y, height;
        private readonly List<int> ids = new List<int>();
        private readonly List<uint> globals = new List<uint>();
        private IntPtr image;
        private const int Units = 0x67E8400, Stride = 0x490;
        private const int PlayerStride = 0x583C;
        private const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Deploy(IntPtr ai, int player);
        private Deploy deploy;
        private IntPtr ai;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        private static void Execute(AutomationRun run, Action action) => run.InSimulation(run.CurrentMission, () => { action(); return 0; });
        internal Fixture(string map, string scenario, Action<string> log)
        { this.map = map; this.scenario = scenario; this.log = log; }
        internal static bool Owns(string name) => name == "ready-cycles" || name == "slow-peasant" || name == "slow-engineer" || name == "control-peasant" ||
            name == "save-before-on" || name == "save-before-off" || name == "save-happened-on" || name == "save-happened-off";
        internal IEnumerable<AutomationStep> Steps()
        {
            if (scenario.StartsWith("save-", StringComparison.Ordinal))
            {
                yield return new AutomationStep("load-user-save", TimeSpan.FromSeconds(180), LoadSave);
                yield return new AutomationStep("observe-user-save", TimeSpan.FromSeconds(300), WatchSave, true);
                yield break;
            }
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(180), Start);
            yield return new AutomationStep("ready", TimeSpan.FromSeconds(60), Ready, true);
            yield return new AutomationStep("native-build-loss-cycles", TimeSpan.FromSeconds(240), Cycles, true);
        }
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin);
            settings.GetType().GetProperty("EnableMod").SetValue(settings, true);
            settings.GetType().GetProperty("EnableSiegeCrewDuplicationFix").SetValue(settings, scenario != "control-peasant");
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT })) yield return wait;
        }
        private IEnumerable<AutomationWait> Ready(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, Prepare);
            Trace.Start(run.CurrentMission);
            bool guildPlaced = false;
            int guild = 0;
            for (int dy = 8; dy <= 32 && !guildPlaced; dy += 4)
                for (int dx = -16; dx <= 16 && !guildPlaced; dx += 4)
                    guildPlaced = run.TrySpawnBuilding(run.CurrentMission, owner, x + dx, y + dy,
                        eMappers.MAPPER_ENGINEERS_GUILD, -1, out guild, out _);
            Require(guildPlaced, "No legal engineers guild placement");
            int guildId = guild;
            Execute(run, () => Write32(0x379AFCC + owner * PlayerStride, guildId));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("ordinary engineers guild confirmed=" + guild);
            Director.instance.SetEngineFrameRate(250);
        }
        private IEnumerable<AutomationWait> LoadSave(AutomationRun run)
        {
            Require(Trace.Available, "Save observation requires build.bat /devdiag");
            bool before = scenario.Contains("before");
            Require(scenario == "save-before-on" || scenario == "save-before-off" ||
                scenario == "save-happened-on" || scenario == "save-happened-off", "Unknown save scenario");
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin);
            settings.GetType().GetProperty("EnableMod").SetValue(settings, true);
            settings.GetType().GetProperty("EnableSiegeCrewDuplicationFix").SetValue(settings, scenario.EndsWith("-on", StringComparison.Ordinal));
            string path = Path.Combine(ConfigSettings.GetSavesPath(), before ? "test AI engineer dupe before.sav" : "test AI engineer dupe happened.sav");
            // Use the explicitly selected test settings, not a historical mod-settings snapshot.
            APIShared.SavegameModSettings.PrepareLoad(path, true);
            foreach (var wait in run.LoadSave(path)) yield return wait;
        }
        private IEnumerable<AutomationWait> WatchSave(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Trace.Start(run.CurrentMission);
            Execute(run, () => Trace.Capture("loaded-paused"));
            Director.instance.SetEngineFrameRate(250);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            // The supplied snapshots are5639 map ticks apart. Observe beyond
            // that window so the second device assignment is not missed.
            for (int sample = 0; sample < 26; sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 256);
                Execute(run, () => Trace.Capture("save-sample"));
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => Trace.Capture("save-end"));
            int newAliases = run.InSimulation(run.CurrentMission, () => Trace.NewAliases);
            Require(run.InSimulation(run.CurrentMission, () => Trace.Healthy), "Diagnostic observation faulted or did not observe enough native runs");
            log("save observation newAliasPairs=" + newAliases);
            if (scenario == "save-before-on") Require(newAliases == 0, "New multiple crew references appeared with the active fix");
            log("save observation complete; no fixture units, groups, resources or AI orders were injected");
        }
        private unsafe void Prepare()
        {
            string file = Path.Combine(BepInEx.Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var input = File.OpenRead(file)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") == Hash, "Unknown native image");
            image = GetModuleHandle("CrusaderDE.dll");
            Require(image != IntPtr.Zero, "No native module");
            deploy = Marshal.GetDelegateForFunctionPointer<Deploy>(IntPtr.Add(image, 0x2ECD0));
            ai = new IntPtr(image.ToInt64() + (long)GameGlobalsManager.Instance.AILordManagerRVA);
            owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            Require(owner >= 1 && owner <= 8 && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "Not a local human fixture");
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            bool found = false;
            for (int dy = 8; dy <= 48 && !found; dy += 4)
                for (int dx = -24; dx <= 24 && !found; dx += 4)
                {
                    int tx = keep.X + dx, ty = keep.Y + dy;
                    var tiles = GameTileManagerAPI.Instance;
                    if (!tiles.IsTileInsideMapBounds(tx, ty)) continue;
                    int tile = tiles.GetTileId(tx, ty);
                    if (!tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetChimpLayer()[tile] != 0) continue;
                    x = tx; y = ty; height = tiles.GetTileHeight(tile); found = true;
                }
            Require(found, "No vacant spawn tile near keep");
            // On-demand AI decision needs an AIC but the human slot deliberately
            // has none. Use the existing opponent's AIC for this disposable world.
            int aic = 0;
            for (int p = 1; p <= 8 && aic == 0; p++)
                if (GamePlayerManagerAPI.Instance.IsAIPlayer(p)) aic = Read32(0x379D0D0 + p * PlayerStride);
            Require(aic > 0 && aic < 256, "No initialized opponent AIC");
            Write32(0x379D0D0 + owner * PlayerStride, aic);
            Marshal.WriteInt32(ai, (aic - 1) * 0x5E4 + 0x1F0, 1);
            for (int n = 0; n < 8; n++) Marshal.WriteInt32(ai, (aic - 1) * 0x5E4 + 0x1D0 + n * 4, (int)eMappers.MAPPER_CATAPULT);
            Write32(0x379B2DC + owner * PlayerStride, 10000);
            log("prepared owner=" + owner + " aic=" + aic + " spawn=" + x + "," + y + " scenario=" + scenario +
                " fixes-order-entry=" + unchecked((ushort)Marshal.ReadInt16(image, 0x2EFCB)).ToString("X4"));
        }
        private IEnumerable<AutomationWait> Cycles(AutomationRun run)
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Execute(run, Spawn);
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return AutomationWait.Until("two initialized engineers", () => run.InSimulation(run.CurrentMission, BothReady));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                int ordinal = cycle;
                Execute(run, () => PrepareGroup(ordinal));
                if (scenario == "control-peasant")
                {
                    Execute(run, AssertVanillaPrematureOrder);
                    yield break;
                }
                if (scenario != "ready-cycles")
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                        Execute(run, AssertDeferred);
                    // WaitForTicks is a minimum, not exact stepping. Keep the
                    // two early probes inside the native33-update transition
                    // even if Unity presents a frame late. The high-speed
                    // setting is restored before waiting for actual readiness.
                    Director.instance.SetEngineFrameRate(5);
                    foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission, 1);
                    Execute(run, AssertDeferred);
                    // Slow the same real recruitment transition across one more
                    // scheduling opportunity; the gate must depend on readiness.
                    yield return run.WaitForTicks(run.CurrentMission, 1);
                    Execute(run, AssertDeferred);
                    Director.instance.SetEngineFrameRate(250);
                    yield return AutomationWait.Until("native recruitment/fade completed", () => run.InSimulation(run.CurrentMission, BothReady));
                    foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                }
                Execute(run, Invoke);
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                int device = 0;
                for (int sample = 0; sample < 80 && device == 0; sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission, 16);
                    device = run.InSimulation(run.CurrentMission, FindDevice);
                    if (sample % 10 == 0) Execute(run, LogState);
                }
                Require(device > 0, "No completed catapult after 1280 ticks");
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                int complete = device;
                Execute(run, () => VerifyAndLose(complete));
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, 180);
                Execute(run, VerifyGone);
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                log("cycle=" + cycle + " complete; two matching crew mounted, device lost, no surviving fixture engineers");
            }
        }
        private unsafe void Spawn()
        {
            ids.Clear(); globals.Clear();
            for (int n = 0; n < 2; n++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x + n * 2, y, height, eChimps.CHIMP_TYPE_ENGINEER));
                GameUnit* u = null;
                Require(id > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(id, out u) && u->r_GlobalId != 0, "Spawn failed");
                ids.Add(id); globals.Add(u->r_GlobalId);
            }
        }
        private unsafe bool BothReady()
        {
            foreach (int id in ids)
            {
                Require(GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* u) && u->r_GlobalId == globals[ids.IndexOf(id)], "Recruit identity changed");
                int state = (ushort)Marshal.ReadInt16(Anchor(id), 0x918);
                if (u->r_AliveState != AliveState.IsAlive || u->r_UnitChimp != eChimps.CHIMP_TYPE_ENGINEER ||
                    Marshal.ReadInt16(Anchor(id), 0x9CA) != 0 || state > 2 && state != 0x65 && state != 0x69) return false;
            }
            return true;
        }
        private unsafe void PrepareGroup(int cycle)
        {
            tribe = checked((int)GameTribeManagerAPI.Instance.Create(owner));
            GameTribe* group = null;
            Require(tribe > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out group), "Group creation failed");
            foreach (int id in ids)
            {
                Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "Group assignment failed");
                Marshal.WriteInt16(Anchor(id), 0xA82, 22); // Native role; storage slot below is165.
            }
            Write16(0x379F7BA + owner * PlayerStride, tribe);
            Write32(0x379FB5C + owner * PlayerStride, unchecked((int)group->r_GlobalId));
            Write32(0x379E79C + owner * PlayerStride, 2);
            Write32(0x379E6EC + owner * PlayerStride, 0);
            Write32(0x379E6E4 + owner * PlayerStride, 0);
            if (scenario != "ready-cycles")
            {
                // Reproduce the audited, identity-preserving pending recruitment
                // fields only on our own spawned unit, never on existing units.
                IntPtr pending = Anchor(ids[1]);
                Marshal.WriteInt16(pending, 0x9CA, 1);
                Marshal.WriteInt16(pending, 0x922, (short)eChimps.CHIMP_TYPE_ENGINEER);
                Marshal.WriteInt16(pending, 0x924, 0);
                Marshal.WriteInt32(pending, 0x918, 0x0000006D);
                Marshal.WriteInt16(pending, 0x920, 0);
                Marshal.WriteInt16(pending, 0x986, 0x0100);
                if (scenario == "slow-peasant" || scenario == "control-peasant") Marshal.WriteInt16(pending, 0x6E6, (short)eChimps.CHIMP_TYPE_PEASANT);
            }
            log("group cycle=" + cycle + " tribe=" + tribe + " recruits=" + string.Join(",", ids));
        }
        private void Invoke()
        {
            Write32(0x379E6E8 + owner * PlayerStride, 0);
            deploy(ai, owner);
        }
        private unsafe void AssertDeferred()
        {
            int before = Read32(0x379E6E4 + owner * PlayerStride);
            Invoke();
            Require(Read32(0x379E6E4 + owner * PlayerStride) == before, "Premature machine-index advance");
            Require(GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group->r_UnitsInGroup == 2,
                "Premature recruitment-group consumption");
            Require(FindDevice() == 0, "Premature completed engine");
        }
        private unsafe void AssertVanillaPrematureOrder()
        {
            Invoke();
            LogState();
            Require(Read32(0x379E6E4 + owner * PlayerStride) == 1, "Inactive gate did not advance Vanilla build");
            Require(GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group->r_UnitsInGroup == 1,
                "Vanilla positive control did not strand its pending peasant");
            log("positive control: fix disabled, Vanilla ordered the ready engineer and left the pending peasant in reserve");
        }
        private unsafe int FindDevice()
        {
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int i = 0; i < units.Length; i++)
                if (units[i].r_AliveState == AliveState.IsAlive && units[i].r_ControllableForPlayerId == owner &&
                    units[i].r_UnitChimp == eChimps.CHIMP_TYPE_CATAPULT &&
                    units[i].r_IsKilledByProjectile == 0 && units[i].r_CurrentHealth > 0) return i + 1;
            return 0;
        }
        private void LogState()
        {
            log("state " + string.Join(";", ids.Select(id => id + ":type=" + Marshal.ReadInt16(Anchor(id), 0x6E6) +
                ",life=" + Marshal.ReadInt16(Anchor(id), 0x6E4) + ",state=" + ReadAnchor32(id, 0x918) +
                ",recruit=" + Marshal.ReadInt16(Anchor(id), 0x9CA))));
        }
        private unsafe void VerifyAndLose(int device)
        {
            IntPtr record = Anchor(device);
            Require(Marshal.ReadInt16(record, 0xA0C) == 2, "Completed device crew count differs");
            var actual = new HashSet<int>();
            for (int n = 0; n < 2; n++)
            {
                int id = (ushort)Marshal.ReadInt16(record, 0x96C + n * 2);
                uint global = unchecked((uint)Marshal.ReadInt32(record, 0x974 + n * 4));
                Require(ids.Contains(id) && globals[ids.IndexOf(id)] == global && actual.Add(id), "Device crew identity mismatch/duplicate");
                Require(Marshal.ReadInt16(Anchor(id), 0x918) == 5 && Marshal.ReadInt16(Anchor(id), 0x9CA) == 0,
                    "Crew not bound after construction");
            }
            GameUnitManagerAPI.Instance.KillUnit(device);
        }
        private void VerifyGone()
        {
            foreach (int id in ids)
                Require(Marshal.ReadInt16(Anchor(id), 0x6E4) == 0 ||
                    unchecked((uint)Marshal.ReadInt32(Anchor(id), 0x6F0)) != globals[ids.IndexOf(id)], "Stranded living fixture engineer after loss");
        }
        private IntPtr Anchor(int id) => IntPtr.Add(image, Units + id * Stride);
        private int ReadAnchor32(int id, int offset) => Marshal.ReadInt32(Anchor(id), offset);
        private int Read32(int rva) => Marshal.ReadInt32(image, rva);
        private void Write32(int rva, int value) => Marshal.WriteInt32(image, rva, value);
        private void Write16(int rva, int value) => Marshal.WriteInt16(image, rva, checked((short)value));
        private static void Require(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    }
}
