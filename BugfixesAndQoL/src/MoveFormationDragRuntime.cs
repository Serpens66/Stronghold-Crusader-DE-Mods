using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal static class MoveFormationCommandContext
    {
        // Input events publish on Unity's main thread, while the native
        // MoveHere callbacks execute during the simulation handoff. The one
        // in-flight command therefore has to cross that boundary; a
        // ThreadStatic context would silently lose it.
        private static readonly object syncRoot = new object();
        private static PendingCommand pending;
        private static ActiveCommand active;

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

            int encoded = (int)args.MoveType;
            if (!QueueNativeContract.TryDecodeFormationSpacing(
                    encoded, out int decoded, out int encodedSpacing))
                return;

            bool hasPrivateBits = (encoded & QueueNativeContract.MoveFormationSpacingMask) != 0;
            if (hasPrivateBits)
                args.MoveType = (SHCDESE.Interop.Enums.TribeMoveType)decoded;

            lock (syncRoot)
            {
                PendingCommand command = pending;
                bool matchesLocalRelease = command != null &&
                    command.Matches(args.TribeId, args.TileX, args.TileY);
                if (enabled && args.IsPatrolPath == 0 && (hasPrivateBits || matchesLocalRelease))
                {
                    active = new ActiveCommand(
                        args.TribeId,
                        args.TileX,
                        args.TileY,
                        hasPrivateBits ? encodedSpacing : command.Spacing);
                    if (matchesLocalRelease)
                        pending = null;
                }
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

        internal static void CompleteMoveOrder()
        {
            lock (syncRoot)
                active = null;
        }

        internal static void Clear()
        {
            lock (syncRoot)
            {
                pending = null;
                active = null;
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
            internal ActiveCommand(int tribeId, int tileX, int tileY, int spacing)
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
    }

    internal sealed unsafe class MoveFormationDragRuntime
    {
        private delegate void PreDllCallActionsDelegate(
            EditorDirector self, ref int mouseOverX, ref int mouseOverY);
        private delegate void StartSelectionDelegate(
            TroopSelector self, Vector2 start, Vector2 current);

        private const int MapWidth = 800;
        private const int MaximumPreviewCandidates = 4250 - 250;

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private FieldInfo mouseTileXField;
        private FieldInfo mouseTileYField;
        private FieldInfo mousePosXForEngineField;
        private FieldInfo mousePosYForEngineField;
        private FieldInfo underCursorChimpListField;
        private FieldInfo leftMouseStateForEngineField;
        private FieldInfo rightUpForEngineField;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetDiagnosticsRuntime markers;
        private readonly List<int> previewTiles = new List<int>();
        private readonly HashSet<string> loggedRejectReasons = new HashSet<string>();
        private Hook preDllCallActionsHook;
        private Hook startSelectionHook;
        private PreDllCallActionsDelegate preDllCallActionsOriginal;
        private StartSelectionDelegate startSelectionOriginal;
        private IDisposable keyDownSubscription;
        private IDisposable keyHeldSubscription;
        private IDisposable keyUpSubscription;
        private volatile DragState drag;
        private volatile bool installed;
        private volatile bool failed;

        internal MoveFormationDragRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            LargeMoveTargetDiagnosticsRuntime markers)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        }

        private bool Enabled => installed && !failed && markers.MarkerReplacementAvailable &&
            settings.EnableMod && settings.EnableMoveFormationEnhancements;

        internal void Install()
        {
            if (installed || failed)
                return;
            try
            {
                mouseTileXField = RequireField("mouseTileX", typeof(float));
                mouseTileYField = RequireField("mouseTileY", typeof(float));
                mousePosXForEngineField = RequireField("mousePosXForEngine", typeof(int));
                mousePosYForEngineField = RequireField("mousePosYForEngine", typeof(int));
                underCursorChimpListField = RequireField("underCursorChimpList", typeof(int[]));
                leftMouseStateForEngineField = RequireField(
                    "leftMouseStateForEngine", typeof(int));
                rightUpForEngineField = RequireField("rightUpForEngine", typeof(bool));

                MethodInfo preDllCallActions = typeof(EditorDirector).GetMethod(
                    "preDLLCallActions", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, null) ??
                    throw new MissingMethodException(
                        typeof(EditorDirector).FullName, "preDLLCallActions(ref int, ref int)");
                MethodInfo startSelection = typeof(TroopSelector).GetMethod(
                    "startSelection", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(Vector2), typeof(Vector2) }, null) ??
                    throw new MissingMethodException(
                        typeof(TroopSelector).FullName, "startSelection(Vector2, Vector2)");

                Hook installedPreDll = null;
                Hook installedStartSelection = null;
                IDisposable installedKeyDown = null;
                IDisposable installedKeyHeld = null;
                IDisposable installedKeyUp = null;
                try
                {
                    installedPreDll = new Hook(
                        preDllCallActions,
                        (PreDllCallActionsDelegate)PreDllCallActionsHook);
                    preDllCallActionsOriginal =
                        installedPreDll.GenerateTrampoline<PreDllCallActionsDelegate>();
                    installedStartSelection = new Hook(
                        startSelection,
                        (StartSelectionDelegate)StartSelectionHook);
                    startSelectionOriginal =
                        installedStartSelection.GenerateTrampoline<StartSelectionDelegate>();
                    installedKeyDown = InputR3EventHooks.OnKeyDown.Observable
                        .Subscribe(OnKeyDown);
                    installedKeyHeld = InputR3EventHooks.OnKey.Observable
                        .Subscribe(OnKeyHeld);
                    installedKeyUp = InputR3EventHooks.OnKeyUp.Observable
                        .Subscribe(OnKeyUp);

                    preDllCallActionsHook = installedPreDll;
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
                    installedPreDll?.Dispose();
                    throw;
                }
                installed = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Move formation drag R3 input, selection transition, and Vanilla command-transport hooks installed for the process lifetime.");
            }
            catch
            {
                RollBackUnpublishedHooks();
                throw;
            }
        }

        internal void ResetTransientState()
        {
            drag = null;
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
                DragState state = drag;
                if (state != null)
                {
                    if (IsShiftKey(args.Key))
                        AbortPreview("conflicting-input");
                    else if (TryGetMouseButton(args.Key, out int pressedButton) &&
                        state.Gesture.OnMouseDown(pressedButton) ==
                            MoveFormationGestureResult.Aborted)
                        AbortPreview("conflicting-input");
                    return;
                }

                int commandButton = MoveFormationSpacingPolicy.GetCommandMouseButton(
                    ConfigSettings.Settings_SH1RTSControls);
                if (args.Key != ToKeyCode(commandButton))
                    return;
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
                DragState state = drag;
                if (state == null)
                    return;
                if (IsShiftKey(args.Key))
                {
                    AbortPreview("shift-held");
                    return;
                }
                if (args.Key != ToKeyCode(state.CommandButton))
                    return;
                if (!ValidateActiveDrag(state, requirePureGround: false))
                {
                    AbortPreview("state-changed");
                    return;
                }

                MoveFormationGestureResult result = state.Gesture.OnHeld(
                    state.CommandButton, Input.mousePosition.x, state.ScreenWidth);
                if (result != MoveFormationGestureResult.SpacingChanged)
                    return;
                PublishPreview(state);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: spacing={state.Spacing}; preview={previewTiles.Count}.");
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
                DragState state = drag;
                if (state == null || args.Key != ToKeyCode(state.CommandButton))
                    return;
                if (state.Gesture.OnMouseUp(
                        state.CommandButton,
                        Input.mousePosition.x,
                        state.ScreenWidth) !=
                    MoveFormationGestureResult.Released)
                    return;
                markers.ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: release-event; spacing={state.Spacing}.");
            }
            catch (Exception exception)
            {
                FailOpen("input-up", exception);
            }
        }

        private void TryStartDrag(int commandButton)
        {
            string rejection = GetStartRejection(commandButton);
            if (rejection != null)
            {
                LogRejectedStart(rejection);
                return;
            }

            if (!TryCaptureSelection(
                    out SelectionIdentity[] selection,
                    out int tribeId,
                    out rejection))
            {
                LogRejectedStart(rejection);
                return;
            }

            float tileX = 0;
            float tileY = 0;
            MainControls.instance.getMouseMapTilePosition(ref tileX, ref tileY);
            int anchorX = (int)tileX;
            int anchorY = (int)tileY;
            if (!IsPureGroundTile(anchorX, anchorY, checkUnitUnderCursor: true))
            {
                LogRejectedStart("non-ground-target");
                return;
            }

            Vector3 mouse = Input.mousePosition;
            DragState state = new DragState(
                tribeId,
                anchorX,
                anchorY,
                mouse.x,
                (int)mouse.x,
                Screen.height - (int)mouse.y,
                selection,
                commandButton,
                Screen.width);
            drag = state;
            PublishPreview(state);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MOVE_FORMATION_DRAG: start; tribe={tribeId}; units={selection.Length}; " +
                $"button={commandButton}; target={anchorX},{anchorY}; spacing=2; " +
                $"preview={previewTiles.Count}.");
        }

        private void StartSelectionHook(
            TroopSelector self, Vector2 start, Vector2 current)
        {
            try
            {
                DragState state = drag;
                if (!failed && state != null && state.CommandButton == 0 && Enabled)
                {
                    if (MainControls.instance != null)
                        MainControls.instance.CurrentAction = 0;
                    Shared.DebugLogHelper.LogDebug(
                        log, "MOVE_FORMATION_DRAG: suppressed Vanilla selection-box transition.");
                    return;
                }
            }
            catch (Exception exception)
            {
                FailOpen("selection-transition", exception);
            }
            startSelectionOriginal(self, start, current);
        }

        private void PreDllCallActionsHook(
            EditorDirector self, ref int mouseOverX, ref int mouseOverY)
        {
            DragState state = drag;
            if (failed || state == null)
            {
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
                return;
            }

            bool matchingRelease;
            try
            {
                matchingRelease = IsMatchingVanillaRelease(self, state);
            }
            catch (Exception exception)
            {
                FailOpen("release-detection", exception);
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
                return;
            }
            if (!matchingRelease)
            {
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
                return;
            }

            try
            {
                state.Gesture.OnMouseUp(
                    state.CommandButton,
                    (int)mousePosXForEngineField.GetValue(self),
                    state.ScreenWidth);

                if (!ValidateActiveDrag(state, requirePureGround: true))
                {
                    AbortPreview("release-validation");
                    preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
                    return;
                }
            }
            catch (Exception exception)
            {
                FailOpen("release-validation", exception);
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
                return;
            }

            MainControls controls = MainControls.instance;
            object oldTileX = null;
            object oldTileY = null;
            object oldMouseX = null;
            object oldMouseY = null;
            object oldUnderCursor = null;
            int oldDepth = 0;
            int oldClickDepth = 0;
            bool stateCaptured = false;
            bool trampolineEntered = false;
            try
            {
                oldTileX = mouseTileXField.GetValue(self);
                oldTileY = mouseTileYField.GetValue(self);
                oldMouseX = mousePosXForEngineField.GetValue(self);
                oldMouseY = mousePosYForEngineField.GetValue(self);
                oldUnderCursor = underCursorChimpListField.GetValue(self);
                oldDepth = self.lastTroopOverDepth;
                oldClickDepth = controls.mouseTileClickDepth;
                stateCaptured = true;

                MoveFormationCommandContext.Arm(
                    state.TribeId, state.TileX, state.TileY, state.Spacing);
                markers.ClearPreview();
                mouseTileXField.SetValue(self, (float)state.TileX);
                mouseTileYField.SetValue(self, (float)state.TileY);
                mousePosXForEngineField.SetValue(self, state.PressedEngineX);
                mousePosYForEngineField.SetValue(self, state.PressedEngineY);
                underCursorChimpListField.SetValue(self, Array.Empty<int>());
                self.lastTroopOverDepth = -1;
                controls.mouseTileClickDepth = 49;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: Vanilla release handoff; tribe={state.TribeId}; " +
                    $"target={state.TileX},{state.TileY}; spacing={state.Spacing}.");
                trampolineEntered = true;
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
            }
            catch (Exception exception) when (!trampolineEntered)
            {
                Exception restoreFailure = stateCaptured
                    ? RestoreInputState(
                        self,
                        controls,
                        oldTileX,
                        oldTileY,
                        oldMouseX,
                        oldMouseY,
                        oldUnderCursor,
                        oldDepth,
                        oldClickDepth)
                    : null;
                stateCaptured = false;
                FailOpen(
                    "release-anchor",
                    restoreFailure == null
                        ? exception
                        : new AggregateException(exception, restoreFailure));
                preDllCallActionsOriginal(self, ref mouseOverX, ref mouseOverY);
            }
            finally
            {
                Exception restoreFailure = stateCaptured
                    ? RestoreInputState(
                        self,
                        controls,
                        oldTileX,
                        oldTileY,
                        oldMouseX,
                        oldMouseY,
                        oldUnderCursor,
                        oldDepth,
                        oldClickDepth)
                    : null;
                drag = null;
                MoveFormationCommandContext.Clear();
                markers.ClearPreview();
                if (restoreFailure != null)
                    FailOpen("release-anchor-restore", restoreFailure);
            }
        }

        private bool IsMatchingVanillaRelease(
            EditorDirector director, DragState state)
        {
            int currentCommandButton = MoveFormationSpacingPolicy.GetCommandMouseButton(
                ConfigSettings.Settings_SH1RTSControls);
            if (currentCommandButton != state.CommandButton)
                return false;
            return MoveFormationDragEligibility.IsVanillaRelease(
                state.CommandButton,
                (int)leftMouseStateForEngineField.GetValue(director),
                (bool)rightUpForEngineField.GetValue(director));
        }

        private void PublishPreview(DragState state)
        {
            previewTiles.Clear();
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int anchorTile = GameTileManagerAPI.Instance.GetTileId(state.TileX, state.TileY);
            if ((uint)anchorTile >= (uint)components.Length)
                throw new InvalidOperationException("Preview anchor lies outside the path-component grid.");
            ushort anchorComponent = components[anchorTile];
            int required = Math.Min(state.Selection.Length, MaximumPreviewCandidates);
            foreach (MoveFormationOffset offset in
                MoveFormationSpacingPolicy.EnumerateManhattanOffsets(
                    state.Spacing, MapWidth * 2 - 2))
            {
                TryAddPreviewTile(
                    state.TileX + offset.X,
                    state.TileY + offset.Y,
                    anchorComponent,
                    components);
                if (previewTiles.Count >= required)
                    break;
            }
            markers.SetPreview(previewTiles);
        }

        private void TryAddPreviewTile(
            int x, int y, ushort anchorComponent, Span<ushort> components)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth)
                return;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tileId >= (uint)components.Length || components[tileId] == 0 ||
                (anchorComponent != 0 && components[tileId] != anchorComponent))
                return;
            previewTiles.Add(tileId);
        }

        private static bool TryCaptureSelection(
            out SelectionIdentity[] identities, out int tribeId, out string rejection)
        {
            SelectedUnitInfo[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            if (selected == null || selected.Length < 2)
            {
                identities = Array.Empty<SelectionIdentity>();
                tribeId = 0;
                rejection = "selection-count";
                return false;
            }

            identities = new SelectionIdentity[selected.Length];
            tribeId = -1;
            for (int index = 0; index < selected.Length; index++)
            {
                int unitId = selected[index].UnitId;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_GlobalId == 0)
                {
                    rejection = "selection-invalid-unit";
                    return false;
                }
                if (tribeId < 0)
                    tribeId = unit->r_TribeId;
                else if (tribeId != unit->r_TribeId)
                {
                    rejection = "selection-mixed-tribe";
                    return false;
                }
                identities[index] = new SelectionIdentity(unitId, unit->r_GlobalId);
            }
            Array.Sort(identities, (left, right) => left.UnitId.CompareTo(right.UnitId));

            bool mapEditorSelection = MainViewModel.viewModelLoaded &&
                MainViewModel.Instance != null && MainViewModel.Instance.IsMapEditorMode;
            if (!MoveFormationDragEligibility.RequiresNormalTribeOwnership(
                    mapEditorSelection))
            {
                rejection = null;
                return true;
            }

            if (!GameTribeManagerAPI.Instance.IsValidId(tribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner) ||
                tribe->r_PlayerIdOwner != GamePlayerManagerAPI.Instance.GetLocalPlayerId())
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
                    out SelectionIdentity[] current, out _, out _) ||
                current.Length != expected.Length)
                return false;
            for (int index = 0; index < current.Length; index++)
            {
                if (!current[index].Equals(expected[index]))
                    return false;
            }
            return true;
        }

        private static bool HasValidMap() =>
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            GameMap.instance != null && MainControls.instance != null;

        private static bool IsPureGroundTile(
            int x, int y, bool checkUnitUnderCursor)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth ||
                GameMap.instance.getMapTile(x, y) == null)
                return false;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            var tileManager = GameTileManagerAPI.Instance.TileManager;
            if (tileId < 0 || tileManager == null ||
                (uint)tileId >= (uint)tileManager.StructureGrid.Length)
                return false;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if ((uint)tileId >= (uint)components.Length || components[tileId] == 0 ||
                tileManager.StructureGrid[tileId] != 0)
                return false;
            if (!checkUnitUnderCursor)
                return true;

            int[] underCursor = null;
            int depth = -1;
            GameMap.instance.grabTroopsOnScreen(
                Vector2.zero,
                Vector2.zero,
                ref underCursor,
                Input.mousePosition,
                ref depth);
            return underCursor == null || underCursor.Length == 0;
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
            if (MainControls.instance.CurrentAction != 0)
                return "non-neutral-action";
            if (IsShiftHeld())
                return "shift-held";
            return null;
        }

        private bool ValidateActiveDrag(
            DragState state, bool requirePureGround)
        {
            if (!Enabled || !HasValidMap() ||
                state.CommandButton != MoveFormationSpacingPolicy.GetCommandMouseButton(
                    ConfigSettings.Settings_SH1RTSControls) ||
                FatControler.instance == null || FatControler.instance.overNoesisGUI() ||
                IsShiftHeld() || !SelectionMatches(state.Selection))
                return false;
            return !requirePureGround ||
                IsPureGroundTile(state.TileX, state.TileY, checkUnitUnderCursor: false);
        }

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

        private void LogRejectedStart(string reason)
        {
            string normalized = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            if (!loggedRejectReasons.Add(normalized))
                return;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MOVE_FORMATION_DRAG: start-rejected; reason={normalized}; " +
                "further occurrences of this reason are suppressed.");
        }

        private void AbortPreview(string reason)
        {
            DragState state = drag;
            bool hadDrag = state != null;
            state?.Gesture.Abort();
            drag = null;
            MoveFormationCommandContext.Clear();
            markers.ClearPreview();
            if (hadDrag)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"MOVE_FORMATION_DRAG: aborted; reason={reason ?? "unknown"}.");
            }
        }

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
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
            preDllCallActionsHook?.Dispose();
            keyUpSubscription = null;
            keyHeldSubscription = null;
            keyDownSubscription = null;
            startSelectionHook = null;
            preDllCallActionsHook = null;
            startSelectionOriginal = null;
            preDllCallActionsOriginal = null;
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
            int oldClickDepth)
        {
            Exception firstFailure = null;
            TryRestore(() => mouseTileXField.SetValue(director, oldTileX), ref firstFailure);
            TryRestore(() => mouseTileYField.SetValue(director, oldTileY), ref firstFailure);
            TryRestore(() => mousePosXForEngineField.SetValue(director, oldMouseX), ref firstFailure);
            TryRestore(() => mousePosYForEngineField.SetValue(director, oldMouseY), ref firstFailure);
            TryRestore(() => underCursorChimpListField.SetValue(director, oldUnderCursor), ref firstFailure);
            TryRestore(() => director.lastTroopOverDepth = oldDepth, ref firstFailure);
            TryRestore(() => controls.mouseTileClickDepth = oldClickDepth, ref firstFailure);
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

        private static FieldInfo RequireField(string name, Type type)
        {
            FieldInfo field = typeof(EditorDirector).GetField(name, InstanceFields);
            if (field == null || field.FieldType != type)
                throw new MissingFieldException(typeof(EditorDirector).FullName, name);
            return field;
        }

        private sealed class DragState
        {
            internal DragState(
                int tribeId, int tileX, int tileY, float pressedScreenX,
                int pressedEngineX, int pressedEngineY,
                SelectionIdentity[] selection, int commandButton, int screenWidth)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                PressedEngineX = pressedEngineX;
                PressedEngineY = pressedEngineY;
                Selection = selection;
                Gesture = new MoveFormationDragGesture(commandButton, pressedScreenX);
                ScreenWidth = screenWidth;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int PressedEngineX { get; }
            internal int PressedEngineY { get; }
            internal SelectionIdentity[] Selection { get; }
            internal MoveFormationDragGesture Gesture { get; }
            internal int ScreenWidth { get; }
            internal int CommandButton => Gesture.CommandButton;
            internal int Spacing => Gesture.Spacing;
        }

        private readonly struct SelectionIdentity : IEquatable<SelectionIdentity>
        {
            internal SelectionIdentity(int unitId, uint globalId)
            {
                UnitId = unitId;
                GlobalId = globalId;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            public bool Equals(SelectionIdentity other) =>
                UnitId == other.UnitId && GlobalId == other.GlobalId;
        }
    }
}
