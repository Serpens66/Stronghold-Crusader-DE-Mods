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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if !SHARED_PRESET_TESTS
using R3;
using SHCDESE.EventAPI;
using SHCDESE.NoesisUtil;
using Steamworks;
using UnityEngine;
#endif
using ComboBoxItem = Noesis.ComboBoxItem;
using Visibility = Noesis.Visibility;

namespace Shared
{
    internal sealed class PerPlayerLobbySettingsCoordinator
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
#if !SHARED_PRESET_TESTS
        private static readonly FieldInfo LobbyIdField = typeof(Platform_Multiplayer.MPLobby)
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo LobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember)
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SteamIdValueField = LobbyMemberIdField?.FieldType
            .GetField("m_SteamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#endif
        private readonly PresetLobbyModSettingsViewModel owner;
        private readonly ManualLogSource log;
        private readonly string modName;
        private readonly PerPlayerLobbySettingsContract contract;
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
#if !SHARED_PRESET_TESTS
        private int lastObservedFrame = -1;
        private float nextErrorLogTime;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private bool mapStarted;
        private string lastIdentityDiagnostic = string.Empty;
#endif

        internal PerPlayerLobbySettingsCoordinator(
            PresetLobbyModSettingsViewModel owner,
            ManualLogSource log,
            string modName,
            PerPlayerLobbySettingsContract contract)
        {
            this.owner = owner;
            this.log = log;
            this.modName = modName;
            this.contract = contract;
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
#if !SHARED_PRESET_TESTS
                Application.onBeforeRender += OnBeforeRender;
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
                {
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        FinalizeRosterForMapTransition(args.bMultiplayerSave != 0);
                        mapStarted = true;
                    }
                });
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(args =>
                {
                    if (args.Phase == EventHookPhase.Post)
                        mapStarted = false;
                });
                if (mapStartSubscription == null || mapUnloadSubscription == null)
                    throw new InvalidOperationException("The persistent map lifecycle subscriptions could not be created.");
