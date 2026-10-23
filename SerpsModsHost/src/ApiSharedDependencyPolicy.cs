using System;
using System.IO;

namespace SerpsModsHost
{
    internal enum ApiSharedDependencyStatus
    {
        Satisfied,
        Missing,
        BelowMinimum,
        InvalidLocation,
        Embedded
    }

    internal static class ApiSharedDependencyPolicy
    {
        internal static ApiSharedDependencyStatus Evaluate(Version found, Version required, string dll, string hostDirectory)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            if (found == null) return ApiSharedDependencyStatus.Missing;
            if (found < required) return ApiSharedDependencyStatus.BelowMinimum;
            if (string.IsNullOrWhiteSpace(dll) || string.IsNullOrWhiteSpace(hostDirectory) ||
                !Path.IsPathRooted(dll) || !Path.IsPathRooted(hostDirectory))
                return ApiSharedDependencyStatus.InvalidLocation;
            try
            {
                // Chainloader has already established APIShared's GUID/version. Its
                // install folder may be renamed, nested, or located in Workshop content.
                // Only bundling inside this host package violates the separate dependency.
                string loadedPath = Path.GetFullPath(dll);
                string hostRoot = Path.GetFullPath(hostDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(loadedPath, hostRoot, StringComparison.OrdinalIgnoreCase) ||
                    loadedPath.StartsWith(hostRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    ? ApiSharedDependencyStatus.Embedded : ApiSharedDependencyStatus.Satisfied;
            }
            catch (ArgumentException) { return ApiSharedDependencyStatus.InvalidLocation; }
            catch (NotSupportedException) { return ApiSharedDependencyStatus.InvalidLocation; }
            catch (PathTooLongException) { return ApiSharedDependencyStatus.InvalidLocation; }
        }

        internal static bool IsSatisfied(Version found, Version required, string dll, string hostDirectory) =>
            Evaluate(found, required, dll, hostDirectory) == ApiSharedDependencyStatus.Satisfied;
    }
}
