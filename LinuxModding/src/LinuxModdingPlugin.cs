using BepInEx;

namespace LinuxModding
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class LinuxModdingPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "LinuxModding_Serp";
        public const string PluginName = "Linux Modding Compatibility";
        public const string PluginVersion = "0.1.0";

        // SHCDE destroys the early BepInEx component during startup. Keep the detour
        // rooted independently for the complete lifetime of the game process.
        private static LinuxWorkshopUpdaterBridge bridge;

        private void Awake()
        {
            if (bridge != null)
                return;

            if (!LinuxWorkshopUpdaterBridge.WasStartedByCompatibilityLauncher())
            {
                Log("Compatibility launcher marker not present; plugin remains inactive.");
                return;
            }

            bridge = new LinuxWorkshopUpdaterBridge(Logger);
            bridge.Install();
            Log("Linux Workshop updater bridge active.");
        }

        private void OnDestroy()
        {
            if (bridge != null)
                Log("Plugin component destroyed during startup; updater bridge remains active.");
        }

        private void Log(string message)
        {
            Logger.LogInfo($"[{System.DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
}
