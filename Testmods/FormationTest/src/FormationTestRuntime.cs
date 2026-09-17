using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FormationTest
{
    internal sealed unsafe class FormationTestRuntime
    {
        private delegate int EngineRunDelegate(bool mpFrameSkip);
        private delegate void StartSelectionDelegate(
            TroopSelector self, UnityEngine.Vector2 start, UnityEngine.Vector2 current);
        private delegate void CameraUpdateDelegate(CameraControls2D self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinFormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetGroupUnitIdDelegate(IntPtr tribeManager, int tribeId, int ordinal);

        private const int ProtocolVersion = 1;
        private const int MapWidth = 800;
        private const int MaximumUnitCount = 10000;
        private const int MaximumTribeCount = 4500;
        private const int TribeRecordSize = 0x688;
        private const int TribeUnitCountOffset = 0x5C;
        private const int UnitGroupInactiveStateOffset = 0x29C;
        private const int StandardSelectorRva = 0xE1D30;
        private const int AssassinSelectorRva = 0xE0970;
        private const int GetGroupUnitIdRva = 0x119F90;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativeTribeManagerRva = 0x7CC6720;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int ExpectedSelectorDisplacedBytes = 15;
        private const int MaximumPreviewCandidates = 8192;
        private const int MinimumDragTileDistance = 2;

        private static readonly byte[] StandardSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x18,
            0x48, 0x89, 0x74, 0x24, 0x20
        };

        private static readonly byte[] AssassinSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18
        };

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly ManualLogSource log;
        private readonly CrusaderLibraryLoadContext libraryContext;
        private readonly ConfigEntry<FormationKind> formationConfig;
        private readonly ConfigEntry<int> densityConfig;
        private readonly ConfigEntry<bool> rearSortingConfig;
        private readonly object stateSync = new object();
        private readonly DetourHandle<FormationSlotDelegate> standardSelectorHandle =
            new DetourHandle<FormationSlotDelegate>();
        private readonly DetourHandle<AssassinFormationSlotDelegate> assassinSelectorHandle =
            new DetourHandle<AssassinFormationSlotDelegate>();

        private HookTransaction nativeTransaction;
        private Hook engineRunHook;
        private Hook startSelectionHook;
        private Hook cameraUpdateHook;
        private EngineRunDelegate engineRunOriginal;
        private StartSelectionDelegate startSelectionOriginal;
        private CameraUpdateDelegate cameraUpdateOriginal;
        private IDisposable keyDownSubscription;
        private IDisposable keyUpSubscription;
        private IDisposable packetSubscription;
        private R3PacketEventHook<FormationOrderPacket> packetHook;
        private FieldInfo leftMouseStateField;
        private FieldInfo rightMouseUpField;
        private IntPtr nativeTribeManager;
        private IntPtr nativePathManager;
        private byte* movementTargetAvailability;
        private GetGroupUnitIdDelegate getGroupUnitId;
        private MethodInfo sendChorePayloadMethod;
        private ActiveDrag drag;
        private ActiveFormationCommand activeCommand;
        private int nextOperationId;
        private int lastWheelFrame = -1;
        private bool initialized;
        private bool failed;

        internal FormationTestRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext libraryContext,
            ConfigEntry<FormationKind> formationConfig,
            ConfigEntry<int> densityConfig,
            ConfigEntry<bool> rearSortingConfig)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.libraryContext = libraryContext ??
                throw new ArgumentNullException(nameof(libraryContext));
            this.formationConfig = formationConfig ??
                throw new ArgumentNullException(nameof(formationConfig));
            this.densityConfig = densityConfig ??
                throw new ArgumentNullException(nameof(densityConfig));
            this.rearSortingConfig = rearSortingConfig ??
                throw new ArgumentNullException(nameof(rearSortingConfig));
        }

        internal void Initialize()
        {
            if (initialized)
                return;

            HookTransaction pendingNative = null;
            Hook pendingEngineRun = null;
            Hook pendingStartSelection = null;
            Hook pendingCameraUpdate = null;
            IDisposable pendingKeyDown = null;
            IDisposable pendingKeyUp = null;
            IDisposable pendingPacket = null;
            try
            {
                ulong libraryBase = unchecked((ulong)libraryContext.ModuleHandle.ToInt64());
                ValidateNativeContracts(libraryContext.Memory);
                nativeTribeManager = (IntPtr)(libraryBase + NativeTribeManagerRva);
                nativePathManager = (IntPtr)(libraryBase + NativePathManagerRva);
                movementTargetAvailability = (byte*)(libraryBase +
                    MovementTargetAvailabilityRva);
                getGroupUnitId = Marshal.GetDelegateForFunctionPointer<GetGroupUnitIdDelegate>(
                    (IntPtr)(libraryBase + GetGroupUnitIdRva));
                sendChorePayloadMethod = typeof(GameNetworkAPI).GetMethod(
                    "SendScriptExtenderChorePayload",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(byte[]) },
                    null) ?? throw new MissingMethodException(
                        typeof(GameNetworkAPI).FullName,
                        "SendScriptExtenderChorePayload(byte[])");
                if (sendChorePayloadMethod.ReturnType != typeof(bool))
                    throw new MissingMethodException(
                        typeof(GameNetworkAPI).FullName,
                        "bool SendScriptExtenderChorePayload(byte[])");

                pendingNative = new HookTransaction(
                    libraryContext.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pendingNative.AddDetour(
                    standardSelectorHandle,
                    HookTarget.FromAddress(libraryBase + StandardSelectorRva),
                    (FormationSlotDelegate)ChooseStandardFormationSlot);
                pendingNative.AddDetour(
                    assassinSelectorHandle,
                    HookTarget.FromAddress(libraryBase + AssassinSelectorRva),
                    (AssassinFormationSlotDelegate)ChooseAssassinFormationSlot);
                CommitResult nativeResult = pendingNative.Commit();
                NativeDetour<FormationSlotDelegate> standardDetour =
                    standardSelectorHandle.Hook as NativeDetour<FormationSlotDelegate>;
                NativeDetour<AssassinFormationSlotDelegate> assassinDetour =
                    assassinSelectorHandle.Hook as NativeDetour<AssassinFormationSlotDelegate>;
                if (!nativeResult.IsCompleteSuccess || !standardSelectorHandle.Success ||
                    !assassinSelectorHandle.Success ||
                    standardDetour == null || assassinDetour == null ||
                    standardDetour.DisplacedByteCount != ExpectedSelectorDisplacedBytes ||
                    assassinDetour.DisplacedByteCount != ExpectedSelectorDisplacedBytes)
                {
                    throw new InvalidOperationException(
                        $"Formation selector transaction failed or displaced an unexpected span: " +
                        $"result={nativeResult}, standard={standardDetour?.DisplacedByteCount}, " +
                        $"assassin={assassinDetour?.DisplacedByteCount}.");
                }

                leftMouseStateField = RequireEditorField("leftMouseStateForEngine", typeof(int));
                rightMouseUpField = RequireEditorField("rightUpForEngine", typeof(bool));
                MethodInfo engineRun = typeof(EngineInterface).GetMethod(
                    "run", BindingFlags.Static | BindingFlags.Public,
                    null, new[] { typeof(bool) }, null) ??
                    throw new MissingMethodException(typeof(EngineInterface).FullName, "run(bool)");
                MethodInfo startSelection = typeof(TroopSelector).GetMethod(
                    "startSelection", BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(UnityEngine.Vector2), typeof(UnityEngine.Vector2) },
                    null) ?? throw new MissingMethodException(
                        typeof(TroopSelector).FullName,
                        "startSelection(Vector2, Vector2)");
                MethodInfo cameraUpdate = typeof(CameraControls2D).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic) ??
                    throw new MissingMethodException(typeof(CameraControls2D).FullName, "Update()");

                pendingEngineRun = new Hook(engineRun, (EngineRunDelegate)EngineRunHook);
                engineRunOriginal = pendingEngineRun.GenerateTrampoline<EngineRunDelegate>();
                pendingStartSelection = new Hook(
                    startSelection, (StartSelectionDelegate)StartSelectionHook);
                startSelectionOriginal =
                    pendingStartSelection.GenerateTrampoline<StartSelectionDelegate>();
                pendingCameraUpdate = new Hook(cameraUpdate, (CameraUpdateDelegate)CameraUpdateHook);
                cameraUpdateOriginal =
                    pendingCameraUpdate.GenerateTrampoline<CameraUpdateDelegate>();

                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<FormationOrderPacket>();
                pendingPacket = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
                pendingKeyDown = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);
                pendingKeyUp = InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnKeyUp);

                nativeTransaction = pendingNative;
                pendingNative = null;
                engineRunHook = pendingEngineRun;
                pendingEngineRun = null;
                startSelectionHook = pendingStartSelection;
                pendingStartSelection = null;
                cameraUpdateHook = pendingCameraUpdate;
                pendingCameraUpdate = null;
                packetSubscription = pendingPacket;
                pendingPacket = null;
                keyDownSubscription = pendingKeyDown;
                pendingKeyDown = null;
                keyUpSubscription = pendingKeyUp;
                pendingKeyUp = null;
                initialized = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "FormationTest active: synchronized Chore packet, mouse gesture hooks, " +
                    $"standardSelector=0x{StandardSelectorRva:X}/span{ExpectedSelectorDisplacedBytes}, " +
                    $"assassinSelector=0x{AssassinSelectorRva:X}/span{ExpectedSelectorDisplacedBytes}.");
            }
            catch
            {
                pendingKeyUp?.Dispose();
                pendingKeyDown?.Dispose();
                pendingPacket?.Dispose();
                pendingCameraUpdate?.Dispose();
                pendingStartSelection?.Dispose();
                pendingEngineRun?.Dispose();
                pendingNative?.Dispose();
                throw;
            }
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (!initialized || failed || args == null || args.Phase != EventHookPhase.Post)
                return;
            try
            {
                ActiveDrag state;
                lock (stateSync)
                    state = drag;

                if (state != null)
                {
                    if (args.Key == ToKeyCode(1 - state.CommandButton))
                    {
                        state.Kind = FormationModel.Next(state.Kind);
                        PublishPreview(state, force: true);
                        return;
                    }
                    if (args.Key == KeyCode.Mouse2)
                    {
                        state.RearSorting = !state.RearSorting;
                        PublishPreview(state, force: true);
                        return;
                    }
                    return;
                }

                int commandButton = GetCommandMouseButton();
                if (args.Key == ToKeyCode(commandButton))
                    TryStartDrag(commandButton);
            }
            catch (Exception exception)
            {
                FailOpen("input-down", exception);
            }
        }

        private void OnKeyUp(UnityInputEventArgs args)
        {
            if (!initialized || failed || args == null || args.Phase != EventHookPhase.Post)
                return;
            lock (stateSync)
            {
                if (drag != null && args.Key == ToKeyCode(drag.CommandButton))
                    drag.ReleaseObserved = true;
            }
        }

        private int EngineRunHook(bool mpFrameSkip)
        {
            if (!initialized || failed)
                return engineRunOriginal(mpFrameSkip);

            ActiveDrag state;
            try
            {
                lock (stateSync)
                    state = drag;
                if (state == null)
                    return engineRunOriginal(mpFrameSkip);

                UpdateGesture(state);
                EditorDirector director = EditorDirector.instance;
                bool nativeRelease = director != null &&
                    HasVanillaRelease(state, director);
                if (!nativeRelease || !state.ReleaseObserved)
                    return RunOriginalWithConflictingReleaseSuppressed(
                        state,
                        director,
                        mpFrameSkip,
                        suppressCommandRelease: nativeRelease);

                lock (stateSync)
                {
                    if (!ReferenceEquals(drag, state))
                        return engineRunOriginal(mpFrameSkip);
                    drag = null;
                }
                FormationPreviewOverlay.Clear();

                if (!ValidateActiveDrag(state) || !TryCreatePacket(state, out FormationOrderPacket packet))
                    return engineRunOriginal(mpFrameSkip);

                if (!TryDispatch(packet, out string rejection))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Formation order fell back to Vanilla: {rejection}.");
                    return engineRunOriginal(mpFrameSkip);
                }

                formationConfig.Value = state.Kind;
                densityConfig.Value = FormationModel.NormalizeDensity(state.Density);
                rearSortingConfig.Value = state.RearSorting;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"FORMATION_ORDER_QUEUED: operation={packet.OperationId}, tribe={packet.TribeId}, " +
                    $"target={packet.TargetX},{packet.TargetY}, kind={(FormationKind)packet.Formation}, " +
                    $"density={packet.Density}, rear={packet.RearSorting}, " +
                    $"direction={packet.DirectionSector}, width={packet.Width}.");
                return RunOriginalWithConflictingReleaseSuppressed(
                    state, director, mpFrameSkip, suppressCommandRelease: true);
            }
            catch (Exception exception)
            {
                FailOpen("engine-run", exception);
                return engineRunOriginal(mpFrameSkip);
            }
        }

        private int RunOriginalWithConflictingReleaseSuppressed(
            ActiveDrag state,
            EditorDirector director,
            bool mpFrameSkip,
            bool suppressCommandRelease)
        {
            if (director == null)
                return engineRunOriginal(mpFrameSkip);
            int oldLeft = (int)leftMouseStateField.GetValue(director);
            bool oldRight = (bool)rightMouseUpField.GetValue(director);
            try
            {
                if (state.CommandButton == 0)
                {
                    if (suppressCommandRelease)
                        leftMouseStateField.SetValue(director, 0);
                    rightMouseUpField.SetValue(director, false);
                }
                else
                {
                    leftMouseStateField.SetValue(director, oldLeft == 3 ? 0 : oldLeft);
                    if (suppressCommandRelease)
                        rightMouseUpField.SetValue(director, false);
                }
                return engineRunOriginal(mpFrameSkip);
            }
            finally
            {
                leftMouseStateField.SetValue(director, oldLeft);
                rightMouseUpField.SetValue(director, oldRight);
            }
        }

        private void StartSelectionHook(
            TroopSelector self,
            UnityEngine.Vector2 start,
            UnityEngine.Vector2 current)
        {
            ActiveDrag state;
            lock (stateSync)
                state = drag;
            if (!failed && state != null)
            {
                if (MainControls.instance != null)
                    MainControls.instance.CurrentAction = 0;
                return;
            }
            startSelectionOriginal(self, start, current);
        }

        private void CameraUpdateHook(CameraControls2D self)
        {
            ActiveDrag state;
            lock (stateSync)
                state = drag;
            if (!failed && state != null)
                self.AllowZoom = false;
            cameraUpdateOriginal(self);
        }

        private void UpdateGesture(ActiveDrag state)
        {
            if (!ValidateActiveDrag(state))
            {
                AbortDrag("state-changed");
                return;
            }

            int frame = Time.frameCount;
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f && lastWheelFrame != frame)
            {
                lastWheelFrame = frame;
                state.Density = FormationModel.ChangeDensity(
                    state.Density, wheel > 0f ? 1 : -1);
            }

            if (TryCaptureTarget(out GroundTarget endpoint))
            {
                state.DragDeltaX = endpoint.NativeX - state.Target.NativeX;
                state.DragDeltaY = endpoint.NativeY - state.Target.NativeY;
            }
            PublishPreview(state, force: wheel != 0f);
        }

        private void TryStartDrag(int commandButton)
        {
            if (!HasValidMap() || FatControler.instance == null ||
                FatControler.instance.overNoesisGUI() || IsShiftHeld())
                return;
            if (!TryCaptureSelection(out SelectionIdentity[] selection, out int tribeId) ||
                !TryCaptureTarget(out GroundTarget target))
                return;

            var state = new ActiveDrag(
                commandButton,
                tribeId,
                target,
                selection,
                FormationModel.NormalizeKind((int)formationConfig.Value),
                FormationModel.NormalizeDensity(densityConfig.Value),
                rearSortingConfig.Value);
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector = direction;
            state.Width = width;
            lock (stateSync)
                drag = state;
            PublishPreview(state, force: true);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"FORMATION_DRAG_START: tribe={tribeId}, units={selection.Length}, " +
                $"target={target.NativeX},{target.NativeY}, kind={state.Kind}, " +
                $"density={state.Density}, rear={state.RearSorting}.");
        }

        private bool TryCreatePacket(ActiveDrag state, out FormationOrderPacket packet)
        {
            packet = null;
            ResolveDirectionAndWidth(state, out int direction, out int width);
            if (state.TribeId <= 0 || state.TribeId >= MaximumTribeCount ||
                width <= 0 || width > ushort.MaxValue)
                return false;
            packet = new FormationOrderPacket
            {
                ProtocolVersion = ProtocolVersion,
                OperationId = unchecked(++nextOperationId),
                TribeId = state.TribeId,
                TargetX = state.Target.NativeX,
                TargetY = state.Target.NativeY,
                IsNewOrder = 1,
                MoveType = (int)TribeMoveType.DefaultInSync,
                Formation = (byte)state.Kind,
                Density = (byte)FormationModel.NormalizeDensity(state.Density),
                RearSorting = state.RearSorting,
                DirectionSector = (byte)direction,
                Width = (ushort)width
            };
            return true;
        }

        private bool TryDispatch(FormationOrderPacket packet, out string rejection)
        {
            rejection = null;
            if (!GameNetworkAPI.IsMultiplayerGame())
            {
                ApplyPacket(packet, "singleplayer");
                return true;
            }

            if (packetHook == null)
            {
                rejection = "packet hook is unavailable";
                return false;
            }
            if (SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA == 0)
            {
                rejection = "Chore manager is unavailable";
                return false;
            }
            try
            {
                byte[] body = GameNetworkAPI.Serialize(packet);
                if (body == null || body.Length + sizeof(short) > 1200)
                {
                    rejection = "serialized payload exceeds the Chore limit";
                    return false;
                }
                byte[] blob = new byte[body.Length + sizeof(short)];
                BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
                Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
                object result = sendChorePayloadMethod.Invoke(null, new object[] { blob });
                if (result is bool sent && sent)
                    return true;
                rejection = "Chore transport refused the packet";
                return false;
            }
            catch (Exception exception)
            {
                rejection = exception.Message;
                return false;
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<FormationOrderPacket> args)
        {
            if (args?.Phase != EventHookPhase.Post)
                return;
            try
            {
                ApplyPacket(args.Packet, "multiplayer-chore");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Formation Chore execution failed: {exception}");
            }
        }

        private void ApplyPacket(FormationOrderPacket packet, string source)
        {
            if (!ValidatePacket(packet, out string rejection))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Rejected Formation Chore: {rejection}.");
                return;
            }

            FormationKind kind = FormationModel.NormalizeKind(packet.Formation);
            int density = FormationModel.NormalizeDensity(packet.Density);
            FormationUnit[] units = CaptureOrderedGroupUnits(packet.TribeId);
            if (units.Length == 0)
                throw new InvalidOperationException("The commanded tribe has no active units.");

            ActiveFormationCommand command;
            if (kind == FormationKind.Vanilla)
            {
                command = ActiveFormationCommand.CreateVanilla(
                    packet.TribeId, packet.TargetX, packet.TargetY, density);
            }
            else
            {
                NativeDestination[] destinations = BuildManagedDestinations(
                    packet.TargetX,
                    packet.TargetY,
                    kind,
                    density,
                    packet.DirectionSector,
                    packet.Width,
                    packet.RearSorting,
                    units);
                command = ActiveFormationCommand.CreateManaged(
                    packet.TribeId,
                    packet.TargetX,
                    packet.TargetY,
                    density,
                    destinations);
            }

            lock (stateSync)
            {
                if (activeCommand != null)
                    throw new InvalidOperationException("A nested formation command was rejected.");
                activeCommand = command;
            }
            try
            {
                bool issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                    packet.TribeId,
                    packet.TargetX,
                    packet.TargetY,
                    isPatrolPath: false,
                    bIsNewOrder: packet.IsNewOrder,
                    tribeMoveType: (TribeMoveType)packet.MoveType);
                if (!issued)
                    throw new InvalidOperationException("Vanilla rejected IssueMoveHereCommand.");
            }
            finally
            {
                lock (stateSync)
                    activeCommand = null;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"FORMATION_ORDER_APPLIED: source={source}, operation={packet.OperationId}, " +
                $"tribe={packet.TribeId}, units={units.Length}, kind={kind}, density={density}, " +
                $"rear={packet.RearSorting}, direction={packet.DirectionSector}, width={packet.Width}.");
        }

        private void ChooseStandardFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            ActiveFormationCommand command;
            lock (stateSync)
                command = activeCommand;
            if (!Matches(command, manager, x, y))
            {
                standardSelectorHandle.Original(manager, spacing, x, y);
                return;
            }
            if (!command.Managed)
            {
                standardSelectorHandle.Original(manager, command.Density, x, y);
                return;
            }
            if (!TryTakeDestination(command, out NativeDestination destination))
            {
                standardSelectorHandle.Original(manager, command.Density, x, y);
                return;
            }
            WriteFormationOutput(destination);
        }

        private int ChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            ActiveFormationCommand command;
            lock (stateSync)
                command = activeCommand;
            if (!Matches(command, manager, x, y))
                return assassinSelectorHandle.Original(manager, spacing, x, y);
            if (!command.Managed)
                return assassinSelectorHandle.Original(manager, command.Density, x, y);
            if (!TryTakeDestination(command, out NativeDestination destination))
                return assassinSelectorHandle.Original(manager, command.Density, x, y);
            WriteFormationOutput(destination);
            return destination.TileId;
        }

        private bool Matches(
            ActiveFormationCommand command,
            IntPtr manager,
            int targetX,
            int targetY) =>
            command != null && manager == nativePathManager &&
            command.TargetX == targetX && command.TargetY == targetY;

        private bool TryTakeDestination(
            ActiveFormationCommand command,
            out NativeDestination destination)
        {
            lock (stateSync)
            {
                if (!ReferenceEquals(activeCommand, command) ||
                    command.Cursor < 0 || command.Cursor >= command.Destinations.Length)
                {
                    destination = default;
                    return false;
                }
                destination = command.Destinations[command.Cursor++];
                return true;
            }
        }

        private void WriteFormationOutput(NativeDestination destination)
        {
            int* output = (int*)((byte*)nativeTribeManager.ToPointer() + 0x0C);
            output[0] = destination.X;
            output[1] = destination.Y;
            output[2] = 0;
        }

        private NativeDestination[] BuildManagedDestinations(
            int anchorX,
            int anchorY,
            FormationKind kind,
            int density,
            int direction,
            int width,
            bool rearSorting,
            FormationUnit[] units)
        {
            List<FormationPoint> slots = FormationModel.BuildRelativeSlots(
                kind, units.Length, Math.Max(1, width), density, direction);
            bool assassinOnly = IsAssassinOnly(units);
            List<NativeDestination> candidates = CaptureReachableCandidates(
                anchorX, anchorY, Math.Min(MaximumPreviewCandidates,
                    Math.Max(256, units.Length * 16)), assassinOnly);
            if (candidates.Count == 0)
                throw new InvalidOperationException("No reachable formation destination exists.");

            NativeDestination[] snapped = SnapSlots(anchorX, anchorY, slots, candidates);
            int[] assignment = FormationModel.AssignSlotsByRole(units, slots, rearSorting);
            var result = new NativeDestination[units.Length];
            for (int unitIndex = 0; unitIndex < result.Length; unitIndex++)
            {
                int slotIndex = assignment[unitIndex];
                NativeDestination tile = snapped[slotIndex];
                result[unitIndex] = new NativeDestination(
                    tile.TileId,
                    tile.X,
                    tile.Y,
                    units[unitIndex].Role);
            }
            return result;
        }

        private static NativeDestination[] SnapSlots(
            int anchorX,
            int anchorY,
            IReadOnlyList<FormationPoint> slots,
            IReadOnlyList<NativeDestination> candidates)
        {
            var result = new NativeDestination[slots.Count];
            var used = new bool[candidates.Count];
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                FormationPoint slot = slots[slotIndex];
                int desiredX = anchorX + slot.X;
                int desiredY = anchorY + slot.Y;
                long bestDistance = long.MaxValue;
                int best = -1;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    if (used[candidateIndex])
                        continue;
                    NativeDestination candidate = candidates[candidateIndex];
                    long dx = candidate.X - desiredX;
                    long dy = candidate.Y - desiredY;
                    long distance = dx * dx + dy * dy;
                    if (distance < bestDistance ||
                        (distance == bestDistance &&
                         (best < 0 || candidate.TileId < candidates[best].TileId)))
                    {
                        bestDistance = distance;
                        best = candidateIndex;
                    }
                }
                if (best < 0)
                    best = slotIndex % candidates.Count;
                used[best] = true;
                NativeDestination selected = candidates[best];
                result[slotIndex] = new NativeDestination(
                    selected.TileId, selected.X, selected.Y, FormationRole.Neutral);
            }
            return result;
        }

        private List<NativeDestination> CaptureReachableCandidates(
            int anchorX,
            int anchorY,
            int requestedCount,
            bool assassinOnly)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager ??
                throw new InvalidOperationException("Native tile manager is unavailable.");
            Span<byte> edges = tileManager.PathEdgeMaskGrid;
            Span<ushort> components = tileManager.PathConnectionGrid;
            Span<int> logic = tileManager.LogicGrid;
            int anchorTile = GameTileManagerAPI.Instance.GetTileId(anchorX, anchorY);
            if ((uint)anchorX >= MapWidth || (uint)anchorY >= MapWidth ||
                movementTargetAvailability == null ||
                movementTargetAvailability[anchorY * MapWidth + anchorX] == 0 ||
                (uint)anchorTile >= (uint)components.Length ||
                (uint)anchorTile >= (uint)edges.Length || components[anchorTile] == 0)
                return new List<NativeDestination>();

            int capacity = Math.Min(components.Length, MapWidth * MapWidth);
            var visited = new bool[capacity];
            var queue = new Queue<NativeDestination>();
            var result = new List<NativeDestination>(requestedCount);
            ushort component = components[anchorTile];
            visited[anchorTile] = true;
            queue.Enqueue(new NativeDestination(
                anchorTile, anchorX, anchorY, FormationRole.Neutral));
            while (queue.Count != 0 && result.Count < requestedCount)
            {
                NativeDestination current = queue.Dequeue();
                bool available = movementTargetAvailability[
                    current.Y * MapWidth + current.X] != 0;
                bool assassinAllowed = !assassinOnly ||
                    ((uint)current.TileId < (uint)logic.Length &&
                     (logic[current.TileId] & 0x10000100) == 0);
                if (available && assassinAllowed)
                    result.Add(current);
                byte mask = edges[current.TileId];
                TryEnqueueCandidate(current.X - 1, current.Y, 0x40, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y, 0x04, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X, current.Y - 1, 0x01, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X - 1, current.Y - 1, 0x80, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y - 1, 0x02, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X, current.Y + 1, 0x10, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X - 1, current.Y + 1, 0x20, mask,
                    component, edges, components, visited, queue);
                TryEnqueueCandidate(current.X + 1, current.Y + 1, 0x08, mask,
                    component, edges, components, visited, queue);
            }
            return result;
        }

        private static bool IsAssassinOnly(IReadOnlyList<FormationUnit> units)
        {
            if (units == null || units.Count == 0)
                return false;
            for (int index = 0; index < units.Count; index++)
            {
                if (units[index].UnitType != (int)eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                    return false;
            }
            return true;
        }

        private static void TryEnqueueCandidate(
            int x,
            int y,
            byte requiredMask,
            byte sourceMask,
            ushort component,
            Span<byte> edges,
            Span<ushort> components,
            bool[] visited,
            Queue<NativeDestination> queue)
        {
            if ((sourceMask & requiredMask) == 0 ||
                (uint)x >= MapWidth || (uint)y >= MapWidth)
                return;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)tileId >= (uint)visited.Length || visited[tileId] ||
                (uint)tileId >= (uint)edges.Length ||
                components[tileId] != component)
                return;
            visited[tileId] = true;
            queue.Enqueue(new NativeDestination(
                tileId, x, y, FormationRole.Neutral));
        }

        private FormationUnit[] CaptureOrderedGroupUnits(int tribeId)
        {
            if (tribeId <= 0 || tribeId >= MaximumTribeCount)
                return Array.Empty<FormationUnit>();
            byte* tribe = (byte*)nativeTribeManager.ToPointer() + tribeId * TribeRecordSize;
            int count = *(ushort*)(tribe + TribeUnitCountOffset);
            if (count <= 0 || count > MaximumUnitCount)
                return Array.Empty<FormationUnit>();

            var result = new List<FormationUnit>(count);
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int unitId = getGroupUnitId(nativeTribeManager, tribeId, ordinal);
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != tribeId ||
                    *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                    continue;
                int unitType = (int)unit->r_UnitChimp;
                result.Add(new FormationUnit(
                    unitId, unitType, Classify((eChimps)unitType)));
            }
            return result.ToArray();
        }

        private static FormationRole Classify(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_LORD:
                case eChimps.CHIMP_TYPE_WAR_DOG:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                    return FormationRole.Front;

                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                    return FormationRole.Protected;

                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_ARCHER_debug:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_TUNNELER:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return FormationRole.Rear;

                default:
                    return FormationRole.Neutral;
            }
        }

        private void PublishPreview(ActiveDrag state, bool force)
        {
            ResolveDirectionAndWidth(state, out int direction, out int width);
            if (!force && state.LastPreviewDeltaX == state.DragDeltaX &&
                state.LastPreviewDeltaY == state.DragDeltaY &&
                state.LastPreviewKind == state.Kind &&
                state.LastPreviewDensity == state.Density &&
                state.LastPreviewRear == state.RearSorting)
                return;

            state.DirectionSector = direction;
            state.Width = width;
            state.LastPreviewDeltaX = state.DragDeltaX;
            state.LastPreviewDeltaY = state.DragDeltaY;
            state.LastPreviewKind = state.Kind;
            state.LastPreviewDensity = state.Density;
            state.LastPreviewRear = state.RearSorting;

            FormationUnit[] units = new FormationUnit[state.Selection.Length];
            for (int index = 0; index < units.Length; index++)
            {
                SelectionIdentity selected = state.Selection[index];
                units[index] = new FormationUnit(
                    selected.UnitId,
                    selected.UnitType,
                    Classify((eChimps)selected.UnitType));
            }

            try
            {
                NativeDestination[] destinations;
                if (state.Kind == FormationKind.Vanilla)
                {
                    List<NativeDestination> reachable = CaptureReachableCandidates(
                        state.Target.NativeX,
                        state.Target.NativeY,
                        Math.Max(units.Length * state.Density * 2, units.Length),
                        IsAssassinOnly(units));
                    if (reachable.Count == 0)
                        throw new InvalidOperationException(
                            "No reachable Vanilla preview destination exists.");
                    destinations = new NativeDestination[units.Length];
                    int cursor = 0;
                    for (int index = 0; index < reachable.Count && cursor < destinations.Length; index++)
                    {
                        NativeDestination candidate = reachable[index];
                        int distance = Math.Abs(candidate.X - state.Target.NativeX) +
                            Math.Abs(candidate.Y - state.Target.NativeY);
                        if (distance % state.Density != 0)
                            continue;
                        destinations[cursor++] = new NativeDestination(
                            candidate.TileId, candidate.X, candidate.Y, FormationRole.Neutral);
                    }
                    while (cursor < destinations.Length)
                    {
                        destinations[cursor] = destinations[Math.Max(0, cursor - 1)];
                        cursor++;
                    }
                }
                else
                {
                    destinations = BuildManagedDestinations(
                        state.Target.NativeX,
                        state.Target.NativeY,
                        state.Kind,
                        state.Density,
                        direction,
                        width,
                        state.RearSorting,
                        units);
                }

                var points = new FormationPreviewPoint[destinations.Length];
                for (int index = 0; index < destinations.Length; index++)
                {
                    FormationRole role = state.RearSorting
                        ? destinations[index].Role
                        : FormationRole.Neutral;
                    points[index] = new FormationPreviewPoint(
                        destinations[index].X, destinations[index].Y, role);
                }
                FormationPreviewOverlay.Publish(
                    points, state.Kind, state.Density, state.RearSorting, width);
            }
            catch (Exception exception)
            {
                FormationPreviewOverlay.Clear();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Formation preview unavailable for this target: {exception.Message}");
            }
        }

        private static void ResolveDirectionAndWidth(
            ActiveDrag state,
            out int direction,
            out int width)
        {
            int dragDistance = Math.Max(
                Math.Abs(state.DragDeltaX), Math.Abs(state.DragDeltaY));
            if (dragDistance >= MinimumDragTileDistance)
            {
                direction = FormationModel.QuantizeDirection(
                    state.DragDeltaX, state.DragDeltaY, state.DefaultDirectionSector);
                width = FormationModel.ResolveDraggedWidth(
                    dragDistance, state.Density, state.Selection.Length);
            }
            else
            {
                direction = state.DefaultDirectionSector;
                width = FormationModel.ResolveAutomaticWidth(
                    state.Kind, state.Selection.Length);
            }
        }

        private static bool TryCaptureSelection(
            out SelectionIdentity[] identities,
            out int tribeId)
        {
            SelectedUnitInfo[] selected =
                GamePlayerManagerAPI.Instance.GetSelectedChimps();
            if (selected == null || selected.Length < 2)
            {
                identities = Array.Empty<SelectionIdentity>();
                tribeId = 0;
                return false;
            }

            identities = new SelectionIdentity[selected.Length];
            tribeId = -1;
            long sumX = 0;
            long sumY = 0;
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
                identities[index] = new SelectionIdentity(
                    unitId,
                    unit->r_GlobalId,
                    (int)unit->r_UnitChimp,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY);
                sumX += unit->r_CurrentTilePositionX;
                sumY += unit->r_CurrentTilePositionY;
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
            if (!TryCaptureSelection(out SelectionIdentity[] current, out _) ||
                current.Length != expected.Length)
                return false;
            for (int index = 0; index < current.Length; index++)
            {
                if (current[index].UnitId != expected[index].UnitId ||
                    current[index].GlobalId != expected[index].GlobalId ||
                    current[index].UnitType != expected[index].UnitType)
                    return false;
            }
            return true;
        }

        private static bool TryCaptureTarget(out GroundTarget target)
        {
            target = default;
            if (GameMap.instance == null)
                return false;
            UnityEngine.Vector3 mouseMap = UnityEngine.Vector3.zero;
            Vector3Int tileMap = Vector3Int.zero;
            int clickDepth = 0;
            GameMap.instance.CalcMapTileFromMousePos(
                Input.mousePosition, ref mouseMap, ref tileMap, ref clickDepth);
            GameMapTile mapTile = GameMap.instance.getMapTile(tileMap.x, tileMap.y);
            if (mapTile == null || (uint)mapTile.gameMapX >= MapWidth ||
                (uint)mapTile.gameMapY >= MapWidth)
                return false;
            target = new GroundTarget(mapTile.gameMapX, mapTile.gameMapY);
            return true;
        }

        private bool ValidateActiveDrag(ActiveDrag state) =>
            state != null && HasValidMap() && !IsShiftHeld() &&
            GetCommandMouseButton() == state.CommandButton &&
            SelectionMatches(state.Selection);

        private static bool HasValidMap() =>
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            GameMap.instance != null && EditorDirector.instance != null &&
            MainControls.instance != null;

        private static bool IsShiftHeld()
        {
            KeyManager manager = KeyManager.instance;
            return manager != null &&
                (manager.IsKeyHeldDown(KeyCode.LeftShift, ignoreModifiers: true) ||
                 manager.IsKeyHeldDown(KeyCode.RightShift, ignoreModifiers: true));
        }

        private static int GetCommandMouseButton() =>
            ConfigSettings.Settings_SH1RTSControls ? 0 : 1;

        private static KeyCode ToKeyCode(int mouseButton) =>
            mouseButton == 0 ? KeyCode.Mouse0 : KeyCode.Mouse1;

        private bool HasVanillaRelease(ActiveDrag state, EditorDirector director)
        {
            int leftState = (int)leftMouseStateField.GetValue(director);
            bool rightUp = (bool)rightMouseUpField.GetValue(director);
            return state.CommandButton == 0 ? leftState == 3 : rightUp;
        }

        private static bool ValidatePacket(FormationOrderPacket packet, out string rejection)
        {
            if (packet == null)
                rejection = "packet is null";
            else if (packet.ProtocolVersion != ProtocolVersion)
                rejection = "protocol mismatch";
            else if (packet.OperationId <= 0)
                rejection = "invalid operation ID";
            else if (packet.TribeId <= 0 || packet.TribeId >= MaximumTribeCount)
                rejection = "invalid tribe ID";
            else if ((uint)packet.TargetX >= MapWidth || (uint)packet.TargetY >= MapWidth)
                rejection = "invalid target";
            else if (packet.Formation > (byte)FormationKind.Wedge)
                rejection = "invalid formation";
            else if (packet.Density < 1 || packet.Density > 4)
                rejection = "invalid density";
            else if (packet.DirectionSector > 7)
                rejection = "invalid direction";
            else if (packet.Width == 0 || packet.Width > MaximumUnitCount)
                rejection = "invalid width";
            else if (packet.IsNewOrder != 0 && packet.IsNewOrder != 1)
                rejection = "invalid new-order flag";
            else if (packet.MoveType != (int)TribeMoveType.DefaultInSync &&
                     packet.MoveType != (int)TribeMoveType.Fast)
                rejection = "invalid move type";
            else
            {
                rejection = null;
                return true;
            }
            return false;
        }

        private void AbortDrag(string reason)
        {
            ActiveDrag state;
            lock (stateSync)
            {
                state = drag;
                drag = null;
            }
            if (state == null)
                return;
            FormationPreviewOverlay.Clear();
            Shared.DebugLogHelper.LogDebug(log, $"FORMATION_DRAG_ABORTED: {reason}.");
        }

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
            lock (stateSync)
            {
                drag = null;
                activeCommand = null;
            }
            FormationPreviewOverlay.Clear();
            Shared.DebugLogHelper.LogError(
                log,
                $"FORMATION_TEST_DISABLED: contract={contract}; Vanilla remains active; {exception}");
        }

        private static FieldInfo RequireEditorField(string name, Type type)
        {
            FieldInfo field = typeof(EditorDirector).GetField(name, InstanceFields);
            if (field == null || field.FieldType != type)
                throw new MissingFieldException(typeof(EditorDirector).FullName, name);
            return field;
        }

        private static void ValidateNativeContracts(ReadOnlySpan<byte> memory)
        {
            ValidateBytes(memory, StandardSelectorRva, StandardSelectorPrefix,
                "standard formation selector");
            ValidateBytes(memory, AssassinSelectorRva, AssassinSelectorPrefix,
                "Assassin ground formation selector");
            ValidateBytes(
                memory,
                GetGroupUnitIdRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x63, 0xC2,
                    0x45, 0x33, 0xC9, 0x48, 0x69, 0xD0, 0x88, 0x06,
                    0x00, 0x00
                },
                "group unit iterator");
        }

        private static void ValidateBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || expected == null || rva + expected.Length > memory.Length)
                throw new InvalidOperationException($"{name} lies outside the native image.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (memory[rva + index] != expected[index])
                    throw new InvalidOperationException(
                        $"{name} byte mismatch at RVA 0x{rva + index:X}: " +
                        $"expected 0x{expected[index]:X2}, got 0x{memory[rva + index]:X2}.");
            }
        }

        private sealed class ActiveDrag
        {
            internal ActiveDrag(
                int commandButton,
                int tribeId,
                GroundTarget target,
                SelectionIdentity[] selection,
                FormationKind kind,
                int density,
                bool rearSorting)
            {
                CommandButton = commandButton;
                TribeId = tribeId;
                Target = target;
                Selection = selection;
                Kind = kind;
                Density = density;
                RearSorting = rearSorting;
                LastPreviewDeltaX = int.MinValue;
                LastPreviewDeltaY = int.MinValue;
                LastPreviewKind = (FormationKind)byte.MaxValue;
                LastPreviewDensity = -1;
                int sumX = 0;
                int sumY = 0;
                for (int index = 0; index < selection.Length; index++)
                {
                    sumX += selection[index].X;
                    sumY += selection[index].Y;
                }
                int centerX = sumX / selection.Length;
                int centerY = sumY / selection.Length;
                DefaultDirectionSector = FormationModel.QuantizeDirection(
                    target.NativeX - centerX,
                    target.NativeY - centerY,
                    0);
            }

            internal int CommandButton { get; }
            internal int TribeId { get; }
            internal GroundTarget Target { get; }
            internal SelectionIdentity[] Selection { get; }
            internal FormationKind Kind { get; set; }
            internal int Density { get; set; }
            internal bool RearSorting { get; set; }
            internal bool ReleaseObserved { get; set; }
            internal int DragDeltaX { get; set; }
            internal int DragDeltaY { get; set; }
            internal int DefaultDirectionSector { get; }
            internal int DirectionSector { get; set; }
            internal int Width { get; set; }
            internal int LastPreviewDeltaX { get; set; }
            internal int LastPreviewDeltaY { get; set; }
            internal FormationKind LastPreviewKind { get; set; }
            internal int LastPreviewDensity { get; set; }
            internal bool LastPreviewRear { get; set; }
        }

        private sealed class ActiveFormationCommand
        {
            private ActiveFormationCommand(
                int tribeId,
                int targetX,
                int targetY,
                int density,
                bool managed,
                NativeDestination[] destinations)
            {
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
                Density = density;
                Managed = managed;
                Destinations = destinations ?? Array.Empty<NativeDestination>();
            }

            internal int TribeId { get; }
            internal int TargetX { get; }
            internal int TargetY { get; }
            internal int Density { get; }
            internal bool Managed { get; }
            internal NativeDestination[] Destinations { get; }
            internal int Cursor { get; set; }

            internal static ActiveFormationCommand CreateVanilla(
                int tribeId, int targetX, int targetY, int density) =>
                new ActiveFormationCommand(
                    tribeId, targetX, targetY, density, false, null);

            internal static ActiveFormationCommand CreateManaged(
                int tribeId,
                int targetX,
                int targetY,
                int density,
                NativeDestination[] destinations) =>
                new ActiveFormationCommand(
                    tribeId, targetX, targetY, density, true, destinations);
        }

        private readonly struct SelectionIdentity
        {
            internal SelectionIdentity(
                int unitId, uint globalId, int unitType, int x, int y)
            {
                UnitId = unitId;
                GlobalId = globalId;
                UnitType = unitType;
                X = x;
                Y = y;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal int UnitType { get; }
            internal int X { get; }
            internal int Y { get; }
        }

        private readonly struct GroundTarget
        {
            internal GroundTarget(int nativeX, int nativeY)
            {
                NativeX = nativeX;
                NativeY = nativeY;
            }

            internal int NativeX { get; }
            internal int NativeY { get; }
        }

        private readonly struct NativeDestination
        {
            internal NativeDestination(
                int tileId, int x, int y, FormationRole role)
            {
                TileId = tileId;
                X = x;
                Y = y;
                Role = role;
            }

            internal int TileId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal FormationRole Role { get; }
        }
    }
}
