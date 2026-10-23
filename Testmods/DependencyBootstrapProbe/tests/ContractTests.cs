using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace DependencyBootstrapProbe
{
    internal static class Launcher
    {
        // Only this isolated desktop test process installs a resolver. Runtime code never does.
        private static string game;
        public static int Main(string[] args)
        {
            game = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += ResolveTestDependency;
            try { Run(args); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static Assembly ResolveTestDependency(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] { "BepInEx/core", "Stronghold Crusader Definitive Edition_Data/Managed", "BepInEx/plugins/000shcdese" })
            {
                string path = Path.Combine(game, directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
        {
            if (args.Length == 4 && args[1] == "offline") ShadowTests.Offline(game, args[2], args[3]);
            else if (args.Length == 3 && args[1] == "update-package") UpdateTrialTests.Package(game, args[2]);
            else { ShadowTests.Run(); ContractTests.Run(game); LifecycleTests.Run(game); PairTests.Run(); UpdateTrialTests.Run(); }
        }
    }

    internal static class ContractTests
    {
        private static MethodInfo Start => typeof(BepInEx.Bootstrap.Chainloader).GetMethod("Start");
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

        internal static void Run(string game)
        {
            TestEarlyEntry();
            using (var copy = new DynamicMethodDefinition(Start))
            using (var context = new ILContext(copy.Definition))
            {
                var before = new BodySnapshot(context);
                HookRuntime.ModifyChainloader(context);
                before.AssertPreserved(context, true);
                // Compilation is tested again in Unity Mono; never execute this method or install a hook here.
                var compiled = copy.Generate().CreateDelegate(typeof(Action));
                // Desktop CLR cannot JIT Unity ECalls; full PrepareDelegate remains mandatory in the game.
                GC.KeepAlive(compiled);
                Console.WriteLine("PASS: actual DMD Chainloader transform, stack neutrality, original flow and desktop delegate generation (never invoked; Unity JIT checked in game).");
            }
            Reject("missing field", il => {
                foreach (var field in WinnerFields(il)) field.Name = "missingWinner";
            });
            Reject("wrong field type", il => {
                foreach (var field in WinnerFields(il)) field.FieldType = il.Module.TypeSystem.Int32;
            });
            Reject("ambiguous local", il => {
                string name = WinnerFields(il)[0].DeclaringType.FullName;
                il.Body.Variables.Add(new VariableDefinition(il.Body.Variables.Single(v => v.VariableType.FullName == name).VariableType));
            });
            Reject("missing failure set", il => {
                var local = il.Body.Variables.Single(v => v.VariableType.FullName == "System.Collections.Generic.HashSet`1<System.String>");
                local.VariableType = il.Module.TypeSystem.Object;
            });
            Reject("changed AddComponent", il => {
                ((MethodReference)il.Body.Instructions.Single(i => IsCall(i, "UnityEngine.GameObject", "AddComponent")).Operand).Name = "ChangedAdd";
            });
            Reject("changed ToList", il => {
                var sort = il.Body.Instructions.Single(i => IsCall(i, "BepInEx.Utility", "TopologicalSort"));
                ((GenericInstanceMethod)sort.Next.Operand).ElementMethod.Name = "ChangedList";
            });
            Reject("branch into injection site", il => {
                var target = il.Body.Instructions.Single(i => IsCall(i, "UnityEngine.GameObject", "AddComponent"));
                il.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Br, target));
            });
            Reject("exception boundary at injection site", il => {
                il.Body.ExceptionHandlers[0].TryStart = il.Body.Instructions.Single(i => IsCall(i, "UnityEngine.GameObject", "AddComponent"));
            });
            var se = Assembly.LoadFrom(Path.Combine(game, "BepInEx/plugins/000shcdese/SHCDESE.dll"));
            var manager = se.GetType("SHCDESE.API.Components.ModManager.GameAssetModManager", true);
            var check = manager.GetMethod("PrepareAllAndCheckCompatibility", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (string malformed in new[] { "missing index", "ambiguous index", "changed issues", "branch boundary", "extra return", "return boundary" })
            using (var copy = new DynamicMethodDefinition(check))
            using (var context = new ILContext(copy.Definition))
            {
                var index = context.Body.Variables.Single(v => v.VariableType.FullName.Contains("Dictionary`2<System.String,SHCDESE.API.Components.ModManager.GameAssetModManager/InstalledModVersion>"));
                var site = context.Body.Instructions.Single(i => i.OpCode == OpCodes.Newobj && i.Operand is MethodReference m && m.DeclaringType.FullName.Contains("List`1<SHCDESE.API.Components.ModManager.ModCompatibilityIssue>"));
                if (malformed == "missing index") index.VariableType = context.Module.TypeSystem.Object;
                if (malformed == "ambiguous index") context.Body.Variables.Add(new VariableDefinition(index.VariableType));
                if (malformed == "changed issues") site.OpCode = OpCodes.Nop;
                if (malformed == "branch boundary") context.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Br, site.Next.Next));
                if (malformed == "extra return") context.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                if (malformed == "return boundary") context.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Br, context.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret)));
                var before = new BodySnapshot(context);
                bool rejected = false;
                try { HookRuntime.ModifyCompatibility(context); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Malformed SE contract accepted: " + malformed);
                before.AssertUnchanged(context);
                Console.WriteLine("PASS: SE rejected before mutation: " + malformed);
            }
            ShadowTests.CompareExtender(manager);
            using (var copy = new DynamicMethodDefinition(check))
            using (var context = new ILContext(copy.Definition))
            {
                var before = new BodySnapshot(context);
                HookRuntime.ModifyCompatibility(context);
                before.AssertPreserved(context, false);
                var compiled = copy.Generate().CreateDelegate(typeof(Func<,,>).MakeGenericType(manager, typeof(string), check.ReturnType));
                // Desktop CLR cannot JIT Unity ECalls; full PrepareDelegate remains mandatory in the game.
                GC.KeepAlive(compiled);
                Console.WriteLine("PASS: actual SE DMD transform and desktop delegate generation; no check invoked.");
            }
        }

        private static void TestEarlyEntry()
        {
            int ready = 0, complete = 0, errors = 0;
            var gate = new ReadyGate(() => ready++, () => complete++, (m, d) => errors++);
            gate.Process("Other", "Chainloader ready");
            gate.Process("BepInEx", "Chainloader started");
            gate.Process(null, null);
            Require(ready == 0 && complete == 0 && errors == 0, "Foreign/missing messages triggered callbacks");
            gate.Process("BepInEx", "Chainloader ready");
            gate.Process("BepInEx", "Chainloader ready");
            gate.Process("BepInEx", "Chainloader startup complete");
            gate.Process("BepInEx", "Chainloader startup complete");
            Require(ready == 1 && complete == 1 && errors == 0, "Repeated callbacks");
            new ReadyGate(() => ready++, () => complete++, (m, d) => errors++)
                .Process("BepInEx", "Chainloader startup complete");
            Require(errors == 1 && complete == 1, "Missing ready accepted");
            var failing = new ReadyGate(() => { throw new Exception("fixture"); }, () => { }, (m, d) => { errors++; throw new Exception("logger"); });
            failing.Process("BepInEx", "Chainloader ready");
            failing.Process("BepInEx", "Chainloader ready");
            Require(errors == 2, "Callback failure was not contained exactly once");
            using (var module = ModuleDefinition.ReadModule(typeof(ContractTests).Assembly.Location))
            {
                var names = new[] { "DependencyBootstrapPatcher", "ReadyGate", "ProbeTrace", "ProbeIdentity" };
                var types = module.Types.Where(t => names.Contains(t.Name)).ToArray();
                Require(types.Length == names.Length, "Early types missing");
                foreach (var type in types.Concat(types.SelectMany(t => t.NestedTypes)))
                {
                    foreach (var field in type.Fields) RejectEarlyType(field.FieldType.FullName);
                    foreach (var method in type.Methods)
                    {
                        RejectEarlyType(method.ReturnType.FullName);
                        foreach (var parameter in method.Parameters) RejectEarlyType(parameter.ParameterType.FullName);
                        if (!method.HasBody) continue;
                        foreach (var variable in method.Body.Variables) RejectEarlyType(variable.VariableType.FullName);
                        foreach (var instruction in method.Body.Instructions)
                            if (instruction.Operand is MemberReference member) RejectEarlyType(member.FullName);
                    }
                }
                var finish = types.Single(t => t.Name == "DependencyBootstrapPatcher").Methods.Single(m => m.Name == "Finish");
                Require(!finish.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "InvokeHookRuntime"), "Finish dispatches hooks");
            }
            Console.WriteLine("PASS: early entry has no Unity/MonoMod/Chainloader/HookRuntime type tokens; missing/foreign/duplicate/error listener tests.");
        }
        private static void RejectEarlyType(string name)
        {
            Require(!new[] { "UnityEngine", "MonoMod", "BepInEx.Bootstrap.Chainloader", "DependencyBootstrapProbe.HookRuntime" }.Any(name.Contains), "Early runtime dependency: " + name);
        }
        private static FieldReference[] WinnerFields(ILContext il) => il.Body.Instructions.Select(i => i.Operand as FieldReference)
            .Where(f => f != null && f.Name == "pluginsByGUID").Distinct().ToArray();
        private static bool IsCall(Instruction i, string type, string name) => i.Operand is MethodReference m && m.DeclaringType.FullName == type && m.Name == name;
        private static void Reject(string name, Action<ILContext> mutate)
        {
            using (var copy = new DynamicMethodDefinition(Start))
            using (var context = new ILContext(copy.Definition))
            {
                mutate(context);
                var before = new BodySnapshot(context);
                bool rejected = false;
                try { HookRuntime.ModifyChainloader(context); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Accepted malformed contract: " + name);
                before.AssertUnchanged(context);
                Console.WriteLine("PASS: rejected before mutation: " + name);
            }
        }

        internal sealed class BodySnapshot
        {
            private readonly Instruction[] instructions;
            private readonly OpCode[] codes;
            private readonly object[] operands;
            private readonly ILLabel[] labels;
            private readonly Instruction[] targets;
            private readonly ExceptionHandler[] handlers;
            private readonly Instruction[][] boundaries;
            private readonly VariableDefinition[] locals;
            internal BodySnapshot(ILContext il)
            {
                instructions = il.Body.Instructions.ToArray();
                codes = instructions.Select(i => i.OpCode).ToArray();
                operands = instructions.Select(i => i.Operand).ToArray();
                labels = il.Labels.ToArray(); targets = labels.Select(l => l.Target).ToArray();
                handlers = il.Body.ExceptionHandlers.ToArray(); boundaries = handlers.Select(Boundaries).ToArray();
                locals = il.Body.Variables.ToArray();
            }
            private static Instruction[] Boundaries(ExceptionHandler h) => new[] { h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd, h.FilterStart };
            internal void AssertUnchanged(ILContext il)
            {
                Require(instructions.SequenceEqual(il.Body.Instructions), "Instructions mutated on rejection");
                AssertOriginals(il);
            }
            private void AssertOriginals(ILContext il)
            {
                Require(locals.SequenceEqual(il.Body.Variables), "Original locals changed");
                Require(handlers.SequenceEqual(il.Body.ExceptionHandlers), "Exception handlers changed");
                for (int n = 0; n < instructions.Length; n++)
                    Require(instructions[n].OpCode == codes[n] && Equals(instructions[n].Operand, operands[n]), "Original opcode/operand changed");
                for (int n = 0; n < labels.Length; n++) Require(labels[n].Target == targets[n], "Original branch target changed");
                for (int n = 0; n < handlers.Length; n++) Require(boundaries[n].SequenceEqual(Boundaries(handlers[n])), "Exception boundary changed");
            }
            internal void AssertPreserved(ILContext il, bool chain, int expectedGroups = 3)
            {
                AssertOriginals(il);
                Require(instructions.SequenceEqual(il.Body.Instructions.Where(i => instructions.Contains(i))), "Original order changed");
                int groups = 0;
                for (int pos = 0; pos < il.Body.Instructions.Count;)
                {
                    if (instructions.Contains(il.Body.Instructions[pos])) { pos++; continue; }
                    int start = pos;
                    while (pos < il.Body.Instructions.Count && !instructions.Contains(il.Body.Instructions[pos])) pos++;
                    var next = pos < il.Body.Instructions.Count ? il.Body.Instructions[pos] : null;
                    bool duplicateType = il.Body.Instructions[start].OpCode == OpCodes.Dup;
                    if (il.Body.Instructions[start].OpCode == OpCodes.Call && il.Body.Instructions[start].Operand is MethodReference adapter &&
                        adapter.DeclaringType.FullName == typeof(LocalUpdateTrial).FullName && adapter.Name == "Apply") duplicateType = true;
                    int initial = duplicateType ? 1 : 0, depth = initial;
                    for (int n = start; n < pos; n++)
                    {
                        var instruction = il.Body.Instructions[n];
                        int pop = Pops(instruction), push = Pushes(instruction);
                        Require(depth >= pop, "Inserted block consumes original stack below its Type operand");
                        depth = depth - pop + push;
                    }
                    Require(depth == initial, "Inserted block changes original stack height");
                    groups++;
                }
                Require(groups == expectedGroups, "Unexpected insertion blocks");
            }
            private static int Pops(Instruction i)
            {
                if (i.Operand is MethodReference m && (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt))
                    return m.Parameters.Count + (m.HasThis ? 1 : 0);
                switch (i.OpCode.StackBehaviourPop)
                {
                    case StackBehaviour.Pop0: return 0;
                    case StackBehaviour.Pop1: case StackBehaviour.Popi: case StackBehaviour.Popref: return 1;
                    default: throw new Exception("Unexpected inserted pop: " + i);
                }
            }
            private static int Pushes(Instruction i)
            {
                if (i.Operand is MethodReference m && (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt))
                    return m.ReturnType.FullName == "System.Void" ? 0 : 1;
                switch (i.OpCode.StackBehaviourPush)
                {
                    case StackBehaviour.Push0: return 0;
                    case StackBehaviour.Push1: case StackBehaviour.Pushi: case StackBehaviour.Pushref: return 1;
                    case StackBehaviour.Push1_push1: return 2;
                    default: throw new Exception("Unexpected inserted push: " + i);
                }
            }
        }
    }
}
