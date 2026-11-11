using System;
using System.Diagnostics;
using System.Threading;
using APIShared;
using APIShared.GameModes;
using APIShared.Simulation;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.Interop;
using UnityEngine;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal sealed class PlacementRuntime
    {
        // Vanilla's MainControls.CurrentAction value for mapper placement.
        private const int MapperPlacement = 5;
        private static readonly long QuoteInterval = Stopwatch.Frequency / 10;
        private static PlacementRuntime instance;

        private readonly NativePlacement native;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly PitchLineNetwork network;
        private volatile bool enabled;
        private bool lifecycleReady;
        private bool pitchSendRequested;
        private bool multiplayerReceipt;
        private readonly LineTile[] tiles = new LineTile[LinePolicy.MaxTiles];
        private readonly LineTileState[] tileStates = new LineTileState[LinePolicy.MaxTiles];
        private MissionContext mission;
        private IMissionPlayStateReadinessCapability readiness;
        private PlacementTarget target;
        private LineTile start;
        private LineTile end;
        private bool hasEnd;
        private bool ownsButton;
        private volatile bool failed;
        private bool tickReported;
        private volatile Stage stage;
        private long nextQuote;
        private int? lastSubmitTick;
        private int nextTile;
        private int countToSubmit;
        private int placedCount;
        private bool awaitingReceipt;
        private PreviewSnapshot preview;
        private HoverRequest hover;

        private PlacementRuntime(NativePlacement native, ManualLogSource log, BugfixesAndQoLViewModel settings, PitchLineNetwork network)
        {
            this.native = native;
            this.log = log;
            this.settings = settings;
            this.network = network;
            enabled = settings.EnableClientFeatures && settings.EnablePitchMoatLines;
            settings.SettingChanged += OnSettingChanged;
        }

        internal static bool IsReady => Volatile.Read(ref instance) is PlacementRuntime runtime && runtime.lifecycleReady && !runtime.failed;

        private bool AllowOverlap => mission != null && (!mission.Mode.IsRealMultiplayer || PitchMoatLinesFeature.MultiplayerCompatible);

        internal static void Initialize(ModApiClient api, NativePlacement native, ManualLogSource log, BugfixesAndQoLViewModel settings, PitchLineNetwork network)
        {
            var runtime = new PlacementRuntime(native, log, settings, network);
            api.WhenReady(runtime.RegisterMissionObservers);
            Application.focusChanged += runtime.FocusChanged;
            Volatile.Write(ref instance, runtime);
        }

        private void OnSettingChanged(string property)
        {
            if (property != nameof(settings.EnablePitchMoatLines) && property != nameof(settings.EnableClientFeatures))
                return;
            enabled = settings.EnableClientFeatures && settings.EnablePitchMoatLines;
            if (!enabled)
                lock (ManagedBoundary.EngineGate)
                    ClearPlacement();
        }

        private void RegisterMissionObservers(ModApiClient api)
        {
            PlacementTooltip.Register(log);
            if (!api.TryGetMissionPlayStateReadiness(out readiness, out var diagnostic) ||
                !api.TryGetMissionLifecycle(out var lifecycle, out diagnostic) ||
                !lifecycle.TryRegisterObserver("placement-session", OnStarted, OnEnded, null, out diagnostic))
            {
                lock (ManagedBoundary.EngineGate)
                    Fail("Mission lifecycle unavailable: " + diagnostic.Reason);
            }
            else
                lifecycleReady = true;
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            lock (ManagedBoundary.EngineGate)
            {
                ClearPlacement();
                native.ClearPitchMoatScope();
                tickReported = false;
                var context = notification.Context;
                PitchMoatLinesFeature.CaptureMultiplayerCompatibility(context);
                network.SetMission(context);
                Volatile.Write(ref mission, context);
            }
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            lock (ManagedBoundary.EngineGate)
            {
                if (mission?.SessionId != notification.Context.SessionId)
                    return;
                Volatile.Write(ref mission, null);
                network.SetMission(null);
                PitchMoatLinesFeature.CaptureMultiplayerCompatibility(null);
                ClearPlacement();
                native.ClearPitchMoatScope();
            }
        }

        private void FocusChanged(bool focused)
        {
            if (focused)
                return;
            lock (ManagedBoundary.EngineGate)
                ClearPlacement();
        }

        // This is the game's own main-thread mouse edge, after its UI hit test.
        // SE key events have separate UI filtering and cannot consume this engine gesture.
        internal static void ForwardButton(EditorDirector director, int state)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime != null && runtime.ShouldHandleButton(state) &&
                runtime.HandleButton(director, state, out bool showPreview))
            {
                if (showPreview)
                    runtime.EnsurePreview(director);
                return;
            }
            director.updateLeftMouseStateForEngine(state);
        }

        private bool ShouldHandleButton(int state)
        {
            if (ownsButton)
                return true;
            if (!enabled || state != 1 || Volatile.Read(ref mission) == null || !ManagedBoundary.IsReady ||
                (!HasReleasedLine(stage) && !ValidModifiers()))
                return false;
            var controls = MainControls.instance;
            return controls != null && controls.CurrentAction == MapperPlacement &&
                IsLineMapper(controls.CurrentSubAction);
        }

        private bool HandleButton(EditorDirector director, int state, out bool showPreview)
        {
            showPreview = false;
            lock (ManagedBoundary.EngineGate)
            {
                try
                {
                    // A fresh down also ends a stale capture if focus or the
                    // scene changed before the previous release reached Update.
                    if (state == 1)
                        ownsButton = false;
                    if (ownsButton)
                    {
                        if (state == 2 && stage == Stage.Dragging)
                            UpdateEndpoint(director);
                        if (state == 0 || state == 3)
                        {
                            ownsButton = false;
                            if (stage == Stage.Dragging && state == 3 && UpdateEndpoint(director))
                            {
                                stage = Stage.Released;
                                Volatile.Write(ref preview, null);
                            }
                            else if (!HasReleasedLine(stage))
                                ClearPlacement();
                        }
                        return true;
                    }

                    if (state != 1)
                        return false;
                    bool canUseTool = TryTarget(director, out var current);
                    if (HasReleasedLine(stage) && canUseTool && current.Matches(target) && !IsCancelPressed())
                    {
                        ownsButton = true;
                        director.clearMouseStateForEngine();
                        return true;
                    }
                    ClearPlacement();
                    if (!canUseTool || !ValidModifiers() || IsCancelPressed() ||
                        !TryPointerTile(director, out start))
                        return false;

                    target = current;
                    // Consume the entire button sequence. No held/release event may
                    // escape to the Vanilla brush after the gesture is cancelled.
                    ownsButton = true;
                    director.clearMouseStateForEngine();
                    end = start;
                    hasEnd = true;
                    stage = Stage.Dragging;
                    nextQuote = 0;
                    showPreview = true;
                    return true;
                }
                catch (Exception error)
                {
                    Fail(error.ToString());
                    return ownsButton;
                }
            }
        }

        private void EnsurePreview(EditorDirector director)
        {
            try
            {
                PlacementPreview.EnsureOn(director);
            }
            catch (Exception error)
            {
                lock (ManagedBoundary.EngineGate)
                    Fail(error.ToString());
            }
        }

        internal static void AfterInput(EditorDirector director)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null)
                return;
            if (runtime.stage == Stage.Idle && !runtime.ownsButton)
            {
                try
                {
                    runtime.UpdateHover(director);
                }
                catch (Exception error)
                {
                    lock (ManagedBoundary.EngineGate)
                        runtime.Fail(error.ToString());
                }
                return;
            }
            lock (ManagedBoundary.EngineGate)
            {
                try
                {
                    if (!Application.isFocused || IsCancelPressed() ||
                        (runtime.stage == Stage.Dragging && !ValidModifiers()) ||
                        (runtime.stage != Stage.Idle && (!runtime.TryTarget(director, out var current) ||
                            !current.Matches(runtime.target))))
                        runtime.ClearPlacement();
                    if (runtime.stage == Stage.Dragging && director.overUI())
                        runtime.HideEndpoint();
                    if (runtime.ownsButton && !Input.GetMouseButton(0))
                    {
                        // A release that never reached the game edge (for example
                        // after focus loss) cannot confirm any placement.
                        runtime.ownsButton = false;
                        if (runtime.stage == Stage.Dragging)
                            runtime.ClearPlacement();
                    }
                    runtime.SubmitPreparedPitch(director);
                }
                catch (Exception error)
                {
                    runtime.Fail(error.ToString());
                }
            }
        }

        private void SubmitPreparedPitch(EditorDirector director)
        {
            if (!pitchSendRequested || stage != Stage.Submitting || awaitingReceipt)
                return;
            if (!TryTarget(director, out var current) || !current.Matches(target) ||
                mission?.Mode.IsRealMultiplayer != true || !PitchMoatLinesFeature.MultiplayerCompatible)
            {
                ClearPlacement();
                return;
            }
            if (!SimulationClock.TryGetCurrentTick(mission, out int mapTick))
            {
                StopBatch("simulation clock was unavailable before submission");
                return;
            }
            if (lastSubmitTick.HasValue &&
                (!SimulationClock.TryGetElapsedTicks(lastSubmitTick.Value, mapTick, out int elapsed) || elapsed == 0))
                return;
            if (!native.TryQuote(target.Mapper, target.Player, target.InGame, true,
                new ArraySegment<LineTile>(tiles, nextTile, 1), tileStates, out _))
            {
                StopBatch("live pitch check was unavailable");
                return;
            }
            if (tileStates[0] != LineTileState.Buildable)
            {
                pitchSendRequested = false;
                nextTile++;
                return;
            }
            var submitted = network.TrySubmit(tiles[nextTile], target.Player);
            if (submitted == NativeSubmitResult.Busy)
                return;
            pitchSendRequested = false;
            if (submitted != NativeSubmitResult.Submitted)
            {
                StopBatch("pitch submission was not accepted");
                return;
            }
            awaitingReceipt = true;
            multiplayerReceipt = true;
            lastSubmitTick = mapTick;
        }

        private void UpdateHover(EditorDirector director)
        {
            var context = Volatile.Read(ref mission);
            var previous = Volatile.Read(ref hover);
            if (!enabled || context == null || !director.ctrlPressed || ShiftHeld() || Input.GetMouseButton(0) || IsCancelPressed() ||
                !TryManagedTarget(director, context, out var current) || !TryPointerTile(director, out var tile))
            {
                if (previous != null) Volatile.Write(ref hover, null);
                return;
            }
            if (previous != null && previous.Matches(context, current, tile))
                return;
            if (previous == null)
                EnsurePreview(director);
            if (!failed)
                Volatile.Write(ref hover, new HoverRequest(context, current, tile));
        }

        private bool UpdateEndpoint(EditorDirector director)
        {
            if (!ValidModifiers() || IsCancelPressed() || !TryTarget(director, out var current) ||
                !current.Matches(target))
            {
                ClearPlacement();
                return false;
            }
            if (!TryPointerTile(director, out var tile))
            {
                HideEndpoint();
                return false;
            }
            if (!hasEnd || end.X != tile.X || end.Y != tile.Y)
            {
                end = tile;
                hasEnd = true;
            }
            return true;
        }

        private bool TryTarget(EditorDirector director, out PlacementTarget result) =>
            TryManagedTarget(director, mission, out result) && native.IsReadyForMapper(result.Mapper, result.InGame);

        private bool TryManagedTarget(EditorDirector director, MissionContext context, out PlacementTarget result)
        {
            result = default;
            if (!enabled || failed || !ManagedBoundary.IsReady || context == null ||
                (context.Mode.IsRealMultiplayer && !PitchMoatLinesFeature.MultiplayerCompatible) || (!context.IsEditor &&
                    readiness?.Current?.SessionId != context.SessionId) ||
                !Application.isFocused || director == null || !director.IsActive ||
                !MainViewModel.viewModelLoaded || MainControls.instance == null ||
                MainControls.instance.CurrentAction != MapperPlacement || Director.instance == null ||
                Director.instance.Paused || GameData.scenario?.InGameoverSituation != false ||
                GameMap.instance == null || GameMap.instance.pendingRotationtriggered)
                return false;
            int mapper = MainControls.instance.CurrentSubAction;
            if (!IsLineMapper(mapper))
                return false;
            int player = director.ActivePlayerID;
            if (player <= 0)
                return false;
            result = new PlacementTarget(mapper, player, !context.IsEditor);
            return true;
        }

        private static bool TryPointerTile(EditorDirector director, out LineTile tile)
        {
            tile = default;
            if (director.overUI() || GameMap.instance == null)
                return false;
            var mouse = Input.mousePosition;
            if (mouse.x < 0 || mouse.y < 0 || mouse.x >= Screen.width || mouse.y >= Screen.height)
                return false;
            float x = 0, y = 0;
            MainControls.instance.getMouseMapTilePosition(ref x, ref y);
            var mapTile = GameMap.instance.getMapTile((int)x, (int)y);
            if (mapTile == null)
                return false;
            tile = new LineTile(mapTile.gameMapX, mapTile.gameMapY);
            return true;
        }

        private static bool ValidModifiers()
        {
            return Application.isFocused && !ShiftHeld() &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        }

        private static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsCancelPressed()
        {
            return Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
        }

        private static bool IsLineMapper(int mapper) =>
            mapper == (int)eMappers.MAPPER_PITCH_DITCH || mapper == (int)eMappers.MAPPER_MOAT ||
            mapper == (int)eMappers.MAPPER_DUGMOAT;

        private static bool HasReleasedLine(Stage current) => current == Stage.Released || current == Stage.Submitting;

        internal static void ForwardMapper(int mapper, int x, int y, int size, int player,
            bool inGame, bool constructingOnly, int state)
        {
            var runtime = Volatile.Read(ref instance);
            // This is the special Vanilla cursor branch only. Keep its brush
            // ghost off the map while the scene overlay owns the preview.
            if (runtime != null && ManagedBoundary.IsReady && constructingOnly && size == 0 &&
                Monitor.IsEntered(ManagedBoundary.EngineGate))
            {
                var current = new PlacementTarget(mapper, player, inGame);
                if (runtime.stage != Stage.Idle && runtime.target.Matches(current))
                    return;
                var request = Volatile.Read(ref runtime.hover);
                if (state == 0 && request != null && request.Matches(runtime.mission, current, new LineTile(x, y)) &&
                    runtime.native.IsReadyForMapper(mapper, inGame))
                    return;
            }
            EngineInterface.PlaceMapperItem(mapper, x, y, size, player, inGame, constructingOnly, state);
        }

        internal static void BeforeNativeTick()
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null || Volatile.Read(ref runtime.mission) == null ||
                !Monitor.IsEntered(ManagedBoundary.EngineGate))
                return;
            try
            {
                runtime.Tick();
            }
            catch (Exception error)
            {
                runtime.Fail(error.ToString());
            }
        }

        private void Tick()
        {
            if (failed || !enabled)
                return;
            if (!ManagedBoundary.IsReady)
            {
                ClearPlacement();
                return;
            }
            if (!tickReported)
            {
                tickReported = true;
                log.LogInfo(PitchMoatLinesFeature.Stamp($"PITCH_MOAT_LINE_MISSION_TICK session={mission.SessionId} editor={mission.IsEditor}"));
            }
            if (stage == Stage.Idle)
            {
                TickHover();
                return;
            }
            // These getters only read managed fields. Unity input and scene objects
            // are sampled separately on the input thread.
            var controls = MainControls.instance;
            var director = Director.instance;
            var map = GameMap.instance;
            if (ReferenceEquals(controls, null) || ReferenceEquals(director, null) || director.Paused ||
                ReferenceEquals(map, null) || map.pendingRotationtriggered ||
                controls.CurrentAction != MapperPlacement || controls.CurrentSubAction != target.Mapper)
            {
                ClearPlacement();
                return;
            }

            long now = Stopwatch.GetTimestamp();
            if (stage == Stage.Dragging)
            {
                if (!hasEnd || now < nextQuote)
                    return;
                nextQuote = now + QuoteInterval;
                var line = CurrentLine();
                bool available = TryQuote(line, out var quote);
                var previous = Volatile.Read(ref preview);
                if (previous == null || !previous.Matches(end, available, quote, tileStates))
                    Volatile.Write(ref preview, MakePreview(target, end, line.Count, available, quote));
                return;
            }
            if (stage == Stage.Released)
            {
                var line = CurrentLine();
                if (!TryQuote(line, out var quote) || quote.BuildableCount == 0)
                {
                    ClearPlacement();
                    return;
                }
                countToSubmit = 0;
                for (int i = 0; i < line.Count; i++)
                    if (tileStates[i] == LineTileState.Buildable)
                        tiles[countToSubmit++] = tiles[i];
                nextTile = 0;
                placedCount = 0;
                stage = Stage.Submitting;
            }
            bool allowOverlap = !mission.Mode.IsRealMultiplayer || PitchMoatLinesFeature.MultiplayerCompatible;
            if (awaitingReceipt)
            {
                var receipt = multiplayerReceipt ? network.PollReceipt(tiles[nextTile], target.Player) :
                    native.PollReceipt(target.Mapper, target.Player, target.InGame, allowOverlap, tiles[nextTile]);
                if (receipt == LineReceipt.Pending)
                    return;
                if (receipt != LineReceipt.Accepted && receipt != LineReceipt.Skipped)
                {
                    StopBatch("execution was not confirmed");
                    return;
                }
                awaitingReceipt = false;
                nextTile++;
                if (receipt == LineReceipt.Accepted)
                    placedCount++;
            }
            if (nextTile < countToSubmit && native.IsNativeShiftHeld)
                return;
            if (!SimulationClock.TryGetCurrentTick(mission, out int mapTick))
            {
                StopBatch("simulation clock was unavailable");
                return;
            }
            // Engine runs can repeat without advancing map time. A confirmed
            // predecessor allows the next field only on a new simulation tick.
            if (lastSubmitTick.HasValue &&
                (!SimulationClock.TryGetElapsedTicks(lastSubmitTick.Value, mapTick, out int elapsed) || elapsed == 0))
                return;
            // Recheck the confirmed subset in order. A newly blocked tile leaves
            // the payment phase unchanged, so the next candidate can still fit.
            while (nextTile < countToSubmit)
            {
                if (!native.TryQuote(target.Mapper, target.Player, target.InGame, allowOverlap,
                    new ArraySegment<LineTile>(tiles, nextTile, 1), tileStates, out _))
                {
                    StopBatch("live placement check was unavailable");
                    return;
                }
                if (tileStates[0] == LineTileState.Buildable)
                    break;
                nextTile++;
            }
            if (nextTile == countToSubmit)
            {
                ClearPlacement();
                return;
            }
            if (mission.Mode.IsRealMultiplayer && target.Mapper == (int)eMappers.MAPPER_PITCH_DITCH)
            {
                // Authenticated transport is Unity-only. The next local input update
                // revalidates this candidate under the same monitor before submission.
                pitchSendRequested = true;
                return;
            }
            var submitted = native.TrySubmit(target.Mapper, target.Player, target.InGame, allowOverlap, tiles[nextTile]);
            if (submitted == NativeSubmitResult.Busy)
                return;
            if (submitted != NativeSubmitResult.Submitted)
            {
                StopBatch("Vanilla did not accept the next field");
                return;
            }
            awaitingReceipt = true;
            multiplayerReceipt = false;
            lastSubmitTick = mapTick;
        }

        private void TickHover()
        {
            var request = Volatile.Read(ref hover);
            if (request == null || !ReferenceEquals(request.Context, mission))
                return;
            long now = Stopwatch.GetTimestamp();
            if (now < request.NextQuote)
                return;
            request.NextQuote = now + QuoteInterval;

            var current = request.Target;
            tiles[0] = request.Tile;
            bool available = native.TryQuote(current.Mapper, current.Player, current.InGame, AllowOverlap,
                new ArraySegment<LineTile>(tiles, 0, 1), tileStates, out var quote);
            var previous = Volatile.Read(ref request.Preview);
            if (previous == null || !previous.Matches(request.Tile, available, quote, tileStates))
                Volatile.Write(ref request.Preview, MakePreview(current, request.Tile, 1, available, quote));
        }

        private PreviewSnapshot MakePreview(PlacementTarget current, LineTile endpoint, int count, bool available, LineQuote quote)
        {
            var displayTiles = new LineTile[count];
            var displayStates = new LineTileState[count];
            Array.Copy(tiles, displayTiles, count);
            if (available)
                Array.Copy(tileStates, displayStates, count);
            var snapshot = new PreviewSnapshot(displayTiles, displayStates, endpoint, current.Mapper, current.Player,
                current.InGame, available, quote);
            return snapshot;
        }

        private bool TryQuote(ArraySegment<LineTile> line, out LineQuote quote)
        {
                return native.TryQuote(target.Mapper, target.Player, target.InGame, AllowOverlap,
                    line, tileStates, out quote);
        }

        private ArraySegment<LineTile> CurrentLine() =>
            new ArraySegment<LineTile>(tiles, 0, LinePolicy.GetTiles(start, end, tiles));

        private void HideEndpoint()
        {
            hasEnd = false;
            Volatile.Write(ref preview, null);
        }

        private void ClearPlacement()
        {
            stage = Stage.Idle;
            awaitingReceipt = false;
            pitchSendRequested = false;
            multiplayerReceipt = false;
            lastSubmitTick = null;
            Volatile.Write(ref hover, null);
            HideEndpoint();
        }

        private void StopBatch(string reason)
        {
            log.LogInfo(PitchMoatLinesFeature.Stamp($"PITCH_MOAT_LINE_STOP after {placedCount}/{countToSubmit} confirmed fields: {reason}"));
            ClearPlacement();
        }

        private void Fail(string reason)
        {
            if (!failed)
                log.LogError(PitchMoatLinesFeature.Stamp("PITCH_MOAT_LINE_DISABLED: " + reason));
            failed = true;
            ClearPlacement();
        }

        internal static PreviewSnapshot GetPreview()
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null) return null;
            if (runtime.stage != Stage.Idle) return Volatile.Read(ref runtime.preview);
            var request = Volatile.Read(ref runtime.hover);
            return request != null && ReferenceEquals(request.Context, Volatile.Read(ref runtime.mission))
                ? Volatile.Read(ref request.Preview) : null;
        }

        private sealed class HoverRequest
        {
            internal readonly MissionContext Context;
            internal readonly PlacementTarget Target;
            internal readonly LineTile Tile;
            // Only the engine tick writes the quote. The input thread retires
            // the request itself, so an in-flight quote cannot restore it.
            internal long NextQuote;
            internal PreviewSnapshot Preview;

            internal HoverRequest(MissionContext context, PlacementTarget target, LineTile tile)
            {
                Context = context;
                Target = target;
                Tile = tile;
            }

            internal bool Matches(MissionContext context, PlacementTarget target, LineTile tile) =>
                ReferenceEquals(Context, context) && Target.Matches(target) && Tile.X == tile.X && Tile.Y == tile.Y;
        }

        private enum Stage { Idle, Dragging, Released, Submitting }

        private readonly struct PlacementTarget
        {
            internal readonly int Mapper;
            internal readonly int Player;
            internal readonly bool InGame;

            internal PlacementTarget(int mapper, int player, bool inGame)
            {
                Mapper = mapper;
                Player = player;
                InGame = inGame;
            }

            internal bool Matches(PlacementTarget other) =>
                Mapper == other.Mapper && Player == other.Player && InGame == other.InGame;
        }
    }

    internal sealed class PreviewSnapshot
    {
        internal readonly LineTile[] Tiles;
        internal readonly LineTileState[] States;
        internal readonly int Mapper;
        internal readonly int Player;
        internal readonly bool InGame;
        internal readonly bool Available;
        internal readonly LineQuote Quote;
        private readonly LineTile end;

        internal PreviewSnapshot(LineTile[] tiles, LineTileState[] states, LineTile end, int mapper, int player,
            bool inGame, bool available, LineQuote quote)
        {
            Tiles = tiles;
            States = states;
            this.end = end;
            Mapper = mapper;
            Player = player;
            InGame = inGame;
            Available = available;
            Quote = quote;
        }

        internal bool Matches(LineTile tile, bool available, LineQuote quote, LineTileState[] states)
        {
            if (end.X != tile.X || end.Y != tile.Y || Available != available ||
                Quote.TotalCost != quote.TotalCost || Quote.Stock != quote.Stock)
                return false;
            if (available)
                for (int i = 0; i < States.Length; i++)
                    if (States[i] != states[i]) return false;
            return true;
        }
    }
}
