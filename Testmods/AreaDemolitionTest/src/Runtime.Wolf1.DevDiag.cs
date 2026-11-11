using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Shared;
using UnityEngine;

namespace AreaDemolitionTest
{
    // Temporary, request-gated acceptance bridge. No device input or release-build entry point.
    internal sealed partial class Runtime
    {
        private static Runtime wolfObserver;
        private volatile bool wolfRun;
        private bool wolfPointerActive,wolfHeld;
        private int wolfDownFrame=-1,wolfUpFrame=-1,wolfOriginalAction;
        private Vector3 wolfPoint;
        private ushort wolfFilters;
        private readonly object wolfSync=new object();
        private readonly Dictionary<string,DemolitionTarget> wolfSent=new Dictionary<string,DemolitionTarget>();
        private readonly Dictionary<string,DemolitionTarget> wolfSubmitted=new Dictionary<string,DemolitionTarget>();
        private readonly Dictionary<string,ApplyResult> wolfResults=new Dictionary<string,ApplyResult>();
        private DemolitionTarget[] wolfRed=Array.Empty<DemolitionTarget>();
        private DemolitionTarget[] wolfSentSnapshot=Array.Empty<DemolitionTarget>(),wolfSubmittedSnapshot=Array.Empty<DemolitionTarget>();
        private ApplyResult[] wolfResultSnapshot=Array.Empty<ApplyResult>();
        private readonly ConcurrentQueue<string> wolfRecords=new ConcurrentQueue<string>();
        private int wolfPacket;
        private readonly ConcurrentQueue<Wolf1Timing> wolfTimingQueue=new ConcurrentQueue<Wolf1Timing>();
        private readonly Dictionary<string,Wolf1Timing> wolfTimingTotals=new Dictionary<string,Wolf1Timing>();

        partial void DiagRegister()
        {
            string request=Path.Combine(Paths.ConfigPath,"SerpAutomation",Plugin.Guid+".run");
            if(!File.Exists(request))return;
            DevDiag.Attach(log);
            string[] lines=File.ReadAllLines(request);
            if(lines.Length!=2 || !Guid.TryParseExact(lines[0],"N",out Guid runId) || runId==Guid.Empty || lines[1]!="wolf1-walls" && lines[1]!="wolf1-combined")
            {DevDiag.Log(()=>"wolf1: rejected malformed or unrelated automation request");return;}
            File.Delete(request);
            wolfObserver=this;wolfRun=true;
            var driver=new Wolf1Driver { Begin=WolfBegin,Move=WolfMove,Release=WolfRelease,Cancel=WolfCancel,Read=WolfRead,Reset=WolfReset,
                Idle=()=>!gesture.Owned && !finalCaptureRequested && pendingCommit==null && !network.IsBusy,Expected=(walls,owner,mask)=>native.DiagExpected(walls,owner,mask) };
            try
            {
                InstallWolfTiming();
                renderer.Presented+=WolfPresented;
                Wolf1Scenario.Start(api,log,runId,driver,()=>{WolfCancel();wolfRun=false;},lines[1]);
            }
            catch(Exception error)
            {wolfRun=false;WolfCancel();DevDiag.Log(()=>"wolf1: initialization failed "+error);throw;}
        }

        partial void DiagPointer(ref Vector3 point){if(wolfPointerActive)point=wolfPoint;}
        partial void DiagMouseButton(int button,int phase,ref bool value)
        {
            if(!wolfPointerActive)return;
            value=button==0 && (phase==1?wolfDownFrame==Time.frameCount:phase==2?wolfUpFrame==Time.frameCount:wolfHeld);
        }
        partial void DiagShift(ref bool value){if(wolfPointerActive)value=false;}
        partial void DiagFocus(ref bool value){if(wolfPointerActive)value=true;}
        partial void DiagFreshPointer(ref bool fresh){if(wolfPointerActive)fresh=true;}
        partial void DiagFilters(ref ushort filters){if(wolfPointerActive)filters=wolfFilters;}

