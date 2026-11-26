using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.Automation;
using APIShared.DependencyBootstrap;
using BepInEx.Bootstrap;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> EarlyProviderSteps()
        {yield return new AutomationStep("unity-mono-declarative-early-provider",TimeSpan.FromSeconds(30),EarlyProviderAcceptance);}
        private IEnumerable<AutomationWait> EarlyProviderAcceptance(AutomationRun run)
        {
            yield return AutomationWait.Until("front end without mission for early-provider receipt",()=>RescueScenarioSeries.MenuReady(run));
            Require(run.CurrentMission==null && Type.GetType("Mono.Runtime",false)!=null && typeof(object).Assembly.GetName().Name=="mscorlib",
                "Declarative provider acceptance requires the actual Unity-Mono game process before any mission");
            var receipt=EarlyProviderProbe.Captured;
            Require(EarlyProviderProbe.Fault==null && EarlyProviderProbe.Callbacks==1 && receipt!=null,
                "Actual declaration did not complete exactly one supported early callback: "+(EarlyProviderProbe.Fault??"callback absent"));
            Require(EarlyProviderProbe.OwnEntries==1 && EarlyProviderProbe.OwnReturns==1 && receipt.Thread==EarlyProviderProbe.EntryThread &&
                receipt.Thread==EarlyProviderProbe.ReturnThread && receipt.Thread==Thread.CurrentThread.ManagedThreadId,
                "Provider and ordinary consumer Awake/frontend did not share the measured startup thread");
            var context=receipt.Context;
            Require(receipt.TargetInstanceNull && receipt.OwnerInstanceNull && context.Owner.Guid==Plugin.Guid && context.Target.Guid=="fixes",
                "Original pre-AddComponent CLR-instance proof is absent");
            Require(Chainloader.PluginInfos.TryGetValue(Plugin.Guid,out var ownerInfo) && !ReferenceEquals(ownerInfo.Instance,null) &&
                ReferenceEquals(ownerInfo.Instance,EarlyProviderProbe.OwnObject) && ownerInfo.Metadata.Version.ToString()==context.Owner.Version &&
                EarlyProviderProbe.SamePath(ownerInfo.Location,context.Owner.AssemblyPath) && ownerInfo.Instance.GetType()==typeof(Plugin),
                "Normal consumer AddComponent/Awake did not publish the same measured CLR instance");
            Require(Chainloader.PluginInfos.TryGetValue("fixes",out var targetInfo) && !ReferenceEquals(targetInfo.Instance,null) &&
                targetInfo.Instance.GetType()==context.LoadedType && targetInfo.Metadata.Version.ToString()==context.Target.Version &&
                EarlyProviderProbe.SamePath(targetInfo.Location,context.Target.AssemblyPath),
                "Target AddComponent did not later publish the selected CLR instance/type");
            Require(context.LoadedType.Module==context.LoadedType.Assembly.ManifestModule &&
                context.LoadedType.Module.ModuleVersionId==context.LoadedModuleVersionId &&
                EarlyProviderProbe.SamePath(context.LoadedType.Module.FullyQualifiedName,context.Target.AssemblyPath) &&
                typeof(Plugin).Module==typeof(Plugin).Assembly.ManifestModule && typeof(Plugin).Module.ModuleVersionId==receipt.OwnerMvid &&
                EarlyProviderProbe.SamePath(typeof(Plugin).Module.FullyQualifiedName,context.Owner.AssemblyPath),
                "Later owner/target manifest identity differs from the actual early receipt");
            using(var owner=ModuleDefinition.ReadModule(context.Owner.AssemblyPath))
                Require(owner.Mvid==receipt.OwnerMvid,"Actual selected owner disk MVID changed after early callback");
            using(var target=ModuleDefinition.ReadModule(context.Target.AssemblyPath))
            {
                Require(target.Mvid==context.LoadedModuleVersionId,"Actual selected target disk MVID changed after early callback");
                ValidateFixesAwakeMarker(target,context.LoadedType.FullName);
            }
            var marker=context.LoadedType.GetField("LoggerFactory",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            Require(marker!=null && marker.FieldType.FullName=="Microsoft.Extensions.Logging.ILoggerFactory" &&
                !ReferenceEquals(marker.GetValue(targetInfo.Instance),null),
                "Fixes has no actual Awake-entry marker on its normal assigned instance");
            ValidateEarlyProviderLoader();
            var bootstrap=typeof(EarlyPluginActivationProviderAttribute).Assembly;
            log("early-provider-result actualUnityMono=true callbacks="+EarlyProviderProbe.Callbacks+
                " owner="+context.Owner.Guid+" version="+context.Owner.Version+" path="+context.Owner.AssemblyPath+" mvid="+receipt.OwnerMvid+
                " target="+context.Target.Guid+" version="+context.Target.Version+" path="+context.Target.AssemblyPath+
                " mvid="+context.LoadedModuleVersionId+" loadedType="+context.LoadedType.FullName+
                " callbackThread="+receipt.Thread+" ownAwakeEntryThread="+EarlyProviderProbe.EntryThread+" ownAwakeReturnThread="+EarlyProviderProbe.ReturnThread+
                " beforeTargetInstanceCLRNull=true beforeOwnerInstanceCLRNull=true ownerEntryPresentAtCallback="+receipt.OwnerEntryPresent+
                " apiInstanceAssignedAtCallback="+receipt.ApiInstanceAssigned+" apiFutureAwakeNotRequired=true ownAwakeEntryAndReturn=true"+
                " targetAssignedLater=true fixesAwakeEntryMarker=true fixesWholeAwakeCompletionUnobserved=true fixesAwakeThreadDirectlyUnobserved=true"+
                " bootstrapPath="+bootstrap.Location+" bootstrapMvid="+bootstrap.ManifestModule.ModuleVersionId+
                " bootstrapSHA256="+LoadCacheSha256(bootstrap.Location)+" newHooks=0 nativeCalls=0 gameWrites=0");
        }
        private static IEnumerable<TypeDefinition> EarlyProviderTypes(IEnumerable<TypeDefinition> roots)
        {foreach(var type in roots){yield return type;foreach(var nested in EarlyProviderTypes(type.NestedTypes))yield return nested;}}
        private static void ValidateFixesAwakeMarker(ModuleDefinition module,string typeName)
        {
            var type=EarlyProviderTypes(module.Types).SingleOrDefault(value=>value.FullName.Replace('/','+')==typeName);
            Require(type!=null,"Selected Fixes entry metadata absent");
            var field=type.Fields.SingleOrDefault(value=>value.Name=="LoggerFactory" && value.IsPublic && !value.IsStatic &&
                value.FieldType.FullName=="Microsoft.Extensions.Logging.ILoggerFactory");
            var awake=type.Methods.SingleOrDefault(value=>value.Name=="Awake" && !value.IsStatic && !value.HasParameters &&
                value.ReturnType.FullName=="System.Void" && value.HasBody);
            Require(field!=null && awake!=null,"Installed Fixes Awake-marker contract changed");
            var writers=new List<MethodDefinition>();
            foreach(var entry in EarlyProviderTypes(module.Types))foreach(var method in entry.Methods)
                if(method.HasBody)foreach(var instruction in method.Body.Instructions)
                    if(instruction.OpCode.Code==Code.Stfld && instruction.Operand is FieldReference reference && reference.FullName==field.FullName)
                        writers.Add(method);
            Require(writers.Count==1 && ReferenceEquals(writers[0],awake),
                "Fixes LoggerFactory no longer provides the reviewed sole-internal-Awake assignment marker");
            // This proves Awake reached its marker, not that every later statement completed successfully.
        }
        private static bool EarlyProviderCall(Instruction instruction,string type,string method)=>
            instruction?.Operand is MethodReference reference && reference.DeclaringType.FullName==type && reference.Name==method;
        private static void ValidateEarlyProviderLoader()
        {
            string path=typeof(Chainloader).Assembly.Location;
            Require(string.Equals(LoadCacheSha256(path),"8255B28902886085C578B9E427D3073C97002DB85176D2090CDEDA90EF14CE70",StringComparison.OrdinalIgnoreCase),
                "Unknown actual BepInEx core; pre/post Instance ordering must be audited again");
            using(var core=ModuleDefinition.ReadModule(path))
            {
                var start=core.GetType("BepInEx.Bootstrap.Chainloader").Methods.Single(value=>value.Name=="Start" && value.IsStatic && !value.HasParameters);
                var adds=start.Body.Instructions.Where(value=>EarlyProviderCall(value,"UnityEngine.GameObject","AddComponent")).ToArray();
                Require(adds.Length==1,"Unknown loader AddComponent boundary");var add=adds[0];
                Require(EarlyProviderCall(add.Previous,"System.Reflection.Assembly","GetType") && add.Next?.OpCode.Code==Code.Castclass &&
                    add.Next.Operand is TypeReference cast && cast.FullName=="BepInEx.BaseUnityPlugin" &&
                    EarlyProviderCall(add.Next.Next,"BepInEx.PluginInfo","set_Instance"),"Instance is no longer assigned after ordinary AddComponent return");
                Instruction setter=null;int steps=0;
                for(var cursor=add.Previous;cursor!=null && steps++<12;cursor=cursor.Previous)
                    if(EarlyProviderCall(cursor,"System.Collections.Generic.Dictionary`2<System.String,BepInEx.PluginInfo>","set_Item"))
                    {Require(setter==null,"Ambiguous pre-AddComponent PluginInfos publication");setter=cursor;}
                Require(setter!=null && EarlyProviderCall(setter.Previous?.Previous?.Previous,"BepInEx.Bootstrap.Chainloader","get_PluginInfos") &&
                    setter.Previous.Operand is VariableDefinition inserted && setter.Next?.Operand is VariableDefinition retained &&
                    ReferenceEquals(inserted,retained),"Target PluginInfo is not the same entry inserted before AddComponent");
            }
        }
    }
}
