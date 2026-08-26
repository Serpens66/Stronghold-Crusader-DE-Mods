using BepInEx;

namespace MultiplayerLeaveFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MultiplayerLeaveFixPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "MultiplayerLeaveFix_Serp";
        public const string PluginName = "Multiplayer Leave Fix";
        public const string PluginVersion = "1.0.2";

        // SHCDE destroys the early BepInEx plugin object during startup. The native
        // hooks must therefore remain rooted independently for the process lifetime.
        private static MultiplayerLeaveFixRuntime runtime;

        private void Awake()
        {
            if (runtime != null)
                return;

            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
            runtime = new MultiplayerLeaveFixRuntime(Logger);
            runtime.Apply();
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(
                Logger,
                "Plugin component destroyed during startup; keeping multiplayer leave hooks active for the process lifetime.");
        }
    }
}
