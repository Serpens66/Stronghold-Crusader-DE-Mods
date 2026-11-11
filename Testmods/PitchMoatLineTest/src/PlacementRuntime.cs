using System;
using System.Diagnostics;
using System.Threading;
using APIShared;
using APIShared.GameModes;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.Interop;
using Shared;
using UnityEngine;

namespace PitchMoatLineTest
{
    internal enum PlacementNotice { None, Busy, Stopped }

    internal sealed class PlacementRuntime
    {
        // Vanilla's MainControls.CurrentAction value for mapper placement.
        private const int MapperPlacement = 5;
        private static readonly long QuoteInterval = Stopwatch.Frequency / 10;
        private static readonly long SubmitInterval = (Stopwatch.Frequency * 32 + 999) / 1000;
        private static readonly long ReceiptTimeout = Stopwatch.Frequency * 3;
        private static readonly long BusyNoticeDuration = Stopwatch.Frequency;
        private static PlacementRuntime instance;

        private readonly NativePlacement native;
        private readonly ManualLogSource log;
        private readonly GameplayModActivationProfile profile;
        private readonly LineTile[] tiles = new LineTile[LinePolicy.MaxTiles];
        private readonly LineTileState[] tileStates = new LineTileState[LinePolicy.MaxTiles];
        private MissionContext mission;
        private IMissionPlayStateReadinessCapability readiness;
        private PlacementTarget target;
        private LineTile start;
        private LineTile end;
        private bool hasEnd;
        private bool ownsButton;
        private bool failed;
        private bool tickReported;
        private volatile Stage stage;
        private long nextQuote;
        private long nextSubmit;
        private long receiptDeadline;
        private int nextTile;
        private int countToSubmit;
        private int placedCount;
        private bool awaitingReceipt;
        private NoticeSnapshot notice;
        private PreviewSnapshot preview;
#if SERP_DEVDIAG
        private int gestureNumber;
        private int quotedGesture;
#endif

        private PlacementRuntime(NativePlacement native, ManualLogSource log, bool multiplayer)
        {
            this.native = native;
            this.log = log;
            profile = new GameplayModActivationProfile(Plugin.Guid, "Pitch and Moat Line Test",
                GameplayModAllowedContext.CustomGame | GameplayModAllowedContext.MapEditor |
                GameplayModAllowedContext.Campaign | GameplayModAllowedContext.StandaloneMission |
                GameplayModAllowedContext.VanillaTrail | GameplayModAllowedContext.CustomTrail |
                GameplayModAllowedContext.CoopTrail | GameplayModAllowedContext.SandsOfTime |
                GameplayModAllowedContext.CustomizedVanillaTrail | GameplayModAllowedContext.CustomizedCustomTrail |
                GameplayModAllowedContext.CustomizedCoopTrail | GameplayModAllowedContext.CustomizedSandsOfTime,
                allowRealMultiplayer: multiplayer);
        }

        internal static void Initialize(ModApiClient api, NativePlacement native, ManualLogSource log, bool multiplayer)
        {
            var runtime = new PlacementRuntime(native, log, multiplayer);
            api.WhenReady(runtime.RegisterMissionObservers);
            Application.focusChanged += runtime.FocusChanged;
            Volatile.Write(ref instance, runtime);
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
        }

        private void OnStarted(MissionLifecycleNotification notification)
        {
            lock (ManagedBoundary.EngineGate)
            {
                ClearPlacement();
                native.ClearPitchMoatScope();
                tickReported = false;
                var context = notification.Context;
                bool allowed = GameplayModModePolicy.IsAllowed(profile, context.Mode, out var reason);
                Volatile.Write(ref mission, allowed ? context : null);
                if (!allowed)
                    log.LogInfo(Plugin.Stamp("PITCH_MOAT_LINE_SESSION_INACTIVE: " + reason));
            }
        }

