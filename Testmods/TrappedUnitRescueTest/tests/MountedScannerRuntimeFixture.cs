using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using Iced.Intel;
using TrappedUnitRescueTest;
using CilInstruction=Mono.Cecil.Cil.Instruction;

namespace TrappedUnitRescueTest.Tests
{
    internal static class MountedScannerRuntimeFixture
    {
        private static readonly string GameDirectory=Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ?? @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        private static APIShared.Patching.SafeIlHook installedHook;
        private const ulong Base=0x180000000;
        private static readonly System.Collections.Generic.List<ILContext> contexts=new System.Collections.Generic.List<ILContext>();
        private static int checks,scans;private static object captured;
        private static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
        private sealed class Reader : IMountedScannerNativeReader
        {
            internal bool Fail,Throws;internal int Reads;
            public bool TryRead(CrusaderLibraryLoadContext context,out MountedScannerNativeObservation data,out string reason)
            {
                Reads++;if(Throws)throw new InvalidOperationException();reason=null;data=Observation(context);return !Fail;
            }
        }
        private static MountedScannerNativeObservation Observation(CrusaderLibraryLoadContext context)=>new MountedScannerNativeObservation{
            ModuleBase=Base,ImageSize=context.Memory.Length,FileHash=MountedScannerNativeContract.ImageHash,Executable=true,
            Snapshot=context.Memory.Slice(MountedScannerNativeContract.Rva,MountedScannerNativeContract.Length).ToArray(),Live=MountedScannerNativeContract.ReferenceBytes()};
        private static CrusaderLibraryLoadContext Context()
        {
            var bytes=new byte[MountedScannerNativeContract.Rva+4096];Array.Copy(MountedScannerNativeContract.ReferenceBytes(),0,bytes,MountedScannerNativeContract.Rva,MountedScannerNativeContract.Length);
            var region=ScanRegion.FromMemory(bytes,Base,"offline managed capture fixture; no native module");
            return (CrusaderLibraryLoadContext)Activator.CreateInstance(typeof(CrusaderLibraryLoadContext),BindingFlags.NonPublic|BindingFlags.Instance,null,
                new object[]{new IntPtr(unchecked((long)Base)),region},null);
        }
        private static byte[] Bytes(ModuleDefinition module){using(var memory=new MemoryStream()){module.Write(memory);return memory.ToArray();}}
        private static byte[] BrokenFamily(byte[] bytes)
        {
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var owner=module.GetType(MountedScannerContract.Owner);var install=owner.Methods.Single(m=>m.Name=="InstallMountedKeepRecovery");
                var delayed=owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__") && m.ReturnType.FullName=="System.Void");
                string hash=MountedScannerContract.Fingerprint(install,delayed);
                if(hash==MountedScannerContract.Fixed)
                {
                    // Test INPUT only: exact inverse of the already-audited historical scalar copy.
                    // No file is written; final normalized result must equal the actual known broken family.
                    var store=install.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.FieldType.MetadataType==MetadataType.UInt64);
                    var getter=(MethodReference)store.Previous.Operand;install.Body.Instructions.Remove(store.Previous);
                    var field=((FieldReference)store.Operand).Resolve();field.FieldType=module.ImportReference(typeof(DataScanner));
                    var load=delayed.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name==field.Name);
                    delayed.Body.GetILProcessor().InsertAfter(load,CilInstruction.Create(OpCodes.Callvirt,getter));
                }
                Check(MountedScannerContract.Fingerprint(install,delayed)==MountedScannerContract.Broken,"Input is not the exact supported broken capture family");
                return Bytes(module);
            }
        }
        private static MountedScannerPlan Plan(byte[] bytes)
        {
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {Check(MountedScannerContract.Classify(module,out var plan,out string reason)==MountedScannerClassification.Broken,reason);return plan;}
        }
        public static DataScanner SpyScan(DataScanner scanner,string pattern,int offset){scans++;return scanner.Scan(pattern,offset);}
        public static void Save(object value){captured=value;}
        private static MethodInfo CapturePrefix(Assembly assembly,MountedScannerPlan plan,MountedScannerCapture service)
        {
            var method=(MethodInfo)assembly.ManifestModule.ResolveMethod(plan.Install.MetadataToken.ToInt32());
            using(var copy=new DynamicMethodDefinition(method))
            {
                var il=new ILContext(copy.Definition);contexts.Add(il);
                {
                    il.ReferenceBag=RuntimeILReferenceBag.Instance;il.Invoke(service.Patch);
                    var store=il.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference field && field.FullName==plan.Field.FullName);
                    // Controlled test termination immediately after the ACTUAL original capture store.
                    // No original global lookup, vtable read, AddInline or native publication is executed.
                    il.Body.ExceptionHandlers.Clear();while(store.Next!=null)il.Body.Instructions.Remove(store.Next);
                    var cursor=new ILCursor(il);cursor.Goto(store,MoveType.After);cursor.Emit(OpCodes.Ldloc_0);
                    cursor.Emit(OpCodes.Call,typeof(MountedScannerRuntimeFixture).GetMethod(nameof(Save)));cursor.Emit(OpCodes.Ret);
                    var scan=il.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt && i.Operand is MethodReference r && r.Name=="Scan");
                    scan.OpCode=OpCodes.Call;scan.Operand=il.Method.Module.ImportReference(typeof(MountedScannerRuntimeFixture).GetMethod(nameof(SpyScan)));
                }
                return copy.Generate();
            }
        }
        private static void InvokePrefix(MethodInfo method,CrusaderLibraryLoadContext context,DataScanner scanner)
        {captured=null;var args=method.GetParameters().Length==4?new object[]{null,context,null,scanner}:new object[]{context,null,scanner};method.Invoke(null,args);}
        internal static int Run(string apiSharedDirectory)
        {
            VerifySelectedSdk(apiSharedDirectory);
            string sourcePath=Path.Combine(GameDirectory,"BepInEx","plugins","fixes","fixes.dll");
            byte[] original=File.ReadAllBytes(sourcePath);
            try
            {
                using(var sourceModule=ModuleDefinition.ReadModule(new MemoryStream(original)))
                {
                    var classification=MountedScannerContract.Classify(sourceModule,out _,out string reason);
                    if(classification==MountedScannerClassification.NotApplicable)
                    {Console.WriteLine("SKIP mounted scanner runtime family: "+reason+"; no mounted transformation/native assertions executed");return checks;}
                    Check(classification!=MountedScannerClassification.Unsupported,"Actual Fixes source relevant contract requires review: "+reason);
                }
            string native=GameDirectory+"/Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll";
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(native))
                Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")==MountedScannerNativeContract.ImageHash,"Canonical native test input changed");
            var image=File.ReadAllBytes(native);int pe=BitConverter.ToInt32(image,0x3C),sections=BitConverter.ToUInt16(image,pe+6),table=pe+24+BitConverter.ToUInt16(image,pe+20);bool mapped=false;
            for(int index=0;index<sections;index++)
            {
                int section=table+40*index;uint virtualAddress=BitConverter.ToUInt32(image,section+12),rawLength=BitConverter.ToUInt32(image,section+16),rawPointer=BitConverter.ToUInt32(image,section+20);
                if(MountedScannerNativeContract.Rva<virtualAddress || (ulong)MountedScannerNativeContract.Rva+MountedScannerNativeContract.Length>(ulong)virtualAddress+rawLength)continue;
                int offset=checked((int)(rawPointer+MountedScannerNativeContract.Rva-virtualAddress));
                Check(image.Skip(offset).Take(MountedScannerNativeContract.Length).SequenceEqual(MountedScannerNativeContract.ReferenceBytes()),"Embedded full function differs from canonical PE RVA187110");mapped=true;
            }
            Check(mapped,"Canonical native function has no mapped PE raw span");
            Check(MountedScannerHelpers.Validate(out string helper),helper);
            var context=Context();var good=Observation(context);ulong address=Base+MountedScannerNativeContract.Rva;
            Check(MountedScannerNativeContract.Validate(Base,address,good,out _),"Native contract core rejects exact audited observations");
            Check(!MountedScannerNativeContract.Validate(Base,Base+0xE2610,good,out _),"Wrong historical helper address accepted");
            good.Live[0]^=1;Check(!MountedScannerNativeContract.Validate(Base,address,good,out _),"Foreign live-byte change accepted");
            good=Observation(context);good.Snapshot[1]^=1;Check(!MountedScannerNativeContract.Validate(Base,address,good,out _),"Changed snapshot accepted");
            good=Observation(context);good.FileHash="changed";Check(!MountedScannerNativeContract.Validate(Base,address,good,out _),"Unknown native hash accepted");
            good=Observation(context);good.Executable=false;Check(!MountedScannerNativeContract.Validate(Base,address,good,out _),"Nonexecutable module accepted");
            Check(!new MountedScannerNativeReader().TryRead(context,out _,out _),"Default reader must reject absent real game module in offline process");
            byte[] bytes=BrokenFamily(original);var plan=Plan(bytes);
            var assembly=Assembly.Load(bytes);MountedScannerContract.ValidateLoaded(assembly,plan);
            var source=context.CreateScanner();var reader=new Reader();var service=new MountedScannerCapture(plan,reader,null);
            var prefix=CapturePrefix(assembly,plan,service);scans=0;InvokePrefix(prefix,context,source);
            Check(scans==1 && captured!=null && service.Captures==1 && service.Rejections==0,"Actual transformed capture prefix did not execute one Scan/snapshot");
            var field=assembly.ManifestModule.ResolveField(plan.Field.MetadataToken.ToInt32());var detached=(DataScanner)field.GetValue(captured);
            Check(field.FieldType==typeof(DataScanner) && !ReferenceEquals(detached,source) && detached.CurrentAddress==address,"Capture field/type/scanner identity changed");
            for(int i=0;i<4;i++)source.At(Base+0xE2610+(uint)i);
            Check(detached.CurrentAddress==address,"Later shared scanner mutation changed private address");
            var globals=(GameGlobalsManager)FormatterServices.GetUninitializedObject(typeof(GameGlobalsManager));
            foreach(string name in new[]{"GameUnitManagerVA","CurrentContextUnitIdVA","PathfindingProfilesUnitTableRVA","GameTileManagerVA","GameBuildingManagerVA","GamePlayerManagerVA","MapRowLookupTableRVA","GameCursorManagerVA"})
                typeof(GameGlobalsManager).GetField(name).SetValue(globals,Base+0x300000UL);
            var globalField=captured.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(GameGlobalsManager));
            globalField.SetValue(captured,globals);
            var delayed=assembly.ManifestModule.ResolveMethod(plan.Delayed.MetadataToken.ToInt32());
            var generator=(InlineHookGenerator)Delegate.CreateDelegate(typeof(InlineHookGenerator),captured,(MethodInfo)delayed);
            for(int i=0;i<4;i++)
            {
                var asm=new Assembler(64);generator(asm,ReadOnlySpan<Iced.Intel.Instruction>.Empty,Base+0x100000);
                using(var output=new MemoryStream())
                {
                    asm.Assemble(new StreamCodeWriter(output),Base);var decoder=Decoder.Create(64,new ByteArrayCodeReader(output.ToArray()));decoder.IP=Base;
                    int calls=0;while(decoder.IP<Base+(ulong)output.Length){var instruction=decoder.Decode();if(instruction.FlowControl==Iced.Intel.FlowControl.Call){calls++;Check(instruction.NearBranchTarget==address,"Actual deferred generator emitted wrong native CALL");}}
                    Check(calls==1,"Actual deferred generator native CALL count differs");
                }
                source.At(Base+0xE2610+(uint)i*128);
            }
            var denied=new Reader{Fail=true};var deniedService=new MountedScannerCapture(plan,denied,null);var deniedPrefix=CapturePrefix(assembly,plan,deniedService);
            int callerContinuation=0;InvokePrefix(deniedPrefix,context,source);callerContinuation++;
            Check(captured==null && deniedService.Captures==0 && deniedService.Rejections==1 && callerContinuation==1,"Native failure did not skip only known installer prefix");
            // Actual original option branch is verified by Classify; execute its exact opcode in a controlled slice.
            VerifyOriginalOptionSlice(assembly,plan);
            using(var altered=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                altered.Assembly.Name.Version=new Version(99,0);var m=new MethodDefinition("UnrelatedFutureFeature",Mono.Cecil.MethodAttributes.Public|Mono.Cecil.MethodAttributes.Static,altered.TypeSystem.Void);m.Body.Instructions.Add(CilInstruction.Create(OpCodes.Ret));altered.GetType(MountedScannerContract.Owner).Methods.Add(m);
                Check(MountedScannerContract.Classify(altered,out _,out _)==MountedScannerClassification.Broken,"Unrelated update rejected");
            }
            using(var altered=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var m=altered.GetType(MountedScannerContract.Owner).Methods.Single(x=>x.Name=="InstallMountedKeepRecovery");var size=m.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4_S && Convert.ToInt32(i.Operand)==14);size.Operand=(sbyte)15;
                Check(MountedScannerContract.Classify(altered,out _,out _)==MountedScannerClassification.Unsupported,"Relevant installer manipulation accepted");
            }
            using(var altered=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var emitter=altered.GetType(MountedScannerContract.Owner).Methods.Single(x=>x.Name=="EmitMountedKeepRecovery");var token=emitter.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldtoken && i.Operand is FieldReference);var blob=((FieldReference)token.Operand).Resolve();var data=blob.InitialValue;data[0]^=1;blob.InitialValue=data;
                Check(MountedScannerContract.Classify(altered,out _,out _)==MountedScannerClassification.Unsupported,"Relevant emitter data manipulation accepted");
            }
            VerifyAdditionalContracts(bytes,assembly,plan,context);
            VerifyScalarFixed(bytes);
            var target=assembly.ManifestModule.ResolveMethod(plan.Install.MetadataToken.ToInt32());
            Check(APIShared.Patching.SafeIlHook.TryInstall("MountedScannerFixture","MountedScannerFixture.FullInstaller",target,service.Patch,null,out installedHook,out string hookReason),"Full production SafeIlHook precompile/publication failed: "+hookReason);
            Check(installedHook.IsApplied && installedHook.Failures==0,"Full actual installer hook was not applied");
            }
            finally
            {
                Check(original.SequenceEqual(File.ReadAllBytes(sourcePath)),"Fixture changed actual Fixes source file");
                foreach(var disposable in contexts)disposable.Dispose();contexts.Clear();
            }
            Console.WriteLine("PASS mounted scanner actual-source offline checks="+checks+"; Scan/store production DMD prefix + four real deferred generators; native validation observations tested separately; no game/Game-native hook installation; isolated managed SafeIlHook applied.");
            return checks;
        }
        private static void VerifyAdditionalContracts(byte[] bytes,Assembly assembly,MountedScannerPlan plan,CrusaderLibraryLoadContext context)
        {
            foreach(string family in new[]{"Context","Scanner"})
            using(var module=ModuleDefinition.ReadModule((family=="Context"?typeof(CrusaderLibraryLoadContext):typeof(DataScanner)).Assembly.Location))
            {
                string owner=family=="Context"?MountedScannerContract.ContextType:MountedScannerContract.Scanner;
                foreach(string name in family=="Context"?new[]{"CreateScanner","get_Memory"}:new[]{".ctor","Create","At","get_Found"})AddUnused(module.GetType(owner),name,module.TypeSystem.Int32);
                Check(MountedScannerHelpers.ValidateSelected(module,family,out string reason),"Unused overloads rejected: "+reason);
                var method=MountedScannerHelpers.Select(module,family).Values.First();method.Body.Instructions.Insert(0,CilInstruction.Create(OpCodes.Ldc_I4_0));method.Body.Instructions.Insert(1,CilInstruction.Create(OpCodes.Pop));
                Check(!MountedScannerHelpers.ValidateSelected(module,family,out _),"Changed used helper accepted");
            }
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var owner=module.GetType(MountedScannerContract.Owner);AddUnused(owner,"InstallMountedKeepRecovery",module.TypeSystem.Int32);AddUnused(owner,"EmitMountedKeepRecovery",module.TypeSystem.Int32);
                Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.Broken,"Unused Fixes overload rejected");
                var predicate=owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__") && m.ReturnType.FullName=="System.Boolean");
                predicate.Body.Instructions.Insert(0,CilInstruction.Create(OpCodes.Ldc_I4_0));predicate.Body.Instructions.Insert(1,CilInstruction.Create(OpCodes.Pop));
                Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.Unsupported,"Changed target-selection predicate accepted");
            }
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var owner=module.GetType(MountedScannerContract.Owner);var ctor=owner.Methods.Single(m=>m.IsConstructor && !m.IsStatic);
                var getter=ctor.Body.Instructions.Single(i=>i.Operand is FieldReference f && f.Name=="EnableMountedUnitsKeepRecovery");getter.Next.Next.OpCode=OpCodes.Brtrue_S;
                Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.Unsupported,"Changed original option polarity accepted");
            }
            foreach(var opcode in new[]{OpCodes.Ldfld,OpCodes.Stfld,OpCodes.Ldflda,OpCodes.Ldtoken})
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var owner=module.GetType(MountedScannerContract.Owner);var method=new MethodDefinition("UnrelatedFieldUse",Mono.Cecil.MethodAttributes.Public|Mono.Cecil.MethodAttributes.Static,module.TypeSystem.Void);
                var field=module.LookupToken(plan.Field.MetadataToken) as FieldDefinition;
                if(opcode!=OpCodes.Ldtoken)method.Body.Instructions.Add(CilInstruction.Create(OpCodes.Ldnull));
                if(opcode==OpCodes.Stfld)method.Body.Instructions.Add(CilInstruction.Create(OpCodes.Ldnull));
                method.Body.Instructions.Add(CilInstruction.Create(opcode,field));if(opcode!=OpCodes.Stfld)method.Body.Instructions.Add(CilInstruction.Create(OpCodes.Pop));method.Body.Instructions.Add(CilInstruction.Create(OpCodes.Ret));owner.Methods.Add(method);
                Check(MountedScannerContract.Fingerprint(owner.Methods.Single(m=>m.Name=="InstallMountedKeepRecovery"),owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__") && m.ReturnType.FullName=="System.Void"))==MountedScannerContract.Broken,"Xref test unexpectedly changed root fingerprints");
                Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.Unsupported,"Additional captured-field use accepted: "+opcode);
            }
            var observation=Observation(context);observation.ModuleBase++;Check(!MountedScannerNativeContract.Validate(Base,Base+MountedScannerNativeContract.Rva,observation,out _),"Wrong module accepted");
            observation=Observation(context);observation.ImageSize=1;Check(!MountedScannerNativeContract.Validate(Base,Base+MountedScannerNativeContract.Rva,observation,out _),"Small image accepted");
            Check(!MountedScannerNativeContract.Validate(ulong.MaxValue,1,Observation(context),out _),"Overflow accepted");
            var reader=new Reader();var service=new MountedScannerCapture(plan,reader,s=>throw new Exception("reporting must remain isolated"));
            Check(service.Capture(null,context)==null && reader.Reads==0,"Null source reached native observation");
            Check(service.Capture(context.CreateScanner(),context)==null && reader.Reads==0,"Unfound source reached native observation");
            reader.Throws=true;Check(service.Capture(context.CreateScanner().At(Base+MountedScannerNativeContract.Rva),context)==null,"Reader/report exception escaped");
            using(var copy=new DynamicMethodDefinition(assembly.ManifestModule.ResolveMethod(plan.Install.MetadataToken.ToInt32())))
            using(var il=new ILContext(copy.Definition))
            {
                il.ReferenceBag=RuntimeILReferenceBag.Instance;il.Body.Instructions.Insert(0,CilInstruction.Create(OpCodes.Ldc_I4_0));il.Body.Instructions.Insert(1,CilInstruction.Create(OpCodes.Pop));
                string input=MountedScannerContract.Body(il.Method);bool rejected=false;try{il.Invoke(service.Patch);}catch(InvalidOperationException){rejected=true;}
                Check(rejected && input==MountedScannerContract.Body(il.Method),"Unknown incoming body edited before rejection");
            }
        }
        private static void AddUnused(TypeDefinition owner,string name,TypeReference argument)
        {
            var method=new MethodDefinition(name,Mono.Cecil.MethodAttributes.Public,owner.Module.TypeSystem.Void);method.Parameters.Add(new ParameterDefinition(argument));method.Body.Instructions.Add(CilInstruction.Create(OpCodes.Ret));owner.Methods.Add(method);
        }
        private static void VerifySelectedSdk(string directory)
        {
            if(string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))throw new ArgumentException("Pass the explicitly selected absolute APIShared plugin package directory.");
            string api=Path.Combine(directory,"APIShared.dll"),bootstrap=Path.GetFullPath(Path.Combine(directory,"..","..","patchers","APIShared.DependencyBootstrap.dll"));
            Check(File.ReadAllBytes(api).SequenceEqual(File.ReadAllBytes(typeof(APIShared.Patching.SafeIlHook).Assembly.Location)),"Loaded APIShared differs from explicitly selected package");
            Check(File.ReadAllBytes(bootstrap).SequenceEqual(File.ReadAllBytes(typeof(APIShared.DependencyBootstrap.EarlyPluginActivationContext).Assembly.Location)),"Loaded bootstrap differs from selected SDK pair");
        }
        private static void BuildScalarVector(ModuleDefinition module)
        {
            // Test vector only: no CLI, path-writing API or production fallback exists.
            Check(MountedScannerContract.Classify(module,out var plan,out _)==MountedScannerClassification.Broken,"Scalar vector needs the exact known broken family");
            var getter=plan.Delayed.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt && i.Operand is MethodReference m && m.Name=="get_CurrentAddress" && m.DeclaringType.FullName==MountedScannerContract.Scanner);
            var store=plan.Install.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference field && field.FullName==plan.Field.FullName);
            plan.Install.Body.GetILProcessor().InsertBefore(store,CilInstruction.Create(OpCodes.Callvirt,(MethodReference)getter.Operand));
            plan.Field.FieldType=module.TypeSystem.UInt64;getter.OpCode=OpCodes.Nop;getter.Operand=null;
            Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.AlreadyFixed,"Generated scalar vector not recognized");
        }
        private static void VerifyScalarFixed(byte[] bytes)
        {
            using(var module=ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                var owner=module.GetType(MountedScannerContract.Owner);var emitter=owner.Methods.Single(m=>MountedScannerContract.Matches(m,"EmitMountedKeepRecovery",true,"System.Void",new[]{"Iced.Intel.Assembler"}.Concat(Enumerable.Repeat("System.UInt64",9)).ToArray()));
                string emitterBody=MountedScannerContract.Fingerprint(emitter);Guid mvid=module.Mvid;
                BuildScalarVector(module);
                Check(MountedScannerContract.Classify(module,out _,out _)==MountedScannerClassification.AlreadyFixed,"Known scalar correction not skipped idempotently");
                Check(module.Mvid==mvid && MountedScannerContract.Fingerprint(emitter)==emitterBody,"Scalar vector changed emitter or module identity");
                var delayed=owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__") && m.ReturnType.FullName=="System.Void");
                Check(!delayed.Body.Instructions.Any(i=>i.Operand is MethodReference method && method.Name=="get_CurrentAddress"),"Scalar vector retains mutable deferred read");
                var field=delayed.Body.Instructions.Select(i=>i.Operand).OfType<FieldReference>().Single(f=>f.Name=="relocate").Resolve();
                Check(field.FieldType.MetadataType==MetadataType.UInt64,"Scalar vector field is not an address value");
                foreach(var type in module.Types.SelectMany(t=>new[]{t}.Concat(t.NestedTypes)))foreach(var method in type.Methods.Where(m=>m.HasBody))
                foreach(var instruction in method.Body.Instructions)
                    if(instruction.Operand is FieldReference reference && reference.Name=="relocate" && reference.DeclaringType.FullName==field.DeclaringType.FullName)reference.Name="relocateAddress";
                field.Name="relocateAddress";foreach(var nop in delayed.Body.Instructions.Where(i=>i.OpCode==OpCodes.Nop).ToArray())delayed.Body.Instructions.Remove(nop);
                Check(MountedScannerContract.Classify(module,out _,out string renameReason)==MountedScannerClassification.AlreadyFixed,"Equivalent scalar field name/NOP omission rejected: "+renameReason);
                byte[] serialized=Bytes(module);using(var reread=ModuleDefinition.ReadModule(new MemoryStream(serialized)))
                    Check(MountedScannerContract.Classify(reread,out _,out _)==MountedScannerClassification.AlreadyFixed,"Serialized scalar capture lost contract");

            }
        }
        private static int optionCalls;
        public static void OptionCalled(){optionCalls++;}
        private static void VerifyOriginalOptionSlice(Assembly assembly,MountedScannerPlan plan)
        {
            var ctor=plan.Install.DeclaringType.Methods.Single(m=>m.IsConstructor && !m.IsStatic);
            var call=ctor.Body.Instructions.Single(i=>i.Operand is MethodReference r && r.FullName==plan.Install.FullName);
            var branch=call.Previous;while(branch.OpCode.Code!=Mono.Cecil.Cil.Code.Brfalse && branch.OpCode.Code!=Mono.Cecil.Cil.Code.Brfalse_S)branch=branch.Previous;
            using(var slice=new DynamicMethodDefinition("ActualFixesOptionBranch",typeof(void),new[]{typeof(bool)}))
            {
                var il=slice.GetILProcessor();var done=CilInstruction.Create(OpCodes.Ret);il.Emit(OpCodes.Ldarg_0);il.Emit(branch.OpCode,done);
                il.Emit(OpCodes.Call,slice.Definition.Module.ImportReference(typeof(MountedScannerRuntimeFixture).GetMethod(nameof(OptionCalled))));il.Append(done);
                var action=(Action<bool>)slice.Generate().CreateDelegate(typeof(Action<bool>));optionCalls=0;action(false);Check(optionCalls==0,"Actual false option branch reaches installer");action(true);Check(optionCalls==1,"Actual true option branch skips installer");
            }
        }
    }
}
