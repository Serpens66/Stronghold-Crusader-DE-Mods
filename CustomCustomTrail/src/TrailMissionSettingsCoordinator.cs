using BepInEx.Bootstrap;
using BepInEx.Logging;
using CustomCustomTrail.Core;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using IOPath = System.IO.Path;

namespace CustomCustomTrail
{
    internal sealed class TrailModCompatibilityInfo
    {
        public TrailModCompatibilityInfo(string modId, string displayName, string incompatibilityReason)
        {
            ModId = modId;
            DisplayName = displayName;
            IncompatibilityReason = incompatibilityReason;
        }

        public string ModId { get; }
        public string DisplayName { get; }
        public string IncompatibilityReason { get; }
        public bool IsCompatible => string.IsNullOrEmpty(IncompatibilityReason);
    }

    /// <summary>Owns the process-wide Custom Trail settings and customization integration.</summary>
        internal sealed class TrailMissionSettingsCoordinator : IDisposable
        {
            private const string CoopTrailMakerSourceDirectory = "TrailMakerSource";
            private const string EncodedSettingPrefix = "messagepack-base64:";

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
            private readonly Func<string, bool> isModSelected;
            private readonly List<IDisposable> hooks = new List<IDisposable>();
            private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> persistedPropertiesByType =
                new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            private readonly Dictionary<string, string> lastCompatibilityFailures =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, ModSettingsDefinition> capturedDocumentsByTrailPath =
                new Dictionary<string, ModSettingsDefinition>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> activeParticipantIds = new HashSet<string>(StringComparer.Ordinal);
            private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
            private ManageTrailButtonDelegate manageTrailButtonOriginal;
            private EditorSetupButtonDelegate editorSetupButtonOriginal;
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
            private bool preserveContextForLaunch;
            private bool customTrailLaunchActive;
            private bool cleanupDeferralLogged;
            private bool openingCustomTrailSetup;
            private HUD_IngameMenu.RestartSkirmishMapInfo customTrailSetupRestartInfo;
            private FileHeader customTrailSetupHeader;
            private string activeSidecarPath;
            private long activeSidecarLength = -1;
            private long activeSidecarWriteTicks;
            private bool activeSidecarEditable;
            private bool enabled;
            private readonly List<Button> injectedCoopButtons = new List<Button>();
            private readonly Dictionary<UserControl, TextBlock> coopTrailTitleBlocks =
                new Dictionary<UserControl, TextBlock>();
            private readonly string[] vanillaCoopTrailTitles = new string[4];
            private readonly Dictionary<int, Button> coopSelectionButtons =
                new Dictionary<int, Button>();
            private readonly Dictionary<string, string> coopImportSourceBySelection =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private CheckBox coopTrailExportCheckbox;
            private string coopPackageDisplayName = string.Empty;
            private int coopPackageMissionCount;

            public event Action CoopPackagesChanged;
            public event Action CoopSetupOpened;

            public IReadOnlyList<TrailModCompatibilityInfo> DiscoverModCompatibility()
            {
                var result = new List<TrailModCompatibilityInfo>();
                foreach (IGrouping<string, LobbyModSettingsEntry> group in GetRegistrationGroups())
                {
                    string modId = group.Key;
                    if (string.Equals(modId, CustomCustomTrailPlugin.PluginGuid, StringComparison.Ordinal))
                        continue;
                    LobbyModSettingsEntry entry = group.First();
                    string displayName = GetModDisplayName(entry);
                    string incompatibility = entry == null
                        ? "missing mod-settings registration"
                        : group.Skip(1).Any()
                        ? "multiple mod-settings panels use the same plugin GUID"
                        : GetIncompatibilityReason(entry.ViewModel);
                    result.Add(new TrailModCompatibilityInfo(modId, displayName, incompatibility));
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
                        DebugLogHelper.LogWarning(
                            log,
                            $"Trail mod-settings compatibility rejected [{item.DisplayName}] ({item.ModId}): " +
                            item.IncompatibilityReason + ".");
                    }
                }
                lastCompatibilityFailures.Clear();
                foreach (KeyValuePair<string, string> item in current)
                    lastCompatibilityFailures[item.Key] = item.Value;
            }

