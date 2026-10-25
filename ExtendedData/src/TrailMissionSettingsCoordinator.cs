using APIShared.GameModes;
using APIShared.ModSettings;
using APIShared;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using ExtendedData.Core;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using IOPath = System.IO.Path;

namespace ExtendedData
{
    /// <summary>Owns the process-wide Custom Trail settings and customization integration.</summary>
    internal sealed class TrailMissionSettingsCoordinator : IDisposable, IModSettingsWorkingSourceProvider
        {
            private const string CoopTrailMakerSourceDirectory = "TrailMakerSource";
            private const int CoopCustomizeProtocolVersion = 2;

            private static readonly FieldInfo MpLocalReadyField = typeof(FRONT_Multiplayer).GetField(
                "MPLocalReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly FieldInfo MpLocalReadyLockedField = typeof(FRONT_Multiplayer).GetField(
                "MPLocalReadyLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private delegate void SaveCustomTrailMapDelegate(
                EditorDirector self,
                string mapPath,
                string mapName,
                string trailPath,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);
            private delegate void ManageTrailButtonDelegate(FRONT_ManageTrail self, string command);
            private delegate void EditorSetupButtonDelegate(FRONT_EditorSetup self, string command);
            private delegate void UploadWorkshopMapDelegate(
                Platform_Workshop instance,
                string nameMap,
                string mapTitle,
                string description,
                string[] tags,
                bool publicMap,
                string previewImage,
                Action successAction,
                Action failAction);
            private delegate void ManageTrailInitDelegate(FRONT_ManageTrail self, bool preserveSelection);
            private delegate List<FileHeader> MultiplayerMapsDelegate(MapFileManager self, int sortMode,
                bool ascending, int players, bool builtIn, bool user, bool workshop);
            private delegate void ImportDelegate(FRONT_ManageTrail self, string customFolderName);
            private delegate void ExportDelegate(FRONT_ManageTrail self, string destination);
            private delegate void TwoStringDelegate(FRONT_ManageTrail self, string first, string second);
            private delegate void NoArgumentDelegate(FRONT_ManageTrail self);
            private delegate void StartCustomTrailDelegate(MainViewModel self, string trailName, int missionId, int difficulty);
            private delegate void LaunchCustomTrailDelegate(FRONT_Multiplayer self,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo, FileHeader headerToUse);
            private delegate void MultiplayerOpenDelegate(
                FRONT_Multiplayer self,
                bool skirmishSetup,
                bool fromNew,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo,
                bool coopSetup,
                bool trailMaker,
                int customiseTrailType,
                int customiseTrailId);
            private delegate void StartSkirmishGameDelegate(
                FRONT_Multiplayer self,
                HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo);
            private delegate void FrontendOpenCustomTrailDelegate(FrontendMenus self, string trailName, int level);
            private delegate void FrontendButtonDelegate(FrontendMenus self, string command);
            private delegate void TrailSelectionDelegate(FrontendMenus self, int missionId, bool fromRealClick);
            private delegate void GenerateSwordsDelegate(FrontendMenus self);
            private delegate void CoopTrail1ConstructorDelegate(FRONT_CoopTrail1 self);
            private delegate void CoopTrail2ConstructorDelegate(FRONT_CoopTrail2 self);
            private delegate void CoopTrail3ConstructorDelegate(FRONT_CoopTrail3 self);
            private delegate void CoopTrail4ConstructorDelegate(FRONT_CoopTrail4 self);

            private readonly ManualLogSource log;
            private readonly BugfixesAndQoLTrailCustomizationBridge customizationBridge;
            private readonly Func<string, string, TrailSettingMode> getPropertyMode;
            private readonly Action<ModSettingsDefinition, bool> applyEditorModes;
            private readonly Action<string, ModSettingsDefinition> applyEditorModesForMod;
            private readonly EditorModSettingsSaveOptionsViewModel editorSaveOptions;
            private readonly List<IDisposable> hooks = new List<IDisposable>();
            private readonly Dictionary<string, string> lastCompatibilityFailures =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ModSettingsDefinition> capturedDocumentsByTrailPath =
                new Dictionary<string, ModSettingsDefinition>(StringComparer.OrdinalIgnoreCase);
            private readonly MissionModSettingsSession settingsSession = new MissionModSettingsSession();
            private readonly CoopSettingsDraftState coopSettingsDraft = new CoopSettingsDraftState();
            private Dictionary<string, Dictionary<string, byte[]>> customizeDraftSnapshots;
            private object coopPacketEpoch = new object();
            private object coopPacketLobby;

            internal void ObserveCoopLobby(FRONT_Multiplayer self)
            {
                object lobby = self?.currentLobby;
                if (ReferenceEquals(coopPacketLobby, lobby)) return;
                coopPacketLobby = lobby;
                System.Threading.Volatile.Write(ref coopPacketEpoch, new object());
            }
            private readonly MissionPresetLifecycleState missionPresetLifecycle = new MissionPresetLifecycleState();
            private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
            private ManageTrailButtonDelegate manageTrailButtonOriginal;
            private EditorSetupButtonDelegate editorSetupButtonOriginal;
            private UploadWorkshopMapDelegate uploadWorkshopMapOriginal;
            private ManageTrailInitDelegate manageTrailInitOriginal;
            private MultiplayerMapsDelegate multiplayerMapsOriginal;
            private FileHeader embeddedMakerMapHeader;
            private TwoStringDelegate backupOriginal;
            private ImportDelegate importOriginal;
            private ExportDelegate exportOriginal;
            private NoArgumentDelegate clearMakerOriginal;
            private StartCustomTrailDelegate startCustomTrailOriginal;
            private LaunchCustomTrailDelegate launchCustomTrailOriginal;
            internal LordDataSyncCoordinator LordDataCoordinator { get; set; }
            private MultiplayerOpenDelegate multiplayerOpenOriginal;
            private StartSkirmishGameDelegate startSkirmishGameOriginal;
            private FrontendOpenCustomTrailDelegate frontendOpenCustomTrailOriginal;
            private FrontendButtonDelegate frontendButtonOriginal;
            private TrailSelectionDelegate trailSelectionOriginal;
            private GenerateSwordsDelegate generateSwordsOriginal;
            private CoopTrail1ConstructorDelegate coopTrail1ConstructorOriginal;
            private CoopTrail2ConstructorDelegate coopTrail2ConstructorOriginal;
            private CoopTrail3ConstructorDelegate coopTrail3ConstructorOriginal;
            private CoopTrail4ConstructorDelegate coopTrail4ConstructorOriginal;
            private bool trailContext;
            private string activeContextLabel = "Trail";
            private bool preserveContextForLaunch;
            private bool customTrailLaunchActive;
            private bool cleanupDeferralLogged;
            private bool openingCustomTrailSetup;
            private HUD_IngameMenu.RestartSkirmishMapInfo customTrailSetupRestartInfo;
            private FileHeader customTrailSetupHeader;
            private ModSettingsDefinition trailMakerWorkingDocument;
            private Dictionary<string, Dictionary<string, byte[]>> trailMakerWorkingSnapshots;
            private string trailMakerTrailPath;
            private string pendingTrailMakerTrailPath;
            private bool pendingTrailMakerLoad;
            private bool trailMakerAuthoringActive;
            private string activeSidecarPath;
            private long activeSidecarLength = -1;
            private long activeSidecarWriteTicks;
            private bool activeSidecarEditable;
            private bool activeSidecarPreviewOnly;
            private bool activeSidecarAuthoring;
            private bool workingContextEditable;
            private readonly TrailSettingsSourceState trailSource = new TrailSettingsSourceState();
            private ModSettingsDefinition activeCreatorDocument;
            private bool launchedThroughCustomize;
            private ModSettingsDefinition mapSourceDocument;
            private string workingSourceContextId = string.Empty;
            private ExtendedDataFeatureActivation activation;
            private bool enabled => activation.MissionModSettings;
            private bool EditorEnabled => activation.EditorFiles;
            private bool CoopEnabled => activation.CoopPackages;
            private bool UploadEnabled => activation.WorkshopUploads;
            private bool externalButtonOwner;
            private readonly List<Button> injectedCoopButtons = new List<Button>();
            private readonly Dictionary<UserControl, TextBlock> coopTrailTitleBlocks =
                new Dictionary<UserControl, TextBlock>();
            private readonly Dictionary<UserControl, TextBlock> coopMissionProgressBlocks =
                new Dictionary<UserControl, TextBlock>();
            private readonly string[] vanillaCoopTrailTitles = new string[4];
            private readonly Dictionary<int, Button> coopSelectionButtons =
                new Dictionary<int, Button>();
            private readonly Dictionary<int, bool> vanillaCoopSelectionEnabled =
                new Dictionary<int, bool>();
            private readonly Dictionary<string, string> coopImportSourceBySelection =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private CheckBox coopTrailExportCheckbox;
            private readonly TrailWorkshopUploadOptionsViewModel uploadOptions = new TrailWorkshopUploadOptionsViewModel();
            private CustomLordPopupPatchVerifier customLordPopupPatchVerifier;
            private CustomLordUploadWorkflow customLordUploadWorkflow;
            private readonly object uploadDecisionLock = new object();
            private PendingUploadDecision pendingUploadDecision;
            private string coopPackageDisplayName = string.Empty;
            private int coopPackageMissionCount;
            private short coopCustomizePacketId;
            private short builtInCustomizeOriginPacketId;

            public event Action CoopPackagesChanged;
            internal event Action CoopPackageExported;
            public event Action CoopSetupOpened;
            public event Action<int, int> CoopLaunchReceived;
            public event Action<FRONT_Multiplayer> LobbyOpened;
            public event Func<FRONT_Multiplayer, bool> SinglePlayerCoopStarting;
            public event Func<FRONT_Multiplayer, string> SinglePlayerCoopLaunchValidating;
            public event Action SinglePlayerCoopStartRejected;
            public event Action SourcesChanged;

            public IReadOnlyList<TrailModCompatibilityInfo> DiscoverModCompatibility()
            {
                var result = new List<TrailModCompatibilityInfo>();
                // Consume the same decision and diagnostic as savegame preparation. A
                // rejected endpoint must not be re-probed through a second contract here.
                var reports = SavegameModSettings.GetParticipantDiagnostics(ModSettingsParticipantPurpose.MissionAuthoring)
                    .Where(item => !string.IsNullOrEmpty(item.ModGuid))
                    .GroupBy(item => item.ModGuid, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key,
                        group => group.FirstOrDefault(item => item.IsCompatible) ?? group.First(), StringComparer.Ordinal);
                foreach (IGrouping<string, LobbyModSettingsEntry> group in GetRegistrationGroups())
                {
                    if (IsRegistrationGroupOptedOut(group)) continue;
                    string modId = group.Key;
                    if (string.Equals(modId, ExtendedDataPlugin.PluginGuid, StringComparison.Ordinal)) continue;
                    reports.TryGetValue(modId, out var report);
                    string reason = null;
                    if (group.Skip(1).Any())
                        reason = "multiple mod-settings panels use the same plugin GUID";
                    else if (report?.IsCompatible != true)
                        // Provider exception messages may be empty. A rejected status must
                        // never become compatible through the legacy reason-string adapter.
                        reason = !string.IsNullOrWhiteSpace(report?.Diagnostic) ? report.Diagnostic
                            : report == null ? "missing owning plugin GUID or mod-settings registration" : report.Status.ToString();
                    result.Add(new TrailModCompatibilityInfo(modId, GetModDisplayName(group.First()),
                        report?.IsCompatible == true ? report.HostProperties.ToArray() : Array.Empty<PropertyInfo>(),
                        reason, report?.IsCompatible == true ? (object)report.Endpoint : group.First()?.ViewModel));
                }
                TrailModCompatibilityInfo[] catalog = result
                    .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
                LogChangedCompatibilityFailures(catalog);
                return catalog;
            }

            private void LogChangedCompatibilityFailures(IEnumerable<TrailModCompatibilityInfo> catalog)
            {
                var current = catalog
                    .Where(item => !item.IsCompatible)
                    .ToDictionary(item => item.ModId, item => item.IncompatibilityReason, StringComparer.Ordinal);
                foreach (TrailModCompatibilityInfo item in catalog.Where(item => !item.IsCompatible))
                {
                    if (!lastCompatibilityFailures.TryGetValue(item.ModId, out string previous) ||
                        !string.Equals(previous, item.IncompatibilityReason, StringComparison.Ordinal))
                    {
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:257", $"Map/Trail mod settings [{item.DisplayName}] ({item.ModId}) are not included in creator presets: " +
                                item.IncompatibilityReason + ".");
                    }
                }
                lastCompatibilityFailures.Clear();
                foreach (KeyValuePair<string, string> item in current)
                    lastCompatibilityFailures[item.Key] = item.Value;
            }

            private static string GetModId(LobbyModSettingsEntry entry)
            {
                string pluginId = entry?.Plugin?.Info?.Metadata?.GUID;
                if (!string.IsNullOrWhiteSpace(pluginId))
                    return pluginId;
                return string.IsNullOrWhiteSpace(entry?.Name)
                    ? "<unknown mod-settings registration>"
                    : entry.Name;
            }

            private static string GetModDisplayName(LobbyModSettingsEntry entry)
            {
                string displayName = entry?.Plugin?.Info?.Metadata?.Name;
                return string.IsNullOrWhiteSpace(displayName) ? GetModId(entry) : displayName;
            }

            private static IEnumerable<IGrouping<string, LobbyModSettingsEntry>> GetRegistrationGroups() =>
                GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .GroupBy(GetModId, StringComparer.Ordinal);

            private static bool IsRegistrationGroupOptedOut(IEnumerable<LobbyModSettingsEntry> group) =>
                group.Any(entry => TrailModCompatibilityContract.IsExplicitlyOptedOut(entry?.Plugin));

            public TrailMissionSettingsCoordinator(
                ManualLogSource log,
                ExtendedDataFeatureActivation activation,
                Func<string, string, TrailSettingMode> getPropertyMode,
                Action<ModSettingsDefinition, bool> applyEditorModes,
                Action<string, ModSettingsDefinition> applyEditorModesForMod,
                EditorModSettingsSaveOptionsViewModel editorSaveOptions)
            {
                this.log = log;
                this.activation = activation;
                this.getPropertyMode = getPropertyMode ?? ((_, __) => TrailSettingMode.ModDefault);
                this.applyEditorModes = applyEditorModes;
                this.applyEditorModesForMod = applyEditorModesForMod;
                this.editorSaveOptions = editorSaveOptions ?? throw new ArgumentNullException(nameof(editorSaveOptions));
                customizationBridge = new BugfixesAndQoLTrailCustomizationBridge(log);
            }

            internal void SetActivation(ExtendedDataFeatureActivation value)
            {
                ExtendedDataFeatureActivation previous = activation;
                activation = value;
                if (previous.MissionModSettings != value.MissionModSettings)
                    foreach (Button button in injectedCoopButtons)
                        button.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                if (previous.EditorFiles != value.EditorFiles && coopTrailExportCheckbox != null)
                    coopTrailExportCheckbox.Visibility = EditorEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (previous.WorkshopUploads && !UploadEnabled)
                {
                    uploadOptions.Close();
                    customLordUploadWorkflow?.InvalidatePending();
                }
                if (previous.CoopPackages && !CoopEnabled) SetCoopPackagePresentation(null, 0);
                if (previous.MissionModSettings && !enabled)
                {
                    if (MainViewModel.viewModelLoaded && MainViewModel.Instance != null)
                        MainViewModel.Instance.Show_TrailCustomisationButtons = false;
                    if (!trailMakerAuthoringActive) ExitContext(force: true);
                }
                if (previous.MissionModSettings != value.MissionModSettings && externalButtonOwner)
                    customizationBridge.Refresh();
            }

