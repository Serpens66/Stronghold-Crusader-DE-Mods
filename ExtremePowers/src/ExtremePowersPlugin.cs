using BepInEx;
using ExtremePowers.Integration;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Threading;

namespace ExtremePowers
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtremePowersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ExtremePowers_Serp", PluginName = "Extreme Powers", PluginVersion = "0.1.0";
        private static int initialized; private static IExtremePowersApiClient client; private static IDisposable demoHandle;
        private static Settings.ExtremePowersSettings rootedSettings;
        public Settings.ExtremePowersSettings Settings { get; private set; }
        private void Awake() { Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded."); CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded; }
        private void OnLibraryLoaded(IntPtr handle, ReadOnlySpan<byte> memory)
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return; CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
            string dll = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            Settings = rootedSettings = new Settings.ExtremePowersSettings();
            client = LocalExtremePowersApiClient.Create(dll, handle, memory, IsProtocolReady);
            Shared.LobbyModSettingsPresetRegistration.Register(this, Logger, PluginGuid, Settings, "ScriptExtenderUI/ExtremePowersSettings.xaml");
            Settings.PropertyChanged += (_, __) => ApplySettings(); ApplySettings(); Shared.DebugLogHelper.LogDebug(Logger, client.Status);
        }
        private static bool IsProtocolReady()
        {
            int[] players = Shared.ActivePlayerHelper.GetActivePlayerIds();
            if (players.Length == 0) return false;
            return rootedSettings != null && rootedSettings.System_ArePerPlayerSettingsReady(players, out _);
        }
        private void OnDestroy() { Shared.DebugLogHelper.LogDebug(Logger, "Plugin component destroyed during startup; keeping Extreme Powers hooks and settings rooted."); }
        private void ApplySettings()
        {
            try
            {
                demoHandle?.Dispose(); demoHandle = null;
                if (!Settings.Enabled) { client.RestoreVanilla(); return; }
                client.Apply(Settings); if (Settings.EnableGoldReplacement) demoHandle = client.InstallGoldDemo(Settings);
            }
            catch (Exception ex)
            {
                // Invalid live input must not leave a partially configured native backend active.
                demoHandle?.Dispose(); demoHandle = null;
                client.RestoreVanilla();
                Shared.DebugLogHelper.LogError(Logger, "Extreme Powers settings were rejected; Vanilla tuning restored: " + ex);
            }
        }
    }
}
