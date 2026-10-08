using APIShared.GameModes;
using APIShared.ModSettings;
using APIShared.SerpsMods;
using APIShared.Internal;
#pragma warning disable 1591 // XAML and integration surface is documented by the APIShared preset guide.
using BepInEx;
using BepInEx.Logging;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if !API_SHARED_PRESET_TESTS
using R3;
using SHCDESE.EventAPI;
using SHCDESE.NoesisUtil;
#endif
using ComboBoxItem = Noesis.ComboBoxItem;
using Visibility = Noesis.Visibility;
#if API_SHARED_LOBBY_OBSERVER && !API_SHARED_PRESET_TESTS
using APIShared;
#endif

namespace APIShared.ModSettings
{
    internal sealed class PresetSaveFileExistsException : IOException
    {
        internal PresetSaveFileExistsException(string path)
            : base("The personal preset already exists: " + path)
        {
        }
    }

    internal interface IPresetAtomicFileOperations
    {
        bool Exists(string path);
        void Replace(string sourcePath, string destinationPath);
        void Move(string sourcePath, string destinationPath);
        void Delay(int milliseconds);
    }

    internal readonly struct PresetAtomicPublishResult
    {
        public PresetAtomicPublishResult(bool succeeded, int attempts, IOException error)
        {
            Succeeded = succeeded;
            Attempts = attempts;
            Error = error;
        }

        public bool Succeeded { get; }
        public int Attempts { get; }
        public IOException Error { get; }
    }

    internal static class PresetAtomicFilePublisher
    {
        private static readonly int[] RetryDelaysMilliseconds = { 15, 35, 75 };

        public static PresetAtomicPublishResult Publish(
            string temporaryPath,
            string destinationPath,
            IPresetAtomicFileOperations operations)
        {
            if (string.IsNullOrEmpty(temporaryPath))
                throw new ArgumentException("A temporary preset path is required.", nameof(temporaryPath));
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("A destination preset path is required.", nameof(destinationPath));
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            for (int attempt = 1; attempt <= RetryDelaysMilliseconds.Length + 1; attempt++)
            {
                try
                {
                    if (operations.Exists(destinationPath))
                        operations.Replace(temporaryPath, destinationPath);
                    else
                        operations.Move(temporaryPath, destinationPath);
                    return new PresetAtomicPublishResult(true, attempt, null);
                }
                catch (IOException exception)
                {
                    if (attempt > RetryDelaysMilliseconds.Length)
                        return new PresetAtomicPublishResult(false, attempt, exception);
                    operations.Delay(RetryDelaysMilliseconds[attempt - 1]);
                }
            }

            throw new InvalidOperationException("The bounded preset publish loop terminated unexpectedly.");
        }

        public static PresetAtomicPublishResult Publish(
            string temporaryPath,
            string destinationPath)
        {
            return Publish(temporaryPath, destinationPath, SystemPresetAtomicFileOperations.Instance);
        }

        private sealed class SystemPresetAtomicFileOperations : IPresetAtomicFileOperations
        {
            public static readonly SystemPresetAtomicFileOperations Instance =
                new SystemPresetAtomicFileOperations();

            public bool Exists(string path) => File.Exists(path);

            public void Replace(string sourcePath, string destinationPath) =>
                AtomicFileReplacement.Replace(sourcePath, destinationPath);

            public void Move(string sourcePath, string destinationPath) =>
                File.Move(sourcePath, destinationPath);

            public void Delay(int milliseconds) =>
                System.Threading.Thread.Sleep(milliseconds);
        }
    }

