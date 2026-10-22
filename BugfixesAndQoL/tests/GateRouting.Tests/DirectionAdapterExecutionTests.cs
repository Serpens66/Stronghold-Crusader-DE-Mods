using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using APIShared.GateRouting.Native;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.GateRouting
{
    // Executes all eleven REAL producers/comparisons/branches on private memory.
    // The oracle is the installed original instruction stream with an adjusted
    // input byte, not a second implementation of the native algorithm.
    internal static class DirectionAdapterExecutionTests
    {
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr length, uint flags, uint protection);
        [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr p, UIntPtr size, uint kind);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(IntPtr output, long tile, int compare);
        private static byte[] Assemble(Assembler a, ulong ip)
        { using(var stream=new MemoryStream()){a.Assemble(new StreamCodeWriter(stream),ip);return stream.ToArray();} }
        private static void Put(IntPtr arena, int offset, byte[] bytes) => Marshal.Copy(bytes,0,IntPtr.Add(arena,offset),bytes.Length);
        private static Instruction[] Decode(byte[] bytes, ulong ip)
        {
            var decoder=Decoder.Create(64,new ByteArrayCodeReader(bytes));decoder.IP=ip;var result=new List<Instruction>();
            while(decoder.IP<ip+(ulong)bytes.Length){decoder.Decode(out Instruction instruction);result.Add(instruction);}return result.ToArray();
        }
        private static void Check(bool ok,string why){if(!ok)throw new Exception("Direction machine fixture: "+why);}
        private static readonly MethodInfo Baseline=typeof(DirectionAdapterExecutionTests).Assembly
            .GetType("APIShared.GateRouting.Native.BaselineDirectionFilterEmitter")?.GetMethod("AssembleAndValidate",BindingFlags.NonPublic|BindingFlags.Static);
        internal static void Run()
        {
            IntPtr arena=VirtualAlloc(IntPtr.Zero,(UIntPtr)0x5400000,0x3000,0x40);
            Check(arena!=IntPtr.Zero,"private arena");
            IntPtr slots=Marshal.AllocHGlobal(2048),mask=Marshal.AllocHGlobal(320800),output=Marshal.AllocHGlobal(160);
            try
            {
                for(int i=0;i<2048;i++)Marshal.WriteByte(slots,i,0);
                uint tid=GetCurrentThreadId();IntPtr slot=IntPtr.Add(slots,(int)(tid&63)*32);
                ulong b=(ulong)arena.ToInt64();long checks=0;
                for(int site=0;site<11;site++)
                {
                    Console.WriteLine("Direction adapter native oracle site="+site);
                    int capture=0x2010000,wrapper=0x2020000,raw=0x2000000,filtered=0x2001000;
                    var original=Decode(EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site),b+(uint)EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]);
                    var unmodified=new Assembler(64);foreach(var i in original)unmodified.AddInstruction(i);unmodified.jmp(b+(uint)capture);
                    Put(arena,raw,Assemble(unmodified,b+(uint)raw));
                    var adapter=new Assembler(64);DirectionFilterAdapterEmitter.Emit(adapter,original,site,(ulong)slots.ToInt64());adapter.jmp(b+(uint)capture);
                    Put(arena,filtered,Assemble(adapter,b+(uint)filtered));
                    if(Baseline!=null)
                    {
                        byte[] previous=(byte[])Baseline.Invoke(null,new object[]{EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site),
                            original[0].IP,site,(ulong)slots.ToInt64(),b+(uint)(filtered+0x1000)});
                        Put(arena,filtered+0x1000,previous);
                        var tail=new Assembler(64);tail.jmp(b+(uint)capture);
                        Put(arena,filtered+0x1000+previous.Length,Assemble(tail,b+(uint)(filtered+0x1000+previous.Length)));
                    }
                    // Every original outgoing branch is redirected to the same
                    // capture point in the PRIVATE image; no game code is touched.
                    foreach(var i in original.Where(i=>i.FlowControl==FlowControl.ConditionalBranch||i.FlowControl==FlowControl.UnconditionalBranch))
                    {
                        var branch=new Assembler(64);branch.jmp(b+(uint)capture);Put(arena,checked((int)(i.NearBranchTarget-b)),Assemble(branch,i.NearBranchTarget));
                    }
                    var record=new Assembler(64);
                    AssemblerRegister64[] registers={rax,rcx,rdx,rbx,rsp,rbp,rsi,rdi,r8,r9,r10,r11,r12,r13,r14,r15};
                    for(int i=0;i<registers.Length;i++)record.mov(__qword_ptr[r15+i*8],registers[i]);
                    record.pushfq();record.pop(__qword_ptr[r15+128]);record.ret();Put(arena,capture,Assemble(record,b+(uint)capture));
                    for(int variant=0;variant<(Baseline==null?2:3);variant++)
                    {
                        var a=new Assembler(64);a.push(rbp);a.push(rbx);a.push(rdi);a.push(rsi);a.push(r12);a.push(r13);a.push(r14);a.push(r15);a.sub(rsp,40);
                        a.mov(r15,rcx);a.mov(rdi,rdx);a.mov(ebp,r8d);a.mov(__qword_ptr[r15+136],rsp);
                        if(site==0)a.mov(rbp,b+0x312620UL);
                        a.mov(rbx,site==3?b:0x1111222233334444UL);a.mov(esi,3);a.mov(r14,0x4444555566667777UL);
                        a.mov(rdx,b);a.mov(rcx,site==5?b:site==3?b+0x312620UL:0x2222333344445555UL);
                        a.mov(r13,b);a.mov(r12,rdi);a.mov(r9,site==10?b:0x9999AAAABBBBCCCCUL);
                        a.mov(r10,site==3?rdi:rax); // overwritten immediately below for non-site-3
                        if(site!=3)a.mov(r10d,0);
                        if(site==4)a.mov(rax,rdi);else a.mov(rax,0xAABBCCDDEEFF0011UL);
                        if(site==0||site==5)a.mov(r8,rdi);else a.mov(r8d,19);
                        a.mov(r11,0x88889999AAAABBBBUL);a.cmp(esi,esi);
                        a.call(b+(uint)(variant==0?raw:variant==1?filtered:filtered+0x1000));
                        a.add(rsp,40);a.pop(r15);a.pop(r14);a.pop(r13);a.pop(r12);a.pop(rsi);a.pop(rdi);a.pop(rbx);a.pop(rbp);a.ret();
                        Put(arena,wrapper+variant*0x1000,Assemble(a,b+(uint)(wrapper+variant*0x1000)));
                    }
                    var oracle=(Probe)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(arena,wrapper),typeof(Probe));
                    var actual=(Probe)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(arena,wrapper+0x1000),typeof(Probe));
                    foreach(long tile in new long[]{0,7,320799,320800,-1})foreach(byte value in new byte[]{0,1,127,128,255})
                    foreach(byte filter in new byte[]{0,85,170,255})foreach(int context in new[]{0,1,2,3,4})foreach(int compare in new[]{int.MinValue,int.MaxValue})
                    {
                        bool applies=(context==1||context==4)&&tile>=0&&tile<320800;
                        byte produced=(byte)(applies?value&filter:value);
                        Marshal.WriteInt32(slot,context==0?0:context==2?(int)tid+64:(int)tid);
                        Marshal.WriteIntPtr(slot,8,context==3?IntPtr.Zero:mask);Marshal.WriteInt64(slot,16,0);Marshal.WriteInt32(slot,24,context==4?2:1);
                        if(tile>=0&&tile<320800)Marshal.WriteByte(mask,(int)tile,filter);
                        int grid=checked((int)(0x51890D0L+tile));
                        int source=site>=6&&site<=9?0x312620+(site-6):grid;
                        if(site==0||site==3)Marshal.WriteByte(arena,0x312620,255);
                        Marshal.WriteByte(arena,grid,value);
                        Marshal.WriteInt16(arena,checked((int)(0x52C254EL+tile*2)),(short)(compare==int.MinValue?19:20));
                        Marshal.WriteByte(arena,source,produced);oracle(output,tile,compare);
                        var expected=new long[17];for(int i=0;i<17;i++)expected[i]=Marshal.ReadInt64(output,i*8);
                        if((site==1||site==2)&&applies)
                            expected[11]=(long)((ulong)(uint)(int)(sbyte)value&~255UL)|(byte)(value&filter);
                        Marshal.WriteByte(arena,source,value);actual(output,tile,compare);
                        // Site 10's MOVZX/LEA do not define flags. In the audited
                        // DC3C0 continuation, ADD at DC56B overwrites them before
                        // any flag reader. All other spans define observable flags.
                        for(int i=0;i<17;i++)if(i!=4 && !(site==10&&i==16))
                        {Check(expected[i]==Marshal.ReadInt64(output,i*8),"site="+site+",register="+i+",tile="+tile+",value="+value+",mask="+filter+",context="+context);checks++;}
                        Check(Marshal.ReadInt64(output,32)==Marshal.ReadInt64(output,136)-8,"balanced native stack");
                        Check(Marshal.ReadInt64(slot,16)==(applies&&value!=produced?1:0),"exact changed-edge counter");
                        Check(Marshal.ReadInt32(slot,24)==(context==4?2:1),"nested context depth unchanged");
                    }
                    GC.KeepAlive(oracle);GC.KeepAlive(actual);
                    if(Baseline!=null) Benchmark(arena,slot,mask,output,site,tid);
                }
                Console.WriteLine("PASS: eleven executed direction adapters; "+checks+" original-oracle GPR/flags comparisons, active/inactive/colliding slots, signed bytes, bounds, stack and counters.");
            }
            finally { Marshal.FreeHGlobal(output);Marshal.FreeHGlobal(mask);Marshal.FreeHGlobal(slots);VirtualFree(arena,UIntPtr.Zero,0x8000); }
        }
        private static void Benchmark(IntPtr arena,IntPtr slot,IntPtr mask,IntPtr output,int site,uint tid)
        {
            const int count=100000;
            ulong b=(ulong)arena.ToInt64(); var loops=new Probe[2];
            for(int variant=0;variant<2;variant++)
            {
                int offset=0x2030000+variant*0x1000;var a=new Assembler(64);
                a.push(rbx);a.push(r12);a.push(r13);a.push(r14);a.sub(rsp,40);
                a.mov(rbx,rcx);a.mov(r12,rdx);a.mov(r13d,r8d);a.mov(r14d,count);var loop=a.CreateLabel();a.Label(ref loop);
                a.mov(rcx,rbx);a.mov(rdx,r12);a.mov(r8d,r13d);
                a.call(b+0x2020000UL+(uint)(variant==0?0x2000:0x1000));
                a.dec(r14d);a.jne(loop);a.add(rsp,40);a.pop(r14);a.pop(r13);a.pop(r12);a.pop(rbx);a.ret();
                Put(arena,offset,Assemble(a,b+(uint)offset));loops[variant]=(Probe)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(arena,offset),typeof(Probe));
            }
            Marshal.WriteByte(mask,7,170);Marshal.WriteByte(arena,0x51890D0+7,255);
            Marshal.WriteByte(arena,0x312620,255);if(site>=6&&site<=9)Marshal.WriteByte(arena,0x312620+site-6,255);
            Marshal.WriteInt16(arena,0x52C254E+14,19);
            foreach(bool active in new[]{false,true})
            {
                Marshal.WriteInt32(slot,active?(int)tid:0);Marshal.WriteIntPtr(slot,8,mask);
                var times=new List<double>[] {new List<double>(),new List<double>()};
                for(int pass=0;pass<9;pass++)for(int n=0;n<2;n++)
                {
                    int variant=(n+pass)%2;long start=Stopwatch.GetTimestamp();loops[variant](output,7,int.MinValue);
                    if(pass>=2)times[variant].Add((Stopwatch.GetTimestamp()-start)*1e9/Stopwatch.Frequency/count);
                }
                Console.WriteLine("DIRECTION_BENCH site={0} context={1} beforeNs={2:F2} afterNs={3:F2} iterations={4} medianPasses=7 includesPrivateWrapper=true",
                    site,active?"active":"inactive",times[0].OrderBy(t=>t).ElementAt(3),times[1].OrderBy(t=>t).ElementAt(3),count);
            }
            GC.KeepAlive(loops);
        }
    }
}
