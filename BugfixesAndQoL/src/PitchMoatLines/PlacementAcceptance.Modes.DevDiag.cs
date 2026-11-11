#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal static partial class PlacementAcceptance
    {
        private static EditorDirector brushDirector;
        private static int brushShift = -1;

        private static bool ValidScenario(string name) => new[]
        {
            "placement-throughput", "placement-throughput-editor",
            "placement-disabled-option", "placement-disabled-client",
            "placement-multiplayer-host", "placement-multiplayer-client",
            "placement-multiplayer-host-only-host", "placement-multiplayer-host-only-client",
            "placement-multiplayer-client-only-host", "placement-multiplayer-client-only-client"
        }.Contains(name);

        private static void ApplyBrushModifiers()
        {
            lock (ManagedBoundary.EngineGate)
            {
                if (ReferenceEquals(brushDirector, null) || brushShift < 0) return;
                brushDirector.shiftPressed = brushShift != 0;
                brushDirector.ctrlPressed = false;
            }
        }

        private static IEnumerable<AutomationWait> CheckDisabled(AutomationRun run)
        {
            int owner = EditorDirector.instance.ActivePlayerID;
            PrepareLocalStock(run, owner);
            foreach (eMappers mapper in new[] { eMappers.MAPPER_PITCH_DITCH, eMappers.MAPPER_MOAT })
            {
                foreach (var wait in Select(mapper, run)) yield return wait;
                var line = FindLine(owner, mapper, 3, false);
                inputActive = true;
                try
                {
                    currentInput = new SyntheticInput(line[0], false);
                    PlacementRuntime.AfterInput(EditorDirector.instance);
                    yield return run.WaitForTicks(run.CurrentMission, 4);
                    Require(PlacementRuntime.GetPreview() == null, "Disabled feature showed a custom Ctrl hover.");
                    foreach (bool shift in new[] { false, true })
                    {
                        lock (ManagedBoundary.EngineGate)
                        {
                            var director = EditorDirector.instance;
                            director.clearMouseStateForEngine();
                            currentInput = new SyntheticInput(line[0], true, shift: shift);
                            ForwardSyntheticButton(director, 1);
                            Require(director.peekleftMouseStateForEngine() == 1,
                                "Disabled feature consumed Vanilla button down.");
                            director.clearMouseStateForEngine();
                        }
                    }
                }
                finally { inputActive = false; }
                foreach (bool shift in new[] { false, true })
                    foreach (var wait in CheckVanillaBrush(run, owner, mapper, shift)) yield return wait;
            }
            Log("disabled local option/master preserved Ctrl hover veto, Vanilla input and ordinary/Shift native brushes");
        }

        private static void PrepareLocalStock(AutomationRun run, int owner)
        {
            run.InSimulation(run.CurrentMission, () =>
            {
                int stock = Stock(owner);
                if (stock < 48) GamePlayerManagerAPI.Instance.TryAddGood(owner, eGoods.STORED_PITCH_RAW, 48 - stock);
                Require(Stock(owner) >= 48, "Could not prepare local native-brush stock.");
                return true;
            });
        }

        private static IEnumerable<AutomationWait> CheckVanillaBrush(AutomationRun run, int owner, eMappers mapper, bool shift)
        {
            var mission = run.CurrentMission;
            var director = EditorDirector.instance;
            var center = FindBrushCenter(owner, mapper);
            bool pitch = mapper == eMappers.MAPPER_PITCH_DITCH;
            int expected = pitch ? (shift ? 4 : 1) : (shift ? 25 : 9);
            var area = Enumerable.Range(-3, 7).SelectMany(y => Enumerable.Range(-3, 7)
                .Select(x => new LineTile(center.X + x, center.Y + y))).ToArray();
            bool oldShift = director.shiftPressed, oldControl = director.ctrlPressed;
            int stock = ReadStock(owner), phase = ReadPhase(owner);
            int modifierStart;
            try
            {
                lock (ManagedBoundary.EngineGate)
                {
                    brushDirector = director;
                    brushShift = shift ? 1 : 0;
                    Require(SimulationClock.TryGetCurrentTick(mission, out modifierStart), "No native modifier-start tick.");
                }
                yield return AutomationWait.Until("DLL_RunTick sampled the scoped native Shift modifier", () =>
                {
                    lock (ManagedBoundary.EngineGate)
                        return SimulationClock.TryGetCurrentTick(mission, out int now) &&
                            SimulationClock.TryGetElapsedTicks(modifierStart, now, out int elapsed) && elapsed >= 1 &&
                            Native().IsNativeShiftHeld == shift;
                });
                lock (ManagedBoundary.EngineGate)
                {
                    Require(Native().IsNativeShiftHeld == shift, "Native modifier changed before the normal mapper call.");
                    Require(PlacementRuntime.GetPreview() == null, "A custom line overlay owns the native-brush test.");
                    // Same managed delegate as Vanilla's ordinary brush. No native
                    // scope is armed, so its native width remains the stock width.
                    PlacementRuntime.ForwardMapper((int)mapper, center.X, center.Y, 0, owner, true, true, 1);
                    brushShift = -1;
                    brushDirector = null;
                    director.shiftPressed = oldShift;
                    director.ctrlPressed = oldControl;
                }
                yield return AutomationWait.Until("confirmed stock native brush footprint", () =>
                    area.Count(tile => Present(tile, pitch)) >= expected);
                yield return run.WaitForTicks(mission, 2);
                int count = area.Count(tile => Present(tile, pitch));
                Require(count == expected, $"Vanilla brush differs. mapper={mapper} shift={shift} expected={expected} got={count}");
                Require(PlacementRuntime.GetPreview() == null, "Ordinary native brush activated a custom line overlay.");
                int debit = pitch ? Charge(phase, expected) : 0;
                Require(ReadStock(owner) == stock - debit, "Native brush stock debit differs from placed pitch fields.");
                Log($"native brush mapper={mapper} shift={shift} fields={count} stock={stock}->{ReadStock(owner)}");
            }
            finally
            {
                lock (ManagedBoundary.EngineGate)
                {
                    brushShift = -1;
                    brushDirector = null;
                    director.shiftPressed = oldShift;
                    director.ctrlPressed = oldControl;
                }
            }
        }

        private static LineTile FindBrushCenter(int owner, eMappers mapper)
        {
            lock (ManagedBoundary.EngineGate)
            {
                var api = GameTileManagerAPI.Instance;
                var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
                var states = new LineTileState[49];
                for (int radius = 45; radius <= 170; radius += 5)
                    foreach (int sign in new[] { 1, -1 })
                        for (int y = -radius; y <= radius; y += 5)
                        {
                            var center = new LineTile(keep.X + sign * radius, keep.Y + y);
                            var square = Enumerable.Range(-3, 7).SelectMany(dy => Enumerable.Range(-3, 7)
                                .Select(dx => new LineTile(center.X + dx, center.Y + dy))).ToArray();
                            if (!CleanCorridor(api, square)) continue;
                            if (Native().TryQuote((int)mapper, owner, true, true, square, states, out var quote) &&
                                quote.BuildableCount == square.Length) return center;
                        }
            }
            throw new InvalidOperationException("No fully buildable empty native-brush fixture.");
        }

        private static IEnumerable<AutomationWait> CheckAbortsAndPassthrough(AutomationRun run, int owner)
        {
            var director = EditorDirector.instance;
            foreach (int reason in Enumerable.Range(0, 7))
            {
                foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
                var line = FindLine(owner, eMappers.MAPPER_PITCH_DITCH, 3, false);
                int stock = ReadStock(owner), phase = ReadPhase(owner);
                inputActive = true;
                try
                {
                    Send(director, 1, line[0]);
                    Send(director, 2, line[2]);
                    yield return AutomationWait.Until("anchored buildable line before abort", () =>
                        PlacementRuntime.GetPreview()?.Quote.BuildableCount == 3);
                    if (reason == 3)
                        director.placeBuilding(Enums.eMappers.MAPPER_MOAT);
                    if (reason == 4)
                    {
                        currentInput = new SyntheticInput(line[2], true, focused: false);
                        object runtime = typeof(PlacementRuntime).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
                            .GetValue(null);
                        typeof(PlacementRuntime).GetMethod("FocusChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(runtime, new object[] { false });
                    }
                    currentInput = new SyntheticInput(line[2], reason != 5, control: reason != 0,
                        onMap: reason != 1, cancel: reason == 2, shift: reason == 6, focused: reason != 4);
                    if (reason == 5) PlacementRuntime.AfterInput(director);
                    else
                    {
                        ForwardSyntheticButton(director, 2);
                        currentInput = new SyntheticInput(line[2], false, control: reason != 0,
                            onMap: reason != 1, cancel: reason == 2, shift: reason == 6, focused: reason != 4);
                        ForwardSyntheticButton(director, 3);
                    }
                    currentInput = new SyntheticInput(line[2], false, control: false);
                    PlacementRuntime.AfterInput(director);
                    lock (ManagedBoundary.EngineGate) director.clearMouseStateForEngine();
                    yield return run.WaitForTicks(run.CurrentMission, 8);
                    Require(PlacementRuntime.GetPreview() == null && line.All(tile => !Present(tile, true)) &&
                        ReadStock(owner) == stock && ReadPhase(owner) == phase,
                        "Cancelled gesture later placed or charged fields. reason=" + reason);
                    Log("nonplacement abort passed reason=" + new[]
                    {
                        "Ctrl-release", "invalid-map-target-release", "cancel-signal", "tool-change",
                        "focus-loss-handler", "primary-release-without-edge", "Shift-pressed"
                    }[reason]);
                }
                finally { inputActive = false; }
            }
            foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
            var target = FindLine(owner, eMappers.MAPPER_PITCH_DITCH, 1, false)[0];
            inputActive = true;
            try
            {
                foreach (bool shift in new[] { false, true })
                    lock (ManagedBoundary.EngineGate)
                    {
                        director.clearMouseStateForEngine();
                        currentInput = new SyntheticInput(target, true, control: false, shift: shift);
                        ForwardSyntheticButton(director, 1);
                        Require(director.peekleftMouseStateForEngine() == 1,
                            "Enabled line feature consumed ordinary or Shift brush input.");
                        director.clearMouseStateForEngine();
                    }
            }
            finally { inputActive = false; }
        }

        private static GameMapTile VisibleTile(LineTile tile)
        {
            var map = GameMap.instance;
            map.mapGameTileToTilemapCoord(tile.X, tile.Y, out int x, out int y);
            var visible = map.getMapTile(x, y);
            return visible != null && visible.gameMapX == tile.X && visible.gameMapY == tile.Y ? visible : null;
        }

        private static AutomationWait RequireVisibleGhosts(PreviewSnapshot preview)
        {
            return AutomationWait.Until("actual visible native preview sprites", () =>
            {
                var view = EditorDirector.instance.GetComponent<PlacementPreview>();
                if (view == null || !view.enabled) return false;
                var visible = preview.Tiles.Select(VisibleTile).ToArray();
                if (visible.Any(tile => tile == null)) return false;
                var renderers = view.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.name == "Pitch or moat preview").ToArray();
                if (renderers.Length != visible.Length) return false;
                bool pitch = preview.Mapper == (int)eMappers.MAPPER_PITCH_DITCH;
                var stockPitch = pitch ? spriteLoader.instance.GetGMSprite(Enums.GM.GM_PITCH_DITCHES, 1) : null;
                for (int i = 0; i < visible.Length; i++)
                {
                    var renderer = renderers[i];
                    var sprite = pitch ? stockPitch : visible[i].tileImage;
                    Require(sprite != null && ReferenceEquals(renderer.sprite, sprite) &&
                        Mathf.Abs(renderer.color.a - 0.5f) < 0.001f, "Wrong native preview sprite or opacity.");
                    if (!pitch)
                    {
                        var blue = TilemapManager.tileColBlue;
                        Require(Mathf.Abs(renderer.color.r - blue.r) < 0.001f &&
                            Mathf.Abs(renderer.color.g - blue.g) < 0.001f &&
                            Mathf.Abs(renderer.color.b - blue.b) < 0.001f, "Wrong native moat tint.");
                    }
                }
                Log($"actual renderer mapper={preview.Mapper} visible={renderers.Length} alpha=0.5 native sprites MATCH");
                return true;
            });
        }

        private static AutomationWait RequireActualPitchCostRow(PreviewSnapshot preview)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool clearedPanel = false;
            return AutomationWait.Until("actual Vanilla pitch cost and stock row", () =>
            {
                var main = MainViewModel.Instance;
                main.CrossThreadRolloverUpdate();
                var hud = main.HUDmain;
                hud.UpdateRollover();
                var icon = main.GameSprites[(int)Enums.eUISprites.SPRITE_GOODS_SMALL_PITCH];
                bool first = ReferenceEquals(main.RollOverText_GoodsImage1, icon) &&
                    AmountMatches(main.RollOverText_AmountReq1, main.RollOverText_AmountGot1, preview.Quote);
                bool second = ReferenceEquals(main.RollOverText_GoodsImage2, icon) &&
                    AmountMatches(main.RollOverText_AmountReq2, main.RollOverText_AmountGot2, preview.Quote);
                if (icon != null && (first || second))
                {
                    Log($"actual Vanilla HUD pitch cost={preview.Quote.TotalCost} stock={preview.Quote.Stock} native icon MATCH");
                    return true;
                }
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                bool other = (bool)typeof(HUD_Main).GetField("OtherVisible", flags).GetValue(hud);
                bool priority = (bool)typeof(HUD_Main).GetField("OtherHighestPri", flags).GetValue(hud);
                if (!clearedPanel && other && priority)
                {
                    Log("fixture has a foreign engine-panel hover, clearing it through public SetEnginePanelText");
                    hud.SetEnginePanelText(0, 0, true);
                    clearedPanel = true;
                    return false;
                }
                if (watch.Elapsed.TotalSeconds > 2)
                    throw new InvalidOperationException($"Vanilla pitch row mismatch. expected={preview.Quote.TotalCost}/{preview.Quote.Stock} " +
                        $"row1=[{main.RollOverText_AmountReq1}|{main.RollOverText_AmountGot1}] " +
                        $"row2=[{main.RollOverText_AmountReq2}|{main.RollOverText_AmountGot2}] " +
                        $"title={main.RollOverText} hover={hud.HoverString} other={other}/{priority}");
                return false;
            });
        }

        private static bool AmountMatches(string need, string got, LineQuote quote) =>
            int.TryParse(need?.Trim(), out int cost) && cost == quote.TotalCost &&
            int.TryParse(got?.Trim().Trim('(', ')'), out int stock) && stock == quote.Stock;

        private static int ReadNativeConstant(string name)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            int rva = (int)typeof(NativePlacement).GetField(name, flags).GetRawConstantValue();
            return (int)typeof(NativePlacement).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Native(), new object[] { rva });
        }

        private static int EditorPitchActor(int selected)
        {
            lock (ManagedBoundary.EngineGate)
            {
                int special = (int)typeof(NativePlacement).GetField("EditorPitchOwnerMapType",
                    BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
                return ReadNativeConstant("NativeMapTypeRva") == special
                    ? ReadNativeConstant("EditorChoreActorRva") : selected;
            }
        }

        private static IEnumerable<AutomationWait> CheckEditorPlayers(AutomationRun run)
        {
            var mission = run.CurrentMission;
            var director = EditorDirector.instance;
            director.SetEditorPlayerID(2);
            Require(director.ActivePlayerID == 2, "Public EditorDirector player switch was not retained.");
            foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
            var pitch = FindLine(2, eMappers.MAPPER_PITCH_DITCH, 3, true);
            int actor = EditorPitchActor(2);
            foreach (var wait in Place(run, 2, eMappers.MAPPER_PITCH_DITCH, pitch)) yield return wait;
            foreach (var tile in pitch) PitchSlot(tile, actor);
            foreach (var wait in Select(eMappers.MAPPER_DUGMOAT, run)) yield return wait;
            var moat = FindLine(2, eMappers.MAPPER_DUGMOAT, 3, true);
            foreach (var wait in Place(run, 2, eMappers.MAPPER_DUGMOAT, moat)) yield return wait;
            foreach (var tile in moat)
            {
                Require(Moat(tile).Owner == 2, "Player2 completed-moat owner differs from the Chore sender.");
                var api = GameTileManagerAPI.Instance;
                Require((api.GetTilePropertyFlag(api.GetTileId(tile.X, tile.Y)) &
                    (TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat)) == TilePropertyFlag.IsMoat,
                    "Editor player2 moat was not completed.");
            }
            Log($"editor player2 short lines passed, pitchActor={actor} moatOwner=2");
            foreach (var wait in Select(eMappers.MAPPER_PITCH_DITCH, run)) yield return wait;
            var line = FindLine(2, eMappers.MAPPER_PITCH_DITCH, 64, true);
            inputActive = true;
            Observation sample = null;
            try
            {
                Send(director, 1, line[0]);
                Send(director, 2, line[63]);
                yield return AutomationWait.Until("player2 interruptible64field quote", () =>
                    PlacementRuntime.GetPreview()?.Quote.BuildableCount == 64);
                lock (ManagedBoundary.EngineGate)
                {
                    Require(SimulationClock.TryGetCurrentTick(mission, out int tick), "No editor release tick.");
                    sample = new Observation(mission, 2, true, line, Stock(2), ReadPhase(2), tick);
                    observation = sample;
                    Send(director, 3, line[63]);
                }
                yield return AutomationWait.Until("released player2 line has a strict partial prefix", () =>
                {
                    lock (ManagedBoundary.EngineGate)
                    {
                        Require(sample.Failure == null, sample.Failure);
                        return sample.Count > 0 && sample.Count < 64;
                    }
                });
                NativePlacement.PendingNativeCommand pending;
                int before;
                bool wasLive;
                const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                lock (ManagedBoundary.EngineGate)
                {
                    before = sample.Count;
                    pending = (NativePlacement.PendingNativeCommand)typeof(NativePlacement).GetField("pendingCommand", fields)
                        .GetValue(Native());
                    Require(pending.Valid && pending.Sender == 2, "Released line lost its captured source sender.");
                    wasLive = Native().IsCommandPending(pending);
                    director.SetEditorPlayerID(1);
                    Require(director.ActivePlayerID == 1, "Player1 switch was not retained.");
                    currentInput = new SyntheticInput(line[63], false, control: false);
                    PlacementRuntime.AfterInput(director);
                }
                yield return AutomationWait.Until("captured old-player native command retired", () =>
                {
                    lock (ManagedBoundary.EngineGate) return !Native().IsCommandPending(pending);
                });
                yield return run.WaitForTicks(mission, 8);
                lock (ManagedBoundary.EngineGate)
                {
                    Require(sample.Failure == null && sample.Count >= before && sample.Count <= before + 1 && sample.Count < 64,
                        "Player switch continued or corrupted the remaining line.");
                    var after = (NativePlacement.PendingNativeCommand)typeof(NativePlacement).GetField("pendingCommand", fields)
                        .GetValue(Native());
                    Require(after.Sender == pending.Sender && after.CommandId == pending.CommandId,
                        "Player switch changed the captured native command identity.");
                    foreach (var tile in line.Take(sample.Count)) PitchSlot(tile, actor);
                    Log($"editor released-line switch2->1 prefix={before}->{sample.Count} " +
                        $"queuedSender={pending.Sender} commandId={pending.CommandId} wasLive={wasLive} pitchActor={actor}");
                }
            }
            finally
            {
                lock (ManagedBoundary.EngineGate) observation = null;
                inputActive = false;
            }
        }
    }
}
#endif
