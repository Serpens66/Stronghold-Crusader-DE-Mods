// Feature: Pure list and path policies for exposing protected Vanilla maps in the editor.
using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class VanillaMapEditorPolicy
    {
        internal static bool ShouldExposeBuiltIns(bool featureActive, bool isLoadEditorMap) =>
            featureActive && isLoadEditorMap;

        internal static List<T> MergeEditableBuiltIns<T>(
            IEnumerable<T> vanillaItems,
            IEnumerable<IEnumerable<T>> builtInGroups,
            Func<T, bool> isBuiltIn,
            Func<T, bool> isEditable,
            Func<T, string> pathSelector,
            Func<List<T>, List<T>> sorter)
        {
            if (isBuiltIn == null)
                throw new ArgumentNullException(nameof(isBuiltIn));
            if (isEditable == null)
                throw new ArgumentNullException(nameof(isEditable));
            if (pathSelector == null)
                throw new ArgumentNullException(nameof(pathSelector));
            if (sorter == null)
                throw new ArgumentNullException(nameof(sorter));

            List<T> merged = vanillaItems == null
                ? new List<T>()
                : new List<T>(vanillaItems);
            HashSet<string> knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (T item in merged)
                AddPath(knownPaths, pathSelector(item));

            if (builtInGroups != null)
            {
                foreach (IEnumerable<T> group in builtInGroups)
                {
                    if (group == null)
                        continue;

                    foreach (T item in group)
                    {
                        if (ReferenceEquals(item, null) || !isBuiltIn(item) || !isEditable(item))
                            continue;

                        string path = NormalizeIdentity(pathSelector(item));
                        if (path.Length == 0 || !knownPaths.Add(path))
                            continue;

                        merged.Add(item);
                    }
                }
            }

            return sorter(merged) ?? merged;
        }

        internal static List<T> RemoveMissingUserMaps<T>(
            IEnumerable<T> maps,
            Func<T, bool> isBuiltIn,
            Func<T, string> pathSelector,
            Func<string, bool> fileExists)
        {
            if (isBuiltIn == null)
                throw new ArgumentNullException(nameof(isBuiltIn));
            if (pathSelector == null)
                throw new ArgumentNullException(nameof(pathSelector));
            if (fileExists == null)
                throw new ArgumentNullException(nameof(fileExists));

            List<T> available = new List<T>();
            if (maps == null)
                return available;

            foreach (T map in maps)
            {
                if (ReferenceEquals(map, null) ||
                    (!isBuiltIn(map) && !fileExists(pathSelector(map))))
                {
                    continue;
                }

                available.Add(map);
            }

            return available;
        }

        internal static bool TryResolveDeletableUserMapPath(
            string selectedPath,
            string userMapsDirectory,
            Func<string, bool> fileExists,
            out string safePath)
        {
            safePath = null;
            if (string.IsNullOrWhiteSpace(selectedPath) ||
                string.IsNullOrWhiteSpace(userMapsDirectory) ||
                fileExists == null)
            {
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(selectedPath);
                if (!string.Equals(Path.GetExtension(candidate), ".map", StringComparison.OrdinalIgnoreCase))
                    return false;

                string parent = Path.GetDirectoryName(candidate);
                string userDirectory = Path.GetFullPath(userMapsDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(parent) ||
                    !string.Equals(
                        parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        userDirectory,
                        StringComparison.OrdinalIgnoreCase) ||
                    !fileExists(candidate))
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

        internal static string ResolveProtectedSavePath(
            string requestedPath,
            string builtInMapsDirectory,
            string userMapsDirectory,
            bool featureActive,
            bool mapSave)
        {
            if (!featureActive || !mapSave || string.IsNullOrWhiteSpace(requestedPath))
                return requestedPath;

            string requestedFullPath = Path.GetFullPath(requestedPath);
            string builtInFullPath = EnsureTrailingSeparator(Path.GetFullPath(builtInMapsDirectory));
            if (!requestedFullPath.StartsWith(builtInFullPath, StringComparison.OrdinalIgnoreCase))
                return requestedPath;

            string fileName = Path.GetFileName(requestedFullPath);
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("A protected Vanilla map save target has no file name.");

            return Path.Combine(Path.GetFullPath(userMapsDirectory), fileName);
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            string normalized = NormalizeIdentity(path);
            if (normalized.Length > 0)
                paths.Add(normalized);
        }

        private static string NormalizeIdentity(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
    }
}