        private void OnEnded(MissionLifecycleNotification notification)
        {
            lock (ManagedBoundary.EngineGate)
            {
                if (mission?.SessionId != notification.Context.SessionId)
                    return;
                Volatile.Write(ref mission, null);
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
            if (state != 1 || Volatile.Read(ref mission) == null || !ManagedBoundary.IsReady ||
                (!HasReleasedLine(stage) && !ValidModifiers()))
                return false;
            var controls = MainControls.instance;
            return controls != null && controls.CurrentAction == MapperPlacement &&
                IsLineMapper(controls.CurrentSubAction);
        }

        private bool HandleButton(EditorDirector director, int state, out bool showPreview)
        {
            showPreview = false;
#if SERP_DEVDIAG
            long wait = 0;
            DevDiag.Start(ref wait);
#endif
            lock (ManagedBoundary.EngineGate)
            {
#if SERP_DEVDIAG
                DevDiag.StopSpike(ref wait, "pitch-moat.button.engine-wait", state == 1 ? 0 : 8000);
#endif
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
                                nextSubmit = Stopwatch.GetTimestamp() + SubmitInterval;
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
                        SetNotice(PlacementNotice.Busy, BusyNoticeDuration);
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
#if SERP_DEVDIAG
                    gestureNumber++;
#endif
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
#if SERP_DEVDIAG
            long started = 0;
            DevDiag.Start(ref started);
#endif
            try
            {
                PlacementPreview.EnsureOn(director);
            }
            catch (Exception error)
            {
                lock (ManagedBoundary.EngineGate)
                    Fail(error.ToString());
            }
#if SERP_DEVDIAG
            finally
            {
                DevDiag.StopSpike(ref started, "pitch-moat.preview.ensure", 0,
                    () => $"gesture={gestureNumber}");
            }
#endif
        }

        internal static void AfterInput(EditorDirector director)
        {
            var runtime = Volatile.Read(ref instance);
            if (runtime == null || (runtime.stage == Stage.Idle && !runtime.ownsButton))
                return;
#if SERP_DEVDIAG
            long wait = 0;
            DevDiag.Start(ref wait);
#endif
            lock (ManagedBoundary.EngineGate)
            {
#if SERP_DEVDIAG
                DevDiag.StopSpike(ref wait, "pitch-moat.after-input.engine-wait", 8000);
#endif
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

        private bool TryTarget(EditorDirector director, out PlacementTarget result)
        {
            result = default;
            var context = mission;
            if (failed || !ManagedBoundary.IsReady || context == null || (!context.IsEditor &&
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
            if (player <= 0 || !native.IsReadyForMapper(mapper, !context.IsEditor))
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

        private static bool ValidModifiers() => Application.isFocused && !ShiftHeld() &&
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

        private static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool IsCancelPressed() => Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);

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
            if (runtime != null && ManagedBoundary.IsReady && runtime.stage != Stage.Idle && constructingOnly && size == 0 &&
                Monitor.IsEntered(ManagedBoundary.EngineGate) &&
                runtime.target.Matches(new PlacementTarget(mapper, player, inGame)))
                return;
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
            if (failed)
                return;
            if (!ManagedBoundary.IsReady)
            {
                ClearPlacement();
                return;
            }
            if (!tickReported)
            {
                tickReported = true;
                log.LogInfo(Plugin.Stamp($"PITCH_MOAT_LINE_MISSION_TICK session={mission.SessionId} editor={mission.IsEditor}"));
            }
            if (stage == Stage.Idle)
                return;
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
                {
                    var displayTiles = new LineTile[line.Count];
                    var displayStates = new LineTileState[line.Count];
                    Array.Copy(tiles, displayTiles, line.Count);
                    if (available)
                        Array.Copy(tileStates, displayStates, line.Count);
                    var snapshot = new PreviewSnapshot(displayTiles, displayStates, end, target.Mapper, target.Player,
                        target.InGame, available, quote);
#if SERP_DEVDIAG
                    snapshot.GestureNumber = gestureNumber;
#endif
                    Volatile.Write(ref preview, snapshot);
                }
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
            bool allowOverlap = !mission.Mode.IsRealMultiplayer;
            if (awaitingReceipt)
            {
                var receipt = native.PollReceipt(target.Mapper, target.Player, target.InGame, allowOverlap, tiles[nextTile]);
                if (receipt == LineReceipt.Pending && now < receiptDeadline)
                    return;
                if (receipt != LineReceipt.Accepted)
                {
                    StopBatch("execution was not confirmed");
                    return;
                }
                awaitingReceipt = false;
                nextTile++;
                placedCount++;
            }
            if (nextTile < countToSubmit && (now < nextSubmit || native.IsNativeShiftHeld))
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
#if SERP_DEVDIAG
                DevDiag.Log(() => $"pitch-moat: line completed, gesture={gestureNumber}, mapper={target.Mapper}, fields={placedCount}");
#endif
                ClearPlacement();
                return;
            }
            if (!native.TrySubmit(target.Mapper, target.Player, target.InGame, allowOverlap, tiles[nextTile]))
            {
                StopBatch("Vanilla did not accept the next field");
                return;
            }
            awaitingReceipt = true;
            long submittedAt = Stopwatch.GetTimestamp();
            nextSubmit = submittedAt + SubmitInterval;
            receiptDeadline = submittedAt + ReceiptTimeout;
        }

        private bool TryQuote(ArraySegment<LineTile> line, out LineQuote quote)
        {
#if SERP_DEVDIAG
            long started = 0;
            bool firstQuote = quotedGesture != gestureNumber;
            quotedGesture = gestureNumber;
            DevDiag.Start(ref started);
            try
            {
#endif
                return native.TryQuote(target.Mapper, target.Player, target.InGame, !mission.Mode.IsRealMultiplayer,
                    line, tileStates, out quote);
#if SERP_DEVDIAG
            }
            finally
            {
                DevDiag.StopSpike(ref started, "pitch-moat.quote", firstQuote ? 0 : 8000,
                    () => $"gesture={gestureNumber} stage={stage} mapper={target.Mapper} fields={line.Count}");
            }
#endif
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
            Volatile.Write(ref notice, null);
            HideEndpoint();
        }

        private void StopBatch(string reason)
        {
            log.LogInfo(Plugin.Stamp($"PITCH_MOAT_LINE_STOP after {placedCount}/{countToSubmit} confirmed fields: {reason}"));
            ClearPlacement();
            SetNotice(PlacementNotice.Stopped, Stopwatch.Frequency * 3);
        }

        private void Fail(string reason)
        {
            if (!failed)
                log.LogError(Plugin.Stamp("PITCH_MOAT_LINE_DISABLED: " + reason));
            failed = true;
            ClearPlacement();
        }

        internal static PreviewSnapshot GetPreview()
        {
            var runtime = Volatile.Read(ref instance);
            return runtime == null ? null : Volatile.Read(ref runtime.preview);
        }

        private void SetNotice(PlacementNotice kind, long duration) =>
            Volatile.Write(ref notice, new NoticeSnapshot(target.Mapper, kind, Stopwatch.GetTimestamp() + duration));

        internal static PlacementNotice GetNotice(int mapper)
        {
            var runtime = Volatile.Read(ref instance);
            var current = runtime == null ? null : Volatile.Read(ref runtime.notice);
            return current != null && current.Mapper == mapper && Stopwatch.GetTimestamp() < current.Until
                ? current.Kind : PlacementNotice.None;
        }

        private sealed class NoticeSnapshot
        {
            internal readonly int Mapper;
            internal readonly PlacementNotice Kind;
            internal readonly long Until;

            internal NoticeSnapshot(int mapper, PlacementNotice kind, long until)
            {
                Mapper = mapper;
                Kind = kind;
                Until = until;
            }
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
#if SERP_DEVDIAG
        internal int GestureNumber;
#endif

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
