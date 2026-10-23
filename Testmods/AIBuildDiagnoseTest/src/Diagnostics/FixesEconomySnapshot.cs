using System;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
using RedBird.Core.Memory;

namespace AIBuildDiagnoseTest
{
    internal static class FixesEconomySnapshot
    {
        // Reads the actual applied native-state arrays, not merely the Lord's preference defaults.
        internal static string Capture(int player)
        {
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out var plugin)) return "fixesLoaded=false";
            Type type = plugin.Instance.GetType().Assembly.GetType("Fixes.Detours.FixesAIDetours", false);
            if (type == null) return "fixesPurchaseState=unavailable; unsupported-type";
            var text = new StringBuilder("fixesPurchaseState=");
            foreach (string good in new[] { "Wood", "Iron", "Ale" })
            {
                try
                {
                    string prefix = "buy" + good;
                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    var enabled = type.GetField(prefix + "AtFlagArray", flags)?.GetValue(null) as NativeFlagArray;
                    var at = type.GetField(prefix + "AtArray", flags)?.GetValue(null) as NativeStateArray<int>;
                    var amount = type.GetField(prefix + "AmountArray", flags)?.GetValue(null) as NativeStateArray<int>;
                    if (enabled == null || at == null || amount == null || player < 0 ||
                        player >= enabled.Length || player >= at.Length || player >= amount.Length)
                    { text.Append(good).Append(":unavailable;"); continue; }
                    text.Append(good).Append(":enabled=").Append(enabled[player]).Append("/at=").Append(at[player])
                        .Append("/amount=").Append(amount[player]).Append(';');
                }
                catch (Exception ex) { text.Append(good).Append(":unavailable:").Append(ex.GetType().Name).Append(';'); }
            }
            return text.ToString();
        }
    }
}
