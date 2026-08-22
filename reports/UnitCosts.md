# UnitCosts release status

**Status:** code newer

- Release: [v1.0.14](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.14)
- Release commit: [5a8f668](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/5a8f6683f4e5b5261b6f0c3d2939e821d0fb2861)
- Current main commit: [052884c](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/052884c545ea5a7388b629bab9add42d8bc7c4d0)

## Relevant changed files

- `Shared/GameModeHelper.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`
- `Shared/SerpLocalization.cs`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/UnitCosts.dll`
- `UnitCosts/BepInEx/plugins/UnitCosts_Serp/UnitCosts.pdb`
- `UnitCosts/Locales/ar.txt`
- `UnitCosts/Locales/cs-CZ.txt`
- `UnitCosts/Locales/de-DE.txt`
- `UnitCosts/Locales/el-GR.txt`
- `UnitCosts/Locales/en-US.txt`
- `UnitCosts/Locales/es-ES.txt`
- `UnitCosts/Locales/fr-FR.txt`
- `UnitCosts/Locales/hu-HU.txt`
- `UnitCosts/Locales/it-IT.txt`
- `UnitCosts/Locales/ja-JP.txt`
- `UnitCosts/Locales/ko-KR.txt`
- `UnitCosts/Locales/nl-NL.txt`
- `UnitCosts/Locales/pl-PL.txt`
- `UnitCosts/Locales/pt-BR.txt`
- `UnitCosts/Locales/ru-RU.txt`
- `UnitCosts/Locales/sv-SE.txt`
- `UnitCosts/Locales/th-TH.txt`
- `UnitCosts/Locales/tr-TR.txt`
- `UnitCosts/Locales/uk-UA.txt`
- `UnitCosts/Locales/zh-CN.txt`
- `UnitCosts/Locales/zh-HK.txt`
- `UnitCosts/src/UnitCostsLobbyViewModel.cs`
- `UnitCosts/src/UnitCostsPlugin.cs`
- `UnitCosts/src/UnitCostsRuntime.cs`

Relevant localization keys: `Common.ClientActivationLabel`, `Common.ClientSettingsActivationHelp`, `Common.HostActivationLabel`, `Common.HostSettingsActivationHelp`

## Diff

```diff
diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index f5be1a9f..be7eee52 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -1,4 +1,5 @@
 using SHCDESE.API;
