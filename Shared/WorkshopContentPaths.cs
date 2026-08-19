using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shared
{
    /// <summary>Provides the read-only install roots Steam reports for subscribed SHCDE Workshop items.</summary>
    public static class WorkshopContentPaths
    {
        public static IReadOnlyList<string> GetSubscribedItemRoots(Action<string> warning = null)
        {
            try
            {
                return (Platform_Workshop.Instance.GetListOfSubscribedItemsPaths() ?? new List<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception)
            {
                // Steam can be unavailable during very early plugin construction. Callers refresh
                // when their real UI opens, so an empty initial result is safe.
                warning?.Invoke("Could not enumerate subscribed Steam Workshop content: " + exception.Message);
                return Array.Empty<string>();
            }
        }
    }
}