    internal sealed class PerPlayerLobbySettingsCoordinator
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private readonly PresetLobbyModSettingsViewModel owner;
        private readonly ManualLogSource log;
        private readonly string modName;
        private readonly string ownerGuid;
        private readonly PerPlayerLobbySettingsContract contract;
        private readonly bool routineLoggingEnabled;
        private readonly Dictionary<int, ulong> playersById = new Dictionary<int, ulong>();
        private ulong lobbyId;
        private bool hasLobby;
        private bool publishPending;
        private bool rosterHasUnresolvedPlayers;
        private int resolvedLocalPlayerId;
        private bool isResettingSlots;
        private bool isMirroringLocalSetting;
        private bool isReady = true;
        private string readinessError = string.Empty;
        private bool active;
#if !API_SHARED_PRESET_TESTS
        private IDisposable mapStartSubscription;
        private string lastIdentityDiagnostic = string.Empty;
#endif

        internal PerPlayerLobbySettingsCoordinator(
            PresetLobbyModSettingsViewModel owner,
            ManualLogSource log,
            string modName,
            string ownerGuid,
            PerPlayerLobbySettingsContract contract,
            bool logRoutineActivity)
        {
            this.owner = owner;
            this.log = log;
            this.modName = modName;
            this.ownerGuid = ownerGuid;
            this.contract = contract;
            routineLoggingEnabled = logRoutineActivity;
        }

        internal bool IsReady => isReady;
        internal string ReadinessError => readinessError;

        internal void Activate()
        {
            if (contract.Settings.Count == 0)
                return;
            if (active)
                return;

            try
            {
                owner.PropertyChanged += OnOwnerPropertyChanged;
#if !API_SHARED_PRESET_TESTS
                // SaveLifecycle: normal multiplayer starts and Platform_Multiplayer.StartSave
                // preserve the lobby snapshot and remap Steam identities to final game slots.
                // Only single-player save loads have no multiplayer lobby convergence.
                mapStartSubscription = APIShared.Internal.MissionEvents.NativeStart.Subscribe(args =>
                {
                    if (args.IsBeforeInitialization)
                        FinalizeRosterForMapTransition(args.Context.IsSave && args.Context.Mode.IsRealMultiplayer);
                });
                if (mapStartSubscription == null)
                    throw new InvalidOperationException("The persistent map-start subscription could not be created.");
#endif
                active = true;
                RequestPublish();
#if !API_SHARED_PRESET_TESTS
#if API_SHARED_LOBBY_OBSERVER
                IApiShared api = ApiShared.Current;
                if (!api.TryGetLobbyState(
                        ownerGuid,
                        out ILobbyStateCapability lobbyState,
                        out NativeCapabilityDiagnostic diagnostic))
                {
                    throw new InvalidOperationException(
                        $"The process-wide lobby-state capability is unavailable: " +
                        $"state={diagnostic?.State}, reason={diagnostic?.Reason}");
                }
                if (!lobbyState.TryRegisterObserver(
                        "per-player-settings",
                        OnLobbyStateChanged,
                        out diagnostic))
                {
                    throw new InvalidOperationException(
                        $"The per-player lobby observer could not be registered: " +
                        $"state={diagnostic?.State}, reason={diagnostic?.Reason}");
                }
#else
                throw new InvalidOperationException(
                    "Per-player lobby settings require the APIShared lobby-state bridge.");
#endif
#endif
#if API_SHARED_PRESET_TESTS || API_SHARED_LOBBY_OBSERVER
                LogRoutine(
                    $"[{modName}] Shared per-player lobby convergence activated: " +
                    $"settings=[{string.Join(",", contract.Settings.Select(item => item.Property.Name))}], " +
                    $"required=[{string.Join(",", contract.Settings.Where(item => item.IsReportRequired).Select(item => item.Property.Name))}].");
#endif
            }
            catch (Exception ex)
            {
                Deactivate();
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Shared per-player lobby convergence activation failed: {ex}");
                throw;
            }
        }

        internal void Deactivate()
        {
            owner.PropertyChanged -= OnOwnerPropertyChanged;
#if !API_SHARED_PRESET_TESTS
            mapStartSubscription?.Dispose();
            mapStartSubscription = null;
#endif
            active = false;
            publishPending = false;
        }

