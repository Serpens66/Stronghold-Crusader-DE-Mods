using APIShared;
using BepInEx.Logging;
using ExtendedData.Core;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Path = System.IO.Path;

namespace ExtendedData
{
    internal sealed partial class ExtendedDataRuntime : IDisposable
    {
        private delegate void InitCoopMissionsDelegate(FRONT_Multiplayer self);
        private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
        private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string command);
        private delegate void UpdateHostInfoDelegate(FRONT_Multiplayer self, bool delayed);
        private delegate void UpdateCustomLordNamesDelegate(FRONT_Multiplayer self);
        private delegate string CombinedNameDelegate(Platform_Multiplayer.MPLobbyMember self);
        private delegate void CoopTrail1RowDelegate(FRONT_CoopTrail1.PlayerRow self, FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player);
        private delegate void CoopTrail2RowDelegate(FRONT_CoopTrail2.PlayerRow self, FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player);
        private delegate void CoopTrail3RowDelegate(FRONT_CoopTrail3.PlayerRow self, FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player);
        private delegate void CoopTrail4RowDelegate(FRONT_CoopTrail4.PlayerRow self, FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, int row, int player);

        private sealed class CoopPreviewTeams
        {
            private readonly Grid[] rows;
            private readonly int[] teams = new int[8];
            private readonly bool[] boundaries = new bool[8];
            private readonly Grid[] dividers = new Grid[7];
            private StackPanel list;
            private Image vanillaSword;
            private Visibility vanillaSwordVisibility;
            private int lastRow = -1;
            private bool invalidLayout;

            public CoopPreviewTeams(Grid[] rows) { this.rows = rows; }
            public Grid FirstRow => rows[0];

            public void FailOpen()
            {
                invalidLayout = true;
                if (vanillaSword != null) vanillaSword.Visibility = vanillaSwordVisibility;
                foreach (Grid divider in dividers)
                    if (divider != null) divider.Visibility = Visibility.Collapsed;
            }

            public void Record(FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member,
                int row, bool customMission, Action<string> warn)
            {
                if (row < 0 || row >= teams.Length)
                    return;
                if (row <= lastRow)
                {
                    for (int index = 0; index < teams.Length; index++) teams[index] = -1;
                    lastRow = -1;
                }
                for (int index = lastRow + 1; index < row; index++) teams[index] = -1;
                teams[row] = member == null ? -1 : parent?.currentLobby?.getTeam(member) ?? -1;
                lastRow = row;
                if (row != teams.Length - 1)
                    return;

                if (list == null && !invalidLayout && !TryInstall(out string reason))
                {
                    invalidLayout = true;
                    warn("Custom Coop Trail team preview kept Vanilla layout: " + reason);
                }
                if (invalidLayout)
                    return;

                vanillaSword.Visibility = customMission ? Visibility.Collapsed : vanillaSwordVisibility;
                if (customMission)
                    CoopTrailPreviewTeamPolicy.FillBoundaries(teams, boundaries);
                for (int index = 0; index < dividers.Length; index++)
                    dividers[index].Visibility = customMission && boundaries[index + 1]
                        ? Visibility.Visible : Visibility.Collapsed;
            }

            private bool TryInstall(out string reason)
            {
                reason = null;
                if (rows.Length != 8 || rows[0]?.Parent is not Grid firstWrapper ||
                    firstWrapper.Parent is not StackPanel stack || stack.Children.Count != 9)
                {
                    reason = "expected eight row wrappers and the fixed sword in one nine-child StackPanel";
                    return false;
                }
                for (int index = 0; index < rows.Length; index++)
                {
                    int child = index < 2 ? index : index + 1;
                    if (rows[index]?.Parent is not Grid wrapper ||
                        !ReferenceEquals(wrapper.Parent, stack) ||
                        !ReferenceEquals(stack.Children[child], wrapper))
                    {
                        reason = "row " + (index + 1) + " no longer matches Vanilla's player-list order";
                        return false;
                    }
                }
                if (stack.Children[2] is not Image sword)
                {
                    reason = "fixed Vanilla sword is absent at player-list position three";
                    return false;
                }

                // Insert from the bottom so the validated original child indexes stay valid.
                for (int row = 7; row >= 1; row--)
                {
                    var divider = new Grid { Height = 8, Visibility = Visibility.Collapsed,
                        IsHitTestVisible = false };
                    divider.Children.Add(new Border {
                        Height = 1, Margin = new Thickness(40, 0, 40, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = new SolidColorBrush(Color.FromArgb(255, 165, 139, 83))
                    });
                    dividers[row - 1] = divider;
                    stack.Children.Insert(row < 2 ? row : row + 1, divider);
                }
                list = stack;
                vanillaSword = sword;
                vanillaSwordVisibility = sword.Visibility;
                return true;
            }
        }

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
        private static readonly MethodInfo UpdateRadarShieldPositionsMethod = RequireMethod("UpdateRadarShieldPositions");
        private static readonly MethodInfo ReSortTeamInfoMethod = RequireMethod("ReSortTeamInfo");
        private static readonly FieldInfo MpSetupDataField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LocalReadyField = RequirePrivateLobbyFlag("MPLocalReady");
        private static readonly FieldInfo LocalReadyLockedField = RequirePrivateLobbyFlag("MPLocalReadyLocked");

        private readonly ManualLogSource log;
        private readonly string customTrailsRoot;
        private readonly ExtendedDataSettingsViewModel settings;
        private readonly CoopTrailPackageCatalog packageCatalog = new CoopTrailPackageCatalog();
        private readonly MissionCatalog catalog = new MissionCatalog();
        private readonly EditorModSettingsSaveOptionsViewModel editorSaveOptions =
            new EditorModSettingsSaveOptionsViewModel();
        private readonly CoopMissionCache<ResolvedMission> resolved = new CoopMissionCache<ResolvedMission>();
        private readonly CoopMissionTableOverrides<FRONT_Multiplayer.CoopMissionSetupData> missionTables =
            new CoopMissionTableOverrides<FRONT_Multiplayer.CoopMissionSetupData>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Hook initHook;
        private Hook missionHook;
        private Hook buttonHook;
        private Hook updateHostInfoHook;
        private Hook updateCustomLordNamesHook;
        private Hook combinedNameHook;
        private Hook coopTrail1RowHook;
        private Hook coopTrail2RowHook;
        private Hook coopTrail3RowHook;
        private Hook coopTrail4RowHook;
        private InitCoopMissionsDelegate initTrampoline;
        private CoopMissionChangedDelegate missionTrampoline;
        // Vanilla's CoopMissionChanged ends with UpdateHostInfo() while the lobby AIVs still hold the previous
        // mission's Lords; ExtendedData applies the new ones only after the trampoline. Host Lord publication waits
        // for that (DEVDIAG lobby log 2026-10-26 21:19:56: partner join switched Trail1/10 -> 1/1 and the
        // intermediate check reported "Selected Lord Falcon has no host configuration").
        private int missionChangeDepth;
        private bool hostLordRefreshDeferred;
        private bool preparingHostTrailLordData;
        private ButtonClickedDelegate buttonTrampoline;
        private UpdateHostInfoDelegate updateHostInfoTrampoline;
        private UpdateCustomLordNamesDelegate updateCustomLordNamesTrampoline;
        private CombinedNameDelegate combinedNameTrampoline;
        private CoopTrail1RowDelegate coopTrail1RowTrampoline;
        private CoopTrail2RowDelegate coopTrail2RowTrampoline;
        private CoopTrail3RowDelegate coopTrail3RowTrampoline;
        private CoopTrail4RowDelegate coopTrail4RowTrampoline;
        private CoopPreviewTeams coopTrail1Preview;
        private CoopPreviewTeams coopTrail2Preview;
        private CoopPreviewTeams coopTrail3Preview;
        private CoopPreviewTeams coopTrail4Preview;
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
        private ExtendedDataFeatureActivation activation;
        private readonly CoopTrailMenuViewModel coopTrailMenu;

        public ExtendedDataRuntime(
            ManualLogSource log,
            string customTrailsRoot,
            ExtendedDataSettingsViewModel settings)
        {
            this.log = log;
            this.customTrailsRoot = customTrailsRoot;
            this.settings = settings;
            activation = settings.FeatureActivation;
            coopTrailMenu = new CoopTrailMenuViewModel(settings);
        }

