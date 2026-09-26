using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    internal sealed class CustomLordWorkshopPackageFile
    {
        internal CustomLordWorkshopPackageFile(string sourcePath, string relativePath)
        {
            SourcePath = sourcePath;
            RelativePath = relativePath;
        }

        internal string SourcePath { get; }
        internal string RelativePath { get; }
    }

    /// <summary>
    /// Safely adds files that Vanilla's Custom Lord uploader does not stage itself.
    /// Content validity deliberately remains the responsibility of Vanilla and the Script Extender loaders.
    /// </summary>
    internal static class CustomLordWorkshopPackagePolicy
    {
        internal static bool TryStageFiles(
            string sourceLordRoot,
            string stagingLordRoot,
            out int copiedFileCount,
            out int existingFileCount,
            out int packageFileCount,
            out long packageByteCount,
            out string error)
        {
            copiedFileCount = 0;
            existingFileCount = 0;
            packageFileCount = 0;
            packageByteCount = 0;
            if (!TryCollectFiles(
                    sourceLordRoot,
                    includeAllFiles: false,
                    out string sourceRoot,
                    out List<CustomLordWorkshopPackageFile> files,
                    out error))
            {
                return false;
            }

            string destinationRoot;
            try
            {
                destinationRoot = NormalizeExistingDirectory(stagingLordRoot, "Workshop staging directory");
                if (PathsOverlap(sourceRoot, destinationRoot))
                    throw new InvalidDataException("The source and Workshop staging directories may not overlap.");
            }
            catch (Exception exception)
            {
                error = "Could not validate the Workshop staging directory: " + exception.Message;
                return false;
            }

            List<string> copiedDestinations = new List<string>();
            List<string> createdDirectories = new List<string>();
            try
            {
                foreach (CustomLordWorkshopPackageFile file in files)
                {
                    // Revalidate immediately before use to reduce the opportunity for a filesystem swap.
                    ValidateRegularFile(file.SourcePath, "Custom Lord package file");
                    packageByteCount = checked(packageByteCount + new FileInfo(file.SourcePath).Length);
                    string destination = GetContainedPath(destinationRoot, file.RelativePath);
                    string? destinationDirectory = Path.GetDirectoryName(destination);
                    if (destinationDirectory == null)
                        throw new InvalidDataException("A package destination has no parent directory.");

                    CreateDirectoryChain(destinationRoot, destinationDirectory, createdDirectories);
                    if (Directory.Exists(destination))
                        throw new IOException("A package destination is an existing directory: " + file.RelativePath);

                    if (File.Exists(destination))
                    {
                        ValidateRegularFile(destination, "Existing Workshop staging file");
                        if (FilesAreEqual(file.SourcePath, destination))
                        {
                            existingFileCount++;
                            continue;
                        }
                        throw new IOException("A different package destination already exists: " + file.RelativePath);
                    }

                    CopyNewFile(file.SourcePath, destination, copiedDestinations);
                }

                copiedFileCount = copiedDestinations.Count;
                packageFileCount = files.Count;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                bool rollbackComplete = RollBackExtras(copiedDestinations, createdDirectories);
                packageFileCount = 0;
                packageByteCount = 0;
                error = "Could not copy the extended package into Workshop staging: " + exception.Message;
                if (!rollbackComplete)
                    error += " Rollback was incomplete; inspect the staging directory before uploading.";
                return false;
            }
        }

        internal static bool TryCollectFilesForInspection(
            string sourceLordRoot,
            out List<CustomLordWorkshopPackageFile> files,
            out string error)
        {
            bool result = TryCollectFiles(
                sourceLordRoot,
                includeAllFiles: true,
                out _,
                out files,
                out error);
            return result;
        }

        private static bool TryCollectFiles(
            string sourceLordRoot,
            bool includeAllFiles,
            out string normalizedRoot,
            out List<CustomLordWorkshopPackageFile> files,
            out string error)
        {
            normalizedRoot = string.Empty;
            files = new List<CustomLordWorkshopPackageFile>();
            try
            {
                normalizedRoot = NormalizeExistingDirectory(sourceLordRoot, "Custom Lord directory");
                IReadOnlyList<LordPackageFile> inventory = LordPackageFingerprint.Inventory(normalizedRoot);
                if (!includeAllFiles)
                {
                    string[] missingGameplay = inventory
                        .Where(file => IsExcludedFromExtendedUpload(file.RelativePath) &&
                            LordPackageFingerprint.Classify(file.RelativePath, string.Empty) ==
                                LordPackageFileKind.Gameplay)
                        .Select(file => file.RelativePath).ToArray();
                    if (missingGameplay.Length != 0)
                        throw new InvalidDataException("Gameplay files would be excluded from the Lord upload: " +
                            string.Join(", ", missingGameplay.Take(4)) +
                            (missingGameplay.Length > 4 ? " (and " + (missingGameplay.Length - 4) + " more)" : string.Empty));
                }
                foreach (LordPackageFile file in inventory)
                {
                    if (includeAllFiles || IsAllowedExtendedUploadFile(file.RelativePath))
                        files.Add(new CustomLordWorkshopPackageFile(file.FullPath, file.RelativePath));
                }
                files.Sort((left, right) =>
                {
                    int comparison = StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
                    return comparison != 0
                        ? comparison
                        : StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath);
                });

                for (int index = 1; index < files.Count; index++)
                {
                    if (string.Equals(
                            files[index - 1].RelativePath,
                            files[index].RelativePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "The package contains paths that collide on a case-insensitive filesystem: " +
                            files[index].RelativePath);
                    }
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                files.Clear();
                normalizedRoot = string.Empty;
                error = "The Custom Lord package could not be enumerated safely: " + exception.Message;
                return false;
            }
        }

        private static void CopyNewFile(string source, string destination, List<string> ownedDestinations)
        {
            using (FileStream sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream destinationStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // CreateNew succeeded, so this invocation owns even a partially written destination.
                ownedDestinations.Add(destination);
                sourceStream.CopyTo(destinationStream);
            }
        }

        internal static bool IsAllowedExtendedUploadFile(string relativePath)
        {
            string normalizedPath = NormalizeRelativePath(relativePath);
            string[] segments = normalizedPath.Split('/');
            string fileName = segments[segments.Length - 1];
            string extension = Path.GetExtension(fileName);

            if (extension.Equals(".data", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".ldata", StringComparison.OrdinalIgnoreCase))
                return false;

            // JSON sidecars may belong to any lord subdirectory, including other mods' Overrides.
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                return true;

            if (segments.Length == 1)
            {
                return string.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(segments[0], "Override", StringComparison.OrdinalIgnoreCase))
                return CustomLordCompatibilityProfile.AllowedOverrideExtensions.Contains(extension);

            if (string.Equals(segments[0], "Scripts", StringComparison.OrdinalIgnoreCase))
                return string.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(segments[0], "MapAreas", StringComparison.OrdinalIgnoreCase))
                return string.Equals(extension, ".sema", StringComparison.OrdinalIgnoreCase);

            return segments.Length == 3 &&
                   string.Equals(segments[0], "Locales", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(segments[1]) &&
                   string.Equals(segments[2], "crusader.txt", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsExcludedFromExtendedUpload(string relativePath)
        {
            return !IsAllowedExtendedUploadFile(relativePath) &&
                   !IsVanillaOwnedRootFile(relativePath) &&
                   !IsLocalControlFile(relativePath);
        }

        private static bool IsVanillaOwnedRootFile(string relativePath)
        {
            string normalizedPath = NormalizeRelativePath(relativePath);
            if (normalizedPath.IndexOf('/') >= 0)
                return false;

            if (string.Equals(normalizedPath, "avatar.png", StringComparison.OrdinalIgnoreCase))
                return true;

            string extension = Path.GetExtension(normalizedPath);
            return string.Equals(extension, ".lordjson", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".aivjson", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalControlFile(string relativePath)
        {
            string normalizedPath = NormalizeRelativePath(relativePath);
            string extension = Path.GetExtension(normalizedPath);
            return string.Equals(extension, ".data", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".ldata", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static void CreateDirectoryChain(string root, string directory, List<string> createdDirectories)
        {
            EnsureContainedOrEqual(root, directory);
            EnsureExistingDirectoryChainHasNoReparsePoints(root, directory);
            if (Directory.Exists(directory))
                return;

            string? parent = Path.GetDirectoryName(directory);
            if (parent == null)
                throw new InvalidDataException("A package destination directory has no parent.");
            CreateDirectoryChain(root, parent, createdDirectories);
            Directory.CreateDirectory(directory);
            if (IsReparsePoint(directory))
                throw new InvalidDataException("A newly created Workshop staging directory is a reparse point.");
            createdDirectories.Add(directory);
        }

        private static bool RollBackExtras(List<string> copiedFiles, List<string> createdDirectories)
        {
            bool complete = true;
            for (int index = copiedFiles.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (File.Exists(copiedFiles[index]))
                        File.Delete(copiedFiles[index]);
                }
                catch
                {
                    complete = false;
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
                    complete = false;
                }
            }
            return complete;
        }

        private static string NormalizeExistingDirectory(string path, string displayName)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidDataException(displayName + " must be an absolute path.");

            string fullPath = TrimTrailingSeparators(Path.GetFullPath(path));
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException(displayName + " does not exist.");
            if (IsReparsePoint(fullPath))
                throw new InvalidDataException(displayName + " may not be a reparse point.");
            return fullPath;
        }

        private static string TrimTrailingSeparators(string path)
        {
            string? root = Path.GetPathRoot(path);
            if (root != null && string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return path;
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string GetRelativePath(string root, string path)
        {
            EnsureContained(root, path);
            return path.Substring(root.Length + 1);
        }

        private static string GetContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
                throw new InvalidDataException("Package paths must be relative.");

            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new InvalidDataException(
                        "Package paths may not contain empty, current, or parent segments.");
                }
            }

            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            EnsureContained(root, fullPath);
            return fullPath;
        }

        private static void EnsureContained(string root, string path)
        {
            string prefix = root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A package path escapes its root directory.");
        }

        private static void EnsureContainedOrEqual(string root, string path)
        {
            if (!string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
                EnsureContained(root, path);
        }

        private static bool PathsOverlap(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
                   left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureExistingDirectoryChainHasNoReparsePoints(string root, string directory)
        {
            EnsureContainedOrEqual(root, directory);
            string current = root;
            if (IsReparsePoint(current))
                throw new InvalidDataException("The Workshop staging directory is a reparse point.");
            if (string.Equals(root, directory, StringComparison.OrdinalIgnoreCase))
                return;

            string relative = directory.Substring(root.Length + 1);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                if (Directory.Exists(current) && IsReparsePoint(current))
                {
                    throw new InvalidDataException(
                        "Workshop staging directories may not be reparse points.");
                }
            }
        }

        private static void ValidateRegularFile(string path, string displayName)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(displayName + " does not exist.", path);
            if (IsReparsePoint(path))
                throw new InvalidDataException(displayName + " may not be a reparse point.");
        }

        private static bool FilesAreEqual(string leftPath, string rightPath)
        {
            FileInfo left = new FileInfo(leftPath);
            FileInfo right = new FileInfo(rightPath);
            if (left.Length != right.Length)
                return false;

            const int BufferSize = 81920;
            byte[] leftBuffer = new byte[BufferSize];
            byte[] rightBuffer = new byte[BufferSize];
            using (FileStream leftStream = File.OpenRead(leftPath))
            using (FileStream rightStream = File.OpenRead(rightPath))
            {
                while (true)
                {
                    int leftRead = ReadChunk(leftStream, leftBuffer);
                    int rightRead = ReadChunk(rightStream, rightBuffer);
                    if (leftRead != rightRead)
                        return false;
                    if (leftRead == 0)
                        return true;
                    for (int index = 0; index < leftRead; index++)
                    {
                        if (leftBuffer[index] != rightBuffer[index])
                            return false;
                    }
                }
            }
        }

        private static int ReadChunk(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            return total;
        }

        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
    }
}
