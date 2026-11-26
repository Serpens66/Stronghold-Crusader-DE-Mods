using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.DependencyBootstrap;
using APIShared.Patching;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;

[assembly: EarlyPluginActivationProvider("TrappedUnitRescueTest_Serp","mounted-scanner-capture","fixes",
    "TrappedUnitRescueTest.MountedScannerEarlyProvider","BeforeFixes")]

namespace TrappedUnitRescueTest
{
    // Removable compatibility island: no general-rescue/master setting, DLL rewrite or native hook owner.
    public static class MountedScannerEarlyProvider
    {
        private static SafeIlHook hook;
        private static MountedScannerCapture capture;
        private static int attempted;
        private static string state;
        private static ManualLogSource log;
        internal static string State=>Volatile.Read(ref state);
        public static void BeforeFixes(EarlyPluginActivationContext context)
        {
            if(Interlocked.Exchange(ref attempted,1)!=0)return;
            log=Logger.CreateLogSource("RescueMountedScanner");
            try
            {
                if(context==null || context.Owner.Guid!="TrappedUnitRescueTest_Serp" || context.Target.Guid!="fixes" ||
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
                if(!SafeIlHook.TryInstall("TrappedUnitRescueTest_Serp","TrappedUnitRescue.FixesMountedScannerCapture",target,capture.Patch,log,out hook,out reason))
                {Volatile.Write(ref state,"Unavailable:managed-hook-rejected");Write(true,State);return;}
                hook.Rebuilt+=value=>{if(!value.IsApplied){Volatile.Write(ref state,"Unavailable:managed-rebuild-lost");Write(true,State);}};
                Volatile.Write(ref state,"InstalledAwaitingNative");Write(false,State+"; original Fixes option remains authoritative; GeneralOFF/ON unchanged");
            }
            catch{Volatile.Write(ref state,"Unavailable:early-contract-failed");Write(true,State);}
        }
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
        private int captures,rejections;
        internal int Captures=>Volatile.Read(ref captures);
        internal int Rejections=>Volatile.Read(ref rejections);
        internal MountedScannerCapture(MountedScannerPlan plan,IMountedScannerNativeReader native,Action<string> report)
        {this.plan=plan??throw new ArgumentNullException(nameof(plan));this.native=native??throw new ArgumentNullException(nameof(native));this.report=report;}
        internal DataScanner Capture(DataScanner source,CrusaderLibraryLoadContext context)
        {
            try
            {
                if(source==null || context==null || !source.Found)return Reject("relocate scan/context unavailable");
                ulong address=source.CurrentAddress;
                if(!native.TryRead(context,out var observation,out string reason) ||
                    !MountedScannerNativeContract.Validate(unchecked((ulong)context.ModuleHandle.ToInt64()),address,observation,out reason))
                    return Reject(reason);
                var detached=context.CreateScanner().At(address);
                if(ReferenceEquals(detached,source) || !detached.Found || detached.CurrentAddress!=address)
                    return Reject("independent scanner contract failed");
                Interlocked.Increment(ref captures);return detached;
            }
            catch{return Reject("capture-boundary-exception");}
        }
        private DataScanner Reject(string reason)
        {Interlocked.Increment(ref rejections);try{report?.Invoke("ERROR native Mounted setup unavailable: "+reason+"; only known mounted installer skipped, no settings changed");}catch{}return null;}
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