        public void Initialize()
        {
            Shared.DevDiag.Attach(log);
            GameXAMLManagerAPI.Instance.RegisterBinding("ExtendedDataCoopTrailMenuHost", coopTrailMenu);
            ExtendedDataLaunchOriginApi.Initialize(log);
            ExtendedDataModDataApi.InitializeSource();
            editorSaveOptions.SetEnabled(activation.EditorFiles);
            GameXAMLManagerAPI.Instance.RegisterBinding(
                "ExtendedDataMapEditorSaveOptionsHost",
                editorSaveOptions);
            GameXAMLManagerAPI.Instance.RegisterBinding(
                "ExtendedDataTrailMakerSaveOptionsHost",
                editorSaveOptions);
            missionSettingsCoordinator = new TrailMissionSettingsCoordinator(
                log,
                activation,
                settings.GetTrailPropertyMode,
                settings.ApplyTrailSettingModes,
                settings.ApplyTrailSettingModesForMod,
                editorSaveOptions);
            missionSettingsCoordinator.CoopPackagesChanged += OnActiveCoopPackageChanged;
            missionSettingsCoordinator.CoopPackageExported += OnCoopPackagesExported;
            missionSettingsCoordinator.CoopSetupOpened += OnCoopSetupOpened;
            missionSettingsCoordinator.CoopLaunchReceived += OnCoopLaunchReceived;
            missionSettingsCoordinator.SinglePlayerCoopStarting += PrepareSinglePlayerCoopStart;
            missionSettingsCoordinator.SinglePlayerCoopLaunchValidating += CoopAiStartBlockReason;
            missionSettingsCoordinator.SinglePlayerCoopStartRejected += OnSinglePlayerCoopStartRejected;
            missionSettingsCoordinator.Initialize();
            SavegameModSettings.RegisterTrailCreatorRulesProvider(
                missionSettingsCoordinator.CaptureCreatorRulesForActiveTrail,
                missionSettingsCoordinator.WasLaunchedThroughCustomize);
            mapSettingsCoordinator = new MapModSettingsCoordinator(
                log,
                activation,
                missionSettingsCoordinator,
                editorSaveOptions);
            mapSettingsCoordinator.Initialize();
            lordDataCoordinator = new LordDataSyncCoordinator(log, settings);
            lordDataCoordinator.Initialize();
            missionSettingsCoordinator.LordDataCoordinator = lordDataCoordinator;
            missionSettingsCoordinator.LobbyOpened += lordDataCoordinator.OnLobbyOpened;
            mapSettingsCoordinator.MultiplayerSaveLaunchPreparing += PrepareLordSaveWithDiagnostics;
            InitializeLaunchReadiness();
            RefreshModCompatibility();
            settings.ActiveCoopPackageChanged += OnActiveCoopPackageChanged;
            settings.CoopPackageRemoteStatusChanged += OnCoopPackageRemoteStatusChanged;
            settings.LordPackageManifestChanged += OnLordPackageManifestChangedForSelection;
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(OnMissionEnded));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(notification =>
            {
                if (missionSettingsCoordinator?.HandleMissionStarted(notification) == false)
                    ClearLaunchTracking();
                if (!notification.Context.IsEditor) OnMapStarted(notification.Context);
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
            MethodInfo updateNamesMethod = RequireMethod("UpdateCustomLordNamesFromMP");
            updateCustomLordNamesHook = new Hook(updateNamesMethod,
                (UpdateCustomLordNamesDelegate)UpdateCustomLordNamesHook);
            updateCustomLordNamesTrampoline =
                updateCustomLordNamesHook.GenerateTrampoline<UpdateCustomLordNamesDelegate>();
            MethodInfo combinedNameGetter = typeof(Platform_Multiplayer.MPLobbyMember)
                .GetProperty("CombinedName", BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod();
            if (combinedNameGetter == null)
                throw new MissingMethodException("MPLobbyMember.get_CombinedName");
            combinedNameHook = new Hook(combinedNameGetter, (CombinedNameDelegate)CombinedNameHook);
            combinedNameTrampoline = combinedNameHook.GenerateTrampoline<CombinedNameDelegate>();
            coopTrail1RowHook = new Hook(RequirePreviewUpdate(typeof(FRONT_CoopTrail1.PlayerRow)),
                (CoopTrail1RowDelegate)CoopTrail1RowHook);
            coopTrail1RowTrampoline = coopTrail1RowHook.GenerateTrampoline<CoopTrail1RowDelegate>();
            coopTrail2RowHook = new Hook(RequirePreviewUpdate(typeof(FRONT_CoopTrail2.PlayerRow)),
                (CoopTrail2RowDelegate)CoopTrail2RowHook);
            coopTrail2RowTrampoline = coopTrail2RowHook.GenerateTrampoline<CoopTrail2RowDelegate>();
            coopTrail3RowHook = new Hook(RequirePreviewUpdate(typeof(FRONT_CoopTrail3.PlayerRow)),
                (CoopTrail3RowDelegate)CoopTrail3RowHook);
            coopTrail3RowTrampoline = coopTrail3RowHook.GenerateTrampoline<CoopTrail3RowDelegate>();
            coopTrail4RowHook = new Hook(RequirePreviewUpdate(typeof(FRONT_CoopTrail4.PlayerRow)),
                (CoopTrail4RowDelegate)CoopTrail4RowHook);
            coopTrail4RowTrampoline = coopTrail4RowHook.GenerateTrampoline<CoopTrail4RowDelegate>();
            try
            {
                // An older APIShared lacks the capability; the missing member fails when this call is compiled.
                RegisterCoopAiStartProvider();
            }
            catch (Exception exception)
            {
                coopAiImportUnavailableReason = exception.Message;
                LogError("APIShared Coop AI start import could not be acquired: " + exception);
            }

            InstallLordCatalogueObserver();
            OnActiveCoopPackageChanged();
            // Permanent startup marker for support logs (once per process).
            LogInfo("Runtime initialized; selected Coop Trail packages use cooptrail.json and centralized Trail settings.");
        }

        internal void SetActivation(ExtendedDataFeatureActivation value)
        {
            ExtendedDataFeatureActivation previous = activation;
            activation = value;
            if (previous.EditorFiles != value.EditorFiles)
                editorSaveOptions.SetEnabled(value.EditorFiles);
            missionSettingsCoordinator?.SetActivation(value);
            mapSettingsCoordinator?.SetActivation(value);
            lordDataCoordinator?.SetActivation(value);
            if (previous.CoopPackages != value.CoopPackages)
            {
                selected = null;
                lordDataCoordinator?.SetEmbeddedTrailSlots(null);
                missingMods = Array.Empty<string>();
                OnActiveCoopPackageChanged();
            }
            QueueLaunchReadiness();
        }

        public void RefreshModCompatibility()
        {
            QueueLaunchReadiness();
            if (missionSettingsCoordinator == null)
                return;
            long started = 0;
            Shared.DevDiag.Start(ref started);
            TrailModCompatibilityInfo[] catalog = missionSettingsCoordinator
                .DiscoverModCompatibility()
                .ToArray();
            settings.RefreshModCompatibility(catalog);
            Shared.DevDiag.Stop(ref started, "trail.compatibility.refresh");
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
            // Support-relevant: which mods' settings Maps and Trails can carry. Logged only on change.
            LogInfo(
                    "Map/Trail mod-settings compatibility refreshed: compatible=" +
                    catalog.Count(item => item.IsCompatible) + " [" + compatible + "], incompatible=" +
                    catalog.Count(item => !item.IsCompatible) + " [" + incompatible + "].");
        }

        public void Dispose()
        {
            StopLaunchReadiness();
            coopTrailMenu.Disable();
            activation = ExtendedDataFeatureActivation.Disabled;
            ExtendedDataModDataApi.UnregisterSource();
            editorSaveOptions.SetEnabled(false);
            mapSettingsCoordinator?.SetActivation(activation);
            missionSettingsCoordinator?.SetActivation(activation);
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            // Hook fields and their trampolines remain rooted by the plugin's static runtime.
            // Only logical activation and removable subscriptions are retired here.
            RestoreVanillaMissions();
            settings.ActiveCoopPackageChanged -= OnActiveCoopPackageChanged;
            settings.CoopPackageRemoteStatusChanged -= OnCoopPackageRemoteStatusChanged;
            settings.LordPackageManifestChanged -= OnLordPackageManifestChangedForSelection;
            if (missionSettingsCoordinator != null)
            {
                missionSettingsCoordinator.CoopPackagesChanged -= OnActiveCoopPackageChanged;
                missionSettingsCoordinator.CoopPackageExported -= OnCoopPackagesExported;
                missionSettingsCoordinator.CoopSetupOpened -= OnCoopSetupOpened;
                missionSettingsCoordinator.CoopLaunchReceived -= OnCoopLaunchReceived;
                missionSettingsCoordinator.SinglePlayerCoopStarting -= PrepareSinglePlayerCoopStart;
                missionSettingsCoordinator.SinglePlayerCoopLaunchValidating -= CoopAiStartBlockReason;
                missionSettingsCoordinator.SinglePlayerCoopStartRejected -= OnSinglePlayerCoopStartRejected;
            }
            missionSettingsCoordinator?.ExitContext(force: true);
            mapSettingsCoordinator?.Dispose();
            if (mapSettingsCoordinator != null && lordDataCoordinator != null)
                mapSettingsCoordinator.MultiplayerSaveLaunchPreparing -= PrepareLordSaveWithDiagnostics;
            if (missionSettingsCoordinator != null && lordDataCoordinator != null)
                missionSettingsCoordinator.LobbyOpened -= lordDataCoordinator.OnLobbyOpened;
            lordDataCoordinator?.Dispose();
            missionSettingsCoordinator?.Dispose();
        }

        private void InitCoopMissionsHook(FRONT_Multiplayer self)
        {
            // Vanilla rewrites headers in-place even when its arrays already exist.
            // Restore our entries first; prepared templates must never enter that loop.
            RestoreVanillaMissions();
            initTrampoline(self);
            missionSettingsCoordinator.EnsureCoopCustomizeButtons();
            if (!activation.CoopPackages)
            {
                resolved.Clear();
                return;
            }
            try
            {
                ApplyActivePackage(refreshVisible: false);
            }
            catch (Exception ex)
            {
                LogError("Could not replace Coop missions: " + ex);
            }
        }

        private void CoopMissionChangedHook(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
        {
            try { CoopMissionChangedCore(self, trailId, missionId, resetOrderSwapped); }
            finally
            {
                // Normally ApplySelectedMission already republished; this covers selections without a package mission.
                if (hostLordRefreshDeferred && missionChangeDepth == 0)
                    RefreshHostLordData(self);
            }
        }

        private void CoopMissionChangedCore(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
        {
            if (HasActiveCoopPackage() &&
                !CoopTrailMissionRange.Contains(settings.ActiveCoopPackageMissionCount, trailId, missionId))
            {
                LogWarning("Rejected Coop mission selection outside the active custom package: Trail" +
                    (trailId + 1) + "/" + missionId + ".");
                return;
            }
            // The launch has already captured the edited lobby. A nested Vanilla mission
            // refresh would reset its selected Lords and other Customize settings.
            if (coopLaunchPending)
            {
                if (selected?.Loaded.TrailNumber == trailId + 1 &&
                    selected.Loaded.MissionNumber == missionId)
                    return;
                // A rejected Vanilla start can leave launch preparation pending. A
                // genuinely different mission selection ends that pending attempt.
                coopLaunchPending = false;
            }
            // View without a partner: Vanilla only updates the title and lobby progress there (CoopMissionChanged returns
            // while Show_CoopHostInvitePane is shown) and selects the mission again when the partner joins (Update_Coop).
            // Nothing is resolved or prepared for this selection; only the package mission's title is shown.
            if (HasActiveCoopPackage() && self?.currentLobby?.isHost == true &&
                GetExistingMainViewModel()?.Show_CoopHostInvitePane == true)
            {
                ShowCoopMissionWithoutPartner(self, trailId, missionId, resetOrderSwapped);
                return;
            }
            ResolvedMission next = null;
            if (HasActiveCoopPackage())
            {
                try { next = EnsureResolvedMission(trailId, missionId); }
                catch (Exception exception)
                {
                    RejectMissionSelection(trailId, missionId, exception);
                    return;
                }
            }
            missionSettingsCoordinator.ResetCustomizeForNewCoopSelection();
            // Resolve and install BEFORE Vanilla reads the selected table entry.
            selected = next;
            missionChangeDepth++;
            try { missionTrampoline(self, trailId, missionId, resetOrderSwapped); }
            finally { missionChangeDepth--; }
            PublishPackageMap(self);
            missionSettingsCoordinator.ObserveCoopLobby(self);
            missionSettingsCoordinator.EnsureCoopCustomizeButtons();
            if (!activation.CoopPackages)
                return;
            if (selected == null)
            {
                lordDataCoordinator.SetEmbeddedTrailSlots(null);
                missingMods = Array.Empty<string>();
                missionSettingsCoordinator.ExitContext();
                AppendPackageErrorToDescription(trailId, missionId);
                return;
            }

            try
            {
                ApplySelectedMission(self, true);
                RefreshSelectedLordRequirementStatus(self);
                ActivateSelectedMissionSettingsUnlessMap(
                    self,
                    editable: false,
                    source: "custom Coop mission selection");
            }
            catch (Exception ex)
            {
                selected = null;
                lordDataCoordinator.SetEmbeddedTrailSlots(null);
                missingMods = Array.Empty<string>();
                missionSettingsCoordinator.ExitContext();
                LogError("Could not activate replacement Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ": " + ex);
            }
        }

        private void ShowCoopMissionWithoutPartner(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
        {
            missionSettingsCoordinator.ResetCustomizeForNewCoopSelection();
            selected = null;
            missionChangeDepth++;
            try { missionTrampoline(self, trailId, missionId, resetOrderSwapped); }
            finally { missionChangeDepth--; }
            lordDataCoordinator.SetEmbeddedTrailSlots(null);
            missingMods = Array.Empty<string>();
            missionSettingsCoordinator.ExitContext();
            PublishPackageMap(self);
            missionSettingsCoordinator.ObserveCoopLobby(self);
            missionSettingsCoordinator.EnsureCoopCustomizeButtons();
            MainViewModel model = GetExistingMainViewModel();
            if (model != null && catalog.TryGet(trailId, missionId, out LoadedMission mission))
                model.CoopMissionTitle = mission.Definition.DisplayName;
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string command)
        {
            // Vanilla ignores every button while its leave confirmation is open.
            if (self?.closePanelDisplayed == true)
            {
                buttonTrampoline(self, command);
                return;
            }
            if (activation.CoopPackages && self?.singlePlayerCoop == true &&
                string.Equals(command, "COOP_START", StringComparison.Ordinal))
            {
                try { Shared.CoopAiStartPlanFactory.PrepareDirectPartner(self.currentLobby, self.AIVs); }
                catch (Shared.CoopAiSelectionException failure)
                {
                    LogError(CoopLaunchDiagnostic(self) + ": " + failure);
                    BlockLaunch(command, LocalizeCoopSelectionFailure(failure));
                    return;
                }
            }
            // A failed validation must never trap a player in the ready/locked state.
            if (string.Equals(command, "Ready", StringComparison.Ordinal) && ReadLobbyFlag(self, LocalReadyField) ||
                string.Equals(command, "ReadyLock", StringComparison.Ordinal) &&
                ReadLobbyFlag(self, LocalReadyLockedField))
            {
                buttonTrampoline(self, command);
                return;
            }
            if (IsLaunchCommand(command) && !missionSettingsCoordinator.TryPrepareRestartSettings(
                reason => BlockLaunch(command, reason)))
                return;
            if (activation.CoopPackages && IsLaunchCommand(command) && CurrentSlotOutsidePackage(self))
            {
                BlockLaunch(command, CustomCoopTrailEndNotice(self));
                return;
            }
            if (activation.CoopPackages && selected != null && IsLaunchCommand(command) && CurrentSlotRequiresPackage(self))
                RefreshSelectedLordRequirementStatus(self);
            if (IsStartCommand(command))
                lordDataCoordinator?.LogStartAttempt(command, activation.CoopPackages, self);
            if (activation.LordDataIntegration && IsStartCommand(command) && self?.currentLobby != null &&
                self.currentLobby.isHost && !self.singlePlayerCoop &&
                !FRONT_Multiplayer.skirmishGame &&
                (selected?.Loaded.LordRequirements != null ||
                    lordDataCoordinator.RequiresLordSyncFromSelection(self)))
            {
                if (selected?.Loaded.LordRequirements != null &&
                    !PrepareSelectedLordRequirements(out string preparationReason))
                {
                    BlockLaunch(command, preparationReason);
                    return;
                }
                bool inspected = lordDataCoordinator.RefreshPackageManifest(self, "start-attempt");
                if (!inspected)
                {
                    BlockLaunch(command, lordDataCoordinator.CurrentBlockReason);
                    return;
                }
                if (lordDataCoordinator.NeedsLordStartGate)
                {
                    bool captured = lordDataCoordinator.IsUsingLocalValues ||
                        lordDataCoordinator.RefreshHost(self, "start-attempt");
                    bool ready = lordDataCoordinator.IsReadyToLaunch(self, out string lordDataReason);
                    if (!ready && selected?.Loaded.LordRequirements == null &&
                        lordDataCoordinator.TryUseLocalValues(self))
                    {
                        captured = true;
                        ready = lordDataCoordinator.IsReadyToLaunch(self, out lordDataReason);
                    }
                    lordDataCoordinator.LogStartDecision(captured, ready, lordDataReason);
                    if (!captured || !ready)
                    {
                        BlockLaunch(command, string.IsNullOrEmpty(lordDataReason)
                            ? "Selected Lord data is not synchronized yet." : lordDataReason,
                            lordDataCoordinator.LastSlotSelectionDiagnostic);
                        return;
                    }
                }
                if (selected?.Loaded.LordRequirements != null &&
                    !lordDataCoordinator.IsTrailSelectionManifestReady(self, out string manifestReason))
                {
                    BlockLaunch(command, manifestReason);
                    return;
                }
            }
            if (string.Equals(command, "TMTest", StringComparison.Ordinal) &&
                !missionSettingsCoordinator.PrepareTrailMakerTestLaunch())
            {
                return;
            }
            if (activation.CoopPackages && IsLaunchCommand(command) && CurrentSlotRequiresPackage(self))
            {
                string featureError = SelectedFeatureDependencyError();
                if (featureError != null)
                {
                    BlockLaunch(command, featureError);
                    return;
                }
                if (selected?.Loaded.Definition.Map.Source == "bundled" && !sharedMapLookupRegistered)
                {
                    BlockLaunch(command, SerpLocalization.Get("ExtendedData.MapServiceUnavailable"));
                    return;
                }
                if (!string.IsNullOrEmpty(mapLookupFailure))
                {
                    BlockLaunch(command, mapLookupFailure);
                    return;
                }
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
                if (!IsStartCommand(command) &&
                    !lordDataCoordinator.IsLocalSelectionReady(
                        CurrentLordInfoMap(self), out string localLordReason))
                {
                    BlockLaunch(command, localLordReason);
                    return;
                }
                if (IsStartCommand(command) && !self.singlePlayerCoop && self.currentLobby != null && self.currentLobby.isHost)
                {
                    List<HumanPackageState> participantStates = GetHumanPackageStates(self);
                    Shared.DevDiag.Log(() => "Custom Coop package participant audit: " +
                            DescribeHumanPackageStates(self, participantStates));
                    if (!settings.System_ArePerPlayerSettingsReady(
                            participantStates.Select(state => state.PlayerId),
                            out string syncError))
                    {
                        BlockLaunch(command, SerpLocalization.Get("ExtendedData.ErrorPackageReportsPending"),
                            "Shared personal settings incomplete: " + syncError + "; " +
                            DescribeHumanPackageStates(self, participantStates));
                        return;
                    }
                    if (!AreAllHumanPlayersPackageReady(participantStates))
                    {
                        BlockLaunch(command, GetParticipantPackageBlockReason(participantStates),
                            DescribeHumanPackageStates(self, participantStates));
                        return;
                    }
                }
            }
            if (activation.CoopPackages && selected != null && IsLaunchCommand(command))
            {
                try
                {
                    if (IsStartCommand(command))
                    {
                        // Local starts also enter here through restart/continuation paths. Their final
                        // shared hook validates after preparation, immediately before Vanilla starts.
                        string aiStartReason = self.singlePlayerCoop ? null : CoopAiStartBlockReason(self);
                        if (aiStartReason != null)
                        {
                            BlockLaunch(command, aiStartReason);
                            return;
                        }
                        if (!PrepareSelectedLordRequirements(out string lordReason,
                            captureLocalReplacements: self.singlePlayerCoop))
                        {
                            BlockLaunch(command, lordReason);
                            return;
                        }
                        ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(
                            selected.Loaded.TrailNumber - 1,
                            selected.Loaded.MissionNumber);
                    }
                    ActivateSelectedMissionSettingsUnlessMap(
                        self,
                        editable: false,
                        source: "custom Coop mission " + command,
                        starting: IsStartCommand(command) &&
                            (!string.Equals(command, "COOP_START", StringComparison.Ordinal) ||
                             self.singlePlayerCoop || self.currentLobby?.CountHumanPlayers() == 2));
                    if (IsStartCommand(command) &&
                        (!string.Equals(command, "COOP_START", StringComparison.Ordinal) ||
                         self.singlePlayerCoop || self.currentLobby?.CountHumanPlayers() == 2))
                    {
                        if (!missionSettingsCoordinator.TryPrepareRestartSettings(
                            reason => BlockLaunch(command, reason))) return;
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
                    missionSettingsCoordinator.RecoverCoopDraft();
                    string reason = SerpLocalization.Get("ExtendedData.ErrorCoopPreparationFailed");
                    BlockLaunch(command, reason, "Launch preparation failed: " + exception);
                    return;
                }
            }
            try
            {
                buttonTrampoline(self, command);
                lastAttemptFailure = string.Empty;
                ReportUnacceptedReady(self, command);
                QueueLaunchReadiness();
                if (missionSettingsCoordinator.IsCustomizeDraftEditable) coopLaunchPending = false;
            }
            catch
            {
                coopLaunchPending = false;
                missionSettingsCoordinator.RecoverCoopDraft();
                throw;
            }
        }

        private void UpdateHostInfoHook(FRONT_Multiplayer self, bool delayed)
        {
            updateHostInfoTrampoline(self, delayed);
            if (!activation.CoopPackages || delayed)
                return;
            if (missionChangeDepth > 0)
            {
                hostLordRefreshDeferred = true;
                return;
            }
            RefreshHostLordData(self);
        }

        private void RefreshHostLordData(FRONT_Multiplayer self)
        {
            hostLordRefreshDeferred = false;
            if (!activation.CoopPackages)
                return;
            if (selected != null && CurrentSlotRequiresPackage(self))
            {
                RefreshSelectedLordRequirementStatus(self);
                PrepareHostTrailLordData(self);
            }
            lordDataCoordinator?.RefreshPackageManifest(self, "host-selection-update");
            if (lordDataCoordinator?.IsUsingLocalValues != true)
                lordDataCoordinator?.RefreshHost(self, "host-selection-update");
        }

        public void RefreshPackageCatalog()
        {
            if (refreshingCatalog)
                return;
            refreshingCatalog = true;
            try
            {
                var roots = new List<string> { customTrailsRoot };
                roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(message => LogWarning(message)));
                long started = 0;
                Shared.DevDiag.Start(ref started);
                try
                {
                    packageOverviews = packageCatalog.Discover(roots, message => LogError(message));
                    settings.RefreshPackageOverviews(packageOverviews);
                }
                finally { Shared.DevDiag.StopSpike(ref started, "coop.discovery", 10000); }
            }
            finally
            {
                refreshingCatalog = false;
            }
        }

        private void LogWarning(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) =>
            Shared.DebugLogHelper.LogWarningLimited(log, callerFile + ":" + callerLine, message);

        private void OnActiveCoopPackageChanged()
        {
            if (updatingPackage)
                return;
            updatingPackage = true;
            try
            {
                RefreshPackageCatalog();
                ApplyActivePackage();
            }
            finally
            {
                updatingPackage = false;
            }
        }

        private void OnCoopSetupOpened()
        {
            if (!activation.CoopPackages || selected == null)
                return;
            try
            {
                ActivateSelectedMissionSettingsUnlessMap(
                    GetExistingMainViewModel()?.FRONTMultiplayer,
                    editable: true,
                    source: "custom Coop mission setup");
                MainViewModel.Instance.StandaloneMissionText = BuildMissionDescription();
                {
                    Shared.DevDiag.Log(() => "Refreshed custom Coop mission mod-settings context after opening the setup screen.");
                }
            }
            catch (Exception exception)
            {
                LogError("Could not reactivate custom Coop mission mod settings after opening setup: " + exception);
            }
        }

        private void OnCoopPackageRemoteStatusChanged()
        {
            if (GameNetworkAPI.IsLocalHost() || !activation.CoopPackages)
                return;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
                if (selected != null && CurrentSlotRequiresPackage(self))
                    RefreshSelectedLordRequirementStatus(self);
            });
        }

        private void OnLordPackageManifestChangedForSelection(string _)
        {
            // A manifest can arrive after the host's package status. Re-evaluate the
            // embedded/replaced slot split as soon as that manifest is accepted.
            OnCoopPackageRemoteStatusChanged();
        }

        private void ApplyActivePackage(bool refreshVisible = true)
        {
            bool previousUpdating = updatingPackage;
            updatingPackage = true;
            try { ApplyActivePackageCore(refreshVisible); }
            finally { updatingPackage = previousUpdating; }
        }

        private void ApplyActivePackageCore(bool refreshVisible)
        {
            if (!activation.CoopPackages || string.IsNullOrEmpty(settings.ActiveCoopPackageId))
            {
                bool changed = activePackage != null || selected != null;
                ClearPackageData();
                ClearHostPackageDescriptor();
                missionSettingsCoordinator?.SetCoopPackagePresentation(null, 0);
                localPackageError = string.Empty;
                if (!activation.CoopPackages)
                    SetLocalPackageError(ExtendedDataSettingsViewModel.DisabledStatus, SerpLocalization.Get("ExtendedData.ErrorModDisabled"));
                else SetLocalPackageStatus("OK|VANILLA");
                if (changed && refreshVisible) RefreshVisibleCoopMissionAfterPackageChange();
                return;
            }
            if (!GameNetworkAPI.IsLocalHost() &&
                (!string.Equals(settings.ActiveCoopPackageDescriptor, ExpectedPackageDescriptor(), StringComparison.Ordinal) ||
                 string.IsNullOrEmpty(settings.ActiveCoopPackageFingerprint)))
            {
                ClearPackageData();
                missionSettingsCoordinator?.SetCoopPackagePresentation(settings.ActiveCoopPackageId, settings.ActiveCoopPackageMissionCount);
                SetLocalPackageError(ExtendedDataSettingsViewModel.WaitingStatus, SerpLocalization.Get("ExtendedData.StatusChecking"));
                return;
            }
            try
            {
                CoopTrailPackage next = LoadSelectedPackage();
                if (GameNetworkAPI.IsLocalHost())
                {
                    settings.ActiveCoopPackageFingerprint = next.Manifest.ContentFingerprint;
                    settings.ActiveCoopPackageMissionCount = next.Manifest.MissionCount;
                    settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
                }
                if (!string.Equals(next.Manifest.ContentFingerprint, settings.ActiveCoopPackageFingerprint, StringComparison.OrdinalIgnoreCase) ||
                    next.Manifest.MissionCount != settings.ActiveCoopPackageMissionCount)
                {
                    string reason = SerpLocalization.Get("ExtendedData.ErrorFingerprintMismatch");
                    CoopLaunchBlockDiagnostics.ReportLocal(log, "PackageCheck", reason, DescribeLaunchBlockContext(
                        GetExistingMainViewModel()?.FRONTMultiplayer), "localFingerprint=" + next.Manifest.ContentFingerprint +
                        "; localMissionCount=" + next.Manifest.MissionCount + "; hostMissionCount=" + settings.ActiveCoopPackageMissionCount);
                    ClearPackageData();
                    missionSettingsCoordinator?.SetCoopPackagePresentation(settings.ActiveCoopPackageId, settings.ActiveCoopPackageMissionCount);
                    SetLocalPackageError(ExtendedDataSettingsViewModel.MismatchStatus, reason);
                    ShowLocalPackageBlockAfterSync();
                    return;
                }

                // Identical host/preset notifications and menu reopening must not discard
                // the selected mission or a user's editable Customize draft.
                if (ReferenceEquals(activePackage, next)) return;
                ClearPackageData(clearSession: false);
                activePackage = next;
                // Per-mission load lines are development diagnostics (build.bat /devdiag only).
#if SERP_DEVDIAG
                catalog.Load(next, message => Shared.DevDiag.Log(() => message), message => LogError(message));
#else
                catalog.Load(next, null, message => LogError(message));
#endif
                missionSettingsCoordinator?.SetCoopPackagePresentation(next.Manifest.DisplayName, next.Manifest.MissionCount);
                localPackageError = string.Empty;
                SetLocalPackageStatus(ExpectedReadyStatus());
                if (refreshVisible) RefreshVisibleCoopMissionAfterPackageChange();
            }
            catch (Exception exception)
            {
                ClearPackageData();
                ClearHostPackageDescriptor();
                missionSettingsCoordinator?.SetCoopPackagePresentation(settings.ActiveCoopPackageId, settings.ActiveCoopPackageMissionCount);
                SetLocalPackageError(
                    exception is FileNotFoundException ? ExtendedDataSettingsViewModel.MissingStatus :
                        ExtendedDataSettingsViewModel.InvalidStatusPrefix + exception.Message,
                    CoopPackageBlockMessages.LocalLoadFailure(exception is FileNotFoundException, settings.ActiveCoopPackageId));
                LogError("Selected Coop Trail package is unusable: " + exception);
                ShowLocalPackageBlockAfterSync();
            }
        }

        private void ClearHostPackageDescriptor()
        {
            if (!activation.CoopPackages || !GameNetworkAPI.IsLocalHost()) return;
            settings.ActiveCoopPackageFingerprint = string.Empty;
            settings.ActiveCoopPackageMissionCount = 0;
            settings.ActiveCoopPackageDescriptor = ExpectedPackageDescriptor();
        }

        private void ClearPackageData(bool clearSession = true)
        {
            RestoreVanillaMissions();
            resolved.Clear();
            catalog.Load(null, null, null);
            activePackage = null;
            selected = null;
            missingMods = Array.Empty<string>();
            lordDataCoordinator?.SetEmbeddedTrailSlots(null);
            if (clearSession) packageSession.Clear();
        }

        private void RestoreVanillaMissions() => missionTables.Restore();

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
                self.AIVs[entry.Key] = CopyLordInfo(entry.Value);
                setupData.preferredAIVs[entry.Key] = selected.PreferredAivByPlayerIndex[entry.Key];
                // Vanilla creates Coop AI lobby members without their custom Lord name.
                // The name is needed by both the lobby display and the start snapshot.
                Platform_Multiplayer.MPLobbyMember aiMember = self.currentLobby?.GetLobbyMemberFromThis_PlayerID(entry.Key + 1);
                if (aiMember != null && !entry.Value.builtInLord)
                    aiMember.customLordName = entry.Value.lordName ?? string.Empty;
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
            ReSortTeamInfoMethod.Invoke(self, null);
            UpdateRadarShieldPositionsMethod.Invoke(self, null);
        }

        private void UpdateCustomLordNamesHook(FRONT_Multiplayer self)
        {
            updateCustomLordNamesTrampoline(self);
            if (!activation.CoopPackages || selected == null || self?.currentLobby == null)
                return;
            bool refresh = false;
            foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    continue;
                int playerId = self.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 2 || playerId > 8)
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = DecodeLobbyLord(self, playerId);
                if (info == null || info.builtInLord || info.lordType != member.GetLordType() ||
                    string.IsNullOrWhiteSpace(info.lordName))
                    continue;
                if (!string.Equals(member.customLordName, info.lordName, StringComparison.Ordinal))
                    member.customLordName = info.lordName;
                refresh = true;
            }
            if (refresh)
                ReSortTeamInfoMethod.Invoke(self, null);
        }

        private string CombinedNameHook(Platform_Multiplayer.MPLobbyMember member)
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (!activation.CoopPackages || selected == null || self?.currentLobby?.members == null ||
                !self.currentLobby.coopTrailGame || !self.currentLobby.members.Contains(member))
                return combinedNameTrampoline(member);

            int playerId = self.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            bool isCurrentSlotMember = playerId >= 2 && playerId <= 8 &&
                ReferenceEquals(self.currentLobby.GetLobbyMemberFromThis_PlayerID(playerId), member);
            string name = CoopTrailPreviewNamePolicy.Resolve(
                selected.Loaded.TrailNumber == self.currentLobby.coopTrailID + 1 &&
                selected.Loaded.MissionNumber == self.currentLobby.coopSelectedMission &&
                isCurrentSlotMember,
                member.SkirmishMember && !member.SkirmishHumanMember,
                playerId,
                member.GetLordType(),
                member.customLordName);
            if (name != null)
                return MapFileManager.SplitCustomTrailName(name);
            if (member.SkirmishMember && !member.SkirmishHumanMember &&
                member.GetLordType() >= 29 && member.GetLordType() <= 37 &&
                playerId >= 2 && playerId <= 8)
            {
                // Expected while a mission switch or the host's lobby data is still converging (client log
                // 2026-10-26 21:28); the Vanilla name is the intended fallback, so this is development diagnostics.
                Shared.DevDiag.Log(() => "Custom Coop Trail preview name unavailable for slot " + playerId +
                    ": selected lobby Lord identity is missing or does not match the member (" +
                    self.currentLobby.coopTrailID + "/" + self.currentLobby.coopSelectedMission + "/" +
                    member.GetLordType() + "/" + member.customLordName + ").");
            }
            return combinedNameTrampoline(member);
        }

