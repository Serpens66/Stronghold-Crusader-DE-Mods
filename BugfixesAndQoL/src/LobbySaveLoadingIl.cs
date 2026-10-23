using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Only the final load-button Boolean is transformed; play/readiness/Coop IL is untouched.
    internal static class LobbySaveLoadingIl
    {
        internal static int FindSetter(ILContext context)
        {
            IList<Instruction> code = context.Body.Instructions;
            int found = -1;
            for (int i = 1; i + 10 < code.Count; i++)
            {
                var field = code[i].Operand as FieldReference;
                if (code[i].OpCode != OpCodes.Ldfld || field?.Name != "RefLoadButton" ||
                    field.DeclaringType.FullName != typeof(FRONT_Multiplayer).FullName)
                    continue;
                if (found >= 0) throw new InvalidOperationException("Ambiguous Vanilla load-button assignment.");
                var setter = code[i + 10].Operand as MethodReference;
                var mode = code[i + 8].Operand as FieldReference;
                if (code[i - 1].OpCode != OpCodes.Ldarg_0 || field.FieldType.FullName != "Noesis.Button" ||
                    code[i + 1].OpCode != OpCodes.Ldloc_S ||
                    code[i + 2].OpCode != OpCodes.Brfalse_S || Target(code[i + 2]) != code[i + 7] ||
                    code[i + 3].OpCode != OpCodes.Ldloc_S || code[i + 4].OpCode != OpCodes.Ldc_I4_0 ||
                    code[i + 5].OpCode != OpCodes.Ceq || code[i + 6].OpCode != OpCodes.Br_S ||
                    Target(code[i + 6]) != code[i + 8] || code[i + 7].OpCode != OpCodes.Ldc_I4_0 ||
                    code[i + 8].OpCode != OpCodes.Ldsfld || mode?.Name != "skirmishGame" ||
                    mode.DeclaringType.FullName != typeof(FRONT_Multiplayer).FullName ||
                    code[i + 9].OpCode != OpCodes.Or || code[i + 10].OpCode != OpCodes.Callvirt ||
                    setter?.FullName != "System.Void Noesis.UIElement::set_IsEnabled(System.Boolean)")
                    throw new InvalidOperationException("Vanilla load-button Boolean contract changed.");
                found = i + 10;
            }
            if (found < 0) throw new InvalidOperationException("Vanilla load-button assignment is missing.");
            // No predecessor may skip the added transform or enter it with a different stack.
            foreach (Instruction instruction in code)
            {
                if (Target(instruction) == code[found] ||
                    (instruction.Operand is Instruction[] targets && Array.IndexOf(targets, code[found]) >= 0))
                    throw new InvalidOperationException("Unexpected branch into the load-button setter.");
            }
            return found;
        }

        internal static void Patch(ILContext context, Func<bool, FRONT_Multiplayer, bool> resolve)
        {
            var cursor = new ILCursor(context) { Index = FindSetter(context) };
            // Stack before: Button, Vanilla Boolean. After: Button, resolved Boolean.
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(resolve);
        }

        private static Instruction Target(Instruction instruction)
        {
            return instruction.Operand is ILLabel label ? label.Target : instruction.Operand as Instruction;
        }
    }
}
