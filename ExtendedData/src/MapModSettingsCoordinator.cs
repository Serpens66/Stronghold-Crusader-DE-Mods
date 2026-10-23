using APIShared.ModSettings;
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using ExtendedData.Core;
using ICSharpCode.SharpZipLib.Zip;
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
        internal event Func<FRONT_Multiplayer, FileHeader, bool> MultiplayerSaveLaunchPreparing;
        internal const string SaveDataIdentifier = "ExtendedData-MapModSettings";
        internal const string ArchiveEntryName = "_SE_ModData_" + SaveDataIdentifier + ".msgpack";
        private const int MaxPayloadBytes = 8 * 1024 * 1024;

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
        private readonly EditorModSettingsSaveOptionsViewModel editorSaveOptions;
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
        private bool pendingMapSaveDecision;
        private bool pendingMapSaveIncludesSettings;
        private bool pendingMapSaveHadEntry;
        private ExtendedDataFeatureActivation activation;
        private bool enabled => activation.MissionModSettings;
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
            ExtendedDataFeatureActivation activation,
            TrailMissionSettingsCoordinator settingsCoordinator,
            EditorModSettingsSaveOptionsViewModel editorSaveOptions)
        {
            this.log = log;
            this.activation = activation;
            this.settingsCoordinator = settingsCoordinator ?? throw new ArgumentNullException(nameof(settingsCoordinator));
            this.editorSaveOptions = editorSaveOptions ?? throw new ArgumentNullException(nameof(editorSaveOptions));
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

            APIShared.Savegames.SaveLoadDialogEvents.Register(ExtendedDataPlugin.PluginGuid, "MapAndLordData", new APIShared.Savegames.SaveLoadDialogCallbacks
            {
                Opened = context =>
                {
                    if (activation.EditorFiles && context.RequesterType == Enums.RequesterTypes.SaveEditorMap)
                    {
                        bool existing = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(ArchiveEntryName, ignoreCase: true) != null;
                        context.State = existing;
                        editorSaveOptions.OpenMap(existing);
                    }
                },
                Prepare = context =>
                {
                    // Lord preparation publishes host data and applies Fixes preferences. It is
                    // not a read-only gate: every contribution must get its veto first.
                    bool multiplayerSave = context.RequesterType == Enums.RequesterTypes.LoadMultiplayerGame ||
                        context.RequesterType == Enums.RequesterTypes.LoadMultiplayerCoopGame;
                    if (activation.LordDataIntegration && multiplayerSave && MultiplayerSaveLaunchPreparing != null &&
                        !MultiplayerSaveLaunchPreparing(MainViewModel.viewModelLoaded ? MainViewModel.Instance.FRONTMultiplayer : null, context.Header))
                        throw new InvalidOperationException("Lord-data save preparation is not ready.");
                    if (activation.EditorFiles && context.RequesterType == Enums.RequesterTypes.SaveEditorMap)
                        ArmMapEditorSave(IOPath.Combine(ConfigSettings.GetUserMapsPath(), context.FileName + ".map"),
                            editorSaveOptions.IncludeMapModSettings, context.State is bool existing && existing);
                },
                AttemptFinished = context =>
                {
                    if (context.RequesterType == Enums.RequesterTypes.SaveEditorMap) ClearMapEditorSaveDecision();
                },
                Closed = (context, outcome) =>
                {
                    if (context.RequesterType == Enums.RequesterTypes.SaveEditorMap) editorSaveOptions.CloseMap();
                }
            });

            R3PacketEventHook<MapModSettingsPacket> packetHook =
                GameNetworkAPI.Instance.GetPacketEventFor<MapModSettingsPacket>();
            packetId = packetHook.GetPacketId();
            subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnMapSettingsPacket));
            settingsCoordinator.LobbyOpened += OnLobbyOpened;

            subscriptions.Add(GameplaySessionLifecycle.SubscribeStarted(log, context =>
            {
                if (!context.IsEditor && IsMapContextCurrent())
                {
                    mapMissionActive = true;
                    ModSettingsApplication.ConfirmStarted();
                }
            }));
            subscriptions.Add(MissionEvents.Ended.Subscribe(notification =>
            {
                if (notification.ReplacementContext?.RestartedFromSessionId == notification.Context.SessionId)
                    return;
                if (mapMissionActive)
                    ExitMapContext(broadcast: false, "mission ended");
            }));

            RegisterLobbyObserver();
            DebugLogHelper.LogDebug(log, "Map mod-settings coordinator initialized.");
        }

        internal void SetActivation(ExtendedDataFeatureActivation value)
        {
            bool changed = activation.MissionModSettings != value.MissionModSettings;
            activation = value;
            if (!changed) return;
            if (!value.MissionModSettings)
                ExitMapContext(broadcast: IsHostLobby(), "feature disabled");
            FRONT_Multiplayer lobby = GetReadyLobby();
            if (lobby != null) OnLobbyOpened(lobby);
        }

        public void Dispose()
        {
            activation = ExtendedDataFeatureActivation.Disabled;
            ClearMapEditorSaveDecision();
            settingsCoordinator.LobbyOpened -= OnLobbyOpened;
            foreach (ListView mapList in observedMapLists.Keys)
                mapList.SelectionChanged -= OnMapListSelectionChanged;
            observedMapLists.Clear();
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            // These hooks were already published. Retain them and pass through while disabled.
            if (saveHandlerRegistered)
            {
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
                saveHandlerRegistered = false;
            }
        }

        private void ArmMapEditorSave(string path, bool includeSettings, bool hadEntry)
        {
            string mapsRoot = IOPath.GetFullPath(ConfigSettings.GetUserMapsPath())
                .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar) +
                IOPath.DirectorySeparatorChar;
            string fullPath = IOPath.GetFullPath(path);
            if (!fullPath.StartsWith(mapsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The confirmed Map target is outside the user Maps directory.");
            pendingMapSaveDecision = true;
            pendingMapSaveIncludesSettings = includeSettings;
            pendingMapSaveHadEntry = hadEntry;
            pendingMapSavePath = fullPath;
            pendingMapSavePayload = null;
        }

        private void ClearMapEditorSaveDecision()
        {
            pendingMapSaveDecision = false;
            pendingMapSaveIncludesSettings = false;
            pendingMapSaveHadEntry = false;
            pendingMapSavePayload = null;
            pendingMapSavePath = null;
        }

        private void LeaveLobbyHook(
            FRONT_Multiplayer self,
            bool doLeaveOnSteam,
            bool refreshLobbyList)
        {
            if (!enabled) { leaveLobbyOriginal(self, doLeaveOnSteam, refreshLobbyList); return; }
            SavegameModSettings.CancelLoadChoice();
            if (mapContextActive && !launchInProgress && !mapMissionActive)
                ExitMapContext(broadcast: IsHostLobby(self), "left lobby");
            leaveLobbyOriginal(self, doLeaveOnSteam, refreshLobbyList);
            if (!launchInProgress && !mapMissionActive && ModSettingsApplication.ContextId.Length == 0)
                ModSettingsApplication.ExitContext();
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            if (!enabled) { startSkirmishGameOriginal(self, restartInfo); return; }
            launchInProgress = IsMapContextCurrent();
            try
            {
                if (launchInProgress && !MissionRestart.IsInProgress)
                {
                    settingsCoordinator.EnterStrict(
                        settingsCoordinator.CaptureCurrentDocument(),
                        editable: false,
                        source: "Map launch working copy",
                        presetLabel: "Map",
                        materializeCurrentValues: true);
                }
                if (!settingsCoordinator.TryPrepareRestartSettings()) return;
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
            if (!activation.EditorFiles || !mapSave || !pendingMapSaveDecision)
            {
                saveOriginal(self, path, mapName, lockMap, tempLockOnly, mapSave);
                return;
            }

            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(pendingMapSavePath) ||
                string.IsNullOrEmpty(normalizedPath) ||
                !string.Equals(pendingMapSavePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:429", "Blocked Map mod-settings save because the confirmed target path did not match [" +
                    pendingMapSavePath + "]: [" + normalizedPath + "].");
                ShowEditorSaveError();
                return;
            }

            byte[] removedEntry = null;
            string targetBackup = null;
            bool targetExisted = File.Exists(normalizedPath);
            try
            {
                if (targetExisted)
                {
                    targetBackup = normalizedPath + ".extendeddata-backup-" + Guid.NewGuid().ToString("N");
                    File.Copy(normalizedPath, targetBackup, overwrite: false);
                }

                if (pendingMapSaveIncludesSettings)
                {
                    ModSettingsDefinition document = settingsCoordinator.CaptureCurrentDocument();
                    string json = ModSettingsJson.Serialize(document);
                    byte[] payload = StrictUtf8.GetBytes(json);
                    if (payload.Length > MaxPayloadBytes)
                        throw new InvalidDataException("Captured Map mod settings exceed the supported payload size.");
                    pendingMapSavePayload = payload;
                    pendingMapSavePath = NormalizePath(path);
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:457", "Captured Map mod settings before save; mentioned=[" +
                            string.Join(", ", document.Mods.Keys) + "].");
                }
                else
                {
                    removedEntry = RemoveActiveMapSettingsEntry(pendingMapSaveHadEntry);
                    DebugLogHelper.LogInfo(log, "Removed Map mod settings for the confirmed editor save.");
                }
                saveOriginal(self, path, mapName, lockMap, tempLockOnly, mapSave);
                VerifySavedMapSettings(normalizedPath, pendingMapSaveIncludesSettings, pendingMapSavePayload);
                TryDeleteTemporaryFile(targetBackup);
                DebugLogHelper.LogInfo(
                    log,
                    pendingMapSaveIncludesSettings
                        ? "Saved and verified Map mod settings."
                        : "Saved and verified Map without ExtendedData mod settings.");
            }
            catch (Exception exception)
            {
                TryRestoreActiveMapSettingsEntry(removedEntry);
                TryRollbackMapFile(normalizedPath, targetBackup, targetExisted);
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:480", "The confirmed Map save was rolled back because its mod-settings state could not be published safely: " +
                    exception);
                ShowEditorSaveError();
            }
        }

        private byte[] SaveMapSettings(SaveContext context)
        {
            if (!activation.EditorFiles || !pendingMapSaveDecision || !pendingMapSaveIncludesSettings ||
                context == null || !context.IsMapEditorSave || context.IsSaveFile ||
                pendingMapSavePayload == null)
            {
                return null;
            }

            string contextPath = NormalizePath(context.FilePath);
            if (!string.IsNullOrEmpty(pendingMapSavePath) &&
                !string.IsNullOrEmpty(contextPath) &&
                !string.Equals(pendingMapSavePath, contextPath, StringComparison.OrdinalIgnoreCase))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:502", "Refused to write Map mod settings because the save path changed from [" +
                    pendingMapSavePath + "] to [" + contextPath + "].");
                return null;
            }
            return (byte[])pendingMapSavePayload.Clone();
        }

        private static byte[] RemoveActiveMapSettingsEntry(bool entryWasExpected)
        {
            MapArchive mapArchive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            ZipFile archive = mapArchive?.Archive;
            if (archive == null)
            {
                if (entryWasExpected)
                    throw new InvalidOperationException("The active Map archive disappeared before its mod settings could be removed.");
                return null;
            }

            int index = archive.FindEntry(ArchiveEntryName, ignoreCase: true);
            if (index < 0)
            {
                if (entryWasExpected)
                    throw new InvalidDataException("The Map mod-settings entry disappeared before saving.");
                return null;
            }

            byte[] previous = mapArchive.TryReadBinaryFile(ArchiveEntryName, ignoreCase: true);
            if (previous == null)
                throw new InvalidDataException("The existing Map mod-settings entry could not be read before removal.");
            string actualName = archive[index].Name;
            if (!archive.IsUpdating)
                archive.BeginUpdate();
            archive.Delete(actualName);
            return previous;
        }

        private static void VerifySavedMapSettings(string path, bool included, byte[] expectedPayload)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The game did not create the expected Map file.", path);
            if (!MapArchive.TryLoad(path, out MapArchive archive))
                throw new InvalidDataException("The saved Map archive could not be reopened for verification.");
            using (archive)
            {
                byte[] actual = archive.TryReadBinaryFile(ArchiveEntryName, ignoreCase: true);
                if (!included)
                {
                    if (actual != null)
                        throw new InvalidDataException("The saved Map still contains ExtendedData mod settings.");
                    return;
                }
                if (expectedPayload == null || actual == null || !actual.SequenceEqual(expectedPayload))
                    throw new InvalidDataException("The saved Map does not contain the expected mod-settings payload.");
            }
        }

        private static void TryRestoreActiveMapSettingsEntry(byte[] previous)
        {
            if (previous == null)
                return;
            try
            {
                GameMapArchiveManagerAPI.Instance.TryWriteBinaryFile(
                    ArchiveEntryName,
                    previous,
                    ignoreCase: true,
                    overwrite: true);
            }
            catch
            {
                // The disk rollback below remains authoritative; this only restores the live editor archive.
            }
        }

        private static void TryRollbackMapFile(string path, string backup, bool targetExisted)
        {
            try
            {
                if (targetExisted && !string.IsNullOrEmpty(backup) && File.Exists(backup))
                    File.Copy(backup, path, overwrite: true);
                else if (!targetExisted && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The caller reports the original publication failure; rollback is best effort.
            }
            TryDeleteTemporaryFile(backup);
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // A leftover backup is safer than turning a successful Map save into a rollback.
            }
        }

        private static void ShowEditorSaveError()
        {
            HUD_ConfirmationPopup.ShowOK(
                SerpLocalization.Get("EditorSave.ModSettingsSaveFailed"),
                delegate { });
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
            OnMapSelectionChanged(lobby);
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
            if (enabled && selected != null &&
                TryReadDocument(selected, out ModSettingsDefinition document, out _, logFailure: false))
            {
                settingsCoordinator.SetMapSourceDocument(document, selected.filePath);
                if (HasLocalAuthority(lobby) && !settingsCoordinator.IsContextActive("Trail") && !lobby.trailMakerMode)
                    ApplySelectedMap(lobby);
            }
            else
            {
                settingsCoordinator.SetMapSourceDocument(null);
            }
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
                settingsCoordinator.SetMapSourceDocument(null);
                return;
            }

            string[] missing;
            try
            {
                settingsCoordinator.SetMapSourceDocument(document, selected.filePath);
                missing = settingsCoordinator.EnterStrict(
                document,
                editable: true,
                source: "selected Map",
                presetLabel: "Map");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:700", "Could not apply Map mod settings: " + exception);
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
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:712", "Applied Map mod settings from [" + selected.filePath + "]; mentioned=[" +
                    string.Join(", ", document.Mods.Keys) + "].");
            if (missing.Length != 0)
            {
                ShowMessage(
                    SerpLocalization.Get("ExtendedData.MapModSettingsMissingTitle"),
                    SerpLocalization.Get("ExtendedData.MapModSettingsMissing") + " " + string.Join(", ", missing));
            }
            if (IsHostLobby(lobby))
                BroadcastCurrentState(apply: true);
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
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:757", "Could not read Map mod settings from [" + header.filePath + "]: " + exception);
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
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:784", "Cleared Map mod-settings context: " + reason + ".");
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
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:813", "Could not process Map mod-settings packet: " + exception);
            }
        }

        private void ProcessMapSettingsPacketCore(MapModSettingsPacket packet, CSteamID sender)
        {
            CSteamID? host = GameNetworkAPI.GetHostSteamId();
            if (!host.HasValue || sender != host.Value)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:822", "Rejected Map mod-settings packet from a sender that is not the lobby host.");
                return;
            }
            FRONT_Multiplayer lobby = GetReadyLobby();
            if (!enabled || lobby?.currentLobby == null || lobby.currentLobby.isHost || lobby.singlePlayerCoop ||
                packet == null || packet.ProtocolVersion != MapModSettingsPacket.CurrentProtocolVersion)
            {
                return;
            }
            if (!packet.Apply)
            {
                if (!MatchesLobby(packet, lobby) && !MatchesActiveContext(packet))
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:835", "Ignored Map mod-settings clear for an unrelated map.");
                    return;
                }
                ExitMapContext(broadcast: false, "authenticated host clear");
                return;
            }
            if (!MatchesLobby(packet, lobby))
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:843", "Ignored Map mod-settings packet for a different lobby map.");
                return;
            }
            if (string.IsNullOrEmpty(packet.Json) ||
                StrictUtf8.GetByteCount(packet.Json) > MaxPayloadBytes)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:849", "Rejected invalid Map mod-settings packet payload size.");
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
                settingsCoordinator.SetMapSourceDocument(document, packet.MapFileName + ":" + packet.MapCrc);
                string[] missing = settingsCoordinator.EnterStrict(
                    document,
                    editable: true,
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
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:880", "Applied authenticated host Map mod settings.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:884", "Rejected invalid authenticated Map mod settings: " + exception);
            }
        }

        private void BroadcastCurrentState(
            bool apply,
            string mapFileName = null,
            uint? mapCrc = null)
        {
            FRONT_Multiplayer lobby = GetReadyLobby();
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
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/MapModSettingsCoordinator.cs:916", "Broadcast Map mod-settings " + (apply ? "apply" : "clear") + ".");
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
            lobby = lobby ?? GetReadyLobby();
            return lobby?.currentLobby != null && !lobby.singlePlayerCoop && lobby.currentLobby.isHost;
        }

        private static FRONT_Multiplayer GetReadyLobby() =>
            MainViewModel.viewModelLoaded ? MainViewModel.Instance?.FRONTMultiplayer : null;

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
