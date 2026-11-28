using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared.GameModes;
using BepInEx.Bootstrap;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.Interop;

namespace UiAcceptance
{
    internal static class TrailRoundTripSteps
    {
        private const string MissionFile = "Trail_Mission_1.trail";
        private const string SettingsFile = "Trail_Mission_1.modtrail.json";
        private const string LordsFile = "Trail_Mission_1.lordrequirements.json";
        private const int AuthoredSpeed = 70;
        private const int AuthoredAutosave = 20;
        private const string ScriptLordName = "Caracal";
        private const string ScriptLordGuid = "SerpUiAcceptanceCaracalTrail";
        private const string ScriptLordExtra = "{\"uiacceptance.fixture\":{\"savedValue\":84}}";
        [ThreadStatic] private static string makerRoot, customRoot;

        internal static void Register()
        {
            Runner.Register("Trail.ExportImport", ExportImport);
            Runner.Register("Trail.CoopLordRoundTrip", CoopLordRoundTrip);
        }

        private static IEnumerable<Wait> CoopLordRoundTrip(Step step)
        {
            bool scripted = step.Args.Length == 1 && step.Args[0] == "scripted";
            Runner.Require((step.Args.Length == 0 || scripted) && FatControler.currentScene == Enums.SceneIDS.FrontEnd,
                "Run the Coop Trail Maker check from the frontend.");
            yield return new Until("initialized Trail Maker view", () => FRONT_ManageTrail.Instance != null);
            string fixture = Environment.GetEnvironmentVariable("SHCDE_TRAIL_ACCEPTANCE_FIXTURE");
            Runner.Require(!string.IsNullOrWhiteSpace(fixture) && Path.IsPathRooted(fixture) && File.Exists(fixture),
                "Set SHCDE_TRAIL_ACCEPTANCE_FIXTURE to the original Coop Maker .trail file.");
            string fixtureDirectory = Path.GetDirectoryName(fixture);
            byte[] original = File.ReadAllBytes(fixture);
            byte[] originalLords = File.ReadAllBytes(Path.Combine(fixtureDirectory, LordsFile));
            string root = Path.Combine(Path.GetTempPath(), "ST" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Runner.Require(!Directory.Exists(root), "Private Coop Trail fixture directory already exists.");
            Directory.CreateDirectory(root);
            var restore = new Stack<Action>();
            try
            {
                SetSetting(restore, "ExtendedData_Serp", "EnableMod", true);
                SetSetting(restore, "ExtendedData_Serp", "EnableClientFeatures", true);
                SetSetting(restore, "ExtendedData_Serp", "EnableEditorFileIntegration", true);
                SetSetting(restore, "ExtendedData_Serp", "EnableMissionModSettings", true);
                if (scripted)
                {
                    SetSetting(restore, "ExtendedData_Serp", "EnableCoopPackages", true);
                    SetSetting(restore, "ExtendedData_Serp", "EnableLordDataIntegration", true);
                }
                InstallPathOverrides();
                makerRoot = Path.Combine(root, "Maker");
                customRoot = Path.Combine(root, "Trails");
                string destination = Path.Combine(customRoot, "ExportedCoop");
                Directory.CreateDirectory(makerRoot);
                Directory.CreateDirectory(destination);
                Runner.Require(SamePath(ReadPath(nameof(ConfigSettings.GetUserTrailMakerPath)), makerRoot) &&
                    SamePath(ReadPath(nameof(ConfigSettings.GetUserCustomTrailsPath)), customRoot),
                    "Coop Trail path redirection was not applied.");
                RedirectScanPath(restore, "TrailMakerPath", makerRoot, MapFileManager.Instance.RescanTrailMakerFolder);
                RedirectScanPath(restore, "CustomTrailsPath", customRoot, MapFileManager.Instance.RescanCustomTrailsFolder);
                string scriptPackage = scripted ? VerifyScriptPackage() : null;
                if (scripted) RedirectCoopCatalog(restore);

                string mission = Path.Combine(makerRoot, MissionFile);
                string lordSidecar = Path.Combine(makerRoot, LordsFile);
                File.WriteAllBytes(mission, original);
                File.WriteAllText(Path.Combine(makerRoot, SettingsFile), "{\"schemaVersion\":3,\"mods\":{}}");
                File.WriteAllText(Path.Combine(makerRoot, "cooptrail.enabled"), "enabled");
                var requirements = ReadJsonObject(Path.Combine(fixtureDirectory, LordsFile));
                var rows = ((List<object>)requirements["slots"]).Cast<Dictionary<string, object>>().ToArray();
                var installed = CustomisationFileManager.Instance.GetCustomLords();
                var selected = rows.FirstOrDefault(row =>
                    row.TryGetValue("lordType", out object type) && type != null && Convert.ToInt32(type) == -1 &&
                    row["requiresInstalledPackage"] is bool required && !required &&
                    row["lordName"] is string name &&
                    (scripted ? string.Equals(name, ScriptLordName, StringComparison.OrdinalIgnoreCase) :
                        !installed.Any(lord => string.Equals(lord.lordName, name, StringComparison.OrdinalIgnoreCase))));
                Runner.Require(selected != null, "The fixture does not contain the required Custom Lord slot.");
                const string extra = "{\"uiacceptance.fixture\":{\"savedValue\":42}}";
                selected["modLordJson"] = extra;
                int playerId = Convert.ToInt32(selected["playerId"]);
                File.WriteAllText(lordSidecar, Shared.DependencyFreeJson.Serialize(requirements));

                var mapManager = MapFileManager.Instance;
                mapManager.RescanTrailMakerFolder();
                var maker = FRONT_ManageTrail.Instance;
                FRONT_ManageTrail.Show(false);
                maker.ButtonClicked("1");
                var header = mapManager.GetHeaderFromTrailMaker(Path.GetFileNameWithoutExtension(MissionFile));
                Runner.Require(header != null && header.hasRestartSkirmishInfo && SamePath(header.filePath, mission),
                    "The Trail Maker scan did not select the exact private mission.");
                var initial = ReadRestart(mission);
                int compactPlayer = initial.lordTypes.Take(playerId).Count(type => type != -9999);
                Runner.Require(compactPlayer >= 3 && initial.lordTypes[playerId - 1] != -9999,
                    "The selected fixture Lord is outside the playable Coop AI slots.");
                var initialLord = initial.aivs[playerId - 1];
                var view = MainViewModel.Instance;
                Runner.Require(!GameSteps.ConfirmationVisible(), "A confirmation was already open before Maker Load.");
                maker.ButtonClicked("Load");
                yield return new Until("loaded private Maker mission lobby", () =>
                    !view.Show_ManageTrail && view.FRONTMultiplayer?.trailMakerMode == true);
                var lobby = view.FRONTMultiplayer;
                var liveLord = lobby.AIVs[playerId - 1];
                if (scripted)
                {
                    var scriptLord = CustomisationFileManager.Instance.GetCustomLords().Single(lord =>
                        string.Equals(lord.lordName, ScriptLordName, StringComparison.OrdinalIgnoreCase));
                    var installedAic = scriptLord.configs.Single(config =>
                        SamePath(config.path, scriptPackage) && config.name == initialLord.lordConfig.name);
                    Runner.Require(SamePath(liveLord.lordConfig.path, installedAic.path) &&
                        liveLord.lordConfig.encode().SequenceEqual(installedAic.encode()) &&
                        liveLord.lordConfig.checksum != initialLord.lordConfig.checksum,
                        "Maker Load did not take the edited installed Caracal AIC.");
                    var castle = scriptLord.aivs.Single(aiv => SamePath(aiv.path, scriptPackage));
                    liveLord.aivs.Clear();
                    liveLord.aivs.Add(castle);
                }
                else AssertLord(liveLord, initialLord, "opening the mission without its external Lord package");
                var otherCastle = lobby.AIVs.Where(lord => lord != null && lord != liveLord && lord.aivs != null)
                    .SelectMany(lord => lord.aivs)
                    .FirstOrDefault(aiv => aiv.data?.Length > 0 &&
                        liveLord.aivs.All(existing => existing.checksum != aiv.checksum));
                Runner.Require(otherCastle != null && liveLord.aivs.Count < 8,
                    "The fixture needs a distinct embedded castle to edit the selection.");
                liveLord.aivs.Add(otherCastle);
                var authoredLord = RecordLord(liveLord);
                AcceptLog.Info("COOP_TRAIL_MAKER_LOADED player=" + playerId + " lord=" + liveLord.lordName);

                lobby.ButtonClicked("TMManage");
                yield return new Until("Trail Maker after editing the Lord", () => view.Show_ManageTrail);
                maker.ButtonClicked("1");
                Runner.Require(SamePath(ReadPath(nameof(ConfigSettings.GetUserTrailMakerPath)), makerRoot),
                    "The Maker output no longer points to the private folder.");
                maker.ButtonClicked("Save");
                yield return new Until("private mission overwrite confirmation", () => view.Show_HUD_ConfirmationMP);
                view.ButtonConfirmationCommand.Execute("1");
                var saved = ReadRestart(mission);
                AssertLord(saved.aivs[playerId - 1], authoredLord, "saving the edited Lord selection");
                string savedExtra = scripted ? ScriptLordExtra : extra;
                AssertExtra(lordSidecar, playerId, savedExtra);
                Runner.Require(!Directory.GetFiles(makerRoot, "*.trail").Any(file => !SamePath(file, mission)),
                    "Saving left an unexpected Trail file in the private Maker folder.");
                AcceptLog.Info("COOP_TRAIL_MAKER_SAVED player=" + playerId + " castles=" + authoredLord.Castles.Length);

                InvokeMaker("ExportTrailMissions", destination);
                const string coopDirectory = "CoopMissions";
                string exportedTrail = Path.Combine(destination, coopDirectory, "Assets", "01", "mission.trail");
                Runner.Require(File.Exists(Path.Combine(destination, "cooptrail.json")) &&
                    File.Exists(Path.Combine(destination, coopDirectory, "01.coopmission.json")) && File.Exists(exportedTrail),
                    "The Coop exporter did not publish a complete mission package.");
                AssertLord(ReadRestart(exportedTrail).aivs[playerId - 1], authoredLord, "exporting the Coop mission asset");
                string coopMission = Path.Combine(destination, coopDirectory, "01.coopmission.json");
                string coopLordSidecar = Path.Combine(destination, coopDirectory, "01.lordrequirements.json");
                AssertExtra(coopLordSidecar, compactPlayer, savedExtra);
                AssertExtra(Path.Combine(destination, "TrailMakerSource", LordsFile), playerId, savedExtra);
                Runner.Require(File.ReadAllBytes(fixture).SequenceEqual(original) &&
                    File.ReadAllBytes(Path.Combine(fixtureDirectory, LordsFile)).SequenceEqual(originalLords),
                    "The read-only Coop fixture changed.");
                AcceptLog.Info("COOP_TRAIL_MAKER_EXPORTED player=" + playerId + " compact=" + compactPlayer +
                    " castles=" + authoredLord.Castles.Length + " extrasPreserved=true");
                if (scripted)
                {
                    VerifyRequiredPackage(coopMission, compactPlayer, scriptPackage,
                        () => InvokeMaker("ExportTrailMissions", destination));
                    foreach (Wait wait in StartExportedCoop(view, destination, exportedTrail))
                        yield return wait;
                }
            }
            finally
            {
                makerRoot = customRoot = null;
                foreach (Action undo in restore) undo();
                Runner.Require(SamePath(Path.GetDirectoryName(root), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)),
                    "Unexpected private Coop Trail fixture parent.");
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private static string VerifyScriptPackage()
        {
            string path = Environment.GetEnvironmentVariable("SHCDE_COOP_LORD_FIXTURE");
            string profilePath = Path.Combine(ConfigSettings.GetUserCustomLordsPath(), ScriptLordName);
            Runner.Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && SamePath(path, profilePath) &&
                Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
                "Set SHCDE_COOP_LORD_FIXTURE to the private test Caracal package in CustomLords.");
            Runner.Require(Equals(ReadJsonObject(Path.Combine(path, "info.json"))["GUID"], ScriptLordGuid) &&
                File.Exists(Path.Combine(path, "Scripts", "init.lua")) &&
                GameAssetModManager.Instance.TryGetRegisteredDirectory(ScriptLordGuid, out string registered) &&
                SamePath(registered, path), "The exact script Lord fixture was not registered by Script Extender.");
            return path;
        }

        private static void RedirectCoopCatalog(Stack<Action> restore)
        {
            object runtime = ExtendedRuntime(out object settings);
            FieldInfo root = runtime.GetType().GetField("customTrailsRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo selection = settings.GetType().GetProperty("ActiveCoopPackageId");
            MethodInfo refresh = runtime.GetType().GetMethod("RefreshPackageCatalog", Type.EmptyTypes);
            Runner.Require(root?.FieldType == typeof(string) && selection?.PropertyType == typeof(string) &&
                selection.CanWrite && refresh?.ReturnType == typeof(void),
                "ExtendedData's Coop package catalog contract changed.");
            object originalRoot = root.GetValue(runtime);
            object originalSelection = selection.GetValue(settings);
            restore.Push(() =>
            {
                root.SetValue(runtime, originalRoot);
                refresh.Invoke(runtime, null);
                selection.SetValue(settings, originalSelection);
            });
            root.SetValue(runtime, customRoot);
            Runner.Require(SamePath((string)root.GetValue(runtime), customRoot), "ExtendedData did not use the private Coop catalog root.");
        }

        private static object ExtendedRuntime(out object settings)
        {
            var plugin = Chainloader.PluginInfos["ExtendedData_Serp"].Instance;
            var type = plugin.GetType();
            settings = type.GetProperty("Settings")?.GetValue(plugin) ?? throw new StepFailure("ExtendedData settings are unavailable.");
            return type.GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ??
                throw new StepFailure("ExtendedData runtime is unavailable.");
        }

        private static void VerifyRequiredPackage(string mission, int playerId, string package, Action export)
        {
            Type requirements = Type.GetType("ExtendedData.TrailLordRequirements, ExtendedData.Core", throwOnError: true);
            Type slotType = requirements.Assembly.GetType("ExtendedData.TrailLordSlot", true);
            object ReadSlot()
            {
                object saved = requirements.GetMethod("Read", new[] { typeof(string) }).Invoke(null, new object[] { mission });
                var slots = (IEnumerable)requirements.GetProperty("Slots").GetValue(saved);
                return slots.Cast<object>().Single(item => Equals(slotType.GetProperty("PlayerId").GetValue(item), playerId));
            }
            object slot = ReadSlot();
            string config = (string)slotType.GetProperty("ConfigName").GetValue(slot);
            Type fingerprint = requirements.Assembly.GetType("ExtendedData.LordPackageFingerprint", true);
            object packageState = fingerprint.GetMethod("Capture").Invoke(null, new object[] { package, config });
            string digest = (string)packageState.GetType().GetProperty("Digest").GetValue(packageState);
            Runner.Require(Equals(slotType.GetProperty("RequiresInstalledPackage").GetValue(slot), true) &&
                Equals(slotType.GetProperty("PackageDigest").GetValue(slot), digest),
                "The Coop export did not bind the registered script package fingerprint.");

            var assembly = Chainloader.PluginInfos["ExtendedData_Serp"].Instance.GetType().Assembly;
            MethodInfo resolve = assembly.GetType("ExtendedData.TrailLordPackageRuntime", true).GetMethod("TryResolve",
                BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { slotType, typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(string).MakeByRefType() }, null);
            Runner.Require(resolve?.ReturnType == typeof(bool), "The required-package resolver contract changed.");
            object[] args = { slot, null, null, null };
            Runner.Require((bool)resolve.Invoke(null, args) && SamePath((string)args[2], Path.Combine(package, config + ".lordjson")),
                "The registered package was rejected with a valid mixed castle list: " + args[3]);

            string parent = Path.GetDirectoryName(Path.GetDirectoryName(package));
            string moved = Path.Combine(parent, ".uiacceptance-caracal-unavailable");
            Runner.Require(!Directory.Exists(moved) && !File.Exists(moved), "The package-absence fixture path already exists.");
            Directory.Move(package, moved);
            try
            {
                export();
                args[0] = ReadSlot();
                Runner.Require(Equals(slotType.GetProperty("RequiresInstalledPackage").GetValue(args[0]), true) &&
                    Equals(slotType.GetProperty("PackageDigest").GetValue(args[0]), digest) && !GameSteps.ConfirmationVisible(),
                    "Re-exporting without the package lost its dependency or showed an export error.");
                Runner.Require(!(bool)resolve.Invoke(null, args) &&
                    ((string)args[3])?.IndexOf(ScriptLordName, StringComparison.OrdinalIgnoreCase) >= 0,
                    "The production start resolver accepted the missing script package.");
                AcceptLog.Info("COOP_TRAIL_SCRIPT_PACKAGE_MISSING exported=true rejected=true lord=" + ScriptLordName);
            }
            finally { Directory.Move(moved, package); }
            Runner.Require((bool)resolve.Invoke(null, args), "The restored script package was rejected: " + args[3]);
            AcceptLog.Info("COOP_TRAIL_SCRIPT_PACKAGE_REGISTERED guid=" + ScriptLordGuid + " digest=" + digest);
        }

        private static IEnumerable<Wait> StartExportedCoop(MainViewModel view, string destination, string trail)
        {
            object runtime = ExtendedRuntime(out object settings);
            var manifest = ReadJsonObject(Path.Combine(destination, "cooptrail.json"));
            string fingerprint = (string)manifest["contentFingerprint"];
            var activeFingerprint = settings.GetType().GetProperty("ActiveCoopPackageFingerprint");
            Runner.Require(activeFingerprint?.PropertyType == typeof(string), "ExtendedData's selected-package identity changed.");
            view.FRONTMultiplayer.ButtonClicked("CloseManageTrail");
            view.ButtonMainMenuFunction("Skirmish");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=package-picker skirmish=" + view.Show_Frontend_Skirmish);
            Noesis.ComboBox picker = null;
            int selection = -1;
            string label = manifest["displayName"] + " (" + manifest["missionCount"] + ")";
            yield return NextFrame.Instance;
            var initialPicker = GameXAMLManagerAPI.Instance.FindGlobalElement("ExtendedDataCoopTrailDropdown") as Noesis.ComboBox;
            AcceptLog.Info("COOP_TRAIL_SCRIPT_PICKER visible=" + initialPicker?.IsVisible +
                " enabled=" + initialPicker?.IsEnabled + " context=" + initialPicker?.DataContext?.GetType().FullName);
            yield return new Until("exported package in the Skirmish Coop dropdown", () =>
            {
                picker = GameXAMLManagerAPI.Instance.FindGlobalElement("ExtendedDataCoopTrailDropdown") as Noesis.ComboBox;
                if (picker?.IsVisible != true || !picker.IsEnabled)
                    return false;
                object model = picker.DataContext;
                var labels = model?.GetType().GetProperty("Options")?.GetValue(model) as string[];
                if (labels == null && picker.ItemsSource is IEnumerable items)
                    labels = items.Cast<object>().Select(item => item?.ToString()).ToArray();
                if (labels == null) return false;
                selection = Array.IndexOf(labels, label);
                return selection >= 0;
            });
            picker.SelectedIndex = selection;
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=package-identity index=" + selection + " label=" + label);
            yield return new Until("exact exported Coop package selected", () =>
                string.Equals((string)activeFingerprint.GetValue(settings), fingerprint, StringComparison.OrdinalIgnoreCase));
            view.ButtonMainMenuFunction("Coops");
            view.ButtonMainMenuFunction("Coop");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=local-steam-host fingerprint=" + fingerprint);
            yield return new Until("local Steam Coop host and invite panel", () =>
                view.FRONTMultiplayer?.currentLobby?.isHost == true && view.Show_CoopHostInvitePane);
            view.MultiplayerMenuCommandFunction("CoopSinglePlayer");
            view.SkirmishAIAddClick(((int)AILords.SK_RAT - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=local-ai-ally");
            yield return new Until("local Coop AI ally", () =>
                view.FRONTMultiplayer.singlePlayerCoop && view.Show_CoopHostJoinedPane && !view.Show_CoopHostInvitePane);
            view.TrailCampaignMenuCommandFunction("1");
            string mapPath = Path.Combine(Path.GetDirectoryName(trail), "map.map");
            FieldInfo selected = runtime.GetType().GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo header = selected?.FieldType.GetProperty("Header");
            Runner.Require(header?.PropertyType == typeof(FileHeader), "ExtendedData's prepared Coop map contract changed.");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=prepared-map path=" + mapPath);
            yield return new Until("exact exported map prepared", () =>
                selected.GetValue(runtime) is object prepared && header.GetValue(prepared) is FileHeader map &&
                SamePath(map.filePath, mapPath));
            long before = UiAcceptancePlugin.StartedSession;
            view.MultiplayerMenuCommandFunction("COOP_START");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=mission-start");
            yield return new Until("actual local Coop mission", () =>
                UiAcceptancePlugin.StartedSession > before && UiAcceptancePlugin.LastContext is var context &&
                context?.Mode.Kind == GameModeKind.CoopTrail && !context.Mode.IsRealMultiplayer &&
                !string.IsNullOrEmpty(context.FilePath) && SamePath(context.FilePath, mapPath));
            int ticks = UiAcceptancePlugin.Ticks;
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=simulation-ticks");
            yield return new Until("45 simulation ticks in the exported mission", () =>
            {
                if (view.Show_HUD_Briefing) view.ButtonBriefingResume(null);
                return UiAcceptancePlugin.Ticks >= ticks + 45 && GameData.Instance?.lastGameState != null;
            });
            AcceptLog.Info("COOP_TRAIL_SCRIPT_PACKAGE_STARTED local=true ticks=" + (UiAcceptancePlugin.Ticks - ticks));
            view.HUDmain.InGameOptions(null, null);
            yield return NextFrame.Instance;
            view.ButtonIngameMenuCommand.Execute("6");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=quit-confirmation");
            yield return new Until("return confirmation after scripted Coop", GameSteps.ConfirmationVisible);
            view.ButtonConfirmationCommand.Execute("1");
            AcceptLog.Info("COOP_TRAIL_SCRIPT_NAV wait=frontend");
            yield return new Until("front end after scripted Coop", () => FatControler.currentScene == Enums.SceneIDS.FrontEnd);
        }

        private static void RedirectScanPath(Stack<Action> restore, string field, string path, Action rescan)
        {
            var manager = MapFileManager.Instance;
            // The scan uses constructor-cached paths instead of the ConfigSettings getters.
            var cache = typeof(MapFileManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Runner.Require(cache?.FieldType == typeof(string), "The installed Trail scan cache changed: " + field);
            object prior = cache.GetValue(manager);
            restore.Push(() =>
            {
                cache.SetValue(manager, prior);
                rescan();
            });
            cache.SetValue(manager, path);
        }

        private static Dictionary<string, object> ReadJsonObject(string path) =>
            Shared.DependencyFreeJson.Parse(File.ReadAllText(path)) as Dictionary<string, object> ??
            throw new StepFailure("Expected JSON object at " + path);

        private static void AssertExtra(string sidecar, int playerId, string expected)
        {
            var rows = (List<object>)ReadJsonObject(sidecar)["slots"];
            var slot = rows.Cast<Dictionary<string, object>>()
                .SingleOrDefault(row => Convert.ToInt32(row["playerId"]) == playerId);
            Runner.Require(slot != null && slot["modLordJson"] is string actual &&
                Shared.DependencyFreeJson.Serialize(Shared.DependencyFreeJson.Parse(actual)) ==
                Shared.DependencyFreeJson.Serialize(Shared.DependencyFreeJson.Parse(expected)),
                "The authored extra was lost for player " + playerId + " in " + sidecar);
        }

        private static void AssertLord(FRONT_Multiplayer.MPAIVInfo actual, FRONT_Multiplayer.MPAIVInfo expected, string action)
            => AssertLord(actual, RecordLord(expected), action);

        private static (string Name, byte[] Config, byte[][] Castles) RecordLord(FRONT_Multiplayer.MPAIVInfo lord) =>
            (lord.lordName, lord.lordConfig.encode(), lord.aivs.Select(castle => castle.encode()).ToArray());

        private static void AssertLord(FRONT_Multiplayer.MPAIVInfo actual,
            (string Name, byte[] Config, byte[][] Castles) expected, string action)
        {
            Runner.Require(actual != null && actual.lordName == expected.Name &&
                actual.lordConfig != null && actual.lordConfig.encode().SequenceEqual(expected.Config) &&
                actual.aivs.Count == expected.Castles.Length &&
                actual.aivs.Zip(expected.Castles, (left, right) => left.encode().SequenceEqual(right)).All(match => match),
                "The embedded AIC or ordered AIV data changed while " + action + ".");
        }

        private static IEnumerable<Wait> ExportImport(Step step)
        {
            Runner.Require(step.Args.Length == 0 && FatControler.currentScene == Enums.SceneIDS.FrontEnd,
                "Run Trail export/import only from the frontend.");
            var view = MainViewModel.Instance;
            yield return new Until("initialized Trail Maker view", () => FRONT_ManageTrail.Instance != null);
            string originalMaker = ConfigSettings.GetUserTrailMakerPath();
            string source = Environment.GetEnvironmentVariable("SHCDE_TRAIL_ACCEPTANCE_FIXTURE");
            if (string.IsNullOrWhiteSpace(source)) source = Path.Combine(originalMaker, MissionFile);
            Runner.Require(Path.IsPathRooted(source) && File.Exists(source), "The read-only Trail fixture is missing.");
            // Vanilla File.Copy does not overwrite. Even if some caller had inlined the
            // old getter, it cannot create a new mission in the user's Maker directory.
            string originalFirst = Path.Combine(originalMaker, MissionFile);
            Runner.Require(File.Exists(originalFirst) &&
                File.Exists(Path.Combine(originalMaker, SettingsFile)) && File.Exists(Path.Combine(originalMaker, LordsFile)),
                "The existing Maker files must also protect against an unredirected import.");
            byte[] originalFile = File.ReadAllBytes(originalFirst);
            var restoreSettings = new Stack<Action>();
            string root = Path.Combine(Path.GetTempPath(), "ST" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Runner.Require(!Directory.Exists(root), "Private Trail fixture directory already exists.");
            Directory.CreateDirectory(root);
            try
            {
                SetSetting(restoreSettings, "ExtendedData_Serp", "EnableMod", true);
                SetSetting(restoreSettings, "ExtendedData_Serp", "EnableClientFeatures", true);
                SetSetting(restoreSettings, "ExtendedData_Serp", "EnableEditorFileIntegration", true);
                SetSetting(restoreSettings, "ExtendedData_Serp", "EnableMissionModSettings", true);
                SetSetting(restoreSettings, "BugfixesAndQoL_Serp", "EnableClientFeatures", true);
                SetSetting(restoreSettings, "BugfixesAndQoL_Serp", "EnableCustomTrailExtremeGoldFix", true);

                string authored = Path.Combine(root, "source");
                string exported = Path.Combine(root, "trails", "Export");
                string imported = Path.Combine(root, "imported");
                string replay = Path.Combine(root, "trails", "Reimported");
                foreach (string path in new[] { authored, exported, imported, replay }) Directory.CreateDirectory(path);

                InstallPathOverrides();
                makerRoot = authored;
                customRoot = Path.Combine(root, "trails");
                Runner.Require(ReadPath(nameof(ConfigSettings.GetUserTrailMakerPath)) == makerRoot &&
                    ReadPath(nameof(ConfigSettings.GetUserCustomTrailsPath)) == customRoot, "Trail path redirection was not applied.");

                byte[] sourceBytes = File.ReadAllBytes(source);
                string map = Path.Combine(root, "fixture.map");
                File.WriteAllBytes(map, ContainerBytes("ExtractMap", sourceBytes));
                var restart = ReadRestart(source);
                restart.selectedHeader = MapFileManager.Instance.GetFileInfoFromFileName(map, map, 0);
                Runner.Require(restart.selectedHeader != null, "The extracted embedded map was not readable.");
                restart.MPsetupData.starting_gamespeed = AuthoredSpeed;
                restart.MPsetupData.autosave = AuthoredAutosave;
                restart.MPsetupData.allow_autotrading = 0;
                restart.customisedExtremeTrail = true;
                string authoredTrail = Path.Combine(authored, MissionFile);
                EditorDirector.instance.SaveCustomTrailMap(map, restart.selectedHeader.fileName, authoredTrail, restart);
                Runner.Require(!restart.customisedExtremeTrail, "BugfixesAndQoL did not normalize the authored extreme marker.");
                Runner.Require(restart.MPsetupData.starting_gamespeed == AuthoredSpeed &&
                    restart.MPsetupData.autosave == AuthoredAutosave, "Saving changed in-memory authoring values.");
                AssertSetup(authoredTrail, ConfigSettings.Default_GameSpeed, 0);
                string draftSidecar = Path.Combine(authored, SettingsFile);
                Runner.Require(File.Exists(draftSidecar) && new FileInfo(draftSidecar).Length > 0,
                    "ExtendedData did not save a mod-settings sidecar.");

                // Recreate pre-neutralization authoring metadata in only the test-owned copy.
                // This proves the export's rewrite independently of the save-time rewrite.
                var legacy = ReadRestart(authoredTrail);
                legacy.selectedHeader = restart.selectedHeader;
                legacy.MPsetupData.starting_gamespeed = AuthoredSpeed;
                legacy.MPsetupData.autosave = AuthoredAutosave;
                byte[] authoredBytes = ReplaceRestart(File.ReadAllBytes(authoredTrail), legacy.encode());
                File.WriteAllBytes(authoredTrail, authoredBytes);
                File.Delete(Path.Combine(authored, LordsFile)); // Its old digest belongs to the pre-fixture bytes.
                byte[] authoredSettings = File.ReadAllBytes(draftSidecar);
                AssertSetup(authoredTrail, AuthoredSpeed, AuthoredAutosave);

                InvokeMaker("ExportTrailMissions", exported);
                string exportedTrail = Path.Combine(exported, MissionFile);
                AssertSetup(exportedTrail, ConfigSettings.Default_GameSpeed, 0);
                Runner.Require(File.ReadAllBytes(authoredTrail).SequenceEqual(authoredBytes) &&
                    File.ReadAllBytes(draftSidecar).SequenceEqual(authoredSettings),
                    "Export modified its authored source.");
                Runner.Require(ContainerBytes("ExtractMap", File.ReadAllBytes(exportedTrail))
                    .SequenceEqual(ContainerBytes("ExtractMap", authoredBytes)), "Export changed the embedded map.");
                Runner.Require(File.Exists(Path.Combine(exported, SettingsFile)) && File.Exists(Path.Combine(exported, LordsFile)),
                    "The exported Trail lacks the expected ExtendedData sidecars.");
                Runner.Require(!File.Exists(Path.Combine(exported, "cooptrail.json")), "The single-player fixture exported a Coop package.");
                AcceptLog.Info("TRAIL_ROUNDTRIP_EXPORT mission=1 autoTrade=off speed=40 autosave=0 sourceUnchanged=true");

                makerRoot = imported;
                string userCoopMarker = Path.Combine(originalMaker, "cooptrail.enabled");
                // A normal import may clear the marker. Block any deletion of the user's
                // existing file even if an unsupported runtime inlined the old path getter.
                using (var marker = File.Exists(userCoopMarker) ? File.OpenRead(userCoopMarker) : null)
                    InvokeMaker("ImportTrailMissions", "Export");
                foreach (string name in new[] { MissionFile, SettingsFile, LordsFile })
                {
                    string target = Path.Combine(imported, name);
                    Runner.Require(File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(Path.Combine(exported, name))),
                        "The imported payload did not match export for " + name);
                    File.Copy(target, Path.Combine(replay, name)); // Register the imported bytes as a playable Custom Trail.
                }
                AssertSetup(Path.Combine(imported, MissionFile), ConfigSettings.Default_GameSpeed, 0);
                Runner.Require(File.ReadAllBytes(originalFirst).SequenceEqual(originalFile), "The original Maker mission changed.");
                var manager = MapFileManager.Instance;
                var cache = typeof(MapFileManager).GetField("CustomTrailsPath", BindingFlags.Instance | BindingFlags.NonPublic);
                Runner.Require(cache?.FieldType == typeof(string), "The installed Custom Trail scan cache changed.");
                object prior = cache.GetValue(manager);
                restoreSettings.Push(() =>
                {
                    cache.SetValue(manager, prior);
                    manager.RescanCustomTrailsFolder();
                });
                cache.SetValue(manager, customRoot);
                manager.RescanCustomTrailsFolder();
                var header = manager.GetHeaderFromCustomTrail("Reimported", Path.GetFileNameWithoutExtension(MissionFile));
                Runner.Require(header != null && SamePath(header.filePath, Path.Combine(replay, MissionFile)),
                    "The game did not register the exact imported Trail.");
                long before = UiAcceptancePlugin.StartedSession;
                view.StartCustomTrailMission("Reimported", 1, 1);
                yield return new Until("managed Custom Trail mission", () => UiAcceptancePlugin.StartedSession > before &&
                    UiAcceptancePlugin.LastContext?.Mode.Kind == GameModeKind.CustomTrail);
                yield return new Until("imported mission briefing", () => view.Show_HUD_Briefing);
                view.ButtonBriefingResume(null);
                int initialTicks = UiAcceptancePlugin.Ticks;
                yield return new Until("imported mission simulation and current play state", () =>
                    UiAcceptancePlugin.Ticks >= initialTicks + 45 && GameData.Instance?.lastGameState != null);
                Runner.Require(!GameData.Instance.lastGameState.MP_AllowAutoTrading, "AutoTrade became enabled in the reimported mission.");
                AcceptLog.Info("TRAIL_ROUNDTRIP_IMPORTED playable=true autoTrade=off sidecars=2 session=" +
                    UiAcceptancePlugin.StartedSession);
                view.HUDmain.InGameOptions(null, null);
                yield return NextFrame.Instance;
                view.ButtonIngameMenuCommand.Execute("6");
                yield return new Until("return confirmation", GameSteps.ConfirmationVisible);
                view.ButtonConfirmationCommand.Execute("1");
                yield return new Until("front end after the replay of the imported mission", () =>
                    FatControler.currentScene == Enums.SceneIDS.FrontEnd);
            }
            finally
            {
                makerRoot = customRoot = null;
                foreach (Action undo in restoreSettings) undo();
                // Only the unique test-created directory under the known temp parent is owned.
                Runner.Require(SamePath(Path.GetDirectoryName(root), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)),
                    "Unexpected private Trail fixture parent.");
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private static void SetSetting(Stack<Action> restore, string guid, string name, bool value)
        {
            Runner.Require(Chainloader.PluginInfos.TryGetValue(guid, out var info), "Missing required test consumer " + guid);
            object model = info.Instance.GetType().GetProperty("Settings")?.GetValue(info.Instance) ??
                throw new StepFailure("Settings unavailable for " + guid);
            PropertyInfo property = model.GetType().GetProperty(name) ?? throw new StepFailure("Missing setting " + name);
            object previous = property.GetValue(model);
            restore.Push(() => property.SetValue(model, previous));
            property.SetValue(model, value);
            Runner.Require(Equals(property.GetValue(model), value), guid + " refused " + name);
        }

        private static void InstallPathOverrides()
        {
            PatchPath(nameof(ConfigSettings.GetUserTrailMakerPath), nameof(GetMakerPath));
            PatchPath(nameof(ConfigSettings.GetUserCustomTrailsPath), nameof(GetCustomPath));
        }

        private static void PatchPath(string name, string prefix)
        {
            MethodInfo method = typeof(ConfigSettings).GetMethod(name, Type.EmptyTypes);
            Runner.Require(method?.ReturnType == typeof(string) && method.IsStatic, "Trail path getter contract changed: " + name);
            var patch = typeof(TrailRoundTripSteps).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
            UiAcceptancePlugin.Harmony.Patch(method, prefix: new HarmonyMethod(patch));
        }

        private static string ReadPath(string getter) => (string)typeof(ConfigSettings).GetMethod(getter, Type.EmptyTypes).Invoke(null, null);

        private static bool GetMakerPath(ref string __result) => Redirect(makerRoot, ref __result);
        private static bool GetCustomPath(ref string __result) => Redirect(customRoot, ref __result);
        private static bool Redirect(string path, ref string result)
        {
            if (path == null) return true;
            result = path;
            return false;
        }

        private static void InvokeMaker(string method, string path)
        {
            MethodInfo value = typeof(FRONT_ManageTrail).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            Runner.Require(value?.ReturnType == typeof(void), "Missing Trail Maker callback " + method);
            value.Invoke(FRONT_ManageTrail.Instance, new object[] { path });
        }

        private static HUD_IngameMenu.RestartSkirmishMapInfo ReadRestart(string path) =>
            MapFileManager.Instance.GetFileInfoFromFileName(path, path, 4, loadRestartInfo: true)?.restartSkirmishInfo ??
            throw new StepFailure("The game did not read the restart setup for " + path);

        private static void AssertSetup(string path, int speed, int autosave)
        {
            var restart = ReadRestart(path);
            Runner.Require(restart.MPsetupData?.starting_gamespeed == speed &&
                restart.MPsetupData.autosave == autosave && restart.MPsetupData.allow_autotrading == 0 &&
                !restart.customisedExtremeTrail, "Unexpected authored or exported setup for " + path);
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        private static Type ContainerType => Type.GetType("APIShared.Maps.TrailContainer, APIShared.Content", throwOnError: true);
        private static byte[] ContainerBytes(string method, byte[] bytes) =>
            (byte[])ContainerType.GetMethod(method).Invoke(null, new object[] { bytes, int.MaxValue });

        private static byte[] ReplaceRestart(byte[] source, byte[] restart)
        {
            object layout = ContainerType.GetMethod("ReadLayout").Invoke(null, new object[] { source, int.MaxValue });
            Type layoutType = layout.GetType();
            int lengthField = (int)layoutType.GetProperty("RestartSizeFieldOffset").GetValue(layout);
            int payload = (int)layoutType.GetProperty("RestartPayloadOffset").GetValue(layout);
            int oldLength = (int)layoutType.GetProperty("RestartSize").GetValue(layout);
            int oldEnd = checked(payload + oldLength);
            var result = new byte[checked(source.Length - oldLength + restart.Length)];
            Buffer.BlockCopy(source, 0, result, 0, payload);
            Buffer.BlockCopy(BitConverter.GetBytes(restart.Length), 0, result, lengthField, sizeof(int));
            Buffer.BlockCopy(restart, 0, result, payload, restart.Length);
            Buffer.BlockCopy(source, oldEnd, result, payload + restart.Length, source.Length - oldEnd);
            return result;
        }
    }
}
