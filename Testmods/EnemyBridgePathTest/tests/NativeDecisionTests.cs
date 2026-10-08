using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EnemyBridgePathTest
{
    internal static class NativeDecisionTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Leaf(IntPtr p,int a);
        [DllImport("kernel32",SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr p,UIntPtr size,uint allocation,uint protect);
        [DllImport("kernel32",SetLastError=true)] private static extern bool VirtualFree(IntPtr p,UIntPtr size,uint type);
        private static int checks;
        internal static int Run()
        {
            const string path="E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll";
            byte[] image=File.ReadAllBytes(path);
            using(var sha=SHA256.Create()) Check(BitConverter.ToString(sha.ComputeHash(image)).Replace("-","")==BridgeNativeDefinition.NativeHash,"canonical hash");
            ReproducePatchedAttack(image);
            ReproducePatchedSelection(image);
            foreach(var site in BridgeNativeDefinition.Sites)
            {
                if(!BridgeNativeDefinition.OwnsEntry(site))continue;
                int fileOffset=Offset(image,site.Rva);
                byte[] original=new byte[site.Size]; Array.Copy(image,fileOffset,original,0,original.Length);
                for(int i=0;i<site.Bytes.Length;i++) Check(original[i]==site.Bytes[i],"exact prologue");
                IntPtr copy=Marshal.AllocHGlobal(original.Length);
                NativeDetour<Leaf> hook=null; Leaf callback=(_,a)=>a;
                try
                {
                    Marshal.Copy(original,0,copy,original.Length);
                    var request=new DetourRequest<Leaf>{Name="copied bridge "+site.Name,TargetAddress=unchecked((ulong)copy.ToInt64()),Callback=callback};
                    hook=(NativeDetour<Leaf>)BridgeNativeHooks.CreateBackend(site.Size).CreateDetour(in request);
                    BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));
                    hook.Enable(); BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));
                }
                finally
                {
                    // Unpublished copied-memory test candidate only; never a game/runtime hook.
                    hook?.Dispose();
                    byte[] restored=new byte[original.Length]; Marshal.Copy(copy,restored,0,restored.Length);
                    Check(Equal(restored,original),"copied candidate restores original bytes");
                    Marshal.FreeHGlobal(copy); GC.KeepAlive(callback);
                }
            }
            // Execute the actual production R2 wrapper against an independent native leaf.
            IntPtr leaf=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
            Check(leaf!=IntPtr.Zero,"executable fixture allocation");
            var bytes=new byte[32]; for(int i=0;i<bytes.Length;i++) bytes[i]=0x90;
            bytes[0]=0x8B; bytes[1]=0xC2; bytes[14]=0xC3; // mov eax,edx; nops; ret
            Marshal.Copy(bytes,0,leaf,bytes.Length);
            var trace=new BridgeDecisionTrace(null,_=>new BridgeDecisionTrace.AttackStamp(1,1,1,0,0,0,0),()=>1); trace.StartSession(1); var runtime=new BridgeNativeHooks(trace);
            typeof(BridgeNativeHooks).GetField("libraryBase",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(runtime,unchecked((ulong)leaf.ToInt64()));
            var synthetic=new BridgeNativeDefinition.Site(0,"synthetic","R2","8BC2909090909090909090909090",32);
            typeof(BridgeNativeHooks).GetMethod("AddR2",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(runtime,new object[]{synthetic});
            var hooks=(List<IHook>)typeof(BridgeNativeHooks).GetField("permanent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
            try
            {
                hooks[0].Enable();
                var invoke=Marshal.GetDelegateForFunctionPointer<Leaf>(leaf);
                foreach(int value in new[]{0,1,-1,123456,int.MinValue,int.MaxValue}) Check(invoke(IntPtr.Zero,value)==value,"actual wrapper preserves native return");
                Check(trace.Entered==6&&trace.Exited==6,"actual wrapper pairs exactly once");
                trace.MarkInstalled(BridgeNativeDefinition.OwnedCount);
                Check(trace.NativeReadiness.Contains("topologyAvailable=False")&&!trace.Summary().Contains("nativeCoverageComplete=True"),"missing shared publisher cannot claim full coverage");
                APIShared.EnemyBridgeDiagnosticBridge.PublishTopologyOwner();
                Check(trace.NativeReadiness.Contains("nativeCallsObserved=True")&&trace.Summary().Contains("nativeCoverageComplete=True"),"ready and completed current-map calls are separate evidence");
                trace.StartSession(2);
                Check(trace.NativeReadiness.Contains("nativeCallsObserved=False")&&trace.Summary().Contains("nativeCoverageComplete=False"),"previous map native counts cannot satisfy new-map coverage");
            }
            finally { hooks[0].Dispose(); VirtualFree(leaf,UIntPtr.Zero,0x8000); GC.KeepAlive(runtime); }
            ExerciseVoidWrappers();
            ExerciseAccessWrapper();
            return checks;
        }
        private static void ReproducePatchedSelection(byte[] image)
        {
            var site=Array.Find(BridgeNativeDefinition.Sites,s=>s.Rva==0x2C5A0);
            byte[] bytes=new byte[site.Size];Array.Copy(image,Offset(image,site.Rva),bytes,0,bytes.Length);
            IntPtr copy=Marshal.AllocHGlobal(bytes.Length),stub=Marshal.AllocHGlobal(32);NativeDetour<Leaf> hook=null;Leaf callback=(_,a)=>a;
            try
            {
                int patch=0x2C5E1-site.Rva;bytes[patch]=0xff;bytes[patch+1]=0x25;Array.Clear(bytes,patch+2,4);Array.Copy(BitConverter.GetBytes(stub.ToInt64()),0,bytes,patch+6,8);Marshal.Copy(bytes,0,copy,bytes.Length);
                var request=new DetourRequest<Leaf>{Name="copied Fixes-owned target-count body",TargetAddress=unchecked((ulong)copy.ToInt64()),Callback=callback};
                hook=(NativeDetour<Leaf>)BridgeNativeHooks.CreateBackend(site.Size).CreateDetour(in request);
                BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));hook.Enable();BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));
                Console.WriteLine("PASS exact installed NativeX64 backend on copied Fixes-patched 2C5A0 body");
            }
            finally {hook?.Dispose();Marshal.FreeHGlobal(copy);Marshal.FreeHGlobal(stub);GC.KeepAlive(callback);}
        }
        private static void ReproducePatchedAttack(byte[] image)
        {
            var site=Array.Find(BridgeNativeDefinition.Sites,s=>s.Rva==0x3C2E0);
            var bytes=new byte[site.Size];Array.Copy(image,Offset(image,site.Rva),bytes,0,bytes.Length);
            // Exact Fixes patch sites and stub pointers from the failed 20:22 start.
            var baseline=(byte[])bytes.Clone();
            BridgeNativeHooks.ValidateAttackBody(baseline,baseline);
            foreach(var patch in new[]{Tuple.Create(0x3C30F,0x00007FFDC7716490L,18),Tuple.Create(0x3C3D9,0x00007FFDC7716310L,36)})
            {
                int offset=patch.Item1-site.Rva;for(int i=0;i<patch.Item3;i++)bytes[offset+i]=0x90;
                bytes[offset]=0xFF;bytes[offset+1]=0x25;Array.Clear(bytes,offset+2,4);
                Array.Copy(BitConverter.GetBytes(patch.Item2),0,bytes,offset+6,8);
            }
            BridgeNativeHooks.ValidateAttackBody(baseline,bytes);
            var unknown=(byte[])bytes.Clone();unknown[80]^=1;bool refused=false;
            try {BridgeNativeHooks.ValidateAttackBody(baseline,unknown);}catch(InvalidOperationException){refused=true;}
            Check(refused,"unknown live-body mutation rejected");
            unknown=(byte[])bytes.Clone();unknown[0x2F+17]=0xCC;refused=false;
            try {BridgeNativeHooks.ValidateAttackBody(baseline,unknown);}catch(InvalidOperationException){refused=true;}
            Check(refused,"unknown inline tail rejected");
            IntPtr copy=Marshal.AllocHGlobal(bytes.Length);NativeDetour<BridgeNativeHooks.V2> hook=null;
            BridgeNativeHooks.V2 callback=(_,a)=>{};
            try
            {
                Marshal.Copy(bytes,0,copy,bytes.Length);
                var request=new DetourRequest<BridgeNativeHooks.V2>{Name="failed live-body replay",TargetAddress=unchecked((ulong)copy.ToInt64()),Callback=callback};
                hook=(NativeDetour<BridgeNativeHooks.V2>)BridgeNativeHooks.CreateBackend(site.Size).CreateDetour(in request);
                Console.WriteLine("Patched attack full-body scan: expected="+site.Bytes.Length+",actual="+hook.DisplacedByteCount+",scheme="+hook.Scheme);
                Check(hook.DisplacedByteCount!=site.Bytes.Length,"reproduced inline pointer interpreted as incoming branch");
                refused=false;
                try {BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));}
                catch(InvalidOperationException error) {refused=error.Message.Contains("displaced=57/expected:15")&&error.Message.Contains("phase=prepared")&&error.Message.Contains("pointerSlot=")&&error.Message.Contains("trampoline=");}
                Check(refused,"mismatch explains exact prepared contract without relaxing it");
                hook.Dispose();hook=null;
                hook=(NativeDetour<BridgeNativeHooks.V2>)BridgeNativeHooks.CreateBackend(BridgeNativeHooks.ScanLimit(site)).CreateDetour(in request);
                BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));
                hook.Enable();BridgeNativeHooks.Validate(hook,site,unchecked((ulong)copy.ToInt64()));
                Check(hook.DisplacedByteCount==15,"patched body with bounded audited scan publishes exact 15-byte V2 contract");
            }
            finally {hook?.Dispose();Marshal.FreeHGlobal(copy);GC.KeepAlive(callback);}
            ExercisePreparationRollback();
        }
        private static void ExercisePreparationRollback()
        {
            var bytes=new byte[32];for(int i=0;i<bytes.Length;i++)bytes[i]=0x90;bytes[14]=0xC3;
            IntPtr copy=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,copy,bytes.Length);
            var runtime=new BridgeNativeHooks(new BridgeDecisionTrace(null,testCapture:()=>1));
            typeof(BridgeNativeHooks).GetField("libraryBase",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(runtime,unchecked((ulong)copy.ToInt64()));
            var site=new BridgeNativeDefinition.Site(0,"rollback-copy","V2","9090909090909090909090909090",32);
            typeof(BridgeNativeHooks).GetMethod("AddV2",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(runtime,new object[]{site});
            var hooks=(List<IHook>)typeof(BridgeNativeHooks).GetField("permanent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
            Check(hooks.Count==1&&!hooks[0].IsInstalled,"candidate prepared but never published");
            typeof(BridgeNativeHooks).GetMethod("RollbackUnpublished",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(runtime,null);
            Check(hooks.Count==0,"failed candidate rollback clears roots");
            var restored=new byte[32];Marshal.Copy(copy,restored,0,32);Check(Equal(bytes,restored),"candidate rollback preserves live bytes");
            Marshal.FreeHGlobal(copy);
        }
        private static void ExerciseVoidWrappers()
        {
            foreach(bool wide in new[]{false,true})
            {
                IntPtr code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
                IntPtr data=Marshal.AllocHGlobal(12);
                Check(code!=IntPtr.Zero,"void native fixture allocation");
                // mov [rcx],edx; mov [rcx+4],r8d; inc dword [rcx+8]; nops; ret
                var bytes=new byte[32];for(int i=0;i<32;i++)bytes[i]=0x90;
                byte[] body={0x89,0x11,0x44,0x89,0x41,0x04,0xFF,0x41,0x08};Array.Copy(body,bytes,body.Length);bytes[14]=0xC3;
                Marshal.Copy(bytes,0,code,bytes.Length);
                bool fail=false;
                var trace=new BridgeDecisionTrace(null,_=>default,()=>{if(fail)throw new Exception("capture fixture");return 1;});trace.StartSession(1);
                var runtime=new BridgeNativeHooks(trace);
                typeof(BridgeNativeHooks).GetField("libraryBase",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(runtime,unchecked((ulong)code.ToInt64()));
                var site=new BridgeNativeDefinition.Site(0,wide?"fixture-V3":"fixture-V2",wide?"V3":"V2","891144894104FF41089090909090",32);
                typeof(BridgeNativeHooks).GetMethod(wide?"AddV3":"AddV2",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(runtime,new object[]{site});
                var hooks=(List<IHook>)typeof(BridgeNativeHooks).GetField("permanent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
                try
                {
                    hooks[0].Enable();
                    var validate=(List<Action>)typeof(BridgeNativeHooks).GetField("validate",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);foreach(var action in validate)action();
                    Marshal.WriteInt32(data,8,0);
                    for(int i=0;i<2;i++)
                    {
                        fail=i==1;int a=i==0?int.MinValue:12345,b=i==0?int.MaxValue:-19;
                        if(wide)Marshal.GetDelegateForFunctionPointer<BridgeNativeHooks.V3>(code)(data,a,b);
                        else Marshal.GetDelegateForFunctionPointer<BridgeNativeHooks.V2>(code)(data,a);
                        Check(Marshal.ReadInt32(data)==a&&(!wide||Marshal.ReadInt32(data,4)==b),"production void wrapper preserves native integer arguments");
                        Check(Marshal.ReadInt32(data,8)==i+1,"production void wrapper invokes original exactly once even on capture failure");
                    }
                    Check(trace.Entered==2&&trace.Exited==2&&trace.Failures==2,"void wrapper capture exceptions preserve balanced native execution");
                }
                finally {hooks[0].Dispose();Marshal.FreeHGlobal(data);VirtualFree(code,UIntPtr.Zero,0x8000);GC.KeepAlive(runtime);}
            }
        }
        private static void ExerciseAccessWrapper()
        {
            IntPtr code=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40),data=Marshal.AllocHGlobal(12);
            Check(code!=IntPtr.Zero,"access wrapper fixture allocation");
            byte[] bytes=new byte[32];for(int i=0;i<bytes.Length;i++)bytes[i]=0x90;
            byte[] body={0x89,0x11,0x44,0x89,0x41,0x04,0xFF,0x41,0x08};Array.Copy(body,bytes,body.Length);bytes[14]=0x48;bytes[15]=0xB8;
            Array.Copy(BitConverter.GetBytes(0x1122334455667788L),0,bytes,16,8);bytes[24]=0xC3;Marshal.Copy(bytes,0,code,32);
            bool fail=false;var trace=new BridgeDecisionTrace(null,_=>default,()=>{if(fail)throw new Exception("fixture");return 1;});trace.StartSession(1);
            var runtime=new BridgeNativeHooks(trace);typeof(BridgeNativeHooks).GetField("libraryBase",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(runtime,unchecked((ulong)code.ToInt64()));
            var site=new BridgeNativeDefinition.Site(0,"fixture-L3","L3","891144894104FF41089090909090",32);
            typeof(BridgeNativeHooks).GetMethod("AddL3",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(runtime,new object[]{site});
            var hooks=(List<IHook>)typeof(BridgeNativeHooks).GetField("permanent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(runtime);
            try
            {
                hooks[0].Enable();var call=Marshal.GetDelegateForFunctionPointer<BridgeNativeHooks.L3>(code);
                foreach(int a in new[]{1,8,-1,int.MaxValue})
                {
                    Marshal.WriteInt32(data,8,0);fail=a<0;long result=call(data,a,31);
                    Check(result==0x1122334455667788L&&Marshal.ReadInt32(data)==a&&Marshal.ReadInt32(data,4)==31&&Marshal.ReadInt32(data,8)==1,"actual L3 preserves players/full return and calls original once despite capture failure");
                }
                Check(trace.Entered==4&&trace.Exited==4,"actual L3 scope pairing");
            }
            finally {hooks[0].Dispose();VirtualFree(code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(data);GC.KeepAlive(runtime);}
        }
        private static bool Equal(byte[] a,byte[] b) { if(a.Length!=b.Length)return false; for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true; }
        private static void Check(bool value,string text) { checks++; if(!value)throw new Exception(text); }
        private static int Offset(byte[] image,int rva)
        {
            int pe=BitConverter.ToInt32(image,0x3C),count=BitConverter.ToUInt16(image,pe+6);
            int table=pe+24+BitConverter.ToUInt16(image,pe+20);
            for(int i=0;i<count;i++) { int at=table+i*40,va=BitConverter.ToInt32(image,at+12),size=BitConverter.ToInt32(image,at+16);
                if(rva>=va&&rva-va<size)return BitConverter.ToInt32(image,at+20)+rva-va; }
            throw new Exception("RVA not in raw section");
        }
    }

}
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static long Bytes;
        internal static readonly System.Collections.Generic.List<string> Recent=new System.Collections.Generic.List<string>();
        internal static bool ThrowNext;
        internal static void LogInfo(ManualLogSource log,string text)
        {
            if(ThrowNext) {ThrowNext=false;throw new IOException("fixture output failure");}
            Bytes+=System.Text.Encoding.UTF8.GetByteCount(text)+95; // conservative logger prefix plus CRLF
            if(Recent.Count>=10000)Recent.RemoveRange(0,5000);Recent.Add(text);
        }
        internal static void LogError(ManualLogSource log,string text) { throw new InvalidOperationException("Unexpected diagnostic error: "+text); }
        internal static string CurrentNativeSha256 => EnemyBridgePathTest.BridgeNativeDefinition.NativeHash;
        internal static bool ReportNativeLibraryVersion(ManualLogSource log,string name,bool requireCurrentVersion) => true;
    }
    internal sealed class GameplaySessionStartedContext
    {
        internal long SessionId=1;internal bool IsLoadedSave=false,IsEditor=false;
        internal readonly ModeInfo Mode=new ModeInfo();
    }
    internal sealed class ModeInfo {internal string ToDiagnosticString()=>"fixture";}
}
