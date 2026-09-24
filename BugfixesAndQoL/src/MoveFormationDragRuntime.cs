using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal static class MoveFormationCommandContext
    {
        // R3 input and the native MoveHere event can cross a managed/simulation
        // boundary. The one in-flight command must therefore be process-global,
        // synchronized, and explicitly cleared rather than ThreadStatic.
        private static readonly object syncRoot = new object();
        private static PendingCommand pending;
        private static ActiveCommand active;
        private static int moveChoreExecutionDepth;
        private static TribeIssueOrderMoveHereEventArgs observedPreEvent;

        internal static void Arm(int tribeId, int tileX, int tileY, int spacing)
        {
            lock (syncRoot)
            {
                pending = new PendingCommand(
                    tribeId, tileX, tileY, MoveFormationSpacingPolicy.Normalize(spacing));
                active = null;
            }
        }

        internal static bool TryMarkOutgoing(
            int tribeId, int tileX, int tileY, int moveType,
            out int markedMoveType, out bool pendingMatched)
        {
            lock (syncRoot)
            {
                PendingCommand command = pending;
                markedMoveType = moveType;
                pendingMatched = command != null && command.Matches(tribeId, tileX, tileY);
                return pendingMatched && QueueNativeContract.TryEncodeFormationSpacing(
                    moveType, command.Spacing, out markedMoveType);
            }
        }

        internal static void ObserveMoveOrder(TribeIssueOrderMoveHereEventArgs args, bool enabled)
        {
            if (args == null || args.Phase != EventHookPhase.Pre)
                return;

            lock (syncRoot)
            {
                if (ReferenceEquals(observedPreEvent, args))
                    return;
                observedPreEvent = args;
                // A missing Post event (for example when Extended Shift consumes
                // its marked Vanilla command) must not leak spacing into a later Move.
                active = null;
            }

            int encoded = (int)args.MoveType;
            bool hasPrivateBits =
                (encoded & QueueNativeContract.MoveFormationSpacingMask) != 0;
            bool executingMoveChore;
            lock (syncRoot)
                executingMoveChore = moveChoreExecutionDepth > 0;
            bool hasTransportSpacing =
                QueueNativeContract.TryResolveExecutedFormationSpacing(
                    encoded,
                    executingMoveChore,
                    out int decoded,
                    out int encodedSpacing);
            if (!hasTransportSpacing && !hasPrivateBits)
            {
                lock (syncRoot)
                {
                    if (pending == null ||
                        !pending.Matches(args.TribeId, args.TileX, args.TileY))
                        return;
                }
            }
            if (hasPrivateBits)
                args.MoveType = (TribeMoveType)decoded;

            lock (syncRoot)
            {
                PendingCommand command = pending;
                bool matchesLocalRelease = command != null &&
                    command.Matches(args.TribeId, args.TileX, args.TileY);
                if (enabled && args.IsPatrolPath == 0 &&
                    (hasTransportSpacing || matchesLocalRelease))
                {
                    active = new ActiveCommand(
                        args.TribeId,
                        args.TileX,
                        args.TileY,
                        hasTransportSpacing ? encodedSpacing : command.Spacing,
                        encoded,
                        hasTransportSpacing ? decoded : encoded,
                        executingMoveChore);
                    if (matchesLocalRelease)
                        pending = null;
                }
            }
        }

        internal static void EnterMoveChoreExecution()
        {
            lock (syncRoot)
                moveChoreExecutionDepth++;
        }

        internal static void ExitMoveChoreExecution()
        {
            lock (syncRoot)
            {
                if (moveChoreExecutionDepth > 0)
                    moveChoreExecutionDepth--;
            }
        }

        internal static bool TryGetActive(
            int tribeId, int tileX, int tileY, out int spacing)
        {
            lock (syncRoot)
            {
                ActiveCommand command = active;
                if (command != null && command.Matches(tribeId, tileX, tileY))
                {
                    spacing = command.Spacing;
                    return true;
                }
                spacing = MoveFormationSpacingPolicy.Default;
                return false;
            }
        }

        internal static bool TryGetActiveDecodeDiagnostic(
            int tribeId,
            int tileX,
            int tileY,
            out int rawMoveType,
            out int decodedMoveType,
            out int spacing,
            out bool executingMoveChore)
        {
            lock (syncRoot)
            {
                ActiveCommand command = active;
                if (command != null && command.Matches(tribeId, tileX, tileY))
                {
                    rawMoveType = command.RawMoveType;
                    decodedMoveType = command.DecodedMoveType;
                    spacing = command.Spacing;
                    executingMoveChore = command.ExecutingMoveChore;
                    return true;
                }

                rawMoveType = 0;
                decodedMoveType = 0;
                spacing = MoveFormationSpacingPolicy.Default;
                executingMoveChore = false;
                return false;
            }
        }

        internal static void CompleteMoveOrder()
        {
            lock (syncRoot)
            {
                active = null;
                observedPreEvent = null;
            }
        }

        internal static void Clear()
        {
            lock (syncRoot)
            {
                pending = null;
                active = null;
                moveChoreExecutionDepth = 0;
                observedPreEvent = null;
            }
        }

        private sealed class PendingCommand
        {
            internal PendingCommand(int tribeId, int tileX, int tileY, int spacing)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                Spacing = spacing;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int Spacing { get; }
            internal bool Matches(int tribeId, int tileX, int tileY) =>
                TribeId == tribeId && TileX == tileX && TileY == tileY;
        }

        private sealed class ActiveCommand
        {
            internal ActiveCommand(
                int tribeId,
                int tileX,
                int tileY,
                int spacing,
                int rawMoveType,
                int decodedMoveType,
                bool executingMoveChore)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                Spacing = spacing;
                RawMoveType = rawMoveType;
                DecodedMoveType = decodedMoveType;
                ExecutingMoveChore = executingMoveChore;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int Spacing { get; }
            internal int RawMoveType { get; }
            internal int DecodedMoveType { get; }
            internal bool ExecutingMoveChore { get; }
            internal bool Matches(int tribeId, int tileX, int tileY) =>
                TribeId == tribeId && TileX == tileX && TileY == tileY;
        }
    }

    internal sealed unsafe class MoveFormationDragRuntime
    {
        private delegate int EngineRunDelegate(bool mpFrameSkip);
        private delegate void StartSelectionDelegate(
            TroopSelector self, Vector2 start, Vector2 current);

        private const int NativeMapWidth = 800;

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticMethods =
            BindingFlags.Static | BindingFlags.Public;

        private readonly object dragSync = new object();
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetMarkerRuntime markers;
        private readonly Func<int, int, bool> targetAvailable;
        private readonly MoveFormationPreviewPlanner previewPlanner;
        private readonly List<int> previewTiles = new List<int>();
        private readonly List<MoveFormationDestination> previewDestinations =
            new List<MoveFormationDestination>();
        private readonly HashSet<string> loggedRejectReasons = new HashSet<string>();

        private NativeTroopCommandModeReader commandModeReader;
        private FieldInfo mouseTileXField;
        private FieldInfo mouseTileYField;
        private FieldInfo mousePosXForEngineField;
        private FieldInfo mousePosYForEngineField;
        private FieldInfo underCursorChimpListField;
        private FieldInfo leftMouseStateForEngineField;
        private FieldInfo rightUpForEngineField;
        private Hook engineRunHook;
        private Hook startSelectionHook;
        private EngineRunDelegate engineRunOriginal;
        private StartSelectionDelegate startSelectionOriginal;
        private IDisposable keyDownSubscription;
        private IDisposable keyHeldSubscription;
        private IDisposable keyUpSubscription;
        private DragState drag;
        private volatile bool installed;
        private volatile bool failed;

        internal MoveFormationDragRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            LargeMoveTargetMarkerRuntime markers,
            Func<int, int, bool> targetAvailable)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
            this.targetAvailable = targetAvailable ??
                throw new ArgumentNullException(nameof(targetAvailable));
            previewPlanner = new MoveFormationPreviewPlanner(this.targetAvailable);
        }

        private bool Enabled => installed && !failed &&
            markers.MarkerReplacementAvailable && settings.EnableMod &&
            settings.EnableMoveFormationEnhancements;

        internal void Install(CrusaderLibraryLoadContext libraryContext)
        {
            if (installed || failed)
                return;
            if (libraryContext == null)
                throw new ArgumentNullException(nameof(libraryContext));
            try
            {
                commandModeReader = new NativeTroopCommandModeReader(
                    libraryContext.ModuleHandle,
                    libraryContext.Memory);
                mouseTileXField = RequireEditorField("mouseTileX", typeof(float));
                mouseTileYField = RequireEditorField("mouseTileY", typeof(float));
                mousePosXForEngineField =
                    RequireEditorField("mousePosXForEngine", typeof(int));
                mousePosYForEngineField =
                    RequireEditorField("mousePosYForEngine", typeof(int));
                underCursorChimpListField =
                    RequireEditorField("underCursorChimpList", typeof(int[]));
                leftMouseStateForEngineField =
                    RequireEditorField("leftMouseStateForEngine", typeof(int));
                rightUpForEngineField =
                    RequireEditorField("rightUpForEngine", typeof(bool));

                MethodInfo engineRun = typeof(EngineInterface).GetMethod(
                    "run", StaticMethods, null, new[] { typeof(bool) }, null) ??
                    throw new MissingMethodException(
                        typeof(EngineInterface).FullName, "run(bool)");
                if (engineRun.ReturnType != typeof(int))
                    throw new MissingMethodException(
                        typeof(EngineInterface).FullName, "int run(bool)");
                MethodInfo startSelection = typeof(TroopSelector).GetMethod(
                    "startSelection",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(Vector2), typeof(Vector2) },
                    null) ?? throw new MissingMethodException(
                        typeof(TroopSelector).FullName,
                        "startSelection(Vector2, Vector2)");

                Hook installedEngineRun = null;
                Hook installedStartSelection = null;
                IDisposable installedKeyDown = null;
                IDisposable installedKeyHeld = null;
                IDisposable installedKeyUp = null;
                try
                {
                    installedEngineRun = new Hook(
                        engineRun, (EngineRunDelegate)EngineRunHook);
                    engineRunOriginal =
                        installedEngineRun.GenerateTrampoline<EngineRunDelegate>();
                    installedStartSelection = new Hook(
                        startSelection, (StartSelectionDelegate)StartSelectionHook);
                    startSelectionOriginal =
                        installedStartSelection.GenerateTrampoline<StartSelectionDelegate>();
                    installedKeyDown = InputR3EventHooks.OnKeyDown.Observable
                        .Subscribe(OnKeyDown);
                    installedKeyHeld = InputR3EventHooks.OnKey.Observable
                        .Subscribe(OnKeyHeld);
                    installedKeyUp = InputR3EventHooks.OnKeyUp.Observable
                        .Subscribe(OnKeyUp);

                    engineRunHook = installedEngineRun;
                    startSelectionHook = installedStartSelection;
                    keyDownSubscription = installedKeyDown;
                    keyHeldSubscription = installedKeyHeld;
                    keyUpSubscription = installedKeyUp;
                }
                catch
                {
                    installedKeyUp?.Dispose();
                    installedKeyHeld?.Dispose();
                    installedKeyDown?.Dispose();
                    installedStartSelection?.Dispose();
                    installedEngineRun?.Dispose();
                    throw;
                }

                installed = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Move formation drag R3 input, selection transition, and full Vanilla EngineInterface.run transaction installed for the process lifetime.");
            }
            catch
            {
                // This path is initialization-only. Once installed is published,
                // process-lifetime hooks are never disposed by map/plugin lifecycle.
                if (!installed)
                    RollBackUnpublishedHooks();
                throw;
            }
        }

        internal void ResetTransientState()
        {
            AbortPreview("map-reset");
            MoveFormationCommandContext.Clear();
            markers.ClearPreview();
        }

        internal void DisableForProcess(string contract, Exception exception)
        {
            if (!failed)
                FailOpen(contract, exception);
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;
            try
            {
                DragState state;
                lock (dragSync)
                    state = drag;
                if (state != null)
                {
                    if (IsShiftKey(args.Key))
                    {
                        AbortPreview("conflicting-input");
                        return;
                    }
                    if (TryGetMouseButton(args.Key, out int pressedButton))
                    {
                        if (pressedButton == state.CommandButton &&
                            state.ReleaseGate.Released)
                        {
                            AbortPreview("stale-release-before-new-down");
                        }
                        else
                        {
                            if (state.ReleaseGate.OnMouseDown(pressedButton) ==
                                MoveFormationGestureResult.Aborted)
                                AbortPreview("conflicting-input");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                int commandButton = MoveFormationSpacingPolicy.GetCommandMouseButton(
                    ConfigSettings.Settings_SH1RTSControls);
                if (args.Key == ToKeyCode(commandButton))
                    TryStartDrag(commandButton);
            }
            catch (Exception exception)
            {
                FailOpen("input-down", exception);
            }
        }

        private void OnKeyHeld(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;
            try
            {
                DragState state;
                lock (dragSync)
                    state = drag;
                if (state == null)
                    return;
                if (IsShiftKey(args.Key))
                {
                    AbortPreview("shift-held");
                    return;
                }
                if (args.Key != ToKeyCode(state.CommandButton))
                    return;
                if (!ValidateActiveDrag(state))
                {
                    AbortPreview("state-changed");
                    return;
                }
                GroundMovePreviewRejection modeRejection = EvaluateCommandMode();
                if (modeRejection != GroundMovePreviewRejection.None)
                {
                    AbortPreview("command-" + ToRejectionReason(modeRejection));
                    return;
                }
                GroundMovePreviewRejection targetRejection =
                    EvaluateFixedGroundTarget(state.Target);
                if (targetRejection != GroundMovePreviewRejection.None)
                {
                    AbortPreview("target-" + ToRejectionReason(targetRejection));
                    return;
                }

                MoveFormationGestureResult heldResult;
                lock (dragSync)
                {
                    if (!ReferenceEquals(drag, state))
                        return;
                    heldResult = state.ReleaseGate.OnHeld(
                        Input.mousePosition.x, state.ScreenWidth);
                }
                if (heldResult != MoveFormationGestureResult.SpacingChanged)
                    return;
                PublishPreview(state);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: spacing-change; spacing={state.Spacing}; preview={previewTiles.Count}.");
            }
            catch (Exception exception)
            {
                FailOpen("input-held", exception);
            }
        }

        private void OnKeyUp(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Post)
                return;
            try
            {
                DragState state;
                MoveFormationGestureResult releaseResult;
                lock (dragSync)
                {
                    state = drag;
                    if (state == null || args.Key != ToKeyCode(state.CommandButton))
                        return;
                    releaseResult = state.ReleaseGate.OnInputRelease(
                        Input.mousePosition.x, state.ScreenWidth);
                }
                if (releaseResult != MoveFormationGestureResult.Released)
                    return;
                markers.ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: release-event; spacing={state.Spacing}; awaiting Vanilla release.");
            }
            catch (Exception exception)
            {
                FailOpen("input-up", exception);
            }
        }

        private void TryStartDrag(int commandButton)
        {
            GroundMovePreviewRejection modeRejection = EvaluateCommandMode();
            if (modeRejection != GroundMovePreviewRejection.None)
            {
                LogRejectedStart(ToRejectionReason(modeRejection));
                return;
            }
            string rejection = GetStartRejection(commandButton);
            if (rejection != null)
            {
                LogRejectedStart(rejection);
                return;
            }
            if (!TryCaptureSelection(
                    out SelectionIdentity[] selection,
                    out int[] unitTypes,
                    out int tribeId,
                    out rejection))
            {
                LogRejectedStart(rejection);
                return;
            }
            if (!TryCaptureFreshTarget(
                    out GroundTarget target,
                    out rejection))
            {
                LogRejectedStart(
                    rejection,
                    rejection == "tilemap-outside-map"
                        ? (GroundTarget?)null
                        : target);
                return;
            }

            Vector3 mouse = Input.mousePosition;
            DragState state = new DragState(
                tribeId,
                target,
                mouse.x,
                (int)mouse.x,
                Screen.height - (int)mouse.y,
                selection,
                unitTypes,
                commandButton,
                Screen.width);
            lock (dragSync)
                drag = state;
            PublishPreview(state);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MOVE_FORMATION_DRAG: drag-start; tribe={tribeId}; units={selection.Length}; " +
                $"button={commandButton}; tilemap={target.TileMapX},{target.TileMapY}; " +
                $"native={target.NativeX},{target.NativeY}; clickDepth={target.ClickDepth}; " +
                $"spacing=2; preview={previewTiles.Count}.");
        }

        private void StartSelectionHook(
            TroopSelector self, Vector2 start, Vector2 current)
        {
            try
            {
                DragState state;
                lock (dragSync)
                    state = drag;
                if (!failed && state != null && state.CommandButton == 0 && Enabled)
                {
                    if (MainControls.instance != null)
                        MainControls.instance.CurrentAction = 0;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "MOVE_FORMATION_DRAG: suppressed Vanilla selection-box transition during left-command drag.");
                    return;
                }
            }
            catch (Exception exception)
            {
                FailOpen("selection-transition", exception);
            }
            startSelectionOriginal(self, start, current);
        }

        private int EngineRunHook(bool mpFrameSkip)
        {
            DragState state = null;
            try
            {
                EditorDirector director = EditorDirector.instance;
                lock (dragSync)
                {
                    if (!failed && drag != null && director != null &&
                        drag.CommandButton == MoveFormationSpacingPolicy.GetCommandMouseButton(
                            ConfigSettings.Settings_SH1RTSControls) &&
                        drag.ReleaseGate.TryClaimVanillaRelease(
                            (int)leftMouseStateForEngineField.GetValue(director),
                            (bool)rightUpForEngineField.GetValue(director),
                            drag.ScreenWidth))
                    {
                        state = drag;
                        drag = null;
                    }
                }
            }
            catch (Exception exception)
            {
                FailOpen("run-release-detection", exception);
                return engineRunOriginal(mpFrameSkip);
            }

            if (state == null)
                return engineRunOriginal(mpFrameSkip);

            try
            {
                if (!ValidateActiveDrag(state))
                {
                    ClearCompletedDrag("run-release-validation-failed");
                    return engineRunOriginal(mpFrameSkip);
                }
                if (!StoredCoordinateMappingMatches(state.Target))
                {
                    ClearCompletedDrag("coordinate-mapping-changed");
                    return engineRunOriginal(mpFrameSkip);
                }
                GroundMovePreviewRejection modeRejection = EvaluateCommandMode();
                if (modeRejection != GroundMovePreviewRejection.None)
                {
                    ClearCompletedDrag(
                        "command-" + ToRejectionReason(modeRejection));
                    return engineRunOriginal(mpFrameSkip);
                }
                GroundMovePreviewRejection targetRejection =
                    EvaluateFixedGroundTarget(state.Target);
                if (targetRejection != GroundMovePreviewRejection.None)
                {
                    ClearCompletedDrag(
                        "target-" + ToRejectionReason(targetRejection));
                    return engineRunOriginal(mpFrameSkip);
                }
            }
            catch (Exception exception)
            {
                FailOpen("run-release-validation", exception);
                return engineRunOriginal(mpFrameSkip);
            }

            return RunAnchoredVanillaTransaction(state, mpFrameSkip);
        }

        private int RunAnchoredVanillaTransaction(DragState state, bool mpFrameSkip)
        {
            GroundMovePreviewRejection modeRejection = EvaluateCommandMode();
            if (modeRejection != GroundMovePreviewRejection.None)
            {
                ClearCompletedDrag(
                    "handoff-command-" + ToRejectionReason(modeRejection));
                return engineRunOriginal(mpFrameSkip);
            }

            EditorDirector director = EditorDirector.instance;
            MainControls controls = MainControls.instance;
            object oldTileX;
            object oldTileY;
            object oldMouseX;
            object oldMouseY;
            object oldUnderCursor;
            int oldDepth;
            int oldClickDepth;
            bool oldOverTopHalf;

            try
            {
                oldTileX = mouseTileXField.GetValue(director);
                oldTileY = mouseTileYField.GetValue(director);
                oldMouseX = mousePosXForEngineField.GetValue(director);
                oldMouseY = mousePosYForEngineField.GetValue(director);
                oldUnderCursor = underCursorChimpListField.GetValue(director);
                oldDepth = director.lastTroopOverDepth;
                oldClickDepth = controls.mouseTileClickDepth;
                oldOverTopHalf = GameMap.instance.overTopHalf;
            }
            catch (Exception exception)
            {
                FailOpen("run-anchor-capture", exception);
                return engineRunOriginal(mpFrameSkip);
            }

            int previewCount = 0;
            try
            {
                PublishPreview(state);
                previewCount = previewTiles.Count;
                mouseTileXField.SetValue(director, (float)state.Target.TileMapX);
                mouseTileYField.SetValue(director, (float)state.Target.TileMapY);
                mousePosXForEngineField.SetValue(director, state.PressedEngineX);
                mousePosYForEngineField.SetValue(director, state.PressedEngineY);
                underCursorChimpListField.SetValue(
                    director, state.Target.UnderCursorUnitIds);
                director.lastTroopOverDepth = state.Target.TroopDepth;
                controls.mouseTileClickDepth = state.Target.ClickDepth;
                GameMap.instance.overTopHalf = state.Target.OverTopHalf;
                GroundMovePreviewRejection finalModeRejection =
                    EvaluateCommandMode();
                if (finalModeRejection != GroundMovePreviewRejection.None)
                {
                    Exception restoreFailure = RestoreInputState(
                        director,
                        controls,
                        oldTileX,
                        oldTileY,
                        oldMouseX,
                        oldMouseY,
                        oldUnderCursor,
                        oldDepth,
                        oldClickDepth,
                        oldOverTopHalf);
                    markers.ClearPreview();
                    if (restoreFailure != null)
                        FailOpen("run-anchor-mode-restore", restoreFailure);
                    return engineRunOriginal(mpFrameSkip);
                }
                MoveFormationCommandContext.Arm(
                    state.TribeId,
                    state.Target.NativeX,
                    state.Target.NativeY,
                    state.Spacing);
                markers.ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: run-release-handoff; tribe={state.TribeId}; " +
                    $"tilemap={state.Target.TileMapX},{state.Target.TileMapY}; " +
                    $"native={state.Target.NativeX},{state.Target.NativeY}; " +
                    $"spacing={state.Spacing}; preview={previewCount}; " +
                    $"releaseEvent={state.ReleaseGate.ReleaseEventSeen}.");
            }
            catch (Exception exception)
            {
                Exception restoreFailure = RestoreInputState(
                    director,
                    controls,
                    oldTileX,
                    oldTileY,
                    oldMouseX,
                    oldMouseY,
                    oldUnderCursor,
                    oldDepth,
                    oldClickDepth,
                    oldOverTopHalf);
                MoveFormationCommandContext.Clear();
                markers.ClearPreview();
                FailOpen(
                    "run-anchor-apply",
                    restoreFailure == null
                        ? exception
                        : new AggregateException(exception, restoreFailure));
                return engineRunOriginal(mpFrameSkip);
            }

            bool originalEntered = false;
            try
            {
                originalEntered = true;
                return engineRunOriginal(mpFrameSkip);
            }
            finally
            {
                Exception restoreFailure = RestoreInputState(
                    director,
                    controls,
                    oldTileX,
                    oldTileY,
                    oldMouseX,
                    oldMouseY,
                    oldUnderCursor,
                    oldDepth,
                    oldClickDepth,
                    oldOverTopHalf);
                MoveFormationCommandContext.Clear();
                markers.ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: run-context-cleared; originalEntered={originalEntered}.");
                if (restoreFailure != null)
                    FailOpen("run-anchor-restore", restoreFailure);
            }
        }

        private void PublishPreview(DragState state)
        {
            previewDestinations.Clear();
            previewTiles.Clear();
            try
            {
                GroundMovePreviewRejection modeRejection = EvaluateCommandMode();
                if (modeRejection != GroundMovePreviewRejection.None)
                {
                    markers.ClearPreview();
                    return;
                }
                GroundMovePreviewRejection rejection =
                    EvaluateFixedGroundTarget(state.Target);
                if (rejection != GroundMovePreviewRejection.None)
                {
                    markers.ClearPreview();
                    return;
                }
                previewPlanner.Plan(
                    state.Target.NativeX,
                    state.Target.NativeY,
                    state.Spacing,
                    state.Selection.Length,
                    state.UnitTypes,
                    previewDestinations);
                for (int index = 0; index < previewDestinations.Count; index++)
                    previewTiles.Add(previewDestinations[index].TileId);
                markers.SetPreview(previewTiles);
            }
            catch (InvalidOperationException)
            {
                // Entity and structure clicks may legitimately have no ground
                // preview anchor. Vanilla remains authoritative at release.
                markers.ClearPreview();
            }
        }

        private static bool TryCaptureSelection(
            out SelectionIdentity[] identities,
            out int[] unitTypes,
            out int tribeId,
            out string rejection)
        {
            identities = Array.Empty<SelectionIdentity>();
            unitTypes = Array.Empty<int>();
            tribeId = 0;
            rejection = "selection-count";

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int localPlayerId = playerApi.GetLocalPlayerId();
            if (localPlayerId < 1 || localPlayerId > 8)
                return false;
            int selectedCount = playerApi.GetSelectedChimpsCount(localPlayerId);
            if (!MoveFormationDragEligibility.IsUsableSelectionCount(selectedCount))
                return false;

            SelectedUnitInfo[] selected;
            try
            {
                selected = playerApi.GetSelectedChimps();
            }
            catch (ArgumentOutOfRangeException)
            {
                // The Script Extender currently constructs its result list directly
                // from a native count. A transient -1 must reject this gesture without
                // permanently disabling the otherwise fail-open client feature.
                rejection = "selection-count-transient";
                return false;
            }
            if (selected == null || selected.Length != selectedCount)
                return false;

            identities = new SelectionIdentity[selected.Length];
            tribeId = -1;
            for (int index = 0; index < selected.Length; index++)
            {
                int unitId = selected[index].UnitId;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_GlobalId == 0)
                {
                    unitTypes = Array.Empty<int>();
                    rejection = "selection-invalid-unit";
                    return false;
                }
                if (tribeId < 0)
                    tribeId = unit->r_TribeId;
                else if (tribeId != unit->r_TribeId)
                {
                    unitTypes = Array.Empty<int>();
                    rejection = "selection-mixed-tribe";
                    return false;
                }
                identities[index] = new SelectionIdentity(
                    unitId, unit->r_GlobalId, selected[index].UnitType);
            }
            Array.Sort(identities, (left, right) =>
                left.UnitId.CompareTo(right.UnitId));
            unitTypes = new int[identities.Length];
            for (int index = 0; index < identities.Length; index++)
                unitTypes[index] = identities[index].UnitType;

            bool mapEditorSelection = Shared.GameModeHelper.IsMapEditor();
            if (!MoveFormationDragEligibility.RequiresNormalTribeOwnership(
                    mapEditorSelection))
            {
                rejection = null;
                return true;
            }

            if (!GameTribeManagerAPI.Instance.IsValidId(tribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId, out GameTribe* tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner) ||
                tribe->r_PlayerIdOwner !=
                    GamePlayerManagerAPI.Instance.GetLocalPlayerId())
            {
                rejection = "selection-not-local-player";
                return false;
            }
            rejection = null;
            return true;
        }

        private static bool SelectionMatches(SelectionIdentity[] expected)
        {
            if (!TryCaptureSelection(
                    out SelectionIdentity[] current,
                    out _,
                    out _,
                    out _) || current.Length != expected.Length)
                return false;
            for (int index = 0; index < current.Length; index++)
            {
                if (!current[index].Equals(expected[index]))
                    return false;
            }
            return true;
        }

        private bool TryCaptureFreshTarget(
            out GroundTarget target,
            out string rejection)
        {
            Vector3 mouse = Input.mousePosition;
            Vector3 mouseMap = Vector3.zero;
            Vector3Int tileMap = Vector3Int.zero;
            int clickDepth = 0;
            GameMap.instance.CalcMapTileFromMousePos(
                mouse, ref mouseMap, ref tileMap, ref clickDepth);
            bool overTopHalf = GameMap.instance.overTopHalf;
            GameMapTile mapTile = GameMap.instance.getMapTile(tileMap.x, tileMap.y);
            if (mapTile == null)
            {
                target = default;
                rejection = "tilemap-outside-map";
                return false;
            }

            int[] underCursor = null;
            int troopDepth = -1;
            GameMap.instance.grabTroopsOnScreen(
                Vector2.zero,
                Vector2.zero,
                ref underCursor,
                mouse,
                ref troopDepth);
            target = new GroundTarget(
                tileMap.x,
                tileMap.y,
                mapTile.gameMapX,
                mapTile.gameMapY,
                clickDepth,
                overTopHalf,
                underCursor == null ? Array.Empty<int>() : (int[])underCursor.Clone(),
                troopDepth);
            if ((uint)target.NativeX >= NativeMapWidth ||
                (uint)target.NativeY >= NativeMapWidth)
            {
                rejection = "native-coordinate-outside-map";
                return false;
            }
            GroundMovePreviewRejection targetRejection =
                EvaluateInitialGroundTarget(target, target.UnderCursorUnitIds.Length);
            if (targetRejection != GroundMovePreviewRejection.None)
            {
                rejection = ToRejectionReason(targetRejection);
                return false;
            }
            rejection = null;
            return true;
        }

        private GroundMovePreviewRejection EvaluateInitialGroundTarget(
            GroundTarget target,
            int underCursorUnitCount)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager;
            int tileId = GameTileManagerAPI.Instance.GetTileId(
                target.NativeX, target.NativeY);
            bool insideMap = IsTargetInsideNativeMap(target, tileId, tileManager);
            int tileUnitId = insideMap
                ? tileManager.TileUnitIdGrid[tileId]
                : 0;
            int tileBuildingId = insideMap
                ? tileManager.StructureGrid[tileId]
                : 0;
            bool hasPathComponent = insideMap &&
                tileManager.PathConnectionGrid[tileId] != 0;

            GameCursorManager* cursor =
                GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
            bool cursorInGame = cursor != null && cursor->r_IsCursorInGame == 1;
            bool cursorSnapshotMatches = cursor != null &&
                cursor->r_MouseTileX == (uint)target.NativeX &&
                cursor->r_MouseTileY == (uint)target.NativeY;
            return GroundMovePreviewEligibility.EvaluateInitial(
                new GroundMovePreviewSnapshot(
                    insideMap,
                    cursorInGame,
                    cursorSnapshotMatches,
                    underCursorUnitCount,
                    cursor != null && cursor->r_HoverOverUnitId != 0 ? 1 : 0,
                    tileUnitId,
                    cursor != null && cursor->r_HoverOverBuildingId != 0 ? 1 : 0,
                    cursor != null && cursor->r_HoveringOverWall != 0,
                    tileBuildingId,
                    insideMap && targetAvailable(target.NativeX, target.NativeY),
                    hasPathComponent));
        }

        private GroundMovePreviewRejection EvaluateCommandMode()
        {
            if (commandModeReader == null)
                return GroundMovePreviewRejection.NonMoveCommandMode;
            return GroundMovePreviewEligibility.EvaluateCommandMode(
                commandModeReader.Read());
        }

        private GroundMovePreviewRejection EvaluateFixedGroundTarget(
            GroundTarget target)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager;
            int tileId = GameTileManagerAPI.Instance.GetTileId(
                target.NativeX, target.NativeY);
            bool insideMap = IsTargetInsideNativeMap(target, tileId, tileManager);
            return GroundMovePreviewEligibility.EvaluateFixedTarget(
                insideMap,
                insideMap ? tileManager.TileUnitIdGrid[tileId] : 0,
                insideMap ? tileManager.StructureGrid[tileId] : 0,
                insideMap && targetAvailable(target.NativeX, target.NativeY),
                insideMap && tileManager.PathConnectionGrid[tileId] != 0);
        }

        private static bool IsTargetInsideNativeMap(
            GroundTarget target,
            int tileId,
            GameTileManagerView tileManager)
        {
            return tileManager != null &&
                (uint)target.NativeX < NativeMapWidth &&
                (uint)target.NativeY < NativeMapWidth &&
                (uint)tileId < (uint)tileManager.TileUnitIdGrid.Length &&
                (uint)tileId < (uint)tileManager.StructureGrid.Length &&
                (uint)tileId < (uint)tileManager.PathConnectionGrid.Length;
        }

        private static string ToRejectionReason(
            GroundMovePreviewRejection rejection) =>
            rejection.ToString().ToLowerInvariant();

        private static bool StoredCoordinateMappingMatches(GroundTarget target)
        {
            GameMapTile mapTile =
                GameMap.instance.getMapTile(target.TileMapX, target.TileMapY);
            return mapTile != null && mapTile.gameMapX == target.NativeX &&
                mapTile.gameMapY == target.NativeY;
        }

        private string GetStartRejection(int commandButton)
        {
            if (!Enabled)
                return "feature-disabled";
            if (!HasValidMap())
                return "map-unavailable";
            if (commandButton != MoveFormationSpacingPolicy.GetCommandMouseButton(
                    ConfigSettings.Settings_SH1RTSControls))
                return "control-scheme-changed";
            if (FatControler.instance == null || FatControler.instance.overNoesisGUI())
                return "over-ui";
            if (IsShiftHeld())
                return "shift-held";
            return null;
        }

        private bool ValidateActiveDrag(DragState state)
        {
            if (!Enabled || !HasValidMap() ||
                state.ReleaseGate.Aborted ||
                state.CommandButton != MoveFormationSpacingPolicy.GetCommandMouseButton(
                    ConfigSettings.Settings_SH1RTSControls) ||
                FatControler.instance == null || FatControler.instance.overNoesisGUI() ||
                IsShiftHeld() || !SelectionMatches(state.Selection))
                return false;
            return true;
        }

        private static bool HasValidMap() =>
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            GameMap.instance != null && MainControls.instance != null &&
            EditorDirector.instance != null;

        private static bool IsShiftHeld()
        {
            KeyManager manager = KeyManager.instance;
            return manager != null &&
                (manager.IsKeyHeldDown(KeyCode.LeftShift, ignoreModifiers: true) ||
                 manager.IsKeyHeldDown(KeyCode.RightShift, ignoreModifiers: true));
        }

        private static bool IsShiftKey(KeyCode key) =>
            key == KeyCode.LeftShift || key == KeyCode.RightShift;

        private static KeyCode ToKeyCode(int mouseButton) =>
            mouseButton == 0 ? KeyCode.Mouse0 : KeyCode.Mouse1;

        private static bool TryGetMouseButton(KeyCode key, out int mouseButton)
        {
            if (key == KeyCode.Mouse0)
            {
                mouseButton = 0;
                return true;
            }
            if (key == KeyCode.Mouse1)
            {
                mouseButton = 1;
                return true;
            }
            mouseButton = -1;
            return false;
        }

        private void LogRejectedStart(
            string reason, GroundTarget? rejectedTarget = null)
        {
            string normalized = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            if (!loggedRejectReasons.Add(normalized))
                return;
            string coordinates = rejectedTarget.HasValue
                ? $"; tilemap={rejectedTarget.Value.TileMapX},{rejectedTarget.Value.TileMapY}" +
                  $"; native={rejectedTarget.Value.NativeX},{rejectedTarget.Value.NativeY}" +
                  $"; clickDepth={rejectedTarget.Value.ClickDepth}"
                : string.Empty;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MOVE_FORMATION_DRAG: start-rejected; reason={normalized}{coordinates}; " +
                "further occurrences of this reason are suppressed.");
        }

        private void AbortPreview(string reason)
        {
            DragState state;
            lock (dragSync)
            {
                state = drag;
                drag = null;
            }
            state?.ReleaseGate.Abort();
            if (state != null)
            {
                MoveFormationCommandContext.Clear();
                markers.ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: aborted; reason={reason ?? "unknown"}.");
            }
        }

        private void ClearCompletedDrag(string reason)
        {
            MoveFormationCommandContext.Clear();
            markers.ClearPreview();
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MOVE_FORMATION_DRAG: release-fell-back-to-Vanilla; reason={reason}.");
        }

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
            lock (dragSync)
                drag = null;
            MoveFormationCommandContext.Clear();
            Exception cleanupFailure = null;
            try
            {
                markers.ClearPreview();
            }
            catch (Exception failure)
            {
                cleanupFailure = failure;
            }
            Exception reported = cleanupFailure == null
                ? exception
                : new AggregateException(exception, cleanupFailure);
            Shared.DebugLogHelper.LogError(
                log,
                $"MOVE_FORMATION_DRAG_DISABLED: contract={contract}; Vanilla input retained; {reported}");
        }

        private void RollBackUnpublishedHooks()
        {
            keyUpSubscription?.Dispose();
            keyHeldSubscription?.Dispose();
            keyDownSubscription?.Dispose();
            startSelectionHook?.Dispose();
            engineRunHook?.Dispose();
            keyUpSubscription = null;
            keyHeldSubscription = null;
            keyDownSubscription = null;
            startSelectionHook = null;
            engineRunHook = null;
            startSelectionOriginal = null;
            engineRunOriginal = null;
        }

        private Exception RestoreInputState(
            EditorDirector director,
            MainControls controls,
            object oldTileX,
            object oldTileY,
            object oldMouseX,
            object oldMouseY,
            object oldUnderCursor,
            int oldDepth,
            int oldClickDepth,
            bool oldOverTopHalf)
        {
            Exception firstFailure = null;
            TryRestore(
                () => mouseTileXField.SetValue(director, oldTileX), ref firstFailure);
            TryRestore(
                () => mouseTileYField.SetValue(director, oldTileY), ref firstFailure);
            TryRestore(
                () => mousePosXForEngineField.SetValue(director, oldMouseX),
                ref firstFailure);
            TryRestore(
                () => mousePosYForEngineField.SetValue(director, oldMouseY),
                ref firstFailure);
            TryRestore(
                () => underCursorChimpListField.SetValue(director, oldUnderCursor),
                ref firstFailure);
            TryRestore(() => director.lastTroopOverDepth = oldDepth, ref firstFailure);
            TryRestore(() => controls.mouseTileClickDepth = oldClickDepth, ref firstFailure);
            TryRestore(() => GameMap.instance.overTopHalf = oldOverTopHalf,
                ref firstFailure);
            return firstFailure;
        }

        private static void TryRestore(Action restore, ref Exception firstFailure)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                if (firstFailure == null)
                    firstFailure = exception;
            }
        }

        private static FieldInfo RequireEditorField(string name, Type type)
        {
            FieldInfo field = typeof(EditorDirector).GetField(name, InstanceFields);
            if (field == null || field.FieldType != type)
                throw new MissingFieldException(typeof(EditorDirector).FullName, name);
            return field;
        }

        private sealed class DragState
        {
            internal DragState(
                int tribeId,
                GroundTarget target,
                float pressedScreenX,
                int pressedEngineX,
                int pressedEngineY,
                SelectionIdentity[] selection,
                int[] unitTypes,
                int commandButton,
                int screenWidth)
            {
                TribeId = tribeId;
                Target = target;
                PressedEngineX = pressedEngineX;
                PressedEngineY = pressedEngineY;
                Selection = selection;
                UnitTypes = unitTypes;
                ReleaseGate = new MoveFormationReleaseGate(
                    commandButton, pressedScreenX);
                ScreenWidth = screenWidth;
            }

            internal int TribeId { get; }
            internal GroundTarget Target { get; }
            internal int PressedEngineX { get; }
            internal int PressedEngineY { get; }
            internal SelectionIdentity[] Selection { get; }
            internal int[] UnitTypes { get; }
            internal MoveFormationReleaseGate ReleaseGate { get; }
            internal int ScreenWidth { get; }
            internal int CommandButton => ReleaseGate.CommandButton;
            internal int Spacing => ReleaseGate.Spacing;
        }

        private readonly struct GroundTarget
        {
            internal GroundTarget(
                int tileMapX,
                int tileMapY,
                int nativeX,
                int nativeY,
                int clickDepth,
                bool overTopHalf,
                int[] underCursorUnitIds,
                int troopDepth)
            {
                TileMapX = tileMapX;
                TileMapY = tileMapY;
                NativeX = nativeX;
                NativeY = nativeY;
                ClickDepth = clickDepth;
                OverTopHalf = overTopHalf;
                UnderCursorUnitIds = underCursorUnitIds ?? Array.Empty<int>();
                TroopDepth = troopDepth;
            }

            internal int TileMapX { get; }
            internal int TileMapY { get; }
            internal int NativeX { get; }
            internal int NativeY { get; }
            internal int ClickDepth { get; }
            internal bool OverTopHalf { get; }
            internal int[] UnderCursorUnitIds { get; }
            internal int TroopDepth { get; }
        }

        private readonly struct SelectionIdentity : IEquatable<SelectionIdentity>
        {
            internal SelectionIdentity(int unitId, uint globalId, int unitType)
            {
                UnitId = unitId;
                GlobalId = globalId;
                UnitType = unitType;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal int UnitType { get; }
            public bool Equals(SelectionIdentity other) =>
                UnitId == other.UnitId && GlobalId == other.GlobalId &&
                UnitType == other.UnitType;
        }
    }
}
