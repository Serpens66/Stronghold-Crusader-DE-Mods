using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DependencyBootstrapProbe
{
    // File consistency and selected-provider binding, not publisher authentication.
    internal sealed class PairContract
    {
        internal const string Guid = "DependencyBootstrapProbe_Serp";
        internal const string Runtime = "BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll";
        internal const string Bootstrap = "BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll";
        internal const string Manifest = "BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json";
        internal string Root, Version, RuntimeHash, BootstrapHash, ManifestHash;

        internal static void Unlinked(string path)
        {
            for (var p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            {
                // Missing/inaccessible paths fail closed; every package parent must already exist.
                if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Reparse path rejected");
            }
        }
        private static string Digest(object value)
        {
            var text = value as string;
            if (text == null || text.Length != 64 || text.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                throw new InvalidOperationException("Invalid pair digest");
            return text.ToUpperInvariant();
        }
        internal static PairContract Read(string root, string loadedPath, string loadedHash)
        {
            root = Path.GetFullPath(root);
            foreach (var relative in new[] { Runtime, Bootstrap, Manifest }) Unlinked(Path.Combine(root, relative));
            if (!string.Equals(Path.GetFullPath(loadedPath), Path.Combine(root, Bootstrap).Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bootstrap loaded from unexpected path");
            string manifestPath = Path.Combine(root, Manifest);
            var manifestHash = ProbeIdentity.Hash(manifestPath);
            var json = Shared.DependencyFreeJson.Parse(File.ReadAllText(manifestPath)) as Dictionary<string, object>;
            var fields = new[] { "Schema", "Guid", "Version", "RuntimeSHA256", "BootstrapSHA256" };
            if (json == null || json.Count != fields.Length || fields.Any(f => !json.ContainsKey(f)) ||
                !(json["Schema"] is long || json["Schema"] is int) || Convert.ToInt64(json["Schema"]) != 1 || (json["Guid"] as string) != Guid)
                throw new InvalidOperationException("Invalid pair schema/identity");
            Version version;
            if (!(json["Version"] is string) || !System.Version.TryParse((string)json["Version"], out version))
                throw new InvalidOperationException("Invalid pair version");
            var pair = new PairContract { Root = root, Version = (string)json["Version"], ManifestHash = manifestHash,
                RuntimeHash = Digest(json["RuntimeSHA256"]), BootstrapHash = Digest(json["BootstrapSHA256"]) };
            if (pair.BootstrapHash != loadedHash || ProbeIdentity.Hash(Path.Combine(root, Bootstrap)) != pair.BootstrapHash ||
                ProbeIdentity.Hash(Path.Combine(root, Runtime)) != pair.RuntimeHash || ProbeIdentity.Hash(manifestPath) != manifestHash)
                throw new InvalidOperationException("Missing, changed or mixed pair");
            foreach (var file in Files(Path.Combine(root, "BepInEx/patchers")))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(loadedPath), StringComparison.OrdinalIgnoreCase)) continue;
                if (ProbeIdentity.Hash(file) == pair.BootstrapHash) throw new InvalidOperationException("Duplicate bootstrap bytes");
                if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (AssemblyName.GetAssemblyName(file).Name == "DependencyBootstrapProbe.Patcher")
                        throw new InvalidOperationException("Competing bootstrap identity");
                }
                catch (BadImageFormatException) { /* Native libraries are not competing managed bootstraps. */ }
            }
            return pair;
        }
        private static IEnumerable<string> Files(string directory)
        {
            Unlinked(directory);
            foreach (var path in Directory.GetFileSystemEntries(directory))
            {
                Unlinked(path);
                if (Directory.Exists(path)) { foreach (var child in Files(path)) yield return child; }
                else yield return path;
            }
        }
        internal void Bind(IEnumerable<ShadowPlugin> winners, string loadedPath, string loadedHash)
        {
            var current = Read(Root, loadedPath, loadedHash);
            if (current.ManifestHash != ManifestHash) throw new InvalidOperationException("Pair changed after preparation");
            var own = winners.Where(p => string.Equals(p.Guid, Guid, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (own.Length != 1 || own[0].Guid != Guid || own[0].Version != Version ||
                !string.Equals(Path.GetFullPath(own[0].Path), Path.GetFullPath(Path.Combine(Root, Runtime)), StringComparison.OrdinalIgnoreCase) ||
                ProbeIdentity.Hash(own[0].Path) != RuntimeHash)
                throw new InvalidOperationException("Selected provider differs from complete pair");
        }
    }
}
