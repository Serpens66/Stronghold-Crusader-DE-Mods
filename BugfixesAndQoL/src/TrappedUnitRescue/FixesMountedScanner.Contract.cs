using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Utils;
using MonoMod.Cil;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    internal enum MountedScannerClassification {Unsupported,NotApplicable,AlreadyFixed,Broken}
    internal sealed class MountedScannerPlan
    {
        internal MethodDefinition Install,Delayed,Emitter,Predicate;
        internal FieldDefinition Field;
        internal string InstallBody;
    }
    internal static class MountedScannerContract
    {
        internal const string Owner="Fixes.Detours.FixesUnitDetours";
        internal const string Scanner="RedBird.X64.Memory.Scanners.DataScanner";
        internal const string Broken="FE320FBDFB252BAE5988385659235BB0E65C6B3C2226A3BAEDD435857651E433";
        internal const string Fixed="687A9FEF7F0F8F7E0F215DE4CACAC1B6B6F540DAF5B8E94EDBAD46CAE42157A7";
        internal const string Emitter="B4E80252F7099D1EF4F2EE3D97AE09ABF4616B457FE501A8B698CBF9C5D62546";
        internal const string Predicate="98874CFC3748A1742DFE00F660D02E90CBBA646FA4543659B6A500ECA46C4B23";
        internal const string EmitterConstants="175CF9289B598B0C012ADE112495C6016BA35C64D6A60A5D6857EBAE98CEB1CE";
        internal const string ContextType="SHCDESE.API.LowLevel.CrusaderLibraryLoadContext",TransactionType="RedBird.X64.Hooks.Transaction.HookTransaction";
        internal static bool Matches(MethodDefinition method,string name,bool isStatic,string result,params string[] args)=>
            method.Name==name && method.IsStatic==isStatic && method.HasThis==!isStatic && !method.ExplicitThis &&
            method.CallingConvention==MethodCallingConvention.Default && method.GenericParameters.Count==0 && method.ReturnType.FullName==result &&
            method.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(args);
        internal static MountedScannerClassification Classify(ModuleDefinition module,out MountedScannerPlan plan,out string reason)
        {
            plan=null;reason=null;
            try
            {
                var owner=module.GetType(Owner);if(owner==null){reason="mounted-owner-unrecognized";return MountedScannerClassification.Unsupported;}
                var install=owner.Methods.SingleOrDefault(m=>Matches(m,"InstallMountedKeepRecovery",false,"System.Void",ContextType,TransactionType,Scanner));
                if(install==null){reason="no-mounted-installer";return MountedScannerClassification.NotApplicable;}
                var delayed=owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__",StringComparison.Ordinal) && Matches(m,m.Name,false,"System.Void","Iced.Intel.Assembler","System.ReadOnlySpan`1<Iced.Intel.Instruction>","System.UInt64"));
                var emitter=owner.Methods.Single(m=>Matches(m,"EmitMountedKeepRecovery",true,"System.Void",new[]{"Iced.Intel.Assembler"}.Concat(Enumerable.Repeat("System.UInt64",9)).ToArray()));
                var predicate=owner.NestedTypes.SelectMany(t=>t.Methods).Single(m=>m.Name.StartsWith("<InstallMountedKeepRecovery>b__",StringComparison.Ordinal) && Matches(m,m.Name,false,"System.Boolean","System.UInt64"));
                string capture=Fingerprint(install,delayed);
                if(capture!=Broken && capture!=Fixed){reason="mounted-capture-IL-unrecognized:"+capture;return MountedScannerClassification.Unsupported;}
                if(Fingerprint(predicate)!=Predicate || Fingerprint(emitter)!=Emitter || ConstantsFingerprint(emitter)!=EmitterConstants){reason="mounted-emitter-IL-unrecognized";return MountedScannerClassification.Unsupported;}
                ValidateOriginalOption(owner,install);
                if(capture==Fixed)
                {
                    var fixedStore=install.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.FieldType.MetadataType==MetadataType.UInt64);
                    var fixedField=((FieldReference)fixedStore.Operand).Resolve();
                    var fixedRead=delayed.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.FullName==fixedField.FullName);
                    ValidateCaptureReferences(module,install,fixedStore,delayed,fixedRead,fixedField);
                    reason="known-scalar-capture-already-fixed";return MountedScannerClassification.AlreadyFixed;
                }
                var getter=delayed.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt && i.Operand is MethodReference m && m.Name=="get_CurrentAddress" && m.DeclaringType.FullName==Scanner);
                if(getter.Previous.OpCode!=OpCodes.Ldfld)throw new InvalidOperationException();
                var field=((FieldReference)getter.Previous.Operand).Resolve();
                var stores=install.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.FullName==field.FullName).ToArray();
                if(stores.Length!=1 || !(stores[0].Previous.Operand is MethodReference scan) || scan.DeclaringType.FullName!=Scanner || scan.Name!="Scan" || field.FieldType.FullName!=Scanner)
                    throw new InvalidOperationException();
                ValidateCaptureReferences(module,install,stores[0],delayed,getter.Previous,field);
                // The gated emitter is owned only by this exact deferred generator.
                // New callers must not inherit a previously captured scanner receipt.
                int emitterCalls=0;
                foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))
                foreach(var instruction in method.Body.Instructions)
                    if(instruction.Operand is MethodReference reference && reference.FullName==emitter.FullName)
                    {
                        if(method!=delayed || instruction.OpCode!=OpCodes.Call)
                            throw new InvalidOperationException("Unknown mounted emitter caller/address use.");
                        emitterCalls++;
                    }
                if(emitterCalls!=1)throw new InvalidOperationException("Mounted emitter caller count differs.");
                // A new early-return branch may only precede this audited first publication.
                if(!HasUnprotectedPrefix(install,stores[0]) || install.Body.Instructions.TakeWhile(i=>i!=stores[0]).Any(i=>i.Operand is MethodReference m && m.Name=="AddInline"))throw new InvalidOperationException();
                plan=new MountedScannerPlan{Install=install,Delayed=delayed,Emitter=emitter,Predicate=predicate,Field=field,InstallBody=Body(install)};
                return MountedScannerClassification.Broken;
            }
            catch{reason="mounted-relevant-managed-contract-unavailable";return MountedScannerClassification.Unsupported;}
        }
        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach(var type in types){yield return type;foreach(var nested in AllTypes(type.NestedTypes))yield return nested;}
        }
        private static void ValidateCaptureReferences(ModuleDefinition module,MethodDefinition install,Instruction store,
            MethodDefinition delayed,Instruction read,FieldDefinition field)
        {
            int writes=0,reads=0;
            foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))
            foreach(var instruction in method.Body.Instructions)
            {
                if(!(instruction.Operand is FieldReference reference) || reference.Name!=field.Name || reference.DeclaringType.FullName!=field.DeclaringType.FullName)continue;
                if(reference.FieldType.FullName!=field.FieldType.FullName)throw new InvalidOperationException("Captured field reference signature differs.");
                if(method==install && instruction==store && instruction.OpCode==OpCodes.Stfld){writes++;continue;}
                if(method==delayed && instruction==read && instruction.OpCode==OpCodes.Ldfld){reads++;continue;}
                throw new InvalidOperationException("Unknown captured field reader/writer/address/token.");
            }
            if(writes!=1 || reads!=1)throw new InvalidOperationException("Captured field use count differs.");
        }
        internal static bool HasUnprotectedPrefix(MethodDefinition method,Instruction store)
        {
            int end=method.Body.Instructions.IndexOf(store);
            return method.Body.ExceptionHandlers.All(h=>method.Body.Instructions.IndexOf(h.TryStart)>end &&
                method.Body.Instructions.IndexOf(h.HandlerStart)>end && (h.FilterStart==null || method.Body.Instructions.IndexOf(h.FilterStart)>end));
        }
        internal static string ConstantsFingerprint(params MethodDefinition[] methods)
        {
            string text=string.Join(";",Constants(methods).Select(field=>Normalize(field.FullName)+":"+BitConverter.ToString(field.InitialValue)));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",string.Empty);
        }
        private static IEnumerable<FieldDefinition> Constants(IEnumerable<MethodDefinition> methods)
        {
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var method in methods)foreach(var instruction in method.Body.Instructions)
                if(instruction.OpCode==OpCodes.Ldtoken && instruction.Operand is FieldReference reference)
                {
                    var field=reference.Resolve();if(field.InitialValue!=null && field.InitialValue.Length>0 && seen.Add(field.FullName))yield return field;
                }
        }
        internal static void ValidateLoaded(Assembly assembly,MountedScannerPlan plan)
        {
            foreach(var method in new[]{plan.Install,plan.Delayed,plan.Emitter,plan.Predicate})
            {
                ValidateLoadedMethod(assembly,method);
            }
            foreach(var field in Constants(new[]{plan.Emitter}))
            {
                var loaded=assembly.ManifestModule.ResolveField(field.MetadataToken.ToInt32());var bytes=new byte[field.InitialValue.Length];
                RuntimeHelpers.InitializeArray(bytes,loaded.FieldHandle);
                if(!bytes.SequenceEqual(field.InitialValue))throw new InvalidOperationException("Loaded mounted emitter data differs.");
            }
        }
        internal static void ValidateLoadedMethod(Assembly assembly,MethodDefinition method)
        {
                var loaded=assembly.ManifestModule.ResolveMethod(method.MetadataToken.ToInt32());
                if(loaded.GetMethodBody().InitLocals!=method.Body.InitLocals)throw new InvalidOperationException("Loaded InitLocals differs.");
                using(var copy=new DynamicMethodDefinition(loaded))
                {
                    var definition=copy.Definition;
                    if(method.HasThis && !definition.HasThis)
                    {
                        if(definition.Parameters.Count!=method.Parameters.Count+1)throw new InvalidOperationException("DMD this encoding differs.");
                        definition.Parameters.RemoveAt(0);definition.HasThis=true;
                    }
                    if(Body(definition)!=Body(method))throw new InvalidOperationException("Loaded relevant Fixes body differs from disk.");
                }
        }
        private static void ValidateOriginalOption(TypeDefinition owner,MethodDefinition installer)
        {
            var call=owner.Methods.Where(m=>Matches(m,".ctor",false,"System.Void",ContextType,TransactionType,Scanner,"RedBird.Core.Memory.NativeStateBlock")).SelectMany(m=>m.Body.Instructions)
                .Single(i=>i.Operand is MethodReference r && r.FullName==installer.FullName);
            var arg3=call.Previous;var arg2=arg3?.Previous;var arg1=arg2?.Previous;var self=arg1?.Previous;var branch=self?.Previous;
            if(call.OpCode!=OpCodes.Call || arg3?.OpCode!=OpCodes.Ldarg_3 || arg2?.OpCode!=OpCodes.Ldarg_2 ||
                arg1?.OpCode!=OpCodes.Ldarg_1 || self?.OpCode!=OpCodes.Ldarg_0 || branch==null ||
                branch.OpCode.Code!=Code.Brfalse && branch.OpCode.Code!=Code.Brfalse_S || branch.Operand!=call.Next ||
                !(branch.Previous?.Operand is MethodReference getter) || getter.Name!="get_Value" || getter.Parameters.Count!=0 ||
                getter.DeclaringType.FullName!="BepInEx.Configuration.ConfigEntry`1<System.Boolean>" ||
                !(branch.Previous.Previous?.Operand is FieldReference field) || field.Name!="EnableMountedUnitsKeepRecovery" ||
                field.DeclaringType.FullName!="Fixes.Boostrap.Plugin" || field.FieldType.FullName!="BepInEx.Configuration.ConfigEntry`1<System.Boolean>")
                throw new InvalidOperationException("Mounted original option window changed.");
        }
        // MaxStack/declaring-name normalization differs in generated DMD copies; instruction/EH/local contract is identical.
        internal static string Body(MethodDefinition method)
        {
            var b=new StringBuilder();foreach(var v in method.Body.Variables)b.Append("v:").Append(TypeKey(v.VariableType)).Append('|');
            var code=method.Body.Instructions;
            foreach(var i in code)
            {
                if(i.OpCode==OpCodes.Nop)continue;b.Append(i.OpCode.Code).Append(':');object value=i.Operand;
                if(value is ILLabel label)b.Append(Index(code,label.Target));else if(value is ILLabel[] labels)b.Append(string.Join(",",labels.Select(l=>Index(code,l.Target))));else if(value is Instruction t)b.Append(Index(code,t));else if(value is Instruction[] ts)b.Append(string.Join(",",ts.Select(t=>Index(code,t))));
                else if(value is VariableDefinition v)b.Append("v").Append(v.Index);else if(value is ParameterDefinition p)b.Append("p").Append(p.Index);
                else if(value is MemberReference m)b.Append(MemberKey(m));else if(value!=null)b.Append(Convert.ToString(value,CultureInfo.InvariantCulture));b.Append('\n');
            }
            foreach(var h in method.Body.ExceptionHandlers)b.Append(h.HandlerType).Append(':').Append(Index(code,h.TryStart)).Append(':').Append(Index(code,h.TryEnd)).Append(':').Append(Index(code,h.HandlerStart)).Append(':').Append(Index(code,h.HandlerEnd));
            return b.ToString();
        }
        private static string TypeKey(TypeReference type)
        {
            if(type is GenericParameter p)return (p.Type==GenericParameterType.Method?"!!":"!")+p.Position;
            if(type is GenericInstanceType g)return Normalize(g.ElementType.FullName)+"<"+string.Join(",",g.GenericArguments.Select(TypeKey))+">";
            if(type is PointerType pointer)return TypeKey(pointer.ElementType)+"*";
            if(type is ByReferenceType byref)return TypeKey(byref.ElementType)+"&";
            if(type is ArrayType array)return TypeKey(array.ElementType)+"["+new string(',',array.Rank-1)+"]";
            return Normalize(type.FullName);
        }
        private static string MemberKey(MemberReference member)
        {
            if(member is FieldReference field)
            {
                // Reflection/DMD represents these four exact SDK function-pointer fields as IntPtr.
                // Disk fingerprints already bind their complete original signatures.
                bool vtable=field.DeclaringType.FullName=="SHCDESE.Interop.VTables.UnitFunctionsVTable" &&
                    new[]{"KnightUpdate","ArabHorsemanUpdate","BedouinCamelLancerUpdate","BedouinHeavyCamelUpdate"}.Contains(field.Name);
                return (vtable?"System.IntPtr":TypeKey(field.FieldType))+" "+TypeKey(field.DeclaringType)+"::"+Normalize(field.Name);
            }
            if(member is MethodReference method)return TypeKey(method.ReturnType)+" "+TypeKey(method.DeclaringType)+"::"+Normalize(method.Name)+
                (method is GenericInstanceMethod generic?"<"+string.Join(",",generic.GenericArguments.Select(TypeKey))+">":"")+
                "("+string.Join(",",method.Parameters.Select(p=>TypeKey(p.ParameterType)))+")";
            if(member is TypeReference type)return TypeKey(type);
            return Normalize(member.FullName);
        }
        internal static string Fingerprint(params MethodDefinition[] methods)
        {
            var result = new StringBuilder();
            foreach (var method in methods)
            {
                result.Append(Normalize(method.FullName)).Append('|').Append(method.Body.InitLocals).Append('|').Append(method.Body.MaxStackSize);
                foreach (var variable in method.Body.Variables) result.Append("|v:").Append(Normalize(variable.VariableType.FullName));
                var code = method.Body.Instructions;
                foreach (var instruction in code)
                {
                    if (instruction.OpCode == OpCodes.Nop) continue;
                    result.Append('\n').Append(instruction.OpCode.Code).Append(':');
                    object value = instruction.Operand;
                    if (value is Instruction target) result.Append(Index(code, target));
                    else if (value is Instruction[] targets) result.Append(string.Join(",", targets.Select(t => Index(code, t))));
                    else if (value is VariableDefinition variable) result.Append("v").Append(variable.Index);
                    else if (value is ParameterDefinition parameter) result.Append("p").Append(parameter.Index);
                    else if (value is MemberReference member) result.Append(Normalize(member.FullName));
                    else if (value != null) result.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                foreach (var h in method.Body.ExceptionHandlers)
                    result.Append("|eh:").Append(h.HandlerType).Append('|').Append(h.CatchType == null ? "" : Normalize(h.CatchType.FullName))
                        .Append('|').Append(Index(code, h.TryStart)).Append('|').Append(Index(code, h.TryEnd)).Append('|').Append(Index(code, h.HandlerStart)).Append('|').Append(Index(code, h.HandlerEnd)).Append('|').Append(Index(code, h.FilterStart));
                result.Append("\n--\n");
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(result.ToString()))).Replace("-", "");
        }
        private static int Index(Mono.Collections.Generic.Collection<Instruction> code, Instruction target)
        {
            if (target == null) return -1;
            int index = 0;
            foreach (var instruction in code)
            {
                if (instruction == target) return index;
                if (instruction.OpCode != OpCodes.Nop) index++;
            }
            throw new InvalidOperationException("Branch target outside method.");
        }
        private static string Normalize(string value)
        {
            value = Regex.Replace(value, @"<>c__DisplayClass\d+_(\d+)", "<>closure_$1");
            value = Regex.Replace(value, @"(b__|<>9__)(\d+)_(\d+)", "$1ordinal_$3");
            return value.Replace("::relocateAddress", "::relocate");
        }
    }
}
