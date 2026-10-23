using System;
using System.Collections.Generic;
using BugfixesAndQoL;
using BugfixesAndQoL.GateRouting;
using APIShared;
class Program {
 static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static void Main(string[] args){
 bool fixOff=Array.IndexOf(args,"--own-fix-off")>=0;
 BepInEx.Configuration.ConfigFile.Fail=Array.IndexOf(args,"--config-save-fails")>=0;
 var vm=new BugfixesAndQoLViewModel();
 bool foreignBoot=Array.IndexOf(args,"--foreign-host-off")>=0;
 if(foreignBoot){vm.EnableMod=false;vm.EnableEnemyGatePathfindingFix=false;GateAccessRules.TryRegister("boot.foreign","needed",q=>GateAccessDecision.Deny,out _);}
 if(fixOff)vm.EnableEnemyGatePathfindingFix=false;
 GateRoutingFeature.Configure(new BepInEx.Logging.ManualLogSource(),new BepInEx.Configuration.ConfigFile(),vm);
 // Normal builds do not read any development diagnostic configuration. The dedicated
 // build contains both detail migration and Macemen configuration, with isolated errors.
#if SERP_DEVDIAG
 const int diagnosticConfigErrors=2;
#else
 const int diagnosticConfigErrors=0;
#endif
 Check(Shared.DebugLogHelper.Errors==(BepInEx.Configuration.ConfigFile.Fail?diagnosticConfigErrors:0),"config errors are contained and reported");
 GateRoutingFeature.Initialize(new SHCDESE.API.LowLevel.CrusaderLibraryLoadContext(),true);
 if(foreignBoot)
 {
  Check(EnemyGatePathfindingRuntime.Created==1,"foreign provider warms up while own Host is off");
  Shared.GameplaySessionLifecycle.Start.Fire(new Shared.Session());
  Check(GateRoutingFeature.IsActive,"foreign map service starts independently of own Host");
  Check(GateRoutingFeature.IsBlocked(1,1,42,1,0,false),"foreign deny reaches snapshot with H=false");
  Console.WriteLine("PASS: "+checks+" foreign-at-startup H=false checks.");return;
 }
 if(fixOff)
 {
  // Fix off and no foreign rules: no native hooks at library load; enabling later installs lazily.
  Check(EnemyGatePathfindingRuntime.Created==0,"library load without wanted gate routing installs nothing");
  Shared.GameplaySessionLifecycle.Start.Fire(new Shared.Session());
  Check(!GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.Created==0,"map without wanted gate routing stays Vanilla");
  vm.EnableEnemyGatePathfindingFix=true;vm.Changed(nameof(vm.EnableEnemyGatePathfindingFix));
  Check(GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.Created==1&&EnemyGatePathfindingRuntime.Begin==1,"later enabling uses the lazy path once");
  Console.WriteLine("PASS: "+checks+" fix-off orchestration checks (no eager install).");
  return;
 }
 // Native hooks install at library load (user decision 2026-10-20: longer start instead of a
 // first-map-tick freeze); the map stays inactive until a session starts.
 Check(EnemyGatePathfindingRuntime.Created==1&&EnemyGatePathfindingRuntime.Begin==0&&!GateRoutingFeature.IsActive,"library load initializes native service once, map stays inactive");
 Shared.GameplaySessionLifecycle.Start.Fire(new Shared.Session{IsEditor=true});
 Check(GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.Created==1&&EnemyGatePathfindingRuntime.Begin==1,"editor starts once");
 Check(GateRoutingFeature.IsBlocked(2,1,42,1,0,true),"default own rule restricts");
 vm.EnableEnemyGatePathfindingFix=false;vm.Changed(nameof(vm.EnableEnemyGatePathfindingFix));
 Check(!GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.End==1,"off clears runtime");
 int ticks=EnemyGatePathfindingRuntime.Ticks;SHCDESE.API.GameTimeManagerAPI.Instance.Tick();
 UnityEngine.Application.Render();Check(EnemyGatePathfindingRuntime.Ticks==ticks,"inactive does no publisher work");
 vm.EnableEnemyGatePathfindingFix=true;vm.Changed(nameof(vm.EnableEnemyGatePathfindingFix));
 Check(GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.Created==1&&EnemyGatePathfindingRuntime.Begin==2,"reactivation reuses hooks");
 var decision=GateAccessDecision.Allow;
 GateAccessRules.TryRegister("foreign.mod","gate",q=>decision,out _);
 SHCDESE.API.GameTimeManagerAPI.Instance.Tick();
 Check(!GateRoutingFeature.IsBlocked(2,1,42,1,0,true),"contributor removes added restriction");
 decision=GateAccessDecision.Deny;GateAccessRules.Invalidate("foreign.mod","gate");
 SHCDESE.API.GameTimeManagerAPI.Instance.Tick();
 Check(GateRoutingFeature.IsBlocked(1,1,42,1,0,false),"invalidation changes own access");
 vm.EnableEnemyGatePathfindingFix=false;vm.Changed(nameof(vm.EnableEnemyGatePathfindingFix));
 Check(GateRoutingFeature.IsActive,"explicit consumer still served with own rule off");
 vm.EnableMod=false;vm.Changed(nameof(vm.EnableMod));Check(GateRoutingFeature.IsActive,"foreign rule remains served while own Host switch is off");
 Check(GateRoutingFeature.IsBlocked(1,1,42,1,0,false),"foreign deny remains authoritative with H=false");
 decision=GateAccessDecision.Unchanged;GateAccessRules.Invalidate("foreign.mod","gate");SHCDESE.API.GameTimeManagerAPI.Instance.Tick();
 Check(!GateRoutingFeature.IsBlocked(1,1,42,1,0,false),"dormant foreign rule preserves Vanilla with own host off");
 vm.EnableMod=true;vm.Changed(nameof(vm.EnableMod));
 Shared.MissionEvents.Ended.Fire(0);Check(!GateRoutingFeature.IsActive,"map end inactive");
 Shared.GameplaySessionLifecycle.Start.Fire(new Shared.Session());Check(GateRoutingFeature.IsActive&&EnemyGatePathfindingRuntime.Created==1,"map reload no repatch");
 Console.WriteLine("PASS: "+checks+" actual feature orchestration checks; native runtime/SDK are isolated doubles, no hook execution in this fixture.");
 }
}
class EmptyDisposable:IDisposable{public void Dispose(){}}
public class Stream<T>{public IDisposable Subscribe(Action<T> f){Event+=f;return new EmptyDisposable();}event Action<T> Event;public void Fire(T v)=>Event?.Invoke(v);}
namespace R3 {}
namespace SHCDESE.EventAPI {}
namespace SHCDESE.EventAPI.Buildings {static class BuildingR3EventHooks{internal static class OnBuildingCapture{internal static Stream<int> Observable=new Stream<int>();}}}
namespace SHCDESE.EventAPI.Tribes {static class TribeR3EventHooks{internal static class OnTribeIssueOrderWithTarget{internal static Stream<int> Observable=new Stream<int>();}internal static class OnTribeIssueOrderMoveHere{internal static Stream<int> Observable=new Stream<int>();}}}
namespace SHCDESE.EventAPI.Units {static class UnitR3EventHooks{internal static class OnUnitMoveHere{internal static Stream<int> Observable=new Stream<int>();}}}
namespace BepInEx.Logging{class ManualLogSource{}}
namespace BepInEx.Configuration{class ConfigEntry<T>{internal T Value;}class ConfigFile{internal bool SaveOnConfigSet=true;internal static bool Fail;internal void Save(){if(Fail)throw new System.IO.IOException("fixture save failed");} internal ConfigEntry<T> Bind<T>(string a,string b,T v,string d){if(Fail&&b=="MacemenDiagnostics")throw new System.IO.IOException("fixture bind save failed");return new ConfigEntry<T>{Value=v};}}}
namespace BepInEx.Bootstrap{static class Chainloader{internal static Dictionary<string,object> PluginInfos=new Dictionary<string,object>();}}
namespace SHCDESE.API.LowLevel{class CrusaderLibraryLoadContext{}}
namespace SHCDESE.API{class GameTimeManagerAPI{internal static GameTimeManagerAPI Instance=new GameTimeManagerAPI();internal event Action<int> OnTick;internal void Tick()=>OnTick?.Invoke(1);}}
namespace UnityEngine{static class Time{internal static int frameCount;}static class Application{internal static event Action onBeforeRender;internal static void Render(){Time.frameCount++;onBeforeRender?.Invoke();}}}
namespace Shared{class Session{internal bool IsEditor;}static class GameplaySessionLifecycle{internal static Stream<Session> Start=new Stream<Session>();internal static IDisposable SubscribeStarted(object l,Action<Session> a)=>Start.Subscribe(a);}static class MissionEvents{internal static Stream<int> Ended=new Stream<int>();}static class DebugLogHelper{internal static bool DiagnosticsEnabled => false;internal static int Errors;internal static void LogError(object l,string s){if(BepInEx.Configuration.ConfigFile.Fail&&s.Contains("diagnostic config")){Errors++;return;}throw new Exception(s);}internal static void LogInfo(object l,string s){}}}
namespace BugfixesAndQoL{class BugfixesAndQoLViewModel{internal bool EnableMod=true,EnableEnemyGatePathfindingFix=true;internal event Action<string> SettingChanged;internal void Changed(string n)=>SettingChanged?.Invoke(n);}}
namespace BugfixesAndQoL.UnitCommands{class Hook{internal bool Committed=true;}class Owner{internal bool disposed=false;internal Hook pathBuilderDetour=new Hook(),attackApproachFloodBuilderDetour=new Hook(),buildingApproachBuilderDetour=new Hook(),buildingCandidateConsumerDetour=new Hook(),cursorMoveStagerDetour=new Hook(),regionPairReachabilityDetour=new Hook();}static class UnitCommandPathAPI{internal static Owner Runtime=new Owner();}}
namespace BugfixesAndQoL.GateRouting{class EnemyGatePathfindingRuntime{internal static int Created,Begin,End,Ticks;internal EnemyGatePathfindingRuntime(object l,bool d,bool m){Created++;}internal void InitializeNative(object c,bool v){}internal void BeginMap(bool e){Begin++;}internal void EndMap(string s){End++;}internal void OnGameTick(){Ticks++;}internal void ProcessGatePolicies(long now){}internal void ProcessDeferredDiagnostics(){}internal void ObserveBuildingCapture(int a){}internal void ObserveTargetOrder(int a){}internal void ObserveTribeMove(int a){}internal void ObserveUnitMove(int a){}}}
