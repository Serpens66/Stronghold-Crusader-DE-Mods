using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DependencyBootstrapProbe
{
    internal sealed class LifecycleInvocation
    {
        private bool pending;
        internal string Begin()
        {
            if (pending) throw new InvalidOperationException("Overlapping or incomplete lifecycle invocation");
            pending = true;
            return Guid.NewGuid().ToString("N");
        }
        internal void RequirePending() { if (!pending) throw new InvalidOperationException("Lifecycle completion without invocation"); }
        internal void Complete() { RequirePending(); pending = false; }
    }
    internal sealed class LifecycleLocal
    {
        public string Path { get; set; }
        public string SHA256 { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
    // Static, synchronous observers copy data only; no Steam query, archive extraction or updater invocation.
    internal static class LifecycleCapture
    {
        private static string run;
        private static readonly LifecycleInvocation invocation = new LifecycleInvocation();
        private static readonly List<LifecyclePackage> packages = new List<LifecyclePackage>();
        private static readonly List<object> maps = new List<object>();
        private static readonly List<string> errors = new List<string>();
        private static string[] paths;
        private static void Error(Exception ex) { errors.Add(ex.Message); ProbeTrace.Mark("ERROR", "Lifecycle capture " + ex); }
        internal static void Paths(object value)
        {
            try
            {
                run = invocation.Begin();
                packages.Clear(); maps.Clear(); errors.Clear(); paths = null;
                paths = ((IEnumerable<string>)value).ToArray();
                if (paths.Length > 4096) throw new InvalidOperationException("Workshop path limit exceeded");
                ProbeTrace.Mark("LIFECYCLE_PATHS", "run=" + run + " count=" + paths.Length);
            }
            catch (Exception ex) { Error(ex); }
        }
        internal static void MapPath(string map, string folder)
        {
            try { maps.Add(new { Folder = folder, Map = map, Status = string.IsNullOrEmpty(map) ? "NoMapSelectedByExtender" : "SelectedByExtender" }); }
            catch (Exception ex) { Error(ex); }
        }
        internal static void Archive(bool loaded, object archive, string path)
        {
            try
            {
                var record = new LifecyclePackage { Path = path, Loaded = loaded };
                packages.Add(record);
                if (!loaded) { record.Manifest = "RejectedByExtender"; return; }
                object info = Property(archive, "Info");
                if (info != null)
                {
                    record.Guid = Property(info, "GUID") as string;
                    record.Version = Property(info, "Version") as string;
                    record.Manifest = Convert.ToString(Property(info, "Manifest"));
                }
                var entries = Property(archive, "Archive") as IEnumerable;
                if (entries == null) throw new InvalidDataException("Loaded archive has no entry inventory");
                int count = 0;
                foreach (object entry in entries)
                {
                    if (++count > 20000) throw new InvalidDataException("Archive entry limit exceeded");
                    string name = ((string)Property(entry, "Name")).Replace('\\', '/');
                    if (name.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase)) record.Payload.Add(name);
                }
                if (record.Payload.GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    throw new InvalidDataException("Duplicate or case-conflicting archive paths");
                ProbeTrace.Mark("LIFECYCLE_ARCHIVE", "run=" + run + " path=" + path);
            }
            catch (Exception ex) { if (packages.Count > 0) packages[packages.Count - 1].Error = ex.Message; Error(ex); }
        }
        private static object Property(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj, null);
        internal static void Unlinked(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked observation path: " + current);
        }
        private static IEnumerable<string> Files(string directory)
        {
            Unlinked(directory);
            if (!Directory.Exists(directory)) yield break;
            foreach (string file in Directory.GetFiles(directory)) { Unlinked(file); yield return file; }
            foreach (string child in Directory.GetDirectories(directory)) foreach (string file in Files(child)) yield return file;
        }
        internal static LifecycleLocal ReadJson(string file)
        {
            Unlinked(file);
            if (new FileInfo(file).Length > 1024 * 1024) throw new IOException("Metadata limit exceeded: " + file);
            var value = Shared.DependencyFreeJson.Parse(File.ReadAllText(file));
            if (!(value is Dictionary<string, object>)) throw new InvalidDataException("Metadata is not an object: " + file);
            return new LifecycleLocal { Path = file, SHA256 = ProbeIdentity.Hash(file), Metadata = (Dictionary<string, object>)value };
        }
        internal static void Staged()
        {
            try
            {
                invocation.RequirePending();
                if (paths == null) throw new InvalidOperationException("Missing lifecycle paths");
                ProbeTrace.Mark("LIFECYCLE_STAGED", "run=" + run + " before deployment decision");
                string game = BepInEx.Paths.GameRootPath;
                string se = Path.Combine(game, "_SE");
                string staging = Path.Combine(se, ".staging");
                var local = new List<LifecycleLocal>();
                Unlinked(se);
                if (Directory.Exists(se)) foreach (string file in Directory.GetFiles(se, "*.json"))
                { try { local.Add(ReadJson(file)); } catch (Exception ex) { Error(ex); } }
                foreach (var package in packages.Where(p => p.Loaded))
                {
                    string localPath = Path.Combine(se, Path.GetFileNameWithoutExtension(package.Path) + ".json");
                    var metadata = local.SingleOrDefault(p => string.Equals(p.Path, localPath, StringComparison.OrdinalIgnoreCase));
                    LifecycleOwnership.BindLocal(package, localPath, metadata?.Metadata);
                }
                var staged = new List<object>();
                long stagedBytes = 0;
                foreach (string file in Files(staging))
                {
                    stagedBytes += new FileInfo(file).Length;
                    if (staged.Count >= 4096 || stagedBytes > 128 * 1024 * 1024) throw new IOException("Staging observation limit exceeded");
                    staged.Add(new { Path = file.Substring(staging.Length + 1).Replace('\\', '/'), SHA256 = ProbeIdentity.Hash(file) });
                }
                string delete = Path.Combine(staging, "delete_list.txt");
                Unlinked(delete);
                if (File.Exists(delete) && new FileInfo(delete).Length > 1024 * 1024) throw new IOException("Deletion manifest limit exceeded");
                string[] deletions = File.Exists(delete) ? File.ReadAllLines(delete) : new string[0];
                var owners = new List<LifecycleOwner>();
                foreach (var winner in ShadowRuntime.Winners)
                {
                    Unlinked(winner.Path);
                    string full = Path.GetFullPath(winner.Path);
                    string relative = full.StartsWith(game.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ? full.Substring(game.TrimEnd('\\').Length + 1).Replace('\\', '/') : null;
                    owners.Add(LifecycleOwnership.Evaluate(winner.Guid, winner.Version, full, ProbeIdentity.Hash(full), relative, packages, errors.Count == 0));
                }
                bool complete = errors.Count == 0 && maps.Count == paths.Length;
                var report = new { Schema = 1, RunId = run, AtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    PatcherSHA256 = ProbeIdentity.Hash(typeof(LifecycleCapture).Assembly.Location),
                    PluginSHA256 = ProbeIdentity.Hash(Path.Combine(BepInEx.Paths.PluginPath, "DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll")),
                    CoreSHA256 = HookRuntime.CoreHash, ExtenderSHA256 = HookRuntime.ExtenderHash,
                    Complete = complete, SubscriptionCompleteness = "Unknown: observed Extender paths, not proof of all Steam subscriptions",
                    ExternalManagement = "Unverifiable: audited Extender method exposes no authoritative external package-manager ownership",
                    ExternalManagerLaunchSignal = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SCDEModManagerLaunchId")),
                    WorkshopPaths = paths, Maps = maps, Packages = packages, Owners = owners, LocalManifests = local,
                    StagedFiles = staged, DeleteManifest = deletions, Errors = errors, RemovalAuthorized = false };
                string target = Path.Combine(BepInEx.Paths.BepInExRootPath, "DependencyBootstrapProbe.lifecycle.json");
                string snapshot = Path.Combine(BepInEx.Paths.BepInExRootPath, "DependencyBootstrapProbe.lifecycle." + run + ".json");
                Unlinked(target);
                Unlinked(snapshot);
                string json = Shared.DependencyFreeJson.Serialize(report);
                File.WriteAllText(snapshot, json);
                File.WriteAllText(target, json);
                ProbeTrace.Mark("LIFECYCLE_REPORT", "run=" + run + " sha256=" + ProbeIdentity.Hash(target) + " complete=" + complete);
                invocation.Complete();
                if (!complete) ProbeTrace.Mark("ERROR", "Lifecycle report incomplete");
            }
            catch (Exception ex) { Error(ex); }
        }
    }
}
