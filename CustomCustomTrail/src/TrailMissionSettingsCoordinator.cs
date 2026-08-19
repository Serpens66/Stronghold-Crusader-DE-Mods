using BepInEx.Bootstrap;
using BepInEx.Logging;
using CustomCustomTrail.Core;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using IOPath = System.IO.Path;

namespace CustomCustomTrail
{
    /// <summary>Owns the process-wide Custom Trail settings and customization integration.</summary>
    internal sealed class TrailMissionSettingsCoordinator : IDisposable
    {
        private static readonly HashSet<string> TargetModIdSet =
            new HashSet<string>(ModSettingsDefinition.TargetModIds, StringComparer.Ordinal);
            private delegate void SaveCustomTrailMapDelegate(
                EditorDirector self,
                string mapPath,
                string mapName,
                string trailPath,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);
            private delegate void ManageTrailButtonDelegate(FRONT_ManageTrail self, string command);
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

            private readonly ManualLogSource log;
            private readonly List<IDisposable> hooks = new List<IDisposable>();
            private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> persistedPropertiesByType =
                new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
            private ManageTrailButtonDelegate manageTrailButtonOriginal;
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
            private readonly Dictionary<UserControl, string> vanillaCoopTrailTitles =
                new Dictionary<UserControl, string>();
            private CheckBox coopTrailExportCheckbox;
            private string coopPackageDisplayName = string.Empty;
            private int coopPackageMissionCount;

            public event Action CoopPackagesChanged;

