using System;
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

        internal static bool TryStageTrailSidecars(
            string sourceRoot,
            string destinationRoot,
            out int copiedFiles,
            out string error)
        {
            copiedFiles = 0;
            try
            {
                string source = NormalizeExistingDirectory(sourceRoot, "Custom Trail source");
                string destination = NormalizeExistingDirectory(destinationRoot, "Workshop staging destination");
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The Custom Trail source and Workshop staging destination overlap.");

                string[] sidecars = Directory.GetFiles(source, "*.modjson", SearchOption.TopDirectoryOnly);
                Array.Sort(sidecars, StringComparer.OrdinalIgnoreCase);
                foreach (string sidecar in sidecars)
                {
                    RejectReparsePoint(sidecar, "Custom Trail sidecar");
                    string trail = Path.ChangeExtension(sidecar, ".trail");
                    if (!File.Exists(trail))
                        continue;
                    RejectReparsePoint(trail, "Custom Trail mission");

                    string target = Path.Combine(destination, Path.GetFileName(sidecar));
                    File.Copy(sidecar, target, true);
                    copiedFiles++;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
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
