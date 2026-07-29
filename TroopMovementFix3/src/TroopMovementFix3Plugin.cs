using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace TroopMovementFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility(LegacyPluginGuid)]
    [BepInIncompatibility(Fix2PluginGuid)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TroopMovementFix3Plugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string LegacyPluginGuid = "TroopMovementFix_Serp";
        private const string Fix2PluginGuid = "TroopMovementFix2_Serp";

        public const string PluginGuid = "TroopMovementFix3_Serp";
        public const string PluginName = "Troop Movement Fix 3";
        public const string PluginVersion = "1.2.0";

        private static TroopMovementFix3Runtime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;

        private void Awake()
        {
            ModLog.Debug(
                Logger,
                $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new TroopMovementFix3Runtime(Logger);

            if (!libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private void OnDestroy()
        {
            ModLog.Debug(
                Logger,
                "TroopMovementFix3Plugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and native hooks.");
        }

        private void OnCrusaderLibraryLoaded(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
        {
            try
            {
                persistentRuntime?.Apply(libraryHandle, memory);
                ModLog.Debug(
                    Logger,
                    "Crusader library loaded; Troop Movement Fix 3 runtime initialized.");
            }
            catch (Exception ex)
            {
                ModLog.Error(
                    Logger,
                    $"Troop Movement Fix 3 initialization failed; no partial runtime remains active: {ex}");
            }
        }
    }
}
