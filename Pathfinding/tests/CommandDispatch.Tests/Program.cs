using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using OpCodes=Mono.Cecil.Cil.OpCodes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using APIShared.Patching;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.RuntimeDetour;
using RedBird.Core.Memory;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace Shared { static class DebugLogHelper { public static void LogErrorLimited(object log,string key,string message){throw new Exception(message);} } }
namespace Pathfinding {
 static class PathfindingPlugin { internal const string PluginGuid="tests.pathfinding.boundary"; }
 sealed class AssassinPathfindingRuntime {
  internal int Enters,Leaves;internal object Current;
  internal object EnterCommandDispatch(out int epoch){epoch=1;Enters++;object parent=Current;Current=null;return parent;}
  internal void LeaveCommandDispatch(object parent,int epoch){Leaves++;Current=parent;}
 }
}
internal static class Program {
 static int checks,native,pre,post,actions;static bool initialVeto,lateUnveto,throwNative;
 static TestEvent<TribeIssueOrderWithTargetEventArgs> Events;
 sealed class TestEvent<T> { public void Raise(T value){var args=(TribeIssueOrderWithTargetEventArgs)(object)value;if(args.Phase==EventHookPhase.Post){post++;return;}pre++;args.SkipOriginalFunction=initialVeto;args.ReturnValue=73;if(lateUnveto)args.SkipOriginalFunction=false;} }
 [MethodImpl(MethodImplOptions.NoInlining)]
 static long Publisher(NativePointer<GameTribeManager> manager,int tribe,TribeAICommand command,int first,int second,int extra){
  var args=new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Pre,tribe,command,first,second,extra);
  Events.Raise(args);
  if(!args.SkipOriginalFunction){args.ReturnValue=Original(manager,args.TribeId,args.AICommand,args.TargetValue1,args.TargetValue2,args.a6);var result=new TribeIssueOrderWithTargetEventArgs(EventHookPhase.Post,tribe,command,first,second,extra);result.ReturnValue=args.ReturnValue;Events.Raise(result);args.ReturnValue=result.ReturnValue;}
  return args.ReturnValue;
 }
 static long Original(NativePointer<GameTribeManager> manager,int tribe,TribeAICommand command,int first,int second,int extra){native++;Check(tribe==4&&first==8&&second==9&&extra==10,"effective original arguments");if(throwNative)throw new InvalidOperationException("expected native failure");return 91;}
 [MethodImpl(MethodImplOptions.NoInlining)]
 static long MovePublisher(NativePointer<GameTribeManager> manager,int tribe,int x,int y,short patrol,int newOrder,TribeMoveType mode){Check(Pathfinding.TribeCommandDispatchBoundary.IsDispatching,"move original executes inside actual lifetime wrapper");Check(tribe==4&&x==8&&y==9&&patrol==2&&newOrder==1,"move wrapper preserves effective arguments");return 123;}
 static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 static void Field(string name,object value)=>typeof(Pathfinding.TribeCommandDispatchBoundary).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
 static void Main(string[] args){
  string root=Path.GetFullPath(args[0]);string game=args.Length>1?args[1]:@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
  // Release builds compile against the selected published SDK, not the development checkout.
  string api=args.Length>2?args[2]:Path.Combine(root,"APIShared/BepInEx/plugins/APIShared_Serp");
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{string name=new AssemblyName(e.Name).Name+".dll";foreach(string dir in new[]{api,Path.Combine(game,"BepInEx/core"),Path.Combine(game,"BepInEx/plugins/000shcdese"),Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Managed")}){string f=Path.Combine(dir,name);if(File.Exists(f))return Assembly.LoadFrom(f);}return null;};
  Run();Console.WriteLine("PASS: "+checks+" actual final-Pre IL, composition, veto/unveto and publisher lifetime checks.");
 }
 [MethodImpl(MethodImplOptions.NoInlining)] static void Run(){
  Events=new TestEvent<TribeIssueOrderWithTargetEventArgs>();
  var actual=typeof(BulkTribeDetours).GetMethod("c_game_tribe_issueorder_withtarget_hook_impl");
  var broker=typeof(SafeIlHook).Assembly.GetType("APIShared.Units.AiMilitary.Orders.AiMilitaryOrderBroker",true).GetMethod("TransformTarget",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
  var transform=(ILContext.Manipulator)Delegate.CreateDelegate(typeof(ILContext.Manipulator),broker);
  foreach(bool brokerFirst in new[]{false,true})using(var copy=new DynamicMethodDefinition(actual))using(var il=new ILContext(copy.Definition)){
   il.ReferenceBag=RuntimeILReferenceBag.Instance;if(brokerFirst)il.Invoke(transform);il.Invoke(Pathfinding.TribeCommandDispatchBoundary.TransformAcceptedTarget);if(!brokerFirst)il.Invoke(transform);Check(copy.Generate()!=null,"real SE publisher compiles with API broker in either IL order");
  }
  using(var copy=new DynamicMethodDefinition(actual))using(var il=new ILContext(copy.Definition)){
   var ctor=il.Body.Instructions.First(i=>i.Operand is Mono.Cecil.MethodReference m&&m.Name==".ctor"&&m.DeclaringType.FullName==typeof(TribeIssueOrderWithTargetEventArgs).FullName);var phase=ctor.Previous.Previous.Previous.Previous.Previous.Previous;phase.OpCode=OpCodes.Ldc_I4_1;phase.Operand=null;bool rejected=false;try{Pathfinding.TribeCommandDispatchBoundary.TransformAcceptedTarget(il);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Post-constructed Pre local is rejected before publication");
  }
  var method=typeof(Program).GetMethod("Publisher",BindingFlags.Static|BindingFlags.NonPublic);
  Check(SafeIlHook.TryInstall("tests.pathfinding","tests.finalpre",method,Pathfinding.TribeCommandDispatchBoundary.TransformAcceptedTarget,null,out var accepted,out string reason),"synthetic actual transform publishes: "+reason);
  Field("acceptedHook",accepted);Field("ready",1);
  Pathfinding.TribeCommandDispatchBoundary.BindClimb(a=>{actions++;Check(!a.SkipOriginalFunction&&a.Phase==EventHookPhase.Pre,"only final accepted Pre receives business action");});
  var owner=new Pathfinding.AssassinPathfindingRuntime();Pathfinding.TribeCommandDispatchBoundary.BindAssassin(owner);
  var wrapper=(BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate)Delegate.CreateDelegate(typeof(BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate),typeof(Pathfinding.TribeCommandDispatchBoundary).GetMethod("Target",BindingFlags.Static|BindingFlags.NonPublic));
  var held=(BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate)Delegate.CreateDelegate(typeof(BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate),method);
  IntPtr nativeThunk=IntPtr.Zero;
  bool mono=Type.GetType("Mono.Runtime")!=null;
  if(mono)nativeThunk=Marshal.GetFunctionPointerForDelegate(held);
  else Console.WriteLine("LIMIT: CLR cannot marshal SE NativePointer<T>; native held-thunk probe requires game Mono.");
  var hook=new Hook(method,wrapper,new HookConfig{ManualApply=true});Field("targetHook",hook);Field("targetOriginal",hook.GenerateTrampoline<BulkTribeDetours.c_game_tribe_issueorder_withtarget_delegate>());hook.Apply();
  initialVeto=true;Check(held(default,4,(TribeAICommand)3,8,9,10)==73,"veto ReturnValue survives");Check(native==0&&actions==0&&pre==1&&post==0&&owner.Enters==1&&owner.Leaves==1,"no Post veto cleans wrapper without native or business actions");
  lateUnveto=true;Check(held(default,4,(TribeAICommand)3,8,9,10)==91,"late unveto keeps return");Check(native==1&&actions==1&&pre==2&&post==1&&owner.Leaves==2,"late unveto runs final actions and native exactly once");
  throwNative=true;try{held(default,4,(TribeAICommand)3,8,9,10);throw new Exception("expected failure missing");}catch(InvalidOperationException){}Check(owner.Enters==3&&owner.Leaves==3&&!Pathfinding.TribeCommandDispatchBoundary.IsDispatching,"exception without Post still exits wrapper");
  if(mono){
   throwNative=false;
   var call=new DynamicMethod("CallHeldNativePublisher",typeof(long),new[]{typeof(IntPtr)},typeof(Program),true);var gen=call.GetILGenerator();
   gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);gen.Emit(System.Reflection.Emit.OpCodes.Conv_I);gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_4);gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_3);gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_8);gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4,9);gen.Emit(System.Reflection.Emit.OpCodes.Ldc_I4,10);gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
   gen.EmitCalli(System.Reflection.Emit.OpCodes.Calli,CallingConvention.Cdecl,typeof(long),new[]{typeof(IntPtr),typeof(int),typeof(int),typeof(int),typeof(int),typeof(int)});gen.Emit(System.Reflection.Emit.OpCodes.Ret);
   var invoke=(Func<IntPtr,long>)call.CreateDelegate(typeof(Func<IntPtr,long>));
   lateUnveto=false;int beforeNative=native,beforeActions=actions,beforeEnters=owner.Enters;
   Check(invoke(nativeThunk)==73&&native==beforeNative&&actions==beforeActions&&owner.Enters==beforeEnters+1&&owner.Enters==owner.Leaves,"held-before native thunk enters wrapper on final veto");
   lateUnveto=true;Check(invoke(nativeThunk)==91&&native==beforeNative+1&&actions==beforeActions+1&&owner.Enters==beforeEnters+2&&owner.Enters==owner.Leaves,"held-before native thunk enters final accepted actions on late unveto exactly once");
  }
  var moveMethod=typeof(Program).GetMethod("MovePublisher",BindingFlags.Static|BindingFlags.NonPublic);
  var moveHeld=(BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate)Delegate.CreateDelegate(typeof(BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate),moveMethod);
  var moveWrapper=(BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate)Delegate.CreateDelegate(typeof(BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate),typeof(Pathfinding.TribeCommandDispatchBoundary).GetMethod("Move",BindingFlags.Static|BindingFlags.NonPublic));
  var moveHook=new Hook(moveMethod,moveWrapper,new HookConfig{ManualApply=true});Field("moveHook",moveHook);Field("moveOriginal",moveHook.GenerateTrampoline<BulkTribeDetours.c_game_tribe_issueorder_movehere_delegate>());moveHook.Apply();
  int entered=owner.Enters;object parent=new object();owner.Current=parent;
  Check(moveHeld(default,4,8,9,2,1,default)==123&&owner.Enters==entered+1&&owner.Enters==owner.Leaves&&ReferenceEquals(owner.Current,parent),"actual move wrapper preserves result and parent exactly once");
  GC.KeepAlive(moveHeld);GC.KeepAlive(moveHook);GC.KeepAlive(held);GC.KeepAlive(hook);
 }
}
