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

namespace Shared
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
                // SaveLifecycle: this finalizes a multiplayer lobby roster before map entry;
                // saved-game loads have no lobby roster to converge through this coordinator.
                mapStartSubscription = Shared.MissionEvents.NativeStart.Subscribe(args =>
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
                DebugLogHelper.LogInfo(log, message);
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

namespace Shared
{
    /// <summary>
    /// Persists a setting in the shared local preset file without exposing it to
    /// the Script Extender's multiplayer synchronization layer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PresetLocalAttribute : Attribute
    {
    }

    /// <summary>
    /// Adds two local presets to a Script Extender lobby-settings ViewModel while
    /// keeping the outer MessagePack dictionary readable by the Script Extender.
    /// </summary>
    public abstract class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel, IModSettingsWorkingCopyEndpoint
    {
        private static readonly MethodInfo NotifyRevertMethod =
            typeof(LobbyModSettingsBaseViewModel).GetMethod(
                "NotifyRevert",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private PresetController presetController;
        private int selectedPreset;
        private bool missionPresetContext;
        private bool missionPresetEditable;
        private bool isRealMultiplayer;
        private bool isLocalHost = true;
        private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;
#if !API_SHARED_PRESET_TESTS
        private string modSettingsSearchText = string.Empty;
        private string modSettingsSearchExactKey = string.Empty;
        private bool modSettingsSearchIncludeToolTips;
        private bool modSettingsSearchExpanded;
        private int modSettingsSearchFocusRequest;
        private bool presetSavePanelOpen;
        private bool presetLoadPanelOpen;
        private string presetSaveName = string.Empty;
        private string presetSaveDescription = string.Empty;
        private ModSettingsPresetListEntry selectedPresetLoadEntry;
        private ModSettingsPresetSaveTarget selectedPresetSaveTarget;
        private readonly ObservableCollection<ModSettingsPresetListEntry> presetLoadEntries =
            new ObservableCollection<ModSettingsPresetListEntry>();
        private readonly ObservableCollection<ModSettingsPresetSaveTarget> presetSaveTargets =
            new ObservableCollection<ModSettingsPresetSaveTarget>();
        private bool applyingPresetSaveBulkMode;
        private readonly ObservableCollection<PresetSaveSettingViewModel> presetSaveSettings =
            new ObservableCollection<PresetSaveSettingViewModel>();
        private readonly ObservableCollection<ModSettingsWorkingSource> settingsSources =
            new ObservableCollection<ModSettingsWorkingSource>();
        private ModSettingsWorkingSource selectedSettingsSource;
        private string preferredSettingsSourceToken = string.Empty;
        private PublishedModSettingsPreset pendingDeletePreset;
        private string pendingOverwriteId;
        private PresetSaveSelection[] pendingOverwriteSelections;
        private string presetInlineConfirmationTitle = string.Empty;
        private string presetInlineConfirmationMessage = string.Empty;
        private string presetOperationStatus = string.Empty;
        private bool presetOperationFailed;
#endif

        protected PresetLobbyModSettingsViewModel()
        {
#if !API_SHARED_PRESET_TESTS
            System_ToggleModSettingsSearchCommand = new RelayCommand(ToggleModSettingsSearch);
            System_ClearModSettingsSearchCommand = new RelayCommand(ClearModSettingsSearch);
            System_OpenPresetLoadCommand = new RelayCommand(OpenPresetLoad);
            System_ConfirmPresetLoadCommand = new RelayCommand(ConfirmPresetLoad);
            System_DeletePresetCommand = new RelayCommand(DeleteSelectedPreset);
            System_CancelPresetLoadCommand = new RelayCommand(CancelPresetLoad);
            System_OpenPresetSaveCommand = new RelayCommand(OpenPresetSave);
            System_ConfirmPresetSaveCommand = new RelayCommand(ConfirmPresetSave);
            System_CancelPresetSaveCommand = new RelayCommand(CancelPresetSave);
            System_LoadSettingsSourceCommand = new RelayCommand(LoadSelectedSettingsSource);
            System_ConfirmPresetInlineActionCommand = new RelayCommand(ConfirmPresetInlineAction);
            System_CancelPresetInlineActionCommand = new RelayCommand(CancelPresetInlineAction);
            System_DismissPresetStatusCommand = new RelayCommand(DismissPresetStatus);
            ModSettingsWorkingSourceRegistry.SourcesChanged += RebuildSettingsSources;
#endif
        }

        public bool HasHostSettings => presetController?.HasHostSettings ?? false;

        public bool HasClientSettings => presetController?.HasClientSettings ?? false;

        public bool HasHostSettingsActivation => presetController?.HasHostSettingsActivation ?? false;

        public bool HasClientSettingsActivation => presetController?.HasClientSettingsActivation ?? false;

        public bool HostSettingsEnabled
        {
            get => presetController?.HostSettingsEnabled ?? false;
            set => presetController?.SetHostSettingsEnabled(value);
        }

        public bool ClientSettingsEnabled
        {
            get => presetController?.ClientSettingsEnabled ?? false;
            set => presetController?.SetClientSettingsEnabled(value);
        }

        public bool IsLocalSettingsHost => isLocalHost;

        public bool IsRealMultiplayerContext => isRealMultiplayer;

        public bool MissionPresetEditable => missionPresetEditable;

        public bool IsMissionPresetSelected => missionPresetContext && selectedPreset == (presetController?.MissionPresetIndex ?? 2);

        public bool CanEditHostSettings =>
            isLocalHost && (!IsMissionPresetSelected || missionPresetEditable);

        public bool CanEditClientSettings => !IsMissionPresetSelected || missionPresetEditable;

        public bool CanToggleHostSettings =>
            HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;

        public bool CanToggleClientSettings =>
            HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;

        public bool CanChangePreset =>
            (!IsMissionPresetSelected || missionPresetEditable) && (isLocalHost || HasClientSettings);

        public bool CanResetSettings => CanEditHostSettings || (HasClientSettings && CanEditClientSettings);

        public Visibility PresetVisibility =>
            missionPresetContext || isLocalHost || HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ClientSettingsActivationVisibility =>
            HasClientSettingsActivation
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility HostReadOnlyNoticeVisibility =>
            HasHostSettings && isRealMultiplayer && !isLocalHost
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string HostOptionsText =>
            ResolveSettingsUiTextSafe("Common.HostOptions", "HOST OPTIONS");

        public string ClientOptionsText =>
            ResolveSettingsUiTextSafe("Common.ClientOptions", "LOCAL CLIENT OPTIONS");

        public string PresetText =>
            ResolveSettingsUiTextSafe("Common.Preset", "Preset");

        public string ModEnabledText =>
            ResolveSettingsUiTextSafe("Common.EnableMod", "Enable Mod");

        public string HostActivationLabelText =>
            ResolveSettingsUiTextSafe("Common.HostActivationLabel", "(Host-)");

        public string ClientActivationLabelText =>
            ResolveSettingsUiTextSafe("Common.ClientActivationLabel", "(Client settings)");

        public Visibility ActionsScopeNoticeVisibility =>
            isRealMultiplayer && HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string ActionsScopeNoticeText =>
            HasHostSettings && isLocalHost
                ? ResolveSettingsUiTextSafe(
                    "Common.ActionsScopeHost",
                    "Loading a preset or resetting settings affects host settings and your local client settings.")
                : ResolveSettingsUiTextSafe(
                    "Common.ActionsScopeClient",
                    "Loading a preset or resetting settings affects only your local client settings.");

        public string HostReadOnlyNoticeText =>
            ResolveSettingsUiTextSafe("Common.HostReadOnly", "Values from host - read-only");

        public string EnableModHelpText =>
            ResolveSettingsUiTextSafe("Common.EnableModHelp", "Enables or disables this mod for the match.");

        public string HostSettingsActivationHelpText =>
            ResolveSettingsUiTextSafe("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");

        public string ClientSettingsActivationHelpText =>
            ResolveSettingsUiTextSafe("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");

        public string PresetHelpText =>
            ResolveSettingsUiTextSafe("Common.PresetHelp", "Loads saved settings as an editable working copy.");

#if !API_SHARED_PRESET_TESTS
        public string System_PresetLoadText =>
            ResolveSettingsUiTextSafe("Common.PresetLoad", "Load preset");

        public string System_PresetSaveText =>
            ResolveSettingsUiTextSafe("Common.PresetSave", "Save preset");

        public string System_SettingsSourceText =>
            ResolveSettingsUiTextSafe("Common.SettingsSource", "Reset settings to");

        public string System_SettingsSourceLoadText =>
            ResolveSettingsUiTextSafe("Common.SettingsSourceLoad", "Reset");

        public string System_SettingsSourceHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.SettingsSourceHelp",
                "Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.");

        public ObservableCollection<ModSettingsWorkingSource> System_SettingsSources => settingsSources;

        public ModSettingsWorkingSource System_SelectedSettingsSource
        {
            get => selectedSettingsSource;
            set
            {
                if (ReferenceEquals(selectedSettingsSource, value)) return;
                selectedSettingsSource = value;
                base.OnPropertyChanged(nameof(System_SelectedSettingsSource));
                base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
            }
        }

        public bool System_CanLoadSettingsSource =>
            selectedSettingsSource != null && (!IsMissionPresetSelected || missionPresetEditable);

        public Visibility System_SettingsSourceVisibility =>
            presetController?.HasPersistentSettings == true ? Visibility.Visible : Visibility.Collapsed;

        public string System_PresetStatusText => presetController?.GetStatusText(
            ResolveSettingsUiTextSafe("Common.PresetBasedOn", "Based on"),
            ResolveSettingsUiTextSafe("Common.PresetModified", "modified"),
            ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets"),
            ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod"),
            ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets")) ?? string.Empty;

        public Visibility System_PresetStatusVisibility =>
            string.IsNullOrWhiteSpace(System_PresetStatusText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility System_PresetLoadPanelVisibility =>
            presetLoadPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<ModSettingsPresetListEntry> System_PresetLoadEntries =>
            presetLoadEntries;

        public ModSettingsPresetListEntry System_SelectedPresetLoadEntry
        {
            get => selectedPresetLoadEntry;
            set
            {
                if (ReferenceEquals(selectedPresetLoadEntry, value)) return;
                selectedPresetLoadEntry = value;
                base.OnPropertyChanged(nameof(System_SelectedPresetLoadEntry));
                base.OnPropertyChanged(nameof(System_CanDeleteSelectedPreset));
                base.OnPropertyChanged(nameof(System_PresetDeleteVisibility));
            }
        }

        public string System_PresetLoadConfirmText =>
            ResolveSettingsUiTextSafe("Common.PresetLoadConfirm", "Load");

        public string System_PresetLoadCancelText =>
            ResolveSettingsUiTextSafe("Common.PresetLoadCancel", "Cancel");

        public string System_PresetDeleteText =>
            ResolveSettingsUiTextSafe("Common.PresetDelete", "Delete");

        public bool System_CanDeleteSelectedPreset =>
            selectedPresetLoadEntry?.CanDelete == true;

        public Visibility System_PresetDeleteVisibility =>
            System_CanDeleteSelectedPreset ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<ModSettingsPresetSaveTarget> System_PresetSaveTargets =>
            presetSaveTargets;

        public ModSettingsPresetSaveTarget System_SelectedPresetSaveTarget
        {
            get => selectedPresetSaveTarget;
            set
            {
                if (ReferenceEquals(selectedPresetSaveTarget, value)) return;
                selectedPresetSaveTarget = value;
                if (value?.Preset != null)
                {
                    presetSaveName = value.Preset.Name;
                    presetSaveDescription = value.Preset.Description;
                    ApplyPresetToSaveRows(value.Preset);
                }
                else if (value != null)
                {
                    ResetPresetSaveForm();
                }
                base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
                base.OnPropertyChanged(nameof(System_PresetSaveName));
                base.OnPropertyChanged(nameof(System_PresetSaveDescription));
                base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
                base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            }
        }

        public string System_PresetSaveTargetText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveTarget", "Save as");

        public Visibility System_PresetSavePanelVisibility =>
            presetSavePanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public string System_PresetSaveName
        {
            get => presetSaveName;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(presetSaveName, normalized, StringComparison.Ordinal)) return;
                presetSaveName = normalized;
                base.OnPropertyChanged(nameof(System_PresetSaveName));
                base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
                base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            }
        }

        public string System_PresetSaveDescription
        {
            get => presetSaveDescription;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(presetSaveDescription, normalized, StringComparison.Ordinal)) return;
                presetSaveDescription = normalized;
                base.OnPropertyChanged(nameof(System_PresetSaveDescription));
            }
        }

        public ObservableCollection<PresetSaveSettingViewModel> System_PresetSaveSettings =>
            presetSaveSettings;

        public string System_PresetSaveNameText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveName", "Preset name");

        public string System_PresetSaveDescriptionText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveDescription", "Description (optional)");

        public string System_PresetSaveBulkModeText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveBulkMode", "Set all modes");

        public string System_PresetSaveBulkModeHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.PresetSaveBulkModeHelp",
                "Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.");

        public string System_PresetLoadSelectionHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.PresetLoadSelectionHelp",
                "Selecting a preset changes nothing until you choose Load.");

        public ComboBoxItem[] System_PresetSaveBulkModeOptions => new[]
        {
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeHostFixed", "Host Fixed") },
            new ComboBoxItem
            {
                Content = ResolveSettingsUiTextSafe("Common.PresetModeMixed", "Mixed"),
                IsEnabled = false,
            },
        };

        public int System_PresetSaveBulkModeIndex
        {
            get
            {
                if (presetSaveSettings.Count == 0)
                    return (int)PresetSaveBulkMode.HostFixed;
                int[] modes = presetSaveSettings.Select(item => item.SelectedModeIndex).Distinct().ToArray();
                if (modes.Length == 1)
                    return modes[0];
                if (presetSaveSettings.All(item =>
                    item.SelectedModeIndex == (int)(item.Scope == PresetSettingScope.Host
                        ? PublishedPresetValueMode.Fixed
                        : PublishedPresetValueMode.Player)))
                {
                    return (int)PresetSaveBulkMode.HostFixed;
                }
                return (int)PresetSaveBulkMode.Mixed;
            }
            set
            {
                if (value < 0 || value > (int)PresetSaveBulkMode.HostFixed)
                    return;
                applyingPresetSaveBulkMode = true;
                try
                {
                    foreach (PresetSaveSettingViewModel setting in presetSaveSettings)
                    {
                        setting.SelectedModeIndex = value == (int)PresetSaveBulkMode.HostFixed
                            ? (int)(setting.Scope == PresetSettingScope.Host
                                ? PublishedPresetValueMode.Fixed
                                : PublishedPresetValueMode.Player)
                            : value;
                    }
                }
                finally
                {
                    applyingPresetSaveBulkMode = false;
                }
                base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            }
        }

        public string System_PresetSaveConfirmText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveConfirm", "Save");

        public bool System_CanConfirmPresetSave =>
            !string.IsNullOrWhiteSpace(presetSaveName);

        public string System_PresetSaveConfirmHelpText => System_CanConfirmPresetSave
            ? System_PresetSaveConfirmText
            : ResolveSettingsUiTextSafe("Common.PresetSaveNameRequired", "Enter a preset name before saving.");

        public string System_PresetSaveCancelText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");

        public RelayCommand System_OpenPresetLoadCommand { get; }

        public RelayCommand System_ConfirmPresetLoadCommand { get; }

        public RelayCommand System_DeletePresetCommand { get; }

        public RelayCommand System_CancelPresetLoadCommand { get; }

        public RelayCommand System_OpenPresetSaveCommand { get; }

        public RelayCommand System_ConfirmPresetSaveCommand { get; }

        public RelayCommand System_CancelPresetSaveCommand { get; }

        public RelayCommand System_LoadSettingsSourceCommand { get; }

        public RelayCommand System_ConfirmPresetInlineActionCommand { get; }

        public RelayCommand System_CancelPresetInlineActionCommand { get; }

        public RelayCommand System_DismissPresetStatusCommand { get; }

        public string System_PresetInlineConfirmationTitle => presetInlineConfirmationTitle;
        public string System_PresetInlineConfirmationMessage => presetInlineConfirmationMessage;
        public Visibility System_PresetInlineConfirmationVisibility =>
            pendingDeletePreset != null || pendingOverwriteId != null ? Visibility.Visible : Visibility.Collapsed;
        public string System_PresetInlineConfirmText => ResolveSettingsUiTextSafe("Common.PresetConfirm", "Confirm");
        public string System_PresetInlineCancelText => ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");
        public string System_PresetOperationStatusText => presetOperationStatus;
        public Visibility System_PresetOperationStatusVisibility =>
            presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public Visibility System_PresetOperationErrorVisibility =>
            presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility System_PresetOperationSuccessVisibility =>
            Visibility.Collapsed;
        public string System_PresetStatusDismissText => ResolveSettingsUiTextSafe("Common.PresetStatusDismiss", "Close");

        public string System_ModSettingsSearchText
        {
            get => modSettingsSearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(modSettingsSearchText, normalized, StringComparison.Ordinal) &&
                    modSettingsSearchExactKey.Length == 0)
                {
                    return;
                }
                modSettingsSearchText = normalized;
                modSettingsSearchExactKey = string.Empty;
                RaiseModSettingsSearchProperties();
            }
        }

        public bool System_ModSettingsSearchIncludeToolTips
        {
            get => modSettingsSearchIncludeToolTips;
            set
            {
                if (modSettingsSearchIncludeToolTips == value)
                    return;
                modSettingsSearchIncludeToolTips = value;
                RaiseModSettingsSearchProperties();
            }
        }

        public string System_ModSettingsSearchExactKey => modSettingsSearchExactKey;

        public int System_ModSettingsSearchFocusRequest => modSettingsSearchFocusRequest;

        public bool System_ModSettingsSearchHasActiveFilter =>
            modSettingsSearchExactKey.Length > 0 ||
            !string.IsNullOrWhiteSpace(modSettingsSearchText);

        public Visibility System_ModSettingsSearchPanelVisibility =>
            modSettingsSearchExpanded ? Visibility.Visible : Visibility.Collapsed;

        public Visibility System_ModSettingsSearchInactiveVisibility =>
            System_ModSettingsSearchHasActiveFilter ? Visibility.Collapsed : Visibility.Visible;

        public Visibility System_ModSettingsSearchNoResultsVisibility =>
            System_ModSettingsSearchHasActiveFilter &&
            !ModSettingsSearch.HasMatches(
                this,
                modSettingsSearchText,
                modSettingsSearchIncludeToolTips,
                modSettingsSearchExactKey)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string System_ModSettingsSearchLabelText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchLabel", "Search");

        public string System_ModSettingsSearchHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchHelp", "Search setting titles. Optionally include tooltips.");

        public string System_ModSettingsSearchToggleHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchToggleHelp", "Show or hide the settings search.");

        public string System_ModSettingsSearchIncludeToolTipsText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTips", "Search tooltips");

        public string System_ModSettingsSearchIncludeToolTipsHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTipsHelp", "Also search the explanatory tooltips of settings.");

        public string System_ModSettingsSearchClearHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchClearHelp", "Clear the settings filter.");

        public string System_ModSettingsSearchNoResultsText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchNoResults", "No matching settings found.");

        public RelayCommand System_ToggleModSettingsSearchCommand { get; }

        public RelayCommand System_ClearModSettingsSearchCommand { get; }

        private void OpenPresetLoad()
        {
            if (presetLoadPanelOpen)
            {
                presetLoadPanelOpen = false;
                RaisePresetDialogProperties();
                return;
            }
            presetSavePanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
            presetLoadPanelOpen = true;
            RaisePresetDialogProperties();
        }

        private void ConfirmPresetLoad()
        {
            if (selectedPresetLoadEntry?.Preset == null)
                return;

            try
            {
                presetController?.LoadPreset(selectedPresetLoadEntry.Preset);
                presetLoadPanelOpen = false;
                RaisePresetDialogProperties();
                RaiseAccessProperties();
                DismissPresetStatus();
            }
            catch (Exception exception)
            {
                SetPresetStatus(
                    ResolveSettingsUiTextSafe("Common.PresetLoadFailedTitle", "Preset load failed") + ": " + exception.Message,
                    true);
            }
        }

        private void CancelPresetLoad()
        {
            presetLoadPanelOpen = false;
            RaisePresetDialogProperties();
        }

        private void DeleteSelectedPreset()
        {
            PublishedModSettingsPreset preset = selectedPresetLoadEntry?.Preset;
            if (preset == null || !System_CanDeleteSelectedPreset)
                return;

            string message = ResolveSettingsUiTextSafe(
                "Common.PresetDeleteConfirm",
                "The personal preset will be permanently deleted. Continue?") +
                Environment.NewLine + Environment.NewLine + preset.Name;
            pendingDeletePreset = preset;
            pendingOverwriteId = null;
            pendingOverwriteSelections = null;
            presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetDeleteTitle", "Delete personal preset");
            presetInlineConfirmationMessage = message;
            RaisePresetInlineProperties();
        }

        private void RebuildSettingsSources()
        {
            if (presetController == null) return;
            string previous = selectedSettingsSource?.Id;
            settingsSources.Clear();
            settingsSources.Add(new ModSettingsWorkingSource
            {
                Id = ModSettingsWorkingSourceRegistry.ModDefaultsId,
                Kind = ModSettingsWorkingSourceKind.ModDefault,
                DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceDefaults", "Mod defaults"),
            });
            foreach (ModSettingsWorkingSource source in ModSettingsWorkingSourceRegistry.GetProviderSources(presetController.TargetGuid))
            {
                if (source != null && !string.IsNullOrWhiteSpace(source.Id) &&
                    !string.Equals(source.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    if (source.Kind == ModSettingsWorkingSourceKind.Trail)
                        source.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceTrail", "Trail settings");
                    else if (source.Kind == ModSettingsWorkingSourceKind.Map)
                        source.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceMap", "Map settings");
                    settingsSources.Add(source);
                }
            }
            ModSettingsWorkingSource preferredSource = settingsSources.FirstOrDefault(item => item.IsPreferred) ??
                settingsSources.FirstOrDefault(item => string.Equals(item.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal));
            string preferred = preferredSource?.Id ?? ModSettingsWorkingSourceRegistry.ModDefaultsId;
            string preferredToken = preferred + "\n" + (preferredSource?.PreferenceContextId ?? string.Empty);
            bool preferredChanged = !string.Equals(
                preferredSettingsSourceToken,
                preferredToken,
                StringComparison.Ordinal);
            selectedSettingsSource = (preferredChanged
                    ? settingsSources.FirstOrDefault(item => string.Equals(item.Id, preferred, StringComparison.Ordinal))
                    : settingsSources.FirstOrDefault(item => string.Equals(item.Id, previous, StringComparison.Ordinal))) ??
                settingsSources.FirstOrDefault(item => string.Equals(item.Id, preferred, StringComparison.Ordinal)) ??
                settingsSources.FirstOrDefault();
            preferredSettingsSourceToken = preferredToken;
            base.OnPropertyChanged(nameof(System_SettingsSources));
            base.OnPropertyChanged(nameof(System_SelectedSettingsSource));
            base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
            base.OnPropertyChanged(nameof(System_SettingsSourceVisibility));
        }

        private void LoadSelectedSettingsSource()
        {
            ModSettingsWorkingSource source = selectedSettingsSource;
            if (source == null || !System_CanLoadSettingsSource) return;
            try
            {
                if (string.Equals(source.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    if (IsMissionPresetSelected)
                        ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, source.Id);
                    else
                        presetController.ApplyDefaultsAsWorkingCopy();
                }
                else
                {
                    ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, source.Id);
                }
                DismissPresetStatus();
                RaiseAccessProperties();
            }
            catch (Exception exception)
            {
                SetPresetStatus(ResolveSettingsUiTextSafe("Common.SettingsSourceLoadFailed", "Could not reset settings") + ": " + exception.Message, true);
            }
        }

        private void CompletePresetDelete(PublishedModSettingsPreset preset)
        {
            try
            {
                presetController?.DeletePersonalPreset(preset);
                RebuildPresetDialogCatalogs();
                selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
                RaisePresetDialogProperties();
                RaiseAccessProperties();
                DismissPresetStatus();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure("delete", preset?.Id, exception);
                SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetDeleteFailedTitle", "Preset deletion failed") + ": " + exception.Message, true);
            }
        }

        private void OpenPresetSave()
        {
            if (presetSavePanelOpen)
            {
                presetSavePanelOpen = false;
                RaisePresetSaveProperties();
                return;
            }
            presetLoadPanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            selectedPresetSaveTarget = presetSaveTargets.FirstOrDefault();
            ExecutePresetAction();
            base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
        }

        private void RebuildPresetDialogCatalogs()
        {
            presetLoadEntries.Clear();
            presetSaveTargets.Clear();
            presetSaveTargets.Add(new ModSettingsPresetSaveTarget
            {
                DisplayText = ResolveSettingsUiTextSafe("Common.PresetSaveNew", "New personal preset"),
            });

            foreach (PublishedModSettingsPreset preset in presetController?.PublishedPresets ??
                Array.Empty<PublishedModSettingsPreset>())
            {
                string sourceLabel;
                switch (preset.SourceKind)
                {
                    case ModSettingsPresetSourceKind.Personal:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets");
                        break;
                    case ModSettingsPresetSourceKind.Bundled:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod");
                        break;
                    default:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets") +
                            ": " + preset.ProviderName;
                        break;
                }
                presetLoadEntries.Add(new ModSettingsPresetListEntry
                {
                    Preset = preset,
                    SourceLabel = sourceLabel,
                });
                if (preset.CanOverwrite)
                {
                    presetSaveTargets.Add(new ModSettingsPresetSaveTarget
                    {
                        Preset = preset,
                        DisplayText = preset.Name,
                    });
                }
            }
        }

        private void ApplyPresetToSaveRows(PublishedModSettingsPreset preset)
        {
            if (preset == null)
                return;
            foreach (PresetSaveSettingViewModel row in presetSaveSettings)
            {
                if (preset.Settings.TryGetValue(row.PropertyName, out PublishedPresetSetting setting))
                {
                    row.SelectedModeIndex = (int)setting.Mode;
                }
                else
                {
                    row.SelectedModeIndex = (int)PublishedPresetValueMode.Player;
                }
            }
        }

        private void RaisePresetDialogProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetLoadPanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetSavePanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetLoadEntries));
            base.OnPropertyChanged(nameof(System_SelectedPresetLoadEntry));
            base.OnPropertyChanged(nameof(System_CanDeleteSelectedPreset));
            base.OnPropertyChanged(nameof(System_PresetDeleteVisibility));
            base.OnPropertyChanged(nameof(System_PresetSaveTargets));
            base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
            base.OnPropertyChanged(nameof(System_PresetStatusText));
            base.OnPropertyChanged(nameof(System_PresetStatusVisibility));
        }

        private void ExecutePresetAction()
        {
            presetSaveSettings.Clear();
            string[] modeOptions =
            {
                ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default"),
                ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player"),
                ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed"),
            };
            foreach (PresetSettingDescriptor descriptor in System_GetPresetSettingDescriptors())
            {
                var setting = new PresetSaveSettingViewModel(
                    descriptor,
                    ResolvePresetSettingScopeText(descriptor.Scope),
                    modeOptions);
                setting.PropertyChanged += OnPresetSaveSettingPropertyChanged;
                presetSaveSettings.Add(setting);
            }
            ResetPresetSaveForm();
            presetSavePanelOpen = true;
            RaisePresetSaveProperties();
        }

        private void ResetPresetSaveForm()
        {
            foreach (PresetSaveSettingViewModel setting in presetSaveSettings)
            {
                setting.SelectedModeIndex = (int)(setting.Scope == PresetSettingScope.Host
                    ? PublishedPresetValueMode.Fixed
                    : PublishedPresetValueMode.Player);
            }
            presetSaveName = string.Empty;
            presetSaveDescription = string.Empty;
            base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
        }

        private void OnPresetSaveSettingPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!applyingPresetSaveBulkMode &&
                string.Equals(args?.PropertyName, nameof(PresetSaveSettingViewModel.SelectedModeIndex), StringComparison.Ordinal))
            {
                base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            }
        }

        private string ResolvePresetSettingScopeText(PresetSettingScope scope)
        {
            switch (scope)
            {
                case PresetSettingScope.Host:
                    return ResolveSettingsUiTextSafe("Common.PresetScopeHost", "Host");
                case PresetSettingScope.Player:
                    return ResolveSettingsUiTextSafe("Common.PresetScopePlayer", "Player");
                case PresetSettingScope.Local:
                    return ResolveSettingsUiTextSafe("Common.PresetScopeLocal", "Local");
                default:
                    return scope.ToString();
            }
        }

        private void ConfirmPresetSave()
        {
            try
            {
                PresetSaveSelection[] selections = CreatePresetSaveSelections();
                PublishedModSettingsPreset existing = selectedPresetSaveTarget?.Preset;
                if (existing != null)
                {
                    pendingDeletePreset = null;
                    pendingOverwriteId = existing.Id;
                    pendingOverwriteSelections = selections;
                    presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetSaveOverwriteTitle", "Overwrite personal preset");
                    presetInlineConfirmationMessage = ResolveSettingsUiTextSafe("Common.PresetSaveOverwrite", "The selected personal preset will be completely replaced. Continue?");
                    RaisePresetInlineProperties();
                    return;
                }

                string id = presetController?.CreateUniquePersonalPresetId(presetSaveName) ??
                    CreatePublishedPresetId(presetSaveName);
                System_SavePersonalPreset(
                    id,
                    presetSaveName,
                    presetSaveDescription,
                    selections,
                    overwrite: false);
                ShowPresetSaveCompleted();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure("save", selectedPresetSaveTarget?.Preset?.Id, exception);
                ShowPresetSaveError(exception);
            }
        }

        private PresetSaveSelection[] CreatePresetSaveSelections() =>
            presetSaveSettings.Select(item => item.ToSelection()).ToArray();

        private void CompletePresetSave(string id, PresetSaveSelection[] selections, bool overwrite)
        {
            try
            {
                System_SavePersonalPreset(
                    id,
                    presetSaveName,
                    presetSaveDescription,
                    selections,
                    overwrite);
                ShowPresetSaveCompleted();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure(overwrite ? "overwrite" : "save", id, exception);
                ShowPresetSaveError(exception);
            }
        }

        private void ShowPresetSaveCompleted()
        {
            presetSavePanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            RaisePresetSaveProperties();
            DismissPresetStatus();
        }

        private void ShowPresetSaveError(Exception exception)
        {
            SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetSaveFailedTitle", "Preset save failed") + ": " + exception.Message, true);
        }

        private void ConfirmPresetInlineAction()
        {
            PublishedModSettingsPreset delete = pendingDeletePreset;
            string overwriteId = pendingOverwriteId;
            PresetSaveSelection[] selections = pendingOverwriteSelections;
            ClearPresetInlineConfirmation();
            if (delete != null) CompletePresetDelete(delete);
            else if (overwriteId != null) CompletePresetSave(overwriteId, selections ?? Array.Empty<PresetSaveSelection>(), true);
        }

        private void CancelPresetInlineAction()
        {
            if (pendingDeletePreset != null) presetController?.LogPresetOperationCancelled("delete", pendingDeletePreset.Id);
            else if (pendingOverwriteId != null) presetController?.LogPresetOperationCancelled("overwrite", pendingOverwriteId);
            ClearPresetInlineConfirmation();
        }

        private void ClearPresetInlineConfirmation()
        {
            pendingDeletePreset = null;
            pendingOverwriteId = null;
            pendingOverwriteSelections = null;
            presetInlineConfirmationTitle = string.Empty;
            presetInlineConfirmationMessage = string.Empty;
            RaisePresetInlineProperties();
        }

        private void SetPresetStatus(string message, bool failed)
        {
            presetOperationStatus = message ?? string.Empty;
            presetOperationFailed = failed;
            RaisePresetInlineProperties();
        }

        private void DismissPresetStatus() => SetPresetStatus(string.Empty, false);

        private void RaisePresetInlineProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationTitle));
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationMessage));
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationStatusText));
            base.OnPropertyChanged(nameof(System_PresetOperationStatusVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationErrorVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationSuccessVisibility));
        }

        private void CancelPresetSave()
        {
            presetSavePanelOpen = false;
            RaisePresetSaveProperties();
        }

        private void RaisePresetSaveProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetSavePanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetSaveName));
            base.OnPropertyChanged(nameof(System_PresetSaveDescription));
            base.OnPropertyChanged(nameof(System_PresetSaveSettings));
            base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
            base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            RaisePresetDialogProperties();
        }

        private static string CreatePublishedPresetId(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new InvalidDataException("A preset name is required.");
            var result = new System.Text.StringBuilder(trimmed.Length);
            bool separator = false;
            foreach (char character in trimmed.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    result.Append(character);
                    separator = false;
                }
                else if (!separator)
                {
                    result.Append('-');
                    separator = true;
                }
            }
            string id = result.ToString().Trim('-');
            if (id.Length == 0)
                throw new InvalidDataException("The preset name does not contain a usable id.");
            return id.Length <= 128 ? id : id.Substring(0, 128).TrimEnd('-');
        }

        /// <summary>Safe reflection bridge used by the optional global search host.</summary>
        public bool System_ApplyModSettingsSearchTarget(string key, string title)
        {
            string normalizedKey = ModSettingsSearchMatcher.Normalize(key);
            if (normalizedKey.Length == 0)
                return false;

            modSettingsSearchText = title ?? string.Empty;
            modSettingsSearchExactKey = normalizedKey;
            modSettingsSearchExpanded = true;
            RaiseModSettingsSearchProperties();
            return true;
        }

        private void ToggleModSettingsSearch()
        {
            modSettingsSearchExpanded = !modSettingsSearchExpanded;
            RaiseModSettingsSearchProperties();
            if (!modSettingsSearchExpanded)
                return;

            unchecked
            {
                modSettingsSearchFocusRequest++;
                if (modSettingsSearchFocusRequest <= 0)
                    modSettingsSearchFocusRequest = 1;
            }
            base.OnPropertyChanged(nameof(System_ModSettingsSearchFocusRequest));
        }

        private void ClearModSettingsSearch()
        {
            modSettingsSearchText = string.Empty;
            modSettingsSearchExactKey = string.Empty;
            RaiseModSettingsSearchProperties();
        }

        private void RaiseModSettingsSearchProperties()
        {
            base.OnPropertyChanged(nameof(System_ModSettingsSearchText));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchIncludeToolTips));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchExactKey));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchHasActiveFilter));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchPanelVisibility));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchInactiveVisibility));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchNoResultsVisibility));
        }