+using CrusaderDE;
 
 namespace Shared
 {
@@ -17,7 +18,7 @@ namespace Shared
             {
                 foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                 {
-                    if (!member.SkirmishMember)
+                    if (member != null && !member.SkirmishMember)
                         realLobbyMembers++;
                 }
             }
@@ -28,7 +29,7 @@ namespace Shared
             {
                 foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
                 {
-                    if (!member.skirmishAI && member.steamID > 1000)
+                    if (member != null && !member.skirmishAI && member.steamID > 1000)
                         realNetworkGameMembers++;
                 }
             }
@@ -44,7 +45,7 @@ namespace Shared
 
             int gameType = gameData != null ? gameData.game_type : -1;
             int skirmishGameType = gameData != null ? gameData.SkirmishGameType : -1;
-            bool mapEditor = GamePlayerManagerAPI.Instance.IsInMapEditor();
+            bool mapEditor = IsMapEditor();
             // game_type 3 is Vanilla's skirmish family; non-negative subtypes are initialized via StartSkirmishModeGame.
             bool singleplayerSkirmishMode =
                 !realMultiplayer &&
@@ -86,6 +87,33 @@ namespace Shared
         // This broader check also covers future/utility subtypes initialized by Vanilla as skirmish mode.
         public static bool IsSingleplayerSkirmishMode(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsSingleplayerSkirmishMode;
+
+        public static bool IsMapEditor()
+        {
+            try
+            {
+                if (GamePlayerManagerAPI.Instance?.IsInMapEditor() == true)
+                    return true;
+            }
+            catch
+            {
+                // The Script Extender singleton can be unavailable during early plugin startup.
+            }
+
+            // MainViewModel.Instance constructs the ViewModel. Reading it before the
+            // game's own loaded marker is set can therefore fail inside Vanilla code.
+            if (!MainViewModel.viewModelLoaded)
+                return false;
+
+            try
+            {
+                return MainViewModel.Instance?.IsMapEditorMode ?? false;
+            }
+            catch
+            {
+                return false;
+            }
+        }
     }
 
     internal readonly struct GameModeSnapshot

diff --git a/Shared/PresetLobbyModSettingsViewModel.cs b/Shared/PresetLobbyModSettingsViewModel.cs
index 5ef91a1c..5924b0b8 100644
--- a/Shared/PresetLobbyModSettingsViewModel.cs
+++ b/Shared/PresetLobbyModSettingsViewModel.cs
@@ -8,14 +8,605 @@ using SHCDESE.BepInEx.Bootstrap;
 using SHCDESE.ViewModels;
 using System;
 using System.Collections.Generic;
+using System.Collections.ObjectModel;
 using System.Diagnostics;
 using System.IO;
 using System.Linq;
 using System.Reflection;
+using System.Runtime.InteropServices;
 using System.Runtime.CompilerServices;
+#if !SHARED_PRESET_TESTS
+using R3;
+using SHCDESE.EventAPI;
+using UnityEngine;
+#endif
 using ComboBoxItem = Noesis.ComboBoxItem;
 using Visibility = Noesis.Visibility;
 
+namespace Shared
+{
+    internal sealed class PerPlayerLobbySettingsCoordinator
+    {
+        private const int FirstPlayerId = 1;
+        private const int LastPlayerId = 8;
+#if !SHARED_PRESET_TESTS
+        private static readonly FieldInfo LobbyIdField = typeof(Platform_Multiplayer.MPLobby)
+            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly FieldInfo LobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember)
+            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly FieldInfo SteamIdValueField = LobbyMemberIdField?.FieldType
+            .GetField("m_SteamID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        private static readonly MethodInfo GetPlayerIdForSteamIdMethod = typeof(GameNetworkAPI)
+            .GetMethods(BindingFlags.Static | BindingFlags.Public)
+            .Single(method =>
+                method.Name == "GetPlayerIdForSteamId" &&
+                method.GetParameters().Length == 1);
+#endif
+        private readonly PresetLobbyModSettingsViewModel owner;
+        private readonly ManualLogSource log;
+        private readonly string modName;
+        private readonly PerPlayerLobbySettingsContract contract;
+        private readonly Dictionary<int, ulong> playersById = new Dictionary<int, ulong>();
+        private ulong lobbyId;
+        private bool hasLobby;
+        private bool publishPending;
+        private bool rosterHasUnresolvedPlayers;
+        private int resolvedLocalPlayerId;
+        private bool isResettingSlots;
+        private bool isMirroringLocalSetting;
+        private bool isReady = true;
+        private string readinessError = string.Empty;
+        private bool active;
+#if !SHARED_PRESET_TESTS
+        private int lastObservedFrame = -1;
+        private float nextErrorLogTime;
+        private IDisposable mapStartSubscription;
+        private IDisposable mapUnloadSubscription;
+        private bool mapStarted;
+#endif
+
+        internal PerPlayerLobbySettingsCoordinator(
+            PresetLobbyModSettingsViewModel owner,
+            ManualLogSource log,
+            string modName,
+            PerPlayerLobbySettingsContract contract)
+        {
+            this.owner = owner;
+            this.log = log;
+            this.modName = modName;
+            this.contract = contract;
+        }
+
+        internal bool IsReady => isReady;
+        internal string ReadinessError => readinessError;
+
+        internal void Activate()
+        {
+            if (contract.Settings.Count == 0)
+                return;
+            if (active)
+                return;
+
+            try
+            {
+                owner.PropertyChanged += OnOwnerPropertyChanged;
+#if !SHARED_PRESET_TESTS
+                Application.onBeforeRender += OnBeforeRender;
+                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Pre)
+                        mapStarted = true;
+                });
+                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Post)
+                        mapStarted = false;
+                });
+                if (mapStartSubscription == null || mapUnloadSubscription == null)
+                    throw new InvalidOperationException("The persistent map lifecycle subscriptions could not be created.");
+#endif
+                active = true;
+                RequestPublish();
+            }
+            catch
+            {
+                Deactivate();
+                throw;
+            }
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{modName}] Shared per-player lobby convergence activated: " +
+                $"settings=[{string.Join(",", contract.Settings.Select(item => item.Property.Name))}], " +
+                $"required=[{string.Join(",", contract.Settings.Where(item => item.IsReportRequired).Select(item => item.Property.Name))}].");
+        }
+
+        internal void Deactivate()
+        {
+            owner.PropertyChanged -= OnOwnerPropertyChanged;
+#if !SHARED_PRESET_TESTS
+            Application.onBeforeRender -= OnBeforeRender;
+            mapStartSubscription?.Dispose();
+            mapUnloadSubscription?.Dispose();
+            mapStartSubscription = null;
+            mapUnloadSubscription = null;
+            mapStarted = false;
+#endif
+            active = false;
+            publishPending = false;
+        }
+
+        internal void RequestPublish()
+        {
+            publishPending = true;
+        }
+
+        internal bool ArePlayersReady(IEnumerable<int> playerIds, out string error)
+        {
+            int[] supplied = (playerIds ?? Enumerable.Empty<int>()).ToArray();
+            if (supplied.Any(id => !IsValidPlayerId(id)))
+            {
+                error = "At least one supplied human player ID is invalid.";
+                return false;
+            }
+            int[] expected = supplied
+                .Distinct()
+                .OrderBy(id => id)
+                .ToArray();
+            if (expected.Length == 0)
+            {
+                error = "No valid human player IDs were supplied.";
+                return false;
+            }
+            if (rosterHasUnresolvedPlayers)
+            {
+                error = "At least one human lobby member has no stable player ID yet.";
+                return false;
+            }
+            if (hasLobby && !expected.SequenceEqual(playersById.Keys.OrderBy(id => id)))
+            {
+                error = $"The requested human players [{string.Join(",", expected)}] do not match the converged lobby roster [{string.Join(",", playersById.Keys.OrderBy(id => id))}].";
+                return false;
+            }
+            if (hasLobby && (!IsValidPlayerId(resolvedLocalPlayerId) || !playersById.ContainsKey(resolvedLocalPlayerId)))
+            {
+                error = "The local human player ID is not part of the converged lobby roster.";
+                return false;
+            }
+
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings.Where(item => item.IsReportRequired))
+            {
+                Array data = setting.GetData();
+                foreach (int playerId in expected)
+                {
+                    object value = data.GetValue(playerId);
+                    if (!setting.HasReport(value))
+                    {
+                        error = $"Player {playerId} has not reported [{setting.Property.Name}].";
+                        return false;
+                    }
+                }
+            }
+
+            error = string.Empty;
+            return true;
+        }
+
+        internal void Observe(
+            ulong? currentLobbyId,
+            IReadOnlyDictionary<int, ulong> currentPlayers,
+            bool hasUnresolvedPlayers,
+            int localPlayerId,
+            bool preserveForMapTransition)
+        {
+            if (!currentLobbyId.HasValue)
+            {
+                if (preserveForMapTransition)
+                    return;
+
+                if (hasLobby || playersById.Count != 0)
+                {
+                    ResetSlots(Enumerable.Range(FirstPlayerId, LastPlayerId));
+                    hasLobby = false;
+                    lobbyId = 0;
+                    playersById.Clear();
+                    rosterHasUnresolvedPlayers = false;
+                    resolvedLocalPlayerId = 0;
+                    publishPending = false;
+                    contract.LobbyChanged?.Invoke(PerPlayerLobbySnapshot.Empty);
+                }
+                SetReadiness(true, string.Empty);
+                return;
+            }
+
+            // Domain observers run in the lobby only. Settings are immutable once
+            // the map starts, so no file/status refresh may publish into a match.
+            contract.Observe?.Invoke();
+
+            var normalized = new Dictionary<int, ulong>();
+            foreach (KeyValuePair<int, ulong> player in currentPlayers ?? new Dictionary<int, ulong>())
+            {
+                if (IsValidPlayerId(player.Key) && player.Value != 0)
+                    normalized[player.Key] = player.Value;
+            }
+
+            bool sessionChanged = !hasLobby || lobbyId != currentLobbyId.Value;
+            bool membershipChanged = sessionChanged ||
+                normalized.Count != playersById.Count ||
+                normalized.Any(player =>
+                    !playersById.TryGetValue(player.Key, out ulong previousSteamId) ||
+                    previousSteamId != player.Value);
+            bool resolutionChanged = rosterHasUnresolvedPlayers != hasUnresolvedPlayers ||
+                resolvedLocalPlayerId != localPlayerId;
+            if (membershipChanged)
+            {
+                int[] slotsToReset = sessionChanged
+                    ? Enumerable.Range(FirstPlayerId, LastPlayerId).ToArray()
+                    : Enumerable.Range(FirstPlayerId, LastPlayerId)
+                        .Where(id =>
+                            playersById.TryGetValue(id, out ulong previousSteamId) &&
+                            (!normalized.TryGetValue(id, out ulong currentSteamId) ||
+                             currentSteamId != previousSteamId))
+                        .ToArray();
+                ResetSlots(slotsToReset);
+                hasLobby = true;
+                lobbyId = currentLobbyId.Value;
+                playersById.Clear();
+                foreach (KeyValuePair<int, ulong> player in normalized)
+                    playersById[player.Key] = player.Value;
+                publishPending = true;
+                DebugLogHelper.LogInfo(
+                    log,
+                    $"[{modName}] Shared per-player lobby roster changed: lobby={currentLobbyId.Value}, " +
+                    $"sessionChanged={sessionChanged}, players=[{string.Join(",", normalized.Keys.OrderBy(id => id))}], " +
+                    $"unresolved={hasUnresolvedPlayers}, resetSlots=[{string.Join(",", slotsToReset)}].");
+            }
+
+            bool localResolved = IsValidPlayerId(localPlayerId) && normalized.ContainsKey(localPlayerId);
+            rosterHasUnresolvedPlayers = hasUnresolvedPlayers;
+            resolvedLocalPlayerId = localPlayerId;
+            if (membershipChanged || resolutionChanged)
+            {
+                contract.LobbyChanged?.Invoke(new PerPlayerLobbySnapshot(
+                    currentLobbyId,
+                    new Dictionary<int, ulong>(normalized),
+                    hasUnresolvedPlayers,
+                    localPlayerId));
+            }
+            if (publishPending && localResolved && !hasUnresolvedPlayers)
+                PublishLocalSettings(localPlayerId);
+
+            if (hasUnresolvedPlayers)
+                SetReadiness(false, "At least one human lobby member has no stable player ID yet.");
+            else if (!localResolved)
+                SetReadiness(false, "The local human player ID is not part of the resolved lobby roster yet.");
+            else if (!ArePlayersReady(normalized.Keys, out string error))
+                SetReadiness(false, error);
+            else
+                SetReadiness(true, string.Empty);
+        }
+
+        private void PublishLocalSettings(int localPlayerId)
+        {
+            contract.BeforePublish?.Invoke();
+            contract.LocalPlayerResolved?.Invoke(localPlayerId);
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
+            {
+                Array data = setting.GetData();
+                data.SetValue(CloneValue(setting.Property.GetValue(owner)), localPlayerId);
+                owner.System_TriggerUpdate(setting.Property.Name);
+            }
+            publishPending = false;
+            contract.Published?.Invoke();
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{modName}] Shared personal settings advertised for playerId={localPlayerId}, " +
+                $"properties={contract.Settings.Count}.");
+        }
+
+        private void ResetSlots(IEnumerable<int> playerIds)
+        {
+            int[] slots = (playerIds ?? Enumerable.Empty<int>())
+                .Where(IsValidPlayerId)
+                .Distinct()
+                .ToArray();
+            if (slots.Length == 0)
+                return;
+
+            foreach (PerPlayerLobbySettingContract setting in contract.Settings)
+            {
+                Array data = setting.GetData();
+                foreach (int playerId in slots)
+                    data.SetValue(CloneValue(setting.CreateResetValue()), playerId);
+                isResettingSlots = true;
+                try
+                {
+                    owner.System_TriggerUpdate(setting.DataProperty.Name);
+                }
+                finally
+                {
+                    isResettingSlots = false;
+                }
+            }
+        }
+
+        private void SetReadiness(bool value, string error)
+        {
+            error = error ?? string.Empty;
+            if (isReady == value && string.Equals(readinessError, error, StringComparison.Ordinal))
+                return;
+            isReady = value;
+            readinessError = error;
+            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.IsPerPlayerLobbySettingsReady));
+            owner.System_TriggerUpdate(nameof(PresetLobbyModSettingsViewModel.PerPlayerLobbySettingsReadinessError));
+        }
+
+        private void OnOwnerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
+        {
+            if (string.IsNullOrEmpty(args?.PropertyName))
+                return;
+            if (contract.Settings.Any(item => item.DataProperty.Name == args.PropertyName))
+            {
+                if (isResettingSlots || isMirroringLocalSetting)
+                    return;
+                contract.RemoteDataChanged?.Invoke(args.PropertyName);
+                RequestReadinessRefresh();
+                return;
+            }
+
+            PerPlayerLobbySettingContract localSetting = contract.Settings.FirstOrDefault(
+                item => item.Property.Name == args.PropertyName);
+            if (localSetting != null && hasLobby &&
+                IsValidPlayerId(resolvedLocalPlayerId) &&
+                playersById.ContainsKey(resolvedLocalPlayerId))
+            {
+                // The transport does not echo a sender's packet back to itself. Keep
+                // the local companion slot authoritative in Shared so individual mods
+                // never need to resolve or guess their own player ID in a setter.
+                localSetting.GetData().SetValue(
+                    CloneValue(localSetting.Property.GetValue(owner)),
+                    resolvedLocalPlayerId);
+                isMirroringLocalSetting = true;
+                try
+                {
+                    owner.System_TriggerUpdate(localSetting.DataProperty.Name);
+                }
+                finally
+                {
+                    isMirroringLocalSetting = false;
+                }
+                RequestReadinessRefresh();
+            }
+        }
+
+        private void RequestReadinessRefresh()
+        {
+            if (!hasLobby)
+                return;
+            if (!ArePlayersReady(playersById.Keys, out string error))
+                SetReadiness(false, error);
+            else
+                SetReadiness(true, string.Empty);
+        }
+
+#if !SHARED_PRESET_TESTS
+        private void OnBeforeRender()
+        {
+            int frame = Time.frameCount;
+            if (lastObservedFrame >= 0 && frame - lastObservedFrame < 15)
+                return;
+            lastObservedFrame = frame;
+
+            try
+            {
+                if (mapStarted)
+                {
+                    // Lobby settings are immutable during a match. OnUnloadMap is
+                    // the authoritative point at which observation may resume.
+                    return;
+                }
+                ObserveCurrentGameLobby();
+            }
+            catch (Exception exception)
+            {
+                SetReadiness(
+                    false,
+                    "The lobby roster could not be observed; waiting for a successful retry.");
+                if (Time.unscaledTime < nextErrorLogTime)
+                    return;
+                nextErrorLogTime = Time.unscaledTime + 5f;
+                DebugLogHelper.LogError(
+                    log,
+                    $"[{modName}] Shared per-player lobby observer recovered from an error: {exception}");
+            }
+        }
+
+        private void ObserveCurrentGameLobby()
+        {
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
+            if (lobby == null)
+            {
+                bool mapTransition = platform?.gameMembers != null &&
+                    platform.gameMembers.Any(member =>
+                        member != null && !member.skirmishAI && !member.kicked);
+                // There is no stable local player slot outside a lobby. Querying the
+                // Extender here only emits warnings and the value is discarded anyway.
+                Observe(null, null, false, 0, mapTransition);
+                return;
+            }
+
+            var players = new Dictionary<int, ulong>();
+            bool unresolved = false;
+            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members ?? Enumerable.Empty<Platform_Multiplayer.MPLobbyMember>())
+            {
+                if (member == null || member.dummyToBeKicked ||
+                    (member.SkirmishMember && !member.SkirmishHumanMember))
+                    continue;
+                object memberSteamId = LobbyMemberIdField?.GetValue(member);
+                int playerId = memberSteamId == null
+                    ? 0
+                    : (int)GetPlayerIdForSteamIdMethod.Invoke(null, new[] { memberSteamId });
+                ulong steamId = ReadSteamId(memberSteamId);
+                if (!IsValidPlayerId(playerId) || steamId == 0 ||
+                    (players.TryGetValue(playerId, out ulong previous) && previous != steamId))
+                {
+                    unresolved = true;
+                    players.Remove(playerId);
+                    continue;
+                }
+                players[playerId] = steamId;
+            }
+
+            ulong currentLobbyId = ReadSteamId(LobbyIdField?.GetValue(lobby));
+            if (currentLobbyId == 0)
+                unresolved = true;
+            Observe(currentLobbyId, players, unresolved, GetLocalPlayerId(), false);
+        }
+
+        private static ulong ReadSteamId(object steamId)
+        {
+            object value = steamId == null ? null : SteamIdValueField?.GetValue(steamId);
+            return value == null ? 0UL : Convert.ToUInt64(value);
+        }
+
+        private static int GetLocalPlayerId()
+        {
+            int playerId = GameNetworkAPI.GetLocalPlayerId();
+            return IsValidPlayerId(playerId) ? playerId : 0;
+        }
+#endif
+
+        private static bool IsValidPlayerId(int playerId) =>
+            playerId >= FirstPlayerId && playerId <= LastPlayerId;
+
+        internal static object CloneValue(object value)
+        {
+            if (!(value is Array source))
+                return value;
+            Array clone = (Array)source.Clone();
+            for (int index = 0; index < clone.Length; index++)
+            {
+                if (clone.GetValue(index) is Array nested)
+                    clone.SetValue(CloneValue(nested), index);
+            }
+            return clone;
+        }
+    }
+
+    public sealed class PerPlayerLobbySettingsBuilder
+    {
+        private readonly PresetLobbyModSettingsViewModel owner;
+        private readonly Dictionary<string, PerPlayerLobbySettingOptions> options = new Dictionary<string, PerPlayerLobbySettingOptions>(StringComparer.Ordinal);
+        private Action beforePublish;
+        private Action<int> localPlayerResolved;
+        private Action<PerPlayerLobbySnapshot> lobbyChanged;
+        private Action<string> remoteDataChanged;
+        private Action published;
+        private Action observe;
+
+        internal PerPlayerLobbySettingsBuilder(PresetLobbyModSettingsViewModel owner) { this.owner = owner; }
+
+        public PerPlayerLobbySettingsBuilder ResetSlotsWith(string propertyName, Func<object> resetValueFactory) { Get(propertyName).ResetValueFactory = resetValueFactory ?? throw new ArgumentNullException(nameof(resetValueFactory)); return this; }
+        public PerPlayerLobbySettingsBuilder RequireReport(string propertyName, Func<object, bool> hasReport = null) { PerPlayerLobbySettingOptions item = Get(propertyName); item.IsReportRequired = true; item.HasReport = hasReport ?? (value => value != null); return this; }
+        public PerPlayerLobbySettingsBuilder BeforePublish(Action callback) { beforePublish += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenLocalPlayerResolved(Action<int> callback) { localPlayerResolved += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenLobbyChanged(Action<PerPlayerLobbySnapshot> callback) { lobbyChanged += callback; return this; }
+        public PerPlayerLobbySettingsBuilder WhenRemoteDataChanged(Action<string> callback) { remoteDataChanged += callback; return this; }
+        public PerPlayerLobbySettingsBuilder AfterPublish(Action callback) { published += callback; return this; }
+        public PerPlayerLobbySettingsBuilder OnObservation(Action callback) { observe += callback; return this; }
+
+        internal PerPlayerLobbySettingsContract Build()
+        {
+            PropertyInfo[] properties = owner.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
+            foreach (PropertyInfo property in properties)
+            {
+                bool host = property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;
+                bool player = property.GetCustomAttribute<SyncPerPlayerAttribute>() != null;
+                bool local = property.GetCustomAttribute<PresetLocalAttribute>() != null;
+                int classifications = (host ? 1 : 0) + (player ? 1 : 0) + (local ? 1 : 0);
+                if (classifications > 1)
+                    throw new InvalidOperationException($"Setting [{owner.GetType().Name}.{property.Name}] has conflicting sync/preset classifications.");
+            }
+
+            var settings = new List<PerPlayerLobbySettingContract>();
+            foreach (PropertyInfo property in properties.Where(item => item.GetCustomAttribute<SyncPerPlayerAttribute>() != null))
+            {
+                if (!property.CanRead)
+                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] is not readable.");
+                PropertyInfo dataProperty = owner.GetType().GetProperty(property.Name + "Data", BindingFlags.Instance | BindingFlags.Public);
+                if (dataProperty == null || !dataProperty.CanRead || !dataProperty.PropertyType.IsArray)
+                    throw new InvalidOperationException($"Per-player setting [{owner.GetType().Name}.{property.Name}] requires a readable [{property.Name}Data] array.");
+                Type elementType = dataProperty.PropertyType.GetElementType();
+                if (!elementType.IsAssignableFrom(property.PropertyType))
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] has element type [{elementType}], expected [{property.PropertyType}].");
+                Array data = dataProperty.GetValue(owner) as Array;
+                if (data == null || data.Rank != 1 || data.Length < 9)
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must be a one-dimensional array containing slots 0 through 8.");
+                if (!ReferenceEquals(data, dataProperty.GetValue(owner)))
+                    throw new InvalidOperationException($"Companion [{owner.GetType().Name}.{dataProperty.Name}] must return one stable array instance.");
+
+                options.TryGetValue(property.Name, out PerPlayerLobbySettingOptions configured);
+                configured = configured ?? new PerPlayerLobbySettingOptions();
+                settings.Add(new PerPlayerLobbySettingContract(property, dataProperty, data, configured));
+            }
+            foreach (string configuredName in options.Keys)
+                if (!settings.Any(item => item.Property.Name == configuredName))
+                    throw new InvalidOperationException($"Per-player policy references non-[SyncPerPlayer] property [{owner.GetType().Name}.{configuredName}].");
+            return new PerPlayerLobbySettingsContract(settings, beforePublish, localPlayerResolved, lobbyChanged, remoteDataChanged, published, observe);
+        }
+
+        private PerPlayerLobbySettingOptions Get(string propertyName)
+        {
+            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("A property name is required.", nameof(propertyName));
+            if (!options.TryGetValue(propertyName, out PerPlayerLobbySettingOptions value)) options[propertyName] = value = new PerPlayerLobbySettingOptions();
+            return value;
+        }
+    }
+
+    public sealed class PerPlayerLobbySnapshot
+    {
+        internal static readonly PerPlayerLobbySnapshot Empty = new PerPlayerLobbySnapshot(null, new Dictionary<int, ulong>(), false, 0);
+        internal PerPlayerLobbySnapshot(ulong? lobbyId, IReadOnlyDictionary<int, ulong> players, bool unresolved, int localPlayerId)
+        {
+            LobbyId = lobbyId;
+            Players = new ReadOnlyDictionary<int, ulong>(
+                (players ?? new Dictionary<int, ulong>())
+                    .ToDictionary(item => item.Key, item => item.Value));
+            HasUnresolvedPlayers = unresolved;
+            LocalPlayerId = localPlayerId;
+        }
+        public ulong? LobbyId { get; }
+        public IReadOnlyDictionary<int, ulong> Players { get; }
+        public bool HasUnresolvedPlayers { get; }
+        public int LocalPlayerId { get; }
+    }
+
+    internal sealed class PerPlayerLobbySettingOptions { internal Func<object> ResetValueFactory; internal bool IsReportRequired; internal Func<object, bool> HasReport; }
+    internal sealed class PerPlayerLobbySettingContract
+    {
+        private readonly Array data;
+        private readonly PerPlayerLobbySettingOptions options;
+        internal PerPlayerLobbySettingContract(PropertyInfo property, PropertyInfo dataProperty, Array data, PerPlayerLobbySettingOptions options) { Property = property; DataProperty = dataProperty; this.data = data; this.options = options; }
+        internal PropertyInfo Property { get; }
+        internal PropertyInfo DataProperty { get; }
+        internal bool IsReportRequired => options.IsReportRequired;
+        internal Array GetData() => data;
+        internal object CreateResetValue() => options.ResetValueFactory != null ? options.ResetValueFactory() : (Property.PropertyType.IsValueType ? Activator.CreateInstance(Property.PropertyType) : null);
+        internal bool HasReport(object value) => !IsReportRequired || (options.HasReport ?? (item => item != null))(value);
+    }
+    internal sealed class PerPlayerLobbySettingsContract
+    {
+        internal PerPlayerLobbySettingsContract(IReadOnlyList<PerPlayerLobbySettingContract> settings, Action beforePublish, Action<int> localPlayerResolved, Action<PerPlayerLobbySnapshot> lobbyChanged, Action<string> remoteDataChanged, Action published, Action observe) { Settings = settings; BeforePublish = beforePublish; LocalPlayerResolved = localPlayerResolved; LobbyChanged = lobbyChanged; RemoteDataChanged = remoteDataChanged; Published = published; Observe = observe; }
+        internal IReadOnlyList<PerPlayerLobbySettingContract> Settings { get; }
+        internal Action BeforePublish { get; }
+        internal Action<int> LocalPlayerResolved { get; }
+        internal Action<PerPlayerLobbySnapshot> LobbyChanged { get; }
+        internal Action<string> RemoteDataChanged { get; }
+        internal Action Published { get; }
+        internal Action Observe { get; }
+    }
+}
+
 namespace Shared
 {
     /// <summary>
@@ -50,6 +641,7 @@ namespace Shared
         private bool missionPresetEditable;
         private bool isRealMultiplayer;
         private bool isLocalHost = true;
+        private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;
 
         public ComboBoxItem[] PresetOptions => presetOptions;
 
@@ -57,6 +649,22 @@ namespace Shared
 
         public bool HasClientSettings => presetController?.HasClientSettings ?? false;
 
+        public bool HasHostSettingsActivation => presetController?.HasHostSettingsActivation ?? false;
+
+        public bool HasClientSettingsActivation => presetController?.HasClientSettingsActivation ?? false;
+
+        public bool HostSettingsEnabled
+        {
+            get => presetController?.HostSettingsEnabled ?? false;
+            set => presetController?.SetHostSettingsEnabled(value);
+        }
+
+        public bool ClientSettingsEnabled
+        {
+            get => presetController?.ClientSettingsEnabled ?? false;
+            set => presetController?.SetClientSettingsEnabled(value);
+        }
+
         public bool IsLocalSettingsHost => isLocalHost;
 
         public bool IsRealMultiplayerContext => isRealMultiplayer;
@@ -70,6 +678,12 @@ namespace Shared
 
         public bool CanEditClientSettings => true;
 
+        public bool CanToggleHostSettings =>
+            HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;
+
+        public bool CanToggleClientSettings =>
+            HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;
+
         public bool CanChangePreset => isLocalHost || HasClientSettings;
 
         public bool CanResetSettings => CanEditHostSettings || HasClientSettings;
@@ -93,6 +707,15 @@ namespace Shared
         public string PresetText =>
             ResolveSettingsUiText("Common.Preset", "Preset");
 
+        public string ModEnabledText =>
+            ResolveSettingsUiText("Common.EnableMod", "Enable Mod");
+
+        public string HostActivationLabelText =>
+            ResolveSettingsUiText("Common.HostActivationLabel", "(Host-)");
+
+        public string ClientActivationLabelText =>
+            ResolveSettingsUiText("Common.ClientActivationLabel", "(Client settings)");
+
         public Visibility ActionsScopeNoticeVisibility =>
             isRealMultiplayer && HasClientSettings
                 ? Visibility.Visible
@@ -116,6 +739,12 @@ namespace Shared
         public string EnableModHelpText =>
             ResolveSettingsUiText("Common.EnableModHelp", "Enables or disables this mod for the match.");
 
+        public string HostSettingsActivationHelpText =>
+            ResolveSettingsUiText("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");
+
+        public string ClientSettingsActivationHelpText =>
+            ResolveSettingsUiText("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");
+
         public string PresetHelpText =>
             ResolveSettingsUiText("Common.PresetHelp", "Selects a saved preset. Clients change only their personal settings.");
 
@@ -127,6 +756,56 @@ namespace Shared
 
         protected virtual string ResolveSettingsUiText(string key, string fallback) => fallback;
 
+        /// <summary>
+        /// Declares the few domain-specific parts of personal settings. Transport,
+        /// player-slot ownership, lobby convergence and readiness stay in Shared.
+        /// </summary>
+        protected virtual void ConfigurePerPlayerLobbySettings(
+            PerPlayerLobbySettingsBuilder settings)
+        {
+        }
+
+        public bool IsPerPlayerLobbySettingsReady =>
+            perPlayerSettingsCoordinator?.IsReady ?? true;
+
+        public string PerPlayerLobbySettingsReadinessError =>
+            perPlayerSettingsCoordinator?.ReadinessError ?? string.Empty;
+
+        public void System_RequestPerPlayerSettingsPublish()
+        {
+            perPlayerSettingsCoordinator?.RequestPublish();
+        }
+
+        public bool System_ArePerPlayerSettingsReady(
+            IEnumerable<int> playerIds,
+            out string error)
+        {
+            if (perPlayerSettingsCoordinator == null)
+            {
+                error = string.Empty;
+                return true;
+            }
+
+            return perPlayerSettingsCoordinator.ArePlayersReady(playerIds, out error);
+        }
+
+#if SHARED_PRESET_TESTS
+        internal void System_TestObservePerPlayerLobby(
+            ulong? lobbyId,
+            IReadOnlyDictionary<int, ulong> players,
+            bool hasUnresolvedPlayers,
+            int localPlayerId,
+            bool preserveForMapTransition = false)
+        {
+            perPlayerSettingsCoordinator?.Observe(
+                lobbyId,
+                players,
+                hasUnresolvedPlayers,
+                localPlayerId,
+                preserveForMapTransition);
+        }
+#endif
+
         /// <summary>
         /// Authorizes a settings mutation before any backing state is changed.
         /// Preset and Trail snapshots are trusted internal applications; all other
@@ -244,6 +923,27 @@ namespace Shared
             presetController.Activate();
         }
 
