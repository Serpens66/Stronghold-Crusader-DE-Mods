using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace BugfixesAndQoL.SavegameMissingMap
{
    using Restart = HUD_IngameMenu.RestartSkirmishMapInfo;

    internal static class SavegameFix
    {
        internal static volatile bool Enabled;
        internal static Func<bool> RecoveryEnabled = () => true;
        internal static Action<string> Log = _ => { };
        internal static Action<MethodInfo> HookDiagnostic = _ => { };
        internal static Action<string, int> RecoveryDiagnostic = (_, __) => { };
        internal static Action RestartDiagnostic = () => { };
        private static readonly ConditionalWeakTable<Restart, MapIdentity> identities = new ConditionalWeakTable<Restart, MapIdentity>();
        internal static readonly Type RestartType = typeof(Restart);
        internal static readonly MethodInfo Decode = RestartType.GetMethod("decode", new[] { typeof(byte[]), typeof(bool) });
        internal static readonly MethodInfo Encode = RestartType.GetMethod("encode", Type.EmptyTypes);
        internal static readonly MethodInfo Menu = typeof(HUD_IngameMenu).GetMethod("SetAsNormal", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static readonly MethodInfo MakerSelection = typeof(FRONT_ManageTrail).GetMethod("SelectMission", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static MethodInfo RestartConfirmation { get; private set; }
        internal static MethodInfo[] HookTargets => new[] { Decode, Encode, Menu, RestartConfirmation, MakerSelection };
        internal static ILContext.Manipulator[] HookTransforms => new ILContext.Manipulator[] { ModifyDecode, ModifyEncode, ModifyMenu, ModifyRestartConfirmation, ModifyMakerSelection };
        private static readonly FieldInfo RestartButton = typeof(HUD_IngameMenu).GetField("RefButtonRestart", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ValidateMembers()
        {
            if (Decode == null || Encode == null || Menu == null || MakerSelection == null || RestartButton == null || RestartButton.FieldType != typeof(Noesis.Button))
                throw new InvalidOperationException("Installed game member contract changed.");
            RestartConfirmation = FindRestartConfirmation();
        }

        private static MethodInfo FindRestartConfirmation()
        {
            var outer = typeof(HUD_IngameMenu).GetMethod("ButtonIngameMenuFunction", new[] { typeof(int) });
            if (outer == null) throw new InvalidOperationException("Menu delegate owner missing.");
            var candidates = new List<MethodInfo>();
            // Read only: hooking this owner re-executes the Extender's non-idempotent exit-hook installer.
            using (var copy = new DynamicMethodDefinition(outer))
                foreach (var i in copy.Definition.Body.Instructions)
                {
                    if (i.OpCode != OpCodes.Ldftn || i.Previous?.OpCode != OpCodes.Ldarg_0 ||
                        i.Next?.OpCode != OpCodes.Newobj || !(i.Next.Operand is MethodReference ctor) ||
                        ctor.DeclaringType.FullName != "System.Action") continue;
                    var method = ((MethodReference)i.Operand).ResolveReflection() as MethodInfo;
                    if (method == null || method.DeclaringType != typeof(HUD_IngameMenu) || !method.IsPrivate ||
                        method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length != 0) continue;
                    using (var callback = new DynamicMethodDefinition(method))
                        if (callback.Definition.Body.Instructions.Count(IsSkirmishRestartCall) == 1) candidates.Add(method);
                }
            if (candidates.Count != 1) throw new InvalidOperationException("Restart confirmation delegate contract changed.");
            return candidates[0];
        }

        private static bool IsSkirmishRestartCall(Instruction i) => i.OpCode == OpCodes.Callvirt &&
            i.Operand is MethodReference m && m.DeclaringType.FullName == "CrusaderDE.FRONT_Multiplayer" &&
            m.Name == "RestartSkirmishGame" && m.ReturnType.FullName == "System.Void" && m.Parameters.Count == 1 &&
            m.Parameters[0].ParameterType.FullName == "CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo";

        internal static void RunHookPlan(MethodInfo[] methods, ILContext.Manipulator[] transforms,
            Action beforePublish, Action<MethodInfo, ILContext.Manipulator> publish)
        {
            if (methods.Length != transforms.Length) throw new InvalidOperationException("Hook plan length mismatch.");
            for (int i = 0; i < methods.Length; i++)
            {
                try
                {
                    using (var copy = new DynamicMethodDefinition(methods[i]))
                    using (var il = new ILContext(copy.Definition)) il.Invoke(transforms[i]);
                }
                catch (Exception ex) { throw HookFailure("preflight", methods[i], ex); }
            }
            beforePublish();
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                var transform = transforms[i];
                try
                {
                    publish(method, il =>
                    {
                        try { transform(il); }
                        catch (Exception ex) { throw HookFailure("runtime-manipulator", method, ex); }
                    });
                }
                catch (Exception ex) { throw HookFailure("installation", method, ex); }
            }
        }

        private static Exception HookFailure(string phase, MethodInfo method, Exception error) =>
            new InvalidOperationException("Hook phase=" + phase + "; target=" + method.DeclaringType.FullName + "::" + method.Name + "; " + error.Message, error);

        internal static Instruction ResolveBranchTarget(ILContext il, object operand)
        {
            var target = operand is ILLabel label ? label.Target : operand as Instruction;
            if (target == null || !il.Body.Instructions.Contains(target))
                throw new InvalidOperationException("Restart confirmation branch target is missing, unsupported or outside the method body.");
            return target;
        }

        private sealed class MapIdentity
        {
            internal readonly FileHeader SerializationHeader;
            internal MapIdentity(byte[] bytes)
            {
                using (var input = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    int version = input.ReadByte();
                    if (version < 53 || version > 60) throw new InvalidDataException("Unsupported restart metadata version.");
                    int players = input.ReadInt32();
                    if (players < 0 || players > 8) throw new InvalidDataException("Invalid player count.");
                    input.BaseStream.Position = checked(5 + players * 12);
                    byte origin = input.ReadByte();
                    int length = input.ReadInt32();
                    if (origin > 2 || length < 0 || length > input.BaseStream.Length - input.BaseStream.Position)
                        throw new InvalidDataException("Invalid map identity.");
                    string name = new UTF8Encoding(false, true).GetString(input.ReadBytes(length));
                    // This header is ONLY used by encode(), never installed as a loadable map.
                    SerializationHeader = new FileHeader { fileName = name, workshopMap = origin == 2, builtinMap = origin == 1 };
                }
            }
        }

        internal static Restart Recover(Restart value, byte[] bytes)
        {
            if (!Enabled || !RecoveryEnabled()) return null;
            var identity = identities.GetValue(value, _ => new MapIdentity(bytes));
            RuntimeStartup.Report<Action>(report => report(), () => RecoveryDiagnostic(identity.SerializationHeader.fileName, value.aivs == null ? 0 : value.aivs.Length));
            return value;
        }

        internal static Restart Remember(Restart value, byte[] bytes)
        {
            if (Enabled && RecoveryEnabled() && value != null && value.selectedHeader == null)
                identities.GetValue(value, _ => new MapIdentity(bytes));
            return value;
        }

        internal static FileHeader HeaderForEncoding(Restart value)
        {
            if (value.selectedHeader != null) return value.selectedHeader;
            if (Enabled && identities.TryGetValue(value, out var identity)) return identity.SerializationHeader;
            return null;
        }

        // Recovery deliberately leaves selectedHeader null. Presentation may use the retained
        // identity, but must never turn that serialization-only header into a loadable map.
        internal static string MakerMapLabel(Restart value)
        {
            if (value.selectedHeader != null) return value.selectedHeader.display_filename;
            if (Enabled && identities.TryGetValue(value, out var identity)) return identity.SerializationHeader.fileName;
            if (Enabled) return string.Empty;
            return value.selectedHeader.display_filename;
        }

        internal static void ModifyMakerSelection(ILContext il)
        {
            var targets = il.Body.Instructions.Where(i => Field(i, "selectedHeader") &&
                i.Next?.OpCode == OpCodes.Ldfld && i.Next.Operand is FieldReference f &&
                f.DeclaringType.FullName == "FileHeader" && f.Name == "display_filename").ToArray();
            if (targets.Length != 1)
                throw new InvalidOperationException("Trail Maker map-label IL contract changed.");
            var target = targets[0];
            if (il.Body.Instructions.Any(i => ReferenceEquals(i.Operand, target.Next) ||
                i.Operand is ILLabel label && label.Target == target.Next ||
                i.Operand is Instruction[] branches && branches.Contains(target.Next)) ||
                il.Body.ExceptionHandlers.Any(handler => handler.TryStart == target.Next || handler.TryEnd == target.Next ||
                    handler.HandlerStart == target.Next || handler.HandlerEnd == target.Next || handler.FilterStart == target.Next))
                throw new InvalidOperationException("Trail Maker map-label access has an unexpected control-flow entry.");
            target.OpCode = OpCodes.Call;
            target.Operand = il.Import(typeof(SavegameFix).GetMethod(nameof(MakerMapLabel), BindingFlags.Static | BindingFlags.NonPublic));
            target.Next.OpCode = OpCodes.Nop;
            target.Next.Operand = null;
        }

        internal static FileHeader TrailLookup(MapFileManager manager, string trail, string file)
        {
            try { return manager.GetHeaderFromCustomTrail(trail, file); }
            catch (KeyNotFoundException) { if (!Enabled || !RecoveryEnabled()) throw; return null; }
        }

        internal static bool Missing(Restart value)
        {
            return Enabled && value != null && value.selectedHeader == null && identities.TryGetValue(value, out _);
        }

        internal static void UpdateMenu(HUD_IngameMenu menu)
        {
            if (Missing(menu.restartSkirmishMapInfo) && RestartButton.GetValue(menu) is Noesis.Button button) button.IsEnabled = false;
        }

        internal static bool BlockRestart(HUD_IngameMenu menu)
        {
            if (!Missing(menu.restartSkirmishMapInfo)) return false;
            RuntimeStartup.Report<Action>(report => report(), RestartDiagnostic);
            return true;
        }

        private static bool Field(Instruction i, string name) => i.OpCode == OpCodes.Ldfld &&
            i.Operand is FieldReference f && f.Name == name && f.DeclaringType.FullName == "CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo";

        internal static void ModifyDecode(ILContext il)
        {
            var ins = il.Body.Instructions;
            var rejection = ins.Where(i => i.OpCode == OpCodes.Ldnull && i.Next != null &&
                i.Next.OpCode == OpCodes.Stloc_S && i.Next.Next.OpCode == OpCodes.Leave_S &&
                il.Body.ExceptionHandlers.All(e => e.HandlerStart != i.Previous)).ToArray();
            var headers = ins.Where(i => Field(i, "selectedHeader")).ToArray();
            var success = ins.Where(i => i.OpCode == OpCodes.Ldloc_0 && i.Next?.OpCode == OpCodes.Stloc_S && i.Next.Next?.OpCode == OpCodes.Leave_S).ToArray();
            var trail = ins.Where(i => i.Operand is MethodReference m && m.Name == "GetHeaderFromCustomTrail").ToArray();
            if (rejection.Length != 2 || headers.Length != 2 || success.Length != 1 || trail.Length != 1 ||
                il.Body.Variables[0].VariableType.FullName != "CrusaderDE.HUD_IngameMenu/RestartSkirmishMapInfo")
                throw new InvalidOperationException("Decoder map-rejection IL contract changed.");
            // No exception/corruption return is changed. Existing branch destinations stay attached.
            foreach (var target in rejection)
            {
                target.OpCode = OpCodes.Ldloc_0;
                var c = new ILCursor(il); c.Goto(target, MoveType.After);
                c.Emit(OpCodes.Ldarg_0); c.EmitDelegate<Func<Restart, byte[], Restart>>(Recover);
            }
            var cursor = new ILCursor(il); cursor.Goto(success[0], MoveType.After);
            cursor.Emit(OpCodes.Ldarg_0); cursor.EmitDelegate<Func<Restart, byte[], Restart>>(Remember);
            trail[0].OpCode = OpCodes.Call;
            trail[0].Operand = il.Import(typeof(SavegameFix).GetMethod(nameof(TrailLookup), BindingFlags.Static | BindingFlags.NonPublic));
        }

        internal static void ModifyEncode(ILContext il)
        {
            var targets = il.Body.Instructions.Where(i => Field(i, "selectedHeader")).ToArray();
            string[] fields = { "workshopMap", "builtinMap", "fileName" };
            if (targets.Length != 3 || targets.Where((i, n) => !(i.Next.Operand is FieldReference f) || f.Name != fields[n]).Any())
                throw new InvalidOperationException("Encoder map-identity IL contract changed.");
            foreach (var target in targets)
            {
                target.OpCode = OpCodes.Call;
                target.Operand = il.Import(typeof(SavegameFix).GetMethod(nameof(HeaderForEncoding), BindingFlags.Static | BindingFlags.NonPublic));
            }
        }

        internal static void ModifyMenu(ILContext il)
        {
            var exits = il.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            if (exits.Length != 1 || !il.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "RefButtonRestart"))
                throw new InvalidOperationException("Normal-menu IL contract changed.");
            var target = exits[0]; target.OpCode = OpCodes.Ldarg_0;
            var c = new ILCursor(il); c.Goto(target, MoveType.After);
            c.EmitDelegate<Action<HUD_IngameMenu>>(UpdateMenu); c.Emit(OpCodes.Ret);
        }

        internal static void ModifyRestartConfirmation(ILContext il)
        {
            bool instance = il.Method.HasThis && il.Method.Parameters.Count == 0;
            bool copiedInstance = !il.Method.HasThis && il.Method.Parameters.Count == 1 &&
                il.Method.Parameters[0].ParameterType.FullName == "CrusaderDE.HUD_IngameMenu";
            var branches = il.Body.Instructions.Where(i => (i.OpCode == OpCodes.Blt_S || i.OpCode == OpCodes.Blt) &&
                i.Previous?.OpCode == OpCodes.Ldc_I4_0 && i.Previous.Previous?.Operand is MethodReference m &&
                m.DeclaringType.FullName == "GameData" && m.Name == "get_SkirmishTrailType").ToArray();
            if (branches.Length != 1) throw new InvalidOperationException("Restart confirmation requires exactly one signed SkirmishTrailType < 0 branch; found " + branches.Length);
            var target = ResolveBranchTarget(il, branches[0].Operand);
            var exit = il.Body.Instructions.Last();
            if ((!instance && !copiedInstance) || il.Method.ReturnType.FullName != "System.Void" ||
                il.Body.Instructions.Count(IsSkirmishRestartCall) != 1 || target?.OpCode != OpCodes.Ldarg_0 ||
                target.Next?.OpCode != OpCodes.Ldfld || !(target.Next.Operand is FieldReference field) ||
                field.DeclaringType.FullName != "CrusaderDE.HUD_IngameMenu" || field.Name != "restartSkirmishMapInfo" ||
                exit.OpCode != OpCodes.Ret)
                throw new InvalidOperationException("Restart confirmation contract requires void instance callback, one RestartSkirmishGame call, HUD restartSkirmishMapInfo load at branch target and terminal ret.");
            // Keep the branch destination intact. Campaign and built-in trails never enter this block.
            var c = new ILCursor(il); c.Goto(target, MoveType.After);
            c.EmitDelegate<Func<HUD_IngameMenu, bool>>(BlockRestart);
            var exitLabel = c.DefineLabel();
            exitLabel.Target = exit;
            c.Emit(OpCodes.Brtrue, exitLabel);
            c.Emit(OpCodes.Ldarg_0);
        }
    }
}
