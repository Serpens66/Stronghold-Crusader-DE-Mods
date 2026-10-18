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
        var sources = new Dictionary<int, TrailLordPackageRuntime.Candidate>();
        var slots = TrailLordPackageRuntime.Capture(restart, sources, included);
        Check(slots.Count == 1 && slots[0].PlayerId == 6 && sources.Keys.SequenceEqual(new[] { 6 }),
            "capture looked up the future guest or changed original IDs");
        string makerFile = Path.Combine(fixture.Root, "Trail_Mission_1.trail");
        File.WriteAllText(makerFile, "saved restart info");
        TrailLordRequirements.Create(makerFile, slots).Write(makerFile);
        Check(TrailLordRequirements.Read(makerFile).Slots.Single().PlayerId == 6, "Maker sidecar IDs were remapped");
        var remapped = CoopTrailSlotSelection.RemapForCoop(slots, active);
        string coopFile = Path.Combine(fixture.Root, "01.coopmission.json");
        File.WriteAllText(coopFile, "Coop JSON");
        TrailLordRequirements.Create(coopFile, remapped).Write(coopFile);
        Check(TrailLordRequirements.Read(coopFile).Slots.Single().PlayerId == 3, "Coop sidecar ID was not remapped");
        Reject<InvalidDataException>(() => TrailLordPackageRuntime.Capture(restart));
        restart.aivs[5].lordName = "Missing AI";
        Reject<InvalidDataException>(() => TrailLordPackageRuntime.Capture(restart, includedPlayerIds: included));
        restart.lordTypes[5] = -9999;
        var noAi = CoopTrailSlotSelection.GetAiPlayerIds(CoopTrailSlotSelection.GetActiveSlots(restart.lordTypes));
        Check(TrailLordPackageRuntime.Capture(restart, includedPlayerIds: noAi).Count == 0,
            "two-human mission attempted to resolve an AI source");
        Reject<InvalidDataException>(() => CoopTrailSlotSelection.GetActiveSlots(new[] { -1, -9999 }));
    }

    private sealed class Scope : IDisposable
    {
        private readonly string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ExtendedDataRuntimeRegression"));
        internal readonly string Root;
        private readonly CustomisationFileManager.CustomLordConfig[] configs = CustomisationFileManager.Instance.Configs;
        private readonly string[] names = ConfigSettings.extendedLordPaths;
        private readonly string media = ConfigSettings.MediaPath;
        private readonly Dictionary<string, FakePluginInfo> plugins = new(Chainloader.PluginInfos);
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
            lord.aivs.Add(new CustomisationFileManager.CustomAIV { checksum = 22 });
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
            GameAIManagerAPI.Instance.Supported.Clear();
            GameAssetModManager.Instance.Registered.Clear();
            Check(Path.GetDirectoryName(Path.GetFullPath(Root)) == parent, "Unsafe test cleanup path");
            Directory.Delete(Root, true);
        }
    }
}
