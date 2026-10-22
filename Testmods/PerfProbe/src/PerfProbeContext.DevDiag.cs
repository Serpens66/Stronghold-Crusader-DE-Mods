using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace PerfProbe
{
    internal static class PerfProbeContext
    {
        private static bool published;
        internal static void FirstFrame()
        {
            if (published) return;
            published = true;
            Publish("first-render-after-startup");
        }
        internal static void Publish(string reason)
        {
            try
            {
                Shared.DevDiag.Log(() => "runContext schema=2 reason=" + reason + " stamp=" + Stopwatch.GetTimestamp() +
                    " stopwatchFrequency=" + Stopwatch.Frequency + " gameMvid=" + typeof(EngineInterface).Module.ModuleVersionId +
                    " perfProbeMvid=" + typeof(PerfProbeContext).Module.ModuleVersionId +
                    " sampler=" + PerfProbeThreadSampler.Configuration + " " + PerfProbeRuntime.ThresholdConfiguration +
                    " engineCalls=" + PerfProbeExtras.EngineCalls + " gcTracking=" + PerfProbeExtras.GcTracking +
                    " updateSweep=" + PerfProbeExtras.UpdateSweep + " engineMarkers=" + PerfProbeExtras.EngineMarkers +
                    " batchCensus=" + PerfProbeExtras.BatchCensus + " recordBreakdown=" + PerfProbeExtras.RecordBreakdown +
                    " textureInventory=" + PerfProbeExtras.TextureInventory + " renderSnapshots=" + PerfProbeRuntime.RenderSnapshots +
                    " runPhasesMeasured=" + PerfProbeRunTiming.Instrumented);
                foreach (var plugin in Chainloader.PluginInfos.Values)
                {
                    var info = plugin;
                    Shared.DevDiag.Log(() => "runPlugin guid=" + info.Metadata.GUID + " version=" + info.Metadata.Version +
                        " mvid=" + (ReferenceEquals(info.Instance, null) ? "unavailable" : info.Instance.GetType().Module.ModuleVersionId.ToString()));
                }
                foreach (string name in new[] { "run", "GetMeritData", "SetAchData", "LoadSaveFile" })
                {
                    MethodInfo method = AccessTools.Method(typeof(EngineInterface), name);
                    var patches = method == null ? null : Harmony.GetPatchInfo(method);
                    if (patches == null) continue;
                    var text = new StringBuilder("patchOwners method=EngineInterface." + name);
                    foreach (var patch in patches.Prefixes) text.Append(" prefix=").Append(patch.owner).Append('@').Append(patch.PatchMethod.Module.ModuleVersionId);
                    foreach (var patch in patches.Postfixes) text.Append(" postfix=").Append(patch.owner).Append('@').Append(patch.PatchMethod.Module.ModuleVersionId);
                    foreach (var patch in patches.Transpilers) text.Append(" transpiler=").Append(patch.owner).Append('@').Append(patch.PatchMethod.Module.ModuleVersionId);
                    foreach (var patch in patches.Finalizers) text.Append(" finalizer=").Append(patch.owner).Append('@').Append(patch.PatchMethod.Module.ModuleVersionId);
                    string line = text.ToString(); Shared.DevDiag.Log(() => line);
                }
            }
            catch (Exception error) { Shared.DevDiag.Log(() => "runContext unavailable: " + error.GetType().Name); }
        }
    }
}
