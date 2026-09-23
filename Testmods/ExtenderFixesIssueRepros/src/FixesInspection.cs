using RedBird.Core.Memory;
using System;
using System.Collections;
using System.Reflection;

namespace ExtenderFixesIssueRepros
{
    internal static class FixesInspection
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static Type AIDetoursType => Type.GetType("Fixes.Detours.FixesAIDetours, fixes", false);

        internal static bool TryGetAivBuffer(out IntPtr address, out string reason)
        {
            address = IntPtr.Zero;
            reason = string.Empty;
            Type managerType = Type.GetType("Fixes.Detours.DetourManager, fixes", false);
            object manager = managerType?.GetProperty("Instance", All)?.GetValue(null);
            IEnumerable detours = managerType?.GetField("nativeDetours", All)?.GetValue(manager) as IEnumerable;
            if (detours == null)
            {
                reason = "Fixes DetourManager/nativeDetours unavailable";
                return false;
            }

            foreach (object detour in detours)
            {
                if (detour?.GetType().FullName != "Fixes.Detours.FixesAIDetours")
                    continue;
                object value = detour.GetType().GetField("_newOrderedMapTileIdsAddress", All)?.GetValue(detour);
                if (value is IntPtr pointer && pointer != IntPtr.Zero)
                {
                    address = pointer;
                    return true;
                }
            }

            reason = "Fixes enlarged AIV buffer unavailable (check RelocateAIVOrderedMapTileIds)";
            return false;
        }

        internal static bool TryGetFlags(int playerId, out string flags, out string reason)
        {
            flags = string.Empty;
            reason = string.Empty;
            Type type = AIDetoursType;
            if (type == null)
            {
                reason = "FixesAIDetours type unavailable";
                return false;
            }

            string[] names = {
                "wheatSaleCategoryFlagArray",
                "minGoldForHarassmentSiegeEnginesFlagArray",
                "stoneToOxenRatioFlagArray",
                "maxOxenFlagArray"
            };
            string[] values = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                NativeFlagArray array = type.GetField(names[i], All)?.GetValue(null) as NativeFlagArray;
                if (array == null || playerId < 0 || playerId >= array.Length)
                {
                    reason = names[i] + " unavailable";
                    return false;
                }
                values[i] = names[i] + "=" + array[playerId];
            }
            flags = string.Join(" ", values);
            return true;
        }

        internal static bool TryGetPreferencePresence(string lordName, out bool hasPreferences)
        {
            hasPreferences = false;
            Type pluginType = Type.GetType("Fixes.Boostrap.Plugin, fixes", false);
            object plugin = pluginType?.GetField("Instance", All)?.GetValue(null);
            IDictionary dictionary = pluginType?.GetProperty("CustomLordPreferences", All)?.GetValue(plugin) as IDictionary;
            if (dictionary == null || lordName == null)
                return false;
            hasPreferences = dictionary.Contains(lordName);
            return true;
        }
    }
}
