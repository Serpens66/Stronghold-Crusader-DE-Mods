using System;
using System.IO;
using APIShared;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.AreaDemolition
{
    // Process-rooted backend and optional local presentation have independent lifetimes.
    internal static class AreaDemolitionFeature
    {
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel model;
        private static DemolitionSettings settings;
        private static ModApiClient api;
        private static DemolitionNetwork network;
        private static IUiFrameWorkRegistration networkWork;
        private static Runtime runtime;
        private static bool registered, installed;
        internal static Runtime Runtime => runtime;
        internal static bool LocalEnabled => model!=null && model.EnableClientFeatures && model.EnableAreaDemolition;

        internal static void Configure(ManualLogSource logger,BugfixesAndQoLViewModel viewModel,ConfigFile config)
        {
            if(log!=null)return;
            log=logger;model=viewModel;api=ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid);
            ConfigFile legacy=null;
            string path=Path.Combine(Paths.ConfigPath,"AreaDemolitionTest_Serp.cfg");
            if(File.Exists(path))
            {
                try {legacy=new ConfigFile(path,false){SaveOnConfigSet=false};}
                catch(Exception error){log.LogWarning(Stamp("Area demolition filter import skipped: "+error.Message));}
            }
            settings=new DemolitionSettings(config,legacy);
            model.SettingChanged+=SettingChanged;
        }
        internal static void RegisterNetwork()
        {
            if(registered)return;registered=true;
            try
            {
                network=new DemolitionNetwork(api,null,log);
                network.Changed+=RequestNetwork;
                if(!api.Ui.TryRegisterBeforeRender("area-demolition.network-pump",PumpNetwork,out networkWork,out string reason))
                    throw new InvalidOperationException(reason);
                RequestNetwork();
            }
            catch(Exception error){log.LogError(Stamp("Area demolition network unavailable: "+error));}
        }
        private static void RequestNetwork()=>networkWork?.Request();
        private static void PumpNetwork()
        {
            try {network.Pump();networkWork.SetContinuous(network.IsBusy);}
            catch(Exception error){networkWork.SetContinuous(false);log.LogError(Stamp("Area demolition protocol stopped: "+error));}
        }
        internal static void Install(CrusaderLibraryLoadContext context)
        {
            if(installed)return;installed=true;
            if(network==null || networkWork==null)return;
            try
            {
                if(!NativeDemolition.TryCreate(context,log,out var native))
                {log.LogError(Stamp("Area demolition unavailable: native contracts rejected; Vanilla input retained."));return;}
                // Backend failures must remain observable even if the optional local UI fails.
                native.DiagnosticProduced+=message=>api.Ui.TryPost(()=>log.LogError(message));
                network.SetNative(native);
                runtime=new Runtime(api,native,settings,network,()=>LocalEnabled,log);
                runtime.Register();
                ManagedInput.Install(runtime,log);
            }
            catch(Exception error)
            {runtime?.DisablePresentation();log.LogError(Stamp("Area demolition local presentation unavailable: "+error));}
        }
        private static void SettingChanged(string property)
        {
            if(property!=nameof(BugfixesAndQoLViewModel.EnableClientFeatures) && property!=nameof(BugfixesAndQoLViewModel.EnableAreaDemolition))return;
            // Publisher-thread safe: CancelPending touches only the locked local protocol
            // queue. No Noesis access occurs until the requested presentation callback.
            LocalDemolitionGate.ApplyChange(LocalEnabled,CancelUnsent,RequestPresentation);
        }
        private static void CancelUnsent()=>network?.CancelPending();
        private static void RequestPresentation()=>runtime?.RequestRefresh();
        internal static string Stamp(string text)=>"["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] "+text;
    }
}
