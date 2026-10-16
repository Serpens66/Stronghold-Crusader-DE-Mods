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

namespace SavegameMissingMapFixTest
{
    using Restart = HUD_IngameMenu.RestartSkirmishMapInfo;

    internal static class SavegameFix
    {
        internal static volatile bool Enabled;
        internal static Action<string> Log = _ => { };
        private static readonly ConditionalWeakTable<Restart, MapIdentity> identities = new ConditionalWeakTable<Restart, MapIdentity>();
        internal static readonly Type RestartType = typeof(Restart);
        internal static readonly MethodInfo Decode = RestartType.GetMethod("decode", new[] { typeof(byte[]), typeof(bool) });
        internal static readonly MethodInfo Encode = RestartType.GetMethod("encode", Type.EmptyTypes);
        internal static readonly MethodInfo Menu = typeof(HUD_IngameMenu).GetMethod("SetAsNormal", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static readonly MethodInfo Action = typeof(HUD_IngameMenu).GetMethod("ButtonIngameMenuFunction", new[] { typeof(int) });
        private static readonly FieldInfo RestartButton = typeof(HUD_IngameMenu).GetField("RefButtonRestart", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ValidateMembers()
        {
            if (Decode == null || Encode == null || Menu == null || Action == null || RestartButton == null || RestartButton.FieldType != typeof(Noesis.Button))
                throw new InvalidOperationException("Installed game member contract changed.");
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
            if (!Enabled) return null;
            Remember(value, bytes);
            Log("RECOVERED map=" + identities.GetValue(value, _ => new MapIdentity(bytes)).SerializationHeader.fileName +
                "; slots=" + (value.aivs == null ? 0 : value.aivs.Length) + "; originalMapAvailable=false");
            return value;
        }

        internal static Restart Remember(Restart value, byte[] bytes)
        {
            if (Enabled && value != null && value.selectedHeader == null)
                identities.GetValue(value, _ => new MapIdentity(bytes));
            return value;
        }

        internal static FileHeader HeaderForEncoding(Restart value)
        {
            if (value.selectedHeader != null) return value.selectedHeader;
            if (Enabled && identities.TryGetValue(value, out var identity)) return identity.SerializationHeader;
            return null;
        }

        internal static FileHeader TrailLookup(MapFileManager manager, string trail, string file)
        {
            try { return manager.GetHeaderFromCustomTrail(trail, file); }
            catch (KeyNotFoundException) { if (!Enabled) throw; return null; }
        }

        internal static bool Missing(Restart value)
        {
            return Enabled && value != null && value.selectedHeader == null && identities.TryGetValue(value, out _);
        }

        internal static void UpdateMenu(HUD_IngameMenu menu)
        {
            if (Missing(menu.restartSkirmishMapInfo) && RestartButton.GetValue(menu) is Noesis.Button button) button.IsEnabled = false;
        }

        internal static bool BlockAction(HUD_IngameMenu menu, int button)
        {
            if (button != 5 || !Missing(menu.restartSkirmishMapInfo)) return false;
            Log("RESTART_BLOCKED: original map is not installed; current save remains active.");
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

        internal static void ModifyAction(ILContext il)
        {
            bool instance = il.Method.HasThis && il.Method.Parameters.Count == 1;
            bool copiedInstance = !il.Method.HasThis && il.Method.Parameters.Count == 2 &&
                il.Method.Parameters[0].ParameterType.FullName == "CrusaderDE.HUD_IngameMenu";
            if ((!instance && !copiedInstance) || il.Method.Parameters.Last().ParameterType.FullName != "System.Int32")
                throw new InvalidOperationException("Menu action contract changed.");
            var first = il.Body.Instructions[0];
            var c = new ILCursor(il);
            c.Emit(OpCodes.Ldarg_0); c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<HUD_IngameMenu, int, bool>>(BlockAction);
            c.Emit(OpCodes.Brfalse, first); c.Emit(OpCodes.Ret);
        }
    }
}
