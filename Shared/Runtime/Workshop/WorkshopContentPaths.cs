using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Shared
{
    /// <summary>Provides the read-only install roots Steam reports for subscribed SHCDE Workshop items.</summary>
    public static class WorkshopContentPaths
    {
        private static readonly FieldInfo SteamManagerInstanceField = typeof(SteamManager).GetField(
            "s_instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo SteamManagerInitializedField = typeof(SteamManager).GetField(
            "m_bInitialized",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public static bool IsSteamworksReady()
        {
            try
            {
                // Do not use SteamManager.Initialized here: its getter creates the manager when
                // Vanilla has not done so yet, which would move Steam initialization into a mod.
                object instance = SteamManagerInstanceField?.GetValue(null);
                return instance != null &&
                    SteamManagerInitializedField?.GetValue(instance) is bool initialized &&
                    initialized;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyList<string> GetSubscribedItemRoots(Action<string> warning = null)
        {
            // LibraryLoaded occurs before SteamManager.Awake in normal startup. Callers already
            // refresh from their real UI entry points, so defer the Steam API call until then.
            if (!IsSteamworksReady())
                return Array.Empty<string>();

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
                warning?.Invoke("Could not enumerate subscribed Steam Workshop content: " + exception.Message);
                return Array.Empty<string>();
            }
        }
    }
}
