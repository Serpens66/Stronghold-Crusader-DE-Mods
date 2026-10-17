using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace DependencyBootstrapProbe
{
    internal static class LifecycleTests
    {
        private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS lifecycle: " + name); }
        internal static void Run(string game)
        {
            var invocation = new LifecycleInvocation();
            string first = invocation.Begin(); invocation.Complete();
            string second = invocation.Begin();
            Check(first != second, "repeated original invocations receive distinct evidence identities");
            bool overlap = false; try { invocation.Begin(); } catch (InvalidOperationException) { overlap = true; }
            Check(overlap, "overlapping or incomplete invocation rejected"); invocation.Complete();
            bool duplicate = false; try { invocation.Complete(); } catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "duplicate completion rejected");
            var assembly = Assembly.LoadFrom(Path.Combine(game, "BepInEx/plugins/000shcdese/SHCDESE.dll"));
            var owner = assembly.GetType("SHCDESE.API.Components.Archive.MapModManager", true);
            var method = owner.GetMethod("TryUpdateModsFromRemote", BindingFlags.Instance | BindingFlags.NonPublic);
            using (var copy = new DynamicMethodDefinition(method))
            using (var il = new ILContext(copy.Definition))
            {
                var snapshot = new ContractTests.BodySnapshot(il);
                LifecycleHook.Transform(il);
                snapshot.AssertPreserved(il, false, 5);
                var compiled = copy.Generate().CreateDelegate(typeof(Action<>).MakeGenericType(owner));
                GC.KeepAlive(compiled);
                Check(true, "real DMD, five neutral observer blocks, original calls/branches/EH retained; delegate never invoked");
            }
            foreach (string name in new[] { "GetListOfSubscribedItemsPaths", "TryLoad", "GetFirstMapFileInDirectory", "ResetDeployAttempts", "ReadDeployAttempts", "TryStageModUpdate", "MessageBoxA" })
                Reject(method, il => ((MethodReference)il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == name).Operand).Name = "Changed", name);
            Reject(method, il => {
                var load = il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == "TryLoad");
                ((VariableDefinition)load.Previous.Operand).VariableType = il.Module.TypeSystem.Object;
            }, "wrong archive local");
            Reject(method, il => {
                var query = il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == "GetListOfSubscribedItemsPaths");
                il.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Br, query.Next));
            }, "incoming branch to capture boundary");
            Reject(method, il => {
                var query = il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == "GetListOfSubscribedItemsPaths");
                il.Body.Instructions.Insert(0, Instruction.Create(query.OpCode, (MethodReference)query.Operand));
            }, "ambiguous getter");
            Reject(method, il => {
                var map = (MethodReference)il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == "GetFirstMapFileInDirectory").Operand;
                map.ReturnType = il.Module.TypeSystem.Int32;
            }, "changed map return signature");
            Reject(method, il => {
                var query = il.Body.Instructions.First(i => (i.Operand as MethodReference)?.Name == "GetListOfSubscribedItemsPaths");
                il.Body.ExceptionHandlers[0].TryStart = query.Next;
            }, "exception boundary at capture");
            var packages = new List<LifecyclePackage>();
            Func<bool, LifecycleOwner> evaluate = complete => LifecycleOwnership.Evaluate("Probe", "1.0", "C:/fixture/Probe.dll", "hash", "BepInEx/plugins/Probe/Probe.dll", packages, complete);
            Check(evaluate(true).State == "NotFound", "manual installation not treated as unsubscribe");
            packages.Add(new LifecyclePackage { Loaded = true, Manifest = "BepInEx", Path = "A.map", Payload = new List<string> { "BepInEx/plugins/Probe/Probe.dll" } });
            Check(evaluate(true).State == "Unique", "single exact payload claim");
            LifecycleOwnership.BindLocal(packages[0], "fixture.json", new Dictionary<string, object> { { "GUID", "different" }, { "Version", "2" } });
            Check(evaluate(true).State == "Unverifiable", "conflicting local management metadata");
            LifecycleOwnership.BindLocal(packages[0], "fixture.json", null);
            Check(evaluate(true).State == "Unique" && packages[0].LocalState == "NotFound", "missing local receipt explicitly recorded, path claim is not ownership");
            Check(evaluate(false).State == "Unverifiable", "incomplete data cannot establish exclusive ownership");
            packages.Add(new LifecyclePackage { Loaded = true, Manifest = "BepInEx", Path = "B.map", Payload = new List<string> { "bepinex/plugins/probe/probe.dll" } });
            Check(evaluate(true).State == "Ambiguous", "case-conflicting competing package");
            packages.Clear(); packages.Add(new LifecyclePackage { Loaded = false, Path = "failed.map", Payload = new List<string> { "BepInEx/plugins/Probe/Probe.dll" } });
            Check(evaluate(true).State == "NotFound", "failed archive is not accepted as an owner");
            packages[0].Loaded = true; packages[0].Manifest = "Asset";
            Check(evaluate(true).State == "NotFound", "asset archive cannot claim installed BepInEx payload");
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lifecycle-fixtures");
            Directory.CreateDirectory(root); string file = Path.Combine(root, "info.json");
            foreach (string json in new[] { "{broken", "[]" })
            {
                File.WriteAllText(file, json); bool failed = false; try { LifecycleCapture.ReadJson(file); } catch { failed = true; }
                Check(failed, "invalid local metadata rejected");
            }
            File.WriteAllText(file, "{}");
            using (var locked = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
            { bool failed = false; try { LifecycleCapture.ReadJson(file); } catch { failed = true; } Check(failed, "unreadable local metadata rejected"); }
        }
        private static void Reject(MethodInfo method, Action<ILContext> mutation, string name)
        {
            using (var copy = new DynamicMethodDefinition(method))
            using (var il = new ILContext(copy.Definition))
            {
                mutation(il); var snapshot = new ContractTests.BodySnapshot(il); bool rejected = false;
                try { LifecycleHook.Transform(il); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "reject before mutation: " + name); snapshot.AssertUnchanged(il);
            }
        }
    }
}
