using System;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace Shared
{
    // Read once during LibraryLoaded, after Fixes (soft dependency). Never changes its config or hooks.
    internal static class FixesStartupGate
    {
        internal static bool CanInstall(string feature, string section, Action<string> reportError)
        {
            bool present = Chainloader.PluginInfos.TryGetValue("fixes", out var plugin);
            bool? enabled = null;
            if (present)
            {
                try
                {
                    if (plugin.Instance?.Config != null && plugin.Instance.Config.TryGetEntry<bool>(section,
                        "Enabled", out ConfigEntry<bool> setting)) enabled = setting.Value;
                }
                catch { /* Unknown state must not authorize an overlapping hook. */ }
            }
            if (!present || enabled == false) return true;
            reportError(feature + " disabled: Fixes [" + section + "] Enabled is " +
                (enabled == true ? "true" : "unavailable/unverified") +
                ". To use this feature, set [" + section + "] Enabled = false in fixes.cfg and restart the game. " +
                "Independent diagnostics remain available; no Fixes settings or hooks were changed.");
            return false;
        }

        internal static void RequireOriginal(IntPtr address, byte[] expected)
        {
            if (address == IntPtr.Zero) throw new InvalidOperationException("Native diagnostic entry is unavailable.");
            for (int index = 0; index < expected.Length; index++)
                if (Marshal.ReadByte(address, index) != expected[index])
                    throw new InvalidOperationException("Native diagnostic entry has a foreign hook or differs from the audited bytes.");
        }
    }
}
