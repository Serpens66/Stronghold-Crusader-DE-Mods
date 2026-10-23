using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace DependencyBootstrapProbe
{
    internal static class LifecycleHook
    {
        private static ILHook hook;
        internal static void Install(Assembly extender)
        {
            try
            {
                if (hook != null) return;
                if (ProbeIdentity.Hash(extender.Location) != HookRuntime.ExtenderHash) throw new InvalidOperationException("Unknown lifecycle binary");
                var type = extender.GetType("SHCDESE.API.Components.Archive.MapModManager", true);
                var method = type.GetMethod("TryUpdateModsFromRemote", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null || method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                    throw new InvalidOperationException("Lifecycle signature changed");
                ProbeTrace.Mark("LIFECYCLE_PREPARE", "binary=" + ProbeIdentity.Hash(extender.Location));
                using (var copy = new DynamicMethodDefinition(method))
                using (var il = new ILContext(copy.Definition))
                {
                    Transform(il);
                    ProbeTrace.Mark("LIFECYCLE_TRANSFORMED", "validated original calls and boundaries");
                    ProbeTrace.Mark("LIFECYCLE_DELEGATE_BEGIN", "compile only");
                    var compiled = copy.Generate().CreateDelegate(typeof(Action<>).MakeGenericType(type));
                    ProbeTrace.Mark("LIFECYCLE_DELEGATE_END", "never invoked");
                    ProbeTrace.Mark("LIFECYCLE_JIT_BEGIN", "prepare only");
                    RuntimeHelpers.PrepareDelegate(compiled);
                    ProbeTrace.Mark("LIFECYCLE_JIT_END", "prepared");
                    GC.KeepAlive(compiled);
                }
                ProbeTrace.Mark("LIFECYCLE_INSTALL", "publishing permanent observer");
                hook = new ILHook(method, Transform);
                ProbeTrace.Mark("LIFECYCLE_HOOK_INSTALLED", "no additional workshop query");
            }
            catch (Exception ex) { ProbeTrace.Mark("ERROR", "Lifecycle preparation " + ex); }
        }
        private static Instruction Single(ILContext il, string type, string name)
        {
            var hits = il.Body.Instructions.Where(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                i.Operand is MethodReference m && m.DeclaringType.FullName == type && m.Name == name).ToArray();
            if (hits.Length != 1) throw new InvalidOperationException("Lifecycle call mismatch: " + name);
            return hits[0];
        }
        private static void Boundary(ILContext il, Instruction target)
        {
            if (target == null || il.Labels.Any(l => l.Target == target) ||
                il.Body.Instructions.Any(i => i.Operand == target || (i.Operand is Instruction[] a && a.Contains(target))) ||
                il.Body.ExceptionHandlers.Any(h => h.TryStart == target || h.TryEnd == target || h.HandlerStart == target || h.HandlerEnd == target || h.FilterStart == target))
                throw new InvalidOperationException("Lifecycle insertion boundary changed");
        }
        internal static void Transform(ILContext il)
        {
            const string manager = "SHCDESE.API.Components.Archive.MapModManager";
            var paths = Single(il, "Platform_Workshop", "GetListOfSubscribedItemsPaths");
            var map = Single(il, manager, "GetFirstMapFileInDirectory");
            var load = Single(il, "SHCDESE.API.Components.Archive.MapArchive", "TryLoad");
            var reset = Single(il, manager, "ResetDeployAttempts");
            var attempts = Single(il, manager, "ReadDeployAttempts");
            var uninstall = Single(il, manager, "DetectAndStageUninstalls");
            var stage = Single(il, manager, "TryStageModUpdate");
            var prompt = Single(il, "SHCDESE.Interop.MinWinAPI", "MessageBoxA");
            var archive = load.Previous?.Operand as VariableDefinition;
            var mapPath = load.Previous?.Previous?.Operand as VariableDefinition;
            var folder = map.Previous?.Operand as VariableDefinition;
            var body = il.Body.Instructions;
            if (((MethodReference)paths.Operand).ReturnType.FullName != "System.Collections.Generic.List`1<System.String>" ||
                ((MethodReference)paths.Operand).Parameters.Count != 0 ||
                ((MethodReference)map.Operand).ReturnType.FullName != "System.String" ||
                ((MethodReference)map.Operand).Parameters.Count != 1 ||
                ((MethodReference)reset.Operand).ReturnType.FullName != "System.Void" ||
                ((MethodReference)reset.Operand).Parameters.Count != 0 ||
                ((MethodReference)attempts.Operand).ReturnType.FullName != "System.Int32" ||
                ((MethodReference)attempts.Operand).Parameters.Count != 0 ||
                ((MethodReference)load.Operand).Parameters.Count != 2 ||
                ((MethodReference)load.Operand).ReturnType.FullName != "System.Boolean" ||
                load.Previous.OpCode != OpCodes.Ldloca_S || archive?.VariableType.FullName != "SHCDESE.API.Components.Archive.MapArchive" ||
                mapPath?.VariableType.FullName != "System.String" || folder?.VariableType.FullName != "System.String" ||
                load.Previous.Previous.OpCode != OpCodes.Ldloc_S || map.Previous.OpCode != OpCodes.Ldloc_S ||
                body.IndexOf(paths) >= body.IndexOf(uninstall) || body.IndexOf(uninstall) >= body.IndexOf(load) ||
                body.IndexOf(load) >= body.IndexOf(stage) || body.IndexOf(stage) >= body.IndexOf(reset) ||
                body.IndexOf(reset) >= body.IndexOf(attempts) || body.IndexOf(attempts) >= body.IndexOf(prompt))
                throw new InvalidOperationException("Lifecycle data flow changed");
            foreach (var target in new[] { paths.Next, map.Next, load.Next, reset, attempts }) Boundary(il, target);
            var cursor = new ILCursor(il);
            cursor.Goto(paths, MoveType.After);
            cursor.Emit(OpCodes.Call, typeof(LocalUpdateTrial).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic));
            cursor.Emit(OpCodes.Dup); Emit(cursor, "Paths");
            cursor.Goto(map, MoveType.After); cursor.Emit(OpCodes.Dup); cursor.Emit(OpCodes.Ldloc, folder); Emit(cursor, "MapPath");
            cursor.Goto(load, MoveType.After); cursor.Emit(OpCodes.Dup); cursor.Emit(OpCodes.Ldloc, archive); cursor.Emit(OpCodes.Ldloc, mapPath); Emit(cursor, "Archive");
            foreach (var target in new[] { reset, attempts }) { cursor.Goto(target, MoveType.Before); Emit(cursor, "Staged"); }
        }
        private static void Emit(ILCursor cursor, string name) => cursor.Emit(OpCodes.Call,
            typeof(LifecycleCapture).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }
}