        private void WolfBegin(Vector2 point,ushort filters)
        {
            if(!wolfRun || mission==null || mission.Mode.IsRealMultiplayer)throw new InvalidOperationException("Wolf1 requires an explicit local test run.");
            if(wolfPointerActive || gesture.Owned || network.IsBusy)throw new InvalidOperationException("Another gesture or demolition is active.");
            wolfOriginalAction=MainControls.instance.CurrentAction;
            MainControls.instance.CurrentAction=BulldozerAction; // Actual public setter, verified against installed Assembly-CSharp.
            wolfPointerActive=wolfHeld=true;wolfFilters=filters;wolfPoint=new Vector3(point.x,point.y,0);
            wolfDownFrame=Time.frameCount;wolfUpFrame=-1;
            HandlePrimary(EditorDirector.instance,1);
            AfterInput(EditorDirector.instance);
            Wake();
        }
        private void WolfMove(Vector2 point)
        {
            if(!wolfPointerActive || !wolfHeld)throw new InvalidOperationException("No diagnostic pointer gesture.");
            wolfPoint=new Vector3(point.x,point.y,0);
            AfterInput(EditorDirector.instance);Wake();
        }
        private void WolfRelease(Vector2 point)
        {
            if(!wolfPointerActive || !wolfHeld)throw new InvalidOperationException("No diagnostic pointer gesture.");
            wolfPoint=new Vector3(point.x,point.y,0);wolfHeld=false;wolfUpFrame=Time.frameCount;
            HandlePrimary(EditorDirector.instance,3);AfterInput(EditorDirector.instance);Wake();
        }
        private void WolfCancel()
        {
            if(wolfPointerActive)
            {
                CancelSelection();
                if(network.IsBusy)network.CancelPending();
                if(MainControls.instance!=null && MainControls.instance.CurrentAction==BulldozerAction)MainControls.instance.CurrentAction=wolfOriginalAction;
            }
            wolfPointerActive=wolfHeld=false;wolfDownFrame=wolfUpFrame=-1;Wake();
        }
        private void WolfReset()
        {
            if(gesture.Owned || network.IsBusy || finalCaptureRequested || pendingCommit!=null)throw new InvalidOperationException("Cannot reset an active diagnostic operation.");
            lock(wolfSync)
            {
                wolfSubmitted.Clear();wolfSent.Clear();wolfResults.Clear();wolfRed=Array.Empty<DemolitionTarget>();wolfPacket=0;
                wolfSentSnapshot=wolfSubmittedSnapshot=Array.Empty<DemolitionTarget>();wolfResultSnapshot=Array.Empty<ApplyResult>();
            }
            while(wolfRecords.TryDequeue(out _)){}
            while(wolfTimingQueue.TryDequeue(out _)){}wolfTimingTotals.Clear();
        }
        private void WolfPresented(DemolitionTarget[] snapshot)
        {if(wolfRun)lock(wolfSync)if(!ReferenceEquals(wolfRed,snapshot))wolfRed=snapshot??Array.Empty<DemolitionTarget>();}
        private Wolf1State WolfRead()
        {
            while(wolfRecords.TryDequeue(out string record)){string copy=record;DevDiag.Log(()=>copy);}
            while(wolfTimingQueue.TryDequeue(out var timing))
            {
                if(!wolfTimingTotals.TryGetValue(timing.Name,out var total))wolfTimingTotals.Add(timing.Name,total=new Wolf1Timing { Name=timing.Name });
                total.Calls++;total.Elapsed+=timing.Elapsed;total.Maximum=Math.Max(total.Maximum,timing.Elapsed);
            }
            lock(wolfSync)return new Wolf1State {SessionId=mission?.SessionId??0,Actor=actor,Busy=network.IsBusy,FinalPending=finalCaptureRequested,
                PendingReceipt=pendingCommit!=null,GestureOwned=gesture.Owned,Stage=previewPhase.ToString(),
                ProcessedRoots=previewQuote?.NextRoot??0,QuoteRoots=previewQuote?.OrderedRoots?.Length??0,
                RedTargets=wolfRed,SubmittedTargets=wolfSubmittedSnapshot,SentTargets=wolfSentSnapshot,Results=wolfResultSnapshot,
                Status=network.Status,Timings=string.Join(";",wolfTimingTotals.Values.OrderBy(t=>t.Name).Select(t=>t.Name+":"+t.Calls+":"+Milliseconds(t.Elapsed)+":"+Milliseconds(t.Maximum)))};
        }
        private static string Milliseconds(long ticks)=>(ticks*1000d/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        private static DemolitionTarget[] CopyWolfTargets(IEnumerable<DemolitionTarget> targets)=>(targets??Array.Empty<DemolitionTarget>()).Where(t=>t!=null).Select(t=>t.Copy()).ToArray();

        private void InstallWolfTiming()
        {
            var harmony=new Harmony(Plugin.Guid+".wolf1-diagnostic");
            foreach(var item in new[]{new {Type=typeof(Runtime),Names=new[]{"BeginPreview","PreparePreview","AdvancePreview"}},
                new {Type=typeof(SelectionRenderer),Names=new[]{"SetSelection","BeforeCamera","AfterCamera"}},
                new {Type=typeof(NativeDemolition),Names=new[]{"MakeBatches"}}})
                foreach(string name in item.Names)
                {
                    var method=item.Type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                    if(method==null)throw new MissingMethodException(item.Type.Name,name);
                    harmony.Patch(method,new HarmonyMethod(typeof(Runtime),nameof(WolfTimingBegin)),new HarmonyMethod(typeof(Runtime),nameof(WolfTimingEnd)));
                }
            harmony.Patch(typeof(DemolitionNetwork).GetMethod("Submit",BindingFlags.Instance|BindingFlags.NonPublic),new HarmonyMethod(typeof(Runtime),nameof(WolfSubmit)));
            harmony.Patch(typeof(NativeDemolition).GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic),
                new HarmonyMethod(typeof(Runtime),nameof(WolfApplyBegin)),new HarmonyMethod(typeof(Runtime),nameof(WolfApplyEnd)));
            harmony.Patch(typeof(NativeDemolition).GetMethod("Permitted",BindingFlags.Instance|BindingFlags.NonPublic),postfix:new HarmonyMethod(typeof(Runtime),nameof(WolfPermission)));
            harmony.Patch(typeof(NativeDemolition).GetMethod("ClosureOwn",BindingFlags.Instance|BindingFlags.NonPublic),postfix:new HarmonyMethod(typeof(Runtime),nameof(WolfClosure)));
            harmony.Patch(typeof(NativeDemolition).GetMethod("SameState",BindingFlags.Static|BindingFlags.NonPublic),postfix:new HarmonyMethod(typeof(Runtime),nameof(WolfSameState)));
            // Permanent managed diagnostic patches: callbacks become no-ops after the explicit run.
        }
        private static void WolfTimingBegin(MethodBase __originalMethod,out Wolf1Timing __state)
        {
            var observer=wolfObserver;
            __state=observer?.wolfRun==true?new Wolf1Timing {Name=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name,Elapsed=Stopwatch.GetTimestamp()}:null;
        }
        private static void WolfTimingEnd(Wolf1Timing __state)
        {
            var observer=wolfObserver;if(__state==null || observer?.wolfRun!=true)return;
            __state.Elapsed=Stopwatch.GetTimestamp()-__state.Elapsed;
            if(observer.wolfTimingQueue.Count<4096)observer.wolfTimingQueue.Enqueue(__state);
        }
        private static void WolfSubmit(DemolitionTarget[] targets)
        {
            var observer=wolfObserver;if(observer?.wolfRun!=true)return;
            lock(observer.wolfSync)
            {
                foreach(var target in CopyWolfTargets(targets))observer.wolfSubmitted[target.Identity]=target;
                observer.wolfSubmittedSnapshot=observer.wolfSubmitted.Values.ToArray();
            }
        }
        private static void WolfPermission(DemolitionTarget target,int actor,bool __result)
        {if(!__result)WolfRejection("native-permission actor="+actor,target);}
        private static void WolfClosure(DemolitionTarget root,bool __result)
        {if(!__result)WolfRejection("closure",root);}
        private static void WolfSameState(DemolitionTarget expected,DemolitionTarget current,bool __result)
        {
            var observer=wolfObserver;if(__result || observer?.wolfRun!=true || expected==null)return;
            observer.wolfRecords.Enqueue("wolf1 guard=identity expected="+expected.Identity+" expectedOwner="+expected.Owner+" expectedType="+expected.Type+
                " expectedFlags="+expected.Flags+" live="+current?.Identity+" liveOwner="+current?.Owner+" liveType="+current?.Type+" liveFlags="+current?.Flags);
        }
        private static void WolfRejection(string reason,DemolitionTarget target)
        {
            var observer=wolfObserver;if(observer?.wolfRun!=true || target==null)return;
            observer.wolfRecords.Enqueue("wolf1 guard="+reason+" identity="+target.Identity+" kind="+target.Kind);
            if(target.Kind==TargetKind.Wall)
                foreach(string row in observer.native.DiagWallStates(new[]{target}))observer.wolfRecords.Enqueue("wolf1 guard-state "+row);
        }
        private static void WolfApplyBegin(DemolitionTarget[] targets,out Wolf1Timing __state)
        {
            var observer=wolfObserver;__state=null;if(observer?.wolfRun!=true)return;
            __state=new Wolf1Timing {Name="NativeDemolition.Apply"};
            // Local Wolf1 runs: reaching the real receiver is evidence that this batch was submitted.
            string[] before=observer.native.DiagWallStates(targets);
            lock(observer.wolfSync)
            {
                int packet=++observer.wolfPacket;
                foreach(var target in CopyWolfTargets(targets))observer.wolfSent[target.Identity]=target;
                observer.wolfSentSnapshot=observer.wolfSent.Values.ToArray();
                observer.wolfRecords.Enqueue("wolf1 packet="+packet+" receivedCount="+targets.Length+" wallCount="+before.Length);
                foreach(string record in before)observer.wolfRecords.Enqueue("wolf1 packet="+packet+" before "+record);
            }
            __state.Elapsed=Stopwatch.GetTimestamp();
        }
        private static void WolfApplyEnd(ApplyResult[] __result,Wolf1Timing __state)
        {
            WolfTimingEnd(__state);
            var observer=wolfObserver;if(observer?.wolfRun!=true || __result==null)return;
            DemolitionTarget[] sent;
            lock(observer.wolfSync)sent=observer.wolfSentSnapshot.Where(t=>__result.Any(r=>r.Identity==t.Identity)).ToArray();
            string[] after=observer.native.DiagWallStates(sent);
            lock(observer.wolfSync)
            {
                foreach(var result in __result)observer.wolfResults[result.Identity]=new ApplyResult {Identity=result.Identity,Status=result.Status,Detail=result.Detail};
                observer.wolfResultSnapshot=observer.wolfResults.Values.ToArray();
                foreach(string record in after)observer.wolfRecords.Enqueue("wolf1 packet="+observer.wolfPacket+" after "+record);
            }
        }
    }

    internal sealed class Wolf1Timing {internal string Name;internal long Calls,Elapsed,Maximum;}
}
