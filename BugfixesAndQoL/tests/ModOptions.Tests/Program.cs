using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using RedBird.X64.Assembly;

namespace BugfixesAndQoL
{
    public static unsafe class Program
    {
        private static int checks, nextSlot = 1, originals;
        private static uint nextGlobal = 1;
        private static Action nestedBeforeSpawns;
        private static PlaguePopularityFix runtime;
        private static readonly BugfixesAndQoLViewModel settings = new BugfixesAndQoLViewModel();
        private static readonly IntPtr resources = Marshal.AllocHGlobal(0x12EC24);
        private static int* Accumulator => (int*)((byte*)resources + 0x12EC20);

        public static void Original(IntPtr manager, int buildingId)
        {
            originals++;
            Action nested = nestedBeforeSpawns;
            nestedBeforeSpawns = null;
            nested?.Invoke();
            for (int index = 0; index < 6; index++)
            {
                int slot = nextSlot++;
                GameProjectileManagerAPI.Instance.Pool[slot] = new GameProjectile
                {
                    r_AliveState = AliveState.NeedsInit, r_ProjectileType = ProjectileType.Disease, r_GlobalId = nextGlobal++
                };
                ProjectileR3EventHooks.OnProjectileSpawn.Emit(new ProjectileSpawnEventArgs
                { Phase = EventHookPhase.Post, ProjectileType = ProjectileType.Disease, ReturnValue = slot });
            }
        }

