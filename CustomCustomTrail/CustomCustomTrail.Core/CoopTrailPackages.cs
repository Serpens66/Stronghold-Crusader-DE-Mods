using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CustomCustomTrail.Core
{
    public static class CoopTrailPackageManifestJson
    {
        public const int CurrentSchemaVersion = 1;

        public static CoopTrailPackageManifest Read(string path)
        {
            object parsed = PortableJson.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (!(parsed is Dictionary<string, object> root))
                throw new InvalidDataException("Coop Trail manifest root must be an object.");
            return new CoopTrailPackageManifest
            {
                SchemaVersion = RequiredInt(root, "schemaVersion"),
                PackageId = RequiredString(root, "packageId"),
                DisplayName = RequiredString(root, "displayName"),
                MissionCount = RequiredInt(root, "missionCount"),
                ContentFingerprint = RequiredString(root, "contentFingerprint"),
            };
        }

        public static void Validate(CoopTrailPackageManifest manifest)
        {
            if (manifest == null)
                throw new InvalidDataException("Coop Trail manifest is empty.");
            if (manifest.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported Coop Trail manifest schemaVersion " + manifest.SchemaVersion + ".");
            if (!Guid.TryParse(manifest.PackageId, out _))
                throw new InvalidDataException("packageId must be a GUID.");
            if (string.IsNullOrWhiteSpace(manifest.DisplayName))
                throw new InvalidDataException("displayName is required.");
            if (manifest.MissionCount < 1 || manifest.MissionCount > 40)
                throw new InvalidDataException("missionCount must be between 1 and 40.");
            if (!IsSha256(manifest.ContentFingerprint))
                throw new InvalidDataException("contentFingerprint must be a SHA-256 value.");
        }

        public static string Serialize(CoopTrailPackageManifest manifest)
        {
            Validate(manifest);
            return PortableJson.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = manifest.SchemaVersion,
                ["packageId"] = manifest.PackageId,
                ["displayName"] = manifest.DisplayName,
                ["missionCount"] = manifest.MissionCount,
                ["contentFingerprint"] = manifest.ContentFingerprint,
            });
        }

        public static void WriteAtomic(string path, CoopTrailPackageManifest manifest)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, Serialize(manifest), new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static bool IsSha256(string value) =>
            value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F'));

        private static int RequiredInt(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value) || !(value is int result))
                throw new InvalidDataException(name + " must be an integer.");
            return result;
        }

        private static string RequiredString(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value) || !(value is string result))
                throw new InvalidDataException(name + " must be a string.");
            return result;
        }
    }

    public static class CoopTrailPackageFingerprint
    {
        public static string Compute(string packageRoot, IEnumerable<string> files)
        {
            string root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string[] ordered = (files ?? Enumerable.Empty<string>())
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => GetRelativePath(root, path), StringComparer.Ordinal)
                .ToArray();
            using (SHA256 hash = SHA256.Create())
            {
                foreach (string file in ordered)
                {
                    if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Fingerprint file escapes the package root: " + file);
                    if (!File.Exists(file))
                        throw new FileNotFoundException("Fingerprint file was not found.", file);
                    byte[] name = Encoding.UTF8.GetBytes(GetRelativePath(root, file));
                    hash.TransformBlock(name, 0, name.Length, null, 0);
                    hash.TransformBlock(new byte[] { 0 }, 0, 1, null, 0);
                    byte[] content = File.ReadAllBytes(file);
                    hash.TransformBlock(content, 0, content.Length, null, 0);
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return string.Concat(hash.Hash.Select(value => value.ToString("x2")));
            }
        }

        private static string GetRelativePath(string root, string path) =>
            path.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public sealed class CoopTrailPackageCatalog
    {
        private readonly Dictionary<string, CoopTrailPackage> packages =
            new Dictionary<string, CoopTrailPackage>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, CoopTrailPackage> Packages => packages;

        public void Scan(string customTrailsRoot, Action<string> info, Action<string> error)
        {
            packages.Clear();
            if (!Directory.Exists(customTrailsRoot))
                return;

            var candidates = new List<CoopTrailPackage>();
            foreach (string directory in Directory.GetDirectories(customTrailsRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string manifestPath = Path.Combine(directory, "cooptrail.json");
                if (!File.Exists(manifestPath))
                    continue;
                try
                {
                    candidates.Add(Load(directory));
                }
                catch (Exception exception)
                {
                    error?.Invoke("Ignored Coop Trail package [" + Path.GetFileName(directory) + "]: " + exception.Message);
                }
            }

            foreach (IGrouping<string, CoopTrailPackage> group in candidates.GroupBy(item => item.Manifest.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                CoopTrailPackage[] matches = group.ToArray();
                if (matches.Length != 1)
                {
                    error?.Invoke("Ignored duplicate Coop Trail packageId [" + group.Key + "] in: " +
                        string.Join(", ", matches.Select(item => Path.GetFileName(item.RootPath))));
                    continue;
                }
                CoopTrailPackage package = matches[0];
                packages[package.Manifest.PackageId] = package;
                info?.Invoke("Found Coop Trail package [" + package.Manifest.DisplayName + "] with " + package.Manifest.MissionCount + " mission(s).");
            }
        }

        public static CoopTrailPackage Load(string packageRoot)
        {
            string root = Path.GetFullPath(packageRoot);
            string manifestPath = Path.Combine(root, "cooptrail.json");
            CoopTrailPackageManifest manifest = CoopTrailPackageManifestJson.Read(manifestPath);
            CoopTrailPackageManifestJson.Validate(manifest);
            string missionsPath = Path.Combine(root, "CoopMissions");
            var loader = new MissionLoader();
            var missions = new List<LoadedMission>(manifest.MissionCount);
            var fingerprintFiles = new List<string>();
            for (int ordinal = 1; ordinal <= manifest.MissionCount; ordinal++)
            {
                string jsonPath = Path.Combine(missionsPath, ordinal.ToString("00") + ".coopmission.json");
                int trail = ((ordinal - 1) / 10) + 1;
                int mission = ((ordinal - 1) % 10) + 1;
                LoadedMission loaded = loader.Load(jsonPath, trail, mission);
                missions.Add(loaded);
                fingerprintFiles.Add(loaded.JsonPath);
                fingerprintFiles.AddRange(loaded.BundledFiles);
            }
            string fingerprint = CoopTrailPackageFingerprint.Compute(root, fingerprintFiles);
            if (!string.Equals(fingerprint, manifest.ContentFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("contentFingerprint does not match the package contents.");
            return new CoopTrailPackage
            {
                RootPath = root,
                MissionsPath = missionsPath,
                ManifestPath = manifestPath,
                Manifest = manifest,
                Missions = missions,
            };
        }
    }
}
