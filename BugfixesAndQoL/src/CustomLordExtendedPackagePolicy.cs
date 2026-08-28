using Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordPackageDetails
    {
        internal static readonly CustomLordPackageDetails Empty = new CustomLordPackageDetails();

        internal string Description { get; set; } = string.Empty;
        internal string DifficultyRating { get; set; } = string.Empty;
        internal string FavouriteTroops { get; set; } = string.Empty;
        internal string Castles { get; set; } = string.Empty;
        internal string PlayStyle { get; set; } = string.Empty;
        internal string FavouriteSaying { get; set; } = string.Empty;
    }

    internal sealed class CustomLordPackageFile
    {
        internal CustomLordPackageFile(string sourcePath, string relativePath)
        {
            SourcePath = sourcePath;
            RelativePath = relativePath;
        }

        internal string SourcePath { get; }
        internal string RelativePath { get; }
    }

    internal static class CustomLordExtendedPackagePolicy
    {
        internal const string InfoFileName = "info.json";
        internal const string MetadataFileName = "lordmeta.json";
        internal const string OverrideDirectoryName = "Override";
        internal const string EnglishLocale = "en-US";

        private static readonly HashSet<string> AllowedMediaExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".ogg",
                ".wav",
                ".webm"
            };

        private static readonly string[] DetailPropertyNames =
        {
            "LocalizedDescription",
            "LocalizedDifficultyRating",
            "LocalizedFavouriteTroops",
            "LocalizedCastles",
            "LocalizedPlayStyle",
            "LocalizedFavouriteSaying"
        };

        internal static bool TryLoadDetails(
            string lordRoot,
            string locale,
            out CustomLordPackageDetails details,
            out string error)
        {
            details = CustomLordPackageDetails.Empty;
            if (!TryReadDocuments(lordRoot, out Dictionary<string, object> _, out Dictionary<string, object> metadata, out error))
                return false;

            details = new CustomLordPackageDetails
            {
                Description = ResolveLocalized(metadata, DetailPropertyNames[0], locale),
                DifficultyRating = ResolveLocalized(metadata, DetailPropertyNames[1], locale),
                FavouriteTroops = ResolveLocalized(metadata, DetailPropertyNames[2], locale),
                Castles = ResolveLocalized(metadata, DetailPropertyNames[3], locale),
                PlayStyle = ResolveLocalized(metadata, DetailPropertyNames[4], locale),
                FavouriteSaying = ResolveLocalized(metadata, DetailPropertyNames[5], locale)
            };
            return true;
        }

        internal static bool TryCollectUploadFiles(
            string lordRoot,
            out List<CustomLordPackageFile> files,
            out string error)
        {
            files = new List<CustomLordPackageFile>();
            if (!TryReadDocuments(lordRoot, out Dictionary<string, object> _, out Dictionary<string, object> _, out error))
                return false;

            string root = NormalizeDirectory(lordRoot);
            string infoPath = Path.Combine(root, InfoFileName);
            string metadataPath = Path.Combine(root, MetadataFileName);
            files.Add(new CustomLordPackageFile(infoPath, InfoFileName));
            files.Add(new CustomLordPackageFile(metadataPath, MetadataFileName));

            string overrideRoot = Path.Combine(root, OverrideDirectoryName);
            if (!Directory.Exists(overrideRoot))
                return true;

            if (IsReparsePoint(overrideRoot))
            {
                error = "Override is a reparse point.";
                files.Clear();
                return false;
            }

            if (!TryCollectMediaFiles(root, overrideRoot, files, out error))
            {
                files.Clear();
                return false;
            }

            return true;
        }

        internal static bool TryStageUploadFiles(
            string lordRoot,
            string stagingLordRoot,
            out int copiedFileCount,
            out string error)
        {
            copiedFileCount = 0;
            if (!TryCollectUploadFiles(lordRoot, out List<CustomLordPackageFile> files, out error))
                return false;

            string destinationRoot;
            try
            {
                destinationRoot = NormalizeDirectory(stagingLordRoot);
                Directory.CreateDirectory(destinationRoot);
            }
            catch (Exception exception)
            {
                error = "Could not prepare the Workshop staging directory: " + exception.Message;
                return false;
            }

            var copiedDestinations = new List<string>();
            var createdDirectories = new List<string>();
            try
            {
                foreach (CustomLordPackageFile file in files)
                {
                    string destination = GetContainedPath(destinationRoot, file.RelativePath);
                    string destinationDirectory = Path.GetDirectoryName(destination);
                    CreateDirectoryChain(destinationRoot, destinationDirectory, createdDirectories);
                    File.Copy(file.SourcePath, destination, true);
                    copiedDestinations.Add(destination);
                }

                copiedFileCount = copiedDestinations.Count;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                // Vanilla has already staged its own files. Roll back only the extra files added here.
                for (int index = copiedDestinations.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        if (File.Exists(copiedDestinations[index]))
                            File.Delete(copiedDestinations[index]);
                    }
                    catch
                    {
                    }
                }
                for (int index = createdDirectories.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        if (Directory.Exists(createdDirectories[index]) &&
                            Directory.GetFileSystemEntries(createdDirectories[index]).Length == 0)
                        {
                            Directory.Delete(createdDirectories[index]);
                        }
                    }
                    catch
                    {
                    }
                }

                error = "Could not copy the extended package into Workshop staging: " + exception.Message;
                return false;
            }
        }

        private static bool TryReadDocuments(
            string lordRoot,
            out Dictionary<string, object> info,
            out Dictionary<string, object> metadata,
            out string error)
        {
            info = null;
            metadata = null;
            error = string.Empty;
            try
            {
                string root = NormalizeDirectory(lordRoot);
                if (!Directory.Exists(root))
                {
                    error = "The Custom Lord directory does not exist.";
                    return false;
                }
                if (IsReparsePoint(root))
                {
                    error = "The Custom Lord directory is a reparse point.";
                    return false;
                }

                string infoPath = Path.Combine(root, InfoFileName);
                string metadataPath = Path.Combine(root, MetadataFileName);
                if (!File.Exists(infoPath) || !File.Exists(metadataPath))
                {
                    error = "The extended package requires both info.json and lordmeta.json.";
                    return false;
                }
                if (IsReparsePoint(infoPath) || IsReparsePoint(metadataPath))
                {
                    error = "Package metadata files may not be reparse points.";
                    return false;
                }

                info = DependencyFreeJson.Parse(File.ReadAllText(infoPath)) as Dictionary<string, object>;
                metadata = DependencyFreeJson.Parse(File.ReadAllText(metadataPath)) as Dictionary<string, object>;
                if (info == null || metadata == null)
                {
                    error = "Both package metadata files must contain a JSON object.";
                    return false;
                }
                if (!ValidateInfo(info, out error) || !ValidateLordMetadata(metadata, out error))
                    return false;

                return true;
            }
            catch (Exception exception)
            {
                error = "Could not read the extended Custom Lord package: " + exception.Message;
                return false;
            }
        }

        private static bool ValidateInfo(Dictionary<string, object> info, out string error)
        {
            foreach (string property in new[] { "GUID", "Author", "Name", "Version" })
            {
                if (!info.TryGetValue(property, out object value) || !(value is string text) || string.IsNullOrWhiteSpace(text))
                {
                    error = "info.json requires a non-empty string property " + property + ".";
                    return false;
                }
            }

            if (!info.TryGetValue("Manifest", out object manifest) || !IsZeroInteger(manifest))
            {
                error = "info.json must use Script Extender asset Manifest 0.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateLordMetadata(Dictionary<string, object> metadata, out string error)
        {
            if (!ValidateOptionalLocalizedStrings(metadata, "LocalizedDisplayName", out error) ||
                !ValidateOptionalLocalizedLists(metadata, "LocalizedTitles", out error))
            {
                return false;
            }

            foreach (string property in new[] { "FacePath", "JoinAudioPath", "LeaveAudioPath", "IncomingMessage" })
            {
                if (!metadata.TryGetValue(property, out object value) || value == null)
                    continue;
                if (!(value is string text) || !IsSafeAssetReference(text))
                {
                    error = "lordmeta.json contains an unsafe or non-string " + property + ".";
                    return false;
                }
            }

            if (metadata.TryGetValue("Messages", out object messagesValue) && messagesValue != null)
            {
                if (!(messagesValue is Dictionary<string, object> messages))
                {
                    error = "lordmeta.json Messages must be an object.";
                    return false;
                }
                foreach (KeyValuePair<string, object> message in messages)
                {
                    if (!(message.Value is List<object> clips))
                    {
                        error = "Each lordmeta.json message category must be an array.";
                        return false;
                    }
                    foreach (object clipValue in clips)
                    {
                        if (!(clipValue is Dictionary<string, object> clip))
                        {
                            error = "Each lordmeta.json message clip must be an object.";
                            return false;
                        }
                        foreach (string property in new[] { "VideoPath", "AudioPath" })
                        {
                            if (!clip.TryGetValue(property, out object pathValue) || pathValue == null)
                                continue;
                            if (!(pathValue is string path) || !IsSafeAssetReference(path))
                            {
                                error = "lordmeta.json contains an unsafe or non-string message " + property + ".";
                                return false;
                            }
                        }
                        if (!ValidateOptionalLocalizedStrings(clip, "LocalizedText", out error))
                            return false;
                    }
                }
            }

            // Optional description dictionaries are deliberately tolerant: malformed fields are ignored in-game.
            error = string.Empty;
            return true;
        }

        private static bool ValidateOptionalLocalizedStrings(
            Dictionary<string, object> owner,
            string property,
            out string error)
        {
            if (!owner.TryGetValue(property, out object value) || value == null)
            {
                error = string.Empty;
                return true;
            }
            if (!(value is Dictionary<string, object> values))
            {
                error = property + " must be an object containing locale strings.";
                return false;
            }
            foreach (KeyValuePair<string, object> localized in values)
            {
                if (!(localized.Value is string))
                {
                    error = property + " contains a non-string locale value.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateOptionalLocalizedLists(
            Dictionary<string, object> owner,
            string property,
            out string error)
        {
            if (!owner.TryGetValue(property, out object value) || value == null)
            {
                error = string.Empty;
                return true;
            }
            if (!(value is Dictionary<string, object> values))
            {
                error = property + " must be an object containing locale arrays.";
                return false;
            }
            foreach (KeyValuePair<string, object> localized in values)
            {
                if (!(localized.Value is List<object> entries))
                {
                    error = property + " contains a non-array locale value.";
                    return false;
                }
                foreach (object entry in entries)
                {
                    if (!(entry is string))
                    {
                        error = property + " contains a non-string title.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        private static string ResolveLocalized(
            Dictionary<string, object> metadata,
            string property,
            string locale)
        {
            if (!metadata.TryGetValue(property, out object value) ||
                !(value is Dictionary<string, object> localized))
            {
                return string.Empty;
            }

            string text = ReadNonEmptyString(localized, locale);
            if (text.Length == 0 && !string.Equals(locale, EnglishLocale, StringComparison.OrdinalIgnoreCase))
                text = ReadNonEmptyString(localized, EnglishLocale);
            return text;
        }

        private static string ReadNonEmptyString(Dictionary<string, object> localized, string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return string.Empty;

            foreach (KeyValuePair<string, object> entry in localized)
            {
                if (string.Equals(entry.Key, locale, StringComparison.OrdinalIgnoreCase) &&
                    entry.Value is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
            return string.Empty;
        }

        private static bool TryCollectMediaFiles(
            string packageRoot,
            string directory,
            List<CustomLordPackageFile> files,
            out string error)
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                if (IsReparsePoint(file))
                {
                    error = "Package media files may not be reparse points.";
                    return false;
                }
                string fullPath = Path.GetFullPath(file);
                EnsureContained(packageRoot, fullPath);
                if (AllowedMediaExtensions.Contains(Path.GetExtension(fullPath)))
                {
                    string relativePath = fullPath.Substring(packageRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    files.Add(new CustomLordPackageFile(fullPath, relativePath));
                }
            }

            foreach (string child in Directory.GetDirectories(directory))
            {
                if (IsReparsePoint(child))
                {
                    error = "Package media directories may not be reparse points.";
                    return false;
                }
                string fullPath = Path.GetFullPath(child);
                EnsureContained(packageRoot, fullPath);
                if (!TryCollectMediaFiles(packageRoot, fullPath, files, out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private static void CreateDirectoryChain(
            string root,
            string directory,
            List<string> createdDirectories)
        {
            if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
                return;

            string parent = Path.GetDirectoryName(directory);
            if (!string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                CreateDirectoryChain(root, parent, createdDirectories);
            Directory.CreateDirectory(directory);
            createdDirectories.Add(directory);
        }

        private static string GetContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidDataException("Package destination path must be relative.");
            string destination = Path.GetFullPath(Path.Combine(root, relativePath));
            EnsureContained(root, destination);
            return destination;
        }

        private static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("Package directory is empty.");
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void EnsureContained(string root, string path)
        {
            string normalizedRoot = NormalizeDirectory(root);
            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Package path escapes its root directory.");
        }

        private static bool IsReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static bool IsSafeAssetReference(string path)
        {
            if (path == null)
                return false;
            string normalized = path.Trim().Replace('\\', '/');
            if (normalized.Length == 0)
                return true;
            if (Path.IsPathRooted(normalized) || normalized.IndexOf(':') >= 0)
                return false;
            string[] segments = normalized.Split('/');
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                    return false;
            }
            return true;
        }

        private static bool IsZeroInteger(object value)
        {
            if (value is int integer)
                return integer == 0;
            if (value is long longInteger)
                return longInteger == 0L;
            if (value is ulong unsignedInteger)
                return unsignedInteger == 0UL;
            return false;
        }
    }
}
