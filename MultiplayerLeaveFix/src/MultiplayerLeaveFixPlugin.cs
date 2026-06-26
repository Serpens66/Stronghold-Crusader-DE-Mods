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
        public const string PluginVersion = "1.0.0";

        private MultiplayerLeaveFixRuntime runtime;
        private bool runtimeDisposed;

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");
            runtime = new MultiplayerLeaveFixRuntime(Logger);
            runtime.Apply();
        }

        private void OnDestroy()
        {
            DisposeRuntime();
        }

        private void OnApplicationQuit()
        {
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            if (runtimeDisposed)
                return;

            runtime?.Dispose();
            runtimeDisposed = true;
        }
    }
}
