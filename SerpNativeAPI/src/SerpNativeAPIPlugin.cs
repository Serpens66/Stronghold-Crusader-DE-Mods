using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.IO;
using System.Security.Cryptography;

namespace SerpNativeAPI
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SerpNativeAPIPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "SerpNativeAPI_Serp";
        public const string PluginName = "Serp Native API";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            NativeApiLog.Info(Logger, $"{PluginName} {PluginVersion} loaded; awaiting CrusaderLibrary.LibraryLoaded.");
            // The Script Extender event roots this plugin's native initialization after BepInEx
            // destroys its short-lived manager object. Native process state is never torn down here.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
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
                libraryHandle.ToInt64(),
                memory,
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