#endif
                active = true;
                RequestPublish();
            }
            catch
            {
                Deactivate();
                throw;
            }
            DebugLogHelper.LogInfo(
                log,
                $"[{modName}] Shared per-player lobby convergence activated: " +
                $"settings=[{string.Join(",", contract.Settings.Select(item => item.Property.Name))}], " +
                $"required=[{string.Join(",", contract.Settings.Where(item => item.IsReportRequired).Select(item => item.Property.Name))}].");
        }

        internal void Deactivate()
        {
            owner.PropertyChanged -= OnOwnerPropertyChanged;
#if !SHARED_PRESET_TESTS
            Application.onBeforeRender -= OnBeforeRender;
            mapStartSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            mapStartSubscription = null;
            mapUnloadSubscription = null;
            mapStarted = false;
#endif
            active = false;
            publishPending = false;
        }

        internal void RequestPublish()
        {
            publishPending = true;
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
                DebugLogHelper.LogInfo(
                    log,
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
                DebugLogHelper.LogInfo(
                    log,
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
            DebugLogHelper.LogInfo(
                log,
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

#if !SHARED_PRESET_TESTS
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

        private void OnBeforeRender()
        {
            int frame = Time.frameCount;
            if (lastObservedFrame >= 0 && frame - lastObservedFrame < 15)
                return;
            lastObservedFrame = frame;

            try
            {
                if (mapStarted)
                {
                    // Lobby settings are immutable during a match. OnUnloadMap is
                    // the authoritative point at which observation may resume.
                    return;
                }
                ObserveCurrentGameLobby();
            }
            catch (Exception exception)
            {
                SetReadiness(
                    false,
                    "The lobby roster could not be observed; waiting for a successful retry.");
                if (Time.unscaledTime < nextErrorLogTime)
                    return;
                nextErrorLogTime = Time.unscaledTime + 5f;
                DebugLogHelper.LogError(
                    log,
                    $"[{modName}] Shared per-player lobby observer recovered from an error: {exception}");
            }
        }

        private void ObserveCurrentGameLobby()
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby == null)
            {
                bool mapTransition = platform?.gameMembers != null &&
                    platform.gameMembers.Any(member =>
                        member != null && !member.skirmishAI && !member.kicked);
                // There is no stable local player slot outside a lobby. Querying the
                // Extender here only emits warnings and the value is discarded anyway.
                Observe(null, null, false, 0, mapTransition);
                return;
            }

            bool resolvedRoster = PlayerIdentityHelper.TryCaptureHumanRoster(
                preferInGameRoster: false,
                requireAuthoritativeLobbyRoster: true,
                out Dictionary<int, ulong> players,
                out _,
                out _);
            bool unresolved = !resolvedRoster;

            PlayerIdentityResolution localIdentity = PlayerIdentityHelper.CaptureLocalPlayerId(
                preferInGameRoster: false);
            ReportIdentityDiagnostic(localIdentity);
            if (!localIdentity.IsResolved)
                unresolved = true;

            ulong currentLobbyId = ReadSteamId(LobbyIdField?.GetValue(lobby));
            if (currentLobbyId == 0)
                unresolved = true;
            Observe(
                currentLobbyId,
                players,
                unresolved,
                localIdentity.IsResolved ? localIdentity.PlayerId : 0,
                false);
        }

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

        private static ulong ReadSteamId(object steamId)
        {
            object value = steamId == null ? null : SteamIdValueField?.GetValue(steamId);
            return value == null ? 0UL : Convert.ToUInt64(value);
        }

#endif

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
    public abstract class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel
    {
        private static readonly FieldInfo NetworkSyncInProgressField =
            typeof(GameXAMLManagerAPI).GetField(
                "_isProcessingNetworkSync",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo NotifyRevertMethod =
            typeof(LobbyModSettingsBaseViewModel).GetMethod(
                "NotifyRevert",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private ComboBoxItem[] presetOptions = Array.Empty<ComboBoxItem>();
        private PresetController presetController;
        private int selectedPreset;
        private bool missionPresetContext;
        private bool missionPresetEditable;
        private bool isRealMultiplayer;
        private bool isLocalHost = true;
        private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;
#if !SHARED_PRESET_TESTS
        private string modSettingsSearchText = string.Empty;
        private string modSettingsSearchExactKey = string.Empty;
        private bool modSettingsSearchIncludeToolTips;
        private bool modSettingsSearchExpanded;
#endif

        protected PresetLobbyModSettingsViewModel()
        {
#if !SHARED_PRESET_TESTS
            System_ToggleModSettingsSearchCommand = new RelayCommand(ToggleModSettingsSearch);
            System_ClearModSettingsSearchCommand = new RelayCommand(ClearModSettingsSearch);
#endif
        }

        public ComboBoxItem[] PresetOptions => presetOptions;

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

        public bool IsMissionPresetSelected => missionPresetContext && selectedPreset == 2;

        public bool CanEditHostSettings =>
            isLocalHost && (!IsMissionPresetSelected || missionPresetEditable);

        public bool CanEditClientSettings => true;

        public bool CanToggleHostSettings =>
            HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;

        public bool CanToggleClientSettings =>
            HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;

        public bool CanChangePreset => isLocalHost || HasClientSettings;

        public bool CanResetSettings => CanEditHostSettings || HasClientSettings;

        public Visibility PresetVisibility =>
            missionPresetContext || isLocalHost || HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility HostReadOnlyNoticeVisibility =>
            HasHostSettings && isRealMultiplayer && !isLocalHost
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string HostOptionsText =>
            ResolveSettingsUiText("Common.HostOptions", "HOST OPTIONS");

        public string ClientOptionsText =>
            ResolveSettingsUiText("Common.ClientOptions", "LOCAL CLIENT OPTIONS");

        public string PresetText =>
            ResolveSettingsUiText("Common.Preset", "Preset");

        public string ModEnabledText =>
            ResolveSettingsUiText("Common.EnableMod", "Enable Mod");

        public string HostActivationLabelText =>
            ResolveSettingsUiText("Common.HostActivationLabel", "(Host-)");

        public string ClientActivationLabelText =>
            ResolveSettingsUiText("Common.ClientActivationLabel", "(Client settings)");

        public Visibility ActionsScopeNoticeVisibility =>
            isRealMultiplayer && HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string ActionsScopeNoticeText =>
            HasHostSettings && isLocalHost
                ? ResolveSettingsUiText(
                    "Common.ActionsScopeHost",
                    "Preset and reset affect host settings and your local client settings.")
                : ResolveSettingsUiText(
                    "Common.ActionsScopeClient",
                    "Preset and reset affect only your local client settings.");

        public string HostReadOnlyNoticeText =>
            ResolveSettingsUiText("Common.HostReadOnly", "Values from host - read-only");

        public string ResetToDefaultHelpText =>
            ResolveSettingsUiText("Common.ResetToDefaultHelp", "Resets the settings you can control in the current context.");

        public string EnableModHelpText =>
            ResolveSettingsUiText("Common.EnableModHelp", "Enables or disables this mod for the match.");

        public string HostSettingsActivationHelpText =>
            ResolveSettingsUiText("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");

        public string ClientSettingsActivationHelpText =>
            ResolveSettingsUiText("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");

        public string PresetHelpText =>
            ResolveSettingsUiText("Common.PresetHelp", "Selects a saved preset. Clients change only their personal settings.");

#if !SHARED_PRESET_TESTS
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
            ResolveSettingsUiText("Common.ModSettingsSearchLabel", "Search");

        public string System_ModSettingsSearchHelpText =>
            ResolveSettingsUiText("Common.ModSettingsSearchHelp", "Search setting titles. Optionally include tooltips.");

        public string System_ModSettingsSearchToggleHelpText =>
            ResolveSettingsUiText("Common.ModSettingsSearchToggleHelp", "Show or hide the settings search.");

        public string System_ModSettingsSearchIncludeToolTipsText =>
            ResolveSettingsUiText("Common.ModSettingsSearchIncludeToolTips", "Search tooltips");

        public string System_ModSettingsSearchIncludeToolTipsHelpText =>
            ResolveSettingsUiText("Common.ModSettingsSearchIncludeToolTipsHelp", "Also search the explanatory tooltips of settings.");

        public string System_ModSettingsSearchClearHelpText =>
            ResolveSettingsUiText("Common.ModSettingsSearchClearHelp", "Clear the settings filter.");

        public string System_ModSettingsSearchNoResultsText =>
            ResolveSettingsUiText("Common.ModSettingsSearchNoResults", "No matching settings found.");

        public RelayCommand System_ToggleModSettingsSearchCommand { get; }

        public RelayCommand System_ClearModSettingsSearchCommand { get; }

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

        protected bool IsApplyingSettingsSnapshot =>
            presetController?.IsApplyingSnapshot == true;

        protected virtual void OnSettingsSnapshotApplied()
        {
        }

        /// <summary>
        /// Declares the few domain-specific parts of personal settings. Transport,
        /// player-slot ownership, lobby convergence and readiness stay in Shared.
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

#if !SHARED_PRESET_TESTS
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

#if SHARED_PRESET_TESTS
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
            if (IsMissionPresetSelected &&
                !missionPresetEditable &&
                presetController?.IsHostPropertyName(propertyName) == true)
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

        // Zero-based because Noesis binds this value directly to ComboBox.SelectedIndex.
        public int SelectedPreset
        {
            get => selectedPreset;
            set
            {
                int normalized = missionPresetContext && value == 2
                    ? 2
                    : (value == 1 ? 1 : 0);
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

        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName)
        {
            if (presetController != null)
                throw new InvalidOperationException($"Preset storage for [{modName}] was already prepared.");

            presetOptions = new[]
            {
                new ComboBoxItem { Content = GetVanillaText(log, "TEXT_NEW_TEXT2_210", "Preset 1") },
                new ComboBoxItem { Content = GetVanillaText(log, "TEXT_NEW_TEXT2_211", "Preset 2") },
                new ComboBoxItem { Content = string.Empty, Visibility = Visibility.Collapsed },
            };

            presetController = new PresetController(
                this,
                log,
                pluginAssemblyLocation,
                modName);
            presetController.CaptureDefaults();
            PropertyChanged += (_, __) => System_RefreshSettingsAccess();
            System_RefreshSettingsAccess();
        }

        internal void ActivatePresets()
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before it is activated.");

            presetController.Activate();
        }

        internal void ActivatePerPlayerLobbySettings(ManualLogSource log, string modName)
        {
            if (perPlayerSettingsCoordinator != null)
                throw new InvalidOperationException($"Per-player lobby settings for [{modName}] were already activated.");

            var builder = new PerPlayerLobbySettingsBuilder(this);
            ConfigurePerPlayerLobbySettings(builder);
            perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(
                this,
                log,
                modName,
                builder.Build());
            perPlayerSettingsCoordinator.Activate();
        }

        internal void DeactivatePerPlayerLobbySettings()
        {
            perPlayerSettingsCoordinator?.Deactivate();
            perPlayerSettingsCoordinator = null;
        }

        // Neutral reflection boundary used by optional mission coordinators.
        public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
            presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
        {
            if (presetController == null)
                return;

            missionPresetContext = true;
            missionPresetEditable = editable;
            // The items exist when Noesis first materializes the binding. Only the third
            // container's visibility changes, avoiding unsupported ItemsSource refreshes.
            presetOptions[2].Content = label ?? string.Empty;
            presetOptions[2].Visibility = Visibility.Visible;
            presetController.EnterMissionPreset(snapshot, editable);
            RaiseAccessProperties();
        }

        public void System_ExitMissionPreset()
        {
            if (!missionPresetContext || presetController == null)
                return;

            missionPresetContext = false;
            missionPresetEditable = false;
            presetController.ExitMissionPreset();
            presetOptions[2].Visibility = Visibility.Collapsed;
            presetOptions[2].Content = string.Empty;
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

        // The Script Extender's event handler runs synchronously inside the base call.
        // Reattach our reserved keys only after its normal persistence has completed.
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
            OnPropertyChanged(nameof(SelectedPreset));
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
            base.OnPropertyChanged(nameof(HostReadOnlyNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeText));
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetActive));
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

            private const int SchemaVersion = 1;

            private readonly PresetLobbyModSettingsViewModel owner;
            private readonly ManualLogSource log;
            private readonly string modName;
            private readonly string filePath;
            private readonly PropertyInfo[] persistedProperties;
            private readonly PropertyInfo[] hostProperties;
            private readonly PropertyInfo[] clientProperties;
            private readonly PropertyInfo hostSettingsActivationProperty;
            private readonly PropertyInfo clientSettingsActivationProperty;
            private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;

            private Dictionary<string, byte[]> defaults;
            private Dictionary<string, byte[]> preset1;
            private Dictionary<string, byte[]> preset2;
            private Dictionary<string, byte[]> missionPreset;
            private bool active;
            private bool applying;
            private int localSelectedPreset;

            public PresetController(
                PresetLobbyModSettingsViewModel owner,
                ManualLogSource log,
                string pluginAssemblyLocation,
                string modName)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.log = log;
                this.modName = modName ?? throw new ArgumentNullException(nameof(modName));

                string pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation)
                    ?? throw new ArgumentException(
                        $"Cannot determine the plugin directory for [{pluginAssemblyLocation}].",
                        nameof(pluginAssemblyLocation));
                string safeFileName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
                filePath = Path.Combine(
                    pluginDirectory,
                    LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                    safeFileName + LobbyModSettingsStorage.FILE_EXTENSION);

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
            }

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

            public void CaptureDefaults()
            {
                defaults = CaptureCurrentSettings();
            }

            public void Activate()
            {
                if (active)
                    return;

                Dictionary<string, byte[]> payload = null;
                bool fileExists = File.Exists(filePath);
                if (fileExists && !TryReadPayload(out payload))
                {
                    BackupCorruptFile();
                    payload = null;
                }

                int selected = 0;
                if (payload != null && payload.ContainsKey(SchemaVersionKey))
                {
                    try
                    {
                        int schemaVersion = MessagePackSerializer.Deserialize<int>(payload[SchemaVersionKey]);
                        if (schemaVersion != SchemaVersion)
                            throw new InvalidDataException($"Unsupported preset schema version [{schemaVersion}].");

                        selected = NormalizePreset(
                            MessagePackSerializer.Deserialize<int>(payload[ActivePresetKey]));
                        preset1 = ReadSnapshot(payload, Preset1Key) ?? Clone(defaults);
                        preset2 = ReadSnapshot(payload, Preset2Key);
                        DebugLogHelper.LogInfo(
                            log,
                            $"[{modName}] Loaded lobby-settings presets; active preset={selected + 1}, preset2Saved={preset2 != null}.");
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

                if (payload == null || !payload.ContainsKey(SchemaVersionKey))
                {
                    // RegisterLobbyModSettings has already restored a legacy file here.
                    // Capturing the ViewModel preserves those values and supplies defaults
                    // for settings introduced after that file was written.
                    preset1 = CaptureCurrentSettings();
                    preset2 = null;
                    selected = 0;
                    DebugLogHelper.LogInfo(
                        log,
                        fileExists
                            ? $"[{modName}] Migrated legacy lobby settings to preset 1."
                            : $"[{modName}] Initialized preset 1 from code defaults.");
                }

                active = true;
                localSelectedPreset = selected;
                ApplyPreset(selected);
            }

            public void SwitchTo(int selected)
            {
                selected = owner.missionPresetContext && selected == 2
                    ? 2
                    : NormalizePreset(selected);
                if (!active || owner.selectedPreset == selected)
                    return;

                if (owner.missionPresetContext && selected == 2)
                {
                    ApplySnapshot(missionPreset, 2, writeLocalStorage: false);
                    DebugLogHelper.LogInfo(log, $"[{modName}] Restored the active mission preset.");
                    return;
                }

                localSelectedPreset = selected;
                ApplyPreset(selected);
                DebugLogHelper.LogInfo(
                    log,
                    $"[{modName}] Switched to preset {selected + 1}; saved={GetPreset(selected) != null}.");
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

            public void EnterMissionPreset(Dictionary<string, byte[]> snapshot, bool editable)
            {
                missionPreset = snapshot == null ? CreateDisabledSnapshot() : Clone(snapshot);
                ApplySnapshot(missionPreset, 2, writeLocalStorage: false);
                // Property setters invoked by the Trail can make the Extender write its
                // normal storage file. Replace that transient file with locally owned data.
                WriteCombinedPayload();
                DebugLogHelper.LogInfo(log, $"[{modName}] Entered {(editable ? "editable" : "read-only")} mission preset.");
            }

            public void ExitMissionPreset()
            {
                missionPreset = null;
                ApplyPreset(localSelectedPreset);
                DebugLogHelper.LogInfo(log, $"[{modName}] Left mission preset and restored preset {localSelectedPreset + 1}.");
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
                    if (IsHostProperty(property))
                    {
                        if (owner.missionPresetEditable && owner.isLocalHost)
                            StoreProperty(missionPreset, property);
                        // Never leave an externally owned Trail value in local msgpack.
                        WriteCombinedPayload();
                        return;
                    }

                    // Trail owns only host settings. Personal settings remain editable
                    // and persistent without copying Trail values into local msgpack.
                    if (IsClientProperty(property))
                    {
                        StoreProperty(EnsurePreset(localSelectedPreset), property);
                        WriteCombinedPayload();
                    }
                    return;
                }

                // Incoming host values are runtime-only on clients.
                if (IsHostProperty(property) && !owner.isLocalHost)
                {
                    // Network-originated changes returned above. This is therefore a
                    // local/programmatic client edit; restore locally owned host data
                    // after the Extender's generic storage pass.
                    WriteCombinedPayload();
                    return;
                }

                if (owner.isLocalHost || IsClientProperty(property))
                {
                    StoreProperty(EnsurePreset(localSelectedPreset), property);
                    WriteCombinedPayload();
                }
            }

            private Dictionary<string, byte[]> EnsurePreset(int selected)
            {
                Dictionary<string, byte[]> preset = GetPreset(selected);
                if (preset == null)
                {
                    preset = Clone(defaults);
                    SetPreset(selected, preset);
                }
                return preset;
            }

            private void ApplyPreset(int selected)
            {
                Dictionary<string, byte[]> stored = GetPreset(selected);
                ApplySnapshot(stored, selected, writeLocalStorage: true);
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
                        bool include = selected == 2
                            ? IsHostProperty(property)
                            : owner.isLocalHost || IsClientProperty(property);
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
                        $"[{modName}] Could not restore [{property.Name}] from preset {owner.selectedPreset + 1}: {exception.Message}");
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
                        $"[{modName}] Could not capture [{property.Name}] for preset {owner.selectedPreset + 1}: {exception.Message}");
                }
            }

            private void WriteCombinedPayload()
            {
                Dictionary<string, byte[]> payload = ComposeSafeTopLevelSnapshot();
                payload[SchemaVersionKey] = MessagePackSerializer.Serialize(SchemaVersion);
                payload[ActivePresetKey] = MessagePackSerializer.Serialize(localSelectedPreset);
                payload[Preset1Key] = MessagePackSerializer.Serialize(preset1 ?? Clone(defaults));
                if (preset2 != null)
                    payload[Preset2Key] = MessagePackSerializer.Serialize(preset2);

                string directory = Path.GetDirectoryName(filePath);
                string temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(temporaryPath, MessagePackSerializer.Serialize(payload));
                    if (File.Exists(filePath))
                        File.Replace(temporaryPath, filePath, null);
                    else
                        File.Move(temporaryPath, filePath);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{modName}] Could not save lobby-settings presets to [{filePath}]: {exception}");
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
                Dictionary<string, byte[]> ownedPreset = GetPreset(localSelectedPreset) ?? defaults;

                foreach (PropertyInfo property in persistedProperties)
                {
                    bool mayCaptureLive = IsClientProperty(property) ||
                        (owner.isLocalHost && !owner.IsMissionPresetSelected);
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

            private Dictionary<string, byte[]> GetPreset(int selected)
            {
                return selected == 1 ? preset2 : preset1;
            }

            private void SetPreset(int selected, Dictionary<string, byte[]> snapshot)
            {
                if (selected == 1)
                    preset2 = snapshot;
                else
                    preset1 = snapshot;
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
                try
                {
                    if (NetworkSyncInProgressField != null)
                    {
                        return (bool)NetworkSyncInProgressField.GetValue(
                            GameXAMLManagerAPI.Instance);
                    }

                    // Retain safe behavior if a later Extender only renames the field.
                    return new StackTrace().GetFrames()?.Any(frame =>
                        frame.GetMethod()?.DeclaringType == typeof(GameXAMLManagerAPI) &&
                        (frame.GetMethod().Name == "ReceiveSettingsUpdate" ||
                            frame.GetMethod().Name == "ApplyHostOnlyUpdate" ||
                            frame.GetMethod().Name == "ApplyPerPlayerUpdate")) == true;
                }
                catch
                {
                    return false;
                }
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

#if !SHARED_PRESET_TESTS
    /// <summary>
    /// TEMPORARY SCRIPT EXTENDER WORKAROUND.
    /// Remove this class and its registration call once the upstream extender has
    /// fixed all multiplayer settings paths documented below and the fixes have
    /// been verified in a real Host/Client run. Revalidate the private method
    /// signatures, packet routing, and detour behavior after every Extender update;
    /// this workaround may need adaptation even before it can be removed.
    /// </summary>
    internal static class ScriptExtenderMultiplayerSyncWorkaround
    {
        private const string AnchorKey =
            "SerpsMods.Shared.ScriptExtenderMultiplayerSyncWorkaround.v1";
        private const string PerPlayerAnchorKey =
            "SerpsMods.Shared.ScriptExtenderPerPlayerIdentityWorkaround.v1";
        private const string GateKey =
            "SerpsMods.Shared.ScriptExtenderMultiplayerSyncWorkaround.Gate";

        internal static void EnsureInstalled(ManualLogSource log)
        {
            object gate = string.Intern(GateKey);
            lock (gate)
            {
                bool baseInstalled = AppDomain.CurrentDomain.GetData(AnchorKey) != null;
                bool perPlayerInstalled =
                    AppDomain.CurrentDomain.GetData(PerPlayerAnchorKey) != null;
                if (baseInstalled && perPlayerInstalled)
                    return;

                HookAnchor anchor = null;
                PerPlayerIdentityHookAnchor perPlayerAnchor = null;
                try
                {
                    if (!baseInstalled)
                    {
                        anchor = new HookAnchor(log);
                        anchor.Install();
                        AppDomain.CurrentDomain.SetData(AnchorKey, anchor);
                    }
                    if (!perPlayerInstalled)
                    {
                        perPlayerAnchor = new PerPlayerIdentityHookAnchor(log);
                        perPlayerAnchor.Install();
                        AppDomain.CurrentDomain.SetData(
                            PerPlayerAnchorKey,
                            perPlayerAnchor);
                    }
                    DebugLogHelper.LogInfo(
                        log,
                        "Temporary Script Extender multiplayer settings workaround installed " +
                        "(join snapshot, reliable lobby delivery, in-game sender propagation, " +
                        "authoritative per-player identity application). " +
                        "Remove centrally after the upstream fixes are available.");
                }
                catch (Exception ex)
                {
                    perPlayerAnchor?.RollBack();
                    anchor?.RollBack();
                    if (perPlayerAnchor != null)
                        AppDomain.CurrentDomain.SetData(PerPlayerAnchorKey, null);
                    if (anchor != null)
                        AppDomain.CurrentDomain.SetData(AnchorKey, null);
                    Exception cause = Unwrap(ex);
                    DebugLogHelper.LogError(
                        log,
                        "Temporary Script Extender multiplayer settings workaround could not be " +
                        $"installed as one transaction: {cause}");
                    throw new InvalidOperationException(
                        "Lobby mod settings registration aborted because the required " +
                        "multiplayer synchronization workaround is unavailable.",
                        cause);
                }
            }
        }

        private static Exception Unwrap(Exception ex)
        {
            return ex is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : ex;
        }

        private sealed class HookAnchor
        {
            private delegate void SendCustomInfoToMemberDelegate(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPLobbyMember member);

            private delegate void SendPacketToAllLobbyDelegate(
                Platform_Multiplayer.MPData packet);

            private delegate bool ProcessMessageDelegate(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPData data,
                Platform_Multiplayer.MPGameMember fromMember,
                bool fromThread);

            private readonly ManualLogSource log;
            private object sendCustomInfoDetour;
            private object sendPacketToAllLobbyDetour;
            private object processMessageDetour;
            private MethodInfo handleRawPacketMethod;
            private Type steamNetworkingIdentityType;
            private MethodInfo setSteamIdMethod;
            private MethodInfo sendMessageToUserMethod;
            private FieldInfo lobbyMemberIdField;
            private FieldInfo multiplayerInstanceField;
            private Type steamIdType;
            private bool loggedReliableReroute;
            private bool loggedSenderRepair;

            internal HookAnchor(ManualLogSource log)
            {
                this.log = log;
            }

            internal void Install()
            {
                MethodInfo sendCustomInfoMethod = RequireMethod(
                    typeof(Platform_Multiplayer),
                    nameof(Platform_Multiplayer.SendCustomInfoToMember),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    typeof(Platform_Multiplayer.MPLobbyMember));
                MethodInfo sendPacketToAllLobbyMethod = typeof(GameNetworkAPI)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(method =>
                        method.Name == nameof(GameNetworkAPI.SendPacketToAllLobby) &&
                        !method.IsGenericMethod &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == typeof(Platform_Multiplayer.MPData));
                MethodInfo processMessageMethod = RequireMethod(
                    typeof(Platform_Multiplayer),
                    "processMessage",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    typeof(Platform_Multiplayer.MPData),
                    typeof(Platform_Multiplayer.MPGameMember),
                    typeof(bool));

                handleRawPacketMethod = typeof(GameNetworkAPI).GetMethod(
                    "HandleRawPacket",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(short), typeof(byte[]), FindNullableSteamIdType() },
                    null) ?? throw new MissingMethodException(
                        typeof(GameNetworkAPI).FullName,
                        "HandleRawPacket(short, byte[], CSteamID?)");
                steamIdType = Nullable.GetUnderlyingType(
                    handleRawPacketMethod.GetParameters()[2].ParameterType) ??
                    throw new InvalidOperationException("HandleRawPacket sender is not nullable.");
                Assembly steamworksAssembly = steamIdType.Assembly;
                steamNetworkingIdentityType = steamworksAssembly.GetType(
                    "Steamworks.SteamNetworkingIdentity",
                    true);
                Type steamNetworkingMessagesType = steamworksAssembly.GetType(
                    "Steamworks.SteamNetworkingMessages",
                    true);
                setSteamIdMethod = steamNetworkingIdentityType.GetMethod(
                    "SetSteamID",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { steamIdType },
                    null) ?? throw new MissingMethodException(
                        steamNetworkingIdentityType.FullName,
                        "SetSteamID");
                sendMessageToUserMethod = steamNetworkingMessagesType
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(method =>
                        method.Name == "SendMessageToUser" &&
                        method.GetParameters().Length == 5 &&
                        method.GetParameters()[0].ParameterType.IsByRef &&
                        method.GetParameters()[1].ParameterType == typeof(IntPtr));
                lobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember).GetField(
                    "id",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    throw new MissingFieldException(
                        typeof(Platform_Multiplayer.MPLobbyMember).FullName,
                        "id");
                multiplayerInstanceField = typeof(Platform_Multiplayer).GetField(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ??
                    throw new MissingFieldException(
                        typeof(Platform_Multiplayer).FullName,
                        "instance");

                DebugLogHelper.LogInfo(
                    log,
                    "[MP-SYNC-EVIDENCE BASELINE] " +
                    $"extenderJoinDetourInstalled={ReadExtenderJoinDetourState()}. " +
                    "False confirms that the extender declared but did not install its join-sync detour.");

                // TEMPORARY: upstream declares this hook but does not install it.
                sendCustomInfoDetour = CreateManagedDetour(
                    typeof(SendCustomInfoToMemberDelegate),
                    sendCustomInfoMethod,
                    new SendCustomInfoToMemberDelegate(SendCustomInfoToMemberHook));

                // TEMPORARY: upstream lobby broadcast uses Steam send flag 64, which
                // is not reliable. Route through its targeted reliable (flag 40) path.
                sendPacketToAllLobbyDetour = CreateManagedDetour(
                    typeof(SendPacketToAllLobbyDelegate),
                    sendPacketToAllLobbyMethod,
                    new SendPacketToAllLobbyDelegate(SendPacketToAllLobbyHook));

                // TEMPORARY: upstream's processMessage IL hook omits fromMember, so
                // host-only packets received after map start fail sender validation.
                processMessageDetour = CreateManagedDetour(
                    typeof(ProcessMessageDelegate),
                    processMessageMethod,
                    new ProcessMessageDelegate(ProcessMessageHook));
            }

            internal void RollBack()
            {
                DisposeDetour(processMessageDetour);
                DisposeDetour(sendPacketToAllLobbyDetour);
                DisposeDetour(sendCustomInfoDetour);
                processMessageDetour = null;
                sendPacketToAllLobbyDetour = null;
                sendCustomInfoDetour = null;
            }

            private void SendCustomInfoToMemberHook(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPLobbyMember member)
            {
                try
                {
                    GameNetworkAPI.Instance.HandleSendCustomInfoToMember(member);
                    object target = lobbyMemberIdField.GetValue(member);
                    int registeredSettings = GameXAMLManagerAPI.Instance.RegisteredModSettings.Count();
                    DebugLogHelper.LogInfo(
                        log,
                        "[MP-SYNC-EVIDENCE JOIN] " +
                        $"forwarded member=[{member?.name}], steamId={target}, " +
                        $"registeredSettings={registeredSettings}. " +
                        "The following extender sync/apply messages must confirm client receipt.");
                }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Temporary join-settings snapshot workaround failed: {Unwrap(ex)}");
                }

                GetTrampoline<SendCustomInfoToMemberDelegate>(sendCustomInfoDetour)(instance, member);
            }

            private void SendPacketToAllLobbyHook(Platform_Multiplayer.MPData packet)
            {
                Platform_Multiplayer multiplayer =
                    multiplayerInstanceField.GetValue(null) as Platform_Multiplayer;
                if (multiplayer?.activeLobby?.members == null)
                {
                    GetTrampoline<SendPacketToAllLobbyDelegate>(sendPacketToAllLobbyDetour)(packet);
                    return;
                }

                Platform_Multiplayer.MPLobbyMember[] members =
                    multiplayer.activeLobby.members.ToArray();
                int eligibleRecipients = 0;
                int successfulRecipients = 0;
                foreach (Platform_Multiplayer.MPLobbyMember member in members)
                {
                    if (member == null || member.IsSelf() || member.dummyToBeKicked ||
                        (member.SkirmishMember && !member.SkirmishHumanMember))
                        continue;

                    eligibleRecipients++;
                    try
                    {
                        object target = lobbyMemberIdField.GetValue(member);
                        SendReliableLobbyPacket(target, packet);
                        successfulRecipients++;
                    }
                    catch (Exception ex)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"Reliable lobby settings delivery failed for [{member.name}]: {Unwrap(ex)}");
                    }
                }

                if (!loggedReliableReroute)
                {
                    loggedReliableReroute = true;
                    DebugLogHelper.LogInfo(
                        log,
                        "[MP-SYNC-EVIDENCE RELIABLE-SEND] " +
                        $"packetType={packet?.packetType}, payloadBytes={packet?.data?.Length ?? 0}, " +
                        $"eligibleRecipients={eligibleRecipients}, successfulRecipients={successfulRecipients}. " +
                        "The matching client apply message must confirm delivery.");
                }
            }

            private void SendReliableLobbyPacket(
                object targetSteamId,
                Platform_Multiplayer.MPData packet)
            {
                if (targetSteamId == null)
                    throw new InvalidOperationException("The lobby recipient has no Steam ID.");
                if (packet == null)
                    throw new ArgumentNullException(nameof(packet));

                byte[] bytes = packet.ToBytes();
                object identity = Activator.CreateInstance(steamNetworkingIdentityType);
                setSteamIdMethod.Invoke(identity, new[] { targetSteamId });
                GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    object result = sendMessageToUserMethod.Invoke(
                        null,
                        new object[]
                        {
                            identity,
                            pinned.AddrOfPinnedObject(),
                            (uint)bytes.Length,
                            40,
                            2,
                        });
                    if (Convert.ToInt32(result) != 1)
                    {
                        throw new InvalidOperationException(
                            $"SteamNetworkingMessages.SendMessageToUser returned [{result}].");
                    }
                }
                finally
                {
                    pinned.Free();
                }
            }

            private bool ProcessMessageHook(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPData data,
                Platform_Multiplayer.MPGameMember fromMember,
                bool fromThread)
            {
                if (fromThread && data != null &&
                    data.packetType >= (short)CustomNetworkPacketType.CustomPacketStart)
                {
                    // Returning false makes Vanilla enqueue the packet. Its later
                    // main-thread pass preserves fromMember and safely updates UI/settings.
                    return false;
                }

                if (data != null &&
                    fromMember != null &&
                    data.packetType >= (short)CustomNetworkPacketType.CustomPacketStart)
                {
                    try
                    {
                        object sender = Activator.CreateInstance(steamIdType, fromMember.steamID);
                        bool handled = (bool)handleRawPacketMethod.Invoke(
                            GameNetworkAPI.Instance,
                            new object[] { data.packetType, data.data, sender });
                        if (handled)
                        {
                            if (!loggedSenderRepair)
                            {
                                loggedSenderRepair = true;
                                DebugLogHelper.LogInfo(
                                    log,
                                    "[MP-SYNC-EVIDENCE INGAME-SENDER] " +
                                    $"packetType={data.packetType}, payloadBytes={data.data?.Length ?? 0}, " +
                                    $"senderSteamId={fromMember.steamID}, handled={handled}. " +
                                    "The matching host-only apply message must confirm acceptance.");
                            }

                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"In-game settings sender workaround failed; falling back to the extender path: {Unwrap(ex)}");
                    }
                }

                return GetTrampoline<ProcessMessageDelegate>(processMessageDetour)(
                    instance,
                    data,
                    fromMember,
                    fromThread);
            }

            private static MethodInfo RequireMethod(
                Type type,
                string name,
                BindingFlags flags,
                params Type[] parameterTypes)
            {
                return type.GetMethod(name, flags, null, parameterTypes, null) ??
                    throw new MissingMethodException(type.FullName, name);
            }

            private static Type FindNullableSteamIdType()
            {
                MethodInfo candidate = typeof(GameNetworkAPI)
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(method =>
                        method.Name == "HandleRawPacket" &&
                        method.GetParameters().Length == 3);
                return candidate.GetParameters()[2].ParameterType;
            }

            private static string ReadExtenderJoinDetourState()
            {
                try
                {
                    Type managerType = typeof(GameNetworkAPI).Assembly.GetType(
                        "SHCDESE.ManagedHooks.ManagedHookManager",
                        true);
                    object manager = managerType.GetProperty(
                        "Instance",
                        BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    FieldInfo field = managerType.GetField(
                        "platform_Multiplayer_SendCustomInfoToMember_hook",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (manager == null || field == null)
                        return "unknown";

                    return (field.GetValue(manager) != null).ToString();
                }
                catch
                {
                    return "unknown";
                }
            }

            private static object CreateManagedDetour(
                Type delegateType,
                MethodInfo source,
                Delegate target)
            {
                Type openType = typeof(GameNetworkAPI).Assembly.GetType(
                    "SHCDESE.ManagedHooks.ManagedDetour`1",
                    true);
                Type closedType = openType.MakeGenericType(delegateType);
                return Activator.CreateInstance(closedType, source, target);
            }

            private static T GetTrampoline<T>(object detour) where T : class
            {
                object trampoline = detour.GetType()
                    .GetProperty("Trampoline", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(detour);
                return trampoline as T ??
                    throw new InvalidOperationException("Managed detour trampoline is unavailable.");
            }

            private static void DisposeDetour(object detour)
            {
                if (detour == null)
                    return;

                object hook = detour.GetType()
                    .GetProperty("Hook", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(detour);
                (hook as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// The extender authenticates per-player settings with the transport Steam ID,
        /// but maps that identity through lobby list order. Vanilla can already expose
        /// the final game slot while that provisional order still differs. Keep the
        /// authenticated update until the authoritative roster is available and then
        /// write it into the final companion-array slot. Remove this hook after an
        /// upstream fix is verified in a real Host/Client run. Revalidate its private
        /// target signature and semantics after every Script Extender update.
        /// </summary>
        private sealed class PerPlayerIdentityHookAnchor
        {
            private delegate void ApplyPerPlayerUpdateDelegate(
                object viewModel,
                PropertyInfo property,
                LobbyModSettingSyncPacket packet,
                object value,
                CSteamID? sender);

            private readonly ManualLogSource log;
            private readonly List<PendingPerPlayerUpdate> pending =
                new List<PendingPerPlayerUpdate>();
            private object applyPerPlayerUpdateDetour;
            private int lastFlushFrame = -1;
            private string lastFlushError = string.Empty;

            internal PerPlayerIdentityHookAnchor(ManualLogSource log)
            {
                this.log = log;
            }

            internal void Install()
            {
                MethodInfo source = typeof(GameXAMLManagerAPI).GetMethod(
                    "ApplyPerPlayerUpdate",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(object),
                        typeof(PropertyInfo),
                        typeof(LobbyModSettingSyncPacket),
                        typeof(object),
                        typeof(CSteamID?),
                    },
                    null) ?? throw new MissingMethodException(
                        typeof(GameXAMLManagerAPI).FullName,
                        "ApplyPerPlayerUpdate");
                applyPerPlayerUpdateDetour = CreateManagedDetour(
                    typeof(ApplyPerPlayerUpdateDelegate),
                    source,
                    new ApplyPerPlayerUpdateDelegate(ApplyPerPlayerUpdateHook));
                Application.onBeforeRender += FlushPendingUpdates;
            }

            internal void RollBack()
            {
                Application.onBeforeRender -= FlushPendingUpdates;
                DisposeDetour(applyPerPlayerUpdateDetour);
                applyPerPlayerUpdateDetour = null;
                pending.Clear();
            }

            private void ApplyPerPlayerUpdateHook(
                object viewModel,
                PropertyInfo property,
                LobbyModSettingSyncPacket packet,
                object value,
                CSteamID? sender)
            {
                if (viewModel == null || property == null || packet == null || value == null)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Per-player settings update was rejected because its decoded payload is incomplete.");
                    return;
                }
                if (!sender.HasValue || sender.Value.m_SteamID == 0)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Per-player setting [{packet.ModName}.{packet.PropertyName}] was rejected because the transport sender is unavailable.");
                    return;
                }

                ulong senderSteamId = sender.Value.m_SteamID;
                if (!IsCurrentHuman(senderSteamId))
                {
                    // Steam delivery can win the race against Vanilla's lobby-member
                    // projection. Retain the authenticated value, but never apply it
                    // until the same Steam identity appears in an authoritative roster.
                    QueuePendingUpdate(
                        viewModel,
                        property,
                        packet,
                        value,
                        senderSteamId,
                        "The authenticated transport sender is not visible in the human roster yet.");
                    return;
                }

                if (!TryResolveAuthoritativePlayer(
                        senderSteamId,
                        packet.SourcePlayerId,
                        out int playerId,
                        out string resolutionError,
                        out string diagnostic))
                {
                    QueuePendingUpdate(
                        viewModel,
                        property,
                        packet,
                        value,
                        senderSteamId,
                        resolutionError);
                    return;
                }

                ApplyResolvedUpdate(
                    viewModel,
                    property,
                    packet,
                    value,
                    senderSteamId,
                    playerId,
                    diagnostic,
                    deferred: false);
            }

            private void FlushPendingUpdates()
            {
                if (pending.Count == 0 || Time.frameCount == lastFlushFrame)
                    return;
                lastFlushFrame = Time.frameCount;

                try
                {
                    FlushPendingUpdatesCore();
                    lastFlushError = string.Empty;
                }
                catch (Exception ex)
                {
                    string error = ex.GetBaseException().Message;
                    if (!string.Equals(lastFlushError, error, StringComparison.Ordinal))
                    {
                        lastFlushError = error;
                        DebugLogHelper.LogError(
                            log,
                            $"Deferred per-player settings remain blocked because their retry failed: {error}");
                    }
                }
            }

            private void FlushPendingUpdatesCore()
            {

                ulong currentLobbyId = CurrentLobbyId();
                for (int index = pending.Count - 1; index >= 0; index--)
                {
                    PendingPerPlayerUpdate update = pending[index];
                    if (update.LobbyId != 0 && currentLobbyId != 0 &&
                        update.LobbyId != currentLobbyId)
                    {
                        pending.RemoveAt(index);
                        DebugLogHelper.LogError(
                            log,
                            $"Deferred per-player setting [{update.Packet.ModName}.{update.Packet.PropertyName}] from {update.SenderSteamId} was discarded because the lobby changed.");
                        continue;
                    }
                    if (!IsCurrentHuman(update.SenderSteamId))
                        continue;
                    if (!TryResolveAuthoritativePlayer(
                            update.SenderSteamId,
                            update.Packet.SourcePlayerId,
                            out int playerId,
                            out _,
                            out string diagnostic))
                        continue;

                    ApplyResolvedUpdate(
                        update.ViewModel,
                        update.Property,
                        update.Packet,
                        update.Value,
                        update.SenderSteamId,
                        playerId,
                        diagnostic,
                        deferred: true);
                    pending.RemoveAt(index);
                }
            }

            private void QueuePendingUpdate(
                object viewModel,
                PropertyInfo property,
                LobbyModSettingSyncPacket packet,
                object value,
                ulong senderSteamId,
                string reason)
            {
                int existing = pending.FindIndex(item =>
                    ReferenceEquals(item.ViewModel, viewModel) &&
                    string.Equals(item.Property.Name, property.Name, StringComparison.Ordinal) &&
                    item.SenderSteamId == senderSteamId);
                var update = new PendingPerPlayerUpdate(
                    viewModel,
                    property,
                    packet,
                    ClonePendingValue(value),
                    senderSteamId,
                    CurrentLobbyId());
                if (existing >= 0)
                    pending[existing] = update;
                else
                    pending.Add(update);

                DebugLogHelper.LogError(
                    log,
                    $"Per-player setting [{packet.ModName}.{packet.PropertyName}] from authenticated Steam identity {senderSteamId} is waiting for an authoritative player slot without a deadline: {reason}");
            }

            private void ApplyResolvedUpdate(
                object viewModel,
                PropertyInfo property,
                LobbyModSettingSyncPacket packet,
                object value,
                ulong senderSteamId,
                int playerId,
                string diagnostic,
                bool deferred)
            {
                if (!string.IsNullOrEmpty(diagnostic))
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Per-player identity sources differed for [{packet.ModName}.{packet.PropertyName}] from {senderSteamId}: {diagnostic}");
                }
                if (packet.SourcePlayerId != playerId)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Per-player payload slot differed from its authenticated final slot for [{packet.ModName}.{packet.PropertyName}]: payload={packet.SourcePlayerId}, final={playerId}, sender={senderSteamId}. The transport identity wins.");
                }

                string dataPropertyName = property.Name + "Data";
                PropertyInfo dataProperty = viewModel.GetType().GetProperty(
                    dataPropertyName,
                    BindingFlags.Instance | BindingFlags.Public);
                if (dataProperty?.GetValue(viewModel) is not Array data ||
                    playerId < 0 || playerId >= data.Length)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Per-player setting [{packet.ModName}.{packet.PropertyName}] could not be applied to companion [{dataPropertyName}] at final slot {playerId}.");
                    return;
                }

                data.SetValue(ClonePendingValue(value), playerId);
                if (viewModel is LobbyModSettingsBaseViewModel baseViewModel)
                    baseViewModel.System_TriggerUpdate(dataPropertyName);
                DebugLogHelper.LogInfo(
                    log,
                    $"[MP-SYNC-EVIDENCE PER-PLAYER-IDENTITY] mod={packet.ModName}, property={packet.PropertyName}, senderSteamId={senderSteamId}, payloadPlayerId={packet.SourcePlayerId}, finalPlayerId={playerId}, deferred={deferred}.");
            }

            private static bool TryResolveAuthoritativePlayer(
                ulong senderSteamId,
                int payloadPlayerId,
                out int playerId,
                out string error,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                bool lobbyPhase = Platform_Multiplayer.Instance?.activeLobby?.members != null;
                if (!PlayerIdentityHelper.TryCaptureHumanRoster(
                        preferInGameRoster: !lobbyPhase,
                        requireAuthoritativeLobbyRoster: true,
                        out Dictionary<int, ulong> players,
                        out error,
                        out _))
                {
                    playerId = 0;
                    return false;
                }

                PlayerIdentityResolution resolution =
                    PlayerIdentityHelper.ResolveAuthenticatedPerPlayerTarget(
                        senderSteamId,
                        payloadPlayerId,
                        players);
                playerId = resolution.PlayerId;
                if (!resolution.IsResolved)
                {
                    error = resolution.Error;
                    return false;
                }
                diagnostic = PlayerIdentityHelper.CaptureProvisionalPlayerIdDiagnostic(
                    senderSteamId,
                    playerId,
                    inGame: !lobbyPhase);
                error = string.Empty;
                return true;
            }

            private static bool IsCurrentHuman(ulong steamId)
            {
                Platform_Multiplayer platform = Platform_Multiplayer.Instance;
                if (platform?.activeLobby?.members != null)
                {
                    return platform.activeLobby.members.Any(member =>
                        member != null && !member.dummyToBeKicked &&
                        (!member.SkirmishMember || member.SkirmishHumanMember) &&
                        member.id.m_SteamID == steamId);
                }

                return platform?.gameMembers?.Any(member =>
                    member != null && !member.kicked && !member.skirmishAI &&
                    member.steamID == steamId) == true;
            }

            private static ulong CurrentLobbyId() =>
                Platform_Multiplayer.Instance?.activeLobby?.id.m_SteamID ?? 0;

            private static object ClonePendingValue(object value)
            {
                if (value is Array array)
                    return array.Clone();
                if (value is byte[] bytes)
                    return (byte[])bytes.Clone();
                return value;
            }

            private static object CreateManagedDetour(
                Type delegateType,
                MethodInfo source,
                Delegate target)
            {
                Type openType = typeof(GameNetworkAPI).Assembly.GetType(
                    "SHCDESE.ManagedHooks.ManagedDetour`1",
                    true);
                Type closedType = openType.MakeGenericType(delegateType);
                return Activator.CreateInstance(closedType, source, target);
            }

            private static void DisposeDetour(object detour)
            {
                if (detour == null)
                    return;
                object hook = detour.GetType()
                    .GetProperty("Hook", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(detour);
                (hook as IDisposable)?.Dispose();
            }

            private sealed class PendingPerPlayerUpdate
            {
                internal PendingPerPlayerUpdate(
                    object viewModel,
                    PropertyInfo property,
                    LobbyModSettingSyncPacket packet,
                    object value,
                    ulong senderSteamId,
                    ulong lobbyId)
                {
                    ViewModel = viewModel;
                    Property = property;
                    Packet = packet;
                    Value = value;
                    SenderSteamId = senderSteamId;
                    LobbyId = lobbyId;
                }

                internal object ViewModel { get; }
                internal PropertyInfo Property { get; }
                internal LobbyModSettingSyncPacket Packet { get; }
                internal object Value { get; }
                internal ulong SenderSteamId { get; }
                internal ulong LobbyId { get; }
            }
        }
    }
#endif

#if !SHARED_PRESET_TESTS
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
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

#if !SHARED_PRESET_TESTS
            ScriptExtenderMultiplayerSyncWorkaround.EnsureInstalled(log);
            if (GameAssetManagerAPI.Instance.GetModifiedFilePath(
                xamlSourceFile,
                out string absoluteXamlSourceFile))
            {
                // The catalog is read from XAML and is therefore available before Noesis has
                // materialized an unselected tab's controls. This avoids touching native layout.
                ModSettingsSearch.RegisterSource(viewModel, absoluteXamlSourceFile, log, modName);
            }
#endif
            viewModel.PreparePresets(log, plugin.Info.Location, modName);
            // Structural validation must happen before the ViewModel can enter the
            // Extender registry. An invalid personal setting therefore fails closed.
            viewModel.ActivatePerPlayerLobbySettings(log, modName);
            object registeredView = null;
            try
            {
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    plugin,
                    modName,
                    viewModel,
                    xamlSourceFile);
                var registration = GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .FirstOrDefault(entry => ReferenceEquals(entry.ViewModel, viewModel));
                registeredView = registration?.View;
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
            // Views are created before a lobby exists. Refresh the cached role whenever
            // the persistent settings hub opens or changes its selected tab.
            Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>
                viewModel.System_RefreshSettingsAccess();
        }
    }
}
