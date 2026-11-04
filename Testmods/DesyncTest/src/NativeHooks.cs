using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Security.Cryptography;
using APIShared.NativeSites;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.Abstractions.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using static Iced.Intel.AssemblerRegisters;

namespace DesyncTest
{
    internal sealed class NativeHooks
    {
        internal const string NativeHash="FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string BackendHash="0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0";
        internal const string InlineBackendHash="E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB";
        internal const string AssemblerHash="CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F";
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Handler();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Dispatch(IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V1(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V2(IntPtr p,int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Field(IntPtr p,IntPtr field,int size,int scheduled,int unpack);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Receive(int player,IntPtr data,int length);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Forced(IntPtr text,int tick,int expected,int started,int now);
        private readonly NativeCapture capture;
        private readonly List<IHook> roots=new List<IHook>();
        private readonly List<Delegate> delegates=new List<Delegate>();
        private HookTransaction inline;
        private bool published;
        private ulong module;
        private readonly Action<string> report;
        internal NativeHooks(NativeCapture capture,Action<string> report=null) { this.capture=capture;this.report=report; }
        internal sealed class Site
        {
            internal readonly string Name;internal readonly int Rva;internal readonly byte[] Bytes;
            internal Site(string name,int rva,string hex){Name=name;Rva=rva;Bytes=new byte[hex.Length/2];for(int i=0;i<Bytes.Length;i++)Bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);}
        }
        internal static readonly Site[] Sites={
            new Site("checksum",0x22020,"89542410534881ec9000000083b9a80d000000"),
            new Site("scheduler",0xD1AD0,"48895c2408574883ec20448b8118383a00"),
            new Site("field",0x1F5F0,"4585c00f8e9300000048895c2408"),
            new Site("receive",0x86480,"418bc083f9ff751c3bc17518833d39ec4e0800"),
            new Site("execute",0x1F88B,"41ff14c7ffc6c6841f010c0b000a"),
            new Site("pack",0x23A98,"41ff14c48b8bd44c08004489bbf80b3700"),
            new Site("local-immediate",0x23B85,"4c89bb0cd01400480fbe87000c0b0041ff14c4"),
            new Site("measure",0x2400F,"41ff14c6488d0d2e9608068b93d44c0800"),
            new Site("remote-immediate",0x24084,"41ff14c6418bd4488bcbe81d9dffff"),
            new Site("forced",0x1BFAD,"e89e8a18004c8b1def3f32008b1d999f6403")};
        internal void Install(CrusaderLibraryLoadContext context)
        {
            module=unchecked((ulong)context.ModuleHandle.ToInt64());
            if(HashFile(typeof(NativeDetourBackend).Assembly.Location)!=BackendHash)throw new InvalidOperationException("NativeX64 backend needs re-audit.");
            if(HashFile(typeof(X64InlineHook).Assembly.Location)!=InlineBackendHash||HashFile(typeof(Assembler).Assembly.Location)!=AssemblerHash)
                throw new InvalidOperationException("Inline backend or assembler needs re-audit.");
            var claims=new List<Site>();
            try
            {
              foreach(var s in Sites)
              {
                if(s.Rva<0||s.Rva+s.Bytes.Length>context.Memory.Length)throw new InvalidOperationException("Site outside image: "+s.Name);
                var pristine=context.Memory.Slice(s.Rva,s.Bytes.Length);byte[] live=new byte[s.Bytes.Length];Marshal.Copy((IntPtr)(long)(module+(uint)s.Rva),live,0,live.Length);
                for(int i=0;i<live.Length;i++)if(pristine[i]!=s.Bytes[i]||live[i]!=s.Bytes[i])throw new InvalidOperationException("Site changed: "+s.Name);
                using(var sha=SHA256.Create())
                {
                    var claim=NativeSiteRegistry.Register(DesyncTestPlugin.Guid,"DesyncTest."+s.Name,s.Rva,s.Bytes.Length,NativeSiteMode.Exclusive,Hex(sha.ComputeHash(s.Bytes)));
                    if(!claim.Succeeded)throw new InvalidOperationException("Native site refused: "+claim.Reason);
                    claims.Add(s);
                }
              }
                NativeDetour<V2> checksum=null;
                checksum=Prepare(Sites[0],(V2)((p,mode)=>{
                    bool ran=false;
                    if(capture.Active)try{ran=Marshal.ReadInt32(p,0xDA8)==0&&Marshal.ReadInt32(p,0x279FA4)==0;}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}
                    checksum.Original(p,mode);
                    if(capture.Active)try{capture.Checksum(ran,mode);}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}
                }));
                NativeDetour<V1> scheduler=null;
                scheduler=Prepare(Sites[1],(V1)(p=>{scheduler.Original(p);if(capture.Active)try{capture.Sample();}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}}));
                NativeDetour<Field> field=null;
                field=Prepare(Sites[2],(Field)((p,f,n,s,u)=>{if(capture.Active)try{capture.BeforeField(p,f,n,s,u);}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}field.Original(p,f,n,s,u);}));
                NativeDetour<Receive> receive=null;
                receive=Prepare(Sites[3],(Receive)((p,d,n)=>{if(capture.Active)try{capture.Arrival(p,d,n);}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}return receive.Original(p,d,n);}));