        internal void RequestPublish()
        {
            publishPending = true;
            if (hasLobby && !rosterHasUnresolvedPlayers &&
                IsValidPlayerId(resolvedLocalPlayerId) &&
                playersById.ContainsKey(resolvedLocalPlayerId))
            {
                PublishLocalSettings(resolvedLocalPlayerId);
                RequestReadinessRefresh();
            }
        }

        internal bool ArePlayersReady(IEnumerable<int> playerIds, out string error)
        {
            int[] supplied = (playerIds ?? Enumerable.Empty<int>()).ToArray();
            if (supplied.Any(id => !IsValidPlayerId(id)))
            {
                error = "At least one supplied human player ID is invalid.";
                return false;
            }
            int[] expected = supplied
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (expected.Length == 0)
            {
                error = "No valid human player IDs were supplied.";
                return false;
            }
            if (rosterHasUnresolvedPlayers)
            {
                error = "At least one human lobby member has no stable player ID yet.";
                return false;
            }
            if (hasLobby && !expected.SequenceEqual(playersById.Keys.OrderBy(id => id)))
            {
                error = $"The requested human players [{string.Join(",", expected)}] do not match the converged lobby roster [{string.Join(",", playersById.Keys.OrderBy(id => id))}].";
                return false;
            }
            if (hasLobby && (!IsValidPlayerId(resolvedLocalPlayerId) || !playersById.ContainsKey(resolvedLocalPlayerId)))
            {
                error = "The local human player ID is not part of the converged lobby roster.";
                return false;
            }

            foreach (PerPlayerLobbySettingContract setting in contract.Settings.Where(item => item.IsReportRequired))
            {
                Array data = setting.GetData();
                foreach (int playerId in expected)
                {
                    object value = data.GetValue(playerId);
                    if (!setting.HasReport(value))
                    {
                        error = $"Player {playerId} has not reported [{setting.Property.Name}].";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        internal bool RemapForMapTransition(
            IReadOnlyDictionary<int, ulong> finalPlayers,
            int finalLocalPlayerId,
            out string error)
        {
            var normalized = new Dictionary<int, ulong>();
            foreach (KeyValuePair<int, ulong> player in finalPlayers ?? new Dictionary<int, ulong>())
            {
                if (!IsValidPlayerId(player.Key) || player.Value == 0 ||
                    normalized.ContainsKey(player.Key) || normalized.ContainsValue(player.Value))
                {
                    error = "The final human roster contains an invalid or duplicate player identity.";
                    SetReadiness(false, error);
                    return false;
                }
                normalized[player.Key] = player.Value;
            }

            ulong[] previousSteamIds = playersById.Values.OrderBy(value => value).ToArray();
            ulong[] finalSteamIds = normalized.Values.OrderBy(value => value).ToArray();
            if (!hasLobby || previousSteamIds.Length == 0 ||
                !previousSteamIds.SequenceEqual(finalSteamIds))
            {
                error = $"The final human roster [{FormatRoster(normalized)}] does not match the " +
                    $"converged lobby identities [{FormatRoster(playersById)}].";
                SetReadiness(false, error);
                return false;
            }
            if (!IsValidPlayerId(finalLocalPlayerId) || !normalized.ContainsKey(finalLocalPlayerId))
            {
                error = "The final local player ID is not part of the final human roster.";
                SetReadiness(false, error);
                return false;
            }

            bool changed = normalized.Count != playersById.Count ||
                normalized.Any(player =>
                    !playersById.TryGetValue(player.Key, out ulong previousSteamId) ||
                    previousSteamId != player.Value);
            if (changed)
            {
                foreach (PerPlayerLobbySettingContract setting in contract.Settings)
                {
                    Array data = setting.GetData();
                    var valuesBySteamId = new Dictionary<ulong, object>();
                    foreach (KeyValuePair<int, ulong> previousPlayer in playersById)
                        valuesBySteamId[previousPlayer.Value] = CloneValue(data.GetValue(previousPlayer.Key));
                    for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
                        data.SetValue(CloneValue(setting.CreateResetValue()), playerId);
                    foreach (KeyValuePair<int, ulong> finalPlayer in normalized)
                        data.SetValue(CloneValue(valuesBySteamId[finalPlayer.Value]), finalPlayer.Key);
                }

                playersById.Clear();
                foreach (KeyValuePair<int, ulong> player in normalized)
                    playersById[player.Key] = player.Value;
            }

            rosterHasUnresolvedPlayers = false;
            resolvedLocalPlayerId = finalLocalPlayerId;
            contract.LocalPlayerResolved?.Invoke(finalLocalPlayerId);
            contract.LobbyChanged?.Invoke(new PerPlayerLobbySnapshot(
                lobbyId,
                new Dictionary<int, ulong>(playersById),
                false,
                finalLocalPlayerId));
            if (!ArePlayersReady(playersById.Keys, out error))
            {
                SetReadiness(false, error);
                return false;
            }

            SetReadiness(true, string.Empty);
            if (changed)
            {
                LogRoutine(
                    $"[{modName}] Shared personal settings remapped to final game slots: " +
                    $"players=[{FormatRoster(playersById)}], localPlayerId={finalLocalPlayerId}.");
            }
            error = string.Empty;
            return true;
        }

        internal void Observe(
            ulong? currentLobbyId,
            IReadOnlyDictionary<int, ulong> currentPlayers,
            bool hasUnresolvedPlayers,
            int localPlayerId,
            bool preserveForMapTransition)
        {
            if (!currentLobbyId.HasValue)
            {
                if (preserveForMapTransition)
                    return;

                if (hasLobby || playersById.Count != 0)
                {
                    ResetSlots(Enumerable.Range(FirstPlayerId, LastPlayerId));
                    hasLobby = false;
                    lobbyId = 0;
                    playersById.Clear();
                    rosterHasUnresolvedPlayers = false;
                    resolvedLocalPlayerId = 0;
                    publishPending = false;
                    contract.LobbyChanged?.Invoke(PerPlayerLobbySnapshot.Empty);
                }
                SetReadiness(true, string.Empty);
                return;
            }

            // Domain observers run in the lobby only. Settings are immutable once
            // the map starts, so no file/status refresh may publish into a match.
            contract.Observe?.Invoke();

            var normalized = new Dictionary<int, ulong>();
            foreach (KeyValuePair<int, ulong> player in currentPlayers ?? new Dictionary<int, ulong>())
            {
                if (IsValidPlayerId(player.Key) && player.Value != 0)
                    normalized[player.Key] = player.Value;
            }

            bool sessionChanged = !hasLobby || lobbyId != currentLobbyId.Value;
            bool membershipChanged = sessionChanged ||
                normalized.Count != playersById.Count ||
                normalized.Any(player =>
                    !playersById.TryGetValue(player.Key, out ulong previousSteamId) ||
                    previousSteamId != player.Value);
            bool resolutionChanged = rosterHasUnresolvedPlayers != hasUnresolvedPlayers ||
                resolvedLocalPlayerId != localPlayerId;
            if (membershipChanged)
            {
                int[] slotsToReset = sessionChanged
                    ? Enumerable.Range(FirstPlayerId, LastPlayerId).ToArray()
                    : Enumerable.Range(FirstPlayerId, LastPlayerId)
                        .Where(id =>
                            playersById.TryGetValue(id, out ulong previousSteamId) &&
                            (!normalized.TryGetValue(id, out ulong currentSteamId) ||
                             currentSteamId != previousSteamId))
                        .ToArray();
                ResetSlots(slotsToReset);
                hasLobby = true;
                lobbyId = currentLobbyId.Value;
                playersById.Clear();
                foreach (KeyValuePair<int, ulong> player in normalized)
                    playersById[player.Key] = player.Value;
                publishPending = true;
                LogRoutine(
                    $"[{modName}] Shared per-player lobby roster changed: lobby={currentLobbyId.Value}, " +
                    $"sessionChanged={sessionChanged}, players=[{string.Join(",", normalized.Keys.OrderBy(id => id))}], " +
                    $"unresolved={hasUnresolvedPlayers}, resetSlots=[{string.Join(",", slotsToReset)}].");
            }

            bool localResolved = IsValidPlayerId(localPlayerId) && normalized.ContainsKey(localPlayerId);
            rosterHasUnresolvedPlayers = hasUnresolvedPlayers;
            resolvedLocalPlayerId = localPlayerId;
            if (membershipChanged || resolutionChanged)
            {
                contract.LobbyChanged?.Invoke(new PerPlayerLobbySnapshot(
                    currentLobbyId,
                    new Dictionary<int, ulong>(normalized),
                    hasUnresolvedPlayers,
                    localPlayerId));
            }
            if (publishPending && localResolved && !hasUnresolvedPlayers)
                PublishLocalSettings(localPlayerId);

            if (hasUnresolvedPlayers)
                SetReadiness(false, "At least one human lobby member has no stable player ID yet.");
            else if (!localResolved)
                SetReadiness(false, "The local human player ID is not part of the resolved lobby roster yet.");
            else if (!ArePlayersReady(normalized.Keys, out string error))
                SetReadiness(false, error);
            else
                SetReadiness(true, string.Empty);
        }

        private void PublishLocalSettings(int localPlayerId)
        {
            contract.BeforePublish?.Invoke();
            contract.LocalPlayerResolved?.Invoke(localPlayerId);
            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
            {
                Array data = setting.GetData();
                data.SetValue(CloneValue(setting.Property.GetValue(owner)), localPlayerId);
                owner.System_TriggerUpdate(setting.Property.Name);
            }
            publishPending = false;
            contract.Published?.Invoke();
            LogRoutine(
                $"[{modName}] Shared personal settings advertised for playerId={localPlayerId}, " +
                $"properties={contract.Settings.Count}.");
        }

        private void ResetSlots(IEnumerable<int> playerIds)
        {
            int[] slots = (playerIds ?? Enumerable.Empty<int>())
                .Where(IsValidPlayerId)
                .Distinct()
                .ToArray();
            if (slots.Length == 0)
                return;

            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
            {
                Array data = setting.GetData();
                foreach (int playerId in slots)
                    data.SetValue(CloneValue(setting.CreateResetValue()), playerId);
                isResettingSlots = true;
                try
                {
                    owner.System_TriggerUpdate(setting.DataProperty.Name);
                }
                finally
                {
                    isResettingSlots = false;
                }
            }
        }

        private void SetReadiness(bool value, string error)
        {
            error = error ?? string.Empty;
            if (isReady == value && string.Equals(readinessError, error, StringComparison.Ordinal))
                return;
            isReady = value;
            readinessError = error;
            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.IsPerPlayerLobbySettingsReady));
            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.PerPlayerLobbySettingsReadinessError));
        }

