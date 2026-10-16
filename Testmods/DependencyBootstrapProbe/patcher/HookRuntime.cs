using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace DependencyBootstrapProbe
{
    // Shadow stage: no original inventory edits, asset registration or compatibility overrides.
    internal static class HookRuntime
    {
        private static ILHook chainHook;
        private static ILHook compatibilityHook;
        private static bool snapshotSeen;
        private static bool extenderSeen;
        private static int checks;
        public const string CoreHash = "8255B28902886085C578B9E427D3073C97002DB85176D2090CDEDA90EF14CE70";
        public const string ExtenderHash = "E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3";

        // Avoid resolving/JITing Chainloader before the preloader invokes Finish.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install()
        {
            if (chainHook != null) return;
            Mark("CHAIN_PREPARE", "validating loaded assembly and Start");
            Mark("SHADOW_REQUIRED", "stage=2; only a separate index copy may change");
            var assembly = typeof(BepInEx.Bootstrap.Chainloader).Assembly;
            Mark("CORE_BINARY", "sha256=" + ProbeIdentity.Hash(assembly.Location));
            if (!HashMatches(assembly.Location, CoreHash)) { Mark("SKIP", "unknown BepInEx binary"); return; }
            var type = typeof(BepInEx.Bootstrap.Chainloader);
            var loaded = type.GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
            if (loaded == null || (bool)loaded.GetValue(null)) { Mark("SKIP", "Chainloader already ran or contract changed"); return; }
            var method = type.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            using (var copy = new DynamicMethodDefinition(method))
            using (var context = new ILContext(copy.Definition))
            {
                ModifyChainloader(context);
                Mark("CHAIN_TRANSFORMED", "copy validated and modified; compiling");
                Mark("CHAIN_DELEGATE_BEGIN", "generating delegate");
                var compiled = copy.Generate().CreateDelegate(typeof(Action));
                Mark("CHAIN_DELEGATE_END", "delegate created");
                Mark("CHAIN_JIT_BEGIN", "preparing delegate");
                RuntimeHelpers.PrepareDelegate(compiled);
                Mark("CHAIN_JIT_END", "delegate prepared");
                GC.KeepAlive(compiled);
            }
            Mark("CHAIN_COPY_COMPILED", "transformed copy prepared, never invoked");
            Mark("CHAIN_INSTALL", "publishing permanent observer");
            chainHook = new ILHook(method, ModifyChainloader);
            Mark("CHAIN_HOOK_INSTALLED", "permanent; original control flow retained");
        }

        private static bool Calls(Instruction instruction, string type, string name)
        {
            if (instruction == null) return false;
            var method = instruction.Operand as MethodReference;
            return (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                method != null && method.DeclaringType.FullName == type && method.Name == name;
        }

        internal static void ModifyChainloader(ILContext il)
        {
            // Validate every insertion site before editing. No offsets are used as match criteria.
            var instructions = il.Body.Instructions;
            var sorts = instructions.Where(i => Calls(i, "BepInEx.Utility", "TopologicalSort")).ToArray();
            var adds = instructions.Where(i => Calls(i, "UnityEngine.GameObject", "AddComponent")).ToArray();
            var closure = FindWinnerClosure(il);
            var failed = il.Body.Variables.Where(v => v.VariableType.FullName == "System.Collections.Generic.HashSet`1<System.String>").ToArray();
            if (failed.Length != 1) throw new InvalidOperationException("Missing or ambiguous loader failure set");
            if (sorts.Length != 1 || adds.Length != 1 ||
                !Calls(sorts[0].Next, "System.Linq.Enumerable", "ToList") ||
                !Calls(adds[0].Previous, "System.Reflection.Assembly", "GetType"))
                throw new InvalidOperationException("Chainloader insertion contract mismatch");
            var addMethod = (MethodReference)adds[0].Operand;
            if (addMethod.Parameters.Count != 1 || addMethod.Parameters[0].ParameterType.FullName != "System.Type")
                throw new InvalidOperationException("AddComponent signature mismatch");
            RejectBoundary(il, adds[0]);
            RejectBoundary(il, sorts[0].Next.Next);
            var cursor = new ILCursor(il);
            cursor.Goto(adds[0], MoveType.Before);
            cursor.MoveAfterLabels();
            cursor.Emit(OpCodes.Dup); // PluginInfo, GameObject, Type -> same stack after callback.
            cursor.Emit(OpCodes.Ldloc, failed[0]);
            cursor.Emit(OpCodes.Call, Callback(nameof(BeforePlugin)));
            cursor.Goto(sorts[0].Next, MoveType.After);
            cursor.Emit(OpCodes.Ldloc, closure); // The sorted list stays untouched below this object.
            cursor.Emit(OpCodes.Call, Callback(nameof(Snapshot)));
            cursor.Goto(0);
            cursor.Emit(OpCodes.Call, Callback(nameof(OnChainEnter)));
        }

        private static MethodInfo Callback(string name) => typeof(HookRuntime)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        private static void OnChainEnter() => Mark("CHAIN_ENTER", "Start callback");
        private static void OnCompatibilityEnter() { checks++; Mark("COMPATIBILITY_ENTER", "count=" + checks); }

        private static VariableDefinition FindWinnerClosure(ILContext il)
        {
            // DMD imports type references; Resolve() would invoke an unrelated Cecil resolver.
            var fields = il.Body.Instructions.Select(i => i.Operand as FieldReference)
                .Where(f => f != null && f.Name == "pluginsByGUID").ToArray();
            if (fields.Length == 0 || fields.Any(f => f.FullName != fields[0].FullName))
                throw new InvalidOperationException("Missing or ambiguous winner field");
            var field = fields[0];
            var loadedAssembly = typeof(BepInEx.Bootstrap.Chainloader).Assembly;
            if (!field.DeclaringType.FullName.StartsWith("BepInEx.Bootstrap.Chainloader/", StringComparison.Ordinal) ||
                !(field.DeclaringType.Scope is AssemblyNameReference scope) || scope.FullName != loadedAssembly.FullName)
                throw new InvalidOperationException("Winner field assembly/type mismatch");
            var type = loadedAssembly.GetType(field.DeclaringType.FullName.Replace('/', '+'), false);
            var reflected = type?.GetField("pluginsByGUID", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var dictionary = field.FieldType as GenericInstanceType;
            if (reflected == null || reflected.IsStatic || reflected.FieldType != typeof(Dictionary<string, BepInEx.PluginInfo>) ||
                dictionary == null || dictionary.ElementType.FullName != "System.Collections.Generic.Dictionary`2" ||
                dictionary.GenericArguments.Count != 2 || dictionary.GenericArguments[0].FullName != "System.String" ||
                dictionary.GenericArguments[1].FullName != "BepInEx.PluginInfo" ||
                dictionary.GenericArguments[1].Scope.ToString() != loadedAssembly.FullName)
                throw new InvalidOperationException("Winner field signature mismatch");
            var locals = il.Body.Variables.Where(v => v.VariableType.FullName == field.DeclaringType.FullName &&
                v.VariableType.Scope.ToString() == field.DeclaringType.Scope.ToString()).ToArray();
            if (locals.Length != 1) throw new InvalidOperationException("Missing or ambiguous winner local");
            return locals[0];
        }

        private static void RejectBoundary(ILContext il, Instruction target)
        {
            if (target == null || il.Body.Instructions.Any(i => i.Operand == target ||
                (i.Operand is Instruction[] targets && targets.Contains(target))) ||
                il.Labels.Any(label => label.Target == target) ||
                il.Body.ExceptionHandlers.Any(h => h.TryStart == target || h.TryEnd == target ||
                    h.HandlerStart == target || h.HandlerEnd == target || h.FilterStart == target))
                throw new InvalidOperationException("Unexpected insertion control-flow boundary");
        }

        private static void Snapshot(object closure)
        {
            try
            {
                var field = closure.GetType().GetField("pluginsByGUID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var winners = (IDictionary)field.GetValue(closure);
                ShadowRuntime.Winners = new List<ShadowPlugin>();
                foreach (DictionaryEntry entry in winners) ShadowRuntime.Winners.Add(ShadowRuntime.FromInfo((BepInEx.PluginInfo)entry.Value));
                snapshotSeen = true;
                Mark("SNAPSHOT", "selected=" + winners.Count + "; selection is not an Awake success guarantee");
                foreach (DictionaryEntry entry in winners)
                {
                    var info = entry.Value;
                    var metadata = info.GetType().GetProperty("Metadata").GetValue(info, null);
                    Mark("WINNER", "guid=" + entry.Key + " version=" + metadata.GetType().GetProperty("Version").GetValue(metadata, null) +
                        " path=" + info.GetType().GetProperty("Location").GetValue(info, null));
                }
            }
            catch (Exception ex) { Mark("ERROR", "Snapshot " + ex); }
        }

        private static void BeforePlugin(Type type, object failed)
        {
            try
            {
                if (type == null || type.Assembly.GetName().Name != "SHCDESE" || extenderSeen || compatibilityHook != null) return;
                ShadowRuntime.Failed = new HashSet<string>((HashSet<string>)failed, StringComparer.Ordinal);
                extenderSeen = true;
                Mark("BEFORE_EXTENDER", "snapshot=" + snapshotSeen + " type=" + type.FullName);
                Mark("SE_BINARY", "sha256=" + ProbeIdentity.Hash(type.Assembly.Location));
                if (!snapshotSeen || !HashMatches(type.Assembly.Location, ExtenderHash)) { Mark("SKIP", "SE binary or snapshot mismatch"); return; }
                var manager = type.Assembly.GetType("SHCDESE.API.Components.ModManager.GameAssetModManager", true);
                var method = manager.GetMethod("PrepareAllAndCheckCompatibility", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(string) }, null);
                if (method == null || method.IsStatic || !method.ReturnType.IsGenericType ||
                    method.ReturnType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>) ||
                    method.ReturnType.GetGenericArguments()[0] != type.Assembly.GetType("SHCDESE.API.Components.ModManager.ModCompatibilityIssue", true))
                    throw new InvalidOperationException("SE signature mismatch");
                Mark("SE_PREPARE", "validated signature; preparing copy");
                using (var copy = new DynamicMethodDefinition(method))
                using (var context = new ILContext(copy.Definition))
                {
                    ModifyCompatibility(context);
                    Mark("SE_TRANSFORMED", "copy modified; compiling");
                    var delegateType = typeof(Func<,,>).MakeGenericType(manager, typeof(string), method.ReturnType);
                    Mark("SE_DELEGATE_BEGIN", "generating delegate");
                    var compiled = copy.Generate().CreateDelegate(delegateType);
                    Mark("SE_DELEGATE_END", "delegate created");
                    Mark("SE_JIT_BEGIN", "preparing delegate");
                    RuntimeHelpers.PrepareDelegate(compiled);
                    Mark("SE_JIT_END", "delegate prepared");
                    GC.KeepAlive(compiled);
                }
                Mark("SE_COPY_COMPILED", "transformed copy prepared, never invoked");
                Mark("SE_INSTALL", "publishing permanent observer");
                compatibilityHook = new ILHook(method, ModifyCompatibility);
                Mark("SE_HOOK_INSTALLED", "before AddComponent; original check unchanged");
            }
            catch (Exception ex) { Mark("ERROR", "BeforePlugin " + ex); }
        }

        internal static void ModifyCompatibility(ILContext il)
        {
            // At the unique issues-list construction the complete local index exists, before any checks.
            var sites = il.Body.Instructions.Where(i => i.OpCode == OpCodes.Newobj && i.Operand is MethodReference m &&
                m.DeclaringType.FullName == "System.Collections.Generic.List`1<SHCDESE.API.Components.ModManager.ModCompatibilityIssue>").ToArray();
            var indices = il.Body.Variables.Where(v => v.VariableType.FullName ==
                "System.Collections.Generic.Dictionary`2<System.String,SHCDESE.API.Components.ModManager.GameAssetModManager/InstalledModVersion>").ToArray();
            if (sites.Length != 1 || indices.Length != 1) throw new InvalidOperationException("SE shadow index contract mismatch");
            // A branch to the list construction is expected; insert after its store instead, at a boundary with no incoming edges.
            var store = sites[0].Next;
            if (store == null || !store.OpCode.Name.StartsWith("stloc", StringComparison.Ordinal)) throw new InvalidOperationException("SE issue-list store changed");
            RejectBoundary(il, store.Next);
            var cursor = new ILCursor(il);
            cursor.Goto(store, MoveType.After);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, indices[0]);
            cursor.Emit(OpCodes.Call, typeof(ShadowRuntime).GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic));
            cursor.Goto(0);
            cursor.Emit(OpCodes.Call, Callback(nameof(OnCompatibilityEnter)));
        }

        private static bool HashMatches(string path, string expected)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == expected;
        }

        internal static string GetStatus() => "snapshot=" + snapshotSeen + " extender=" + extenderSeen + " checks=" + checks;
        private static void Mark(string marker, string detail) => ProbeTrace.Mark(marker, detail);
    }
}
