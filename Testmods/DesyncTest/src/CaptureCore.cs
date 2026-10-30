using System;
using System.Diagnostics;
using System.Threading;

namespace DesyncTest
{
    internal enum CaptureMode { Off, Light, Deep }
    internal enum RecordKind : ushort { Start=1, End, Arrival, Dispatch, Checksum, CategoryFlags, Field, Unit, UnitHash, WorldUnit, ForcedRun, DeepEnded, Fault }
    internal struct CaptureEvent
    {
        internal RecordKind Kind;
        internal long Session;
        internal int Tick, Phase, Opcode, Player, Command, Planned, Unit, Global, Stage;
    }

    // Wire contract v2: 80-byte little endian header followed by exactly payloadLength bytes.
    // No callbacks, file I/O or allocations while holding this monitor. Producers never wait.
    internal sealed unsafe class CaptureRing
    {
        internal const int HeaderSize = 80;
        private readonly byte[] data;
        private readonly object gate = new object();
        private int head, tail, used;
        private long sequence;
        internal long DroppedRecords, DroppedBytes, ContentionDrops, HistoryOverwrites, FlushThrough;
        private int limit;
        internal CaptureRing(int capacity) { if(capacity<256)throw new ArgumentOutOfRangeException(nameof(capacity)); data=new byte[capacity];limit=capacity; }
        internal void Limit(int capacity) { if(capacity<256||capacity>data.Length)throw new ArgumentOutOfRangeException(nameof(capacity));Volatile.Write(ref limit,capacity); }
        internal int Capacity => data.Length;
        internal bool IsEmpty
        {
            get { if(!Monitor.TryEnter(gate))return false;try{return used==0;}finally{Monitor.Exit(gate);} }
        }
        private int GetInt(int pos) { int n=0;for(int i=0;i<4;i++)n|=data[(pos+i)%data.Length]<<(8*i);return n; }
        private void Put(ref int pos, long n, int count) { for(int i=0;i<count;i++){data[pos]=(byte)n;pos=(pos+1)%data.Length;n>>=8;} }
        private void Copy(ref int pos, byte* source, int length)
        {
            int first=Math.Min(length,data.Length-pos);
            fixed(byte* d=data) { Buffer.MemoryCopy(source,d+pos,first,first); if(first<length)Buffer.MemoryCopy(source+first,d,length-first,length-first); }
            pos=(pos+length)%data.Length;
        }
        internal long Append(CaptureEvent e, IntPtr source, int length, bool flush)
        {
            if(length<0||length>data.Length-HeaderSize){Interlocked.Increment(ref DroppedRecords);Interlocked.Add(ref DroppedBytes,Math.Max(0,length));return 0;}
            if(!Monitor.TryEnter(gate)){Interlocked.Increment(ref ContentionDrops);return 0;}
            try
            {
                int total=HeaderSize+length;
                int bound=Volatile.Read(ref limit);
                // Retained Deep work must be drained, never overwritten to force a smaller Light limit.
                if(total>bound||used>bound){DroppedRecords++;DroppedBytes+=total;return 0;}
                while(bound-used<total)
                {
                    int old=GetInt(head);long oldSeq=0;for(int i=0;i<8;i++)oldSeq|=(long)data[(head+8+i)%data.Length]<<(8*i);
                    if(oldSeq<=Interlocked.Read(ref FlushThrough)){DroppedRecords++;DroppedBytes+=old;}else HistoryOverwrites++;
                    head=(head+old)%data.Length;used-=old;
                }
                long seq=++sequence;int p=tail;
                Put(ref p,total,4);Put(ref p,2,2);Put(ref p,(int)e.Kind,2);
                Put(ref p,seq,8);Put(ref p,e.Session,8);Put(ref p,Stopwatch.GetTimestamp(),8);Put(ref p,DateTime.UtcNow.Ticks,8);
                Put(ref p,e.Tick,4);Put(ref p,e.Phase,4);Put(ref p,e.Opcode,4);Put(ref p,e.Player,4);Put(ref p,e.Command,4);Put(ref p,e.Planned,4);
                Put(ref p,e.Unit,4);Put(ref p,e.Global,4);Put(ref p,e.Stage,4);Put(ref p,length,4);
                if(length>0)Copy(ref p,(byte*)source,length);
                tail=p;used+=total;
                if(flush)Interlocked.Exchange(ref FlushThrough,seq);
                return seq;
            }
            finally { Monitor.Exit(gate); }
        }
        internal int Drain(byte[] destination)
        {
            if(!Monitor.TryEnter(gate))return 0;
            try
            {
                int written=0;
                while(used>0)
                {
                    int length=GetInt(head);long seq=0;
                    for(int i=0;i<8;i++)seq|=(long)data[(head+8+i)%data.Length]<<(8*i);
                    if(seq>Interlocked.Read(ref FlushThrough)||length>destination.Length-written)break;
                    // Bound each ownership interval. A 64-MiB writer buffer must not exclude
                    // non-waiting simulation producers for the duration of a 64-MiB copy.
                    if(written>0&&length>256*1024-written)break;
                    int first=Math.Min(length,data.Length-head);
                    Buffer.BlockCopy(data,head,destination,written,first);
                    if(first<length)Buffer.BlockCopy(data,0,destination,written+first,length-first);
                    head=(head+length)%data.Length;used-=length;written+=length;
                }
                return written;
            }
            finally { Monitor.Exit(gate); }
        }
        internal void Flush() { lock(gate)Interlocked.Exchange(ref FlushThrough,sequence); }
    }

