using BepInEx.Logging;
using CustomCustomTrail.Core;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CustomCustomTrail
{
    internal sealed class CustomCustomTrailRuntime : IDisposable
    {
        private delegate void InitCoopMissionsDelegate(FRONT_Multiplayer self);
        private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
        private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string command);

        private static readonly FieldInfo[] CoopTrailFields = Enumerable.Range(1, 4)
            .Select(index => typeof(FRONT_Multiplayer).GetField("CoopTrail" + index, BindingFlags.Static | BindingFlags.NonPublic))
            .ToArray();
        private static readonly MethodInfo UpdateHostInfoMethod = typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MpSetupDataField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private readonly string customTrailsRoot;
        private readonly CustomCustomTrailSettingsViewModel settings;
        private readonly CoopTrailPackageCatalog packageCatalog = new CoopTrailPackageCatalog();
        private readonly MissionCatalog catalog = new MissionCatalog();
        private readonly Dictionary<int, ResolvedMission> resolved = new Dictionary<int, ResolvedMission>();
        private readonly Dictionary<int, FRONT_Multiplayer.CoopMissionSetupData> vanillaMissions =
            new Dictionary<int, FRONT_Multiplayer.CoopMissionSetupData>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Hook initHook;
        private Hook missionHook;
        private Hook buttonHook;
        private InitCoopMissionsDelegate initTrampoline;
        private CoopMissionChangedDelegate missionTrampoline;
        private ButtonClickedDelegate buttonTrampoline;
        private TrailMissionSettingsCoordinator missionSettingsCoordinator;
        private string[] missingMods = Array.Empty<string>();
        private ResolvedMission selected;
        private CoopTrailPackage activePackage;
        private string localPackageError = string.Empty;
        private bool updatingPackage;
        private bool refreshingCatalog;
        private bool enabled;

        public CustomCustomTrailRuntime(
            ManualLogSource log,
            string customTrailsRoot,
            CustomCustomTrailSettingsViewModel settings)
        {
            this.log = log;
            this.customTrailsRoot = customTrailsRoot;
            this.settings = settings;
            enabled = settings.EnableMod;
        }

        public void Initialize()
        {
            missionSettingsCoordinator = new TrailMissionSettingsCoordinator(log, enabled);
            missionSettingsCoordinator.CoopPackagesChanged += OnActiveCoopPackageChanged;
            missionSettingsCoordinator.CoopSetupOpened += OnCoopSetupOpened;
            missionSettingsCoordinator.Initialize();
            settings.ActiveCoopPackageChanged += OnActiveCoopPackageChanged;
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ClearLaunchState()));

            MethodInfo initMethod = RequireMethod("InitCoopMissions");
            initHook = new Hook(initMethod, (InitCoopMissionsDelegate)InitCoopMissionsHook);
            initTrampoline = initHook.GenerateTrampoline<InitCoopMissionsDelegate>();
            MethodInfo missionMethod = RequireMethod("CoopMissionChanged", typeof(int), typeof(int), typeof(bool));
            missionHook = new Hook(missionMethod, (CoopMissionChangedDelegate)CoopMissionChangedHook);
            missionTrampoline = missionHook.GenerateTrampoline<CoopMissionChangedDelegate>();
            MethodInfo buttonMethod = RequireMethod("ButtonClicked", typeof(string));
            buttonHook = new Hook(buttonMethod, (ButtonClickedDelegate)ButtonClickedHook);
            buttonTrampoline = buttonHook.GenerateTrampoline<ButtonClickedDelegate>();

            RefreshPackageCatalog();
            OnActiveCoopPackageChanged();
            LogInfo("Runtime initialized; selected Coop Trail packages use cooptrail.json and centralized Trail settings.");
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value)
                return;

            enabled = value;
            missionSettingsCoordinator?.SetEnabled(value);
            selected = null;
            missingMods = Array.Empty<string>();
            if (!value)
            {
                RestoreVanillaMissions();
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
            }
            else
            {
                OnActiveCoopPackageChanged();
            }
            LogInfo("Mod " + (value ? "enabled" : "disabled") + "; runtime hooks now " +
                (value ? "apply Custom Trail and Coop replacements." : "pass through to Vanilla."));
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            initHook?.Dispose();
            missionHook?.Dispose();
            buttonHook?.Dispose();
            RestoreVanillaMissions();
            settings.ActiveCoopPackageChanged -= OnActiveCoopPackageChanged;
            if (missionSettingsCoordinator != null)
            {
                missionSettingsCoordinator.CoopPackagesChanged -= OnActiveCoopPackageChanged;
                missionSettingsCoordinator.CoopSetupOpened -= OnCoopSetupOpened;
            }
            missionSettingsCoordinator?.ExitContext(force: true);
            missionSettingsCoordinator?.Dispose();
        }

        private void InitCoopMissionsHook(FRONT_Multiplayer self)
        {
            initTrampoline(self);
            missionSettingsCoordinator.EnsureCoopCustomizeButtons();
            if (!enabled)
            {
                resolved.Clear();
                vanillaMissions.Clear();
                return;
            }
            try
            {
                ApplyActivePackage();
            }
            catch (Exception ex)
            {
                LogError("Could not replace Coop missions: " + ex);
            }
        }

        private void CoopMissionChangedHook(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
        {
            missionTrampoline(self, trailId, missionId, resetOrderSwapped);
            missionSettingsCoordinator.EnsureCoopCustomizeButtons();
            if (!enabled)
                return;
            resolved.TryGetValue(MissionCatalog.ToKey(trailId + 1, missionId), out selected);
            if (selected == null)
            {
                missingMods = Array.Empty<string>();
                missionSettingsCoordinator.ExitContext();
                AppendPackageErrorToDescription(trailId, missionId);
                return;
            }

            try
            {
                ApplySelectedMission(self, true);
                missingMods = missionSettingsCoordinator.Enter(
                    selected.Loaded.Definition.ModSettings,
                    editable: false,
                    source: "custom Coop mission");
            }
            catch (Exception ex)
            {
                selected = null;
                missingMods = Array.Empty<string>();
                missionSettingsCoordinator.ExitContext();
                LogError("Could not activate replacement Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ": " + ex);
            }
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string command)
        {
            if (enabled && IsLaunchCommand(command) && CurrentSlotRequiresPackage(self))
            {
                if (!IsLocalPackageReady())
                {
                    ShowBlockedMessage(GetLocalBlockReason());
                    return;
                }
                if (string.Equals(command, "Play", StringComparison.Ordinal) && self.currentLobby != null && self.currentLobby.isHost &&
                    !AreAllHumanPlayersPackageReady(self))
                {
                    ShowBlockedMessage(SerpLocalization.Get("CustomCustomTrail.ErrorParticipantNotReady"));
                    return;
                }
            }
            if (enabled && selected != null && string.Equals(command, "Play", StringComparison.Ordinal))
                ApplySelectedMission(self, false);
            buttonTrampoline(self, command);
        }

        public void RefreshPackageCatalog()
        {
            if (refreshingCatalog)
                return;
            refreshingCatalog = true;
            try
            {
                packageCatalog.Scan(customTrailsRoot, LogInfo, LogError);
                settings.RefreshPackages(packageCatalog.Packages.Values);
            }
            finally
            {
                refreshingCatalog = false;
            }
        }

        private void OnActiveCoopPackageChanged()
        {
            if (updatingPackage)
                return;
            updatingPackage = true;
            try
            {
                RefreshPackageCatalog();
                if (GameNetworkAPI.IsLocalHost())
                {
                    if (packageCatalog.Packages.TryGetValue(settings.ActiveCoopPackageId, out CoopTrailPackage hostPackage))
                    {
                        settings.ActiveCoopPackageFingerprint = hostPackage.Manifest.ContentFingerprint;
                        settings.ActiveCoopPackageMissionCount = hostPackage.Manifest.MissionCount;
                    }
                    else
                    {
                        settings.ActiveCoopPackageFingerprint = string.Empty;
                        settings.ActiveCoopPackageMissionCount = 0;
                    }
                }
                ApplyActivePackage();
            }
            finally
            {
                updatingPackage = false;
            }
        }

        private void OnCoopSetupOpened()
        {
            if (!enabled || selected == null)
                return;
            try
            {
                missingMods = missionSettingsCoordinator.Enter(
                    selected.Loaded.Definition.ModSettings,
                    editable: false,
                    source: "custom Coop mission setup");
                LogInfo("Reapplied custom Coop mission Trail preset after opening the setup screen.");
            }
            catch (Exception exception)
            {
                LogError("Could not reactivate custom Coop mission mod settings after opening setup: " + exception);
            }
        }

        private void ApplyActivePackage()
        {
            RestoreVanillaMissions();
            missionSettingsCoordinator?.SetCoopPackagePresentation(null, 0);
            catalog.Load(null, null, null);
            resolved.Clear();
            vanillaMissions.Clear();
            activePackage = null;
            selected = null;

            if (!enabled)
            {
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorModDisabled"));
                return;
            }
            if (string.IsNullOrEmpty(settings.ActiveCoopPackageId))
            {
                localPackageError = string.Empty;
                settings.SetLocalPackageStatus("OK|VANILLA");
                return;
            }
            if (string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint))
            {
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.StatusChecking"));
                return;
            }
            if (!packageCatalog.Packages.TryGetValue(settings.ActiveCoopPackageId, out activePackage))
            {
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorPackageMissing") + " " + settings.ActiveCoopPackageId);
                return;
            }
            if (!string.Equals(activePackage.Manifest.ContentFingerprint, settings.ActiveCoopPackageFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorFingerprintMismatch"));
                return;
            }

            catalog.Load(activePackage, LogInfo, LogError);
            var resolver = new MissionAssetResolver();
            var prepared = new List<KeyValuePair<int, ResolvedMission>>();
            try
            {
                foreach (KeyValuePair<int, LoadedMission> entry in catalog.Missions)
                {
                    if (CoopTrailFields[entry.Value.TrailNumber - 1] == null)
                        throw new InvalidDataException("Trail" + entry.Value.TrailNumber + " is unavailable in this game build.");
                    FRONT_Multiplayer.CoopMissionSetupData[] trail = GetTrail(entry.Value.TrailNumber);
                    if (trail == null)
                        continue;
                    ResolvedMission mission = resolver.Resolve(entry.Value);
                    if (!string.IsNullOrWhiteSpace(entry.Value.Definition.ModSettingsError))
                        LogError("Trail" + entry.Value.TrailNumber + "/" + entry.Value.MissionNumber.ToString("00") + " disabled all modSettings: " + entry.Value.Definition.ModSettingsError);
                    prepared.Add(new KeyValuePair<int, ResolvedMission>(entry.Key, mission));
                }
                foreach (KeyValuePair<int, ResolvedMission> entry in prepared)
                {
                    LoadedMission loaded = entry.Value.Loaded;
                    FRONT_Multiplayer.CoopMissionSetupData[] trail = GetTrail(loaded.TrailNumber);
                    resolved[entry.Key] = entry.Value;
                    vanillaMissions[entry.Key] = trail[loaded.MissionNumber - 1];
                    trail[loaded.MissionNumber - 1] = entry.Value.CoopData;
                    LogInfo("Replaced Trail" + loaded.TrailNumber + "/" + loaded.MissionNumber.ToString("00") + " from [" + Path.GetFileName(loaded.JsonPath) + "].");
                }
                missionSettingsCoordinator?.SetCoopPackagePresentation(
                    activePackage.Manifest.DisplayName,
                    activePackage.Manifest.MissionCount);
            }
            catch (Exception exception)
            {
                RestoreVanillaMissions();
                SetLocalPackageError(SerpLocalization.Get("CustomCustomTrail.ErrorPackageInvalid") + " " + exception.Message);
                LogError("Selected Coop Trail package is unusable: " + exception);
                return;
            }
            localPackageError = string.Empty;
            settings.SetLocalPackageStatus(ExpectedReadyStatus());
        }

        private void RestoreVanillaMissions()
        {
            foreach (KeyValuePair<int, FRONT_Multiplayer.CoopMissionSetupData> entry in vanillaMissions)
            {
                int trailNumber = entry.Key / 100;
                int missionNumber = entry.Key % 100;
                if (trailNumber < 1 || trailNumber > CoopTrailFields.Length || CoopTrailFields[trailNumber - 1] == null)
                    continue;
                FRONT_Multiplayer.CoopMissionSetupData[] trail = GetTrail(trailNumber);
                if (trail != null && missionNumber >= 1 && missionNumber <= trail.Length)
                    trail[missionNumber - 1] = entry.Value;
            }
            vanillaMissions.Clear();
            resolved.Clear();
        }

        private void ApplySelectedMission(FRONT_Multiplayer self, bool updateHost)
        {
            if (selected == null)
                return;
            if (self.AIVs == null || self.AIVs.Length != 8)
                self.AIVs = Enumerable.Range(0, 8).Select(_ => new FRONT_Multiplayer.MPAIVInfo()).ToArray();

            EngineInterface.MultiplayerSetupData setupData = (EngineInterface.MultiplayerSetupData)MpSetupDataField.GetValue(self);
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in selected.AiInfoByPlayerIndex)
            {
                self.AIVs[entry.Key] = entry.Value;
                setupData.preferredAIVs[entry.Key] = -entry.Value.rotation - 1;
            }

            List<PlayerDefinition> players = selected.Loaded.Definition.Players.Where(player => player != null && player.Active).ToList();
            for (int index = 0; index < players.Count; index++)
            {
                Platform_Multiplayer.MPLobbyMember member = self.currentLobby?.GetLobbyMemberFromThis_PlayerID(index + 1);
                if (member != null)
                    member.colourID = players[index].Colour;
            }

            MainViewModel.Instance.CoopMissionTitle = selected.Loaded.Definition.DisplayName;
            MainViewModel.Instance.StandaloneMissionText = BuildMissionDescription();
            if (updateHost && self.currentLobby != null && self.currentLobby.isHost)
                UpdateHostInfoMethod?.Invoke(self, null);
        }

        private string BuildMissionDescription()
        {
            string description = selected?.Loaded.Definition.Description ?? string.Empty;
            if (missingMods == null || missingMods.Length == 0)
                return description;
            string warning = "Required mission mods not installed: " + string.Join(", ", missingMods) + ".";
            return string.IsNullOrWhiteSpace(description) ? warning : description + "\r\n\r\n" + warning;
        }

        private static bool IsLaunchCommand(string command) =>
            string.Equals(command, "Ready", StringComparison.Ordinal) ||
            string.Equals(command, "ReadyLock", StringComparison.Ordinal) ||
            string.Equals(command, "Play", StringComparison.Ordinal);

        private bool CurrentSlotRequiresPackage(FRONT_Multiplayer self)
        {
            if (string.IsNullOrEmpty(settings.ActiveCoopPackageId) || settings.ActiveCoopPackageMissionCount <= 0 || self?.currentLobby == null)
                return false;
            int ordinal = (self.currentLobby.coopTrailID * 10) + self.currentLobby.coopSelectedMission;
            return ordinal >= 1 && ordinal <= settings.ActiveCoopPackageMissionCount;
        }

        private bool IsLocalPackageReady() =>
            string.Equals(settings.CoopPackageStatus, ExpectedReadyStatus(), StringComparison.Ordinal);

        private string ExpectedReadyStatus() =>
            "OK|" + settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint;

        private bool AreAllHumanPlayersPackageReady(FRONT_Multiplayer self)
        {
            string expected = ExpectedReadyStatus();
            int expectedHumanPlayers = 0;
            foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
            {
                if (member != null && member.SkirmishHumanMember)
                    expectedHumanPlayers++;
            }

            int checkedHumanPlayers = 0;
            for (int playerId = 1; playerId < settings.CoopPackageStatusData.Length; playerId++)
            {
                Platform_Multiplayer.MPLobbyMember member = self.currentLobby.GetLobbyMemberFromThis_PlayerID(playerId);
                if (member == null || !member.SkirmishHumanMember)
                    continue;
                checkedHumanPlayers++;
                if (!string.Equals(settings.CoopPackageStatusData[playerId], expected, StringComparison.Ordinal))
                    return false;
            }
            return expectedHumanPlayers > 0 && checkedHumanPlayers == expectedHumanPlayers;
        }

        private void AppendPackageErrorToDescription(int zeroBasedTrailId, int oneBasedMissionId)
        {
            int ordinal = (zeroBasedTrailId * 10) + oneBasedMissionId;
            if (ordinal < 1 || ordinal > settings.ActiveCoopPackageMissionCount || IsLocalPackageReady())
                return;
            string current = MainViewModel.Instance.StandaloneMissionText ?? string.Empty;
            string warning = GetLocalBlockReason();
            MainViewModel.Instance.StandaloneMissionText = string.IsNullOrWhiteSpace(current) ? warning : current + "\r\n\r\n" + warning;
        }

        private string GetLocalBlockReason() => string.IsNullOrWhiteSpace(localPackageError)
            ? SerpLocalization.Get("CustomCustomTrail.ErrorPackageNotReady")
            : localPackageError;

        private void SetLocalPackageError(string error)
        {
            localPackageError = error ?? string.Empty;
            settings.SetLocalPackageStatus("ERROR|" + localPackageError);
        }

        private static void ShowBlockedMessage(string message)
        {
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                SerpLocalization.Get("CustomCustomTrail.StartBlockedTitle"),
                delegate { },
                message);
        }

        private void ClearLaunchState()
        {
            missionSettingsCoordinator?.ExitContext(force: true);
            selected = null;
            missingMods = Array.Empty<string>();
        }

        private static FRONT_Multiplayer.CoopMissionSetupData[] GetTrail(int trailNumber)
        {
            FieldInfo field = CoopTrailFields[trailNumber - 1] ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "CoopTrail" + trailNumber);
            return (FRONT_Multiplayer.CoopMissionSetupData[])field.GetValue(null);
        }

        private static MethodInfo RequireMethod(string name, params Type[] parameterTypes)
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            return method ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name);
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
