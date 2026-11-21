using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Input;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using UnityEngine;

namespace AreaConstructionTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.60.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "AreaConstructionTest_Serp", Name = "AreaConstructionTest", Version = "0.3.0";

        private void Awake() => PlacementInput.Install(Logger);
    }

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
        private readonly Queue<Tile> toBuild = new Queue<Tile>();
        private readonly List<Tile> storageOrder = new List<Tile>();
        private readonly HashSet<Tile> storageUnvisited = new HashSet<Tile>();
        private readonly Dictionary<Tile, int> storageFrames = new Dictionary<Tile, int>();
        private readonly List<Tile> quarryPiles = new List<Tile>(4);
        private Tile? storageAwaiting;
        private readonly GhostView view = new GhostView();
        private IMissionPlayStateReadinessCapability readiness;
        private MissionContext mission;
        private NativePlacement native;
        private NativeQuarryPlacement quarry;
        private QuarryNetwork quarryNetwork;
        private QuarryOrder quarryOrder;
        private bool quarryTickReady;
        private GameMap map;
        private Tile start, end;
        private eMappers tool;
        private int actor, scale;
        private bool editor, captured, dragging, faulted, quarryBlock;
        private long nextQuote;

        private PlacementInput(object engine, ManualLogSource log)
        {
            this.engine = engine;
            this.log = log;
        }

        internal static void Install(ManualLogSource log)
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                FieldInfo monitor = typeof(EngineInterface).GetField("threadLock", flags);
                var gate = monitor?.FieldType == typeof(object) ? monitor.GetValue(null) : null;
                MethodInfo update = typeof(EditorDirector).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo button = typeof(EditorDirector).GetMethod(nameof(EditorDirector.updateLeftMouseStateForEngine), new[] { typeof(int) });
                MethodInfo place = typeof(EngineInterface).GetMethod(nameof(EngineInterface.PlaceMapperItem),
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int) });
                if (gate == null || update == null || button == null || place == null)
                    throw new MissingMethodException("Vanilla construction/input boundary");

                var runtime = new PlacementInput(gate, log);
                var harmony = new Harmony(Plugin.Guid);
                harmony.Patch(button, prefix: new HarmonyMethod(typeof(PlacementInput), nameof(Primary)));
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(PlacementInput), nameof(AfterInput)));
                harmony.Patch(place, prefix: new HarmonyMethod(typeof(PlacementInput), nameof(AllowCursor)));
                CrusaderLibrary.Instance.LibraryLoaded += runtime.LibraryLoaded;
                var api = ApiShared.ForMod(Plugin.Guid);
                api.WhenReady(runtime.RegisterLifecycle);
                Application.focusChanged += runtime.FocusChanged;
                Volatile.Write(ref instance, runtime);
            }
            catch (Exception error) { log.LogError(Stamp(error.Message)); }
        }

        private void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                Volatile.Write(ref native, new NativePlacement(context, log));
                Volatile.Write(ref quarry, NativeQuarryPlacement.TryInstall(context, log));
                log.LogInfo(Stamp("Native placement bindings ready."));
            }
            catch (Exception error) { Fail(error); }
        }

        private void RegisterLifecycle(ModApiClient api)
        {
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic) ||
                !api.TryGetMissionLifecycle(out var lifecycle, out diagnostic) ||
                !lifecycle.TryRegisterObserver("area-construction", OnStarted, OnEnded, null, out diagnostic))
            {
                Fail(new InvalidOperationException(diagnostic.Reason));
                return;
            }
            quarryTickReady = SimulationTickEvents.TryRegister(Plugin.Guid, "area-construction.quarry", QuarryTick, out string reason);
            if (!quarryTickReady)
                log.LogError(Stamp("Offline quarry coordination unavailable: " + reason));
            try
            {
                quarryNetwork = new QuarryNetwork(api, log, () => Volatile.Read(ref quarry)?.IsIntact == true, ReceiveQuarries);
            }
            catch (Exception error) { log.LogError(Stamp("Quarry coordination unavailable: " + error.Message)); }
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            lock (engine)
            {
                dragging = false;
                toBuild.Clear();
                storageAwaiting = null;
                quarryOrder = null;
                native?.ValidateStorage();
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
                dragging = false;
                toBuild.Clear();
                storageAwaiting = null;
                quarryOrder = null;
            }
        }

        private void FocusChanged(bool focused)
        {
            if (focused) return;
            lock (engine) dragging = false;
            view.Hide();
        }

        private static bool Primary(EditorDirector __instance, int __0)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null)
                return true;
            lock (runtime.engine)
            {
                try { return !runtime.HandleButton(__instance, __0); }
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
                if (toBuild.Count != 0 || quarryOrder != null || !TryTarget(director, out var selected, out int player, out bool isEditor) ||
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
                storageAwaiting = null;
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
            if (runtime == null || !__6)
                return true;
            lock (runtime.engine)
                return !runtime.dragging || (int)runtime.tool != __0 || runtime.actor != __4;
        }

        private static void AfterInput(EditorDirector __instance)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null)
                return;

            try
            {
                runtime.quarryNetwork?.Pump();
                bool draw;
                QuarryOrder outbound = null;
                lock (runtime.engine)
                {
                    draw = runtime.OnDirectorFrame(__instance);
                    if (runtime.quarryOrder != null && runtime.mission?.Mode.IsRealMultiplayer == true)
                    {
                        outbound = runtime.quarryOrder;
                        runtime.quarryOrder = null;
                    }
                }
                if (outbound != null &&
                    !runtime.quarryNetwork.TrySubmit(outbound.SessionId, outbound.Actor, outbound.Block, outbound.BuildSites, out string reason))
                    runtime.log.LogWarning(Stamp("Quarry block was not submitted: " + reason));

                // Rendering and visibility tests are Unity work. Do not hold
                // the simulation monitor while walking visible scene objects.
                if (draw)
                {
                    if (!runtime.view.Show(__instance, runtime.map, runtime.tool, runtime.scale, runtime.shown))
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
            if (captured)
            {
                bool released = InputState.IsMouseReleased(0);
                if (released || !InputState.IsMouseHeld(0))
                {
                    if (released && dragging && MatchesTarget(director) && !CancelPressed())
                    {
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
                            toBuild.Enqueue(start);
                        else if (NativePlacement.IsStorage(tool))
                        {
                            foreach (Tile tile in storageOrder) toBuild.Enqueue(tile);
                        }
                        else
                        {
                            foreach (var site in shown)
                                if (site.Buildable) toBuild.Enqueue(site.Cursor);
                        }
                    }
                    captured = dragging = false;
                }
            }

            if (!dragging && toBuild.Count == 0)
                return false;

            if (!MatchesTarget(director) || CancelPressed())
            {
                dragging = false;
                toBuild.Clear();
                storageAwaiting = null;
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

            if (tool == eMappers.MAPPER_KILLING_PIT)
                StorageGrid.Fill(grid, start, end, scale, scale, WorldAxisSize);
            else
                AreaGrid.Fill(grid, start, end, scale, NativePlacement.FootprintHeight(tool, scale), WorldAxisSize);
            foreach (var tile in grid)
            {
                int image = frame;
                bool buildable = canProbe && native.ReserveVisual(tool, actor, editor, tile, scale, ref image);
                shown.Add(new Site(tile, buildable, image, buildable ? native.LastPreviewTiles : null));
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

        private void Commit()
        {
            if (!native.Begin(tool, actor, editor) || !native.HasQueueSpace)
                return;
            if (storageAwaiting is Tile inFlight && native.IsPending(inFlight))
                return;
            storageAwaiting = null;

            int attempted = 0, submitted = 0;
            while (toBuild.Count != 0 && attempted < CommitPerUpdate)
            {
                if (!native.HasQueueSpace)
                    break;
                Tile tile = toBuild.Dequeue();
                attempted++;
                int unused = 0;
                if (!native.Reserve(tool, actor, editor, tile, scale, ref unused))
                    continue;
                EngineInterface.PlaceMapperItem((int)tool, tile.X, tile.Y, 0, actor, !editor, false, 1);
                native.Submitted();
                submitted++;
                if (NativePlacement.IsStorage(tool))
                {
                    // A delayed Chore is not a registered neighboring building.
                    // Wait for its queue receipt to finish before testing more sites.
                    storageAwaiting = tile;
                    break;
                }
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
                if (mission == null || mission.IsEditor || mission.Mode.IsRealMultiplayer || quarryOrder == null)
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
            selected == tool && player == actor && isEditor == editor && ReferenceEquals(GameMap.instance, map);

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

            selected = (eMappers)MainControls.instance.CurrentSubAction;
            player = director.ActivePlayerID;
            if (selected == eMappers.MAPPER_QUARRY &&
                (quarry?.IsIntact != true || (!isEditor && !mission.Mode.IsRealMultiplayer && !quarryTickReady) ||
                 (mission.Mode.IsRealMultiplayer && quarryNetwork?.IsReady != true)))
                return false;
            return player >= 1 && player <= 8 && NativePlacement.Supports(selected) &&
                native.SupportsMode(isEditor) && native.IsCurrent(selected);
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
                dragging = false;
                toBuild.Clear();
                quarryOrder = null;
            }
            log.LogError(Stamp(error.ToString()));
        }

        private static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
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
        internal Site(Tile cursor, bool buildable, int frame, int[] previewTiles = null, Tile? pile = null)
        { Cursor = cursor; Buildable = buildable; Frame = frame; PreviewTiles = previewTiles; Pile = pile; }

        internal Tile Cursor { get; }
        internal bool Buildable { get; }
        internal int Frame { get; }
        internal int[] PreviewTiles { get; }
        internal Tile? Pile { get; }
    }
}