#endif

        // Compatibility alias for older views. New XAML binds host and client
        // sections separately so multiplayer and Trail locks remain independent.
        public bool AreSettingsEditable => CanEditHostSettings;

        public bool IsMissionPresetActive => missionPresetContext;

        protected virtual string ResolveSettingsUiText(string key, string fallback) => fallback;

        private string ResolveSettingsUiTextSafe(string key, string fallback)
        {
            string resolved = ResolveSettingsUiText(key, fallback);
            return string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, key, StringComparison.Ordinal)
                ? ResolveApiSharedFallback(key, fallback)
                : resolved;
        }

        private static string ResolveApiSharedFallback(string key, string english)
        {
            string language = string.Empty;
#if !API_SHARED_PRESET_TESTS
            try { language = GameAssetManagerAPI.Instance.CurrentLanguage ?? string.Empty; }
            catch { }
#endif
            if (!language.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                return english;
            switch (key)
            {
                case "Common.PresetLoad": return "Preset laden";
                case "Common.PresetSave": return "Preset speichern";
                case "Common.PresetBasedOn": return "Basiert auf";
                case "Common.PresetModified": return "geändert";
                case "Common.PresetLoadConfirm": return "Laden";
                case "Common.PresetLoadCancel": return "Abbrechen";
                case "Common.PresetDelete": return "Löschen";
                case "Common.PresetDeleteTitle": return "Eigenes Preset löschen";
                case "Common.PresetDeleteConfirm": return "Das eigene Preset wird endgültig gelöscht. Fortfahren?";
                case "Common.PresetDeleteFailedTitle": return "Preset konnte nicht gelöscht werden";
                case "Common.PresetSaveTarget": return "Speichern als";
                case "Common.PresetSaveNew": return "Neues persönliches Preset";
                case "Common.PresetSaveName": return "Presetname";
                case "Common.PresetSaveDescription": return "Beschreibung (optional)";
                case "Common.PresetSaveBulkMode": return "Alle Modi setzen";
                case "Common.PresetSaveBulkModeHelp": return "Standard: Mod-Standard verwenden. Spieler: aktuellen Spielerwert behalten. Fest: gespeicherten Wert anwenden. Host fest: Hostwerte festlegen, Spieler-/lokale Werte behalten.";
                case "Common.PresetLoadSelectionHelp": return "Die Auswahl ändert noch nichts. Erst Laden wendet das Preset an.";
                case "Common.PresetSaveCancel": return "Abbrechen";
                case "Common.PresetSourcePersonal": return "Eigene Presets";
                case "Common.PresetSourceBundled": return "Mit diesem Mod geliefert";
                case "Common.PresetSourceExternal": return "Externe Presets";
                case "Common.PresetSaveConfirm": return "Speichern";
                case "Common.PresetSaveNameRequired": return "Vor dem Speichern einen Presetnamen eingeben.";
                case "Common.PresetModeHostFixed": return "Host fest";
                case "Common.PresetSaveOverwriteTitle": return "Eigenes Preset überschreiben";
                case "Common.PresetSaveOverwrite": return "Das gewählte eigene Preset wird vollständig ersetzt. Fortfahren?";
                case "Common.PresetSaveFailedTitle": return "Preset konnte nicht gespeichert werden";
                case "Common.PresetLoadFailedTitle": return "Preset konnte nicht geladen werden";
                case "Common.SettingsSource": return "Einstellungen zurücksetzen auf";
                case "Common.SettingsSourceLoad": return "Zurücksetzen";
                case "Common.SettingsSourceHelp": return "Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.";
                case "Common.SettingsSourceDefaults": return "Mod-Standards";
                case "Common.SettingsSourceTrail": return "Trail-Einstellungen";
                case "Common.SettingsSourceMap": return "Map-Einstellungen";
                case "Common.SettingsSourceLoadFailed": return "Einstellungen konnten nicht zurückgesetzt werden";
                case "Common.PresetConfirm": return "Bestätigen";
                case "Common.PresetStatusDismiss": return "Schließen";
                default: return english;
            }
        }

        protected bool IsApplyingSettingsSnapshot =>
            presetController?.IsApplyingSnapshot == true;

        /// <summary>
        /// Replaces one captured code-default value without changing the current working settings.
        /// This is intended for defaults which can only be materialized after dynamic discovery.
        /// </summary>
        protected void SetModDefaultValue<T>(string propertyName, T value)
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before dynamic defaults are updated.");
            presetController.SetDefaultValue(propertyName, value);
        }

        protected virtual void OnSettingsSnapshotApplied()
        {
        }

        /// <summary>
        /// Declares the few domain-specific parts of personal settings. Transport,
        /// player-slot ownership, lobby convergence and readiness stay in APIShared.
        /// </summary>
        protected virtual void ConfigurePerPlayerLobbySettings(
            PerPlayerLobbySettingsBuilder settings)
        {
        }

        public bool IsPerPlayerLobbySettingsReady =>
            perPlayerSettingsCoordinator?.IsReady ?? true;

        public string PerPlayerLobbySettingsReadinessError =>
            perPlayerSettingsCoordinator?.ReadinessError ?? string.Empty;

        public void System_RequestPerPlayerSettingsPublish()
        {
            perPlayerSettingsCoordinator?.RequestPublish();
        }

