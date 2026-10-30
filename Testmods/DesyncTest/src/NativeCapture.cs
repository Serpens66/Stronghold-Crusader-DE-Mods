using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace DesyncTest
{
    internal sealed unsafe class NativeCapture
    {
        internal readonly Recorder Recorder;
        internal readonly byte* Base;
        private readonly int imageSize;
        private readonly Dictionary<long,NativeHooks.Handler> handlers=new Dictionary<long,NativeHooks.Handler>();
        [ThreadStatic] private static NativeCapture dispatchOwner;
        [ThreadStatic] private static CaptureEvent dispatchContext;
        [ThreadStatic] private static bool dispatchContextValid;
        private int lastSample=-1,lastCheckpoint=-1;
        private int lastCheckpointMode=-1,lastCheckpointCommand=-1;
        internal bool UnitContract=true;
        // All offsets are pinned to the audited DLL, not inferred from mutable struct comments.
        internal const int Manager=0x8574320, TickRva=0x3665F58, WireUnits=0x67E8A58, Paths=0x7338278;
        internal NativeCapture(Recorder recorder,IntPtr module,int size)
        {
            Recorder=recorder;Base=(byte*)module;imageSize=size;
            if(size<Manager+0x370BFC)throw new InvalidOperationException("Native image lacks audited data ranges.");
            for(int i=0;i<=120;i++)
            {
                long address=*(long*)(Base+0x2C7A30+i*8);
                if(address!=0&&!handlers.ContainsKey(address))handlers.Add(address,Marshal.GetDelegateForFunctionPointer<NativeHooks.Handler>((IntPtr)address));
            }
        }
        internal int Tick => *(int*)(Base+TickRva);
        internal byte* M => Base+Manager;
        internal bool Active => Recorder.Active;
        internal void ResetSession() { lastSample=-1;lastCheckpoint=-1;lastCheckpointMode=-1;lastCheckpointCommand=-1; }
        internal CaptureEvent Context(RecordKind kind,int stage=0)
        {
            var e=new CaptureEvent{Kind=kind,Tick=Tick,Phase=*(int*)(M+0x84CCC),Opcode=-1,Player=*(int*)(M+0x36FA4C),Command=-1,Planned=-1,Stage=stage};
            int slot=*(int*)(M+0x84CC8);
            if(slot>=0&&slot<500)
            {byte* p=M+slot*0x500;e.Opcode=*(sbyte*)(p+0xB0C00);e.Command=*(int*)(p+0xB0C04);e.Planned=*(int*)(p+0xB0BF8);if(e.Phase!=0)e.Player=*(int*)(p+0xB0BFC);}
            return e;
        }
        internal void Dispatch(IntPtr target)
        {
            // The audited table dispatch ABI has no arguments and no consumed result.
            if(!handlers.TryGetValue(target.ToInt64(),out var original))
            { original=Marshal.GetDelegateForFunctionPointer<NativeHooks.Handler>(target);handlers[target.ToInt64()]=original; }
            CaptureEvent e=default;bool valid=false;
            if(Active)try {e=Context(RecordKind.Dispatch);valid=true;Recorder.Record(e,IntPtr.Zero,0);}catch{Interlocked.Increment(ref Recorder.CallbackFailures);}
            var previousOwner=dispatchOwner;var previousContext=dispatchContext;bool previousValid=dispatchContextValid;
            dispatchOwner=this;dispatchContext=e;dispatchContextValid=valid;
            try
            {
              original(); // Always exactly once; capture failures never suppress Vanilla.
              if(Active&&valid)try
              {
                e.Stage=1;Recorder.Record(e,IntPtr.Zero,0);
                if(e.Phase==0&&e.Opcode==56)CaptureUnit(*(int*)(M+0x14D00C),4,RecordKind.Unit,e.Phase);
                if(e.Phase==0&&e.Opcode==101)Recorder.Record(Context(RecordKind.CategoryFlags),(IntPtr)(M+0x14D34C),14);
              }catch{Interlocked.Increment(ref Recorder.CallbackFailures);}
            }
            finally{dispatchOwner=previousOwner;dispatchContext=previousContext;dispatchContextValid=previousValid;}
        }
        internal void Arrival(int player,IntPtr data,int length)
        {
            if(!Active||length<1||length>180000||data==IntPtr.Zero)return;
            // Transport callback only copies supplied bytes; it never reads mutable simulation state.
            byte* wire=(byte*)data;
            var e=new CaptureEvent{Kind=RecordKind.Arrival,Tick=-1,Phase=-1,Opcode=wire[0],Player=player,Command=-1,Planned=-1};
            // Only ordinary inner Chores use this header. Compression/sync/ping retain raw data.
            if(length>=8&&e.Opcode>=2&&e.Opcode<=120)
            {e.Planned=wire[1]|wire[2]<<8|wire[3]<<16;e.Command=*(int*)(wire+4);}
            Recorder.Record(e,data,length);
        }
        internal void BeforeField(IntPtr manager,IntPtr field,int length,int scheduled,int unpack)
        {
            if(!Active||manager!=(IntPtr)M||field==IntPtr.Zero||length<1||length>180000)return;
            // Several handlers change the native phase from pack(1) to execute(0) before
            // transferring fields. Preserve the entry context, including nested dispatches.
            var e=dispatchOwner==this&&dispatchContextValid?dispatchContext:Context(RecordKind.Field);
            e.Kind=RecordKind.Field;e.Global=unpack; // Field.Global is the audited transfer direction.
            int offset=*(int*)(M+0x370BF8),slot=*(int*)(M+0x84CC8);
            int limit=scheduled==0?180000:0x4EC;
            if(offset<0||offset>limit-length||slot<0||slot>=500){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            byte* wire=scheduled==0?M+0x84CD8+offset:M+0xB0C09+slot*0x500+offset;
            if(e.Phase==0&&e.Opcode==56&&unpack==1&&offset==0&&length==4)
                CaptureUnit(*(int*)wire,0,RecordKind.Unit,e.Phase);
            // Capture actual wire fields during execution/packing, never mode-2 measurement.
            if(e.Phase==2)return;
            e.Unit=e.Opcode==56?*(int*)(M+0x14D00C):0;
            e.Stage=offset; // For Field records Stage stores wire offset, not an incident number.
            if(Recorder.IsDeep||*(int*)(M+0xDAC)!=0)
                Recorder.Record(e,unpack==1?(IntPtr)wire:field,length);
        }
        internal void Checksum(bool ran,int mode)
        {
            if(!Active||!ran)return;
            int player=*(int*)(M+0x36FA50);
            if(player<1||player>8){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            var e=Context(RecordKind.Checksum);e.Phase=mode==0?10:13;e.Stage=mode;e.Player=player;
            e.Planned=*(int*)(M+0x14D0E4+player*4);
            Recorder.Record(e,(IntPtr)(M+0x14D108+player*56),56);
            if(!Recorder.IsDeep||!UnitContract||(lastCheckpoint==Tick&&lastCheckpointMode==mode&&lastCheckpointCommand==e.Command))return;
            lastCheckpoint=Tick;lastCheckpointMode=mode;lastCheckpointCommand=e.Command;
            for(int id=1;id<10000;id++)
            {
                if(!Recorder.IsDeep)break;
                byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
                uint actual=*(uint*)(M+0x14F0CC+(player*10000+id)*4);
                if(UnitChecksum.Compute(record,path)!=actual)
                {
                    UnitContract=false;Interlocked.Increment(ref Recorder.HashMismatches);
                    var failure=Context(RecordKind.Fault);failure.Unit=id;failure.Phase=e.Phase;
                    Recorder.Record(failure,IntPtr.Zero,0);return;
                }
                CaptureUnit(id,0,RecordKind.WorldUnit,e.Phase);
            }
        }
        internal void Sample()
        {
            if(!Active||!Recorder.IsDeep||!UnitContract||Tick==lastSample||Tick%Recorder.Options.Interval!=0)return;
            lastSample=Tick;
            byte* hash=stackalloc byte[12];
            for(int id=1;id<10000;id++)
            {
                if(!Recorder.IsDeep)break;
                byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
                *(uint*)hash=UnitChecksum.Compute(record,path);*(ulong*)(hash+4)=UnitChecksum.Strong(record,path);
                var e=Context(RecordKind.UnitHash);e.Phase=11;e.Unit=id;e.Global=*(int*)(record+UnitChecksum.GlobalOffset);
                Recorder.Record(e,(IntPtr)hash,12);
            }
            foreach(int id in Recorder.Options.Focus){if(!Recorder.IsDeep)break;CaptureUnit(id,0,RecordKind.WorldUnit,11);}
        }
        private void CaptureUnit(int id,int stage,RecordKind kind,int phase)
        {
            if(id<1||id>9999){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
            var e=Context(kind,stage);e.Phase=phase;e.Unit=id;e.Global=*(int*)(record+UnitChecksum.GlobalOffset);
            Recorder.Record(e,(IntPtr)record,UnitChecksum.RecordSize);e.Stage=stage+1;
            Recorder.Record(e,(IntPtr)path,UnitChecksum.PathSize);
        }
        internal void Forced(int tick,int expected,int started,int now)
        {
            if(!Active)return;
            Recorder.Record(new CaptureEvent{Kind=RecordKind.ForcedRun,Tick=tick,Phase=12,Planned=expected,Command=started,Player=now,Opcode=120},IntPtr.Zero,0);
        }
    }
}
