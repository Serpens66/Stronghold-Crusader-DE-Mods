using System;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using APIShared;
namespace AssassinAttackControlTest
{
    [BepInPlugin(PluginGuid,"Assassin Attack Control Test","0.1.1")]
    [BepInDependency("000shcdese","2.14.0")]
    [BepInDependency("APIShared_Serp","0.4.11")]
    [BepInDependency("BugfixesAndQoL_Serp","1.0.177")]
    [BepInDependency("fixes",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AssassinAttackControlTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid="AssassinAttackControlTest_Serp";
        private static ManualLogSource log;
        private static AssassinAttackControlTestSettings settings;
        private static AttackControlRuntime runtime;
        private static IDisposable started, ended;
        private static bool subscribed;
        private void Awake()
        {
            log=Logger;
            settings=new AssassinAttackControlTestSettings();
            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(this,"Assassin Attack Control Test",settings,
                "ScriptExtenderUI/AssassinAttackControlTestSettings.xaml");
            if (subscribed) return;
            subscribed=true;
            CrusaderLibrary.Instance.LibraryLoaded+=OnLibraryLoaded;
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime!=null) return;
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log,"Assassin Attack Control Test",requireCurrentVersion:true)) return;
                var candidate=new AttackControlRuntime(log,settings,context.ModuleHandle);
                AssassinAttackControlAPI.RegisterGuard(PluginGuid,candidate.BeforeUpdate);
                runtime=candidate; // Root before persistent subscriptions; no teardown of published guard.
                started=Shared.GameplaySessionLifecycle.SubscribeStarted(log,runtime.BeginMap);
                ended=Shared.MissionEvents.Ended.Subscribe(_=>runtime.EndMap());
                GameTimeManagerAPI.Instance.OnTick+=runtime.OnTick;
                log.LogInfo("Assassin Attack Control Test ready; permanent shared hook and deterministic game-thread decisions.");
            }
            catch (Exception ex) { log.LogError("Assassin Attack Control Test unavailable: "+ex); }
        }
    }
}
