using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadStager : ICustomLordUploadStager
    {
        public bool TryResolveSource(string mapTitle, out string sourcePath, out string error)
        {
            sourcePath = string.Empty;
            // COMPATIBILITY: Recheck CustomLord fields and includeWorkshop semantics after game-DLL updates.
            List<CustomisationFileManager.CustomLord> localLords =
                CustomisationFileManager.Instance.GetCustomLords(includeWorkshop: false);
            int matchCount = 0;
            foreach (CustomisationFileManager.CustomLord lord in localLords)
            {
                if (lord != null && !lord.workshop &&
                    string.Equals(lord.lordName, mapTitle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(lord.customPath))
                        sourcePath = lord.customPath;
                    matchCount++;
                }
            }

            if (matchCount != 1 || string.IsNullOrWhiteSpace(sourcePath))
            {
                error = "source was not uniquely resolved among local non-Workshop lords";
                sourcePath = string.Empty;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryExtendStaging(
            string uploadContentRoot,
            string mapTitle,
            out CustomLordUploadStagingSummary? summary,
            out string error)
        {
            summary = null;
            if (!IsSafeDirectoryName(mapTitle))
            {
                error = "the upload title is not a safe folder name";
                return false;
            }

            if (string.IsNullOrWhiteSpace(uploadContentRoot) || !Path.IsPathRooted(uploadContentRoot))
            {
                error = "Vanilla's staging path is invalid";
                return false;
            }

            if (!TryResolveSource(mapTitle, out string sourcePath, out error))
                return false;

            try
            {
                // COMPATIBILITY: Vanilla currently stages into <upload root>/<lord title> before this hook runs.
                string stagingRoot = Path.GetFullPath(uploadContentRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(stagingRoot, mapTitle));
                string stagingPrefix = stagingRoot + Path.DirectorySeparatorChar;
                if (!Directory.Exists(stagingRoot) ||
                    !destination.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(destination) ||
                    IsReparsePoint(stagingRoot) ||
                    IsReparsePoint(destination))
                {
                    error = "Vanilla's staging directory is unsafe or incomplete";
                    return false;
                }

                if (!CustomLordWorkshopPackagePolicy.TryStageFiles(
                    sourcePath,
                    destination,
                    out int copiedFileCount,
                    out int existingFileCount,
                    out int packageFileCount,
                    out long packageByteCount,
                    out error))
                {
                    return false;
                }

                summary = new CustomLordUploadStagingSummary(
                    sourcePath,
                    destination,
                    packageFileCount,
                    packageByteCount,
                    copiedFileCount,
                    existingFileCount);
                return true;
            }
            catch (Exception exception)
            {
                error = "the staging destination could not be validated: " + exception.Message;
                return false;
            }
        }

        internal static bool IsSafeDirectoryName(string? name)
        {
            if (name == null || string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) ||
                name.IndexOf(':') >= 0 || name == "." || name == ".." ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(name, name.TrimEnd(' ', '.'), StringComparison.Ordinal))
            {
                return false;
            }
            return string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal);
        }

        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
    }
}
