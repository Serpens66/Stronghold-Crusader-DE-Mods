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
        internal int[] Focus=new int[0];
        internal void Validate()
        {
            if(!Enum.IsDefined(typeof(CaptureMode),Mode)||LightMiB<1||LightMiB>512||DeepMiB<LightMiB||DeepMiB>1024||QueueMiB<1||QueueMiB>256||IncidentMiB<1||IncidentMiB>4096||DeepSeconds<1||DeepSeconds>300||Interval<1||SessionBytes<1048576)
                throw new ArgumentException("Invalid capture limits; DeepSeconds must be 1..300, memory and disk limits must be finite.");
            if(Focus==null||Focus.Length>9999)throw new ArgumentException("At most 9999 focus IDs are allowed.");
            var unique=new HashSet<int>();
            foreach(int id in Focus)if(id<1||id>9999||!unique.Add(id))throw new ArgumentException("FocusUnitIds must contain unique native IDs 1..9999.");
        }
        internal object Diagnostic() => new { Mode=Mode.ToString(), LightMiB, DeepMiB, QueueMiB, IncidentMiB,DeepSeconds,Interval,SessionBytes,Focus };
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
    }

    internal sealed unsafe class Recorder
    {
        internal readonly CaptureOptions Options;
        internal readonly CaptureRing Ring;
        private readonly ConcurrentDictionary<long,CaptureSession> sessions=new ConcurrentDictionary<long,CaptureSession>();
        private readonly Dictionary<long,Output> outputs=new Dictionary<long,Output>(); // Writer-owned only.
        private readonly Action<string> report;
        private readonly string root;
        private CaptureSession current;
        private readonly object captureGate=new object();
        private readonly byte[] drain;
        internal long CallbackFailures,HashMismatches,RejectedSessions,WriterFailures,DiskDroppedBytes;
        internal Recorder(CaptureOptions options,string root,Action<string> report)
        {
            options.Validate();Options=options;this.root=root;this.report=report;
            Ring=new CaptureRing(options.Mode==CaptureMode.Off?256:(options.Mode==CaptureMode.Deep?options.DeepMiB:options.LightMiB)*1024*1024);
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
                Ring.Append(new CaptureEvent{Kind=RecordKind.DeepEnded,Session=s.Id,Tick=s.LastTick},IntPtr.Zero,0,true);
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
            Ring.Append(new CaptureEvent{Kind=RecordKind.End,Session=s.Id,Tick=s.LastTick},IntPtr.Zero,0,true);
            Ring.Flush();s.EndRequested=true;
        }
        internal void Record(CaptureEvent e,IntPtr source,int length)
        {
          // Lifecycle/expiry and publication are ordered together. Late transport callbacks
          // cannot publish data for a retired session after its terminal End record.
          if(!Monitor.TryEnter(captureGate)){Interlocked.Increment(ref Ring.ContentionDrops);return;}
          try
          {
            var s=Current;if(s==null)return;
            ExpireDeepLocked(s);
            if((e.Kind==RecordKind.WorldUnit||e.Kind==RecordKind.UnitHash)&&s.Window.Mode!=CaptureMode.Deep)return;
            e.Session=s.Id;if(e.Tick>=0)s.LastTick=e.Tick;
            if(e.Kind==RecordKind.Dispatch&&e.Phase==0&&e.Opcode==54&&e.Stage==0){s.InIncident=true;Interlocked.Increment(ref s.Incident);}
            bool flush=s.InIncident||s.Window.Mode==CaptureMode.Deep||e.Kind==RecordKind.ForcedRun||e.Kind==RecordKind.Fault;
            Ring.Append(e,source,length,flush);
            if(e.Kind==RecordKind.Dispatch&&e.Phase==0&&e.Stage==1&&(e.Opcode==67||e.Opcode==89))s.InIncident=false;
          }
          finally{Monitor.Exit(captureGate);}
        }
        internal void StartWriter() { if(Options.Mode!=CaptureMode.Off)new Thread(WriteLoop){IsBackground=true,Name="DesyncTest writer"}.Start(); }
        private void WriteLoop()
        {
            long nextError=0;
            while(true)
            {
                try { Pump(); }
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
            int n;
            while((n=Ring.Drain(drain))>0)
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
                                output.Write(drain,at,size,this);
                            }
                        }
                        catch(Exception ex)
                        {
                            owner.OutputFailed=true;Interlocked.Increment(ref WriterFailures);Interlocked.Add(ref DiskDroppedBytes,size);
                            SafeReport("DESYNC_SESSION_OUTPUT_FAILED session="+id+" "+ex.Message);
                        }
                        if(end)
                        {
                            try{if(outputs.TryGetValue(id,out output))output.Close(this,true);}
                            catch(Exception ex){Interlocked.Increment(ref WriterFailures);SafeReport("DESYNC_FINALIZE_FAILED session="+id+" "+ex.Message);}
                            finally{outputs.Remove(id);sessions.TryRemove(id,out _);}
                        }
                    }
                    at+=size;
                }
            }
            // An overflow can discard even an End record. Once all committed data has drained,
            // close retired sessions too, rather than exhausting the bounded pending-session table.
            if(Ring.IsEmpty)
            {
                foreach(var pair in sessions)
                {
                    if(!pair.Value.EndRequested)continue;
                    try{if(outputs.TryGetValue(pair.Key,out var output))output.Close(this,true);}
                    catch(Exception ex){Interlocked.Increment(ref WriterFailures);SafeReport("DESYNC_FINALIZE_FAILED session="+pair.Key+" "+ex.Message);}
                    finally{outputs.Remove(pair.Key);sessions.TryRemove(pair.Key,out _);}
                }
            }
            foreach(var pair in outputs)
            {
                try{pair.Value.Checkpoint(this);}
                catch(Exception ex)
                {
                    // Keep draining subsequent records and sessions after an unwritable disk.
                    // A final manifest may itself be impossible; its earlier complete=false remains valid.
                    if(sessions.TryGetValue(pair.Key,out var s)&&!s.OutputFailed)
                    {s.OutputFailed=true;Interlocked.Increment(ref WriterFailures);SafeReport("DESYNC_CHECKPOINT_FAILED session="+pair.Key+" "+ex.Message);}
                }
            }
        }
        private void SafeReport(string text) { try{report(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")+" UTC "+text);}catch{} }
        private sealed class Output
        {
            private readonly CaptureSession owner;
            private readonly CaptureOptions options;
            private readonly FileStream events,snapshots;
            private readonly Dictionary<int,long> incidentBytes=new Dictionary<int,long>();
            private long bytes,lastManifest;
            private int incident;
            private bool inIncident;
            private bool truncated,failed;
            private readonly List<string> timeline=new List<string>();
            private int omittedSummaryLines;
            internal Output(CaptureSession s,CaptureOptions options)
            {
                owner=s;this.options=options;Directory.CreateDirectory(s.Folder);
                events=new FileStream(Path.Combine(s.Folder,"events.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                try{snapshots=new FileStream(Path.Combine(s.Folder,"snapshots.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);}catch{events.Dispose();throw;}
                try{File.WriteAllText(Path.Combine(s.Folder,"summary.txt"),"DesyncTest format 2; read-only prototype.\r\nCore: native dispatch, checksums, transport, unit wire repair.\r\nOther categories: raw fields only. Mod internal state providers: absent.\r\nAnalyze with Analyze-Desync.py; recordings may be incomplete.\r\n");}
                catch{events.Dispose();snapshots.Dispose();throw;}
            }
            internal void Write(byte[] data,int at,int length,Recorder recorder)
            {
                if(failed){Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;}
                var kind=(RecordKind)BitConverter.ToUInt16(data,at+6);
                int phase=BitConverter.ToInt32(data,at+44),op=BitConverter.ToInt32(data,at+48),stage=BitConverter.ToInt32(data,at+72);
                if(kind==RecordKind.Dispatch&&phase==0&&op==54&&stage==0){incident++;inIncident=true;}
                if((kind==RecordKind.Dispatch&&phase==0&&((op==54&&stage==0)||((op==67||op==89)&&stage==1)))||
                    (kind==RecordKind.Unit&&(stage==0||stage==4))||kind==RecordKind.ForcedRun||kind==RecordKind.DeepEnded||kind==RecordKind.Fault)
                {
                    if(timeline.Count<128)timeline.Add(new DateTime(BitConverter.ToInt64(data,at+32),DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss.fff")+
                        " UTC seq="+BitConverter.ToInt64(data,at+8)+" tick="+BitConverter.ToInt32(data,at+40)+" attempt="+incident+" "+kind+
                        " opcode="+op+" stage="+stage+" unit="+BitConverter.ToInt32(data,at+64)+" global="+BitConverter.ToInt32(data,at+68));
                    else omittedSummaryLines++;
                }
                if(incident>4096){truncated=true;Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;}
                incidentBytes.TryGetValue(incident,out long used);
                // Use the event's own monotonic capture time: a lost DeepEnded marker must
                // not bypass Light limits, nor should a writer backlog relabel earlier Deep data.
                bool capIncident=(options.Mode!=CaptureMode.Deep||BitConverter.ToInt64(data,at+24)>=owner.Window.Deadline)&&inIncident;
                if(kind==RecordKind.Dispatch&&phase==0&&stage==1&&(op==67||op==89))inIncident=false;
                // Reserve metadata space inside the session budget, including atomic manifest replacement.
                if(bytes+length>options.SessionBytes-131072||(capIncident&&used+length>(long)options.IncidentMiB*1024*1024))
                {truncated=true;Interlocked.Add(ref recorder.DiskDroppedBytes,length);return;}
                var stream=kind==RecordKind.Unit||kind==RecordKind.WorldUnit||kind==RecordKind.Field||kind==RecordKind.Arrival?snapshots:events;
                try{stream.Write(data,at,length);bytes+=length;if(capIncident)incidentBytes[incident]=used+length;}
                catch {failed=true;truncated=true;throw;}
            }
            internal void Checkpoint(Recorder r)
            {
                if(Stopwatch.GetTimestamp()-lastManifest<Stopwatch.Frequency)return;
                if(owner.OutputFailed)return;
                if(!failed){events.Flush();snapshots.Flush();}Manifest(r,false);Summary(false);lastManifest=Stopwatch.GetTimestamp();
            }
            private void Manifest(Recorder r,bool ended)
            {
                string json=Shared.DependencyFreeJson.Serialize(new { format=2,plugin="DesyncTest_Serp",version="0.1.0",session=owner.Id,identity=owner.Identity,clockFrequency=Stopwatch.Frequency,
                    config=options.Diagnostic(), ended,complete=ended&&!owner.OutputFailed&&!truncated&&r.Ring.DroppedRecords==0&&r.Ring.ContentionDrops==0&&r.CallbackFailures==0&&r.WriterFailures==0&&r.DiskDroppedBytes==0&&r.HashMismatches==0,
                    bytes,incidents=owner.Incident,finalMode=owner.Window.Mode.ToString(),endRequested=owner.EndRequested,
                    losses=new {scope="process-wide",r.Ring.DroppedRecords,r.Ring.DroppedBytes,r.Ring.ContentionDrops,r.Ring.HistoryOverwrites,r.CallbackFailures,r.WriterFailures,r.DiskDroppedBytes,r.RejectedSessions,r.HashMismatches},
                    coverage=new {unitWirePrefix=4,unitRecord=1168,pathBytes=1000,requestedUnitSlots=9999,snapshotSeriesCanBePartial=true,normalArrivalHeaders=true,fifoAcceptance=false,hostChanges=false,modInternalProviders=false,otherRepairLayouts=false},
                    files=new[]{"events.bin","snapshots.bin","summary.txt"} });
                // Participant/component metadata is bounded as well; never exceed the reserved budget.
                if(System.Text.Encoding.UTF8.GetByteCount(json)>30000)throw new InvalidDataException("Manifest exceeds metadata budget.");
                string path=Path.Combine(owner.Folder,"manifest.json"),tmp=path+".tmp";
                File.WriteAllText(tmp,json);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
            }
            internal void Close(Recorder r,bool ended)
            { try{events.Flush();snapshots.Flush();Summary(ended);Manifest(r,ended);}finally{events.Dispose();snapshots.Dispose();} }
            private void Summary(bool ended)
            {
                File.WriteAllText(Path.Combine(owner.Folder,"summary.txt"),
                    "DesyncTest format 2; session="+owner.Id+" ended="+ended+" bytes="+bytes+" attempts="+incident+"\r\n"+
                    "54=start, 67=finish, 89=abort; Unit stage 0=old, 4=final. ForcedRun is a separate wait-path event.\r\n"+
                    "Completeness/losses: manifest.json. Timeline is bounded; omitted lines="+omittedSummaryLines+".\r\n"+
                    "Other repair layouts: raw only. Mod internals/effective settings: unavailable. No root-cause proof.\r\n"+
                    string.Join("\r\n",timeline)+"\r\n");
            }
        }
    }
}
