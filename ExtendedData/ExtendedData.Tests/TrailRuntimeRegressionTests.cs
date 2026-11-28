using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using CrusaderDE;
using ExtendedData;
using ExtendedData.Core;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;

internal static class TrailRuntimeRegressionTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static T Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    internal static void Publication()
    {
        foreach (string scenario in new[] { "new", "replace", "backup", "install", "manifest",
            "restore", "delete-new", "cleanup", "new-install", "new-manifest" })
        {
            using var fixture = new Scope();
            string target = Path.Combine(fixture.Root, "target");
            string stage = Path.Combine(fixture.Root, "stage");
            string oldMissions = Path.Combine(target, "CoopMissions");
            string newMissions = Path.Combine(stage, "CoopMissions");
            Directory.CreateDirectory(newMissions);
            File.WriteAllText(Path.Combine(newMissions, "mission"), "new mission");
            bool hasOld = scenario != "new" && !scenario.StartsWith("new-", StringComparison.Ordinal);
            var oldManifest = Manifest('A');
            var newManifest = Manifest('B');
            string manifestPath = Path.Combine(target, "cooptrail.json");
            if (hasOld)
            {
                Directory.CreateDirectory(oldMissions);
                File.WriteAllText(Path.Combine(oldMissions, "mission"), "old mission");
                CoopTrailPackageManifestJson.WriteAtomic(manifestPath, oldManifest);
            }
            var files = new PublicationFiles(stage, target);
            if (scenario == "restore") files.Fail.UnionWith(new[] { "install", "restore" });
            else if (scenario == "delete-new") files.Fail.UnionWith(new[] { "manifest", "delete-new" });
            else if (scenario != "new" && scenario != "replace")
                files.Fail.Add(scenario.StartsWith("new-", StringComparison.Ordinal) ? scenario.Substring(4) : scenario);

            bool succeeds = scenario == "new" || scenario == "replace" || scenario == "cleanup";
            IOException error = null;
            if (succeeds) CoopTrailPackagePublication.Publish(stage, target, newManifest, files);
            else error = Reject<IOException>(() => CoopTrailPackagePublication.Publish(stage, target, newManifest, files));

            if (succeeds)
            {
                Check(File.ReadAllText(Path.Combine(oldMissions, "mission")) == "new mission", scenario + ": new mission lost");
                Check(CoopTrailPackageManifestJson.Read(manifestPath).ContentFingerprint == newManifest.ContentFingerprint,
                    scenario + ": manifest not committed");
            }
            else if (hasOld)
            {
                Check(CoopTrailPackageManifestJson.Read(manifestPath).ContentFingerprint == oldManifest.ContentFingerprint,
                    scenario + ": original manifest changed");
                if (scenario == "restore" || scenario == "delete-new")
                {
                    Check(File.ReadAllText(Path.Combine(files.BackupPath, "mission")) == "old mission",
                        scenario + ": recovery backup lost");
                    Check(error.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count == 2 &&
                        error.Message.Contains(files.BackupPath) && error.Message.Contains("injected " +
                        (scenario == "restore" ? "install" : "manifest")) &&
                        error.Message.Contains("injected " + scenario), scenario + ": recovery details missing");
                }
                else Check(File.ReadAllText(Path.Combine(oldMissions, "mission")) == "old mission",
                    scenario + ": old mission lost");
                if (scenario == "backup" || scenario == "install")
                    Check(files.Deleted.Count == 0 && Directory.Exists(newMissions), scenario + ": deleted unowned data");
            }
            else Check(!Directory.Exists(oldMissions) && !File.Exists(manifestPath), scenario + ": partial first export remains");
            if (scenario == "cleanup")
                Check(File.ReadAllText(Path.Combine(files.BackupPath, "mission")) == "old mission",
                    "cleanup failure rolled back committed package or destroyed backup");
        }
    }

    private static CoopTrailPackageManifest Manifest(char fingerprint) => new()
    {
        SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
        PackageId = Guid.NewGuid().ToString("D"), DisplayName = "Test", MissionCount = 1,
        ContentFingerprint = new string(fingerprint, 64),
    };

    private sealed class PublicationFiles : ICoopTrailPackageFiles
    {
        private readonly string stage, target;
        internal readonly HashSet<string> Fail = new(StringComparer.Ordinal);
        internal readonly List<string> Deleted = new();
        internal string BackupPath;
        internal PublicationFiles(string stage, string target) { this.stage = stage; this.target = target; }
        private void Fault(string operation) { if (Fail.Contains(operation)) throw new IOException("injected " + operation); }
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void MoveDirectory(string source, string destination)
        {
            string operation;
            if (source == Path.Combine(target, "CoopMissions")) { operation = "backup"; BackupPath = destination; }
            else operation = source == Path.Combine(stage, "CoopMissions") ? "install" : "restore";
            Fault(operation);
            Directory.Move(source, destination);
        }
        public void DeleteDirectory(string path)
        {
            Deleted.Add(path);
            Fault(path == BackupPath ? "cleanup" : "delete-new");
            Directory.Delete(path, true);
        }
        public void WriteManifest(string path, CoopTrailPackageManifest manifest)
        {
            Fault("manifest");
            CoopTrailPackageManifestJson.WriteAtomic(path, manifest);
        }
    }

    internal static void PortableLords()
    {
        using var fixture = new Scope();
        var slot = new TrailLordSlot { PlayerId = 3, LordType = -1, LordName = "Example",
            ConfigName = "aic", ConfigChecksum = "11", AivChecksums = new[] { "22" } };
        Check(TrailLordPackageRuntime.TryResolve(slot, out var media, out var path, out var reason) &&
            media == null && path == null && !string.IsNullOrWhiteSpace(reason), "portable Lord requires installation");
        slot.RequiresInstalledPackage = true;
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _), "missing mandatory package accepted");
        var lord = fixture.InstallLord("Example", "installed", unsupported: true);
        slot.PackageDigest = LordPackageFingerprint.Capture(lord.configs[0].path, "aic").Digest;
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _), "unregistered mandatory package accepted");
        GameAssetModManager.Instance.Registered["review-lord"] = fixture.Root;
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _), "wrong registered directory accepted");
        GameAssetModManager.Instance.Registered["review-lord"] = lord.configs[0].path;
        string infoPath = Path.Combine(lord.configs[0].path, "info.json");
        string validInfo = File.ReadAllText(infoPath);
        File.WriteAllText(infoPath, "{");
        Shared.RuntimeHealth.Reports.Clear();
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _) &&
            Shared.RuntimeHealth.Reports.Contains("lord-package-manifest"), "invalid required manifest is rejected and reported");
        File.WriteAllText(infoPath, validInfo);
        string digest = slot.PackageDigest;
        slot.PackageDigest = new string('F', 64);
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _), "changed mandatory package accepted");
        slot.PackageDigest = digest;
        Check(TrailLordPackageRuntime.TryResolve(slot, out media, out path, out _) && media == null &&
            path == Path.Combine(lord.configs[0].path, "aic.lordjson"), "registered matching package rejected without media");
        slot.AivChecksums = new[] { "22", "791" };
        Check(TrailLordPackageRuntime.TryResolve(slot, out _, out path, out _) &&
            path == Path.Combine(lord.configs[0].path, "aic.lordjson"),
            "a registered matching script package rejected a mixed embedded castle selection");
        slot.AivChecksums = new[] { "22" };
        GameAIManagerAPI.Instance.Supported.Add("Example");
        Check(TrailLordPackageRuntime.TryResolve(slot, out media, out _, out _) && media == "Example",
            "registered optional media not resolved");
        slot.RequiresInstalledPackage = false;
        fixture.InstallLord("Other", "other", unsupported: false);
        Directory.CreateDirectory(Path.Combine(ConfigSettings.MediaPath, "Example"));
        Directory.CreateDirectory(Path.Combine(ConfigSettings.MediaPath, "Other"));
        Check(TrailLordPackageRuntime.TryResolve(slot, out media, out path, out reason) && media == null &&
            path == null && reason.Contains("Several"), "ambiguous optional media blocked the mission or chose a provider");
        // Legacy sidecars can omit LordType: do not guess when distinct installed types match.
        CustomisationFileManager.Instance.Configs = new[] { new CustomisationFileManager.CustomLordConfig
            { lordType = 0, name = "aic", checksum = 11, path = lord.configs[0].path } };
        CustomisationFileManager.Instance.AivsByType[0] = lord.aivs;
        slot.LordType = null;
        Check(TrailLordPackageRuntime.TryResolve(slot, out media, out _, out reason) && media == null &&
            reason.Contains("ambiguous"), "legacy optional type ambiguity became mandatory");
        slot.RequiresInstalledPackage = true;
        Check(!TrailLordPackageRuntime.TryResolve(slot, out _, out _, out _), "mandatory type ambiguity accepted");
        Reject<InvalidDataException>(() => LordDataSnapshot.ValidateModLord("{"));
        Reject<InvalidOperationException>(() => new FixesLordPreferencesBridge().ValidateSnapshotValue("{}"));
    }

    internal static void CaptureSelection()
    {
        using var fixture = new Scope();
        var lord = fixture.InstallLord("Example", "installed", unsupported: false);
        var restart = new HUD_IngameMenu.RestartSkirmishMapInfo
        {
            lordTypes = new List<int> { -9999, -1, -9999, 1, -9999, 2 },
            aivs = new FRONT_Multiplayer.MPAIVInfo[6],
        };
        restart.aivs[3] = new FRONT_Multiplayer.MPAIVInfo { lordName = "Missing Guest", lordType = -1,
            lordConfig = new CustomisationFileManager.CustomLordConfig { name = "missing", checksum = 99 } };
        restart.aivs[5] = new FRONT_Multiplayer.MPAIVInfo { lordName = "Example", lordType = -1,
            lordConfig = lord.configs[0], aivs = lord.aivs };
        var active = CoopTrailSlotSelection.GetActiveSlots(restart.lordTypes);
        Check(active.SequenceEqual(new[] { 1, 3, 5 }), "empty slots changed active order");
        var included = CoopTrailSlotSelection.GetAiPlayerIds(active);
        Check(included.SetEquals(new[] { 6 }), "human slots were not removed before capture");
        var slots = TrailLordRequirementSource.CaptureForSave(null, restart, included, out _);
        Check(slots.Count == 1 && slots[0].PlayerId == 6 && slots[0].ModLordJson != null,
            "capture included the future guest, dropped the AI's extras or changed original IDs");
        string makerFile = Path.Combine(fixture.Root, "Trail_Mission_1.trail");
        File.WriteAllText(makerFile, "saved restart info");
        TrailLordRequirements.Create(makerFile, slots).Write(makerFile);
        Check(TrailLordRequirements.Read(makerFile).Slots.Single().PlayerId == 6, "Maker sidecar IDs were remapped");
        var exported = TrailLordRequirementSource.ReadForExport(makerFile, restart, included);
        var remapped = CoopTrailSlotSelection.RemapForCoop(exported, active);
        string coopFile = Path.Combine(fixture.Root, "01.coopmission.json");
        File.WriteAllText(coopFile, "Coop JSON");
        TrailLordRequirements.Create(coopFile, remapped).Write(coopFile);
        Check(TrailLordRequirements.Read(coopFile).Slots.Single().PlayerId == 3, "Coop sidecar ID was not remapped");
        restart.aivs[5].lordName = "Missing AI";
        var embedded = TrailLordRequirementSource.CaptureForSave(makerFile, restart, included, out _).Single();
        Check(embedded.PlayerId == 6 && embedded.EmbeddedValuesOnly && embedded.ModLordJson == null,
            "a replacement AI inherited extra values from a different Lord in the same slot");
        restart.lordTypes[5] = -9999;
        var noAi = CoopTrailSlotSelection.GetAiPlayerIds(CoopTrailSlotSelection.GetActiveSlots(restart.lordTypes));
        Check(TrailLordRequirementSource.CaptureForSave(makerFile, restart, noAi, out _).Count == 0,
            "two-human mission attempted to resolve an AI source");
        Reject<InvalidDataException>(() => CoopTrailSlotSelection.GetActiveSlots(new[] { -1, -9999 }));
    }

    internal static void MakerRefreshesAndExportsEditedLord()
    {
        using var fixture = new Scope();
        var lord = fixture.InstallLord("Lord Bessy", "installed", unsupported: false);
        string root = lord.configs[0].path;
        string extra = Path.Combine(root, "aic.modlord.json");
        string fixesFile = Path.Combine(root, "Override", "Fixes", "preferences.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fixesFile));
        const string initialFixes = "{\"Value\":13}";
        const string editedFixes = "{\"Value\":29}";
        File.WriteAllText(fixesFile, initialFixes);
        var fixesPlugin = new Fixes.Boostrap.Plugin();
        Chainloader.PluginInfos["fixes"] = new FakePluginInfo { Instance = fixesPlugin };
        fixesPlugin.CustomLordPreferences["Lord Bessy"] = new Fixes.Config.CustomLordPreferencesEntry { Value = 13 };
        var fixes = new FixesLordPreferencesBridge();
        string trail = Path.Combine(fixture.Root, "Trail_Mission_2.trail");
        var restart = BessySelection(11, Castle("castle", 22, 1), Castle("embedded-only castle", 791, 3));
        File.WriteAllText(trail, "original restart data");
        var original = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out _).Single();
        Check(original.ModLordJson == File.ReadAllText(extra) && original.FixesJson == fixes.Normalize(initialFixes),
            "original installed values were not captured");
        TrailLordRequirements.Create(trail, new[] { original }).Write(trail);

        lord.configs[0].checksum = 55;
        lord.aivs[0] = Castle("castle", 44, 2, root);
        const string editedExtra = "{\"test.mod\":{\"value\":2}}";
        const string metadata = "{\"LocalizedDisplayName\":{\"de-DE\":\"Bessy\"}}";
        File.WriteAllText(extra, editedExtra);
        File.WriteAllText(fixesFile, editedFixes);
        File.WriteAllText(Path.Combine(root, "lordmeta.json"), metadata);
        TrailLordPackageRuntime.RefreshMakerSelection(restart);

        var current = restart.aivs[4];
        Check(current.lordConfig.checksum == 55 && current.aivs.Select(aiv => aiv.checksum).SequenceEqual(new ulong[] { 44, 791 }) &&
            current.aivs.Select(aiv => aiv.data[0]).SequenceEqual(new short[] { 2, 3 }),
            "the saved names did not load the edited catalog data while preserving the mixed castle selection");
        var edited = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out var warnings).Single();
        Check(warnings.Count == 0 && edited.ConfigChecksum == "55" && edited.AivChecksums.SequenceEqual(new[] { "44", "791" }) &&
            edited.ModLordJson == editedExtra && edited.Presentation?.MetadataJson == metadata &&
            edited.FixesJson == fixes.Normalize(editedFixes),
            "the refreshed selection did not capture its current extras and exact castle order");
        File.WriteAllText(trail, "edited restart data");
        TrailLordRequirements.Create(trail, new[] { edited }).Write(trail);

        using (File.Open(extra, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var recovered = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out warnings).Single();
            Check(warnings.Count > 0 && recovered.ModLordJson == editedExtra,
                "a temporary read error was treated as removal of previously saved extras");
        }

        File.Delete(extra);
        File.Delete(fixesFile);
        var exported = TrailLordRequirementSource.ReadForExport(trail, restart, null).Single();
        Check(exported.ModLordJson == editedExtra && exported.FixesJson == edited.FixesJson,
            "export pulled unsaved changes from the live installation");
        var removed = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out _).Single();
        Check(removed.ModLordJson == null && removed.FixesJson == null && removed.Presentation?.MetadataJson == metadata,
            "deleting an extra value from the selected package did not take effect on save");
        TrailLordRequirements.Create(trail, new[] { removed }).Write(trail);
        exported = TrailLordRequirementSource.ReadForExport(trail, restart, null).Single();
        Check(exported.ModLordJson == null && exported.FixesJson == null,
            "export revived an extra value removed by the author");
    }

    internal static void MakerPreservesEmbeddedLord()
    {
        using var fixture = new Scope();
        var lord = fixture.InstallLord("Lord Bessy", "installed", unsupported: true);
        string root = lord.configs[0].path;
        string trail = Path.Combine(fixture.Root, "Trail_Mission_2.trail");
        var restart = BessySelection(11, Castle("castle", 22, 1));
        File.WriteAllText(trail, "original restart data");
        var stored = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out _).Single();
        Check(stored.RequiresInstalledPackage && stored.ModLordJson != null, "the fixture did not capture its dependency and extras");
        TrailLordRequirements.Create(trail, new[] { stored }).Write(trail);

        var duplicate = fixture.InstallLord("Lord Bessy", "duplicate", unsupported: false);
        lord.configs[0].checksum = duplicate.configs[0].checksum = 55;
        lord.aivs[0] = Castle("castle", 44, 2, root);
        duplicate.aivs[0] = Castle("castle", 45, 3, duplicate.configs[0].path);
        TrailLordPackageRuntime.RefreshMakerSelection(restart);
        var current = restart.aivs[4];
        Check(current.lordConfig.checksum == 11 && current.aivs.Single().data[0] == 1,
            "ambiguous same-name installations replaced the embedded Lord");
        var ambiguous = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out _).Single();
        Check(ambiguous.RequiresInstalledPackage && ambiguous.PackageDigest == stored.PackageDigest,
            "saving an ambiguous embedded Lord picked a different package");

        CustomisationFileManager.Instance.CustomLords.Clear();
        current.aivs.Insert(0, Castle("another embedded castle", 791, 4));
        var saved = TrailLordRequirementSource.CaptureForSave(trail, restart, null, out _).Single();
        Check(saved.ModLordJson == stored.ModLordJson && saved.RequiresInstalledPackage && saved.PackageDigest == stored.PackageDigest &&
            saved.AivChecksums.SequenceEqual(new[] { "791", "22" }),
            "a missing installation or castle edit discarded previously saved extras");
        File.WriteAllText(trail, "new restart data");
        TrailLordRequirements.Create(trail, new[] { saved }).Write(trail);
        var exported = TrailLordRequirementSource.ReadForExport(trail, restart, null).Single();
        Check(exported.RequiresInstalledPackage && exported.PackageDigest == stored.PackageDigest &&
            !TrailLordPackageRuntime.TryResolve(exported, out _, out _, out _),
            "exporting without the installation lost the dependency or bypassed the start check");

        File.AppendAllText(trail, " prior Maker rewrote the mission only");
        Check(TrailLordRequirementSource.ReadForExport(trail, restart, null).Single().PackageDigest == stored.PackageDigest,
            "export rejected a legacy re-save of the same Lord or lost its known dependency");
        restart.aivs[4].lordName = "Replacement";
        Check(TrailLordRequirementSource.ReadForExport(trail, restart, null).Single().EmbeddedValuesOnly,
            "the legacy sidecar donated data to a different Lord");
        restart.aivs[4].lordName = "Lord Bessy";

        string sidecar = TrailLordRequirements.SidecarPath(trail);
        File.Delete(sidecar);
        var legacy = TrailLordRequirementSource.ReadForExport(trail, restart, null).Single();
        Check(legacy.EmbeddedValuesOnly && legacy.ConfigChecksum == "11" && legacy.ModLordJson == null &&
            !legacy.RequiresInstalledPackage && legacy.AivChecksums.SequenceEqual(new[] { "791", "22" }),
            "legacy export did not retain exactly the embedded data without an installation");

        File.WriteAllText(sidecar, "{");
        Reject<InvalidDataException>(() => TrailLordRequirementSource.ReadForExport(trail, restart, null));
    }

    private static HUD_IngameMenu.RestartSkirmishMapInfo BessySelection(ulong checksum,
        params CustomisationFileManager.CustomAIV[] castles)
    {
        var restart = new HUD_IngameMenu.RestartSkirmishMapInfo
        {
            lordTypes = new List<int> { -1, 1, 2, -9999, 3 },
            aivs = new FRONT_Multiplayer.MPAIVInfo[5],
        };
        restart.aivs[4] = new FRONT_Multiplayer.MPAIVInfo { lordName = "Lord Bessy", lordType = 33,
            lordConfig = new CustomisationFileManager.CustomLordConfig { lordType = -1, name = "aic", checksum = checksum },
            aivs = castles.ToList() };
        return restart;
    }

    private static CustomisationFileManager.CustomAIV Castle(string name, ulong checksum, short value, string path = null) =>
        new CustomisationFileManager.CustomAIV { lordType = -1, AIVName = name, checksum = checksum, data = new[] { value }, path = path };

    private sealed class Scope : IDisposable
    {
        private readonly string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ExtendedDataRuntimeRegression"));
        internal readonly string Root;
        private readonly CustomisationFileManager.CustomLordConfig[] configs = CustomisationFileManager.Instance.Configs;
        private readonly string[] names = ConfigSettings.extendedLordPaths;
        private readonly string media = ConfigSettings.MediaPath;
        private readonly Dictionary<string, FakePluginInfo> plugins = new(Chainloader.PluginInfos);
        private readonly Fixes.Boostrap.Plugin fixes = Fixes.Boostrap.Plugin.Instance;
        internal Scope()
        {
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Chainloader.PluginInfos.Clear();
            CustomisationFileManager.Instance.Configs = Array.Empty<CustomisationFileManager.CustomLordConfig>();
            CustomisationFileManager.Instance.CustomLords.Clear();
            CustomisationFileManager.Instance.AivsByType.Clear();
            ConfigSettings.extendedLordPaths = new[] { "Rat", "Wolf" };
            ConfigSettings.MediaPath = Path.Combine(Root, "media");
            GameAIManagerAPI.Instance.Supported.Clear();
            GameAssetModManager.Instance.Registered.Clear();
        }
        internal CustomisationFileManager.CustomLord InstallLord(string name, string folder, bool unsupported)
        {
            string path = Path.Combine(Root, folder);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "aic.lordjson"), "{\"lord\":{}}");
            File.WriteAllText(Path.Combine(path, "castle.aivjson"), "{}");
            File.WriteAllText(Path.Combine(path, "info.json"), "{\"GUID\":\"review-lord\"}");
            File.WriteAllText(Path.Combine(path, "aic.modlord.json"), "{\"test.mod\":{\"value\":1}}");
            if (unsupported) File.WriteAllText(Path.Combine(path, "init.lua"), "return true");
            var lord = new CustomisationFileManager.CustomLord { lordName = name };
            lord.configs.Add(new CustomisationFileManager.CustomLordConfig { lordType = -1, name = "aic", path = path, checksum = 11 });
            lord.aivs.Add(Castle("castle", 22, 1, path));
            CustomisationFileManager.Instance.CustomLords.Add(lord);
            return lord;
        }
        public void Dispose()
        {
            CustomisationFileManager.Instance.Configs = configs;
            CustomisationFileManager.Instance.CustomLords.Clear();
            CustomisationFileManager.Instance.AivsByType.Clear();
            ConfigSettings.extendedLordPaths = names;
            ConfigSettings.MediaPath = media;
            Chainloader.PluginInfos.Clear();
            foreach (var entry in plugins) Chainloader.PluginInfos.Add(entry.Key, entry.Value);
            Fixes.Boostrap.Plugin.Instance = fixes;
            GameAIManagerAPI.Instance.Supported.Clear();
            GameAssetModManager.Instance.Registered.Clear();
            Check(Path.GetDirectoryName(Path.GetFullPath(Root)) == parent, "Unsafe test cleanup path");
            Directory.Delete(Root, true);
        }
    }
}