            private string GetIncompatibilityReason(object viewModel)
            {
                return TrailModCompatibilityContract.Evaluate(
                    viewModel,
                    (property, value) => MessagePackSerializer.Serialize(property.PropertyType, value),
                    (type, bytes) => MessagePackSerializer.Deserialize(type, bytes))
                    .IncompatibilityReason;
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

            public TrailMissionSettingsCoordinator(ManualLogSource log, bool enabled, Func<string, bool> isModSelected)
            {
                this.log = log;
                this.enabled = enabled;
                this.isModSelected = isModSelected ?? (_ => true);
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
                    SetCoopPackagePresentation(null, 0);
                    if (MainViewModel.Instance != null)
                        MainViewModel.Instance.Show_TrailCustomisationButtons = false;
                    ExitContext(force: true);
                }
            }

            public void Initialize()
            {
                CaptureVanillaCoopTrailTitles();
                saveCustomTrailMapOriginal = InstallHook(
                    typeof(EditorDirector).GetMethod(nameof(EditorDirector.SaveCustomTrailMap)),
                    (SaveCustomTrailMapDelegate)SaveCustomTrailMapHook);
                manageTrailButtonOriginal = InstallHook(
                    typeof(FRONT_ManageTrail).GetMethod("ButtonClicked", BindingFlags.Instance | BindingFlags.Public),
                    (ManageTrailButtonDelegate)ManageTrailButtonHook);
                editorSetupButtonOriginal = InstallHook(
                    typeof(FRONT_EditorSetup).GetMethod("ButtonClicked", BindingFlags.Instance | BindingFlags.Public),
                    (EditorSetupButtonDelegate)EditorSetupButtonHook);
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
                SetCoopPackagePresentation(null, 0);
                foreach (IDisposable hook in hooks)
                    hook.Dispose();
                hooks.Clear();
            }