                Dispatch dispatch=capture.Dispatch;delegates.Add(dispatch);
                ulong dispatchAddress=unchecked((ulong)Marshal.GetFunctionPointerForDelegate(dispatch).ToInt64());
                var dummy=Marshal.GetDelegateForFunctionPointer<Forced>((IntPtr)(long)(module+0x1A4A50));delegates.Add(dummy);
                Forced forced=(text,tick,expected,started,now)=>{
                    if(capture.Active)try{capture.Forced(tick,expected,started,now);}catch{Interlocked.Increment(ref capture.Recorder.CallbackFailures);}
                    dummy(text,tick,expected,started,now);
                };delegates.Add(forced);
                ulong forcedAddress=unchecked((ulong)Marshal.GetFunctionPointerForDelegate(forced).ToInt64());
                inline=new HookTransaction(context.Region,SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions{FailureMode=TransactionFailureMode.RollbackAndThrow,InstallOnCommit=false,OwnsHooks=true});
                var handles=new List<HookHandle<X64InlineHook>>();
                for(int i=4;i<Sites.Length;i++)
                {
                    var site=Sites[i];var handle=new HookHandle<X64InlineHook>();handles.Add(handle);
                    inline.AddInline(handle,HookTarget.FromAddress(module+(uint)site.Rva),(a,ins,ret)=>EmitInline(a,ins,site,dispatchAddress,forcedAddress),hookSize:site.Bytes.Length);
                }
                var result=inline.Commit();if(!result.IsCompleteSuccess)throw new InvalidOperationException("Inline transaction incomplete.");
                for(int i=0;i<handles.Count;i++)if(!handles[i].Success||handles[i].IsInstalled||handles[i].Hook.DisplacedByteCount!=Sites[i+4].Bytes.Length)
                    throw new InvalidOperationException("Inline displaced span mismatch: "+Sites[i+4].Name);
                // Preparation/relocation of ALL ten sites finishes before the first executable write.
                foreach(var s in Sites)
                {
                    var decoder=Decoder.Create(64,new ByteArrayCodeReader(s.Bytes));var lengths=new List<int>();
                    while(decoder.IP<(ulong)s.Bytes.Length){decoder.Decode(out var instruction);if(instruction.IsInvalid)throw new InvalidOperationException("Invalid instruction span.");lengths.Add(instruction.Length);}
                    var next=context.Memory.Slice(s.Rva+s.Bytes.Length,16).ToArray();var follow=Decoder.Create(64,new ByteArrayCodeReader(next));follow.Decode(out var first);
                    if(first.IsInvalid)throw new InvalidOperationException("Invalid continuation.");
                    report?.Invoke("DESYNC_SITE "+s.Name+" rva=0x"+s.Rva.ToString("X")+" end=0x"+(s.Rva+s.Bytes.Length).ToString("X")+" length="+s.Bytes.Length+" instructions="+string.Join(",",lengths)+" next="+first.Code);
                }
                // Every native original and callback is rooted before publication. No transaction can
                // roll published hooks back: its commit above only constructed inactive candidates.
                published=true;
                foreach(var h in roots)h.Enable();
                foreach(var h in handles)h.Hook.Enable();
                // Validate the exact backend-generated entry shape after publication too.
                foreach(var check in checks)check();
            }
            catch
            {
                if(!published){foreach(var h in roots)((IDisposable)h).Dispose();inline?.Dispose();foreach(var s in claims)NativeSiteRegistry.Unregister(DesyncTestPlugin.Guid,"DesyncTest."+s.Name);}
                throw;
            }
        }
        private readonly List<Action> checks=new List<Action>();
        internal static void EmitInline(Assembler a,ReadOnlySpan<Instruction> instructions,Site site,ulong dispatchAddress,ulong forcedAddress)
        {
            bool replaced=false;
            foreach(var instruction in instructions)
            {
                if(!replaced&&instruction.FlowControl==FlowControl.IndirectCall)
                {
                    if(site.Rva==0x1F88B)a.mov(rcx,__qword_ptr[r15+rax*8]);
                    else if(site.Rva==0x23A98||site.Rva==0x23B85)a.mov(rcx,__qword_ptr[r12+rax*8]);
                    else a.mov(rcx,__qword_ptr[r14+rax*8]);
                    a.AddUnrestrictedCall(dispatchAddress);replaced=true;
                }
                else if(!replaced&&site.Rva==0x1BFAD&&instruction.FlowControl==FlowControl.Call)
                {a.AddUnrestrictedCall(forcedAddress);replaced=true;}
                else a.AddInstruction(instruction);
            }
            if(!replaced)throw new InvalidOperationException("Audited call missing: "+site.Name);
        }
        private NativeDetour<T> Prepare<T>(Site site,T callback) where T:Delegate
        {
            delegates.Add(callback);
            var backend=new NativeDetourBackend(new NativeDetourOptions{AllowedSchemes=DetourScheme.Absolute,FollowCalls=false,FollowJumps=false,
                Chaining=HookChainingPolicy.Refuse,FunctionScanLimit=site.Bytes.Length,SuspendThreadsDuringPatch=false});
            var request=new DetourRequest<T>{Name="DesyncTest."+site.Name,TargetAddress=module+(uint)site.Rva,Callback=callback};
            var h=(NativeDetour<T>)backend.CreateDetour(in request);roots.Add(h);
            Action validate=()=>{
                if(h.Scheme!=DetourScheme.Absolute||h.DisplacedByteCount!=site.Bytes.Length||h.TargetAddress!=module+(uint)site.Rva||h.TrampolineAddress==IntPtr.Zero||h.HookEntryPointAddress==IntPtr.Zero||h.PointerSlot!=IntPtr.Zero)
                    throw new InvalidOperationException("Native detour contract mismatch: "+site.Name);
                if(h.IsInstalled)
                {
                    byte[] bytes=new byte[site.Bytes.Length];Marshal.Copy((IntPtr)(long)(module+(uint)site.Rva),bytes,0,bytes.Length);
                    if(bytes[0]!=0xFF||bytes[1]!=0x25||BitConverter.ToInt32(bytes,2)!=0||BitConverter.ToInt64(bytes,6)!=h.HookEntryPointAddress.ToInt64())throw new InvalidOperationException("Native detour patch mismatch.");
                }
            };
            validate();checks.Add(validate);return h;
        }
        internal static string HashFile(string path) {using(var s=File.OpenRead(path))using(var h=SHA256.Create())return Hex(h.ComputeHash(s));}
        private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","");
    }
}
