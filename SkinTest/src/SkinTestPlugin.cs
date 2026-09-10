using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace SkinTest
{
    [BepInDependency(ScriptExtenderGuid, ScriptExtenderVersion)]
    [BepInDependency(ApiSharedGuid, ApiSharedVersion)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SkinTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ScriptExtenderVersion = "2.3.0";
        private const string ApiSharedGuid = "APIShared_Serp";
        private const string ApiSharedVersion = "0.2.0";
        public const string PluginGuid = "SkinTest_Serp";
        public const string PluginName = "SkinTest";
        public const string PluginVersion = "0.1.0";

        // SHCDE destroys the BepInEx component during normal startup. These static
        // roots intentionally keep the visual runtime and hook alive for the process.
        private static ManualLogSource persistentLog;
        private static SwordsmanSkinRuntime runtime;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            LogInfo($"{PluginName} {PluginVersion} loaded; waiting for Script Extender {ScriptExtenderVersion}.");
            if (librarySubscriptionInstalled)
                return;

            librarySubscriptionInstalled = true;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            SwordsmanSkinRuntime candidate = null;
            try
            {
                candidate = new SwordsmanSkinRuntime(persistentLog);
                candidate.Initialize();
                runtime = candidate;
                LogInfo("SH1DE European swordsman skin initialized.");
            }
            catch (System.Exception ex)
            {
                candidate?.Dispose();
                LogError($"Initialization failed closed; Vanilla sprites remain active: {ex}");
                return;
            }
            // APIShared registrations are process-lifetime publications. They happen only after
            // the successfully initialized runtime has been rooted and can no longer be rolled back.
            runtime.RegisterTroopHudWithApiShared();
        }

        private static void LogInfo(string message) => persistentLog.LogInfo($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
        private static void LogError(string message) => persistentLog.LogError($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
