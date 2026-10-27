using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace BetterPathfindingTest
{
    internal sealed class EngineGate
    {
        private readonly object gate;
        internal EngineGate()
        {
            Type engine = typeof(EngineInterface);
            FieldInfo field = engine.GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo run = engine.GetMethod("run", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            if (field == null || field.FieldType != typeof(object) || run == null) throw new NotSupportedException("Engine lock signature changed.");
            byte[] il = run.GetMethodBody()?.GetILAsByteArray();
            if (il == null) throw new NotSupportedException("Engine run has no IL.");
            var single = new OpCode[256]; var extended = new OpCode[256];
            foreach (FieldInfo opcode in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (opcode.FieldType == typeof(OpCode)) { OpCode op = (OpCode)opcode.GetValue(null); if (op.Size == 1) single[(byte)op.Value] = op; else extended[(byte)op.Value] = op; }
            int lockLoad = -1, enter = -1, native = -1, exit = -1, enters = 0, exits = 0, calls = 0;
            for (int offset = 0; offset < il.Length;)
            {
                int start = offset; byte first = il[offset++]; OpCode op = first == 0xFE ? extended[il[offset++]] : single[first];
                if (op.Size == 0) throw new NotSupportedException("Invalid engine IL.");
                int size;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = checked(4 + BitConverter.ToInt32(il, offset) * 4); break;
                    default: size = 4; break;
                }
                if (size < 0 || offset + size > il.Length) throw new NotSupportedException("Truncated engine IL.");
                if (op == OpCodes.Ldsfld && run.Module.ResolveField(BitConverter.ToInt32(il, offset)) == field) lockLoad = start;
                if (op == OpCodes.Call || op == OpCodes.Callvirt)
                {
                    MethodBase method = run.Module.ResolveMethod(BitConverter.ToInt32(il, offset));
                    if (method.DeclaringType == typeof(Monitor) && method.Name == "Enter") { enter = start; enters++; }
                    if (method.DeclaringType == typeof(Monitor) && method.Name == "Exit") { exit = start; exits++; }
                    if (method.DeclaringType == engine && method.Name == "DLL_RunTick") { native = start; calls++; }
                }
                offset += size;
            }
            bool hasFinally = false; foreach (ExceptionHandlingClause clause in run.GetMethodBody().ExceptionHandlingClauses) if (clause.Flags == ExceptionHandlingClauseOptions.Finally) hasFinally = true;
            if (!(lockLoad >= 0 && lockLoad < enter && enter < native && native < exit && enters == 1 && exits == 1 && calls == 1 && hasFinally)) throw new NotSupportedException("Audited EngineInterface.run lock scope changed.");
            gate = field.GetValue(null) ?? throw new NotSupportedException("Engine lock missing.");
        }
        internal bool TryEnter() => Monitor.TryEnter(gate);
        internal void Exit() => Monitor.Exit(gate);
    }
}
