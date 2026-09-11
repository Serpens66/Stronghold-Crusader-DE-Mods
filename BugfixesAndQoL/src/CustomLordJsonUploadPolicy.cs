// Feature: Safe staging of direct JSON sidecars for Custom and Extended Lord uploads.
using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal enum CustomLordJsonUploadMode
    {
        None,
        CustomLord,
        ExtendedCpuLord
    }

    internal static class CustomLordJsonUploadPolicy
    {
        private const string CustomLordTag = "Custom Lord";
        private const string ExtendedCpuLordTag = "Extended CPU Lord";

        internal static CustomLordJsonUploadMode Classify(string[] tags)
        {
            bool customLord = ContainsExact(tags, CustomLordTag);
            bool extendedLord = ContainsExact(tags, ExtendedCpuLordTag);
            if (customLord == extendedLord)
                return CustomLordJsonUploadMode.None;
            return customLord
                ? CustomLordJsonUploadMode.CustomLord
                : CustomLordJsonUploadMode.ExtendedCpuLord;
        }

        internal static bool TryStageDirectJsonFiles(
            string sourceDirectory,
            string stagingRoot,
            string stagingChildName,
            out int copiedFileCount,
            out int existingFileCount,
            out string error)
        {
            copiedFileCount = 0;
            existingFileCount = 0;
            var copiedDestinations = new List<string>();
            try
            {
                string source = NormalizeExistingDirectory(sourceDirectory, "Lord source directory");
                string root = NormalizeExistingDirectory(stagingRoot, "Workshop staging root");
                string destination = ResolveDirectChild(root, stagingChildName);
                if (!Directory.Exists(destination))
                    throw new DirectoryNotFoundException("Vanilla's Lord staging directory does not exist.");
                RejectReparsePoint(destination, "Workshop Lord staging directory");
                if (PathsOverlap(source, destination))
                    throw new InvalidDataException("The Lord source and Workshop staging directories overlap.");

                string[] sourceFiles = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly);
                Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
                foreach (string sourceFile in sourceFiles)
                {
                    if (!string.Equals(Path.GetExtension(sourceFile), ".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    RejectReparsePoint(sourceFile, "Lord JSON file");
                    string destinationFile = Path.Combine(destination, Path.GetFileName(sourceFile));
                    if (Directory.Exists(destinationFile))
                        throw new IOException("A JSON destination is an existing directory: " + Path.GetFileName(sourceFile));
                    if (File.Exists(destinationFile))
                    {
                        RejectReparsePoint(destinationFile, "Existing Workshop JSON file");
                        if (!FilesAreEqual(sourceFile, destinationFile))
                            throw new IOException("A different JSON destination already exists: " + Path.GetFileName(sourceFile));
                        existingFileCount++;
                        continue;
                    }

                    CopyNewFile(sourceFile, destinationFile, copiedDestinations);
                }

                copiedFileCount = copiedDestinations.Count;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                RollBackCopiedFiles(copiedDestinations);
                copiedFileCount = 0;
                existingFileCount = 0;
                error = exception.Message;
                return false;
            }
        }

        private static bool ContainsExact(string[] tags, string expected)
        {
            if (tags == null)
                return false;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
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

        private static string ResolveDirectChild(string root, string childName)
        {
            if (string.IsNullOrWhiteSpace(childName) ||
                !string.Equals(childName, Path.GetFileName(childName), StringComparison.Ordinal) ||
                childName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException("The Workshop Lord folder name is invalid.");
            }

            string destination = Path.GetFullPath(Path.Combine(root, childName));
            string prefix = root + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                destination.Length <= prefix.Length ||
                destination.IndexOf(Path.DirectorySeparatorChar, prefix.Length) >= 0 ||
                destination.IndexOf(Path.AltDirectorySeparatorChar, prefix.Length) >= 0)
            {
                throw new InvalidDataException("The Workshop Lord folder is not a direct child of the staging root.");
            }
            return destination;
        }

        private static void CopyNewFile(string source, string destination, List<string> ownedDestinations)
        {
            using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // CreateNew establishes ownership, so a failed partial copy can be rolled back safely.
                ownedDestinations.Add(destination);
                sourceStream.CopyTo(destinationStream);
            }
        }

        private static bool FilesAreEqual(string leftPath, string rightPath)
        {
            var left = new FileInfo(leftPath);
            var right = new FileInfo(rightPath);
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

        private static void RollBackCopiedFiles(List<string> copiedFiles)
        {
            for (int index = copiedFiles.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (File.Exists(copiedFiles[index]))
                        File.Delete(copiedFiles[index]);
                }
                catch
                {
                }
            }
        }

        private static bool PathsOverlap(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
                   left.StartsWith(right + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   right.StartsWith(left + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void RejectReparsePoint(string path, string description)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(description + " is a reparse point: " + path);
        }
    }
}
