using BugfixesAndQoL;
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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        internal FormationRuntime(
            ManualLogSource log,
            CrusaderLibraryLoadContext libraryContext,
            ConfigEntry<FormationKind> formationConfig,
            ConfigEntry<int> densityConfig,
            ConfigEntry<RangedPlacementMode> placementModeConfig,
            IFormationPresentation menuViewModel,
            IUnitCommandSettings settings,
            UnitCommandPathRuntime commandRuntime,
            LargeMoveTargetMarkerRenderer markerRenderer)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.commandRuntime = commandRuntime ?? throw new ArgumentNullException(nameof(commandRuntime));
            this.markerRenderer = markerRenderer ?? throw new ArgumentNullException(nameof(markerRenderer));
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

            Hook pendingEngineRun = null;
            Hook pendingCameraUpdate = null;
            IDisposable pendingKeyDown = null;
            IDisposable pendingKeyHeld = null;
            IDisposable pendingKeyUp = null;
            IDisposable pendingPacket = null;
            try
            {
                mainThreadId = Environment.CurrentManagedThreadId;
                ulong libraryBase = unchecked((ulong)libraryContext.ModuleHandle.ToInt64());
                ValidateNativeContracts(libraryContext.Memory);
                commandModeReader = new APIShared.Internal.NativeTroopCommandModeReader(
                    libraryContext.ModuleHandle,
                    libraryContext.Memory);
                if (!commandRuntime.FormationHooksAvailable || !markerRenderer.ReplacementAvailable)
                    throw new InvalidOperationException("Shared formation selectors or marker renderer are unavailable.");
                groundFeedbackReader = new NativeGroundMoveFeedbackReader(libraryContext.ModuleHandle, libraryContext.Memory);
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

                pendingEngineRun = new Hook(engineRun, (EngineRunDelegate)EngineRunHook,
                    new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.Formation.Release" });
                engineRunOriginal = pendingEngineRun.GenerateTrampoline<EngineRunDelegate>();
                pendingCameraUpdate = new Hook(cameraUpdate, (CameraUpdateDelegate)CameraUpdateHook,
                    new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.Formation.Zoom" });
                cameraUpdateOriginal =
                    pendingCameraUpdate.GenerateTrampoline<CameraUpdateDelegate>();

                engineRunHook = pendingEngineRun;
                pendingEngineRun = null;
                engineRunHook.Apply();
                cameraUpdateHook = pendingCameraUpdate;
                pendingCameraUpdate = null;
                cameraUpdateHook.Apply();

                packetHook = GameNetworkAPI.Instance.GetPacketEventFor<FormationOrderPacket>();
                pendingPacket = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
                pendingKeyDown = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);
                pendingKeyHeld = InputR3EventHooks.OnKey.Observable.Subscribe(OnKeyHeld);
                pendingKeyUp = InputR3EventHooks.OnKeyUp.Observable.Subscribe(OnKeyUp);

                packetSubscription = pendingPacket;
                pendingPacket = null;
                keyDownSubscription = pendingKeyDown;
                pendingKeyDown = null;
                keyHeldSubscription = pendingKeyHeld;
                pendingKeyHeld = null;
                keyUpSubscription = pendingKeyUp;
                pendingKeyUp = null;
                commandRuntime.formationRuntime = this;
                initialized = true;

                APIShared.Internal.DebugLogHelper.LogInfo(
                    log,
                    "BugfixesAndQoL Formation active: synchronized Chore packet, mouse gesture hooks, " +
                    $"mainThread={mainThreadId}, " +
                    "selectors=APIShared/process-owned, " +
                    $"unitTarget=0x{UnitMoveTargetRva:X}/extender-event, " +
                    $"nativeAuditSpan={ExpectedUnitMoveTargetAuditBytes}.");
            }
            catch
            {
                failed = true;
                pendingKeyUp?.Dispose();
                pendingKeyHeld?.Dispose();
                pendingKeyDown?.Dispose();
                pendingPacket?.Dispose();
                pendingCameraUpdate?.Dispose();
                pendingEngineRun?.Dispose();
                throw;
            }
        }
    }
}
