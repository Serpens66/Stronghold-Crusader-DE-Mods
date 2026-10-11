using System;
using System.IO;

namespace SerpsModsHost
{
    internal static class ApiSharedDependencyPolicy
    {
        internal static bool IsSeparate(string dll, string pluginRoot) =>
            !string.IsNullOrWhiteSpace(dll) && string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(dll)).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(Path.Combine(pluginRoot, "APIShared_Serp")).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        internal static bool IsSatisfied(Version found, Version required, string dll, string pluginRoot)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            if (found == null || found < required) return false;
            return IsSeparate(dll, pluginRoot);
        }
    }
}
