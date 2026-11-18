using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using Mono.Cecil;

namespace WorkshopLoadProbe
{
    // Preloader entry. Like APIShared.DependencyBootstrap, Finish only reads the switch and
    // attaches a static log listener; the IL hook is prepared at "Chainloader ready".
    public static class ProbePatcher
    {
        public static IEnumerable<string> TargetDLLs => new string[0];
        private static ILogListener listener;
        internal static ProbeConfig Config;

        public static void Patch(AssemblyDefinition assembly) { }

        public static void Finish()
        {
            try
            {
                if (listener != null) return;
                ProbeLog.Initialize();
                Config = ProbeConfig.Read(BepInEx.Paths.ConfigPath);
                if (Config.Mode == ProbeMode.Off)
                {
                    ProbeLog.Info("INACTIVE", Config.Error ?? "mode=off");
                    return;
                }
                ProbeLog.Info("CONFIG", Config.ToString());
                listener = new ReadyListener();
                Logger.Listeners.Add(listener);
            }
            catch (Exception ex) { ProbeLog.Error("ERROR", "Finish " + ex); }
        }

        // String dispatch keeps MonoMod types off the early Finish path.
        private static void OnReady()
        {
            var type = typeof(ProbePatcher).Assembly.GetType("WorkshopLoadProbe.ChainHook", true);
            type.GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        private sealed class ReadyListener : ILogListener
        {
            private int seen;
            public void LogEvent(object sender, LogEventArgs args)
            {
                try
                {
                    if (args?.Source?.SourceName != "BepInEx" || !(args.Data is string message)) return;
                    if (message == "Chainloader ready" && Interlocked.Exchange(ref seen, 1) == 0) OnReady();
                }
                catch (Exception ex) { ProbeLog.Error("ERROR", "ready listener " + ex); }
            }
            public void Dispose() { /* Process lifetime; the hook is never undone. */ }
        }
    }
}
