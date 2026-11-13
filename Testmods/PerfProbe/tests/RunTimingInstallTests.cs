using System;
using System.Reflection;

namespace PerfProbeInstallFixture
{
    // Harmony publication/reflection boundaries are recorded, never initialized in this
    // desktop test process (the game's Harmony targets its Mono runtime).
    internal static class AccessTools
    {
        internal static MethodInfo Method(Type type, string name, Type[] parameters) => type.GetMethod(name, parameters);
    }
    internal static class Priority { internal const int First = 800, Last = 0; }
    internal sealed class HarmonyMethod
    {
        internal readonly MethodInfo method;
        internal int priority;
        internal HarmonyMethod(Type type, string name)
        {
            method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(type.FullName, name);
        }
    }
    internal static class Shared
    {
        internal static class DebugLogHelper
        {
            internal static void LogWarning(object log, string text) { throw new Exception(text); }
        }
    }
    internal sealed class EngineInterface { public void run(bool skip) { } }
    internal static class PerfProbeRunTiming { internal static void SafeTranspile() { } }
    internal sealed class Config
    {
        internal bool? Override;
        internal sealed class Entry { internal bool Value; }
        internal Entry Bind(string section, string key, bool fallback, string description)
        {
            if (section != "Spikes" || key != "RunPhaseTiming" || !fallback || string.IsNullOrEmpty(description))
                throw new Exception("RunPhaseTiming config identity/default changed");
            return new Entry { Value = Override ?? fallback };
        }
    }
    internal sealed class PatchRecorder
    {
        internal HarmonyMethod Prefix, Postfix, Finalizer, Transpiler;
        internal int Calls;
        internal void Patch(MethodInfo target, HarmonyMethod prefix, HarmonyMethod postfix,
            HarmonyMethod finalizer, HarmonyMethod transpiler, object ilmanipulator)
        {
            if (target == null || target.Name != "run" || target.GetParameters()[0].ParameterType != typeof(bool) || ilmanipulator != null)
                throw new Exception("Engine run target/publication contract changed");
            Calls++; Prefix = prefix; Postfix = postfix; Finalizer = finalizer; Transpiler = transpiler;
        }
    }
    internal static class PerfProbeRuntime
    {
        private static readonly object log = new object();
        internal static readonly PatchRecorder harmony = new PatchRecorder();
        /* SETTING */
        /* INSTALL */
        private static void BeginEngineRun() { }
        private static void EndEngineRun() { }
        private static void FailedEngineRun() { }
        internal static void Bind(Config config) { /* BINDING */ }
        internal static void Install() { InstallRunTiming(); }
    }
    public static class RunTimingInstallTests
    {
        public static string Run()
        {
            Check(PerfProbeRuntime.RunPhaseTiming, "unbound default disabled prior behavior");
            foreach (bool? configured in new bool?[] { null, false, true })
            {
                PerfProbeRuntime.Bind(new Config { Override = configured });
                PerfProbeRuntime.Install();
                var patch = PerfProbeRuntime.harmony;
                Check(patch.Prefix?.method.Name == "BeginEngineRun" && patch.Prefix.priority == Priority.First,
                    "sampler run-entry hook missing/reordered");
                Check(patch.Postfix?.method.Name == "EndEngineRun" && patch.Postfix.priority == Priority.Last &&
                    patch.Finalizer?.method.Name == "FailedEngineRun" && patch.Finalizer.priority == Priority.Last,
                    "completion/failure outer timing missing/reordered");
                Check(configured == false ? patch.Transpiler == null : patch.Transpiler?.method.Name == "SafeTranspile",
                    "internal phase instrumentation differs from configured/default state");
            }
            Check(PerfProbeRuntime.harmony.Calls == 3, "run timing was skipped or patched twice");
            return "PASS: actual run timing installer/config; default and enabled retain transpiler, disabled retains prefix/postfix/finalizer only.";
        }
        private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
