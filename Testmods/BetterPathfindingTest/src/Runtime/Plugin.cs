using System.Reflection;
using BepInEx;

[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
[assembly: AssemblyInformationalVersion("0.3.0")]

namespace BetterPathfindingTest
{
    [BepInPlugin(PluginGuid, "BetterPathfindingTest", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.16.3")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "BetterPathfindingTest_Serp", PluginVersion = "0.3.0";
        private static bool initialized;
        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            Shared.DebugLogHelper.LogInfo(Logger, "BetterPathfindingTest " + PluginVersion + " loaded; read-only prototype. Resident navigation requires the /devdiag package; performance remains unaccepted.");
            DiagAttach(Logger);
        }
        static partial void DiagAttach(BepInEx.Logging.ManualLogSource log);
    }
}