        private static MethodInfo RequirePreviewUpdate(Type rowType) =>
            rowType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(FRONT_Multiplayer), typeof(Platform_Multiplayer.MPLobbyMember),
                    typeof(int), typeof(int) }, null)
            ?? throw new MissingMethodException(rowType.FullName, "Update");

        private bool IsSelectedCustomCoopPreview(FRONT_Multiplayer parent) =>
            activation.CoopPackages && selected != null && parent?.currentLobby?.coopTrailGame == true &&
            selected.Loaded.TrailNumber == parent.currentLobby.coopTrailID + 1 &&
            selected.Loaded.MissionNumber == parent.currentLobby.coopSelectedMission;

        private void CoopTrail1RowHook(FRONT_CoopTrail1.PlayerRow self, FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member, int row, int player)
        {
            coopTrail1RowTrampoline(self, parent, member, row, player);
            try
            {
                FRONT_CoopTrail1.PlayerRow[] players = FRONT_CoopTrail1.Instance?.playerRows;
                if (players == null || players.Length != 8) return;
                if (coopTrail1Preview == null || !ReferenceEquals(coopTrail1Preview.FirstRow, players[0].RefRow))
                    coopTrail1Preview = new CoopPreviewTeams(players.Select(item => item.RefRow).ToArray());
                coopTrail1Preview.Record(parent, member, row, IsSelectedCustomCoopPreview(parent), message => LogWarning(message));
            }
            catch (Exception ex) { LogPreviewFailure(1, ex); }
        }

        private void CoopTrail2RowHook(FRONT_CoopTrail2.PlayerRow self, FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member, int row, int player)
        {
            coopTrail2RowTrampoline(self, parent, member, row, player);
            try
            {
                FRONT_CoopTrail2.PlayerRow[] players = FRONT_CoopTrail2.Instance?.playerRows;
                if (players == null || players.Length != 8) return;
                if (coopTrail2Preview == null || !ReferenceEquals(coopTrail2Preview.FirstRow, players[0].RefRow))
                    coopTrail2Preview = new CoopPreviewTeams(players.Select(item => item.RefRow).ToArray());
                coopTrail2Preview.Record(parent, member, row, IsSelectedCustomCoopPreview(parent), message => LogWarning(message));
            }
            catch (Exception ex) { LogPreviewFailure(2, ex); }
        }

        private void CoopTrail3RowHook(FRONT_CoopTrail3.PlayerRow self, FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member, int row, int player)
        {
            coopTrail3RowTrampoline(self, parent, member, row, player);
            try
            {
                FRONT_CoopTrail3.PlayerRow[] players = FRONT_CoopTrail3.Instance?.playerRows;
                if (players == null || players.Length != 8) return;
                if (coopTrail3Preview == null || !ReferenceEquals(coopTrail3Preview.FirstRow, players[0].RefRow))
                    coopTrail3Preview = new CoopPreviewTeams(players.Select(item => item.RefRow).ToArray());
                coopTrail3Preview.Record(parent, member, row, IsSelectedCustomCoopPreview(parent), message => LogWarning(message));
            }
            catch (Exception ex) { LogPreviewFailure(3, ex); }
        }

        private void CoopTrail4RowHook(FRONT_CoopTrail4.PlayerRow self, FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member, int row, int player)
        {
            coopTrail4RowTrampoline(self, parent, member, row, player);
            try
            {
                FRONT_CoopTrail4.PlayerRow[] players = FRONT_CoopTrail4.Instance?.playerRows;
                if (players == null || players.Length != 8) return;
                if (coopTrail4Preview == null || !ReferenceEquals(coopTrail4Preview.FirstRow, players[0].RefRow))
                    coopTrail4Preview = new CoopPreviewTeams(players.Select(item => item.RefRow).ToArray());
                coopTrail4Preview.Record(parent, member, row, IsSelectedCustomCoopPreview(parent), message => LogWarning(message));
            }
            catch (Exception ex) { LogPreviewFailure(4, ex); }
        }

        private readonly HashSet<int> previewTeamFailures = new HashSet<int>();
        private void LogPreviewFailure(int page, Exception ex)
        {
            switch (page)
            {
                case 1: coopTrail1Preview?.FailOpen(); break;
                case 2: coopTrail2Preview?.FailOpen(); break;
                case 3: coopTrail3Preview?.FailOpen(); break;
                case 4: coopTrail4Preview?.FailOpen(); break;
            }
            if (previewTeamFailures.Add(page))
                LogWarning("Custom Coop Trail team preview kept Vanilla layout on page " + page + ": " + ex);
        }

        private static FRONT_Multiplayer.MPAIVInfo CopyLordInfo(FRONT_Multiplayer.MPAIVInfo source) =>
            CoopMissionDataCopies.CopyLord(source);

        private Dictionary<int, FRONT_Multiplayer.MPAIVInfo> CurrentLordInfoMap(FRONT_Multiplayer self)
        {
            var infos = self?.AIVs?.Take(8).Select((info, index) => new { info, index })
                .Where(item => item.info != null)
                .ToDictionary(item => item.index + 1, item => item.info) ??
                new Dictionary<int, FRONT_Multiplayer.MPAIVInfo>();
            if (self?.currentLobby == null || self.currentLobby.isHost || self.singlePlayerCoop)
                return infos;
            foreach (int playerId in Enumerable.Range(2, 7).Where(id => IsActiveAiSlot(self, id)))
            {
                FRONT_Multiplayer.MPAIVInfo transmitted = DecodeLobbyLord(self, playerId);
                if (transmitted == null)
                    continue;
                if (transmitted.builtInLord || lordDataCoordinator.IsManifestReplacementSlot(playerId))
                    infos[playerId] = transmitted;
                else if (infos.TryGetValue(playerId, out FRONT_Multiplayer.MPAIVInfo local))
                {
                    // Vanilla publishes the chosen configuration, while the bundled local
                    // entry still holds the complete AIV list and the media name to remap.
                    FRONT_Multiplayer.MPAIVInfo selectedInfo = CopyLordInfo(local);
                    selectedInfo.lordConfig = transmitted.lordConfig;
                    infos[playerId] = selectedInfo;
                }
            }
            return infos;
        }

        private static FRONT_Multiplayer.MPAIVInfo DecodeLobbyLord(FRONT_Multiplayer self, int playerId)
        {
            if (self?.currentLobby == null)
                return null;
            string data;
            switch (playerId)
            {
                case 2: data = self.currentLobby.AIVDataPlayer2; break;
                case 3: data = self.currentLobby.AIVDataPlayer3; break;
                case 4: data = self.currentLobby.AIVDataPlayer4; break;
                case 5: data = self.currentLobby.AIVDataPlayer5; break;
                case 6: data = self.currentLobby.AIVDataPlayer6; break;
                case 7: data = self.currentLobby.AIVDataPlayer7; break;
                case 8: data = self.currentLobby.AIVDataPlayer8; break;
                default: return null;
            }
            if (string.IsNullOrWhiteSpace(data))
                return null;
            var info = new FRONT_Multiplayer.MPAIVInfo();
            info.decode(data);
            return info;
        }

        private HashSet<int> EmbeddedLordSlots(FRONT_Multiplayer self)
        {
            var matched = new HashSet<int>();
            if (selected?.Loaded.LordRequirements == null || self?.currentLobby == null)
                return matched;
            foreach (TrailLordSlot slot in selected.Loaded.LordRequirements.Slots)
            {
                if (self.AIVs == null || slot.PlayerId > self.AIVs.Length ||
                    !IsActiveAiSlot(self, slot.PlayerId))
                    continue;
                FRONT_Multiplayer.MPAIVInfo info = self.AIVs[slot.PlayerId - 1];
                if (!self.currentLobby.isHost && !self.singlePlayerCoop)
                {
                    FRONT_Multiplayer.MPAIVInfo transmitted = DecodeLobbyLord(self, slot.PlayerId);
                    if (transmitted?.builtInLord == true ||
                        lordDataCoordinator.IsManifestReplacementSlot(slot.PlayerId))
                        continue;
                }
                // The same Lord name with another configuration is a replacement: its
                // values come from the host snapshot, never from this mission's stored slot.
                if (info?.lordConfig != null && TrailLordSelectionPolicy.UsesEmbeddedLord(slot,
                    info.builtInLord, info.lordName,
                    lordDataCoordinator.PreparedTrailMediaAlias(selected.Loaded.LordRequirements,
                        slot.PlayerId), info.lordType, info.lordConfig.lordType,
                    info.lordConfig.name, info.lordConfig.checksum.ToString()))
                    matched.Add(slot.PlayerId);
            }
            return matched;
        }

        private void RefreshSelectedLordRequirementStatus(FRONT_Multiplayer self)
        {
            lordDataCoordinator.SetEmbeddedTrailSlots(selected?.Loaded.LordRequirements == null
                ? null : EmbeddedLordSlots(self));
            if (selected?.Loaded.LordRequirements == null)
            {
                localPackageError = string.Empty;
                SetLocalPackageStatus(ExpectedReadyStatus());
                return;
            }
            if (lordDataCoordinator.CanSatisfyTrail(selected.Loaded.LordRequirements,
                    CurrentLordInfoMap(self), out string reason, out bool selectionPending))
            {
                localPackageError = string.Empty;
                SetLocalPackageStatus(ExpectedReadyStatus());
            }
            // A client compares the new mission with lobby Lord data the host has not republished yet
            // (client log 2026-10-26 21:28:38). Report it as pending; the host's update re-evaluates it and
            // APIShared escalates a state that persists. The Start checks remain complete.
            else if (selectionPending && self?.currentLobby?.isHost == false && !self.singlePlayerCoop)
            {
                localPackageError = string.Empty;
                SetLocalPackageStatus(ExtendedDataSettingsViewModel.WaitingStatus);
            }
            else
                SetLocalPackageError(ExtendedDataSettingsViewModel.InvalidStatusPrefix + reason, reason);
        }

        // Publishes the selected Trail's embedded Lord values as soon as the host lobby has a partner, so clients
        // acknowledge them before Start. Without this the first Start click published them and was rejected
        // while the acknowledgement was in flight (DEVDIAG lobby log 2026-10-26 19:15, source=trail-fixes).
        // This includes the Lord media names: before, the first Start click published them and was rejected with
        // "Lord media selection changed; ... start again". The click repeats the preparation; an unchanged digest
        // and unchanged lobby names keep the acknowledgements.
        private void PrepareHostTrailLordData(FRONT_Multiplayer self)
        {
            if (!activation.CoopPackages || !activation.LordDataIntegration || coopLaunchPending ||
                preparingHostTrailLordData || selected?.Loaded.LordRequirements == null ||
                self?.currentLobby?.isHost != true || self.singlePlayerCoop ||
                self.currentLobby.CountHumanPlayers() < 2 || !CurrentSlotRequiresPackage(self))
                return;
            // Publishing changed media names calls UpdateHostInfo, which re-enters through RefreshHostLordData.
            preparingHostTrailLordData = true;
            try
            {
                // A false result only means that acknowledgements or the media publication are pending, or the
                // selection is not satisfiable; the status provider and the Start click report those reasons.
                PrepareSelectedLordRequirements(out _);
            }
            finally { preparingHostTrailLordData = false; }
        }

        private bool PrepareSelectedLordRequirements(out string reason, bool captureLocalReplacements = false)
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            if (!lordDataCoordinator.PrepareTrail(selected?.Loaded.LordRequirements,
                CurrentLordInfoMap(self), true, out reason, captureLocalReplacements))
                return false;
            if (selected != null && selected.Loaded.LordRequirements == null)
                lordDataCoordinator.PrepareLegacyTrailPresentation(CurrentLordInfoMap(self), self?.singlePlayerCoop != true);
            if (self?.currentLobby != null && !self.currentLobby.isHost && !self.singlePlayerCoop)
            {
                var localInfos = self.AIVs?.Take(8).Select((info, index) => new { info, index })
                    .Where(item => item.info != null)
                    .ToDictionary(item => item.index + 1, item => item.info);
                lordDataCoordinator.RemapTrailMedia(localInfos);
            }
            if (self?.currentLobby?.isHost == true && !self.singlePlayerCoop &&
                selected?.Loaded.LordRequirements != null &&
                selected.Loaded.LordRequirements.Slots.Any(slot =>
                    IsActiveAiSlot(self, slot.PlayerId) && self.AIVs != null &&
                    slot.PlayerId <= self.AIVs.Length &&
                    !string.Equals(self.AIVs[slot.PlayerId - 1]?.lordName,
                        DecodeLobbyLord(self, slot.PlayerId)?.lordName, StringComparison.Ordinal)))
            {
                // SE media names can differ from the author-facing embedded name.
                // Publish the chosen provider through Vanilla before either player starts.
                UpdateHostInfoMethod.Invoke(self, new object[] { false });
                reason = "Lord media selection changed; wait for the updated lobby selection to be confirmed, then start again.";
                return false;
            }
            return true;
        }

        private string SelectedFeatureDependencyError()
        {
            if (!activation.MissionModSettings && selected?.Loaded.Definition.ModSettings?.Mods?.Count > 0)
                return SerpLocalization.Get("ExtendedData.MissionSettingsRequired");
            if (!activation.LordDataIntegration && selected?.Loaded.LordRequirements != null)
                return SerpLocalization.Get("ExtendedData.LordIntegrationRequired");
            return null;
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

        private bool HasActiveCoopPackage() =>
            activation.CoopPackages && !string.IsNullOrEmpty(settings.ActiveCoopPackageId);

        private bool CurrentSlotOutsidePackage(FRONT_Multiplayer self) =>
            HasActiveCoopPackage() && self?.currentLobby?.coopTrailGame == true &&
            !CoopTrailMissionRange.Contains(settings.ActiveCoopPackageMissionCount,
                self.currentLobby.coopTrailID, self.currentLobby.coopSelectedMission);

        private string CustomCoopTrailEndNotice(FRONT_Multiplayer self) =>
            settings.ActiveCoopPackageMissionCount >= 1 && settings.ActiveCoopPackageMissionCount <= 40
                ? SerpLocalization.Get("ExtendedData.CustomCoopTrailEndNotice")
                    .Replace("{Count}", settings.ActiveCoopPackageMissionCount.ToString())
                : GetLocalBlockReason();

        private bool IsLocalPackageReady() =>
            string.Equals(settings.CoopPackageStatus, ExpectedReadyStatus(), StringComparison.Ordinal);

        private string ExpectedReadyStatus() =>
            "OK|" + settings.ActiveCoopPackageId + "|" + settings.ActiveCoopPackageFingerprint +
            (selected?.Loaded.LordRequirements == null ? string.Empty :
                "|" + selected.Loaded.TrailNumber + "/" + selected.Loaded.MissionNumber +
                "/" + selected.Loaded.LordRequirements.MissionDigest + "/" +
                LordSelectionDigest(GetExistingMainViewModel()?.FRONTMultiplayer));

        private static string LordSelectionDigest(FRONT_Multiplayer self)
        {
            if (self?.currentLobby?.members == null)
                return "UNAVAILABLE";
            string slots = string.Join(",", Enumerable.Range(2, 7)
                .Where(playerId => IsActiveAiSlot(self, playerId)));
            return LordDataSyncDiagnostics.Hash(slots + ":" +
                self.currentLobby.AIVDataChecksum());
        }

        private static bool IsActiveAiSlot(FRONT_Multiplayer self, int playerId) =>
            self?.currentLobby?.members?.Any(member => member != null &&
                !member.dummyToBeKicked && member.SkirmishMember &&
                !member.SkirmishHumanMember &&
                self.currentLobby.getThisPlayerFromSteamID(member.id.m_SteamID) == playerId) == true;

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
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/ExtendedDataRuntime.cs:1370", $"Custom Coop package player identity mismatch; launch remains blocked until the roster converges: {diagnostic}");
            }
        }

        private bool AreAllHumanPlayersPackageReady(IReadOnlyCollection<HumanPackageState> states) =>
            states.Count > 0 && states.All(state =>
                state.PlayerId > 0 &&
                string.Equals(state.Status, ExpectedReadyStatus(), StringComparison.Ordinal));

        private string GetParticipantPackageBlockReason(IReadOnlyCollection<HumanPackageState> states) =>
            CoopPackageBlockMessages.Participants(states.Select(state => new KeyValuePair<string, string>(
                state.Name, state.PlayerId > 0 ? state.Status : null)), ExpectedReadyStatus());

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
                ", status=" + DescribePackageStatus(state.Status, expected) +
                ", reported=" + state.Status + "]"));
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
            if (self?.currentLobby == null || !self.currentLobby.coopTrailGame ||
                !ReferenceEquals(Platform_Multiplayer.Instance?.activeLobby, self.currentLobby))
                return;
            int trailId = self.currentLobby.coopTrailID;
            int missionId = self.currentLobby.coopSelectedMission;
            if (trailId < 0 || trailId >= CoopTrailFields.Length || missionId < 1 || missionId > 10)
                return;

            if (CurrentSlotOutsidePackage(self))
            {
                int available = CoopTrailMissionRange.CountOnPage(
                    settings.ActiveCoopPackageMissionCount, trailId);
                if (self.currentLobby.isHost && IsLocalPackageReady())
                {
                    FrontendMenus menus = GetExistingMainViewModel()?.FrontEndMenu;
                    if (available == 0)
                    {
                        int lastTrail = (settings.ActiveCoopPackageMissionCount - 1) / 10;
                        int lastMission = settings.ActiveCoopPackageMissionCount - lastTrail * 10;
                        MainViewModel viewModel = GetExistingMainViewModel();
                        if (menus != null && viewModel != null)
                        {
                            FrontendMenus.CurrentSelectedTrail = lastTrail + 21;
                            viewModel.Show_CoopTrail1 = lastTrail == 0;
                            viewModel.Show_CoopTrail2 = lastTrail == 1;
                            viewModel.Show_CoopTrail3 = lastTrail == 2;
                            viewModel.Show_CoopTrail4 = lastTrail == 3;
                            menus.GenerateSwords();
                            menus.ButtonTrailCampaignClicked(lastMission);
                        }
                        self.CoopMissionChanged(lastTrail, lastMission, false);
                    }
                    else if (menus != null && FrontendMenus.CurrentSelectedTrail == trailId + 21)
                        menus.ButtonTrailCampaignClicked(available, true);
                    else
                        self.CoopMissionChanged(trailId, available, false);
                }
                return;
            }

            // Host package settings can arrive after AutoJoinLobby selected Vanilla data.
            // Re-run the same Vanilla selection path so map, AIs, title and Trail preset agree.
            self.CoopMissionChanged(trailId, missionId, false);
            Shared.DevDiag.Log(() => "Refreshed visible Coop mission after package settings changed: Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ".");
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

        internal static MainViewModel GetExistingMainViewModel() =>
            MainViewModelInstanceField?.GetValue(null) as MainViewModel;

        private void BlockLaunch(string command, string reason, string detail = null)
            => ReportLaunchFailure(command, reason, detail, showPopup: true);

        private void ReportLaunchFailure(string command, string reason, string detail, bool showPopup)
        {
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            lastAttemptFailure = reason ?? string.Empty;
            lastAttemptScope = LaunchScope(self);
            lastAttemptRevision = System.Threading.Interlocked.Read(ref readinessInputRevision);
            // APIShared logs the attempt in full and announces it once in an active Steam lobby.
            try
            {
                APIShared.Lobby.LobbyLaunchStatus.ReportAttempt(ExtendedDataPlugin.PluginGuid,
                    command == "Ready" || command == "ReadyLock" ? APIShared.Lobby.LobbyLaunchOperation.Ready
                        : APIShared.Lobby.LobbyLaunchOperation.Start,
                    new APIShared.Lobby.LobbyLaunchStatusItem(reason, APIShared.Lobby.LobbyLaunchStatusKind.Blocking,
                        "command=" + command + "; " + DescribeLaunchBlockContext(self) +
                        (string.IsNullOrEmpty(detail) ? string.Empty : "; " + detail)));
            }
            catch (Exception error) { LogError("Could not report launch block " + command + ": " + reason + "; " + error); }
            QueueLaunchReadiness();
            // Chat and log do not depend on a popup being rendered successfully.
            if (showPopup)
                try { ShowBlockedMessage(reason); }
                catch (Exception error) { LogError("Could not display launch block dialog: " + error); }
        }

        private void ClearLaunchTracking()
        {
            selected = null;
            lordDataCoordinator?.SetEmbeddedTrailSlots(null);
            missingMods = Array.Empty<string>();
            coopLaunchPending = false;
        }

        private void ActivateSelectedMissionSettings(bool editable, string source, bool starting)
        {
            if (!activation.MissionModSettings || selected == null)
                return;
            missingMods = missionSettingsCoordinator.PrepareCoopSettings(
                selected.Loaded.Definition.ModSettings,
                "coop:" + settings.ActiveCoopPackageId + ":" + settings.ActiveCoopPackageFingerprint + ":" + selected.Loaded.TrailNumber + ":" + selected.Loaded.MissionNumber,
                editable, starting);
        }

        private void ActivateSelectedMissionSettingsUnlessMap(
            FRONT_Multiplayer lobby,
            bool editable,
            string source,
            bool starting = false)
        {
            if (MissionRestart.IsInProgress)
            {
                Shared.DevDiag.Log(() => "Retained current mission settings during " + source + ".");
                return;
            }
            if (mapSettingsCoordinator?.IsActiveForLobby(lobby) == true)
            {
                Shared.DevDiag.Log(() => "Retained the manually selected Map mod-settings preset during " + source + ".");
                return;
            }
            ActivateSelectedMissionSettings(editable, source, starting);
        }

        private void OnMapStarted(MissionContext mission)
        {
            readinessInMission = true;
            ExtendedDataLaunchOriginApi.MarkMapStarted(mission.SessionId);
            if (!coopLaunchPending || selected == null)
                return;
            coopLaunchPending = false;
            {
                Shared.DevDiag.Log(() => "Custom Coop mission map started; retaining its active mod-settings preset.");
            }
        }

        private bool PrepareSinglePlayerCoopStart(FRONT_Multiplayer lobby)
        {
            if (CurrentSlotOutsidePackage(lobby))
            {
                BlockLaunch("COOP_START", CustomCoopTrailEndNotice(lobby));
                return false;
            }
            if (!activation.CoopPackages || lobby?.currentLobby == null || !lobby.singlePlayerCoop ||
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
                RefreshPackageCatalog();
                CoopTrailPackage currentPackage = LoadSelectedPackage(forceValidation: true);
                if (!string.Equals(currentPackage.Manifest.ContentFingerprint,
                        settings.ActiveCoopPackageFingerprint, StringComparison.OrdinalIgnoreCase) ||
                    currentPackage.Manifest.MissionCount != settings.ActiveCoopPackageMissionCount)
                    throw new InvalidDataException(SerpLocalization.Get("ExtendedData.ErrorFingerprintMismatch"));
                selected = EnsureResolvedMission(trailId, missionId);
                string featureError = SelectedFeatureDependencyError();
                if (featureError != null) throw new InvalidOperationException(featureError);
                RefreshSelectedLordRequirementStatus(lobby);
                if (!PrepareSelectedLordRequirements(out string lordReason, captureLocalReplacements: true))
                    throw new InvalidDataException(lordReason);
                ActivateSelectedMissionSettingsUnlessMap(
                    lobby,
                    editable: false,
                    source: "single-player Coop restart", starting: true);
                if (!missionSettingsCoordinator.TryPrepareRestartSettings(
                    reason => BlockLaunch("COOP_START", reason))) return false;
                ExtendedDataLaunchOriginApi.PrepareCoopLaunch(trailId, missionId, MissionRestart.IsInProgress);
                missionSettingsCoordinator.PrepareCoopMissionLaunch();
                coopLaunchPending = true;
                Shared.DevDiag.Log(() => 
                        "Revalidated custom single-player Coop restart: Trail" + (trailId + 1) + "/" +
                        missionId.ToString("00") + ".");
                return true;
            }
            catch (Exception exception)
            {
                coopLaunchPending = false;
                missionSettingsCoordinator.RecoverCoopDraft();
                string reason = SerpLocalization.Get("ExtendedData.ErrorCoopPreparationFailed");
                BlockLaunch("COOP_START", reason, "Single-player Coop restart failed: " + exception);
                return false;
            }
        }

        private void OnSinglePlayerCoopStartRejected() => coopLaunchPending = false;

        private void OnCoopLaunchReceived(int trailId, int missionId)
        {
            if (!activation.CoopPackages) return;
            try { selected = EnsureResolvedMission(trailId, missionId); }
            catch (Exception exception)
            {
                RejectMissionSelection(trailId, missionId, exception);
                return;
            }
            string featureError = SelectedFeatureDependencyError();
            if (featureError != null)
            {
                BlockLaunch("COOP_START", featureError);
                return;
            }
            RefreshSelectedLordRequirementStatus(GetExistingMainViewModel()?.FRONTMultiplayer);
            if (!IsLocalPackageReady())
            {
                BlockLaunch("COOP_START", GetLocalBlockReason());
                return;
            }
            if (!lordDataCoordinator.IsLocalSelectionReady(
                CurrentLordInfoMap(GetExistingMainViewModel()?.FRONTMultiplayer),
                out string localLordReason))
            {
                BlockLaunch("COOP_START", localLordReason);
                return;
            }

            // This authenticated launch boundary also covers clients which never execute
            // the host's setup-screen button handler.
            ExtendedDataLaunchOriginApi.PrepareCoopLaunch(trailId, missionId, MissionRestart.IsInProgress);

            // Clients do not execute the host's COOP_START button handler. The authenticated
            // transition supplies the missing launch boundary before OnUnloadMap clears presets.
            if (!PrepareSelectedLordRequirements(out string lordReason))
            {
                BlockLaunch("COOP_START", lordReason);
                return;
            }
            ActivateSelectedMissionSettingsUnlessMap(
                GetExistingMainViewModel()?.FRONTMultiplayer,
                editable: false,
                source: "authenticated host Coop launch", starting: true);
            coopLaunchPending = true;
            missionSettingsCoordinator.PrepareCoopMissionLaunch();
            // Support-relevant once per launch.
            LogInfo($"Prepared authenticated Coop Trail launch trail={trailId + 1}, mission={missionId}; retaining its active mod-settings preset across map unload.");
        }

        private void OnMissionEnded(MissionLifecycleNotification notification)
        {
            readinessInMission = false;
            QueueLaunchReadiness();
            bool retained = missionSettingsCoordinator?.HandleMissionEnded(notification) == true;
            if (retained)
            {
                if (!notification.HasStarted) coopLaunchPending = false;
                return;
            }
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

        private static FieldInfo RequirePrivateLobbyFlag(string name)
        {
            FieldInfo field = typeof(FRONT_Multiplayer).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.FieldType == typeof(bool) ? field :
                throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name);
        }

        private static bool ReadLobbyFlag(FRONT_Multiplayer self, FieldInfo field) =>
            self != null && (bool)field.GetValue(self);

        // Player/support-relevant information only; development diagnostics use Shared.DevDiag.
        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) => Shared.DebugLogHelper.LogErrorLimited(log, callerFile + ":" + callerLine, message);
    }
}
