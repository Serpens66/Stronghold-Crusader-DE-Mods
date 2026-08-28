using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordExtendedPackageFeature : IDisposable
    {
        private delegate void UploadWorkshopMapDelegate(
            Platform_Workshop self,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction);

        private sealed class CacheEntry
        {
            internal PackageSignature Signature;
            internal CustomLordPackageDetails Details;
        }

        private struct PackageSignature : IEquatable<PackageSignature>
        {
            internal bool InfoExists;
            internal long InfoLength;
            internal long InfoWriteTicks;
            internal bool MetadataExists;
            internal long MetadataLength;
            internal long MetadataWriteTicks;

            public bool Equals(PackageSignature other) =>
                InfoExists == other.InfoExists &&
                InfoLength == other.InfoLength &&
                InfoWriteTicks == other.InfoWriteTicks &&
                MetadataExists == other.MetadataExists &&
                MetadataLength == other.MetadataLength &&
                MetadataWriteTicks == other.MetadataWriteTicks;
        }

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<string, CacheEntry> metadataCache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Hook uploadHook;
        private readonly UploadWorkshopMapDelegate uploadOriginal;
        private bool disposed;

        internal CustomLordExtendedPackageFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            MethodInfo method = typeof(Platform_Workshop).GetMethod(
                nameof(Platform_Workshop.UploadWorkshopMap),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string[]), typeof(bool),
                    typeof(string), typeof(Action), typeof(Action)
                },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(Platform_Workshop).FullName, nameof(Platform_Workshop.UploadWorkshopMap));

            Hook installed = null;
            try
            {
                installed = new Hook(method, (UploadWorkshopMapDelegate)UploadWorkshopMapHook);
                uploadOriginal = installed.GenerateTrampoline<UploadWorkshopMapDelegate>();
                uploadHook = installed;
            }
            catch
            {
                installed?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL extended Custom Lord package hook installed.");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableCustomLordExtendedPackages;

        internal void ApplySetting()
        {
            if (!IsEnabled)
                metadataCache.Clear();
        }

        internal CustomLordPackageDetails GetDetails(CustomisationFileManager.CustomLord lord)
        {
            // This guard intentionally precedes every path and FileInfo access.
            if (!IsEnabled || lord == null || string.IsNullOrWhiteSpace(lord.customPath))
                return CustomLordPackageDetails.Empty;

            string root;
            PackageSignature signature;
            try
            {
                root = Path.GetFullPath(lord.customPath);
                signature = GetSignature(root);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Could not inspect extended Custom Lord metadata for [{lord.lordName}]: {exception.Message}");
                return CustomLordPackageDetails.Empty;
            }

            if (metadataCache.TryGetValue(root, out CacheEntry cached) && cached.Signature.Equals(signature))
                return cached.Details;

            CustomLordPackageDetails details = CustomLordPackageDetails.Empty;
            if (!CustomLordExtendedPackagePolicy.TryLoadDetails(
                    root,
                    SerpLocalization.GetActiveLocale(),
                    out CustomLordPackageDetails loaded,
                    out string error))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"Extended Custom Lord details were not loaded for [{lord.lordName}]: {error}");
            }
            else
            {
                details = loaded;
            }

            metadataCache[root] = new CacheEntry { Signature = signature, Details = details };
            return details;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            metadataCache.Clear();
            uploadHook?.Undo();
            uploadHook?.Dispose();
        }

        private void UploadWorkshopMapHook(
            Platform_Workshop self,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction)
        {
            // Do not enumerate lords or touch package files unless the host explicitly enables the feature.
            if (IsEnabled && HasExactCustomLordTag(tags))
            {
                try
                {
                    TryExtendCustomLordStaging(nameMap, mapTitle);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Extended Custom Lord data was omitted from Workshop upload; Vanilla upload continues: {exception}");
                }
            }

            uploadOriginal(
                self,
                nameMap,
                mapTitle,
                description,
                tags,
                publicMap,
                previewImage,
                successAction,
                failAction);
        }

        private void TryExtendCustomLordStaging(string uploadContentRoot, string mapTitle)
        {
            if (string.IsNullOrWhiteSpace(mapTitle) ||
                !string.Equals(mapTitle, Path.GetFileName(mapTitle), StringComparison.Ordinal))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Extended Custom Lord data was omitted because the upload title is not a safe folder name.");
                return;
            }

            List<CustomisationFileManager.CustomLord> localLords =
                CustomisationFileManager.Instance.GetCustomLords(includeWorkshop: false);
            CustomisationFileManager.CustomLord sourceLord = null;
            int matches = 0;
            foreach (CustomisationFileManager.CustomLord lord in localLords)
            {
                if (lord != null && !lord.workshop &&
                    string.Equals(lord.lordName, mapTitle, StringComparison.OrdinalIgnoreCase))
                {
                    sourceLord = lord;
                    matches++;
                }
            }

            if (matches != 1 || sourceLord == null || string.IsNullOrWhiteSpace(sourceLord.customPath))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extended Custom Lord data was omitted because upload source [{mapTitle}] was not uniquely resolved.");
                return;
            }

            string stagingRoot = Path.GetFullPath(uploadContentRoot ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = Path.GetFullPath(Path.Combine(stagingRoot, mapTitle));
            string stagingPrefix = stagingRoot + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Extended Custom Lord data was omitted because the staging path escaped Vanilla's upload directory.");
                return;
            }

            if (!CustomLordExtendedPackagePolicy.TryStageUploadFiles(
                    sourceLord.customPath,
                    destination,
                    out int copiedFileCount,
                    out string error))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Extended Custom Lord data was omitted from [{mapTitle}]; Vanilla upload continues: {error}");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Added {copiedFileCount} extended Custom Lord package files to Workshop staging for [{mapTitle}].");
        }

        private static bool HasExactCustomLordTag(string[] tags)
        {
            if (tags == null)
                return false;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, "Custom Lord", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static PackageSignature GetSignature(string root)
        {
            FileInfo info = new FileInfo(Path.Combine(root, CustomLordExtendedPackagePolicy.InfoFileName));
            FileInfo metadata = new FileInfo(Path.Combine(root, CustomLordExtendedPackagePolicy.MetadataFileName));
            return new PackageSignature
            {
                InfoExists = info.Exists,
                InfoLength = info.Exists ? info.Length : 0L,
                InfoWriteTicks = info.Exists ? info.LastWriteTimeUtc.Ticks : 0L,
                MetadataExists = metadata.Exists,
                MetadataLength = metadata.Exists ? metadata.Length : 0L,
                MetadataWriteTicks = metadata.Exists ? metadata.LastWriteTimeUtc.Ticks : 0L
            };
        }
    }
}
