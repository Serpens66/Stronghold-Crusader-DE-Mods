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
        private string lastHostGateDiagnostic;
        private string lastHostCaptureDiagnostic;
        private string lastRemoteStatusDiagnostic;
        private string lastMapTransitionDiagnostic;
        private string lastMapAppliedDiagnostic;
        private string lastClientReceivedDiagnostic;
        private string lastClientAcceptedDiagnostic;
        private string lastHostEchoDiagnostic;
        private int[] lobbyHumanSlots = Array.Empty<int>();
        private int lobbyLocalPlayerId;

        internal LordDataSyncCoordinator(ManualLogSource log, ExtendedDataSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
        }

        internal void Initialize()
        {
            settings.LordDataSnapshotChanged += OnSnapshotChanged;
            settings.LordDataLobbyChanged += OnLobbyChanged;
            settings.LordDataRemoteStatusChanged += OnRemoteStatusChanged;
            settings.LordDataSnapshotMutationRejected += OnSnapshotMutationRejected;
            settings.LordDataLocalStatusChanged += OnLocalStatusChanged;
            saveRegistered = ModSaveDataAPI.Instance.RegisterModDataHandler(
                SaveIdentifier, SaveSnapshot, LoadSnapshot);
            if (!saveRegistered)
                throw new InvalidOperationException("Lord-data save identifier is already registered.");
            subscriptions.Add(MissionEvents.Initialization.Subscribe(notification =>
            {
                if (notification.Context.Mode.IsRealMultiplayer || active != null)
                    LogMapTransition(notification.Context.Mode.IsRealMultiplayer);
                if (active == null)
                    return;
                if (notification.Context.Mode.IsRealMultiplayer)
                {
                    try
                    {
                        fixes.Apply(active);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, "Lord-data map initialization failed: stage=fixes-apply," +
                            "digest=" + active.Digest + ",error=" + exception);
                        throw;
                    }
                    ExtendedDataModDataApi.SetNetworkSnapshot(active, true);
                    if (!string.Equals(lastMapAppliedDiagnostic, active.Digest, StringComparison.Ordinal))
                    {
                        lastMapAppliedDiagnostic = active.Digest;
                        DebugLogHelper.LogInfo(log, "Lord-data map initialization applied: " +
                            LordDataSyncDiagnostics.DescribeSnapshot(active));
                        LogEffectiveFixes(active, "map-initialization");
                    }
                }
                else
                {
                    fixes.Restore();
                    active = null;
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
                    DebugLogHelper.LogInfo(log, "Lord-data snapshot cleared for single-player map initialization.");
                }
            }));
            subscriptions.Add(MissionEvents.Ended.Subscribe(_ =>
            {
                bool hadLordSession = active != null || lobbyId.HasValue;
                fixes.Restore();
                ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
                lastMapTransitionDiagnostic = null;
                lastMapAppliedDiagnostic = null;
                if (hadLordSession)
                    DebugLogHelper.LogInfo(log, "Lord-data map ended; session Fixes preferences restored.");
            }));
        }

        internal void OnLobbyOpened(FRONT_Multiplayer lobby)
            => OnLobbyOpened(lobby, "lobby-opened");

        private void OnLobbyOpened(FRONT_Multiplayer lobby, string source)
        {
            if (IsHostLobby(lobby))
                RefreshHost(lobby, source);
            else if (lobby?.currentLobby != null && !string.IsNullOrEmpty(settings.LordDataSnapshot))
            {
                DebugLogHelper.LogInfo(log, "Lord-data existing host setting observed on lobby open: source=" +
                    source + ",wire=" + LordDataSyncDiagnostics.DescribeJson(settings.LordDataSnapshot, false));
                OnSnapshotChanged(settings.LordDataSnapshot);
            }
            else
                LogHostGateSkip(lobby, source);
        }

        internal bool RefreshHost(FRONT_Multiplayer lobby, string source)
        {
            if (!IsHostLobby(lobby))
            {
                LogHostGateSkip(lobby, source);
                return true;
            }
            string stage = "capture";
            try
            {
                LordDataSnapshot next = CaptureLobby(lobby);
                string signature = next.SessionId + ":" + next.Digest;
                if (!string.Equals(signature, lastHostCaptureDiagnostic, StringComparison.Ordinal) ||
                    string.Equals(source, "start-attempt", StringComparison.Ordinal))
                    DebugLogHelper.LogInfo(log, "Lord-data host capture: source=" + source + "," +
                        LordDataSyncDiagnostics.DescribeSnapshot(next));
                lastHostCaptureDiagnostic = signature;
                stage = "publish";
                Publish(next, source);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                settings.LordDataStatus = "ERROR|" + error;
                DebugLogHelper.LogError(log, "Lord-data host capture failed: source=" + source +
                    ",lobby=" + lobbyId + ",stage=" + stage + ",error=" + exception);
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
                bool reconstructed = saved == null;
                if (saved == null)
                    saved = CaptureLegacySave(lobby, header);
                if (saved.FixesInstalled != fixes.Installed)
                    throw new InvalidDataException("The installed Fixes state differs from the saved Lord data.");
                ValidateSavedLords(header, saved);
                Publish(LordDataSnapshot.Create(CurrentSessionId(lobby), saved.FixesInstalled, saved.Slots),
                    reconstructed ? "legacy-save" : "multiplayer-save");
                bool ready = IsReadyToLaunch(lobby, out string reason);
                LogStartDecision(true, ready, reason);
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

        internal void LogStartAttempt(string command, bool runtimeEnabled, FRONT_Multiplayer lobby)
        {
            DebugLogHelper.LogInfo(log, "Lord-data start hook reached: command=" +
                LordDataSyncDiagnostics.SafeLabel(command) +
                ",runtimeEnabled=" + runtimeEnabled + "," + DescribeHostGate(lobby) +
                "," + DescribeAcknowledgements(lobbyHumanSlots));
        }

        internal void LogStartDecision(bool captured, bool ready, string reason)
        {
            DebugLogHelper.LogInfo(log, "Lord-data start decision: captureSucceeded=" + captured +
                ",ready=" + ready + ",reason=" + (string.IsNullOrEmpty(reason) ? "none" : reason) +
                "," + DescribeAcknowledgements(lobbyHumanSlots));
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

        private void Publish(LordDataSnapshot snapshot, string source)
        {
            bool changed = active == null || !string.Equals(active.Digest, snapshot.Digest, StringComparison.Ordinal);
            fixes.Apply(snapshot);
            if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
            {
                DebugLogHelper.LogInfo(log, "Lord-data host Fixes values applied: source=" + source +
                    ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                    ",selectedLords=" + snapshot.Slots.Count);
                LogEffectiveFixes(snapshot, "host-" + source);
            }
            error = string.Empty;
            active = snapshot;
            ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
            settings.LordDataStatus = "READY|" + snapshot.Digest;
            if (!string.Equals(settings.LordDataSnapshot, snapshot.WireJson, StringComparison.Ordinal))
                settings.LordDataSnapshot = snapshot.WireJson;
            if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
                DebugLogHelper.LogInfo(log, "Lord-data host publication: source=" + source +
                    ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                    ",snapshotSettingMatches=" + string.Equals(settings.LordDataSnapshot,
                        snapshot.WireJson, StringComparison.Ordinal) +
                    ",localStatus=" + LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, snapshot.Digest));
        }

        private void OnSnapshotChanged(string wireJson)
        {
            if (GameNetworkAPI.IsLocalHost())
            {
                string signature = LordDataSyncDiagnostics.Hash(wireJson);
                if (!string.Equals(signature, lastHostEchoDiagnostic, StringComparison.Ordinal))
                {
                    lastHostEchoDiagnostic = signature;
                    DebugLogHelper.LogInfo(log, "Lord-data setting callback ignored as local host echo: lobby=" +
                        lobbyId + ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
                }
                return;
            }
            string receivedSignature = lobbyId + ":" + LordDataSyncDiagnostics.Hash(wireJson);
            if (!string.Equals(receivedSignature, lastClientReceivedDiagnostic, StringComparison.Ordinal))
            {
                lastClientReceivedDiagnostic = receivedSignature;
                DebugLogHelper.LogInfo(log, "Lord-data host setting received: lobby=" + lobbyId +
                    ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
            }
            UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                string stage = "parse";
                try
                {
                    LordDataSnapshot snapshot = LordDataSnapshot.Parse(wireJson);
                    bool newlyAccepted = !string.Equals(snapshot.Digest, lastClientAcceptedDiagnostic,
                        StringComparison.Ordinal);
                    if (newlyAccepted)
                        DebugLogHelper.LogInfo(log, "Lord-data snapshot parsed: " +
                            LordDataSyncDiagnostics.DescribeSnapshot(snapshot));
                    stage = "session-check";
                    FRONT_Multiplayer lobby = MainViewModel.Instance?.FRONTMultiplayer;
                    if (lobby?.currentLobby == null ||
                        !string.Equals(snapshot.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
                        throw new InvalidDataException("Lord-data snapshot belongs to another lobby.");
                    stage = "fixes-apply";
                    fixes.Apply(snapshot);
                    if (newlyAccepted)
                    {
                        DebugLogHelper.LogInfo(log, "Lord-data client Fixes values applied: session=" +
                            snapshot.SessionId + ",digest=" + snapshot.Digest +
                            ",selectedLords=" + snapshot.Slots.Count);
                        LogEffectiveFixes(snapshot, "client-receive");
                    }
                    active = snapshot;
                    error = string.Empty;
                    ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
                    stage = "status-set";
                    settings.LordDataStatus = "READY|" + snapshot.Digest;
                    if (newlyAccepted)
                        DebugLogHelper.LogInfo(log, "Lord-data client acknowledged: session=" +
                            snapshot.SessionId + ",digest=" + snapshot.Digest + ",localStatus=" +
                            LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, snapshot.Digest));
                    lastClientAcceptedDiagnostic = snapshot.Digest;
                }
                catch (Exception exception)
                {
                    active = null;
                    error = exception.Message;
                    settings.LordDataStatus = "ERROR|" + error;
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, true);
                    DebugLogHelper.LogError(log, "Rejected host Lord-data snapshot: stage=" + stage +
                        ",lobby=" + lobbyId + ",wireBytes=" + Encoding.UTF8.GetByteCount(wireJson ?? string.Empty) +
                        ",wireSha256=" + LordDataSyncDiagnostics.Hash(wireJson) + ",error=" + exception);
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
            int[] players = snapshot?.Players?.Keys.OrderBy(id => id).ToArray() ?? Array.Empty<int>();
            DebugLogHelper.LogInfo(log, "Lord-data lobby observation: previous=" + lobbyId +
                ",current=" + snapshot?.LobbyId + ",players=[" + string.Join(",", players) +
                "],unresolved=" + snapshot?.HasUnresolvedPlayers + ",localPlayer=" +
                snapshot?.LocalPlayerId + "," + DescribeAcknowledgements(players));
            lobbyHumanSlots = players;
            lobbyLocalPlayerId = snapshot?.LocalPlayerId ?? 0;
            if (snapshot?.LobbyId == lobbyId)
                return;
            lobbyId = snapshot?.LobbyId;
            fixes.Restore();
            active = null;
            error = string.Empty;
            ExtendedDataModDataApi.SetNetworkSnapshot(null, snapshot?.LobbyId != null);
            settings.LordDataStatus = string.Empty;
            lastHostGateDiagnostic = null;
            lastHostCaptureDiagnostic = null;
            lastRemoteStatusDiagnostic = null;
            lastMapTransitionDiagnostic = null;
            lastMapAppliedDiagnostic = null;
            lastClientReceivedDiagnostic = null;
            lastClientAcceptedDiagnostic = null;
            lastHostEchoDiagnostic = null;
            DebugLogHelper.LogInfo(log, "Lord-data session reset: lobby=" + lobbyId +
                ",fixesRestored=true,activeSnapshot=absent.");
            if (snapshot?.LobbyId != null)
                OnLobbyOpened(MainViewModel.Instance?.FRONTMultiplayer, "lobby-change");
        }

        private void OnRemoteStatusChanged()
        {
            string summary = DescribeAcknowledgements(lobbyHumanSlots);
            if (string.Equals(summary, lastRemoteStatusDiagnostic, StringComparison.Ordinal))
                return;
            lastRemoteStatusDiagnostic = summary;
            DebugLogHelper.LogInfo(log, "Lord-data remote status changed: " + summary);
        }

        private void LogEffectiveFixes(LordDataSnapshot snapshot, string source)
        {
            if (!snapshot.FixesInstalled)
            {
                DebugLogHelper.LogInfo(log, "Lord-data Fixes verification: source=" + source +
                    ",digest=" + snapshot.Digest + ",state=not-installed.");
                return;
            }
            foreach (LordDataSlot slot in snapshot.Slots.GroupBy(item => item.LordName,
                StringComparer.Ordinal).Select(group => group.First()))
            {
                try
                {
                    string actual = fixes.Capture(slot.LordName);
                    bool matches = string.Equals(actual, slot.FixesJson, StringComparison.Ordinal);
                    string message = "Lord-data Fixes verification: source=" + source +
                        ",digest=" + snapshot.Digest + ",lord=" + LordDataSyncDiagnostics.SafeLabel(slot.LordName) +
                        ",expected=" + LordDataSyncDiagnostics.DescribeJson(slot.FixesJson, true) +
                        ",actual=" + LordDataSyncDiagnostics.DescribeJson(actual, true) +
                        ",matches=" + matches;
                    if (matches)
                        DebugLogHelper.LogInfo(log, message);
                    else
                        DebugLogHelper.LogError(log, message);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, "Lord-data Fixes verification failed: source=" + source +
                        ",digest=" + snapshot.Digest + ",lord=" + LordDataSyncDiagnostics.SafeLabel(slot.LordName) +
                        ",error=" + exception);
                }
            }
        }

        private void OnLocalStatusChanged(string status) =>
            DebugLogHelper.LogInfo(log, "Lord-data local status changed: lobby=" + lobbyId +
                ",digest=" + (active?.Digest ?? "none") + ",status=" +
                LordDataSyncDiagnostics.DescribeStatus(status, active?.Digest));

        private void OnSnapshotMutationRejected(int bytes) =>
            DebugLogHelper.LogError(log, "Lord-data snapshot setting write rejected by Modsettings ownership: " +
                "lobby=" + lobbyId + ",characters=" + bytes + ",localHost=" + GameNetworkAPI.IsLocalHost());

        private void LogMapTransition(bool realMultiplayer)
        {
            string signature = realMultiplayer + ":" + lobbyId + ":" + active?.Digest + ":" +
                settings.LordDataStatus;
            if (string.Equals(signature, lastMapTransitionDiagnostic, StringComparison.Ordinal))
                return;
            lastMapTransitionDiagnostic = signature;
            string message = "Lord-data map transition: realMultiplayer=" + realMultiplayer +
                ",runtimeEnabled=" + settings.IsRuntimeEnabled + "," +
                DescribeAcknowledgements(lobbyHumanSlots);
            if (realMultiplayer && settings.IsRuntimeEnabled && active == null)
                DebugLogHelper.LogError(log, message + ",problem=no acknowledged host snapshot at map initialization.");
            else if (realMultiplayer && settings.IsRuntimeEnabled && !AllKnownPlayersAcknowledged())
                DebugLogHelper.LogError(log, message + ",problem=one or more known player acknowledgements are missing or stale.");
            else
                DebugLogHelper.LogInfo(log, message);
        }

        private bool AllKnownPlayersAcknowledged()
        {
            if (active == null)
                return false;
            string expected = "READY|" + active.Digest;
            if (!string.Equals(settings.LordDataStatus, expected, StringComparison.Ordinal))
                return false;
            return lobbyHumanSlots.All(id => string.Equals(
                id == lobbyLocalPlayerId ? settings.LordDataStatus :
                    id > 0 && id < settings.LordDataStatusData.Length
                        ? settings.LordDataStatusData[id] : null,
                expected, StringComparison.Ordinal));
        }

        private string DescribeAcknowledgements(IEnumerable<int> playerIds)
        {
            string expected = active?.Digest;
            return "lobby=" + lobbyId + ",expectedDigest=" + (expected ?? "none") +
                ",local=" + LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, expected) +
                ",players=[" + string.Join(";", (playerIds ?? Enumerable.Empty<int>()).Select(id =>
                    id + ":" + LordDataSyncDiagnostics.DescribeStatus(
                        id > 0 && id < settings.LordDataStatusData.Length
                            ? settings.LordDataStatusData[id] : null, expected))) + "]";
        }

        private void LogHostGateSkip(FRONT_Multiplayer lobby, string source)
        {
            string gate = DescribeHostGate(lobby);
            if (string.Equals(gate, lastHostGateDiagnostic, StringComparison.Ordinal))
                return;
            lastHostGateDiagnostic = gate;
            DebugLogHelper.LogInfo(log, "Lord-data host capture skipped: source=" + source + "," + gate);
        }

        private static string DescribeHostGate(FRONT_Multiplayer lobby) =>
            LordDataSyncDiagnostics.DescribeHostGate(
                lobby?.currentLobby != null,
                lobby?.currentLobby?.isHost == true,
                lobby?.singlePlayerCoop == true,
                Shared.GameModeHelper.IsRealMultiplayer());

        private static bool IsHostLobby(FRONT_Multiplayer lobby) =>
            lobby?.currentLobby != null && !lobby.singlePlayerCoop &&
            lobby.currentLobby.isHost && Shared.GameModeHelper.IsRealMultiplayer();

        private static string CurrentSessionId(FRONT_Multiplayer lobby) =>
            lobby.currentLobby.id.m_SteamID.ToString();

        public void Dispose()
        {
            settings.LordDataSnapshotChanged -= OnSnapshotChanged;
            settings.LordDataLobbyChanged -= OnLobbyChanged;
            settings.LordDataRemoteStatusChanged -= OnRemoteStatusChanged;
            settings.LordDataSnapshotMutationRejected -= OnSnapshotMutationRejected;
            settings.LordDataLocalStatusChanged -= OnLocalStatusChanged;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            if (saveRegistered)
                ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveIdentifier);
            fixes.Restore();
        }
    }
}
