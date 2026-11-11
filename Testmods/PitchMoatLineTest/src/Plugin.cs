using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace PitchMoatLineTest
{
    [BepInPlugin(Guid, "Pitch and Moat Line Test", Version)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.46.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PitchMoatLineTest_Serp";
        public const string Version = "0.2.0";

        private static ManualLogSource log;
        private static bool attempted;
        private static bool allowMultiplayerTest;

        private void Awake()
        {
            if (log != null)
                return;
            log = Logger;
#if SERP_DEVDIAG
            Shared.DevDiag.Attach(log);
            allowMultiplayerTest = Config.Bind("Test", "AllowUnverifiedMultiplayerTest", false,
                "Use on matching host and client installations only for multiplayer acceptance.").Value;
#else
            allowMultiplayerTest = false;
#endif
            // The plugin GameObject is destroyed during startup. These callbacks are static.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (attempted)
                return;
            attempted = true;
            try
            {
                ManagedBoundary.Install(log);
                if (!NativePlacement.TryCreate(context, log, out var native))
                    throw new InvalidOperationException("The native placement contract could not be initialized.");
                PlacementRuntime.Initialize(ApiShared.ForMod(Guid), native, log, allowMultiplayerTest);
                log.LogInfo(Stamp("PITCH_MOAT_LINE_READY"));
            }
            catch (Exception error)
            {
                log.LogError(Stamp("PITCH_MOAT_LINE_DISABLED: " + error));
            }
        }

        internal static string Stamp(string message) => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
    }
}
