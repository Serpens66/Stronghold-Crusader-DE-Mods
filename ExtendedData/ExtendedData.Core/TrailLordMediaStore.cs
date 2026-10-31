using APIShared.Content.Lords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    // One content-addressed store per package, shared by all missions. Only presentation files are copied.
    public static class TrailLordMediaStore
    {
        public const string DirectoryName = "LordMedia";
        private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp", ".gif", ".webp", ".ogg", ".wav", ".mp3", ".flac", ".webm", ".mp4" };

        public static LordPresentationData Capture(string name, string source)
        {
            string path = Path.Combine(source, "lordmeta.json");
            string json = File.Exists(path) ? File.ReadAllText(path, new UTF8Encoding(false, true)) : null;
            var result = new LordPresentationData { DisplayName = LordPresentationData.FallbackName(name), MetadataJson = json, SourceRoot = source };
            LordPresentationData.Validate(result); return result;
        }
        public static LordPresentationData SelectMedia(LordPresentationData source, bool include)
        {
            if (source == null) return null;
            var result = new LordPresentationData { DisplayName = source.DisplayName, MetadataJson = source.MetadataJson, SourceRoot = source.SourceRoot };
            if (!include || source.MetadataJson == null) return result;
            if (source.MediaId != null && source.MediaHashes != null && source.SourceRoot != null)
            { result.MediaId = source.MediaId; result.MediaHashes = source.MediaHashes; return result; }
            if (source.SourceRoot == null || !Directory.Exists(source.SourceRoot))
                throw new InvalidDataException("Lord media source is unavailable: " + source.DisplayName);
            string[] references = LordMetadataDocument.Parse(source.MetadataJson).MediaReferences().Select(NormalizeReference).ToArray();
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var foundReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in LordPackageFingerprint.Inventory(source.SourceRoot))
            {
                if (!Extensions.Contains(Path.GetExtension(file.RelativePath))) continue;
                string relative = file.RelativePath;
                if (relative.StartsWith("Override/", StringComparison.OrdinalIgnoreCase)) relative = relative.Substring(9);
                string logical = relative;
                if (logical.StartsWith("Locales/", StringComparison.OrdinalIgnoreCase))
                { int slash = logical.IndexOf('/', 8); if (slash >= 0) logical = logical.Substring(slash + 1); }
                var matches = references.Where(reference => string.Equals(logical, reference, StringComparison.OrdinalIgnoreCase) ||
                    !Path.HasExtension(reference) && string.Equals(Path.ChangeExtension(logical, null), reference, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 0) continue;
                foreach (string match in matches) foundReferences.Add(match);
                if (!LordPresentationData.SafeRelative(relative) || hashes.ContainsKey(relative)) throw new InvalidDataException("Ambiguous Lord media path: " + relative);
                hashes.Add(relative, Hash(File.ReadAllBytes(file.FullPath)));
            }
            string missing = references.FirstOrDefault(reference => !foundReferences.Contains(reference));
            if (missing != null) throw new InvalidDataException("Cannot embed unresolved media for Lord " + source.DisplayName + ": " + missing + ". Export without media or restore the source package.");
            result.MediaHashes = hashes;
            result.MediaId = InventoryHash(hashes);
            return result;
        }
        public static long ByteCount(IEnumerable<LordPresentationData> values)
        {
            long size = 0;
            foreach (var value in values.Where(x => x?.MediaId != null).GroupBy(x => x.MediaId).Select(g => g.First()))
                foreach (string path in value.MediaHashes.Keys) size = checked(size + new FileInfo(SourcePath(value, path)).Length);
            return size;
        }
        public static void Stage(IEnumerable<LordPresentationData> values, string destinationRoot)
        {
            foreach (var value in values.Where(x => x?.MediaId != null).GroupBy(x => x.MediaId).Select(g => g.First()))
            {
                LordPresentationData.Validate(value);
                if (value.MediaId != InventoryHash(value.MediaHashes)) throw new InvalidDataException("Lord media inventory hash mismatch.");
                foreach (var file in value.MediaHashes)
                {
                    ValidateMediaPath(file.Key);
                    string source = SourcePath(value, file.Key);
                    RejectLinks(source, value.SourceRoot);
                    byte[] bytes = File.ReadAllBytes(source);
                    if (Hash(bytes) != file.Value) throw new InvalidDataException("Lord media changed during export: " + file.Key);
                    string target = Path.Combine(destinationRoot, DirectoryName, value.MediaId, "Override", file.Key.Replace('/', Path.DirectorySeparatorChar));
                    RejectLinks(target, destinationRoot);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllBytes(target, bytes);
                }
            }
        }
        public static void Publish(IEnumerable<LordPresentationData> values, string destinationRoot)
        {
            string root = Path.GetFullPath(destinationRoot);
            string target = Path.Combine(root, DirectoryName);
            RejectLinks(target, root);
            // Only replace a demonstrably generated, unmodified content-addressed store.
            if (Directory.Exists(target)) foreach (string directory in Directory.GetDirectories(target))
            {
                var inventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in LordPackageFingerprint.Inventory(directory))
                {
                    if (!file.RelativePath.StartsWith("Override/", StringComparison.Ordinal) || !Extensions.Contains(Path.GetExtension(file.RelativePath)))
                        throw new InvalidDataException("Unowned file in Lord media output: " + file.RelativePath);
                    inventory.Add(file.RelativePath.Substring(9), Hash(File.ReadAllBytes(file.FullPath)));
                }
                if (InventoryHash(inventory) != Path.GetFileName(directory)) throw new InvalidDataException("Modified Lord media output must be preserved: " + directory);
            }
            if (Directory.Exists(target) && Directory.GetFiles(target).Length != 0) throw new InvalidDataException("Unowned files in Lord media output.");
            string stage = Path.Combine(root, ".lordmedia-stage-" + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(root, ".lordmedia-old-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(stage, DirectoryName));
            bool backedUp = false, committed = false;
            try
            {
                Stage(values, stage);
                if (Directory.Exists(target)) { Directory.Move(target, backup); backedUp = true; }
                try { Directory.Move(Path.Combine(stage, DirectoryName), target); committed = true; }
                catch { if (backedUp) Directory.Move(backup, target); throw; }
            }
            finally
            {
                if (Directory.Exists(stage)) { try { Directory.Delete(stage, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                // Successful publication is the commit point. A cleanup failure must retain the valid output.
                if (committed && backedUp) { try { Directory.Delete(backup, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        public static IReadOnlyList<string> BindAndVerify(IEnumerable<LordPresentationData> values, string root)
        {
            root = Path.GetFullPath(root);
            var paths = new List<string>();
            var verified = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var value in values.Where(x => x != null))
            {
                if (value.MediaId == null) continue;
                LordPresentationData.Validate(value);
                if (value.MediaId != InventoryHash(value.MediaHashes)) throw new InvalidDataException("Invalid Lord media inventory identity.");
                if (verified.TryGetValue(value.MediaId, out string existing)) { value.SourceRoot = existing; continue; }
                string mediaRoot = root;
                // Coop packages share one store between runtime missions and editable Maker sources.
                if (Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) == Core.CoopTrailPackagePublication.TrailMakerSourceDirectory &&
                    !Directory.Exists(Path.Combine(root, DirectoryName, value.MediaId)))
                    mediaRoot = Path.Combine(Path.GetDirectoryName(root), Core.CoopTrailPackagePublication.MissionsDirectory);
                string source = Path.Combine(mediaRoot, DirectoryName, value.MediaId);
                foreach (var file in value.MediaHashes)
                {
                    ValidateMediaPath(file.Key);
                    string path = Path.Combine(source, "Override", file.Key.Replace('/', Path.DirectorySeparatorChar));
                    // Reject directory links as well as file links before accepting untrusted Workshop contents.
                    RejectLinks(path, mediaRoot);
                    if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != file.Value) throw new InvalidDataException("Missing or modified Lord media: " + path);
                    paths.Add(path);
                }
                value.SourceRoot = source;
                verified.Add(value.MediaId, source);
            }
            return paths;
        }
        private static string SourcePath(LordPresentationData value, string relative)
        {
            if (value.SourceRoot == null) throw new InvalidDataException("No source for embedded Lord media: " + value.DisplayName);
            string overridden = Path.Combine(value.SourceRoot, "Override", relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(overridden) ? overridden : Path.Combine(value.SourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        }
        private static string NormalizeReference(string path)
        {
            string result = path.Replace('\\', '/');
            if (result.StartsWith("Override/", StringComparison.OrdinalIgnoreCase)) result = result.Substring(9);
            if (!LordPresentationData.SafeRelative(result)) throw new InvalidDataException("Unsafe Lord media reference: " + path);
            return result;
        }
        private static void ValidateMediaPath(string path)
        {
            if (!LordPresentationData.SafeRelative(path) || !Extensions.Contains(Path.GetExtension(path)))
                throw new InvalidDataException("Not a presentation media file: " + path);
        }
        private static void RejectLinks(string path, string root)
        {
            string boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(path);
            if (!full.Equals(boundary, StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lord media path leaves its package.");
            for (string part = full; part != null && part.Length >= boundary.Length; part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked Lord media path.");
        }
        private static string InventoryHash(IReadOnlyDictionary<string, string> inventory) => Hash(Encoding.UTF8.GetBytes(
            string.Join("\n", inventory.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "\n" + x.Value))));
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