        private void OnOwnerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args?.PropertyName))
                return;
            if (contract.Settings.Any(item => item.DataProperty.Name == args.PropertyName))
            {
                if (isResettingSlots || isMirroringLocalSetting)
                    return;
                contract.RemoteDataChanged?.Invoke(args.PropertyName);
                RequestReadinessRefresh();
                return;
            }

            PerPlayerLobbySettingContract localSetting = contract.Settings.FirstOrDefault(
                item => item.Property.Name == args.PropertyName);
            if (localSetting != null && hasLobby &&
                IsValidPlayerId(resolvedLocalPlayerId) &&
                playersById.ContainsKey(resolvedLocalPlayerId))
            {
                // The transport does not echo a sender's packet back to itself. Keep
                // the local companion slot authoritative in Shared so individual mods
                // never need to resolve or guess their own player ID in a setter.
                localSetting.GetData().SetValue(
                    CloneValue(localSetting.Property.GetValue(owner)),
                    resolvedLocalPlayerId);
                isMirroringLocalSetting = true;
                try
                {
                    owner.System_TriggerUpdate(localSetting.DataProperty.Name);
                }
                finally
                {
                    isMirroringLocalSetting = false;
                }
                RequestReadinessRefresh();
            }
        }

        private void RequestReadinessRefresh()
        {
            if (!hasLobby)
                return;
            if (!ArePlayersReady(playersById.Keys, out string error))
                SetReadiness(false, error);
            else
                SetReadiness(true, string.Empty);
        }

