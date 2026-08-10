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

            public TrailMissionSettingsCoordinator(ManualLogSource log)
            {
                this.log = log;
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
                DebugLogHelper.LogInfo(log, "Trail mission-settings coordinator initialized.");
            }

            public void Dispose()
            {
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
                // Vanilla invokes Init again after its confirmation callback deleted a mission.
                TryFileOperation("clean orphan Trail sidecars", DeleteOrphanMakerSidecars);
            }

            private void BackupHook(FRONT_ManageTrail self, string source, string destination)
            {
                backupOriginal(self, source, destination);
                TryFileOperation("back up Trail sidecars", () => CopySidecars(source, destination, overwrite: true));
            }

            private void ImportHook(FRONT_ManageTrail self, string customFolderName)
            {
                importOriginal(self, customFolderName);
                string source = IOPath.Combine(ConfigSettings.GetUserCustomTrailsPath(), customFolderName);
                TryFileOperation("import Trail sidecars", () => CopySidecars(source, ConfigSettings.GetUserTrailMakerPath(), overwrite: false));
            }

            private void ExportHook(FRONT_ManageTrail self, string destination)
            {
                exportOriginal(self, destination);
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
                TryFileOperation("clear Trail sidecars", () =>
                {
                    foreach (string sidecar in Directory.GetFiles(ConfigSettings.GetUserTrailMakerPath(), "Trail_Mission_*.modjson"))
                        File.Delete(sidecar);
                });
            }

            private void StartCustomTrailHook(MainViewModel self, string trailName, int missionId, int difficulty)
            {
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
                MainViewModel.Instance.Show_TrailCustomisationButtons = true;
            }

            private void TrailSelectionHook(FrontendMenus self, int missionId, bool fromRealClick)
            {
                trailSelectionOriginal(self, missionId, fromRealClick);
                if (!openingCustomTrailSetup &&
                    FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
                    EnterSelectedCustomTrail(self);
            }

            private void FrontendButtonHook(FrontendMenus self, string command)
            {
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
                InjectCoopCustomizeButton(FRONT_CoopTrail1.Instance);
                InjectCoopCustomizeButton(FRONT_CoopTrail2.Instance);
                InjectCoopCustomizeButton(FRONT_CoopTrail3.Instance);
                InjectCoopCustomizeButton(FRONT_CoopTrail4.Instance);
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
                    Margin = new Thickness(0, 0, 0, 78),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Style = anchor.Style,
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
                    ModSettingsEntry entry = document.Mods.TryGetValue(participant.Key, out ModSettingsEntry stored)
                        ? stored
                        : new ModSettingsEntry();
                    Dictionary<string, byte[]> snapshot =
                        (Dictionary<string, byte[]>)Invoke(participant.Value, "System_CreateDisabledMissionPresetSnapshot");
                    if (entry.Enabled)
                    {
                        snapshot["EnableMod"] = MessagePackSerializer.Serialize(true);
                        Dictionary<string, PropertyInfo> properties = GetPersistedProperties(participant.Value);
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
