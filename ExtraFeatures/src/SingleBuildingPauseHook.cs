// Feature: Ctrl-click to pause only the selected production building.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ExtraFeatures
{
    internal sealed class SingleBuildingPauseHook : IDisposable
    {
        private delegate void ButtonToggleZzzModeDelegate(MainViewModel self, object parameter);
        private delegate void NoesisGuiUpdateChecksInGameDelegate(FatControler self);

        private static readonly bool EnablePeriodicManualSleepOverrideRestore = false;
        private const long DuplicateToggleSuppressMilliseconds = 750;
        private const int ChoreProtocolVersion = 2;
        private const int SetSingleBuildingAction = 1;
        private const int ResetBuildingTypeAction = 2;
        private static readonly object ManualSleepOverridesLock = new object();
        private static readonly Dictionary<int, ManualSleepOverride> ManualSleepOverrides = new Dictionary<int, ManualSleepOverride>();
        private static readonly Dictionary<IntPtr, int> ManualSleepOverrideIdsBySleepingAddress = new Dictionary<IntPtr, int>();

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly Hook buttonHook;
        private readonly Hook guiUpdateHook;
        private readonly ButtonToggleZzzModeDelegate buttonTrampoline;
        private readonly NoesisGuiUpdateChecksInGameDelegate guiUpdateTrampoline;
        private int lastManualToggleBuildingId;
        private long lastManualToggleTimestamp;
        private Action synchronizeSleepStates;
        private bool disposed;
        private bool networkInitialized;
        private int nextOperationId;
        private R3PacketEventHook<SingleBuildingPausePacket> pausePacketHook;
        private IDisposable pausePacketSubscription;

        public SingleBuildingPauseHook(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ?? throw new ArgumentNullException(nameof(multiplayerFeatureGate));

            MethodInfo buttonMethod = FindButtonToggleZzzModeMethod();
            MethodInfo guiUpdateMethod = FindNoesisGuiUpdateChecksInGameMethod();
            Hook installedButtonHook = null;
            Hook installedGuiUpdateHook = null;
            try
            {
                installedButtonHook = new Hook(buttonMethod, (ButtonToggleZzzModeDelegate)ButtonToggleZzzModeHook);
                ButtonToggleZzzModeDelegate installedButtonTrampoline = installedButtonHook.GenerateTrampoline<ButtonToggleZzzModeDelegate>();

                installedGuiUpdateHook = new Hook(guiUpdateMethod, (NoesisGuiUpdateChecksInGameDelegate)NoesisGuiUpdateChecksInGameHook);
                NoesisGuiUpdateChecksInGameDelegate installedGuiUpdateTrampoline = installedGuiUpdateHook.GenerateTrampoline<NoesisGuiUpdateChecksInGameDelegate>();

                buttonHook = installedButtonHook;
                buttonTrampoline = installedButtonTrampoline;
                guiUpdateHook = installedGuiUpdateHook;
                guiUpdateTrampoline = installedGuiUpdateTrampoline;
            }
            catch
            {
                installedGuiUpdateHook?.Dispose();
                installedButtonHook?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            buttonHook?.Undo();
            buttonHook?.Dispose();
            guiUpdateHook?.Undo();
            guiUpdateHook?.Dispose();
            ClearManualSleepOverrides();
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            pausePacketHook = GameNetworkAPI.Instance.GetPacketEventFor<SingleBuildingPausePacket>();
            pausePacketSubscription = pausePacketHook.GetBaseHook().Observable.Subscribe(OnPausePacketReceived);
            networkInitialized = true;
            LogInfo($"Chore packet registered eagerly: packetId={pausePacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
        }

        public void ClearOverrides(string reason)
        {
            ClearManualSleepOverrides();
        }

        internal void SetSleepStateSynchronizer(Action synchronizer)
        {
            synchronizeSleepStates = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        }

        internal unsafe static bool TryResolveManualOverrideForSleepingAddress(IntPtr sleepingAddress, out ManualSleepOverrideMatch match)
        {
            match = default;
            if (sleepingAddress == IntPtr.Zero)
                return false;

            ManualSleepOverride entry;
            lock (ManualSleepOverridesLock)
            {
                if (!ManualSleepOverrideIdsBySleepingAddress.TryGetValue(sleepingAddress, out int buildingId) ||
                    !ManualSleepOverrides.TryGetValue(buildingId, out entry))
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(sleepingAddress);
                    return false;
                }

                if (entry.SleepingAddress != sleepingAddress)
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(sleepingAddress);
                    return false;
                }
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(entry.BuildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive ||
                (IntPtr)(&building->r_IsSleeping) != sleepingAddress)
            {
                RemoveManualSleepOverride(entry.BuildingId);
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

        private static MethodInfo FindNoesisGuiUpdateChecksInGameMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                "NoesisGUIUpdateChecksInGame",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                throw new MissingMethodException(typeof(FatControler).FullName, "NoesisGUIUpdateChecksInGame");

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

        private void NoesisGuiUpdateChecksInGameHook(FatControler self)
        {
            guiUpdateTrampoline(self);

            if (!IsFeatureActive())
                return;

            try
            {
                if (EnablePeriodicManualSleepOverrideRestore)
                    ApplyManualSleepOverrides();

                RefreshSelectedBuildingSleepButton();
            }
            catch (Exception ex)
            {
                LogError($"single-building pause update failed: {ex}");
            }
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
            return networkInitialized && pausePacketHook != null && ChoreNetworkTransport.IsAvailable;
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
            byte[] body = GameNetworkAPI.Serialize(packet);
            byte[] blob = new byte[sizeof(short) + body.Length];
            BitConverter.GetBytes(pausePacketHook.GetPacketId()).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                LogError($"single-building pause Chore was not queued; no local action was applied: operationId={operationId}, payloadBytes={blob.Length}.");
                return false;
            }

            LogInfo($"single-building pause Chore queued: operationId={operationId}, action={action}, buildingGlobalId={buildingGlobalId}, targetSleeping={targetSleeping}, synchronizeAfterReset={synchronizeAfterReset}, payloadBytes={blob.Length}.");
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
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_AliveState == AliveState.IsAlive && (int)building.r_GlobalId == globalId)
                    return index + 1;
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
            if (GetManualSleepOverrideCount() == 0 ||
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

        private static int ClearManualOverridesForBuildingType(int owner, eStructs buildingType)
        {
            List<int> idsToRemove = new List<int>();

            lock (ManualSleepOverridesLock)
            {
                foreach (ManualSleepOverride entry in ManualSleepOverrides.Values)
                {
                    if (entry.Owner == owner && entry.BuildingType == buildingType)
                        idsToRemove.Add(entry.BuildingId);
                }

                foreach (int buildingId in idsToRemove)
                    RemoveManualSleepOverrideUnsafe(buildingId);
            }

            return idsToRemove.Count;
        }

        private unsafe void ApplyManualSleepOverrides()
        {
            if (GetManualSleepOverrideCount() == 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            List<int> idsToRemove = null;
            List<ManualSleepOverride> overrides;

            lock (ManualSleepOverridesLock)
                overrides = new List<ManualSleepOverride>(ManualSleepOverrides.Values);

            foreach (ManualSleepOverride entry in overrides)
            {
                if (!buildingApi.TryGetBuildingById(entry.BuildingId, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.IsAlive)
                {
                    if (idsToRemove == null)
                        idsToRemove = new List<int>();

                    idsToRemove.Add(entry.BuildingId);
                    continue;
                }

                byte desired = (byte)(entry.IsSleeping ? 1 : 0);
                if (building->r_IsSleeping == desired)
                    continue;

                building->r_IsSleeping = desired;
            }

            if (idsToRemove == null)
                return;

            lock (ManualSleepOverridesLock)
            {
                foreach (int buildingId in idsToRemove)
                    RemoveManualSleepOverrideUnsafe(buildingId);
            }

        }

        private void RefreshSelectedBuildingSleepButton()
        {
            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return;

            int selectedBuildingId = GameData.Instance.lastGameState.in_structure;
            if (selectedBuildingId <= 0)
                return;

            if (TryGetManualSleepOverride(selectedBuildingId, out bool isSleeping))
                UpdateSleepButtonVisibility(MainViewModel.Instance, isSleeping);
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

        private static int GetManualSleepOverrideCount()
        {
            lock (ManualSleepOverridesLock)
                return ManualSleepOverrides.Count;
        }

        private unsafe static bool SetManualSleepOverride(int buildingId, bool isSleeping)
        {
            if (buildingId <= 0)
                return false;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            ManualSleepOverride entry = new ManualSleepOverride
            {
                BuildingId = buildingId,
                IsSleeping = isSleeping,
                SleepingAddress = (IntPtr)(&building->r_IsSleeping),
                BuildingType = building->r_BuildingType,
                Owner = building->r_PlayerIdOwner
            };

            lock (ManualSleepOverridesLock)
            {
                if (ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride oldEntry) &&
                    ManualSleepOverrideIdsBySleepingAddress.TryGetValue(oldEntry.SleepingAddress, out int oldBuildingId) &&
                    oldBuildingId == buildingId)
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(oldEntry.SleepingAddress);
                }

                ManualSleepOverrides[buildingId] = entry;
                ManualSleepOverrideIdsBySleepingAddress[entry.SleepingAddress] = buildingId;
            }

            return true;
        }

        private static bool TryGetManualSleepOverride(int buildingId, out bool isSleeping)
        {
            lock (ManualSleepOverridesLock)
            {
                if (ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride entry))
                {
                    isSleeping = entry.IsSleeping;
                    return true;
                }
            }

            isSleeping = false;
            return false;
        }

        private static int ClearManualSleepOverrides()
        {
            lock (ManualSleepOverridesLock)
            {
                int count = ManualSleepOverrides.Count;
                ManualSleepOverrides.Clear();
                ManualSleepOverrideIdsBySleepingAddress.Clear();
                return count;
            }
        }

        private static void RemoveManualSleepOverride(int buildingId)
        {
            lock (ManualSleepOverridesLock)
                RemoveManualSleepOverrideUnsafe(buildingId);
        }

        private static void RemoveManualSleepOverrideUnsafe(int buildingId)
        {
            if (!ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride entry))
                return;

            ManualSleepOverrides.Remove(buildingId);
            if (ManualSleepOverrideIdsBySleepingAddress.TryGetValue(entry.SleepingAddress, out int indexedBuildingId) &&
                indexedBuildingId == buildingId)
            {
                ManualSleepOverrideIdsBySleepingAddress.Remove(entry.SleepingAddress);
            }
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
            log.LogError($"[{TimestampNow()}] Extra Features {message}");
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private struct ManualSleepOverride
        {
            public int BuildingId;
            public bool IsSleeping;
            public IntPtr SleepingAddress;
            public eStructs BuildingType;
            public int Owner;
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
