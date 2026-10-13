using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared.GateRouting.Native;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
namespace BugfixesAndQoL.GateRouting
{
    internal static class GateNativeDetourTests
    {
        [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint allocation,uint protection);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Call();
        private static readonly List<object> Roots=new List<object>();
        internal static int Run()
        {
            int checks=0;
            int[] rvas={0x1232E0,0xDC3C0,0x113BC0};
            byte[][] starts={new byte[]{0x48,0x89,0x5c,0x24,0x08,0x57},new byte[]{0x48,0x89,0x5c,0x24,0x20,0x55},new byte[]{0x48,0x89,0x5c,0x24,0x08,0x48,0x89,0x6c,0x24,0x10}};
            for(int n=0;n<3;n++)
            {
                IntPtr page=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
                if(page==IntPtr.Zero)throw new InvalidOperationException("Private gate detour test allocation failed");
                Roots.Add(page);IntPtr counter=page+512;
                var code=new List<byte>(starts[n]);code.AddRange(new byte[]{0x48,0xb8});code.AddRange(BitConverter.GetBytes(counter.ToInt64()));code.AddRange(new byte[]{0xff,0x00});
                if(n<2)code.Add(n==0?(byte)0x5f:(byte)0x5d);
                code.Add(0xc3);Marshal.Copy(code.ToArray(),0,page,code.Count);
                int calls=0;NativeDetour<Call> hook=null;
                Call callback=()=>{calls++;hook.Original();};
                var request=new DetourRequest<Call>{Name="private migrated gate detour",TargetAddress=(ulong)page.ToInt64(),Callback=callback};
                hook=(NativeDetour<Call>)GateNativeDetourContract.Backend.CreateDetour(in request);
                GateNativeDetourContract.Validate(hook,request.TargetAddress,GateNativeDetourContract.Length(rvas[n]),false);checks++;
                for(int i=0;i<starts[n].Length;i++)if(Marshal.ReadByte(hook.TrampolineAddress,i)!=starts[n][i])throw new InvalidOperationException("Relocated gate prologue differs");
                Roots.Add(hook);Roots.Add(callback);
                hook.Enable();GateNativeDetourContract.Validate(hook,request.TargetAddress,starts[n].Length,true);checks++;
                Marshal.GetDelegateForFunctionPointer<Call>(page)();
                if(calls!=1||Marshal.ReadInt32(counter)!=1)throw new InvalidOperationException("Gate callback/original not executed exactly once");checks++;
            }
            Console.WriteLine("PASS: migrated NativeX64 Indirect detours: 6/6/10 bytes, pointer slots, prologues, one callback and one original execution.");
            return checks;
        }
    }
}
