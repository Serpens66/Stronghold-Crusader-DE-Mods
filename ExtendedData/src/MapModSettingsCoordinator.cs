using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using ExtendedData.Core;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.SaveData;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IOPath = System.IO.Path;

namespace ExtendedData
{
    internal sealed class MapModSettingsCoordinator : IDisposable
    {
        internal const string SaveDataIdentifier = "ExtendedData-MapModSettings";
        internal const string ArchiveEntryName = "_SE_ModData_" + SaveDataIdentifier + ".msgpack";
        internal const string UseMapCommand = "ExtendedDataUseMapModSettings";
        private const int MaxPayloadBytes = 1024 * 1024;

        private delegate void SaveSaveGameOrMapDelegate(
            EditorDirector self,
            string path,
            string mapName,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave);

        private delegate void LeaveLobbyDelegate(
            FRONT_Multiplayer self,
            bool doLeaveOnSteam,
            bool refreshLobbyList);

        private delegate void StartSkirmishGameDelegate(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly ManualLogSource log;
        private readonly TrailMissionSettingsCoordinator settingsCoordinator;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<ListView, FRONT_Multiplayer> observedMapLists =
            new Dictionary<ListView, FRONT_Multiplayer>();
        private Hook saveHook;
        private Hook leaveLobbyHook;
        private Hook startSkirmishGameHook;
        private SaveSaveGameOrMapDelegate saveOriginal;
        private LeaveLobbyDelegate leaveLobbyOriginal;
        private StartSkirmishGameDelegate startSkirmishGameOriginal;
        private byte[] pendingMapSavePayload;
        private string pendingMapSavePath;
        private bool enabled;
        private bool mapContextActive;
        private bool mapMissionActive;
        private bool launchInProgress;
        private string activeMapFileName = string.Empty;
        private string activeMapPath = string.Empty;
        private uint activeMapCrc;
        private string activeJson = string.Empty;
        private short packetId;
        private bool saveHandlerRegistered;
        private ulong? lastLobbyId;
        private string lastRosterSignature = string.Empty;

        internal MapModSettingsCoordinator(
            ManualLogSource log,
            bool enabled,
            TrailMissionSettingsCoordinator settingsCoordinator)
        {
            this.log = log;
            this.enabled = enabled;
            this.settingsCoordinator = settingsCoordinator ?? throw new ArgumentNullException(nameof(settingsCoordinator));
        }

        internal void Initialize()
        {
            MethodInfo saveMethod = RequireInstanceMethod(
                typeof(EditorDirector),
                nameof(EditorDirector.SaveSaveGameOrMap),
                typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool));
            MethodInfo leaveLobbyMethod = RequireInstanceMethod(
                typeof(FRONT_Multiplayer),
                "LeaveLobby",
                typeof(bool), typeof(bool));
            MethodInfo startSkirmishGameMethod = RequireInstanceMethod(
                typeof(FRONT_Multiplayer),
                "StartSkirmishGame",
                typeof(HUD_IngameMenu.RestartSkirmishMapInfo));

            bool registered = ModSaveDataAPI.Instance.RegisterModDataHandler(
                SaveDataIdentifier,
                SaveMapSettings,
                (_, __) => { });
            if (!registered)
                throw new InvalidOperationException("The Map mod-settings save-data identifier is already registered.");
            saveHandlerRegistered = true;

            saveHook = new Hook(saveMethod, (SaveSaveGameOrMapDelegate)SaveSaveGameOrMapHook);
            saveOriginal = saveHook.GenerateTrampoline<SaveSaveGameOrMapDelegate>();

            leaveLobbyHook = new Hook(leaveLobbyMethod, (LeaveLobbyDelegate)LeaveLobbyHook);
            leaveLobbyOriginal = leaveLobbyHook.GenerateTrampoline<LeaveLobbyDelegate>();

            startSkirmishGameHook = new Hook(
                startSkirmishGameMethod,
                (StartSkirmishGameDelegate)StartSkirmishGameHook);
            startSkirmishGameOriginal = startSkirmishGameHook.GenerateTrampoline<StartSkirmishGameDelegate>();

            R3PacketEventHook<MapModSettingsPacket> packetHook =
                GameNetworkAPI.Instance.GetPacketEventFor<MapModSettingsPacket>();
            packetId = packetHook.GetPacketId();
            subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnMapSettingsPacket));
            settingsCoordinator.LobbyOpened += OnLobbyOpened;

