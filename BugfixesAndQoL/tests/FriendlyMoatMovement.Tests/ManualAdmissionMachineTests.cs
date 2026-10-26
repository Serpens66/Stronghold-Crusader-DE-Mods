using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL.UnitCommands;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class ManualAdmissionMachineTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Probe(IntPtr units, int id, long tribe);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr ThreadToken();
    private static readonly List<object> roots = new();
    internal static void Validate()
    {
        const int maximum = 1000, bytes = (maximum+1)*0x490+0xB00;
        byte* units = (byte*)NativeMemory.AllocZeroed(bytes);
        byte* paths = (byte*)NativeMemory.Alloc((maximum+1)*1000);
        IntPtr* slot = (IntPtr*)NativeMemory.AllocZeroed((nuint)IntPtr.Size);
        var tokenCode = ExecutableFunction.Create(ManualGroupAdmissionGate.EmitThreadToken);
        roots.Add(tokenCode);
        IntPtr token = tokenCode.GetDelegate<ThreadToken>()();
        var batch = new ManualRegionAdmissionBatch { Tribe = 77 };
        var rejected = ExecutableFunction.Create(a => {
            // The gate must restore ZF, live scratch registers and the wrapper stack.
            var error=a.CreateLabel();a.jnz(error);
            a.cmp(r10,101);a.jne(error);a.cmp(r11,102);a.jne(error);
            a.xor(eax,eax);a.pop(r15);a.pop(rsi);a.pop(r13);a.ret();
            a.Label(ref error);a.mov(eax,-99);a.pop(r15);a.pop(rsi);a.pop(r13);a.ret();
        });
        roots.Add(rejected);
        // Real order-writing prefix followed by a destructive failing path-builder
        // double. A return-count-only double would miss the original regression.
        var function=ExecutableFunction.Create(a=>{
            a.push(r13);a.push(rsi);a.push(r15);
            a.mov(r13,rcx);a.mov(eax,edx);a.mov(rsi,r8);a.mov(r15d,37);
            a.mov(ecx,eax);a.imul(rcx,rcx,0x490);
            a.mov(r10,101);a.mov(r11,102);a.cmp(eax,eax);
            a.db(ManualGroupAdmissionGate.Bytes);
            var error=a.CreateLabel();a.jnz(error);
            a.cmp(r10,101);a.jne(error);a.cmp(r11,102);a.jne(error);
            a.mov(__word_ptr[rcx+r13+0x918],dx);
            a.mov(__dword_ptr[rcx+r13+0x748],0);
            a.mov(__word_ptr[rcx+r13+0x744],37);
            a.mov(r11,unchecked((ulong)(nuint)paths));a.mov(r10d,eax);a.imul(r10,r10,1000);
            a.mov(__qword_ptr[r11+r10],0);
            a.mov(eax,1);a.pop(r15);a.pop(rsi);a.pop(r13);a.ret();
            a.Label(ref error);a.mov(eax,-98);a.pop(r15);a.pop(rsi);a.pop(r13);a.ret();
        });
        roots.Add(function);
        byte[] code = new byte[160]; Marshal.Copy(function.EntryPoint,code,0,code.Length);
        int index=Enumerable.Range(0,code.Length-14).Single(i=>code.AsSpan(i,14).SequenceEqual(ManualGroupAdmissionGate.Bytes));
        var hook=new X64InlineHook(function.Address+(uint)index,14,NullLogger.Instance,"admission machine contract");
        hook.Generate((a,original,resume)=>{
            ManualGroupAdmissionGate.Emit(a,(IntPtr)slot,rejected.Address);
            a.AddInstructions(original);
        });
        if(hook.DisplacedByteCount!=14)throw new Exception("Admission displaced span changed");
        hook.Enable();roots.Add(hook);
        Probe run=function.GetDelegate<Probe>();
        int assertions=0;
        void Check(bool result,string name){assertions++;if(!result)throw new Exception(name);}
        long generation=0;
        foreach(int count in new[]{100,200,550,1000})
        {
            new Span<byte>(units,bytes).Fill(0xA5);
            new Span<byte>(paths,(maximum+1)*1000).Fill(0xB6);
            batch.Reset(++generation);
            for(int id=1;id<=count;id++)
            {
                *(uint*)(units+id*0x490+0x6F0)=(uint)(500+id);
                batch.Add(id,(uint)(500+id),id<=count/2?7:42,0,true,true);
            }
            batch.Evaluate(1,42,(p,s,t,m)=>0);batch.Publish(token,(IntPtr)units);*slot=batch.NativeState;
            for(int id=1;id<=count;id++)
            {
                byte[] state=new ReadOnlySpan<byte>(units+id*0x490+0x658,0x490).ToArray();
                byte[] path=new ReadOnlySpan<byte>(paths+id*1000,1000).ToArray();
                int result=run((IntPtr)units,id,77);
                bool blocked=id<=count/2;
                Check(result==(blocked?0:1),"gate follows per-member regional admission");
                Check(new ReadOnlySpan<byte>(units+id*0x490+0x658,0x490).SequenceEqual(state)==blocked,
                    "blocked running unit retains all native target, movement and order bytes");
                Check(new ReadOnlySpan<byte>(paths+id*1000,1000).SequenceEqual(path)==blocked,
                    "blocked running unit retains its packed path");
            }
            Check(batch.Queries==1,"one native graph query for the entire split selection");
        }
        // Wrong tribe/thread, stale generation and recycled IDs must not apply a foreign mask.
        *slot=batch.NativeState;
        Check(run((IntPtr)units,1,78)==1,"other tribe cannot inherit mask");
        *(IntPtr*)batch.NativeState=(IntPtr)123;
        Check(run((IntPtr)units,1,77)==1,"other thread cannot inherit mask");
        batch.Publish(token,(IntPtr)units);*(uint*)(units+0x490+0x6F0)=9999;
        Check(run((IntPtr)units,1,77)==1,"recycled ID cannot inherit mask");
        *(uint*)(units+0x490+0x6F0)=501;
        batch.Reset(++generation);batch.Publish(token,(IntPtr)units);
        Check(run((IntPtr)units,1,77)==1,"stale generation cannot inherit mask");
        *slot=IntPtr.Zero;
        Check(run((IntPtr)units,1,77)==1,"inactive gate replays original writes");

        // This measures only filter bookkeeping and the real generated gate. Native
        // E2610/engine latency still requires separate in-game measurements.
        var times=new List<double>();
        for(int iteration=0;iteration<120;iteration++)
        {
            long start=Stopwatch.GetTimestamp();batch.Reset(++generation);
            for(int id=1;id<=maximum;id++)batch.Add(id,(uint)(500+id),id<=500?7:42,0,true,true);
            batch.Evaluate(1,42,(p,s,t,m)=>0);batch.Publish(token,(IntPtr)units);*slot=batch.NativeState;
            for(int id=1;id<=maximum;id++)run((IntPtr)units,id,77);
            if(iteration>=20)times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        times.Sort();
        Console.WriteLine($"PASS: {assertions} executed native admission assertions; real inline hook, order/path preservation, identity/thread/generation, 100/200/550/1000.");
        Console.WriteLine($"FILTER HARNESS ONLY 1000 units p95={times[94]:F3}ms p99={times[98]:F3}ms; graph/engine timing excluded.");
        *slot=IntPtr.Zero;
        // Published stub dependencies remain allocated until process exit.
    }
}
