using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;

namespace EnemyBridgePathTest
{
    internal static class TraceTransportTests
    {
        private sealed class ThrowingArtifact : System.Collections.Generic.IEnumerable<byte[]>
        {
            public System.Collections.Generic.IEnumerator<byte[]> GetEnumerator()=>throw new InvalidOperationException("fixture-before-publication");
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
        }
        private static System.Collections.Generic.IEnumerable<byte[]> MinimalArtifact(){yield return BridgeInputArtifact.Header("fixture=True\n",1);}
        private static void PairReservation()
        {
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pair-reservation-"+Guid.NewGuid().ToString("N"));var writer=new BridgeInputArtifact(folder,(_,__,___)=>{});bool threw=false;
            try{writer.EnqueuePair(1,1,MinimalArtifact(),2,new ThrowingArtifact(),1);}catch(InvalidOperationException){threw=true;}
            if(!threw||writer.Pending(1)||!writer.Status(1).Contains("inputArtifactsRequested=0"))throw new Exception("unpublished pair failure must preserve both slots");
            if(!writer.EnqueuePair(1,1,MinimalArtifact(),2,MinimalArtifact(),1)||writer.EnqueuePair(1,3,MinimalArtifact(),4,MinimalArtifact(),1))throw new Exception("pair atomically reserves exactly two slots");
            int frames=0;while(writer.Pending(1)&&frames++<20)writer.Pump();if(writer.Pending(1)||!File.Exists(Path.Combine(folder,"bridge-1-1.bin"))||!File.Exists(Path.Combine(folder,"bridge-1-2.bin")))throw new Exception("bounded pair delivery");
            if(!writer.Enqueue(2,5,MinimalArtifact(),1)||writer.EnqueuePair(2,6,MinimalArtifact(),7,MinimalArtifact(),1)||!writer.Status(2).Contains("inputArtifactsRequested=1"))throw new Exception("existing independent slot cannot partially accept a pair");
        }
        internal static int Run()
        {
            PairReservation();
            if(BridgeTraceTransport.WarmUp()<0||BridgeTraceTransport.WarmUp()!=0)throw new Exception("publisher warmup must be once and synthetic");
            string prefix="[Info : bridge] bridge trace seq=1,session=9,thread=15,tick=2,clock=9223372036854775807,physical=3,topology=4,";
            string detail="rows=["+string.Concat(Enumerable.Repeat("-9223372036854775808/4294967297/8/225464/0/0;",32))+"],unicode=Br\u00FCcke";
            string plain=prefix+"kind=command-frame-batch,"+detail;
            string packed=BridgeTraceTransport.EncodeBatch(new[]{plain},out int raw,out int saved);
            if(saved<=0||BridgeTraceTransport.DecodeLines(packed).Single()!=plain)throw new Exception("transport exact signed/unsigned/Unicode roundtrip");
            bool rejected=false;try{BridgeTraceTransport.DecodeLines(packed.Substring(0,packed.Length-1));}catch(Exception){rejected=true;}
            if(!rejected)throw new Exception("transport truncation accepted");
            int hash=packed.IndexOf("sha256=")+7;
            rejected=false;try{BridgeTraceTransport.DecodeLines(packed.Substring(0,hash)+(packed[hash]=='0'?'1':'0')+packed.Substring(hash+1));}catch(InvalidDataException){rejected=true;}
            if(!rejected)throw new Exception("transport corruption accepted");
            string oversized=prefix+"kind=context,"+new string('x',BridgeTraceTransport.MaximumBytes+1);
            rejected=false;try{BridgeTraceTransport.EncodeBatch(new[]{oversized},out raw,out saved);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("transport maximum bound");
            if(BridgeTraceTransport.DecodeLines(plain).Single()!=plain)throw new Exception("historical plain transport changed");
            if(BridgeTraceTransport.DecodeLines(prefix+"kind=session-delivered,deliveryComplete=True").Single()!=prefix+"kind=session-delivered,deliveryComplete=True")throw new Exception("plain delivery marker changed");
            var rows=Enumerable.Range(0,16).Select(i=>plain.Replace("seq=1,","seq="+(i+1)+"," )).ToArray();
            string batch=BridgeTraceTransport.EncodeBatch(rows,out raw,out saved);
            if(batch==null||saved<=0||!BridgeTraceTransport.DecodeLines(batch).SequenceEqual(rows))throw new Exception("batch preserves every envelope and row");
            rejected=false;try{BridgeTraceTransport.DecodeLines(batch.Replace("records=16,","records=15,"));}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("batch count corruption accepted");
            Console.WriteLine("PASS bounded lossless publisher transport: full64-bit rows, Unicode, hash, truncation,64KiB bound and plain delivery markers");return 8;
        }
        internal static int Replay(string path)
        {
            var output=new StringBuilder();long oldBytes=0,newBytes=0,saved=0;int records=0,packedRecords=0;
            BridgeTraceTransport.WarmUp();long encodeTicks=0,decodeTicks=0,maxEncodeTicks=0,maxDecodeTicks=0;int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
            var watch=Stopwatch.StartNew();var pending=new System.Collections.Generic.List<string>();int pendingBytes=0;
            void Flush()
            {
                if(pending.Count==0)return;
                long started=Stopwatch.GetTimestamp();string wire=BridgeTraceTransport.EncodeBatch(pending,out int original,out int reduction);long elapsed=Stopwatch.GetTimestamp()-started;encodeTicks+=elapsed;maxEncodeTicks=Math.Max(maxEncodeTicks,elapsed);
                if(wire!=null)
                {started=Stopwatch.GetTimestamp();if(!BridgeTraceTransport.DecodeLines(wire).SequenceEqual(pending))throw new Exception("frozen batch changed any byte or field");elapsed=Stopwatch.GetTimestamp()-started;decodeTicks+=elapsed;maxDecodeTicks=Math.Max(maxDecodeTicks,elapsed);output.Append(wire).Append("\r\n");newBytes+=Encoding.UTF8.GetByteCount(wire)+2;packedRecords+=pending.Count;saved+=reduction;}
                else foreach(string line in pending){output.Append(line).Append("\r\n");newBytes+=Encoding.UTF8.GetByteCount(line)+2;}
                pending.Clear();pendingBytes=0;
            }
            foreach(string line in File.ReadLines(path))
            {
                if(line.Contains("bridge trace seq=")&&line.Contains(",kind="))
                {
                    int bytes=Encoding.UTF8.GetByteCount(line)+1;oldBytes+=bytes+1;records++;
                    bool direct=line.Contains("kind=session-end,")||line.Contains("kind=session-delivered,")||line.Contains("kind=interval-")||line.Contains("readiness")||line.Contains("kind=input-artifact-");
                    if(direct||bytes>BridgeTraceTransport.MaximumBytes){Flush();output.Append(line).Append("\r\n");newBytes+=bytes+1;continue;}
                    if(pendingBytes+bytes>BridgeTraceTransport.MaximumBytes)Flush();
                    pending.Add(line);pendingBytes+=bytes;if(pending.Count==16)Flush();
                }
                else {Flush();output.Append(line).Append("\r\n");}
            }
            Flush();
            string result=path+".packed.log";File.WriteAllText(result,output.ToString(),new UTF8Encoding(false));
            Console.WriteLine("Offline transport cost: encodeMs="+(encodeTicks*1000.0/Stopwatch.Frequency)+",decodeMs="+(decodeTicks*1000.0/Stopwatch.Frequency)+",maxEncodeMs="+(maxEncodeTicks*1000.0/Stopwatch.Frequency)+",maxDecodeMs="+(maxDecodeTicks*1000.0/Stopwatch.Frequency)+",GC0="+(GC.CollectionCount(0)-gc0)+",GC1="+(GC.CollectionCount(1)-gc1)+",GC2="+(GC.CollectionCount(2)-gc2)+",gamePublisherCost=not-measured");
            Console.WriteLine("PASS production transport frozen replay:records="+records+",packed="+packedRecords+",bridgeBytesWithPrefixes="+oldBytes+"->"+newBytes+",saved="+saved+",offlineEncodeDecodeMs="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",gameCost=not-measured,output="+result);return 0;
        }
    }
}