#if !API_SHARED_PRESET_TESTS
        // SerpsModsHost discovers this method by reflection. Keeping the bridge on the
        // common base type lets every mod remain usable without the optional pack host.
        public IReadOnlyList<ModSettingsSearchEntry> System_GetModSettingsSearchEntries(
            Noesis.FrameworkElement view) =>
            ModSettingsSearch.Export(this, view);

#endif

        public bool System_ArePerPlayerSettingsReady(
            IEnumerable<int> playerIds,
            out string error)
        {
            if (perPlayerSettingsCoordinator == null)
            {
                error = string.Empty;
                return true;
            }

            return perPlayerSettingsCoordinator.ArePlayersReady(playerIds, out error);
        }

#if API_SHARED_PRESET_TESTS
        internal void System_TestObservePerPlayerLobby(
            ulong? lobbyId,
            IReadOnlyDictionary<int, ulong> players,
            bool hasUnresolvedPlayers,
            int localPlayerId,
            bool preserveForMapTransition = false)
        {
            perPlayerSettingsCoordinator?.Observe(
                lobbyId,
                players,
                hasUnresolvedPlayers,
                localPlayerId,
                preserveForMapTransition);
        }

        internal bool System_TestRemapPerPlayerLobbyForMapTransition(
            IReadOnlyDictionary<int, ulong> players,
            int localPlayerId,
            out string error)
        {
            if (perPlayerSettingsCoordinator == null)
            {
                error = "The per-player settings coordinator is unavailable.";
                return false;
            }
            return perPlayerSettingsCoordinator.RemapForMapTransition(
                players,
                localPlayerId,
                out error);
        }
