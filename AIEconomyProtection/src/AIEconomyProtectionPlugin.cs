using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace AIEconomyProtection
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIEconomyProtectionPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "AIEconomyProtection_Serp";
        public const string PluginName = "AI Economy Protection";
        public const string PluginVersion = "0.5.2";

        private static AIEconomyProtectionHook economyProtectionHook;
        private static AIEconomyProtectionSettings settings;
        private static bool libraryLoadedHandlerRegistered;
        private static bool lobbySettingsRegistered;
        private static bool applicationQuitting;

        public AIEconomyProtectionSettings Settings => settings;

        private void Awake()
        {
            if (settings == null)
                settings = new AIEconomyProtectionSettings();

            if (!libraryLoadedHandlerRegistered)
            {
                libraryLoadedHandlerRegistered = true;
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            }
        }

        private void OnDestroy()
        {
            // SHCDE destroys the BepInEx manager object during startup; runtime hooks
            // must stay alive until the process exits or OnApplicationQuit runs.
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            DisposeHook();
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (applicationQuitting || economyProtectionHook != null)
                return;

            try
            {
                RegisterLobbySettings();

                economyProtectionHook = new AIEconomyProtectionHook(Logger, settings, libraryHandle, memory);
            }
            catch (Exception ex)
            {
                Logger.LogError($"AI economy protection hooks could not be installed. The mod is inactive: {ex}");
            }
        }

        private void RegisterLobbySettings()
        {
            if (lobbySettingsRegistered)
                return;

            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                this,
                PluginGuid,
                settings,
                "ScriptExtenderUI/AIEconomyProtectionSettings.xaml");
            lobbySettingsRegistered = true;
        }

        private void DisposeHook()
        {
            if (!libraryLoadedHandlerRegistered && economyProtectionHook == null)
                return;

            if (libraryLoadedHandlerRegistered)
            {
                libraryLoadedHandlerRegistered = false;
                CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            }

            economyProtectionHook?.Dispose();
            economyProtectionHook = null;
            lobbySettingsRegistered = false;
        }
    }
}
