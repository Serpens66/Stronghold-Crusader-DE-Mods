using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace TrappedUnitRescueTest
{
    // Only the wall replacement's reachable local helpers are pinned. Compiler numbering,
    // metadata tokens, assembly versions and unrelated Fixes features are not identities.
    internal static class FixesManagedContract
    {
        private const string Owner = "Fixes.Events.FixesAIEvents";
        private const string Expected = "33E2D9BCABB08CA26049001C6CC6AD68DE2DFF35B053EF9ED035B947159AB582";

        internal static void Validate(Assembly assembly)
        {
            // Disk is used only to enumerate the relevant closure and RVA constants. Every
            // method body is also checked from its loaded MethodInfo before any publication.
            using (var module = ModuleDefinition.ReadModule(assembly.Location))
            {
                if (module.Mvid != assembly.ManifestModule.ModuleVersionId)
                    throw new InvalidOperationException("Loaded/disk Fixes module identity differs.");
                Validate(module);
                foreach (var field in Constants(Closure(module)))
                {
                    var loadedField = assembly.ManifestModule.ResolveField(field.MetadataToken.ToInt32());
                    var bytes = new byte[field.InitialValue.Length];
                    RuntimeHelpers.InitializeArray(bytes, loadedField.FieldHandle);
                    if (!bytes.SequenceEqual(field.InitialValue))
                        throw new InvalidOperationException("Loaded Fixes wall constant differs: " + field.FullName);
                }
                foreach (MethodDefinition method in Closure(module))
                {
                    var loaded = (MethodBase)assembly.ManifestModule.ResolveMethod(method.MetadataToken.ToInt32());
                    using (var definition = new DynamicMethodDefinition(loaded))
                    {
                        // DMD omits InitLocals; recover this semantic bit from the actual
                        // loaded body rather than discarding it from the contract.
                        definition.Definition.Body.InitLocals = loaded.GetMethodBody().InitLocals;
                        definition.Definition.Body.MaxStackSize = loaded.GetMethodBody().MaxStackSize;
                        // DMD turns instance methods into static methods with an explicit
                        // first self parameter. Verify and reverse only that known encoding.
                        if (method.HasThis && !definition.Definition.HasThis)
                        {
                            if (loaded.IsStatic || definition.Definition.Parameters.Count != method.Parameters.Count + 1 ||
                                TypeKey(definition.Definition.Parameters[0].ParameterType) != TypeKey(method.DeclaringType))
                                throw new InvalidOperationException("Unknown DMD instance encoding: " + method.FullName);
                            definition.Definition.Parameters.RemoveAt(0);
                            definition.Definition.HasThis = true;
                        }
                        if (Body(method) != Body(definition.Definition))
                            throw new InvalidOperationException("Loaded Fixes wall IL differs: " + method.FullName);
                    }
                }
            }
        }

        internal static void Validate(ModuleDefinition module)
        {
            string actual = Fingerprint(module);
            if (actual != Expected)
                throw new InvalidOperationException("Fixes wall replacement IL contract changed: " + actual);
        }

        internal static string Fingerprint(ModuleDefinition module)
        {
            var methods = Closure(module);
            var parts = methods.Select(m => Normalize(m.FullName) + "\n" + Body(m)).OrderBy(s => s, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n--\n", parts) + "\nconstants=" + string.Join(";", Constants(methods).Select(f => Normalize(f.FullName) + ":" + BitConverter.ToString(f.InitialValue)).OrderBy(v => v, StringComparer.Ordinal))))).Replace("-", "");
        }

        private static List<MethodDefinition> Closure(ModuleDefinition module)
        {
            TypeDefinition owner = module.GetType(Owner);
            if (owner == null) throw new InvalidOperationException("Fixes wall owner missing.");
            MethodDefinition root = owner.Methods.Single(m => m.Name == "OnAIBuildWall");
            var pending = new Queue<MethodDefinition>(); pending.Enqueue(root);
            var result = new List<MethodDefinition>();
            while (pending.Count != 0)
            {
                var current = pending.Dequeue();
                if (result.Contains(current)) continue;
                if (!current.HasBody) throw new InvalidOperationException("Fixes wall helper body missing.");
                result.Add(current);
                foreach (var instruction in current.Body.Instructions)
                {
                    var call = instruction.Operand as MethodReference;
                    if (call == null || !(call.DeclaringType.FullName == Owner || call.DeclaringType.FullName.StartsWith(Owner + "/", StringComparison.Ordinal))) continue;
                    // Post preference lookups and external APIs remain original; all directly
                    // reachable helpers defined by the wall owner are part of this contract.
                    var definition = call.Resolve();
                    if (definition == null || definition.Module != module) throw new InvalidOperationException("Fixes wall helper resolution changed.");
                    pending.Enqueue(definition);
                }
            }
            return result;
        }

        private static IEnumerable<FieldDefinition> Constants(IEnumerable<MethodDefinition> methods)
        {
            var fields = new List<FieldDefinition>();
            foreach (var method in methods)
                foreach (var instruction in method.Body.Instructions)
                {
                    var reference = instruction.Operand as FieldReference;
                    if (reference == null || !reference.DeclaringType.FullName.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal)) continue;
                    var field = reference.Resolve();
                    if (field == null || field.InitialValue == null || field.InitialValue.Length == 0)
                        throw new InvalidOperationException("Fixes wall array data missing.");
                    if (!fields.Contains(field)) fields.Add(field);
                }
            return fields;
        }

        private static string Normalize(string value)
        {
            value = Regex.Replace(value, @"<>c__DisplayClass\d+_(\d+)", "<>wallClosure_$1");
            return Regex.Replace(value, @"<([^>]+)>b__\d+_(\d+)", "<$1>b__compiler_$2");
        }

        private static string TypeKey(TypeReference type)
        {
            var generic = type as GenericParameter;
            if (generic != null) return (generic.Type == GenericParameterType.Method ? "!!" : "!") + generic.Position;
            var instance = type as GenericInstanceType;
            if (instance != null) return TypeKey(instance.ElementType) + "<" + string.Join(",", instance.GenericArguments.Select(TypeKey)) + ">";
            var byref = type as ByReferenceType; if (byref != null) return TypeKey(byref.ElementType) + "&";
            var pointer = type as PointerType; if (pointer != null) return TypeKey(pointer.ElementType) + "*";
            var array = type as ArrayType; if (array != null) return TypeKey(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
            return Normalize(type.FullName);
        }

        private static string MemberKey(MemberReference member)
        {
            var method = member as MethodReference;
            if (method != null)
            {
                var instance = method as GenericInstanceMethod;
                string name = Normalize(method.Name) + (instance == null ? "" : "<" + string.Join(",", instance.GenericArguments.Select(TypeKey)) + ">");
                return TypeKey(method.ReturnType) + " " + TypeKey(method.DeclaringType) + "::" + name + "(" + string.Join(",", method.Parameters.Select(p => TypeKey(p.ParameterType))) + ")";
            }
            var field = member as FieldReference;
            if (field != null) return TypeKey(field.FieldType) + " " + TypeKey(field.DeclaringType) + "::" + Normalize(field.Name);
            var type = member as TypeReference;
            return type == null ? Normalize(member.FullName) : TypeKey(type);
        }

        private static string Body(MethodDefinition method)
        {
            var instructions = method.Body.Instructions;
            var text = new StringBuilder();
            text.Append(method.HasThis).Append('|').Append(method.ExplicitThis).Append('|').Append(method.CallingConvention).Append('|');
            text.Append(Normalize(method.ReturnType.FullName));
            foreach (var parameter in method.Parameters) text.Append('|').Append(Normalize(parameter.ParameterType.FullName));
            text.Append("\ninit=").Append(method.Body.InitLocals).Append("|stack=").Append(method.Body.MaxStackSize);
            foreach (var variable in method.Body.Variables) text.Append("\nlocal=").Append(Normalize(variable.VariableType.FullName));
            foreach (var instruction in instructions)
            {
                text.Append('\n').Append(instruction.OpCode.Code).Append(':');
                object operand = instruction.Operand;
                var branch = operand as Instruction;
                var branches = operand as Instruction[];
                var member = operand as MemberReference;
                var variable = operand as VariableDefinition;
                var parameter = operand as ParameterDefinition;
                if (branch != null) text.Append(instructions.IndexOf(branch));
                else if (branches != null) text.Append(string.Join(",", branches.Select(b => instructions.IndexOf(b))));
                else if (variable != null) text.Append("v").Append(variable.Index);
                else if (parameter != null) text.Append("p").Append(parameter.Index);
                else if (member != null) text.Append(MemberKey(member));
                else if (operand != null) text.Append(Convert.ToString(operand, CultureInfo.InvariantCulture));
            }
            foreach (var handler in method.Body.ExceptionHandlers)
                text.Append("\neh=").Append(handler.HandlerType).Append('|').Append(handler.CatchType == null ? "" : Normalize(handler.CatchType.FullName))
                    .Append('|').Append(instructions.IndexOf(handler.TryStart)).Append('|').Append(instructions.IndexOf(handler.TryEnd))
                    .Append('|').Append(instructions.IndexOf(handler.HandlerStart)).Append('|').Append(instructions.IndexOf(handler.HandlerEnd))
                    .Append('|').Append(instructions.IndexOf(handler.FilterStart));
            return text.ToString();
        }
    }
}
