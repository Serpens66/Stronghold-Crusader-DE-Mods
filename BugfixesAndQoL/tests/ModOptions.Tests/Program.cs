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
            Shared.GameplaySessionLifecycle.End();
            settings.EnableMod = enabled;
            settings.EnablePlaguePopularityFix = enabled;
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext());
        }
        // Multiplayer saves report IsSaveFile=false, and the Extender reads the record in Pre and Post.
        private static void LoadSave(byte[] bytes, bool enabled, bool loadedSave = true)
        {
            Shared.GameplaySessionLifecycle.End();
            Shared.GameplaySessionLifecycle.BeforeLoad();
            ModSaveDataAPI.Instance.Load(bytes, new LoadContext { IsSaveFile = false });
            ModSaveDataAPI.Instance.Load(bytes, new LoadContext { IsSaveFile = false });
            settings.EnableMod = enabled;
            settings.EnablePlaguePopularityFix = enabled;
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext { IsLoadedSave = loadedSave });
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
        private static void Opportunity(int player, EventHookPhase phase = EventHookPhase.Pre) =>
            PlayerR3EventHooks.OnPlayerCalculatePopularity.Emit(new SHCDESE.EventAPI.Player.PlayerCalculatePopularityEventArgs
            { Phase = phase, PlayerId = player });
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

            LoadSave(disabledSave, true);
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
            LoadSave(enabledSave, false);
            Check(Correct(2).RDX == 1000, "on-save loads with correction disabled");
            LoadSave(enabledSave, true);
            Check((short)Correct(2).RAX == -25, "second reactivation in same process works");

            // Stale-record protection: a fresh session without herds still replaces the archive entry.
            Start(true);
            PlaguePopularitySaveState empty = Decode(Save());
            Check(empty.Herds.Length == 0 && empty.ManagedPlayerIds.Length == 0, "empty ledger writes a record instead of keeping the loaded one");
            LoadSave(Save(), true);
            Check(Correct(2).RDX == 1000, "empty record clears herds of an earlier save");
            LoadSave(enabledSave, true, loadedSave: false);
            Check(Correct(2).RDX == 1000, "record is discarded when the session is not a loaded save");
            Shared.GameplaySessionLifecycle.End();
            ModSaveDataAPI.Instance.Load(enabledSave, new LoadContext());
            Shared.GameplaySessionLifecycle.BeforeLoad();
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext { IsLoadedSave = true });
            Check(Correct(2).RDX == 1000, "a new root load discards a record parked by an earlier load");
            Shared.GameplaySessionLifecycle.End();
            Check(Save().SequenceEqual(new[] { MessagePackCode.Nil }), "save without an active ledger writes nil");
            Check(ModSaveDataAPI.Instance.Save(new SaveContext { IsMapEditorSave = true }) == null, "editor saves write no plague record");
            int nilWarnings = Shared.DebugLogHelper.Warnings;
            LoadSave(new[] { MessagePackCode.Nil }, true);
            Check(Shared.DebugLogHelper.Warnings == nilWarnings && Correct(2).RDX == 1000, "nil record restores nothing without a warning");

            Create(1);
            Action stale = GameTimeManagerAPI.Instance.Timers.Pending.Values.First();
            Start(true);
            Create(1);
            int warnings = Shared.DebugLogHelper.Warnings;
            stale();
            Check(Shared.DebugLogHelper.Warnings == warnings, "old-session watchdog cannot warn for new session");
            Opportunity(1);
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings + 1, "temporary watchdog warns once");
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings + 1, "watchdog does not repeat");

            Start(true);
            warnings = Shared.DebugLogHelper.Warnings;
            Create(1);
            Opportunity(2); // Another player cannot prove an opportunity for player one.
            Opportunity(1, EventHookPhase.Post);
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings, "no matching Pre opportunity produces no warning");
            Create(1);
            Opportunity(1);
            Action oldRevision = GameTimeManagerAPI.Instance.Timers.Pending.Values.First();
            Create(1); // Replaces the watched revision, including its observed opportunities.
            oldRevision();
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings, "old revision cannot donate an opportunity or warn");
            Create(1);
            Opportunity(1);
            Action correctedTimer = GameTimeManagerAPI.Instance.Timers.Pending.Values.First();
            Correct(1);
            correctedTimer();
            Check(Shared.DebugLogHelper.Warnings == warnings, "canceled successful revision cannot warn even if queued");
            Create(2);
            Opportunity(2);
            foreach (var herd in Decode(Save()).Herds.Where(h => h.PlayerId == 2))
                foreach (int slot in herd.ProjectileSlotIds) GameProjectileManagerAPI.Instance.Pool[slot].r_AliveState = AliveState.ToDelete;
            Check((short)Correct(2).RAX == 0, "observed calculation still corrects after the last herd expires");
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings, "zero-herd success cancels watchdog");
            Create(1);
            Create(2);
            Opportunity(1);
            GameTimeManagerAPI.Instance.Timers.Fire();
            Check(Shared.DebugLogHelper.Warnings == warnings + 1, "two watched players keep calculation opportunities separate");
            Start(true);

            Shared.DebugLogHelper.FailDebug = true;
            Create(2);
            Check((short)Correct(2).RAX == -25, "debug listener failure preserves correction");
            Shared.DebugLogHelper.FailDebug = false;
            GameTimeManagerAPI.Instance.Timers.Fail = true;
            Create(2);
            Check((short)Correct(2).RAX == -50, "watchdog failure preserves correction");
            GameTimeManagerAPI.Instance.Timers.Fail = false;
            Create(1);
            Opportunity(1);
            Action disabledTimer = GameTimeManagerAPI.Instance.Timers.Pending.Values.First();
            int beforeDisable = Shared.DebugLogHelper.Warnings;
            settings.EnableMod = false;
            Check(GameTimeManagerAPI.Instance.Timers.Pending.Count == 0, "disable cancels pending diagnostics");
            disabledTimer();
            Check(Shared.DebugLogHelper.Warnings == beforeDisable, "queued callback cannot warn after disable");
            int rejections = Shared.DebugLogHelper.Warnings;
            LoadSave(new byte[] { 0xC1 }, true);
            // Each of the two load passes rejects the record; the real helper rate-limits by site.
            Check(Shared.DebugLogHelper.Warnings == rejections + 2 && Correct(1).RDX == 1000, "invalid save rejects state and preserves Vanilla");
            Check(Decode(Save()).Herds.Length == 0, "rejected save has no partial ledger");

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
            Shared.GameplaySessionLifecycle.End();
            Shared.GameplaySessionLifecycle.BeforeLoad();
            settings.EnableMod = false;
            ModSaveDataAPI.Instance.Load(compatibilitySave, new LoadContext());
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext { IsLoadedSave = true });
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

        private static bool Rejects<T>(byte[] bytes)
        {
            try { MessagePackSerializer.Deserialize<T>(bytes); return false; }
            catch (MessagePackSerializationException) { return true; }
        }

        private static byte[] Raw(params object[] fields) => MessagePackSerializer.Serialize<object>(fields);

        private static void FeatureSaveStateTests()
        {
            var pause = new SingleBuildingPauseSaveState
            {
                Entries = new[]
                {
                    new SingleBuildingPauseSaveEntry { BuildingId = 3, GlobalId = 70, Owner = 1, BuildingType = 20, IsSleeping = true },
                    new SingleBuildingPauseSaveEntry { BuildingId = 9, GlobalId = 71, Owner = 8, BuildingType = 0, IsSleeping = false }
                }
            };
            SingleBuildingPauseSaveState pauseCopy = MessagePackSerializer.Deserialize<SingleBuildingPauseSaveState>(MessagePackSerializer.Serialize(pause));
            Check(pauseCopy.Entries.Length == 2 && pauseCopy.Entries[0].BuildingId == 3 && pauseCopy.Entries[0].GlobalId == 70 &&
                pauseCopy.Entries[0].Owner == 1 && pauseCopy.Entries[0].BuildingType == 20 && pauseCopy.Entries[0].IsSleeping &&
                pauseCopy.Entries[1].Owner == 8 && !pauseCopy.Entries[1].IsSleeping, "single-building pause record round-trips every field");
            Check(MessagePackSerializer.Deserialize<SingleBuildingPauseSaveState>(MessagePackSerializer.Serialize(new SingleBuildingPauseSaveState())).Entries.Length == 0,
                "empty single-building pause record replaces stale archive state");
            Check(Rejects<SingleBuildingPauseSaveState>(Raw(2, new object[0])), "newer single-building pause version is rejected");
            Check(Rejects<SingleBuildingPauseSaveState>(Raw(1, new object[] { new object[] { 3, 70, 1, 20, true }, new object[] { 3, 71, 1, 20, false } })),
                "duplicate single-building pause building is rejected");
            Check(Rejects<SingleBuildingPauseSaveState>(Raw(1, new object[] { new object[] { 3, 70, 9, 20, true } })), "out-of-range pause owner is rejected");
            Check(Rejects<SingleBuildingPauseSaveState>(Raw(1, new object[] { new object[] { 3, 0, 1, 20, true } })), "pause entry without global ID is rejected");
            Check(Rejects<SingleBuildingPauseSaveState>(new byte[] { MessagePackCode.Nil }), "invalidated pause record restores nothing");

            var climb = new AssassinClimbSaveState { ForbiddenPlayerIds = new[] { 2, 8 } };
            AssassinClimbSaveState climbCopy = MessagePackSerializer.Deserialize<AssassinClimbSaveState>(MessagePackSerializer.Serialize(climb));
            Check(climbCopy.ForbiddenPlayerIds.SequenceEqual(new[] { 2, 8 }), "Assassin climb record round-trips forbidden players");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 2, 2 })), "duplicate Assassin climb player is rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 0 })) && Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 9 })),
                "out-of-range Assassin climb players are rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 1 })), "oversized Assassin climb record is rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(2, new object[0])), "newer Assassin climb version is rejected");

            var movement = new TroopMovementSyncSaveState
            {
                Entries = new[]
                {
                    new TroopMovementSyncSaveEntry { TribeId = 5, TribeGlobalId = 900, Running = true, RunningSpeedBonus = 4, PreviousMovementSpeed = 30, AppliedMovementSpeed = 18 },
                    new TroopMovementSyncSaveEntry { TribeId = 4500, TribeGlobalId = uint.MaxValue, Running = false, RunningSpeedBonus = 0, PreviousMovementSpeed = ushort.MaxValue, AppliedMovementSpeed = 1 }
                }
            };
            TroopMovementSyncSaveState movementCopy = MessagePackSerializer.Deserialize<TroopMovementSyncSaveState>(MessagePackSerializer.Serialize(movement));
            Check(movementCopy.Entries.Length == 2 && movementCopy.Entries[0].TribeId == 5 && movementCopy.Entries[0].TribeGlobalId == 900 &&
                movementCopy.Entries[0].Running && movementCopy.Entries[0].RunningSpeedBonus == 4 &&
                movementCopy.Entries[0].PreviousMovementSpeed == 30 && movementCopy.Entries[0].AppliedMovementSpeed == 18 &&
                movementCopy.Entries[1].TribeGlobalId == uint.MaxValue && !movementCopy.Entries[1].Running &&
                movementCopy.Entries[1].PreviousMovementSpeed == ushort.MaxValue, "troop movement record round-trips every field");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 4501, 1u, false, 0, 1, 1 } })), "tribe beyond the cadence table is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 3, 1, 1 } })), "walking cadence with a running bonus is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 0, 1, 1 }, new object[] { 5, 2u, false, 0, 1, 1 } })),
                "duplicate troop movement tribe is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 0, 1 } })), "short troop movement entry is rejected");
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

        // Automatic must not merge on 6-8 GB cards (~4 GB texture use with the merge), an explicit
        // On keeps the technical 6 GB floor, Off never merges; stored values stay strict integers.
        private static void SpriteSheetMergeModeTests()
        {
            Check(SpriteSheetMergePolicy.Refusal(SpriteSheetMergeMode.Automatic, 8191) != null, "automatic below 8 GB is refused");
            Check(SpriteSheetMergePolicy.Refusal(SpriteSheetMergeMode.Automatic, 8192) == null, "automatic from 8 GB merges");
            Check(SpriteSheetMergePolicy.Refusal(SpriteSheetMergeMode.On, 6143) != null, "on below 6 GB is refused");
            Check(SpriteSheetMergePolicy.Refusal(SpriteSheetMergeMode.On, 6144) == null, "on from 6 GB merges");
            Check(SpriteSheetMergePolicy.Refusal(SpriteSheetMergeMode.Off, 65536) != null, "off never merges");
            foreach (SpriteSheetMergeMode mode in new[] { SpriteSheetMergeMode.Automatic, SpriteSheetMergeMode.On, SpriteSheetMergeMode.Off })
                Check(MessagePackSerializer.Deserialize<SpriteSheetMergeMode>(MessagePackSerializer.Serialize(mode)) == mode, "mode roundtrip " + mode);
            foreach (byte[] invalid in new[] { MessagePackSerializer.Serialize(3), MessagePackSerializer.Serialize(true) })
            {
                bool rejected = false;
                try { MessagePackSerializer.Deserialize<SpriteSheetMergeMode>(invalid); }
                catch (MessagePackSerializationException) { rejected = true; }
                Check(rejected, "unknown stored mode is rejected");
            }
        }

        public static int Main()
        {
            try { BugfixesAndQoL.VanillaPerformance.RadarThrottleTests.Run(); SpriteSheetMergeModeTests();LedgerTests(); RuntimeTests(); CountdownTests(); FeatureSaveStateTests(); Console.WriteLine("PASS: " + checks + " mod-option/ledger/save/runtime checks (simulated publishers; no in-game FPS claim)."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
