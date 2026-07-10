using BepInEx;
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
        public const string PluginVersion = "0.2.0";

        private AIEconomyProtectionHook economyProtectionHook;
        private bool disposed;

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            DisposeHook();
        }

        private void OnApplicationQuit()
        {
            DisposeHook();
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (disposed || economyProtectionHook != null)
                return;

            try
            {
                economyProtectionHook = new AIEconomyProtectionHook(Logger, libraryHandle, memory);
                Logger.LogInfo("AI building pause prevention and emergency-demolition prevention are active.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"AI economy protection hooks could not be installed. The mod is inactive: {ex}");
            }
        }

        private void DisposeHook()
        {
            if (disposed)
                return;

            disposed = true;
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            economyProtectionHook?.Dispose();
            economyProtectionHook = null;
        }
    }
}
