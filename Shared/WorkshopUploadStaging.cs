using System;
using System.Collections.Generic;
using System.IO;

namespace Shared
{
    internal static class WorkshopUploadStaging
    {
        internal static bool TryResetDirectChild(
            string stagingRoot,
            string itemName,
            out string destination,
            out string error)
        {
            destination = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(stagingRoot) || !Path.IsPathRooted(stagingRoot))
                    throw new InvalidDataException("The Workshop staging root is invalid.");
                if (string.IsNullOrWhiteSpace(itemName) ||
                    !string.Equals(itemName, Path.GetFileName(itemName), StringComparison.Ordinal) ||
                    itemName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException("The Workshop item name is not a safe folder name.");
                }

                string root = Path.GetFullPath(stagingRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);
                RejectReparsePoint(root, "Workshop staging root");

                destination = Path.GetFullPath(Path.Combine(root, itemName));
                string expectedPrefix = root + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
                    destination.Length <= expectedPrefix.Length ||
                    destination.IndexOf(Path.DirectorySeparatorChar, expectedPrefix.Length) >= 0 ||
                    destination.IndexOf(Path.AltDirectorySeparatorChar, expectedPrefix.Length) >= 0)
                {
                    throw new InvalidDataException("The Workshop staging destination is not a direct child of its root.");
                }

                if (Directory.Exists(destination))
                {
                    RejectTreeReparsePoints(destination);
                    Directory.Delete(destination, true);
                }
                else if (File.Exists(destination))
                {
                    throw new InvalidDataException("The Workshop staging destination is an existing file.");
                }

                Directory.CreateDirectory(destination);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                destination = string.Empty;
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryStageTrailJsonFiles(
            string sourceRoot,
            string destinationRoot,
            out int copiedFiles,
            out string error)
        {
            copiedFiles = 0;
            var copiedDestinations = new List<string>();
            var createdDirectories = new List<string>();
            try
            {
                string source = NormalizeExistingDirectory(sourceRoot, "Custom Trail source");
                string destination = NormalizeExistingDirectory(destinationRoot, "Workshop staging destination");
                if (PathsOverlap(source, destination))
                    throw new InvalidDataException("The Custom Trail source and Workshop staging destination overlap.");

                var jsonFiles = new List<string>();
                CollectJsonFiles(source, source, jsonFiles);
                jsonFiles.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string jsonFile in jsonFiles)
                {
                    RejectReparsePoint(jsonFile, "Custom Trail JSON file");
                    string relativePath = jsonFile.Substring(source.Length + 1);
                    string target = Path.GetFullPath(Path.Combine(destination, relativePath));
                    if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("A Custom Trail JSON path escapes its staging directory.");
                    string parent = Path.GetDirectoryName(target)
                        ?? throw new InvalidDataException("A Custom Trail JSON destination has no parent directory.");
                    CreateDirectoryChain(destination, parent, createdDirectories);
                    if (Directory.Exists(target))
                        throw new IOException("A JSON destination is an existing directory: " + relativePath);
                    if (File.Exists(target))
                    {
                        RejectReparsePoint(target, "Existing Workshop JSON file");
                        if (!FilesAreEqual(jsonFile, target))
                            throw new IOException("A different JSON destination already exists: " + relativePath);
                        continue;
                    }
                    using (var input = new FileStream(jsonFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        copiedDestinations.Add(target);
                        input.CopyTo(output);
                    }
                    copiedFiles++;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                for (int index = copiedDestinations.Count - 1; index >= 0; index--)
                {
                    try { File.Delete(copiedDestinations[index]); }
                    catch { }
                }
                for (int index = createdDirectories.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        if (Directory.Exists(createdDirectories[index]) &&
                            Directory.GetFileSystemEntries(createdDirectories[index]).Length == 0)
                            Directory.Delete(createdDirectories[index]);
                    }
                    catch { }
                }
                copiedFiles = 0;
                error = exception.Message;
                return false;
            }
        }

        private static void CollectJsonFiles(string root, string directory, List<string> files)
        {
            RejectReparsePoint(directory, "Custom Trail source directory");
            foreach (string file in Directory.GetFiles(directory))
            {
                RejectReparsePoint(file, "Custom Trail source file");
                if (string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
                    files.Add(file);
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                string fullPath = Path.GetFullPath(child);
                if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A Custom Trail source path escapes its root directory.");
                CollectJsonFiles(root, fullPath, files);
            }
        }

        private static void CreateDirectoryChain(string root, string directory, List<string> created)
        {
            if (string.Equals(root, directory, StringComparison.OrdinalIgnoreCase))
                return;
            if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A Custom Trail JSON destination escapes its staging directory.");
            string parent = Path.GetDirectoryName(directory)
                ?? throw new InvalidDataException("A Custom Trail JSON destination has no parent directory.");
            CreateDirectoryChain(root, parent, created);
            if (Directory.Exists(directory))
            {
                RejectReparsePoint(directory, "Workshop staging directory");
                return;
            }
            Directory.CreateDirectory(directory);
            created.Add(directory);
        }

        private static bool FilesAreEqual(string leftPath, string rightPath)
        {
            if (new FileInfo(leftPath).Length != new FileInfo(rightPath).Length)
                return false;
            using (FileStream left = File.OpenRead(leftPath))
            using (FileStream right = File.OpenRead(rightPath))
            {
                byte[] leftBuffer = new byte[81920];
                byte[] rightBuffer = new byte[81920];
                int leftCount;
                while ((leftCount = left.Read(leftBuffer, 0, leftBuffer.Length)) > 0)
                {
                    int rightCount = 0;
                    while (rightCount < leftCount)
                    {
                        int count = right.Read(rightBuffer, rightCount, leftCount - rightCount);
                        if (count == 0)
                            return false;
                        rightCount += count;
                    }
                    for (int index = 0; index < leftCount; index++)
                        if (leftBuffer[index] != rightBuffer[index])
                            return false;
                }
                return right.ReadByte() < 0;
            }
        }

        private static bool PathsOverlap(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
                   left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeExistingDirectory(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidDataException(description + " is invalid.");
            string normalized = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(normalized))
                throw new DirectoryNotFoundException(description + " does not exist.");
            RejectReparsePoint(normalized, description);
            return normalized;
        }

        private static void RejectTreeReparsePoints(string root)
        {
            RejectReparsePoint(root, "Workshop staging destination");
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly))
                RejectReparsePoint(file, "Workshop staging file");
            foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                RejectReparsePoint(directory, "Workshop staging directory");
                // Reject the directory before descending so junction targets are never traversed.
                RejectTreeReparsePoints(directory);
            }
        }

        private static void RejectReparsePoint(string path, string description)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(description + " is a reparse point: " + path);
        }
    }
}
