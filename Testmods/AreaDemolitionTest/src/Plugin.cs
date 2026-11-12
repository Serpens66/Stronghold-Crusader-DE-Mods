using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace AreaDemolitionTest
{
    [BepInPlugin(Guid,"Area Demolition Test",Version)]
    [BepInDependency("000shcdese","2.14.3")]
    [BepInDependency("APIShared_Serp","0.49.0")]
    [BepInDependency("fixes",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="AreaDemolitionTest_Serp", Version="0.1.6";
        private static ManualLogSource log;
        private static DemolitionSettings settings;
        private static Runtime runtime;
        private static bool attempted;
        private void Awake()
        {
            if (log!=null) return;
            log=Logger; settings=new DemolitionSettings(Config);
            // Startup destroys the Plugin component. All continuing publishers and targets are process-rooted.
            CrusaderLibrary.Instance.LibraryLoaded+=Loaded;
        }
        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            if (attempted) return; attempted=true;
            try
            {
                if (!NativeDemolition.TryCreate(context,log,out var native))
                { log.LogError(Stamp("AREA_DEMOLITION_DISABLED: native contracts unavailable; Vanilla input retained.")); return; }
                runtime=new Runtime(ApiShared.ForMod(Guid),native,settings,log);
                runtime.Register();
                ManagedInput.Install(runtime,log);
                log.LogInfo(Stamp("AREA_DEMOLITION_READY: process-rooted input, mission and presentation registrations."));
            }
            catch(Exception error) { log.LogError(Stamp("AREA_DEMOLITION_DISABLED: "+error)); }
        }
        internal static string Stamp(string text)=>"["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] "+text;
    }
}
