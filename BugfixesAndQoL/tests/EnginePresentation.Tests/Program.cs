using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using BugfixesAndQoL.VanillaPerformance;

// Only external publishers/native exports are doubles. The production patches,
// cache, mailbox, IL bridge and DevDiag implementation are compiled by source link.
namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public readonly List<string> Lines = new List<string>();
        public void LogMessage(object value) { lock (Lines) Lines.Add(value.ToString()); }
    }
}
namespace R3
{
    public sealed class Subject
    {
        private readonly List<Action<object>> actions = new List<Action<object>>();
        public IDisposable Subscribe(Action<object> action) { actions.Add(action); return null; }
        public void Publish() { foreach (var a in actions) a(null); }
    }
}
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, string value) { log?.LogMessage(value); }
        internal static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, string value) { log?.LogMessage(value); }
    }
    internal static class MissionEvents { internal static readonly R3.Subject Loading = new R3.Subject(), Ended = new R3.Subject(); }
    internal static class GameplaySessionLifecycle
    {
        internal static Action<object> Start;
        internal static IDisposable SubscribeStarted(BepInEx.Logging.ManualLogSource log, Action<object> action) { Start = action; return null; }
    }
}
public sealed class MemoryBuffers
{
    public static readonly MemoryBuffers instance = new MemoryBuffers();
    public static bool Available = true;
    [MethodImpl(MethodImplOptions.NoInlining)] public object getFreeBuffer(bool writing) => Available ? new object() : null;
    [MethodImpl(MethodImplOptions.NoInlining)] public void returnBuffer(object buffer) { EngineInterface.Publications++; }
}
public sealed class EditorDirector
{
    public static readonly EditorDirector instance = new EditorDirector();
    [MethodImpl(MethodImplOptions.NoInlining)] public void preDLLCallActions(ref int a, ref int b) { a++; b++; }
}
public static class EngineInterface
{
    private static readonly object threadLock = new object();
    public static readonly List<string> Writes = new List<string>();
    public static string Saved;
    public static int Food, Wood, Weapons, MeritReads, NativeCalls, Publications, Seed = 7;
    public static bool ThrowMerit, ThrowAchievement, ThrowNative, ThrowSave, SafeSave = true;
    public static Action NativeAction, AchievementAction, MeritAction;
    public static object Lock => threadLock;
    [MethodImpl(MethodImplOptions.NoInlining)] public static int[,] GetMeritData()
    { lock (threadLock) return DLL_GetMeritData(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public static int[,] DLL_GetMeritData()
    { MeritReads++; MeritAction?.Invoke(); if (ThrowMerit) throw new ApplicationException("merit-original"); return new int[,] { { Seed, 2 } }; }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void SetAchData(int food, int wood, int weapons)
    { lock (threadLock) DLL_SetAchValues(food, wood, weapons); }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void DLL_SetAchValues(int food, int wood, int weapons)
    {
        if (!Monitor.IsEntered(threadLock)) throw new Exception("Native write outside engine lock");
        AchievementAction?.Invoke();
        if (ThrowAchievement) throw new ApplicationException("achievement-original");
        Food = food; Wood = wood; Weapons = weapons; Writes.Add(food+"/"+wood+"/"+weapons);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public static int DLL_RunTick(bool skip)
    { NativeCalls++; NativeAction?.Invoke(); if (ThrowNative) throw new ApplicationException("run-original"); return skip ? 19 : 23; }
    [MethodImpl(MethodImplOptions.NoInlining)] public static object CopyPlayStateStruct() => new object();
    [MethodImpl(MethodImplOptions.NoInlining)] public static int run(bool skip)
    {
        object buffer = MemoryBuffers.instance.getFreeBuffer(true);
        if (buffer == null) return 0;
        lock (threadLock)
        {
            int a = 0, b = 0; EditorDirector.instance.preDLLCallActions(ref a, ref b);
            int value = DLL_RunTick(skip); CopyPlayStateStruct();
            MemoryBuffers.instance.returnBuffer(buffer); return value;
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool SaveSaveGame(string path="",int x=0,int y=0,int sx=0,int sy=0,bool locked=false,bool temporary=false,bool map=false)
    { if (!SafeSave) return false; lock (threadLock) return DLL_SaveSaveGame(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool DLL_SaveSaveGame()
    { if(ThrowSave)throw new ApplicationException("save-original"); Saved = Food+"/"+Wood+"/"+Weapons; return true; }
}
internal static class Program
{
    private static int checks;
    private static bool foreignSkip;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    private static readonly BepInEx.Logging.ManualLogSource Log = new BepInEx.Logging.ManualLogSource();
    private static void Start()
    { Shared.GameplaySessionLifecycle.Start(null); EnginePresentationBridge.SetEnabled(true); MeritDataLockOptimization.SetEnabled(true); }
    private static void Busy(Action main)
    {
        using (var ready = new ManualResetEventSlim()) using (var done = new ManualResetEventSlim())
        {
            var holder = new Thread(() => { lock (EngineInterface.Lock) { ready.Set(); done.Wait(); } });
            holder.Start(); Check(ready.Wait(5000), "holder startup");
            try { main(); } finally { done.Set(); Check(holder.Join(5000), "holder completion"); }
        }
    }
    private static Exception Try(Action action) { try { action(); return null; } catch (Exception ex) { return ex; } }
    public static int Main()
    {
        try
        {
            EnginePresentationBridge.Install(Log); MeritDataLockOptimization.Install(Log); Start();
            Check(EnginePresentationBridge.Active, "bridge installation: "+string.Join(" | ", Log.Lines));
            Merit(); Achievements(); ConcurrentMailbox(); Il();
#if SERP_DEVDIAG
            Batch();
#endif
            Console.WriteLine("PASS engine presentation: "+checks+" assertions; real Harmony prefixes/finalizers, run/save IL, races, sessions and PerfProbe composition."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Merit()
    {
        int[,] first = EngineInterface.GetMeritData(); first[0,0] = 99;
        var session = EnginePresentationBridge.Current;
        Check(session.MeritCache.Data[0,0] == 7, "first clone isolation");
        session.MeritCache = new MeritDataLockOptimization.Snapshot(session, new int[,]{{11,2}}, Stopwatch.GetTimestamp()-Stopwatch.Frequency*4);
        long stamp = session.MeritCache.Stamp; int reads = EngineInterface.MeritReads;
        Busy(() =>
        {
            var watch = Stopwatch.StartNew(); int[,] result = EngineInterface.GetMeritData();
            Check(watch.ElapsedMilliseconds < 100 && result[0,0] == 11, "expired snapshot must not block");
            result[0,0] = 50;
            Check(session.MeritCache.Data[0,0] == 11 && session.MeritCache.Stamp == stamp, "served clone/age isolation");
            Check(EngineInterface.MeritReads == reads && session.MeritRequested == 1, "refresh requested without native read");
        });
        EngineInterface.Seed = 29; EngineInterface.run(false);
        Check(session.MeritCache.Data[0,0] == 29 && EngineInterface.MeritReads == reads+1, "sim refresh before publication");
        session.MeritRequested=1; reads=EngineInterface.MeritReads;
        EngineInterface.run(false);
        Check(session.MeritRequested==1 && EngineInterface.MeritReads==reads,"refresh request retained during one-second throttle");
        session.MeritLastAttempt-=Stopwatch.Frequency*2; EngineInterface.run(false);
        Check(session.MeritRequested==0 && EngineInterface.MeritReads==reads+1,"throttled request refreshed when due");
        EngineInterface.ThrowMerit = true;
        Check(Try(() => EngineInterface.GetMeritData())?.Message == "merit-original", "original exception retained");
        Check(!Monitor.IsEntered(EngineInterface.Lock), "Merit finalizer releases outer monitor on exception");
        EngineInterface.ThrowMerit = false;
        lock (EngineInterface.Lock) { EngineInterface.GetMeritData(); Check(Monitor.IsEntered(EngineInterface.Lock), "reentrant caller ownership retained"); }
        var foreign = new Harmony("fixture.foreign-merit");
        foreign.Patch(typeof(EngineInterface).GetMethod("GetMeritData"), prefix: new HarmonyMethod(typeof(Program), nameof(ForeignPrefix)) { priority = Priority.Last });
        var old = session.MeritCache; foreignSkip = true;
        Check(EngineInterface.GetMeritData()[0,0] == 123 && ReferenceEquals(old, session.MeritCache), "foreign skip does not publish false fresh data");
        Check(!Monitor.IsEntered(EngineInterface.Lock), "foreign skip releases held monitor"); foreignSkip = false;
        Shared.MissionEvents.Loading.Publish(); Start();
        Check(EnginePresentationBridge.Current.MeritCache == null && !ReferenceEquals(session, EnginePresentationBridge.Current), "load/session isolation");
        EngineInterface.MeritAction=()=>{Shared.MissionEvents.Loading.Publish();Shared.GameplaySessionLifecycle.Start(null);};
        EngineInterface.GetMeritData(); EngineInterface.MeritAction=null;
        Check(EnginePresentationBridge.Current.MeritCache==null,"publication racing session retirement cannot seed new map");
        reads=EngineInterface.MeritReads;
        var foreignRead=new Thread(()=>EngineInterface.GetMeritData());foreignRead.Start();Check(foreignRead.Join(5000),"foreign Merit completion");
        Check(EngineInterface.MeritReads==reads+1 && EnginePresentationBridge.Current.MeritCache==null,"foreign reads retain synchronous original without publishing a UI snapshot");
        EngineInterface.GetMeritData();Check(EnginePresentationBridge.Current.MeritCache!=null,"first main read seeds new session");
    }
    private static bool ForeignPrefix(ref int[,] __result)
    { if (!foreignSkip) return true; __result = new int[,]{{123,0}}; return false; }
    private static void Achievements()
    {
        Busy(() =>
        {
            var watch = Stopwatch.StartNew(); EngineInterface.SetAchData(1,2,3); EngineInterface.SetAchData(4,5,6);
            Check(watch.ElapsedMilliseconds < 100 && EnginePresentationBridge.Current.Pending.Food == 4, "complete tuple coalescing without wait");
        });
        MemoryBuffers.Available = false; int calls = EngineInterface.NativeCalls;
        Check(EngineInterface.run(false) == 0 && EngineInterface.NativeCalls == calls && EnginePresentationBridge.Current.Pending != null, "no-buffer does not enter bridge/native");
        MemoryBuffers.Available = true;
        Check(EngineInterface.run(true) == 19 && EngineInterface.Food == 4 && EngineInterface.Wood == 5 && EngineInterface.Weapons == 6, "skip/native preserves tuple and result");
        long firstScope = 0, secondScope = 0;
        EngineInterface.NativeAction = () => firstScope = BugfixesAndQoL.SimulationRunScope.Current; EngineInterface.run(false);
        EngineInterface.NativeAction = () => secondScope = BugfixesAndQoL.SimulationRunScope.Current; EngineInterface.run(false); EngineInterface.NativeAction = null;
        Check(firstScope != 0 && secondScope != 0 && firstScope != secondScope && BugfixesAndQoL.SimulationRunScope.Current == 0,
            "each run marks its native tick with its own scope and clears it before publication");
        Busy(() => EngineInterface.SetAchData(int.MinValue, int.MaxValue, -7));
        EngineInterface.SaveSaveGame(); Check(EngineInterface.Saved == int.MinValue+"/"+int.MaxValue+"/-7", "save sees full original integer tuple");
        Busy(() => EngineInterface.SetAchData(8,9,10));
        var writer = new Thread(() => EngineInterface.SetAchData(40,50,60)); writer.Start(); Check(writer.Join(5000), "foreign writer completion");
        EngineInterface.run(false); Check(EngineInterface.Food == 40 && EnginePresentationBridge.Current.Pending == null, "old mailbox cannot overwrite foreign writer");
        Busy(() => EngineInterface.SetAchData(71,72,73)); EngineInterface.ThrowAchievement = true;
        Check(Try(() => EngineInterface.SaveSaveGame())?.Message == "achievement-original", "failed flush prevents save");
        Check(EnginePresentationBridge.Current.Pending.Food == 71 && !Monitor.IsEntered(EngineInterface.Lock), "failed flush retained and monitor released");
        EngineInterface.ThrowAchievement = false; EngineInterface.SaveSaveGame(); Check(EngineInterface.Saved == "71/72/73", "retry persists exact tuple");
        Busy(()=>EngineInterface.SetAchData(72,73,74));EngineInterface.ThrowAchievement=true;Exception foreignFailure=null;
        var failingWriter=new Thread(()=>foreignFailure=Try(()=>EngineInterface.SetAchData(73,74,75)));
        failingWriter.Start();Check(failingWriter.Join(5000)&&foreignFailure?.Message=="achievement-original","foreign prefix flush preserves transfer exception");
        bool released=Monitor.TryEnter(EngineInterface.Lock);if(released)Monitor.Exit(EngineInterface.Lock);
        Check(released&&EnginePresentationBridge.Current.Pending.Food==72,"foreign prefix exception finalizer releases its held monitor and retains pending data");
        EngineInterface.ThrowAchievement=false;EngineInterface.SaveSaveGame();
        Busy(()=>EngineInterface.SetAchData(74,75,76)); EngineInterface.SafeSave=false;
        Check(!EngineInterface.SaveSaveGame() && EnginePresentationBridge.Current.Pending.Food==74,"unsafe save preserves pending tuple without entering native path");
        EngineInterface.SafeSave=true; EngineInterface.ThrowSave=true;
        Check(Try(()=>EngineInterface.SaveSaveGame())?.Message=="save-original" && !Monitor.IsEntered(EngineInterface.Lock),"native save failure preserves original exception and finally");
        EngineInterface.ThrowSave=false;EngineInterface.SaveSaveGame();Check(EngineInterface.Saved=="74/75/76","drained native values survive failed save");
        Busy(()=>EngineInterface.SetAchData(77,78,79)); var failureSession=EnginePresentationBridge.Current;
        EngineInterface.AchievementAction=()=>{EngineInterface.AchievementAction=null;AchievementDataOptimization.Publish(failureSession,87,88,89);};
        EngineInterface.ThrowAchievement=true;Try(()=>EngineInterface.SaveSaveGame());EngineInterface.ThrowAchievement=false;
        Check(failureSession.Pending.Food==87,"failed older flush cannot replace concurrently published newer tuple");
        EngineInterface.SaveSaveGame();Check(EngineInterface.Saved=="87/88/89","newer tuple retried as complete publication");
        Busy(() => EngineInterface.SetAchData(80,81,82)); EnginePresentationBridge.SetEnabled(false);
        Check(EngineInterface.Food == 80 && EnginePresentationBridge.Current.Pending == null, "disable drains pending");
        EngineInterface.SetAchData(90,91,92); Check(EngineInterface.Food == 90, "disabled path synchronous");
        long disabledScope = 0; EngineInterface.NativeAction = () => disabledScope = BugfixesAndQoL.SimulationRunScope.Current;
        EngineInterface.run(false); EngineInterface.NativeAction = null;
        Check(disabledScope != 0 && BugfixesAndQoL.SimulationRunScope.Current == 0, "run scope is independent of the performance option"); Start();
        Busy(() => EngineInterface.SetAchData(100,101,102)); var retired = EnginePresentationBridge.Current;
        Shared.MissionEvents.Ended.Publish(); Start(); EngineInterface.run(false);
        Check(EngineInterface.Food == 90 && retired.Pending != null, "retired pending tuple never reaches new map");
        EngineInterface.ThrowNative = true;
        Check(Try(() => EngineInterface.run(false))?.Message == "run-original" && !Monitor.IsEntered(EngineInterface.Lock), "native exception/finally preserved");
        EngineInterface.ThrowNative = false;
        int nesting = 0; EngineInterface.NativeAction = () => { if (nesting++ == 0) Check(EngineInterface.run(true) == 19, "nested native result"); };
        Check(EngineInterface.run(false) == 23, "outer native result"); EngineInterface.NativeAction = null;
    }
    private static void ConcurrentMailbox()
    {
        var session=EnginePresentationBridge.Current; Exception error=null;int done=0, observations=0;
        var producer=new Thread(()=>{try{for(int i=0;i<10000;i++)AchievementDataOptimization.Publish(session,i,i+1,i+2);}catch(Exception ex){error=ex;}finally{Volatile.Write(ref done,1);}});
        producer.Start();
        while(Volatile.Read(ref done)==0 || Volatile.Read(ref session.Pending)!=null)
        {
            lock(EngineInterface.Lock){AchievementDataOptimization.Flush(session);
                if(EngineInterface.Wood!=EngineInterface.Food+1 || EngineInterface.Weapons!=EngineInterface.Food+2)throw new Exception("torn achievement tuple");observations++;}
        }
        Check(producer.Join(5000)&&error==null&&observations>0,"10000 concurrent complete-tuple publications/drains");
        Check(EngineInterface.Food==9999&&EngineInterface.Wood==10000&&EngineInterface.Weapons==10001,"latest concurrent tuple retained");
        var old=session; Shared.MissionEvents.Loading.Publish();Start();
        AchievementDataOptimization.Publish(old,-1,-2,-3);lock(EngineInterface.Lock)AchievementDataOptimization.Flush(old);
        Check(EngineInterface.Food==9999,"late work on retired session cannot mutate new map");
    }
    private static void Il()
    {
        var method = typeof(EngineInterface).GetMethod("run");
        var original = PatchProcessor.GetOriginalInstructions(method).ToList();
        var labels=original.Select(i=>i.labels.ToArray()).ToArray();
        var blocks=original.Select(i=>i.blocks.Select(b=>b.blockType.ToString()+"/"+b.catchType).ToArray()).ToArray();
        var transformed=EnginePresentationBridge.Transpile(original,method).ToList();
        Check(transformed.Sum(i=>i.labels.Count)==labels.Sum(l=>l.Length) && transformed.Sum(i=>i.blocks.Count)==blocks.Sum(b=>b.Length),"transformation retains all labels and exception boundaries");
        Check(original.Select((i,n)=>i.labels.SequenceEqual(labels[n])&&i.blocks.Select(b=>b.blockType.ToString()+"/"+b.catchType).SequenceEqual(blocks[n])).All(x=>x),"incoming IL metadata remains unmodified");
        var broken = original.Select(i => new CodeInstruction(i)).ToList();
        broken.RemoveAll(i => i.operand is MethodInfo m && m.Name == "DLL_RunTick");
        var result = EnginePresentationBridge.Transpile(broken, method).ToList();
        Check(result.Count == broken.Count && result.Zip(broken, (a,b) => ReferenceEquals(a,b)).All(x=>x), "rejected IL untouched");
        EnginePresentationBridge.Transpile(original, method).ToList();
        var profiler = new Harmony("PerfProbe_Serp.Timing");
        profiler.Patch(method, transpiler: new HarmonyMethod(typeof(PerfProbe.PerfProbeRunTiming), "SafeTranspile"));
        var parent = PerfProbe.PerfProbeRunTiming.Begin(false);
        Check(EngineInterface.run(false) == 23, "PerfProbe composition preserves original");
        var timing = PerfProbe.PerfProbeRunTiming.Complete(parent);
        Check(timing.NativeCompleted && timing.BufferObserved, "PerfProbe anchors survive bridge");
        MemoryBuffers.Available = false; Check(EngineInterface.run(false) == 0, "combined no-buffer path"); MemoryBuffers.Available = true;
        EngineInterface.ThrowNative=true;parent=PerfProbe.PerfProbeRunTiming.Begin(true);
        Check(Try(()=>EngineInterface.run(true))?.Message=="run-original"&&!Monitor.IsEntered(EngineInterface.Lock),"combined exception preserves final release");
        PerfProbe.PerfProbeRunTiming.Complete(parent);EngineInterface.ThrowNative=false;
    }
#if SERP_DEVDIAG
    private static void Batch()
    {
        Shared.DevDiagRuntime.Attach(Log); int lines = Log.Lines.Count;
        Shared.DevDiag.BeginBatch(); Shared.DevDiag.BeginBatch();
        Shared.DevDiagRuntime.Add("fixture.batch", 3, 17); Shared.DevDiagRuntime.Add("fixture.batch", 2, 8);
        Shared.DevDiagRuntime.Spike("fixture.spike", Stopwatch.Frequency/5, "queued=true");
        Shared.DevDiag.EndBatch(); Check(Log.Lines.Count == lines, "nested batch cannot log within engine scope");
        Shared.DevDiag.EndBatch(); Check(Log.Lines.Count == lines+1 && Log.Lines.Last().Contains("queued=true"), "deferred spike emitted once after scope");
        var type = typeof(Shared.DevDiagRuntime); var flags = BindingFlags.Static|BindingFlags.NonPublic;
        string[] names = (string[])type.GetField("Names",flags).GetValue(null);
        long[] counts = (long[])type.GetField("Calls",flags).GetValue(null), ticks = (long[])type.GetField("Ticks",flags).GetValue(null), max = (long[])type.GetField("MaxTicks",flags).GetValue(null);
        int index = Array.IndexOf(names,"fixture.batch"); Check(counts[index] == 5 && ticks[index] == 25 && max[index] == 17, "batch sums and maximum are exact");
        var threads = Enumerable.Range(0,4).Select(_ => new Thread(() => { for(int i=0;i<10000;i++){ Shared.DevDiag.BeginBatch(); Shared.DevDiagRuntime.Add("fixture.concurrent",1,2); Shared.DevDiag.EndBatch(); } })).ToArray();
        foreach(var t in threads)t.Start();foreach(var t in threads)Check(t.Join(5000),"batch thread completion");
        index = Array.IndexOf(names,"fixture.concurrent"); Check(counts[index] == 40000 && ticks[index] == 80000 && max[index] == 2,"concurrent batch totals");
        type.GetField("spikeLines",flags).SetValue(null,0);lines=Log.Lines.Count;
        Shared.DevDiag.BeginBatch();for(int i=0;i<65;i++)Shared.DevDiagRuntime.Spike("fixture.bounded",1,null);
        Check(Log.Lines.Count==lines,"overloaded batch must not write within scope");Shared.DevDiag.EndBatch();
        Check(Log.Lines.Count==lines+60,"batch spike cap is bounded without losing suppression count");
        index=Array.IndexOf(names,"spikes.suppressed");Check(counts[index]==5,"exact bounded spike suppression");
        Shared.DevDiag.BeginBatch();for(int i=0;i<140;i++)Shared.DevDiagRuntime.Add("fixture.capacity."+i,1,0);Shared.DevDiag.EndBatch();
        Check((int)type.GetField("used",flags).GetValue(null)==128&&(long)type.GetField("rejectedAdds",flags).GetValue(null)>0,"counter capacity loss remains visible");
        var slots=(System.Collections.IDictionary)type.GetField("localSlots",flags).GetValue(null);
        Check(slots.Count<=128,"per-thread counter lookup cache remains bounded");
    }
#endif
}