+        internal void ActivatePerPlayerLobbySettings(ManualLogSource log, string modName)
+        {
+            if (perPlayerSettingsCoordinator != null)
+                throw new InvalidOperationException($"Per-player lobby settings for [{modName}] were already activated.");
+
+            var builder = new PerPlayerLobbySettingsBuilder(this);
+            ConfigurePerPlayerLobbySettings(builder);
+            perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(
+                this,
+                log,
+                modName,
+                builder.Build());
+            perPlayerSettingsCoordinator.Activate();
+        }
+
+        internal void DeactivatePerPlayerLobbySettings()
+        {
+            perPlayerSettingsCoordinator?.Deactivate();
+            perPlayerSettingsCoordinator = null;
+        }
+
         // Neutral reflection boundary used by optional mission coordinators.
         public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
             presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
@@ -309,6 +1009,11 @@ namespace Shared
             try
             {
                 base.OnPropertyChanged(name);
+
+                if (presetController?.IsHostSettingsActivationProperty(name) == true)
+                    base.OnPropertyChanged(nameof(HostSettingsEnabled));
+                if (presetController?.IsClientSettingsActivationProperty(name) == true)
+                    base.OnPropertyChanged(nameof(ClientSettingsEnabled));
             }
             finally
             {
@@ -333,10 +1038,16 @@ namespace Shared
             base.OnPropertyChanged(nameof(IsRealMultiplayerContext));
             base.OnPropertyChanged(nameof(HasHostSettings));
             base.OnPropertyChanged(nameof(HasClientSettings));
+            base.OnPropertyChanged(nameof(HasHostSettingsActivation));
+            base.OnPropertyChanged(nameof(HasClientSettingsActivation));
+            base.OnPropertyChanged(nameof(HostSettingsEnabled));
+            base.OnPropertyChanged(nameof(ClientSettingsEnabled));
             base.OnPropertyChanged(nameof(MissionPresetEditable));
             base.OnPropertyChanged(nameof(IsMissionPresetSelected));
             base.OnPropertyChanged(nameof(CanEditHostSettings));
             base.OnPropertyChanged(nameof(CanEditClientSettings));
+            base.OnPropertyChanged(nameof(CanToggleHostSettings));
+            base.OnPropertyChanged(nameof(CanToggleClientSettings));
             base.OnPropertyChanged(nameof(CanChangePreset));
             base.OnPropertyChanged(nameof(CanResetSettings));
             base.OnPropertyChanged(nameof(PresetVisibility));
@@ -389,6 +1100,8 @@ namespace Shared
             private readonly PropertyInfo[] persistedProperties;
             private readonly PropertyInfo[] hostProperties;
             private readonly PropertyInfo[] clientProperties;
+            private readonly PropertyInfo hostSettingsActivationProperty;
+            private readonly PropertyInfo clientSettingsActivationProperty;
             private readonly Dictionary<string, PropertyInfo> persistedPropertiesByName;
 
             private Dictionary<string, byte[]> defaults;
@@ -427,12 +1140,34 @@ namespace Shared
                     .ToDictionary(property => property.Name, StringComparer.Ordinal);
                 hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
                 clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
+                hostSettingsActivationProperty = FindSettingsActivationProperty(hostProperties, "EnableMod");
+                clientSettingsActivationProperty = FindSettingsActivationProperty(clientProperties, "EnableClientFeatures", "EnableMod");
             }
 
             public bool HasHostSettings => hostProperties.Length != 0;
 
             public bool HasClientSettings => clientProperties.Length != 0;
 
