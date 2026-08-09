using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using IOPath = System.IO.Path;

namespace Shared
{
    /// <summary>
    /// Process-wide Trail integration. Every participating mod embeds this type, but only
    /// the lexicographically first loaded participant installs hooks and writes sidecars.
    /// </summary>
    public static class TrailModSettingsRuntime
    {
        public static readonly string[] TargetModIds = TrailModSettingsRegistry.TargetModIds;

        private static LeaderRuntime leader;

        public static void RegisterParticipant(
            BaseUnityPlugin plugin,
            ManualLogSource log,
            string modId)
        {
            if (plugin == null || !TargetModIds.Contains(modId, StringComparer.Ordinal))
                return;

            string elected = TrailModSettingsRegistry.ElectLeader(Chainloader.PluginInfos.Keys);
            if (!string.Equals(elected, modId, StringComparison.Ordinal) || leader != null)
                return;

            leader = new LeaderRuntime(log, modId);
            leader.Initialize();
        }

        // Neutral reflection API used by CoopTrailReplacer without a hard assembly reference.
        public static void System_EnterCoopTrailJson(string modSettingsJson, bool editable)
        {
            leader?.EnterJson(modSettingsJson, editable, "custom Coop mission");
        }

        public static void System_ExitTrailContext()
        {
            leader?.ExitContext();
        }

        public static string[] System_GetMissingEnabledMods(string modSettingsJson)
        {
            if (leader == null)
                return Array.Empty<string>();
            return leader.GetMissingEnabledMods(modSettingsJson);
        }

        private sealed class LeaderRuntime
        {
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
            private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
            private delegate void FrontendOpenCustomTrailDelegate(FrontendMenus self, string trailName, int level);
            private delegate void FrontendButtonDelegate(FrontendMenus self, string command);
            private delegate void TrailSelectionDelegate(FrontendMenus self, int missionId);

            private readonly ManualLogSource log;
            private readonly string ownerModId;
            private readonly List<IDisposable> hooks = new List<IDisposable>();
            private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
            private ManageTrailButtonDelegate manageTrailButtonOriginal;
            private ManageTrailInitDelegate manageTrailInitOriginal;
            private TwoStringDelegate backupOriginal;
            private ImportDelegate importOriginal;
            private ExportDelegate exportOriginal;
            private NoArgumentDelegate clearMakerOriginal;
            private StartCustomTrailDelegate startCustomTrailOriginal;
            private MultiplayerOpenDelegate multiplayerOpenOriginal;
            private CoopMissionChangedDelegate coopMissionChangedOriginal;
            private FrontendOpenCustomTrailDelegate frontendOpenCustomTrailOriginal;
            private FrontendButtonDelegate frontendButtonOriginal;
            private TrailSelectionDelegate trailSelectionOriginal;
            private bool trailContext;
            private bool preserveContextForLaunch;

