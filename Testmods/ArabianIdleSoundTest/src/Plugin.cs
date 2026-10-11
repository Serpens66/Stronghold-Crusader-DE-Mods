using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace ArabianIdleSoundTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.7.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ArabianIdleSoundTest_Serp";
        public const string Name = "Arabian Idle Sound Test";
        public const string Version = "0.1.0";
        private static ConfigEntry<bool> enabledSetting;
        private static ManualLogSource log;
        private static SoundRuntime runtime;
        private static int attempted;

        private void Awake()
        {
            log = Logger;
            enabledSetting = Config.Bind("General", "Enabled", true,
                "Reduce only the Arabian swordsman's idle sword-draw playback to 25 percent. Restart or reload the config for comparison.");
            enabledSetting.SettingChanged += SettingChanged;
            SettingChanged(null, EventArgs.Empty);
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + "; waiting for native library; factor=0.25.");
            // LibraryLoaded may replay synchronously when subscribing.
            CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
        }

        private static void SettingChanged(object sender, EventArgs args) => SoundPolicy.SetEnabled(enabledSetting.Value);

        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            // Root the candidate BEFORE any hook can become visible. Failed publication
            // leaves installed callbacks inert and rooted until process exit.
            runtime = new SoundRuntime(log);
            try { runtime.Install(context); }
            catch (Exception ex)
            {
                SoundPolicy.SetReady(false);
                Shared.DebugLogHelper.LogError(log, "Idle sword-draw feature disabled; initialization failed: " + ex);
            }
        }
    }
}