+            public bool HasHostSettingsActivation => hostSettingsActivationProperty != null;
+
+            public bool HasClientSettingsActivation => clientSettingsActivationProperty != null;
+
+            public bool HostSettingsEnabled => ReadSettingsActivation(hostSettingsActivationProperty);
+
+            public bool ClientSettingsEnabled => ReadSettingsActivation(clientSettingsActivationProperty);
+
+            public void SetHostSettingsEnabled(bool value) =>
+                WriteSettingsActivation(hostSettingsActivationProperty, value);
+
+            public void SetClientSettingsEnabled(bool value) =>
+                WriteSettingsActivation(clientSettingsActivationProperty, value);
+
+            public bool IsHostSettingsActivationProperty(string propertyName) =>
+                IsSettingsActivationProperty(hostSettingsActivationProperty, propertyName);
+
+            public bool IsClientSettingsActivationProperty(string propertyName) =>
+                IsSettingsActivationProperty(clientSettingsActivationProperty, propertyName);
+
             public bool IsApplyingSnapshot => applying;
 
             public bool IsHostPropertyName(string propertyName) =>
@@ -891,6 +1626,40 @@ namespace Shared
                 (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null ||
                     property.GetCustomAttribute<PresetLocalAttribute>() != null);
 
+            private static PropertyInfo FindSettingsActivationProperty(
+                IEnumerable<PropertyInfo> properties,
+                params string[] preferredNames)
+            {
+                foreach (string name in preferredNames)
+                {
+                    PropertyInfo property = properties.FirstOrDefault(item =>
+                        item.Name == name &&
+                        item.PropertyType == typeof(bool) &&
+                        item.CanRead &&
+                        item.CanWrite);
+                    if (property != null)
+                        return property;
+                }
+
+                return null;
+            }
+
+            private bool ReadSettingsActivation(PropertyInfo property) =>
+                property != null && (bool)property.GetValue(owner);
+
+            private void WriteSettingsActivation(PropertyInfo property, bool value)
+            {
+                if (property == null || ReadSettingsActivation(property) == value)
+                    return;
+
+                property.SetValue(owner, value);
+            }
+
+            private static bool IsSettingsActivationProperty(
+                PropertyInfo property,
+                string propertyName) =>
+                property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal);
+
             public static bool IsNetworkSyncInProgress()
             {
                 try
@@ -982,10 +1751,15 @@ namespace Shared
                 catch (Exception ex)
                 {
                     anchor.RollBack();
+                    Exception cause = Unwrap(ex);
                     DebugLogHelper.LogError(
                         log,
                         "Temporary Script Extender multiplayer settings workaround could not be " +
-                        $"installed as one transaction: {Unwrap(ex)}");
+                        $"installed as one transaction: {cause}");
+                    throw new InvalidOperationException(
+                        "Lobby mod settings registration aborted because the required " +
+                        "multiplayer synchronization workaround is unavailable.",
+                        cause);
                 }
             }
         }
