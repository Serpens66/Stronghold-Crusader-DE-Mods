using System;
namespace R3 { }
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace SHCDESE.API.LowLevel { public class CrusaderLibraryLoadContext { } }
namespace APIShared { }
namespace APIShared.GateRouting.Native {
 public class GateRoutingNativeHooks {
 public static int Requests;
 public static bool TryRequest(string owner,string id,SHCDESE.API.LowLevel.CrusaderLibraryLoadContext c,BepInEx.Logging.ManualLogSource l,out GateRoutingNativeHooks b,out string reason) { Requests++;b=new();reason=null;return true; }
 }
}
namespace Shared {
 sealed class Subscription : IDisposable { public void Dispose() { throw new Exception("Published subscription disposed"); } }
 static class GameplaySessionLifecycle { internal static Action<object> Started; internal static IDisposable SubscribeStarted(object log,Action<object> call) { Started+=call;return new Subscription(); } }
 static class MissionEvents { internal static readonly Event Ended=new(); internal sealed class Event { internal Action<object> Call; internal IDisposable Subscribe(Action<object> call) { Call+=call;return new Subscription(); } } }
 static class DebugLogHelper { internal static void LogInfo(object log,string text) {} }
}
namespace BugfixesAndQoL {
 class BugfixesAndQoLViewModel { internal bool EnableMod=true,EnableAssassinGatehouseClimbFix=true,EnableEnemyGatePathfindingFix=true; }
 static class BugfixesAndQoLPlugin { internal const string PluginGuid="test"; }
}
namespace BugfixesAndQoL.GateRouting {
 sealed class AssassinGateDescentRuntime {
 internal static bool Active; internal static int Creations,Resets;
 internal AssassinGateDescentRuntime(SHCDESE.API.LowLevel.CrusaderLibraryLoadContext c,APIShared.GateRouting.Native.GateRoutingNativeHooks b) { Creations++; }
 internal void SetActive(bool value) { Active=value; }
 internal void ResetCounters() { Resets++; }
 internal void SetObservers(Action<IntPtr,bool> c,Action<int> e) { }
 internal string Summary=>"fixture";
 }
 static class FeatureHarness {
 internal static void Run(Action<bool,string> check) {
 var settings=new BugfixesAndQoLViewModel();
 AssassinGatehouseDescentFeature.Initialize(new(),new(),settings,true);
 check(!AssassinGateDescentRuntime.Active,"menu inactive");
 Shared.GameplaySessionLifecycle.Started(null);
 for(int policy=0;policy<2;policy++)for(int climb=0;climb<2;climb++) {
 settings.EnableEnemyGatePathfindingFix=policy!=0;settings.EnableAssassinGatehouseClimbFix=climb!=0;
 AssassinPathAPI.DirectGatehouseClimbingEnabled=climb!=0;AssassinGatehouseDescentFeature.Refresh();
 check(AssassinGateDescentRuntime.Active==(climb!=0),"actual feature policy="+policy+" climb="+climb);
 }
 AssassinGatehouseDescentFeature.SetObservers(null,null);check(AssassinGateDescentRuntime.Active,"detach diagnosis preserves function");
 settings.EnableMod=false;AssassinGatehouseDescentFeature.Refresh();check(!AssassinGateDescentRuntime.Active,"whole mod disabled");
 settings.EnableMod=true;AssassinGatehouseDescentFeature.Refresh();check(AssassinGateDescentRuntime.Active,"reenable without hooks");
 Shared.MissionEvents.Ended.Call(null);check(!AssassinGateDescentRuntime.Active,"map ended");
 Shared.GameplaySessionLifecycle.Started(null);check(AssassinGateDescentRuntime.Active && AssassinGateDescentRuntime.Resets==2,"second map");
 AssassinGatehouseDescentFeature.Initialize(new(),new(),settings,true);
 check(AssassinGateDescentRuntime.Creations==1 && APIShared.GateRouting.Native.GateRoutingNativeHooks.Requests==1,"single installation");
 }
 }
}
