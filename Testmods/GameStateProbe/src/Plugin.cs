using System;
using BepInEx;
using BepInEx.Logging;

namespace GameStateProbe
{
    /// <summary>
    /// Development-only probe: answers read-only state queries on 127.0.0.1 (see ProbeServer).
    /// Lifetime: the listener thread and the static ProbeServer are rooted independently of this
    /// component, which the SHCDE startup cleanup destroys right after Awake. The OnTick subscription
    /// is rooted by the Extender's GameTimeManagerAPI singleton. Nothing is torn down in OnDestroy.
    /// </summary>
    [BepInPlugin(Guid, "Game State Probe", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "GameStateProbe_Serp";
        private static bool initialized;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            int port = Config.Bind("Server", "Port", 47811, "TCP port on 127.0.0.1 (loopback only).").Value;
            ProbeServer.Start(Logger, port);
        }

        internal static void Log(ManualLogSource log, string text) =>
            log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] GAMESTATE_PROBE " + text);
    }
}