#endif

        /// <summary>
        /// Authorizes a settings mutation before any backing state is changed.
        /// Preset and Trail snapshots are trusted internal applications; all other
        /// writes use the Script Extender's ownership gate.
        /// </summary>
        protected bool CanMutateSetting([CallerMemberName] string propertyName = null)
        {
            if (presetController?.IsApplyingSnapshot == true)
                return true;

            // The Extender reaches the setter only after it has verified the packet's
            // sender and opened its authorised-update scope. A read-only Trail locks
            // local edits, but must not reject that authoritative host state.
            if (PresetController.IsNetworkSyncInProgress())
                return CanEdit(propertyName);

            System_RefreshSettingsAccess();
            if (IsMissionPresetSelected && !missionPresetEditable)
            {
                NotifyRejectedProperty(propertyName);
                return false;
            }

            return CanEdit(propertyName);
        }

        /// <summary>
        /// Also refreshes editable proxy properties after a rejected write. The
        /// Extender's private revert path keeps these notifications out of sync
        /// and storage just like the primary property notification.
        /// </summary>
        protected bool CanMutateSettingWithDependents(
            string propertyName,
            params string[] dependentPropertyNames)
        {
            if (CanMutateSetting(propertyName))
                return true;

            if (dependentPropertyNames == null)
                return false;

            foreach (string dependentPropertyName in dependentPropertyNames)
            {
                if (!string.IsNullOrEmpty(dependentPropertyName) &&
                    !string.Equals(propertyName, dependentPropertyName, StringComparison.Ordinal))
                {
                    NotifyRejectedProperty(dependentPropertyName);
                }
            }

            return false;
        }

        private void NotifyRejectedProperty(string propertyName)
        {
            if (NotifyRevertMethod != null && !string.IsNullOrEmpty(propertyName))
                NotifyRevertMethod.Invoke(this, new object[] { propertyName });
        }

#if API_SHARED_PRESET_TESTS
        internal int System_WorkingStateWriteCount => presetController?.TestWriteCount ?? 0;

        // Retained only in the source-linked regression harness for hostile legacy selection attempts.
        public int SelectedPreset
        {
            get => selectedPreset;
            set
            {
                int normalized = presetController?.NormalizeSelection(value, missionPresetContext) ??
                    (value == 1 ? 1 : 0);
                if (selectedPreset == normalized)
                    return;

                if (presetController == null)
                {
                    selectedPreset = normalized;
                    base.OnPropertyChanged(nameof(SelectedPreset));
                    return;
                }

                presetController.SwitchTo(normalized);
            }
        }
#endif

        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName,
            string targetGuid,
            Version targetVersion,
            bool logRoutineActivity = true)
        {
            if (presetController != null)
                throw new InvalidOperationException($"Preset storage for [{modName}] was already prepared.");

            presetController = new PresetController(
                this,
                log,
                pluginAssemblyLocation,
                modName,
                targetGuid,
                targetVersion,
                logRoutineActivity);
            presetController.CaptureDefaults();
#if !API_SHARED_PRESET_TESTS
            RebuildSettingsSources();
#endif
            PropertyChanged += (_, __) => System_RefreshSettingsAccess();
            System_RefreshSettingsAccess();
        }

#if API_SHARED_PRESET_TESTS
        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName)
        {
            PreparePresets(
                log,
                pluginAssemblyLocation,
                modName,
                "Tests." + modName,
                new Version(1, 0, 0));
        }
#endif

        internal void ActivatePresets()
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before it is activated.");

            presetController.Activate();
        }

        internal void PreparePerPlayerLobbySettings(
            ManualLogSource log,
            string modName,
            string ownerGuid,
            bool logRoutineActivity = true)
        {
            if (perPlayerSettingsCoordinator != null)
                throw new InvalidOperationException($"Per-player lobby settings for [{modName}] were already prepared.");

            var builder = new PerPlayerLobbySettingsBuilder(this);
            ConfigurePerPlayerLobbySettings(builder);
            perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(
                this,
                log,
                modName,
                ownerGuid,
                builder.Build(),
                logRoutineActivity);
        }

        internal void ActivatePerPlayerLobbySettings()
        {
            if (perPlayerSettingsCoordinator == null)
                throw new InvalidOperationException("Per-player lobby settings must be prepared before activation.");
            perPlayerSettingsCoordinator.Activate();
        }

        internal void DeactivatePerPlayerLobbySettings()
        {
            perPlayerSettingsCoordinator?.Deactivate();
            perPlayerSettingsCoordinator = null;
        }

        // Typed mission-preset endpoint used by ExtendedData and other optional coordinators.
        public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
            presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreateModDefaultSnapshot() =>
            presetController?.CreateDefaultSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreateCurrentMissionPresetSnapshot() =>
            presetController?.CreateCurrentMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreatePlayerMissionPresetSnapshot() =>
            presetController?.CreatePlayerMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_ApplyMissionPresetSnapshot(Dictionary<string, byte[]> snapshot, string label)
        {
            presetController?.ApplyMissionWorkingSnapshot(snapshot, label);
            RaiseAccessProperties();
        }

        public Dictionary<string, byte[]> System_CreateCurrentWorkingSnapshot() =>
            presetController?.CreateCurrentMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
        {
            presetController?.ApplyWorkingSnapshot(snapshot);
            RaiseAccessProperties();
        }

        public void System_LoadModDefaults()
        {
            if (IsMissionPresetSelected)
                ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, ModSettingsWorkingSourceRegistry.ModDefaultsId);
            else
                presetController?.ApplyDefaultsAsWorkingCopy();
            RaiseAccessProperties();
        }

        /// <summary>Returns the persistent settings available to shared preset authoring UI.</summary>
        public IReadOnlyList<PresetSettingDescriptor> System_GetPresetSettingDescriptors() =>
            presetController?.GetSettingDescriptors() ?? Array.Empty<PresetSettingDescriptor>();

        /// <summary>Saves selected settings as a persistent, personal preset JSON file.</summary>
        public string System_SavePersonalPreset(
            string id,
            string name,
            string description,
            IEnumerable<PresetSaveSelection> selections,
            bool overwrite) =>
            presetController?.SavePersonalPreset(id, name, description, selections, overwrite) ?? string.Empty;

#if API_SHARED_PRESET_TESTS
        internal IReadOnlyList<PublishedModSettingsPreset> System_TestPublishedPresets =>
            presetController?.PublishedPresets ?? Array.Empty<PublishedModSettingsPreset>();

        internal void System_TestRefreshPresetCatalog() => presetController?.RefreshCatalog();

        internal void System_TestLoadPreset(string stableId)
        {
            PublishedModSettingsPreset preset = presetController?.PublishedPresets.FirstOrDefault(item =>
                string.Equals(item.StableId, stableId, StringComparison.Ordinal));
            if (preset == null)
                throw new InvalidDataException("The requested test preset is unavailable.");
            presetController.LoadPreset(preset);
        }

        internal void System_TestDeletePreset(string stableId)
        {
            PublishedModSettingsPreset preset = presetController?.PublishedPresets.FirstOrDefault(item =>
                string.Equals(item.StableId, stableId, StringComparison.Ordinal));
            if (preset == null)
                throw new InvalidDataException("The requested test preset is unavailable.");
            presetController.DeletePersonalPreset(preset);
        }

#endif

        public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
        {
            if (presetController == null)
                return;

            missionPresetContext = true;
            missionPresetEditable = editable;
            presetController.EnterMissionPreset(snapshot, label, editable);
            RaiseAccessProperties();
        }

        public void System_ExitMissionPreset()
        {
            if (!missionPresetContext || presetController == null)
                return;

            missionPresetContext = false;
            missionPresetEditable = false;
            presetController.ExitMissionPreset();
            RaiseAccessProperties();
        }

        public void System_RefreshSettingsAccess()
        {
            bool currentIsRealMultiplayer;
            bool currentIsHost;
            try
            {
                currentIsRealMultiplayer = GameModeHelper.IsRealMultiplayer();
                // Authority and game-mode presentation are independent. The Extender
                // correctly reports local Skirmish and Trail lobbies as local host.
                currentIsHost = GameNetworkAPI.IsLocalHost();
            }
            catch
            {
                // Registration can precede the network singleton. Preserve the last
                // confirmed role so a transient failure never unlocks a client.
                return;
            }

            if (isLocalHost == currentIsHost && isRealMultiplayer == currentIsRealMultiplayer)
                return;

            isLocalHost = currentIsHost;
            isRealMultiplayer = currentIsRealMultiplayer;
            RaiseAccessProperties();
        }

        // Let the Extender process the notification first; then update our own working state.
        protected new void OnPropertyChanged(string name)
        {
            try
            {
                base.OnPropertyChanged(name);

                if (presetController?.IsHostSettingsActivationProperty(name) == true)
                    base.OnPropertyChanged(nameof(HostSettingsEnabled));
                if (presetController?.IsClientSettingsActivationProperty(name) == true)
                    base.OnPropertyChanged(nameof(ClientSettingsEnabled));
            }
            finally
            {
                presetController?.AfterPropertyChanged(name);
                System_RefreshSettingsAccess();
            }
        }

        private void SetSelectedPresetCore(int value)
        {
            if (selectedPreset == value)
                return;

            selectedPreset = value;
#if API_SHARED_PRESET_TESTS
            OnPropertyChanged(nameof(SelectedPreset));
#endif
            RaiseAccessProperties();
        }

        private void RaiseAccessProperties()
        {
            base.OnPropertyChanged(nameof(IsLocalSettingsHost));
            base.OnPropertyChanged(nameof(IsRealMultiplayerContext));
            base.OnPropertyChanged(nameof(HasHostSettings));
            base.OnPropertyChanged(nameof(HasClientSettings));
            base.OnPropertyChanged(nameof(HasHostSettingsActivation));
            base.OnPropertyChanged(nameof(HasClientSettingsActivation));
            base.OnPropertyChanged(nameof(HostSettingsEnabled));
            base.OnPropertyChanged(nameof(ClientSettingsEnabled));
            base.OnPropertyChanged(nameof(MissionPresetEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetSelected));
            base.OnPropertyChanged(nameof(CanEditHostSettings));
            base.OnPropertyChanged(nameof(CanEditClientSettings));
            base.OnPropertyChanged(nameof(CanToggleHostSettings));
            base.OnPropertyChanged(nameof(CanToggleClientSettings));
            base.OnPropertyChanged(nameof(CanChangePreset));
            base.OnPropertyChanged(nameof(CanResetSettings));
            base.OnPropertyChanged(nameof(PresetVisibility));
            base.OnPropertyChanged(nameof(ClientSettingsActivationVisibility));
            base.OnPropertyChanged(nameof(HostReadOnlyNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeText));
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetActive));
#if !API_SHARED_PRESET_TESTS
            base.OnPropertyChanged(nameof(System_PresetStatusText));
            base.OnPropertyChanged(nameof(System_PresetStatusVisibility));
            base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
