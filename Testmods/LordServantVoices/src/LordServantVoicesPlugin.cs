using APIShared;
using BepInEx;
using System;
using System.Threading;

namespace LordServantVoices
{
    [BepInDependency(ScriptExtenderGuid, "2.14.3")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.47.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class LordServantVoicesPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "LordServantVoices_Serp";
        public const string PluginName = "Lord Servant Voices";
        public const string PluginVersion = "1.0.0";

        // Static roots: the plugin component is destroyed by the SHCDE startup cleanup,
        // while the readiness callback, Unity context and runtime must stay alive.
        private static readonly ModApiClient Api = ApiShared.ForMod(PluginGuid);
        private static SynchronizationContext unityContext;
        private static int started;

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            LordServantVoicesRuntime.Log = Logger;
            // Awake runs on Unity; WhenReady may call back on the initialization thread.
            unityContext = SynchronizationContext.Current;
            if (unityContext == null)
            {
                Shared.DebugLogHelper.LogError(Logger, "LORD_SERVANT_VOICES_DISABLED: no Unity synchronization context in Awake.");
                return;
            }
            Api.WhenReady(_ => unityContext.Post(__ => LordServantVoicesRuntime.Initialize(), null));
        }
    }
}
