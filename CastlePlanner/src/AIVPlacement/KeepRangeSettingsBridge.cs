using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CastlePlanner.AIVPlacement.Core;
using MapParser.Core;
using SHCDESE.API;

namespace CastlePlanner.AIVPlacement
{
    internal static class KeepRangeSettingsBridge
    {
        private static bool failureLogged;
        internal static bool NativeCompatible { get; set; }

        internal static KeepRangeSnapshot Capture(IDictionary<int, int> teams, ManualLogSource log)
        {
            bool? bypass = false;
            var ranges = new Dictionary<int, int>();
            try
            {
                if (Chainloader.PluginInfos.ContainsKey("BugfixesAndQoL_Serp"))
                {
                    object[] arguments = { false };
                    MethodInfo method = Resolve("BugfixesAndQoL_Serp", "TryGetLobbyAIKeepRangeBypass",
                        new[] { typeof(bool).MakeByRefType() });
                    bypass = method != null && method.Invoke(null, arguments) is bool known && known
                        ? (bool?)arguments[0] : null;
                }
                if (bypass == true) return new KeepRangeSnapshot(true, ranges, teams);
                if (!NativeCompatible) return new KeepRangeSnapshot(null, ranges, teams);
                GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
                int value = api.KeepProximityOverride.GetValue();
                bool rangeKnown = true;
                if (Chainloader.PluginInfos.ContainsKey("ExtraFeatures_Serp"))
                {
                    object[] arguments = { value, value };
                    MethodInfo method = Resolve("ExtraFeatures_Serp", "TryGetLobbyKeepRangeOverride",
                        new[] { typeof(int), typeof(int).MakeByRefType() });
                    rangeKnown = method != null && method.Invoke(null, arguments) is bool known && known;
                    if (rangeKnown) value = (int)arguments[1];
                }
                if (rangeKnown)
                    foreach (int size in MapTileGeometry.SupportedWorldSizes)
                        ranges[size] = value > 0 ? value : api.GetKeepProximityRange(size);
                if ((!rangeKnown || !bypass.HasValue) && !failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(log,
                        "Keep range lobby status unavailable in an installed optional mod; no certain range verdict will be published.");
                }
            }
            catch (Exception ex)
            {
                ranges.Clear();
                if (!failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(log, "Keep range lobby capture failed: " + ex);
                }
            }
            return new KeepRangeSnapshot(bypass, ranges, teams);
        }

        private static MethodInfo Resolve(string guid, string name, Type[] parameters)
        {
            // The assembly and static API survive the destruction of the BepInEx component.
            Type type = Chainloader.PluginInfos[guid].Instance.GetType();
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
        }
    }
}
