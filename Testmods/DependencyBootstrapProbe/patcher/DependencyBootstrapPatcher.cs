using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Mono.Cecil;

namespace DependencyBootstrapProbe
{
    // Preloader contract only. No Unity/MonoMod types, Chainloader access or eager hook setup.
    public static class DependencyBootstrapPatcher
    {
        public static IEnumerable<string> TargetDLLs => new string[0];
        private static ILogListener listener;
        private static string mode;
        public static void Patch(AssemblyDefinition assembly) { }
        public static void Finish()
        {
            try
            {
                if (listener != null) return;
                ProbeTrace.Initialize();
                string identity = ProbeIdentity.Hash(typeof(DependencyBootstrapPatcher).Assembly.Location);
                mode = "Observe";
                if (File.Exists(Path.Combine(BepInEx.Paths.ConfigPath, "DependencyBootstrapProbe.disabled"))) mode = "Disabled";
                else
                {
                    string active = Path.Combine(BepInEx.Paths.ConfigPath, "DependencyBootstrapProbe.active");
                    if (File.Exists(active))
                    {
                        if (File.ReadAllText(active).Trim() == identity) mode = "Active";
                        else ProbeTrace.Mark("ERROR", "active authorization belongs to another build; remaining in Observe");
                    }
                }
                ProbeTrace.Mark("FINISH", "mode=" + mode + " patcher=" + identity);
                ProbeTrace.Mark(mode == "Disabled" ? "DISABLED" : "MODE", mode);
                listener = new ReadinessListener(new ReadyGate(OnReady, OnComplete, ProbeTrace.Mark));
                Logger.Listeners.Add(listener);
                ProbeTrace.Mark("LISTENER_ATTACHED", "no hooks prepared in Finish");
            }
            catch (Exception ex) { ProbeTrace.Mark("ERROR", "Finish " + ex); }
        }
        private static void OnReady()
        {
            ProbeTrace.Mark("READY_OBSERVED", "mode=" + mode + "; Initialize has not yet assigned _initialized");
            if (mode == "Active") InvokeHookRuntime("Install");
        }
        private static void OnComplete()
        {
            string status = "snapshot=False extender=False checks=0";
            if (mode == "Active") status = (string)InvokeHookRuntime("GetStatus");
            ProbeTrace.Mark("CHAIN_COMPLETE", status);
        }
        // String-based dispatch deliberately avoids a runtime type token on the early path.
        private static object InvokeHookRuntime(string method)
        {
            var type = typeof(DependencyBootstrapPatcher).Assembly.GetType("DependencyBootstrapProbe.HookRuntime", true);
            return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        private sealed class ReadinessListener : ILogListener
        {
            private readonly ReadyGate gate;
            internal ReadinessListener(ReadyGate gate) { this.gate = gate; }
            public void LogEvent(object sender, LogEventArgs args)
            {
                try { gate.Process(args?.Source?.SourceName, args?.Data as string); }
                catch (Exception ex) { ProbeTrace.Mark("ERROR", "listener " + ex); }
            }
            public void Dispose() { /* Process lifetime, no hook teardown. */ }
        }
    }
}