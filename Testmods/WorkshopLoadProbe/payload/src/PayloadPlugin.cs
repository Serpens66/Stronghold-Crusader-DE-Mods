using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx;

namespace WorkshopLoadProbe.Payload
{
    // Placed only outside BepInEx/plugins (simulated or real Workshop item). Hard-depends on
    // the extender and references a helper DLL from its own folder (P2).
    [BepInPlugin(Guid, "Workshop Load Probe Payload", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    public sealed class PayloadPlugin : BaseUnityPlugin
    {
        public const string Guid = "WorkshopLoadProbePayload_Serp";

        private void Awake()
        {
            Mark("PAYLOAD_AWAKE", "info=" + Info.Location + " assembly=" + typeof(PayloadPlugin).Assembly.Location + " pluginPath=" + Paths.PluginPath);
            try { Mark("PAYLOAD_HELPER", Helper()); }
            catch (Exception ex) { Logger.LogError(Stamp() + " WLP PAYLOAD_HELPER_FAILED " + ex.GetType().Name + ": " + ex.Message); }
        }

        // Separate method: the helper type is resolved when this is JIT-compiled, inside the try above.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Helper() => WorkshopLoadProbe.Helper.ProbeHelper.Describe();

        private void Mark(string marker, string detail) => Logger.LogInfo(Stamp() + " WLP " + marker + " " + detail);

        private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
