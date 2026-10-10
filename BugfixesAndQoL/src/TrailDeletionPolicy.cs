using System;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class TrailDeletionPolicy
    {
        internal static bool TryResolve(string name, string sourcePath, bool workshop,
            string rootPath, out string safePath)
        {
            safePath = null;
            if (workshop || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rootPath) ||
                name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal))
                return false;
            try
            {
                string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
                string candidate = Path.GetFullPath(sourcePath);
                if (!string.Equals(Path.GetDirectoryName(candidate), root, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(candidate) || HasReparsePoint(root) || HasReparsePoint(candidate))
                    return false;
                CheckTree(candidate);
                safePath = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static void Delete(string name, string requestedPath, bool workshop, string rootPath)
        {
            if (!TryResolve(name, requestedPath, workshop, rootPath, out string safePath))
                throw new IOException("The selected Trail is no longer a safe local folder.");
            Directory.Delete(safePath, true);
            if (Directory.Exists(safePath))
                throw new IOException("The Trail folder still exists after deletion.");
        }

        private static bool HasReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static void CheckTree(string directory)
        {
            foreach (string entry in Directory.GetFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Trail deletion refuses symbolic links and junctions.");
                if ((attributes & FileAttributes.Directory) != 0)
                    CheckTree(entry);
            }
        }
    }
}
