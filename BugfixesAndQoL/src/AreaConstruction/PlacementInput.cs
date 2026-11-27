using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Input;
using APIShared.Presentation;
using APIShared.Simulation;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using UnityEngine;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed class PlacementInput
    {
        private const int BuildAction = 5;
        private const int WorldAxisSize = 800;
        private const int CommitPerUpdate = 24;
        private static readonly long QuoteInterval = Stopwatch.Frequency / 8;
        private static PlacementInput instance;

        private readonly object engine;
        private readonly ManualLogSource log;
        private readonly List<Tile> grid = new List<Tile>();
        private readonly List<Site> shown = new List<Site>();
        private readonly Queue<(Tile Cursor, eMappers Mapper)> toBuild = new Queue<(Tile, eMappers)>();
        private readonly List<Tile> storageOrder = new List<Tile>();
        private readonly HashSet<Tile> storageUnvisited = new HashSet<Tile>();
        private readonly Dictionary<Tile, int> storageFrames = new Dictionary<Tile, int>();
        private readonly List<Tile> quarryPiles = new List<Tile>(4);
        private StorageBuildOrder storageBuild;
        private NativePlacementFeedback nativeFeedback;
        private PlacementFeedback buildFeedback;
        private bool storageClockWarningLogged;
        private readonly GhostView view = new GhostView();
        private IMissionPlayStateReadinessCapability readiness;
        private MissionContext mission;
        private NativePlacement native;
        private NativeQuarryPlacement quarry;
        private QuarryNetwork quarryNetwork;
        private IUiFrameWorkRegistration networkWork;
        private QuarryOrder quarryOrder;
        private bool quarryTickReady;
        private GameMap map;
        private Tile start, end;
        private eMappers tool;
        private int actor, scale;
        private bool editor, captured, dragging, faulted, quarryBlock;
        private long nextQuote;
        private int cancellation, appliedCancellation;

        private PlacementInput(object engine, ManualLogSource log)
        {
            this.engine = engine;
            this.log = log;
        }

        internal static PlacementInput Create(ManualLogSource log) => new PlacementInput(EngineThreadLock.Gate, log);

        internal void Install(CrusaderLibraryLoadContext context, StorageEmptyLimitPatch storageLimit)
        {
            try
            {
                Volatile.Write(ref quarry, NativeQuarryPlacement.TryInstall(context, log));
                Volatile.Write(ref native, new NativePlacement(context, log, storageLimit));
                nativeFeedback = NativePlacementFeedback.Install(context, log);
                MethodInfo update = typeof(EditorDirector).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo button = typeof(EditorDirector).GetMethod(nameof(EditorDirector.updateLeftMouseStateForEngine), new[] { typeof(int) });
                MethodInfo place = typeof(EngineInterface).GetMethod(nameof(EngineInterface.PlaceMapperItem),
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int) });
                if (update == null || button == null || place == null)
                    throw new MissingMethodException("Vanilla construction/input boundary");

                var harmony = new Harmony(BugfixesAndQoLPlugin.PluginGuid + ".AreaConstruction.Input");
                harmony.Patch(button, prefix: new HarmonyMethod(typeof(PlacementInput), nameof(Primary)));
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(PlacementInput), nameof(AfterInput)));
                harmony.Patch(place, prefix: new HarmonyMethod(typeof(PlacementInput), nameof(AllowCursor)));
                Application.focusChanged += FocusChanged;
                Volatile.Write(ref instance, this);
                log.LogInfo(Stamp("Native placement bindings ready."));
            }
            catch (Exception error) { Fail(error); }
        }

        internal void RegisterLifecycle(ModApiClient api)
        {
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic) ||
                !api.TryGetMissionLifecycle(out var lifecycle, out diagnostic) ||
                !lifecycle.TryRegisterObserver("area-construction", OnStarted, OnEnded, null, out diagnostic))
            {
                Fail(new InvalidOperationException(diagnostic.Reason));
                return;
            }
            quarryTickReady = SimulationTickEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "area-construction.quarry", QuarryTick, out string reason);
            if (!quarryTickReady)
                log.LogError(Stamp("Offline quarry coordination unavailable: " + reason));
            try
            {
                if (!api.Ui.TryRegisterBeforeRender("area-construction.network-pump", PumpNetwork, out networkWork, out reason))
                    throw new InvalidOperationException(reason);
                quarryNetwork = new QuarryNetwork(api, log, () => Volatile.Read(ref quarry)?.IsIntact == true,
                    ReceiveQuarries, active => networkWork.SetContinuous(active), () => networkWork.Request());
            }
            catch (Exception error) { log.LogError(Stamp("Quarry coordination unavailable: " + error.Message)); }
        }

        private void PumpNetwork()
        {
            try { quarryNetwork?.Pump(); }
            catch (Exception error)
            {
                networkWork.SetContinuous(false);
                quarryNetwork?.SuspendSending();
                log.LogError(Stamp("Quarry coordination pump stopped: " + error));
            }
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            lock (engine)
            {
                captured = false;
                ClearLocalWork();
                native?.ValidateStorage();
                nativeFeedback?.Recheck();
                quarry?.Recheck();
                mission = notification.Context;
            }
            log.LogInfo(Stamp("Area-construction mission began."));
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            lock (engine)
            {
                if (mission?.SessionId != notification.Context.SessionId)
                    return;
                mission = null;
                captured = false;
                ClearLocalWork();
            }
        }

        private void FocusChanged(bool focused)
        {
            if (focused) return;
            if (dragging)
                lock (engine) dragging = false;
            view.Hide();
        }

        // Setting notifications can arrive off the Unity thread. Consume the cancellation under
        // the engine monitor before any local input, even after a rapid disable and re-enable.
        internal void CancelLocalWork() => Interlocked.Increment(ref cancellation);

        private bool ApplyLocalActivation()
        {
            int generation = Volatile.Read(ref cancellation);
            bool enabled = AreaConstructionFeature.LocalEnabled;
            if (generation != appliedCancellation || !enabled)
            {
                ClearLocalWork();
                appliedCancellation = generation;
            }
            return enabled;
        }

        private void ClearLocalWork()
        {
            dragging = false;
            toBuild.Clear();
            buildFeedback = null;
            storageBuild = null;
            quarryOrder = null;
        }

        private static bool Primary(EditorDirector __instance, int __0)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null || !Shared.UnityMainThreadDispatch.IsMainThread)
                return true;
            // An owned gesture keeps its release/cancellation path. Other inputs
            // belong to Vanilla until local construction can actually begin.
            if (!runtime.captured)
            {
                if (__0 != 1 || !AreaConstructionFeature.LocalEnabled)
                    return true;
                var controls = MainControls.instance;
                if (controls == null || controls.CurrentAction != BuildAction)
                    return true;
            }
            lock (runtime.engine)
            {
                try
                {
                    if (!runtime.ApplyLocalActivation())
                    {
                        if (__0 == 1) runtime.captured = false;
                        return !runtime.captured;
                    }
                    return !runtime.HandleButton(__instance, __0);
                }
                catch (Exception error)
                {
                    runtime.Fail(error);
                    return !runtime.captured;
                }
            }
        }

        private bool HandleButton(EditorDirector director, int state)
        {
            if (state == 1)
            {
                if (captured)
                    dragging = false;
                captured = false;
                if (toBuild.Count != 0 || storageBuild != null || quarryOrder != null ||
                    !TryTarget(director, out var selected, out int player, out bool isEditor) ||
                    !TryPointerTile(director, false, out var pointer))
                    return false;

                int footprint = BuildingScales.GetScale(selected);
                int frame = native.Frame(selected, player);
                if ((uint)(footprint - 1) >= NativePlacement.MaximumFootprint ||
                    frame <= 0 || !native.Begin(selected, player, isEditor) ||
                    (NativePlacement.IsStorage(selected) && !native.Reserve(selected, player, isEditor, pointer, footprint, ref frame)))
                    return false;

                start = end = pointer;
                tool = selected;
                actor = player;
                editor = isEditor;
                scale = footprint;
                map = GameMap.instance;
                captured = dragging = true;
                nextQuote = 0;
                shown.Clear();
                quarryBlock = false;
                storageOrder.Clear();
                director.clearMouseStateForEngine();
                return true;
            }

            // Vanilla uses state 0 over Noesis and can omit the setter entirely.
            // The director postfix owns the physical release for this gesture.
            return captured;
        }

        private static bool AllowCursor(int __0, int __4, bool __6)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null || !__6 || !Shared.UnityMainThreadDispatch.IsMainThread || !runtime.dragging)
                return true;
            lock (runtime.engine)
                return !runtime.ApplyLocalActivation() || !runtime.dragging ||
                    !PlacementVariants.SameSelection(runtime.tool, (eMappers)__0) || runtime.actor != __4;
        }

        private static void AfterInput(EditorDirector __instance)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null)
                return;

            try
            {
                // Unity input creates local work; other threads only cancel or consume it.
                // Keep idle frames outside the shared simulation monitor.
                if (!runtime.captured && !runtime.dragging && runtime.toBuild.Count == 0 &&
                    runtime.storageBuild == null && Volatile.Read(ref runtime.quarryOrder) == null &&
                    Volatile.Read(ref runtime.cancellation) == runtime.appliedCancellation)
                {
                    runtime.view.Hide();
                    return;
                }

                bool draw;
                QuarryOrder outbound = null;
                int generation = 0;
                lock (runtime.engine)
                {
                    draw = runtime.OnDirectorFrame(__instance);
                    if (runtime.quarryOrder != null && runtime.mission?.Mode.IsRealMultiplayer == true)
                    {
                        outbound = runtime.quarryOrder;
                        runtime.quarryOrder = null;
                        generation = runtime.appliedCancellation;
                    }
                }
                if (outbound != null && AreaConstructionFeature.LocalEnabled && generation == Volatile.Read(ref runtime.cancellation) &&
                    !runtime.quarryNetwork.TrySubmit(outbound.SessionId, outbound.Actor, outbound.Block, outbound.BuildSites, out string reason))
                    runtime.log.LogWarning(Stamp("Quarry block was not submitted: " + reason));

                // Rendering and visibility tests are Unity work. Do not hold
                // the simulation monitor while walking visible scene objects.
                if (draw && AreaConstructionFeature.LocalEnabled)
                {
                    if (!runtime.view.Show(__instance, runtime.map, runtime.tool, runtime.scale, runtime.shown, runtime.actor))
                    {
                        runtime.shown.Clear();
                        runtime.storageOrder.Clear();
                    }
                }
                else runtime.view.Hide();
            }
            catch (Exception error)
            {
                runtime.Fail(error);
                runtime.view.Hide();
            }
        }

        private bool OnDirectorFrame(EditorDirector director)
        {
            if (!ApplyLocalActivation())
            {
                if (captured && (InputState.IsMouseReleased(0) || !InputState.IsMouseHeld(0)))
                    captured = false;
                return false;
            }
            if (captured)
            {
                bool released = InputState.IsMouseReleased(0);
                if (released || !InputState.IsMouseHeld(0))
                {
                    if (released && dragging && MatchesTarget(director) && !CancelPressed())
                    {
                        if (CrewPlacement.Required(tool) != 0)
                        {
                            if (TryPointerTile(director, true, out var releaseTile))
                                end = releaseTile;
                            Quote();
                        }
                        buildFeedback = nativeFeedback?.Available == true && !start.Equals(end)
                            ? new PlacementFeedback() : null;
                        if (tool == eMappers.MAPPER_QUARRY && quarryBlock)
                        {
                            var accepted = new List<Tile>(4);
                            foreach (var site in shown)
                                if (site.Buildable) accepted.Add(site.Cursor);
                            if (accepted.Count != 0)
                            {
                                quarryOrder = new QuarryOrder(mission.SessionId, actor, grid.ToArray(), accepted.ToArray());
                                if (editor)
                                {
                                    var order = quarryOrder;
                                    quarryOrder = null;
                                    BuildQuarries(order.Actor, order.Block, order.BuildSites, true);
                                }
                            }
                        }
                        else if (tool == eMappers.MAPPER_QUARRY)
                            toBuild.Enqueue((start, tool));
                        else if (NativePlacement.IsStorage(tool))
                            ReleaseStorage();
                        else
                        {
                            foreach (var site in shown)
                                if (site.Buildable) toBuild.Enqueue((site.Cursor, site.Mapper ?? tool));
                        }
                    }
                    captured = dragging = false;
                }
            }

            if (storageBuild != null)
            {
                DrainStorage(director);
                return false;
            }

            if (!dragging && toBuild.Count == 0)
                return false;

            if (!MatchesTarget(director) || CancelPressed())
            {
                dragging = false;
                toBuild.Clear();
                quarryOrder = null;
                return false;
            }

            if (map.pendingRotationtriggered)
            {
                nextQuote = 0;
                return false;
            }
            if (toBuild.Count != 0)
                Commit();
            if (!dragging)
                return false;

            if (TryPointerTile(director, true, out var pointer))
            {
                long now = Stopwatch.GetTimestamp();
                if (!pointer.Equals(end) || now >= nextQuote)
                {
                    end = pointer;
                    nextQuote = now + QuoteInterval;
                    Quote();
                }
            }

            return true;
        }

        private void Quote()
        {
            shown.Clear();
            bool canProbe = native.Begin(tool, actor, editor);
            int frame = native.Frame(tool, actor);
            if (tool == eMappers.MAPPER_QUARRY)
            {
                quarryBlock = QuarryLayout.Fill(grid, start, end);
                quarryPiles.Clear();
                Predicate<Tile> permits = pile => QuarryLayout.AllowsPile(pile, grid, quarryPiles);
                foreach (Tile tile in grid)
                {
                    int image = frame;
                    Tile pile = default;
                    bool hasPile = canProbe && quarryBlock && quarry.TryChoose(actor, tile, permits, out pile);
                    bool buildable = canProbe && (!quarryBlock || hasPile) &&
                        native.ReserveVisual(tool, actor, editor, tile, scale, ref image);
                    if (buildable && hasPile)
                        quarryPiles.Add(pile);
                    shown.Add(new Site(tile, buildable, image, pile: hasPile ? (Tile?)pile : null));
                }
                return;
            }
            if (NativePlacement.IsStorage(tool))
            {
                StorageGrid.Fill(grid, start, end, scale, scale, WorldAxisSize);
                storageFrames.Clear();
                storageOrder.Clear();
                if (canProbe)
                    StorageGrid.CollectConnected(grid, scale, scale, QuoteStorage, storageOrder, storageUnvisited);
                foreach (Tile tile in grid)
                    shown.Add(storageFrames.TryGetValue(tile, out int image)
                        ? new Site(tile, true, image)
                        : new Site(tile, false, frame));
                return;
            }

            if (tool == eMappers.MAPPER_KILLING_PIT || NativePlacement.IsFarm(tool))
                StorageGrid.Fill(grid, start, end, scale, scale, WorldAxisSize);
            else
                AreaGrid.Fill(grid, start, end, scale, NativePlacement.FootprintHeight(tool, scale), WorldAxisSize,
                    spaceEveryPlacement: CrewPlacement.Required(tool) != 0);
            eMappers variant = tool;
            foreach (var tile in grid)
            {
                int image = variant == tool ? frame : native.Frame(variant, actor);
                bool buildable = canProbe && native.ReserveVisual(variant, actor, editor, tile, scale, ref image);
                shown.Add(new Site(tile, buildable, image, buildable ? native.LastPreviewTiles : null, mapper: variant));
                if (buildable)
                    variant = PlacementVariants.Next(variant);
            }
        }

        private bool QuoteStorage(Tile tile, Tile? predecessor)
        {
            int frame = native.Frame(tool, actor);
            if (!native.ReserveStoragePreview(tool, actor, editor, tile, scale, ref frame, predecessor.HasValue))
                return false;
            storageFrames.Add(tile, frame);
            return true;
        }

        private void ReleaseStorage()
        {
            if (storageOrder.Count != 0)
                storageBuild = new StorageBuildOrder(mission.SessionId, map, actor, editor, tool, scale, storageOrder.ToArray(), buildFeedback);
        }

        private void DrainStorage(EditorDirector director)
        {
            var order = storageBuild;
            if (mission?.SessionId != order.SessionId || !ReferenceEquals(GameMap.instance, order.Map) ||
                GameData.scenario?.InGameoverSituation == true)
            {
                storageBuild = null;
                return;
            }

            if (director == null || !director.IsActive)
                return;
            if (director.ActivePlayerID != order.Actor)
            {
                storageBuild = null;
                return;
            }

            if (Director.instance == null || Director.instance.Paused || GameData.scenario == null ||
                order.Map.pendingRotationtriggered || (!order.Editor && readiness?.Current?.SessionId != order.SessionId))
                return;

            // The engine monitor protects the clock read until the native state for that tick is stable.
            int? currentTick = null;
            if (!order.Editor)
            {
                if (SimulationClock.TryGetCurrentTick(mission, out int tick))
                    currentTick = tick;
                else if (!storageClockWarningLogged)
                {
                    storageClockWarningLogged = true;
                    log.LogWarning(Stamp("Native map tick unavailable; area storage uses frame cadence until it returns."));
                }
            }

            if (!order.Cadence.TryBegin(currentTick) ||
                !native.BeginQueuedStorage(order.Tool, order.Actor, order.Editor) || !native.HasQueueSpace)
                return;

            // The next section can use only an initialized neighbor, even after the native Chore has finished.
            if (order.Awaiting is Tile inFlight && (native.IsPending(inFlight) ||
                native.IsStorageInitializing(order.Tool, order.Actor, inFlight, order.Scale)))
                return;

            int first = order.Next;
            bool submitted = false;
            while (order.Next < order.Tiles.Length && order.Next - first < CommitPerUpdate)
            {
                if (!ApplyLocalActivation() || storageBuild != order)
                    return;
                Tile tile = order.Tiles[order.Next++];
                if (!native.TryPlaceQueuedStorage(order.Tool, order.Actor, order.Editor, tile, order.Scale, order.Feedback))
                    continue;
                native.Submitted();
                order.Awaiting = tile;
                submitted = true;
                break;
            }

            if (order.Next == order.Tiles.Length)
                storageBuild = null;
            if (order.Next > first)
                log.LogInfo(Stamp("Rechecked " + (order.Next - first) + " storage sites, submitted " + (submitted ? 1 : 0) + " Vanilla build inputs."));
        }

        private void Commit()
        {
            if (!native.Begin(tool, actor, editor) || !native.HasQueueSpace)
                return;
            if (native.HasPendingCrewOrder)
                return;
            if (!native.HasCrew)
            {
                toBuild.Clear();
                return;
            }

            // Each crew order may change the selection. Read it again after Vanilla has processed the order.
            bool usesCrew = !editor && CrewPlacement.Required(tool) != 0;
            int attempted = 0, submitted = 0;
            while (toBuild.Count != 0 && attempted < CommitPerUpdate)
            {
                if (!ApplyLocalActivation() || toBuild.Count == 0)
                    break;
                if (!native.HasQueueSpace)
                    break;
                var (tile, mapper) = toBuild.Dequeue();
                attempted++;
                int unused = 0;
                if (!native.Reserve(mapper, actor, editor, tile, scale, ref unused))
                    continue;
                native.Place(mapper, tile, actor, editor, buildFeedback);
                native.Submitted();
                submitted++;
                if (usesCrew)
                    break;
            }
            if (attempted > 0)
                log.LogInfo(Stamp("Rechecked " + attempted + " area sites, submitted " + submitted + " Vanilla build inputs."));
        }

        private void QuarryTick(int ignored)
        {
            if (Volatile.Read(ref quarryOrder) == null)
                return;
            lock (engine)
            {
                if (!ApplyLocalActivation() || mission == null || mission.IsEditor || mission.Mode.IsRealMultiplayer || quarryOrder == null)
                    return;
                var order = quarryOrder;
                quarryOrder = null;
                if (order.SessionId == mission.SessionId && readiness?.Current?.SessionId == mission.SessionId)
                    BuildQuarries(order.Actor, order.Block, order.BuildSites, false);
            }
        }

        private void ReceiveQuarries(int issuer, Tile[] block, Tile[] buildSites)
        {
            lock (engine)
                if (mission?.Mode.IsRealMultiplayer == true && readiness?.Current?.SessionId == mission.SessionId)
                    BuildQuarries(issuer, block, buildSites, false);
        }

        private void BuildQuarries(int player, Tile[] block, Tile[] buildSites, bool isEditor)
        {
            if (quarry?.IsIntact != true || !QuarryLayout.IsBlock(block))
                return;

            // Use live occupancy and cost for each candidate after the preceding build.
            var acceptedPiles = new List<Tile>(4);
            Predicate<Tile> permits = pile => QuarryLayout.AllowsPile(pile, block, acceptedPiles);
            foreach (Tile tile in block)
            {
                if (Array.IndexOf(buildSites, tile) < 0 || !quarry.RefreshBeforeBuild() ||
                    !quarry.TryChoose(player, tile, permits, out var pile))
                    continue;
                if (quarry.TryBuild(player, tile, pile, isEditor))
                    acceptedPiles.Add(pile);
            }
        }

        private bool MatchesTarget(EditorDirector director) =>
            TryTarget(director, out var selected, out int player, out bool isEditor) &&
            PlacementVariants.SameSelection(tool, selected) && player == actor && isEditor == editor && ReferenceEquals(GameMap.instance, map);

        private bool TryTarget(EditorDirector director, out eMappers selected, out int player, out bool isEditor)
        {
            selected = default;
            player = 0;
            isEditor = false;
            if (faulted || Volatile.Read(ref native) == null || mission == null || !Application.isFocused ||
                director == null || !director.IsActive || Director.instance == null || Director.instance.Paused ||
                MainControls.instance == null || MainControls.instance.CurrentAction != BuildAction ||
                GameMap.instance == null ||
                GameData.scenario?.InGameoverSituation != false)
                return false;

            isEditor = mission.IsEditor;
            if (!isEditor && readiness?.Current?.SessionId != mission.SessionId)
                return false;

            player = director.ActivePlayerID;
            if (!native.TryResolveMapper((eMappers)MainControls.instance.CurrentSubAction, out selected))
                return false;
            if (selected == eMappers.MAPPER_QUARRY &&
                (quarry?.IsIntact != true || (!isEditor && !mission.Mode.IsRealMultiplayer && !quarryTickReady) ||
                 (mission.Mode.IsRealMultiplayer && quarryNetwork?.IsReady != true)))
                return false;
            return player >= 1 && player <= 8 && native.SupportsMode(isEditor);
        }

        private static bool TryPointerTile(EditorDirector director, bool owned, out Tile tile)
        {
            tile = default;
            if ((!owned && director.overUI()) || GameMap.instance.pendingRotationtriggered)
                return false;
            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0 || mouse.x >= Screen.width || mouse.y < 0 || mouse.y >= Screen.height)
                return false;

            // Project the raw pointer so a Noesis visual cannot freeze the map hit.
            var map = GameMap.instance;
            GameMapTile mapped;
            if (owned && director.overUI())
            {
                Vector3 world = default;
                Vector3Int cell = default;
                int depth = 0;
                map.CalcMapTileFromMousePos(mouse, ref world, ref cell, ref depth, true, true);
                mapped = map.getMapTile(cell.x, cell.y);
            }
            else
            {
                float x = 0, y = 0;
                MainControls.instance.getMouseMapTilePosition(ref x, ref y);
                mapped = map.getMapTile((int)x, (int)y);
            }
            if (mapped == null || (uint)mapped.gameMapX >= WorldAxisSize || (uint)mapped.gameMapY >= WorldAxisSize)
                return false;
            tile = new Tile(mapped.gameMapX, mapped.gameMapY);
            return true;
        }

        private static bool CancelPressed() =>
            InputState.IsMousePressed(1) || InputState.IsPressed(KeyCode.Escape);

        private void Fail(Exception error)
        {
            lock (engine)
            {
                faulted = true;
                ClearLocalWork();
            }
            log.LogError(Stamp(error.ToString()));
        }

        private static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        private sealed class StorageBuildOrder
        {
            internal StorageBuildOrder(long sessionId, GameMap map, int actor, bool editor, eMappers tool, int scale, Tile[] tiles,
                PlacementFeedback feedback)
            {
                SessionId = sessionId;
                Map = map;
                Actor = actor;
                Editor = editor;
                Tool = tool;
                Scale = scale;
                Tiles = tiles;
                Feedback = feedback;
            }

            internal long SessionId { get; }
            internal GameMap Map { get; }
            internal int Actor { get; }
            internal bool Editor { get; }
            internal eMappers Tool { get; }
            internal int Scale { get; }
            internal Tile[] Tiles { get; }
            internal PlacementFeedback Feedback { get; }
            internal int Next { get; set; }
            internal Tile? Awaiting { get; set; }
            internal StorageBuildCadence Cadence;
        }
    }

    internal sealed class QuarryOrder
    {
        internal QuarryOrder(long sessionId, int actor, Tile[] block, Tile[] buildSites)
        { SessionId = sessionId; Actor = actor; Block = block; BuildSites = buildSites; }

        internal long SessionId { get; }
        internal int Actor { get; }
        internal Tile[] Block { get; }
        internal Tile[] BuildSites { get; }
    }

    internal readonly struct Site
    {
        internal Site(Tile cursor, bool buildable, int frame, int[] previewTiles = null, Tile? pile = null, eMappers? mapper = null)
        { Cursor = cursor; Buildable = buildable; Frame = frame; PreviewTiles = previewTiles; Pile = pile; Mapper = mapper; }

        internal Tile Cursor { get; }
        internal bool Buildable { get; }
        internal int Frame { get; }
        internal int[] PreviewTiles { get; }
        internal Tile? Pile { get; }
        internal eMappers? Mapper { get; }
    }
}
