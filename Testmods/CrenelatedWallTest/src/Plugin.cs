using APIShared;
using APIShared.ModSettings;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace CrenelatedWallTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.9.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "CrenelatedWallTest_Serp", Name = "Crenelated Wall Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static WallSettings settings;
        private static WallRuntime runtime;
        private static bool registered;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger; settings = new WallSettings();
            try
            {
                LobbyModSettingsPresetRegistration.Register(this, log, Name, settings,
                    "ScriptExtenderUI/CrenelatedWallTestSettings.xaml");
                CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_BOOT version=" + Version + "; revision=editor-endpoints-20261015; APIShared public services only; permanent local endpoint graphics hook.");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "CRENEL_INIT_FAILED settings: " + ex); }
        }
        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null || !Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, true)) return;
            try
            {
                var candidate = new WallRuntime(log, settings, new NativeAvailability(context, log));
                runtime = candidate; // Published registrations retain this object for process lifetime.
                candidate.Register(ApiShared.ForMod(Guid), context);
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "CRENEL_INIT_FAILED runtime remains inactive: " + ex); }
        }
    }
}