@@ -1017,7 +1791,9 @@ namespace Shared
             private object sendPacketToAllLobbyDetour;
             private object processMessageDetour;
             private MethodInfo handleRawPacketMethod;
-            private MethodInfo sendPacketToSteamIdMethod;
+            private Type steamNetworkingIdentityType;
+            private MethodInfo setSteamIdMethod;
+            private MethodInfo sendMessageToUserMethod;
             private FieldInfo lobbyMemberIdField;
             private FieldInfo multiplayerInstanceField;
             private Type steamIdType;
@@ -1059,16 +1835,31 @@ namespace Shared
                     null) ?? throw new MissingMethodException(
                         typeof(GameNetworkAPI).FullName,
                         "HandleRawPacket(short, byte[], CSteamID?)");
-                sendPacketToSteamIdMethod = typeof(GameNetworkAPI)
-                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
-                    .Single(method =>
-                        method.Name == "SendPacketToSteamId" &&
-                        !method.IsGenericMethod &&
-                        method.GetParameters().Length == 2 &&
-                        method.GetParameters()[1].ParameterType == typeof(Platform_Multiplayer.MPData));
                 steamIdType = Nullable.GetUnderlyingType(
                     handleRawPacketMethod.GetParameters()[2].ParameterType) ??
                     throw new InvalidOperationException("HandleRawPacket sender is not nullable.");
