using System;
using System.Diagnostics;
using System.Threading;
using APIShared;
using APIShared.GameModes;
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
        private bool submissionFailurePending;
        private volatile Stage stage;
        private long nextQuote;
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
            PlacementTooltip.Register(log, settings);
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
                submissionFailurePending = false;
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
                submissionFailurePending = false;
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
                !ValidModifiers())
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
                                CommitLine();
                            }
                            else
                                ClearPlacement();
                        }
                        return true;
                    }

                    if (state != 1)
                        return false;
                    bool canUseTool = TryTarget(director, out var current);
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
            runtime.ShowSubmissionFailure();
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
                }
                catch (Exception error)
                {
                    runtime.Fail(error.ToString());
                }
            }
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
            if (failed || !enabled) return;
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
            // Only an unconfirmed gesture is tied to the current tool. Confirmed
            // batches already belong to the native queue, not this input runtime.
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
            if (!hasEnd || now < nextQuote) return;
            nextQuote = now + QuoteInterval;
            var line = CurrentLine();
            bool available = TryQuote(line, out var quote);
            var previous = Volatile.Read(ref preview);
            if (previous == null || !previous.Matches(end, available, quote, tileStates))
                Volatile.Write(ref preview, MakePreview(target, end, line.Count, available, quote));
        }

        // The game's release edge is on Unity, under EngineGate. Snapshot and
        // enqueue before returning to Vanilla so the next input cannot revoke it.
        private void CommitLine()
        {
            try
            {
                var line = CurrentLine();
                if (!TryQuote(line, out var quote))
                {
                    ReportUnsubmitted("live placement check unavailable");
                    return;
                }
                if (quote.BuildableCount == 0) return;
                int count = 0;
                for (int i = 0; i < line.Count; i++)
                    if (tileStates[i] == LineTileState.Buildable)
                        tiles[count++] = tiles[i];
                var operation = target.Mapper == (int)eMappers.MAPPER_PITCH_DITCH
                    ? PitchLineOperation.Pitch : native.MoatOperation;
                var result = network.TrySubmit(operation, new ArraySegment<LineTile>(tiles, 0, count), target.Player);
                if (result != NativeSubmitResult.Submitted)
                    ReportUnsubmitted(result == NativeSubmitResult.Busy ? "native queue full" : "batch submission rejected");
            }
            finally { ClearPlacement(); }
        }

        private void ReportUnsubmitted(string reason)
        {
            submissionFailurePending = true;
            log.LogWarning(PitchMoatLinesFeature.Stamp("PITCH_MOAT_LINE_NOT_SUBMITTED: " + reason));
        }

        private void ShowSubmissionFailure()
        {
            if (!submissionFailurePending || mission == null || !MainViewModel.viewModelLoaded)
                return;
            submissionFailurePending = false;
            // AfterInput is Unity-only and outside EngineGate. An error dialog
            // must never hold the native monitor or participate in Chore receive.
            try
            {
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                    SerpLocalization.Get("BugfixesAndQoL.PitchMoatLines"), () => { },
                    SerpLocalization.Get("BugfixesAndQoL.LineSubmissionFailed"));
            }
            catch (Exception error)
            {
                log.LogWarning(PitchMoatLinesFeature.Stamp("PITCH_MOAT_LINE_NOTICE_FAILED: " + error));
            }
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
            Volatile.Write(ref hover, null);
            HideEndpoint();
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

        private enum Stage { Idle, Dragging }

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
