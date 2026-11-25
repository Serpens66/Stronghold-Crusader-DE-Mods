using System;
using APIShared;
using BepInEx.Logging;

namespace BugfixesAndQoL.AutoSend
{
    internal static class AutoSendFeature
    {
        // Publishers retain this runtime after the early plugin component is destroyed.
        private static AutoSendRuntime runtime;

        internal static void Initialize(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (runtime != null) return;
            try
            {
                runtime = new AutoSendRuntime(ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid), log, settings);
                // Register on every peer, independently of its personal feature setting.
                runtime.Register();
                log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] AutoSend initialized through shared mission, simulation, Chore and UI publishers.");
            }
            catch (Exception error)
            {
                log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] AutoSend initialization failed: {error}");
            }
        }
    }
}
