using System;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared.DependencyBootstrap;
using BepInEx.Bootstrap;

[assembly: EarlyPluginActivationProvider("TrappedUnitRescueTest_Serp","acceptance-declarative-provider","fixes",
    "TrappedUnitRescueTest.EarlyProviderProbe","BeforeFixes")]

namespace TrappedUnitRescueTest
{
    // Pure managed startup receipt. No Unity/APIShared runtime call, logger, resolver, hook, native call or game write.
    // No static/module initializer: all fields start at CLR defaults. The matching bootstrap roots the declaration.
    public static class EarlyProviderProbe
    {
        private const string OwnerGuid="TrappedUnitRescueTest_Serp",TargetGuid="fixes";
        internal sealed class Receipt
        {
            internal readonly EarlyPluginActivationContext Context;
            internal readonly int Thread;
            internal readonly Guid OwnerMvid;
            internal readonly bool TargetInstanceNull,OwnerEntryPresent,OwnerInstanceNull,ApiInstanceAssigned;
            internal Receipt(EarlyPluginActivationContext context,int thread,Guid ownerMvid,bool targetNull,
                bool ownerPresent,bool ownerNull,bool apiAssigned)
            {Context=context;Thread=thread;OwnerMvid=ownerMvid;TargetInstanceNull=targetNull;OwnerEntryPresent=ownerPresent;
                OwnerInstanceNull=ownerNull;ApiInstanceAssigned=apiAssigned;}
        }
        private static Receipt receipt;
        private static string fault;
        private static int callbacks,ownEntries,ownReturns,entryThread,returnThread;
        private static object ownObject;
        internal static Receipt Captured=>Volatile.Read(ref receipt);
        internal static string Fault=>Volatile.Read(ref fault);
        internal static int Callbacks=>Volatile.Read(ref callbacks);
        internal static int OwnEntries=>Volatile.Read(ref ownEntries);
        internal static int OwnReturns=>Volatile.Read(ref ownReturns);
        internal static int EntryThread=>Volatile.Read(ref entryThread);
        internal static int ReturnThread=>Volatile.Read(ref returnThread);
        internal static object OwnObject=>Volatile.Read(ref ownObject);

        public static void BeforeFixes(EarlyPluginActivationContext context)
        {
            if(Interlocked.Increment(ref callbacks)!=1){Volatile.Write(ref fault,"Early provider callback replayed.");return;}
            try
            {
                Check(context!=null && context.Owner.Guid==OwnerGuid && context.Target.Guid==TargetGuid,"Wrong selected owner/target.");
                Check(Volatile.Read(ref ownEntries)==0 && Volatile.Read(ref ownReturns)==0,"Consumer Awake already ran before target boundary.");
                var own=typeof(EarlyProviderProbe).Assembly;var target=context.LoadedType;
                Check(SamePath(own.Location,context.Owner.AssemblyPath) && context.Owner.EntryTypeName=="TrappedUnitRescueTest.Plugin" &&
                    target!=null && target.FullName==context.Target.EntryTypeName &&
                    SamePath(target.Assembly.Location,context.Target.AssemblyPath) && target.Module==target.Assembly.ManifestModule &&
                    target.Module.ModuleVersionId==context.LoadedModuleVersionId &&
                    SamePath(target.Module.FullyQualifiedName,context.Target.AssemblyPath),"Actual manifest identity does not match selected metadata.");
                // Actual BepInEx inserts TARGET PluginInfo before AddComponent, and assigns Instance only after return.
                Check(Chainloader.PluginInfos.TryGetValue(TargetGuid,out var targetInfo) &&
                    targetInfo.Metadata.GUID==context.Target.Guid && targetInfo.Metadata.Version.ToString()==context.Target.Version &&
                    SamePath(targetInfo.Location,context.Target.AssemblyPath),"Target PluginInfo is not the selected pre-AddComponent entry.");
                bool targetNull=ReferenceEquals(targetInfo.Instance,null);
                bool ownerPresent=Chainloader.PluginInfos.TryGetValue(OwnerGuid,out var ownerInfo);
                bool ownerNull=!ownerPresent || ReferenceEquals(ownerInfo.Instance,null);
                Check(!ownerPresent || ownerInfo.Metadata.GUID==context.Owner.Guid && ownerInfo.Metadata.Version.ToString()==context.Owner.Version &&
                    SamePath(ownerInfo.Location,context.Owner.AssemblyPath),"Owner PluginInfo differs from selected metadata.");
                Check(targetNull && ownerNull,"CLR plugin Instance was assigned before the early callback.");
                // APIShared can still be a future Awake. Its current state is recorded, never required.
                bool apiAssigned=Chainloader.PluginInfos.TryGetValue("APIShared_Serp",out var apiInfo) && !ReferenceEquals(apiInfo.Instance,null);
                Volatile.Write(ref receipt,new Receipt(context,Thread.CurrentThread.ManagedThreadId,
                    own.ManifestModule.ModuleVersionId,targetNull,ownerPresent,ownerNull,apiAssigned));
            }
            catch{Volatile.Write(ref fault,"Early provider metadata contract failed; no startup correction was attempted.");}
        }
        internal static void OwnAwakeEntered(object value)
        {
            Interlocked.Increment(ref ownEntries);Volatile.Write(ref entryThread,Thread.CurrentThread.ManagedThreadId);
            Volatile.Write(ref ownObject,value);
        }
        internal static void OwnAwakeReturned(object value)
        {
            if(!ReferenceEquals(Volatile.Read(ref ownObject),value))Volatile.Write(ref fault,"Consumer Awake instance changed.");
            Interlocked.Increment(ref ownReturns);Volatile.Write(ref returnThread,Thread.CurrentThread.ManagedThreadId);
        }
        private static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        internal static bool SamePath(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),
            Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    }
}
