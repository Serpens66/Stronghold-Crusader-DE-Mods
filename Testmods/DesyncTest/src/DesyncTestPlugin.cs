using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace DesyncTest
{
    [BepInPlugin(Guid,"DesyncTest","0.1.0")]
    [BepInDependency("000shcdese","2.14.3")]
    [BepInDependency("APIShared_Serp","0.16.3")]
    [BepInDependency("fixes",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DesyncTestPlugin:BaseUnityPlugin
    {
        public const string Guid="DesyncTest_Serp";
        private static Recorder recorder;
        private static NativeHooks hooks;
        private static NativeCapture capture;
        private static IDisposable lifecycle;
        private static ManualLogSource log;
        private static SynchronizationContext main;
        private static CaptureOptions options;
        private void Awake()
        {
            log=Logger;main=SynchronizationContext.Current;
            try
            {
                options=new CaptureOptions();
                string mode=Config.Bind("Capture","Mode","Light","Off, Light or Deep. Read on process start; Deep falls back to Light after its time limit.").Value;
                if(!Enum.TryParse(mode,true,out options.Mode))throw new ArgumentException("Mode must be Off, Light or Deep.");
                options.LightMiB=Config.Bind("Limits","LightMemoryMiB",32).Value;
                options.DeepMiB=Config.Bind("Limits","DeepMemoryMiB",256).Value;
                options.QueueMiB=Config.Bind("Limits","WriterQueueMiB",64).Value;
                options.IncidentMiB=Config.Bind("Limits","LightIncidentMiB",128).Value;
                options.SessionBytes=Config.Bind("Limits","SessionBytes",4L*1024*1024*1024).Value;
                options.DeepSeconds=Config.Bind("Deep","DurationSeconds",300,"Maximum 300 monotonic real seconds including pauses, starting at completed mission initialization.").Value;
                options.Interval=Config.Bind("Deep","IntervalTicks",10).Value;
                string focus=Config.Bind("Deep","FocusUnitIds","","Comma separated native game IDs; empty means broad hashes without extra focused snapshots.").Value;
                if(!string.IsNullOrWhiteSpace(focus))options.Focus=Array.ConvertAll(focus.Split(','),v=>int.Parse(v.Trim(),System.Globalization.CultureInfo.InvariantCulture));
                options.Validate();
                if(main==null)throw new InvalidOperationException("Validated Unity synchronization context is unavailable.");
                CrusaderLibrary.Instance.LibraryLoaded+=LibraryLoaded;
                ReportInfo("DESYNC_PLUGIN_LOADED mode="+options.Mode+"; no network sends or simulation mutation.");
            }
            catch(Exception ex){ReportInfo("DESYNC_INITIALIZATION_FAILED "+ex);}
        }
        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if(recorder!=null)return;
            try
            {
                string dll=Path.Combine(Paths.GameRootPath,"Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll");
                if(NativeHooks.HashFile(dll)!=NativeHooks.NativeHash)throw new InvalidOperationException("Unknown Native hash: native readers disabled; re-audit required.");
                if(Marshal.SizeOf(typeof(GameUnit))!=UnitChecksum.RecordSize||Marshal.OffsetOf(typeof(GameUnit),nameof(GameUnit.r_GlobalId)).ToInt32()!=0x94||
                    Marshal.OffsetOf(typeof(GameUnitManager),nameof(GameUnitManager.GameUnitArray)).ToInt32()!=0x65C)
                    throw new InvalidOperationException("Installed unit view differs from audited wire-prefix layout.");
                recorder=new Recorder(options,Path.Combine(Paths.BepInExRootPath,"DesyncLogs"),PostInfo);
                capture=new NativeCapture(recorder,context.ModuleHandle,context.Memory.Length);
                // Set static roots BEFORE publication; a partial install remains logically inactive forever.
                hooks=new NativeHooks(capture,ReportInfo);hooks.Install(context);
                lifecycle=Shared.GameplaySessionLifecycle.SubscribeStarted(log,Started,()=>recorder.End());
                recorder.StartWriter();
                ReportInfo("DESYNC_CORE_READY native="+NativeHooks.NativeHash+"; lifecycle=APIShared; unitWirePrefix=4; otherRepairLayouts=raw-only; runtimeAcceptance=pending.");
            }
            catch(Exception ex){ReportInfo("DESYNC_CORE_UNAVAILABLE "+ex);}
        }
        private static void Started(Shared.GameplaySessionStartedContext context)
        {
            // Registration may replay an existing mission during initialization, before ready is set.
            if(recorder==null||capture==null)return;
            capture.ResetSession();
            var plugins=new List<object>();
            var participants=new List<object>();
            var members=Platform_Multiplayer.Instance.gameMembers;
            if(members!=null)foreach(var member in members)
                participants.Add(new {player=member.playerID,steam=member.steamID.ToString(),host=member.isHost,self=member.isSelf,ai=member.skirmishAI,kicked=member.kicked});
            foreach(var pair in Chainloader.PluginInfos)
            {
                string file=pair.Value.Instance?.GetType().Assembly.Location;
                string hash="unavailable";try{if(!string.IsNullOrEmpty(file))hash=NativeHooks.HashFile(file);}catch{}
                plugins.Add(new {guid=pair.Key,version=pair.Value.Metadata.Version.ToString(),sha256=hash});
            }
            recorder.Start(context.SessionId,new {nativeHash=NativeHooks.NativeHash,managedHash=NativeHooks.HashFile(Path.Combine(Paths.ManagedPath,"Assembly-CSharp.dll")),
                nativeContracts=new {detourBackend=NativeHooks.BackendHash,inlineBackend=NativeHooks.InlineBackendHash,assembler=NativeHooks.AssemblerHash},
                startKind=context.Kind.ToString(),file=context.SaveFileName,mode=context.Mode.ToDiagnosticString(),replayed=context.IsReplay,plugins,participants,
                effectiveModSettings="not captured: no optional provider contract in prototype",participantMapping="mode snapshot; native source player IDs on dispatch"});
            ReportInfo("DESYNC_SESSION_STARTED id="+context.SessionId+" mode="+options.Mode+" "+context.Mode.ToDiagnosticString());
        }
        private static void PostInfo(string text) { main.Post(_=>ReportInfo(text),null); }
        private static void ReportInfo(string text) { Shared.DebugLogHelper.LogInfo(log,text); }
    }
}
