using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Health;
using APIShared.Modifiers;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace LocalResyncAcceptance
{
    [BepInPlugin(Guid, "Local Resync Acceptance", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.72.1")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("BuildingLimit_Serp")]
    [BepInDependency("UnitLimit_Serp")]
    public sealed class AcceptancePlugin : BaseUnityPlugin
    {
        public const string Guid = "LocalResyncAcceptance_Serp";
        private void Awake() => Acceptance.Initialize(Logger);
    }

    internal sealed class Acceptance
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Static | BindingFlags.Instance;
        private readonly ManualLogSource log;
        private readonly Guid id;
        private string save;
        private int unitSlot;
        private uint unitGlobal, expectedBasis, expectedMaximum, expectedCurrent;

        private Acceptance(ManualLogSource log, Guid id) { this.log = log; this.id = id; }

        internal static void Initialize(ManualLogSource log)
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", AcceptancePlugin.Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length != 2 || lines[1] != "local-resync-owners") return;
            Require(Guid.TryParseExact(lines[0], "N", out Guid id) && id != Guid.Empty, "Invalid run ID");
            SHCDESE.BepInEx.Bootstrap.Plugin.Instance.DisableSteamUGCUpdates.Value = true;
            var api = ApiShared.ForMod(AcceptancePlugin.Guid);
            api.WhenReady(_ => Require(api.Ui.TryPost(() => {
                Require(string.Join("\n", File.ReadAllLines(request)) == string.Join("\n", lines), "Run request changed");
                File.Delete(request);
                api.Automation.Start(id, new Acceptance(log, id).Steps(), log, quitWhenDone: true);
            }), "Unity dispatch unavailable"));
        }

        private IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("start-local-skirmish", TimeSpan.FromSeconds(150), Start);
            yield return new AutomationStep("building-pause-and-limit-rebuild", TimeSpan.FromSeconds(35), CheckBuildingsAndLimits, true);
            yield return new AutomationStep("health-repair-and-save", TimeSpan.FromSeconds(40), RepairAndSave, true);
            yield return new AutomationStep("load-owned-save", TimeSpan.FromSeconds(90), Reload);
            yield return new AutomationStep("verify-health-after-load", TimeSpan.FromSeconds(35), VerifyLoaded, true);
        }

        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            object pause = Get(Runtime("BugfixesAndQoL_Serp"), "singleBuildingPauseHook");
            object settings = Get(pause, "settings");
            settings.GetType().GetProperty("EnableMod").SetValue(settings, true);
            settings.GetType().GetProperty("EnableSingleBuildingPause").SetValue(settings, true);
            Require(HealthModifiers.TryRegister(AcceptancePlugin.Guid, "double-human-archer-hp", context =>
                context.Kind == HealthObjectKind.Unit && context.UnitType == eChimps.CHIMP_TYPE_ARCHER && !context.IsAIPlayer
                    ? ValueModifierContribution.Factor(2) : ValueModifierContribution.None,
                out HealthModifierHandle health, out string reason), reason);
            health.SetEnabled(true);
            Require(HealthModifiers.TryEnableMultiplayerResync(out reason), reason);
            foreach (var wait in run.StartSkirmish("test AI.map", new[] { AILords.SK_RAT }, 60)) yield return wait;
            var mission = run.CurrentMission;
            Require(mission != null && !mission.Mode.IsRealMultiplayer && !mission.IsEditor && !mission.IsSave, "Expected a new local mission");
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 8);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            unitSlot = run.InSimulation(mission, SpawnArcher);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 24);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
        }

        private int SpawnArcher()
        {
            var players = GamePlayerManagerAPI.Instance;
            int player = players.GetLocalPlayerId();
            Require(player == 1 && players.GetPlayerKeepId(player) > 0, "Local player and keep are not ready");
            var door = players.GetPlayerKeepDoorPosition(player);
            var tiles = GameTileManagerAPI.Instance;
            for (int radius = 1; radius <= 8; radius++)
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                int x = door.X + dx, y = door.Y + dy;
                if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                int tile = tiles.GetTileId(x, y);
                if (!tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetTileBuildingId(tile) != 0) continue;
                int slot = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(player, player, x, y,
                    tiles.GetTileHeight(tile), eChimps.CHIMP_TYPE_ARCHER));
                Require(slot > 0, "Archer placement failed on a validated tile");
                Info($"TEST_ARCHER slot={slot} tile={x},{y}");
                return slot;
            }
            throw new InvalidOperationException("No free archer tile around the local keep");
        }

        private IEnumerable<AutomationWait> CheckBuildingsAndLimits(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                CheckBuildings();
                CheckUnits();
                return true;
            });
            yield break;
        }

        private void CheckBuildings()
        {
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int slot = 0;
            GameBuilding live = default;
            for (int i = 0; i < buildings.Length; i++)
            {
                ref var building = ref buildings[i];
                if (building.r_AliveState != AliveState.IsAlive || building.r_PlayerIdOwner != 1 || building.r_GlobalId == 0) continue;
                slot = i + 1;
                live = building;
                break;
            }
            Require(slot > 0, "No live test building");
            object pause = Get(Runtime("BugfixesAndQoL_Serp"), "singleBuildingPauseHook");
            Require((bool)Get(pause, "resyncReady"), "Building pause resync provider unavailable");
            int staleGlobal = int.MaxValue;
            foreach (ref var building in buildings)
                Require(building.r_GlobalId != (uint)staleGlobal, "Stale building identity is already in use");
            Call(pause, "BeginResync");
            Array imported = (Array)Call(pause, "PrepareResync", PauseRecord(staleGlobal, live));
            Require(imported.Length == 0, "Missing predecessor inherited a pause");
            Call(pause, "CommitResync", imported);
            Call(pause, "CompleteResync");
            Require(OverrideCount(pause) == 0, "Stale pause installed");
            Call(pause, "BeginResync");
            imported = (Array)Call(pause, "PrepareResync", PauseRecord(checked((int)live.r_GlobalId), live));
            Require(imported.Length == 1 && (int)Get(imported.GetValue(0), "Slot") == slot, "Valid pause did not resolve to the live slot");
            Call(pause, "CommitResync", imported);
            Call(pause, "CompleteResync");
            Require(OverrideCount(pause) == 1, "Valid pause was not installed");
            // Drop the synthetic override before allowing simulation ticks again.
            Call(pause, "BeginResync");
            Call(pause, "CommitResync", (Array)Call(pause, "PrepareResync", ReadOnlyMemory<byte>.Empty));
            Call(pause, "CompleteResync");
            Info($"PAUSE_OWNER_PASS slot={slot} liveGlobal={live.r_GlobalId} staleGlobal={staleGlobal}");

            object limit = Runtime("BuildingLimit_Serp");
            object cache = Get(limit, "activeBuildingCache");
            int expected = 0;
            foreach (ref var building in buildings)
                if (building.r_AliveState == AliveState.IsAlive && building.r_PlayerIdOwner == live.r_PlayerIdOwner &&
                    building.r_BuildingType == live.r_BuildingType) expected++;
            CorruptCount(cache, "GetActiveBuildingCount", expected, live.r_PlayerIdOwner, live.r_BuildingType);
            Call(limit, "RebuildRepairedCaches");
            Require((int)Call(cache, "GetActiveBuildingCount", live.r_PlayerIdOwner, live.r_BuildingType) == expected, "Wrong rebuilt building count");
            Info($"BUILDING_COUNT_PASS type={live.r_BuildingType} stale=0 expected={expected}");
        }

        private static ReadOnlyMemory<byte> PauseRecord(int global, GameBuilding live)
        {
            using (var data = new MemoryStream())
            using (var writer = new BinaryWriter(data))
            {
                writer.Write(global);
                writer.Write((int)live.r_PlayerIdOwner);
                writer.Write((int)live.r_BuildingType);
                writer.Write((byte)1);
                return data.ToArray();
            }
        }

        private static int OverrideCount(object pause)
        {
            object store = Get(pause, "overrides");
            return (int)store.GetType().GetProperty("Count", Members).GetValue(store);
        }

        private void CheckUnits()
        {
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            const int player = 1;
            int expected = 0;
            for (int i = 0; i < units.Length; i++)
            {
                ref var unit = ref units[i];
                if ((unit.r_AliveState != AliveState.IsAlive && unit.r_AliveState != AliveState.NeedsInit) ||
                    unit.r_ControllableForPlayerId != player || unit.r_UnitChimp != eChimps.CHIMP_TYPE_ARCHER) continue;
                expected++;
                if (i + 1 == unitSlot && unit.r_AliveState == AliveState.IsAlive) unitGlobal = unit.r_GlobalId;
            }
            Require(expected >= 1 && unitGlobal != 0, "The spawned archer is missing from the active records");
            object limit = Runtime("UnitLimit_Serp");
            object cache = Get(limit, "activeUnitCache");
            CorruptCount(cache, "GetActiveUnitCount", expected, player, eChimps.CHIMP_TYPE_ARCHER);
            Call(limit, "RebuildRepairedCaches");
            Require((int)Call(cache, "GetActiveUnitCount", player, eChimps.CHIMP_TYPE_ARCHER) == expected, "Wrong rebuilt unit count");
            Info($"UNIT_COUNT_PASS slot={unitSlot} global={unitGlobal} stale=0 expected={expected}");
        }

        private static void CorruptCount(object cache, string getter, int expected, params object[] args)
        {
            Require((int)Call(cache, getter, args) == expected && expected > 0, "Cache baseline differs from live world");
            ((IDictionary)Get(cache, "countsByOwnerAndType")).Clear();
            Require((int)Call(cache, getter, args) == 0, "Failed to inject missing cache count");
        }

        private IEnumerable<AutomationWait> RepairAndSave(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, ExerciseHealth);
            save = Path.Combine(ConfigSettings.GetSavesPath(), $"SerpAcceptance-{id:N}.sav");
            Require(!File.Exists(save), "Refusing to overwrite a save");
            yield return AutomationWait.Until("safe local save", () => Director.instance.SafeToSave());
            Director.instance.FinishedSaving();
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            Require(EngineInterface.SaveSaveGame(save, screen.x, screen.y, x, y, false, false, false) &&
                File.Exists(save) && new FileInfo(save).Length > 0, "Native test save failed");
            Info("OWNED_SAVE_WRITTEN " + Path.GetFileName(save));
        }

        private unsafe bool ExerciseHealth()
        {
            Type health = typeof(HealthModifiers).Assembly.GetType("APIShared.Health.HealthRuntime", true);
            GameUnit unit = ReadArcher();
            var records = (IDictionary)Get(Get(health, "ledger"), "Records");
            long key = ((long)HealthObjectKind.Unit << 32) | (uint)unitSlot;
            object live = records[key];
            Require(live != null && (uint)Get(live, "GlobalId") == unitGlobal, "No valid source HP basis");
            expectedBasis = (uint)Get(live, "Basis");
            expectedMaximum = checked(expectedBasis * 2);
            Require(unit.r_MaxHealth == expectedMaximum && unit.r_CurrentHealth == expectedMaximum, "Initial x2 HP not applied exactly once");
            if (APIShared.UnitAccess.TryGetById(unitSlot, out _, out _))
                GameUnitManagerAPI.Instance.SetCurrentHealth(unitSlot, checked((int)(expectedBasis + expectedBasis / 2)));
            else throw new InvalidOperationException("Cannot wound the verified test archer");
            expectedCurrent = ReadArcher().r_CurrentHealth;
            Require(expectedCurrent == expectedBasis + expectedBasis / 2 && expectedCurrent < expectedMaximum, "A reproducible wound was not established");

            byte[] hostBytes = (byte[])Call(health, "CaptureRepair");
            object predecessor = typeof(object).GetMethod("MemberwiseClone", Members).Invoke(live, null);
            predecessor.GetType().GetField("GlobalId", Members).SetValue(predecessor, uint.MaxValue);
            records[key] = predecessor;
            var staleCapture = (byte[])Call(health, "CaptureRepair");
            var stalePrepared = Call(health, "PrepareRepair", new ReadOnlyMemory<byte>(staleCapture));
            Require(!((IDictionary)Get(stalePrepared, "Records")).Contains(key), "Wrong identity passed HP filtering");
            var imported = Call(health, "PrepareRepair", new ReadOnlyMemory<byte>(hostBytes));
            var restored = ((IDictionary)Get(imported, "Records"))[key];
            Require(restored != null && (uint)Get(restored, "Basis") == expectedBasis &&
                (uint)Get(restored, "GlobalId") == unitGlobal, "Replacement health basis was not imported");
            Call(health, "CommitRepair", imported);
            unit = ReadArcher();
            Require(unit.r_MaxHealth == expectedMaximum && unit.r_CurrentHealth == expectedCurrent, "Repair rewrote native health");
            Info($"HEALTH_OWNER_PASS slot={unitSlot} global={unitGlobal} basis={expectedBasis} current={expectedCurrent} maximum={expectedMaximum}");
            return true;
        }

        private IEnumerable<AutomationWait> Reload(AutomationRun run)
        {
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            foreach (var wait in run.LoadSave(save)) yield return wait;
        }

        private IEnumerable<AutomationWait> VerifyLoaded(AutomationRun run)
        {
            var mission = run.CurrentMission;
            Require(mission.IsSave && !mission.Mode.IsRealMultiplayer, "Expected owned local save");
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 12);
            run.InSimulation(mission, () => {
                GameUnit unit = ReadArcher();
                Require(unit.r_MaxHealth == expectedMaximum && unit.r_CurrentHealth == expectedCurrent,
                    $"Loaded HP differed current={unit.r_CurrentHealth}/{expectedCurrent} maximum={unit.r_MaxHealth}/{expectedMaximum}");
                Type health = typeof(HealthModifiers).Assembly.GetType("APIShared.Health.HealthRuntime", true);
                long key = ((long)HealthObjectKind.Unit << 32) | (uint)unitSlot;
                object ledger = Get(health, "ledger");
                var restored = ((IDictionary)Get(ledger, "Records"))[key];
                Require(restored != null && (uint)Get(restored, "GlobalId") == unitGlobal &&
                    (uint)Get(restored, "Basis") == expectedBasis && (uint)Get(restored, "AppliedMaximum") == expectedMaximum,
                    "Save did not restore the original health basis and the applied multiplier");
                Info($"HEALTH_RELOAD_PASS global={unitGlobal} basis={expectedBasis} current={unit.r_CurrentHealth} maximum={unit.r_MaxHealth}");
                return true;
            });
        }

        private GameUnit ReadArcher()
        {
            var unit = GameUnitManagerAPI.Instance.GetUnitsAsSpan()[unitSlot - 1];
            Require(unit.r_GlobalId == unitGlobal && unit.r_UnitChimp == eChimps.CHIMP_TYPE_ARCHER &&
                unit.r_AliveState == AliveState.IsAlive, "The test archer's identity changed");
            return unit;
        }

        private static object Runtime(string guid)
        {
            var info = Chainloader.PluginInfos[guid];
            return Get(info.Instance, "runtime");
        }

        private static object Get(object target, string member)
        {
            Type type = target as Type ?? target.GetType();
            return (type.GetField(member, Members) ?? throw new MissingFieldException(type.FullName, member))
                .GetValue(target is Type ? null : target);
        }

        private static object Call(object target, string member, params object[] args)
        {
            Type type = target as Type ?? target.GetType();
            return (type.GetMethod(member, Members) ?? throw new MissingMethodException(type.FullName, member))
                .Invoke(target is Type ? null : target, args);
        }

        private void Info(string message) => log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] LOCAL_RESYNC {message}");

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
