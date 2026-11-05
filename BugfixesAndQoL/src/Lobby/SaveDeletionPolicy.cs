using System;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class SaveDeletionPolicy
    {
        internal static int FindNextAvailableIndex(
            int deletedIndex,
            int itemCount,
            Func<int, bool> isAvailable)
        {
            if (deletedIndex < 0 || itemCount <= 0 || isAvailable == null)
                return -1;

            for (int index = Math.Min(deletedIndex, itemCount - 1); index < itemCount; index++)
            {
                if (isAvailable(index))
                    return index;
            }

            for (int index = Math.Min(deletedIndex, itemCount - 1) - 1; index >= 0; index--)
            {
                if (isAvailable(index))
                    return index;
            }

            return -1;
        }

        internal static bool TryResolveDeletableSavePath(
            string selectedPath,
            string savesDirectory,
            bool multiplayer,
            Func<string, bool> fileExists,
            out string safePath)
        {
            safePath = null;
            if (string.IsNullOrWhiteSpace(selectedPath) ||
                string.IsNullOrWhiteSpace(savesDirectory) ||
                fileExists == null)
            {
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(selectedPath);
                string expectedExtension = multiplayer ? ".msv" : ".sav";
                if (!string.Equals(
                        Path.GetExtension(candidate),
                        expectedExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string parent = Path.GetDirectoryName(candidate);
                string root = Path.GetFullPath(savesDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(parent) ||
                    !string.Equals(
                        parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        root,
                        StringComparison.OrdinalIgnoreCase) ||
                    !fileExists(candidate) ||
                    (File.GetAttributes(candidate) &
                        (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                    (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                safePath = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
