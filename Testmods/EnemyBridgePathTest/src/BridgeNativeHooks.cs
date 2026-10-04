using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EnemyBridgePathTest
{
    // Rooted by the static plugin runtime. Installed hooks have no teardown path.
    internal sealed class BridgeNativeHooks
    {
        private readonly BridgeDecisionTrace trace;
        private readonly List<IHook> permanent = new List<IHook>();
        private readonly List<Action> validate = new List<Action>();
        private readonly List<Delegate> callbacks = new List<Delegate>();
        private ulong libraryBase;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void V0();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void V2(IntPtr p, int a);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void V3(IntPtr p, int a, int b);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void V4(IntPtr p, int a, int b, int c);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void V5(IntPtr p, int a, int b, int c, int d);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Assign(IntPtr p, short a, int b, short c, short d);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void AssignWide(IntPtr p, short a, long unused, short c, short d);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int R2(IntPtr p, int a);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate long L2(IntPtr p, int a);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate long L3(IntPtr p, int a, int b);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int R7(IntPtr p, int a, int b, int c, int d, int e, int f);

        internal BridgeNativeHooks(BridgeDecisionTrace trace) { this.trace = trace; }
        internal static NativeDetourBackend CreateBackend(int scanLimit) => new NativeDetourBackend(new NativeDetourOptions
        {
            AllowedSchemes = DetourScheme.Absolute, FollowCalls = false, FollowJumps = false,
            Chaining = HookChainingPolicy.Refuse, FunctionScanLimit = scanLimit
        });
        internal void Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            string backendPath = typeof(NativeDetourBackend).Assembly.Location;
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(backendPath))
                if (BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != BridgeNativeDefinition.BackendHash)
                    throw new InvalidOperationException("Installed NativeX64 backend changed; re-audit before enabling diagnosis.");
            libraryBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            trace.SetNative(context.ModuleHandle, context.Memory.Length);
            // Check every live entry before preparing any hook. A second owner fails closed.
            foreach (var site in BridgeNativeDefinition.Sites)
            {
                if (site.Rva < 0 || site.Rva + site.Bytes.Length > context.Memory.Length)
                    throw new InvalidOperationException("Native entry outside loaded module");
                byte[] live = new byte[site.Bytes.Length];
                Marshal.Copy(new IntPtr(unchecked((long)(libraryBase + (uint)site.Rva))), live, 0, live.Length);
                for (int i = 0; i < live.Length; i++)
                    if (live[i] != site.Bytes[i]) throw new InvalidOperationException("Native entry already changed: " + site.Name);
            }
            PrepareAll();
            foreach (var check in validate) check();
            // All originals and delegates already rooted. No normal path ever disables these hooks.
            foreach (var hook in permanent) hook.Enable();
            foreach (var hook in permanent)
                if(!hook.IsInstalled) throw new InvalidOperationException("Native diagnostic hook not installed");
            foreach (var check in validate) check();
            trace.MarkInstalled(permanent.Count);
            Shared.DebugLogHelper.LogInfo(log, "bridge decision hooks installed: backend=NativeX64,scheme=Absolute,entries=" +
                permanent.Count + ",extraNativeSearches=0,policyWrites=0,ownedGateAndMovementEntries=untouched");
        }
        private NativeDetour<T> Prepare<T>(BridgeNativeDefinition.Site site, T callback) where T : Delegate
        {
            callbacks.Add(callback);
            var request = new DetourRequest<T> { Name = "bridge-diagnosis-" + site.Name,
                TargetAddress = libraryBase + (uint)site.Rva, Callback = callback };
            var hook = (NativeDetour<T>)CreateBackend(site.Size).CreateDetour(in request);
            permanent.Add(hook);
            validate.Add(() => Validate(hook, site, libraryBase + (uint)site.Rva));
            return hook;
        }
        internal static void Validate<T>(NativeDetour<T> hook, BridgeNativeDefinition.Site site, ulong address) where T : Delegate
        {
            if (hook.Scheme != DetourScheme.Absolute || hook.DisplacedByteCount != site.Bytes.Length ||
                hook.TargetAddress != address || hook.PointerSlot != IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero || hook.TrampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("NativeX64 contract mismatch: " + site.Name);
            if (!hook.IsInstalled) return;
            byte[] patch = new byte[site.Bytes.Length];
            Marshal.Copy(new IntPtr(unchecked((long)address)), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 || BitConverter.ToInt32(patch, 2) != 0 ||
                BitConverter.ToInt64(patch, 6) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("NativeX64 Absolute patch mismatch: " + site.Name);
            for (int i = 14; i < patch.Length; i++)
                if (patch[i] != 0x90) throw new InvalidOperationException("NativeX64 padding mismatch");
        }
        // Each wrapper has exactly one original call, outside diagnostic exception handling.
        private void AddV2(BridgeNativeDefinition.Site s)
        {
            NativeDetour<V2> h = null;
            V2 cb = (p,a) => { var t = trace.Enter(s,p,a); bool ok=false;
                try { h.Original(p,a); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h = Prepare(s,cb);
        }
        private void AddV3(BridgeNativeDefinition.Site s)
        {
            NativeDetour<V3> h = null;
            V3 cb = (p,a,b) => { var t=trace.Enter(s,p,a,b); bool ok=false;
                try { h.Original(p,a,b); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h=Prepare(s,cb);
        }
        private void AddV4(BridgeNativeDefinition.Site s)
        {
            NativeDetour<V4> h=null;
            V4 cb=(p,a,b,c) => { var t=trace.Enter(s,p,a,b,c); bool ok=false;
                try { h.Original(p,a,b,c); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h=Prepare(s,cb);
        }
        private void AddV5(BridgeNativeDefinition.Site s)
        {
            NativeDetour<V5> h=null;
            V5 cb=(p,a,b,c,d) => { var t=trace.Enter(s,p,a,b,c,d); bool ok=false;
                try { h.Original(p,a,b,c,d); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h=Prepare(s,cb);
        }
        private void AddAssign(BridgeNativeDefinition.Site s)
        {
            NativeDetour<Assign> h=null;
            Assign cb=(p,a,b,c,d) => { var t=trace.Enter(s,p,a,b,c,d); bool ok=false;
                try { h.Original(p,a,b,c,d); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h=Prepare(s,cb);
        }
        private void AddAssignWide(BridgeNativeDefinition.Site s)
        {
            NativeDetour<AssignWide> h=null;
            AssignWide cb=(p,a,b,c,d) => { var t=trace.Enter(s,p,a,unchecked((int)b),c,d); bool ok=false;
                try { h.Original(p,a,b,c,d); ok=true; } finally { trace.Exit(t,ok,null,p); } };
            h=Prepare(s,cb);
        }
        private void AddR2(BridgeNativeDefinition.Site s)
        {
            NativeDetour<R2> h=null;
            R2 cb=(p,a) => { var t=trace.Enter(s,p,a); int result=0; bool ok=false;
                try { result=h.Original(p,a); ok=true; return result; } finally { trace.Exit(t,ok,result,p); } };
            h=Prepare(s,cb);
        }
        private void AddL2(BridgeNativeDefinition.Site s)
        {
            NativeDetour<L2> h=null;
            L2 cb=(p,a) => { var t=trace.Enter(s,p,a); long result=0; bool ok=false;
                try { result=h.Original(p,a); ok=true; return result; } finally { trace.Exit(t,ok,result,p); } };
            h=Prepare(s,cb);
        }
        private void AddL3(BridgeNativeDefinition.Site s)
        {
            NativeDetour<L3> h=null;
            L3 cb=(p,a,b) => { var t=trace.Enter(s,p,a,b); long result=0; bool ok=false;
                try { result=h.Original(p,a,b); ok=true; return result; } finally { trace.Exit(t,ok,result,p); } };
            h=Prepare(s,cb);
        }
        private void AddR7(BridgeNativeDefinition.Site s)
        {
            NativeDetour<R7> h=null;
            R7 cb=(p,a,b,c,d,e,f) => { var t=trace.Enter(s,p,a,b,c,d,e,f); int result=0; bool ok=false;
                try { result=h.Original(p,a,b,c,d,e,f); ok=true; return result; } finally { trace.Exit(t,ok,result,p); } };
            h=Prepare(s,cb);
        }
        private void AddV0(BridgeNativeDefinition.Site s)
        {
            NativeDetour<V0> h=null;
            V0 cb=() => { var t=trace.Enter(s,IntPtr.Zero); bool ok=false;
                try { h.Original(); ok=true; } finally { trace.Exit(t,ok,null,IntPtr.Zero); } };
            h=Prepare(s,cb);
        }
        private void PrepareAll()
        {
            foreach (var s in BridgeNativeDefinition.Sites)
                switch (s.Signature)
                {
                    case "V0": AddV0(s); break; case "V2": AddV2(s); break;
                    case "V3": AddV3(s); break; case "V4": AddV4(s); break;
                    case "V5": AddV5(s); break; case "Assign": AddAssign(s); break;
                    case "AssignWide": AddAssignWide(s); break;
                    case "R2": AddR2(s); break; case "L2": AddL2(s); break;
                    case "L3": AddL3(s); break; case "R7": AddR7(s); break;
                    default: throw new InvalidOperationException("Unknown audited ABI");
                }
        }
    }
}
