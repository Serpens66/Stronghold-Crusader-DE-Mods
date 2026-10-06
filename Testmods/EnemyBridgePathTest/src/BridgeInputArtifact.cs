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
        private readonly Queue<Job> jobs=new Queue<Job>();
        private readonly Action<long,string,string> emit;
        private readonly string directory;
        private readonly Dictionary<long,int> accepted=new Dictionary<long,int>();
        private Job active;
        private readonly HashSet<long> failures=new HashSet<long>();
        private long writtenTicks,writtenBytes;
        private sealed class Job
        {
            internal long Session,Id,Bytes;
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
        internal bool Enqueue(long session,long id,IEnumerable<byte[]> source)
        {
            accepted.TryGetValue(session,out int count);if(count>=2||jobs.Count>=4)return false;
            accepted[session]=count+1;
            string name="bridge-"+session+"-"+id;
            jobs.Enqueue(new Job {Session=session,Id=id,Source=source.GetEnumerator(),Partial=Path.Combine(directory,name+".partial"),Final=Path.Combine(directory,name+".bin")});
            emit(session,"input-artifact-pending","definition="+id+",schema=1,slot="+(count+1)+",complete=False");return true;
        }
        internal void Pump()
        {
            long start=Stopwatch.GetTimestamp();
            try
            {
                if(active==null) {if(jobs.Count==0)return;active=jobs.Dequeue();Directory.CreateDirectory(directory);active.Stream=new FileStream(active.Partial,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096);}
                var block=new byte[BlockLimit];int length=0;
                while(length<block.Length&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<2)
                {
                    if(active.Piece==null||active.Offset==active.Piece.Length)
                    {
                        if(active.Footer)break;
                        if(active.Source.MoveNext()) {active.Piece=active.Source.Current;active.Offset=0;}
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
                if(length!=0) {active.Stream.Write(block,0,length);active.Whole.TransformBlock(block,0,length,null,0);writtenBytes+=length;}
                if(active.Footer&&active.Offset==active.Piece.Length)
                {
                    active.Whole.TransformFinalBlock(Array.Empty<byte>(),0,0);active.Stream.Close();active.Stream=null;
                    File.Move(active.Partial,active.Final);
                    emit(active.Session,"input-artifact-complete","definition="+active.Id+",schema=1,path=["+active.Final+"],bytes="+(active.Bytes+48)+",sha256="+Hex(active.Whole.Hash)+",payloadSha256="+Hex(active.Payload.Hash)+",complete=True");active=null;
                }
            }
            catch(Exception error)
            {
                if(active!=null) {failures.Add(active.Session);try {active.Stream?.Close();}catch {}emit(active.Session,"input-artifact-failed","definition="+active.Id+",complete=False,reason="+error.GetType().Name);active=null;}
            }
            finally {writtenTicks+=Stopwatch.GetTimestamp()-start;}
        }
        internal static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-","");
        internal static IEnumerable<byte[]> Section(string name,Array data,int width)
        {
            byte[] label=Encoding.UTF8.GetBytes(name);var header=new byte[12+label.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(label.Length),0,header,0,4);Buffer.BlockCopy(label,0,header,4,label.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(data.Length),0,header,4+label.Length,4);Buffer.BlockCopy(BitConverter.GetBytes(width),0,header,8+label.Length,4);yield return header;
            int bytes=checked(data.Length*width);for(int offset=0;offset<bytes;offset+=32768)
            {var block=new byte[Math.Min(32768,bytes-offset)];Buffer.BlockCopy(data,offset,block,0,block.Length);yield return block;}
        }
        internal static byte[] Header(string metadata)
        {
            byte[] text=Encoding.UTF8.GetBytes(metadata);var result=new byte[16+text.Length];Buffer.BlockCopy(Encoding.ASCII.GetBytes("BRGINP01"),0,result,0,8);
            Buffer.BlockCopy(BitConverter.GetBytes(1),0,result,8,4);Buffer.BlockCopy(BitConverter.GetBytes(text.Length),0,result,12,4);Buffer.BlockCopy(text,0,result,16,text.Length);return result;
        }
    }
}
