using BepInEx;
using SHCDESE.API.LowLevel;

namespace MakeDesync
{
    [BepInPlugin(Guid, "MakeDesync", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.67.0")]
    [BepInDependency(DepositGuid, "0.3.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "MakeDesync_Serp";
        internal const string DepositGuid = "DepositRestorationTest_Serp";
        private static Runtime runtime;

        private void Awake()
        {
            runtime = new Runtime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += runtime.Initialize;
        }
    }
}