+                Assembly steamworksAssembly = steamIdType.Assembly;
+                steamNetworkingIdentityType = steamworksAssembly.GetType(
+                    "Steamworks.SteamNetworkingIdentity",
+                    true);
+                Type steamNetworkingMessagesType = steamworksAssembly.GetType(
+                    "Steamworks.SteamNetworkingMessages",
+                    true);
+                setSteamIdMethod = steamNetworkingIdentityType.GetMethod(
+                    "SetSteamID",
+                    BindingFlags.Instance | BindingFlags.Public,
+                    null,
+                    new[] { steamIdType },
+                    null) ?? throw new MissingMethodException(
+                        steamNetworkingIdentityType.FullName,
+                        "SetSteamID");
+                sendMessageToUserMethod = steamNetworkingMessagesType
+                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
+                    .Single(method =>
+                        method.Name == "SendMessageToUser" &&
+                        method.GetParameters().Length == 5 &&
+                        method.GetParameters()[0].ParameterType.IsByRef &&
+                        method.GetParameters()[1].ParameterType == typeof(IntPtr));
                 lobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember).GetField(
                     "id",
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
@@ -1161,14 +1952,15 @@ namespace Shared
                 int successfulRecipients = 0;
                 foreach (Platform_Multiplayer.MPLobbyMember member in members)
                 {
-                    if (member == null || member.IsSelf() || member.SkirmishMember)
+                    if (member == null || member.IsSelf() || member.dummyToBeKicked ||
+                        (member.SkirmishMember && !member.SkirmishHumanMember))
                         continue;
 
                     eligibleRecipients++;
                     try
                     {
                         object target = lobbyMemberIdField.GetValue(member);
-                        sendPacketToSteamIdMethod.Invoke(null, new[] { target, (object)packet });
+                        SendReliableLobbyPacket(target, packet);
                         successfulRecipients++;
                     }
                     catch (Exception ex)
@@ -1191,12 +1983,57 @@ namespace Shared
                 }
             }
 