        private static object Call(string method, params object[] args) => typeof(PlaguePopularityFix)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, args);
        private static void Check(bool valid, string label) { checks++; if (!valid) throw new Exception(label); }
        private static void Start(bool enabled)
        {
            ModSaveDataAPI.Instance.Reset();
            settings.EnableMod = enabled;
            settings.EnablePlaguePopularityFix = enabled;
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext());
        }
        private static void Create(int player)
        {
            *GameBuildingManagerAPI.Instance.Building = new GameBuilding { r_PlayerIdOwner = player, r_GlobalId = 1 };
            int before = originals;
            Call("CreatePlagueHerd", IntPtr.Zero, 1);
            Check(originals == before + 1, "Vanilla called exactly once");
        }
        private static X64SmartCPUContext Correct(int player)
        {
            *Accumulator = 1000;
            X64SmartCPUContext context = new X64SmartCPUContext
            { RAX = 0xABCD000000000000UL | unchecked((ushort)-150), RDX = 1000, R14 = (ulong)player, R12 = (ulong)resources.ToInt64() };
            Call("CorrectPlaguePopularity", new NativePointer<X64SmartCPUContext>(&context));
            return context;
        }
        private static byte[] Save() => ModSaveDataAPI.Instance.Save(new SaveContext());
        private static PlaguePopularitySaveState Decode(byte[] bytes) => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(bytes);

        private static void RuntimeTests()
        {
            runtime = new PlaguePopularityFix(new BepInEx.Logging.ManualLogSource(), settings,
                new RedBird.Core.Memory.ScanRegion(), ReadOnlySpan<byte>.Empty, 0, true);
            Start(false);
            Create(1);
            byte[] disabledSave = Save();
            Check(Decode(disabledSave).Herds.Length == 1, "disabled session preserves save provenance");
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 0, "disabled fix has no watchdog");
            GameProjectileManagerAPI.Instance.Reads = 0;
            var disabled = Correct(1);
            Check(disabled.RDX == 1000 && (short)disabled.RAX == -150 && *Accumulator == 1000, "disabled preserves Vanilla registers/memory");
            Check(GameProjectileManagerAPI.Instance.Reads == 0, "disabled callback has no projectile reads");

            Start(true);
            ModSaveDataAPI.Instance.Load(disabledSave, new LoadContext());
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 1, "loaded herd arms one watchdog");
            var corrected = Correct(1);
            Check((short)corrected.RAX == -25 && corrected.RDX == 1125 && *Accumulator == 1125, "off-save loads with active correction");
            Check((corrected.RAX >> 48) == 0xABCD, "AX update preserves upper register bits");
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 0, "successful correction cancels watchdog");

            Create(2);
            Create(1);
            GameProjectileManagerAPI.Instance.Reads = 0;
            corrected = Correct(1);
            Check((short)corrected.RAX == -50, "two herds for same player");
            Check(GameProjectileManagerAPI.Instance.Reads == 12, "player-local reconciliation skips other player's six clouds");
            int livingSlot = Decode(Save()).Herds.First(h => h.PlayerId == 1).ProjectileSlotIds[0];
            ProjectileR3EventHooks.OnProjectileDelete.Emit(new ProjectileDeleteEventArgs { Phase = EventHookPhase.Post, ProjectileId = livingSlot });
            Check(Decode(Save()).Herds.Where(h => h.PlayerId == 1).Sum(h => h.ProjectileSlotIds.Length) == 12,
                "redirected/ignored delete post keeps still-living original identity");

            foreach (var herd in Decode(Save()).Herds.Where(h => h.PlayerId == 1))
                foreach (int slot in herd.ProjectileSlotIds) GameProjectileManagerAPI.Instance.Pool[slot].r_AliveState = AliveState.ToDelete;
            corrected = Correct(1);
            Check((short)corrected.RAX == 0 && corrected.RDX == 1150 && *Accumulator == 1150, "terminal state before delete removes stale penalty immediately");
            var ended = Decode(Save());
            Check(ended.ManagedPlayerIds.Contains(1) && ended.Herds.All(h => h.PlayerId != 1), "last-herd zero remains managed in saves");

            byte[] enabledSave = Save();
            Start(false);
            ModSaveDataAPI.Instance.Load(enabledSave, new LoadContext());
            Check(Correct(2).RDX == 1000, "on-save loads with correction disabled");
            Start(true);
            ModSaveDataAPI.Instance.Load(enabledSave, new LoadContext());
            Check((short)Correct(2).RAX == -25, "second reactivation in same process works");

            Create(1);
            Action stale = GameTimeManagerAPI.Instance.Timers.Pending.Values.First();
            Start(true);
            Create(1);
            int warnings = Shared.DebugLogHelper.Warnings;
            stale();
            Check(Shared.DebugLogHelper.Warnings == warnings, "old-session watchdog cannot warn for new session");
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings + 1, "temporary watchdog warns once");
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings + 1, "watchdog does not repeat");

            Shared.DebugLogHelper.FailDebug = true;
            Create(2);
            Check((short)Correct(2).RAX == -25, "debug listener failure preserves correction");
            Shared.DebugLogHelper.FailDebug = false;
            GameTimeManagerAPI.Instance.Timers.Fail = true;
            Create(2);
            Check((short)Correct(2).RAX == -50, "watchdog failure preserves correction");
            GameTimeManagerAPI.Instance.Timers.Fail = false;
            settings.EnableMod = false;
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 0, "disable cancels pending diagnostics");
            Start(true);
            int errors = Shared.DebugLogHelper.Errors;
            ModSaveDataAPI.Instance.Load(new byte[] { 0xC1 }, new LoadContext());
            Check(Shared.DebugLogHelper.Errors == errors + 1 && Correct(1).RDX == 1000, "invalid save rejects state and preserves Vanilla");
            Check(Save() == null, "rejected save has no partial ledger");

            Start(true);
            nestedBeforeSpawns = () => Create(2);
            *GameBuildingManagerAPI.Instance.Building = new GameBuilding { r_PlayerIdOwner = 1, r_GlobalId = 1 };
            int calls = originals;
            Call("CreatePlagueHerd", IntPtr.Zero, 1);
            Check(originals == calls + 2, "nested captures call each Vanilla invocation exactly once");
            var nestedState = Decode(Save());
            Check(nestedState.Herds.Length == 2 && nestedState.Herds.All(h => h.ProjectileSlotIds.Length == 6), "nested capture restores outer context");
            Check((short)Correct(1).RAX == -25 && (short)Correct(2).RAX == -25, "nested herds retain independent owners");
            byte[] compatibilitySave = Save();
            ModSaveDataAPI.Instance.Reset();
            settings.EnableMod = false;
            ModSaveDataAPI.Instance.Load(compatibilitySave, new LoadContext());
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext());
            settings.EnableMod = true; // Models late settings restoration at the load boundary.
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 2, "late save-setting activation arms tracked players");
            Check((short)Correct(1).RAX == -25, "late save-setting activation corrects without reinstalling");
        }

        private static void LedgerTests()
        {
            var ledger = new PlagueHerdLedger();
            ledger.Add(1, new List<PlagueProjectileIdentity> { new PlagueProjectileIdentity(1, 10) });
            ledger.Add(2, new List<PlagueProjectileIdentity> { new PlagueProjectileIdentity(2, 20) });
            int reads = 0;
            ledger.ReconcileDeletedSlot(200, x => { reads++; return false; });
            Check(reads == 0, "untracked delete does not query native state");
            ledger.ReconcileDeletedSlot(1, x => { reads++; return true; });
            Check(reads == 1 && ledger.Count == 2, "tracked delete probes only one identity");
            ledger.Add(2, new List<PlagueProjectileIdentity> { new PlagueProjectileIdentity(1, 30) });
            Check(ledger.CountHerds(1) == 0 && ledger.CountHerds(2) == 2, "slot reuse removes former owner and replaces identity");
            ledger.ReconcilePlayer(2, x => x.GlobalId == 30);
            Check(ledger.Count == 1 && ledger.ToSaveRecords()[0].ProjectileGlobalIds[0] == 30, "global identity survives slot reuse");
            ledger.Clear();
            Check(ledger.Count == 0 && ledger.CountProjectiles(2) == 0, "reset clears all indexes");
            // Compare a mixed-player event history against a simple reference table.
            var expected = new Dictionary<int, Tuple<int, uint>>();
            var random = new Random(12345);
            for (int step = 0; step < 1000; step++)
            {
                int slot = random.Next(1, 65), player = random.Next(1, 9);
                uint global = (uint)step + 1;
                ledger.Add(player, new List<PlagueProjectileIdentity> { new PlagueProjectileIdentity(slot, global) });
                expected[slot] = Tuple.Create(player, global);
                if (step % 3 == 0) { ledger.ReconcileDeletedSlot(slot, _ => false); expected.Remove(slot); }
                for (int id = 1; id <= 8; id++) Check(ledger.CountHerds(id) == expected.Values.Count(x => x.Item1 == id), "mixed event-history model agreement");
            }
        }

        private static void CountdownTests()
        {
            var countdownSettings = new BugfixesAndQoLViewModel();
            new TimerCountdownFeature(new BepInEx.Logging.ManualLogSource(), countdownSettings);
            int writes = TimerCountdownViewModel.Writes, reads = CrusaderDE.GameData.scenario.Reads;
            for (int index = 0; index < 50; index++) UnityEngine.Application.Render();
            Check(TimerCountdownViewModel.Writes == writes && CrusaderDE.GameData.scenario.Reads == reads, "cold-disabled countdown does no presentation/timer work");
            foreach (bool enabled in new[] { true, false, true, false })
            {
                countdownSettings.ShowCountdownTimers = enabled;
                countdownSettings.EnableClientFeatures = enabled;
                UnityEngine.Application.Render();
                writes = TimerCountdownViewModel.Writes; reads = CrusaderDE.GameData.scenario.Reads;
                for (int index = 0; index < 50; index++) UnityEngine.Application.Render();
                Check(enabled ? CrusaderDE.GameData.scenario.Reads == reads + 50 :
                    CrusaderDE.GameData.scenario.Reads == reads && TimerCountdownViewModel.Writes == writes,
                    "countdown off/on/off/on retains active cadence and disabled idle");
            }
        }

        public static int Main()
        {
            try { LedgerTests(); RuntimeTests(); CountdownTests(); Console.WriteLine("PASS: " + checks + " mod-option/ledger/save/runtime checks (simulated publishers; no in-game FPS claim)."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