#if !API_SHARED_PRESET_TESTS
        private void FinalizeRosterForMapTransition(bool multiplayerSave)
        {
            if (!hasLobby || !GameModeHelper.IsRealMultiplayer(multiplayerSave))
                return;

            if (!PlayerIdentityHelper.TryCaptureHumanRoster(
                preferInGameRoster: true,
                out Dictionary<int, ulong> finalPlayers,
                out string rosterError))
            {
                SetReadiness(false, rosterError);
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Shared final player roster resolution failed; dependent gameplay must remain blocked: {rosterError}");
                return;
            }

            PlayerIdentityResolution localIdentity =
                PlayerIdentityHelper.CaptureLocalPlayerId(
                    realMultiplayer: true,
                    preferInGameRoster: true);
            ReportIdentityDiagnostic(localIdentity);
            if (!localIdentity.IsResolved)
            {
                SetReadiness(false, localIdentity.Error);
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Shared final local player resolution failed; dependent gameplay must remain blocked: {localIdentity.Error}");
                return;
            }

            if (!RemapForMapTransition(finalPlayers, localIdentity.PlayerId, out string remapError))
            {
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Shared per-player map-transition remap failed; dependent gameplay must remain blocked: {remapError}");
            }
        }

#if API_SHARED_LOBBY_OBSERVER
        private void OnLobbyStateChanged(LobbyStateSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            if (!string.IsNullOrEmpty(snapshot.Error))
            {
                SetReadiness(false, snapshot.Error);
                ReportIdentityDiagnostic(new PlayerIdentityResolution(
                    0,
                    false,
                    snapshot.Error,
                    snapshot.Diagnostic));
                return;
            }
            if (!string.IsNullOrEmpty(snapshot.Diagnostic))
            {
                ReportIdentityDiagnostic(new PlayerIdentityResolution(
                    snapshot.LocalPlayerId,
                    true,
                    string.Empty,
                    snapshot.Diagnostic));
            }
            Observe(
                snapshot.LobbyId,
                snapshot.Players,
                snapshot.HasUnresolvedPlayers,
                snapshot.LocalPlayerId,
                snapshot.PreserveForMapTransition);
        }
