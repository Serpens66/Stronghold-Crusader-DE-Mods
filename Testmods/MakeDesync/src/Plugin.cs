using BepInEx;
using SHCDESE.API.LowLevel;

namespace MakeDesync
{
    [BepInPlugin(Guid, "MakeDesync", "0.2.2")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.69.0")]
    [BepInDependency(BugfixesGuid, "1.0.240")]
    [BepInDependency("AIMarketOverbuyDiagnostic_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "MakeDesync_Serp";
        internal const string BugfixesGuid = "BugfixesAndQoL_Serp";
        private static Runtime runtime;

        // Test-only entry point for the armed UiAcceptance scenario.
        public static bool TryQueueAcceptance(string check, int tile)
        {
            switch (check)
            {
                case "units": return runtime?.TryQueue(UnityEngine.KeyCode.F9) == true;
                case "moat": return tile >= 0 && runtime?.TryQueue(UnityEngine.KeyCode.F11, tile) == true;
                case "soil": return runtime?.TryQueue(UnityEngine.KeyCode.F8) == true;
                default: return false;
            }
        }

        private void Awake()
        {
            runtime = new Runtime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += runtime.Initialize;
        }
    }
}
