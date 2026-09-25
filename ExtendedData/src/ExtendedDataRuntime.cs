using APIShared;
using BepInEx.Logging;
using ExtendedData.Core;
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

namespace ExtendedData
{
    internal sealed class ExtendedDataRuntime : IDisposable
    {
        private delegate void InitCoopMissionsDelegate(FRONT_Multiplayer self);
        private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
        private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string command);
        private delegate void UpdateHostInfoDelegate(FRONT_Multiplayer self, bool delayed);

        private sealed class HumanPackageState
        {
            public HumanPackageState(
                string name,
                int playerId,
                string status,
                bool skirmishMember,
                bool skirmishHumanMember)
            {
                Name = name;
                PlayerId = playerId;
                Status = status ?? string.Empty;
                SkirmishMember = skirmishMember;
                SkirmishHumanMember = skirmishHumanMember;
            }

            public string Name { get; }
            public int PlayerId { get; }
            public string Status { get; }
            public bool SkirmishMember { get; }
            public bool SkirmishHumanMember { get; }
        }

        private static readonly FieldInfo[] CoopTrailFields = Enumerable.Range(1, 4)
            .Select(index => typeof(FRONT_Multiplayer).GetField("CoopTrail" + index, BindingFlags.Static | BindingFlags.NonPublic))
            .ToArray();
        private static readonly FieldInfo MainViewModelInstanceField = typeof(MainViewModel)
            .GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateHostInfoMethod = RequireMethod("UpdateHostInfo", typeof(bool));
        private static readonly FieldInfo MpSetupDataField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private readonly string customTrailsRoot;
        private readonly ExtendedDataSettingsViewModel settings;
        private readonly CoopTrailPackageCatalog packageCatalog = new CoopTrailPackageCatalog();
        private readonly MissionCatalog catalog = new MissionCatalog();
        private readonly EditorModSettingsSaveOptionsViewModel editorSaveOptions =
            new EditorModSettingsSaveOptionsViewModel();
        private readonly Dictionary<int, ResolvedMission> resolved = new Dictionary<int, ResolvedMission>();
        private readonly Dictionary<int, FRONT_Multiplayer.CoopMissionSetupData> vanillaMissions =
            new Dictionary<int, FRONT_Multiplayer.CoopMissionSetupData>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Hook initHook;
        private Hook missionHook;
        private Hook buttonHook;
        private Hook updateHostInfoHook;
        private InitCoopMissionsDelegate initTrampoline;
        private CoopMissionChangedDelegate missionTrampoline;
        private ButtonClickedDelegate buttonTrampoline;
        private UpdateHostInfoDelegate updateHostInfoTrampoline;
        private TrailMissionSettingsCoordinator missionSettingsCoordinator;
        private MapModSettingsCoordinator mapSettingsCoordinator;
        private LordDataSyncCoordinator lordDataCoordinator;
        private string[] missingMods = Array.Empty<string>();
        private ResolvedMission selected;
        private CoopTrailPackage activePackage;
        private string localPackageError = string.Empty;
        private bool updatingPackage;
        private bool refreshingCatalog;
        private bool coopLaunchPending;
        private string lastShownLocalBlockSignature = string.Empty;
        private string lastPackageRosterDiagnostic = string.Empty;
        private string lastCompatibilityLogSignature = string.Empty;
        private bool enabled;

        public ExtendedDataRuntime(
            ManualLogSource log,
            string customTrailsRoot,
            ExtendedDataSettingsViewModel settings)
        {
            this.log = log;
            this.customTrailsRoot = customTrailsRoot;
            this.settings = settings;
            enabled = settings.IsRuntimeEnabled;
        }

