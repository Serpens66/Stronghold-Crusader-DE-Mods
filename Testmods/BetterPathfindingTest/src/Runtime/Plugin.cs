using System.Reflection;
using BepInEx;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace BetterPathfindingTest
{
    [BepInPlugin(PluginGuid, "BetterPathfindingTest", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.16.3")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "BetterPathfindingTest_Serp", PluginVersion = "0.1.0";
        private static bool initialized;
        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            Shared.DebugLogHelper.LogInfo(Logger, "BetterPathfindingTest 0.1.0 loaded; read-only observer. Automatic comparisons require the /devdiag package.");
            DiagAttach(Logger);
        }
        static partial void DiagAttach(BepInEx.Logging.ManualLogSource log);
    }
}
