using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EnemyBridgePathTest
{
    // Lazy sections are copied input only. One writer survives map end via the static trace.
    // File is published only after footer/hash/close; a .partial file is never evidence.
    internal sealed class BridgeInputArtifact
    {
        internal const int BlockLimit=65536;
        [ThreadStatic] private static string currentSection;
        private string maxSourceSection="none";
        private long sourceGc0,sourceGc1,sourceGc2;
        private Queue<Job> jobs=new Queue<Job>();
        private readonly Action<long,string,string> emit;
        private readonly string directory;
        private readonly Dictionary<long,int> accepted=new Dictionary<long,int>();
        private Job active;
        private readonly HashSet<long> failures=new HashSet<long>();
        private long writtenTicks,writtenBytes,maxPumpTicks,maxSourceTicks,maxWriteTicks,maxIoTicks,maxHashTicks,maxFinishTicks,pumpOverBudget;
        private readonly byte[] outputBlock=new byte[BlockLimit];
        private static string Ms(long ticks)=>(ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        internal string IntervalCosts(){string text="artifactMaxPumpMs="+Ms(maxPumpTicks)+",artifactMaxSourceMs="+Ms(maxSourceTicks)+",artifactMaxSourceSection=["+maxSourceSection+"],artifactSourceGc0="+sourceGc0+",artifactSourceGc1="+sourceGc1+",artifactSourceGc2="+sourceGc2+",artifactMaxWriteHashMs="+Ms(maxWriteTicks)+",artifactMaxIoMs="+Ms(maxIoTicks)+",artifactMaxWholeHashMs="+Ms(maxHashTicks)+",artifactMaxFinishMs="+Ms(maxFinishTicks)+",artifactPumpOverBudget="+pumpOverBudget+",artifactCostScopes=nested-source-write-whole-hash-finish-in-pump";maxSourceSection="none";sourceGc0=sourceGc1=sourceGc2=0;maxPumpTicks=maxSourceTicks=maxWriteTicks=maxIoTicks=maxHashTicks=maxFinishTicks=pumpOverBudget=0;return text;}
        private sealed class Job
        {
            internal long Session,Id,Bytes;
            internal int Schema;
            internal string Partial,Final;
            internal IEnumerator<byte[]> Source;
            internal FileStream Stream;
            internal SHA256 Payload=SHA256.Create(),Whole=SHA256.Create();
            internal byte[] Piece;
            internal int Offset;
            internal bool Footer;
        }
        internal BridgeInputArtifact(string directory,Action<long,string,string> emit) {this.directory=directory;this.emit=emit;}
        internal bool Pending(long session) {if(active!=null&&active.Session==session)return true;foreach(var job in jobs)if(job.Session==session)return true;return false;}
        internal string Status(long session)
        {accepted.TryGetValue(session,out int count);return "inputArtifactsRequested="+count+",inputArtifactsPending="+Pending(session)+",inputArtifactDeliveryComplete="+(!Pending(session)&&!failures.Contains(session))+",inputArtifactFailed="+failures.Contains(session);}
        internal long Bytes => writtenBytes;
        internal long Ticks => writtenTicks;
        internal bool Enqueue(long session,long id,IEnumerable<byte[]> source,int schema=1)
        {
            accepted.TryGetValue(session,out int count);if(count>=2||jobs.Count>=4)return false;
            accepted[session]=count+1;
            string name="bridge-"+session+"-"+id;
            jobs.Enqueue(new Job {Session=session,Id=id,Schema=schema,Source=source.GetEnumerator(),Partial=Path.Combine(directory,name+".partial"),Final=Path.Combine(directory,name+".bin")});
            emit(session,"input-artifact-pending","definition="+id+",schema="+schema+",slot="+(count+1)+",complete=False");return true;
        }
        internal void Pump()
        { PumpCore(); }
        // Reserve the two session slots together. Neither iterator observes
        // mutable runtime selection state; callers pass frozen metadata.
        internal bool EnqueuePair(long session,long planId,IEnumerable<byte[]> plan,long commandId,IEnumerable<byte[]> command,int schema=2)
        {
            accepted.TryGetValue(session,out int count);
            if(count!=0||jobs.Count>2||planId==commandId)return false;
            Job first=null,second=null;
            try
            {
                first=PairJob(session,planId,plan,schema);second=PairJob(session,commandId,command,schema);
                var next=new Queue<Job>(jobs);next.Enqueue(first);next.Enqueue(second);
                accepted[session]=2;jobs=next;
            }
            catch
            {
                first?.Source?.Dispose();second?.Source?.Dispose();first?.Payload?.Dispose();first?.Whole?.Dispose();second?.Payload?.Dispose();second?.Whole?.Dispose();throw;
            }
            try
            {
                emit(session,"input-artifact-pending","definition="+planId+",schema="+schema+",slot=1,complete=False");
                emit(session,"input-artifact-pending","definition="+commandId+",schema="+schema+",slot=2,complete=False");
            }
            catch{failures.Add(session);}return true;
        }
        private Job PairJob(long session,long id,IEnumerable<byte[]> source,int schema)
        {string name="bridge-"+session+"-"+id;var job=new Job{Session=session,Id=id,Schema=schema,Partial=Path.Combine(directory,name+".partial"),Final=Path.Combine(directory,name+".bin")};try{job.Source=source.GetEnumerator();return job;}catch{job.Payload.Dispose();job.Whole.Dispose();throw;}}
        private void PumpCore()
        {
            long start=Stopwatch.GetTimestamp();
            try
            {
                if(active==null) {if(jobs.Count==0)return;active=jobs.Dequeue();Directory.CreateDirectory(directory);active.Stream=new FileStream(active.Partial,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096);}
                var block=outputBlock;int length=0,sourceAdvances=0;
                while(length<block.Length&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<2)
                {
                    if(active.Piece==null||active.Offset==active.Piece.Length)
                    {
                        if(active.Footer)break;
                        // Bound iterator preparation independently of output bytes.
                        // Leave time for the single write/hash operation below.
                        if(sourceAdvances>=8||(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency>=1)break;
                        sourceAdvances++;
                        int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);currentSection=null;
                        long sourceStart=Stopwatch.GetTimestamp();bool more=active.Source.MoveNext();long sourceTicks=Stopwatch.GetTimestamp()-sourceStart;
                        sourceGc0+=GC.CollectionCount(0)-gc0;sourceGc1+=GC.CollectionCount(1)-gc1;sourceGc2+=GC.CollectionCount(2)-gc2;
                        if(sourceTicks>maxSourceTicks){maxSourceTicks=sourceTicks;maxSourceSection=currentSection??"metadata-or-unclassified-iterator";}
                        if(more) {active.Piece=active.Source.Current;active.Offset=0;}
                        else
                        {
                            active.Payload.TransformFinalBlock(Array.Empty<byte>(),0,0);
                            active.Piece=new byte[48];Buffer.BlockCopy(active.Payload.Hash,0,active.Piece,0,32);
                            Buffer.BlockCopy(BitConverter.GetBytes(active.Bytes),0,active.Piece,32,8);Buffer.BlockCopy(Encoding.ASCII.GetBytes("BRGEND01"),0,active.Piece,40,8);
                            active.Offset=0;active.Footer=true;
                        }
                    }
                    if(active.Piece==null||active.Piece.Length==0)continue;
                    int count=Math.Min(block.Length-length,active.Piece.Length-active.Offset);
                    Buffer.BlockCopy(active.Piece,active.Offset,block,length,count);
                    if(!active.Footer) {active.Payload.TransformBlock(active.Piece,active.Offset,count,null,0);active.Bytes+=count;}
                    active.Offset+=count;length+=count;
                }
                if(length!=0) {long writeStart=Stopwatch.GetTimestamp();active.Stream.Write(block,0,length);long hashStart=Stopwatch.GetTimestamp();maxIoTicks=Math.Max(maxIoTicks,hashStart-writeStart);active.Whole.TransformBlock(block,0,length,null,0);long finished=Stopwatch.GetTimestamp();maxHashTicks=Math.Max(maxHashTicks,finished-hashStart);writtenBytes+=length;maxWriteTicks=Math.Max(maxWriteTicks,finished-writeStart);}
                if(active.Footer&&active.Offset==active.Piece.Length)
                {
                    long finishStart=Stopwatch.GetTimestamp();active.Whole.TransformFinalBlock(Array.Empty<byte>(),0,0);active.Stream.Close();active.Stream=null;
                    File.Move(active.Partial,active.Final);maxFinishTicks=Math.Max(maxFinishTicks,Stopwatch.GetTimestamp()-finishStart);
                    emit(active.Session,"input-artifact-complete","definition="+active.Id+",schema="+active.Schema+",path=["+active.Final+"],bytes="+(active.Bytes+48)+",sha256="+Hex(active.Whole.Hash)+",payloadSha256="+Hex(active.Payload.Hash)+",complete=True");active=null;
                }
            }
            catch(Exception error)
            {
                if(active!=null) {failures.Add(active.Session);try {active.Stream?.Close();}catch {}emit(active.Session,"input-artifact-failed","definition="+active.Id+",complete=False,reason="+error.GetType().Name);active=null;}
            }
            finally {long elapsed=Stopwatch.GetTimestamp()-start;writtenTicks+=elapsed;maxPumpTicks=Math.Max(maxPumpTicks,elapsed);if(elapsed*1000.0/Stopwatch.Frequency>2)pumpOverBudget++;}
        }
        internal static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-","");
        internal static IEnumerable<byte[]> Section(string name,Array data,int width,bool transientBuffers=false)
        {
            byte[] label=Encoding.UTF8.GetBytes(name);var header=new byte[12+label.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(label.Length),0,header,0,4);Buffer.BlockCopy(label,0,header,4,label.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(data.Length),0,header,4+label.Length,4);Buffer.BlockCopy(BitConverter.GetBytes(width),0,header,8+label.Length,4);currentSection=name;yield return header;
            // Private streaming contract: Current is consumed in full before MoveNext.
            // A writer never retains a yielded chunk after advancing this iterator.
            int bytes=checked(data.Length*width);byte[] block=null;
            for(int offset=0;offset<bytes;offset+=8192)
            {int length=Math.Min(8192,bytes-offset);if(!transientBuffers||block==null||block.Length!=length)block=new byte[length];Buffer.BlockCopy(data,offset,block,0,length);currentSection=name;yield return block;}
        }
        internal static byte[] Header(string metadata,int schema=1)
        {
            byte[] text=Encoding.UTF8.GetBytes(metadata);var result=new byte[16+text.Length];Buffer.BlockCopy(Encoding.ASCII.GetBytes("BRGINP01"),0,result,0,8);
            Buffer.BlockCopy(BitConverter.GetBytes(schema),0,result,8,4);Buffer.BlockCopy(BitConverter.GetBytes(text.Length),0,result,12,4);Buffer.BlockCopy(text,0,result,16,text.Length);return result;
        }
    }
}