#endif
        }

        private static string GetVanillaText(
            ManualLogSource log,
            string key,
            string fallback)
        {
            try
            {
                if (CrusaderDE.Translate.Instance.GameTexts.TryGetValue(key, out string value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Could not read Vanilla preset text [{key}]: {exception.Message}");
            }

            DebugLogHelper.LogWarning(
                log,
                $"Vanilla preset text [{key}] is unavailable; using [{fallback}].");
            return fallback;
        }

        private sealed class PresetController
        {
            internal const string SchemaVersionKey = "__SerpPresetSchemaVersion";
            internal const string ActivePresetKey = "__SerpActivePreset";
            internal const string Preset1Key = "__SerpPreset1";
            internal const string Preset2Key = "__SerpPreset2";
            internal const string PublishedPresetKey = "__SerpPublishedPreset";
            internal const string CurrentSettingsKey = "__SerpCurrentSettings";
            internal const string BasedOnPresetKey = "__SerpBasedOnPreset";
            internal const string PresetDirtyKey = "__SerpPresetDirty";
            internal const string LegacyPresetImportCompletedKey = "__SerpLegacyPresetImportCompleted";

            private const int SchemaVersion = 3;

            private readonly PresetLobbyModSettingsViewModel owner;
            private readonly ManualLogSource log;
            private readonly string modName;
            private readonly bool routineLoggingEnabled;
            private readonly string filePath;
            private readonly string pluginDirectory;
            private readonly string personalPresetDirectory;
            private readonly string targetGuid;
            private readonly Version targetVersion;
            private readonly PropertyInfo[] persistedProperties;
            private readonly PropertyInfo[] hostProperties;
            private readonly PropertyInfo[] clientProperties;
            private readonly PropertyInfo hostSettingsActivationProperty;
            private readonly PropertyInfo clientSettingsActivationProperty;
            private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;
            private readonly List<PublishedModSettingsPreset> publishedPresets =
                new List<PublishedModSettingsPreset>();

            private Dictionary<string, byte[]> defaults;
            private Dictionary<string, byte[]> preset1;
            private Dictionary<string, byte[]> missionPreset;
            private bool active;
            private bool applying;
            private PublishedModSettingsPreset activePublishedPreset;
            private PublishedModSettingsPreset suspendedPublishedPreset;
            private string basedOnStableId = string.Empty;
            private string basedOnName = string.Empty;
            private string basedOnSource = string.Empty;
            private bool presetDirty;
            private bool legacyPresetImportCompleted;
            private bool legacyMigrationPending;
            private string suspendedBasedOnStableId = string.Empty;
            private string suspendedBasedOnName = string.Empty;
            private string suspendedBasedOnSource = string.Empty;
            private bool suspendedPresetDirty;
            private string missionPresetLabel = string.Empty;
#if API_SHARED_PRESET_TESTS
            internal int TestWriteCount { get; private set; }
#endif

            public PresetController(
                PresetLobbyModSettingsViewModel owner,
                ManualLogSource log,
                string pluginAssemblyLocation,
                string modName,
                string targetGuid,
                Version targetVersion,
                bool logRoutineActivity)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.log = log;
                this.modName = modName ?? throw new ArgumentNullException(nameof(modName));
                routineLoggingEnabled = logRoutineActivity;

                pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation)
                    ?? throw new ArgumentException(
                        $"Cannot determine the plugin directory for [{pluginAssemblyLocation}].",
                        nameof(pluginAssemblyLocation));
                this.targetGuid = ModSettingsPresetCatalog.ValidateTargetGuid(
                    targetGuid ?? throw new ArgumentNullException(nameof(targetGuid)));
                this.targetVersion = targetVersion;
                string safeFileName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
                filePath = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    safeFileName + LobbyModSettingsStorage.FILE_EXTENSION);
                personalPresetDirectory = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    "Presets",
                    "Override",
                    this.targetGuid);

                persistedProperties = owner.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(IsPersistedProperty)
                    .ToArray();
                persistedPropertiesByName = persistedProperties
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
                clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
                hostSettingsActivationProperty = FindSettingsActivationProperty(hostProperties, "EnableMod");
                clientSettingsActivationProperty = FindSettingsActivationProperty(clientProperties, "EnableClientFeatures", "EnableMod");
                if (persistedProperties.Length != 0)
                    RefreshCatalog();
            }

            public IReadOnlyList<PublishedModSettingsPreset> PublishedPresets => publishedPresets;
            public string TargetGuid => targetGuid;
            public bool HasPersistentSettings => persistedProperties.Length != 0;

            public string GetStatusText(
                string basedOnText,
                string modifiedText,
                string personalSourceText,
                string bundledSourceText,
                string externalSourceText)
            {
                if (owner.IsMissionPresetSelected)
                    return missionPresetLabel;
                if (string.IsNullOrWhiteSpace(basedOnName))
                    return string.Empty;
                string status = (basedOnText ?? "Based on") + ": " + basedOnName;
                string localizedSource = activePublishedPreset == null
                    ? basedOnSource
                    : DescribeSource(
                        activePublishedPreset,
                        personalSourceText,
                        bundledSourceText,
                        externalSourceText);
                if (!string.IsNullOrWhiteSpace(localizedSource))
                    status += " · " + localizedSource;
                if (presetDirty)
                    status += " (" + (modifiedText ?? "modified") + ")";
                return status;
            }

            public void RefreshCatalog()
            {
                if (persistedProperties.Length == 0)
                {
                    publishedPresets.Clear();
                    return;
                }

                Directory.CreateDirectory(personalPresetDirectory);
                var refreshed = new List<PublishedModSettingsPreset>();
                foreach (PublishedModSettingsPreset preset in ModSettingsPresetCatalog.Discover(
                    targetGuid,
                    targetVersion,
                    pluginDirectory,
                    personalPresetDirectory,
                    log))
                {
                    try
                    {
                        ValidatePublishedPreset(preset);
                        refreshed.Add(preset);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Published preset [{preset.SourcePath}] is incompatible with [{modName}]: {exception.Message}");
                    }
                }
                publishedPresets.Clear();
                publishedPresets.AddRange(refreshed);
                activePublishedPreset = publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
                if (activePublishedPreset != null)
                {
                    basedOnName = activePublishedPreset.Name;
                    basedOnSource = DescribeSource(activePublishedPreset);
                }
                bool clearedMissingSource = active && basedOnStableId.Length != 0 && activePublishedPreset == null;
                if (clearedMissingSource)
                {
                    LogRoutine($"[{modName}] Preset source [{SanitizeLogValue(basedOnStableId)}] is unavailable; retained the materialized working settings.");
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    presetDirty = false;
                }
                if (clearedMissingSource && active)
                    WriteCombinedPayload();
            }

            public string CreateUniquePersonalPresetId(string name)
            {
                string baseId = CreatePersonalPresetId(name);
                string candidate = baseId;
                int suffix = 2;
                var occupied = new HashSet<string>(
                    publishedPresets.Where(item => item.SourceKind == ModSettingsPresetSourceKind.Personal)
                        .Select(item => item.Id),
                    StringComparer.OrdinalIgnoreCase);
                while (occupied.Contains(candidate) || File.Exists(Path.Combine(personalPresetDirectory, "preset_" + candidate + ".json")))
                    candidate = baseId + "-" + suffix++;
                return candidate;
            }

            private static string CreatePersonalPresetId(string name)
            {
                string trimmed = (name ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                    throw new InvalidDataException("A preset name is required.");
                var result = new System.Text.StringBuilder(trimmed.Length);
                bool separator = false;
                foreach (char character in trimmed.ToLowerInvariant())
                {
                    if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    {
                        result.Append(character);
                        separator = false;
                    }
                    else if (!separator)
                    {
                        result.Append('-');
                        separator = true;
                    }
                }
                string id = result.ToString().Trim('-');
                if (id.Length == 0)
                    throw new InvalidDataException("The preset name does not contain a usable id.");
                return id.Length <= 128 ? id : id.Substring(0, 128).TrimEnd('-');
            }

            private bool MigrateStagedExports()
            {
                string staged = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    "PresetExports",
                    "Override",
                    targetGuid);
                if (!Directory.Exists(staged))
                    return true;
                Directory.CreateDirectory(personalPresetDirectory);
                bool succeeded = true;
                foreach (string source in Directory.GetFiles(staged, "preset_*.json", SearchOption.TopDirectoryOnly))
                {
                    string destination = Path.Combine(personalPresetDirectory, Path.GetFileName(source));
                    if (File.Exists(destination))
                        continue;
                    try
                    {
                        PublishPresetJson(destination, File.ReadAllText(source), overwrite: false);
                        LogRoutine($"[{modName}] Imported staged personal preset [{source}] to [{destination}].");
                    }
                    catch (Exception exception)
                    {
                        succeeded = false;
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not import staged personal preset [{source}]: {exception.Message}");
                    }
                }
                return succeeded;
            }

            public int MissionPresetIndex => 1;

            public bool HasHostSettings => hostProperties.Length != 0;

            public bool HasClientSettings => clientProperties.Length != 0;

            public bool HasHostSettingsActivation => hostSettingsActivationProperty != null;

            public bool HasClientSettingsActivation => clientSettingsActivationProperty != null;

            public bool HostSettingsEnabled => ReadSettingsActivation(hostSettingsActivationProperty);

            public bool ClientSettingsEnabled => ReadSettingsActivation(clientSettingsActivationProperty);

            public void SetHostSettingsEnabled(bool value) =>
                WriteSettingsActivation(hostSettingsActivationProperty, value);

            public void SetClientSettingsEnabled(bool value) =>
                WriteSettingsActivation(clientSettingsActivationProperty, value);

            public bool IsHostSettingsActivationProperty(string propertyName) =>
                IsSettingsActivationProperty(hostSettingsActivationProperty, propertyName);

            public bool IsClientSettingsActivationProperty(string propertyName) =>
                IsSettingsActivationProperty(clientSettingsActivationProperty, propertyName);

            public bool IsApplyingSnapshot => applying;

            public bool IsHostPropertyName(string propertyName) =>
                !string.IsNullOrEmpty(propertyName) &&
                persistedPropertiesByName.TryGetValue(propertyName, out PropertyInfo property) &&
                IsHostProperty(property);

#if API_SHARED_PRESET_TESTS
            public int NormalizeSelection(int selected, bool missionContext)
            {
                if (missionContext && selected == MissionPresetIndex)
                    return selected;
                return 0;
            }
#endif

            public void CaptureDefaults()
            {
                defaults = CaptureCurrentSettings();
            }

            public void SetDefaultValue<T>(string propertyName, T value)
            {
                if (string.IsNullOrWhiteSpace(propertyName) ||
                    !persistedPropertiesByName.TryGetValue(propertyName, out PropertyInfo property))
                {
                    throw new InvalidDataException($"Unknown persistent setting [{propertyName}].");
                }
                if (property.PropertyType != typeof(T))
                {
                    throw new InvalidDataException(
                        $"Default setting [{propertyName}] expects [{property.PropertyType.FullName}], not [{typeof(T).FullName}].");
                }
                if (value == null && property.PropertyType.IsValueType &&
                    Nullable.GetUnderlyingType(property.PropertyType) == null)
                {
                    throw new InvalidDataException($"Default setting [{propertyName}] cannot be null.");
                }

                byte[] serialized = MessagePackSerializer.Serialize(property.PropertyType, value);
                MessagePackSerializer.Deserialize(property.PropertyType, serialized);
                defaults[property.Name] = serialized;
            }

            public void Activate()
            {
                if (active)
                    return;

                if (persistedProperties.Length == 0)
                {
                    active = true;
                    LogRoutine($"[{modName}] Preset storage skipped because the ViewModel has no persistent settings.");
                    return;
                }

                Dictionary<string, byte[]> payload = null;
                bool fileExists = File.Exists(filePath);
                if (fileExists && !TryReadPayload(out payload))
                {
                    BackupCorruptFile();
                    payload = null;
                }

                bool needsRewrite = false;
                PublishedModSettingsPreset legacyPublishedToMaterialize = null;
                Dictionary<string, byte[]> legacyPreset1 = null;
                Dictionary<string, byte[]> legacyPreset2 = null;
                int legacySelected = 0;
                int loadedSchemaVersion = 0;
                string oldPublishedId = string.Empty;
                if (payload != null && payload.ContainsKey(SchemaVersionKey))
                {
                    try
                    {
                        int schemaVersion = MessagePackSerializer.Deserialize<int>(payload[SchemaVersionKey]);
                        loadedSchemaVersion = schemaVersion;
                        if (schemaVersion < 1 || schemaVersion > SchemaVersion)
                            throw new InvalidDataException($"Unsupported preset schema version [{schemaVersion}].");
                        if (schemaVersion == SchemaVersion)
                        {
                            if (payload.TryGetValue(LegacyPresetImportCompletedKey, out byte[] importCompletedBytes))
                                legacyPresetImportCompleted = MessagePackSerializer.Deserialize<bool>(importCompletedBytes);
                            preset1 = ReadSnapshot(payload, CurrentSettingsKey) ?? CaptureCurrentSettings();
                            if (payload.TryGetValue(BasedOnPresetKey, out byte[] basedOnBytes))
                                basedOnStableId = MessagePackSerializer.Deserialize<string>(basedOnBytes) ?? string.Empty;
                            if (payload.TryGetValue(PresetDirtyKey, out byte[] dirtyBytes))
                                presetDirty = MessagePackSerializer.Deserialize<bool>(dirtyBytes);
                        }
                        else
                        {
                            legacySelected = payload.TryGetValue(ActivePresetKey, out byte[] selectedBytes)
                                ? NormalizePreset(MessagePackSerializer.Deserialize<int>(selectedBytes))
                                : 0;
                            legacyPreset1 = ReadSnapshot(payload, Preset1Key) ?? CaptureCurrentSettings();
                            legacyPreset2 = ReadSnapshot(payload, Preset2Key);
                            if (schemaVersion >= 2 && payload.TryGetValue(PublishedPresetKey, out byte[] publishedBytes))
                                oldPublishedId = MessagePackSerializer.Deserialize<string>(publishedBytes) ?? string.Empty;
                        }
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Preset metadata is invalid: {exception}");
                        BackupCorruptFile();
                        payload = null;
                    }
                }

                if (payload != null && loadedSchemaVersion > 0 && loadedSchemaVersion < SchemaVersion)
                {
                    preset1 = Clone(legacySelected == 1 && legacyPreset2 != null ? legacyPreset2 : legacyPreset1);
                    legacyPublishedToMaterialize = FindLegacyPublishedPreset(oldPublishedId);
                    try
                    {
                        SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", legacyPreset1);
                        if (legacyPreset2 != null)
                            SaveLegacySnapshotAsPersonalPreset("legacy-preset-2", "Preset 2 (migrated)", legacyPreset2);
                        RefreshCatalog();
                        if (legacyPublishedToMaterialize == null)
                        {
                            PublishedModSettingsPreset migrated = publishedPresets.FirstOrDefault(item =>
                                item.SourceKind == ModSettingsPresetSourceKind.Personal &&
                                string.Equals(item.Id, legacySelected == 1 ? "legacy-preset-2" : "legacy-preset-1", StringComparison.OrdinalIgnoreCase));
                            SetBasedOn(migrated, modified: false);
                        }
                        needsRewrite = true;
                        LogRoutine($"[{modName}] Migrated legacy Preset 1/2 storage to personal JSON presets.");
                    }
                    catch (Exception exception)
                    {
                        legacyMigrationPending = true;
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not publish legacy presets; the valid MessagePack data was retained for retry: {exception}");
                    }
                }

                if (payload == null || !payload.ContainsKey(SchemaVersionKey))
                {
                    // The compatibility load before registration restored a legacy file here.
                    // Capturing the ViewModel preserves those values and supplies defaults
                    // for settings introduced after that file was written.
                    preset1 = CaptureCurrentSettings();
                    if (fileExists)
                    {
                        try
                        {
                            SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", preset1);
                            RefreshCatalog();
                            SetBasedOn(publishedPresets.FirstOrDefault(item =>
                                item.SourceKind == ModSettingsPresetSourceKind.Personal &&
                                string.Equals(item.Id, "legacy-preset-1", StringComparison.OrdinalIgnoreCase)), modified: false);
                            needsRewrite = true;
                        }
                        catch (Exception exception)
                        {
                            legacyMigrationPending = true;
                            DebugLogHelper.LogError(
                                log,
                                $"[{modName}] Could not publish the legacy lobby-settings preset; the original file was retained for retry: {exception}");
                        }
                    }
                    LogRoutine(
                        fileExists && !legacyMigrationPending
                            ? $"[{modName}] Migrated legacy lobby settings to a personal preset."
                            : fileExists
                                ? $"[{modName}] Deferred legacy lobby-settings migration after a publication failure."
                            : $"[{modName}] Initialized editable settings from code defaults.");
                }

                if (!legacyMigrationPending && !legacyPresetImportCompleted)
                {
                    legacyPresetImportCompleted = MigrateStagedExports();
                    RefreshCatalog();
                    needsRewrite = true;
                }

                string storedBasedOnStableId = basedOnStableId;
                RestoreBasedOnMetadata();
                if (storedBasedOnStableId.Length != 0 && basedOnStableId.Length == 0)
                    needsRewrite = true;
                LogRoutine($"[{modName}] Loaded editable lobby-settings working state; basedOn={SanitizeLogValue(basedOnStableId)}, modified={presetDirty}.");

                active = true;
                ApplySnapshot(preset1, 0, writeLocalStorage: false);
                if (legacyPublishedToMaterialize != null)
                    ApplyPublishedPreset(legacyPublishedToMaterialize, writeLocalStorage: false);

                // Persist the envelope immediately after reading a legacy top-level payload.
                // Otherwise an unchanged legacy file would be re-imported on every startup and
                // could never retain the stable identity of a subsequently selected public preset.
                if (needsRewrite && !legacyMigrationPending)
                    WriteCombinedPayload();
            }