#endif

        private void ReportIdentityDiagnostic(PlayerIdentityResolution identity)
        {
            string diagnostic = identity.IsResolved ? identity.Diagnostic : identity.Error;
            if (string.IsNullOrEmpty(diagnostic) ||
                string.Equals(lastIdentityDiagnostic, diagnostic, StringComparison.Ordinal))
                return;
            lastIdentityDiagnostic = diagnostic;
            DebugLogHelper.LogError(
                log,
                $"[{modName}] Shared player identity source mismatch: {diagnostic}");
        }

#endif

        private void LogRoutine(string message)
        {
            if (routineLoggingEnabled)
                DebugLogHelper.LogDebug(log, message);
        }

        private static string FormatRoster(IReadOnlyDictionary<int, ulong> players) =>
            string.Join(",", (players ?? new Dictionary<int, ulong>())
                .OrderBy(player => player.Key)
                .Select(player => $"{player.Key}:{player.Value}"));

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;

        internal static object CloneValue(object value)
        {
            if (!(value is Array source))
                return value;
            Array clone = (Array)source.Clone();
            for (int index = 0; index < clone.Length; index++)
            {
                if (clone.GetValue(index) is Array nested)
                    clone.SetValue(CloneValue(nested), index);
            }
            return clone;
        }
    }

    public sealed class PerPlayerLobbySettingsBuilder
    {
        private readonly PresetLobbyModSettingsViewModel owner;
        private readonly Dictionary<string, PerPlayerLobbySettingOptions> options = new Dictionary<string, PerPlayerLobbySettingOptions>(StringComparer.Ordinal);
        private Action beforePublish;
        private Action<int> localPlayerResolved;
        private Action<PerPlayerLobbySnapshot> lobbyChanged;
        private Action<string> remoteDataChanged;
        private Action published;
        private Action observe;

        internal PerPlayerLobbySettingsBuilder(PresetLobbyModSettingsViewModel owner) { this.owner = owner; }

        public PerPlayerLobbySettingsBuilder ResetSlotsWith(string propertyName, Func<object> resetValueFactory) { Get(propertyName).ResetValueFactory = resetValueFactory ?? throw new ArgumentNullException(nameof(resetValueFactory)); return this; }
        public PerPlayerLobbySettingsBuilder RequireReport(string propertyName, Func<object, bool> hasReport = null) { PerPlayerLobbySettingOptions item = Get(propertyName); item.IsReportRequired = true; item.HasReport = hasReport ?? (value => value != null); return this; }
        public PerPlayerLobbySettingsBuilder BeforePublish(Action callback) { beforePublish += callback; return this; }
        public PerPlayerLobbySettingsBuilder WhenLocalPlayerResolved(Action<int> callback) { localPlayerResolved += callback; return this; }
        public PerPlayerLobbySettingsBuilder WhenLobbyChanged(Action<PerPlayerLobbySnapshot> callback) { lobbyChanged += callback; return this; }
        public PerPlayerLobbySettingsBuilder WhenRemoteDataChanged(Action<string> callback) { remoteDataChanged += callback; return this; }
        public PerPlayerLobbySettingsBuilder AfterPublish(Action callback) { published += callback; return this; }
        public PerPlayerLobbySettingsBuilder OnObservation(Action callback) { observe += callback; return this; }

        internal PerPlayerLobbySettingsContract Build()
        {
            PropertyInfo[] properties = owner.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                bool host = property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;
                bool player = property.GetCustomAttribute<SyncPerPlayerAttribute>() != null;
                bool local = property.GetCustomAttribute<PresetLocalAttribute>() != null;
                int classifications = (host ? 1 : 0) + (player ? 1 : 0) + (local ? 1 : 0);
                if (classifications > 1)
                    throw new InvalidOperationException($"Setting [{owner.GetType().Name}.{property.Name}] has conflicting sync/preset classifications.");
            }

            var settings = new List<PerPlayerLobbySettingContract>();
            foreach (PropertyInfo property in properties.Where(item => item.GetCustomAttribute<SyncPerPlayerAttribute>() != null))
            {
                if (!property.CanRead)
                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] is not readable.");
                PropertyInfo dataProperty = owner.GetType().GetProperty(property.Name + "Data", BindingFlags.Instance | BindingFlags.Public);
                if (dataProperty == null || !dataProperty.CanRead || !dataProperty.PropertyType.IsArray)
                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] requires a readable [{property.Name}Data] array.");
                Type elementType = dataProperty.PropertyType.GetElementType();
                if (!elementType.IsAssignableFrom(property.PropertyType))
                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] has element type [{elementType}], expected [{property.PropertyType}].");
                Array data = dataProperty.GetValue(owner) as Array;
                if (data == null || data.Rank != 1 || data.Length < 9)
                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must be a one-dimensional array containing slots 0 through 8.");
                if (!ReferenceEquals(data, dataProperty.GetValue(owner)))
                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must return one stable array instance.");

                options.TryGetValue(property.Name, out PerPlayerLobbySettingOptions configured);
                configured = configured ?? new PerPlayerLobbySettingOptions();
                settings.Add(new PerPlayerLobbySettingContract(property, dataProperty, data, configured));
            }
            foreach (string configuredName in options.Keys)
                if (!settings.Any(item => item.Property.Name == configuredName))
                    throw new InvalidOperationException($"Per-player policy references non-[SyncPerPlayer] property [{owner.GetType().Name}.{configuredName}].");
            return new PerPlayerLobbySettingsContract(settings, beforePublish, localPlayerResolved, lobbyChanged, remoteDataChanged, published, observe);
        }

        private PerPlayerLobbySettingOptions Get(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("A property name is required.", nameof(propertyName));
            if (!options.TryGetValue(propertyName, out PerPlayerLobbySettingOptions value)) options[propertyName] = value = new PerPlayerLobbySettingOptions();
            return value;
        }
    }

    public sealed class PerPlayerLobbySnapshot
    {
        internal static readonly PerPlayerLobbySnapshot Empty = new PerPlayerLobbySnapshot(null, new Dictionary<int, ulong>(), false, 0);
        internal PerPlayerLobbySnapshot(ulong? lobbyId, IReadOnlyDictionary<int, ulong> players, bool unresolved, int localPlayerId)
        {
            LobbyId = lobbyId;
            Players = new ReadOnlyDictionary<int, ulong>(
                (players ?? new Dictionary<int, ulong>())
                    .ToDictionary(item => item.Key, item => item.Value));
            HasUnresolvedPlayers = unresolved;
            LocalPlayerId = localPlayerId;
        }
        public ulong? LobbyId { get; }
        public IReadOnlyDictionary<int, ulong> Players { get; }
        public bool HasUnresolvedPlayers { get; }
        public int LocalPlayerId { get; }
    }

    internal sealed class PerPlayerLobbySettingOptions { internal Func<object> ResetValueFactory; internal bool IsReportRequired; internal Func<object, bool> HasReport; }
    internal sealed class PerPlayerLobbySettingContract
    {
        private readonly Array data;
        private readonly PerPlayerLobbySettingOptions options;
        internal PerPlayerLobbySettingContract(PropertyInfo property, PropertyInfo dataProperty, Array data, PerPlayerLobbySettingOptions options) { Property = property; DataProperty = dataProperty; this.data = data; this.options = options; }
        internal PropertyInfo Property { get; }
        internal PropertyInfo DataProperty { get; }
        internal bool IsReportRequired => options.IsReportRequired;
        internal Array GetData() => data;
        internal object CreateResetValue() => options.ResetValueFactory != null ? options.ResetValueFactory() : (Property.PropertyType.IsValueType ? Activator.CreateInstance(Property.PropertyType) : null);
        internal bool HasReport(object value) => !IsReportRequired || (options.HasReport ?? (item => item != null))(value);
    }
    internal sealed class PerPlayerLobbySettingsContract
    {
        internal PerPlayerLobbySettingsContract(IReadOnlyList<PerPlayerLobbySettingContract> settings, Action beforePublish, Action<int> localPlayerResolved, Action<PerPlayerLobbySnapshot> lobbyChanged, Action<string> remoteDataChanged, Action published, Action observe) { Settings = settings; BeforePublish = beforePublish; LocalPlayerResolved = localPlayerResolved; LobbyChanged = lobbyChanged; RemoteDataChanged = remoteDataChanged; Published = published; Observe = observe; }
        internal IReadOnlyList<PerPlayerLobbySettingContract> Settings { get; }
        internal Action BeforePublish { get; }
        internal Action<int> LocalPlayerResolved { get; }
        internal Action<PerPlayerLobbySnapshot> LobbyChanged { get; }
        internal Action<string> RemoteDataChanged { get; }
        internal Action Published { get; }
        internal Action Observe { get; }
    }
}
