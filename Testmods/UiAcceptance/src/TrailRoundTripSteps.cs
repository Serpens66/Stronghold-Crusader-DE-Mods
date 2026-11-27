using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared.GameModes;
using BepInEx.Bootstrap;
using CrusaderDE;
using HarmonyLib;

namespace UiAcceptance
{
    internal static class TrailRoundTripSteps
    {
        private const string MissionFile = "Trail_Mission_1.trail";
        private const string SettingsFile = "Trail_Mission_1.modtrail.json";
        private const string LordsFile = "Trail_Mission_1.lordrequirements.json";
        private const int AuthoredSpeed = 70;
        private const int AuthoredAutosave = 20;
        [ThreadStatic] private static string makerRoot, customRoot;

        internal static void Register() => Runner.Register("Trail.ExportImport", ExportImport);

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
