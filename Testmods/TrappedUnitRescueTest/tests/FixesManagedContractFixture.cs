using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TrappedUnitRescueTest;

namespace TrappedUnitRescueTest.Tests
{
    internal static class FixesManagedContractFixture
    {
        private static readonly string PathToFixes = System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ?? @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition", "BepInEx", "plugins", "fixes", "fixes.dll");
        internal static void Run()
        {
            using (var module = ModuleDefinition.ReadModule(PathToFixes))
            {
                Console.WriteLine("Wall contract fingerprint=" + FixesManagedContract.Fingerprint(module));
                FixesManagedContract.Validate(module);
            }
            var assembly = Assembly.LoadFrom(PathToFixes);
            FixesManagedContract.Validate(assembly);
            Accept(module => {
                module.Assembly.Name.Version = new Version(90, 1, 2, 3);
                var unrelated = Owner(module);
                var method = new MethodDefinition("AddedUnrelatedMountedFeature", Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Public, module.TypeSystem.Void);
                unrelated.Methods.Add(method); method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            });
            Accept(module => {
                foreach (var type in All(module))
                {
                    type.Name = Regex.Replace(type.Name, @"<>c__DisplayClass(\d+)_(\d+)", m => "<>c__DisplayClass" + (int.Parse(m.Groups[1].Value) + 100) + "_" + m.Groups[2].Value);
                    foreach (var method in type.Methods)
                        method.Name = Regex.Replace(method.Name, @"b__(\d+)_(\d+)", m => "b__" + (int.Parse(m.Groups[1].Value) + 100) + "_" + m.Groups[2].Value);
                }
            });
            Reject(module => Root(module).Body.MaxStackSize = 0);
            Reject(module => {
                var branch = Root(module).Body.Instructions.First(i => i.Operand is Instruction);
                branch.Operand = Root(module).Body.Instructions[0];
            });
            Reject(module => {
                var radius = Owner(module).Methods.Single(m => m.Name == "BuildSafePositionMap").Body.Instructions.First(i => i.OpCode == OpCodes.Ldc_I4_S && Convert.ToInt32(i.Operand) == 15);
                radius.Operand = (sbyte)16;
            });
            Reject(module => {
                var delayed = All(module).SelectMany(t => t.Methods).Single(m => m.Name.StartsWith("<MoveSiegeUnit>b__", StringComparison.Ordinal));
                var call = delayed.Body.Instructions.First(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt);
                var original = (MethodReference)call.Operand;
                call.Operand = new MethodReference("WrongRecoveryCall", original.ReturnType, original.DeclaringType) { HasThis = original.HasThis };
            });
            Reject(module => {
                var field = ((FieldReference)Root(module).Body.Instructions.First(i => i.OpCode == OpCodes.Ldtoken && i.Operand is FieldReference).Operand).Resolve();
                byte[] value = (byte[])field.InitialValue.Clone(); value[0] ^= 1; field.InitialValue = value;
            });
            Console.WriteLine("PASS: Fixes relevant managed IL contracts; unrelated feature/version accepted, root/helper/delayed/data mutations rejected.");
        }
        private static TypeDefinition Owner(ModuleDefinition m) => m.GetType("Fixes.Events.FixesAIEvents");
        private static MethodDefinition Root(ModuleDefinition m) => Owner(m).Methods.Single(v => v.Name == "OnAIBuildWall");
        private static System.Collections.Generic.IEnumerable<TypeDefinition> All(ModuleDefinition m) => m.Types.SelectMany(t => new[] { t }.Concat(t.NestedTypes));
        private static void Accept(Action<ModuleDefinition> mutation)
        { using (var m = ModuleDefinition.ReadModule(PathToFixes)) { mutation(m); FixesManagedContract.Validate(m); } }
        private static void Reject(Action<ModuleDefinition> mutation)
        {
            using (var m = ModuleDefinition.ReadModule(PathToFixes))
            {
                mutation(m); bool rejected = false;
                try { FixesManagedContract.Validate(m); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new Exception("Relevant Fixes wall mutation was accepted.");
            }
        }
    }
}
