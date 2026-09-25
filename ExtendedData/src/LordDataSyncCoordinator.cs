using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.SaveData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtendedData
{
    internal sealed class LordDataSyncCoordinator : IDisposable
    {
        internal const string SaveIdentifier = "ExtendedData-LordData";
        internal const string SaveEntry = "_SE_ModData_" + SaveIdentifier + ".msgpack";
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly ManualLogSource log;
        private readonly ExtendedDataSettingsViewModel settings;
        private readonly FixesLordPreferencesBridge fixes = new FixesLordPreferencesBridge();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private LordDataSnapshot active;
        private ulong? lobbyId;
        private string error = string.Empty;
        private bool saveRegistered;

        internal LordDataSyncCoordinator(ManualLogSource log, ExtendedDataSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        internal void Initialize()
        {
            settings.LordDataSnapshotChanged += OnSnapshotChanged;
            settings.LordDataLobbyChanged += OnLobbyChanged;
            saveRegistered = ModSaveDataAPI.Instance.RegisterModDataHandler(
                SaveIdentifier, SaveSnapshot, LoadSnapshot);
            if (!saveRegistered)
                throw new InvalidOperationException("Lord-data save identifier is already registered.");
            subscriptions.Add(MissionEvents.Initialization.Subscribe(notification =>
            {
                if (active == null)
                    return;
                if (notification.Context.Mode.IsRealMultiplayer)
                {
                    fixes.Apply(active);
                    ExtendedDataModDataApi.SetNetworkSnapshot(active, true);
                }
                else
                {
                    fixes.Restore();
                    active = null;
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
                }
            }));
            subscriptions.Add(MissionEvents.Ended.Subscribe(_ =>
            {
                fixes.Restore();
                ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
            }));
        }

        internal void OnLobbyOpened(FRONT_Multiplayer lobby)
        {
            if (IsHostLobby(lobby))
                RefreshHost(lobby);
            else if (lobby?.currentLobby != null && !string.IsNullOrEmpty(settings.LordDataSnapshot))
                OnSnapshotChanged(settings.LordDataSnapshot);
        }

        internal bool RefreshHost(FRONT_Multiplayer lobby)
        {
            if (!IsHostLobby(lobby))
                return true;
            try
            {
                LordDataSnapshot next = CaptureLobby(lobby);
                Publish(next);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                settings.LordDataStatus = "ERROR|" + error;
                DebugLogHelper.LogError(log, "Lord-data host capture failed: " + exception);
                return false;
            }
        }

        internal bool PrepareSave(FRONT_Multiplayer lobby, FileHeader header)
        {
            if (!IsHostLobby(lobby))
                return true;
            try
            {
                if (header == null || string.IsNullOrWhiteSpace(header.filePath))
                    throw new InvalidDataException("The selected multiplayer save has no file path.");
                LordDataSnapshot saved = null;
                if (MapArchive.TryLoad(header.filePath, out MapArchive archive))
                {
                    using (archive)
                    {
                        byte[] bytes = archive.TryReadBinaryFile(SaveEntry, ignoreCase: true);
                        if (bytes != null)
                            saved = LordDataSnapshot.Parse(StrictUtf8.GetString(bytes));
                    }
                }
                if (saved == null)
                    saved = CaptureLegacySave(lobby, header);
                if (saved.FixesInstalled != fixes.Installed)
                    throw new InvalidDataException("The installed Fixes state differs from the saved Lord data.");
                ValidateSavedLords(header, saved);
                Publish(LordDataSnapshot.Create(CurrentSessionId(lobby), saved.FixesInstalled, saved.Slots));
                bool ready = IsReadyToLaunch(lobby, out _);
                return ready;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                settings.LordDataStatus = "ERROR|" + error;
                DebugLogHelper.LogError(log, "Lord-data save preparation failed: " + exception);
                return false;
            }
        }

        internal bool IsReadyToLaunch(FRONT_Multiplayer lobby, out string reason)
        {
            reason = error;
            if (!IsHostLobby(lobby))
                return true;
            if (active == null || !string.Equals(active.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
            {
                reason = "The host Lord-data snapshot is not ready.";
                return false;
            }
            if (!string.IsNullOrEmpty(error))
                return false;
            if (!PlayerIdentityHelper.TryCaptureHumanRoster(false, true,
                out Dictionary<int, ulong> players, out string rosterError, out _))
            {
                reason = "The multiplayer roster is unresolved: " + rosterError;
                return false;
            }
            if (!settings.System_ArePerPlayerSettingsReady(players.Keys, out string reportError))
            {
                reason = "Player settings are incomplete: " + reportError;
                return false;
            }
            string expected = "READY|" + active.Digest;
            foreach (int playerId in players.Keys)
            {
                string status = playerId > 0 && playerId < settings.LordDataStatusData.Length
                    ? settings.LordDataStatusData[playerId] : null;
                if (playerId == GameNetworkAPI.GetLocalPlayerId())
                    status = settings.LordDataStatus;
                if (!string.Equals(status, expected, StringComparison.Ordinal))
                {
                    reason = "Player " + playerId + " has not applied the selected Lord data.";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }


        private LordDataSnapshot CaptureLobby(FRONT_Multiplayer lobby)
        {
            var slots = new List<LordDataSlot>();
            if (lobby.AIVs == null)
                throw new InvalidDataException("Lobby Lord selection is unavailable.");
            for (int index = 0; index < Math.Min(8, lobby.AIVs.Length); index++)
            {
                FRONT_Multiplayer.MPAIVInfo selected = lobby.AIVs[index];
                if (selected == null || selected.builtInLord || string.IsNullOrWhiteSpace(selected.lordName))
                    continue;
                slots.Add(CaptureSlot(index + 1, selected.lordName, selected.lordConfig));
            }
            return LordDataSnapshot.Create(CurrentSessionId(lobby), fixes.Installed, slots);
        }

        private LordDataSlot CaptureSlot(int playerId, string lordName,
            CustomisationFileManager.CustomLordConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.name) ||
                string.IsNullOrWhiteSpace(config.path))
                throw new InvalidDataException("Selected Lord " + lordName + " has no local configuration on the host.");
            string path = Path.Combine(config.path, config.name + ".modlord.json");
            string modLordJson = null;
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length > LordDataSnapshot.MaxSidecarBytes)
                    throw new InvalidDataException(path + " exceeds 64 KiB.");
                modLordJson = StrictUtf8.GetString(bytes).TrimStart('\uFEFF');
                LordDataSnapshot.ValidateModLord(modLordJson);
            }
            RejectUnsyncedGameplayFiles(config.path);
            return new LordDataSlot
            {
                PlayerId = playerId,
                LordName = lordName,
                ConfigName = config.name,
                ConfigChecksum = config.checksum.ToString(),
                ModLordJson = modLordJson,
                FixesJson = fixes.Capture(lordName),
            };
        }

        private static void RejectUnsyncedGameplayFiles(string root)
        {
            string info = Path.Combine(root, "info.json");
            if (!File.Exists(info))
                return;
            var document = Shared.DependencyFreeJson.Parse(File.ReadAllText(info)) as Dictionary<string, object>;
            if (document == null || !document.TryGetValue("NetworkMode", out object mode) || Convert.ToInt32(mode) != 0)
                return;
            if (Directory.EnumerateFiles(root, "*.lua", SearchOption.AllDirectories).Any())
                throw new InvalidDataException("Selected NetworkMode=0 Lord contains unsynchronized Lua gameplay files: " + root);
            if (Directory.EnumerateFiles(root, "*.sema", SearchOption.AllDirectories).Any())
                throw new InvalidDataException("Selected NetworkMode=0 Lord contains unsynchronized MapAreas data: " + root);
            string overrides = Path.Combine(root, "Override");
            if (Directory.Exists(overrides) && Directory.EnumerateFiles(overrides, "*.json", SearchOption.AllDirectories)
                .Any(path => !path.EndsWith(Path.Combine("Fixes", "preferences.json"), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Selected NetworkMode=0 Lord contains other unsynchronized Override JSON files: " + root);
        }

        private LordDataSnapshot CaptureLegacySave(FRONT_Multiplayer lobby, FileHeader header)
        {
            FileHeader detailed = MapFileManager.Instance.GetFileInfoFromFileName(
                header.filePath, header.filePath, 0, loadRestartInfo: true);
            string[] names = detailed?.restartMPInfo?.LordNames;
            if (names == null || names.Length != 8)
                throw new InvalidDataException("Legacy save has no usable Lord identities.");
            var slots = new List<LordDataSlot>();
            for (int index = 0; index < names.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(names[index]))
                    continue;
                if (lobby.AIVs != null && index < lobby.AIVs.Length &&
                    lobby.AIVs[index]?.builtInLord == true &&
                    string.Equals(lobby.AIVs[index].lordName, names[index], StringComparison.OrdinalIgnoreCase))
                    continue;
                List<CustomisationFileManager.CustomLordConfig> configs =
                    CustomisationFileManager.Instance.getLordLordList(-1, names[index]);
                if (configs == null || configs.Count != 1)
                    throw new InvalidDataException("Legacy save Lord configuration is ambiguous or missing: " + names[index]);
                slots.Add(CaptureSlot(index + 1, names[index], configs[0]));
            }
            return LordDataSnapshot.Create(CurrentSessionId(lobby), fixes.Installed, slots);
        }

        private static void ValidateSavedLords(FileHeader header, LordDataSnapshot snapshot)
        {
            FileHeader detailed = MapFileManager.Instance.GetFileInfoFromFileName(
                header.filePath, header.filePath, 0, loadRestartInfo: true);
            string[] names = detailed?.restartMPInfo?.LordNames;
            if (names == null || names.Length != 8 || snapshot.Slots.Any(slot =>
                !string.Equals(names[slot.PlayerId - 1], slot.LordName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Saved Lord-data identities do not match the selected multiplayer save.");
        }

        private void Publish(LordDataSnapshot snapshot)
        {
            fixes.Apply(snapshot);
            error = string.Empty;
            active = snapshot;
            ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
            settings.LordDataStatus = "READY|" + snapshot.Digest;
            if (!string.Equals(settings.LordDataSnapshot, snapshot.WireJson, StringComparison.Ordinal))
                settings.LordDataSnapshot = snapshot.WireJson;
        }

        private void OnSnapshotChanged(string wireJson)
        {
            if (GameNetworkAPI.IsLocalHost())
                return;
            UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    LordDataSnapshot snapshot = LordDataSnapshot.Parse(wireJson);
                    FRONT_Multiplayer lobby = MainViewModel.Instance?.FRONTMultiplayer;
                    if (lobby?.currentLobby == null ||
                        !string.Equals(snapshot.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
                        throw new InvalidDataException("Lord-data snapshot belongs to another lobby.");
                    fixes.Apply(snapshot);
                    active = snapshot;
                    error = string.Empty;
                    ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
                    settings.LordDataStatus = "READY|" + snapshot.Digest;
                }
                catch (Exception exception)
                {
                    active = null;
                    error = exception.Message;
                    settings.LordDataStatus = "ERROR|" + error;
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, true);
                    DebugLogHelper.LogError(log, "Rejected host Lord-data snapshot: " + exception);
                }
            });
        }

        private byte[] SaveSnapshot(SaveContext context) =>
            context.IsSaveFile && active != null ? StrictUtf8.GetBytes(active.WireJson) : null;

        private void LoadSnapshot(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile || bytes == null)
                return;
            try
            {
                LordDataSnapshot.Parse(StrictUtf8.GetString(bytes));
                if (active == null)
                {
                    // Save data is not an authenticated replacement for the lobby host.
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, true);
                    DebugLogHelper.LogError(log, "Lord data was loaded without an acknowledged host snapshot.");
                }
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Invalid Lord data in save: " + exception);
            }
        }

        private void OnLobbyChanged(PerPlayerLobbySnapshot snapshot)
        {
            if (snapshot?.LobbyId == lobbyId)
                return;
            lobbyId = snapshot?.LobbyId;
            fixes.Restore();
            active = null;
            error = string.Empty;
            ExtendedDataModDataApi.SetNetworkSnapshot(null, snapshot?.LobbyId != null);
            settings.LordDataStatus = string.Empty;
            if (snapshot?.LobbyId != null)
                OnLobbyOpened(MainViewModel.Instance?.FRONTMultiplayer);
        }

        private static bool IsHostLobby(FRONT_Multiplayer lobby) =>
            lobby?.currentLobby != null && !lobby.singlePlayerCoop &&
            lobby.currentLobby.isHost && Shared.GameModeHelper.IsRealMultiplayer();

        private static string CurrentSessionId(FRONT_Multiplayer lobby) =>
            lobby.currentLobby.id.m_SteamID.ToString();

        public void Dispose()
        {
            settings.LordDataSnapshotChanged -= OnSnapshotChanged;
            settings.LordDataLobbyChanged -= OnLobbyChanged;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveRegistered)
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveIdentifier);
            fixes.Restore();
        }
    }
}
