using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace AutoSendTest
{
    [BepInPlugin(Guid, "Auto Send Test", Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.32.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AutoSendPlugin : BaseUnityPlugin
    {
        public const string Guid = "AutoSendTest_Serp", Version = "0.2.1";
        private static AutoSendRuntime runtime;
        private static ManualLogSource log;
        private static bool registered;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger;
            // The static delegate and runtime survive destruction of the early plugin object.
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
        }
        private static void Loaded(CrusaderLibraryLoadContext ignored)
        {
            if (runtime != null) return;
            try
            {
                runtime = new AutoSendRuntime(ApiShared.ForMod(Guid), log);
                runtime.Register();
                log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] AutoSend ready; rooted by mission, Chore, simulation and shared UI publishers.");
            }
            catch (Exception error)
            { log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] AutoSend initialization failed: " + error); }
        }
    }
}
