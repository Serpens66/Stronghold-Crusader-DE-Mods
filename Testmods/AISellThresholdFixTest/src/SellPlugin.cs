using System;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;

namespace AISellThresholdFixTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SellPlugin : BaseUnityPlugin
    {
        public const string Guid = "AISellThresholdFixTest_Serp", Name = "AI Sell Threshold Fix Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static SellRuntime runtime;
        private static bool registered;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger;
            Shared.DevDiag.Attach(log);
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(log, Name + " " + Version + " loaded; always active, all game modes.");
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                // The static publisher and static runtime survive Unity's early plugin cleanup.
                var candidate = new SellRuntime(log, context);
                runtime = candidate;
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                candidate.Publish();
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "Both AI sell fixes inactive: " + ex); }
        }
        private static void OnTick(int tick)
        {
            try { runtime?.OnTick(tick); }
            catch (Exception ex) { Shared.DebugLogHelper.LogErrorLimited(log, "ai-sell-tick", "AI sell support callback failed: " + ex); }
        }
    }
}
