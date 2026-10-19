using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace PerfProbe
{
    // Read-only render/zoom measurement. All probes live in PerfProbePlugin.DevDiag.cs and exist
    // only in build.bat /devdiag builds; a normal build loads as a no-op.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class PerfProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "PerfProbe_Serp", Name = "Perf Probe", Version = "0.1.0";
        private static ManualLogSource log;
        private static bool registered, installed;

        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger;
            // Rooted by the extender's library publisher, not by this component (startup cleanup).
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            bool active = false;
            DiagIsActive(ref active);
            DiagBindConfig(Config);
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; measurements " +
                (active ? "active (DEVDIAG build)." : "inactive (no DEVDIAG build)."));
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (installed) return;
            installed = true;
            DiagInstall(log);
        }

        static partial void DiagIsActive(ref bool active);
        static partial void DiagBindConfig(BepInEx.Configuration.ConfigFile config);
        static partial void DiagInstall(ManualLogSource logger);
    }
}
