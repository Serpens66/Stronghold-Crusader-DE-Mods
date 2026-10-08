using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Security.Cryptography;

namespace APIShared
{
    /// <summary>BepInEx host for the process-wide APIShared.</summary>
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    [BepInDependency("scde.sc2-fog-of-war", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class APISharedPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        /// <summary>Stable BepInEx plugin GUID.</summary>
        public const string PluginGuid = "APIShared_Serp";
        /// <summary>Display name of the API plugin.</summary>
        public const string PluginName = "APIShared";
        /// <summary>Current API plugin version.</summary>
        public const string PluginVersion = "0.4.12";

        private void Awake()
        {
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            UnitAccess.InitializeDiagnostics(Logger);
            ApiSharedRuntime.ProcessInstance.InitializeManaged(Logger);
            // The Script Extender event roots this plugin's native initialization after BepInEx
            // destroys its short-lived manager object. Native process state is never torn down here.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            // MainHUD loads before a consumer may request the on-demand repair capability.
            GameXAMLManagerAPI.Instance.RegisterBinding(
                "APISharedRepairExtrasHost", ApiSharedRuntime.ProcessInstance.RepairTooltip);
            SavegameModSettings.Initialize(Logger);
            string hash;
            try
            {
                hash = ComputeInstalledHash();
            }
            catch (Exception ex)
            {
                NativeApiLog.Error(Logger, $"Could not hash the installed CrusaderDE.dll: {ex}");
                hash = string.Empty;
            }
            AssassinPathAPI.Initialize(context.ModuleHandle, context.Memory, context.Region, hash, Logger);
            AssassinAttackControlAPI.Initialize(context.ModuleHandle, context.Region, hash, Logger);
            ApiSharedRuntime.ProcessInstance.Initialize(
                context.ModuleHandle.ToInt64(),
                context.Memory,
                hash,
                new ProcessNativeMemory(),
                Logger,
                nativeRegion: context.Region,
                installAivBuildStep: true);
            // AIBuildDiagnoseTest BEGIN -- context only; no hook or sampling without registration.
            AiBuildDiagnostic.Initialize(context.ModuleHandle.ToInt64(), hash, context.Region, Logger);
            // AIBuildDiagnoseTest END
        }

        private static string ComputeInstalledHash()
        {
            string path = Path.Combine(
                Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data",
                "Plugins",
                "x86_64",
                "CrusaderDE.dll");
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