#if API_SHARED_PRESET_TESTS
            public void SwitchTo(int selected)
            {
                selected = NormalizeSelection(selected, owner.missionPresetContext);
                if (!active || owner.selectedPreset == selected)
                    return;
                if (owner.missionPresetContext && !owner.missionPresetEditable)
                    return;

                if (owner.missionPresetContext && selected == MissionPresetIndex)
                {
                    activePublishedPreset = null;
                    ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                    LogRoutine($"[{modName}] Restored the active mission preset.");
                    return;
                }

                ApplySnapshot(preset1, 0, writeLocalStorage: true);
            }
#endif

            public void LoadPreset(PublishedModSettingsPreset preset)
            {
                if (preset == null || !publishedPresets.Contains(preset))
                    throw new InvalidDataException("The selected preset is unavailable.");
                if (owner.IsMissionPresetSelected && !owner.missionPresetEditable)
                    throw new InvalidOperationException("The active mission preset is read-only.");
                ApplyPublishedPreset(preset, writeLocalStorage: !owner.IsMissionPresetSelected);
            }

            public void ApplyDefaultsAsWorkingCopy()
            {
                if (owner.IsMissionPresetSelected)
                    throw new InvalidOperationException("Mission defaults must be materialized by the registered mission source provider.");
                var prepared = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (PropertyInfo property in persistedProperties)
                {
                    if (IsHostProperty(property) && !owner.isLocalHost) continue;
                    if (defaults.TryGetValue(property.Name, out byte[] value))
                        prepared[property.Name] = value == null ? null : (byte[])value.Clone();
                }
                ApplySnapshot(prepared, 0, writeLocalStorage: false);
                foreach (KeyValuePair<string, byte[]> entry in prepared) preset1[entry.Key] = entry.Value;
                SetBasedOn(null, false);
                WriteCombinedPayload();
                LogRoutine($"[{modName}] Loaded Mod defaults as editable working settings.");
            }

            public void ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
            {
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                if (owner.IsMissionPresetSelected)
                {
                    ApplyMissionWorkingSnapshot(snapshot, missionPresetLabel);
                    return;
                }
                Dictionary<string, byte[]> clone = Clone(snapshot);
                ApplySnapshot(clone, 0, writeLocalStorage: false);
                preset1 = clone;
                WriteCombinedPayload();
            }

            private void RestoreBasedOnMetadata()
            {
                activePublishedPreset = publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
                if (activePublishedPreset == null)
                {
                    if (basedOnStableId.Length != 0)
                        LogRoutine($"[{modName}] Preset source [{SanitizeLogValue(basedOnStableId)}] is unavailable; retained the materialized working settings.");
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    return;
                }
                basedOnName = activePublishedPreset.Name;
                basedOnSource = DescribeSource(activePublishedPreset);
            }

            private PublishedModSettingsPreset FindLegacyPublishedPreset(string legacyStableId)
            {
                if (string.IsNullOrWhiteSpace(legacyStableId))
                    return null;
                return publishedPresets.FirstOrDefault(item =>
                    string.Equals(item.ProviderGuid + "\n" + item.TargetGuid + "\n" + item.Id,
                        legacyStableId,
                        StringComparison.OrdinalIgnoreCase));
            }

            private void SetBasedOn(PublishedModSettingsPreset preset, bool modified)
            {
                activePublishedPreset = preset;
                basedOnStableId = preset?.StableId ?? string.Empty;
                basedOnName = preset?.Name ?? string.Empty;
                basedOnSource = preset == null ? string.Empty : DescribeSource(preset);
                presetDirty = preset != null && modified;
            }

            private static string DescribeSource(PublishedModSettingsPreset preset)
            {
                return DescribeSource(preset, "Personal presets", "Bundled with this mod", "External presets");
            }

            private static string DescribeSource(
                PublishedModSettingsPreset preset,
                string personalSourceText,
                string bundledSourceText,
                string externalSourceText)
            {
                switch (preset.SourceKind)
                {
                    case ModSettingsPresetSourceKind.Personal: return personalSourceText;
                    case ModSettingsPresetSourceKind.Bundled: return bundledSourceText;
                    case ModSettingsPresetSourceKind.External: return externalSourceText + ": " + preset.ProviderName;
                    default: return preset.ProviderName;
                }
            }

            private void SaveLegacySnapshotAsPersonalPreset(
                string id,
                string name,
                Dictionary<string, byte[]> snapshot)
            {
                Directory.CreateDirectory(personalPresetDirectory);
                string path = Path.Combine(personalPresetDirectory, "preset_" + id + ".json");
                var settings = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
                foreach (PropertyInfo property in persistedProperties)
                {
                    if (snapshot == null || !snapshot.TryGetValue(property.Name, out byte[] bytes) || bytes == null)
                        continue;
                    object value = MessagePackSerializer.Deserialize(property.PropertyType, bytes);
                    settings[property.Name] = new PublishedPresetSetting
                    {
                        Mode = PublishedPresetValueMode.Fixed,
                        Value = ModSettingsPresetJson.ToJsonValue(property.PropertyType, value),
                    };
                }
                string json = ModSettingsPresetJson.Serialize(
                    targetGuid,
                    id,
                    name,
                    "Migrated from the previous local Preset 1/2 storage.",
                    string.Empty,
                    string.Empty,
                    settings);
                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    if (string.Equals(existing, json, StringComparison.Ordinal))
                        return;
                    throw new InvalidDataException(
                        $"The migration target [{path}] already exists with different contents.");
                }
                PublishPresetJson(path, json, overwrite: false);
            }

            public IReadOnlyList<PresetSettingDescriptor> GetSettingDescriptors() =>
                persistedProperties.Select(property => new PresetSettingDescriptor
                {
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                    Scope = IsHostProperty(property)
                        ? PresetSettingScope.Host
                        : property.GetCustomAttribute<SyncPerPlayerAttribute>() != null
                            ? PresetSettingScope.Player
                            : PresetSettingScope.Local,
                }).OrderBy(item => item.Scope).ThenBy(item => item.PropertyName, StringComparer.Ordinal).ToArray();

            public string SavePersonalPreset(
                string id,
                string name,
                string description,
                IEnumerable<PresetSaveSelection> selections,
                bool overwrite)
            {
                PresetSaveSelection[] selectedSettings = (selections ?? Enumerable.Empty<PresetSaveSelection>()).ToArray();
                if (selectedSettings.Length == 0)
                    throw new InvalidDataException("At least one setting must be selected for saving.");
                IGrouping<string, PresetSaveSelection> duplicate = selectedSettings
                    .GroupBy(item => item?.PropertyName ?? string.Empty, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null)
                    throw new InvalidDataException($"Setting [{duplicate.Key}] was selected more than once.");

                var exported = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
                foreach (PresetSaveSelection selection in selectedSettings)
                {
                    if (selection == null || !persistedPropertiesByName.TryGetValue(selection.PropertyName, out PropertyInfo property))
                        throw new InvalidDataException($"Unknown persistent setting [{selection?.PropertyName}].");
                    object value = null;
                    if (selection.Mode == PublishedPresetValueMode.Fixed)
                    {
                        object currentValue = property.GetValue(owner);
                        if (IsHostProperty(property) && !owner.isLocalHost)
                        {
                            Dictionary<string, byte[]> ownedPreset = preset1 ?? defaults;
                            if (!ownedPreset.TryGetValue(property.Name, out byte[] ownedBytes) || ownedBytes == null)
                                throw new InvalidDataException($"Locally owned value for [{property.Name}] is unavailable.");
                            currentValue = MessagePackSerializer.Deserialize(property.PropertyType, ownedBytes);
                        }
                        value = ModSettingsPresetJson.ToJsonValue(property.PropertyType, currentValue);
                    }
                    exported.Add(property.Name, new PublishedPresetSetting { Mode = selection.Mode, Value = value });
                }

                string json = ModSettingsPresetJson.Serialize(targetGuid, id, name, description, string.Empty, string.Empty, exported);
                string safeId = string.Concat(id.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(safeId))
                    throw new InvalidDataException("Preset id has no safe filename characters.");
                string path = Path.Combine(
                    personalPresetDirectory,
                    "preset_" + safeId + ".json");
                PublishPresetJson(path, json, overwrite);
                RefreshCatalog();
                LogRoutine(
                    $"[{modName}] {(overwrite ? "Overwrote" : "Saved")} personal preset [{id}] at [{path}].");
                return path;
            }

            public void LogPresetOperationCancelled(string operation, string id)
            {
                LogRoutine(
                    $"[{modName}] Personal preset operation [{operation}] for [{id ?? string.Empty}] was cancelled.");
            }

            public void LogPresetOperationFailure(string operation, string id, Exception exception)
            {
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Personal preset operation [{operation}] for [{id ?? string.Empty}] failed: {exception}");
            }

            public void DeletePersonalPreset(PublishedModSettingsPreset preset)
            {
                if (preset == null ||
                    preset.SourceKind != ModSettingsPresetSourceKind.Personal ||
                    !publishedPresets.Contains(preset))
                {
                    throw new InvalidDataException("Only an available personal preset can be deleted.");
                }

                string path = Path.GetFullPath(preset.SourcePath ?? string.Empty);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                if (!File.Exists(path))
                    throw new FileNotFoundException("The selected personal preset no longer exists.", path);

                string stableId = preset.StableId;
                File.Delete(path);

                bool metadataChanged = false;
                if (string.Equals(basedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
                {
                    activePublishedPreset = null;
                    basedOnStableId = string.Empty;
                    basedOnName = string.Empty;
                    basedOnSource = string.Empty;
                    presetDirty = false;
                    metadataChanged = true;
                }
                if (string.Equals(suspendedBasedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
                {
                    suspendedPublishedPreset = null;
                    suspendedBasedOnStableId = string.Empty;
                    suspendedBasedOnName = string.Empty;
                    suspendedBasedOnSource = string.Empty;
                    suspendedPresetDirty = false;
                    metadataChanged = true;
                }

                RefreshCatalog();
                if (metadataChanged && active)
                    WriteCombinedPayload();
                LogRoutine($"[{modName}] Deleted personal preset [{SanitizeLogValue(stableId)}].");
            }

            private void PublishPresetJson(string path, string json, bool overwrite)
            {
                string directory = Path.GetDirectoryName(path);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                Directory.CreateDirectory(directory);
                ModSettingsPresetCatalog.ValidatePersonalWritePath(
                    pluginDirectory,
                    personalPresetDirectory,
                    path);
                string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
                    if (!overwrite)
                    {
                        try
                        {
                            File.Move(temporaryPath, path);
                        }
                        catch (IOException)
                        {
                            if (File.Exists(path))
                                throw new PresetSaveFileExistsException(path);
                            throw;
                        }
                    }
                    else
                    {
                        PresetAtomicPublishResult publish = PresetAtomicFilePublisher.Publish(temporaryPath, path);
                        if (!publish.Succeeded) throw publish.Error;
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }

            public Dictionary<string, byte[]> CreateDisabledSnapshot()
            {
                Dictionary<string, byte[]> snapshot = CopyProperties(defaults, hostProperties);
                if (persistedPropertiesByName.TryGetValue("EnableMod", out PropertyInfo enableProperty) &&
                    enableProperty.PropertyType == typeof(bool))
                {
                    snapshot[enableProperty.Name] = MessagePackSerializer.Serialize(false);
                }
                return snapshot;
            }

            public Dictionary<string, byte[]> CreateDefaultSnapshot() => Clone(defaults);

            public Dictionary<string, byte[]> CreateCurrentMissionSnapshot() =>
                Clone(owner.IsMissionPresetSelected ? missionPreset : preset1 ?? defaults);

            public Dictionary<string, byte[]> CreatePlayerMissionSnapshot() =>
                Clone(owner.IsMissionPresetSelected ? preset1 ?? defaults : CaptureCurrentSettings());

            public void ApplyMissionWorkingSnapshot(Dictionary<string, byte[]> snapshot, string label)
            {
                if (!owner.IsMissionPresetSelected || !owner.missionPresetEditable)
                    throw new InvalidOperationException("Mission settings are not currently editable.");
                if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
                Dictionary<string, byte[]> merged = Clone(missionPreset ?? defaults);
                foreach (KeyValuePair<string, byte[]> entry in snapshot)
                {
                    if (!persistedPropertiesByName.TryGetValue(entry.Key, out PropertyInfo property))
                        continue;
                    if (IsHostProperty(property) && !owner.isLocalHost)
                        continue;
                    merged[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                }
                missionPreset = merged;
                missionPresetLabel = label ?? string.Empty;
                ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                LogRoutine($"[{modName}] Loaded mission source [{SanitizeLogValue(missionPresetLabel)}] into the editable working copy.");
            }

            public void EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
            {
                suspendedPublishedPreset = activePublishedPreset;
                suspendedBasedOnStableId = basedOnStableId;
                suspendedBasedOnName = basedOnName;
                suspendedBasedOnSource = basedOnSource;
                suspendedPresetDirty = presetDirty;
                activePublishedPreset = null;
                missionPresetLabel = label ?? string.Empty;
                missionPreset = Clone(preset1 ?? defaults);
                Dictionary<string, byte[]> supplied = snapshot ?? CreateDisabledSnapshot();
                foreach (KeyValuePair<string, byte[]> entry in supplied)
                    missionPreset[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
                LogRoutine($"[{modName}] Entered {(editable ? "editable" : "read-only")} mission preset.");
            }

            public void ExitMissionPreset()
            {
                missionPreset = null;
                basedOnStableId = suspendedBasedOnStableId;
                basedOnName = suspendedBasedOnName;
                basedOnSource = suspendedBasedOnSource;
                presetDirty = suspendedPresetDirty;
                activePublishedPreset = suspendedPublishedPreset != null && publishedPresets.Contains(suspendedPublishedPreset)
                    ? suspendedPublishedPreset
                    : null;
                ApplySnapshot(preset1, 0, writeLocalStorage: true);
                suspendedPublishedPreset = null;
                missionPresetLabel = string.Empty;
                LogRoutine($"[{modName}] Left mission preset and restored the previous normal preset.");
            }

            public void AfterPropertyChanged(string propertyName)
            {
                if (!active || applying || string.IsNullOrEmpty(propertyName))
                    return;

                persistedPropertiesByName.TryGetValue(
                    propertyName,
                    out PropertyInfo property);

                // Keep verified host state in the transient Trail snapshot as well.
                // Otherwise switching to a local preset and back would restore the
                // client's stale local Trail value. Never write this branch to disk.
                if (IsNetworkSyncInProgress())
                {
                    if (property != null &&
                        owner.IsMissionPresetSelected &&
                        IsHostProperty(property))
                    {
                        StoreProperty(missionPreset, property);
                    }
                    return;
                }

                if (property == null)
                    return;

                if (owner.IsMissionPresetSelected)
                {
                    if (owner.missionPresetEditable &&
                        (owner.isLocalHost || IsClientProperty(property)))
                        StoreProperty(missionPreset, property);
                    // Mission-owned values remain in memory until the normal preset is restored.
                    return;
                }

                // Incoming host values are runtime-only on clients.
                if (IsHostProperty(property) && !owner.isLocalHost)
                {
                    // A local client edit cannot replace the locally owned host preset.
                    return;
                }

                if (owner.isLocalHost || IsClientProperty(property))
                {
                    StoreProperty(preset1, property);
                    if (basedOnStableId.Length != 0)
                        presetDirty = true;
                    WriteCombinedPayload();
                }
            }

            private void ApplyPublishedPreset(PublishedModSettingsPreset preset, bool writeLocalStorage)
            {
                if (preset == null)
                    throw new ArgumentNullException(nameof(preset));

                Dictionary<string, byte[]> playerPreset = owner.IsMissionPresetSelected
                    ? Clone(preset1 ?? defaults)
                    : CaptureCurrentSettings();
                var prepared = new Dictionary<PropertyInfo, byte[]>();
                foreach (KeyValuePair<string, PublishedPresetSetting> entry in preset.Settings)
                {
                    PropertyInfo property = persistedPropertiesByName[entry.Key];
                    if (IsHostProperty(property) && !owner.isLocalHost)
                        continue;

                    byte[] bytes;
                    switch (entry.Value.Mode)
                    {
                        case PublishedPresetValueMode.ModDefault:
                            if (!defaults.TryGetValue(property.Name, out bytes))
                                throw new InvalidDataException($"Code default for [{property.Name}] is unavailable.");
                            break;
                        case PublishedPresetValueMode.Player:
                            if (!playerPreset.TryGetValue(property.Name, out bytes) &&
                                !defaults.TryGetValue(property.Name, out bytes))
                            {
                                throw new InvalidDataException($"Player value for [{property.Name}] is unavailable.");
                            }
                            break;
                        case PublishedPresetValueMode.Fixed:
                            object converted = ModSettingsPresetJson.ConvertValue(entry.Value.Value, property.PropertyType);
                            bytes = MessagePackSerializer.Serialize(property.PropertyType, converted);
                            break;
                        default:
                            throw new InvalidDataException($"Unsupported published preset mode for [{property.Name}].");
                    }
                    prepared[property] = (byte[])bytes.Clone();
                }

                applying = true;
                try
                {
                    foreach (KeyValuePair<PropertyInfo, byte[]> entry in prepared)
                    {
                        if (!TryApplyProperty(entry.Key, entry.Value))
                            throw new InvalidDataException($"Published value for [{entry.Key.Name}] could not be applied.");
                    }
                    if (owner.IsMissionPresetSelected)
                    {
                        foreach (PropertyInfo property in prepared.Keys)
                            StoreProperty(missionPreset, property);
                        owner.SetSelectedPresetCore(MissionPresetIndex);
                    }
                    else
                    {
                        foreach (PropertyInfo property in prepared.Keys)
                            StoreProperty(preset1, property);
                        SetBasedOn(preset, modified: false);
                        owner.SetSelectedPresetCore(0);
                    }
                }
                finally
                {
                    applying = false;
                }
                owner.OnSettingsSnapshotApplied();
                if (writeLocalStorage) WriteCombinedPayload();
                LogRoutine($"[{modName}] Loaded editable preset [{preset.Name}] from [{preset.ProviderName}].");
            }

            private static string SanitizeLogValue(string value) =>
                (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");

            private void ValidatePublishedPreset(PublishedModSettingsPreset preset)
            {
                foreach (KeyValuePair<string, PublishedPresetSetting> entry in preset.Settings)
                {
                    if (!persistedPropertiesByName.TryGetValue(entry.Key, out PropertyInfo property))
                        throw new InvalidDataException($"Unknown persistent property [{entry.Key}].");
                    if (entry.Value == null)
                        throw new InvalidDataException($"Setting [{entry.Key}] is null.");
                    if (entry.Value.Mode == PublishedPresetValueMode.Fixed)
                    {
                        object converted = ModSettingsPresetJson.ConvertValue(entry.Value.Value, property.PropertyType);
                        MessagePackSerializer.Serialize(property.PropertyType, converted);
                    }
                }
            }

            private void ApplySnapshot(
                Dictionary<string, byte[]> stored,
                int selected,
                bool writeLocalStorage)
            {
                applying = true;
                try
                {
                    foreach (PropertyInfo property in persistedProperties)
                    {
                        bool include = selected == MissionPresetIndex || owner.isLocalHost || IsClientProperty(property);
                        if (!include)
                            continue;

                        byte[] bytes = null;
                        if (stored != null)
                            stored.TryGetValue(property.Name, out bytes);
                        if (bytes == null)
                            defaults.TryGetValue(property.Name, out bytes);
                        if (bytes == null || !property.CanWrite)
                            continue;

                        if (!TryApplyProperty(property, bytes) &&
                            defaults.TryGetValue(property.Name, out byte[] defaultBytes) &&
                            !ReferenceEquals(bytes, defaultBytes))
                        {
                            TryApplyProperty(property, defaultBytes);
                        }
                    }

                    owner.SetSelectedPresetCore(selected);
                }
                finally
                {
                    applying = false;
                }

                owner.OnSettingsSnapshotApplied();

                if (writeLocalStorage)
                    WriteCombinedPayload();
            }

            private bool TryApplyProperty(PropertyInfo property, byte[] bytes)
            {
                try
                {
                    object value = MessagePackSerializer.Deserialize(property.PropertyType, bytes);
                    if (value == null)
                        return false;

                    property.SetValue(owner, value);
                    return true;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Could not restore [{property.Name}] from the current settings snapshot: {exception.Message}");
                    return false;
                }
            }

            private Dictionary<string, byte[]> CaptureCurrentSettings()
            {
                Dictionary<string, byte[]> snapshot =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (PropertyInfo property in persistedProperties)
                    StoreProperty(snapshot, property);
                return snapshot;
            }

            private void StoreProperty(
                Dictionary<string, byte[]> snapshot,
                PropertyInfo property)
            {
                if (!property.CanRead)
                    return;

                try
                {
                    object value = property.GetValue(owner);
                    if (value == null)
                    {
                        snapshot.Remove(property.Name);
                        return;
                    }

                    snapshot[property.Name] =
                        MessagePackSerializer.Serialize(property.PropertyType, value);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Could not capture [{property.Name}] for the current settings snapshot: {exception.Message}");
                }
            }

            private void WriteCombinedPayload()
            {
                if (persistedProperties.Length == 0 || legacyMigrationPending)
                    return;

                Dictionary<string, byte[]> payload = ComposeSafeTopLevelSnapshot();
                payload[SchemaVersionKey] = MessagePackSerializer.Serialize(SchemaVersion);
                payload[CurrentSettingsKey] = MessagePackSerializer.Serialize(preset1 ?? Clone(defaults));
                if (!string.IsNullOrEmpty(basedOnStableId))
                    payload[BasedOnPresetKey] = MessagePackSerializer.Serialize(basedOnStableId);
                payload[PresetDirtyKey] = MessagePackSerializer.Serialize(presetDirty);
                payload[LegacyPresetImportCompletedKey] = MessagePackSerializer.Serialize(legacyPresetImportCompleted);

                string directory = Path.GetDirectoryName(filePath);
                string temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
                int publishAttempts = 0;
                try
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(temporaryPath, MessagePackSerializer.Serialize(payload));
                    PresetAtomicPublishResult result =
                        PresetAtomicFilePublisher.Publish(temporaryPath, filePath);
                    publishAttempts = result.Attempts;
                    if (!result.Succeeded)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"[{modName}] Could not atomically publish lobby-settings presets to [{filePath}] " +
                            $"after {result.Attempts} attempts; hresult=0x{result.Error.HResult:X8}: {result.Error}");
                    }
#if API_SHARED_PRESET_TESTS
                    else
                        TestWriteCount++;
#endif
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not save lobby-settings presets to [{filePath}]; " +
                        $"publishAttempts={publishAttempts}, hresult=0x{exception.HResult:X8}: {exception}");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogWarning(
                            log,
                            $"[{modName}] Could not remove temporary preset file [{temporaryPath}]: {exception.Message}");
                    }
                }
            }

            private Dictionary<string, byte[]> ComposeSafeTopLevelSnapshot()
            {
                Dictionary<string, byte[]> snapshot =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                Dictionary<string, byte[]> ownedPreset = preset1 ?? defaults;

                foreach (PropertyInfo property in persistedProperties)
                {
                    bool mayCaptureLive = !owner.IsMissionPresetSelected &&
                        (IsClientProperty(property) || owner.isLocalHost);
                    if (mayCaptureLive)
                    {
                        StoreProperty(snapshot, property);
                        continue;
                    }

                    // Preserve the user's own host preset instead of serializing a
                    // remote host value or an externally owned Trail value.
                    if (ownedPreset.TryGetValue(property.Name, out byte[] bytes))
                        snapshot[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                    else if (defaults.TryGetValue(property.Name, out bytes))
                        snapshot[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                }

                return snapshot;
            }

            private bool TryReadPayload(out Dictionary<string, byte[]> payload)
            {
                payload = null;
                if (!File.Exists(filePath))
                    return false;

                try
                {
                    payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                        File.ReadAllBytes(filePath));
                    return payload != null;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not read lobby-settings presets from [{filePath}]: {exception}");
                    return false;
                }
            }

            private void BackupCorruptFile()
            {
                if (!File.Exists(filePath))
                    return;

                string backupPath = filePath + ".corrupt-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
                try
                {
                    File.Copy(filePath, backupPath, false);
                    DebugLogHelper.LogWarning(
                        log,
                        $"[{modName}] Preserved invalid preset data at [{backupPath}].");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not preserve invalid preset data: {exception}");
                }
            }

            private Dictionary<string, byte[]> ReadSnapshot(
                Dictionary<string, byte[]> payload,
                string key)
            {
                if (!payload.TryGetValue(key, out byte[] bytes))
                    return null;

                return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes);
            }

            private static int NormalizePreset(int selected)
            {
                return selected == 1 ? 1 : 0;
            }

            private static bool IsPersistedProperty(PropertyInfo property)
            {
                return property.GetCustomAttribute<DoNotPersistAttribute>() == null &&
                    (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<SyncHostOnlyAttribute>() != null ||
                    property.GetCustomAttribute<PresetLocalAttribute>() != null);
            }

            private static bool IsHostProperty(PropertyInfo property) =>
                property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;

            private static bool IsClientProperty(PropertyInfo property) =>
                property.GetCustomAttribute<SyncHostOnlyAttribute>() == null &&
                (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<PresetLocalAttribute>() != null);

            private static PropertyInfo FindSettingsActivationProperty(
                IEnumerable<PropertyInfo> properties,
                params string[] preferredNames)
            {
                foreach (string name in preferredNames)
                {
                    PropertyInfo property = properties.FirstOrDefault(item =>
                        item.Name == name &&
                        item.PropertyType == typeof(bool) &&
                        item.CanRead &&
                        item.CanWrite);
                    if (property != null)
                        return property;
                }

                return null;
            }

            private bool ReadSettingsActivation(PropertyInfo property) =>
                property != null && (bool)property.GetValue(owner);

            private void WriteSettingsActivation(PropertyInfo property, bool value)
            {
                if (property == null || ReadSettingsActivation(property) == value)
                    return;

                property.SetValue(owner, value);
            }

            private static bool IsSettingsActivationProperty(
                PropertyInfo property,
                string propertyName) =>
                property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal);

            public static bool IsNetworkSyncInProgress()
            {
                return GameXAMLManagerAPI.Instance != null &&
                    GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin ==
                    LobbyModSettingsChangeOrigin.IncomingNetwork;
            }

            private static Dictionary<string, byte[]> CopyProperties(
                Dictionary<string, byte[]> source,
                IEnumerable<PropertyInfo> properties)
            {
                Dictionary<string, byte[]> result =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (source == null)
                    return result;

                foreach (PropertyInfo property in properties)
                {
                    if (source.TryGetValue(property.Name, out byte[] bytes))
                        result[property.Name] = bytes == null ? null : (byte[])bytes.Clone();
                }
                return result;
            }

            private void LogRoutine(string message)
            {
                if (routineLoggingEnabled)
                    DebugLogHelper.LogInfo(log, message);
            }

            private static Dictionary<string, byte[]> Clone(
                Dictionary<string, byte[]> source)
            {
                Dictionary<string, byte[]> clone =
                    new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (source == null)
                    return clone;

                foreach (KeyValuePair<string, byte[]> entry in source)
                    clone[entry.Key] = entry.Value == null ? null : (byte[])entry.Value.Clone();
                return clone;
            }
        }
    }

