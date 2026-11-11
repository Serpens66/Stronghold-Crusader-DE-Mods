#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace BugfixesAndQoL.PitchMoatLines
{
    // Temporary focused acceptance. Inputs enter the real local callback. The
    // readback observer independently measures completed native map ticks.
    internal static partial class PlacementAcceptance
    {
        [ThreadStatic] private static bool inputActive;
        [ThreadStatic] private static SyntheticInput currentInput;
        [ThreadStatic] private static bool syntheticEdge;
        private static ManualLogSource logger;
        private static Observation observation;
        private static int fixtureNumber;
        private static string scenarioName;

        internal readonly struct SyntheticInput
        {
            internal readonly LineTile Tile;
            internal readonly bool Held, Control, OnMap, Cancel, Shift, Focused;

            internal SyntheticInput(LineTile tile, bool held, bool control = true,
                bool onMap = true, bool cancel = false, bool shift = false, bool focused = true)
            {
                Tile = tile;
                Held = held;
                Control = control;
                OnMap = onMap;
                Cancel = cancel;
                Shift = shift;
                Focused = focused;
            }
        }

        internal static bool TryInput(out SyntheticInput input)
        {
            input = currentInput;
            return inputActive;
        }

        internal static bool IsSyntheticEdge => syntheticEdge;

        private static void ForwardSyntheticButton(EditorDirector director, int state)
        {
            bool previous = syntheticEdge;
            syntheticEdge = true;
            try { PlacementRuntime.ForwardButton(director, state); }
            finally { syntheticEdge = previous; }
        }

        internal static void Start(ModApiClient api, ManualLogSource log)
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", BugfixesAndQoLPlugin.PluginGuid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            // Other BugfixesAndQoL scenarios share this request file; leave theirs untouched.
            if (lines.Length != 2 || !ValidScenario(lines[1])) return;
            File.Delete(request);
            if (!Guid.TryParseExact(lines[0], "N", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("Invalid focused placement acceptance request.");
            logger = log;
            scenarioName = lines[1];
            api.WhenReady(ready =>
            {
                var settings = (BugfixesAndQoLViewModel)typeof(PitchMoatLinesFeature)
                    .GetField("settings", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Require(settings != null, "No configured BFQoL settings.");
                bool multiplayer = scenarioName.StartsWith("placement-multiplayer-", StringComparison.Ordinal);
                multiplayerHost = multiplayer && scenarioName.EndsWith("-host", StringComparison.Ordinal);
                bool hostOnly = scenarioName.Contains("-host-only-");
                bool clientOnly = scenarioName.Contains("-client-only-");
                bool disabled = scenarioName.StartsWith("placement-disabled-", StringComparison.Ordinal);
                bool enableMaster = scenarioName != "placement-disabled-client";
                bool enableLine = scenarioName != "placement-disabled-option" &&
                    (!hostOnly || multiplayerHost) && (!clientOnly || !multiplayerHost);
                settings.EnableClientFeatures = enableMaster;
                settings.EnablePitchMoatLines = enableLine;
                if (multiplayerHost) settings.EnableMod = false;
                Require(settings.EnableClientFeatures == enableMaster && settings.EnablePitchMoatLines == enableLine,
                    "Could not configure the local fixture settings before mission start.");
                Log($"fixture settings EnableMod={settings.EnableMod} EnableClientFeatures={enableMaster} " +
                    $"EnablePitchMoatLines={enableLine} before mission start");
                Require(EngineRunEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "acceptance-readback", ApplyBrushModifiers,
                    ObserveCompletedRun, out string reason), "Engine observation unavailable: " + reason);
                bool editor = lines[1] == "placement-throughput-editor";
                ready.Automation.Start(id, new[]
                {
                    new AutomationStep("open-map", TimeSpan.FromSeconds(multiplayer ? 900 : 120),
                        multiplayer ? OpenMultiplayer : editor ? OpenEditor : OpenSkirmish),
                    new AutomationStep("tick-throughput-payments-overlap", TimeSpan.FromSeconds(multiplayer ? 900 : 180),
                        multiplayer ? CheckMultiplayer : disabled ? CheckDisabled : editor ? CheckEditor : CheckPlacement,
                        missionBound: true)
                }, log, quitWhenDone: true);
            });
        }

        private static IEnumerable<AutomationWait> OpenEditor(AutomationRun run)
        {
            yield return AutomationWait.Until("ready main menu", () => MainViewModel.viewModelLoaded &&
                FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
                !MainViewModel.Instance.Show_IntroSequence);
            MainViewModel.Instance.ButtonMainMenuFunction("MapEditor");
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            MainViewModel.Instance.MapEditorSetupFunction("Start");
            yield return AutomationWait.Until("active map editor", () => run.CurrentMission?.IsEditor == true &&
                EditorDirector.instance != null && EditorDirector.instance.IsActive && Director.instance != null &&
                Director.instance.SimRunning && GameMap.instance != null);
        }

        private static IEnumerable<AutomationWait> CheckEditor(AutomationRun run)
        {
            int owner = EditorDirector.instance.ActivePlayerID;
            foreach (int count in new[] { 3, 64 })
            {
                foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_PITCH_DITCH, count)) yield return wait;
                foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_DUGMOAT, count)) yield return wait;
            }
            foreach (var wait in CheckEditorPlayers(run)) yield return wait;
        }

        private static IEnumerable<AutomationWait> OpenSkirmish(AutomationRun run)
        {
            foreach (var wait in run.StartSkirmish("4spielerkarte.map", new[] { AILords.SK_RAT }))
                yield return wait;
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("active skirmish", () => Director.instance.SimRunning &&
                !Director.instance.Paused && !MainViewModel.Instance.Show_HUD_Briefing);
            yield return run.WaitForTicks(mission, 4);
        }

        private static IEnumerable<AutomationWait> CheckPlacement(AutomationRun run)
        {
            int owner = EditorDirector.instance.ActivePlayerID;
            Require(owner > 0, "No local owner.");
            run.InSimulation(run.CurrentMission, () =>
            {
                int stock = Stock(owner);
                if (stock < 48)
                    GamePlayerManagerAPI.Instance.TryAddGood(owner, eGoods.STORED_PITCH_RAW, 48 - stock);
                Require(Stock(owner) >= 48, "Could not prepare test stock.");
                return true;
            });
            int initialPhase = ReadPhase(owner);
            Require(initialPhase == 0, "Fresh skirmish payment phase was " + initialPhase);
            var seen = new HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                int phase = ReadPhase(owner);
                seen.Add(phase);
                foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_PITCH_DITCH, 3))
                    yield return wait;
            }
            Require(seen.Count == 4 && ReadPhase(owner) == 0, "Did not exercise all four payment phases.");
            foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_PITCH_DITCH, 64))
                yield return wait;
            foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_MOAT, 3))
                yield return wait;
            foreach (var wait in PlaceFresh(run, owner, eMappers.MAPPER_MOAT, 64))
                yield return wait;

            // The same line is overlaid in each direction. Existing pitch slots
            // and planned-moat records must retain their identity.
            foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
            var first = FindLine(owner, eMappers.MAPPER_PITCH_DITCH, 3, false);
            foreach (var wait in Place(run, owner, eMappers.MAPPER_PITCH_DITCH, first)) yield return wait;
            int[] pitches = first.Select(tile => PitchSlot(tile, owner)).ToArray();
            foreach (var wait in Select(eMappers.MAPPER_MOAT, run)) yield return wait;
            foreach (var wait in Place(run, owner, eMappers.MAPPER_MOAT, first)) yield return wait;
            for (int i = 0; i < first.Length; i++)
                Require(PitchSlot(first[i], owner) == pitches[i] && Moat(first[i]).Owner == owner,
                    "Pitch then moat replaced the pitch record.");
            Log("pitch then planned moat preserved all pitch record identities");

            var second = FindLine(owner, eMappers.MAPPER_MOAT, 3, false);
            foreach (var wait in Place(run, owner, eMappers.MAPPER_MOAT, second)) yield return wait;
            MoatState[] moats = second.Select(Moat).ToArray();
            foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
            foreach (var wait in Place(run, owner, eMappers.MAPPER_PITCH_DITCH, second)) yield return wait;
            for (int i = 0; i < second.Length; i++)
            {
                var actual = Moat(second[i]);
                Require(actual.Index == moats[i].Index && actual.Progress == moats[i].Progress &&
                    actual.Owner == moats[i].Owner && PitchSlot(second[i], owner) > 0,
                    "Moat then pitch changed the planned moat record.");
            }
            Log("planned moat then pitch preserved all moat task identities, owners and progress");
            foreach (var wait in CheckAbortsAndPassthrough(run, owner)) yield return wait;
        }

        private static IEnumerable<AutomationWait> Select(eMappers mapper, AutomationRun run)
        {
            EditorDirector.instance.placeBuilding((Enums.eMappers)mapper);
            yield return run.WaitForTicks(run.CurrentMission, 2);
            Require(MainControls.instance.CurrentSubAction == (int)mapper, "Selected mapper was not retained.");
        }

        private static IEnumerable<AutomationWait> PlaceFresh(AutomationRun run, int owner, eMappers mapper, int count)
        {
            foreach (var wait in Select(mapper, run)) yield return wait;
            var line = FindLine(owner, mapper, count, run.CurrentMission.IsEditor);
            foreach (var wait in Place(run, owner, mapper, line)) yield return wait;
        }

        private static IEnumerable<AutomationWait> Place(AutomationRun run, int owner, eMappers mapper, LineTile[] line)
        {
            var mission = run.CurrentMission;
            var director = EditorDirector.instance;
            if (line.Length == 3)
            {
                GamePlayerManagerAPI.Instance.SetScreenCenterToTilePosition(
                    new System.Numerics.Vector2(line[1].X, line[1].Y));
                yield return AutomationWait.Until("three fixture tiles in the current map view", () =>
                    line.All(tile => VisibleTile(tile) != null));
            }
            bool pitch = mapper == eMappers.MAPPER_PITCH_DITCH;
            bool editor = mission.IsEditor;
            int stockBefore = ReadStock(owner), phaseBefore = ReadPhase(owner);
            bool[] before = line.Select(tile => Present(tile, pitch)).ToArray();
            Require(before.All(value => !value), "Fixture already contains the requested mapper.");
            inputActive = true;
            try
            {
                Send(director, 1, line[0]);
                Send(director, 2, line[line.Length - 1]);
                PreviewSnapshot preview = null;
                yield return AutomationWait.Until("quoted line", () =>
                {
                    preview = PlacementRuntime.GetPreview();
                    return preview != null && preview.Mapper == (int)mapper && preview.Tiles.Length == line.Length;
                });
                Require(preview.Available && preview.Quote.BuildableCount == line.Length &&
                    preview.Tiles.Select((tile, i) => tile.X == line[i].X && tile.Y == line[i].Y).All(value => value),
                    "Unexpected line quote or anchored raster.");
                int cost = pitch && !editor ? Charge(phaseBefore, line.Length) : 0;
                Require(preview.Quote.TotalCost == cost, "Quote disagrees with four-tile payment cycle.");
                yield return run.WaitForTicks(mission, 8);
                Require(line.All(tile => !Present(tile, pitch)) && ReadStock(owner) == stockBefore &&
                    ReadPhase(owner) == phaseBefore, "Held drag placed a field or changed stock/phase.");
                if (line.Length == 3)
                    yield return RequireVisibleGhosts(preview);
                if (pitch && !editor)
                    yield return RequireActualPitchCostRow(preview);

                Observation sample;
                lock (ManagedBoundary.EngineGate)
                {
                    Require(SimulationClock.TryGetCurrentTick(mission, out int tick), "No native release tick.");
                    sample = new Observation(mission, owner, pitch, line, stockBefore, phaseBefore, tick);
                    observation = sample;
                    Send(director, 3, line[line.Length - 1]);
                }
                yield return AutomationWait.Until("all fields confirmed by native map ticks", () =>
                {
                    lock (ManagedBoundary.EngineGate)
                    {
                        Require(sample.Failure == null, sample.Failure);
                        return sample.Count == line.Length;
                    }
                });
                yield return run.WaitForTicks(mission, 2);
                lock (ManagedBoundary.EngineGate)
                {
                    Require(sample.Failure == null, sample.Failure);
                    Require(sample.Count == line.Length && Stock(owner) == stockBefore - cost,
                        "Final field count or stock differs from the quote.");
                    Require(sample.ProgressTicks.Count == line.Length, "Missing native per-field observations.");
                    if (line.Length > 1 && !mission.Mode.IsRealMultiplayer)
                    {
                        Require(SimulationClock.TryGetElapsedTicks(sample.ProgressTicks[0],
                            sample.ProgressTicks[sample.ProgressTicks.Count - 1], out int span) && span == line.Length - 1,
                            "Line did not advance by exactly one field on each consecutive native map tick.");
                    }
                    Require(SimulationClock.TryGetElapsedTicks(sample.ReleaseTick,
                        sample.ProgressTicks[sample.ProgressTicks.Count - 1], out int elapsed), "Invalid tick elapsed time.");
                    Log($"mapper={mapper} fields={line.Length} phase={phaseBefore}->{ReadPhase(owner)} " +
                        $"stock={stockBefore}->{Stock(owner)} release={sample.ReleaseTick} elapsed={elapsed} " +
                        $"progress-ticks=[{string.Join(",", sample.ProgressTicks)}]");
                    completedObservation = sample;
                    observation = null;
                }
                if (!pitch) CheckMoatWidth(line);
                currentInput = new SyntheticInput(line[line.Length - 1], false, control: false);
                PlacementRuntime.AfterInput(director);
                yield return AutomationWait.Until("line completed", () => PlacementRuntime.GetPreview() == null);
            }
            finally
            {
                lock (ManagedBoundary.EngineGate) observation = null;
                inputActive = false;
            }
        }

        private static void ObserveCompletedRun()
        {
            lock (ManagedBoundary.EngineGate)
            {
                var sample = observation;
                if (sample == null || sample.Failure != null) return;
                try
                {
                    Require(SimulationClock.TryGetCurrentTick(sample.Mission, out int tick), "Readback lost native clock.");
                    int count = 0;
                    bool gap = false;
                    foreach (var tile in sample.Line)
                    {
                        bool present = Present(tile, sample.Pitch);
                        Require(!present || !gap, "Fields were placed out of anchored order.");
                        if (present) count++;
                        else gap = true;
                    }
                    Require(count >= sample.Count && count <= sample.Count + 1,
                        "More than one new field appeared in an engine run, or a prior field disappeared.");
                    if (count > sample.Count)
                    {
                        if (sample.ProgressTicks.Count > 0)
                            Require(SimulationClock.TryGetElapsedTicks(sample.ProgressTicks[sample.ProgressTicks.Count - 1],
                                tick, out int delta) && delta >= 1 && (sample.Mission.Mode.IsRealMultiplayer || delta == 1),
                                "Placement progress skipped or reused a native map tick.");
                        sample.ProgressTicks.Add(tick);
                        sample.ProgressStocks.Add(Stock(sample.Owner));
                        sample.ProgressPhases.Add(ReadPhase(sample.Owner));
                        sample.Count = count;
                    }
                    int cost = sample.Pitch && !sample.Mission.IsEditor ? Charge(sample.InitialPhase, count) : 0;
                    Require(Stock(sample.Owner) == sample.InitialStock - cost,
                        "Native stock progression disagrees with accepted field count.");
                    int phase = sample.Pitch && !sample.Mission.IsEditor ? (sample.InitialPhase + count) % 4 : sample.InitialPhase;
                    Require(ReadPhase(sample.Owner) == phase, "Native payment phase disagrees with accepted field count.");
                }
                catch (Exception error) { sample.Failure = error.ToString(); }
            }
        }

        private static int Charge(int phase, int count) => phase == 0 ? (count + 3) / 4 : (count + phase - 1) / 4;

        private static LineTile[] FindLine(int owner, eMappers mapper, int count, bool editor)
        {
            lock (ManagedBoundary.EngineGate)
            {
                var api = GameTileManagerAPI.Instance;
                var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
                int centerX = editor ? GameTileManagerAPI.MAX_WIDTH / 2 : keep.X;
                int centerY = editor ? GameTileManagerAPI.MAX_HEIGHT / 2 : keep.Y;
                var states = new LineTileState[64];
                var native = Native();
                for (int radius = 45; radius <= 170; radius += 5)
                {
                    foreach (int sign in new[] { 1, -1 })
                    {
                        for (int y = -radius; y <= radius; y += 5)
                        {
                            var start = new LineTile(centerX + sign * radius, centerY + y);
                            var line = Enumerable.Range(0, count).Select(i => new LineTile(start.X + i, start.Y)).ToArray();
                            if (!CleanCorridor(api, line)) continue;
                            if (native.TryQuote((int)mapper, owner, !editor, true, line, states, out var quote) &&
                                quote.BuildableCount == count)
                            {
                                Log($"fixture={++fixtureNumber} mapper={mapper} start={start.X},{start.Y} fields={count}");
                                return line;
                            }
                        }
                    }
                }
            }
            throw new InvalidOperationException("No clean natively buildable corridor for " + mapper + " count=" + count);
        }

        private static bool CleanCorridor(GameTileManagerAPI api, LineTile[] line)
        {
            const TilePropertyFlag blocked = TilePropertyFlag.Sea | TilePropertyFlag.RealityEdge |
                TilePropertyFlag.MapBorder | TilePropertyFlag.ImpassableEdge | TilePropertyFlag.IsWall |
                TilePropertyFlag.IsBuilding | TilePropertyFlag.IsTree | TilePropertyFlag.IsFarm |
                TilePropertyFlag.GoodsyardRelated | TilePropertyFlag.HasStone | TilePropertyFlag.HasIron |
                TilePropertyFlag.River | TilePropertyFlag.Ford | TilePropertyFlag.IsSwamp |
                TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat | TilePropertyFlag.PitchTrap;
            foreach (var tile in line)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int y = tile.Y + dy;
                    if (!api.IsTileInsideMapBounds(tile.X, y)) return false;
                    int id = api.GetTileId(tile.X, y);
                    if ((api.GetTilePropertyFlag(id) & blocked) != 0 || api.GetTileHeight(id) >= 12) return false;
                }
            return true;
        }

        private static NativePlacement Native() => (NativePlacement)typeof(NativePlacement)
            .GetField("rooted", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static int ReadPhase(int owner)
        {
            lock (ManagedBoundary.EngineGate)
            {
                object budget = typeof(NativePlacement).GetMethod("GetPitchBudget", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Native(), new object[] { owner });
                return (int)typeof(PitchBudget).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(budget);
            }
        }

        private static bool Present(LineTile tile, bool pitch)
        {
            lock (ManagedBoundary.EngineGate)
            {
                var api = GameTileManagerAPI.Instance;
                var flags = api.GetTilePropertyFlag(api.GetTileId(tile.X, tile.Y));
                return (flags & (pitch ? TilePropertyFlag.PitchTrap : TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsMoat)) != 0;
            }
        }

        private static int PitchSlot(LineTile tile, int owner)
        {
            lock (ManagedBoundary.EngineGate)
            {
                Require(Present(tile, true), "Missing pitch flag.");
                var slots = GameTileManagerAPI.Instance.GetPitchSlots();
                int found = 0;
                for (int i = 1; i < slots.Length; i++)
                {
                    ref readonly var entry = ref slots[i];
                    if (entry.TileX != tile.X || entry.TileY != tile.Y || entry.OwnerId != owner) continue;
                    Require(found == 0, "Duplicate pitch record.");
                    found = i;
                }
                Require(found > 0, "Missing pitch owner record.");
                return found;
            }
        }

        private static unsafe MoatState Moat(LineTile tile)
        {
            lock (ManagedBoundary.EngineGate)
            {
                var api = GameTileManagerAPI.Instance;
                int id = api.GetTileId(tile.X, tile.Y);
                int index = api.GetMoatWorkTaskIndexLayer()[id];
                if (index == 0) return default;
                Require(api.TryGetMoatWorkTaskAtTile(tile.X, tile.Y, out var task), "Missing moat task.");
                Require(task.Pointer->r_TileId == id && task.Pointer->r_TileX == tile.X && task.Pointer->r_TileY == tile.Y &&
                    (api.GetTilePropertyFlag(id) & (TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsMoat)) != 0,
                    "Incoherent moat record.");
                return new MoatState(index, task.Pointer->r_OwnerPlayerId, task.Pointer->r_Progress);
            }
        }

        private static void CheckMoatWidth(LineTile[] line)
        {
            foreach (var tile in line)
                Require(Moat(new LineTile(tile.X, tile.Y - 1)).Index == 0 &&
                    Moat(new LineTile(tile.X, tile.Y + 1)).Index == 0, "Moat exceeds one field width.");
        }

        private static void Send(EditorDirector director, int state, LineTile tile)
        {
            currentInput = new SyntheticInput(tile, state != 3);
            ForwardSyntheticButton(director, state);
        }

        private static int Stock(int owner) => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_PITCH_RAW);
        private static int ReadStock(int owner) { lock (ManagedBoundary.EngineGate) return Stock(owner); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Log(string message) => logger.LogInfo(PitchMoatLinesFeature.Stamp("PLACEMENT_ACCEPTANCE " + message));

        private sealed class Observation
        {
            internal readonly MissionContext Mission;
            internal readonly int Owner, InitialStock, InitialPhase, ReleaseTick;
            internal readonly bool Pitch;
            internal readonly LineTile[] Line;
            internal readonly List<int> ProgressTicks = new List<int>(64);
            internal readonly List<int> ProgressStocks = new List<int>(64);
            internal readonly List<int> ProgressPhases = new List<int>(64);
            internal int Count;
            internal string Failure;

            internal Observation(MissionContext mission, int owner, bool pitch, LineTile[] line,
                int stock, int phase, int releaseTick)
            {
                Mission = mission;
                Owner = owner;
                Pitch = pitch;
                Line = line;
                InitialStock = stock;
                InitialPhase = phase;
                ReleaseTick = releaseTick;
            }
        }

        private readonly struct MoatState
        {
            internal readonly int Index, Owner, Progress;
            internal MoatState(int index, int owner, int progress) { Index = index; Owner = owner; Progress = progress; }
        }
    }
}
#endif