+            private void SendReliableLobbyPacket(
+                object targetSteamId,
+                Platform_Multiplayer.MPData packet)
+            {
+                if (targetSteamId == null)
+                    throw new InvalidOperationException("The lobby recipient has no Steam ID.");
+                if (packet == null)
+                    throw new ArgumentNullException(nameof(packet));
+
+                byte[] bytes = packet.ToBytes();
+                object identity = Activator.CreateInstance(steamNetworkingIdentityType);
+                setSteamIdMethod.Invoke(identity, new[] { targetSteamId });
+                GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
+                try
+                {
+                    object result = sendMessageToUserMethod.Invoke(
+                        null,
+                        new object[]
+                        {
+                            identity,
+                            pinned.AddrOfPinnedObject(),
+                            (uint)bytes.Length,
+                            40,
+                            2,
+                        });
+                    if (Convert.ToInt32(result) != 1)
+                    {
+                        throw new InvalidOperationException(
+                            $"SteamNetworkingMessages.SendMessageToUser returned [{result}].");
+                    }
+                }
+                finally
+                {
+                    pinned.Free();
+                }
+            }
+
             private bool ProcessMessageHook(
                 Platform_Multiplayer instance,
                 Platform_Multiplayer.MPData data,
                 Platform_Multiplayer.MPGameMember fromMember,
                 bool fromThread)
             {
+                if (fromThread && data != null &&
+                    data.packetType >= (short)CustomNetworkPacketType.CustomPacketStart)
+                {
+                    // Returning false makes Vanilla enqueue the packet. Its later
+                    // main-thread pass preserves fromMember and safely updates UI/settings.
+                    return false;
+                }
+
                 if (data != null &&
                     fromMember != null &&
                     data.packetType >= (short)CustomNetworkPacketType.CustomPacketStart)
@@ -1335,16 +2172,28 @@ namespace Shared
             ScriptExtenderMultiplayerSyncWorkaround.EnsureInstalled(log);
 #endif
             viewModel.PreparePresets(log, plugin.Info.Location, modName);
-            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
-                plugin,
-                modName,
-                viewModel,
-                xamlSourceFile);
-
-            bool registered = GameXAMLManagerAPI.Instance.RegisteredModSettings
-                .Any(entry => ReferenceEquals(entry.ViewModel, viewModel));
+            // Structural validation must happen before the ViewModel can enter the
+            // Extender registry. An invalid personal setting therefore fails closed.
+            viewModel.ActivatePerPlayerLobbySettings(log, modName);
+            bool registered;
+            try
+            {
+                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
+                    plugin,
+                    modName,
+                    viewModel,
+                    xamlSourceFile);
+                registered = GameXAMLManagerAPI.Instance.RegisteredModSettings
+                    .Any(entry => ReferenceEquals(entry.ViewModel, viewModel));
+            }
+            catch
+            {
+                viewModel.DeactivatePerPlayerLobbySettings();
+                throw;
+            }
             if (!registered)
             {
+                viewModel.DeactivatePerPlayerLobbySettings();
                 DebugLogHelper.LogError(
                     log,
                     $"[{modName}] Presets were not activated because lobby-settings registration failed.");
@@ -1352,7 +2201,6 @@ namespace Shared
             }
 
             viewModel.ActivatePresets();
-
             // Views are created before a lobby exists. Refresh the cached role whenever
             // the persistent settings hub opens or changes its selected tab.
             Plugin.ModSettingsHubViewModel.PropertyChanged += (_, __) =>

diff --git a/Shared/SerpLocalization.cs b/Shared/SerpLocalization.cs
index 4b7b8a89..7fc91c58 100644
--- a/Shared/SerpLocalization.cs
+++ b/Shared/SerpLocalization.cs
@@ -8,6 +8,10 @@ public static class SerpLocalization
 {
     public const string ResetToDefault = "Common.ResetToDefault";
     public const string EnableMod = "Common.EnableMod";
+    public const string HostActivationLabel = "Common.HostActivationLabel";
+    public const string ClientActivationLabel = "Common.ClientActivationLabel";
+    public const string HostSettingsActivationHelp = "Common.HostSettingsActivationHelp";
+    public const string ClientSettingsActivationHelp = "Common.ClientSettingsActivationHelp";
     public const string HostOptions = "Common.HostOptions";
     public const string ClientOptions = "Common.ClientOptions";
     public const string HostReadOnly = "Common.HostReadOnly";
@@ -175,8 +192,13 @@ public static class SerpLocalization
         { SerpsModsClearErrorsHelp, "Clears only this diagnostic display. The BepInEx log remains unchanged." },
         { SerpsModsNoErrors, "No errors recorded." },
         { SerpsModsSummaryFormat, "Pack {Version}: expected {Expected}, validated {Validated}, registered {Registered}." },
+        { SerpsModsLobbyHashMismatch, "ERROR: The installed mods of {Player} differ from lobby host {Host} ({Player}: {PlayerHash}, {Host}: {HostHash})." },
         { ResetToDefault, "Reset to Default" },
         { EnableMod, "Enable Mod" },
+        { HostActivationLabel, "(Host-)" },
+        { ClientActivationLabel, "(Client settings)" },
+        { HostSettingsActivationHelp, "Enables or disables all host-controlled settings of this mod." },
+        { ClientSettingsActivationHelp, "Enables or disables all local and personal client settings of this mod." },
         { HostOptions, "HOST OPTIONS" },
         { ClientOptions, "LOCAL CLIENT OPTIONS" },
         { HostReadOnly, "Values from host - read-only" },

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
index 475de088..4610052e 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/info.json
@@ -3,10 +3,22 @@
   "Author": "Serpens66",
   "Name": "Unit Costs",
   "Description": "Configure recruit costs in Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.14",
+  "Version": "1.0.16",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.16",
+      "Changes": [
+        "Isolated native gold costs, extra costs, enforcement, and optional UI hooks, with per-unit apply and restore failure handling."
+      ]
+    },
+    {
+      "Version": "1.0.15",
+      "Changes": [
+        "Disabled custom recruitment-cost UI, validation, and extra-resource deductions in the map editor."
+      ]
+    },
     {
       "Version": "1.0.14",
       "Changes": [

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ar.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
index aa380b69..383e4cdd 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/de-DE.txt
@@ -2,6 +2,10 @@
 # Format: key=value
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 UnitCosts.Title=Grundkosten (Mensch und KI)
 UnitCosts.Help=Hier können nur native Goldkosten geändert werden. Materialkosten werden über die Zusatzkosten für menschliche Spieler hinzugefügt.
 UnitCosts.ExtraTitle=Zusatzkosten (nur Mensch)

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
index 7b1f7a4a..97c71b0b 100644
--- a/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
+++ b/UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml
@@ -14,17 +14,14 @@
                 HorizontalScrollBarVisibility="Auto">
     <StackPanel Margin="10">
       <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
-        <Border Style="{StaticResource HostActivationBorder}"><CheckBox IsEnabled="{Binding CanEditHostSettings}"
-                  IsChecked="{Binding EnableMod, Mode=TwoWay}"
-                  Content="{Binding EnableModText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding EnableModHelpText}"
-                  Foreground="White"
-                  FontWeight="Bold"
-                  VerticalAlignment="Center"/></Border>
-        <TextBlock Text="{Binding PresetText}" Visibility="{Binding PresetVisibility}" Foreground="#CCCCCC" VerticalAlignment="Center" Margin="14,0,6,0"/>
-        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}"
-                  SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}"
-                  Width="170"
-                  VerticalAlignment="Center"/>
+        <TextBlock Text="{Binding ModEnabledText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        <Border Style="{StaticResource HostActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleHostSettings}" IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}" Content="{Binding HostActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding HostSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <Border Style="{StaticResource ClientActivationBorder}" Margin="8,0,0,0">
+          <CheckBox IsEnabled="{Binding CanToggleClientSettings}" IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}" Content="{Binding ClientActivationLabelText}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ClientSettingsActivationHelpText}" Foreground="White" FontWeight="Bold" VerticalAlignment="Center"/>
+        </Border>
+        <ComboBox IsEnabled="{Binding CanChangePreset}" Visibility="{Binding PresetVisibility}" ItemsSource="{Binding PresetOptions}" SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}" ToolTipService.ShowDuration="60000" ToolTip="{Binding PresetHelpText}" Width="170" VerticalAlignment="Center" Margin="14,0,0,0"/>
         <Button IsEnabled="{Binding CanResetSettings}" Content="{Binding ResetToDefaultText}" Command="{Binding ResetToDefaultCommand}" ToolTipService.ShowDuration="60000" ToolTip="{Binding ResetToDefaultHelpText}" HorizontalAlignment="Left" Padding="10,3" Margin="14,0,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>

