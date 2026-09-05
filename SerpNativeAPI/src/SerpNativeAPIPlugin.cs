using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SerpNativeAPI
{
    /// <summary>BepInEx host for the process-wide Serp native API.</summary>
    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SerpNativeAPIPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        /// <summary>Stable BepInEx plugin GUID.</summary>
        public const string PluginGuid = "SerpNativeAPI_Serp";
        /// <summary>Display name of the API plugin.</summary>
        public const string PluginName = "Serp Native API";
        /// <summary>Current API plugin version.</summary>
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            NativeApiLog.Info(Logger, $"{PluginName} {PluginVersion} loaded; awaiting CrusaderLibrary.LibraryLoaded.");
            // The Script Extender event roots this plugin's native initialization after BepInEx
            // destroys its short-lived manager object. Native process state is never torn down here.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
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
            SerpNativeApiRuntime.ProcessInstance.Initialize(
                context.ModuleHandle.ToInt64(),
                context.Memory,
                hash,
                new ProcessNativeMemory(),
                new ScriptExtenderSelectedUnitCommandEventSource(),
                Logger);
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
