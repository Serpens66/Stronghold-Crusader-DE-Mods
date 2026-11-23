using System;
using BepInEx;
using SHCDESE.API.LowLevel;
using Shared;

namespace DepositRestoration
{
    [BepInPlugin(Guid, "Deposit Restoration", "0.3.1")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.67.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Guid = "DepositRestorationTest_Serp";
        private static DepositRestorationRuntime runtime;

        private void Awake()
        {
            try
            {
                runtime = new DepositRestorationRuntime(Guid, Logger);
                CrusaderLibrary.Instance.LibraryLoaded += runtime.Install;
            }
            catch (Exception error)
            {
                DebugLogHelper.LogError(Logger, "Could not register wall deposit restoration: " + error);
            }
        }
    }
}