        public void Initialize()
        {
            ExtendedDataLaunchOriginApi.Initialize(log);
            editorSaveOptions.SetEnabled(enabled);
            GameXAMLManagerAPI.Instance.RegisterBinding(
                "ExtendedDataMapEditorSaveOptionsHost",
                editorSaveOptions);
            GameXAMLManagerAPI.Instance.RegisterBinding(
                "ExtendedDataTrailMakerSaveOptionsHost",
                editorSaveOptions);
            missionSettingsCoordinator = new TrailMissionSettingsCoordinator(
                log,
                enabled,
                settings.GetTrailPropertyMode,
                settings.ApplyTrailSettingModes,
                settings.ApplyTrailSettingModesForMod,
                editorSaveOptions);
            missionSettingsCoordinator.CoopPackagesChanged += OnActiveCoopPackageChanged;
            missionSettingsCoordinator.CoopSetupOpened += OnCoopSetupOpened;
            missionSettingsCoordinator.CoopLaunchReceived += OnCoopLaunchReceived;
            missionSettingsCoordinator.SinglePlayerCoopStarting += PrepareSinglePlayerCoopStart;
            missionSettingsCoordinator.Initialize();
            mapSettingsCoordinator = new MapModSettingsCoordinator(
                log,
                enabled,
                missionSettingsCoordinator,
                editorSaveOptions);
            mapSettingsCoordinator.Initialize();
            lordDataCoordinator = new LordDataSyncCoordinator(log, settings);
            lordDataCoordinator.Initialize();
            missionSettingsCoordinator.LobbyOpened += lordDataCoordinator.OnLobbyOpened;
            mapSettingsCoordinator.MultiplayerSaveLaunchPreparing += lordDataCoordinator.PrepareSave;
            RefreshModCompatibility();
            settings.ActiveCoopPackageChanged += OnActiveCoopPackageChanged;
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnMissionEnded));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(notification =>
            {
                if (missionSettingsCoordinator?.HandleMissionStarted(notification) == false)
                    ClearLaunchTracking();
                if (!notification.Context.IsEditor) OnMapStarted();
            }));

            MethodInfo initMethod = RequireMethod("InitCoopMissions");
            initHook = new Hook(initMethod, (InitCoopMissionsDelegate)InitCoopMissionsHook);
            initTrampoline = initHook.GenerateTrampoline<InitCoopMissionsDelegate>();
            MethodInfo missionMethod = RequireMethod("CoopMissionChanged", typeof(int), typeof(int), typeof(bool));
            missionHook = new Hook(missionMethod, (CoopMissionChangedDelegate)CoopMissionChangedHook);
            missionTrampoline = missionHook.GenerateTrampoline<CoopMissionChangedDelegate>();
            MethodInfo buttonMethod = RequireMethod("ButtonClicked", typeof(string));
            buttonHook = new Hook(buttonMethod, (ButtonClickedDelegate)ButtonClickedHook);
            buttonTrampoline = buttonHook.GenerateTrampoline<ButtonClickedDelegate>();
            updateHostInfoHook = new Hook(UpdateHostInfoMethod, (UpdateHostInfoDelegate)UpdateHostInfoHook);
            updateHostInfoTrampoline = updateHostInfoHook.GenerateTrampoline<UpdateHostInfoDelegate>();

            RefreshPackageCatalog();
            OnActiveCoopPackageChanged();
            LogInfo("Runtime initialized; selected Coop Trail packages use cooptrail.json and centralized Trail settings.");
        }

        public void SetEnabled(bool value)
        {
            if (enabled == value)
                return;

            enabled = value;
            editorSaveOptions.SetEnabled(value);
            missionSettingsCoordinator?.SetEnabled(value);
            mapSettingsCoordinator?.SetEnabled(value);
            selected = null;
            missingMods = Array.Empty<string>();
            if (!value)
            {
                RestoreVanillaMissions();
                SetLocalPackageError(
                    ExtendedDataSettingsViewModel.DisabledStatus,
                    SerpLocalization.Get("ExtendedData.ErrorModDisabled"));
            }
            else
            {
                OnActiveCoopPackageChanged();
            }
            LogInfo("Mod " + (value ? "enabled" : "disabled") + "; runtime hooks now " +
                (value ? "apply Custom Trail and Coop replacements." : "pass through to Vanilla."));
        }

        public void RefreshModCompatibility()
        {
            if (missionSettingsCoordinator == null)
                return;
            TrailModCompatibilityInfo[] catalog = missionSettingsCoordinator
                .DiscoverModCompatibility()
                .ToArray();
            settings.RefreshModCompatibility(catalog);
            string compatible = string.Join(", ", catalog
                .Where(item => item.IsCompatible)
                .Select(item => item.DisplayName)
                .OrderBy(item => item, StringComparer.Ordinal));
            string incompatible = string.Join(", ", catalog
                .Where(item => !item.IsCompatible)
                .Select(item => item.DisplayName)
                .OrderBy(item => item, StringComparer.Ordinal));
            string signature = compatible + "\n" + incompatible;
            if (string.Equals(lastCompatibilityLogSignature, signature, StringComparison.Ordinal))
                return;
            lastCompatibilityLogSignature = signature;
            LogInfo(
                "Map/Trail mod-settings compatibility refreshed: compatible=" +
                catalog.Count(item => item.IsCompatible) + " [" + compatible + "], incompatible=" +
                catalog.Count(item => !item.IsCompatible) + " [" + incompatible + "].");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            initHook?.Dispose();
            missionHook?.Dispose();
            buttonHook?.Dispose();
            updateHostInfoHook?.Dispose();
            RestoreVanillaMissions();
            settings.ActiveCoopPackageChanged -= OnActiveCoopPackageChanged;
            if (missionSettingsCoordinator != null)
            {
                missionSettingsCoordinator.CoopPackagesChanged -= OnActiveCoopPackageChanged;
                missionSettingsCoordinator.CoopSetupOpened -= OnCoopSetupOpened;
                missionSettingsCoordinator.CoopLaunchReceived -= OnCoopLaunchReceived;
                missionSettingsCoordinator.SinglePlayerCoopStarting -= PrepareSinglePlayerCoopStart;
            }
            missionSettingsCoordinator?.ExitContext(force: true);
            mapSettingsCoordinator?.Dispose();
            if (mapSettingsCoordinator != null && lordDataCoordinator != null)
                mapSettingsCoordinator.MultiplayerSaveLaunchPreparing -= lordDataCoordinator.PrepareSave;
            if (missionSettingsCoordinator != null && lordDataCoordinator != null)
                missionSettingsCoordinator.LobbyOpened -= lordDataCoordinator.OnLobbyOpened;
            lordDataCoordinator?.Dispose();
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
            // Vanilla can refresh the selected mission while a launch is already changing maps.
            // Do not mistake that nested refresh for leaving the mission and discard its Trail preset.
            if (!coopLaunchPending)
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
                ActivateSelectedMissionSettingsUnlessMap(
                    self,
                    editable: false,
                    source: "custom Coop mission selection");
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
            if (enabled && IsStartCommand(command) && self?.currentLobby != null &&
                self.currentLobby.isHost && !self.singlePlayerCoop)
            {
                bool captured = lordDataCoordinator.RefreshHost(self);
                bool ready = lordDataCoordinator.IsReadyToLaunch(self, out string lordDataReason);
                if (!captured || !ready)
                {
                    BlockLaunch(command, string.IsNullOrEmpty(lordDataReason)
                        ? "Selected Lord data is not synchronized yet." : lordDataReason);
                    return;
                }
            }
            if (enabled && string.Equals(command, "TMTest", StringComparison.Ordinal) &&
                !missionSettingsCoordinator.PrepareTrailMakerTestLaunch())
            {
                return;
            }
            if (enabled && IsLaunchCommand(command) && CurrentSlotRequiresPackage(self))
            {
                if (!IsLocalPackageReady())
                {
                    BlockLaunch(command, GetLocalBlockReason());
                    return;
                }
                if (selected == null)
                {
                    BlockLaunch(command, SerpLocalization.Get("ExtendedData.ErrorPackageNotReady"));
                    return;
                }
                if (IsStartCommand(command) && !self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
                {
                    List<HumanPackageState> participantStates = GetHumanPackageStates(self);
                    LogInfo("Custom Coop package participant audit: " +
                        DescribeHumanPackageStates(self, participantStates));
                    if (!settings.System_ArePerPlayerSettingsReady(
                            participantStates.Select(state => state.PlayerId),
                            out string syncError))
                    {
                        LogError("Blocked custom Coop package launch because Shared personal settings are incomplete: " + syncError);
                        BlockLaunch(command, SerpLocalization.Get("ExtendedData.ErrorPackageNotReady"));
                        return;
                    }
                    if (!AreAllHumanPlayersPackageReady(participantStates))
                    {
                        BlockLaunch(command, GetParticipantPackageBlockReason(participantStates));
                        return;
                    }
                }
            }
            if (enabled && selected != null && IsLaunchCommand(command))
            {
                try
                {
                    if (IsStartCommand(command))
                    {
                        ApplySelectedMission(self, false);
                        ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(
                            selected.Loaded.TrailNumber - 1,
                            selected.Loaded.MissionNumber);
                    }
                    ActivateSelectedMissionSettingsUnlessMap(
                        self,
                        editable: false,
                        source: "custom Coop mission " + command);
                    if (IsStartCommand(command))
                    {
                        coopLaunchPending = true;
                        missionSettingsCoordinator.PrepareCoopMissionLaunch();
                        if (!self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
                        {
                            missionSettingsCoordinator.BroadcastCoopLaunch(
                                selected.Loaded.TrailNumber - 1,
                                selected.Loaded.MissionNumber);
                        }
                    }
                }
                catch (Exception exception)
                {
                    string reason = SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + exception.Message;
                    LogError("Blocked custom Coop mission " + command + " because launch preparation failed: " + exception);
                    ShowBlockedMessage(reason);
                    return;
                }
            }
            buttonTrampoline(self, command);
        }

        private void UpdateHostInfoHook(FRONT_Multiplayer self, bool delayed)
        {
            updateHostInfoTrampoline(self, delayed);
            if (enabled && !delayed)
                lordDataCoordinator?.RefreshHost(self);
        }

        public void RefreshPackageCatalog()
        {
            if (refreshingCatalog)
                return;
            refreshingCatalog = true;
            try
            {
                var roots = new List<string> { customTrailsRoot };
                roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(LogWarning));
                packageCatalog.Scan(roots, LogInfo, LogError);
                settings.RefreshPackages(packageCatalog.Packages.Values);
            }
            finally
            {
                refreshingCatalog = false;
            }
        }

        private void LogWarning(string message) =>
            Shared.DebugLogHelper.LogWarning(log, message);

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
                        settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
                    }
                    else
                    {
                        settings.ActiveCoopPackageFingerprint = string.Empty;
                        settings.ActiveCoopPackageMissionCount = 0;
                        settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
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
                ActivateSelectedMissionSettingsUnlessMap(
                    GetExistingMainViewModel()?.FRONTMultiplayer,
                    editable: true,
                    source: "custom Coop mission setup");
                LogInfo("Refreshed custom Coop mission mod-settings context after opening the setup screen.");
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
                SetLocalPackageError(ExtendedDataSettingsViewModel.DisabledStatus, SerpLocalization.Get("ExtendedData.ErrorModDisabled"));
                return;
            }
            if (string.IsNullOrEmpty(settings.ActiveCoopPackageId))
            {
                localPackageError = string.Empty;
                SetLocalPackageStatus("OK|VANILLA");
                return;
            }
            if (!GameNetworkAPI.IsLocalHost() &&
                !string.Equals(settings.ActiveCoopPackageDescriptor, ExpectedPackageDescriptor(), StringComparison.Ordinal))
            {
                SetLocalPackageError(ExtendedDataSettingsViewModel.WaitingStatus, SerpLocalization.Get("ExtendedData.StatusChecking"));
                return;
            }
            if (string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint))
            {
                SetLocalPackageError(ExtendedDataSettingsViewModel.WaitingStatus, SerpLocalization.Get("ExtendedData.StatusChecking"));
                return;
            }
            if (!packageCatalog.Packages.TryGetValue(settings.ActiveCoopPackageId, out activePackage))
            {
                SetLocalPackageError(
                    ExtendedDataSettingsViewModel.MissingStatus,
                    SerpLocalization.Get("ExtendedData.ErrorPackageMissing") + " " + settings.ActiveCoopPackageId);
                RefreshVisibleCoopMissionAfterPackageChange();
                ShowLocalPackageBlockAfterSync();
                return;
            }
            if (!string.Equals(activePackage.Manifest.ContentFingerprint, settings.ActiveCoopPackageFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                SetLocalPackageError(
                    ExtendedDataSettingsViewModel.MismatchStatus,
                    SerpLocalization.Get("ExtendedData.ErrorFingerprintMismatch"));
                RefreshVisibleCoopMissionAfterPackageChange();
                ShowLocalPackageBlockAfterSync();
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
                        LogError("Trail" + entry.Value.TrailNumber + "/" + entry.Value.MissionNumber.ToString("00") + " ignored an invalid mod-settings sidecar; local mod settings remain unchanged: " + entry.Value.Definition.ModSettingsError);
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
                SetLocalPackageError(
                    ExtendedDataSettingsViewModel.InvalidStatusPrefix + exception.Message,
                    SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + exception.Message);
                LogError("Selected Coop Trail package is unusable: " + exception);
                RefreshVisibleCoopMissionAfterPackageChange();
                ShowLocalPackageBlockAfterSync();
                return;
            }
            localPackageError = string.Empty;
            SetLocalPackageStatus(ExpectedReadyStatus());
            RefreshVisibleCoopMissionAfterPackageChange();
            ShowLocalPackageBlockAfterSync();
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
            if (selected == null || self == null)
                return;
            if (self.AIVs == null || self.AIVs.Length != 8)
                self.AIVs = Enumerable.Range(0, 8).Select(_ => new FRONT_Multiplayer.MPAIVInfo()).ToArray();

            EngineInterface.MultiplayerSetupData setupData = (EngineInterface.MultiplayerSetupData)MpSetupDataField.GetValue(self);
            ApplyMultiplayerSetup(setupData, selected.Loaded.Definition.Settings.MultiplayerSetup);
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in selected.AiInfoByPlayerIndex)
            {
                self.AIVs[entry.Key] = entry.Value;
                setupData.preferredAIVs[entry.Key] = selected.PreferredAivByPlayerIndex[entry.Key];
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
                UpdateHostInfoMethod.Invoke(self, new object[] { false });
        }

        private static void ApplyMultiplayerSetup(
            EngineInterface.MultiplayerSetupData target,
            MultiplayerSetupSettings source)
        {
            if (target == null || source == null)
                return;
            target.starting_gamespeed = source.StartingGameSpeed;
            target.win_condition = source.WinCondition;
            target.allow_autotrading = source.AllowAutoTrading;
            target.no_knockdown_walls = source.NoKnockdownWalls;
            target.autosave = source.AutoSave;
            target.peacetime = source.PeaceTime;
            target.no_cows = source.NoCows;
            target.no_dogs = source.NoDogs;
            target.extreme_troops = source.ExtremeTroops;
            target.extreme_powers = source.ExtremePowers;
            target.extreme_powers_around_lord = source.ExtremePowersAroundLord;
            target.allow_outposts = source.AllowOutposts;
            target.advanced_options = source.AdvancedOptions;
            target.advanced_skirmish_options = source.AdvancedSkirmishOptions;
            target.advopt_pre_build = source.PreBuild;
            target.advopt_improved_arabswordsmen = source.ImprovedArabSwordsmen;
            target.advopt_improved_laddermen = source.ImprovedLaddermen;
            target.advopt_improved_spearmen = source.ImprovedSpearmen;
            target.advopt_rebalanced_horsearchers = source.RebalancedHorseArchers;
            target.advopt_improved_fletchers = source.ImprovedFletchers;
            target.advopt_uncapped_peasants = source.UncappedPeasants;
            target.advopt_faster_peasants = source.FasterPeasants;
            target.advopt_enemy_hps = source.EnemyHitPoints;
            target.global_improved_sieging = source.ImprovedSieging;
            target.advopt_healers = source.Healers;
            target.advopt_eunuchs = source.Eunuchs;
            target.advopt_nogold = source.NoGold;
            target.global_improved_sieging2 = source.ImprovedSieging2;
            for (int index = 3; index < source.BuildingsAvailable.Length; index++)
                target.MP_BuildingsAvailable[index] = source.BuildingsAvailable[index];
            Array.Copy(source.GoodsAvailable, target.MP_GoodsAvailable, source.GoodsAvailable.Length);
            Array.Copy(source.TroopsAvailable, target.MP_TroopsAvailable, source.TroopsAvailable.Length);
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
            IsStartCommand(command);

        private static bool IsStartCommand(string command) =>
            string.Equals(command, "Play", StringComparison.Ordinal) ||
            string.Equals(command, "COOP_START", StringComparison.Ordinal);

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

        private string ExpectedPackageDescriptor() =>
            settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint + "|" + settings.ActiveCoopPackageMissionCount;

        private List<HumanPackageState> GetHumanPackageStates(FRONT_Multiplayer self)
        {
            var result = new List<HumanPackageState>();
            if (self?.currentLobby?.members == null)
                return result;

            bool rosterResolved = Shared.PlayerIdentityHelper.TryCaptureHumanRoster(
                preferInGameRoster: false,
                requireAuthoritativeLobbyRoster: true,
                out Dictionary<int, ulong> playersById,
                out string rosterError,
                out string rosterDiagnostic);
            ReportPackageRosterDiagnostic(
                rosterResolved ? rosterDiagnostic : rosterError);

            foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
            {
                // Vanilla treats every non-Skirmish lobby member as human. The separate
                // SkirmishHumanMember flag only distinguishes humans from Skirmish AIs.
                if (member == null || member.dummyToBeKicked ||
                    (!member.SkirmishHumanMember && member.SkirmishMember))
                    continue;

                Shared.PlayerIdentityResolution identity = rosterResolved
                    ? Shared.PlayerIdentityHelper.ResolvePlayerIdForSteamId(
                        member.id.m_SteamID,
                        playersById)
                    : default(Shared.PlayerIdentityResolution);
                int playerId = identity.IsResolved ? identity.PlayerId : 0;
                if (rosterResolved && !identity.IsResolved)
                    ReportPackageRosterDiagnostic(identity.Error);
                string status = playerId > 0 && playerId < settings.CoopPackageStatusData.Length
                    ? settings.CoopPackageStatusData[playerId] ?? string.Empty
                    : string.Empty;
                string name = string.IsNullOrWhiteSpace(member.name)
                    ? "Player " + (playerId > 0 ? playerId.ToString() : "?")
                    : member.name;
                result.Add(new HumanPackageState(
                    name,
                    playerId,
                    status,
                    member.SkirmishMember,
                    member.SkirmishHumanMember));
            }
            return result;
        }

        private void ReportPackageRosterDiagnostic(string diagnostic)
        {
            diagnostic = diagnostic ?? string.Empty;
            if (string.Equals(lastPackageRosterDiagnostic, diagnostic, StringComparison.Ordinal))
                return;
            lastPackageRosterDiagnostic = diagnostic;
            if (!string.IsNullOrEmpty(diagnostic))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Custom Coop package player identity mismatch; launch remains blocked until the roster converges: {diagnostic}");
            }
        }

        private bool AreAllHumanPlayersPackageReady(IReadOnlyCollection<HumanPackageState> states) =>
            states.Count > 0 && states.All(state =>
                state.PlayerId > 0 &&
                string.Equals(state.Status, ExpectedReadyStatus(), StringComparison.Ordinal));

        private string GetParticipantPackageBlockReason(IReadOnlyCollection<HumanPackageState> states)
        {
            string expected = ExpectedReadyStatus();
            var missing = new List<string>();
            var mismatched = new List<string>();
            var notReady = new List<string>();
            foreach (HumanPackageState state in states)
            {
                if (state.PlayerId > 0 && string.Equals(state.Status, expected, StringComparison.Ordinal))
                    continue;
                if (string.Equals(state.Status, ExtendedDataSettingsViewModel.MissingStatus, StringComparison.Ordinal))
                    missing.Add(state.Name);
                else if (string.Equals(state.Status, ExtendedDataSettingsViewModel.MismatchStatus, StringComparison.Ordinal) ||
                    state.Status.StartsWith("OK|", StringComparison.Ordinal))
                    mismatched.Add(state.Name);
                else
                    notReady.Add(state.Name);
            }

            var reasons = new List<string>();
            if (missing.Count != 0)
                reasons.Add(SerpLocalization.Get("ExtendedData.ErrorParticipantsMissing") + " " + string.Join(", ", missing));
            if (mismatched.Count != 0)
                reasons.Add(SerpLocalization.Get("ExtendedData.ErrorParticipantsMismatch") + " " + string.Join(", ", mismatched));
            if (notReady.Count != 0)
                reasons.Add(SerpLocalization.Get("ExtendedData.ErrorParticipantsNotReady") + " " + string.Join(", ", notReady));
            return reasons.Count == 0
                ? SerpLocalization.Get("ExtendedData.ErrorPackageNotReady")
                : string.Join("\r\n", reasons);
        }

        private string DescribeHumanPackageStates(
            FRONT_Multiplayer self,
            IReadOnlyCollection<HumanPackageState> states)
        {
            int lobbyMemberCount = self?.currentLobby?.members?.Count ?? -1;
            if (states.Count == 0)
                return "lobbyMembers=" + lobbyMemberCount + ", humans=none";
            string expected = ExpectedReadyStatus();
            return "lobbyMembers=" + lobbyMemberCount + ", humans=" + string.Join("; ", states.Select(state =>
                state.Name + "[playerId=" + state.PlayerId +
                ", kind=" + (state.SkirmishMember ? "skirmish-human" : "coop-human") +
                ", skirmishHuman=" + state.SkirmishHumanMember +
                ", status=" + DescribePackageStatus(state.Status, expected) + "]"));
        }

        private static string DescribePackageStatus(string status, string expected)
        {
            if (string.Equals(status, expected, StringComparison.Ordinal))
                return "ready";
            if (string.Equals(status, ExtendedDataSettingsViewModel.MissingStatus, StringComparison.Ordinal))
                return "missing";
            if (string.Equals(status, ExtendedDataSettingsViewModel.MismatchStatus, StringComparison.Ordinal) ||
                (status ?? string.Empty).StartsWith("OK|", StringComparison.Ordinal))
            {
                return "mismatch";
            }
            if ((status ?? string.Empty).StartsWith(ExtendedDataSettingsViewModel.InvalidStatusPrefix, StringComparison.Ordinal))
                return "invalid";
            if (string.Equals(status, ExtendedDataSettingsViewModel.DisabledStatus, StringComparison.Ordinal))
                return "disabled";
            return string.IsNullOrEmpty(status) ? "unreported" : "waiting";
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
            ? SerpLocalization.Get("ExtendedData.ErrorPackageNotReady")
            : localPackageError;

        private void SetLocalPackageError(string status, string error)
        {
            localPackageError = error ?? string.Empty;
            SetLocalPackageStatus(status);
        }

        private void SetLocalPackageStatus(string status)
        {
            settings.SetLocalPackageStatus(status);
            // A derived status can remain textually identical after host settings
            // arrive. Requesting Shared publication still advertises it for the
            // current player slot after lobby convergence.
            settings.System_RequestPerPlayerSettingsPublish();
        }

        private void RefreshVisibleCoopMissionAfterPackageChange()
        {
            // Do not use Instance here: its getter constructs Vanilla's view model before the UI is ready.
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (self?.currentLobby == null || !self.currentLobby.coopTrailGame)
                return;
            int trailId = self.currentLobby.coopTrailID;
            int missionId = self.currentLobby.coopSelectedMission;
            if (trailId < 0 || trailId >= CoopTrailFields.Length || missionId < 1 || missionId > 10)
                return;

            // Host package settings can arrive after AutoJoinLobby selected Vanilla data.
            // Re-run the same Vanilla selection path so map, AIs, title and Trail preset agree.
            self.CoopMissionChanged(trailId, missionId, false);
            LogInfo("Refreshed visible Coop mission after package settings changed: Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ".");
        }

        private void ShowLocalPackageBlockAfterSync()
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (GameNetworkAPI.IsLocalHost() || !CurrentSlotRequiresPackage(self) || IsLocalPackageReady() ||
                string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint))
            {
                lastShownLocalBlockSignature = string.Empty;
                return;
            }
            string status = settings.CoopPackageStatus ?? string.Empty;
            if (!status.StartsWith(ExtendedDataSettingsViewModel.ErrorStatusPrefix, StringComparison.Ordinal))
                return;
            string signature = settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint + "|" +
                self.currentLobby.coopTrailID + "|" + self.currentLobby.coopSelectedMission + "|" + status;
            if (string.Equals(lastShownLocalBlockSignature, signature, StringComparison.Ordinal))
                return;
            lastShownLocalBlockSignature = signature;
            LogError("Showing immediate custom Coop package validation failure after host settings sync: " + GetLocalBlockReason());
            ShowBlockedMessage(GetLocalBlockReason());
        }

        private static void ShowBlockedMessage(string message)
        {
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                SerpLocalization.Get("ExtendedData.StartBlockedTitle"),
                delegate { },
                message);
        }

        private static MainViewModel GetExistingMainViewModel() =>
            MainViewModelInstanceField?.GetValue(null) as MainViewModel;

        private void BlockLaunch(string command, string reason)
        {
            LogError("Blocked custom Coop mission " + command + ": " + reason);
            ShowBlockedMessage(reason);
        }

        private void ClearLaunchTracking()
        {
            selected = null;
            missingMods = Array.Empty<string>();
            coopLaunchPending = false;
        }

        private void ActivateSelectedMissionSettings(bool editable, string source)
        {
            if (selected == null)
                return;
            missingMods = missionSettingsCoordinator.Enter(
                selected.Loaded.Definition.ModSettings,
                editable,
                source);
        }

        private void ActivateSelectedMissionSettingsUnlessMap(
            FRONT_Multiplayer lobby,
            bool editable,
            string source)
        {
            if (mapSettingsCoordinator?.IsActiveForLobby(lobby) == true)
            {
                LogInfo("Retained the manually selected Map mod-settings preset during " + source + ".");
                return;
            }
            ActivateSelectedMissionSettings(editable, source);
        }

        private void OnMapStarted()
        {
            ExtendedDataLaunchOriginApi.MarkMapStarted();
            if (!coopLaunchPending || selected == null)
                return;
            coopLaunchPending = false;
            LogInfo("Custom Coop mission map started; retaining its active mod-settings preset.");
        }

        private bool PrepareSinglePlayerCoopStart(FRONT_Multiplayer lobby)
        {
            if (!enabled || lobby?.currentLobby == null || !lobby.singlePlayerCoop ||
                !lobby.currentLobby.coopTrailGame || !CurrentSlotRequiresPackage(lobby))
            {
                return true;
            }
            if (coopLaunchPending && selected != null)
                return true;

            int trailId = lobby.currentLobby.coopTrailID;
            int missionId = lobby.currentLobby.coopSelectedMission;
            try
            {
                var roots = new List<string> { customTrailsRoot };
                roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(LogWarning));
                packageCatalog.Scan(roots, LogInfo, LogError);

                if (!packageCatalog.Packages.TryGetValue(
                        settings.ActiveCoopPackageId,
                        out CoopTrailPackage currentPackage))
                {
                    throw new FileNotFoundException(
                        SerpLocalization.Get("ExtendedData.ErrorPackageMissing") + " " +
                        settings.ActiveCoopPackageId);
                }
                if (!string.Equals(
                        currentPackage.Manifest.ContentFingerprint,
                        settings.ActiveCoopPackageFingerprint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(SerpLocalization.Get("ExtendedData.ErrorFingerprintMismatch"));
                }

                var restartCatalog = new MissionCatalog();
                restartCatalog.Load(currentPackage, LogInfo, LogError);
                if (!restartCatalog.Missions.TryGetValue(
                        MissionCatalog.ToKey(trailId + 1, missionId),
                        out LoadedMission loaded))
                {
                    throw new InvalidDataException(
                        "The active package does not contain Trail" + (trailId + 1) + "/" +
                        missionId.ToString("00") + ".");
                }

                selected = new MissionAssetResolver().Resolve(loaded);
                ApplySelectedMission(lobby, updateHost: false);
                ActivateSelectedMissionSettingsUnlessMap(
                    lobby,
                    editable: false,
                    source: "single-player Coop restart");
                ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(trailId, missionId);
                missionSettingsCoordinator.PrepareCoopMissionLaunch();
                coopLaunchPending = true;
                LogInfo(
                    "Revalidated custom single-player Coop restart: Trail" + (trailId + 1) + "/" +
                    missionId.ToString("00") + ".");
                return true;
            }
            catch (Exception exception)
            {
                selected = null;
                missingMods = Array.Empty<string>();
                coopLaunchPending = false;
                missionSettingsCoordinator.ExitContext(force: true);
                string reason = SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + exception.Message;
                LogError("Blocked custom single-player Coop restart: " + exception);
                ShowBlockedMessage(reason);
                return false;
            }
        }

        private void OnCoopLaunchReceived(int trailId, int missionId)
        {
            if (!enabled || !resolved.TryGetValue(MissionCatalog.ToKey(trailId + 1, missionId), out ResolvedMission mission))
            {
                LogError($"Ignored authenticated Coop Trail launch for unavailable Trail{trailId + 1}/{missionId:00}.");
                return;
            }
            if (!IsLocalPackageReady())
            {
                LogError($"Ignored authenticated Coop Trail launch for Trail{trailId + 1}/{missionId:00} because the local package is not ready.");
                return;
            }

            // This authenticated launch boundary also covers clients which never execute
            // the host's setup-screen button handler.
            ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(trailId, missionId);

            // Clients do not execute the host's COOP_START button handler. The authenticated
            // transition supplies the missing launch boundary before OnUnloadMap clears presets.
            selected = mission;
            ApplySelectedMission(GetExistingMainViewModel()?.FRONTMultiplayer, false);
            ActivateSelectedMissionSettingsUnlessMap(
                GetExistingMainViewModel()?.FRONTMultiplayer,
                editable: false,
                source: "authenticated host Coop launch");
            coopLaunchPending = true;
            missionSettingsCoordinator.PrepareCoopMissionLaunch();
            LogInfo($"Prepared authenticated Coop Trail launch trail={trailId + 1}, mission={missionId}; retaining its active mod-settings preset across map unload.");
        }

        private void OnMissionEnded(MissionLifecycleNotification notification)
        {
            bool retained = missionSettingsCoordinator?.HandleMissionEnded(notification) == true;
            if (retained)
                return;
            ClearLaunchTracking();
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
