using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DesyncTest
{
    internal sealed class CaptureOptions
    {
        internal CaptureMode Mode=CaptureMode.Light;
        internal int LightMiB=32,DeepMiB=256,QueueMiB=64,IncidentMiB=128,DeepSeconds=300,Interval=10;
        internal long SessionBytes=4L*1024*1024*1024;
        internal long EffectiveSessionBytes => Math.Min(SessionBytes,(Mode==CaptureMode.Deep?512L:256L)*1024*1024);
        internal int[] Focus=new int[0];
        internal int WorkerPlayer;
        internal void Validate()
        {
            if(!Enum.IsDefined(typeof(CaptureMode),Mode)||LightMiB<1||LightMiB>512||DeepMiB<LightMiB||DeepMiB>1024||QueueMiB<1||QueueMiB>256||IncidentMiB<1||IncidentMiB>4096||DeepSeconds<1||DeepSeconds>300||Interval<1||SessionBytes<1048576)
                throw new ArgumentException("Invalid capture limits; DeepSeconds must be 1..300, memory and disk limits must be finite.");
            if(Focus==null||Focus.Length>9999)throw new ArgumentException("At most 9999 focus IDs are allowed.");
            if(WorkerPlayer<-1||WorkerPlayer>8)throw new ArgumentException("Worker player must be -1 (all), 0 (off), or 1..8.");
            var unique=new HashSet<int>();
            foreach(int id in Focus)if(id<1||id>9999||!unique.Add(id))throw new ArgumentException("FocusUnitIds must contain unique native IDs 1..9999.");
        }
        internal object Diagnostic() => new { Mode=Mode.ToString(), LightMiB, DeepMiB, QueueMiB, IncidentMiB,DeepSeconds,Interval,SessionBytes,Focus,WorkerPlayer };
    }

    internal sealed class CaptureSession
    {
        internal long Id;
        internal string Folder;
        internal object Identity;
        internal DeepWindow Window;
        internal volatile bool EndRequested;
        internal bool DeepEndReported;
        internal int LastTick=-1,Incident;
        internal bool InIncident;
        internal bool OutputFailed;
        internal bool HistoryEnded, CheckpointsEnded;
        internal long IncidentHistoryBytes, IncidentBudgetDropped;
    }

    internal sealed unsafe class Recorder
    {
        private const ushort IncidentDetailFlag=0x8000; // Internal queue flag, removed before export.
        internal readonly CaptureOptions Options;
        internal readonly CaptureRing Ring;
        internal readonly CaptureRing Checkpoints;
        private long sequence;
        private readonly ConcurrentDictionary<long,CaptureSession> sessions=new ConcurrentDictionary<long,CaptureSession>();
        private readonly Dictionary<long,Output> outputs=new Dictionary<long,Output>(); // Writer-owned only.
        private readonly Action<string> report;
        private readonly string root;
        private CaptureSession current;
        private readonly object captureGate=new object();
        private readonly byte[] drain;
        internal long CallbackFailures,HashMismatches,RejectedSessions,WriterFailures,DiskDroppedBytes,BuildingRowsDeferred;
        internal Recorder(CaptureOptions options,string root,Action<string> report)
        {
            options.Validate();Options=options;this.root=root;this.report=report;
            Ring=new CaptureRing(options.Mode==CaptureMode.Off?256:(options.Mode==CaptureMode.Deep?options.DeepMiB:options.LightMiB)*1024*1024);
            Checkpoints=new CaptureRing(options.Mode==CaptureMode.Off?256:4*1024*1024);
            drain=new byte[options.Mode==CaptureMode.Off?256:options.QueueMiB*1024*1024];
        }
        internal CaptureSession Current => Volatile.Read(ref current);
        internal bool Active { get { var s=Current;return s!=null&&s.Window.Mode!=CaptureMode.Off; } }
        internal bool IsDeep
        {
            get
            {
                ExpireDeep();var s=Current;
                return s!=null&&s.Window.Mode==CaptureMode.Deep&&Stopwatch.GetTimestamp()<s.Window.Deadline;
            }
        }
        private void ExpireDeep()
        {
            if(!Monitor.TryEnter(captureGate))return;
            try{ExpireDeepLocked(Current);}finally{Monitor.Exit(captureGate);}
        }
        private void ExpireDeepLocked(CaptureSession s)
        {
            if(s!=null&&s.Window.Expire(Stopwatch.GetTimestamp()))
            {
                Ring.Append(new CaptureEvent{Kind=RecordKind.DeepEnded,Session=s.Id,Tick=s.LastTick},IntPtr.Zero,0,true,++sequence,true);
                Ring.Limit(Options.LightMiB*1024*1024);
            }
        }
        internal void Start(long id,object identity)
        {
          lock(captureGate)
          {
            EndLocked();if(Options.Mode==CaptureMode.Off)return;
            Ring.Limit((Options.Mode==CaptureMode.Deep?Options.DeepMiB:Options.LightMiB)*1024*1024);
            if(sessions.Count>=8){Interlocked.Increment(ref RejectedSessions);return;}
            var s=new CaptureSession{Id=id,Identity=identity,Window=new DeepWindow(Options.Mode,Stopwatch.GetTimestamp(),(long)Options.DeepSeconds*Stopwatch.Frequency),
                Folder=Path.Combine(root,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-p"+Process.GetCurrentProcess().Id+"-s"+id)};
            if(!sessions.TryAdd(id,s)){Interlocked.Increment(ref RejectedSessions);return;}
            Volatile.Write(ref current,s);
            Record(new CaptureEvent{Kind=RecordKind.Start,Session=id,Tick=-1},IntPtr.Zero,0);
          }
        }
        internal void End()
        { lock(captureGate)EndLocked(); }
        private void EndLocked()
        {
            var s=Interlocked.Exchange(ref current,null);if(s==null)return;
            var end=new CaptureEvent{Kind=RecordKind.End,Session=s.Id,Tick=s.LastTick};
            Ring.Append(end,IntPtr.Zero,0,true,++sequence,true);
            Checkpoints.Append(end,IntPtr.Zero,0,true,++sequence,true);
            Ring.Flush();s.EndRequested=true;
        }
        internal bool Record(CaptureEvent e,IntPtr source,int length)
        {
          // Lifecycle/expiry and publication are ordered together. Late transport callbacks
          // cannot publish data for a retired session after its terminal End record.
          if(!Monitor.TryEnter(captureGate)){Interlocked.Increment(ref Ring.ContentionDrops);return false;}
          try
          {
            var s=Current;if(s==null)return false;
            ExpireDeepLocked(s);
            if((e.Kind==RecordKind.WorldUnit||e.Kind==RecordKind.UnitHash)&&s.Window.Mode!=CaptureMode.Deep)return false;
            e.Session=s.Id;if(e.Tick>=0)s.LastTick=e.Tick;
            bool start=e.Kind==RecordKind.Dispatch&&e.Phase==0&&e.Opcode==54&&e.Stage==0;
            bool finish=e.Kind==RecordKind.Dispatch&&e.Phase==0&&e.Stage==1&&(e.Opcode==67||e.Opcode==89);
            if(start){s.InIncident=true;s.IncidentHistoryBytes=0;Interlocked.Increment(ref s.Incident);}
            bool flush=s.InIncident||s.Window.Mode==CaptureMode.Deep||e.Kind==RecordKind.ForcedRun||e.Kind==RecordKind.Fault;
            bool essential=start||finish||e.Kind==RecordKind.Start||e.Kind==RecordKind.Checksum||
                e.Kind==RecordKind.UnitChecksumTable||e.Kind==RecordKind.BuildingChecksumTable;
            if(s.InIncident&&!essential&&s.Window.Mode==CaptureMode.Light)
            {
                long bytes=CaptureRing.HeaderSize+(long)length;
                if(s.IncidentHistoryBytes+bytes>(long)Options.IncidentMiB*1024*1024)
                {
                    Interlocked.Add(ref s.IncidentBudgetDropped,bytes);
                    Ring.FlushPublished();
                    return false;
                }
                s.IncidentHistoryBytes+=bytes;
            }
            if(s.InIncident&&!essential)e.Kind=(RecordKind)((ushort)e.Kind|IncidentDetailFlag);
            bool recorded=(essential?Checkpoints:Ring).Append(e,source,length,essential||flush,++sequence,true)!=0;
            if(start||finish)Ring.FlushPublished();
            if(finish)s.InIncident=false;
            return recorded;
          }
          finally{Monitor.Exit(captureGate);}
        }
        internal void StartWriter() { if(Options.Mode!=CaptureMode.Off)new Thread(WriteLoop){IsBackground=true,Name="DesyncTest writer"}.Start(); }
        private void WriteLoop()
        {
            long nextError=0,nextPrune=0;
            while(true)
            {
                try
                {
                    Pump();
                    long now=Stopwatch.GetTimestamp();
                    if(now>=nextPrune)
                    {
                        nextPrune=now+300*Stopwatch.Frequency;
                        CaptureRetention.Prune(root,DateTime.UtcNow,SafeReport);
                    }
                }
                catch(Exception ex){Interlocked.Increment(ref WriterFailures);if(Stopwatch.GetTimestamp()>=nextError){SafeReport("DESYNC_WRITER_FAILED "+ex.Message);nextError=Stopwatch.GetTimestamp()+30*Stopwatch.Frequency;}}
                Thread.Sleep(250);
            }
        }
        internal void Pump()
        {
            ExpireDeep();
            var active=Current;
            if(active!=null&&Options.Mode==CaptureMode.Deep&&active.Window.Mode==CaptureMode.Light&&!active.DeepEndReported)
            {active.DeepEndReported=true;SafeReport("DESYNC_DEEP_ENDED session="+active.Id+"; continuing Light.");}
            Drain(Checkpoints,true);
            Drain(Ring,false);
            bool drained=Ring.IsEmpty&&Checkpoints.IsEmpty;
            foreach(var pair in sessions)
            {
                var owner=pair.Value;
                if(!owner.EndRequested&&!(owner.HistoryEnded&&owner.CheckpointsEnded))continue;
                if(!drained&&!(owner.HistoryEnded&&owner.CheckpointsEnded))continue;
                try{if(outputs.TryGetValue(pair.Key,out var output))output.Close(this,true);}
                catch(Exception ex){Interlocked.Increment(ref WriterFailures);SafeReport("DESYNC_FINALIZE_FAILED session="+pair.Key+" "+ex.Message);}
                finally{outputs.Remove(pair.Key);sessions.TryRemove(pair.Key,out _);}
            }
            foreach(var pair in outputs)
            {
                try{pair.Value.Checkpoint(this);}
                catch(Exception ex)
                {
                    if(sessions.TryGetValue(pair.Key,out var s)&&!s.OutputFailed)
                    {s.OutputFailed=true;Interlocked.Increment(ref WriterFailures);SafeReport("DESYNC_CHECKPOINT_FAILED session="+pair.Key+" "+ex.Message);}
                }
            }
        }
        private void Drain(CaptureRing input,bool priority)
        {
            int n;
            while((n=input.Drain(drain))>0)
            {
                int at=0;
                while(at<n)
                {
                    int size=BitConverter.ToInt32(drain,at);long id=BitConverter.ToInt64(drain,at+16);
                    if(sessions.TryGetValue(id,out var owner))
                    {
                        bool end=(RecordKind)BitConverter.ToUInt16(drain,at+6)==RecordKind.End;
                        Output output=null;
                        try
                        {
                            if(owner.OutputFailed)Interlocked.Add(ref DiskDroppedBytes,size);
                            else
                            {
                                if(!outputs.TryGetValue(id,out output)){output=new Output(owner,Options);outputs.Add(id,output);}
                                output.Write(drain,at,size,this,priority);
                            }
                        }
                        catch(Exception ex)
                        {
                            owner.OutputFailed=true;Interlocked.Increment(ref WriterFailures);Interlocked.Add(ref DiskDroppedBytes,size);
                            SafeReport("DESYNC_SESSION_OUTPUT_FAILED session="+id+" "+ex.Message);
                        }
                        if(end)
                        {
                            if(priority)owner.CheckpointsEnded=true;
                            else owner.HistoryEnded=true;
                        }
                    }
                    at+=size;
                }
            }
        }
        private void SafeReport(string text) { try{report(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")+" UTC "+text);}catch{} }
        private sealed class Output
        {
            private readonly CaptureSession owner;
            private readonly CaptureOptions options;
            private readonly FileStream events,snapshots,markers,evidence;
            private FileStream checkpointFile;
            private long bytes,lastManifest;
            private long checkpointBytes,previousCheckpointBytes,detailBytes,ordinaryDetailBytes,markerBytes,evidenceBytes,lastEvidenceSequence;
            private int checkpointRotations;
            // Six regular or repair checksum calls, three frames each. Writer-owned.
            private readonly Queue<byte[]> recentChecks=new Queue<byte[]>(18);
            private bool truncated,failed;
            private readonly List<string> timeline=new List<string>();
            private int omittedSummaryLines;
            internal Output(CaptureSession s,CaptureOptions options)
            {
                owner=s;this.options=options;Directory.CreateDirectory(s.Folder);
                events=new FileStream(Path.Combine(s.Folder,"events.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                try
                {
                    snapshots=new FileStream(Path.Combine(s.Folder,"snapshots.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    checkpointFile=new FileStream(Path.Combine(s.Folder,"checkpoints.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read|FileShare.Delete);
                    markers=new FileStream(Path.Combine(s.Folder,"markers.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    evidence=new FileStream(Path.Combine(s.Folder,"evidence.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                }
                catch{events.Dispose();snapshots?.Dispose();checkpointFile?.Dispose();markers?.Dispose();throw;}
            }
            internal void Write(byte[] data,int at,int length,Recorder recorder,bool priority)
            {
                if(failed){Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;}
                ushort rawKind=BitConverter.ToUInt16(data,at+6);
                bool incidentDetail=(rawKind&IncidentDetailFlag)!=0;
                var kind=(RecordKind)(rawKind&~IncidentDetailFlag);
                if(incidentDetail)data[at+7]&=0x7f; // Keep the published format unchanged.
                int phase=BitConverter.ToInt32(data,at+44),op=BitConverter.ToInt32(data,at+48),stage=BitConverter.ToInt32(data,at+72);
                if(kind==RecordKind.End&&priority)return; // Private second-queue barrier.
                if((kind==RecordKind.Dispatch&&phase==0&&((op==54&&stage==0)||((op==67||op==89)&&stage==1)))||
                    (kind==RecordKind.Unit&&(stage==0||stage==4))||kind==RecordKind.ForcedRun||kind==RecordKind.DeepEnded||kind==RecordKind.Fault)
                {
                    if(timeline.Count<128)timeline.Add(new DateTime(BitConverter.ToInt64(data,at+32),DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss.fff")+
                        " UTC seq="+BitConverter.ToInt64(data,at+8)+" tick="+BitConverter.ToInt32(data,at+40)+" "+kind+
                        " opcode="+op+" stage="+stage+" unit="+BitConverter.ToInt32(data,at+64)+" global="+BitConverter.ToInt32(data,at+68));
                    else omittedSummaryLines++;
                }
                long budget=options.EffectiveSessionBytes-131072;
                long checkpointBudget=Math.Min(64L*1024*1024,budget/4);
                const int markerBudget=64*1024;
                long evidenceBudget=checkpointBudget/4;
                bool marker=priority&&(kind==RecordKind.Start||kind==RecordKind.Dispatch);
                long segmentBudget=(checkpointBudget-markerBudget-evidenceBudget)/2;
                long detailBudget=budget-checkpointBudget;
                bool detailLimit=detailBytes+length>detailBudget||(!incidentDetail&&ordinaryDetailBytes+length>detailBudget/3);
                if(marker?markerBytes+length>markerBudget:priority?length>segmentBudget:detailLimit)
                {truncated=true;Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;}
                if(priority&&!marker&&checkpointBytes+length>segmentBudget&&!RotateCheckpoints())
                {
                    truncated=true;Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;
                }
                var stream=marker?markers:priority?checkpointFile:
                    (kind==RecordKind.Unit||kind==RecordKind.WorldUnit||kind==RecordKind.Field||kind==RecordKind.Arrival||kind==RecordKind.BuildingState?snapshots:events);
                try
                {
                    stream.Write(data,at,length);
                    bytes+=length;
                    if(marker)
                    {
                        markerBytes+=length;
                        if(op==54&&stage==0)PinCheckpoints(evidenceBudget,recorder);
                    }
                    else if(priority)
                    {
                        checkpointBytes+=length;
                        var copy=new byte[length];
                        Buffer.BlockCopy(data,at,copy,0,length);
                        if(recentChecks.Count==18)recentChecks.Dequeue();
                        recentChecks.Enqueue(copy);
                    }
                    else
                    {
                        detailBytes+=length;
                        if(!incidentDetail)ordinaryDetailBytes+=length;
                    }
                }
                catch {failed=true;truncated=true;throw;}
            }
            private void PinCheckpoints(long budget,Recorder recorder)
            {
                foreach(var frame in recentChecks)
                {
                    long sequence=BitConverter.ToInt64(frame,8);
                    if(sequence<=lastEvidenceSequence)continue;
                    if(evidenceBytes+frame.Length>budget)
                    {
                        truncated=true;
                        Interlocked.Add(ref recorder.DiskDroppedBytes,frame.Length);
                        continue;
                    }
                    evidence.Write(frame,0,frame.Length);
                    bytes+=frame.Length;evidenceBytes+=frame.Length;lastEvidenceSequence=sequence;
                }
            }
            private bool RotateCheckpoints()
            {
                string current=Path.Combine(owner.Folder,"checkpoints.bin");
                string previous=Path.Combine(owner.Folder,"checkpoints.previous.bin");
                checkpointFile.Dispose();
                try
                {
                    if(File.Exists(previous))File.Replace(current,previous,null);
                    else File.Move(current,previous);
                }
                catch(IOException) when(File.Exists(current))
                {
                    // A live reader may temporarily deny renaming on Windows.
                    checkpointFile=new FileStream(current,FileMode.Append,FileAccess.Write,FileShare.Read|FileShare.Delete);
                    return false;
                }
                checkpointFile=new FileStream(current,FileMode.CreateNew,FileAccess.Write,FileShare.Read|FileShare.Delete);
                bytes-=previousCheckpointBytes;
                previousCheckpointBytes=checkpointBytes;
                checkpointBytes=0;
                checkpointRotations++;
                return true;
            }
            internal void Checkpoint(Recorder r)
            {
                if(Stopwatch.GetTimestamp()-lastManifest<Stopwatch.Frequency)return;
                if(owner.OutputFailed)return;
                if(!failed){events.Flush();snapshots.Flush();checkpointFile.Flush();markers.Flush();evidence.Flush();}Manifest(r,false);Summary(false);lastManifest=Stopwatch.GetTimestamp();
            }
            private void Manifest(Recorder r,bool ended)
            {
                string json=Shared.DependencyFreeJson.Serialize(new { format=2,retentionSchema=1,plugin="DesyncTest_Serp",version="0.1.8",session=owner.Id,identity=owner.Identity,clockFrequency=Stopwatch.Frequency,
                    config=options.Diagnostic(), ended,complete=ended&&!owner.OutputFailed&&!truncated&&checkpointRotations<2&&r.Ring.DroppedRecords==0&&r.Ring.ContentionDrops==0&&r.Checkpoints.DroppedRecords==0&&r.Checkpoints.ContentionDrops==0&&owner.IncidentBudgetDropped==0&&r.CallbackFailures==0&&r.WriterFailures==0&&r.DiskDroppedBytes==0&&r.HashMismatches==0,
                    bytes,checkpointBytes,previousCheckpointBytes,checkpointRotations,evidenceBytes,detailBytes,lastObservedTick=Volatile.Read(ref owner.LastTick),incidents=owner.Incident,finalMode=owner.Window.Mode.ToString(),endRequested=owner.EndRequested,
                    losses=new {scope="process-wide",r.Ring.DroppedRecords,r.Ring.DroppedBytes,r.Ring.ContentionDrops,r.Ring.HistoryOverwrites,r.CallbackFailures,r.WriterFailures,r.DiskDroppedBytes,r.RejectedSessions,r.HashMismatches},
                    checkpointLosses=new {r.Checkpoints.DroppedRecords,r.Checkpoints.DroppedBytes,r.Checkpoints.ContentionDrops,incidentBudgetDropped=Interlocked.Read(ref owner.IncidentBudgetDropped)},
                    coverage=new {unitWirePrefix=4,unitRecord=1168,pathBytes=1000,requestedUnitSlots=9999,nativeUnitChecksumTables=true,nativeBuildingChecksumTables=true,buildingRowsDeferred=r.BuildingRowsDeferred,snapshotSeriesCanBePartial=true,normalArrivalHeaders=true,executedCustomChoreFields=true,fifoAcceptance=false,hostChanges=false,modInternalProviders=false,otherRepairLayouts=false},
                    files=checkpointRotations==0?new[]{"events.bin","snapshots.bin","checkpoints.bin","markers.bin","evidence.bin","summary.txt"}:
                        new[]{"events.bin","snapshots.bin","checkpoints.bin","checkpoints.previous.bin","markers.bin","evidence.bin","summary.txt"} });
                // Participant/component metadata is bounded as well; never exceed the reserved budget.
                if(System.Text.Encoding.UTF8.GetByteCount(json)>30000)throw new InvalidDataException("Manifest exceeds metadata budget.");
                string path=Path.Combine(owner.Folder,"manifest.json"),tmp=path+".tmp";
                File.WriteAllText(tmp,json);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            }
            internal void Close(Recorder r,bool ended)
            { try{events.Flush();snapshots.Flush();checkpointFile.Flush();markers.Flush();evidence.Flush();Summary(ended);Manifest(r,ended);}finally{events.Dispose();snapshots.Dispose();checkpointFile.Dispose();markers.Dispose();evidence.Dispose();} }
            private void Summary(bool ended)
            {
                File.WriteAllText(Path.Combine(owner.Folder,"summary.txt"),
                    "DesyncTest format 2; session="+owner.Id+" ended="+ended+" bytes="+bytes+" attempts="+owner.Incident+"\r\n"+
                    "54=start, 67=finish, 89=abort; Unit stage 0=old, 4=final. ForcedRun is a separate wait-path event.\r\n"+
                    "Completeness/losses: manifest.json. Timeline is bounded; omitted lines="+omittedSummaryLines+".\r\n"+
                    "Other repair layouts: raw only. Mod internals/effective settings: unavailable. No root-cause proof.\r\n"+
                    string.Join("\r\n",timeline)+"\r\n");
            }
        }
    }
}