            public LeaderRuntime(ManualLogSource log, string ownerModId)
            {
                this.log = log;
                this.ownerModId = ownerModId;
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
                coopMissionChangedOriginal = InstallHook(
                    typeof(FRONT_Multiplayer).GetMethod(
                        nameof(FRONT_Multiplayer.CoopMissionChanged),
                        new[] { typeof(int), typeof(int), typeof(bool) }),
                    (CoopMissionChangedDelegate)CoopMissionChangedHook);
                frontendOpenCustomTrailOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.OpenCustomTrail), new[] { typeof(string), typeof(int) }),
                    (FrontendOpenCustomTrailDelegate)FrontendOpenCustomTrailHook);
                frontendButtonOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod("ButtonClicked", new[] { typeof(string) }),
                    (FrontendButtonDelegate)FrontendButtonHook);
                trailSelectionOriginal = InstallHook(
                    typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.ButtonTrailCampaignClicked), new[] { typeof(int) }),
                    (TrailSelectionDelegate)TrailSelectionHook);

                InjectCoopCustomizeButtons();

                DebugLogHelper.LogInfo(log, $"Trail mod-settings leader [{ownerModId}] initialized.");
            }

            public void EnterJson(string json, bool editable, string source)
            {
                try
                {
                    TrailSettingsDocument document = TrailSettingsJson.ParseObject(json);
                    ApplyDocument(document, editable);
                    DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not load {source} mod settings; all Trail mods are disabled: {exception}");
                    ApplyDocument(TrailSettingsDocument.CreateDisabled(), editable);
                }
            }

            public string[] GetMissingEnabledMods(string json)
            {
                TrailSettingsDocument document;
                try
                {
                    document = TrailSettingsJson.ParseObject(json);
                }
                catch
                {
                    return Array.Empty<string>();
                }

                return document.Mods
                    .Where(entry => entry.Value != null && entry.Value.Enabled)
                    .Select(entry => entry.Key)
                    .Where(id => !Chainloader.PluginInfos.ContainsKey(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
            }

            public void ExitContext()
            {
                if (!trailContext)
                    return;

                foreach (object viewModel in FindTargetViewModels().Values)
                    Invoke(viewModel, "System_ExitTrailPreset");
                trailContext = false;
                preserveContextForLaunch = false;
                DebugLogHelper.LogInfo(log, "Left Trail mod-settings context.");
            }

            private void SaveCustomTrailMapHook(
                EditorDirector self,
                string mapPath,
                string mapName,
                string trailPath,
                HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
            {
                saveCustomTrailMapOriginal(self, mapPath, mapName, trailPath, restartInfo);
                try
                {
                    if (!File.Exists(trailPath))
                        throw new FileNotFoundException("The game did not create the expected Trail mission.", trailPath);
                    TrailSettingsJson.WriteAtomic(IOPath.ChangeExtension(trailPath, ".modjson"), CaptureDocument());
                    DebugLogHelper.LogInfo(log, $"Saved Trail mod settings beside [{trailPath}].");
                }
                catch (Exception exception)
                {
                    DebugLogHelper.LogError(log, $"Could not save Trail mod settings for [{trailPath}]: {exception}");
                }
            }

            private void ManageTrailButtonHook(FRONT_ManageTrail self, string command)
            {
                if (string.Equals(command, "Load", StringComparison.Ordinal))
                {
                    try
                    {
                        int selected = (int)typeof(FRONT_ManageTrail)
                            .GetField("SelectedMission", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(self);
                        FileHeader header = MapFileManager.Instance.GetHeaderFromTrailMaker(
                            FRONT_ManageTrail.GetMakerFileName(selected));
                        if (header != null)
                            EnterSidecar(header.filePath, editable: true);
                    }
                    catch (Exception exception)
                    {
                        DebugLogHelper.LogError(log, $"Could not load editable Trail mod settings: {exception}");
                        ApplyDocument(TrailSettingsDocument.CreateDisabled(), editable: true);
                    }
                }

                manageTrailButtonOriginal(self, command);
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
                        ApplyDocument(TrailSettingsDocument.CreateDisabled(), editable: false);
                    }
                }
                preserveContextForLaunch = false;
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
                    ExitContext();
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

            private void CoopMissionChangedHook(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
            {
                coopMissionChangedOriginal(self, trailId, missionId, resetOrderSwapped);
                InjectCoopCustomizeButtons();
            }

            private void FrontendOpenCustomTrailHook(FrontendMenus self, string trailName, int level)
            {
                frontendOpenCustomTrailOriginal(self, trailName, level);
                MainViewModel.Instance.Show_TrailCustomisationButtons = true;
                EnterSelectedCustomTrail(self);
            }

            private void TrailSelectionHook(FrontendMenus self, int missionId)
            {
                trailSelectionOriginal(self, missionId);
                if (FrontendMenus.CurrentSelectedTrail >= 90 && FrontendMenus.CurrentSelectedTrail <= 92)
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
                frontendButtonOriginal(self, command);
                if (string.Equals(command, "Skirmish", StringComparison.Ordinal) ||
                    string.Equals(command, "MapEditor", StringComparison.Ordinal) ||
                    string.Equals(command, "BackMain", StringComparison.Ordinal) ||
                    string.Equals(command, "Coops", StringComparison.Ordinal))
                {
                    ExitContext();
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
                    ApplyDocument(TrailSettingsDocument.CreateDisabled(), editable: false);
                }
            }

            private void OpenSelectedCustomTrailSetup(FrontendMenus menus)
            {
                FileHeader header = GetSelectedCustomTrailHeader(menus)
                    ?? throw new InvalidDataException("The selected Custom Trail mission has no file header.");
                HUD_IngameMenu.RestartSkirmishMapInfo restart = header.restartSkirmishInfo
                    ?? throw new InvalidDataException("The selected Custom Trail mission has no skirmish setup data.");
                restart.selectedHeader = header;
                restart.customTrail = true;
                restart.customTestMission = false;
                restart.customTrailName = menus.CustomTrailName;
                restart.customTrailLevel = FrontendMenus.CurrentSelectedCustomTrailMission;
                FieldInfo difficulty = typeof(FrontendMenus).GetField("currentDifficultySetting", BindingFlags.Instance | BindingFlags.NonPublic);
                if (difficulty != null)
                    restart.customTrailDifficulty = (int)difficulty.GetValue(menus);

                preserveContextForLaunch = true;
                FRONT_Multiplayer.Open(skirmishSetup: true, restart, coopSetup: false, trailMaker: false);
                DebugLogHelper.LogInfo(log, $"Opened Custom Trail setup [{menus.CustomTrailName}] mission {restart.customTrailLevel}.");
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

            private void InjectCoopCustomizeButtons()
            {
                InjectCoopCustomizeButton(FRONT_CoopTrail1.Instance);
                InjectCoopCustomizeButton(FRONT_CoopTrail2.Instance);
                InjectCoopCustomizeButton(FRONT_CoopTrail3.Instance);
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
                string sidecar = IOPath.ChangeExtension(trailPath, ".modjson");
                TrailSettingsDocument document = File.Exists(sidecar)
                    ? TrailSettingsJson.Read(sidecar)
                    : TrailSettingsDocument.CreateDisabled();
                ApplyDocument(document, editable);
            }

            private TrailSettingsDocument CaptureDocument()
            {
                TrailSettingsDocument document = TrailSettingsDocument.CreateDisabled();
                foreach (KeyValuePair<string, object> participant in FindTargetViewModels())
                {
                    object viewModel = participant.Value;
                    PropertyInfo enableProperty = viewModel.GetType().GetProperty("EnableMod");
                    bool enabled = enableProperty != null && enableProperty.PropertyType == typeof(bool) &&
                        (bool)enableProperty.GetValue(viewModel);
                    TrailModEntry target = document.Mods[participant.Key];
                    target.Enabled = enabled;
                    if (!enabled)
                        continue;

                    foreach (PropertyInfo property in GetPersistedProperties(viewModel).Where(p => p.Name != "EnableMod"))
                        target.Settings[property.Name] = property.GetValue(viewModel);
                }
                return document;
            }

            private void ApplyDocument(TrailSettingsDocument document, bool editable)
            {
                Dictionary<string, object> participants = FindTargetViewModels();
                var prepared = new List<KeyValuePair<object, Dictionary<string, byte[]>>>();
                foreach (KeyValuePair<string, object> participant in participants)
                {
                    TrailModEntry entry = document.Mods.TryGetValue(participant.Key, out TrailModEntry stored)
                        ? stored
                        : new TrailModEntry();
                    Dictionary<string, byte[]> snapshot =
                        (Dictionary<string, byte[]>)Invoke(participant.Value, "System_CreateDisabledTrailSnapshot");
                    if (entry.Enabled)
                    {
                        snapshot["EnableMod"] = MessagePackSerializer.Serialize(true);
                        Dictionary<string, PropertyInfo> properties = GetPersistedProperties(participant.Value)
                            .ToDictionary(property => property.Name, StringComparer.Ordinal);
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
                    Invoke(item.Key, "System_EnterTrailPreset", item.Value, editable);
                trailContext = true;
            }

            private Dictionary<string, object> FindTargetViewModels()
            {
                return GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .Where(entry => TargetModIds.Contains(entry.Name, StringComparer.Ordinal))
                    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Last().ViewModel, StringComparer.Ordinal);
            }

            private static IEnumerable<PropertyInfo> GetPersistedProperties(object viewModel)
            {
                return viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanRead && property.CanWrite && property.GetCustomAttributes(false)
                        .Any(attribute => attribute.GetType().Name == "SyncPerPlayerAttribute" ||
                            attribute.GetType().Name == "SyncHostOnlyAttribute"));
            }

            private static object ConvertJsonValue(object value, Type targetType)
            {
                if (value == null)
                    throw new InvalidDataException($"Null cannot be assigned to [{targetType.FullName}].");
                Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (effectiveType.IsInstanceOfType(value))
                    return value;
                if (effectiveType.IsEnum)
                    return Enum.Parse(effectiveType, Convert.ToString(value, CultureInfo.InvariantCulture), true);
                if (effectiveType.IsArray && value is IEnumerable enumerable)
                {
                    Type elementType = effectiveType.GetElementType();
                    List<object> items = enumerable.Cast<object>().ToList();
                    Array array = Array.CreateInstance(elementType, items.Count);
                    for (int index = 0; index < items.Count; index++)
                        array.SetValue(ConvertJsonValue(items[index], elementType), index);
                    return array;
                }
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

}
