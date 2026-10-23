using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace PerfProbe
{
    // Executable, deliberately small IL fixture. Unity's MonoMod cannot copy an
    // Add-Type in-memory assembly under desktop .NET; emit the tested contract
    // directly instead. The PRODUCTION transpiler transforms and executes it.
    internal static class RunIlFixture
    {
        internal static Func<bool, int> Create()
        {
            Type type = typeof(DiagnosticHelperTests.RunFixture);
            var dynamic = new DynamicMethod("PerfProbeRunFixture", typeof(int), new[] { typeof(bool) }, type.Module, true);
            var g = dynamic.GetILGenerator();
            var buffer = g.DeclareLocal(typeof(object)); var gate = g.DeclareLocal(typeof(object));
            var taken = g.DeclareLocal(typeof(bool)); var a = g.DeclareLocal(typeof(int));
            var b = g.DeclareLocal(typeof(int)); var result = g.DeclareLocal(typeof(int));
            Label have = g.DefineLabel(), done = g.DefineLabel(), skip = g.DefineLabel();
            FieldInfo Field(string name) => type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var code = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldsfld, Field("buffers")), new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Callvirt, typeof(DiagnosticHelperTests.MemoryBuffers).GetMethod("getFreeBuffer")),
                new CodeInstruction(OpCodes.Stloc, buffer), new CodeInstruction(OpCodes.Ldloc, buffer), new CodeInstruction(OpCodes.Brtrue, have),
                new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret),
                new CodeInstruction(OpCodes.Ldsfld, Field("threadLock")), new CodeInstruction(OpCodes.Stloc, gate),
                new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Stloc, taken),
                new CodeInstruction(OpCodes.Ldloc, gate), new CodeInstruction(OpCodes.Ldloca, taken),
                new CodeInstruction(OpCodes.Call, typeof(Monitor).GetMethod("Enter", new[] { typeof(object), typeof(bool).MakeByRefType() })),
                new CodeInstruction(OpCodes.Ldsfld, Field("editor")), new CodeInstruction(OpCodes.Ldloca, a), new CodeInstruction(OpCodes.Ldloca, b),
                new CodeInstruction(OpCodes.Callvirt, typeof(DiagnosticHelperTests.EditorDirector).GetMethod("preDLLCallActions")),
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, type.GetMethod("DLL_RunTick")), new CodeInstruction(OpCodes.Stloc, result),
                new CodeInstruction(OpCodes.Call, type.GetMethod("CopyPlayStateStruct")), new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldsfld, Field("buffers")), new CodeInstruction(OpCodes.Ldloc, buffer),
                new CodeInstruction(OpCodes.Callvirt, typeof(DiagnosticHelperTests.MemoryBuffers).GetMethod("returnBuffer")),
                new CodeInstruction(OpCodes.Leave, done), new CodeInstruction(OpCodes.Ldloc, taken), new CodeInstruction(OpCodes.Brfalse, skip),
                new CodeInstruction(OpCodes.Ldloc, gate), new CodeInstruction(OpCodes.Call, typeof(Monitor).GetMethod("Exit")),
                new CodeInstruction(OpCodes.Nop), new CodeInstruction(OpCodes.Nop),
                new CodeInstruction(OpCodes.Ldloc, result), new CodeInstruction(OpCodes.Ret)
            };
            code[8].labels.Add(have); code[12].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            code[28].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)); code[32].labels.Add(skip);
            code[33].blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock)); code[34].labels.Add(done);
            var transformed = PerfProbeRunTiming.Transpile(code, type.GetMethod("run"));
            foreach (var instruction in transformed)
            {
                foreach (var block in instruction.blocks)
                    switch (block.blockType)
                    {
                        case ExceptionBlockType.BeginExceptionBlock: g.BeginExceptionBlock(); break;
                        case ExceptionBlockType.BeginFinallyBlock: g.BeginFinallyBlock(); break;
                        case ExceptionBlockType.EndExceptionBlock: g.EndExceptionBlock(); break;
                    }
                foreach (var label in instruction.labels) g.MarkLabel(label);
                object operand = instruction.operand;
                if (operand == null) g.Emit(instruction.opcode);
                else if (operand is MethodInfo method) g.Emit(instruction.opcode, method);
                else if (operand is FieldInfo field) g.Emit(instruction.opcode, field);
                else if (operand is LocalBuilder local) g.Emit(instruction.opcode, local);
                else if (operand is Label target) g.Emit(instruction.opcode, target);
                else throw new InvalidOperationException("Unsupported fixture operand");
            }
            return (Func<bool, int>)dynamic.CreateDelegate(typeof(Func<bool, int>));
        }
    }
}
