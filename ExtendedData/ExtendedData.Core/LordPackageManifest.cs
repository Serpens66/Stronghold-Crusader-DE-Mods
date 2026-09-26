using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    public sealed class LordPackageFileState
    {
        public string Digest { get; set; }
        public bool HasUnsupportedGameplayFiles { get; set; }
        public IReadOnlyList<string> GameplayPaths { get; set; }
        public IReadOnlyList<string> UnsupportedPaths { get; set; }
    }

    public static class LordPackageFingerprint
    {
        private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp", ".gif", ".webp",
            ".wav", ".ogg", ".mp3", ".flac", ".webm", ".mp4"
        };

        public static LordPackageFileState Capture(string root, string configName)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ||
                string.IsNullOrWhiteSpace(configName))
                throw new InvalidDataException("The selected Lord directory or configuration is unavailable.");
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var paths = new List<string>();
            Collect(normalizedRoot, normalizedRoot, paths);
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            if (paths.Select(path => path.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() != paths.Count)
                throw new InvalidDataException("The Lord package contains case-colliding file paths.");
            var gameplay = new List<string>();
            var unsupported = new List<string>();
            var digestSource = new StringBuilder();
            foreach (string path in paths)
            {
                string relative = path.Substring(normalizedRoot.Length + 1).Replace('\\', '/');
                string extension = Path.GetExtension(relative);
                if (MediaExtensions.Contains(extension) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                    IsLocalControl(relative))
                    continue;
                gameplay.Add(relative);
                if (!IsSupported(relative, configName))
                    unsupported.Add(relative);
                byte[] bytes = File.ReadAllBytes(path);
                digestSource.Append(relative.ToUpperInvariant()).Append('\n')
                    .Append(bytes.Length).Append('\n').Append(Hash(bytes)).Append('\n');
            }
            return new LordPackageFileState
            {
                Digest = Hash(Encoding.UTF8.GetBytes(digestSource.ToString())),
                HasUnsupportedGameplayFiles = unsupported.Count != 0,
                GameplayPaths = gameplay,
                UnsupportedPaths = unsupported,
            };
        }

        private static void Collect(string root, string directory, List<string> files)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A Lord package directory is a link: " + directory);
            foreach (string path in Directory.GetFiles(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A Lord package file is a link: " + path);
                files.Add(path);
            }
            foreach (string child in Directory.GetDirectories(directory))
                Collect(root, child, files);
        }

        private static bool IsLocalControl(string relative) =>
            relative.IndexOf('/') < 0 &&
            (relative.EndsWith(".data", StringComparison.OrdinalIgnoreCase) ||
             relative.EndsWith(".ldata", StringComparison.OrdinalIgnoreCase));

        private static bool IsSupported(string relative, string configName)
        {
            if (relative.IndexOf('/') < 0)
            {
                string extension = Path.GetExtension(relative);
                if (extension.Equals(".lordjson", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".aivjson", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals("info.json", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals("lordmeta.json", StringComparison.OrdinalIgnoreCase) ||
                    relative.Equals(configName + ".modlord.json", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return relative.Equals("Override/Fixes/preferences.json", StringComparison.OrdinalIgnoreCase) ||
                (relative.StartsWith("Locales/", StringComparison.OrdinalIgnoreCase) &&
                 relative.EndsWith("/crusader.txt", StringComparison.OrdinalIgnoreCase));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }

    public sealed class LordPackageSlot
    {
        public int PlayerId { get; set; }
        public int LordType { get; set; }
        public string LordName { get; set; }
        public string ConfigName { get; set; }
        public string ConfigChecksum { get; set; }
        public string FileDigest { get; set; }
        public bool NeedsLocalFiles { get; set; }
        public bool NeedsSnapshot { get; set; }
        public string FixesDigest { get; set; }
    }

    public sealed class LordPackageManifest
    {
        public const int MaximumBytes = 128 * 1024;
        public string SessionId { get; private set; }
        public string Digest { get; private set; }
        public bool UseLocalValues { get; private set; }
        public IReadOnlyList<LordPackageSlot> Slots { get; private set; }
        public string WireJson { get; private set; }

        public static LordPackageManifest Create(string sessionId, bool useLocalValues,
            IEnumerable<LordPackageSlot> slots)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new InvalidDataException("A Lord package session ID is required.");
            LordPackageSlot[] ordered = (slots ?? Enumerable.Empty<LordPackageSlot>())
                .OrderBy(slot => slot.PlayerId).ToArray();
            Validate(ordered);
            string payload = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["version"] = 1,
                ["session"] = sessionId,
                ["local"] = useLocalValues,
                ["slots"] = ordered.Select(slot => (object)new Dictionary<string, object>
                {
                    ["playerId"] = slot.PlayerId,
                    ["lordType"] = slot.LordType,
                    ["lordName"] = slot.LordName,
                    ["configName"] = slot.ConfigName,
                    ["configChecksum"] = slot.ConfigChecksum,
                    ["fileDigest"] = slot.FileDigest,
                    ["needsLocalFiles"] = slot.NeedsLocalFiles,
                    ["needsSnapshot"] = slot.NeedsSnapshot,
                    ["fixesDigest"] = slot.FixesDigest,
                }).ToList(),
            });
            string digest = Hash(Encoding.UTF8.GetBytes(payload));
            string wire = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["payload"] = payload,
                ["digest"] = digest,
            });
            if (Encoding.UTF8.GetByteCount(wire) > MaximumBytes)
                throw new InvalidDataException("The selected Lord package manifest exceeds 128 KiB.");
            return new LordPackageManifest
            {
                SessionId = sessionId,
                Digest = digest,
                UseLocalValues = useLocalValues,
                Slots = ordered,
                WireJson = wire,
            };
        }

        public static LordPackageManifest Parse(string wire)
        {
            if (string.IsNullOrEmpty(wire) || Encoding.UTF8.GetByteCount(wire) > MaximumBytes)
                throw new InvalidDataException("The Lord package manifest is missing or too large.");
            var envelope = Shared.DependencyFreeJson.Parse(wire) as Dictionary<string, object>;
            string payload = envelope != null && envelope.TryGetValue("payload", out object p) ? p as string : null;
            string digest = envelope != null && envelope.TryGetValue("digest", out object d) ? d as string : null;
            if (payload == null || digest == null ||
                !string.Equals(Hash(Encoding.UTF8.GetBytes(payload)), digest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The Lord package manifest checksum is invalid.");
            var data = Shared.DependencyFreeJson.Parse(payload) as Dictionary<string, object>;
            if (data == null || Convert.ToInt32(data["version"]) != 1 || !(data["local"] is bool local) ||
                !(data["slots"] is List<object> items))
                throw new InvalidDataException("The Lord package manifest format is invalid.");
            LordPackageSlot[] slots = items.Select(item =>
            {
                var row = item as Dictionary<string, object> ?? throw new InvalidDataException("Invalid Lord package slot.");
                return new LordPackageSlot
                {
                    PlayerId = Convert.ToInt32(row["playerId"]),
                    LordType = Convert.ToInt32(row["lordType"]),
                    LordName = row["lordName"] as string,
                    ConfigName = row["configName"] as string,
                    ConfigChecksum = row["configChecksum"] as string,
                    FileDigest = row["fileDigest"] as string,
                    NeedsLocalFiles = row["needsLocalFiles"] is bool required && required,
                    NeedsSnapshot = row["needsSnapshot"] is bool snapshotRequired && snapshotRequired,
                    FixesDigest = row["fixesDigest"] as string,
                };
            }).ToArray();
            Validate(slots);
            return Create(data["session"] as string, local, slots);
        }

        private static void Validate(LordPackageSlot[] slots)
        {
            if (slots.Length > 8 || slots.Select(slot => slot.PlayerId).Distinct().Count() != slots.Length ||
                slots.Any(slot => slot.PlayerId < 1 || slot.PlayerId > 8 ||
                    string.IsNullOrWhiteSpace(slot.LordName) || string.IsNullOrWhiteSpace(slot.ConfigName) ||
                    string.IsNullOrWhiteSpace(slot.FileDigest)))
                throw new InvalidDataException("The selected Lord package slots are invalid.");
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
