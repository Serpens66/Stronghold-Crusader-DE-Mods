using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace SkinTest
{
    [BepInDependency(ScriptExtenderGuid, ScriptExtenderVersion)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SkinTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ScriptExtenderVersion = "2.3.0";
        public const string PluginGuid = "SkinTest_Serp";
        public const string PluginName = "SkinTest";
        public const string PluginVersion = "0.1.0";
        private SwordsmanSkinRuntime runtime;

        private void Awake()
        {
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            LogInfo($"{PluginName} {PluginVersion} loaded; waiting for Script Extender {ScriptExtenderVersion}.");
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
            try
            {
                runtime = new SwordsmanSkinRuntime(Logger);
                runtime.Initialize();
                LogInfo("SH1DE European swordsman skin initialized.");
            }
            catch (System.Exception ex)
            {
                runtime?.Dispose();
                runtime = null;
                LogError($"Initialization failed closed; Vanilla sprites remain active: {ex}");
            }
        }

        private void OnDestroy()
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
            runtime?.Dispose();
            runtime = null;
        }

        private void LogInfo(string message) => Logger.LogInfo($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
        private void LogError(string message) => Logger.LogError($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
