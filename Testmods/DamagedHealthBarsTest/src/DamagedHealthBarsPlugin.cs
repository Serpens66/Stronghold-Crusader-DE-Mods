using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace DamagedHealthBarsTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class DamagedHealthBarsPlugin : BaseUnityPlugin
    {
        public const string Guid = "DamagedHealthBarsTest_Serp";
        public const string Name = "Damaged Health Bars Test";
        public const string Version = "0.1.0";

        private static DamagedHealthBarsRuntime runtime;
        private static ManualLogSource log;
        private static bool registered;

        private void Awake()
        {
            log = Logger;
            Info("Loaded; Alt+H starts disabled. Waiting for the native library.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                // The runtime, its delegates, subscriptions, hooks and native flag remain rooted.
                runtime = DamagedHealthBarsRuntime.Install(context, log);
            }
            catch (Exception ex)
            {
                Error("Health-bar feature remains disabled: " + ex);
            }
        }

        internal static void Info(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        internal static void Error(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