            public void Initialize()
            {
                ModSettingsWorkingSourceRegistry.Register(this);
                externalButtonOwner = customizationBridge.TryRegister(
                    () => enabled,
                    HandleExternalCustomTrailCustomize,
                    HandleExternalCoopTrailCustomize);
                customLordPopupPatchVerifier = new CustomLordPopupPatchVerifier(log);
                CustomLordRuntimeRules lordRules = CustomLordRuntimeRules.Discover(typeof(GameAIManagerAPI).Assembly);
                customLordUploadWorkflow = new CustomLordUploadWorkflow(
                    log,
                    new CustomLordUploadStager(),
                    new CustomLordUploadConfirmation(),
                    lordRules, () => UploadEnabled);
                DebugLogHelper.LogDebug(
                    log,
                    "Custom Lord preflight rules: Script Extender=" + lordRules.ExtenderIdentity +
                    ", reflectedLordInfoFields=" + lordRules.LordInfoFields.Count +
                    ", reflectedMessageTypes=" + lordRules.MessageTypes.Count +
                    ", publicValidator=" + (lordRules.PublicValidator != null) + ".");
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding("ExtendedDataUploadOptionsHost", uploadOptions);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:371", "Extended Data upload checkbox binding failed; additional files remain enabled: " + exception);
                }
                CaptureVanillaCoopTrailTitles();
                R3PacketEventHook<CoopCustomizePacket> packetHook =
                    GameNetworkAPI.Instance.GetPacketEventFor<CoopCustomizePacket>();
                coopCustomizePacketId = packetHook.GetPacketId();
                hooks.Add(packetHook.GetBaseHook().Observable.Subscribe(OnCoopCustomizePacket));
                R3PacketEventHook<BuiltInCustomizeOriginPacket> builtInOriginPacketHook =
                    GameNetworkAPI.Instance.GetPacketEventFor<BuiltInCustomizeOriginPacket>();
                builtInCustomizeOriginPacketId = builtInOriginPacketHook.GetPacketId();
                hooks.Add(builtInOriginPacketHook.GetBaseHook().Observable.Subscribe(OnBuiltInCustomizeOriginPacket));
                saveCustomTrailMapOriginal = InstallHook(
                    typeof(EditorDirector).GetMethod(nameof(EditorDirector.SaveCustomTrailMap)),
                    (SaveCustomTrailMapDelegate)SaveCustomTrailMapHook);
                manageTrailButtonOriginal = InstallHook(
                    typeof(FRONT_ManageTrail).GetMethod("ButtonClicked", BindingFlags.Instance | BindingFlags.Public),
                    (ManageTrailButtonDelegate)ManageTrailButtonHook);
                multiplayerMapsOriginal = InstallHook(
                    typeof(MapFileManager).GetMethod(nameof(MapFileManager.GetMultiplayerMaps)),
                    (MultiplayerMapsDelegate)MultiplayerMapsHook);
                editorSetupButtonOriginal = InstallHook(
                    typeof(FRONT_EditorSetup).GetMethod("ButtonClicked", BindingFlags.Instance | BindingFlags.Public),
                    (EditorSetupButtonDelegate)EditorSetupButtonHook);
                uploadWorkshopMapOriginal = InstallHook(
                    typeof(Platform_Workshop).GetMethod(
                        nameof(Platform_Workshop.UploadWorkshopMap),
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[]
                        {
                            typeof(string), typeof(string), typeof(string), typeof(string[]),
                            typeof(bool), typeof(string), typeof(Action), typeof(Action)
                        },
                        null),
                    (UploadWorkshopMapDelegate)UploadWorkshopMapHook);
                manageTrailInitOriginal = InstallHook(
                    RequireManageTrailMethod("Init", typeof(bool)),
                    (ManageTrailInitDelegate)ManageTrailInitHook);
                backupOriginal = InstallHook(RequireManageTrailMethod("DoBackup", typeof(string), typeof(string)), (TwoStringDelegate)BackupHook);
                importOriginal = InstallHook(RequireManageTrailMethod("ImportTrailMissions", typeof(string)), (ImportDelegate)ImportHook);
                exportOriginal = InstallHook(RequireManageTrailMethod("ExportTrailMissions", typeof(string)), (ExportDelegate)ExportHook);
                clearMakerOriginal = InstallHook(RequireManageTrailMethod("ClearMakerFolder"), (NoArgumentDelegate)ClearMakerHook);
                startCustomTrailOriginal = InstallHook(
                    typeof(MainViewModel).GetMethod(nameof(MainViewModel.StartCustomTrailMission)),
                    (StartCustomTrailDelegate)StartCustomTrailHook);
                launchCustomTrailOriginal = InstallHook(
                    typeof(FRONT_Multiplayer).GetMethod(nameof(FRONT_Multiplayer.StartCustomTrailMission),
                        BindingFlags.Instance | BindingFlags.Public),
                    (LaunchCustomTrailDelegate)LaunchCustomTrailHook);
                multiplayerOpenOriginal = InstallHook(
                    typeof(FRONT_Multiplayer).GetMethod(
                        "doOpen",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[]
                        {
                            typeof(bool), typeof(bool), typeof(HUD_IngameMenu.RestartSkirmishMapInfo),
                            typeof(bool), typeof(bool), typeof(int), typeof(int),
                        },
                        null),
                    (MultiplayerOpenDelegate)MultiplayerOpenHook);
                startSkirmishGameOriginal = InstallHook(
                    typeof(FRONT_Multiplayer).GetMethod(
                        "StartSkirmishGame",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(HUD_IngameMenu.RestartSkirmishMapInfo) },
                        null),
                    (StartSkirmishGameDelegate)StartSkirmishGameHook);
                frontendOpenCustomTrailOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.OpenCustomTrail), new[] { typeof(string), typeof(int) }),
                    (FrontendOpenCustomTrailDelegate)FrontendOpenCustomTrailHook);
                frontendButtonOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod("ButtonClicked", new[] { typeof(string) }),
                    (FrontendButtonDelegate)FrontendButtonHook);
                trailSelectionOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod(
                        nameof(FrontendMenus.ButtonTrailCampaignClicked),
                        new[] { typeof(int), typeof(bool) }),
                    (TrailSelectionDelegate)TrailSelectionHook);
                generateSwordsOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.GenerateSwords), Type.EmptyTypes),
                    (GenerateSwordsDelegate)GenerateSwordsHook);
                coopTrail1ConstructorOriginal = InstallHook(
                    typeof(FRONT_CoopTrail1).GetConstructor(Type.EmptyTypes),
                    (CoopTrail1ConstructorDelegate)CoopTrail1ConstructorHook);
                coopTrail2ConstructorOriginal = InstallHook(
                    typeof(FRONT_CoopTrail2).GetConstructor(Type.EmptyTypes),
                    (CoopTrail2ConstructorDelegate)CoopTrail2ConstructorHook);
                coopTrail3ConstructorOriginal = InstallHook(
                    typeof(FRONT_CoopTrail3).GetConstructor(Type.EmptyTypes),
                    (CoopTrail3ConstructorDelegate)CoopTrail3ConstructorHook);
                coopTrail4ConstructorOriginal = InstallHook(
                    typeof(FRONT_CoopTrail4).GetConstructor(Type.EmptyTypes),
                    (CoopTrail4ConstructorDelegate)CoopTrail4ConstructorHook);

                EnsureCoopCustomizeButtons();
                EnsureTrailMakerCoopCheckbox(FRONT_ManageTrail.Instance);
                DebugLogHelper.LogDebug(log, "Trail mission-settings coordinator initialized.");
            }

            public void Dispose()
            {
                activation = ExtendedDataFeatureActivation.Disabled;
                ModSettingsWorkingSourceRegistry.Unregister(this);
                SetCoopPackagePresentation(null, 0);
                // The list also contains subscriptions. Release those, but retain every
                // published interception and its trampoline for the lifetime of the process.
                foreach (IDisposable item in hooks)
                    if (!(item is Hook)) item.Dispose();
                hooks.RemoveAll(item => !(item is Hook));
            }

            public string[] Enter(ModSettingsDefinition document, bool editable, string source)
            {
                return Enter(document, editable, source, "Trail");
            }

            internal string[] Enter(
                ModSettingsDefinition document,
                bool editable,
                string source,
                string presetLabel)
            {
                try
                {
                    return trailSource.Activate(document, source, () =>
                    {
                        document = ModSettingsJson.NormalizeAndValidate(document, source + ".modSettings");
                        ModSettingsApplication.EnterContext(BuildRestartContext(source, document));
                        ApplyDocument(document, editable, presetLabel);
                        activeCreatorDocument = CloneDocument(document);
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:497", $"Loaded {source} mod settings; editable={editable}.");
                        return GetMissingMentionedMods(document);
                    });
                }
                catch (Exception exception)
                {
                    if (ModSettingsApplication.HasApplicationEndpoints || ModSettingsApplication.HasRestartPreparation || ModSettingsApplication.HasActivationFailures) throw;
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:503", $"Could not load {source} mod settings; sidecar mod settings are ignored: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable, presetLabel);
                    activeCreatorDocument = ModSettingsDefinition.CreateModDefaults();
                    return Array.Empty<string>();
                }
                finally { SourcesChanged?.Invoke(); }
            }

            internal string[] EnterStrict(
                ModSettingsDefinition document,
                bool editable,
                string source,
                string presetLabel,
                bool materializeCurrentValues = false)
            {
                document = ValidateStrict(document, source);
                if (string.IsNullOrEmpty(ModSettingsApplication.ContextId)) ModSettingsApplication.EnterContext(BuildRestartContext(source, document));
                ApplyDocument(document, editable, presetLabel, materializeCurrentValues: materializeCurrentValues);
                activeCreatorDocument = CloneDocument(document);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:521", $"Loaded {source} mod settings; editable={editable}.");
                return GetMissingMentionedMods(document);
            }

            internal ModSettingsDefinition ValidateStrict(ModSettingsDefinition document, string source)
            {
                document = ModSettingsJson.NormalizeAndValidate(document, source + ".modSettings");
                ValidateDocumentValues(document);
                return document;
            }

            internal ModSettingsDefinition CaptureCurrentDocument() => CaptureDocument();
            internal bool WasLaunchedThroughCustomize() => launchedThroughCustomize;
            internal void ResetCustomizeForNewCoopSelection()
            {
                launchedThroughCustomize = false;
                coopSettingsDraft.Reset();
                customizeDraftSnapshots = null;
            }

            internal string[] PrepareCoopSettings(ModSettingsDefinition document, string source,
                bool editable, bool starting)
            {
                if (!trailContext) coopSettingsDraft.Reset();
                coopSettingsDraft.Prepare(source, launchedThroughCustomize, editable, starting,
                    () => Enter(document, editable, source),
                    FreezeWorkingDraft, ReopenWorkingDraft);
                return GetMissingMentionedMods(document);
            }

            internal bool HasCustomizeDraft => trailContext && launchedThroughCustomize;
            internal bool IsCustomizeDraftEditable => HasCustomizeDraft && workingContextEditable;

            internal void RecoverCoopDraft()
            {
                coopSettingsDraft.Recover(ReopenWorkingDraft);
                if (HasCustomizeDraft && !workingContextEditable) ReopenWorkingDraft();
            }

            private void FreezeWorkingDraft()
            {
                customizeDraftSnapshots = CaptureCurrentSnapshots(FindCompatibleViewModels());
                try
                {
                    ApplyDocument(CaptureDocument(), editable: false,
                        materializeCurrentValues: true, preserveEditorModes: true);
                }
                catch (Exception failure)
                {
                    try { ReopenWorkingDraft(); }
                    catch (Exception recovery) { throw new AggregateException("Draft preparation and recovery failed.", failure, recovery); }
                    throw;
                }
            }

            private void ReopenWorkingDraft() => ApplyDocument(ModSettingsDefinition.CreateModDefaults(),
                editable: true, preserveCurrentValues: true, startingSnapshots: customizeDraftSnapshots,
                preserveEditorModes: true);

            internal Dictionary<string, Dictionary<string, TrailCreatorRule>> CaptureCreatorRulesForActiveTrail()
            {
                var result = new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal);
                ModSettingsDefinition document = activeCreatorDocument;
                if (document == null || !trailContext) return result;
                foreach (var participant in FindCompatibleViewModels())
                {
                    document.Mods.TryGetValue(participant.Key, out ModSettingsEntry entry);
                    result[participant.Key] = MissionModSettingsCreatorRules.Capture(participant.Value, ToSharedEntry(entry),
                        (name, error) => Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/trail-rules", "Could not capture Trail rule " +
                            participant.Key + "." + name + ": " + error.Message));
                }
                return result;
            }

            internal void SetMapSourceDocument(ModSettingsDefinition document, string contextId = null)
            {
                mapSourceDocument = CloneDocument(document);
                if (document != null) ModSettingsApplication.EnterContext(BuildRestartContext("map:" + (contextId ?? ""), document, File.Exists(contextId) ? contextId : null));
                if (!trailSource.IsAvailable)
                    workingSourceContextId = document == null ? string.Empty : "map:" + (contextId ?? string.Empty);
                SourcesChanged?.Invoke();
            }

            public IReadOnlyList<ModSettingsWorkingSource> GetSources(string targetGuid)
            {
                var result = new List<ModSettingsWorkingSource>();
                string preferenceContext = trailSource.IsAvailable ? trailSource.ContextId : workingSourceContextId;
                if (trailSource.IsAvailable)
                    result.Add(new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.TrailId, Kind = ModSettingsWorkingSourceKind.Trail, DisplayName = "Trail settings", IsPreferred = true, PreferenceContextId = preferenceContext });
                if (mapSourceDocument != null)
                    result.Add(new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.MapId, Kind = ModSettingsWorkingSourceKind.Map, DisplayName = "Map settings", IsPreferred = !trailSource.IsAvailable, PreferenceContextId = preferenceContext });
                return result;
            }

            public void Apply(string targetGuid, string sourceId) => ApplyMany(new[] { targetGuid }, sourceId);

            public void ApplyMany(IEnumerable<string> targetGuids, string sourceId)
            {
                if (!trailContext || !workingContextEditable)
                    throw new InvalidOperationException("Mission ModSettings are not currently editable.");
                ModSettingsDefinition source = ResolveWorkingSource(sourceId);
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();
                string[] ids = (targetGuids ?? Enumerable.Empty<string>()).Where(participants.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
                var workingEndpoints = new Dictionary<string, IModSettingsWorkingCopyEndpoint>(StringComparer.Ordinal);
                var prepared = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
                var rollback = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
                foreach (string id in ids)
                {
                    if (!(participants[id] is IModSettingsWorkingCopyEndpoint endpoint))
                        throw new InvalidOperationException("The selected mod does not support editable mission working sources: " + id);
                    workingEndpoints[id] = endpoint;
                    rollback[id] = endpoint.System_CreateCurrentMissionPresetSnapshot();
                    prepared[id] = MaterializeSource(id, endpoint, source,
                        string.Equals(sourceId, ModSettingsWorkingSourceRegistry.TrailId, StringComparison.Ordinal));
                }
                ModSettingsDefinition oldModes = CaptureDocument();
                try
                {
                    foreach (string id in ids)
                    {
                        applyEditorModesForMod?.Invoke(id, source);
                        workingEndpoints[id].System_ApplyMissionPresetSnapshot(prepared[id], DescribeWorkingSource(sourceId));
                    }
                }
                catch
                {
                    foreach (string id in ids)
                    {
                        try { workingEndpoints[id].System_ApplyMissionPresetSnapshot(rollback[id], activeContextLabel); }
                        catch (Exception rollbackException) { Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:630", "Could not roll back source application for [" + id + "]: " + rollbackException); }
                    }
                    applyEditorModes?.Invoke(oldModes, false);
                    throw;
                }
            }

            private ModSettingsDefinition ResolveWorkingSource(string sourceId)
            {
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                    return ModSettingsDefinition.CreateModDefaults();
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.TrailId, StringComparison.Ordinal) && trailSource.IsAvailable)
                    return trailSource.Capture();
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.MapId, StringComparison.Ordinal) && mapSourceDocument != null)
                    return CloneDocument(mapSourceDocument);
                throw new InvalidOperationException("The selected reset source is unavailable.");
            }

            private Dictionary<string, byte[]> MaterializeSource(string modId, IModSettingsWorkingCopyEndpoint endpoint,
                ModSettingsDefinition document, bool playableBaseline)
            {
                ModSettingsEntry entry = null;
                document?.Mods?.TryGetValue(modId, out entry);
                var resolved = MissionModSettingsPreparation.MaterializeSource(endpoint, ToSharedEntry(entry));
                if (!playableBaseline) return resolved;
                return TrailSettingsSourceState.ComposeResetSnapshot(
                    endpoint.System_CreateDisabledMissionPresetSnapshot(), resolved, entry);
            }

            private static MissionModSettingsEntry ToSharedEntry(ModSettingsEntry entry) => entry == null ? null :
                new MissionModSettingsEntry { PlayerSettings = entry.PlayerSettings, Overrides = entry.Overrides };

            private static string DescribeWorkingSource(string sourceId) =>
                string.Equals(sourceId, ModSettingsWorkingSourceRegistry.TrailId, StringComparison.Ordinal) ? "Trail" :
                string.Equals(sourceId, ModSettingsWorkingSourceRegistry.MapId, StringComparison.Ordinal) ? "Map" : "Mod defaults";

            private static ModSettingsDefinition CloneDocument(ModSettingsDefinition document) =>
                document == null ? null : ModSettingsJson.ParseObject(ModSettingsJson.Serialize(document));

            internal bool IsContextActive(string presetLabel) =>
                trailContext && string.Equals(activeContextLabel, presetLabel, StringComparison.Ordinal);

            private void ValidateDocumentValues(ModSettingsDefinition document)
            {
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();
                foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in participants)
                {
                    if (!document.Mods.TryGetValue(participant.Key, out ModSettingsEntry entry) || entry == null)
                        continue;
                    MissionModSettingsPreparation.ValidateEntry(participant.Value, ToSharedEntry(entry));
                }
            }

            private static string[] GetMissingMentionedMods(ModSettingsDefinition document) =>
                document.Mods
                    .Where(entry => entry.Value != null &&
                        ((entry.Value.PlayerSettings?.Length ?? 0) != 0 ||
                        (entry.Value.Overrides?.Count ?? 0) != 0))
                    .Select(entry => entry.Key)
                    .Where(id => !Chainloader.PluginInfos.ContainsKey(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();

            public void ExitContext(bool force = false)
            {
                if (!force && (preserveContextForLaunch || customTrailLaunchActive))
                {
                    if (!cleanupDeferralLogged)
                    {
                        cleanupDeferralLogged = true;
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:705", "Deferred Trail mod-settings cleanup while Custom Trail setup/mission is active.");
                    }
                    return;
                }
                if (!trailContext)
                {
                    coopSettingsDraft.Reset();
                    customizeDraftSnapshots = null;
                    if (ModSettingsApplication.ContextId.Length != 0) ModSettingsApplication.ExitContext();
                    preserveContextForLaunch = false;
                    customTrailLaunchActive = false;
                    customTrailSetupRestartInfo = null;
                    customTrailSetupHeader = null;
                    cleanupDeferralLogged = false;
                    ClearActiveSidecar();
                    ClearTrailMakerAuthoringState();
                    missionPresetLifecycle.Reset();
                    workingContextEditable = false;
                    trailSource.Clear();
                    activeCreatorDocument = null;
                    launchedThroughCustomize = false;
                    mapSourceDocument = null;
                    workingSourceContextId = string.Empty;
                    SourcesChanged?.Invoke();
                    return;
                }

                ExitActiveParticipants();
                coopSettingsDraft.Reset();
                customizeDraftSnapshots = null;
                ModSettingsApplication.ExitContext();
                trailContext = false;
                workingContextEditable = false;
                preserveContextForLaunch = false;
                customTrailLaunchActive = false;
                customTrailSetupRestartInfo = null;
                customTrailSetupHeader = null;
                cleanupDeferralLogged = false;
                ClearActiveSidecar();
                ClearTrailMakerAuthoringState();
                missionPresetLifecycle.Reset();
                trailSource.Clear();
                activeCreatorDocument = null;
                launchedThroughCustomize = false;
                mapSourceDocument = null;
                workingSourceContextId = string.Empty;
                SourcesChanged?.Invoke();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:748", "Left " + activeContextLabel + " mod-settings context.");
                activeContextLabel = "Trail";
            }

            public bool HandleMissionEnded(MissionLifecycleNotification notification)
            {
                ExtendedDataLaunchOriginApi.OnMissionEnded(notification?.Context?.SessionId ?? 0,
                    notification?.EndReason ?? MissionEndReason.Unloaded, notification?.HasStarted == true,
                    notification?.ReplacementContext?.SessionId,
                    notification?.ReplacementContext != null &&
                    notification.ReplacementContext.RestartedFromSessionId == notification.Context?.SessionId);
                if (notification?.ReplacementContext?.RestartedFromSessionId == notification?.Context?.SessionId &&
                    notification?.ReplacementContext != null)
                {
                    ExtendedDataLaunchOriginApi.MarkRestartPending();
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/restart", "Retained mission settings for restart session " + notification.ReplacementContext.SessionId + ".");
                    return true;
                }
                MissionEndReason reason = notification == null
                    ? MissionEndReason.Unloaded
                    : notification.EndReason;
                if (HasCustomizeDraft && notification?.HasStarted == false &&
                    (reason == MissionEndReason.Failed || reason == MissionEndReason.Exception))
                {
                    missionPresetLifecycle.Reset();
                    customTrailLaunchActive = false;
                    RecoverCoopDraft();
                    return true;
                }
                MissionPresetEndAction action = missionPresetLifecycle.End(MapEndKind(reason));
                if (action == MissionPresetEndAction.Preserve)
                {
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:760", "Retained the active Map/Trail mod-settings preset across an expected mission replacement.");
                    return true;
                }

                if (action == MissionPresetEndAction.SuspendTrailMaker && trailMakerAuthoringActive)
                {
                    if (trailContext)
                    {
                        ExitActiveParticipants();
                        trailContext = false;
                        workingContextEditable = false;
                    }
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:774", "Suspended the Trail Maker mission preset until the authoring lobby returns.");
                    preserveContextForLaunch = false;
                    customTrailLaunchActive = false;
                    customTrailSetupRestartInfo = null;
                    customTrailSetupHeader = null;
                    cleanupDeferralLogged = false;
                    activeContextLabel = "Trail";
                    ClearActiveSidecar();
                    return false;
                }

                ExitContext(force: true);
                return false;
            }

            public bool HandleMissionStarted(MissionLifecycleNotification notification)
            {
                if (notification?.Context?.RestartedFromSessionId != null)
                {
                    ConfirmStartedSettings(notification);
                    return true;
                }
                MissionPresetLaunchKind pending = missionPresetLifecycle.PendingLaunch;
                if (pending == MissionPresetLaunchKind.None)
                {
                    if (notification?.Context?.IsSave == true && ModSettingsApplication.ContextId.StartsWith("save:", StringComparison.Ordinal))
                        ConfirmStartedSettings(notification);
                    else if (trailContext && notification?.Context != null) settingsSession.ConfirmStarted(notification.Context.SessionId);
                    return true;
                }

                GameModeKind actual = notification == null || notification.Context == null
                    ? GameModeKind.Unknown
                    : notification.Context.Mode.Kind;
                bool matches =
                    (pending == MissionPresetLaunchKind.CustomTrail && actual == GameModeKind.CustomTrail) ||
                    (pending == MissionPresetLaunchKind.CoopTrail && actual == GameModeKind.CoopTrail) ||
                    (pending == MissionPresetLaunchKind.TrailMakerTest && actual == GameModeKind.CustomGame);
                if (!matches || !missionPresetLifecycle.ConfirmStarted(pending))
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:810", "The prepared Map/Trail mod-settings preset did not match the started mission; " +
                        "expected=" + pending + ", actual=" + actual + ".");
                    ExitContext(force: true);
                    return false;
                }

                ConfirmStartedSettings(notification);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:819", "Confirmed active mission preset: " + pending + ".");
                return true;
            }

            private void ConfirmStartedSettings(MissionLifecycleNotification notification)
            {
                ModSettingsApplication.ConfirmStarted();
                if (trailContext && notification?.Context != null) settingsSession.ConfirmStarted(notification.Context.SessionId);
            }

            private static string BuildRestartContext(string identity, ModSettingsDefinition document, string contentPath = null)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    string contentFingerprint = "";
                    if (contentPath != null)
                        using (var content = File.OpenRead(contentPath))
                            contentFingerprint = BitConverter.ToString(sha.ComputeHash(content));
                    byte[] digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(ModSettingsJson.Serialize(document) + contentFingerprint));
                    return identity + "|" + BitConverter.ToString(digest).Replace("-", "");
                }
            }

            internal bool TryPrepareRestartSettings(Action<string> onBlocked = null)
            {
                string reason;
                try
                {
                    if (ModSettingsApplication.PrepareLaunch()) return true;
                    reason = SerpLocalization.Get("ExtendedData.SettingsRestartRequired");
                }
                catch (Exception error)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:845", "Configuration launch preparation failed: " + error);
                    reason = error.GetBaseException().Message;
                }
                // Button callers use their contextual reporter; other launch routes retain one dialog.
                if (onBlocked != null) onBlocked(reason);
                else
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData.ConfigurationLaunchBlocked", reason);
                    ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"), reason);
                }
                RecoverCoopDraft();
                return false;
            }

            internal void PrepareCoopMissionLaunch()
            {
                if (MissionRestart.IsInProgress) return;
                missionPresetLifecycle.Prepare(MissionPresetLaunchKind.CoopTrail);
            }

            internal bool PrepareTrailMakerTestLaunch()
            {
                if (MissionRestart.IsInProgress) return true;
                if (!enabled)
                {
                    if (!trailMakerAuthoringActive || !trailContext) return true;
                    // An editor draft is editable metadata; it must not become active game rules
                    // when the host has disabled mission-setting application.
                    ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"),
                        SerpLocalization.Get("ExtendedData.MissionSettingsRequired"));
                    return false;
                }
                if (missionPresetLifecycle.PendingLaunch == MissionPresetLaunchKind.TrailMakerTest)
                    return true;
                try
                {
                    CaptureTrailMakerWorkingDocument("test launch");
                    ApplyDocument(
                        trailMakerWorkingDocument ?? ModSettingsDefinition.CreateModDefaults(),
                        editable: false,
                        preserveCurrentValues: true);
                    missionPresetLifecycle.Prepare(MissionPresetLaunchKind.TrailMakerTest);
                    return true;
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:874", "Could not prepare Trail Maker test mod settings: " + exception);
                    ExitContext(force: true);
                    ShowInformation(
                        SerpLocalization.Get("ExtendedData.StartBlockedTitle"),
                        SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + exception.Message);
                    return false;
                }
            }

            private static MissionPresetEndKind MapEndKind(MissionEndReason reason)
            {
                switch (reason)
                {
                    case MissionEndReason.Replaced: return MissionPresetEndKind.Replaced;
                    case MissionEndReason.Unloaded: return MissionPresetEndKind.Unloaded;
                    case MissionEndReason.SceneChanged: return MissionPresetEndKind.SceneChanged;
                    case MissionEndReason.Failed: return MissionPresetEndKind.Failed;
                    case MissionEndReason.Exception: return MissionPresetEndKind.Exception;
                    case MissionEndReason.ApplicationExit: return MissionPresetEndKind.ApplicationExit;
                    default: return MissionPresetEndKind.Unloaded;
                }
            }

            private void SaveCustomTrailMapHook(
                EditorDirector self,
                string mapPath,
                string mapName,
                string trailPath,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
            {
                if (!EditorEnabled)
                {
                    saveCustomTrailMapOriginal(self, mapPath, mapName, trailPath, restartInfo);
                    return;
                }
                ModSettingsDefinition document = null;
                try
                {
                    // Vanilla unloads and rebuilds the editor inside the original save call.
                    // Capture synchronously before invoking it so every save uses its own visible values.
                    document = CaptureDocument();
                    string[] mentionedMods = document.Mods.Keys.ToArray();
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:916", "Captured Trail mod settings before save; mentioned=[" + string.Join(", ", mentionedMods) + "].");
                    if (editorSaveOptions.IncludeTrailModSettings)
                    {
                        // Vanilla can enter Trail export before this save call returns. Keep the
                        // synchronous capture available to both exporters until it reaches disk.
                        capturedDocumentsByTrailPath[IOPath.GetFullPath(trailPath)] = document;
                    }
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:928", $"Could not capture the Trail Maker working settings before saving [{trailPath}]: {exception}");
                    ShowInformation(
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailedTitle"),
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailed"));
                    return;
                }

                saveCustomTrailMapOriginal(self, mapPath, mapName, trailPath, restartInfo);
                string sidecar = MissionLoader.GetTrailModSettingsPath(trailPath);
                try
                {
                    if (!File.Exists(trailPath))
                        throw new FileNotFoundException("The game did not create the expected Trail mission.", trailPath);
                    if (editorSaveOptions.IncludeTrailModSettings)
                    {
                        ModSettingsJson.WriteAtomic(sidecar, document);
                        workingSourceContextId = "trail:" + IOPath.GetFullPath(sidecar);
                        trailSource.Set(document, workingSourceContextId);
                    }
                    else
                    {
                        if (File.Exists(sidecar))
                            File.Delete(sidecar);
                        trailSource.Clear();
                    }
                    SourcesChanged?.Invoke();
                    capturedDocumentsByTrailPath.Remove(IOPath.GetFullPath(trailPath));
                    DebugLogHelper.LogInfo(
                        log,
                        editorSaveOptions.IncludeTrailModSettings
                            ? $"Saved Trail mod settings beside [{trailPath}]."
                            : $"Saved Trail mission without a mod-settings sidecar [{trailPath}].");
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:964", $"Could not publish the selected Trail mod-settings state for [{trailPath}]: {exception}");
                    ShowInformation(
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailedTitle"),
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailed"));
                    return;
                }

                try
                {
                    // Keep the just-saved mission editable even if Vanilla rebuilt the UI.
                    if (enabled) ApplyDocument(document, editable: true);
                    if (editorSaveOptions.IncludeTrailModSettings)
                    {
                        var info = new FileInfo(sidecar);
                        activeSidecarPath = sidecar;
                        activeSidecarLength = info.Length;
                        activeSidecarWriteTicks = info.LastWriteTimeUtc.Ticks;
                    }
                    else
                    {
                        activeSidecarPath = null;
                        activeSidecarLength = -1;
                        activeSidecarWriteTicks = 0;
                    }
                    activeSidecarEditable = true;
                    activeSidecarPreviewOnly = false;
                    UpdateTrailMakerWorkingDocument(document, trailPath);
                    missionPresetLifecycle.CompleteTrailMakerReturn();
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:995", $"Saved Trail mod settings for [{trailPath}], but could not reactivate the editable Trail preset: {exception}");
                }
            }

            private void ManageTrailButtonHook(FRONT_ManageTrail self, string command)
            {
                if (!EditorEnabled)
                {
                    manageTrailButtonOriginal(self, command);
                    return;
                }
                if (string.Equals(command, "DoImport", StringComparison.Ordinal) &&
                    (self.FindName("ImportList") as ListView)?.SelectedItem is FileRow row)
                {
                    bool mapped = coopImportSourceBySelection.TryGetValue(row.Text1, out string source);
                    source = mapped ? source : IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), row.Text1);
                    if (mapped || File.Exists(IOPath.Combine(source, "cooptrail.json")))
                    {
                        CoopTrailMakerSources.RequestImport(source,
                            () => manageTrailButtonOriginal(self, command),
                            exception =>
                            {
                                Shared.DebugLogHelper.LogErrorLimited(log, "trail-maker-import",
                                    "Could not import Coop Trail [" + source + "]: " + exception);
                                ShowInformation(SerpLocalization.Get("ExtendedData.SidecarsIncompleteTitle"),
                                    SerpLocalization.Get("ExtendedData.ErrorPackageInvalid") + " " + exception.Message);
                                // The Maker lives on the MP frontend, just like its export error popup.
                                MainViewModel.Instance.Show_HUD_Confirmation = false;
                                MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
                                MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
                            });
                        return;
                    }
                }
                FileHeader loadedHeader = null;
                if (string.Equals(command, "Load", StringComparison.Ordinal))
                {
                    try
                    {
                        int selected = (int)typeof(FRONT_ManageTrail)
                            .GetField("SelectedMission", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(self);
                        loadedHeader = MapFileManager.Instance.GetHeaderFromTrailMaker(
                            FRONT_ManageTrail.GetMakerFileName(selected));
                    }
                    catch (Exception exception)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1021", $"Could not load editable Trail mod settings: {exception}");
                    }
                }
                pendingTrailMakerLoad = string.Equals(command, "Load", StringComparison.Ordinal);
                pendingTrailMakerTrailPath = loadedHeader?.filePath;
                try
                {
                    if (pendingTrailMakerLoad)
                    {
                        try
                        {
                            // The Maker's Vanilla reader rejects absent source maps before Open.
                            // Decode the portable setup here, preserving its embedded Lord data.
                            HUD_IngameMenu.RestartSkirmishMapInfo restart;
                            // Keep both readers on the same source bytes; an external edit or
                            // replacement must not combine one setup with a different map.
                            using (var sourceLock = File.OpenRead(pendingTrailMakerTrailPath))
                            {
                                FileHeader container = MapFileManager.Instance.GetFileInfoFromFileName(
                                    pendingTrailMakerTrailPath, pendingTrailMakerTrailPath, 4, loadRestartInfo: true);
                                restart = container?.restartSkirmishInfo ??
                                    throw new InvalidDataException("The Trail mission has no readable saved setup.");
                                embeddedMakerMapHeader = null;
                                if (restart.selectedHeader == null)
                                    restart.selectedHeader = embeddedMakerMapHeader = TrailMakerEmbeddedMap.Prepare(
                                        pendingTrailMakerTrailPath,
                                        IOPath.Combine(BepInEx.Paths.CachePath, "ExtendedData", "TrailMaker"),
                                        ReadEmbeddedMakerMap);
                            }
                            FRONT_Multiplayer.Open(skirmishSetup: true, restartInfo: restart, coopSetup: false, trailMaker: true);
                        }
                        catch (Exception exception)
                        {
                            embeddedMakerMapHeader = null;
                            Shared.DebugLogHelper.LogErrorLimited(log, "trail-maker-embedded-map",
                                "Could not open the embedded Trail Maker map: " + exception);
                            ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"),
                                SerpLocalization.Get("ExtendedData.TrailMakerMapUnavailable"));
                        }
                    }
                    else manageTrailButtonOriginal(self, command);
                }
                finally
                {
                    pendingTrailMakerTrailPath = null;
                    pendingTrailMakerLoad = false;
                }

                if (string.Equals(command, "Import", StringComparison.Ordinal))
                {
                    TryFileOperation("add Coop Trails to Vanilla's import list", () => AddCoopImportRows(self));
                }
                else if (string.Equals(command, "Export", StringComparison.Ordinal))
                {
                    TryFileOperation("add Coop Trails to Vanilla's export list", () => AddCoopExportRows(self));
                }
            }

            private void EditorSetupButtonHook(FRONT_EditorSetup self, string command)
            {
                if (!UploadEnabled)
                {
                    uploadOptions.Close();
                    editorSetupButtonOriginal(self, command);
                    return;
                }

                FileRow selectedRow = (self.FindName("UploadList") as ListView)?.SelectedItem as FileRow;
                bool doUpload = string.Equals(command, "DoUpload", StringComparison.Ordinal);
                bool isTrail = selectedRow?.trail != null;
                bool isCustomLord = selectedRow?.lord != null;
                if (doUpload && ((UploadEnabled && isTrail) || isCustomLord))
                {
                    string uploadRoot = ConfigSettings.GetWorkshopUploadContentPath();
                    string itemName = isTrail ? selectedRow.trail.Name : selectedRow.lord.lordName;
                    if (!WorkshopUploadStaging.TryResetDirectChild(uploadRoot, itemName, out _, out string cleanupError))
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1058", $"Extended Data Workshop staging cleanup failed for [{itemName}]: {cleanupError}");
                        HUD_ConfirmationPopup.ShowOK(
                            SerpLocalization.Get("WorkshopUpload.StagingCleanupFailed"),
                            delegate { });
                        return;
                    }

                    if (isTrail && IsCoopPackageFolder(itemName))
                    {
                        try
                        {
                            UploadCoopTrailPackage(self, selectedRow.trail, uploadOptions.IncludeExtendedData);
                        }
                        catch (Exception exception)
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1073", $"Could not upload the Coop Trail package to Steam Workshop: {exception}");
                            HUD_ConfirmationPopup.ShowOK(
                                Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125),
                                delegate { });
                        }
                        return;
                    }

                    PendingUploadDecision attempt = ArmUploadDecision(uploadRoot, itemName, uploadOptions.IncludeExtendedData);
                    try
                    {
                        editorSetupButtonOriginal(self, command);
                    }
                    catch
                    {
                        ClearUploadDecision(attempt);
                        throw;
                    }
                    return;
                }

                editorSetupButtonOriginal(self, command);

                if (string.Equals(command, "UploadTrail", StringComparison.Ordinal))
                {
                    if (UploadEnabled)
                        TryFileOperation("add Coop Trails to Vanilla's Workshop upload list", () => AddCoopWorkshopRows(self));
                }
                else if (string.Equals(command, "Upload", StringComparison.Ordinal))
                {
                    if (selectedRow?.trail != null && UploadEnabled)
                        uploadOptions.Open(WorkshopUploadOptionKind.Trail);
                    else if (selectedRow?.lord != null)
                        uploadOptions.Open(WorkshopUploadOptionKind.CustomLord);
                    else
                        uploadOptions.Close();
                }
                else if (string.Equals(command, "CloseDoUpload", StringComparison.Ordinal) ||
                         string.Equals(command, "CloseUpload", StringComparison.Ordinal))
                {
                    uploadOptions.Close();
                }
            }

            private void UploadWorkshopMapHook(
                Platform_Workshop instance,
                string nameMap,
                string mapTitle,
                string description,
                string[] tags,
                bool publicMap,
                string previewImage,
                Action successAction,
                Action failAction)
            {
                if (!UploadEnabled)
                {
                    uploadWorkshopMapOriginal(instance, nameMap, mapTitle, description, tags,
                        publicMap, previewImage, successAction, failAction);
                    return;
                }

                PendingUploadDecision decision = GetUploadDecision(nameMap, mapTitle);
                Action terminalSuccess = WrapTerminalCallback(decision, successAction);
                Action terminalFailure = WrapTerminalCallback(decision, failAction);
                if (decision != null && !decision.IncludeExtendedData)
                {
                    uploadWorkshopMapOriginal(instance, nameMap, mapTitle, description, tags,
                        publicMap, previewImage, terminalSuccess, terminalFailure);
                    return;
                }
                CustomLordJsonUploadMode lordMode = CustomLordJsonUploadPolicy.Classify(tags);
                if (lordMode == CustomLordJsonUploadMode.CustomLord)
                {
                    customLordUploadWorkflow.Handle(
                        new CustomLordUploadRequest(
                            instance,
                            nameMap,
                            mapTitle,
                            description,
                            tags,
                            publicMap,
                            previewImage,
                            terminalSuccess,
                            terminalFailure,
                            decision?.IncludeExtendedData ?? true),
                        request => uploadWorkshopMapOriginal(
                            request.Instance,
                            request.NameMap,
                            request.MapTitle,
                            request.Description,
                            request.Tags,
                            request.PublicMap,
                            request.PreviewImage,
                            request.SuccessAction,
                            request.FailAction),
                        () => IsCurrentUploadDecision(decision));
                    return;
                }

                if (lordMode == CustomLordJsonUploadMode.ExtendedCpuLord)
                {
                    try
                    {
                        if (!TryResolveExtendedLordJsonSource(
                                mapTitle,
                                tags,
                                out string source,
                                out string stagingChild,
                                out string resolutionError))
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1170", $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {resolutionError}");
                            InvokeUploadFailure(mapTitle, terminalFailure);
                            return;
                        }
                        if (!CustomLordWorkshopPackagePolicy.TryStageFiles(
                                source,
                                stagingChild,
                                out int copiedFiles,
                                out int existingFiles,
                                out int packageFiles,
                                out long packageBytes,
                                out string stagingError))
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1185", $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {stagingError}");
                            InvokeUploadFailure(mapTitle, terminalFailure);
                            return;
                        }
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1191", $"Extended CPU Lord staging ready for [{mapTitle}]: {copiedFiles} copied, {existingFiles} already present, {packageFiles} package files, {packageBytes} bytes.");
                    }
                    catch (Exception exception)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1197", $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {exception}");
                        InvokeUploadFailure(mapTitle, terminalFailure);
                        return;
                    }
                }
                else if (IsCustomTrailUpload(tags))
                {
                    string source = IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), mapTitle);
                    string destination = IOPath.Combine(nameMap, mapTitle);
                    if (!WorkshopUploadStaging.TryStageTrailJsonFiles(source, destination,
                            out int requiredFiles, out string requiredError, requirementsOnly: true))
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1211", $"Custom Trail Lord requirements could not be staged for [{mapTitle}]; upload aborted: {requiredError}");
                        InvokeUploadFailure(mapTitle, terminalFailure);
                        return;
                    }
                    if (decision?.IncludeExtendedData == true)
                    {
                        if (!WorkshopUploadStaging.TryStageTrailJsonFiles(source, destination,
                                out int copiedFiles, out string error))
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1222", $"Custom Trail JSON files could not be staged for [{mapTitle}]; upload aborted: {error}");
                            InvokeUploadFailure(mapTitle, terminalFailure);
                            return;
                        }
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1227", $"Added {copiedFiles} Custom Trail JSON file(s) and {requiredFiles} required Lord file(s) to Workshop staging for [{mapTitle}].");
                    }
                }

                uploadWorkshopMapOriginal(
                    instance,
                    nameMap,
                    mapTitle,
                    description,
                    tags,
                    publicMap,
                    previewImage,
                    terminalSuccess,
                    terminalFailure);
            }

            private static bool IsCustomTrailUpload(string[] tags) =>
                tags != null && tags.Any(tag => string.Equals(tag, "Custom Trail", StringComparison.Ordinal));

            private void InvokeUploadFailure(string mapTitle, Action failAction)
            {
                try
                {
                    failAction?.Invoke();
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1255", $"Vanilla's Workshop failure callback failed for [{mapTitle}]: {exception}");
                }
            }

            private PendingUploadDecision ArmUploadDecision(string root, string itemName, bool includeExtendedData)
            {
                lock (uploadDecisionLock)
                    return pendingUploadDecision = new PendingUploadDecision(root, itemName, includeExtendedData);
            }

            private PendingUploadDecision GetUploadDecision(string root, string itemName)
            {
                lock (uploadDecisionLock)
                {
                    if (pendingUploadDecision == null || !pendingUploadDecision.Matches(root, itemName))
                        return null;
                    return pendingUploadDecision;
                }
            }

            private Action WrapTerminalCallback(PendingUploadDecision decision, Action callback)
            {
                return () =>
                {
                    lock (uploadDecisionLock)
                        if (ReferenceEquals(pendingUploadDecision, decision)) pendingUploadDecision = null;
                    callback?.Invoke();
                };
            }

            private void ClearUploadDecision(PendingUploadDecision attempt)
            {
                lock (uploadDecisionLock)
                    if (ReferenceEquals(pendingUploadDecision, attempt)) pendingUploadDecision = null;
            }

            private bool IsCurrentUploadDecision(PendingUploadDecision attempt)
            {
                lock (uploadDecisionLock)
                    return ReferenceEquals(pendingUploadDecision, attempt);
            }

            private static bool TryResolveExtendedLordJsonSource(
                string mapTitle,
                string[] tags,
                out string source,
                out string stagingChild,
                out string error)
            {
                source = string.Empty;
                stagingChild = string.Empty;
                int lordType = -1;
                int tagMatches = 0;
                for (int index = 0; index < ConfigSettings.extendedLordPaths.Length; index++)
                {
                    foreach (string tag in tags ?? Array.Empty<string>())
                    {
                        if (string.Equals(tag, ConfigSettings.extendedLordPaths[index], StringComparison.Ordinal))
                        {
                            lordType = index;
                            stagingChild = ConfigSettings.extendedLordPaths[index];
                            tagMatches++;
                        }
                    }
                }
                if (tagMatches != 1)
                {
                    error = "the Extended Lord path tag was not uniquely resolved";
                    return false;
                }

                List<CustomisationFileManager.CustomLordConfig> configs =
                    CustomisationFileManager.Instance.getLordLordList(lordType);
                int sourceMatches = 0;
                if (configs != null)
                {
                    foreach (CustomisationFileManager.CustomLordConfig config in configs)
                    {
                        if (config != null && !config.workshop &&
                            string.Equals(config.name, mapTitle, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(config.path))
                        {
                            source = config.path;
                            sourceMatches++;
                        }
                    }
                }
                error = sourceMatches == 1
                    ? string.Empty
                    : "the local Extended Lord configuration source was not uniquely resolved";
                return sourceMatches == 1;
            }

            private void ManageTrailInitHook(FRONT_ManageTrail self, bool preserveSelection)
            {
                manageTrailInitOriginal(self, preserveSelection);
                if (!EditorEnabled)
                    return;
                EnsureTrailMakerCoopCheckbox(self);
                EnableImportForCoopPackages(self);
                // Vanilla invokes Init again after its confirmation callback deleted a mission.
                TryFileOperation("clean orphan Trail sidecars", DeleteOrphanMakerSidecars);
            }

            private void EnableImportForCoopPackages(FRONT_ManageTrail page)
            {
                if (!GetImportableCoopSources(includeWorkshop: true).Any())
                    return;
                if (page.FindName("Import") is Button importButton)
                {
                    importButton.IsEnabled = true;
                    importButton.Opacity = 1f;
                }
            }

            private void AddCoopImportRows(FRONT_ManageTrail page)
            {
                ListView importList = page.FindName("ImportList") as ListView;
                ObservableCollection<FileRow> rows = importList?.ItemsSource as ObservableCollection<FileRow>;
                if (rows == null)
                    throw new InvalidOperationException("Vanilla's Trail import list is unavailable.");

                coopImportSourceBySelection.Clear();
                AddCoopRows(rows, GetImportableCoopSources(includeWorkshop: true), registerImportSources: true);
            }

            private void AddCoopExportRows(FRONT_ManageTrail page)
            {
                ListView exportList = page.FindName("ExportList") as ListView;
                ObservableCollection<FileRow> rows = exportList?.ItemsSource as ObservableCollection<FileRow>;
                if (rows == null)
                    throw new InvalidOperationException("Vanilla's Trail export list is unavailable.");

                AddCoopRows(rows, GetImportableCoopSources(includeWorkshop: false), registerImportSources: false);
            }

            private void AddCoopWorkshopRows(FRONT_EditorSetup page)
            {
                ListView uploadList = page.FindName("UploadList") as ListView;
                ObservableCollection<FileRow> rows = uploadList?.ItemsSource as ObservableCollection<FileRow>;
                if (rows == null)
                    throw new InvalidOperationException("Vanilla's Trail Workshop upload list is unavailable.");

                var existing = new HashSet<string>(
                    rows.Where(row => row?.trail != null).Select(row => row.trail.Name),
                    StringComparer.OrdinalIgnoreCase);
                foreach (CoopTrailMakerSource source in GetImportableCoopSources(includeWorkshop: false))
                {
                    if (!existing.Add(source.SelectionName))
                        continue;
                    string packageRoot = source.PackageRoot;
                    var trail = new MapFileManager.CustomTrailInfo
                    {
                        Name = source.SelectionName,
                        DisplayName = source.Manifest.DisplayName,
                        FullPath = packageRoot,
                        workshopUploadInfoAvailable = File.Exists(IOPath.Combine(packageRoot, source.SelectionName + ".data")),
                    };
                    // Vanilla derives Count from the headers dictionary. Placeholder keys retain
                    // its existing length display and Short/Medium/Long Workshop categorisation.
                    for (int mission = 1; mission <= source.Manifest.MissionCount; mission++)
                        trail.headers[mission.ToString("00", CultureInfo.InvariantCulture)] = null;
                    var row = new FileRow
                    {
                        Text1 = source.Manifest.DisplayName,
                        Text2 = source.Manifest.MissionCount.ToString(CultureInfo.InvariantCulture),
                        trail = trail,
                    };
                    if (trail.workshopUploadInfoAvailable)
                        row.TypeImage = MainViewModel.Instance.GameSprites[746];
                    rows.Add(row);
                }
            }

            private static bool IsCoopPackageFolder(string folderName)
            {
                if (string.IsNullOrWhiteSpace(folderName) ||
                    !string.Equals(folderName, IOPath.GetFileName(folderName), StringComparison.Ordinal))
                    return false;
                string root = IOPath.GetFullPath(ConfigSettings.GetUserCustomTrailsPath());
                string packageRoot = IOPath.GetFullPath(IOPath.Combine(root, folderName));
                string rootPrefix = root.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar) + IOPath.DirectorySeparatorChar;
                return packageRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(IOPath.Combine(packageRoot, "cooptrail.json"));
            }

            private void UploadCoopTrailPackage(
                FRONT_EditorSetup page,
                MapFileManager.CustomTrailInfo trail,
                bool includeModSettings)
            {
                string source = IOPath.GetFullPath(IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), trail.Name));
                CoopTrailPackage package = CoopTrailPackageCatalog.Load(source);
                string uploadContent = ConfigSettings.GetWorkshopUploadContentPath();
                string destination = IOPath.Combine(uploadContent, trail.Name);
                CoopTrailPackage stagedPackage = CoopWorkshopPackageStaging.Stage(
                    package,
                    destination,
                    trail.Name + ".data",
                    includeModSettings,
                    out int copiedModSettings);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1455", $"Prepared Coop Trail Workshop package [{trail.Name}] with " +
                        $"mod-settings included={includeModSettings}, sidecars={copiedModSettings}, " +
                        $"fingerprint={stagedPackage.Manifest.ContentFingerprint}.");

                var tags = new List<string> { "Custom Trail" };
                string previewName;
                if (package.Manifest.MissionCount <= 20)
                {
                    previewName = "Short.png";
                    tags.Add("Short (1-20)");
                }
                else if (package.Manifest.MissionCount <= 30)
                {
                    previewName = "Medium.png";
                    tags.Add("Medium (21-30)");
                }
                else
                {
                    previewName = "Long.png";
                    tags.Add("Long (31-50)");
                }

                string previewSource = IOPath.Combine(UnityEngine.Application.streamingAssetsPath, "WorkshopImages", previewName);
                string uploadImage = IOPath.Combine(ConfigSettings.GetWorkshopUploadRootPath(), "Upload.png");
                File.Copy(previewSource, uploadImage, true);
                TextBox descriptionBox = page.FindName("WorkshopMapDescription") as TextBox;
                Grid uploadPanel = page.FindName("UploadPanel") as Grid;
                if (descriptionBox == null || uploadPanel == null)
                    throw new InvalidOperationException("Vanilla's Workshop uploader controls are unavailable.");

                string description = descriptionBox.Text;
                MainViewModel.Instance.Show_EditorWorkshop_Uploader = false;
                FRONT_EditorSetup.canCloseWorkshop = false;
                uploadPanel.Visibility = Visibility.Visible;
                Platform_Workshop.Instance.UploadWorkshopMap(
                    uploadContent,
                    trail.Name,
                    description,
                    tags.ToArray(),
                    true,
                    uploadImage,
                    delegate
                    {
                        ulong publishId = Platform_Workshop.Instance.GetPublishID();
                        File.WriteAllText(
                            IOPath.Combine(source, trail.Name + ".data"),
                            publishId + "\n0\n" + description);
                        trail.workshopUploadInfoAvailable = true;
                        HUD_ConfirmationPopup.ShowOK(
                            Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 124),
                            delegate
                            {
                                FRONT_EditorSetup.canCloseWorkshop = true;
                                uploadPanel.Visibility = Visibility.Hidden;
                                page.ButtonClicked("UploadTrail");
                            });
                    },
                    delegate
                    {
                        HUD_ConfirmationPopup.ShowOK(
                            Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125),
                            delegate
                            {
                                FRONT_EditorSetup.canCloseWorkshop = true;
                                uploadPanel.Visibility = Visibility.Hidden;
                            });
                    });
            }

            private void AddCoopRows(
                ObservableCollection<FileRow> rows,
                IEnumerable<CoopTrailMakerSource> sources,
                bool registerImportSources)
            {
                var existing = new HashSet<string>(
                    rows.Where(row => row != null).Select(row => row.Text1),
                    StringComparer.OrdinalIgnoreCase);
                foreach (CoopTrailMakerSource source in sources)
                {
                    if (!existing.Add(source.SelectionName))
                    {
                        // Vanilla already lists legacy local packages with root-level .trail
                        // files. Retain their identity too if the manifest disappears later.
                        if (registerImportSources && !source.Workshop)
                            coopImportSourceBySelection[source.SelectionName] = source.PackageRoot;
                        continue;
                    }
                    if (registerImportSources)
                        coopImportSourceBySelection[source.SelectionName] = source.PackageRoot;
                    rows.Add(new FileRow
                    {
                        Text1 = source.SelectionName,
                        Text2 = source.MissionCount.ToString(CultureInfo.InvariantCulture),
                        // Public Vanilla metadata lets UI consumers distinguish local packages
                        // from subscriptions without relying on names or Steam icons.
                        trail = new MapFileManager.CustomTrailInfo
                        {
                            Name = source.SelectionName,
                            FullPath = source.PackageRoot,
                            workshop = source.Workshop,
                        },
                    });
                }
            }

            private readonly CoopTrailMakerSources makerSources = new CoopTrailMakerSources();

            private IReadOnlyList<CoopTrailMakerSource> GetImportableCoopSources(bool includeWorkshop)
            {
                string localRoot = IOPath.GetFullPath(ConfigSettings.GetUserCustomTrailsPath());
                var roots = new List<string> { localRoot };
                if (includeWorkshop)
                    roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(message =>
                        Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1562", message)));
                return makerSources.Discover(roots, localRoot, message =>
                    Shared.DebugLogHelper.LogWarningLimited(log, "trail-maker-discovery", message));
            }

            private void BackupHook(FRONT_ManageTrail self, string source, string destination)
            {
                backupOriginal(self, source, destination);
                if (!EditorEnabled)
                    return;
                TryFileOperation("back up Trail sidecars", () =>
                    ReportSkippedSidecars("ExtendedData.BackupSidecarsIncomplete",
                        CopySidecars(source, destination, overwrite: true)));
                TryFileOperation("back up the Coop Trail Maker marker", () => CopyCoopMarker(source, destination));
                TryFileOperation("back up the Coop Trail package", () => CopyCoopPackage(source, destination));
            }

            private void ImportHook(FRONT_ManageTrail self, string customFolderName)
            {
                if (!EditorEnabled)
                {
                    importOriginal(self, customFolderName);
                    return;
                }
                string source = coopImportSourceBySelection.TryGetValue(customFolderName, out string mappedSource)
                    ? mappedSource
                    : IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), customFolderName);
                bool coopPackage = File.Exists(IOPath.Combine(source, "cooptrail.json"));
                string trailSource = GetCoopTrailMakerSource(source, coopPackage);
                bool mappedImport = coopImportSourceBySelection.ContainsKey(customFolderName);
                string vanillaImportFolder = mappedImport
                    ? trailSource
                    : string.Equals(trailSource, source, StringComparison.OrdinalIgnoreCase)
                        ? customFolderName
                        : IOPath.Combine(customFolderName, CoopTrailMakerSourceDirectory);
                // Use Vanilla itself for .trail files. Its File.Copy call does not overwrite,
                // and any name collision aborts before sidecars or the Coop marker are changed.
                importOriginal(self, vanillaImportFolder);
                TryFileOperation("import Trail sidecars", () =>
                    ReportSkippedSidecars("ExtendedData.ImportSidecarsIncomplete",
                        CopySidecars(trailSource, ConfigSettings.GetUserTrailMakerPath(), overwrite: false)));
                TryFileOperation("import the Coop Trail Maker state", () =>
                {
                    SetMakerCoopEnabled(coopPackage);
                    RefreshTrailMakerCoopCheckbox();
                });
            }

            private void ExportHook(FRONT_ManageTrail self, string destination)
            {
                if (!EditorEnabled)
                {
                    exportOriginal(self, destination);
                    return;
                }
                ExportTrail(self, destination, allowEmbeddedValuesOnly: false);
            }

            // Every mission's Lord requirements and the Coop package are prepared before any destination
            // file is replaced. The Coop package and its editable Maker sources are published together.
            private void ExportTrail(FRONT_ManageTrail self, string destination, bool allowEmbeddedValuesOnly)
            {
                string makerRoot = ConfigSettings.GetUserTrailMakerPath();
                bool exportCoop = IsMakerCoopEnabled();
                var makerRequirements = new Dictionary<string, IReadOnlyList<TrailLordSlot>>(StringComparer.OrdinalIgnoreCase);
                CoopTrailPackageExporter.PreparedPackage prepared = null;
                try
                {
                    if (exportCoop)
                        prepared = new CoopTrailPackageExporter().Prepare(
                            makerRoot,
                            destination,
                            ReadModSettingsForExport,
                            (trail, restart, aiPlayerIds) => ResolveMakerRequirements(trail, restart, aiPlayerIds,
                                allowEmbeddedValuesOnly, makerRequirements));
                    // Also Maker missions beyond the 40 Coop missions keep bound requirements.
                    PrepareMakerRequirements(makerRoot, allowEmbeddedValuesOnly, makerRequirements, exportCoop);
                }
                catch (Exception exception)
                {
                    // A later Maker mission can fail after the Coop package was already staged.
                    prepared?.Dispose();
                    TrailLordAuthoringUnavailableException gap = FindAuthoringGap(exception);
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1663", "Could not prepare Trail export: " + exception);
                    if (gap != null && !allowEmbeddedValuesOnly)
                        OfferEmbeddedValuesOnlyExport(self, destination, exception as CoopMissionExportException, gap);
                    else
                        ShowExportFailure(exception as CoopMissionExportException);
                    return;
                }

                try
                {
                    if (prepared != null)
                    {
                        string trailMakerSource = prepared.TrailMakerSourceStaging;
                        Directory.CreateDirectory(trailMakerSource);
                        exportOriginal(self, trailMakerSource);
                        ExportSidecars(trailMakerSource);
                        WriteLordRequirements(trailMakerSource, makerRoot, makerRequirements);
                        prepared.Publish(destination);
                        RemoveNormalTrailFiles(destination);
                        DebugLogHelper.LogInfo(log, "Published Coop Trail package [" + prepared.Package.Manifest.DisplayName +
                            "] with " + prepared.Package.Manifest.MissionCount +
                            " mission(s); editable Trail Maker sources were stored below [" + CoopTrailMakerSourceDirectory + "].");
                    }
                    else
                    {
                        exportOriginal(self, destination);
                        ExportSidecars(destination);
                        WriteLordRequirements(destination, makerRoot, makerRequirements);
                        RemoveCoopPackage(destination);
                    }
                    CoopPackageExported?.Invoke();
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1695", "Could not finish Trail export: " + exception);
                    ShowExportFailure();
                }
                finally
                {
                    prepared?.Dispose();
                }
            }

            private IReadOnlyList<TrailLordSlot> ResolveMakerRequirements(string trailPath,
                HUD_IngameMenu.RestartSkirmishMapInfo restart, ISet<int> includedPlayerIds,
                bool allowEmbeddedValuesOnly, Dictionary<string, IReadOnlyList<TrailLordSlot>> resolved)
            {
                TrailLordRequirementSource.Resolution resolution = TrailLordRequirementSource.Resolve(
                    trailPath, restart, includedPlayerIds, allowEmbeddedValuesOnly);
                resolved[IOPath.GetFullPath(trailPath)] = resolution.Slots;
                if (resolution.MigratedFixesSources)
                    DebugLogHelper.LogInfo(log, "Legacy Trail Fixes inheritance supplemented with current host/author sources; " +
                        "stored preferences retained: " + IOPath.GetFileName(trailPath));
                if (resolution.ReusedPlayerIds.Count > 0 || resolution.EmbeddedOnlyPlayerIds.Count > 0)
                    DebugLogHelper.LogInfo(log, "Trail Lord requirements for [" + IOPath.GetFileName(trailPath) +
                        "]: stored=[" + string.Join(",", resolution.ReusedPlayerIds) +
                        "], embeddedOnly=[" + string.Join(",", resolution.EmbeddedOnlyPlayerIds) +
                        "], captured=[" + string.Join(",", resolution.InstalledSources.Keys) + "].");
                return resolution.Slots;
            }

            // Maker missions keep their original player IDs. Coop Maker sources describe only the slots
            // that remain AI after the first two occupied slots become host and guest.
            private void PrepareMakerRequirements(string makerRoot, bool allowEmbeddedValuesOnly,
                Dictionary<string, IReadOnlyList<TrailLordSlot>> resolved, bool coopMakerSource)
            {
                foreach (string trail in MakerTrailsInExportOrder(makerRoot))
                {
                    if (resolved.ContainsKey(IOPath.GetFullPath(trail)))
                        continue;
                    FileHeader header = MapFileManager.Instance.GetFileInfoFromFileName(
                        trail, trail, 4, loadRestartInfo: true);
                    if (header?.restartSkirmishInfo == null)
                        throw new InvalidDataException("Trail mission has no Lord data: " + trail);
                    ISet<int> includedPlayerIds = coopMakerSource
                        ? CoopTrailSlotSelection.GetAiPlayerIds(
                            CoopTrailSlotSelection.GetActiveSlots(header.restartSkirmishInfo.lordTypes))
                        : null;
                    ResolveMakerRequirements(trail, header.restartSkirmishInfo, includedPlayerIds,
                        allowEmbeddedValuesOnly, resolved);
                }
            }

            // Vanilla's ExportTrailMissions numbers the existing Maker missions consecutively.
            private static IEnumerable<string> MakerTrailsInExportOrder(string makerRoot)
            {
                for (int sourceIndex = 0; sourceIndex < 50; sourceIndex++)
                {
                    string sourceTrail = IOPath.Combine(makerRoot, FRONT_ManageTrail.GetMakerFileName(sourceIndex) + ".trail");
                    if (File.Exists(sourceTrail))
                        yield return sourceTrail;
                }
            }

            private static TrailLordAuthoringUnavailableException FindAuthoringGap(Exception exception)
            {
                for (Exception current = exception; current != null; current = current.InnerException)
                    if (current is TrailLordAuthoringUnavailableException gap)
                        return gap;
                return null;
            }

            // Explicit authoring decision; the default remains to abort. The decision is stored with
            // the affected slots (embeddedValuesOnly) and is retained by later exports.
            private void OfferEmbeddedValuesOnlyExport(FRONT_ManageTrail self, string destination,
                CoopMissionExportException missionFailure, TrailLordAuthoringUnavailableException gap)
            {
                try
                {
                    string message = SerpLocalization.Get("ExtendedData.ExportEmbeddedOnlyQuestion")
                        .Replace("{File}", IOPath.GetFileName(gap.TrailPath ?? string.Empty))
                        .Replace("{Mission}", missionFailure?.Ordinal.ToString() ?? "-")
                        .Replace("{Lord}", gap.LordName ?? string.Empty)
                        .Replace("{Player}", gap.PlayerId.ToString())
                        .Replace("{Reason}", gap.InnerException?.Message ?? gap.Message);
                    HUD_ConfirmationPopup.ShowConfirmationMessage(
                        SerpLocalization.Get("ExtendedData.ExportEmbeddedOnlyTitle"),
                        delegate
                        {
                            DebugLogHelper.LogInfo(log, "Trail export continues with embedded AI data only after the author's confirmation: [" +
                                gap.TrailPath + "], player " + gap.PlayerId + ".");
                            ExportTrail(self, destination, allowEmbeddedValuesOnly: true);
                            MapFileManager.Instance.RescanCustomTrailsFolder();
                        },
                        delegate { },
                        message,
                        MPConf: true,
                        tall: true);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1790", "Could not show the embedded-data export decision: " + exception);
                    ShowExportFailure(missionFailure);
                }
            }

            private void ExportSidecars(string destination)
            {
                foreach (string stale in Directory.GetFiles(
                    destination,
                    "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                    File.Delete(stale);

                int outputIndex = 0;
                foreach (string sourceTrail in MakerTrailsInExportOrder(ConfigSettings.GetUserTrailMakerPath()))
                {
                    if (TryReadModSettingsForExport(sourceTrail, out ModSettingsDefinition document))
                    {
                        string target = IOPath.Combine(
                            destination,
                            FRONT_ManageTrail.GetMakerFileName(outputIndex) + MissionLoader.ModSettingsFileSuffix);
                        ModSettingsJson.WriteAtomic(target, document);
                    }
                    outputIndex++;
                }
            }

            // Uses the same source-to-output numbering as Vanilla's export and the mod-settings sidecars.
            // The exported Trail is a byte copy of its Maker source, so the prepared slots bind to it.
            private static void WriteLordRequirements(string destination, string makerRoot,
                IReadOnlyDictionary<string, IReadOnlyList<TrailLordSlot>> prepared)
            {
                foreach (string stale in Directory.GetFiles(destination,
                    "Trail_Mission_*" + TrailLordRequirements.Suffix))
                    File.Delete(stale);
                int outputIndex = 0;
                foreach (string sourceTrail in MakerTrailsInExportOrder(makerRoot))
                {
                    string target = IOPath.Combine(destination, FRONT_ManageTrail.GetMakerFileName(outputIndex) + ".trail");
                    outputIndex++;
                    if (!prepared.TryGetValue(IOPath.GetFullPath(sourceTrail), out IReadOnlyList<TrailLordSlot> slots))
                        continue;
                    if (!File.Exists(target))
                        throw new FileNotFoundException("The exported Trail mission is missing.", target);
                    TrailLordRequirements.Create(target, slots).Write(target);
                }
            }

            private ModSettingsDefinition ReadModSettingsForExport(string trailPath)
            {
                return TryReadModSettingsForExport(trailPath, out ModSettingsDefinition document)
                    ? document
                    : ModSettingsDefinition.CreateModDefaults();
            }

            private bool TryReadModSettingsForExport(string trailPath, out ModSettingsDefinition document)
            {
                string fullTrailPath = IOPath.GetFullPath(trailPath);
                if (capturedDocumentsByTrailPath.TryGetValue(fullTrailPath, out document))
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1760", $"Using synchronously captured Trail mod settings for export [{fullTrailPath}].");
                    return true;
                }
                string sidecar = MissionLoader.GetTrailModSettingsPath(fullTrailPath);
                if (File.Exists(sidecar))
                {
                    document = ModSettingsJson.Read(sidecar);
                    return true;
                }
                document = null;
                return false;
            }

            private void ClearMakerHook(FRONT_ManageTrail self)
            {
                clearMakerOriginal(self);
                if (!EditorEnabled)
                    return;
                TryFileOperation("clear Trail sidecars", () =>
                {
                    foreach (string sidecar in Directory.GetFiles(
                        ConfigSettings.GetUserTrailMakerPath(),
                        "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                        File.Delete(sidecar);
                    foreach (string sidecar in Directory.GetFiles(
                        ConfigSettings.GetUserTrailMakerPath(),
                        "Trail_Mission_*" + TrailLordRequirements.Suffix))
                        File.Delete(sidecar);
                    SetMakerCoopEnabled(false);
                    RefreshTrailMakerCoopCheckbox();
                });
            }

            private void EnsureTrailMakerCoopCheckbox(FRONT_ManageTrail page)
            {
                if (page == null || coopTrailExportCheckbox != null)
                    return;
                CheckBox anchor = page.FindName("ExportBackup") as CheckBox;
                Panel host = anchor == null ? null : VisualTreeHelper.GetParent(anchor) as Panel;
                if (anchor == null || host == null)
                    return;
                Thickness rowMargin = anchor.Margin;
                var checkbox = new CheckBox
                {
                    Name = "ExtendedDataCoopExport",
                    Content = SerpLocalization.Get("ExtendedData.TrailMakerCoop"),
                    ToolTip = SerpLocalization.Get("ExtendedData.TrailMakerCoopHelp"),
                    Foreground = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 0, 0, 0)),
                    FontSize = 20,
                    Style = anchor.Style,
                    Height = anchor.Height,
                    Margin = new Thickness(28, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = IsMakerCoopEnabled(),
                    Visibility = EditorEnabled ? Visibility.Visible : Visibility.Collapsed,
                };
                ToolTipService.SetShowDuration(checkbox, 60000);
                checkbox.Click += (_, __) =>
                {
                    try
                    {
                        if (!EditorEnabled) { checkbox.IsChecked = IsMakerCoopEnabled(); return; }
                        SetMakerCoopEnabled(checkbox.IsChecked == true);
                    }
                    catch (Exception exception)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1822", "Could not save Coop Trail Maker state: " + exception);
                        checkbox.IsChecked = IsMakerCoopEnabled();
                    }
                };

                // Keep both export options in one centered row. This remains readable with
                // localized labels and avoids consuming the vertical space above Backup.
                var optionRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = rowMargin,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                host.Children.Remove(anchor);
                anchor.Margin = new Thickness(0);
                anchor.HorizontalAlignment = HorizontalAlignment.Left;
                anchor.VerticalAlignment = VerticalAlignment.Center;
                optionRow.Children.Add(anchor);
                optionRow.Children.Add(checkbox);
                host.Children.Add(optionRow);
                coopTrailExportCheckbox = checkbox;
            }

            private static string GetCoopMarkerPath(string root) => IOPath.Combine(root, "cooptrail.enabled");

            private static bool IsMakerCoopEnabled() =>
                File.Exists(GetCoopMarkerPath(ConfigSettings.GetUserTrailMakerPath()));

            private static void SetMakerCoopEnabled(bool value)
            {
                string marker = GetCoopMarkerPath(ConfigSettings.GetUserTrailMakerPath());
                if (value)
                    File.WriteAllText(marker, "enabled\r\n", new System.Text.UTF8Encoding(false));
                else if (File.Exists(marker))
                    File.Delete(marker);
            }

            private void RefreshTrailMakerCoopCheckbox()
            {
                if (coopTrailExportCheckbox != null)
                    coopTrailExportCheckbox.IsChecked = IsMakerCoopEnabled();
            }

            private static void CopyCoopMarker(string source, string destination)
            {
                string marker = GetCoopMarkerPath(source);
                if (File.Exists(marker))
                    File.Copy(marker, GetCoopMarkerPath(destination), true);
            }

            private static void CopyCoopPackage(string source, string destination)
            {
                string manifest = IOPath.Combine(source, "cooptrail.json");
                string missions = IOPath.Combine(source, "CoopMissions");
                if (!File.Exists(manifest) || !Directory.Exists(missions))
                    return;
                File.Copy(manifest, IOPath.Combine(destination, "cooptrail.json"), true);
                CopyDirectory(missions, IOPath.Combine(destination, "CoopMissions"));
                string trailMakerSource = IOPath.Combine(source, CoopTrailMakerSourceDirectory);
                if (Directory.Exists(trailMakerSource))
                    CopyDirectory(trailMakerSource, IOPath.Combine(destination, CoopTrailMakerSourceDirectory));
            }

            private static string GetCoopTrailMakerSource(string packageRoot, bool coopPackage)
            {
                if (!coopPackage)
                    return packageRoot;
                return CoopTrailMakerSources.GetSourceDirectory(packageRoot);
            }

            private static void RemoveNormalTrailFiles(string destination)
            {
                foreach (string trail in Directory.GetFiles(destination, "*.trail"))
                    File.Delete(trail);
                foreach (string sidecar in Directory.GetFiles(
                    destination,
                    "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                    File.Delete(sidecar);
                foreach (string sidecar in Directory.GetFiles(
                    destination,
                    "Trail_Mission_*" + TrailLordRequirements.Suffix))
                    File.Delete(sidecar);
            }

            // A partial sidecar transfer must not look like a complete import or backup.
            private void ReportSkippedSidecars(string messageKey, List<string> skipped)
            {
                if (skipped == null || skipped.Count == 0)
                    return;
                ShowInformation(SerpLocalization.Get("ExtendedData.SidecarsIncompleteTitle"),
                    SerpLocalization.Get(messageKey).Replace("{Files}", string.Join(", ", skipped)));
            }

            private static void CopyDirectory(string source, string destination)
            {
                Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(source))
                    File.Copy(file, IOPath.Combine(destination, IOPath.GetFileName(file)), true);
                foreach (string directory in Directory.GetDirectories(source))
                    CopyDirectory(directory, IOPath.Combine(destination, IOPath.GetFileName(directory)));
            }

            private static void RemoveCoopPackage(string destination)
            {
                string root = IOPath.GetFullPath(destination);
                string manifest = IOPath.Combine(root, "cooptrail.json");
                string missions = IOPath.Combine(root, "CoopMissions");
                string trailMakerSource = IOPath.Combine(root, CoopTrailMakerSourceDirectory);
                if (File.Exists(manifest))
                    File.Delete(manifest);
                if (Directory.Exists(missions))
                    Directory.Delete(missions, true);
                if (Directory.Exists(trailMakerSource))
                    Directory.Delete(trailMakerSource, true);
            }

            private void ShowExportFailure(CoopMissionExportException missionFailure = null)
            {
                try
                {
                    // Name the failing mission and its concrete cause; file errors are not
                    // reported as an unexplained multiplayer incompatibility.
                    string message = missionFailure == null
                        ? SerpLocalization.Get("ExtendedData.ExportFailedLog")
                        : SerpLocalization.Get("ExtendedData.ExportFailedMission")
                            .Replace("{Mission}", missionFailure.Ordinal.ToString())
                            .Replace("{File}", IOPath.GetFileName(missionFailure.SourcePath ?? string.Empty))
                            .Replace("{Reason}", missionFailure.Reason);
                    HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                        SerpLocalization.Get("ExtendedData.ExportFailedTitle"), delegate { },
                        message);
                    MainViewModel.Instance.Show_HUD_Confirmation = false;
                    MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
                    MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1941", "Could not display the Trail export error popup: " + exception);
                }
            }
            private static void ShowInformation(string title, string message)
            {
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(title, delegate { }, message);
            }

            private void StartCustomTrailHook(MainViewModel self, string trailName, int missionId, int difficulty)
            {
                bool customizedRestart = false;
                if (!preserveContextForLaunch && !MissionRestart.IsInProgress)
                {
                    HUD_IngameMenu.RestartSkirmishMapInfo restartInfo =
                        MainViewModel.Instance?.HUDIngameMenu?.restartSkirmishMapInfo;
                    customizedRestart =
                        ExtendedDataLaunchOriginApi.Origin ==
                            ExtendedDataLaunchOriginKind.CustomizedCustomTrail &&
                        restartInfo?.customTrail == true &&
                        restartInfo.customTrailLevel == missionId &&
                        string.Equals(restartInfo.customTrailName, trailName, StringComparison.Ordinal);
                    if (customizedRestart)
                        ExtendedDataLaunchOriginApi.MarkRestartPending();
                    else
                        ExtendedDataLaunchOriginApi.Clear();
                }
                if (!enabled && !activation.LordDataIntegration)
                {
                    if (!TryPrepareRestartSettings()) return;
                    startCustomTrailOriginal(self, trailName, missionId, difficulty);
                    return;
                }
                if (enabled && !preserveContextForLaunch && !MissionRestart.IsInProgress)
                {
                    launchedThroughCustomize = false;
                    try
                    {
                        FileHeader header = ResolveCustomTrailHeader(trailName, missionId);
                        bool validSidecar = EnterSidecar(header.filePath, editable: false);
                        if (validSidecar && !customizedRestart)
                        {
                            ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(
                                FrontendMenus.CurrentSelectedTrail,
                                missionId);
                        }
                    }
                    catch (Exception exception)
                    {
                        ExtendedDataLaunchOriginApi.Clear();
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:1990", $"Could not prepare Custom Trail mod settings: {exception}");
                        if (ModSettingsApplication.HasApplicationEndpoints || ModSettingsApplication.HasRestartPreparation || ModSettingsApplication.HasActivationFailures)
                        {
                            ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"), exception.GetBaseException().Message);
                            return;
                        }
                        ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable: false);
                    }
                }
                else if (enabled && workingContextEditable && !MissionRestart.IsInProgress)
                {
                    // Customize is an editable draft. The launched mission receives the
                    // materialized result as a read-only working snapshot.
                    FreezeWorkingDraft();
                }
                try
                {
                    FileHeader lordHeader = ResolveCustomTrailHeader(trailName, missionId);
                    TrailLordRequirements requirements = TrailLordRequirements.Read(lordHeader.filePath);
                    if (requirements == null)
                        LordDataCoordinator?.SetEmbeddedTrailSlots(null);
                    else
                    {
                        FileHeader detailed = MapFileManager.Instance.GetFileInfoFromFileName(
                            lordHeader.filePath, lordHeader.filePath, 4, loadRestartInfo: true);
                        if (detailed?.restartSkirmishInfo?.aivs == null)
                            throw new InvalidDataException("Custom Trail Lord data is unavailable.");
                        LordDataCoordinator?.SetEmbeddedTrailSlots(
                            requirements.Slots.Select(slot => slot.PlayerId));
                        PrepareCustomTrailLords(requirements,
                            ToLordInfoMap(detailed.restartSkirmishInfo.aivs), false);
                    }
                    if (requirements == null)
                        LordDataCoordinator?.PrepareTrail(null, null, false, out _);
                }
                catch (Exception exception)
                {
                    LordDataCoordinator?.SetEmbeddedTrailSlots(null);
                    LordDataCoordinator?.PrepareTrail(null, null, false, out _);
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2029", "Blocked Custom Trail launch: " + exception);
                    ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"), exception.Message);
                    RecoverCoopDraft();
                    return;
                }
                if (!TryPrepareRestartSettings()) return;
                preserveContextForLaunch = false;
                customTrailLaunchActive = true;
                if (!MissionRestart.IsInProgress)
                    missionPresetLifecycle.Prepare(MissionPresetLaunchKind.CustomTrail);
                try { startCustomTrailOriginal(self, trailName, missionId, difficulty); }
                catch { RecoverCoopDraft(); throw; }
            }

            private void LaunchCustomTrailHook(FRONT_Multiplayer self,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo, FileHeader headerToUse)
            {
                LordDataCoordinator?.RemapTrailMedia(ToLordInfoMap(restartInfo?.aivs));
                launchCustomTrailOriginal(self, restartInfo, headerToUse);
            }

            private static Dictionary<int, FRONT_Multiplayer.MPAIVInfo> ToLordInfoMap(
                FRONT_Multiplayer.MPAIVInfo[] aivs) =>
                (aivs ?? Array.Empty<FRONT_Multiplayer.MPAIVInfo>())
                    .Select((info, index) => new { info, index })
                    .ToDictionary(item => item.index + 1, item => item.info);

            private void PrepareCustomTrailLords(TrailLordRequirements requirements,
                IReadOnlyDictionary<int, FRONT_Multiplayer.MPAIVInfo> infos,
                bool captureLocalReplacements)
            {
                if (LordDataCoordinator == null)
                    throw new InvalidDataException("Custom Trail Lord validation is unavailable.");
                if (!LordDataCoordinator.PrepareTrail(requirements, infos, false,
                        out string reason, captureLocalReplacements))
                    throw new InvalidDataException(reason);
            }

            private void MultiplayerOpenHook(
                FRONT_Multiplayer self,
                bool skirmishSetup,
                bool fromNew,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo,
                bool coopSetup,
                bool trailMaker,
                int customiseTrailType,
                int customiseTrailId)
            {
                // Origin tracking is shared infrastructure for the other gameplay mods and must
                // remain active even when ExtendedData's own visible features are disabled.
                bool preserve = enabled && preserveContextForLaunch;
                if (EditorEnabled && trailMaker && !pendingTrailMakerLoad &&
                    trailMakerAuthoringActive && trailContext)
                {
                    CaptureTrailMakerWorkingDocument("lobby rebuild");
                    missionPresetLifecycle.AwaitTrailMakerReturn();
                }
                if (!trailMaker)
                    ClearTrailMakerAuthoringState();
                if (!trailMaker && !preserve)
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    if (enabled)
                        ExitContext(force: true);
                }
                multiplayerOpenOriginal(
                    self,
                    skirmishSetup,
                    fromNew,
                    restartInfo,
                    coopSetup,
                    trailMaker,
                    customiseTrailType,
                    customiseTrailId);
                // FrontendMenus may defer Customize behind a confirmation dialog. Confirm the
                // state written by Vanilla so an internally caught doOpen failure stays closed.
                if (IsConfirmedBuiltInCustomizeTransition(
                    self,
                    fromNew,
                    skirmishSetup,
                    restartInfo,
                    coopSetup,
                    trailMaker,
                    customiseTrailType,
                    customiseTrailId))
                {
                    CaptureBuiltInCustomizeOrigin(customiseTrailType, customiseTrailId);
                }
                if (!enabled && !(EditorEnabled && trailMaker))
                {
                    ObserveCoopLobby(self);
                    LobbyOpened?.Invoke(self);
                    return;
                }
                if (preserve)
                    preserveContextForLaunch = false;
                if (trailMaker && EditorEnabled)
                {
                    if (IsConfirmedTrailMakerLobby(self))
                        ActivateTrailMakerLobby(restartInfo);
                    else
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2124", "Trail Maker lobby transition did not complete; discarded its pending mod-settings state.");
                        ExitContext(force: true);
                    }
                }
                ObserveCoopLobby(self);
                LobbyOpened?.Invoke(self);
            }

            private static FileHeader ReadEmbeddedMakerMap(string path, string label)
            {
                FileHeader header = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0);
                if (header == null || header.mapType != 1)
                    throw new InvalidDataException("The embedded Trail map is not a readable multiplayer map.");
                if (!string.IsNullOrWhiteSpace(label)) header.display_filename = label;
                return header;
            }

            private List<FileHeader> MultiplayerMapsHook(MapFileManager self, int sortMode, bool ascending,
                int players, bool builtIn, bool user, bool workshop)
            {
                List<FileHeader> maps = multiplayerMapsOriginal(self, sortMode, ascending, players, builtIn, user, workshop);
                // populateMapList selects by object identity. Expose the loaded document's map
                // for this authoring context only, including when normal user maps are filtered.
                // Do not install it into the user's global map catalog or change their filters.
                if (EditorEnabled && embeddedMakerMapHeader != null && (pendingTrailMakerLoad || trailMakerAuthoringActive))
                {
                    maps = maps == null ? new List<FileHeader>() : new List<FileHeader>(maps);
                    if (!maps.Contains(embeddedMakerMapHeader)) maps.Insert(0, embeddedMakerMapHeader);
                }
                return maps;
            }

            private void StartSkirmishGameHook(
                FRONT_Multiplayer self,
                HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo)
            {
                if (customTrailRestartInfo != null &&
                    ExtendedDataLaunchOriginApi.Origin != ExtendedDataLaunchOriginKind.None)
                {
                    ExtendedDataLaunchOriginApi.MarkRestartPending();
                }
                BroadcastBuiltInCustomizeOrigin(self);
                if (!enabled && !activation.LordDataIntegration && !CoopEnabled)
                {
                    startSkirmishGameOriginal(self, customTrailRestartInfo);
                    return;
                }
                bool startInvoked = false;
                try
                {
                    if (self?.trailMakerMode == true)
                    {
                        if (!PrepareTrailMakerTestLaunch())
                            return;
                    }
                    if (CoopEnabled && self?.singlePlayerCoop == true && self.currentLobby?.coopTrailGame == true)
                    {
                        Func<FRONT_Multiplayer, bool> prepare = SinglePlayerCoopStarting;
                        if (prepare != null && !prepare(self))
                            return;
                    }
                    if (customTrailRestartInfo == null && customTrailSetupRestartInfo != null)
                    {
                        // Rebuild the embedded restart data from the edited lobby while retaining
                        // the Custom Trail identity needed by the native mission loader and restart flow.
                        customTrailRestartInfo = customTrailSetupRestartInfo;
                        customTrailRestartInfo.MPsetupData = (EngineInterface.MultiplayerSetupData)typeof(FRONT_Multiplayer)
                            .GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(self);
                        customTrailRestartInfo.importMembers(self.currentLobby);
                        customTrailRestartInfo.importAIVs(self.AIVs);
                        // The lobby uses the original map header, while Vanilla's Custom Trail
                        // launch path requires the .trail container header in the restart payload.
                        customTrailRestartInfo.selectedHeader = customTrailSetupHeader;
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2172", $"Starting customized Custom Trail [{customTrailRestartInfo.customTrailName}] " +
                                $"mission {customTrailRestartInfo.customTrailLevel}.");
                    }

                    if (customTrailRestartInfo?.customTrail == true)
                    {
                        try
                        {
                            FileHeader lordHeader = ResolveCustomTrailHeader(
                                customTrailRestartInfo.customTrailName,
                                customTrailRestartInfo.customTrailLevel);
                            TrailLordRequirements requirements = TrailLordRequirements.Read(lordHeader.filePath);
                            if (requirements == null)
                            {
                                LordDataCoordinator?.SetEmbeddedTrailSlots(null);
                                LordDataCoordinator?.PrepareTrail(null, null, false, out _);
                            }
                            else
                            {
                                Dictionary<int, FRONT_Multiplayer.MPAIVInfo> infos =
                                    ToLordInfoMap(customTrailRestartInfo.aivs);
                                LordDataCoordinator?.SetEmbeddedTrailSlots(requirements.Slots
                                    .Where(slot => infos.TryGetValue(slot.PlayerId,
                                        out FRONT_Multiplayer.MPAIVInfo info) && info?.lordConfig != null &&
                                        TrailLordSelectionPolicy.UsesEmbeddedLord(slot,
                                            info.builtInLord, info.lordName,
                                            LordDataCoordinator.PreparedTrailMediaAlias(
                                                requirements, slot.PlayerId), info.lordType,
                                            info.lordConfig.lordType, info.lordConfig.name,
                                            info.lordConfig.checksum.ToString()))
                                    .Select(slot => slot.PlayerId));
                                PrepareCustomTrailLords(requirements, infos, true);
                                LordDataCoordinator.RemapTrailMedia(infos);
                            }
                        }
                        catch (Exception exception)
                        {
                            LordDataCoordinator?.SetEmbeddedTrailSlots(null);
                            LordDataCoordinator?.PrepareTrail(null, null, false, out _);
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2211", "Blocked customized Custom Trail launch: " + exception);
                            ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"),
                                exception.Message);
                            return;
                        }
                    }

                    if (customTrailRestartInfo != null && customTrailRestartInfo.customTrail)
                        customTrailLaunchActive = true;
                    if (self?.singlePlayerCoop == true &&
                        ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.CustomizedCoopTrail)
                    {
                        try
                        {
                            ValidateSinglePlayerCoopAivs(self);
                            string importError = SinglePlayerCoopLaunchValidating?.Invoke(self);
                            if (importError != null) throw new InvalidDataException(importError);
                        }
                        catch (Exception exception)
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2229",
                                "Blocked Custom Coop Trail launch: trail=" + (self.currentLobby?.coopTrailID + 1) +
                                ", mission=" + self.currentLobby?.coopSelectedMission + ", route=" +
                                (MissionRestart.IsInProgress ? "restart" : ExtendedDataLaunchOriginApi.RestoredFromSave ? "restore" :
                                    FRONT_Multiplayer.skirmishGame ? "customize" : "direct") + "; " + exception);
                            string reason = exception is Shared.CoopAiSelectionException selection
                                ? SerpLocalization.Get("CoopAi.Error." + selection.Code).Replace("{Player}", selection.PlayerId.ToString())
                                : exception.Message;
                            ShowInformation(SerpLocalization.Get("ExtendedData.StartBlockedTitle"), reason);
                            return;
                        }
                    }
                    if (enabled && HasCustomizeDraft && workingContextEditable && !MissionRestart.IsInProgress)
                        FreezeWorkingDraft();
                    if (!TryPrepareRestartSettings()) return;
                    startInvoked = true;
                    try { startSkirmishGameOriginal(self, customTrailRestartInfo); }
                    catch { startInvoked = false; RecoverCoopDraft(); throw; }
                    customTrailSetupRestartInfo = null;
                    customTrailSetupHeader = null;
                }
                finally
                {
                    // Validation can reject a start without an exception or a lifecycle
                    // attempt. Release the read-only draft in that path as well.
                    if (!startInvoked)
                    {
                        if (self?.singlePlayerCoop == true)
                        {
                            missionPresetLifecycle.CancelPendingLaunch();
                            ExtendedDataLaunchOriginApi.CancelPendingLaunch();
                            SinglePlayerCoopStartRejected?.Invoke();
                        }
                        RecoverCoopDraft();
                    }
                }
            }

            private static void ValidateSinglePlayerCoopAivs(FRONT_Multiplayer self)
            {
                Shared.CoopAiStartPlanFactory.ValidateLobby(self?.currentLobby, self?.AIVs);
            }

            private void FrontendOpenCustomTrailHook(FrontendMenus self, string trailName, int level)
            {
                frontendOpenCustomTrailOriginal(self, trailName, level);
                if (externalButtonOwner)
                    customizationBridge.Refresh();
                else
                    MainViewModel.Instance.Show_TrailCustomisationButtons = enabled;
            }

            private void TrailSelectionHook(FrontendMenus self, int missionId, bool fromRealClick)
            {
                int page = FrontendMenus.CurrentSelectedTrail - 21;
                if (CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                    page >= 0 && page < 4 && missionId > 0)
                {
                    int available = CoopTrailMissionRange.CountOnPage(coopPackageMissionCount, page);
                    if (available == 0 || (fromRealClick && missionId > available))
                        return;
                    if (missionId > available)
                        missionId = available;
                }
                trailSelectionOriginal(self, missionId, fromRealClick);
                if (page >= 0 && page < 4)
                    RefreshCoopMissionProgress(page);
                if (enabled && !openingCustomTrailSetup &&
                    FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
                    EnterSelectedCustomTrail(self);
            }

            private void GenerateSwordsHook(FrontendMenus self)
            {
                generateSwordsOriginal(self);
                RefreshCoopMissionSymbols();
            }

            private void RefreshCoopMissionSymbols()
            {
                int page = FrontendMenus.CurrentSelectedTrail - 21;
                if (!CoopEnabled || string.IsNullOrWhiteSpace(coopPackageDisplayName) || page < 0 || page >= 4 ||
                    !MainViewModel.viewModelLoaded)
                    return;
                int available = CoopTrailMissionRange.CountOnPage(coopPackageMissionCount, page);
                for (int index = available; index < 10; index++)
                    MainViewModel.Instance.SetTrailSwordImage(index, null);
            }

            private void FrontendButtonHook(FrontendMenus self, string command)
            {
                int coopPage = string.Equals(command, "Coop", StringComparison.Ordinal) ? 0 :
                    string.Equals(command, "Coop2", StringComparison.Ordinal) ? 1 :
                    string.Equals(command, "Coop3", StringComparison.Ordinal) ? 2 :
                    string.Equals(command, "Coop4", StringComparison.Ordinal) ? 3 : -1;
                if (CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) && coopPage >= 0 &&
                    CoopTrailMissionRange.CountOnPage(coopPackageMissionCount, coopPage) == 0)
                    return;
                bool preserveTrailMakerMapEditor = string.Equals(command, "MapEditor", StringComparison.Ordinal) &&
                    MainViewModel.Instance.FRONTMultiplayer.trailMakerMode;
                if (!enabled && !CoopEnabled && !EditorEnabled)
                {
                    frontendButtonOriginal(self, command);
                    bool disabledContextChange =
                        string.Equals(command, "Skirmish", StringComparison.Ordinal) ||
                        (string.Equals(command, "MapEditor", StringComparison.Ordinal) && !preserveTrailMakerMapEditor) ||
                        string.Equals(command, "BackMain", StringComparison.Ordinal) ||
                        string.Equals(command, "Coops", StringComparison.Ordinal) ||
                        IsBuiltInTrailOpenCommand(command) ||
                        IsCoopTrailOpenCommand(command);
                    if (disabledContextChange)
                        ExtendedDataLaunchOriginApi.Clear();
                    return;
                }
                if (enabled && !externalButtonOwner &&
                    string.Equals(command, "Customize", StringComparison.Ordinal) &&
                    FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
                {
                    try
                    {
                        OpenSelectedCustomTrailSetup(self);
                    }
                    catch (Exception exception)
                    {
                        preserveContextForLaunch = false;
                        ExtendedDataLaunchOriginApi.Clear();
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2348", $"Could not open Custom Trail setup: {exception}");
                    }
                    return;
                }
                if (EditorEnabled && preserveTrailMakerMapEditor)
                {
                    CaptureTrailMakerWorkingDocument("Map Editor transition");
                    missionPresetLifecycle.AwaitTrailMakerReturn();
                }
                frontendButtonOriginal(self, command);
                if (string.Equals(command, "Coops", StringComparison.Ordinal))
                {
                    CoopPackagesChanged?.Invoke();
                    UpdateCoopSelectionTitles(self);
                }
                if (IsCoopTrailOpenCommand(command))
                    EnsureCoopCustomizeButtons();
                bool leavesTrailMaker = string.Equals(command, "MapEditor", StringComparison.Ordinal) &&
                    !preserveTrailMakerMapEditor;
                if (string.Equals(command, "Skirmish", StringComparison.Ordinal) ||
                    leavesTrailMaker ||
                    string.Equals(command, "BackMain", StringComparison.Ordinal) ||
                    string.Equals(command, "Coops", StringComparison.Ordinal) ||
                    IsBuiltInTrailOpenCommand(command))
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    ExitContext(force: true);
                }
                else if (IsCoopTrailOpenCommand(command))
                {
                    // Entering a Coop Trail page is a direct selection until Customize is pressed.
                    ExtendedDataLaunchOriginApi.Clear();
                }
            }

            private static bool IsCoopTrailOpenCommand(string command) =>
                string.Equals(command, "Coop", StringComparison.Ordinal) ||
                string.Equals(command, "Coop2", StringComparison.Ordinal) ||
                string.Equals(command, "Coop3", StringComparison.Ordinal) ||
                string.Equals(command, "Coop4", StringComparison.Ordinal);

            private static bool IsBuiltInTrailOpenCommand(string command) =>
                string.Equals(command, "Trail", StringComparison.Ordinal) ||
                string.Equals(command, "Trail2", StringComparison.Ordinal) ||
                string.Equals(command, "Trail3", StringComparison.Ordinal) ||
                string.Equals(command, "Sands1", StringComparison.Ordinal) ||
                string.Equals(command, "Sands2", StringComparison.Ordinal) ||
                string.Equals(command, "Sands3", StringComparison.Ordinal) ||
                string.Equals(command, "Sands4", StringComparison.Ordinal) ||
                string.Equals(command, "Sands5", StringComparison.Ordinal) ||
                string.Equals(command, "Sands6", StringComparison.Ordinal) ||
                string.Equals(command, "Sands7", StringComparison.Ordinal) ||
                string.Equals(command, "Sands8", StringComparison.Ordinal);

            private static bool IsConfirmedBuiltInCustomizeTransition(
                FRONT_Multiplayer self,
                bool fromNew,
                bool skirmishSetup,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo,
                bool coopSetup,
                bool trailMaker,
                int trailType,
                int missionId)
            {
                return self?.currentLobby != null &&
                    ReferenceEquals(Platform_Multiplayer.Instance?.activeLobby, self.currentLobby) &&
                    MainViewModel.Instance?.Show_MultiplayerSetup == true &&
                    fromNew && skirmishSetup && restartInfo == null && !coopSetup && !trailMaker &&
                    missionId >= 0 && IsBuiltInTrailType(trailType) &&
                    FRONT_Multiplayer.customizedTrail &&
                    FRONT_Multiplayer.customizedTrailType == trailType &&
                    FRONT_Multiplayer.customizedTrailID == missionId;
            }

            private static bool IsBuiltInTrailType(int trailType) =>
                (trailType >= ExtendedDataLaunchOriginApi.FirstVanillaTrailType &&
                 trailType <= ExtendedDataLaunchOriginApi.LastVanillaTrailType) ||
                (trailType >= ExtendedDataLaunchOriginApi.FirstSandsOfTimeTrailType &&
                 trailType <= ExtendedDataLaunchOriginApi.LastSandsOfTimeTrailType);

            private static void CaptureBuiltInCustomizeOrigin(int trailType, int missionId)
            {
                if (trailType >= ExtendedDataLaunchOriginApi.FirstVanillaTrailType &&
                    trailType <= ExtendedDataLaunchOriginApi.LastVanillaTrailType)
                {
                    ExtendedDataLaunchOriginApi.SetCustomizedVanillaTrail(
                        trailType,
                        trailType,
                        missionId);
                }
                else if (trailType >= ExtendedDataLaunchOriginApi.FirstSandsOfTimeTrailType &&
                    trailType <= ExtendedDataLaunchOriginApi.LastSandsOfTimeTrailType)
                {
                    ExtendedDataLaunchOriginApi.SetCustomizedSandsOfTime(
                        trailType,
                        trailType,
                        missionId);
                }
                else
                {
                    ExtendedDataLaunchOriginApi.Clear();
                }
            }

            private void CoopTrail1ConstructorHook(FRONT_CoopTrail1 self)
            {
                coopTrail1ConstructorOriginal(self);
                InitializeCoopPage(self, 0);
            }

            private void CoopTrail2ConstructorHook(FRONT_CoopTrail2 self)
            {
                coopTrail2ConstructorOriginal(self);
                InitializeCoopPage(self, 1);
            }

            private void CoopTrail3ConstructorHook(FRONT_CoopTrail3 self)
            {
                coopTrail3ConstructorOriginal(self);
                InitializeCoopPage(self, 2);
            }

            private void CoopTrail4ConstructorHook(FRONT_CoopTrail4 self)
            {
                coopTrail4ConstructorOriginal(self);
                InitializeCoopPage(self, 3);
            }

            private void InitializeCoopPage(UserControl page, int zeroBasedTrail)
            {
                InjectCoopCustomizeButton(page);
                if (UpdateCoopTrailTitle(page, zeroBasedTrail))
                {
                    LogCoopPresentationInitialized(zeroBasedTrail, "constructor");
                    return;
                }

                // Noesis can finish the managed constructor before the logical and visual
                // trees are materialized. Retry once at the framework's deterministic Loaded
                // event instead of treating this normal first phase as a feature failure.
                RoutedEventHandler loaded = null;
                loaded = (_, __) =>
                {
                    page.Loaded -= loaded;
                    InjectCoopCustomizeButton(page);
                    if (UpdateCoopTrailTitle(page, zeroBasedTrail))
                    {
                        LogCoopPresentationInitialized(zeroBasedTrail, "loaded");
                        return;
                    }

                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2499", "Could not find the logical title element after Loaded for Coop Trail " +
                        (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
                };
                page.Loaded += loaded;
                DebugLogHelper.LogDebug(
                    log,
                    "Deferred custom presentation until Loaded for Coop Trail " +
                    (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
            }

            private void LogCoopPresentationInitialized(int zeroBasedTrail, string phase)
            {
                DebugLogHelper.LogDebug(
                    log,
                    "Initialized custom presentation for Coop Trail " +
                    (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) +
                    "; phase=" + phase + ".");
            }

            private void EnterSelectedCustomTrail(FrontendMenus menus)
            {
                try
                {
                    FileHeader header = GetSelectedCustomTrailHeader(menus);
                    if (header != null)
                        EnterSidecar(header.filePath, editable: false, previewOnly: true);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2530", $"Could not select Custom Trail mod settings: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable: false);
                }
            }

            private void OpenSelectedCustomTrailSetup(FrontendMenus menus)
            {
                int missionId = FrontendMenus.CurrentSelectedCustomTrailMission;
                int trailId = FrontendMenus.CurrentSelectedTrail;
                if (missionId <= 0 || trailId < 90 || trailId > 92 || string.IsNullOrWhiteSpace(menus.CustomTrailName))
                    throw new InvalidDataException("The selected Custom Trail mission is invalid.");

                FileHeader header = GetSelectedCustomTrailHeader(menus);
                if (header == null || !header.hasRestartSkirmishInfo)
                    throw new InvalidDataException("The selected Custom Trail mission has no skirmish setup data.");

                // Vanilla stores the complete lobby setup inside every .trail. Reading the full
                // header is the Custom Trail equivalent of getTrailMissionInfo for built-in Trails.
                FileHeader fullHeader = MapFileManager.Instance.GetFileInfoFromFileName(
                    header.filePath,
                    header.filePath,
                    4,
                    loadRestartInfo: true);
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo = fullHeader?.restartSkirmishInfo;
                if (restartInfo == null)
                    throw new InvalidDataException("The selected Custom Trail mission setup could not be decoded.");

                int difficulty = (int)typeof(FrontendMenus)
                    .GetField("currentDifficultySetting", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(menus);
                FileHeader lobbyMapHeader = restartInfo.selectedHeader;
                if (lobbyMapHeader == null)
                    throw new InvalidDataException("The original Custom Trail map is not available in the local map catalog.");
                restartInfo.customTrail = true;
                restartInfo.customTrailName = menus.CustomTrailName;
                restartInfo.customTrailLevel = missionId;
                restartInfo.customTrailDifficulty = difficulty;
                customTrailLaunchActive = false;
                cleanupDeferralLogged = false;
                customTrailSetupRestartInfo = restartInfo;
                customTrailSetupHeader = header;
                launchedThroughCustomize = true;
                ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(trailId, missionId);

                openingCustomTrailSetup = true;
                try
                {
                    // Match Vanilla's Customize transition so the setup replaces the Trail page.
                    // Ignore selection callbacks raised by doOpen; they refer to transient UI state.
                    FrontendMenus.ClearUIPanels(frontEndState: true, logo: false);
                    MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
                    preserveContextForLaunch = true;
                    FRONT_Multiplayer.Open(
                        skirmishSetup: true,
                        restartInfo: restartInfo,
                        coopSetup: false,
                        trailMaker: false,
                        customiseTrailType: -1,
                        customiseTrailID: -1);
                }
                finally
                {
                    openingCustomTrailSetup = false;
                }
                // doOpen can trigger unrelated context cleanup; apply the selected mission again
                // after all lobby view models exist so Trail is visible and selected immediately.
                EnterSidecar(header.filePath, editable: true);
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2597", $"Opened Custom Trail setup [{menus.CustomTrailName}] mission {missionId}; " +
                        $"map=[{lobbyMapHeader.display_filename}], path=[{lobbyMapHeader.filePath}].");
            }

            private static FileHeader GetSelectedCustomTrailHeader(FrontendMenus menus)
            {
                int mission = FrontendMenus.CurrentSelectedCustomTrailMission;
                if (mission <= 0 || string.IsNullOrWhiteSpace(menus.CustomTrailName))
                    return null;
                return MapFileManager.Instance.GetHeaderFromCustomTrail(
                    menus.CustomTrailName,
                    FRONT_ManageTrail.GetMakerFileName(mission - 1));
            }

            private static FileHeader ResolveCustomTrailHeader(string trailName, int missionId)
            {
                if (string.IsNullOrWhiteSpace(trailName) || missionId <= 0)
                    throw new InvalidDataException("The Custom Trail name or 1-based mission number is invalid.");

                FileHeader header = MapFileManager.Instance.GetHeaderFromCustomTrail(
                    trailName,
                    FRONT_ManageTrail.GetMakerFileName(missionId - 1));
                if (header == null || string.IsNullOrWhiteSpace(header.filePath))
                    throw new InvalidDataException("The Custom Trail mission could not be resolved.");

                string trailPath = IOPath.GetFullPath(header.filePath);
                if (!string.Equals(IOPath.GetExtension(trailPath), ".trail", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(trailPath))
                {
                    throw new InvalidDataException("The resolved Custom Trail mission has no valid .trail file.");
                }
                return header;
            }

            internal void EnsureCoopCustomizeButtons()
            {
                UserControl[] pages =
                {
                    FRONT_CoopTrail1.Instance,
                    FRONT_CoopTrail2.Instance,
                    FRONT_CoopTrail3.Instance,
                    FRONT_CoopTrail4.Instance,
                };
                for (int index = 0; index < pages.Length; index++)
                {
                    InjectCoopCustomizeButton(pages[index]);
                    UpdateCoopTrailTitle(pages[index], index);
                }
                // MainViewModel.Instance constructs Vanilla's view model when read. During early
                // plugin initialization that constructor is not ready yet, so only refresh buttons
                // already discovered after the real FrontendMenus screen has opened.
                UpdateCoopSelectionTitles(null);
            }

            internal void SetCoopPackagePresentation(string displayName, int missionCount)
            {
                coopPackageDisplayName = displayName ?? string.Empty;
                coopPackageMissionCount = Math.Max(0, Math.Min(40, missionCount));
                EnsureCoopCustomizeButtons();
                if (MainViewModel.viewModelLoaded &&
                    FrontendMenus.CurrentSelectedTrail >= 21 && FrontendMenus.CurrentSelectedTrail <= 24)
                    MainViewModel.Instance.FrontEndMenu?.GenerateSwords();
            }

            private void CaptureVanillaCoopTrailTitles()
            {
                for (int index = 0; index < vanillaCoopTrailTitles.Length; index++)
                {
                    string key = GetCoopTrailTranslationKey(index);
                    if (Translate.Instance.GameTexts.TryGetValue(key, out string title))
                        vanillaCoopTrailTitles[index] = title;
                }
            }

            private static string GetCoopTrailTranslationKey(int zeroBasedTrail) =>
                "TEXT_COOP_0" + (23 + zeroBasedTrail).ToString(CultureInfo.InvariantCulture);

            private bool UpdateCoopTrailTitle(UserControl page, int zeroBasedTrail)
            {
                if (page == null)
                    return false;

                if (!coopTrailTitleBlocks.TryGetValue(page, out TextBlock title))
                {
                    string vanillaTitle = vanillaCoopTrailTitles[zeroBasedTrail];
                    if (string.IsNullOrEmpty(vanillaTitle))
                        return false;
                    title = FindLogicalDescendantTextBlock(page, vanillaTitle);
                    if (title == null)
                        return false;
                    // The original dictionary-index binding does not observe replacement values and
                    // can reapply Vanilla when the pane first becomes visible. We own this one title.
                    BindingOperations.ClearBinding(title, TextBlock.TextProperty);
                    coopTrailTitleBlocks[page] = title;
                }

                bool packageOccupiesTrail = CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                    coopPackageMissionCount > zeroBasedTrail * 10;
                title.Text = packageOccupiesTrail
                    ? coopPackageDisplayName
                    : CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName)
                        ? SerpLocalization.Get("ExtendedData.NoCustomCoopMissions")
                        : vanillaCoopTrailTitles[zeroBasedTrail];
                UpdateCoopMissionProgress(page, title, zeroBasedTrail);
                return true;
            }

            private void RefreshCoopMissionProgress(int zeroBasedTrail)
            {
                UserControl page = zeroBasedTrail == 0 ? FRONT_CoopTrail1.Instance :
                    zeroBasedTrail == 1 ? (UserControl)FRONT_CoopTrail2.Instance :
                    zeroBasedTrail == 2 ? FRONT_CoopTrail3.Instance : FRONT_CoopTrail4.Instance;
                if (page != null && coopTrailTitleBlocks.TryGetValue(page, out TextBlock title))
                    UpdateCoopMissionProgress(page, title, zeroBasedTrail);
            }

            private void UpdateCoopMissionProgress(UserControl page, TextBlock title, int zeroBasedTrail)
            {
                if (!coopMissionProgressBlocks.TryGetValue(page, out TextBlock progress))
                {
                    Grid host = page.FindName("mapgrid") as Grid;
                    if (host == null)
                        return;
                    progress = new TextBlock
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 42, 0, 0),
                        Width = 240,
                        FontSize = 24,
                        Foreground = title.Foreground,
                        Background = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                        TextAlignment = TextAlignment.Center,
                        IsHitTestVisible = false,
                        Visibility = Visibility.Collapsed,
                    };
                    host.Children.Add(progress);
                    coopMissionProgressBlocks[page] = progress;
                }
                int selectedMission = zeroBasedTrail == 0 ? FrontendMenus.CurrentSelectedTrailCoop1Mission :
                    zeroBasedTrail == 1 ? FrontendMenus.CurrentSelectedTrailCoop2Mission :
                    zeroBasedTrail == 2 ? FrontendMenus.CurrentSelectedTrailCoop3Mission :
                    FrontendMenus.CurrentSelectedTrailCoop4Mission;
                bool showProgress = enabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                    CoopTrailMissionRange.Contains(coopPackageMissionCount, zeroBasedTrail, selectedMission);
                progress.Text = showProgress
                    ? SerpLocalization.Get("ExtendedData.CustomCoopMissionProgress")
                        .Replace("{Current}", (zeroBasedTrail * 10 + selectedMission).ToString(CultureInfo.InvariantCulture))
                        .Replace("{Count}", coopPackageMissionCount.ToString(CultureInfo.InvariantCulture))
                    : string.Empty;
                progress.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            }

            private static TextBlock FindLogicalDescendantTextBlock(DependencyObject parent, string expectedText)
            {
                foreach (object value in LogicalTreeHelper.GetChildren(parent))
                {
                    if (!(value is DependencyObject child))
                        continue;
                    if (child is TextBlock textBlock && string.Equals(textBlock.Text, expectedText, StringComparison.Ordinal))
                        return textBlock;
                    TextBlock nested = FindLogicalDescendantTextBlock(child, expectedText);
                    if (nested != null)
                        return nested;
                }
                return null;
            }

            private void UpdateCoopSelectionTitles(FrontendMenus menus)
            {
                string[] commands = { "Coop", "Coop2", "Coop3", "Coop4" };
                for (int zeroBasedTrail = 0; zeroBasedTrail < commands.Length; zeroBasedTrail++)
                {
                    if (!coopSelectionButtons.TryGetValue(zeroBasedTrail, out Button button))
                    {
                        if (menus == null)
                            continue;
                        button = FindDescendantButton(menus, commands[zeroBasedTrail]);
                        if (button == null)
                            continue;
                        coopSelectionButtons[zeroBasedTrail] = button;
                        vanillaCoopSelectionEnabled[zeroBasedTrail] = button.IsEnabled;
                    }

                    string vanillaTitle = vanillaCoopTrailTitles[zeroBasedTrail];
                    if (string.IsNullOrEmpty(vanillaTitle))
                        continue;
                    bool packageOccupiesTrail = CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                        coopPackageMissionCount > zeroBasedTrail * 10;
                    bool packageSelected = CoopEnabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName);
                    PropEx.SetTextCentre(button, packageOccupiesTrail ? coopPackageDisplayName :
                        packageSelected ? SerpLocalization.Get("ExtendedData.NoCustomCoopMissions") : vanillaTitle);
                    button.IsEnabled = (!packageSelected || packageOccupiesTrail) &&
                        vanillaCoopSelectionEnabled[zeroBasedTrail];
                }
            }

            private static Button FindDescendantButton(DependencyObject parent, string commandParameter)
            {
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int index = 0; index < childCount; index++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                    if (child is Button button &&
                        string.Equals(button.CommandParameter as string, commandParameter, StringComparison.Ordinal))
                        return button;
                    Button nested = FindDescendantButton(child, commandParameter);
                    if (nested != null)
                        return nested;
                }
                return null;
            }

            private readonly HashSet<UserControl> injectedCoopPages = new HashSet<UserControl>();

            private void InjectCoopCustomizeButton(UserControl page)
            {
                if (externalButtonOwner)
                {
                    customizationBridge.Refresh();
                    return;
                }
                if (page == null || injectedCoopPages.Contains(page))
                    return;
                Button anchor = page.FindName("CoopKick") as Button;
                Grid host = anchor == null ? null : VisualTreeHelper.GetParent(anchor) as Grid;
                if (anchor == null || host == null)
                    return;

                var button = new Button
                {
                    Name = "SharedTrailCustomize",
                    Width = 200,
                    // BTN_SH_GlowL has transparent vertical padding; overlap the layout boxes for a 3 px visible gap.
                    Margin = new Thickness(0, 0, 0, -30),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Style = anchor.Style,
                    Visibility = enabled ? Visibility.Visible : Visibility.Collapsed,
                };
                PropEx.SetTextCentre(button, Translate.Instance.GameTexts.TryGetValue("TEXT_CUSTOMISATION_071", out string text) ? text : "Customize");
                PropEx.SetTextLeft(button, string.Empty);
                PropEx.SetTextRight(button, string.Empty);
                PropEx.SetGlowButtonTextHeight(button, 28);
                button.Click += (_, __) =>
                {
                    try
                    {
                        CustomizeCurrentCoopTrail();
                    }
                    catch (Exception exception)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2851", $"Could not open Coop Trail setup: {exception}");
                    }
                };
                host.Children.Add(button);
                injectedCoopButtons.Add(button);
                injectedCoopPages.Add(page);
            }

            private void CustomizeCurrentCoopTrail()
            {
                FRONT_Multiplayer self = MainViewModel.Instance.FRONTMultiplayer;
                int currentTrail = FrontendMenus.CurrentSelectedTrail;
                int mission = currentTrail == 21 ? FrontendMenus.CurrentSelectedTrailCoop1Mission :
                    currentTrail == 22 ? FrontendMenus.CurrentSelectedTrailCoop2Mission :
                    currentTrail == 23 ? FrontendMenus.CurrentSelectedTrailCoop3Mission :
                    currentTrail == 24 ? FrontendMenus.CurrentSelectedTrailCoop4Mission : -1;
                int trailId = currentTrail - 21;
                if (mission <= 0 || trailId < 0 || trailId > 3 || self.currentLobby == null)
                    return;

                if (!self.singlePlayerCoop && !self.currentLobby.isHost)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2873", "Ignored Coop Trail Customize click from a non-host client.");
                    return;
                }

                OpenCoopTrailSetup(self, trailId, mission, notifyClients: !self.singlePlayerCoop, source: "local host");
            }

            private bool HandleExternalCustomTrailCustomize()
            {
                if (!enabled)
                    return false;
                FrontendMenus menus = MainViewModel.Instance?.FrontEndMenu;
                if (menus == null)
                    throw new InvalidOperationException("The FrontendMenus instance is unavailable.");
                OpenSelectedCustomTrailSetup(menus);
                return true;
            }

            private bool HandleExternalCoopTrailCustomize()
            {
                if (!enabled)
                    return false;
                CustomizeCurrentCoopTrail();
                return true;
            }

            private void OpenCoopTrailSetup(
                FRONT_Multiplayer self,
                int trailId,
                int mission,
                bool notifyClients,
                string source)
            {
                if (self?.currentLobby == null || trailId < 0 || trailId > 3 || mission < 1 || mission > 10)
                    throw new InvalidDataException("The Coop Trail setup transition is invalid.");

                SetSelectedCoopMission(trailId, mission);
                ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(trailId, mission);
                try
                {
                    self.CoopMissionChanged(trailId, mission);
                    // Start the editor with the actual partner's defaults, not Vanilla's redundant Rat placeholder.
                    if (self.singlePlayerCoop)
                        Shared.CoopAiStartPlanFactory.PrepareDirectPartner(self.currentLobby, self.AIVs);
                    launchedThroughCustomize = true;
                    if (notifyClients)
                        BroadcastCoopCustomize(trailId, mission);
                    MethodInfo showSetup = typeof(FRONT_Multiplayer).GetMethod("ShowSetupScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (self.singlePlayerCoop)
                    {
                        FRONT_Multiplayer.skirmishGame = true;
                        FRONT_Multiplayer.coopGame = true;
                        FRONT_Multiplayer.coopGame_IsHost = true;
                        FRONT_Multiplayer.customCoopGame = false;
                        // The Coop Trail page starts with the local player ready. Vanilla's normal
                        // skirmish setup starts unready; restore that state so its Play button is not
                        // covered by the obsolete ReadyLock control after choosing Customize.
                        (MpLocalReadyField ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPLocalReady"))
                            .SetValue(self, false);
                        (MpLocalReadyLockedField ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "MPLocalReadyLocked"))
                            .SetValue(self, false);
                        MainViewModel.Instance.SkirmishSetupMode = true;
                        MainViewModel.Instance.MultiplayerSetupMode = false;
                        MainViewModel.Instance.Show_SkirmishRandomAI = true;
                        MainViewModel.Instance.Show_SkirmishTeams = true;
                        MainViewModel.Instance.Show_MPIsHost = true;
                        MainViewModel.Instance.Show_MPSteamIdentity = false;
                        showSetup.Invoke(self, null);
                    }
                    else
                    {
                        showSetup.Invoke(self, null);
                    }
                }
                catch
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    throw;
                }

                MainViewModel.Instance.Show_CoopHostInvitePane = false;
                MainViewModel.Instance.Show_CoopHostJoinedPane = false;
                MainViewModel.Instance.Show_CoopClientPane = false;
                MainViewModel.Instance.Show_CoopMapIcons = false;
                MainViewModel.Instance.Show_CoopAIAllyPanel = false;
                MainViewModel.Instance.Show_CoopOptions = false;
                MainViewModel.Instance.Show_CoopWaiting = false;
                MainViewModel.Instance.Show_MPSharing = false;
                Shared.CoopTrailSetupPresentation.Refresh(self, preserveMissionText: trailSource.IsAvailable);
                MainViewModel.Instance.Show_MultiplayerSetup = true;
                if (trailId == 0) MainViewModel.Instance.Show_CoopTrail1 = false;
                if (trailId == 1) MainViewModel.Instance.Show_CoopTrail2 = false;
                if (trailId == 2) MainViewModel.Instance.Show_CoopTrail3 = false;
                if (trailId == 3) MainViewModel.Instance.Show_CoopTrail4 = false;
                // ShowSetupScreen rebuilds lobby settings. Reapply the selected mission only
                // after that transition, matching the working Custom Trail Customize path.
                CoopSetupOpened?.Invoke();
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2969", $"Opened Coop Trail setup trail={trailId + 1}, mission={mission}, source={source}.");
            }

            private void BroadcastCoopCustomize(int trailId, int missionId)
            {
                BroadcastCoopTransition(trailId, missionId, launch: false);
            }

            private void BroadcastBuiltInCustomizeOrigin(FRONT_Multiplayer self)
            {
                ExtendedDataLaunchOriginKind origin = ExtendedDataLaunchOriginApi.Origin;
                if ((origin != ExtendedDataLaunchOriginKind.CustomizedVanillaTrail &&
                     origin != ExtendedDataLaunchOriginKind.CustomizedSandsOfTime) ||
                    self?.currentLobby == null || !self.currentLobby.isHost ||
                    self.currentLobby.members == null || self.currentLobby.members.Count <= 1)
                {
                    return;
                }

                var packet = new BuiltInCustomizeOriginPacket
                {
                    ProtocolVersion = BuiltInCustomizeOriginPacket.CurrentProtocolVersion,
                    TrailType = ExtendedDataLaunchOriginApi.TrailType,
                    MissionId = ExtendedDataLaunchOriginApi.MissionId,
                };
                if (!BuiltInCustomizeOriginPacket.IsValid(packet))
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:2997", "Refused to broadcast an invalid Built-in Customize origin.");
                    return;
                }

                byte[] bytes = MessagePackSerializer.Serialize(packet);
                GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
                {
                    packetType = builtInCustomizeOriginPacketId,
                    data = bytes,
                    dataLength = bytes.Length,
                    dataOffset = 0,
                });
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3009", $"Broadcast Built-in Customize launch origin: trailType={packet.TrailType}, " +
                        $"missionIndex={packet.MissionId}, packetId={builtInCustomizeOriginPacketId}.");
            }

            internal void BroadcastCoopLaunch(int trailId, int missionId)
            {
                BroadcastCoopTransition(trailId, missionId, launch: true);
            }

            private void BroadcastCoopTransition(int trailId, int missionId, bool launch)
            {
                var packet = new CoopCustomizePacket
                {
                    ProtocolVersion = CoopCustomizeProtocolVersion,
                    TrailId = trailId,
                    MissionId = missionId,
                    Launch = launch,
                };
                byte[] bytes = MessagePackSerializer.Serialize(packet);
                GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
                {
                    packetType = coopCustomizePacketId,
                    data = bytes,
                    dataLength = bytes.Length,
                    dataOffset = 0,
                });
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3037", $"Broadcast Coop Trail {(launch ? "launch" : "setup")} transition " +
                        $"trail={trailId + 1}, mission={missionId}, packetId={coopCustomizePacketId}.");
            }

            private void OnCoopCustomizePacket(ReceiveCustomPacketEventArgs<CoopCustomizePacket> args)
            {
                CoopCustomizePacket source = args?.Packet;
                if (source == null || !args.SenderSteamId.HasValue)
                    return;
                var packet = new CoopCustomizePacket
                {
                    ProtocolVersion = source.ProtocolVersion,
                    TrailId = source.TrailId,
                    MissionId = source.MissionId,
                    Launch = source.Launch
                };
                ulong senderSteamId = args.SenderSteamId.Value.m_SteamID;
                object epoch = System.Threading.Volatile.Read(ref coopPacketEpoch);
                APIShared.ApiShared.ForMod(ExtendedDataPlugin.PluginGuid).Network.TryPostPresentation(
                    () => {
                        if (!ReferenceEquals(epoch, coopPacketEpoch) ||
                            !ReferenceEquals(coopPacketLobby, Platform_Multiplayer.Instance?.activeLobby)) return;
                        ProcessCoopCustomizePacket(packet, new CSteamID(senderSteamId));
                    });
            }

            private void ProcessCoopCustomizePacket(CoopCustomizePacket packet, CSteamID senderSteamId)
            {
                try
                {
                    CSteamID? host = GameNetworkAPI.GetHostSteamId();
                    if (!host.HasValue || senderSteamId != host.Value)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3067", "Rejected Coop Trail transition from a sender that is not the lobby host.");
                        return;
                    }

                    if (packet == null || packet.ProtocolVersion != CoopCustomizeProtocolVersion ||
                        packet.TrailId < 0 || packet.TrailId > 3 || packet.MissionId < 1 || packet.MissionId > 10)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3076", "Rejected invalid Coop Trail transition packet.");
                        return;
                    }

                    FRONT_Multiplayer self = MainViewModel.Instance.FRONTMultiplayer;
                    if ((!enabled && !(packet.Launch && CoopEnabled)) || self?.currentLobby == null || self.currentLobby.isHost ||
                        self.singlePlayerCoop || !self.currentLobby.coopTrailGame)
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3084", "Ignored Coop Trail transition outside an active client Coop lobby.");
                        return;
                    }

                    if (packet.Launch)
                    {
                        CoopLaunchReceived?.Invoke(packet.TrailId, packet.MissionId);
                        return;
                    }

                    OpenCoopTrailSetup(
                        self,
                        packet.TrailId,
                        packet.MissionId,
                        notifyClients: false,
                        source: "authenticated host packet");
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3103", "Could not apply Coop Trail transition from host: " + exception);
                }
            }

            private void OnBuiltInCustomizeOriginPacket(
                ReceiveCustomPacketEventArgs<BuiltInCustomizeOriginPacket> args)
            {
                BuiltInCustomizeOriginPacket source = args?.Packet;
                if (source == null || !args.SenderSteamId.HasValue)
                    return;
                var packet = new BuiltInCustomizeOriginPacket
                {
                    ProtocolVersion = source.ProtocolVersion,
                    TrailType = source.TrailType,
                    MissionId = source.MissionId
                };
                ulong senderSteamId = args.SenderSteamId.Value.m_SteamID;
                object epoch = System.Threading.Volatile.Read(ref coopPacketEpoch);
                APIShared.ApiShared.ForMod(ExtendedDataPlugin.PluginGuid).Network.TryPostPresentation(
                    () => {
                        if (!ReferenceEquals(epoch, coopPacketEpoch) ||
                            !ReferenceEquals(coopPacketLobby, Platform_Multiplayer.Instance?.activeLobby)) return;
                        ProcessBuiltInCustomizeOriginPacket(packet, new CSteamID(senderSteamId));
                    });
            }

            private void ProcessBuiltInCustomizeOriginPacket(
                BuiltInCustomizeOriginPacket packet,
                CSteamID senderSteamId)
            {
                try
                {
                    CSteamID? host = GameNetworkAPI.GetHostSteamId();
                    if (!host.HasValue || senderSteamId != host.Value)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3133", "Rejected Built-in Customize origin from a sender that is not the lobby host.");
                        return;
                    }

                    if (!BuiltInCustomizeOriginPacket.IsValid(packet))
                    {
                        ExtendedDataLaunchOriginApi.Clear();
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3142", "Rejected invalid Built-in Customize origin packet.");
                        return;
                    }

                    FRONT_Multiplayer self = MainViewModel.Instance?.FRONTMultiplayer;
                    if (self?.currentLobby == null || self.currentLobby.isHost ||
                        !FRONT_Multiplayer.skirmishGame || FRONT_Multiplayer.coopGame)
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3150", "Ignored Built-in Customize origin outside an active client Skirmish lobby.");
                        return;
                    }

                    CaptureBuiltInCustomizeOrigin(packet.TrailType, packet.MissionId);
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3157", $"Accepted Built-in Customize launch origin from host: trailType={packet.TrailType}, " +
                            $"missionIndex={packet.MissionId}.");
                }
                catch (Exception exception)
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3165", "Could not apply Built-in Customize origin from host: " + exception);
                }
            }

            private static void SetSelectedCoopMission(int trailId, int missionId)
            {
                FrontendMenus.CurrentSelectedTrail = trailId + 21;
                if (trailId == 0) FrontendMenus.CurrentSelectedTrailCoop1Mission = missionId;
                if (trailId == 1) FrontendMenus.CurrentSelectedTrailCoop2Mission = missionId;
                if (trailId == 2) FrontendMenus.CurrentSelectedTrailCoop3Mission = missionId;
                if (trailId == 3) FrontendMenus.CurrentSelectedTrailCoop4Mission = missionId;
            }

            private static bool IsConfirmedTrailMakerLobby(FRONT_Multiplayer self) =>
                self?.currentLobby != null &&
                self.trailMakerMode &&
                ReferenceEquals(Platform_Multiplayer.Instance?.activeLobby, self.currentLobby) &&
                MainViewModel.Instance?.Show_MultiplayerSetup == true;

            private void ActivateTrailMakerLobby(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
            {
                string requestedTrailPath = pendingTrailMakerTrailPath;
                string source;
                try
                {
                    if (pendingTrailMakerLoad && !string.IsNullOrWhiteSpace(requestedTrailPath))
                    {
                        bool sidecarExists = EnterSidecar(requestedTrailPath, editable: true, authoring: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), requestedTrailPath);
                        source = sidecarExists ? "loaded mission sidecar" : "loaded mission defaults";
                    }
                    else if (pendingTrailMakerLoad)
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true, preserveCurrentValues: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        source = "unavailable loaded mission defaults";
                    }
                    else if (missionPresetLifecycle.AwaitingTrailMakerReturn && trailMakerWorkingDocument != null)
                    {
                        ModSettingsDefinition document = trailMakerWorkingDocument;
                        ApplyDocument(document, editable: true, preserveCurrentValues: true,
                            startingSnapshots: trailMakerWorkingSnapshots);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), trailMakerTrailPath);
                        source = restartInfo?.customTestMission == true
                            ? "test return draft"
                            : "authoring session draft";
                    }
                    else
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true, useFixedDefaults: true,
                            preserveCurrentValues: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        source = "new mission defaults";
                    }
                    missionPresetLifecycle.CompleteTrailMakerReturn();
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3222", "Activated editable Trail Maker mod-settings context from " + source + ".");
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3228", "Could not activate the Trail Maker mod-settings draft; applying editable defaults: " + exception);
                    try
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true, useFixedDefaults: true,
                            preserveCurrentValues: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        missionPresetLifecycle.CompleteTrailMakerReturn();
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3238", "Activated editable Trail Maker mod-settings context from fail-closed defaults.");
                    }
                    catch (Exception fallbackException)
                    {
                        Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3244", "Could not activate fail-closed Trail Maker defaults; leaving the authoring context: " +
                            fallbackException);
                        try
                        {
                            ExitContext(force: true);
                        }
                        catch (Exception cleanupException)
                        {
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3254", "Could not fully clean up the failed Trail Maker context: " + cleanupException);
                        }
                    }
                }
            }

            private void CaptureTrailMakerWorkingDocument(string transition)
            {
                try
                {
                    ModSettingsDefinition document = trailContext &&
                        string.Equals(activeContextLabel, "Trail", StringComparison.Ordinal)
                        ? CaptureDocument()
                        : trailMakerWorkingDocument ?? ModSettingsDefinition.CreateModDefaults();
                    UpdateTrailMakerWorkingDocument(document, trailMakerTrailPath);
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3271", "Captured editable Trail Maker mod-settings draft before " + transition + ".");
                }
                catch (Exception exception)
                {
                    trailMakerWorkingDocument = ModSettingsDefinition.CreateModDefaults();
                    trailMakerTrailPath = null;
                    trailMakerAuthoringActive = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3280", "Could not capture the Trail Maker mod-settings draft before " + transition +
                        "; the return will use editable defaults: " + exception);
                }
            }

            private void UpdateTrailMakerWorkingDocument(ModSettingsDefinition document, string trailPath)
            {
                // Store an independent normalized copy because ApplyDocument removes obsolete
                // properties from the instance it receives.
                trailMakerWorkingDocument = ModSettingsJson.ParseObject(ModSettingsJson.Serialize(document));
                trailMakerWorkingSnapshots = CaptureCurrentSnapshots(FindCompatibleViewModels());
                trailMakerTrailPath = string.IsNullOrWhiteSpace(trailPath)
                    ? null
                    : IOPath.GetFullPath(trailPath);
                trailMakerAuthoringActive = true;
            }

            private void ClearTrailMakerAuthoringState()
            {
                embeddedMakerMapHeader = null;
                trailMakerWorkingDocument = null;
                trailMakerWorkingSnapshots = null;
                trailMakerTrailPath = null;
                pendingTrailMakerTrailPath = null;
                pendingTrailMakerLoad = false;
                trailMakerAuthoringActive = false;
                missionPresetLifecycle.Reset();
            }

            private bool EnterSidecar(string trailPath, bool editable, bool previewOnly = false, bool authoring = false)
            {
                string sidecar = MissionLoader.GetTrailModSettingsPath(trailPath);
                bool exists = File.Exists(sidecar);
                long length = -1;
                long writeTicks = 0;
                if (exists)
                {
                    var info = new FileInfo(sidecar);
                    length = info.Length;
                    writeTicks = info.LastWriteTimeUtc.Ticks;
                }

                if (trailContext && activeSidecarEditable == editable &&
                    activeSidecarPreviewOnly == previewOnly &&
                    activeSidecarAuthoring == authoring &&
                    string.Equals(activeSidecarPath, sidecar, StringComparison.OrdinalIgnoreCase) &&
                    activeSidecarLength == length && activeSidecarWriteTicks == writeTicks &&
                    AreAllTrailPresetsActive())
                {
                    return exists;
                }

                trailSource.Clear();
                try
                {
                    ModSettingsDefinition document = exists
                        ? ModSettingsJson.Read(sidecar)
                        : ModSettingsDefinition.CreateModDefaults();
                    workingSourceContextId = "trail:" + IOPath.GetFullPath(sidecar);
                    return trailSource.Activate(exists ? document : null, workingSourceContextId, () =>
                    {
                        ModSettingsApplication.EnterContext(BuildRestartContext(workingSourceContextId, document, trailPath));
                        ApplyDocument(document, editable, useFixedDefaults: !exists && editable,
                            previewOnly: previewOnly, preserveCurrentValues: editable && authoring);
                        activeCreatorDocument = CloneDocument(document);
                        string[] mentionedMods = document.Mods.Keys.ToArray();
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3343", $"Loaded Trail sidecar [{sidecar}]; exists={exists}, editable={editable}, " +
                                "mentioned=[" + string.Join(", ", mentionedMods) + "].");
                        activeSidecarPath = sidecar;
                        activeSidecarLength = length;
                        activeSidecarWriteTicks = writeTicks;
                        activeSidecarEditable = editable;
                        activeSidecarPreviewOnly = previewOnly;
                        activeSidecarAuthoring = authoring;
                        return exists;
                    });
                }
                finally { SourcesChanged?.Invoke(); }
            }

            private ModSettingsDefinition CaptureDocument()
            {
                ModSettingsDefinition document = ModSettingsDefinition.CreateModDefaults();
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();

                foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in participants)
                {
                    MissionModSettingsEntry captured = MissionModSettingsPreparation.CaptureEntry(participant.Value, name =>
                    {
                        TrailSettingMode mode = getPropertyMode(participant.Key, name);
                        return mode == TrailSettingMode.Player ? PublishedPresetValueMode.Player :
                            mode == TrailSettingMode.Fixed ? PublishedPresetValueMode.Fixed : PublishedPresetValueMode.ModDefault;
                    });
                    var target = new ModSettingsEntry { PlayerSettings = captured.PlayerSettings, Overrides = captured.Overrides };
                    if (target.PlayerSettings.Length != 0 || target.Overrides.Count != 0)
                        document.Mods[participant.Key] = target;
                }
                return ModSettingsJson.NormalizeAndValidate(document, "captured Map/Trail mod settings");
            }

            private void ApplyDocument(
                ModSettingsDefinition document,
                bool editable,
                string presetLabel = "Trail",
                bool useFixedDefaults = false,
                bool previewOnly = false,
                bool preserveCurrentValues = false,
                Dictionary<string, Dictionary<string, byte[]>> startingSnapshots = null,
                bool materializeCurrentValues = false,
                bool preserveEditorModes = false)
            {
                ClearActiveSidecar();
                var shared = new MissionModSettingsDocument {
                    SchemaVersion = document.SchemaVersion,
                    Mods = document.Mods.ToDictionary(item => item.Key, item => ToSharedEntry(item.Value), StringComparer.Ordinal)
                };
                MissionModSettingsApplicationKind kind = materializeCurrentValues ? MissionModSettingsApplicationKind.Materialized :
                    preserveCurrentValues ? (editable ? MissionModSettingsApplicationKind.Authoring : MissionModSettingsApplicationKind.AuthoringTest) :
                    previewOnly ? MissionModSettingsApplicationKind.Preview :
                    editable ? MissionModSettingsApplicationKind.Editable : MissionModSettingsApplicationKind.Playable;
                try
                {
                    var removed = settingsSession.Apply(shared, FindCompatibleViewModels(), kind, presetLabel, startingSnapshots,
                        prepared =>
                        {
                            CopyPreparedEntries(prepared, document);
                            if (editable && !preserveEditorModes) applyEditorModes?.Invoke(document, useFixedDefaults);
                        });
                    foreach (var item in removed)
                        if (item.Value.Count != 0)
                            {
                                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtendedData/settings-pruning", "Ignored obsolete Map/Trail settings for [" +
                                item.Key + "]: " + string.Join(", ", item.Value) + ". They will be omitted on the next save.");
                            }
                    trailContext = true;
                    workingContextEditable = editable;
                    if (preserveEditorModes)
                        foreach (var participant in FindCompatibleViewModels())
                            if (participant.Value is IModSettingsMissionSourceEndpoint source)
                                source.System_SetExplicitMissionSettings(activeCreatorDocument?.Mods.ContainsKey(participant.Key) == true);
                    activeContextLabel = string.IsNullOrWhiteSpace(presetLabel) ? "Trail" : presetLabel;
                }
                catch
                {
                    trailContext = settingsSession.ActiveModGuids.Count != 0;
                    throw;
                }
                finally { CopyPreparedEntries(shared, document); }
            }

            private static void CopyPreparedEntries(MissionModSettingsDocument source, ModSettingsDefinition target)
            {
                foreach (string id in target.Mods.Keys.ToArray())
                {
                    if (!source.Mods.TryGetValue(id, out MissionModSettingsEntry entry)) { target.Mods.Remove(id); continue; }
                    if (entry != null && target.Mods[id] != null)
                    {
                        target.Mods[id].PlayerSettings = entry.PlayerSettings;
                        target.Mods[id].Overrides = entry.Overrides;
                    }
                }
            }

            private Dictionary<string, Dictionary<string, byte[]>> CaptureCurrentSnapshots(
                Dictionary<string, IModSettingsPresetEndpoint> participants)
            {
                var snapshots = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in participants)
                {
                    snapshots[participant.Key] = participant.Value is IModSettingsWorkingCopyEndpoint working
                        ? working.System_CreateCurrentWorkingSnapshot()
                        : MissionModSettingsPreparation.CaptureCurrentSnapshot(participant.Value);
                }
                return snapshots;
            }

            private void ExitActiveParticipants() => settingsSession.Exit();

            private Dictionary<string, IModSettingsPresetEndpoint> FindCompatibleViewModels()
            {
                return SavegameModSettings.GetCompatibleParticipants()
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            }

            private bool AreAllTrailPresetsActive()
            {
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();
                return settingsSession.ActiveModGuids.All(id =>
                {
                    if (!participants.TryGetValue(id, out IModSettingsPresetEndpoint viewModel))
                        return false;
                    return viewModel.IsMissionPresetActive;
                });
            }

            private void ClearActiveSidecar()
            {
                activeSidecarPath = null;
                activeSidecarLength = -1;
                activeSidecarWriteTicks = 0;
                activeSidecarEditable = false;
                activeSidecarPreviewOnly = false;
                activeSidecarAuthoring = false;
            }



            // Sidecars travel with their own Trail only: mod settings and Lord requirements of a mission
            // whose .trail is not in the source are never copied. Requirements of a re-saved Trail stay
            // useful (export re-checks each slot's identity); a damaged file is reported and skipped, never
            // repaired here. Existing targets stay untouched unless overwrite is requested. Returns the
            // files not copied.
            private List<string> CopySidecars(string source, string destination, bool overwrite)
            {
                var skipped = new List<string>();
                if (!Directory.Exists(source) || !Directory.Exists(destination))
                    return skipped;
                foreach (string trail in Directory.GetFiles(source, "*.trail"))
                {
                    var sidecars = new List<string> { MissionLoader.GetTrailModSettingsPath(trail) };
                    string requirements = TrailLordRequirements.SidecarPath(trail);
                    if (File.Exists(requirements))
                    {
                        string problem = null;
                        try
                        {
                            TrailLordRequirements.ReadUnbound(trail, out _);
                        }
                        catch (Exception exception)
                        {
                            problem = exception.Message;
                        }
                        if (problem == null)
                            sidecars.Add(requirements);
                        else
                        {
                            skipped.Add(IOPath.GetFileName(requirements));
                            Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3395", $"Did not copy Lord requirements [{requirements}]: {problem}.");
                        }
                    }
                    foreach (string sidecar in sidecars)
                    {
                        if (!File.Exists(sidecar))
                            continue;
                        string target = IOPath.Combine(destination, IOPath.GetFileName(sidecar));
                        if (!overwrite && File.Exists(target))
                        {
                            skipped.Add(IOPath.GetFileName(sidecar));
                            Shared.DebugLogHelper.LogWarningLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3405", $"Kept existing sidecar [{target}] instead of importing [{sidecar}].");
                            continue;
                        }
                        File.Copy(sidecar, target, overwrite);
                    }
                }
                return skipped;
            }

            private void DeleteOrphanMakerSidecars()
            {
                string root = ConfigSettings.GetUserTrailMakerPath();
                foreach (string sidecar in Directory.GetFiles(
                    root,
                    "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                {
                    if (!File.Exists(MissionLoader.GetTrailPathFromModSettingsPath(sidecar)))
                        File.Delete(sidecar);
                }
                foreach (string sidecar in Directory.GetFiles(root, "Trail_Mission_*" + TrailLordRequirements.Suffix))
                {
                    string trail = sidecar.Substring(0, sidecar.Length - TrailLordRequirements.Suffix.Length) + ".trail";
                    if (!File.Exists(trail))
                        File.Delete(sidecar);
                }
            }

            private void TryFileOperation(string action, Action operation)
            {
                try
                {
                    operation();
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtendedData/src/TrailMissionSettingsCoordinator.cs:3632", $"Could not {action}: {exception}");
                }
            }

            private MethodInfo RequireManageTrailMethod(string name, params Type[] parameters) =>
                typeof(FRONT_ManageTrail).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    parameters,
                    null) ?? throw new MissingMethodException(typeof(FRONT_ManageTrail).FullName, name);

            private T InstallHook<T>(MethodBase method, T replacement) where T : Delegate
            {
                if (method == null)
                    throw new MissingMethodException("Trail mod-settings hook target was not found.");
                var hook = new Hook(method, replacement);
                hooks.Add(hook);
                return hook.GenerateTrampoline<T>();
            }

            private sealed class PendingUploadDecision
            {
                private readonly string root;
                private readonly string itemName;

                internal PendingUploadDecision(string root, string itemName, bool includeExtendedData)
                {
                    this.root = Normalize(root);
                    this.itemName = itemName ?? string.Empty;
                    IncludeExtendedData = includeExtendedData;
                }

                internal bool IncludeExtendedData { get; }

                internal bool Matches(string candidateRoot, string candidateItemName) =>
                    string.Equals(root, Normalize(candidateRoot), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(itemName, candidateItemName, StringComparison.OrdinalIgnoreCase);

                private static string Normalize(string value)
                {
                    try { return IOPath.GetFullPath(value ?? string.Empty); }
                    catch { return value ?? string.Empty; }
                }
            }

        }
}
