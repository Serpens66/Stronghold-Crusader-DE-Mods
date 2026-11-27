using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.DependencyBootstrap;
using APIShared.Patching;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using Instruction = Mono.Cecil.Cil.Instruction;

[assembly: EarlyPluginActivationProvider("BugfixesAndQoL_Serp","mounted-scanner-capture","fixes",
    "BugfixesAndQoL.TrappedUnitRescue.MountedScannerEarlyProvider","BeforeFixes")]

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Removable compatibility island. Permanent setup; host master/rescue gates only data.
    public static class MountedScannerEarlyProvider
    {
        private static SafeIlHook hook;
        private static SafeIlHook emitterHook;
        private static MountedScannerCapture capture;
        private static int attempted;
        private static string state;
        private static ManualLogSource log;
        private static int requested;
        internal static string State=>Volatile.Read(ref state);
        internal static void SetEnabled(bool enabled)
        {Volatile.Write(ref requested,enabled?1:0);capture?.SetEnabled(enabled);}
        internal static bool RecheckPublishedContracts()
        {
            bool valid=capture==null || capture.RecheckPublishedContracts();
            if(!valid)Volatile.Write(ref state,"Unavailable:native-target-contract-lost");
            return valid;
        }
        public static void BeforeFixes(EarlyPluginActivationContext context)
        {
            if(Interlocked.Exchange(ref attempted,1)!=0)return;
            log=Logger.CreateLogSource("RescueMountedScanner");
            try
            {
                if(context==null || context.Owner.Guid!="BugfixesAndQoL_Serp" || context.Target.Guid!="fixes" ||
                    context.LoadedType.Assembly.ManifestModule.ModuleVersionId!=context.LoadedModuleVersionId)
                    throw new InvalidOperationException("Selected provider identity unavailable.");
                MountedScannerPlan plan;MountedScannerClassification classification;string reason;
                using(var module=ModuleDefinition.ReadModule(context.Target.AssemblyPath))
                    {
                    if(module.Mvid!=context.LoadedModuleVersionId)throw new InvalidOperationException("Loaded/disk Fixes differs.");
                    classification=MountedScannerContract.Classify(module,out plan,out reason);
                    if(classification==MountedScannerClassification.Broken)MountedScannerContract.ValidateLoaded(context.LoadedType.Assembly,plan);
                }
                if(classification!=MountedScannerClassification.Broken)
                {Volatile.Write(ref state,classification+":"+reason);Write(classification==MountedScannerClassification.Unsupported,State);return;}
                if(!MountedScannerHelpers.Validate(out reason))
                {Volatile.Write(ref state,"Unsupported:"+reason);Write(true,State);return;}
                var target=context.LoadedType.Assembly.ManifestModule.ResolveMethod(plan.Install.MetadataToken.ToInt32());
                capture=new MountedScannerCapture(plan,new MountedScannerNativeReader(),message=>Write(true,message));
                // Emitter first: until the capture hook is also ready this emits the original CALL.
                var emitter=context.LoadedType.Assembly.ManifestModule.ResolveMethod(plan.Emitter.MetadataToken.ToInt32());
                if(!SafeIlHook.TryInstall("BugfixesAndQoL_Serp","TrappedUnitRescue.FixesMountedScannerDispatch",emitter,capture.PatchEmitter,log,out emitterHook,out reason))
                {Volatile.Write(ref state,"Unavailable:emitter-hook-rejected");Write(true,State);return;}
                if(!SafeIlHook.TryInstall("BugfixesAndQoL_Serp","TrappedUnitRescue.FixesMountedScannerCapture",target,capture.Patch,log,out hook,out reason))
                {Volatile.Write(ref state,"Unavailable:managed-hook-rejected");Write(true,State);return;}
                hook.Rebuilt+=value=>{if(!value.IsApplied)Lose();};
                emitterHook.Rebuilt+=value=>{if(!value.IsApplied)Lose();};
                capture.SetAvailable(true);capture.SetEnabled(Volatile.Read(ref requested)!=0);
                Volatile.Write(ref state,"InstalledAwaitingNative");Write(false,State+"; original Fixes option authoritative; correction follows host master/rescue setting");
            }
            catch{Volatile.Write(ref state,"Unavailable:early-contract-failed");Write(true,State);}
        }
        private static void Lose()
        {capture?.SetAvailable(false);Volatile.Write(ref state,"Unavailable:managed-rebuild-lost");Write(true,State);}
        private static void Write(bool error,string message)
        {
            try{string line="["+DateTime.Now.ToString("HH:mm:ss.fff")+"] MOUNTED_SCANNER "+message;if(error && message.StartsWith("ERROR ",StringComparison.Ordinal))log?.LogError(line);else if(error)log?.LogWarning(line);else log?.LogInfo(line);}catch{}
        }
    }
    // Real producer wrapper, dependency-injected only at its read-only native observation boundary.
    internal sealed class MountedScannerCapture
    {
        private readonly MountedScannerPlan plan;
        private readonly IMountedScannerNativeReader native;
        private readonly Action<string> report;
        // Native code only reads this process-lifetime cell. Never free after publication.
        private readonly IntPtr activation;
        private DataScanner originalScanner;
        private ulong capturedAddress,capturedModuleBase;
        private byte[] targetReference;
        private int available,requested,targetLost;
        private int captures,rejections;
        internal int Captures=>Volatile.Read(ref captures);
        internal int Rejections=>Volatile.Read(ref rejections);
        internal MountedScannerCapture(MountedScannerPlan plan,IMountedScannerNativeReader native,Action<string> report)
        {this.plan=plan??throw new ArgumentNullException(nameof(plan));this.native=native??throw new ArgumentNullException(nameof(native));this.report=report;
            activation=Marshal.AllocHGlobal(4);Marshal.WriteInt32(activation,0);}
        internal void SetAvailable(bool value){Volatile.Write(ref available,value && Volatile.Read(ref targetLost)==0?1:0);PublishGate();}
        internal void SetEnabled(bool value){Volatile.Write(ref requested,value?1:0);PublishGate();}
        private unsafe void PublishGate()=>Volatile.Write(ref *(int*)activation,Volatile.Read(ref targetLost)==0 && Volatile.Read(ref available)!=0 && Volatile.Read(ref requested)!=0?1:0);
        internal bool RecheckPublishedContracts()
        {
            if(Volatile.Read(ref targetLost)!=0)return false;
            // No installer capture when the original Fixes option was false.
            if(targetReference==null)return true;
            string reason="native-target-live-reader-unavailable";
            try
            {
                if(native is IMountedScannerLiveReader reader && reader.TryReadLive(capturedAddress,capturedModuleBase,out var bytes,out reason) &&
                    bytes!=null && bytes.SequenceEqual(targetReference))return true;
                if(reason==null)reason="native-target-live-foreign-change";
            }
            catch{reason="native-target-live-recheck-exception";}
            if(Interlocked.Exchange(ref targetLost,1)==0)
            {
                SetAvailable(false);
                try{report?.Invoke("ERROR Mounted native target contract lost at RVA187110: "+reason+"; correction permanently disabled, no repair");}catch{}
            }
            return false;
        }
        internal DataScanner Capture(DataScanner source,CrusaderLibraryLoadContext context)
        {
            try
            {
                if(source==null || context==null || !source.Found)return Reject("relocate scan/context unavailable");
                if(Volatile.Read(ref available)==0)return source;
                ulong address=source.CurrentAddress;
                if(!native.TryRead(context,out var observation,out string reason) ||
                    !MountedScannerNativeContract.Validate(unchecked((ulong)context.ModuleHandle.ToInt64()),address,observation,out reason))
                    return Reject(reason);
                var detached=context.CreateScanner().At(address);
                if(ReferenceEquals(detached,source) || !detached.Found || detached.CurrentAddress!=address)
                    return Reject("independent scanner contract failed");
                originalScanner=source;capturedAddress=address;capturedModuleBase=observation.ModuleBase;targetReference=(byte[])observation.Live.Clone();
                Interlocked.Increment(ref captures);return detached;
            }
            catch{return Reject("capture-boundary-exception");}
        }
        private DataScanner Reject(string reason)
        {Interlocked.Increment(ref rejections);try{report?.Invoke("ERROR native Mounted setup unavailable: "+reason+"; only known mounted installer skipped, no settings changed");}catch{}return null;}
        internal void PatchEmitter(ILContext il)
        {
            if(MountedScannerContract.Body(il.Method)!=MountedScannerContract.Body(plan.Emitter))
                throw new InvalidOperationException("Mounted emitter input changed.");
            var calls=il.Body.Instructions.Where(i=>(i.OpCode==OpCodes.Call || i.OpCode==OpCodes.Callvirt) &&
                i.Operand is MethodReference m && m.DeclaringType.FullName=="Iced.Intel.Assembler" && m.Name=="call" &&
                m.HasThis && m.ReturnType.FullName=="System.Void" && m.Parameters.Count==1 && m.Parameters[0].ParameterType.FullName=="System.UInt64").ToArray();
            if(calls.Length!=1)throw new InvalidOperationException("Mounted native CALL emission changed.");
            // Keep the original instruction as a branch/EH boundary; replace its operation in place.
            calls[0].OpCode=OpCodes.Nop;calls[0].Operand=null;
            var cursor=new ILCursor(il);cursor.Goto(calls[0],MoveType.After);
            cursor.EmitDelegate<Action<Assembler,ulong>>(EmitCall);
        }
        internal void EmitCall(Assembler assembler,ulong address)
        {
            var source=originalScanner;
            if(source==null || address!=capturedAddress){assembler.call(address);return;}
            // Read at the same delayed emission boundary as the original getter, NOT during capture.
            EmitConditionalCall(assembler,unchecked((ulong)activation.ToInt64()),address,source.CurrentAddress);
        }
        internal static void EmitConditionalCall(Assembler assembler,ulong gate,ulong corrected,ulong original)
        {
            var legacy=assembler.CreateLabel();var done=assembler.CreateLabel();
            assembler.pushfq();assembler.push(rax);assembler.mov(rax,gate);
            assembler.cmp(__byte_ptr[rax],0);assembler.pop(rax);assembler.je(legacy);
            assembler.popfq();assembler.call(corrected);assembler.jmp(done);
            assembler.Label(ref legacy);assembler.popfq();assembler.call(original);
            // The original emitter binds its own restore label immediately next.
            // Iced permits only one label per instruction; NOP is flags/register neutral.
            assembler.Label(ref done);assembler.nop();
        }
        internal void Patch(ILContext il)
        {
            if(MountedScannerContract.Body(il.Method)!=plan.InstallBody)throw new InvalidOperationException("Mounted installer input changed.");
            var stores=il.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference field && field.FullName==plan.Field.FullName).ToArray();
            if(stores.Length!=1)throw new InvalidOperationException("Mounted capture store changed.");var store=stores[0];
            if(!MountedScannerContract.HasUnprotectedPrefix(il.Method,store) || il.Body.Instructions.Any(i=>ReferenceEquals(i.Operand,store) || i.Operand is ILLabel label && label.Target==store || i.Operand is ILLabel[] labels && labels.Any(l=>l.Target==store) || i.Operand is Instruction[] targets && targets.Contains(store)))
                throw new InvalidOperationException("Mounted store has an unknown incoming boundary.");
            var cursor=new ILCursor(il);cursor.Goto(store,MoveType.Before);
            // Original stack [closure,sharedScanner]. Only the scanner value is replaced; field type is unchanged.
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<DataScanner,CrusaderLibraryLoadContext,DataScanner>>(Capture);
            cursor.Emit(OpCodes.Dup);cursor.Emit(OpCodes.Brtrue,store);
            // A known technical native failure leaves this VOID installer before any AddInline, not its caller.
            cursor.Emit(OpCodes.Pop);cursor.Emit(OpCodes.Pop);cursor.Emit(OpCodes.Ret);
        }
    }
}