#if !API_SHARED_PRESET_TESTS
    internal static class ModSettingsHorizontalFocusScrollGuard
    {
        private static readonly Dictionary<Noesis.ScrollViewer, DiagnosticState> AttachedScrollViewers =
            new Dictionary<Noesis.ScrollViewer, DiagnosticState>();

        public static bool Attach(
            object view,
            ManualLogSource log,
            string modName)
        {
            Noesis.ScrollViewer scrollViewer = FindFirstScrollViewer(
                view as Noesis.FrameworkElement);
            if (scrollViewer == null || AttachedScrollViewers.ContainsKey(scrollViewer))
                return false;

            var state = new DiagnosticState(
                scrollViewer,
                log,
                string.Equals(modName, "CastlePlanner_Serp", StringComparison.Ordinal));
            AttachedScrollViewers.Add(scrollViewer, state);
            state.Attach();
            return true;
        }

        private static Noesis.ScrollViewer FindFirstScrollViewer(
            Noesis.DependencyObject parent)
        {
            if (parent == null)
                return null;
            if (parent is Noesis.ScrollViewer scrollViewer)
                return scrollViewer;

            int childCount = Noesis.VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                Noesis.ScrollViewer child = FindFirstScrollViewer(
                    Noesis.VisualTreeHelper.GetChild(parent, index));
                if (child != null)
                    return child;
            }

            return null;
        }

        private sealed class DiagnosticState
        {
            private readonly Noesis.ScrollViewer scrollViewer;
            private readonly ManualLogSource log;
            private readonly bool diagnosticsEnabled;
            private float acceptedHorizontalOffset;
            private bool manualHorizontalScrollAuthorized;
            private bool restoringHorizontalOffset;

            public DiagnosticState(
                Noesis.ScrollViewer scrollViewer,
                ManualLogSource log,
                bool diagnosticsEnabled)
            {
                this.scrollViewer = scrollViewer;
                this.log = log;
                this.diagnosticsEnabled = diagnosticsEnabled;
                acceptedHorizontalOffset = scrollViewer.HorizontalOffset;
            }

            public void Attach()
            {
                scrollViewer.PreviewMouseDown += OnPreviewMouseDown;
                scrollViewer.PreviewKeyDown += OnPreviewKeyDown;
                scrollViewer.ScrollChanged += OnScrollChanged;
                Log(
                    $"attached; horizontal={scrollViewer.HorizontalOffset:0.###}, " +
                    $"vertical={scrollViewer.VerticalOffset:0.###}, " +
                    $"extentWidth={scrollViewer.ExtentWidth:0.###}, " +
                    $"viewportWidth={scrollViewer.ViewportWidth:0.###}.");
            }

            private void OnPreviewMouseDown(
                object sender,
                Noesis.MouseButtonEventArgs args)
            {
                manualHorizontalScrollAuthorized =
                    IsHorizontalScrollBarInput(args.Source);
                Log(
                    $"PreviewMouseDown; source={Describe(args.Source)}, " +
                    $"horizontalScrollbar={manualHorizontalScrollAuthorized}, " +
                    $"acceptedHorizontal={acceptedHorizontalOffset:0.###}, " +
                    $"currentHorizontal={scrollViewer.HorizontalOffset:0.###}.");
            }

            private void OnPreviewKeyDown(
                object sender,
                Noesis.KeyEventArgs args)
            {
                manualHorizontalScrollAuthorized =
                    IsHorizontalScrollBarInput(args.Source);
                Log(
                    $"PreviewKeyDown; source={Describe(args.Source)}, " +
                    $"horizontalScrollbar={manualHorizontalScrollAuthorized}.");
            }

            private bool IsHorizontalScrollBarInput(object source)
            {
                var current = source as Noesis.DependencyObject;
                while (current != null && !ReferenceEquals(current, scrollViewer))
                {
                    if (current is Noesis.ScrollBar scrollBar)
                        return scrollBar.Orientation == Noesis.Orientation.Horizontal;

                    current = Noesis.VisualTreeHelper.GetParent(current);
                }
                return false;
            }

            private void OnScrollChanged(
                object sender,
                Noesis.ScrollChangedEventArgs args)
            {
                if (Math.Abs(args.HorizontalChange) < 0.001f &&
                    Math.Abs(args.VerticalChange) < 0.001f)
                {
                    return;
                }

                Log(
                    $"ScrollChanged; horizontal={args.HorizontalOffset:0.###}, " +
                    $"horizontalChange={args.HorizontalChange:0.###}, " +
                    $"vertical={args.VerticalOffset:0.###}, " +
                    $"verticalChange={args.VerticalChange:0.###}.");

                if (Math.Abs(args.HorizontalChange) < 0.001f)
                    return;

                if (manualHorizontalScrollAuthorized)
                {
                    acceptedHorizontalOffset = args.HorizontalOffset;
                    Log(
                        $"accepted explicit horizontal scrollbar input; horizontal=" +
                        $"{acceptedHorizontalOffset:0.###}.");
                    return;
                }

                if (restoringHorizontalOffset ||
                    Math.Abs(args.HorizontalOffset - acceptedHorizontalOffset) < 0.001f)
                {
                    return;
                }

                // Horizontal movement is permitted only after explicit input inside the
                // horizontal ScrollBar template. Focus, layout and programmatic reveal
                // operations therefore cannot move the settings page sideways.
                restoringHorizontalOffset = true;
                try
                {
                    scrollViewer.ScrollToHorizontalOffset(acceptedHorizontalOffset);
                }
                finally
                {
                    restoringHorizontalOffset = false;
                }
                Log(
                    $"rejected non-scrollbar horizontal scroll; horizontal=" +
                    $"{scrollViewer.HorizontalOffset:0.###}, preserved=" +
                    $"{acceptedHorizontalOffset:0.###}.");
            }

            private void Log(string message)
            {
                if (diagnosticsEnabled)
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"[CastlePlanner ModSettingsScrollDiagnostic] {message}");
                }
            }

            private static string Describe(object value) =>
                value == null ? "null" : value.GetType().FullName;
        }
    }