            subscriptions.Add(GameplaySessionLifecycle.SubscribeStarted(log, context =>
            {
                if (!context.IsEditor && IsMapContextCurrent())
                    mapMissionActive = true;
            }));
            subscriptions.Add(MissionEvents.Ended.Subscribe(_ =>
            {
                if (mapMissionActive)
                    ExitMapContext(broadcast: false, "mission ended");
            }));

            RegisterLobbyObserver();
            DebugLogHelper.LogInfo(log, "Map mod-settings coordinator initialized.");
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            if (!value)
                ExitMapContext(broadcast: IsHostLobby(), "feature disabled");
            FRONT_Multiplayer lobby = MainViewModel.Instance?.FRONTMultiplayer;
            if (lobby != null)
                OnLobbyOpened(lobby);
        }

        internal bool TryHandleCommand(FRONT_Multiplayer lobby, string command)
        {
            if (string.Equals(command, UseMapCommand, StringComparison.Ordinal))
            {
                if (enabled)
                    ApplySelectedMap(lobby);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            settingsCoordinator.LobbyOpened -= OnLobbyOpened;
            foreach (ListView mapList in observedMapLists.Keys)
                mapList.SelectionChanged -= OnMapListSelectionChanged;
            observedMapLists.Clear();
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            startSkirmishGameHook?.Dispose();
            leaveLobbyHook?.Dispose();
            saveHook?.Dispose();
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
        }

        private void LeaveLobbyHook(
            FRONT_Multiplayer self,
            bool doLeaveOnSteam,
            bool refreshLobbyList)
        {
            if (mapContextActive && !launchInProgress && !mapMissionActive)
                ExitMapContext(broadcast: IsHostLobby(self), "left lobby");
            leaveLobbyOriginal(self, doLeaveOnSteam, refreshLobbyList);
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            launchInProgress = IsMapContextCurrent();
            try
            {
                startSkirmishGameOriginal(self, restartInfo);
            }
            finally
            {
                launchInProgress = false;
            }
        }

        private void SaveSaveGameOrMapHook(
            EditorDirector self,
            string path,
            string mapName,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave)
        {
            pendingMapSavePayload = null;
            pendingMapSavePath = null;
            if (enabled && mapSave)
            {
                try
                {
                    ModSettingsDefinition document = settingsCoordinator.CaptureCurrentDocument();
                    string json = ModSettingsJson.Serialize(document);
                    byte[] payload = StrictUtf8.GetBytes(json);
                    if (payload.Length > MaxPayloadBytes)
                        throw new InvalidDataException("Captured Map mod settings exceed the supported payload size.");
                    pendingMapSavePayload = payload;
                    pendingMapSavePath = NormalizePath(path);
                    DebugLogHelper.LogInfo(
                        log,
                        "Captured Map mod settings before save; mentioned=[" +
                        string.Join(", ", document.Mods.Keys) + "].");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Could not capture Map mod settings before saving [" + path +
                        "]; the existing embedded settings will be preserved: " + exception);
                }
            }

            try
            {
                saveOriginal(self, path, mapName, lockMap, tempLockOnly, mapSave);
            }
            finally
            {
                pendingMapSavePayload = null;
                pendingMapSavePath = null;
            }
        }

        private byte[] SaveMapSettings(SaveContext context)
        {
            if (!enabled || context == null || !context.IsMapEditorSave || context.IsSaveFile ||
                pendingMapSavePayload == null)
            {
                return null;
            }

            string contextPath = NormalizePath(context.FilePath);
            if (!string.IsNullOrEmpty(pendingMapSavePath) &&
                !string.IsNullOrEmpty(contextPath) &&
                !string.Equals(pendingMapSavePath, contextPath, StringComparison.OrdinalIgnoreCase))
            {
                DebugLogHelper.LogError(
                    log,
                    "Refused to write Map mod settings because the save path changed from [" +
                    pendingMapSavePath + "] to [" + contextPath + "].");
                return null;
            }
            return (byte[])pendingMapSavePayload.Clone();
        }

        private void OnLobbyOpened(FRONT_Multiplayer lobby)
        {
            if (lobby == null)
                return;
            ListView mapList = lobby.FindName("MapList") as ListView;
            if (mapList != null && !observedMapLists.ContainsKey(mapList))
            {
                observedMapLists.Add(mapList, lobby);
                mapList.SelectionChanged += OnMapListSelectionChanged;
            }
            RefreshButton(lobby);
        }

        private void OnMapListSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ListView mapList && observedMapLists.TryGetValue(mapList, out FRONT_Multiplayer lobby))
                OnMapSelectionChanged(lobby);
        }

        private void OnMapSelectionChanged(FRONT_Multiplayer lobby)
        {
            FileHeader selected = GetSelectedHeader(lobby);
            // Vanilla clears the selection transiently while rebuilding the same map list.
            // Only a new, concrete map identity counts as a map change.
            if (mapContextActive && selected != null && !MatchesActiveMap(selected))
                ExitMapContext(broadcast: IsHostLobby(lobby), "selected map changed");
            RefreshButton(lobby);
        }

        internal bool IsActiveForLobby(FRONT_Multiplayer lobby)
        {
            if (!IsMapContextCurrent() || lobby == null)
                return false;
            FileHeader selected = GetSelectedHeader(lobby);
            if (selected != null)
                return MatchesActiveMap(selected);
            if (lobby.currentLobby != null)
            {
                return string.Equals(
                        activeMapFileName,
                        lobby.currentLobby.mapFileName,
                        StringComparison.OrdinalIgnoreCase) &&
                    activeMapCrc == unchecked((uint)EditorDirector.getIntFromString(lobby.currentLobby.crc));
            }
            return false;
        }

        private void RefreshButton(FRONT_Multiplayer lobby)
        {
            Button button = lobby?.FindName("ExtendedDataUseMapModSettings") as Button;
            if (button == null)
                return;

            FileHeader selected = GetSelectedHeader(lobby);
            bool activeForSelection = IsMapContextCurrent() && MatchesActiveMap(selected);
            PropEx.SetTextCentre(
                button,
                SerpLocalization.Get(activeForSelection
                    ? "ExtendedData.MapModSettingsActive"
                    : "ExtendedData.UseMapModSettings"));
            button.ToolTip = SerpLocalization.Get("ExtendedData.UseMapModSettingsHelp");
            bool authority = HasLocalAuthority(lobby) && !lobby.trailMakerMode;
            button.Visibility = enabled && authority ? Visibility.Visible : Visibility.Collapsed;
            if (button.Visibility != Visibility.Visible)
            {
                button.IsEnabled = false;
                return;
            }

            button.IsEnabled = selected != null && TryReadDocument(selected, out _, out _, logFailure: false);
        }

        private void ApplySelectedMap(FRONT_Multiplayer lobby)
        {
            if (!HasLocalAuthority(lobby) || lobby.trailMakerMode)
                return;

            FileHeader selected = GetSelectedHeader(lobby);
            if (selected == null || !TryReadDocument(selected, out ModSettingsDefinition document, out string json, logFailure: true))
            {
                ShowMessage(
                    SerpLocalization.Get("ExtendedData.MapModSettingsErrorTitle"),
                    SerpLocalization.Get("ExtendedData.MapModSettingsUnavailable"));
                RefreshButton(lobby);
                return;
            }

            string[] missing;
            try
            {
                missing = settingsCoordinator.EnterStrict(
                document,
                editable: false,
                source: "selected Map",
                presetLabel: "Map");
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Could not apply Map mod settings: " + exception);
                ShowMessage(
                    SerpLocalization.Get("ExtendedData.MapModSettingsErrorTitle"),
                    SerpLocalization.Get("ExtendedData.MapModSettingsUnavailable"));
                return;
            }
            mapContextActive = true;
            mapMissionActive = false;
            activeMapFileName = GetLobbyMapName(lobby, selected);
            activeMapPath = NormalizePath(selected.filePath);
            activeMapCrc = selected.crc;
            activeJson = json;
            DebugLogHelper.LogInfo(
                log,
                "Applied Map mod settings from [" + selected.filePath + "]; mentioned=[" +
                string.Join(", ", document.Mods.Keys) + "].");
            if (missing.Length != 0)
            {
                ShowMessage(
                    SerpLocalization.Get("ExtendedData.MapModSettingsMissingTitle"),
                    SerpLocalization.Get("ExtendedData.MapModSettingsMissing") + " " + string.Join(", ", missing));
            }
            if (IsHostLobby(lobby))
                BroadcastCurrentState(apply: true);
            RefreshButton(lobby);
        }

        private bool TryReadDocument(
            FileHeader header,
            out ModSettingsDefinition document,
            out string json,
            bool logFailure)
        {
            document = null;
            json = null;
            if (header == null || string.IsNullOrWhiteSpace(header.filePath))
                return false;
            try
            {
                if (!MapArchive.TryLoad(header.filePath, out MapArchive archive))
                    return false;
                using (archive)
                {
                    byte[] bytes = archive.TryReadBinaryFile(ArchiveEntryName, ignoreCase: true);
                    if (bytes == null || bytes.Length == 0 || bytes.Length > MaxPayloadBytes)
                        return false;
                    json = StrictUtf8.GetString(bytes);
                    if (json.Length > 0 && json[0] == '\uFEFF')
                        json = json.Substring(1);
                    document = ModSettingsJson.ParseObject(json);
                    document = settingsCoordinator.ValidateStrict(document, "embedded Map");
                    return true;
                }
            }
            catch (Exception exception)
            {
                if (logFailure)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Could not read Map mod settings from [" + header.filePath + "]: " + exception);
                }
                return false;
            }
        }

        private void ExitMapContext(bool broadcast, string reason)
        {
            bool wasCurrent = IsMapContextCurrent();
            string clearedMapFileName = activeMapFileName;
            uint clearedMapCrc = activeMapCrc;
            mapContextActive = false;
            mapMissionActive = false;
            launchInProgress = false;
            activeMapFileName = string.Empty;
            activeMapPath = string.Empty;
            activeMapCrc = 0;
            activeJson = string.Empty;
            if (wasCurrent)
                settingsCoordinator.ExitContext(force: true);
            if (broadcast)
                BroadcastCurrentState(
                    apply: false,
                    mapFileName: clearedMapFileName,
                    mapCrc: clearedMapCrc);
            DebugLogHelper.LogInfo(log, "Cleared Map mod-settings context: " + reason + ".");
        }

        private void OnMapSettingsPacket(ReceiveCustomPacketEventArgs<MapModSettingsPacket> args)
        {
            MapModSettingsPacket source = args?.Packet;
            if (source == null || !args.SenderSteamId.HasValue)
                return;
            var packet = new MapModSettingsPacket
            {
                ProtocolVersion = source.ProtocolVersion,
                Apply = source.Apply,
                MapFileName = source.MapFileName,
                MapCrc = source.MapCrc,
                Json = source.Json,
            };
            ulong sender = args.SenderSteamId.Value.m_SteamID;
            UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => ProcessMapSettingsPacket(packet, new CSteamID(sender)));
        }

        private void ProcessMapSettingsPacket(MapModSettingsPacket packet, CSteamID sender)
        {
            try
            {
                ProcessMapSettingsPacketCore(packet, sender);
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Could not process Map mod-settings packet: " + exception);
            }
        }

        private void ProcessMapSettingsPacketCore(MapModSettingsPacket packet, CSteamID sender)
        {
            CSteamID? host = GameNetworkAPI.GetHostSteamId();
            if (!host.HasValue || sender != host.Value)
            {
                DebugLogHelper.LogError(log, "Rejected Map mod-settings packet from a sender that is not the lobby host.");
                return;
            }
            FRONT_Multiplayer lobby = MainViewModel.Instance?.FRONTMultiplayer;
            if (!enabled || lobby?.currentLobby == null || lobby.currentLobby.isHost || lobby.singlePlayerCoop ||
                packet == null || packet.ProtocolVersion != MapModSettingsPacket.CurrentProtocolVersion)
            {
                return;
            }
            if (!packet.Apply)
            {
                if (!MatchesLobby(packet, lobby) && !MatchesActiveContext(packet))
                {
                    DebugLogHelper.LogWarning(log, "Ignored Map mod-settings clear for an unrelated map.");
                    return;
                }
                ExitMapContext(broadcast: false, "authenticated host clear");
                return;
            }
            if (!MatchesLobby(packet, lobby))
            {
                DebugLogHelper.LogWarning(log, "Ignored Map mod-settings packet for a different lobby map.");
                return;
            }
            if (string.IsNullOrEmpty(packet.Json) ||
                StrictUtf8.GetByteCount(packet.Json) > MaxPayloadBytes)
            {
                DebugLogHelper.LogError(log, "Rejected invalid Map mod-settings packet payload size.");
                return;
            }
            if (IsMapContextCurrent() && packet.MapCrc == activeMapCrc &&
                string.Equals(packet.MapFileName, activeMapFileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(packet.Json, activeJson, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                ModSettingsDefinition document = ModSettingsJson.ParseObject(packet.Json ?? string.Empty);
                string[] missing = settingsCoordinator.EnterStrict(
                    document,
                    editable: false,
                    source: "authenticated host Map",
                    presetLabel: "Map");
                mapContextActive = true;
                mapMissionActive = false;
                activeMapFileName = packet.MapFileName;
                activeMapPath = string.Empty;
                activeMapCrc = packet.MapCrc;
                activeJson = packet.Json;
                if (missing.Length != 0)
                {
                    ShowMessage(
                        SerpLocalization.Get("ExtendedData.MapModSettingsMissingTitle"),
                        SerpLocalization.Get("ExtendedData.MapModSettingsMissing") + " " + string.Join(", ", missing));
                }
                DebugLogHelper.LogInfo(log, "Applied authenticated host Map mod settings.");
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Rejected invalid authenticated Map mod settings: " + exception);
            }
        }

        private void BroadcastCurrentState(
            bool apply,
            string mapFileName = null,
            uint? mapCrc = null)
        {
            FRONT_Multiplayer lobby = MainViewModel.Instance?.FRONTMultiplayer;
            if (!IsHostLobby(lobby))
                return;
            var packet = new MapModSettingsPacket
            {
                ProtocolVersion = MapModSettingsPacket.CurrentProtocolVersion,
                Apply = apply,
                MapFileName = apply
                    ? activeMapFileName
                    : mapFileName ?? lobby.currentLobby.mapFileName,
                MapCrc = apply
                    ? activeMapCrc
                    : mapCrc ?? unchecked((uint)EditorDirector.getIntFromString(lobby.currentLobby.crc)),
                Json = apply ? activeJson : string.Empty,
            };
            byte[] bytes = MessagePackSerializer.Serialize(packet);
            GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
            {
                packetType = packetId,
                data = bytes,
                dataLength = bytes.Length,
                dataOffset = 0,
            });
            DebugLogHelper.LogInfo(log, "Broadcast Map mod-settings " + (apply ? "apply" : "clear") + ".");
        }

        private void RegisterLobbyObserver()
        {
            IApiShared api = APIShared.ApiShared.Current;
            if (!api.TryGetLobbyState(
                    ExtendedDataPlugin.PluginGuid,
                    out ILobbyStateCapability lobbyState,
                    out NativeCapabilityDiagnostic diagnostic) ||
                !lobbyState.TryRegisterObserver(
                    "map-mod-settings",
                    OnLobbyStateChanged,
                    out diagnostic))
            {
                throw new InvalidOperationException(
                    "The Map mod-settings lobby observer is unavailable: state=" + diagnostic?.State +
                    ", reason=" + diagnostic?.Reason);
            }
        }

        private void OnLobbyStateChanged(LobbyStateSnapshot snapshot)
        {
            string roster = snapshot == null
                ? string.Empty
                : string.Join(",", snapshot.Players.OrderBy(item => item.Key).Select(item => item.Key + ":" + item.Value));
            bool changed = snapshot?.LobbyId != lastLobbyId ||
                !string.Equals(roster, lastRosterSignature, StringComparison.Ordinal);
            lastLobbyId = snapshot?.LobbyId;
            lastRosterSignature = roster;
            if (changed && IsMapContextCurrent() && IsHostLobby())
                BroadcastCurrentState(apply: true);
        }

        private bool MatchesLobby(MapModSettingsPacket packet, FRONT_Multiplayer lobby) =>
            string.Equals(packet.MapFileName, lobby.currentLobby.mapFileName, StringComparison.OrdinalIgnoreCase) &&
            packet.MapCrc == unchecked((uint)EditorDirector.getIntFromString(lobby.currentLobby.crc));

        private bool MatchesActiveContext(MapModSettingsPacket packet) =>
            mapContextActive &&
            string.Equals(packet.MapFileName, activeMapFileName, StringComparison.OrdinalIgnoreCase) &&
            packet.MapCrc == activeMapCrc;

        private bool MatchesActiveMap(FileHeader header)
        {
            if (header == null)
                return false;
            if (!string.IsNullOrEmpty(activeMapPath))
                return string.Equals(activeMapPath, NormalizePath(header.filePath), StringComparison.OrdinalIgnoreCase) &&
                    activeMapCrc == header.crc;
            string fileName = string.IsNullOrWhiteSpace(header.fileName)
                ? IOPath.GetFileName(header.filePath)
                : header.fileName;
            return activeMapCrc == header.crc &&
                string.Equals(activeMapFileName, fileName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMapContextCurrent() =>
            mapContextActive && settingsCoordinator.IsContextActive("Map");

        private static FileHeader GetSelectedHeader(FRONT_Multiplayer lobby)
        {
            ListView list = lobby?.FindName("MapList") as ListView;
            return (list?.SelectedItem as FileRow)?.fileHeader;
        }

        private static bool HasLocalAuthority(FRONT_Multiplayer lobby)
        {
            if (lobby == null)
                return false;
            if (lobby.currentLobby == null || lobby.singlePlayerCoop)
                return FRONT_Multiplayer.skirmishGame || lobby.singlePlayerCoop;
            return lobby.currentLobby.isHost;
        }

        private static bool IsHostLobby(FRONT_Multiplayer lobby = null)
        {
            lobby = lobby ?? MainViewModel.Instance?.FRONTMultiplayer;
            return lobby?.currentLobby != null && !lobby.singlePlayerCoop && lobby.currentLobby.isHost;
        }

        private static string GetLobbyMapName(FRONT_Multiplayer lobby, FileHeader header) =>
            lobby?.currentLobby == null || string.IsNullOrWhiteSpace(lobby.currentLobby.mapFileName)
                ? IOPath.GetFileName(header.filePath)
                : lobby.currentLobby.mapFileName;

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try { return IOPath.GetFullPath(path); }
            catch { return path; }
        }

        private static MethodInfo RequireInstanceMethod(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method != null)
                return method;
            throw new MissingMethodException(
                type.FullName,
                name + "(" + string.Join(", ", parameterTypes.Select(item => item.FullName)) + ")");
        }

        private static void ShowMessage(string title, string message) =>
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(title, delegate { }, message);
    }
}