            public string[] Enter(ModSettingsDefinition document, bool editable, string source)
            {
                try
                {
                    document = ModSettingsJson.NormalizeAndValidate(document, source + ".modSettings");
                    ApplyDocument(document, editable);
                    DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
                    return GetMissingEnabledMods(document);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not load {source} mod settings; embedded mod settings are ignored: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateUnmanaged(), editable);
                    return Array.Empty<string>();
                }
            }

            private static string[] GetMissingEnabledMods(ModSettingsDefinition document) =>
                document.Mods
                    .Where(entry => entry.Value != null && entry.Value.Enabled)
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
                    return;
                }

                ExitActiveParticipants();
                trailContext = false;
                preserveContextForLaunch = false;
                customTrailLaunchActive = false;
                customTrailSetupRestartInfo = null;
                customTrailSetupHeader = null;
                cleanupDeferralLogged = false;
                ClearActiveSidecar();
                DebugLogHelper.LogInfo(log, "Left Trail mod-settings context.");
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
                    string[] enabledMods = document.Mods
                        .Where(entry => entry.Value.Enabled)
                        .Select(entry => entry.Key)
                        .ToArray();
                    DebugLogHelper.LogInfo(
                        log,
                        "Captured Trail mod settings before save; enabled=[" + string.Join(", ", enabledMods) + "].");
                    // Vanilla can enter Trail export before this save call returns. Keep the
                    // synchronous capture available to both exporters until it reaches disk.
                    capturedDocumentsByTrailPath[IOPath.GetFullPath(trailPath)] = document;
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Could not capture Trail mod settings before saving [{trailPath}]; its sidecar will not be changed: {exception}");
                }

                saveCustomTrailMapOriginal(self, mapPath, mapName, trailPath, restartInfo);
                if (document == null)
                    return;
                string sidecar = IOPath.GetFullPath(IOPath.ChangeExtension(trailPath, ".modjson"));
                try
                {
                    if (!File.Exists(trailPath))
                        throw new FileNotFoundException("The game did not create the expected Trail mission.", trailPath);
                    ModSettingsJson.WriteAtomic(sidecar, document);
                    capturedDocumentsByTrailPath.Remove(IOPath.GetFullPath(trailPath));
                    DebugLogHelper.LogInfo(log, $"Saved Trail mod settings beside [{trailPath}].");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not save Trail mod settings for [{trailPath}]: {exception}");
                    return;
                }

                try
                {
                    // Keep the just-saved mission editable even if Vanilla rebuilt the UI.
                    ApplyDocument(document, editable: true);
                    var info = new FileInfo(sidecar);
                    activeSidecarPath = sidecar;
                    activeSidecarLength = info.Length;
                    activeSidecarWriteTicks = info.LastWriteTimeUtc.Ticks;
                    activeSidecarEditable = true;
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

                manageTrailButtonOriginal(self, command);

                // Vanilla rebuilds the setup UI while loading. Apply the Trail snapshot only
                // afterwards so that cleanup cannot immediately restore the local preset.
                if (string.Equals(command, "Load", StringComparison.Ordinal))
                {
                    try
                    {
                        if (loadedHeader != null)
                            EnterSidecar(loadedHeader.filePath, editable: true);
                        else
                            ApplyDocument(ModSettingsDefinition.CreateUnmanaged(), editable: true);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Could not activate editable Trail mod settings: {exception}");
                        ApplyDocument(ModSettingsDefinition.CreateUnmanaged(), editable: true);
                    }
                }
                else if (string.Equals(command, "Import", StringComparison.Ordinal))
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
                if (enabled && string.Equals(command, "DoUpload", StringComparison.Ordinal))
                {
                    FileRow selectedRow = (self.FindName("UploadList") as ListView)?.SelectedItem as FileRow;
                    if (selectedRow?.trail != null && IsCoopPackageFolder(selectedRow.trail.Name))
                    {
                        try
                        {
                            UploadCoopTrailPackage(self, selectedRow.trail);
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
                }

                editorSetupButtonOriginal(self, command);

                if (enabled && string.Equals(command, "UploadTrail", StringComparison.Ordinal))
                    TryFileOperation("add Coop Trails to Vanilla's Workshop upload list", () => AddCoopWorkshopRows(self));
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

            private void UploadCoopTrailPackage(FRONT_EditorSetup page, MapFileManager.CustomTrailInfo trail)
            {
                string source = IOPath.GetFullPath(IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), trail.Name));
                CoopTrailPackage package = CoopTrailPackageCatalog.Load(source);
                string uploadContent = ConfigSettings.GetWorkshopUploadContentPath();
                string destination = IOPath.Combine(uploadContent, trail.Name);
                Directory.CreateDirectory(destination);
                CopyWorkshopPackage(source, destination, trail.Name + ".data");

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

            private static void CopyWorkshopPackage(string source, string destination, string metadataFileName)
            {
                Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(source))
                {
                    if (string.Equals(IOPath.GetFileName(file), metadataFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    File.Copy(file, IOPath.Combine(destination, IOPath.GetFileName(file)), true);
                }
                foreach (string directory in Directory.GetDirectories(source))
                    CopyDirectory(directory, IOPath.Combine(destination, IOPath.GetFileName(directory)));
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
                            SerpLocalization.Get("CustomCustomTrail.ExportFailedTitle"),
                            SerpLocalization.Get("CustomCustomTrail.ExportFailed") + "\r\n" + exception.Message);
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
                        SerpLocalization.Get("CustomCustomTrail.ExportFailedTitle"),
                        SerpLocalization.Get("CustomCustomTrail.ExportFailed") + "\r\n" + exception.Message);
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
                    foreach (string stale in Directory.GetFiles(destination, "Trail_Mission_*.modjson"))
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
                            string target = IOPath.Combine(destination, FRONT_ManageTrail.GetMakerFileName(outputIndex) + ".modjson");
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
                    : ModSettingsDefinition.CreateUnmanaged();
            }

            private bool TryReadModSettingsForExport(string trailPath, out ModSettingsDefinition document)
            {
                string fullTrailPath = IOPath.GetFullPath(trailPath);
                if (capturedDocumentsByTrailPath.TryGetValue(fullTrailPath, out document))
                {
                    DebugLogHelper.LogInfo(log, $"Using synchronously captured Trail mod settings for export [{fullTrailPath}].");
                    return true;
                }
                string sidecar = IOPath.ChangeExtension(fullTrailPath, ".modjson");
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
                    foreach (string sidecar in Directory.GetFiles(ConfigSettings.GetUserTrailMakerPath(), "Trail_Mission_*.modjson"))
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
                    Name = "CustomCustomTrailCoopExport",
                    Content = SerpLocalization.Get("CustomCustomTrail.TrailMakerCoop"),
                    ToolTip = SerpLocalization.Get("CustomCustomTrail.TrailMakerCoopHelp"),
                    Foreground = new SolidColorBrush(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
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
                foreach (string sidecar in Directory.GetFiles(destination, "Trail_Mission_*.modjson"))
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
                if (!enabled)
                {
                    startCustomTrailOriginal(self, trailName, missionId, difficulty);
                    return;
                }
                if (!preserveContextForLaunch)
                {
                    try
                    {
                        FileHeader header = MapFileManager.Instance.GetHeaderFromCustomTrail(
                            trailName,
                            FRONT_ManageTrail.GetMakerFileName(missionId - 1));
                        if (header != null)
                            EnterSidecar(header.filePath, editable: false);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Could not prepare Custom Trail mod settings: {exception}");
                        ApplyDocument(ModSettingsDefinition.CreateUnmanaged(), editable: false);
                    }
                }
                preserveContextForLaunch = false;
                customTrailLaunchActive = true;
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
                if (!enabled)
                {
                    multiplayerOpenOriginal(self, skirmishSetup, fromNew, restartInfo, coopSetup, trailMaker, customiseTrailType, customiseTrailId);
                    return;
                }
                bool preserve = preserveContextForLaunch;
                if (!trailMaker && !preserve)
                    ExitContext(force: true);
                multiplayerOpenOriginal(
                    self,
                    skirmishSetup,
                    fromNew,
                    restartInfo,
                    coopSetup,
                    trailMaker,
                    customiseTrailType,
                    customiseTrailId);
                if (preserve)
                    preserveContextForLaunch = false;
            }

            private void StartSkirmishGameHook(
                FRONT_Multiplayer self,
                HUD_IngameMenu.RestartSkirmishMapInfo customTrailRestartInfo)
            {
                if (!enabled)
                {
                    startSkirmishGameOriginal(self, customTrailRestartInfo);
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
                if (!enabled)
                {
                    frontendButtonOriginal(self, command);
                    return;
                }
                if (string.Equals(command, "Customize", StringComparison.Ordinal) &&
                    FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
                {
                    try
                    {
                        OpenSelectedCustomTrailSetup(self);
                    }
                    catch (Exception exception)
                    {
                        preserveContextForLaunch = false;
                        DebugLogHelper.LogError(log, $"Could not open Custom Trail setup: {exception}");
                    }
                    return;
                }
                bool preserveTrailMakerMapEditor = string.Equals(command, "MapEditor", StringComparison.Ordinal) &&
                    MainViewModel.Instance.FRONTMultiplayer.trailMakerMode;
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
                    string.Equals(command, "Coops", StringComparison.Ordinal))
                {
                    ExitContext(force: true);
                }
            }

            private static bool IsCoopTrailOpenCommand(string command) =>
                string.Equals(command, "Coop", StringComparison.Ordinal) ||
                string.Equals(command, "Coop2", StringComparison.Ordinal) ||
                string.Equals(command, "Coop3", StringComparison.Ordinal) ||
                string.Equals(command, "Coop4", StringComparison.Ordinal);

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
                // The Vanilla constructor has now loaded the XAML and assigned all named controls.
                // This is the first deterministic point at which the first-visit title exists.
                InjectCoopCustomizeButton(page);
                if (UpdateCoopTrailTitle(page, zeroBasedTrail))
                {
                    DebugLogHelper.LogDebug(
                        log,
                        "Initialized custom presentation for Coop Trail " +
                        (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
                }
                else
                {
                    DebugLogHelper.LogWarning(
                        log,
                        "Could not find the logical title element for Coop Trail " +
                        (zeroBasedTrail + 1).ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

            private void EnterSelectedCustomTrail(FrontendMenus menus)
            {
                try
                {
                    FileHeader header = GetSelectedCustomTrailHeader(menus);
                    if (header != null)
                        EnterSidecar(header.filePath, editable: false);
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not select Custom Trail mod settings: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateUnmanaged(), editable: false);
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
                EnterSidecar(header.filePath, editable: false);
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

                self.CoopMissionChanged(trailId, mission);
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

                MainViewModel.Instance.Show_CoopHostInvitePane = false;
                MainViewModel.Instance.Show_CoopHostJoinedPane = false;
                MainViewModel.Instance.Show_CoopClientPane = false;
                MainViewModel.Instance.Show_CoopMapIcons = false;
                MainViewModel.Instance.Show_CoopAIAllyPanel = false;
                MainViewModel.Instance.Show_CoopOptions = false;
                MainViewModel.Instance.Show_CoopWaiting = false;
                MainViewModel.Instance.Show_MPSharing = false;
                MainViewModel.Instance.Show_MultiplayerSetup = true;
                if (currentTrail == 21) MainViewModel.Instance.Show_CoopTrail1 = false;
                if (currentTrail == 22) MainViewModel.Instance.Show_CoopTrail2 = false;
                if (currentTrail == 23) MainViewModel.Instance.Show_CoopTrail3 = false;
                if (currentTrail == 24) MainViewModel.Instance.Show_CoopTrail4 = false;
                // ShowSetupScreen rebuilds lobby settings. Reapply the selected mission only
                // after that transition, matching the working Custom Trail Customize path.
                CoopSetupOpened?.Invoke();
                DebugLogHelper.LogInfo(log, $"Opened Coop Trail setup trail={trailId + 1}, mission={mission}.");
            }

            private void EnterSidecar(string trailPath, bool editable)
            {
                string sidecar = IOPath.GetFullPath(IOPath.ChangeExtension(trailPath, ".modjson"));
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
                    string.Equals(activeSidecarPath, sidecar, StringComparison.OrdinalIgnoreCase) &&
                    activeSidecarLength == length && activeSidecarWriteTicks == writeTicks &&
                    AreAllTrailPresetsActive())
                {
                    return;
                }

                ModSettingsDefinition document = exists
                    ? ModSettingsJson.Read(sidecar)
                    : ModSettingsDefinition.CreateUnmanaged();
                ApplyDocument(document, editable);
                string[] enabledMods = document.Mods
                    .Where(entry => entry.Value.Enabled)
                    .Select(entry => entry.Key)
                    .ToArray();
                DebugLogHelper.LogInfo(
                    log,
                    $"Loaded Trail sidecar [{sidecar}]; exists={exists}, editable={editable}, " +
                    "enabled=[" + string.Join(", ", enabledMods) + "].");
                activeSidecarPath = sidecar;
                activeSidecarLength = length;
                activeSidecarWriteTicks = writeTicks;
                activeSidecarEditable = editable;
            }

            private ModSettingsDefinition CaptureDocument()
            {
                ModSettingsDefinition document = ModSettingsDefinition.CreateUnmanaged();
                Dictionary<string, object> participants = FindCompatibleViewModels(selectedOnly: true);

                foreach (KeyValuePair<string, object> participant in participants)
                {
                    object viewModel = participant.Value;
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(viewModel);
                    bool enabled = !properties.TryGetValue("EnableMod", out PropertyInfo enableProperty) ||
                        enableProperty.PropertyType != typeof(bool) ||
                        (bool)enableProperty.GetValue(viewModel);
                    var target = new ModSettingsEntry();
                    document.Mods[participant.Key] = target;
                    target.Enabled = enabled;
                    if (!enabled)
                        continue;

                    foreach (PropertyInfo property in properties.Values.Where(property => property.Name != "EnableMod"))
                    {
                        object value = property.GetValue(viewModel);
                        target.Settings[property.Name] = ModSettingsJson.IsSupportedValue(value)
                            ? value
                            : EncodeSettingValue(property.PropertyType, value);
                    }
                }
                return document;
            }

            private void ApplyDocument(ModSettingsDefinition document, bool editable)
            {
                ClearActiveSidecar();
                Dictionary<string, object> allParticipants = FindCompatibleViewModels(selectedOnly: false);
                ExitActiveParticipants(allParticipants);
                Dictionary<string, object> participants = allParticipants
                    .Where(item => document.Mods.ContainsKey(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                var prepared = new List<Tuple<string, object, Dictionary<string, byte[]>>>(participants.Count);
                foreach (KeyValuePair<string, object> participant in participants)
                {
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(participant.Value);
                    string[] removedSettings = ModSettingsJson.RemoveUnknownSettings(
                        document,
                        participant.Key,
                        properties.Keys.Where(name => name != "EnableMod"));
                    if (removedSettings.Length != 0)
                    {
                        DebugLogHelper.LogInfo(
                            log,
                            $"Ignored obsolete Trail settings for [{participant.Key}]: " +
                            string.Join(", ", removedSettings) + ". They will be omitted on the next save.");
                    }

                    ModSettingsEntry entry = document.Mods[participant.Key];
                    // Begin with every current host default. Sparse old Trail files therefore
                    // gain newly introduced settings without affecting personal client options.
                    Dictionary<string, byte[]> snapshot =
                        (Dictionary<string, byte[]>)Invoke(participant.Value, "System_CreateDisabledMissionPresetSnapshot");
                    if (entry.Enabled)
                    {
                        if (properties.TryGetValue("EnableMod", out PropertyInfo enableProperty) &&
                            enableProperty.PropertyType == typeof(bool))
                        {
                            snapshot[enableProperty.Name] = MessagePackSerializer.Serialize(true);
                        }
                        foreach (KeyValuePair<string, object> setting in entry.Settings)
                        {
                            if (!properties.TryGetValue(setting.Key, out PropertyInfo property) || property.Name == "EnableMod")
                                continue;
                            object converted = ConvertJsonValue(setting.Value, property.PropertyType);
                            snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, converted);
                        }
                    }
                    prepared.Add(Tuple.Create(participant.Key, participant.Value, snapshot));
                }

                try
                {
                    foreach (Tuple<string, object, Dictionary<string, byte[]>> item in prepared)
                    {
                        Invoke(item.Item2, "System_EnterMissionPreset", item.Item3, "Trail", editable);
                        activeParticipantIds.Add(item.Item1);
                    }
                    trailContext = true;
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
                        DebugLogHelper.LogError(log, "Could not fully roll back Trail mod settings: " + rollbackException);
                    }
                    trailContext = activeParticipantIds.Count != 0;
                    throw;
                }
            }

            private void ExitActiveParticipants(Dictionary<string, object> participants = null)
            {
                participants = participants ?? FindCompatibleViewModels(selectedOnly: false);
                foreach (string modId in activeParticipantIds.ToArray())
                {
                    if (!participants.TryGetValue(modId, out object viewModel))
                    {
                        DebugLogHelper.LogWarning(log, $"Could not leave missing active Trail settings endpoint [{modId}].");
                        continue;
                    }
                    Invoke(viewModel, "System_ExitMissionPreset");
                    activeParticipantIds.Remove(modId);
                }
            }

            private Dictionary<string, object> FindCompatibleViewModels(bool selectedOnly)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (IGrouping<string, LobbyModSettingsEntry> group in GetRegistrationGroups())
                {
                    string modId = group.Key;
                    if (group.Skip(1).Any())
                        continue;
                    LobbyModSettingsEntry entry = group.First();
                    if (entry == null ||
                        string.Equals(modId, CustomCustomTrailPlugin.PluginGuid, StringComparison.Ordinal) ||
                        GetIncompatibilityReason(entry.ViewModel) != null ||
                        (selectedOnly && !isModSelected(modId)))
                    {
                        continue;
                    }
                    result[modId] = entry.ViewModel;
                }
                return result;
            }

            private Dictionary<string, PropertyInfo> GetPersistedProperties(object viewModel)
            {
                Type type = viewModel.GetType();
                if (persistedPropertiesByType.TryGetValue(type, out Dictionary<string, PropertyInfo> cached))
                    return cached;
                // Trail sidecars define shared match rules only. Personal and transient
                // properties remain owned by each participant and never enter .modjson.
                cached = TrailModCompatibilityContract.GetTrailProperties(type)
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                persistedPropertiesByType[type] = cached;
                return cached;
            }

            private bool AreAllTrailPresetsActive()
            {
                Dictionary<string, object> participants = FindCompatibleViewModels(selectedOnly: false);
                return activeParticipantIds.All(id =>
                {
                    if (!participants.TryGetValue(id, out object viewModel))
                        return false;
                    PropertyInfo property = viewModel.GetType().GetProperty("IsMissionPresetActive", BindingFlags.Instance | BindingFlags.Public);
                    return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(viewModel);
                });
            }

            private void ClearActiveSidecar()
            {
                activeSidecarPath = null;
                activeSidecarLength = -1;
                activeSidecarWriteTicks = 0;
                activeSidecarEditable = false;
            }

            private static object ConvertJsonValue(object value, Type targetType)
            {
                if (value == null)
                    throw new InvalidDataException($"Null cannot be assigned to [{targetType.FullName}].");
                if (targetType != typeof(string) && value is string encoded &&
                    encoded.StartsWith(EncodedSettingPrefix, StringComparison.Ordinal))
                {
                    byte[] bytes = Convert.FromBase64String(encoded.Substring(EncodedSettingPrefix.Length));
                    return MessagePackSerializer.Deserialize(targetType, bytes);
                }
                Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (effectiveType.IsInstanceOfType(value))
                    return value;
                if (effectiveType.IsArray && value is IEnumerable sequence && !(value is string))
                {
                    Type elementType = effectiveType.GetElementType();
                    var converted = new List<object>();
                    foreach (object item in sequence)
                        converted.Add(ConvertJsonValue(item, elementType));
                    Array array = Array.CreateInstance(elementType, converted.Count);
                    for (int index = 0; index < converted.Count; index++)
                        array.SetValue(converted[index], index);
                    return array;
                }
                return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
            }

            private static string EncodeSettingValue(Type propertyType, object value)
            {
                byte[] bytes = MessagePackSerializer.Serialize(propertyType, value);
                return EncodedSettingPrefix + Convert.ToBase64String(bytes);
            }

            private void CopySidecars(string source, string destination, bool overwrite)
            {
                if (!Directory.Exists(source) || !Directory.Exists(destination))
                    return;
                foreach (string sidecar in Directory.GetFiles(source, "*.modjson"))
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
                foreach (string sidecar in Directory.GetFiles(root, "Trail_Mission_*.modjson"))
                {
                    if (!File.Exists(IOPath.ChangeExtension(sidecar, ".trail")))
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

            private static object Invoke(object target, string method, params object[] arguments)
            {
                MethodInfo found = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
                if (found == null)
                    throw new MissingMethodException(target.GetType().FullName, method);
                return found.Invoke(target, arguments);
            }
        }
}