#else
    internal static class ModSettingsHorizontalFocusScrollGuard
    {
        private static readonly HashSet<object> AttachedViews = new HashSet<object>();

        internal static int AttachedViewCount => AttachedViews.Count;

        public static bool Attach(
            object view,
            ManualLogSource log,
            string modName) =>
            view != null && AttachedViews.Add(view);

        internal static void ResetForTests() => AttachedViews.Clear();
    }
#endif

    public static class LobbyModSettingsPresetRegistration
    {
        public static void Register(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string modName,
            PresetLobbyModSettingsViewModel viewModel,
            string xamlSourceFile)
        {
            Register(
                plugin,
                log,
                modName,
                viewModel,
                xamlSourceFile,
                true);
        }

        public static void Register(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string modName,
            PresetLobbyModSettingsViewModel viewModel,
            string xamlSourceFile,
            bool logRoutineActivity)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

#if !API_SHARED_PRESET_TESTS
            if (GameAssetManagerAPI.Instance.GetModifiedFilePath(
                xamlSourceFile,
                out string absoluteXamlSourceFile))
            {
                // The catalog is read from XAML and is therefore available before Noesis has
                // materialized an unselected tab's controls. This avoids touching native layout.
                ModSettingsSearch.RegisterSource(viewModel, absoluteXamlSourceFile, log, modName);
            }
#endif
            viewModel.PreparePresets(
                log,
                plugin.Info.Location,
                modName,
                plugin.Info.Metadata.GUID,
                plugin.Info.Metadata.Version,
                logRoutineActivity);
            // Structural validation must happen before the ViewModel can enter the
            // Extender registry. An invalid personal setting therefore fails closed.
            viewModel.PreparePerPlayerLobbySettings(
                log,
                modName,
                plugin.Info.Metadata.GUID,
                logRoutineActivity);
            object registeredView = null;
            try
            {
                // Preserve the old pre-binding load (including legacy files), while keeping
                // subsequent writes under the preset controller's ownership.
                try
                {
                    new LobbyModSettingsStorage(plugin.Info.Location, modName).Load(viewModel);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log,
                        $"[{modName}] Initial lobby-settings load failed; keeping ViewModel defaults: {exception}");
                }
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    plugin,
                    modName,
                    viewModel,
                    xamlSourceFile,
                    useBuiltInPersistence: false);
                var registration = GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .FirstOrDefault(entry => ReferenceEquals(entry.ViewModel, viewModel));
#if API_SHARED_PRESET_TESTS
                // The classic test harness deliberately does not load Noesis.NoesisGUI.
                // Reflection keeps the registration semantics under test without
                // introducing a runtime-only FrameworkElement assembly dependency.
                registeredView = registration?.GetType()
                    .GetProperty("View", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(registration);
#else
                registeredView = registration?.View;
#endif
            }
            catch
            {
                viewModel.DeactivatePerPlayerLobbySettings();
                throw;
            }
            if (registeredView == null)
            {
                viewModel.DeactivatePerPlayerLobbySettings();
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Presets were not activated because lobby-settings registration failed.");
                return;
            }

            ModSettingsHorizontalFocusScrollGuard.Attach(
                registeredView,
                log,
                modName);
            viewModel.ActivatePresets();
            viewModel.ActivatePerPlayerLobbySettings();
#if !API_SHARED_PRESET_TESTS
            // Views are created before a lobby exists. Refresh the cached role whenever
            // the persistent settings hub opens or changes its selected tab.
            Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>
                viewModel.System_RefreshSettingsAccess();
#endif
        }
    }
}