    internal static unsafe class UnitChecksum
    {
        // Native wire record starts four bytes BEFORE the Extender GameUnit view.
        internal const int RecordSize=0x490, PathSize=1000, HashStart=0x8C, HashLength=0x404, MaskStart=0x41C, GlobalOffset=0x98;
        internal static uint RotateAdd(byte* p,int start,int length,int zeroStart=-1,int zeroLength=0)
        {
            uint result=0,word=0;int count=0;
            for(int i=0;i<length;i++)
            {
                if(count==4){uint sum=unchecked(result+word);result=(sum<<1)|(sum>>31);word=0;count=0;}
                int offset=start+i;
                uint value=offset>=zeroStart&&offset<zeroStart+zeroLength?0:unchecked((uint)(sbyte)p[offset]);
                word=unchecked((word<<8)|value);count++;
            }
            return unchecked(result+word);
        }
        internal static uint Compute(byte* record,byte* path) => unchecked(
            RotateAdd(record,HashStart,HashLength,MaskStart,8)+RotateAdd(path,0,PathSize)+
            *(uint*)(record+0x3C)+*(uint*)(record+0x40)+*(uint*)(record+0x44));
        internal static ulong Strong(byte* record,byte* path)
        {
            ulong h=14695981039346656037UL;
            for(int i=HashStart;i<RecordSize;i++){byte v=i>=MaskStart&&i<MaskStart+8?(byte)0:record[i];h=unchecked((h^v)*1099511628211UL);}
            for(int i=0;i<PathSize;i++)h=unchecked((h^path[i])*1099511628211UL);
            for(int i=0x3C;i<0x48;i++)h=unchecked((h^record[i])*1099511628211UL);
            return h;
        }
    }

    internal sealed class DeepWindow
    {
        private long deadline;
        private int mode;
        internal CaptureMode Mode => (CaptureMode)Volatile.Read(ref mode);
        internal long Deadline => deadline;
        internal DeepWindow(CaptureMode mode,long now,long duration) { this.mode=(int)mode;deadline=checked(now+duration); }
        internal bool Expire(long now) { return now>=deadline&&Interlocked.CompareExchange(ref mode,(int)CaptureMode.Light,(int)CaptureMode.Deep)==(int)CaptureMode.Deep; }
    }
}
