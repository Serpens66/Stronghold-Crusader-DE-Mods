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
    internal sealed class TrailModCompatibilityInfo
    {
        public TrailModCompatibilityInfo(
            string modId,
            string displayName,
            PropertyInfo[] properties,
            string incompatibilityReason)
        {
            ModId = modId;
            DisplayName = displayName;
            Properties = properties ?? Array.Empty<PropertyInfo>();
            IncompatibilityReason = incompatibilityReason;
        }

        public string ModId { get; }
        public string DisplayName { get; }
        public PropertyInfo[] Properties { get; }
        public string IncompatibilityReason { get; }
        public bool IsCompatible => string.IsNullOrEmpty(IncompatibilityReason);
    }

    /// <summary>Owns the process-wide Custom Trail settings and customization integration.</summary>
        internal sealed class TrailMissionSettingsCoordinator : IDisposable, IModSettingsWorkingSourceProvider
        {
            private const string CoopTrailMakerSourceDirectory = "TrailMakerSource";
            private const string EncodedSettingPrefix = "messagepack-base64:";
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
            private delegate void ImportDelegate(FRONT_ManageTrail self, string customFolderName);
            private delegate void ExportDelegate(FRONT_ManageTrail self, string destination);
            private delegate void TwoStringDelegate(FRONT_ManageTrail self, string first, string second);
            private delegate void NoArgumentDelegate(FRONT_ManageTrail self);
            private delegate void StartCustomTrailDelegate(MainViewModel self, string trailName, int missionId, int difficulty);
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
            private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> persistedPropertiesByType =
                new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            private readonly Dictionary<string, string> lastCompatibilityFailures =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ModSettingsDefinition> capturedDocumentsByTrailPath =
                new Dictionary<string, ModSettingsDefinition>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> activeParticipantIds = new HashSet<string>(StringComparer.Ordinal);
            private readonly MissionPresetLifecycleState missionPresetLifecycle = new MissionPresetLifecycleState();
            private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
            private ManageTrailButtonDelegate manageTrailButtonOriginal;
            private EditorSetupButtonDelegate editorSetupButtonOriginal;
            private UploadWorkshopMapDelegate uploadWorkshopMapOriginal;
            private ManageTrailInitDelegate manageTrailInitOriginal;
            private TwoStringDelegate backupOriginal;
            private ImportDelegate importOriginal;
            private ExportDelegate exportOriginal;
            private NoArgumentDelegate clearMakerOriginal;
            private StartCustomTrailDelegate startCustomTrailOriginal;
            private MultiplayerOpenDelegate multiplayerOpenOriginal;
            private StartSkirmishGameDelegate startSkirmishGameOriginal;
            private FrontendOpenCustomTrailDelegate frontendOpenCustomTrailOriginal;
            private FrontendButtonDelegate frontendButtonOriginal;
            private TrailSelectionDelegate trailSelectionOriginal;
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
            private string trailMakerTrailPath;
            private string pendingTrailMakerTrailPath;
            private bool pendingTrailMakerLoad;
            private bool trailMakerAuthoringActive;
            private string activeSidecarPath;
            private long activeSidecarLength = -1;
            private long activeSidecarWriteTicks;
            private bool activeSidecarEditable;
            private bool activeSidecarPreviewOnly;
            private bool workingContextEditable;
            private ModSettingsDefinition trailSourceDocument;
            private ModSettingsDefinition mapSourceDocument;
            private string workingSourceContextId = string.Empty;
            private bool enabled;
            private bool externalButtonOwner;
            private readonly List<Button> injectedCoopButtons = new List<Button>();
            private readonly Dictionary<UserControl, TextBlock> coopTrailTitleBlocks =
                new Dictionary<UserControl, TextBlock>();
            private readonly string[] vanillaCoopTrailTitles = new string[4];
            private readonly Dictionary<int, Button> coopSelectionButtons =
                new Dictionary<int, Button>();
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
            public event Action CoopSetupOpened;
            public event Action<int, int> CoopLaunchReceived;
            public event Action<FRONT_Multiplayer> LobbyOpened;
            public event Func<FRONT_Multiplayer, bool> SinglePlayerCoopStarting;
            public event Action SourcesChanged;

            public IReadOnlyList<TrailModCompatibilityInfo> DiscoverModCompatibility()
            {
                var result = new List<TrailModCompatibilityInfo>();
                foreach (IGrouping<string, LobbyModSettingsEntry> group in GetRegistrationGroups())
                {
                    if (IsRegistrationGroupOptedOut(group))
                        continue;
                    string modId = group.Key;
                    if (string.Equals(modId, ExtendedDataPlugin.PluginGuid, StringComparison.Ordinal))
                        continue;
                    LobbyModSettingsEntry entry = group.First();
                    string displayName = GetModDisplayName(entry);
                    TrailModCompatibilityResult compatibility = entry == null || group.Skip(1).Any()
                        ? null
                        : GetCompatibility(entry.ViewModel);
                    string incompatibility = entry == null
                        ? "missing mod-settings registration"
                        : group.Skip(1).Any()
                        ? "multiple mod-settings panels use the same plugin GUID"
                        : compatibility.IncompatibilityReason;
                    result.Add(new TrailModCompatibilityInfo(
                        modId,
                        displayName,
                        compatibility?.Properties,
                        incompatibility));
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
                        DebugLogHelper.LogInfo(
                            log,
                            $"Map/Trail mod settings [{item.DisplayName}] ({item.ModId}) are not included in creator presets: " +
                            item.IncompatibilityReason + ".");
                    }
                }
                lastCompatibilityFailures.Clear();
                foreach (KeyValuePair<string, string> item in current)
                    lastCompatibilityFailures[item.Key] = item.Value;
            }

            private TrailModCompatibilityResult GetCompatibility(object viewModel)
            {
                if (!(viewModel is IModSettingsPresetEndpoint endpoint))
                {
                    return new TrailModCompatibilityResult(
                        Array.Empty<PropertyInfo>(),
                        "missing typed APIShared preset endpoint");
                }
                return TrailModCompatibilityContract.Evaluate(
                    viewModel,
                    endpoint.System_CreateDisabledMissionPresetSnapshot,
                    (property, value) => MessagePackSerializer.Serialize(property.PropertyType, value),
                    (type, bytes) => MessagePackSerializer.Deserialize(type, bytes));
            }

            private string GetIncompatibilityReason(object viewModel) =>
                GetCompatibility(viewModel).IncompatibilityReason;

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
                bool enabled,
                Func<string, string, TrailSettingMode> getPropertyMode,
                Action<ModSettingsDefinition, bool> applyEditorModes,
                Action<string, ModSettingsDefinition> applyEditorModesForMod,
                EditorModSettingsSaveOptionsViewModel editorSaveOptions)
            {
                this.log = log;
                this.enabled = enabled;
                this.getPropertyMode = getPropertyMode ?? ((_, __) => TrailSettingMode.ModDefault);
                this.applyEditorModes = applyEditorModes;
                this.applyEditorModesForMod = applyEditorModesForMod;
                this.editorSaveOptions = editorSaveOptions ?? throw new ArgumentNullException(nameof(editorSaveOptions));
                customizationBridge = new BugfixesAndQoLTrailCustomizationBridge(log);
            }

            public void SetEnabled(bool value)
            {
                enabled = value;
                foreach (Button button in injectedCoopButtons)
                    button.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (coopTrailExportCheckbox != null)
                    coopTrailExportCheckbox.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (!value)
                {
                    uploadOptions.Close();
                    SetCoopPackagePresentation(null, 0);
                    if (MainViewModel.viewModelLoaded && MainViewModel.Instance != null)
                        MainViewModel.Instance.Show_TrailCustomisationButtons = false;
                    ExitContext(force: true);
                }
                if (externalButtonOwner)
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
                    lordRules);
                DebugLogHelper.LogInfo(
                    log,
                    "Custom Lord preflight rules: Script Extender=" + lordRules.ExtenderIdentity +
                    ", knownProfile=" + lordRules.IsKnownIdentity +
                    ", reflectedLordInfoFields=" + lordRules.LordInfoFields.Count +
                    ", reflectedMessageTypes=" + lordRules.MessageTypes.Count +
                    ", publicValidator=" + (lordRules.PublicValidator != null) + ".");
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding("ExtendedDataUploadOptionsHost", uploadOptions);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        "Extended Data upload checkbox binding failed; additional files remain enabled: " + exception);
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
                DebugLogHelper.LogInfo(log, "Trail mission-settings coordinator initialized.");
            }

            public void Dispose()
            {
                ModSettingsWorkingSourceRegistry.Unregister(this);
                SetCoopPackagePresentation(null, 0);
                foreach (IDisposable hook in hooks)
                    hook.Dispose();
                hooks.Clear();
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
                    document = ModSettingsJson.NormalizeAndValidate(document, source + ".modSettings");
                    ApplyDocument(document, editable, presetLabel);
                    DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
                    return GetMissingMentionedMods(document);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not load {source} mod settings; sidecar mod settings are ignored: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable, presetLabel);
                    return Array.Empty<string>();
                }
            }

            internal string[] EnterStrict(
                ModSettingsDefinition document,
                bool editable,
                string source,
                string presetLabel)
            {
                document = ValidateStrict(document, source);
                ApplyDocument(document, editable, presetLabel);
                DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
                return GetMissingMentionedMods(document);
            }

            internal ModSettingsDefinition ValidateStrict(ModSettingsDefinition document, string source)
            {
                document = ModSettingsJson.NormalizeAndValidate(document, source + ".modSettings");
                ValidateDocumentValues(document);
                return document;
            }

            internal ModSettingsDefinition CaptureCurrentDocument() => CaptureDocument();

            internal void SetMapSourceDocument(ModSettingsDefinition document, string contextId = null)
            {
                mapSourceDocument = CloneDocument(document);
                if (trailSourceDocument == null)
                    workingSourceContextId = document == null ? string.Empty : "map:" + (contextId ?? string.Empty);
                SourcesChanged?.Invoke();
            }

            public IReadOnlyList<ModSettingsWorkingSource> GetSources(string targetGuid)
            {
                var result = new List<ModSettingsWorkingSource>();
                if (trailSourceDocument != null)
                    result.Add(new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.TrailId, Kind = ModSettingsWorkingSourceKind.Trail, DisplayName = "Trail settings", IsPreferred = true, PreferenceContextId = workingSourceContextId });
                if (mapSourceDocument != null)
                    result.Add(new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.MapId, Kind = ModSettingsWorkingSourceKind.Map, DisplayName = "Map settings", IsPreferred = trailSourceDocument == null, PreferenceContextId = workingSourceContextId });
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
                    prepared[id] = MaterializeSource(id, endpoint, source);
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
                        catch (Exception rollbackException) { DebugLogHelper.LogError(log, "Could not roll back source application for [" + id + "]: " + rollbackException); }
                    }
                    applyEditorModes?.Invoke(oldModes, false);
                    throw;
                }
            }

            private ModSettingsDefinition ResolveWorkingSource(string sourceId)
            {
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                    return ModSettingsDefinition.CreateModDefaults();
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.TrailId, StringComparison.Ordinal) && trailSourceDocument != null)
                    return CloneDocument(trailSourceDocument);
                if (string.Equals(sourceId, ModSettingsWorkingSourceRegistry.MapId, StringComparison.Ordinal) && mapSourceDocument != null)
                    return CloneDocument(mapSourceDocument);
                throw new InvalidOperationException("The selected reset source is unavailable.");
            }

            private Dictionary<string, byte[]> MaterializeSource(string modId, IModSettingsWorkingCopyEndpoint endpoint, ModSettingsDefinition document)
            {
                Dictionary<string, byte[]> snapshot = endpoint.System_CreateModDefaultSnapshot();
                if (document?.Mods == null || !document.Mods.TryGetValue(modId, out ModSettingsEntry entry) || entry == null)
                    return snapshot;
                Dictionary<string, PropertyInfo> properties = GetPersistedProperties(endpoint);
                Dictionary<string, byte[]> player = endpoint.System_CreatePlayerMissionPresetSnapshot();
                foreach (string propertyName in entry.PlayerSettings ?? Array.Empty<string>())
                    if (properties.ContainsKey(propertyName) && player.TryGetValue(propertyName, out byte[] bytes)) snapshot[propertyName] = (byte[])bytes.Clone();
                foreach (KeyValuePair<string, object> setting in entry.Overrides ?? new Dictionary<string, object>(StringComparer.Ordinal))
                    if (properties.TryGetValue(setting.Key, out PropertyInfo property)) snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, ConvertJsonValue(setting.Value, property.PropertyType));
                return snapshot;
            }

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
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(participant.Value);
                    foreach (KeyValuePair<string, object> setting in entry.Overrides)
                    {
                        if (properties.TryGetValue(setting.Key, out PropertyInfo property))
                            ConvertJsonValue(setting.Value, property.PropertyType);
                    }
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
                        DebugLogHelper.LogInfo(log, "Deferred Trail mod-settings cleanup while Custom Trail setup/mission is active.");
                    }
                    return;
                }
                if (!trailContext)
                {
                    preserveContextForLaunch = false;
                    customTrailLaunchActive = false;
                    customTrailSetupRestartInfo = null;
                    customTrailSetupHeader = null;
                    cleanupDeferralLogged = false;
                    ClearActiveSidecar();
                    ClearTrailMakerAuthoringState();
                    missionPresetLifecycle.Reset();
                    workingContextEditable = false;
                    trailSourceDocument = null;
                    mapSourceDocument = null;
                    workingSourceContextId = string.Empty;
                    SourcesChanged?.Invoke();
                    return;
                }

                ExitActiveParticipants();
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
                trailSourceDocument = null;
                mapSourceDocument = null;
                workingSourceContextId = string.Empty;
                SourcesChanged?.Invoke();
                DebugLogHelper.LogInfo(log, "Left " + activeContextLabel + " mod-settings context.");
                activeContextLabel = "Trail";
            }

            public bool HandleMissionEnded(MissionLifecycleNotification notification)
            {
                MissionEndReason reason = notification == null
                    ? MissionEndReason.Unloaded
                    : notification.EndReason;
                MissionPresetEndAction action = missionPresetLifecycle.End(MapEndKind(reason));
                if (action == MissionPresetEndAction.Preserve)
                {
                    DebugLogHelper.LogInfo(
                        log,
                        "Retained the active Map/Trail mod-settings preset across an expected mission replacement.");
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
                    DebugLogHelper.LogInfo(
                        log,
                        "Suspended the Trail Maker mission preset until the authoring lobby returns.");
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
                MissionPresetLaunchKind pending = missionPresetLifecycle.PendingLaunch;
                if (pending == MissionPresetLaunchKind.None)
                    return true;

                GameModeKind actual = notification == null || notification.Context == null
                    ? GameModeKind.Unknown
                    : notification.Context.Mode.Kind;
                bool matches =
                    (pending == MissionPresetLaunchKind.CustomTrail && actual == GameModeKind.CustomTrail) ||
                    (pending == MissionPresetLaunchKind.CoopTrail && actual == GameModeKind.CoopTrail) ||
                    (pending == MissionPresetLaunchKind.TrailMakerTest && actual == GameModeKind.CustomGame);
                if (!matches || !missionPresetLifecycle.ConfirmStarted(pending))
                {
                    DebugLogHelper.LogError(
                        log,
                        "The prepared Map/Trail mod-settings preset did not match the started mission; " +
                        "expected=" + pending + ", actual=" + actual + ".");
                    ExitContext(force: true);
                    return false;
                }

                DebugLogHelper.LogInfo(log, "Confirmed active mission preset: " + pending + ".");
                return true;
            }

            internal void PrepareCoopMissionLaunch()
            {
                missionPresetLifecycle.Prepare(MissionPresetLaunchKind.CoopTrail);
            }

            internal bool PrepareTrailMakerTestLaunch()
            {
                if (!enabled)
                    return true;
                if (missionPresetLifecycle.PendingLaunch == MissionPresetLaunchKind.TrailMakerTest)
                    return true;
                try
                {
                    CaptureTrailMakerWorkingDocument("test launch");
                    ApplyDocument(
                        trailMakerWorkingDocument ?? ModSettingsDefinition.CreateModDefaults(),
                        editable: false);
                    missionPresetLifecycle.Prepare(MissionPresetLaunchKind.TrailMakerTest);
                    return true;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, "Could not prepare Trail Maker test mod settings: " + exception);
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
                if (!enabled)
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
                    DebugLogHelper.LogInfo(
                        log,
                        "Captured Trail mod settings before save; mentioned=[" + string.Join(", ", mentionedMods) + "].");
                    if (editorSaveOptions.IncludeTrailModSettings)
                    {
                        // Vanilla can enter Trail export before this save call returns. Keep the
                        // synchronous capture available to both exporters until it reaches disk.
                        capturedDocumentsByTrailPath[IOPath.GetFullPath(trailPath)] = document;
                    }
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Could not capture the Trail Maker working settings before saving [{trailPath}]: {exception}");
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
                        trailSourceDocument = CloneDocument(document);
                    }
                    else
                    {
                        if (File.Exists(sidecar))
                            File.Delete(sidecar);
                        trailSourceDocument = null;
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
                    DebugLogHelper.LogError(log, $"Could not publish the selected Trail mod-settings state for [{trailPath}]: {exception}");
                    ShowInformation(
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailedTitle"),
                        SerpLocalization.Get("EditorSave.ModSettingsSaveFailed"));
                    return;
                }

                try
                {
                    // Keep the just-saved mission editable even if Vanilla rebuilt the UI.
                    ApplyDocument(document, editable: true);
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
                    DebugLogHelper.LogError(
                        log,
                        $"Saved Trail mod settings for [{trailPath}], but could not reactivate the editable Trail preset: {exception}");
                }
            }

            private void ManageTrailButtonHook(FRONT_ManageTrail self, string command)
            {
                if (!enabled)
                {
                    manageTrailButtonOriginal(self, command);
                    return;
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
                        DebugLogHelper.LogError(log, $"Could not load editable Trail mod settings: {exception}");
                    }
                }
                pendingTrailMakerLoad = string.Equals(command, "Load", StringComparison.Ordinal);
                pendingTrailMakerTrailPath = loadedHeader?.filePath;
                try
                {
                    manageTrailButtonOriginal(self, command);
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
                FileRow selectedRow = (self.FindName("UploadList") as ListView)?.SelectedItem as FileRow;
                bool doUpload = string.Equals(command, "DoUpload", StringComparison.Ordinal);
                bool isTrail = selectedRow?.trail != null;
                bool isCustomLord = selectedRow?.lord != null;
                if (doUpload && ((enabled && isTrail) || isCustomLord))
                {
                    string uploadRoot = ConfigSettings.GetWorkshopUploadContentPath();
                    string itemName = isTrail ? selectedRow.trail.Name : selectedRow.lord.lordName;
                    if (!WorkshopUploadStaging.TryResetDirectChild(uploadRoot, itemName, out _, out string cleanupError))
                    {
                        DebugLogHelper.LogError(log, $"Extended Data Workshop staging cleanup failed for [{itemName}]: {cleanupError}");
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
                            DebugLogHelper.LogError(log, $"Could not upload the Coop Trail package to Steam Workshop: {exception}");
                            HUD_ConfirmationPopup.ShowOK(
                                Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 125),
                                delegate { });
                        }
                        return;
                    }

                    ArmUploadDecision(uploadRoot, itemName, uploadOptions.IncludeExtendedData);
                    try
                    {
                        editorSetupButtonOriginal(self, command);
                    }
                    catch
                    {
                        ClearUploadDecision(uploadRoot, itemName);
                        throw;
                    }
                    return;
                }

                editorSetupButtonOriginal(self, command);

                if (string.Equals(command, "UploadTrail", StringComparison.Ordinal))
                {
                    if (enabled)
                        TryFileOperation("add Coop Trails to Vanilla's Workshop upload list", () => AddCoopWorkshopRows(self));
                }
                else if (string.Equals(command, "Upload", StringComparison.Ordinal))
                {
                    if (selectedRow?.trail != null && enabled)
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
                PendingUploadDecision decision = GetUploadDecision(nameMap, mapTitle);
                Action terminalSuccess = WrapTerminalCallback(nameMap, mapTitle, successAction);
                Action terminalFailure = WrapTerminalCallback(nameMap, mapTitle, failAction);
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
                            request.FailAction));
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
                            DebugLogHelper.LogError(
                                log,
                                $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {resolutionError}");
                            InvokeUploadFailure(mapTitle, terminalFailure);
                            return;
                        }
                        if (!CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                                source,
                                nameMap,
                                stagingChild,
                                out int copiedJsonFiles,
                                out int existingJsonFiles,
                                out string stagingError))
                        {
                            DebugLogHelper.LogError(
                                log,
                                $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {stagingError}");
                            InvokeUploadFailure(mapTitle, terminalFailure);
                            return;
                        }
                        DebugLogHelper.LogInfo(
                            log,
                            $"Extended CPU Lord JSON staging ready for [{mapTitle}]: {copiedJsonFiles} copied, {existingJsonFiles} already present.");
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"Extended CPU Lord upload [{mapTitle}] aborted because JSON staging failed: {exception}");
                        InvokeUploadFailure(mapTitle, terminalFailure);
                        return;
                    }
                }
                else if (decision != null && decision.IncludeExtendedData && IsCustomTrailUpload(tags))
                {
                    string source = IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), mapTitle);
                    string destination = IOPath.Combine(nameMap, mapTitle);
                    if (!WorkshopUploadStaging.TryStageTrailJsonFiles(
                            source,
                            destination,
                            out int copiedFiles,
                            out string error))
                    {
                        DebugLogHelper.LogError(
                            log,
                            $"Custom Trail JSON files could not be staged for [{mapTitle}]; upload aborted: {error}");
                        InvokeUploadFailure(mapTitle, terminalFailure);
                        return;
                    }
                    DebugLogHelper.LogInfo(
                        log,
                        $"Added {copiedFiles} Custom Trail JSON file(s) to Workshop staging for [{mapTitle}].");
                }
                else if (decision != null && !decision.IncludeExtendedData && IsCustomTrailUpload(tags))
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Custom Trail upload [{mapTitle}] excludes mod-settings sidecars by explicit user choice.");
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
                    DebugLogHelper.LogError(
                        log,
                        $"Vanilla's Workshop failure callback failed for [{mapTitle}]: {exception}");
                }
            }

            private void ArmUploadDecision(string root, string itemName, bool includeExtendedData)
            {
                lock (uploadDecisionLock)
                    pendingUploadDecision = new PendingUploadDecision(root, itemName, includeExtendedData);
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

            private Action WrapTerminalCallback(string root, string itemName, Action callback)
            {
                return () =>
                {
                    ClearUploadDecision(root, itemName);
                    callback?.Invoke();
                };
            }

            private void ClearUploadDecision(string root, string itemName)
            {
                lock (uploadDecisionLock)
                {
                    if (pendingUploadDecision?.Matches(root, itemName) == true)
                        pendingUploadDecision = null;
                }
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
                if (!enabled)
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
                foreach (CoopTrailSource source in GetImportableCoopSources(includeWorkshop: false))
                {
                    if (!existing.Add(source.SelectionName))
                        continue;
                    string packageRoot = source.PackageRoot;
                    CoopTrailPackage package = CoopTrailPackageCatalog.Load(packageRoot);
                    var trail = new MapFileManager.CustomTrailInfo
                    {
                        Name = source.SelectionName,
                        DisplayName = package.Manifest.DisplayName,
                        FullPath = packageRoot,
                        workshopUploadInfoAvailable = File.Exists(IOPath.Combine(packageRoot, source.SelectionName + ".data")),
                    };
                    // Vanilla derives Count from the headers dictionary. Placeholder keys retain
                    // its existing length display and Short/Medium/Long Workshop categorisation.
                    for (int mission = 1; mission <= package.Manifest.MissionCount; mission++)
                        trail.headers[mission.ToString("00", CultureInfo.InvariantCulture)] = null;
                    var row = new FileRow
                    {
                        Text1 = package.Manifest.DisplayName,
                        Text2 = package.Manifest.MissionCount.ToString(CultureInfo.InvariantCulture),
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
                DebugLogHelper.LogInfo(
                    log,
                    $"Prepared Coop Trail Workshop package [{trail.Name}] with " +
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
                IEnumerable<CoopTrailSource> sources,
                bool registerImportSources)
            {
                var existing = new HashSet<string>(
                    rows.Where(row => row != null).Select(row => row.Text1),
                    StringComparer.OrdinalIgnoreCase);
                foreach (CoopTrailSource source in sources)
                {
                    if (!existing.Add(source.SelectionName))
                        continue;
                    if (registerImportSources)
                        coopImportSourceBySelection[source.SelectionName] = source.PackageRoot;
                    rows.Add(new FileRow
                    {
                        Text1 = source.SelectionName,
                        Text2 = source.MissionCount.ToString(CultureInfo.InvariantCulture),
                    });
                }
            }

            private IEnumerable<CoopTrailSource> GetImportableCoopSources(bool includeWorkshop)
            {
                string localRoot = IOPath.GetFullPath(ConfigSettings.GetUserCustomTrailsPath());
                var roots = new List<string> { localRoot };
                if (includeWorkshop)
                    roots.AddRange(Shared.WorkshopContentPaths.GetSubscribedItemRoots(message =>
                        DebugLogHelper.LogWarning(log, message)));
                var catalog = new CoopTrailPackageCatalog();
                catalog.Scan(roots, null, message => DebugLogHelper.LogWarning(log, message));
                foreach (CoopTrailPackage package in catalog.Packages.Values
                    .OrderBy(item => item.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    string source = IOPath.Combine(package.RootPath, CoopTrailMakerSourceDirectory);
                    if (!Directory.Exists(source))
                        continue;
                    int count = Directory.GetFiles(source, "*.trail").Length;
                    if (count > 0)
                    {
                        bool workshop = !IsDirectChildOf(package.RootPath, localRoot);
                        string selectionName = workshop
                            ? package.Manifest.DisplayName + " [Steam Workshop]"
                            : IOPath.GetFileName(package.RootPath);
                        yield return new CoopTrailSource(selectionName, package.RootPath, count);
                    }
                }
            }

            private static bool IsDirectChildOf(string directory, string parent)
            {
                string actualParent = IOPath.GetDirectoryName(IOPath.GetFullPath(directory));
                return string.Equals(actualParent, IOPath.GetFullPath(parent), StringComparison.OrdinalIgnoreCase);
            }

            private void BackupHook(FRONT_ManageTrail self, string source, string destination)
            {
                backupOriginal(self, source, destination);
                if (!enabled)
                    return;
                TryFileOperation("back up Trail sidecars", () => CopySidecars(source, destination, overwrite: true));
                TryFileOperation("back up the Coop Trail Maker marker", () => CopyCoopMarker(source, destination));
                TryFileOperation("back up the Coop Trail package", () => CopyCoopPackage(source, destination));
            }

            private void ImportHook(FRONT_ManageTrail self, string customFolderName)
            {
                if (!enabled)
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
                    CopySidecars(trailSource, ConfigSettings.GetUserTrailMakerPath(), overwrite: false));
                TryFileOperation("import the Coop Trail Maker state", () =>
                {
                    SetMakerCoopEnabled(coopPackage);
                    RefreshTrailMakerCoopCheckbox();
                });
            }

            private sealed class CoopTrailSource
            {
                public CoopTrailSource(string selectionName, string packageRoot, int missionCount)
                {
                    SelectionName = selectionName;
                    PackageRoot = packageRoot;
                    MissionCount = missionCount;
                }

                public string SelectionName { get; }
                public string PackageRoot { get; }
                public int MissionCount { get; }
            }

            private void ExportHook(FRONT_ManageTrail self, string destination)
            {
                if (!enabled)
                {
                    exportOriginal(self, destination);
                    return;
                }
                CoopTrailPackageExporter.PreparedPackage prepared = null;
                bool exportCoop = IsMakerCoopEnabled();
                if (exportCoop)
                {
                    try
                    {
                        prepared = new CoopTrailPackageExporter().Prepare(
                            ConfigSettings.GetUserTrailMakerPath(),
                            destination,
                            ReadModSettingsForExport);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, "Could not prepare Coop Trail export: " + exception);
                        ShowInformation(
                            SerpLocalization.Get("ExtendedData.ExportFailedTitle"),
                            SerpLocalization.Get("ExtendedData.ExportFailed") + "\r\n" + exception.Message);
                        return;
                    }
                }

                try
                {
                    if (prepared != null)
                    {
                        string trailMakerSource = IOPath.Combine(destination, CoopTrailMakerSourceDirectory);
                        Directory.CreateDirectory(trailMakerSource);
                        exportOriginal(self, trailMakerSource);
                        ExportSidecars(trailMakerSource);
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
                        RemoveCoopPackage(destination);
                    }
                    CoopPackagesChanged?.Invoke();
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, "Could not finish Trail export: " + exception);
                    ShowInformation(
                        SerpLocalization.Get("ExtendedData.ExportFailedTitle"),
                        SerpLocalization.Get("ExtendedData.ExportFailed") + "\r\n" + exception.Message);
                }
                finally
                {
                    prepared?.Dispose();
                }
            }

            private void ExportSidecars(string destination)
            {
                TryFileOperation("export Trail sidecars", () =>
                {
                    foreach (string stale in Directory.GetFiles(
                        destination,
                        "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                        File.Delete(stale);

                    string makerRoot = ConfigSettings.GetUserTrailMakerPath();
                    int outputIndex = 0;
                    for (int sourceIndex = 0; sourceIndex < 50; sourceIndex++)
                    {
                        string sourceTrail = IOPath.Combine(makerRoot, FRONT_ManageTrail.GetMakerFileName(sourceIndex) + ".trail");
                        if (!File.Exists(sourceTrail))
                            continue;
                        if (TryReadModSettingsForExport(sourceTrail, out ModSettingsDefinition document))
                        {
                            string target = IOPath.Combine(
                                destination,
                                FRONT_ManageTrail.GetMakerFileName(outputIndex) + MissionLoader.ModSettingsFileSuffix);
                            ModSettingsJson.WriteAtomic(target, document);
                        }
                        outputIndex++;
                    }
                });
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
                    DebugLogHelper.LogInfo(log, $"Using synchronously captured Trail mod settings for export [{fullTrailPath}].");
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
                if (!enabled)
                    return;
                TryFileOperation("clear Trail sidecars", () =>
                {
                    foreach (string sidecar in Directory.GetFiles(
                        ConfigSettings.GetUserTrailMakerPath(),
                        "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
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
                    Visibility = enabled ? Visibility.Visible : Visibility.Collapsed,
                };
                ToolTipService.SetShowDuration(checkbox, 60000);
                checkbox.Click += (_, __) =>
                {
                    try
                    {
                        SetMakerCoopEnabled(checkbox.IsChecked == true);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, "Could not save Coop Trail Maker state: " + exception);
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
                string nested = IOPath.Combine(packageRoot, CoopTrailMakerSourceDirectory);
                // Packages produced before 1.3.5 kept their editable sources in the root.
                return Directory.Exists(nested) ? nested : packageRoot;
            }

            private static void RemoveNormalTrailFiles(string destination)
            {
                foreach (string trail in Directory.GetFiles(destination, "*.trail"))
                    File.Delete(trail);
                foreach (string sidecar in Directory.GetFiles(
                    destination,
                    "Trail_Mission_*" + MissionLoader.ModSettingsFileSuffix))
                    File.Delete(sidecar);
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

            private static void ShowInformation(string title, string message)
            {
                HUD_ConfirmationPopup.ShowConfirmationOKMessage(title, delegate { }, message);
            }

            private void StartCustomTrailHook(MainViewModel self, string trailName, int missionId, int difficulty)
            {
                bool customizedRestart = false;
                if (!preserveContextForLaunch)
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
                if (!enabled)
                {
                    startCustomTrailOriginal(self, trailName, missionId, difficulty);
                    return;
                }
                if (!preserveContextForLaunch)
                {
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
                        DebugLogHelper.LogError(log, $"Could not prepare Custom Trail mod settings: {exception}");
                        ApplyDocument(ModSettingsDefinition.CreateModDefaults(), editable: false);
                    }
                }
                else if (workingContextEditable)
                {
                    // Customize is an editable draft. The launched mission receives the
                    // materialized result as a read-only working snapshot.
                    ApplyDocument(CaptureDocument(), editable: false, presetLabel: "Trail");
                }
                preserveContextForLaunch = false;
                customTrailLaunchActive = true;
                missionPresetLifecycle.Prepare(MissionPresetLaunchKind.CustomTrail);
                startCustomTrailOriginal(self, trailName, missionId, difficulty);
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
                if (enabled && trailMaker && !pendingTrailMakerLoad &&
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
                if (!enabled)
                    return;
                if (preserve)
                    preserveContextForLaunch = false;
                if (trailMaker)
                {
                    if (IsConfirmedTrailMakerLobby(self))
                        ActivateTrailMakerLobby(restartInfo);
                    else
                    {
                        DebugLogHelper.LogWarning(
                            log,
                            "Trail Maker lobby transition did not complete; discarded its pending mod-settings state.");
                        ExitContext(force: true);
                    }
                }
                LobbyOpened?.Invoke(self);
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
                if (!enabled)
                {
                    startSkirmishGameOriginal(self, customTrailRestartInfo);
                    return;
                }
                if (self?.trailMakerMode == true)
                {
                    if (!PrepareTrailMakerTestLaunch())
                        return;
                }
                if (enabled && self?.singlePlayerCoop == true && self.currentLobby?.coopTrailGame == true)
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
                    DebugLogHelper.LogInfo(
                        log,
                        $"Starting customized Custom Trail [{customTrailRestartInfo.customTrailName}] " +
                        $"mission {customTrailRestartInfo.customTrailLevel}.");
                }

                if (customTrailRestartInfo != null && customTrailRestartInfo.customTrail)
                    customTrailLaunchActive = true;
                startSkirmishGameOriginal(self, customTrailRestartInfo);
                customTrailSetupRestartInfo = null;
                customTrailSetupHeader = null;
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
                trailSelectionOriginal(self, missionId, fromRealClick);
                if (enabled && !openingCustomTrailSetup &&
                    FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
                    EnterSelectedCustomTrail(self);
            }

            private void FrontendButtonHook(FrontendMenus self, string command)
            {
                bool preserveTrailMakerMapEditor = string.Equals(command, "MapEditor", StringComparison.Ordinal) &&
                    MainViewModel.Instance.FRONTMultiplayer.trailMakerMode;
                if (!enabled)
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
                if (!externalButtonOwner &&
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
                        DebugLogHelper.LogError(log, $"Could not open Custom Trail setup: {exception}");
                    }
                    return;
                }
                if (preserveTrailMakerMapEditor)
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

                    DebugLogHelper.LogWarning(
                        log,
                        "Could not find the logical title element after Loaded for Coop Trail " +
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
                    DebugLogHelper.LogError(log, $"Could not select Custom Trail mod settings: {exception}");
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
                DebugLogHelper.LogInfo(
                    log,
                    $"Opened Custom Trail setup [{menus.CustomTrailName}] mission {missionId}; " +
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

                bool packageOccupiesTrail = enabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                    coopPackageMissionCount > zeroBasedTrail * 10;
                title.Text = packageOccupiesTrail
                    ? coopPackageDisplayName
                    : vanillaCoopTrailTitles[zeroBasedTrail];
                return true;
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
                    }

                    string vanillaTitle = vanillaCoopTrailTitles[zeroBasedTrail];
                    if (string.IsNullOrEmpty(vanillaTitle))
                        continue;
                    bool packageOccupiesTrail = enabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                        coopPackageMissionCount > zeroBasedTrail * 10;
                    PropEx.SetTextCentre(button, packageOccupiesTrail ? coopPackageDisplayName : vanillaTitle);
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
                        DebugLogHelper.LogError(log, $"Could not open Coop Trail setup: {exception}");
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
                    DebugLogHelper.LogWarning(log, "Ignored Coop Trail Customize click from a non-host client.");
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
                        typeof(FRONT_Multiplayer).GetMethod("SetupSkirmishModeSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(self, null);
                        typeof(FRONT_Multiplayer).GetMethod("updateSteamIDMappings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(self, null);
                        typeof(FRONT_Multiplayer).GetMethod("UpdateRadarShieldPositions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(self, null);
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
                MainViewModel.Instance.Show_MultiplayerSetup = true;
                if (trailId == 0) MainViewModel.Instance.Show_CoopTrail1 = false;
                if (trailId == 1) MainViewModel.Instance.Show_CoopTrail2 = false;
                if (trailId == 2) MainViewModel.Instance.Show_CoopTrail3 = false;
                if (trailId == 3) MainViewModel.Instance.Show_CoopTrail4 = false;
                // ShowSetupScreen rebuilds lobby settings. Reapply the selected mission only
                // after that transition, matching the working Custom Trail Customize path.
                CoopSetupOpened?.Invoke();
                DebugLogHelper.LogInfo(log, $"Opened Coop Trail setup trail={trailId + 1}, mission={mission}, source={source}.");
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
                    DebugLogHelper.LogError(log, "Refused to broadcast an invalid Built-in Customize origin.");
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
                DebugLogHelper.LogInfo(
                    log,
                    $"Broadcast Built-in Customize launch origin: trailType={packet.TrailType}, " +
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
                DebugLogHelper.LogInfo(
                    log,
                    $"Broadcast Coop Trail {(launch ? "launch" : "setup")} transition " +
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
                UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                    () => ProcessCoopCustomizePacket(packet, new CSteamID(senderSteamId)));
            }

            private void ProcessCoopCustomizePacket(CoopCustomizePacket packet, CSteamID senderSteamId)
            {
                try
                {
                    CSteamID? host = GameNetworkAPI.GetHostSteamId();
                    if (!host.HasValue || senderSteamId != host.Value)
                    {
                        DebugLogHelper.LogError(
                            log,
                            "Rejected Coop Trail transition from a sender that is not the lobby host.");
                        return;
                    }

                    if (packet == null || packet.ProtocolVersion != CoopCustomizeProtocolVersion ||
                        packet.TrailId < 0 || packet.TrailId > 3 || packet.MissionId < 1 || packet.MissionId > 10)
                    {
                        DebugLogHelper.LogError(log, "Rejected invalid Coop Trail transition packet.");
                        return;
                    }

                    FRONT_Multiplayer self = MainViewModel.Instance.FRONTMultiplayer;
                    if (!enabled || self?.currentLobby == null || self.currentLobby.isHost ||
                        self.singlePlayerCoop || !self.currentLobby.coopTrailGame)
                    {
                        DebugLogHelper.LogWarning(log, "Ignored Coop Trail transition outside an active client Coop lobby.");
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
                    DebugLogHelper.LogError(log, "Could not apply Coop Trail transition from host: " + exception);
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
                UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                    () => ProcessBuiltInCustomizeOriginPacket(packet, new CSteamID(senderSteamId)));
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
                        DebugLogHelper.LogError(
                            log,
                            "Rejected Built-in Customize origin from a sender that is not the lobby host.");
                        return;
                    }

                    if (!BuiltInCustomizeOriginPacket.IsValid(packet))
                    {
                        ExtendedDataLaunchOriginApi.Clear();
                        DebugLogHelper.LogError(log, "Rejected invalid Built-in Customize origin packet.");
                        return;
                    }

                    FRONT_Multiplayer self = MainViewModel.Instance?.FRONTMultiplayer;
                    if (self?.currentLobby == null || self.currentLobby.isHost ||
                        !FRONT_Multiplayer.skirmishGame || FRONT_Multiplayer.coopGame)
                    {
                        DebugLogHelper.LogWarning(
                            log,
                            "Ignored Built-in Customize origin outside an active client Skirmish lobby.");
                        return;
                    }

                    CaptureBuiltInCustomizeOrigin(packet.TrailType, packet.MissionId);
                    DebugLogHelper.LogInfo(
                        log,
                        $"Accepted Built-in Customize launch origin from host: trailType={packet.TrailType}, " +
                        $"missionIndex={packet.MissionId}.");
                }
                catch (Exception exception)
                {
                    ExtendedDataLaunchOriginApi.Clear();
                    DebugLogHelper.LogError(log, "Could not apply Built-in Customize origin from host: " + exception);
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
                        bool sidecarExists = EnterSidecar(requestedTrailPath, editable: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), requestedTrailPath);
                        source = sidecarExists ? "loaded mission sidecar" : "loaded mission defaults";
                    }
                    else if (pendingTrailMakerLoad)
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        source = "unavailable loaded mission defaults";
                    }
                    else if (missionPresetLifecycle.AwaitingTrailMakerReturn && trailMakerWorkingDocument != null)
                    {
                        ModSettingsDefinition document = trailMakerWorkingDocument;
                        ApplyDocument(document, editable: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), trailMakerTrailPath);
                        source = restartInfo?.customTestMission == true
                            ? "test return draft"
                            : "authoring session draft";
                    }
                    else
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true, useFixedDefaults: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        source = "new mission defaults";
                    }
                    missionPresetLifecycle.CompleteTrailMakerReturn();
                    DebugLogHelper.LogInfo(
                        log,
                        "Activated editable Trail Maker mod-settings context from " + source + ".");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        "Could not activate the Trail Maker mod-settings draft; applying editable defaults: " + exception);
                    try
                    {
                        ModSettingsDefinition defaults = ModSettingsDefinition.CreateModDefaults();
                        ApplyDocument(defaults, editable: true, useFixedDefaults: true);
                        UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                        missionPresetLifecycle.CompleteTrailMakerReturn();
                        DebugLogHelper.LogInfo(
                            log,
                            "Activated editable Trail Maker mod-settings context from fail-closed defaults.");
                    }
                    catch (Exception fallbackException)
                    {
                        DebugLogHelper.LogError(
                            log,
                            "Could not activate fail-closed Trail Maker defaults; leaving the authoring context: " +
                            fallbackException);
                        try
                        {
                            ExitContext(force: true);
                        }
                        catch (Exception cleanupException)
                        {
                            DebugLogHelper.LogError(
                                log,
                                "Could not fully clean up the failed Trail Maker context: " + cleanupException);
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
                    DebugLogHelper.LogInfo(
                        log,
                        "Captured editable Trail Maker mod-settings draft before " + transition + ".");
                }
                catch (Exception exception)
                {
                    trailMakerWorkingDocument = ModSettingsDefinition.CreateModDefaults();
                    trailMakerTrailPath = null;
                    trailMakerAuthoringActive = true;
                    DebugLogHelper.LogError(
                        log,
                        "Could not capture the Trail Maker mod-settings draft before " + transition +
                        "; the return will use editable defaults: " + exception);
                }
            }

            private void UpdateTrailMakerWorkingDocument(ModSettingsDefinition document, string trailPath)
            {
                // Store an independent normalized copy because ApplyDocument removes obsolete
                // properties from the instance it receives.
                trailMakerWorkingDocument = ModSettingsJson.ParseObject(ModSettingsJson.Serialize(document));
                trailMakerTrailPath = string.IsNullOrWhiteSpace(trailPath)
                    ? null
                    : IOPath.GetFullPath(trailPath);
                trailMakerAuthoringActive = true;
            }

            private void ClearTrailMakerAuthoringState()
            {
                trailMakerWorkingDocument = null;
                trailMakerTrailPath = null;
                pendingTrailMakerTrailPath = null;
                pendingTrailMakerLoad = false;
                trailMakerAuthoringActive = false;
                missionPresetLifecycle.Reset();
            }

            private bool EnterSidecar(string trailPath, bool editable, bool previewOnly = false)
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
                    string.Equals(activeSidecarPath, sidecar, StringComparison.OrdinalIgnoreCase) &&
                    activeSidecarLength == length && activeSidecarWriteTicks == writeTicks &&
                    AreAllTrailPresetsActive())
                {
                    return exists;
                }

                ModSettingsDefinition document = exists
                    ? ModSettingsJson.Read(sidecar)
                    : ModSettingsDefinition.CreateModDefaults();
                trailSourceDocument = exists ? CloneDocument(document) : null;
                workingSourceContextId = "trail:" + IOPath.GetFullPath(sidecar);
                SourcesChanged?.Invoke();
                ApplyDocument(document, editable, useFixedDefaults: !exists && editable,
                    previewOnly: previewOnly);
                string[] mentionedMods = document.Mods.Keys.ToArray();
                DebugLogHelper.LogInfo(
                    log,
                    $"Loaded Trail sidecar [{sidecar}]; exists={exists}, editable={editable}, " +
                    "mentioned=[" + string.Join(", ", mentionedMods) + "].");
                activeSidecarPath = sidecar;
                activeSidecarLength = length;
                activeSidecarWriteTicks = writeTicks;
                activeSidecarEditable = editable;
                activeSidecarPreviewOnly = previewOnly;
                return exists;
            }

            private ModSettingsDefinition CaptureDocument()
            {
                ModSettingsDefinition document = ModSettingsDefinition.CreateModDefaults();
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();

                foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in participants)
                {
                    IModSettingsPresetEndpoint viewModel = participant.Value;
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(viewModel);
                    var target = new ModSettingsEntry();
                    foreach (PropertyInfo property in properties.Values)
                    {
                        TrailSettingMode mode = getPropertyMode(participant.Key, property.Name);
                        if (mode == TrailSettingMode.Player)
                        {
                            target.PlayerSettings = target.PlayerSettings
                                .Concat(new[] { property.Name })
                                .ToArray();
                        }
                        else if (mode == TrailSettingMode.Fixed)
                        {
                            object value = property.GetValue(viewModel);
                            target.Overrides[property.Name] = ModSettingsJson.IsSupportedValue(value)
                                ? value
                                : EncodeSettingValue(property.PropertyType, value);
                        }
                    }
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
                bool previewOnly = false)
            {
                ClearActiveSidecar();
                Dictionary<string, IModSettingsPresetEndpoint> allParticipants = FindCompatibleViewModels();
                ExitActiveParticipants(allParticipants);
                var prepared = new List<Tuple<string, IModSettingsPresetEndpoint, Dictionary<string, byte[]>, bool>>(allParticipants.Count);
                foreach (KeyValuePair<string, IModSettingsPresetEndpoint> participant in allParticipants)
                {
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(participant.Value);
                    string[] removedSettings = ModSettingsJson.RemoveUnknownSettings(
                        document,
                        participant.Key,
                        properties.Keys);
                    if (removedSettings.Length != 0)
                    {
                        DebugLogHelper.LogInfo(
                            log,
                            $"Ignored obsolete Map/Trail settings for [{participant.Key}]: " +
                            string.Join(", ", removedSettings) + ". They will be omitted on the next save.");
                    }

                    document.Mods.TryGetValue(participant.Key, out ModSettingsEntry entry);
                    // A direct Trail selection is only a preview. Mods absent from its
                    // sidecar keep their editable personal values until launch.
                    if (previewOnly && entry == null)
                        continue;
                    // The participant owns its Trail-safe baseline. Missing mods and settings
                    // therefore retain their own defaults, normally EnableMod=false.
                    Dictionary<string, byte[]> snapshot =
                        participant.Value.System_CreateDisabledMissionPresetSnapshot();
                    if (entry != null)
                    {
                        // ExitActiveParticipants restored the normal local preset. On the host
                        // these values become authoritative and the Extender synchronizes the
                        // resolved snapshot to clients.
                        foreach (string propertyName in entry.PlayerSettings)
                        {
                            if (!properties.TryGetValue(propertyName, out PropertyInfo property))
                                continue;
                            object current = property.GetValue(participant.Value);
                            snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, current);
                        }
                        // Fixed Trail values have final precedence.
                        foreach (KeyValuePair<string, object> setting in entry.Overrides)
                        {
                            if (!properties.TryGetValue(setting.Key, out PropertyInfo property))
                                continue;
                            object converted = ConvertJsonValue(setting.Value, property.PropertyType);
                            snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, converted);
                        }
                    }
                    prepared.Add(Tuple.Create(participant.Key, participant.Value, snapshot, entry != null));
                }

                if (editable)
                    applyEditorModes?.Invoke(document, useFixedDefaults);

                try
                {
                    foreach (Tuple<string, IModSettingsPresetEndpoint, Dictionary<string, byte[]>, bool> item in prepared)
                    {
                        if (item.Item2 is IModSettingsMissionSourceEndpoint sourceEndpoint)
                            sourceEndpoint.System_SetExplicitMissionSettings(item.Item4);
                        item.Item2.System_EnterMissionPreset(item.Item3, presetLabel, editable);
                        activeParticipantIds.Add(item.Item1);
                    }
                    trailContext = true;
                    workingContextEditable = editable;
                    activeContextLabel = string.IsNullOrWhiteSpace(presetLabel) ? "Trail" : presetLabel;
                }
                catch
                {
                    // Roll back only participants whose entry call actually completed.
                    try
                    {
                        ExitActiveParticipants(allParticipants);
                    }
                    catch (Exception rollbackException)
                    {
                        DebugLogHelper.LogError(log, "Could not fully roll back Map/Trail mod settings: " + rollbackException);
                    }
                    trailContext = activeParticipantIds.Count != 0;
                    throw;
                }
            }

            private void ExitActiveParticipants(Dictionary<string, IModSettingsPresetEndpoint> participants = null)
            {
                participants = participants ?? FindCompatibleViewModels();
                foreach (string modId in activeParticipantIds.ToArray())
                {
                    if (!participants.TryGetValue(modId, out IModSettingsPresetEndpoint viewModel))
                    {
                        DebugLogHelper.LogWarning(log, $"Could not leave missing active Map/Trail settings endpoint [{modId}].");
                        continue;
                    }
                    viewModel.System_ExitMissionPreset();
                    activeParticipantIds.Remove(modId);
                }
            }

            private Dictionary<string, IModSettingsPresetEndpoint> FindCompatibleViewModels()
            {
                var result = new Dictionary<string, IModSettingsPresetEndpoint>(StringComparer.Ordinal);
                foreach (IGrouping<string, LobbyModSettingsEntry> group in GetRegistrationGroups())
                {
                    if (IsRegistrationGroupOptedOut(group))
                        continue;
                    string modId = group.Key;
                    if (group.Skip(1).Any())
                        continue;
                    LobbyModSettingsEntry entry = group.First();
                    if (entry == null ||
                        !(entry.ViewModel is IModSettingsPresetEndpoint endpoint) ||
                        string.Equals(modId, ExtendedDataPlugin.PluginGuid, StringComparison.Ordinal) ||
                        GetIncompatibilityReason(entry.ViewModel) != null)
                    {
                        continue;
                    }
                    result[modId] = endpoint;
                }
                return result;
            }

            private Dictionary<string, PropertyInfo> GetPersistedProperties(object viewModel)
            {
                Type type = viewModel.GetType();
                if (persistedPropertiesByType.TryGetValue(type, out Dictionary<string, PropertyInfo> cached))
                    return cached;
                // Trail sidecars define shared match rules only. Personal and transient
                // properties remain owned by each participant and never enter .modtrail.json.
                cached = TrailModCompatibilityContract.GetTrailProperties(type)
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                persistedPropertiesByType[type] = cached;
                return cached;
            }

            private bool AreAllTrailPresetsActive()
            {
                Dictionary<string, IModSettingsPresetEndpoint> participants = FindCompatibleViewModels();
                return activeParticipantIds.All(id =>
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
            }

            private static object ConvertJsonValue(object value, Type targetType)
                => ModSettingsPresetJson.ConvertValue(value, targetType);

            private static string EncodeSettingValue(Type propertyType, object value)
            {
                byte[] bytes = MessagePackSerializer.Serialize(propertyType, value);
                return EncodedSettingPrefix + Convert.ToBase64String(bytes);
            }

            private void CopySidecars(string source, string destination, bool overwrite)
            {
                if (!Directory.Exists(source) || !Directory.Exists(destination))
                    return;
                foreach (string sidecar in Directory.GetFiles(
                    source,
                    "*" + MissionLoader.ModSettingsFileSuffix))
                {
                    string target = IOPath.Combine(destination, IOPath.GetFileName(sidecar));
                    if (!overwrite && File.Exists(target))
                        continue;
                    File.Copy(sidecar, target, overwrite);
                }
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
            }

            private void TryFileOperation(string action, Action operation)
            {
                try
                {
                    operation();
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not {action}: {exception}");
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
