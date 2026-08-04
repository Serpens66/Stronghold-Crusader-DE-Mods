using System;
using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace AIVPlacementLobby
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIVPlacementLobbyPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "AIVPlacementLobby_Serp";
        public const string PluginName = "AIV Placement Lobby";
        public const string PluginVersion = "0.3.3";

        private static AIVPlacementLobbyRuntime processLifetimeRuntime;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        // The manager GameObject is destroyed during startup, so the runtime stays static.
        private void OnLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                if (processLifetimeRuntime != null)
                    return;
                processLifetimeRuntime = new AIVPlacementLobbyRuntime(Logger);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "AIVPlacementLobbyAivSelectionListHost",
                    processLifetimeRuntime.SelectionList);
                processLifetimeRuntime.Install();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"AIV lobby data-flow setup failed: {ex}");
            }
        }
    }
}
