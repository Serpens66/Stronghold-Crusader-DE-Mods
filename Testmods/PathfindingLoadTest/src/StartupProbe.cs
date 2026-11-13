using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace PathfindingLoadTest
{
    // Explicit startup-probe only: observe before Serilog's asynchronous sink.
    // The prefix never changes arguments or skips the original error logger.
    internal static class StartupProbe
    {
        private static readonly Harmony Patch = new Harmony("Serp.PathfindingLoadTest.StartupProbe");
        private static readonly object Sync = new object();
        private static readonly List<string> Captures = new List<string>();
        private static bool registered, active;

        internal static void Enable()
        {
            lock (Sync)
            {
                if (!registered)
                {
                    MethodInfo target = typeof(SHCDESE.Logging.LogHelper).GetMethod("Error",
                        new[] { typeof(string), typeof(string), typeof(string) });
                    if (target == null || target.ReturnType != typeof(void) ||
                        PatchProcessor.GetOriginalInstructions(target).Count == 0)
                        throw new InvalidOperationException("Startup probe logger contract unavailable.");
                    Patch.Patch(target, prefix: new HarmonyMethod(typeof(StartupProbe), nameof(BeforeError)));
                    registered = true;
                }
                active = true;
            }
        }

        private static void BeforeError(string __0, string __1)
        {
            if (__1 != "TryGetBuildingById" || __0 == null ||
                !__0.Contains("Tried to access building index that was out of range: [")) return;
            try
            {
                lock (Sync)
                {
                    if (!active || Captures.Count >= 4) return;
                    string trace = new StackTrace(true).ToString();
                    if (trace.Length > 24000) trace = trace.Substring(0, 24000);
                    Captures.Add("STARTUP_LOOKUP_CAPTURE at=" + DateTime.Now.ToString("HH:mm:ss.fff") +
                        " thread=" + Thread.CurrentThread.ManagedThreadId + " message=" + __0 +
                        Environment.NewLine + trace);
                }
            }
            catch { /* Observation must never change the original logging path. */ }
        }

        internal static void Flush(Action<string> log)
        {
            string[] captured;
            lock (Sync) { if (!active) return; active = false; captured = Captures.ToArray(); }
            foreach (string trace in captured) log(trace);
            log("STARTUP_LOOKUP_CAPTURE count=" + captured.Length);
        }

        internal static void VerifyBuildingSnapshots(Action<string> log)
        {
            var assembly = Array.Find(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "BugfixesAndQoL");
            if (assembly == null) throw new InvalidOperationException("Startup regression requires BFQoL.");
            var root = assembly.GetType("BugfixesAndQoL.BugfixesAndQoLRuntime", true);
            var runtime = root.GetField("processAIPreplacedBuildingFixRuntime", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (runtime == null) throw new InvalidOperationException("Preplaced runtime unavailable.");
            if (!(bool)runtime.GetType().GetField("sessionEnabled", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime))
                throw new InvalidOperationException("Preplaced fix must be enabled for startup regression.");
            var capture = runtime.GetType().GetMethod("TryCaptureBuilding", BindingFlags.NonPublic | BindingFlags.Instance);
            if (capture == null) throw new MissingMethodException("TryCaptureBuilding");
            var snapshotType = capture.GetParameters()[1].ParameterType.GetElementType();
            foreach (int id in new[] { 0, -1, int.MinValue })
            {
                var args = new object[] { id, null };
                if ((bool)capture.Invoke(runtime, args) || !args[1].Equals(Activator.CreateInstance(snapshotType)))
                    throw new InvalidOperationException("Missing-building snapshot was not false/default: " + id);
            }
            VerifyValidSnapshot(runtime, capture);
            lock (Sync)
                if (Captures.Count != 0) throw new InvalidOperationException("Unexpected invalid building lookup remains.");
            log("startup-regression actual helper: IDs 0/-1/int.MinValue=false/default; valid snapshot unchanged; invalid lookup errors=0");
        }

        private static unsafe void VerifyValidSnapshot(object runtime, MethodInfo capture)
        {
            var api = SHCDESE.API.GameBuildingManagerAPI.Instance;
            foreach (int id in api.GetAllAliveBuildings())
            {
                if (!global::APIShared.BuildingAccess.TryGetById(api, id, out SHCDESE.Interop.GameBuilding* building, out _)) continue;
                var args = new object[] { id, null };
                if (!(bool)capture.Invoke(runtime, args)) throw new InvalidOperationException("Valid building rejected.");
                var snapshot = args[1];
                var type = snapshot.GetType();
                if ((int)type.GetProperty("Id").GetValue(snapshot) != id ||
                    (uint)type.GetProperty("GlobalId").GetValue(snapshot) != building->r_GlobalId ||
                    (int)type.GetProperty("OwnerId").GetValue(snapshot) != building->r_PlayerIdOwner)
                    throw new InvalidOperationException("Valid building snapshot changed.");
                return;
            }
            throw new InvalidOperationException("No alive building for positive-ID regression.");
        }
        // Process-owned prefix remains installed, logically inactive after Flush.
    }
}
