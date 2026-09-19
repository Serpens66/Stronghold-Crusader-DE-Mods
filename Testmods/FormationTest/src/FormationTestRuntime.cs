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
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
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
        private delegate void CameraUpdateDelegate(CameraControls2D self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AssassinFormationSlotDelegate(IntPtr manager, int spacing, int x, int y);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long CommonGroupMoveDelegate(
            IntPtr manager, int tribeId, short x, short y, short patrol, int newOrder);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetGroupUnitIdDelegate(IntPtr tribeManager, int tribeId, int ordinal);

        private const int ProtocolVersion = 3;
        private const int MapWidth = 800;
        private const int MaximumUnitCount = 10000;
        private const int MaximumTribeCount = 4500;
        private const int TribeRecordSize = 0x688;
        private const int TribeUnitCountOffset = 0x5C;
        private const int UnitGroupInactiveStateOffset = 0x29C;
        private const int StandardSelectorRva = 0xE1D30;
        private const int AssassinSelectorRva = 0xE0970;
        private const int CommonGroupMoveRva = 0x118E00;
        private const int UnitMoveTargetRva = 0x196280;
        private const int GetGroupUnitIdRva = 0x119F90;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativeTribeManagerRva = 0x7CC6720;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int ExpectedUnitMoveTargetAuditBytes = 14;
        private const int MaximumPreviewCandidates = 8192;
        private const int MinimumDragTileDistance = 2;

        private static readonly byte[] StandardSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x18,
            0x48, 0x89, 0x74, 0x24, 0x20,
            0x89, 0x54, 0x24, 0x10, 0x57,
            0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x4C, 0x63, 0x1D, 0xE1, 0x49, 0xBE, 0x07,
            0x4C, 0x8B, 0xF1, 0x48, 0x63
        };

        private static readonly byte[] AssassinSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18,
            0x48, 0x89, 0x7C, 0x24, 0x20,
            0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x4C, 0x63, 0x1D, 0xA1, 0x5D, 0xBE, 0x07,
            0x4C, 0x8B, 0xF1,
            0x48, 0x63, 0x81, 0x6C, 0x5F, 0x15, 0x00,
            0x45, 0x8B, 0xF9
        };

        private static readonly byte[] CommonGroupMovePrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18,
            0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0xF2, 0x45, 0x33, 0xED, 0x8B, 0xD6,
            0x45, 0x8B, 0xF1, 0x45, 0x8B, 0xF8, 0x48, 0x8B, 0xE9,
            0x41, 0x8B, 0xDD
        };

        private static readonly byte[] UnitMoveTargetPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x20,
            0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56,
            0x41, 0x57, 0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0xF2, 0x45, 0x33, 0xD2,
            0x48, 0x69, 0xFE, 0x90, 0x04, 0x00, 0x00,
            0x4D, 0x63, 0xF0,
            0x48, 0x8D, 0x15, 0x55, 0x9D, 0xE6, 0xFF,
            0x48, 0x03, 0xF9
        };

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly ManualLogSource log;
        private readonly CrusaderLibraryLoadContext libraryContext;
        private readonly ConfigEntry<FormationKind> formationConfig;
        private readonly ConfigEntry<int> densityConfig;
        private readonly ConfigEntry<RangedPlacementMode> placementModeConfig;
        private readonly FormationMenuViewModel menuViewModel;
        private readonly object stateSync = new object();
        private readonly DetourHandle<FormationSlotDelegate> standardSelectorHandle =
            new DetourHandle<FormationSlotDelegate>();
        private readonly DetourHandle<AssassinFormationSlotDelegate> assassinSelectorHandle =
            new DetourHandle<AssassinFormationSlotDelegate>();
        private readonly DetourHandle<CommonGroupMoveDelegate> commonGroupMoveHandle =
            new DetourHandle<CommonGroupMoveDelegate>();

        private FormationPreviewMarkerRenderer markerRenderer;
        private HookTransaction nativeTransaction;
        private Hook engineRunHook;
        private Hook cameraUpdateHook;
        private EngineRunDelegate engineRunOriginal;
        private CameraUpdateDelegate cameraUpdateOriginal;
        private IDisposable keyDownSubscription;
        private IDisposable keyHeldSubscription;
        private IDisposable keyUpSubscription;
        private IDisposable packetSubscription;
        private IDisposable tribeMoveSubscription;
        private IDisposable unitMoveSubscription;
        private R3PacketEventHook<FormationOrderPacket> packetHook;
        private FieldInfo leftMouseStateField;
        private FieldInfo rightMouseDownField;
        private FieldInfo rightMouseUpField;
        private FieldInfo mouseStateReadField;
        private FieldInfo mouseUpPendingField;
        private IntPtr nativeTribeManager;
        private IntPtr nativePathManager;
        private byte* movementTargetAvailability;
        private GetGroupUnitIdDelegate getGroupUnitId;
        private MethodInfo sendChorePayloadMethod;
        private ActiveDrag drag;
        private PendingFormationCommand pendingCommand;
        private ActiveFormationCommand activeCommand;
        private ActiveFormationCommand commonGroupCommand;
        private UnitAssignmentFrame unitAssignmentFrame;
        private UnitFallbackAttempt unitFallbackAttempt;
        private ReleaseConsumptionWatch releaseConsumptionWatch;
        private int nextOperationId;
        private int lastWheelFrame = -1;
        private int mainThreadId;
        private bool initialized;
        private bool failed;

        internal FormationTestRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext libraryContext,
            ConfigEntry<FormationKind> formationConfig,
            ConfigEntry<int> densityConfig,
            ConfigEntry<RangedPlacementMode> placementModeConfig,
            FormationMenuViewModel menuViewModel)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.libraryContext = libraryContext ??
                throw new ArgumentNullException(nameof(libraryContext));
            this.formationConfig = formationConfig ??
                throw new ArgumentNullException(nameof(formationConfig));
            this.densityConfig = densityConfig ??
                throw new ArgumentNullException(nameof(densityConfig));
            this.placementModeConfig = placementModeConfig ??
                throw new ArgumentNullException(nameof(placementModeConfig));
            this.menuViewModel = menuViewModel ??
                throw new ArgumentNullException(nameof(menuViewModel));
        }

        internal void Initialize()
        {
            if (initialized)
                return;

            HookTransaction pendingNative = null;
            Hook pendingEngineRun = null;
            Hook pendingCameraUpdate = null;
            IDisposable pendingKeyDown = null;
            IDisposable pendingKeyHeld = null;
            IDisposable pendingKeyUp = null;
            IDisposable pendingPacket = null;
            IDisposable pendingTribeMove = null;
            IDisposable pendingUnitMove = null;
            FormationPreviewMarkerRenderer pendingMarkerRenderer = null;
            try
            {
                mainThreadId = Environment.CurrentManagedThreadId;
                ulong libraryBase = unchecked((ulong)libraryContext.ModuleHandle.ToInt64());
                ValidateNativeContracts(libraryContext.Memory);
                byte[] standardSelectorEntry = NativeDetourEntryContract.Capture(
                    libraryBase + StandardSelectorRva);
                byte[] assassinSelectorEntry = NativeDetourEntryContract.Capture(
                    libraryBase + AssassinSelectorRva);
                byte[] commonGroupMoveEntry = NativeDetourEntryContract.Capture(
                    libraryBase + CommonGroupMoveRva);
                pendingMarkerRenderer = new FormationPreviewMarkerRenderer(
                    log,
                    FormationPreviewOverlay.Clear);
                pendingMarkerRenderer.Install(libraryContext);
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
                pendingNative.AddDetour(
                    commonGroupMoveHandle,
                    HookTarget.FromAddress(libraryBase + CommonGroupMoveRva),
                    (CommonGroupMoveDelegate)CommonGroupMoveHook);
                CommitResult nativeResult = pendingNative.Commit();
                NativeDetour<FormationSlotDelegate> standardDetour =
                    standardSelectorHandle.Hook as NativeDetour<FormationSlotDelegate>;
                NativeDetour<AssassinFormationSlotDelegate> assassinDetour =
                    assassinSelectorHandle.Hook as NativeDetour<AssassinFormationSlotDelegate>;
                NativeDetour<CommonGroupMoveDelegate> commonDetour =
                    commonGroupMoveHandle.Hook as NativeDetour<CommonGroupMoveDelegate>;
                if (!nativeResult.IsCompleteSuccess || !standardSelectorHandle.Success ||
                    !assassinSelectorHandle.Success ||
                    !commonGroupMoveHandle.Success ||
                    standardDetour == null || assassinDetour == null ||
                    commonDetour == null)
                {
                    throw new InvalidOperationException(
                        $"Formation selector transaction failed: " +
                        $"result={nativeResult}, standard={standardDetour?.DisplacedByteCount}, " +
                        $"assassin={assassinDetour?.DisplacedByteCount}, " +
                        $"common={commonDetour?.DisplacedByteCount}.");
                }
                string standardContract = ValidateNativeDetourContract(
                    standardDetour,
                    libraryBase + StandardSelectorRva,
                    standardSelectorEntry,
                    StandardSelectorPrefix,
                    "standardSelector");
                string assassinContract = ValidateNativeDetourContract(
                    assassinDetour,
                    libraryBase + AssassinSelectorRva,
                    assassinSelectorEntry,
                    AssassinSelectorPrefix,
                    "assassinSelector");
                string commonContract = ValidateNativeDetourContract(
                    commonDetour,
                    libraryBase + CommonGroupMoveRva,
                    commonGroupMoveEntry,
                    CommonGroupMovePrefix,
                    "commonGroup");

                leftMouseStateField = RequireEditorField("leftMouseStateForEngine", typeof(int));
                rightMouseDownField = RequireEditorField("rightDownForEngine", typeof(bool));
                rightMouseUpField = RequireEditorField("rightUpForEngine", typeof(bool));
                mouseStateReadField = RequireEditorField("stateRead", typeof(bool));
                mouseUpPendingField = RequireEditorField("upPending", typeof(bool));
                MethodInfo engineRun = typeof(EngineInterface).GetMethod(
                    "run", BindingFlags.Static | BindingFlags.Public,
                    null, new[] { typeof(bool) }, null) ??
                    throw new MissingMethodException(typeof(EngineInterface).FullName, "run(bool)");
                MethodInfo cameraUpdate = typeof(CameraControls2D).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic) ??
                    throw new MissingMethodException(typeof(CameraControls2D).FullName, "Update()");

                pendingEngineRun = new Hook(engineRun, (EngineRunDelegate)EngineRunHook);
                engineRunOriginal = pendingEngineRun.GenerateTrampoline<EngineRunDelegate>();
                pendingCameraUpdate = new Hook(cameraUpdate, (CameraUpdateDelegate)CameraUpdateHook);
                cameraUpdateOriginal =
                    pendingCameraUpdate.GenerateTrampoline<CameraUpdateDelegate>();

                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<FormationOrderPacket>();
                pendingPacket = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
                pendingTribeMove = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(OnTribeIssueOrderMoveHere);
                pendingUnitMove = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(OnUnitMoveHere);
                pendingKeyDown = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);
                pendingKeyHeld = InputR3EventHooks.OnKey.Observable.Subscribe(OnKeyHeld);
                pendingKeyUp = InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnKeyUp);

                nativeTransaction = pendingNative;
                pendingNative = null;
                engineRunHook = pendingEngineRun;
                pendingEngineRun = null;
                cameraUpdateHook = pendingCameraUpdate;
                pendingCameraUpdate = null;
                packetSubscription = pendingPacket;
                pendingPacket = null;
                tribeMoveSubscription = pendingTribeMove;
                pendingTribeMove = null;
                unitMoveSubscription = pendingUnitMove;
                pendingUnitMove = null;
                keyDownSubscription = pendingKeyDown;
                pendingKeyDown = null;
                keyHeldSubscription = pendingKeyHeld;
                pendingKeyHeld = null;
                keyUpSubscription = pendingKeyUp;
                pendingKeyUp = null;
                markerRenderer = pendingMarkerRenderer;
                pendingMarkerRenderer.PublishProcessLifetime();
                pendingMarkerRenderer = null;
                initialized = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "FormationTest active: synchronized Chore packet, mouse gesture hooks, " +
                    $"mainThread={mainThreadId}, " +
                    $"{standardContract}, {assassinContract}, {commonContract}, " +
                    $"unitTarget=0x{UnitMoveTargetRva:X}/extender-event, " +
                    $"nativeAuditSpan={ExpectedUnitMoveTargetAuditBytes}.");
            }
            catch
            {
                pendingUnitMove?.Dispose();
                pendingTribeMove?.Dispose();
                pendingKeyUp?.Dispose();
                pendingKeyHeld?.Dispose();
                pendingKeyDown?.Dispose();
                pendingPacket?.Dispose();
                pendingCameraUpdate?.Dispose();
                pendingEngineRun?.Dispose();
                pendingNative?.Dispose();
                pendingMarkerRenderer?.RollbackUnpublished();
                throw;
            }
        }

        private static string ValidateNativeDetourContract<TFunction>(
            NativeDetour<TFunction> detour,
            ulong expectedTarget,
            byte[] entrySnapshot,
            byte[] vanillaPrefix,
            string name)
            where TFunction : Delegate
        {
            if (!detour.IsInstalled || detour.TargetAddress != expectedTarget ||
                detour.TrampolineAddress == IntPtr.Zero || detour.TrampolineSize <= 0 ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero || detour.ChainDepth < 1)
            {
                throw new InvalidOperationException(
                    $"{name} did not publish a complete native detour contract: " +
                    $"installed={detour.IsInstalled}, target=0x{detour.TargetAddress:X}, " +
                    $"expected=0x{expectedTarget:X}, trampoline=0x{detour.TrampolineAddress.ToInt64():X}, " +
                    $"trampolineSize={detour.TrampolineSize}, hookEntry=0x{detour.HookEntryPointAddress.ToInt64():X}, " +
                    $"originalEntry=0x{detour.OriginalEntryPointAddress.ToInt64():X}, chain={detour.ChainDepth}.");
            }

            string scheme = detour.Scheme.ToString();
            if (scheme == "Indirect" && detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException($"{name} selected Indirect without a pointer slot.");
            if (scheme != "Indirect" && detour.PointerSlot != IntPtr.Zero)
                throw new InvalidOperationException(
                    $"{name} selected {scheme} with an unexpected pointer slot.");

            NativeDetourEntryContract.Validate(
                entrySnapshot,
                vanillaPrefix,
                scheme,
                detour.DisplacedByteCount,
                out bool chainedEntry);
            return $"{name}=0x{expectedTarget:X}/scheme{scheme}/" +
                $"span{detour.DisplacedByteCount}/chain{detour.ChainDepth}/" +
                $"entry{(chainedEntry ? "hook" : "vanilla")}";
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (!initialized || failed || args == null)
                return;
            try
            {
                RequireMainThread("input-down");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;

                if (state != null)
                {
                    if (TryGetMouseButton(args.Key, out int pressedButton) &&
                        state.ReleaseGate.ReleaseEventSeen)
                    {
                        AbortDrag("stale-release-before-new-down");
                        if (pressedButton == state.CommandButton)
                            TryStartDrag(state.CommandButton);
                        return;
                    }
                    if (!state.ReleaseGate.CanModify)
                        return;
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

        private void OnKeyHeld(UnityInputEventArgs args)
        {
            if (!initialized || failed || args == null)
                return;
            try
            {
                RequireMainThread("input-held");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;
                if (state == null || !state.ReleaseGate.CanModify ||
                    args.Key != ToKeyCode(state.CommandButton))
                    return;
                UpdateGesture(state);
            }
            catch (Exception exception)
            {
                FailOpen("input-held", exception);
            }
        }

        private void OnKeyUp(UnityInputEventArgs args)
        {
            if (!initialized || failed || args == null)
                return;
            try
            {
                RequireMainThread("input-up");
                if (args.Phase != EventHookPhase.Post)
                    return;
                ActiveDrag state;
                lock (stateSync)
                    state = drag;
                if (state == null || args.Key != ToKeyCode(state.CommandButton))
                    return;
                if (FatControler.instance != null &&
                    FatControler.instance.overNoesisGUI())
                {
                    AbortDrag("release-over-ui");
                    return;
                }

                UpdateGesture(state);
                bool releaseObserved = false;
                lock (stateSync)
                {
                    if (ReferenceEquals(drag, state))
                        releaseObserved = state.ReleaseGate.ObserveInputRelease();
                }
                if (!releaseObserved)
                    return;
                ClearPreview();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"FORMATION_RELEASE_EVENT: button={state.CommandButton}, " +
                    $"thread={Environment.CurrentManagedThreadId}; awaiting native release.");
            }
            catch (Exception exception)
            {
                FailOpen("input-up", exception);
            }
        }

        private int EngineRunHook(bool mpFrameSkip)
        {
            if (!initialized || failed)
                return engineRunOriginal(mpFrameSkip);

            bool originalEntered = false;
            FormationMouseState inputState = default;
            try
            {
                AgeReleaseConsumptionWatch();
                EditorDirector director = EditorDirector.instance;
                if (director == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                inputState = CaptureMouseState(director);

                ActiveDrag state;
                lock (stateSync)
                    state = drag;
                if (state == null)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                bool releaseClaimed;
                lock (stateSync)
                {
                    releaseClaimed = ReferenceEquals(drag, state) &&
                        state.ReleaseGate.TryClaimVanillaRelease(inputState);
                    if (releaseClaimed)
                        drag = null;
                }
                if (!releaseClaimed)
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                ClearPreview();
                LogDebugNoThrow(
                    $"FORMATION_RELEASE_CLAIMED: tribe={state.TribeId}, " +
                    $"button={state.CommandButton}, " +
                    $"releaseEventSeen={state.ReleaseGate.ReleaseEventSeen}, " +
                    $"left={inputState.LeftState}, rightDown={inputState.RightDown}, " +
                    $"rightUp={inputState.RightUp}, stateRead={inputState.StateRead}, " +
                    $"upPending={inputState.UpPending}.");

                // The full release state was captured before dispatch. Once dispatch
                // is accepted, no fallible reflection read may stand between
                // acceptance and permanent consumption of the input edge.
                if (!TryCreatePacket(state, out FormationOrderPacket packet))
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);

                DispatchDisposition disposition = TryDispatch(packet, out string rejection);
                if (disposition != DispatchDisposition.Accepted)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Formation order fell back to Vanilla: {rejection}.");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }

                return RunOriginalAfterReleaseConsumed(
                    state,
                    director,
                    mpFrameSkip,
                    packet,
                    inputState,
                    ref originalEntered);
            }
            catch (Exception exception)
            {
                FailOpen("engine-run", exception);
                if (originalEntered)
                    throw;
                return RunOriginalOnce(mpFrameSkip, ref originalEntered);
            }
        }

        private int RunOriginalAfterReleaseConsumed(
            ActiveDrag state,
            EditorDirector director,
            bool mpFrameSkip,
            FormationOrderPacket packet,
            FormationMouseState originalState,
            ref bool originalEntered)
        {
            if (director == null)
            {
                throw new InvalidOperationException(
                    "EditorDirector disappeared after the formation order was accepted.");
            }

            try
            {
                director.clearMouseStateForEngine();
            }
            finally
            {
                // clearMouseStateForEngine does not clear rightDown/rightUp. Applying
                // the audited terminal state also makes consumption robust if that
                // method changes part of the state before reporting a failure.
                ApplyMouseState(director, FormationReleaseStateModel.Consume());
            }
            lock (stateSync)
            {
                releaseConsumptionWatch = new ReleaseConsumptionWatch(
                    packet.OperationId, state.TribeId, state.CommandButton, 2);
            }
            LogDebugNoThrow(
                $"FORMATION_RELEASE_CONSUMED: operation={packet.OperationId}, " +
                $"tribe={state.TribeId}, button={state.CommandButton}, " +
                $"left={originalState.LeftState}, rightDown={originalState.RightDown}, " +
                $"rightUp={originalState.RightUp}, " +
                $"stateRead={originalState.StateRead}, " +
                $"upPending={originalState.UpPending}.");

            try
            {
                formationConfig.Value = state.Kind;
                densityConfig.Value = FormationModel.NormalizeDensity(state.Density);
                placementModeConfig.Value = state.PlacementMode;
                LogDebugNoThrow(
                    $"FORMATION_ORDER_QUEUED: operation={packet.OperationId}, " +
                    $"tribe={packet.TribeId}, target={packet.TargetX},{packet.TargetY}, " +
                    $"kind={(FormationKind)packet.Formation}, density={packet.Density}, " +
                    $"placement={(RangedPlacementMode)packet.PlacementMode}, direction={packet.DirectionSector}, " +
                    $"width={packet.Width}, rows={FormationModel.ResolveActualRows((FormationKind)packet.Formation, packet.UnitCount, packet.Width)}, " +
                    $"units={packet.UnitCount}, " +
                    $"plan=0x{packet.PlanHash:X16}.");
            }
            catch (Exception exception)
            {
                LogWarningNoThrow(
                    $"FORMATION_SELECTION_PERSIST_FAILED: operation={packet.OperationId}, " +
                    $"error={exception.Message}.");
            }

            // The accepted packet is now the sole authority for this gesture. Do not
            // restore any release state even if the original run reports an error:
            // restoring it would enqueue a second Vanilla move on the next tick.
            return RunOriginalOnce(mpFrameSkip, ref originalEntered);
        }

        private int RunOriginalOnce(bool mpFrameSkip, ref bool originalEntered)
        {
            if (originalEntered)
                throw new InvalidOperationException(
                    "EngineInterface.run original was entered more than once for one hook invocation.");
            originalEntered = true;
            return engineRunOriginal(mpFrameSkip);
        }

        private void CameraUpdateHook(CameraControls2D self)
        {
            ActiveDrag state;
            lock (stateSync)
                state = drag;
            if (!failed && state != null)
                self.AllowZoom = false;
            cameraUpdateOriginal(self);
            menuViewModel.RefreshHostState();
            FormationPreviewOverlay.Refresh();
        }

        private void UpdateGesture(ActiveDrag state)
        {
            bool abort;
            int changedDensity = 0;
            lock (stateSync)
            {
                if (!ReferenceEquals(drag, state) || !state.ReleaseGate.CanModify)
                    return;
                abort = !ValidateActiveDrag(state);
                if (!abort)
                {
                    int frame = Time.frameCount;
                    float wheel = Input.mouseScrollDelta.y;
                    if (wheel != 0f && lastWheelFrame != frame)
                    {
                        lastWheelFrame = frame;
                        int newDensity = FormationModel.ChangeDensity(
                            state.Density, wheel > 0f ? 1 : -1);
                        if (newDensity != state.Density)
                        {
                            state.Density = newDensity;
                            changedDensity = newDensity;
                        }
                    }

                    if (TryCaptureTarget(out GroundTarget endpoint))
                    {
                        state.DragDeltaX = endpoint.NativeX - state.Target.NativeX;
                        state.DragDeltaY = endpoint.NativeY - state.Target.NativeY;
                    }
                    PublishPreview(state, force: false);
                }
            }
            if (abort)
                AbortDrag("state-changed");
            else if (changedDensity != 0)
                LogDebugNoThrow(
                    $"FORMATION_DENSITY_CHANGED: density={changedDensity}, " +
                    $"thread={Environment.CurrentManagedThreadId}.");
        }

        private void TryStartDrag(int commandButton)
        {
            if (!HasValidMap() || markerRenderer == null ||
                !markerRenderer.ReplacementAvailable || FatControler.instance == null ||
                FatControler.instance.overNoesisGUI() || IsShiftHeld())
                return;

            menuViewModel.CloseMenu();
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
                FormationModel.NormalizePlacementMode((int)placementModeConfig.Value));
            ResolveDirectionAndWidth(state, out int direction, out int width);
            state.DirectionSector = direction;
            state.Width = width;
            lock (stateSync)
            {
                releaseConsumptionWatch = null;
                drag = state;
                PublishPreview(state, force: true);
            }
            Shared.DebugLogHelper.LogDebug(
                log,
                $"FORMATION_DRAG_START: tribe={tribeId}, units={selection.Length}, " +
                $"target={target.NativeX},{target.NativeY}, kind={state.Kind}, " +
                $"density={state.Density}, placement={state.PlacementMode}.");
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
                PlacementMode = (byte)state.PlacementMode,
                DirectionSector = (byte)direction,
                Width = (ushort)width,
                UnitCount = checked((ushort)state.Selection.Length),
                PlanHash = state.Kind == FormationKind.Vanilla
                    ? 0UL
                    : state.PreviewPlanHash
            };
            return state.Kind == FormationKind.Vanilla || state.HasPreviewPlan;
        }

        private DispatchDisposition TryDispatch(
            FormationOrderPacket packet,
            out string rejection)
        {
            rejection = null;
            if (!GameNetworkAPI.IsMultiplayerGame())
            {
                if (ApplyPacket(packet, "singleplayer"))
                    return DispatchDisposition.Accepted;
                rejection = "Vanilla rejected the synchronized move command";
                return DispatchDisposition.Rejected;
            }

            if (packetHook == null)
            {
                rejection = "packet hook is unavailable";
                return DispatchDisposition.Rejected;
            }
            if (SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA == 0)
            {
                rejection = "Chore manager is unavailable";
                return DispatchDisposition.Rejected;
            }
            try
            {
                byte[] body = GameNetworkAPI.Serialize(packet);
                if (body == null || body.Length + sizeof(short) > 1200)
                {
                    rejection = "serialized payload exceeds the Chore limit";
                    return DispatchDisposition.Rejected;
                }
                byte[] blob = new byte[body.Length + sizeof(short)];
                BitConverter.GetBytes(packetHook.GetPacketId()).CopyTo(blob, 0);
                Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
                object result = sendChorePayloadMethod.Invoke(null, new object[] { blob });
                if (result is bool sent && sent)
                    return DispatchDisposition.Accepted;
                rejection = "Chore transport refused the packet";
                return DispatchDisposition.Rejected;
            }
            catch (Exception exception)
            {
                rejection = exception.Message;
                return DispatchDisposition.Rejected;
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<FormationOrderPacket> args)
        {
            if (args?.Phase != EventHookPhase.Post)
                return;
            try
            {
                if (!ApplyPacket(args.Packet, "multiplayer-chore"))
                {
                    LogErrorNoThrow(
                        "Formation Chore was rejected before Vanilla could accept it.");
                }
            }
            catch (Exception exception)
            {
                LogErrorNoThrow($"Formation Chore execution failed: {exception}");
            }
        }

        private bool ApplyPacket(FormationOrderPacket packet, string source)
        {
            if (!ValidatePacket(packet, out string rejection))
            {
                LogErrorNoThrow($"Rejected Formation Chore: {rejection}.");
                return TryIssuePacketVanillaFallback(packet, source, rejection);
            }

            var command = new PendingFormationCommand(packet, source);

            lock (stateSync)
            {
                if (pendingCommand != null || activeCommand != null)
                    throw new InvalidOperationException("A nested formation command was rejected.");
                pendingCommand = command;
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
                {
                    LogWarningNoThrow(
                        $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                        $"operation={packet.OperationId}, reason=move-command-rejected.");
                    return false;
                }
                return true;
            }
            finally
            {
                lock (stateSync)
                {
                    if (ReferenceEquals(pendingCommand, command))
                        pendingCommand = null;
                    if (activeCommand != null && activeCommand.Pending == command)
                    {
                        ClearUnitAssignmentFrames(activeCommand);
                        activeCommand = null;
                        commonGroupCommand = null;
                        LogWarningNoThrow(
                            $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                            $"operation={packet.OperationId}, reason=missing-post-event.");
                    }
                }
            }
        }

        private bool TryIssuePacketVanillaFallback(
            FormationOrderPacket packet,
            string source,
            string reason)
        {
            if (packet == null || packet.TribeId <= 0 ||
                packet.TribeId >= MaximumTribeCount ||
                (uint)packet.TargetX >= MapWidth || (uint)packet.TargetY >= MapWidth ||
                (packet.IsNewOrder != 0 && packet.IsNewOrder != 1) ||
                (packet.MoveType != (int)TribeMoveType.DefaultInSync &&
                 packet.MoveType != (int)TribeMoveType.Fast))
                return false;
            bool issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                packet.TribeId,
                packet.TargetX,
                packet.TargetY,
                isPatrolPath: false,
                bIsNewOrder: packet.IsNewOrder,
                tribeMoveType: (TribeMoveType)packet.MoveType);
            LogWarningNoThrow(
                $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={source}, " +
                $"operation={packet.OperationId}, reason={reason}, issued={issued}.");
            return issued;
        }

        private void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!initialized || failed || args == null)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                PendingFormationCommand pending;
                lock (stateSync)
                {
                    pending = pendingCommand;
                }
                DetectUnexpectedSecondOrder(args, pending);
                if (pending == null || !pending.Matches(args))
                    return;

                try
                {
                    FormationOrderPacket packet = pending.Packet;
                    FormationKind kind = FormationModel.NormalizeKind(packet.Formation);
                    int density = FormationModel.NormalizeDensity(packet.Density);
                    FormationUnit[] units = CaptureOrderedGroupUnits(packet.TribeId);
                    if (units.Length == 0)
                        throw new InvalidOperationException(
                            "The commanded tribe has no active units at dispatch.");
                    if (units.Length != packet.UnitCount)
                    {
                        throw new InvalidOperationException(
                            $"Formation unit count changed: packet={packet.UnitCount}, " +
                            $"current={units.Length}.");
                    }

                    NativeDestination[] destinations = Array.Empty<NativeDestination>();
                    if (kind != FormationKind.Vanilla)
                    {
                        destinations = BuildManagedDestinations(
                            packet.TargetX,
                            packet.TargetY,
                            kind,
                            density,
                            packet.DirectionSector,
                            packet.Width,
                            FormationModel.NormalizePlacementMode(packet.PlacementMode),
                            units,
                            out _);
                        ulong actualPlanHash = ComputePlanHash(
                            units, destinations,
                            FormationModel.NormalizePlacementMode(packet.PlacementMode));
                        if (actualPlanHash != packet.PlanHash)
                        {
                            throw new InvalidOperationException(
                                $"Formation plan hash mismatch: packet=0x{packet.PlanHash:X16}, " +
                                $"current=0x{actualPlanHash:X16}.");
                        }
                    }

                    ActiveFormationCommand active = new ActiveFormationCommand(
                        pending, kind, density, units, destinations);
                    lock (stateSync)
                    {
                        if (!ReferenceEquals(pendingCommand, pending) || activeCommand != null)
                            return;
                        pendingCommand = null;
                        activeCommand = active;
                    }
                    LogDebugNoThrow(
                        $"FORMATION_ORDER_SCOPE_PRE: source={pending.Source}, " +
                        $"operation={packet.OperationId}, tribe={packet.TribeId}, " +
                        $"expected={units.Length}, kind={kind}, " +
                        $"thread={Environment.CurrentManagedThreadId}.");
                }
                catch (Exception exception)
                {
                    lock (stateSync)
                    {
                        if (ReferenceEquals(pendingCommand, pending))
                            pendingCommand = null;
                        ClearUnitAssignmentFrames(null);
                        activeCommand = null;
                        commonGroupCommand = null;
                    }
                    LogWarningNoThrow(
                        $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: source={pending.Source}, " +
                        $"operation={pending.Packet.OperationId}, reason=preparation-failed, " +
                        $"error={exception.Message}.");
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;

            ActiveFormationCommand completed = null;
            try
            {
                lock (stateSync)
                {
                    if (activeCommand != null && activeCommand.Matches(args))
                        completed = activeCommand;
                }
            }
            finally
            {
                lock (stateSync)
                {
                    if (ReferenceEquals(activeCommand, completed))
                    {
                        ClearUnitAssignmentFrames(completed);
                        activeCommand = null;
                    }
                    if (ReferenceEquals(commonGroupCommand, completed))
                        commonGroupCommand = null;
                }
            }

            if (completed == null)
                return;
            if (!completed.Managed)
            {
                if (completed.AssignedCount > 0)
                {
                    LogDebugNoThrow(
                        $"FORMATION_ORDER_APPLIED: source={completed.Pending.Source}, " +
                        $"operation={completed.Pending.Packet.OperationId}, " +
                        $"tribe={completed.TribeId}, path={completed.AssignmentPath}, " +
                        $"assigned={completed.AssignedCount}, expected={completed.ExpectedCount}.");
                }
                else
                {
                    LogWarningNoThrow(
                        $"FORMATION_ORDER_FELL_BACK_TO_VANILLA: " +
                        $"source={completed.Pending.Source}, " +
                        $"operation={completed.Pending.Packet.OperationId}, " +
                        $"tribe={completed.TribeId}, reason=no-native-assignments.");
                }
                return;
            }

            int successfulFormationTargets = completed.SuccessfulTerminalCount;
            UnitFallbackSummary fallbackSummary = RunUnitFallbacks(completed);
            string common =
                $"source={completed.Pending.Source}, " +
                $"operation={completed.Pending.Packet.OperationId}, " +
                $"tribe={completed.TribeId}, path={completed.AssignmentPath}, " +
                $"selectorAssigned={completed.AssignedCount}, " +
                $"terminalAttempts={completed.TerminalAttemptCount}, " +
                $"formationSucceeded={successfulFormationTargets}, " +
                $"fallbackSucceeded={fallbackSummary.Succeeded}, " +
                $"fallbackFailed={fallbackSummary.Failed}, " +
                $"expected={completed.ExpectedCount}";
            if (successfulFormationTargets == completed.ExpectedCount)
                LogDebugNoThrow($"FORMATION_ORDER_APPLIED: {common}.");
            else if (successfulFormationTargets > 0)
                LogWarningNoThrow($"FORMATION_ORDER_PARTIAL_FALLBACK: {common}.");
            else
                LogWarningNoThrow($"FORMATION_ORDER_FELL_BACK_TO_VANILLA: {common}.");
        }

        private UnitFallbackSummary RunUnitFallbacks(ActiveFormationCommand command)
        {
            UnitFallbackRequest[] requests = command.GetFallbackRequests();
            int succeeded = 0;
            int failedCount = 0;
            for (int index = 0; index < requests.Length; index++)
            {
                UnitFallbackRequest request = requests[index];
                if (!TryGetMatchingUnit(
                        request.UnitId, request.GlobalId, out _))
                {
                    failedCount++;
                    LogWarningNoThrow(
                        $"FORMATION_UNIT_FALLBACK_ERROR: " +
                        $"operation={command.Pending.Packet.OperationId}, " +
                        $"unit={request.UnitId}, error=unit-identity-changed.");
                    continue;
                }
                var attempt = new UnitFallbackAttempt(
                    request.UnitId,
                    command.TargetX,
                    command.TargetY,
                    request.Unknown);
                lock (stateSync)
                {
                    if (unitFallbackAttempt != null)
                        throw new InvalidOperationException("Nested unit fallback was rejected.");
                    unitFallbackAttempt = attempt;
                }
                try
                {
                    GameUnitManagerAPI.Instance.MoveToTile(
                        request.UnitId,
                        command.TargetX,
                        command.TargetY,
                        request.Unknown);
                }
                catch (Exception exception)
                {
                    LogWarningNoThrow(
                        $"FORMATION_UNIT_FALLBACK_ERROR: " +
                        $"operation={command.Pending.Packet.OperationId}, " +
                        $"unit={request.UnitId}, error={exception.Message}.");
                }
                finally
                {
                    lock (stateSync)
                    {
                        if (ReferenceEquals(unitFallbackAttempt, attempt))
                            unitFallbackAttempt = null;
                    }
                }

                bool accepted = attempt.ReturnValue > 0 &&
                    TryGetMatchingUnit(
                        request.UnitId, request.GlobalId, out GameUnit* verifiedUnit) &&
                    verifiedUnit->r_TargetTilePositionX == command.TargetX &&
                    verifiedUnit->r_TargetTilePositionY == command.TargetY;
                if (accepted)
                    succeeded++;
                else
                    failedCount++;
                LogWarningNoThrow(
                    $"FORMATION_UNIT_FELL_BACK_TO_VANILLA: " +
                    $"operation={command.Pending.Packet.OperationId}, unit={request.UnitId}, " +
                    $"return={attempt.ReturnValue}, accepted={accepted}.");
            }
            return new UnitFallbackSummary(succeeded, failedCount);
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
                RecordSelectorAssignment(command, false);
                return;
            }
            if (!TryTakeDestination(command, out NativeDestination destination))
            {
                standardSelectorHandle.Original(manager, command.Density, x, y);
                return;
            }
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, false);
        }

        private int ChooseAssassinFormationSlot(IntPtr manager, int spacing, int x, int y)
        {
            ActiveFormationCommand command;
            lock (stateSync)
                command = activeCommand;
            if (!Matches(command, manager, x, y))
                return assassinSelectorHandle.Original(manager, spacing, x, y);
            if (!command.Managed)
            {
                int result = assassinSelectorHandle.Original(manager, command.Density, x, y);
                RecordSelectorAssignment(command, true);
                return result;
            }
            if (!TryTakeDestination(command, out NativeDestination destination))
                return assassinSelectorHandle.Original(manager, command.Density, x, y);
            WriteFormationOutput(destination);
            RecordSelectorAssignment(command, true);
            return destination.TileId;
        }

        private long CommonGroupMoveHook(
            IntPtr manager,
            int tribeId,
            short x,
            short y,
            short patrol,
            int newOrder)
        {
            ActiveFormationCommand previous;
            UnitAssignmentFrame previousUnitFrame;
            ActiveFormationCommand command = null;
            lock (stateSync)
            {
                previous = commonGroupCommand;
                previousUnitFrame = unitAssignmentFrame;
                ActiveFormationCommand candidate = activeCommand;
                commonGroupCommand = null;
                unitAssignmentFrame = null;
                if (candidate != null && candidate.Managed &&
                    manager == nativeTribeManager && candidate.TribeId == tribeId &&
                    candidate.TargetX == x && candidate.TargetY == y &&
                    patrol == 0 && candidate.IsNewOrder == newOrder)
                {
                    command = candidate;
                    command.CommonPathEntered = true;
                    commonGroupCommand = command;
                }
            }
            try
            {
                return commonGroupMoveHandle.Original(
                    manager, tribeId, x, y, patrol, newOrder);
            }
            finally
            {
                lock (stateSync)
                {
                    ClearUnitAssignmentFrames(command);
                    if (ReferenceEquals(commonGroupCommand, command))
                        commonGroupCommand = previous;
                    unitAssignmentFrame = previousUnitFrame;
                }
            }
        }

        private void OnUnitMoveHere(UnitMoveHereEventArgs args)
        {
            if (!initialized || failed || args == null)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                if (args.SkipOriginalFunction)
                    return;
                try
                {
                    lock (stateSync)
                    {
                        PruneUnitAssignmentFrames();
                        ActiveFormationCommand command = activeCommand;
                        if (command == null || !command.Managed ||
                             !command.TryGetUnitDestination(
                                 args.UnitId,
                                 out NativeDestination destination,
                                 out uint globalId) ||
                             !TryGetMatchingUnit(args.UnitId, globalId, out GameUnit* unit))
                        {
                            return;
                        }
                        UnitAssignmentFrame parent = unitAssignmentFrame;
                        int originalX = args.TileX;
                        int originalY = args.TileY;
                        args.TileX = destination.X;
                        args.TileY = destination.Y;
                        unit->r_AttackMoveToTargetTileX = (ushort)destination.X;
                        unit->r_AttackMoveToTargetTileY = (ushort)destination.Y;
                        unitAssignmentFrame = new UnitAssignmentFrame(
                            args,
                            parent,
                            command,
                            args.UnitId,
                            globalId,
                            destination,
                            originalX,
                            originalY,
                            args.Unknown);
                    }
                }
                catch (Exception exception)
                {
                    DisableAfterNativeFailure("unit-target-pre", exception);
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post)
                return;
            try
            {
                lock (stateSync)
                {
                    UnitFallbackAttempt fallback = unitFallbackAttempt;
                    if (fallback != null && fallback.Matches(args))
                    {
                        fallback.Observe(args.ReturnValue);
                        return;
                    }
                    PruneUnitAssignmentFrames();
                    UnitAssignmentFrame frame = unitAssignmentFrame;
                    if (frame == null)
                        return;
                    try
                    {
                        bool accepted = false;
                        if (args.UnitId == frame.UnitId &&
                            args.TileX == frame.OriginalX &&
                            args.TileY == frame.OriginalY &&
                            args.Unknown == frame.OriginalUnknown &&
                            args.ReturnValue > 0 &&
                            !frame.PreArgs.SkipOriginalFunction &&
                            ReferenceEquals(activeCommand, frame.Command) &&
                            TryGetMatchingUnit(
                                frame.UnitId, frame.GlobalId, out GameUnit* unit) &&
                            unit->r_TargetTilePositionX == frame.Destination.X &&
                            unit->r_TargetTilePositionY == frame.Destination.Y &&
                            unit->r_AttackMoveToTargetTileX == frame.Destination.X &&
                            unit->r_AttackMoveToTargetTileY == frame.Destination.Y)
                        {
                            accepted = true;
                        }
                        FinishUnitAssignmentFrame(frame, accepted);
                    }
                    finally
                    {
                        unitAssignmentFrame = frame.Parent;
                    }
                }
            }
            catch (Exception exception)
            {
                DisableAfterNativeFailure("unit-target-post", exception);
            }
        }

        private void PruneUnitAssignmentFrames()
        {
            while (unitAssignmentFrame != null &&
                (unitAssignmentFrame.PreArgs.SkipOriginalFunction ||
                 !ReferenceEquals(unitAssignmentFrame.Command, activeCommand)))
            {
                UnitAssignmentFrame frame = unitAssignmentFrame;
                unitAssignmentFrame = frame.Parent;
                FinishUnitAssignmentFrame(frame, false);
            }
        }

        private void ClearUnitAssignmentFrames(ActiveFormationCommand command)
        {
            while (unitAssignmentFrame != null &&
                (command == null || ReferenceEquals(unitAssignmentFrame.Command, command)))
            {
                UnitAssignmentFrame frame = unitAssignmentFrame;
                unitAssignmentFrame = frame.Parent;
                FinishUnitAssignmentFrame(frame, false);
            }
        }

        private static bool TryGetMatchingUnit(
            int unitId,
            uint globalId,
            out GameUnit* unit)
        {
            unit = null;
            return globalId != 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) &&
                unit != null && unit->r_AliveState == AliveState.IsAlive &&
                unit->r_GlobalId == globalId;
        }

        private static void FinishUnitAssignmentFrame(
            UnitAssignmentFrame frame,
            bool accepted)
        {
            if (frame == null)
                return;
            frame.Command.RecordTerminalResult(
                frame.UnitId,
                accepted,
                frame.OriginalUnknown);
            if (accepted)
                return;
            if (frame.PreArgs.UnitId == frame.UnitId &&
                frame.PreArgs.TileX == frame.Destination.X &&
                frame.PreArgs.TileY == frame.Destination.Y)
            {
                frame.PreArgs.TileX = frame.OriginalX;
                frame.PreArgs.TileY = frame.OriginalY;
            }
            if (TryGetMatchingUnit(frame.UnitId, frame.GlobalId, out GameUnit* unit) &&
                unit->r_AttackMoveToTargetTileX == frame.Destination.X &&
                unit->r_AttackMoveToTargetTileY == frame.Destination.Y)
            {
                unit->r_AttackMoveToTargetTileX = (ushort)frame.OriginalX;
                unit->r_AttackMoveToTargetTileY = (ushort)frame.OriginalY;
            }
        }

        private void RecordSelectorAssignment(
            ActiveFormationCommand command,
            bool assassin)
        {
            lock (stateSync)
            {
                if (ReferenceEquals(activeCommand, command))
                    command.RecordSelectorAssignment(assassin);
            }
        }

        private void DisableAfterNativeFailure(string stage, Exception exception)
        {
            lock (stateSync)
            {
                ClearUnitAssignmentFrames(null);
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitFallbackAttempt = null;
                failed = true;
            }
            try
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Formation native assignment failed open at {stage}; " +
                    $"future formation commands are disabled: {exception}");
            }
            catch
            {
            }
        }

        private void LogDebugNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogDebug(log, message);
                }
                catch
                {
                }
            });
        }

        private void LogWarningNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogWarning(log, message);
                }
                catch
                {
                }
            });
        }

        private void LogErrorNoThrow(string message)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogError(log, message);
                }
                catch
                {
                }
            });
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
            RangedPlacementMode placementMode,
            FormationUnit[] units,
            out FormationDirectionIndicator directionIndicator)
        {
            directionIndicator = FormationDirectionIndicator.Hidden;
            List<FormationPoint> slots = FormationModel.BuildRelativeSlots(
                kind, units.Length, Math.Max(1, width), density, direction);
            bool assassinOnly = IsAssassinOnly(units);
            List<NativeDestination> candidates = CaptureReachableCandidates(
                anchorX, anchorY, Math.Min(MaximumPreviewCandidates,
                    Math.Max(256, units.Length * 16)), assassinOnly);
            if (candidates.Count == 0)
                throw new InvalidOperationException("No reachable formation destination exists.");

            NativeDestination[] snapped = SnapSlots(anchorX, anchorY, slots, candidates);
            directionIndicator = BuildDirectionIndicator(slots, snapped, direction);
            int[] assignment = FormationModel.AssignSlotsByRole(units, slots, placementMode);
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

        private static FormationDirectionIndicator BuildDirectionIndicator(
            IReadOnlyList<FormationPoint> slots,
            IReadOnlyList<NativeDestination> snapped,
            int directionSector)
        {
            int count = Math.Min(slots?.Count ?? 0, snapped?.Count ?? 0);
            if (count == 0)
                return FormationDirectionIndicator.Hidden;
            int frontRank = int.MaxValue;
            for (int index = 0; index < count; index++)
                frontRank = Math.Min(frontRank, slots[index].Rank);
            long totalX = 0;
            long totalY = 0;
            int frontCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (slots[index].Rank != frontRank)
                    continue;
                totalX += snapped[index].X;
                totalY += snapped[index].Y;
                frontCount++;
            }
            if (frontCount == 0)
                return FormationDirectionIndicator.Hidden;
            int centerX = (int)Math.Round((double)totalX / frontCount);
            int centerY = (int)Math.Round((double)totalY / frontCount);
            FormationModel.GetForwardVector(
                directionSector, out int forwardX, out int forwardY);
            return new FormationDirectionIndicator(
                true,
                centerX + forwardX,
                centerY + forwardY,
                centerX + forwardX * 4,
                centerY + forwardY * 4);
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
                    unitId, unit->r_GlobalId, unitType, Classify((eChimps)unitType)));
            }
            result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
            return result.ToArray();
        }

        private static ulong ComputePlanHash(
            IReadOnlyList<FormationUnit> units,
            IReadOnlyList<NativeDestination> destinations,
            RangedPlacementMode placementMode)
        {
            int count = Math.Min(units?.Count ?? 0, destinations?.Count ?? 0);
            ulong hash = FormationPlanHash.Begin(count, placementMode);
            for (int index = 0; index < count; index++)
            {
                FormationUnit unit = units[index];
                NativeDestination destination = destinations[index];
                FormationPlanHash.AddEntry(
                    ref hash,
                    unit.UnitId,
                    unit.GlobalId,
                    destination.X,
                    destination.Y,
                    destination.Role);
            }
            return hash;
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
            state.DirectionSector = direction;
            state.Width = width;
            FormationPreviewKey previewKey = FormationPreviewKey.Create(
                state.Kind,
                state.Density,
                state.PlacementMode,
                direction,
                width,
                state.Target.NativeX,
                state.Target.NativeY,
                state.Selection.Length);
            if (!force && state.HasLastPreviewKey &&
                state.LastPreviewKey.Equals(previewKey))
                return;
            state.LastPreviewKey = previewKey;
            state.HasLastPreviewKey = true;
            state.HasPreviewPlan = false;
            state.PreviewPlanHash = 0UL;

            FormationUnit[] units = new FormationUnit[state.Selection.Length];
            for (int index = 0; index < units.Length; index++)
            {
                SelectionIdentity selected = state.Selection[index];
                units[index] = new FormationUnit(
                    selected.UnitId,
                    selected.GlobalId,
                    selected.UnitType,
                    Classify((eChimps)selected.UnitType));
            }

            try
            {
                NativeDestination[] destinations;
                FormationDirectionIndicator directionIndicator =
                    FormationDirectionIndicator.Hidden;
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
                        state.PlacementMode,
                        units,
                        out directionIndicator);
                }

                var points = new FormationPreviewPoint[destinations.Length];
                for (int index = 0; index < destinations.Length; index++)
                {
                    points[index] = new FormationPreviewPoint(
                        destinations[index].X,
                        destinations[index].Y,
                        destinations[index].Role);
                }
                state.PreviewPlanHash = state.Kind == FormationKind.Vanilla
                    ? 0UL
                    : ComputePlanHash(units, destinations, state.PlacementMode);
                state.HasPreviewPlan = state.Kind == FormationKind.Vanilla ||
                    destinations.Length == units.Length;
                var markerTiles = new int[destinations.Length];
                for (int index = 0; index < destinations.Length; index++)
                    markerTiles[index] = destinations[index].TileId;
                if (!markerRenderer.SetPreviewMarkerTiles(markerTiles))
                {
                    FormationPreviewOverlay.Clear();
                    return;
                }
                FormationPreviewOverlay.Publish(points, directionIndicator);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"FORMATION_PREVIEW_UPDATED: kind={previewKey.Kind}, " +
                    $"density={previewKey.Density}, placement={previewKey.PlacementMode}, " +
                    $"direction={previewKey.DirectionSector}, width={previewKey.Width}, " +
                    $"rows={FormationModel.ResolveActualRows(previewKey.Kind, previewKey.UnitCount, previewKey.Width)}, " +
                    $"markers={new HashSet<int>(markerTiles).Count}, " +
                    $"plan=0x{state.PreviewPlanHash:X16}, " +
                    $"thread={Environment.CurrentManagedThreadId}.");
            }
            catch (Exception exception)
            {
                ClearPreview();
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
                    state.Kind, dragDistance, state.Selection.Length);
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
            if (selected == null || selected.Length < 2 ||
                selected.Length > FormationPreviewMarkerModel.MaximumMarkers)
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
                tribe == null || tribe->r_AliveState != AliveState.IsAlive)
                return false;
            bool isMapEditor = MainViewModel.instance != null &&
                MainViewModel.instance.IsMapEditorMode;
            if (!isMapEditor &&
                (GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner) ||
                 tribe->r_PlayerIdOwner != GamePlayerManagerAPI.Instance.GetLocalPlayerId()))
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
            state != null && markerRenderer != null &&
            markerRenderer.ReplacementAvailable && HasValidMap() && !IsShiftHeld() &&
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

        private FormationMouseState CaptureMouseState(EditorDirector director) =>
            new FormationMouseState(
                (int)leftMouseStateField.GetValue(director),
                (bool)rightMouseDownField.GetValue(director),
                (bool)rightMouseUpField.GetValue(director),
                (bool)mouseStateReadField.GetValue(director),
                (bool)mouseUpPendingField.GetValue(director));

        private void ApplyMouseState(
            EditorDirector director,
            FormationMouseState state)
        {
            leftMouseStateField.SetValue(director, state.LeftState);
            rightMouseDownField.SetValue(director, state.RightDown);
            rightMouseUpField.SetValue(director, state.RightUp);
            mouseStateReadField.SetValue(director, state.StateRead);
            mouseUpPendingField.SetValue(director, state.UpPending);
        }

        private void AgeReleaseConsumptionWatch()
        {
            lock (stateSync)
            {
                if (releaseConsumptionWatch == null)
                    return;
                if (!releaseConsumptionWatch.AdvanceEngineRun())
                    releaseConsumptionWatch = null;
            }
        }

        private void DetectUnexpectedSecondOrder(
            TribeIssueOrderMoveHereEventArgs args,
            PendingFormationCommand pending)
        {
            ReleaseConsumptionWatch watch;
            lock (stateSync)
            {
                watch = releaseConsumptionWatch;
                if (watch == null || watch.Reported || args.TribeId != watch.TribeId)
                    return;
                if (pending != null && pending.Matches(args))
                    return;
                watch.Reported = true;
            }

            LogWarningNoThrow(
                $"FORMATION_UNEXPECTED_SECOND_ORDER: operation={watch.OperationId}, " +
                $"tribe={watch.TribeId}, button={watch.CommandButton}, " +
                $"target={args.TileX},{args.TileY}.");
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
            else if (packet.PlacementMode > (byte)RangedPlacementMode.Center)
                rejection = "invalid ranged placement mode";
            else if (packet.DirectionSector > 7)
                rejection = "invalid direction";
            else if (packet.Width == 0 || packet.Width > MaximumUnitCount)
                rejection = "invalid width";
            else if (packet.UnitCount < 2 ||
                     packet.UnitCount > FormationPreviewMarkerModel.MaximumMarkers)
                rejection = "invalid unit count";
            else if (packet.Formation == (byte)FormationKind.Vanilla &&
                     packet.PlanHash != 0UL)
                rejection = "Vanilla packet unexpectedly contains a plan hash";
            else if (packet.Formation != (byte)FormationKind.Vanilla &&
                     packet.PlanHash == 0UL)
                rejection = "managed formation packet has no plan hash";
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
            ClearPreview();
            Shared.DebugLogHelper.LogDebug(log, $"FORMATION_DRAG_ABORTED: {reason}.");
        }

        private void FailOpen(string contract, Exception exception)
        {
            failed = true;
            lock (stateSync)
            {
                drag = null;
                pendingCommand = null;
                activeCommand = null;
                commonGroupCommand = null;
                unitAssignmentFrame = null;
                unitFallbackAttempt = null;
                releaseConsumptionWatch = null;
            }
            ClearPreview();
            menuViewModel.CloseMenu();
            Shared.DebugLogHelper.LogError(
                log,
                $"FORMATION_TEST_DISABLED: contract={contract}; Vanilla remains active; {exception}");
        }

        private void ClearPreview()
        {
            markerRenderer?.ClearPreviewMarkerTiles();
            FormationPreviewOverlay.Clear();
        }

        private void RequireMainThread(string contract)
        {
            int current = Environment.CurrentManagedThreadId;
            if (current != mainThreadId)
            {
                throw new InvalidOperationException(
                    $"Formation input callback '{contract}' ran on thread {current}; " +
                    $"expected main thread {mainThreadId}.");
            }
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
            if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionX)).ToInt32() !=
                    0xC4 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionY)).ToInt32() !=
                    0xC6 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AttackMoveToTargetTileX)).ToInt32() !=
                    0x2D8 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AttackMoveToTargetTileY)).ToInt32() !=
                    0x2DA)
            {
                throw new InvalidOperationException(
                    "GameUnit target tile offsets no longer match the audited native layout.");
            }
            ValidateBytes(memory, StandardSelectorRva, StandardSelectorPrefix,
                "standard formation selector");
            ValidateUniqueEntry(memory, StandardSelectorRva, StandardSelectorPrefix,
                "standard formation selector");
            ValidateBytes(memory, AssassinSelectorRva, AssassinSelectorPrefix,
                "Assassin ground formation selector");
            ValidateUniqueEntry(memory, AssassinSelectorRva, AssassinSelectorPrefix,
                "Assassin ground formation selector");
            ValidateBytes(memory, CommonGroupMoveRva, CommonGroupMovePrefix,
                "common group movement path");
            ValidateUniqueEntry(memory, CommonGroupMoveRva, CommonGroupMovePrefix,
                "common group movement path");
            ValidateBytes(memory, UnitMoveTargetRva, UnitMoveTargetPrefix,
                "terminal unit movement target writer");
            ValidateUniqueEntry(memory, UnitMoveTargetRva, UnitMoveTargetPrefix,
                "terminal unit movement target writer");
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
                RangedPlacementMode placementMode)
            {
                CommandButton = commandButton;
                TribeId = tribeId;
                Target = target;
                Selection = selection;
                Kind = kind;
                Density = density;
                PlacementMode = placementMode;
                ReleaseGate = new FormationReleaseGate(commandButton);
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
            internal RangedPlacementMode PlacementMode { get; set; }
            internal FormationReleaseGate ReleaseGate { get; }
            internal int DragDeltaX { get; set; }
            internal int DragDeltaY { get; set; }
            internal int DefaultDirectionSector { get; }
            internal int DirectionSector { get; set; }
            internal int Width { get; set; }
            internal bool HasLastPreviewKey { get; set; }
            internal FormationPreviewKey LastPreviewKey { get; set; }
            internal bool HasPreviewPlan { get; set; }
            internal ulong PreviewPlanHash { get; set; }
        }

        private enum DispatchDisposition
        {
            Rejected = 0,
            Accepted = 1
        }

        private sealed class ReleaseConsumptionWatch
        {
            private int remainingEngineRuns;

            internal ReleaseConsumptionWatch(
                int operationId,
                int tribeId,
                int commandButton,
                int remainingEngineRuns)
            {
                OperationId = operationId;
                TribeId = tribeId;
                CommandButton = commandButton;
                this.remainingEngineRuns = remainingEngineRuns;
            }

            internal int OperationId { get; }
            internal int TribeId { get; }
            internal int CommandButton { get; }
            internal bool Reported { get; set; }

            internal bool AdvanceEngineRun()
            {
                remainingEngineRuns--;
                return remainingEngineRuns > 0;
            }
        }

        private sealed class PendingFormationCommand
        {
            internal PendingFormationCommand(FormationOrderPacket packet, string source)
            {
                Packet = packet ?? throw new ArgumentNullException(nameof(packet));
                Source = source ?? string.Empty;
            }

            internal FormationOrderPacket Packet { get; }
            internal string Source { get; }

            internal bool Matches(TribeIssueOrderMoveHereEventArgs args) =>
                args != null && FormationOrderMatchModel.Matches(
                    Packet.TribeId,
                    Packet.TargetX,
                    Packet.TargetY,
                    Packet.IsNewOrder,
                    Packet.MoveType,
                    args.TribeId,
                    args.TileX,
                    args.TileY,
                    args.IsPatrolPath,
                    args.IsNewOrder,
                    (int)args.MoveType);
        }

        private sealed class ActiveFormationCommand
        {
            private readonly Dictionary<int, NativeDestination> destinationsByUnitId;
            private readonly Dictionary<int, uint> globalIdsByUnitId;
            private readonly Dictionary<int, int> terminalUnknownByUnitId =
                new Dictionary<int, int>();
            private readonly HashSet<int> successfulTerminalUnitIds = new HashSet<int>();
            private int standardAssignments;
            private int assassinAssignments;

            internal ActiveFormationCommand(
                PendingFormationCommand pending,
                FormationKind kind,
                int density,
                FormationUnit[] units,
                NativeDestination[] destinations)
            {
                Pending = pending ?? throw new ArgumentNullException(nameof(pending));
                Kind = kind;
                Density = density;
                ExpectedCount = units?.Length ?? 0;
                Destinations = destinations ?? Array.Empty<NativeDestination>();
                destinationsByUnitId = new Dictionary<int, NativeDestination>(ExpectedCount);
                globalIdsByUnitId = new Dictionary<int, uint>(ExpectedCount);
                if (Managed && Destinations.Length != ExpectedCount)
                {
                    throw new InvalidOperationException(
                        "The native unit and destination counts do not match.");
                }
                for (int index = 0; Managed && index < ExpectedCount; index++)
                {
                    int unitId = units[index].UnitId;
                    uint globalId = units[index].GlobalId;
                    if (unitId <= 0 || globalId == 0 ||
                        destinationsByUnitId.ContainsKey(unitId))
                        throw new InvalidOperationException(
                            $"Invalid or duplicate native unit identity {unitId}/{globalId}.");
                    destinationsByUnitId.Add(unitId, Destinations[index]);
                    globalIdsByUnitId.Add(unitId, globalId);
                }
            }

            internal PendingFormationCommand Pending { get; }
            internal FormationKind Kind { get; }
            internal int TribeId => Pending.Packet.TribeId;
            internal int TargetX => Pending.Packet.TargetX;
            internal int TargetY => Pending.Packet.TargetY;
            internal int IsNewOrder => Pending.Packet.IsNewOrder;
            internal int Density { get; }
            internal bool Managed => Kind != FormationKind.Vanilla;
            internal int ExpectedCount { get; }
            internal NativeDestination[] Destinations { get; }
            internal int Cursor { get; set; }
            internal bool CommonPathEntered { get; set; }
            internal int TerminalAttemptCount => terminalUnknownByUnitId.Count;
            internal int SuccessfulTerminalCount => successfulTerminalUnitIds.Count;
            internal int AssignedCount =>
                Math.Min(
                    ExpectedCount,
                    standardAssignments + assassinAssignments);
            internal string AssignmentPath
            {
                get
                {
                    int kinds = (standardAssignments > 0 ? 1 : 0) +
                        (assassinAssignments > 0 ? 1 : 0) +
                        (CommonPathEntered ? 1 : 0);
                    if (kinds > 1)
                        return "mixed";
                    if (CommonPathEntered)
                        return "common";
                    if (assassinAssignments > 0)
                        return "assassin";
                    if (standardAssignments > 0)
                        return "standard";
                    return CommonPathEntered ? "common-unassigned" : "none";
                }
            }

            internal bool Matches(TribeIssueOrderMoveHereEventArgs args) =>
                Pending.Matches(args);

            internal bool TryGetUnitDestination(
                int unitId,
                out NativeDestination destination,
                out uint globalId)
            {
                if (destinationsByUnitId.TryGetValue(unitId, out destination) &&
                    globalIdsByUnitId.TryGetValue(unitId, out globalId))
                {
                    return true;
                }
                destination = default;
                globalId = 0;
                return false;
            }

            internal void RecordSelectorAssignment(bool assassin)
            {
                if (assassin)
                    assassinAssignments++;
                else
                    standardAssignments++;
            }

            internal void RecordTerminalResult(int unitId, bool accepted, int unknown)
            {
                terminalUnknownByUnitId[unitId] = unknown;
                if (accepted)
                    successfulTerminalUnitIds.Add(unitId);
            }

            internal UnitFallbackRequest[] GetFallbackRequests()
            {
                var result = new List<UnitFallbackRequest>();
                foreach (KeyValuePair<int, NativeDestination> pair in destinationsByUnitId)
                {
                    if (successfulTerminalUnitIds.Contains(pair.Key) ||
                        !globalIdsByUnitId.TryGetValue(pair.Key, out uint globalId))
                        continue;
                    terminalUnknownByUnitId.TryGetValue(pair.Key, out int unknown);
                    result.Add(new UnitFallbackRequest(pair.Key, globalId, unknown));
                }
                result.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
                return result.ToArray();
            }
        }

        private static void ValidateUniqueEntry(
            ReadOnlySpan<byte> memory,
            int expectedRva,
            byte[] expected,
            string name)
        {
            string[] bytes = new string[expected.Length];
            for (int index = 0; index < expected.Length; index++)
                bytes[index] = expected[index].ToString("X2");
            int resolved = Shared.NativePatternResolver.FindUniquePattern(
                memory, string.Join(" ", bytes), name);
            if (resolved != expectedRva)
            {
                throw new InvalidOperationException(
                    $"{name} resolved to RVA 0x{resolved:X}, expected 0x{expectedRva:X}.");
            }
        }

        private sealed class UnitAssignmentFrame
        {
            internal UnitAssignmentFrame(
                UnitMoveHereEventArgs preArgs,
                UnitAssignmentFrame parent,
                ActiveFormationCommand command,
                int unitId,
                uint globalId,
                NativeDestination destination,
                int originalX,
                int originalY,
                int originalUnknown)
            {
                PreArgs = preArgs;
                Parent = parent;
                Command = command;
                UnitId = unitId;
                GlobalId = globalId;
                Destination = destination;
                OriginalX = originalX;
                OriginalY = originalY;
                OriginalUnknown = originalUnknown;
            }

            internal UnitMoveHereEventArgs PreArgs { get; }
            internal UnitAssignmentFrame Parent { get; }
            internal ActiveFormationCommand Command { get; }
            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal NativeDestination Destination { get; }
            internal int OriginalX { get; }
            internal int OriginalY { get; }
            internal int OriginalUnknown { get; }
        }

        private sealed class UnitFallbackAttempt
        {
            internal UnitFallbackAttempt(int unitId, int x, int y, int unknown)
            {
                UnitId = unitId;
                X = x;
                Y = y;
                Unknown = unknown;
            }

            internal int UnitId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal int Unknown { get; }
            internal long ReturnValue { get; private set; }

            internal bool Matches(UnitMoveHereEventArgs args) =>
                args != null && args.Phase == EventHookPhase.Post &&
                args.UnitId == UnitId && args.TileX == X && args.TileY == Y &&
                args.Unknown == Unknown;

            internal void Observe(long returnValue)
            {
                ReturnValue = returnValue;
            }
        }

        private readonly struct UnitFallbackRequest
        {
            internal UnitFallbackRequest(int unitId, uint globalId, int unknown)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Unknown = unknown;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal int Unknown { get; }
        }

        private readonly struct UnitFallbackSummary
        {
            internal UnitFallbackSummary(int succeeded, int failed)
            {
                Succeeded = succeeded;
                Failed = failed;
            }

            internal int Succeeded { get; }
            internal int Failed { get; }
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