diff --git a/UnitCosts/Locales/ar.txt b/UnitCosts/Locales/ar.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/ar.txt
+++ b/UnitCosts/Locales/ar.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/cs-CZ.txt b/UnitCosts/Locales/cs-CZ.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/cs-CZ.txt
+++ b/UnitCosts/Locales/cs-CZ.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/de-DE.txt b/UnitCosts/Locales/de-DE.txt
index aa380b69..383e4cdd 100644
--- a/UnitCosts/Locales/de-DE.txt
+++ b/UnitCosts/Locales/de-DE.txt
@@ -2,6 +2,10 @@
 # Format: key=value
 Common.ResetToDefault=Zurücksetzen
 Common.EnableMod=Mod aktivieren
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client-Settings)
+Common.HostSettingsActivationHelp=Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.
+Common.ClientSettingsActivationHelp=Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.
 UnitCosts.Title=Grundkosten (Mensch und KI)
 UnitCosts.Help=Hier können nur native Goldkosten geändert werden. Materialkosten werden über die Zusatzkosten für menschliche Spieler hinzugefügt.
 UnitCosts.ExtraTitle=Zusatzkosten (nur Mensch)

diff --git a/UnitCosts/Locales/el-GR.txt b/UnitCosts/Locales/el-GR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/el-GR.txt
+++ b/UnitCosts/Locales/el-GR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/en-US.txt b/UnitCosts/Locales/en-US.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/en-US.txt
+++ b/UnitCosts/Locales/en-US.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/es-ES.txt b/UnitCosts/Locales/es-ES.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/es-ES.txt
+++ b/UnitCosts/Locales/es-ES.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/fr-FR.txt b/UnitCosts/Locales/fr-FR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/fr-FR.txt
+++ b/UnitCosts/Locales/fr-FR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/hu-HU.txt b/UnitCosts/Locales/hu-HU.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/hu-HU.txt
+++ b/UnitCosts/Locales/hu-HU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/it-IT.txt b/UnitCosts/Locales/it-IT.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/it-IT.txt
+++ b/UnitCosts/Locales/it-IT.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/ja-JP.txt b/UnitCosts/Locales/ja-JP.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/ja-JP.txt
+++ b/UnitCosts/Locales/ja-JP.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/ko-KR.txt b/UnitCosts/Locales/ko-KR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/ko-KR.txt
+++ b/UnitCosts/Locales/ko-KR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/nl-NL.txt b/UnitCosts/Locales/nl-NL.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/nl-NL.txt
+++ b/UnitCosts/Locales/nl-NL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/pl-PL.txt b/UnitCosts/Locales/pl-PL.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/pl-PL.txt
+++ b/UnitCosts/Locales/pl-PL.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/pt-BR.txt b/UnitCosts/Locales/pt-BR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/pt-BR.txt
+++ b/UnitCosts/Locales/pt-BR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/ru-RU.txt b/UnitCosts/Locales/ru-RU.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/ru-RU.txt
+++ b/UnitCosts/Locales/ru-RU.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/sv-SE.txt b/UnitCosts/Locales/sv-SE.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/sv-SE.txt
+++ b/UnitCosts/Locales/sv-SE.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/th-TH.txt b/UnitCosts/Locales/th-TH.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/th-TH.txt
+++ b/UnitCosts/Locales/th-TH.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/tr-TR.txt b/UnitCosts/Locales/tr-TR.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/tr-TR.txt
+++ b/UnitCosts/Locales/tr-TR.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/uk-UA.txt b/UnitCosts/Locales/uk-UA.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/uk-UA.txt
+++ b/UnitCosts/Locales/uk-UA.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/zh-CN.txt b/UnitCosts/Locales/zh-CN.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/zh-CN.txt
+++ b/UnitCosts/Locales/zh-CN.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/Locales/zh-HK.txt b/UnitCosts/Locales/zh-HK.txt
index 39b70e30..96302488 100644
--- a/UnitCosts/Locales/zh-HK.txt
+++ b/UnitCosts/Locales/zh-HK.txt
@@ -1,6 +1,10 @@
 # Serp mod localization
 # Format: key=value
 Common.EnableMod=Enable Mod
+Common.HostActivationLabel=(Host-)
+Common.ClientActivationLabel=(Client settings)
+Common.HostSettingsActivationHelp=Enables or disables all host-controlled settings of this mod.
+Common.ClientSettingsActivationHelp=Enables or disables all local and personal client settings of this mod.
 Common.ResetToDefault=Reset to Default
 UnitCosts.Title=Base Costs (Human and AI)
 UnitCosts.Help=Only native gold costs can be changed here. Add material costs through extra costs for human players.

diff --git a/UnitCosts/src/UnitCostsLobbyViewModel.cs b/UnitCosts/src/UnitCostsLobbyViewModel.cs
index 6a3b0c66..e5a1f837 100644
--- a/UnitCosts/src/UnitCostsLobbyViewModel.cs
+++ b/UnitCosts/src/UnitCostsLobbyViewModel.cs
@@ -594,6 +594,9 @@ namespace UnitCosts
         {
             try
             {
+                if (!CrusaderDE.MainViewModel.viewModelLoaded)
+                    return null;
+
                 return CrusaderDE.MainViewModel.Instance?.getSmallGoodsIcon((int)good);
             }
             catch
@@ -966,7 +969,7 @@ namespace UnitCosts
 
             private void SetAmount(int value, bool notifyOwner)
             {
-                if (canEdit != null && !canEdit())
+                if (notifyOwner && canEdit != null && !canEdit())
                 {
                     OnPropertyChanged(nameof(AmountText));
                     return;

diff --git a/UnitCosts/src/UnitCostsPlugin.cs b/UnitCosts/src/UnitCostsPlugin.cs
index dd37fd11..53e4fe1d 100644
--- a/UnitCosts/src/UnitCostsPlugin.cs
+++ b/UnitCosts/src/UnitCostsPlugin.cs
@@ -17,7 +17,7 @@ namespace UnitCosts
 
         public const string PluginGuid = "UnitCosts_Serp";
         public const string PluginName = "Unit Costs";
-        public const string PluginVersion = "1.0.14";
+        public const string PluginVersion = "1.0.16";
 
         private UnitCostsRuntime runtime;
         private int libraryInitializationStarted;
@@ -41,25 +41,35 @@ namespace UnitCosts
 
             CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
 
+            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
+            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
             try
             {
-                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
-                runtime.InitializeAfterLibraryLoaded();
-                Settings.RefreshLocalizedNames();
                 Shared.LobbyModSettingsPresetRegistration.Register(
                     this,
                     Logger,
                     "UnitCosts_Serp",
                     Settings,
                     "ScriptExtenderUI/UnitCostsSettings.xaml");
-                GameXAMLManagerAPI.Instance.RegisterBinding(
+            }
+            catch (Exception ex)
+            {
+                Shared.DebugLogHelper.LogError(Logger, $"UnitCosts settings registration failed; gameplay runtime stopped fail-closed: {ex}");
+                return;
+            }
+
+            TryInitializeStage("notification overlay binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                     "UnitCostsNotificationOverlay",
-                    runtime.Notification);
-                GameXAMLManagerAPI.Instance.RegisterBinding(
+                    runtime.Notification));
+            TryInitializeStage("siege notification binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                     "UnitCostsSiegeNotificationInlineHost",
-                    runtime.Notification);
-                RegisterRecruitmentCostTooltipBindings();
-                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; UnitCosts UI registered.");
+                    runtime.Notification));
+            TryInitializeStage("recruitment tooltip bindings", RegisterRecruitmentCostTooltipBindings);
+
+            try
+            {
+                runtime.InitializeAfterLibraryLoaded();
+                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; UnitCosts runtime initialized.");
             }
             catch (Exception ex)
             {
@@ -67,6 +77,15 @@ namespace UnitCosts
             }
         }
 
+        private void TryInitializeStage(string stageName, Action initialize)
+        {
+            try { initialize(); }
+            catch (Exception ex)
+            {
+                Shared.DebugLogHelper.LogError(Logger, $"UnitCosts {stageName} failed; independent stages continue: {ex}");
+            }
+        }
+
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/UnitCosts.diff).
