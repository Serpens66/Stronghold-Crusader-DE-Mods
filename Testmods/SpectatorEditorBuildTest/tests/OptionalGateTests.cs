using System;
using System.Collections.Generic;
using SpectatorEditorBuildTest;

namespace BepInEx.Bootstrap
{
    public static class Chainloader { public static Dictionary<string, Plugin> PluginInfos = new Dictionary<string, Plugin>(); }
    public sealed class Plugin { public object Instance; public Metadata Metadata = new Metadata(); }
    public sealed class Metadata { public Version Version = new Version(1,0,114); }
}
namespace Shared
{
    public static class GameplayModActivationGate { public static bool Allowed=true; internal static bool IsEnabled(bool enabled)=>enabled&&Allowed; }
}
namespace Fixture
{
    public sealed class Settings { public bool EnableMod {get;set;}=true; }
    public sealed class Model
    {
        public Action Toggle;public bool Visible,Automatic,Bridge,Fail;public int Shows;public object IconImageSource {get;private set;}
        public Model(Action toggle) {Toggle=toggle;}
        public void SetIcon(object icon) {IconImageSource=icon;}
        public void Show(bool automatic,bool bridge) {Shows++;if(Fail)throw new InvalidOperationException("broken provider");Visible=true;Automatic=automatic;Bridge=bridge;}
        public void Hide() {Visible=false;}
    }
    public sealed class GateRuntime
    {
        private bool automationReady=true,mapActive=true,loadedMapStatePending=false;
        private Settings settings=new Settings();
        private HashSet<int> manual=new HashSet<int>();
        public object ButtonViewModel {get;}=new Model(()=>{});
        public int Player,Global,Calls; public bool Reject;
        private bool IsManualOnlyBuilding(int building)=>manual.Contains(building);
        private void ApplyManualState(int player,int global,bool automatic,string source)
        {Player=player;Global=global;Calls++;if(Reject)return;if(automatic)manual.Remove(global-1000);else manual.Add(global-1000);}
        public void SetReady(bool ready){automationReady=ready;}
        public void SetActive(bool active){mapActive=active;}
        public void SetLoading(bool loading){loadedMapStatePending=loading;}
        public void SetEnabled(bool enabled){settings.EnableMod=enabled;}
    }
    public sealed class Owner {private GateRuntime gatehouseAutomationRuntime;public Owner(GateRuntime value){gatehouseAutomationRuntime=value;} }
    public sealed class Root {private Owner runtime;public Root(GateRuntime value){runtime=new Owner(value);} }
    public static class Tests
    {
        private static int checks;
        private static void Expect(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
        public static int Main()
        {
            try
            {
                GateBindingFixture.Run();
                Expect(!new OptionalGateAutomation(()=>{}).Ready,"optional absent");
                var runtime=new GateRuntime();var plugin=new BepInEx.Bootstrap.Plugin{Instance=new Root(runtime)};
                BepInEx.Bootstrap.Chainloader.PluginInfos.Add("ExtraFeatures_Serp",plugin);
                int clicks=0;var adapter=new OptionalGateAutomation(()=>clicks++);
                Expect(adapter.Ready,"audited provider ready");
                adapter.Refresh(21,false);var model=(Model)adapter.Model;
                Expect(model.Visible&&model.Automatic&&!model.Bridge,"gate automatic presentation");
                model.Toggle();Expect(clicks==1,"button invokes test action only");
                adapter.Apply(8,21,1021,false);
                Expect(runtime.Player==8&&runtime.Global==1021&&runtime.Calls==1,"explicit AI/global passed once");
                adapter.Refresh(21,true);Expect(!model.Automatic&&model.Bridge,"bridge shares authoritative state");
                Expect(adapter.Automatic(22),"other building unaffected");
                adapter.Apply(8,21,1021,true);Expect(adapter.Automatic(21),"restore automatic");
                runtime.SetLoading(true);Expect(!adapter.Ready,"save pending blocked");runtime.SetLoading(false);
                runtime.SetActive(false);Expect(!adapter.Ready,"inactive map blocked");runtime.SetActive(true);
                runtime.SetReady(false);Expect(!adapter.Ready,"native provider unavailable");runtime.SetReady(true);
                runtime.SetEnabled(false);adapter.Refresh(21,false);Expect(!adapter.Ready&&!model.Visible,"disabled provider hidden");runtime.SetEnabled(true);
                Shared.GameplayModActivationGate.Allowed=false;Expect(!adapter.Ready,"provider mode gate preserved");Shared.GameplayModActivationGate.Allowed=true;
                runtime.Reject=true;bool rejected=false;try{adapter.Apply(8,21,1021,false);}catch(InvalidOperationException){rejected=true;}
                Expect(rejected,"provider refusal not reported as success");
                model.Fail=true;int errors=0;int calls=model.Shows;
                for(int frame=0;frame<1000;frame++)
                    try {adapter.Refresh(21,false);} catch {errors++;}
                Expect(errors==1&&model.Shows==calls+1,"broken provider is invoked and reported only once across 1000 frames");
                Expect(!adapter.Ready&&!model.Visible,"failed optional button hidden and commands disabled");
                plugin.Metadata.Version=new Version(1,0,115);rejected=false;try{new OptionalGateAutomation(()=>{});}catch(NotSupportedException){rejected=true;}
                Expect(rejected,"unknown internal version rejected");
                Console.WriteLine("PASS: "+checks+" production ExtraFeatures adapter checks.");return 0;
            }
            catch(Exception e){Console.Error.WriteLine(e);return 1;}
        }
    }
}
