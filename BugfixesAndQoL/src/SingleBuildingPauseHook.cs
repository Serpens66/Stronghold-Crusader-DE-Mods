// Feature: Ctrl-click to pause only the selected production building.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class SingleBuildingPauseHook : IDisposable
    {
        private delegate void ButtonToggleZzzModeDelegate(MainViewModel self, object parameter);
        private delegate bool AddChimpActionsDelegate(
            FatControler self,
            EngineInterface.PlayState state,
            ref string line1,
            ref string line2,
            bool islamic);

        private const long DuplicateToggleSuppressMilliseconds = 750;
        private const int ChoreProtocolVersion = 2;
        private const int SetSingleBuildingAction = 1;
        private const int ResetBuildingTypeAction = 2;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly SingleBuildingPauseOverrideStore overrides = new SingleBuildingPauseOverrideStore();
        private Hook buttonHook;
        private Hook addChimpActionsHook;
        private ButtonToggleZzzModeDelegate buttonTrampoline;
        private AddChimpActionsDelegate addChimpActionsTrampoline;
        private int lastManualToggleBuildingId;
        private long lastManualToggleTimestamp;
        private Action synchronizeSleepStates;
        private Action<bool> setSleepOverrideInterceptionEnabled;
        private bool localHooksInstalled;
        private bool overrideHooksActive;
        private bool overrideHookActivationFailureLogged;
        private bool overrideHookDeactivationFailureLogged;
        private bool uiRefreshFailureLogged;
        private bool networkInitialized;
        private int nextOperationId;
        private R3PacketEventHook<SingleBuildingPausePacket> pausePacketHook;
        private IDisposable pausePacketSubscription;
        private IDisposable buildingDeleteSubscription;

        public SingleBuildingPauseHook(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));

        }

        public void InstallLocalHooks()
        {
            if (localHooksInstalled)
                return;

            MethodInfo buttonMethod = FindButtonToggleZzzModeMethod();
            MethodInfo addChimpActionsMethod = FindAddChimpActionsMethod();
            Hook installedButtonHook = null;
            Hook installedAddChimpActionsHook = null;
            try
            {
                installedButtonHook = new Hook(buttonMethod, (ButtonToggleZzzModeDelegate)ButtonToggleZzzModeHook);
                ButtonToggleZzzModeDelegate installedButtonTrampoline = installedButtonHook.GenerateTrampoline<ButtonToggleZzzModeDelegate>();

                installedAddChimpActionsHook = new Hook(
                    addChimpActionsMethod,
                    (AddChimpActionsDelegate)AddChimpActionsHook);
                AddChimpActionsDelegate installedAddChimpActionsTrampoline =
                    installedAddChimpActionsHook.GenerateTrampoline<AddChimpActionsDelegate>();
                // Individual state does not exist yet, so keep the render-time correction dormant.
                installedAddChimpActionsHook.Undo();
                if (installedAddChimpActionsHook.IsApplied)
                    throw new InvalidOperationException("The building-action UI hook remained active after preparation.");

                buttonHook = installedButtonHook;
                buttonTrampoline = installedButtonTrampoline;
                addChimpActionsHook = installedAddChimpActionsHook;
                addChimpActionsTrampoline = installedAddChimpActionsTrampoline;
                localHooksInstalled = true;
            }
            catch
            {
                installedAddChimpActionsHook?.Dispose();
                installedButtonHook?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            pausePacketSubscription?.Dispose();
            pausePacketSubscription = null;
            buildingDeleteSubscription?.Dispose();
            buildingDeleteSubscription = null;
            pausePacketHook = null;
            networkInitialized = false;
            UninstallLocalHooks();
            ClearManualSleepOverrides();
        }

        public void UninstallLocalHooks()
        {
            if (!localHooksInstalled)
                return;

            // Clear gameplay state before disposing the prepared managed hooks.
            localHooksInstalled = false;
            ClearManualSleepOverrides();
            ReleaseHook("button", ref buttonHook);
            buttonTrampoline = null;
            ReleaseHook("building action UI", ref addChimpActionsHook);
            addChimpActionsTrampoline = null;
        }

        private void ReleaseHook(string hookName, ref Hook hook)
        {
            Hook current = hook;
            hook = null;
            if (current == null)
                return;

            try
            {
                current.Undo();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL single-building pause {hookName} hook undo failed: {ex}");
            }

            try
            {
                current.Dispose();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL single-building pause {hookName} hook disposal failed: {ex}");
            }
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            pausePacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SingleBuildingPausePacket>();
            pausePacketSubscription = pausePacketHook.GetBaseHook().Observable.Subscribe(OnPausePacketReceived);
            buildingDeleteSubscription = BuildingR3EventHooks.OnBuildingDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingDeleting);
            networkInitialized = true;
            LogInfo($"Chore packet registered eagerly: packetId={pausePacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void ClearOverrides(string reason)
        {
            ClearManualSleepOverrides();
        }

        internal void SetSleepStateBridge(
            Action synchronizer,
            Action<bool> setInterceptionEnabled)
        {
            synchronizeSleepStates = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
            setSleepOverrideInterceptionEnabled =
                setInterceptionEnabled ?? throw new ArgumentNullException(nameof(setInterceptionEnabled));
        }

        internal unsafe bool TryResolveManualOverrideForSleepingAddress(
            IntPtr sleepingAddress,
            out ManualSleepOverrideMatch match)
        {
            match = default;
            if (sleepingAddress == IntPtr.Zero)
                return false;

            if (!overrides.TryGetBySleepingAddress(sleepingAddress, out SingleBuildingPauseOverride entry))
                return false;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(entry.BuildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive ||
                (IntPtr)(&building->r_IsSleeping) != sleepingAddress ||
                (int)building->r_GlobalId != entry.GlobalId ||
                building->r_PlayerIdOwner != entry.Owner ||
                building->r_BuildingType != entry.BuildingType)
            {
                bool becameEmpty = overrides.Remove(entry.BuildingId);
                if (becameEmpty)
                {
                    // This resolver runs inside the native hook. Never rewrite that hook's
                    // target bytes until the callback has returned to the engine.
                    UnityMainThreadDispatcher.EnqueueStatic(DeactivateOverrideHooksIfEmpty);
                }
                return false;
            }

            match = new ManualSleepOverrideMatch
            {
                BuildingId = entry.BuildingId,
                IsSleeping = entry.IsSleeping,
                BuildingType = building->r_BuildingType,
                Owner = building->r_PlayerIdOwner,
                CurrentSleeping = building->r_IsSleeping
            };
            return true;
        }

        private static MethodInfo FindButtonToggleZzzModeMethod()
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                "ButtonToggleZZZMode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonToggleZZZMode");

            return method;
        }

        private static MethodInfo FindAddChimpActionsMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                "addChimpActions",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(EngineInterface.PlayState),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                    typeof(bool)
                },
                null);

            if (method == null || method.ReturnType != typeof(bool))
                throw new MissingMethodException(typeof(FatControler).FullName, "addChimpActions");

            return method;
        }

        private void ButtonToggleZzzModeHook(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            bool controlPressed = IsControlPressed();
            bool mapEditor = IsMapEditor();
            bool ownedByControlledPlayer = !mapEditor || IsSelectedBuildingOwnedByControlledPlayer(selectedBuildingId);

            if (!IsFeatureActive() ||
                !ownedByControlledPlayer)
            {
                if (mapEditor && !ownedByControlledPlayer)
                {
                    LogInfo($"single-building pause editor action delegated to Vanilla: selectedBuildingId={selectedBuildingId}, activePlayerId={GetControlledPlayerId()}, reason=selected-building-not-owned.");
                }
                buttonTrampoline(self, parameter);
                return;
            }

            if (mapEditor)
            {
                LogInfo($"single-building pause editor action accepted: selectedBuildingId={selectedBuildingId}, activePlayerId={GetControlledPlayerId()}, controlPressed={controlPressed}.");
            }

            if (!controlPressed)
            {
                if (IsRecentManualToggle(selectedBuildingId))
                    return;

                if (RequiresChoreTransport())
                {
                    try
                    {
                        ToggleSelectedBuildingTypeMultiplayer(self, parameter);
                    }
                    catch (Exception ex)
                    {
                        LogError($"building-type sleep toggle failed: {ex}");
                    }
                    return;
                }

                ToggleSelectedBuildingTypeFromSelectedState(self, parameter);
                return;
            }

            try
            {
                ToggleSelectedBuildingOnly(self);
            }
            catch (Exception ex)
            {
                LogError($"single-building pause failed: {ex}");
            }
        }

        private bool AddChimpActionsHook(
            FatControler self,
            EngineInterface.PlayState state,
            ref string line1,
            ref string line2,
            bool islamic)
        {
            bool result = addChimpActionsTrampoline(self, state, ref line1, ref line2, islamic);

            try
            {
                if (state != null &&
                    state.in_structure > 0 &&
                    overrides.TryGet(state.in_structure, out SingleBuildingPauseOverride entry))
                {
                    UpdateSleepButtonVisibility(MainViewModel.Instance, entry.IsSleeping);
                }
            }
            catch (Exception ex)
            {
                if (!uiRefreshFailureLogged)
                {
                    uiRefreshFailureLogged = true;
                    LogError($"single-building pause UI correction failed; Vanilla visibility remains active: {ex}");
                }
            }

            return result;
        }

        private bool IsFeatureActive()
        {
            return settings.EnableMod &&
                settings.EnableSingleBuildingPause &&
                (!RequiresChoreTransport() || IsChoreTransportReady());
        }

        private unsafe void ToggleSelectedBuildingOnly(MainViewModel self)
        {
            int buildingId = TryGetSelectedBuildingId();
            if (buildingId <= 0)
                return;

            if (IsDuplicateManualToggle(buildingId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building))
                return;

            bool hasOverride = TryGetManualSleepOverride(buildingId, out bool overrideSleeping);
            bool wasSleeping = hasOverride ? overrideSleeping : building->r_IsSleeping == 1;
            bool targetSleeping = !wasSleeping;

            if (RequiresChoreTransport())
            {
                int globalId = (int)building->r_GlobalId;
                int playerId = GetControlledPlayerId();
                if (globalId <= 0 || building->r_PlayerIdOwner != playerId)
                {
                    LogError($"single-building pause refused because the selected building has no valid synchronized identity: buildingId={buildingId}, globalId={globalId}, owner={building->r_PlayerIdOwner}, localPlayer={playerId}.");
                    return;
                }

                if (TrySendPauseChore(playerId, globalId, targetSleeping, SetSingleBuildingAction, false))
                    MarkManualToggle(buildingId);
                return;
            }

            if (!SetManualSleepOverride(buildingId, targetSleeping))
                return;

            // Do not write r_IsSleeping directly. The native sleep-state sync must
            // observe the state change so it can run the game's worker reset and
            // reassignment bookkeeping for this building.
            synchronizeSleepStates?.Invoke();
            UpdateSleepButtonVisibility(self, targetSleeping);
            MarkManualToggle(buildingId);
        }

        private bool RequiresChoreTransport()
        {
            return multiplayerFeatureGate.BlocksLocalStateChanges;
        }

        private bool IsChoreTransportReady()
        {
            return BugfixesAndQoLChoreSender.IsAvailable(
                networkInitialized && pausePacketHook != null,
                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);
        }

        private unsafe void ToggleSelectedBuildingTypeMultiplayer(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            if (selectedBuildingId <= 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
                return;

            bool selectedHasOverride = TryGetManualSleepOverride(selectedBuildingId, out bool overrideSleeping);
            bool selectedWasSleeping = selectedHasOverride ? overrideSleeping : selectedBuilding->r_IsSleeping == 1;
            bool targetSleeping = !selectedWasSleeping;
            bool buildingTypeWasSleeping = GameData.Instance.lastGameState.building_type_sleeping != 0;
            int playerId = GetControlledPlayerId();
            int globalId = (int)selectedBuilding->r_GlobalId;
            if (globalId <= 0 || selectedBuilding->r_PlayerIdOwner != playerId)
            {
                LogError($"building-type sleep toggle refused because the selected building has no valid synchronized identity: buildingId={selectedBuildingId}, globalId={globalId}, owner={selectedBuilding->r_PlayerIdOwner}, localPlayer={playerId}.");
                return;
            }

            // Queue the override reset first. If Vanilla's type state must also change, its native
            // GameAction is queued second so every peer observes the same two-step order.
            bool needsVanillaTypeToggle = buildingTypeWasSleeping != targetSleeping;
            if (!TrySendPauseChore(
                    playerId,
                    globalId,
                    targetSleeping,
                    ResetBuildingTypeAction,
                    synchronizeAfterReset: !needsVanillaTypeToggle))
                return;

            if (needsVanillaTypeToggle)
                buttonTrampoline(self, parameter);

            UpdateSleepButtonVisibility(self, targetSleeping);
        }

        private bool TrySendPauseChore(
            int playerId,
            int buildingGlobalId,
            bool targetSleeping,
            int action,
            bool synchronizeAfterReset)
        {
            if (!IsChoreTransportReady())
            {
                LogError("single-building pause refused in multiplayer because the Chore transport is unavailable.");
                return false;
            }

            int operationId = unchecked(++nextOperationId);
            var packet = new SingleBuildingPausePacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                PlayerId = playerId,
                OperationId = operationId,
                BuildingGlobalId = buildingGlobalId,
                TargetSleeping = targetSleeping,
                Action = action,
                SynchronizeAfterReset = synchronizeAfterReset
            };
            short packetId = pausePacketHook?.GetPacketId() ?? (short)0;
            if (!BugfixesAndQoLChoreSender.TrySend(
                    packet,
                    packetId,
                    networkInitialized && pausePacketHook != null,
                    value => GameNetworkAPI.Serialize(value),
                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                    out byte[] body,
                    out string rejectionReason))
            {
                LogError($"single-building pause Chore was not queued; no local action was applied: operationId={operationId}, reason={rejectionReason}.");
                return false;
            }

            LogInfo($"single-building pause Chore queued: operationId={operationId}, action={action}, buildingGlobalId={buildingGlobalId}, targetSleeping={targetSleeping}, synchronizeAfterReset={synchronizeAfterReset}, payloadBytes={sizeof(short) + body.Length}.");
            return true;
        }

        private unsafe void OnPausePacketReceived(ReceiveCustomPacketEventArgs<SingleBuildingPausePacket> args)
        {
            SingleBuildingPausePacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                (packet.Action != SetSingleBuildingAction && packet.Action != ResetBuildingTypeAction) ||
                packet.PlayerId <= 0 || packet.BuildingGlobalId <= 0)
            {
                LogError("rejected a single-building pause Chore with an invalid payload.");
                return;
            }

            // A packet queued just before a synchronized setting change must not
            // recreate overrides after the feature was disabled and cleared.
            if (!settings.EnableMod || !settings.EnableSingleBuildingPause)
                return;

            try
            {
                if (synchronizeSleepStates == null)
                {
                    LogError($"single-building pause Chore cannot execute because the native sleep synchronizer is unavailable: operationId={packet.OperationId}.");
                    return;
                }

                int buildingId = FindAliveBuildingIdByGlobalId(packet.BuildingGlobalId);
                if (buildingId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building->r_PlayerIdOwner != packet.PlayerId)
                {
                    LogError($"single-building pause Chore could not resolve the owned building: operationId={packet.OperationId}, buildingGlobalId={packet.BuildingGlobalId}, playerId={packet.PlayerId}.");
                    return;
                }

                if (packet.Action == ResetBuildingTypeAction)
                {
                    int removedOverrides = ClearManualOverridesForBuildingType(
                        packet.PlayerId,
                        building->r_BuildingType);
                    if (packet.SynchronizeAfterReset)
                        synchronizeSleepStates.Invoke();

                    int selectedBuildingId = TryGetSelectedBuildingId();
                    if (selectedBuildingId > 0 &&
                        GameBuildingManagerAPI.Instance.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding) &&
                        selectedBuilding->r_PlayerIdOwner == packet.PlayerId &&
                        selectedBuilding->r_BuildingType == building->r_BuildingType)
                    {
                        UpdateSleepButtonVisibility(MainViewModel.Instance, packet.TargetSleeping);
                    }
                    LogInfo(
                        $"building-type sleep Chore executed: operationId={packet.OperationId}, " +
                        $"buildingType={building->r_BuildingType}, playerId={packet.PlayerId}, " +
                        $"targetSleeping={packet.TargetSleeping}, synchronizeAfterReset={packet.SynchronizeAfterReset}, " +
                        $"removedOverrides={removedOverrides}.");
                    return;
                }

                if (!SetManualSleepOverride(buildingId, packet.TargetSleeping))
                {
                    LogError($"single-building pause Chore could not store the override: operationId={packet.OperationId}, buildingId={buildingId}.");
                    return;
                }

                synchronizeSleepStates?.Invoke();
                if (TryGetSelectedBuildingId() == buildingId)
                    UpdateSleepButtonVisibility(MainViewModel.Instance, packet.TargetSleeping);
                LogInfo($"single-building pause Chore executed: operationId={packet.OperationId}, action={packet.Action}, buildingId={buildingId}, buildingGlobalId={packet.BuildingGlobalId}, targetSleeping={packet.TargetSleeping}.");
            }
            catch (Exception ex)
            {
                LogError($"single-building pause Chore execution failed: operationId={packet.OperationId}, exception={ex}");
            }
        }

        private static int FindAliveBuildingIdByGlobalId(int globalId)
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_AliveState == AliveState.IsAlive && (int)building.r_GlobalId == globalId)
                    return spanIndex + 1;
            }

            return 0;
        }

        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();

        private static int GetControlledPlayerId()
        {
            if (IsMapEditor())
                return EditorDirector.instance?.ActivePlayerID ?? -1;

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            return localPlayerId > 0 ? localPlayerId : 1;
        }

        private static unsafe bool IsSelectedBuildingOwnedByControlledPlayer(int buildingId)
        {
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) &&
                building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_PlayerIdOwner == GetControlledPlayerId();
        }

        private unsafe void ToggleSelectedBuildingTypeFromSelectedState(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            if (selectedBuildingId <= 0)
            {
                buttonTrampoline(self, parameter);
                return;
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
            {
                buttonTrampoline(self, parameter);
                return;
            }

            bool selectedHasOverride = TryGetManualSleepOverride(selectedBuildingId, out bool overrideSleeping);
            bool selectedWasSleeping = selectedHasOverride ? overrideSleeping : selectedBuilding->r_IsSleeping == 1;
            bool targetSleeping = !selectedWasSleeping;
            bool buildingTypeWasSleeping = GameData.Instance.lastGameState.building_type_sleeping != 0;

            ClearManualOverridesForSelectedBuildingType();

            // If the selected building had an individual override opposite to the
            // type-wide state, clearing that override already produces the desired
            // result. Otherwise let the vanilla GameAction toggle the whole type so
            // every affected building runs the native worker bookkeeping.
            if (buildingTypeWasSleeping != targetSleeping)
                buttonTrampoline(self, parameter);
            else
                synchronizeSleepStates?.Invoke();

            UpdateSleepButtonVisibility(self, targetSleeping);
        }

        private static bool IsControlPressed()
        {
            bool editorCtrl = EditorDirector.instance != null && EditorDirector.instance.ctrlPressed;
            bool keyManagerCtrl = KeyManager.instance != null &&
                (KeyManager.instance.IsKeyHeldDown(KeyCode.LeftControl, true) ||
                 KeyManager.instance.IsKeyHeldDown(KeyCode.RightControl, true));
            return editorCtrl || keyManagerCtrl;
        }

        private static int TryGetSelectedBuildingId()
        {
            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return 0;

            return GameData.Instance.lastGameState.in_structure;
        }

        private unsafe void ClearManualOverridesForSelectedBuildingType()
        {
            if (overrides.Count == 0 ||
                GameData.Instance == null ||
                GameData.Instance.lastGameState == null)
                return;

            int selectedBuildingId = GameData.Instance.lastGameState.in_structure;
            if (selectedBuildingId <= 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
                return;

            ClearManualOverridesForBuildingType(
                selectedBuilding->r_PlayerIdOwner,
                selectedBuilding->r_BuildingType);
        }

        private int ClearManualOverridesForBuildingType(int owner, eStructs buildingType)
        {
            OverrideRemovalResult result = overrides.RemoveForBuildingType(owner, buildingType);
            if (result.BecameEmpty)
                DeactivateOverrideHooks();
            return result.Count;
        }

        private bool IsDuplicateManualToggle(int buildingId)
        {
            return IsRecentManualToggle(buildingId);
        }

        private bool IsRecentManualToggle(int buildingId)
        {
            if (buildingId <= 0)
                return false;

            long now = Stopwatch.GetTimestamp();
            long elapsedMilliseconds = (now - lastManualToggleTimestamp) * 1000 / Stopwatch.Frequency;
            return buildingId == lastManualToggleBuildingId &&
                elapsedMilliseconds >= 0 &&
                elapsedMilliseconds < DuplicateToggleSuppressMilliseconds;
        }

        private void MarkManualToggle(int buildingId)
        {
            lastManualToggleBuildingId = buildingId;
            lastManualToggleTimestamp = Stopwatch.GetTimestamp();
        }

        private unsafe bool SetManualSleepOverride(int buildingId, bool isSleeping)
        {
            if (buildingId <= 0)
                return false;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            if (overrides.Count == 0 && !TryActivateOverrideHooks())
                return false;

            overrides.Set(new SingleBuildingPauseOverride(
                buildingId,
                isSleeping,
                (IntPtr)(&building->r_IsSleeping),
                building->r_BuildingType,
                building->r_PlayerIdOwner,
                (int)building->r_GlobalId));

            return true;
        }

        private bool TryGetManualSleepOverride(int buildingId, out bool isSleeping)
        {
            if (overrides.TryGet(buildingId, out SingleBuildingPauseOverride entry))
            {
                isSleeping = entry.IsSleeping;
                return true;
            }

            isSleeping = false;
            return false;
        }

        private int ClearManualSleepOverrides()
        {
            int count = overrides.Clear();
            DeactivateOverrideHooks();
            return count;
        }

        private void OnBuildingDeleting(BuildingDeleteEventArgs args)
        {
            if (args == null || args.BuildingId <= 0)
                return;

            if (overrides.Remove(args.BuildingId))
            {
                // The event originates in a native detour. Defer code-patch changes
                // until the engine has returned to Unity's main-thread update.
                UnityMainThreadDispatcher.EnqueueStatic(DeactivateOverrideHooksIfEmpty);
            }
        }

        private bool TryActivateOverrideHooks()
        {
            if (overrideHooksActive)
                return true;
            if (setSleepOverrideInterceptionEnabled == null ||
                synchronizeSleepStates == null ||
                !localHooksInstalled ||
                addChimpActionsHook == null)
            {
                LogOverrideHookActivationFailure(
                    "the native bridge or prepared building-action UI hook is unavailable");
                return false;
            }

            try
            {
                // Apply both dependencies before the store transition. If either
                // activation fails, the caller discards the individual pause.
                if (!addChimpActionsHook.IsApplied)
                    addChimpActionsHook.Apply();
                if (!addChimpActionsHook.IsApplied)
                    throw new InvalidOperationException("The building-action UI hook did not become active.");
                setSleepOverrideInterceptionEnabled(true);
                overrideHooksActive = true;
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    setSleepOverrideInterceptionEnabled(false);
                }
                catch (Exception rollbackEx)
                {
                    ex = new AggregateException(ex, rollbackEx);
                }

                try
                {
                    if (addChimpActionsHook.IsApplied)
                        addChimpActionsHook.Undo();
                }
                catch (Exception rollbackEx)
                {
                    ex = new AggregateException(ex, rollbackEx);
                }

                overrideHooksActive = false;
                LogOverrideHookActivationFailure(ex.ToString());
                return false;
            }
        }

        private void LogOverrideHookActivationFailure(string details)
        {
            if (overrideHookActivationFailureLogged)
                return;

            overrideHookActivationFailureLogged = true;
            LogError(
                $"single-building override hooks could not be activated; the action was discarded: {details}");
        }

        private void DeactivateOverrideHooksIfEmpty()
        {
            if (overrides.Count == 0)
                DeactivateOverrideHooks();
        }

        private void DeactivateOverrideHooks()
        {
            Exception firstFailure = null;
            try
            {
                if (overrideHooksActive)
                    setSleepOverrideInterceptionEnabled?.Invoke(false);
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }

            if (firstFailure == null)
            {
                overrideHooksActive = false;
                QueueOverrideUiHookRefresh();
                return;
            }

            if (!overrideHookDeactivationFailureLogged)
            {
                overrideHookDeactivationFailureLogged = true;
                LogError($"single-building override hooks could not be fully deactivated: {firstFailure}");
            }
        }

        private void QueueOverrideUiHookRefresh()
        {
            UnityMainThreadDispatcher.EnqueueStatic(() =>
            {
                if (!localHooksInstalled || addChimpActionsHook == null)
                    return;

                bool shouldApply = overrides.Count > 0;
                try
                {
                    if (shouldApply)
                    {
                        if (!addChimpActionsHook.IsApplied)
                            addChimpActionsHook.Apply();
                    }
                    else if (addChimpActionsHook.IsApplied)
                    {
                        addChimpActionsHook.Undo();
                        if (addChimpActionsHook.IsApplied)
                            throw new InvalidOperationException("The building-action UI hook remained active.");
                    }
                }
                catch (Exception ex)
                {
                    if (shouldApply && !overrideHookActivationFailureLogged)
                    {
                        overrideHookActivationFailureLogged = true;
                        LogError($"single-building pause UI hook could not be activated: {ex}");
                    }
                    else if (!shouldApply && !overrideHookDeactivationFailureLogged)
                    {
                        overrideHookDeactivationFailureLogged = true;
                        LogError($"single-building pause UI hook could not be deactivated: {ex}");
                    }
                }
            });
        }

        private void UpdateSleepButtonVisibility(MainViewModel self, bool isSleeping)
        {
            try
            {
                if (self == null || self.HUDBuildingPanel == null)
                    return;

                if (self.HUDBuildingPanel.RefBuildingZZZButtonOff != null)
                    self.HUDBuildingPanel.RefBuildingZZZButtonOff.Visibility = isSleeping ? (Visibility)2 : (Visibility)1;

                if (self.HUDBuildingPanel.RefBuildingZZZButtonOn != null)
                    self.HUDBuildingPanel.RefBuildingZZZButtonOn.Visibility = isSleeping ? (Visibility)1 : (Visibility)2;
            }
            catch (Exception ex)
            {
                LogError($"single-building pause UI refresh failed: {ex}");
            }
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] Bugfixes and QoL {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        internal struct ManualSleepOverrideMatch
        {
            public int BuildingId;
            public bool IsSleeping;
            public eStructs BuildingType;
            public int Owner;
            public byte CurrentSleeping;
        }
    }
}
