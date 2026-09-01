using System;
using System.Collections.Generic;
using System.IO;

namespace CustomCustomTrail.Core
{
    public static class CoopWorkshopPackageStaging
    {
        public static CoopTrailPackage Stage(
            CoopTrailPackage sourcePackage,
            string destination,
            string metadataFileName,
            bool includeModSettings,
            out int copiedModSettings)
        {
            if (sourcePackage == null)
                throw new ArgumentNullException(nameof(sourcePackage));
            CoopTrailPackageManifestJson.Validate(sourcePackage.Manifest);

            string source = Path.GetFullPath(sourcePackage.RootPath);
            string target = Path.GetFullPath(destination ?? throw new ArgumentNullException(nameof(destination)));
            EnsureSeparateRoots(source, target);
            EnsureSafeDirectory(source, "Coop package source");
            Directory.CreateDirectory(target);
            EnsureSafeDirectory(target, "Workshop staging destination");
            if (Directory.EnumerateFileSystemEntries(target).GetEnumerator().MoveNext())
                throw new InvalidDataException("Workshop staging destination is not empty.");

            copiedModSettings = 0;
            CopyDirectory(
                source,
                target,
                source,
                metadataFileName,
                includeModSettings,
                ref copiedModSettings);

            var fingerprintFiles = new List<string>();
            var loader = new MissionLoader();
            string missionsRoot = Path.Combine(target, "CoopMissions");
            for (int ordinal = 1; ordinal <= sourcePackage.Manifest.MissionCount; ordinal++)
            {
                string jsonPath = Path.Combine(missionsRoot, ordinal.ToString("00") + ".coopmission.json");
                LoadedMission loaded = loader.Load(jsonPath, ((ordinal - 1) / 10) + 1, ((ordinal - 1) % 10) + 1);
                fingerprintFiles.Add(loaded.JsonPath);
                fingerprintFiles.AddRange(loaded.BundledFiles);
                if (loaded.ModSettingsPath != null)
                    fingerprintFiles.Add(loaded.ModSettingsPath);
            }

            var manifest = new CoopTrailPackageManifest
            {
                SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
                PackageId = sourcePackage.Manifest.PackageId,
                DisplayName = sourcePackage.Manifest.DisplayName,
                MissionCount = sourcePackage.Manifest.MissionCount,
                ContentFingerprint = CoopTrailPackageFingerprint.Compute(target, fingerprintFiles),
            };
            CoopTrailPackageManifestJson.WriteAtomic(Path.Combine(target, "cooptrail.json"), manifest);
            return CoopTrailPackageCatalog.Load(target);
        }

        private static void CopyDirectory(
            string source,
            string destination,
            string sourceRoot,
            string metadataFileName,
            bool includeModSettings,
            ref int copiedModSettings)
        {
            foreach (string file in Directory.GetFiles(source))
            {
                EnsureNotReparsePoint(file, "Coop package file");
                bool sourceRootFile = string.Equals(source, sourceRoot, StringComparison.OrdinalIgnoreCase);
                if (sourceRootFile && string.Equals(Path.GetFileName(file), metadataFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool modSettings = string.Equals(Path.GetExtension(file), ".modjson", StringComparison.OrdinalIgnoreCase);
                if (modSettings && !includeModSettings)
                    continue;
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
                if (modSettings)
                    copiedModSettings++;
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                EnsureSafeDirectory(directory, "Coop package directory");
                string childDestination = Path.Combine(destination, Path.GetFileName(directory));
                Directory.CreateDirectory(childDestination);
                CopyDirectory(
                    directory,
                    childDestination,
                    sourceRoot,
                    metadataFileName,
                    includeModSettings,
                    ref copiedModSettings);
            }
        }

        private static void EnsureSeparateRoots(string source, string destination)
        {
            string sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Coop package source and Workshop staging destination must be separate.");
            }
        }

        private static void EnsureSafeDirectory(string path, string label)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException(label + " was not found: " + path);
            EnsureNotReparsePoint(path, label);
        }

        private static void EnsureNotReparsePoint(string path, string label)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(label + " must not be a reparse point: " + path);
        }
    }
}
