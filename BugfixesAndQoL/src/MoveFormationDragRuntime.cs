using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI;
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
        // EditorDirector.Update publishes on Unity's main thread, while
        // EngineInterface.run and the native MoveHere callbacks execute on the
        // simulation thread. The one in-flight command therefore has to cross
        // that boundary; a ThreadStatic context would silently lose it.
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
        private delegate void EditorUpdateDelegate(EditorDirector self);
        private delegate int EngineRunDelegate(bool multiplayerFrameSkip);

        private const int MapWidth = 800;
        private const int MaximumPreviewCandidates = 4250 - 250;

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private FieldInfo mouseTileXField;
        private FieldInfo mouseTileYField;
        private FieldInfo mousePosXForEngineField;
        private FieldInfo mousePosYForEngineField;
        private FieldInfo underCursorChimpListField;
        private FieldInfo overNoesisUiField;
        private FieldInfo troopSelectMouseStartField;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetDiagnosticsRuntime markers;
        private readonly List<int> previewTiles = new List<int>();
        private Hook editorUpdateHook;
        private Hook engineRunHook;
        private EditorUpdateDelegate editorUpdateOriginal;
        private EngineRunDelegate engineRunOriginal;
        private volatile DragState drag;
        private volatile bool injectReleasedAnchor;
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
                overNoesisUiField = RequireField("overNoesisUI", typeof(bool));
                troopSelectMouseStartField = RequireField("troopSelectMouseStart", typeof(Vector2));
                MethodInfo update = typeof(EditorDirector).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null) ??
                    throw new MissingMethodException(typeof(EditorDirector).FullName, "Update");
                MethodInfo run = typeof(EngineInterface).GetMethod(
                    "run", BindingFlags.Static | BindingFlags.Public,
                    null, new[] { typeof(bool) }, null) ??
                    throw new MissingMethodException(typeof(EngineInterface).FullName, "run(bool)");

                editorUpdateHook = new Hook(update, (EditorUpdateDelegate)EditorUpdateHook);
                editorUpdateOriginal = editorUpdateHook.GenerateTrampoline<EditorUpdateDelegate>();
                engineRunHook = new Hook(run, (EngineRunDelegate)EngineRunHook);
                engineRunOriginal = engineRunHook.GenerateTrampoline<EngineRunDelegate>();
                installed = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Move formation drag input and release-anchor hooks installed for the process lifetime.");
            }
            catch
            {
                RollBackUnpublishedHooks();
                throw;
            }
        }

        internal void ResetTransientState()
        {
            RestoreLeftSelectionStart(drag);
            drag = null;
            injectReleasedAnchor = false;
            MoveFormationCommandContext.Clear();
            markers.ClearPreview();
        }

        internal void DisableForProcess(string contract, Exception exception)
        {
            if (!failed)
                FailOpen(contract, exception);
        }

        private void EditorUpdateHook(EditorDirector self)
        {
            if (!failed)
            {
                try
                {
                    PrepareReleaseBeforeVanilla(self);
                }
                catch (Exception exception)
                {
                    FailOpen("pre-release-input", exception);
                }
            }
            try
            {
                editorUpdateOriginal(self);
            }
            catch
            {
                ResetTransientState();
                throw;
            }
            if (failed)
                return;
            try
            {
                ProcessInput(self);
            }
            catch (Exception exception)
            {
                FailOpen("input", exception);
            }
        }

        private int EngineRunHook(bool multiplayerFrameSkip)
        {
            if (!injectReleasedAnchor || failed)
                return engineRunOriginal(multiplayerFrameSkip);
            DragState releasedDrag = drag;
            if (releasedDrag == null)
                return engineRunOriginal(multiplayerFrameSkip);
            bool canInject;
            try
            {
                canInject = Enabled && HasValidMap() &&
                    SelectionMatches(releasedDrag.Selection) &&
                    IsPureGroundTile(releasedDrag.TileX, releasedDrag.TileY);
            }
            catch (Exception exception)
            {
                FailOpen("release-anchor-validate", exception);
                return engineRunOriginal(multiplayerFrameSkip);
            }
            if (!canInject)
            {
                ResetTransientState();
                return engineRunOriginal(multiplayerFrameSkip);
            }

            EditorDirector director = EditorDirector.instance;
            MainControls controls = MainControls.instance;
            if (director == null || controls == null)
            {
                ResetTransientState();
                return engineRunOriginal(multiplayerFrameSkip);
            }
            object oldTileX;
            object oldTileY;
            object oldMouseX;
            object oldMouseY;
            object oldUnderCursor;
            int oldDepth;
            int oldClickDepth;
            try
            {
                oldTileX = mouseTileXField.GetValue(director);
                oldTileY = mouseTileYField.GetValue(director);
                oldMouseX = mousePosXForEngineField.GetValue(director);
                oldMouseY = mousePosYForEngineField.GetValue(director);
                oldUnderCursor = underCursorChimpListField.GetValue(director);
                oldDepth = director.lastTroopOverDepth;
                oldClickDepth = controls.mouseTileClickDepth;
            }
            catch (Exception exception)
            {
                FailOpen("release-anchor-prepare", exception);
                return engineRunOriginal(multiplayerFrameSkip);
            }
            bool trampolineEntered = false;
            try
            {
                mouseTileXField.SetValue(director, (float)releasedDrag.TileX);
                mouseTileYField.SetValue(director, (float)releasedDrag.TileY);
                mousePosXForEngineField.SetValue(director, releasedDrag.PressedEngineX);
                mousePosYForEngineField.SetValue(director, releasedDrag.PressedEngineY);
                underCursorChimpListField.SetValue(director, Array.Empty<int>());
                director.lastTroopOverDepth = -1;
                controls.mouseTileClickDepth = 49;
                trampolineEntered = true;
                return engineRunOriginal(multiplayerFrameSkip);
            }
            catch (Exception exception) when (!trampolineEntered)
            {
                FailOpen("release-anchor", exception);
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
                    releasedDrag);
                drag = null;
                injectReleasedAnchor = false;
                MoveFormationCommandContext.Clear();
                if (restoreFailure != null)
                    FailOpen("release-anchor-restore", restoreFailure);
            }
            return engineRunOriginal(multiplayerFrameSkip);
        }

        private void ProcessInput(EditorDirector director)
        {
            int commandButton = MoveFormationSpacingPolicy.GetCommandMouseButton(
                ConfigSettings.Settings_SH1RTSControls);
            int oppositeButton = commandButton == 0 ? 1 : 0;

            if (drag != null)
            {
                if (injectReleasedAnchor)
                    return;
                if (!Enabled || !HasValidMap() || director.shiftPressed ||
                    (bool)overNoesisUiField.GetValue(director) ||
                    Input.GetMouseButtonDown(oppositeButton) ||
                    !SelectionMatches(drag.Selection))
                {
                    AbortPreview();
                    return;
                }

                if (Input.GetMouseButtonUp(commandButton))
                {
                    MoveFormationCommandContext.Arm(
                        drag.TribeId, drag.TileX, drag.TileY, drag.Spacing);
                    injectReleasedAnchor = true;
                    markers.ClearPreview();
                    return;
                }

                if (!Input.GetMouseButton(commandButton))
                {
                    AbortPreview();
                    return;
                }

                int spacing = MoveFormationSpacingPolicy.FromHorizontalDrag(
                    drag.PressedScreenX, Input.mousePosition.x, Screen.width);
                if (spacing != drag.Spacing)
                {
                    drag.Spacing = spacing;
                    PublishPreview(drag);
                }
                return;
            }

            if (!Enabled || !Input.GetMouseButtonDown(commandButton) ||
                director.shiftPressed || !HasValidMap() ||
                (bool)overNoesisUiField.GetValue(director) ||
                MainControls.instance.CurrentAction != 0)
                return;

            int[] underCursor = (int[])underCursorChimpListField.GetValue(director);
            if (underCursor != null && underCursor.Length != 0)
                return;
            if (!TryCaptureSelection(out SelectionIdentity[] selection, out int tribeId))
                return;

            float tileX = 0;
            float tileY = 0;
            MainControls.instance.getMouseMapTilePosition(ref tileX, ref tileY);
            int anchorX = (int)tileX;
            int anchorY = (int)tileY;
            if (!IsPureGroundTile(anchorX, anchorY))
                return;

            bool suppressLeftSelection = commandButton == 0;
            object oldTroopSelectMouseStart = suppressLeftSelection
                ? troopSelectMouseStartField.GetValue(director)
                : null;
            drag = new DragState(
                tribeId,
                anchorX,
                anchorY,
                Input.mousePosition.x,
                (int)Input.mousePosition.x,
                Screen.height - (int)Input.mousePosition.y,
                selection,
                director,
                suppressLeftSelection,
                oldTroopSelectMouseStart);
            if (suppressLeftSelection)
                troopSelectMouseStartField.SetValue(director, new Vector2(-1f, -1f));
            PublishPreview(drag);
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
            out SelectionIdentity[] identities, out int tribeId)
        {
            SelectedUnitInfo[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            if (selected == null || selected.Length < 2)
            {
                identities = Array.Empty<SelectionIdentity>();
                tribeId = 0;
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
                    return false;
                if (tribeId < 0)
                    tribeId = unit->r_TribeId;
                else if (tribeId != unit->r_TribeId)
                    return false;
                identities[index] = new SelectionIdentity(unitId, unit->r_GlobalId);
            }
            Array.Sort(identities, (left, right) => left.UnitId.CompareTo(right.UnitId));
            if (!GameTribeManagerAPI.Instance.IsValidId(tribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner) ||
                tribe->r_PlayerIdOwner != GamePlayerManagerAPI.Instance.GetLocalPlayerId())
                return false;
            return true;
        }

        private static bool SelectionMatches(SelectionIdentity[] expected)
        {
            if (!TryCaptureSelection(out SelectionIdentity[] current, out _ ) ||
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

        private static bool IsPureGroundTile(int x, int y)
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
            return (uint)tileId < (uint)components.Length && components[tileId] != 0 &&
                tileManager.StructureGrid[tileId] == 0;
        }

        private void AbortPreview()
        {
            RestoreLeftSelectionStart(drag);
            drag = null;
            injectReleasedAnchor = false;
            MoveFormationCommandContext.Clear();
            markers.ClearPreview();
        }

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
            AbortPreview();
            Shared.DebugLogHelper.LogError(
                log,
                $"MOVE_FORMATION_DRAG_DISABLED: contract={contract}; Vanilla input retained; {exception}");
        }

        private void RollBackUnpublishedHooks()
        {
            editorUpdateHook?.Dispose();
            engineRunHook?.Dispose();
            editorUpdateHook = null;
            engineRunHook = null;
            editorUpdateOriginal = null;
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
            DragState releasedDrag)
        {
            Exception firstFailure = null;
            TryRestore(() => mouseTileXField.SetValue(director, oldTileX), ref firstFailure);
            TryRestore(() => mouseTileYField.SetValue(director, oldTileY), ref firstFailure);
            TryRestore(() => mousePosXForEngineField.SetValue(director, oldMouseX), ref firstFailure);
            TryRestore(() => mousePosYForEngineField.SetValue(director, oldMouseY), ref firstFailure);
            TryRestore(() => underCursorChimpListField.SetValue(director, oldUnderCursor), ref firstFailure);
            TryRestore(() => director.lastTroopOverDepth = oldDepth, ref firstFailure);
            TryRestore(() => controls.mouseTileClickDepth = oldClickDepth, ref firstFailure);
            if (releasedDrag?.SuppressLeftSelection == true)
            {
                TryRestore(
                    () => troopSelectMouseStartField.SetValue(
                        releasedDrag.Director, releasedDrag.OldTroopSelectMouseStart),
                    ref firstFailure);
            }
            return firstFailure;
        }

        private void RestoreLeftSelectionStart(DragState state)
        {
            if (state?.SuppressLeftSelection != true || state.Director == null)
                return;
            try
            {
                troopSelectMouseStartField.SetValue(state.Director, state.OldTroopSelectMouseStart);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MOVE_FORMATION_LEFT_SELECTION_RESTORE_FAILED: {exception}");
            }
        }

        private void PrepareReleaseBeforeVanilla(EditorDirector director)
        {
            DragState state = drag;
            if (state == null || injectReleasedAnchor)
                return;
            int commandButton = MoveFormationSpacingPolicy.GetCommandMouseButton(
                ConfigSettings.Settings_SH1RTSControls);
            if (!Input.GetMouseButtonUp(commandButton))
                return;

            state.Spacing = MoveFormationSpacingPolicy.FromHorizontalDrag(
                state.PressedScreenX, Input.mousePosition.x, Screen.width);

            int oppositeButton = commandButton == 0 ? 1 : 0;
            bool shiftPressed = Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
            if (!Enabled || !HasValidMap() || shiftPressed ||
                FatControler.instance == null || FatControler.instance.overNoesisGUI() ||
                Input.GetMouseButtonDown(oppositeButton) ||
                !SelectionMatches(state.Selection))
            {
                AbortPreview();
                return;
            }

            MoveFormationCommandContext.Arm(
                state.TribeId, state.TileX, state.TileY, state.Spacing);
            injectReleasedAnchor = true;
            markers.ClearPreview();
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
                int pressedEngineX, int pressedEngineY, SelectionIdentity[] selection,
                EditorDirector director, bool suppressLeftSelection,
                object oldTroopSelectMouseStart)
            {
                TribeId = tribeId;
                TileX = tileX;
                TileY = tileY;
                PressedScreenX = pressedScreenX;
                PressedEngineX = pressedEngineX;
                PressedEngineY = pressedEngineY;
                Selection = selection;
                Director = director;
                SuppressLeftSelection = suppressLeftSelection;
                OldTroopSelectMouseStart = oldTroopSelectMouseStart;
                Spacing = MoveFormationSpacingPolicy.Default;
            }

            internal int TribeId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal float PressedScreenX { get; }
            internal int PressedEngineX { get; }
            internal int PressedEngineY { get; }
            internal SelectionIdentity[] Selection { get; }
            internal EditorDirector Director { get; }
            internal bool SuppressLeftSelection { get; }
            internal object OldTroopSelectMouseStart { get; }
            internal int Spacing { get; set; }
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
