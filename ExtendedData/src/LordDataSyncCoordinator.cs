using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
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
        private LordPackageManifest packageManifest;
        private readonly Dictionary<int, string> localLordPaths = new Dictionary<int, string>();
        private bool activeFromSave;
        private ulong? lobbyId;
        private string error = string.Empty;
        private bool hostPreferencesUnavailable;
        private bool saveRegistered;
        private string lastHostGateDiagnostic;
        private string lastHostCaptureDiagnostic;
        private string lastRemoteStatusDiagnostic;
        private string lastMapTransitionDiagnostic;
        private string lastMapAppliedDiagnostic;
        private string lastClientReceivedDiagnostic;
        private string lastClientAcceptedDiagnostic;
        private string lastHostEchoDiagnostic;
        private readonly Dictionary<string, string> lastAssetDiagnostics =
            new Dictionary<string, string>(StringComparer.Ordinal);
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
            settings.LordPackageManifestChanged += OnPackageManifestChanged;
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
                if (notification.Context.Mode.IsRealMultiplayer || active != null || packageManifest != null)
                    LogMapTransition(notification.Context.Mode.IsRealMultiplayer);
                if (!notification.Context.Mode.IsRealMultiplayer)
                {
                    fixes.Restore();
                    active = null;
                    packageManifest = null;
                    localLordPaths.Clear();
                    ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
                    DebugLogHelper.LogInfo(log, "Lord-data session cleared for single-player map initialization.");
                    return;
                }
                if (packageManifest?.UseLocalValues == true && localLordPaths.Count != 0)
                {
                    fixes.Restore();
                    ExtendedDataModDataApi.SetVerifiedLocalLords(localLordPaths);
                    return;
                }
                if (active == null)
                    return;
                if (notification.Context.Mode.IsRealMultiplayer)
                {
                    try
                    {
                        fixes.Apply(active);
                        VerifyEffectiveFixes(active, "map-initialization", true);
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
                    }
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
            {
                RefreshPackageManifest(lobby, source);
                if (!IsUsingLocalValues)
                    RefreshHost(lobby, source);
            }
            else if (lobby?.currentLobby != null && !lobby.currentLobby.isHost &&
                ActiveLobbyMatches(lobby) && ObservedLobbyMatches(lobby) &&
                !string.IsNullOrEmpty(settings.LordDataSnapshot))
            {
                DebugLogHelper.LogInfo(log, "Lord-data existing host setting observed on lobby open: source=" +
                    source + ",wire=" + LordDataSyncDiagnostics.DescribeJson(settings.LordDataSnapshot, false));
                OnSnapshotChanged(settings.LordDataSnapshot);
            }
            if (lobby?.currentLobby != null && !lobby.currentLobby.isHost &&
                ActiveLobbyMatches(lobby) && ObservedLobbyMatches(lobby) &&
                !string.IsNullOrEmpty(settings.LordPackageManifest))
                OnPackageManifestChanged(settings.LordPackageManifest);
            if (!IsHostLobby(lobby) &&
                (lobby?.currentLobby == null || lobby.currentLobby.isHost))
                LogHostGateSkip(lobby, source);
        }

        internal bool RefreshHost(FRONT_Multiplayer lobby, string source)
        {
            if (!IsHostLobby(lobby))
            {
                LogHostGateSkip(lobby, source);
                return false;
            }
            string stage = "capture";
            try
            {
                LordDataSnapshot next = CaptureLobby(lobby);
                string signature = next.SessionId + ":" + next.Digest;
                if (!string.Equals(signature, lastHostCaptureDiagnostic, StringComparison.Ordinal) ||
                    string.Equals(source, "start-attempt", StringComparison.Ordinal))
                    DebugLogHelper.LogInfo(log, "Lord-data host capture: source=" + source + "," +
                        LordDataSyncDiagnostics.DescribeSnapshot(next) +
                        ",selection=" + DescribeLobbySelection(lobby));
                lastHostCaptureDiagnostic = signature;
                stage = "publish";
                Publish(next, source);
                hostPreferencesUnavailable = false;
                return true;
            }
            catch (Exception exception)
            {
                hostPreferencesUnavailable = exception.Message.StartsWith(
                    "Fixes did not load the available preferences", StringComparison.Ordinal);
                Invalidate(exception.Message);
                DebugLogHelper.LogError(log, "Lord-data host capture failed: source=" + source +
                    ",lobby=" + lobbyId + ",stage=" + stage + ",error=" + exception);
                return false;
            }
        }

        internal bool RequiresLordSyncFromSelection(FRONT_Multiplayer lobby)
        {
            if (lobby?.AIVs == null)
                return false;
            for (int index = 0; index < Math.Min(8, lobby.AIVs.Length); index++)
            {
                if (!IsActiveAiSlot(lobby, index + 1))
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = lobby.AIVs[index];
                if (info == null || info.builtInLord || string.IsNullOrWhiteSpace(info.lordName))
                    continue;
                var config = info.lordConfig;
                if (config == null || string.IsNullOrWhiteSpace(config.path) ||
                    string.IsNullOrWhiteSpace(config.name))
                    return true;
                try
                {
                    LordPackageFileState files = LordPackageFingerprint.Capture(config.path, config.name);
                    if (files.HasUnsupportedGameplayFiles ||
                        File.Exists(Path.Combine(config.path, config.name + ".modlord.json")) ||
                        fixes.Capture(info.lordName) != null ||
                        (fixes.Installed && File.Exists(Path.Combine(config.path,
                            "Override", "Fixes", "preferences.json"))))
                        return true;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, "Selected Lord data inspection failed: lord=" +
                        LordDataSyncDiagnostics.SafeLabel(info.lordName) + ",error=" + exception);
                    return true;
                }
            }
            return false;
        }

        internal bool NeedsLordStartGate => packageManifest?.Slots.Any(slot =>
            slot.NeedsSnapshot || slot.NeedsLocalFiles) == true;

        internal bool IsUsingLocalValues => packageManifest?.UseLocalValues == true;

        internal string CurrentBlockReason => string.IsNullOrWhiteSpace(error)
            ? "Selected Lord data has not been confirmed by every player." : error;

        internal bool RefreshPackageManifest(FRONT_Multiplayer lobby, string source)
        {
            if (!IsHostLobby(lobby))
                return false;
            try
            {
                if (!IsUsingLocalValues)
                    fixes.Restore();
                var slots = new List<LordPackageSlot>();
                if (lobby.AIVs == null)
                    throw new InvalidDataException("The selected Lord list is unavailable.");
                for (int index = 0; index < Math.Min(8, lobby.AIVs.Length); index++)
                {
                    if (!IsActiveAiSlot(lobby, index + 1))
                        continue;
                    FRONT_Multiplayer.MPAIVInfo info = lobby.AIVs[index];
                    if (info == null || info.builtInLord || string.IsNullOrWhiteSpace(info.lordName))
                        continue;
                    var config = info.lordConfig;
                    if (config == null || string.IsNullOrWhiteSpace(config.path) ||
                        string.IsNullOrWhiteSpace(config.name))
                        throw new InvalidDataException("Selected Lord " + info.lordName + " has no host configuration.");
                    LordPackageFileState files = LordPackageFingerprint.Capture(config.path, config.name);
                    string fixesJson = null;
                    bool fixesCaptureFailed = false;
                    try { fixesJson = fixes.Capture(info.lordName); }
                    catch (Exception exception)
                    {
                        fixesCaptureFailed = true;
                        DebugLogHelper.LogError(log, "Lord package effective Fixes capture failed: lord=" +
                            LordDataSyncDiagnostics.SafeLabel(info.lordName) + ",error=" + exception);
                    }
                    string sidecar = Path.Combine(config.path, config.name + ".modlord.json");
                    slots.Add(new LordPackageSlot
                    {
                        PlayerId = index + 1,
                        LordType = info.lordType,
                        LordName = info.lordName,
                        ConfigName = config.name,
                        ConfigChecksum = config.checksum.ToString(),
                        FileDigest = files.Digest,
                        NeedsLocalFiles = files.HasUnsupportedGameplayFiles,
                        NeedsSnapshot = File.Exists(sidecar) || fixesJson != null || fixesCaptureFailed ||
                            (fixes.Installed && File.Exists(Path.Combine(config.path,
                                "Override", "Fixes", "preferences.json"))),
                        FixesDigest = fixesJson == null ? null : LordDataSyncDiagnostics.Hash(fixesJson),
                    });
                    DebugLogHelper.LogInfo(log, "Lord package inspected: source=" + source +
                        ",slot=" + (index + 1) + ",lord=" + LordDataSyncDiagnostics.SafeLabel(info.lordName) +
                        ",gameplayFiles=" + files.GameplayPaths.Count + ",digest=" + files.Digest +
                        ",unsupported=[" + string.Join(";", files.UnsupportedPaths.Select(
                            LordDataSyncDiagnostics.SafeLabel)) + "]");
                }
                bool keepLocal = packageManifest?.UseLocalValues == true &&
                    packageManifest.Slots.Count == slots.Count &&
                    packageManifest.Slots.Zip(slots, (left, right) => left.PlayerId == right.PlayerId &&
                        left.LordType == right.LordType &&
                        string.Equals(left.LordName, right.LordName, StringComparison.Ordinal) &&
                        string.Equals(left.ConfigName, right.ConfigName, StringComparison.Ordinal) &&
                        string.Equals(left.FileDigest, right.FileDigest, StringComparison.Ordinal) &&
                        left.NeedsLocalFiles == right.NeedsLocalFiles &&
                        left.NeedsSnapshot == right.NeedsSnapshot).All(match => match);
                PublishPackageManifest(LordPackageManifest.Create(CurrentSessionId(lobby), keepLocal, slots), source);
                return true;
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Lord package inspection failed: source=" + source +
                    ",error=" + exception);
                error = exception.Message;
                return false;
            }
        }

        private void PublishPackageManifest(LordPackageManifest next, string source)
        {
            bool changed = packageManifest == null ||
                !string.Equals(packageManifest.Digest, next.Digest, StringComparison.Ordinal);
            if (!string.Equals(settings.LordPackageManifest, next.WireJson, StringComparison.Ordinal))
                settings.LordPackageManifest = next.WireJson;
            if (!string.Equals(settings.LordPackageManifest, next.WireJson, StringComparison.Ordinal))
                throw new InvalidOperationException("The host Lord package setting rejected the manifest.");
            packageManifest = next;
            SetLocalPackageStatus(next);
            if (changed)
                DebugLogHelper.LogInfo(log, "Lord package manifest published: source=" + source +
                    ",session=" + next.SessionId + ",digest=" + next.Digest +
                    ",localMode=" + next.UseLocalValues + ",slots=" + next.Slots.Count);
        }

        private void OnPackageManifestChanged(string wire)
        {
            if (GameNetworkAPI.IsLocalHost())
                return;
            UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                if (!string.Equals(settings.LordPackageManifest, wire, StringComparison.Ordinal))
                    return;
                try
                {
                    LordPackageManifest received = LordPackageManifest.Parse(wire);
                    FRONT_Multiplayer lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
                    if (!ActiveLobbyMatches(lobby) || !ObservedLobbyMatches(lobby) ||
                        !string.Equals(received.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
                        throw new InvalidDataException("The Lord package manifest belongs to another lobby.");
                    packageManifest = received;
                    SetLocalPackageStatus(received);
                    if (!QueueClientStatusPublication("package-manifest", received.SessionId,
                        received.Digest, wire, settings.LordPackageStatus))
                        throw new InvalidOperationException(
                            "The Lord package confirmation could not be queued for publication.");
                    DebugLogHelper.LogInfo(log, "Lord package manifest received: session=" +
                        received.SessionId + ",digest=" + received.Digest +
                        ",localMode=" + received.UseLocalValues +
                        ",status=" + settings.LordPackageStatus);
                }
                catch (Exception exception)
                {
                    packageManifest = null;
                    localLordPaths.Clear();
                    Invalidate(exception.Message);
                    settings.LordPackageStatus = "ERROR|MANIFEST";
                    DebugLogHelper.LogError(log, "Lord package manifest rejected: " + exception);
                }
            });
        }

        private void SetLocalPackageStatus(LordPackageManifest manifest)
        {
            if (manifest.UseLocalValues)
                fixes.Restore();
            localLordPaths.Clear();
            int matchedMask = 0;
            foreach (LordPackageSlot slot in manifest.Slots)
            {
                if (!TryResolveLocalLord(slot, manifest.UseLocalValues, out string localPath,
                    out string diagnostic))
                {
                    DebugLogHelper.LogInfo(log, "Lord package local match failed: slot=" + slot.PlayerId +
                        ",lord=" + LordDataSyncDiagnostics.SafeLabel(slot.LordName) +
                        ",reason=" + diagnostic);
                    continue;
                }
                matchedMask |= 1 << (slot.PlayerId - 1);
                localLordPaths[slot.PlayerId] = localPath;
            }
            int allMask = manifest.Slots.Aggregate(0, (mask, slot) => mask | (1 << (slot.PlayerId - 1)));
            if (manifest.UseLocalValues && matchedMask == allMask)
            {
                fixes.Restore();
                ExtendedDataModDataApi.SetVerifiedLocalLords(
                    new Dictionary<int, string>(localLordPaths));
            }
            else if (manifest.UseLocalValues)
                ExtendedDataModDataApi.SetNetworkSnapshot(null, true);
            settings.LordPackageStatus = "LOCAL|" + manifest.Digest + "|" + matchedMask;
        }

        private bool TryResolveLocalLord(LordPackageSlot slot, bool verifyEffectiveFixes,
            out string lordJsonPath, out string diagnostic)
        {
            lordJsonPath = null;
            diagnostic = "configuration missing or contents differ";
            var candidates = new List<CustomisationFileManager.CustomLordConfig>();
            foreach (int lordType in new[] { slot.LordType, -1 }.Distinct())
            {
                List<CustomisationFileManager.CustomLordConfig> matches =
                    CustomisationFileManager.Instance.getLordLordList(lordType, slot.LordName);
                if (matches != null)
                    candidates.AddRange(matches);
            }
            foreach (var config in candidates.Where(item => item != null &&
                string.Equals(item.name, slot.ConfigName, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    LordPackageFileState local = LordPackageFingerprint.Capture(config.path, config.name);
                    if (!string.Equals(local.Digest, slot.FileDigest, StringComparison.Ordinal))
                        continue;
                    if (verifyEffectiveFixes && slot.FixesDigest != null &&
                        !string.Equals(LordDataSyncDiagnostics.Hash(fixes.Capture(slot.LordName)),
                            slot.FixesDigest, StringComparison.Ordinal))
                    {
                        diagnostic = "effective Fixes preferences differ";
                        continue;
                    }
                    string selectedPath = Path.Combine(config.path, config.name + ".lordjson");
                    if (!File.Exists(selectedPath))
                        continue;
                    lordJsonPath = selectedPath;
                    diagnostic = string.Empty;
                    return true;
                }
                catch (Exception exception)
                {
                    diagnostic = exception.Message;
                }
            }
            return false;
        }

        internal bool PrepareSave(FRONT_Multiplayer lobby, FileHeader header)
        {
            try
            {
                if (header == null || string.IsNullOrWhiteSpace(header.filePath))
                    return true;
                LordDataSnapshot saved = null;
                LordPackageManifest savedManifest = null;
                if (MapArchive.TryLoad(header.filePath, out MapArchive archive))
                {
                    using (archive)
                    {
                        byte[] bytes = archive.TryReadBinaryFile(SaveEntry, ignoreCase: true);
                        if (bytes != null)
                            ParseSavedLordData(bytes, out saved, out savedManifest);
                    }
                }
                if (saved == null && savedManifest == null &&
                    !SaveHasPotentialLordData(header))
                    return true;
                bool reconstructed = saved == null;
                if (saved == null && savedManifest?.UseLocalValues == true)
                    saved = LordDataSnapshot.Create(savedManifest.SessionId, fixes.Installed,
                        savedManifest.Slots.Select(slot => new LordDataSlot
                        {
                            PlayerId = slot.PlayerId,
                            LordName = slot.LordName,
                            ConfigName = slot.ConfigName,
                            ConfigChecksum = slot.ConfigChecksum,
                        }));
                if (saved == null)
                    saved = CaptureLegacySave(lobby, header);
                if (savedManifest == null)
                    savedManifest = CaptureSavedPackageManifest(saved);
                if (!savedManifest.Slots.Any(slot => slot.NeedsSnapshot || slot.NeedsLocalFiles))
                    return true;
                if (!IsHostLobby(lobby))
                {
                    LogHostGateSkip(lobby, "multiplayer-save");
                    return false;
                }
                if (saved.FixesInstalled != fixes.Installed)
                    throw new InvalidDataException("The installed Fixes state differs from the saved Lord data.");
                ValidateSavedLords(header, saved);
                if (!saved.Slots.Select(slot => slot.PlayerId + ":" + slot.LordName + ":" + slot.ConfigName)
                    .SequenceEqual(savedManifest.Slots.Select(slot => slot.PlayerId + ":" +
                        slot.LordName + ":" + slot.ConfigName)))
                    throw new InvalidDataException("Saved Lord package identities do not match the saved values.");
                PublishPackageManifest(LordPackageManifest.Create(CurrentSessionId(lobby),
                    savedManifest.UseLocalValues, savedManifest.Slots), "multiplayer-save");
                if (!savedManifest.UseLocalValues)
                    Publish(LordDataSnapshot.Create(CurrentSessionId(lobby), saved.FixesInstalled, saved.Slots),
                        reconstructed ? "legacy-save" : "multiplayer-save");
                bool ready = IsReadyToLaunch(lobby, out string reason);
                LogStartDecision(true, ready, reason);
                return ready;
            }
            catch (Exception exception)
            {
                Invalidate(exception.Message);
                DebugLogHelper.LogError(log, "Lord-data save preparation failed: " + exception);
                return false;
            }
        }

        private bool SaveHasPotentialLordData(FileHeader header)
        {
            FileHeader detailed = MapFileManager.Instance.GetFileInfoFromFileName(
                header.filePath, header.filePath, 0, loadRestartInfo: true);
            string[] names = detailed?.restartMPInfo?.LordNames;
            if (names == null)
                return false;
            foreach (string name in names.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                foreach (int lordType in Enumerable.Range(-1, ConfigSettings.extendedLordPaths.Length + 1))
                {
                    var configs = CustomisationFileManager.Instance.getLordLordList(lordType, name);
                    foreach (var config in configs ?? new List<CustomisationFileManager.CustomLordConfig>())
                    {
                        if (config == null || string.IsNullOrWhiteSpace(config.path))
                            continue;
                        LordPackageFileState files = LordPackageFingerprint.Capture(config.path, config.name);
                        if (files.HasUnsupportedGameplayFiles ||
                            File.Exists(Path.Combine(config.path, config.name + ".modlord.json")) ||
                            fixes.Capture(name) != null)
                            return true;
                    }
                }
            }
            return false;
        }

        private LordPackageManifest CaptureSavedPackageManifest(LordDataSnapshot snapshot)
        {
            var slots = new List<LordPackageSlot>();
            foreach (LordDataSlot saved in snapshot.Slots)
            {
                var candidates = Enumerable.Range(-1, ConfigSettings.extendedLordPaths.Length + 1)
                    .SelectMany(lordType => CustomisationFileManager.Instance
                        .getLordLordList(lordType, saved.LordName) ??
                        new List<CustomisationFileManager.CustomLordConfig>())
                    .Where(config => config != null &&
                        string.Equals(config.name, saved.ConfigName, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(config => config.path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()).ToArray();
                if (candidates.Length != 1)
                    throw new InvalidDataException("The saved Lord configuration is missing or ambiguous: " + saved.LordName);
                LordPackageFileState files = LordPackageFingerprint.Capture(candidates[0].path,
                    candidates[0].name);
                slots.Add(new LordPackageSlot
                {
                    PlayerId = saved.PlayerId,
                    LordType = candidates[0].lordType,
                    LordName = saved.LordName,
                    ConfigName = saved.ConfigName,
                    ConfigChecksum = saved.ConfigChecksum,
                    FileDigest = files.Digest,
                    NeedsLocalFiles = files.HasUnsupportedGameplayFiles,
                    NeedsSnapshot = saved.ModLordJson != null || saved.FixesJson != null,
                    FixesDigest = saved.FixesJson == null ? null : LordDataSyncDiagnostics.Hash(saved.FixesJson),
                });
            }
            return LordPackageManifest.Create(snapshot.SessionId, false, slots);
        }

        internal bool IsReadyToLaunch(FRONT_Multiplayer lobby, out string reason)
        {
            reason = error;
            if (!NeedsLordStartGate)
            {
                reason = string.Empty;
                return true;
            }
            if (!IsHostLobby(lobby))
            {
                reason = "Cannot verify the selected Lord data while the multiplayer lobby is not ready.";
                return false;
            }
            if (packageManifest == null ||
                !string.Equals(packageManifest.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal) ||
                !string.Equals(settings.LordPackageManifest, packageManifest.WireJson, StringComparison.Ordinal))
            {
                reason = "The selected Lord package manifest is not published yet.";
                return false;
            }
            if (packageManifest.UseLocalValues)
                return AreLocalPackagesReady(lobby, true, out reason);
            if (packageManifest.Slots.Any(slot => slot.NeedsLocalFiles) &&
                !AreLocalPackagesReady(lobby, false, out reason))
                return false;
            if (active == null || !string.Equals(active.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
            {
                reason = "The selected Lord values are not synchronized yet: " +
                    string.Join(", ", packageManifest.Slots.Where(slot => slot.NeedsSnapshot)
                        .Select(slot => slot.LordName).Distinct(StringComparer.Ordinal)) + ".";
                return false;
            }
            if (!string.IsNullOrEmpty(error))
                return false;
            if (!string.Equals(settings.LordDataSnapshot, active.WireJson, StringComparison.Ordinal))
            {
                reason = "The published Lord-data setting does not match the host snapshot.";
                return false;
            }
            if (!activeFromSave && (lobby?.AIVs == null || !LordDataSyncDiagnostics.MatchesSelectedSlots(active,
                lobby.AIVs.Take(8).Select((selected, index) => new { selected, index })
                    .Where(item => IsActiveAiSlot(lobby, item.index + 1) &&
                        item.selected != null && !item.selected.builtInLord &&
                        !string.IsNullOrWhiteSpace(item.selected.lordName))
                    .Select(item => item.index + 1))))
            {
                reason = "The selected Custom Lord slots do not match the published snapshot.";
                return false;
            }
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

        private bool AreLocalPackagesReady(FRONT_Multiplayer lobby, bool allSlots, out string reason)
        {
            reason = string.Empty;
            if (packageManifest == null || !PlayerIdentityHelper.TryCaptureHumanRoster(false, true,
                out Dictionary<int, ulong> players, out string rosterError, out _))
            {
                reason = "The multiplayer player list is not ready for Lord file verification.";
                return false;
            }
            int requiredMask = packageManifest.Slots.Where(slot => allSlots || slot.NeedsLocalFiles)
                .Aggregate(0, (mask, slot) => mask | (1 << (slot.PlayerId - 1)));
            foreach (int playerId in players.Keys)
            {
                string status = playerId == GameNetworkAPI.GetLocalPlayerId()
                    ? settings.LordPackageStatus
                    : playerId > 0 && playerId < settings.LordPackageStatusData.Length
                        ? settings.LordPackageStatusData[playerId] : null;
                string[] parts = (status ?? string.Empty).Split('|');
                if (parts.Length != 3 || parts[0] != "LOCAL" ||
                    !string.Equals(parts[1], packageManifest.Digest, StringComparison.Ordinal) ||
                    !int.TryParse(parts[2], out int matchedMask) ||
                    (matchedMask & requiredMask) != requiredMask)
                {
                    string lordNames = string.Join(", ", packageManifest.Slots
                        .Where(slot => (requiredMask & (1 << (slot.PlayerId - 1))) != 0)
                        .Select(slot => slot.LordName).Distinct(StringComparer.Ordinal));
                    reason = "Player " + playerId + " needs the same complete selected Lord files: " + lordNames + ".";
                    return false;
                }
            }
            return true;
        }

        internal bool TryUseLocalValues(FRONT_Multiplayer lobby)
        {
            if (!IsHostLobby(lobby) || packageManifest == null || packageManifest.UseLocalValues ||
                hostPreferencesUnavailable || !AreLocalPackagesReady(lobby, true, out _))
                return false;
            PublishPackageManifest(LordPackageManifest.Create(packageManifest.SessionId, true,
                packageManifest.Slots), "local-fallback");
            fixes.Restore();
            active = null;
            activeFromSave = false;
            settings.LordDataSnapshot = string.Empty;
            error = string.Empty;
            hostPreferencesUnavailable = false;
            return true;
        }

        internal void LogStartAttempt(string command, bool runtimeEnabled, FRONT_Multiplayer lobby)
        {
            DebugLogHelper.LogInfo(log, "Lord-data start hook reached: command=" +
                LordDataSyncDiagnostics.SafeLabel(command) +
                ",runtimeEnabled=" + runtimeEnabled + "," + DescribeHostGate(lobby) +
                "," + DescribeAcknowledgements(lobbyHumanSlots) +
                ",selection=" + DescribeLobbySelection(lobby));
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
                if (!IsActiveAiSlot(lobby, index + 1))
                    continue;
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
            string fixesJson = fixes.Capture(lordName);
            LogFixesAssetSource(lordName, fixesJson);
            return new LordDataSlot
            {
                PlayerId = playerId,
                LordName = lordName,
                ConfigName = config.name,
                ConfigChecksum = config.checksum.ToString(),
                ModLordJson = modLordJson,
                FixesJson = fixesJson,
            };
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
                CustomisationFileManager.CustomLordConfig[] configs =
                    Enumerable.Range(-1, ConfigSettings.extendedLordPaths.Length + 1)
                        .SelectMany(lordType => CustomisationFileManager.Instance
                            .getLordLordList(lordType, names[index]) ??
                            new List<CustomisationFileManager.CustomLordConfig>())
                        .Where(config => config != null)
                        .GroupBy(config => config.path, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First()).ToArray();
                if (configs.Length != 1)
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
            VerifyEffectiveFixes(snapshot, "host-" + source,
                changed || string.Equals(source, "start-attempt", StringComparison.Ordinal));
            if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
            {
                DebugLogHelper.LogInfo(log, "Lord-data host Fixes values applied: source=" + source +
                    ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                    ",selectedLords=" + snapshot.Slots.Count);
            }
            if (!string.Equals(settings.LordDataSnapshot, snapshot.WireJson, StringComparison.Ordinal))
                settings.LordDataSnapshot = snapshot.WireJson;
            if (!string.Equals(settings.LordDataSnapshot, snapshot.WireJson, StringComparison.Ordinal))
                throw new InvalidOperationException("The host Lord-data Modsetting rejected the snapshot.");
            error = string.Empty;
            active = snapshot;
            activeFromSave = string.Equals(source, "legacy-save", StringComparison.Ordinal) ||
                string.Equals(source, "multiplayer-save", StringComparison.Ordinal);
            ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
            settings.LordDataStatus = "READY|" + snapshot.Digest;
            if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
                DebugLogHelper.LogInfo(log, "Lord-data host publication: source=" + source +
                    ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                    ",snapshotSettingMatches=" + string.Equals(settings.LordDataSnapshot,
                        snapshot.WireJson, StringComparison.Ordinal) +
                    ",localStatus=" + LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, snapshot.Digest));
        }

        private void OnSnapshotChanged(string wireJson)
        {
            if (packageManifest?.UseLocalValues == true)
                return;
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
                if (packageManifest?.UseLocalValues == true ||
                    !string.Equals(settings.LordDataSnapshot, wireJson, StringComparison.Ordinal))
                    return;
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
                    FRONT_Multiplayer lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
                    if (!ActiveLobbyMatches(lobby) || !ObservedLobbyMatches(lobby) ||
                        !string.Equals(snapshot.SessionId, CurrentSessionId(lobby), StringComparison.Ordinal))
                        throw new InvalidDataException("Lord-data snapshot belongs to another lobby.");
                    stage = "fixes-apply";
                    fixes.Apply(snapshot);
                    VerifyEffectiveFixes(snapshot, "client-receive", newlyAccepted);
                    if (newlyAccepted)
                    {
                        DebugLogHelper.LogInfo(log, "Lord-data client Fixes values applied: session=" +
                            snapshot.SessionId + ",digest=" + snapshot.Digest +
                            ",selectedLords=" + snapshot.Slots.Count);
                    }
                    active = snapshot;
                    error = string.Empty;
                    ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
                    stage = "status-set";
                    settings.LordDataStatus = "READY|" + snapshot.Digest;
                    stage = "status-publication";
                    if (!QueueClientStatusPublication("lord-snapshot", snapshot.SessionId,
                        snapshot.Digest, wireJson, settings.LordDataStatus))
                        throw new InvalidOperationException(
                            "The Lord-data confirmation could not be queued for publication.");
                    if (newlyAccepted)
                        DebugLogHelper.LogInfo(log, "Lord-data client values applied; confirmation queued: session=" +
                            snapshot.SessionId + ",digest=" + snapshot.Digest + ",localStatus=" +
                            LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, snapshot.Digest));
                    lastClientAcceptedDiagnostic = snapshot.Digest;
                }
                catch (Exception exception)
                {
                    Invalidate(exception.Message);
                    DebugLogHelper.LogError(log, "Rejected host Lord-data snapshot: stage=" + stage +
                        ",lobby=" + lobbyId + ",wireBytes=" + Encoding.UTF8.GetByteCount(wireJson ?? string.Empty) +
                        ",wireSha256=" + LordDataSyncDiagnostics.Hash(wireJson) + ",error=" + exception);
                }
            });
        }

        private byte[] SaveSnapshot(SaveContext context)
        {
            if (!context.IsSaveFile || (active == null && packageManifest == null))
                return null;
            string json = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["version"] = 2,
                ["snapshot"] = active?.WireJson,
                ["lordPackageManifest"] = packageManifest?.WireJson,
            });
            return StrictUtf8.GetBytes(json);
        }

        private static void ParseSavedLordData(byte[] bytes, out LordDataSnapshot snapshot,
            out LordPackageManifest manifest)
        {
            string json = StrictUtf8.GetString(bytes);
            var envelope = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
            if (envelope != null && envelope.ContainsKey("lordPackageManifest"))
            {
                if (Convert.ToInt32(envelope["version"]) != 2)
                    throw new InvalidDataException("Unsupported saved Lord data format.");
                string snapshotJson = envelope["snapshot"] as string;
                string manifestJson = envelope["lordPackageManifest"] as string;
                snapshot = snapshotJson == null ? null : LordDataSnapshot.Parse(snapshotJson);
                manifest = manifestJson == null ? null : LordPackageManifest.Parse(manifestJson);
            }
            else
            {
                snapshot = LordDataSnapshot.Parse(json);
                manifest = null;
            }
        }

        private void LoadSnapshot(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile || bytes == null)
                return;
            try
            {
                ParseSavedLordData(bytes, out LordDataSnapshot saved, out LordPackageManifest savedManifest);
                if (active == null && packageManifest?.UseLocalValues != true &&
                    (saved != null || savedManifest != null))
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
            packageManifest = null;
            localLordPaths.Clear();
            activeFromSave = false;
            error = string.Empty;
            hostPreferencesUnavailable = false;
            ExtendedDataModDataApi.SetNetworkSnapshot(null, snapshot?.LobbyId != null);
            settings.LordDataStatus = "NONE";
            settings.LordPackageStatus = string.Empty;
            lastHostGateDiagnostic = null;
            lastHostCaptureDiagnostic = null;
            lastRemoteStatusDiagnostic = null;
            lastMapTransitionDiagnostic = null;
            lastMapAppliedDiagnostic = null;
            lastClientReceivedDiagnostic = null;
            lastClientAcceptedDiagnostic = null;
            lastHostEchoDiagnostic = null;
            lastAssetDiagnostics.Clear();
            DebugLogHelper.LogInfo(log, "Lord-data session reset: lobby=" + lobbyId +
                ",fixesRestored=true,activeSnapshot=absent.");
            if (snapshot?.LobbyId != null)
                OnLobbyOpened(ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer, "lobby-change");
        }

        private void OnRemoteStatusChanged()
        {
            string summary = DescribeAcknowledgements(lobbyHumanSlots);
            if (string.Equals(summary, lastRemoteStatusDiagnostic, StringComparison.Ordinal))
                return;
            lastRemoteStatusDiagnostic = summary;
            DebugLogHelper.LogInfo(log, "Lord-data remote status changed: " + summary);
        }

        private void VerifyEffectiveFixes(LordDataSnapshot snapshot, string source, bool logSuccess)
        {
            if (!snapshot.FixesInstalled)
            {
                if (logSuccess)
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
                    if (matches && logSuccess)
                        DebugLogHelper.LogInfo(log, message);
                    else if (!matches)
                    {
                        DebugLogHelper.LogError(log, message);
                        throw new InvalidDataException("Fixes preference values do not match for selected Lord " +
                            slot.LordName + ".");
                    }
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, "Lord-data Fixes verification failed: source=" + source +
                        ",digest=" + snapshot.Digest + ",lord=" + LordDataSyncDiagnostics.SafeLabel(slot.LordName) +
                        ",error=" + exception);
                    throw;
                }
            }
        }

        private void OnLocalStatusChanged(string status) =>
            DebugLogHelper.LogInfo(log, "Lord-data local status changed: lobby=" + lobbyId +
                ",digest=" + (active?.Digest ?? "none") + ",status=" +
                LordDataSyncDiagnostics.DescribeStatus(status, active?.Digest));

        private bool QueueClientStatusPublication(string source, string sessionId,
            string digest, string wire, string status)
        {
            bool queued = UnityMainThreadDispatch.TryEnqueue(() =>
            {
                try
                {
                    FRONT_Multiplayer lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
                    bool isSnapshot = string.Equals(source, "lord-snapshot", StringComparison.Ordinal);
                    bool current = ActiveLobbyMatches(lobby) && ObservedLobbyMatches(lobby) &&
                        !GameNetworkAPI.IsLocalHost() &&
                        (isSnapshot
                            ? packageManifest?.UseLocalValues != true &&
                              LordDataSyncDiagnostics.MatchesDeferredPublication(
                                  sessionId, digest, wire, status, CurrentSessionId(lobby),
                                  active?.Digest, settings.LordDataSnapshot, settings.LordDataStatus)
                            : LordDataSyncDiagnostics.MatchesDeferredPublication(
                                  sessionId, digest, wire, status, CurrentSessionId(lobby),
                                  packageManifest?.Digest, settings.LordPackageManifest,
                                  settings.LordPackageStatus));
                    if (!current)
                    {
                        DebugLogHelper.LogInfo(log, "Lord-data deferred confirmation skipped: source=" +
                            source + ",session=" + sessionId + ",digest=" + digest +
                            ",reason=selection-or-lobby-changed.");
                        return;
                    }
                    LobbyModSettingsChangeOrigin origin =
                        GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin;
                    if (origin != LobbyModSettingsChangeOrigin.Local)
                        throw new InvalidOperationException(
                            "The Lord confirmation still has Modsettings change origin " + origin + ".");
                    // The Extender suppresses nested PropertyChanged broadcasts while it applies
                    // an incoming host setting. APIShared republishes the current personal values
                    // after that incoming update has returned.
                    settings.System_RequestPerPlayerSettingsPublish();
                    DebugLogHelper.LogInfo(log, "Lord-data deferred confirmation publication requested: source=" +
                        source + ",session=" + sessionId + ",digest=" + digest +
                        ",origin=" + origin +
                        ",status=" + (isSnapshot
                            ? LordDataSyncDiagnostics.DescribeStatus(status, digest)
                            : LordDataSyncDiagnostics.SafeLabel(status)));
                }
                catch (Exception exception)
                {
                    Invalidate("Could not publish the selected Lord confirmation: " + exception.Message);
                    DebugLogHelper.LogError(log, "Lord-data deferred confirmation failed: source=" +
                        source + ",session=" + sessionId + ",digest=" + digest +
                        ",error=" + exception);
                }
            });
            if (!queued)
                DebugLogHelper.LogError(log, "Lord-data confirmation scheduling failed: source=" +
                    source + ",session=" + sessionId + ",digest=" + digest +
                    ",dispatcherInitialized=" + UnityMainThreadDispatch.IsInitialized + ".");
            return queued;
        }

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
            if (realMultiplayer && settings.IsRuntimeEnabled && NeedsLordStartGate &&
                active == null && packageManifest?.UseLocalValues != true)
                DebugLogHelper.LogError(log, message + ",problem=no acknowledged host snapshot at map initialization.");
            else if (realMultiplayer && settings.IsRuntimeEnabled && NeedsLordStartGate &&
                packageManifest?.UseLocalValues != true && !AllKnownPlayersAcknowledged())
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
            string signature = source + ":" + gate;
            if (string.Equals(signature, lastHostGateDiagnostic, StringComparison.Ordinal))
                return;
            lastHostGateDiagnostic = signature;
            DebugLogHelper.LogInfo(log, "Lord-data host capture skipped: source=" + source + "," + gate);
        }

        private string DescribeHostGate(FRONT_Multiplayer lobby) =>
            LordDataSyncDiagnostics.DescribeHostGate(
                lobby?.currentLobby != null,
                lobby?.currentLobby?.isHost == true,
                lobby?.singlePlayerCoop == true,
                ActiveLobbyMatches(lobby),
                ObservedLobbyMatches(lobby),
                HasRealLobbyMember(lobby),
                Shared.GameModeHelper.IsRealMultiplayer());

        private bool IsHostLobby(FRONT_Multiplayer lobby) =>
            LordDataSyncDiagnostics.IsHostLobby(
                lobby?.currentLobby != null,
                lobby?.currentLobby?.isHost == true,
                lobby?.singlePlayerCoop == true,
                ActiveLobbyMatches(lobby),
                ObservedLobbyMatches(lobby),
                HasRealLobbyMember(lobby));

        private static bool ActiveLobbyMatches(FRONT_Multiplayer lobby) =>
            lobby?.currentLobby != null &&
            Platform_Multiplayer.Instance?.activeLobby?.id.m_SteamID ==
                lobby.currentLobby.id.m_SteamID && lobby.currentLobby.id.m_SteamID != 0;

        private bool ObservedLobbyMatches(FRONT_Multiplayer lobby) =>
            lobbyId.HasValue && lobby?.currentLobby != null &&
            lobbyId.Value == lobby.currentLobby.id.m_SteamID;

        private static bool HasRealLobbyMember(FRONT_Multiplayer lobby) =>
            lobby?.currentLobby?.members?.Any(member => member != null &&
                !member.dummyToBeKicked && !member.SkirmishMember) == true;

        private static bool IsActiveAiSlot(FRONT_Multiplayer lobby, int playerId) =>
            lobby?.currentLobby?.members?.Any(member => member != null &&
                !member.dummyToBeKicked && member.SkirmishMember &&
                !member.SkirmishHumanMember &&
                lobby.currentLobby.getThisPlayerFromSteamID(member.id.m_SteamID) == playerId) == true;

        private static string DescribeLobbySelection(FRONT_Multiplayer lobby)
        {
            if (lobby?.AIVs == null)
                return "unavailable";
            return "[" + string.Join(";", lobby.AIVs.Take(8).Select((selected, index) =>
                selected == null ? (index + 1) + ":empty" :
                (index + 1) + ":builtIn=" + selected.builtInLord +
                ":lord=" + LordDataSyncDiagnostics.SafeLabel(selected.lordName) +
                ":config=" + LordDataSyncDiagnostics.SafeLabel(selected.lordConfig?.name))) + "]";
        }

        private void LogFixesAssetSource(string lordName, string effectiveJson)
        {
            if (!fixes.Installed)
                return;
            string source;
            bool assetPresent = false;
            try
            {
                if (GameAIManagerAPI.Instance?.LordDataDict == null ||
                    !GameAIManagerAPI.Instance.LordDataDict.TryGetValue(
                        lordName.ToLowerInvariant(), out var entry))
                    source = "lord-entry=missing";
                else if (GameAssetManagerAPI.Instance == null)
                    source = "asset-provider=unavailable";
                else if (GameAssetManagerAPI.Instance.GetModFileTextContent(
                    entry.AssetProviderGuid, "override/fixes/preferences.json", out string assetJson))
                {
                    assetPresent = true;
                    source = "provider=" + LordDataSyncDiagnostics.SafeLabel(entry.AssetProviderGuid) +
                        ",asset=" + LordDataSyncDiagnostics.DescribeJson(assetJson, true);
                }
                else
                    source = "provider=" + LordDataSyncDiagnostics.SafeLabel(entry.AssetProviderGuid) +
                        ",asset=absent";
            }
            catch (Exception exception)
            {
                source = "asset-query-error=" + exception.GetType().Name;
            }
            string diagnostic = "lord=" + LordDataSyncDiagnostics.SafeLabel(lordName) +
                ",effective=" + LordDataSyncDiagnostics.DescribeJson(effectiveJson, true) +
                "," + source;
            if (lastAssetDiagnostics.TryGetValue(lordName, out string previous) &&
                string.Equals(diagnostic, previous, StringComparison.Ordinal))
            {
                if (assetPresent && effectiveJson == null)
                    throw new InvalidDataException("Fixes did not load the available preferences for selected Lord " + lordName + ".");
                return;
            }
            lastAssetDiagnostics[lordName] = diagnostic;
            if (assetPresent && effectiveJson == null)
            {
                DebugLogHelper.LogError(log, "Lord-data Fixes source: " + diagnostic +
                    ",problem=asset-present-but-effective-entry-missing.");
                throw new InvalidDataException("Fixes did not load the available preferences for selected Lord " + lordName + ".");
            }
            DebugLogHelper.LogInfo(log, "Lord-data Fixes source: " + diagnostic);
        }

        private void Invalidate(string message)
        {
            try { fixes.Restore(); }
            catch (Exception exception)
            {
                DebugLogHelper.LogError(log, "Lord-data Fixes restore failed after sync error: " + exception);
            }
            active = null;
            activeFromSave = false;
            error = message ?? "Unknown Lord-data error.";
            ExtendedDataModDataApi.SetNetworkSnapshot(null, lobbyId.HasValue);
            settings.LordDataStatus = "ERROR|" + error;
        }

        private static string CurrentSessionId(FRONT_Multiplayer lobby) =>
            lobby.currentLobby.id.m_SteamID.ToString();

        public void Dispose()
        {
            settings.LordDataSnapshotChanged -= OnSnapshotChanged;
            settings.LordPackageManifestChanged -= OnPackageManifestChanged;
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