            public TrailMissionSettingsCoordinator(ManualLogSource log, bool enabled)
            {
                this.log = log;
                this.enabled = enabled;
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
                saveCustomTrailMapOriginal = InstallHook(
                    typeof(EditorDirector).GetMethod(nameof(EditorDirector.SaveCustomTrailMap)),
                    (SaveCustomTrailMapDelegate)SaveCustomTrailMapHook);
                manageTrailButtonOriginal = InstallHook(
                    typeof(FRONT_ManageTrail).GetMethod("ButtonClicked", BindingFlags.Instance | BindingFlags.Public),
                    (ManageTrailButtonDelegate)ManageTrailButtonHook);
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
                    DebugLogHelper.LogError(log, $"Could not load {source} mod settings; all Trail mods are disabled: {exception}");
                    ApplyDocument(ModSettingsDefinition.CreateDisabled(), editable);
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

                foreach (object viewModel in FindTargetViewModels().Values)
                    Invoke(viewModel, "System_ExitMissionPreset");
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
                    document = CaptureDocument(requireLoadedEndpoints: true);
                    string[] enabledMods = document.Mods
                        .Where(entry => entry.Value.Enabled)
                        .Select(entry => entry.Key)
                        .ToArray();
                    DebugLogHelper.LogInfo(
                        log,
                        "Captured Trail mod settings before save; enabled=[" + string.Join(", ", enabledMods) + "].");
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
                            ApplyDocument(ModSettingsDefinition.CreateDisabled(), editable: true);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Could not activate editable Trail mod settings: {exception}");
                        ApplyDocument(ModSettingsDefinition.CreateDisabled(), editable: true);
                    }
                }
            }

            private void ManageTrailInitHook(FRONT_ManageTrail self, bool preserveSelection)
            {
                manageTrailInitOriginal(self, preserveSelection);
                if (!enabled)
                    return;
                EnsureTrailMakerCoopCheckbox(self);
                // Vanilla invokes Init again after its confirmation callback deleted a mission.
                TryFileOperation("clean orphan Trail sidecars", DeleteOrphanMakerSidecars);
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
                importOriginal(self, customFolderName);
                if (!enabled)
                    return;
                string source = IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), customFolderName);
                TryFileOperation("import Trail sidecars", () => CopySidecars(source, ConfigSettings.GetUserTrailMakerPath(), overwrite: false));
                TryFileOperation("import the Coop Trail Maker state", () =>
                {
                    SetMakerCoopEnabled(File.Exists(IOPath.Combine(source, "cooptrail.json")));
                    RefreshTrailMakerCoopCheckbox();
                });
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
                        prepared = new CoopTrailPackageExporter().Prepare(ConfigSettings.GetUserTrailMakerPath(), destination);
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
                    exportOriginal(self, destination);
                    ExportSidecars(destination);
                    if (prepared != null)
                    {
                        prepared.Publish(destination);
                        DebugLogHelper.LogInfo(log, "Published Coop Trail package [" + prepared.Package.Manifest.DisplayName +
                            "] with " + prepared.Package.Manifest.MissionCount + " mission(s).");
                    }
                    else
                    {
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
                        string sidecar = IOPath.ChangeExtension(sourceTrail, ".modjson");
                        if (File.Exists(sidecar))
                        {
                            string target = IOPath.Combine(destination, FRONT_ManageTrail.GetMakerFileName(outputIndex) + ".modjson");
                            File.Copy(sidecar, target, overwrite: true);
                        }
                        outputIndex++;
                    }
                });
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
                if (File.Exists(manifest))
                    File.Delete(manifest);
                if (Directory.Exists(missions))
                    Directory.Delete(missions, true);
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
                        ApplyDocument(ModSettingsDefinition.CreateDisabled(), editable: false);
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
                    ApplyDocument(ModSettingsDefinition.CreateDisabled(), editable: false);
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
            }

            internal void SetCoopPackagePresentation(string displayName, int missionCount)
            {
                coopPackageDisplayName = displayName ?? string.Empty;
                coopPackageMissionCount = Math.Max(0, Math.Min(40, missionCount));
                EnsureCoopCustomizeButtons();
            }

            private void UpdateCoopTrailTitle(UserControl page, int zeroBasedTrail)
            {
                if (page == null)
                    return;

                if (!coopTrailTitleBlocks.TryGetValue(page, out TextBlock title))
                {
                    string key = "TEXT_COOP_0" + (23 + zeroBasedTrail).ToString(CultureInfo.InvariantCulture);
                    if (!Translate.Instance.GameTexts.TryGetValue(key, out string vanillaTitle))
                        return;
                    title = FindDescendantTextBlock(page, vanillaTitle);
                    if (title == null)
                        return;
                    coopTrailTitleBlocks[page] = title;
                    vanillaCoopTrailTitles[page] = vanillaTitle;
                }

                bool packageOccupiesTrail = enabled && !string.IsNullOrWhiteSpace(coopPackageDisplayName) &&
                    coopPackageMissionCount > zeroBasedTrail * 10;
                title.Text = packageOccupiesTrail
                    ? coopPackageDisplayName
                    : vanillaCoopTrailTitles[page];
            }

            private static TextBlock FindDescendantTextBlock(DependencyObject parent, string expectedText)
            {
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int index = 0; index < childCount; index++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                    if (child is TextBlock textBlock && string.Equals(textBlock.Text, expectedText, StringComparison.Ordinal))
                        return textBlock;
                    TextBlock nested = FindDescendantTextBlock(child, expectedText);
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
                    // Keep this just below CoopKick so the player list cannot cover it.
                    Margin = new Thickness(0, 0, 0, -51),
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
                    : ModSettingsDefinition.CreateDisabled();
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

            private ModSettingsDefinition CaptureDocument(bool requireLoadedEndpoints = false)
            {
                ModSettingsDefinition document = ModSettingsDefinition.CreateDisabled();
                Dictionary<string, object> participants = FindTargetViewModels();
                if (requireLoadedEndpoints)
                {
                    string[] missingEndpoints = ModSettingsDefinition.TargetModIds
                        .Where(id => Chainloader.PluginInfos.ContainsKey(id) && !participants.ContainsKey(id))
                        .ToArray();
                    if (missingEndpoints.Length != 0)
                    {
                        throw new InvalidOperationException(
                            "Loaded settings mods have no registered ViewModel endpoint: " + string.Join(", ", missingEndpoints));
                    }
                }

                foreach (KeyValuePair<string, object> participant in participants)
                {
                    object viewModel = participant.Value;
                    Dictionary<string, PropertyInfo> properties = GetPersistedProperties(viewModel);
                    bool enabled = properties.TryGetValue("EnableMod", out PropertyInfo enableProperty) &&
                        enableProperty.PropertyType == typeof(bool) &&
                        (bool)enableProperty.GetValue(viewModel);
                    ModSettingsEntry target = document.Mods[participant.Key];
                    target.Enabled = enabled;
                    if (!enabled)
                        continue;

                    foreach (PropertyInfo property in properties.Values.Where(property => property.Name != "EnableMod"))
                        target.Settings[property.Name] = property.GetValue(viewModel);
                }
                return document;
            }

            private void ApplyDocument(ModSettingsDefinition document, bool editable)
            {
                ClearActiveSidecar();
                Dictionary<string, object> participants = FindTargetViewModels();
                var prepared = new List<KeyValuePair<object, Dictionary<string, byte[]>>>(participants.Count);
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

                    ModSettingsEntry entry = document.Mods.TryGetValue(participant.Key, out ModSettingsEntry stored)
                        ? stored
                        : new ModSettingsEntry();
                    // Begin with every current host default. Sparse old Trail files therefore
                    // gain newly introduced settings without affecting personal client options.
                    Dictionary<string, byte[]> snapshot =
                        (Dictionary<string, byte[]>)Invoke(participant.Value, "System_CreateDisabledMissionPresetSnapshot");
                    if (entry.Enabled)
                    {
                        snapshot["EnableMod"] = MessagePackSerializer.Serialize(true);
                        foreach (KeyValuePair<string, object> setting in entry.Settings)
                        {
                            if (!properties.TryGetValue(setting.Key, out PropertyInfo property) || property.Name == "EnableMod")
                                continue;
                            object converted = ConvertJsonValue(setting.Value, property.PropertyType);
                            snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, converted);
                        }
                    }
                    prepared.Add(new KeyValuePair<object, Dictionary<string, byte[]>>(participant.Value, snapshot));
                }

                foreach (KeyValuePair<object, Dictionary<string, byte[]>> item in prepared)
                    Invoke(item.Key, "System_EnterMissionPreset", item.Value, "Trail", editable);
                trailContext = true;
            }

            private Dictionary<string, object> FindTargetViewModels()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var entry in GameXAMLManagerAPI.Instance.RegisteredModSettings)
                {
                    if (TargetModIdSet.Contains(entry.Name))
                        result[entry.Name] = entry.ViewModel;
                }
                return result;
            }

            private Dictionary<string, PropertyInfo> GetPersistedProperties(object viewModel)
            {
                Type type = viewModel.GetType();
                if (persistedPropertiesByType.TryGetValue(type, out Dictionary<string, PropertyInfo> cached))
                    return cached;
                cached = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanRead && property.CanWrite && property.GetCustomAttributes(false)
                        // Trail sidecars define the shared match rules. Personal settings
                        // remain owned by each participant and must never enter .modjson.
                        .Any(attribute => attribute.GetType().Name == "SyncHostOnlyAttribute"))
                    .ToDictionary(property => property.Name, StringComparer.Ordinal);
                persistedPropertiesByType[type] = cached;
                return cached;
            }

            private bool AreAllTrailPresetsActive()
            {
                Dictionary<string, object> participants = FindTargetViewModels();
                return participants.Count > 0 && participants.Values.All(viewModel =>
                {
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
                Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (effectiveType.IsInstanceOfType(value))
                    return value;
                return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
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

            private T InstallHook<T>(MethodInfo method, T replacement) where T : Delegate
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
