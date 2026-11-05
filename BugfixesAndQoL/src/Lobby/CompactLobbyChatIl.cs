using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace BugfixesAndQoL
{
    internal static class CompactLobbyChatIl
    {
        internal static int FindInsertion(ILContext context)
        {
            var code = context.Body.Instructions;
            string[] owners = { "CrusaderDE.FRONT_Multiplayer", "CrusaderDE.FRONT_CoopTrail1",
                "CrusaderDE.FRONT_CoopTrail2", "CrusaderDE.FRONT_CoopTrail3", "CrusaderDE.FRONT_CoopTrail4" };
            int start = -1, scrolls = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!(code[i].Operand is MethodReference call) ||
                    call.FullName != "System.Void Noesis.ScrollViewer::ScrollToBottom()") continue;
                if (scrolls >= owners.Length || i < 2 || code[i].OpCode != OpCodes.Callvirt ||
                    code[i - 1].OpCode != OpCodes.Ldfld ||
                    !(code[i - 1].Operand is FieldReference field) ||
                    field.Name != "RefMP_ChatScrollView" || field.FieldType.FullName != "Noesis.ScrollViewer" ||
                    field.DeclaringType.FullName != owners[scrolls])
                    throw new InvalidOperationException("Unexpected lobby chat scroll contract.");
                if (scrolls == 0)
                {
                    if (code[i - 2].OpCode != OpCodes.Ldarg_0)
                        throw new InvalidOperationException("Unexpected lobby chat receiver.");
                    start = i - 2;
                }
                else if (i != start + scrolls * 3 + 2 || code[i - 2].OpCode != OpCodes.Ldsfld ||
                    !(code[i - 2].Operand is FieldReference instance) || instance.Name != "Instance" ||
                    instance.DeclaringType.FullName != owners[scrolls])
                    throw new InvalidOperationException("Lobby chat scroll sequence is not contiguous.");
                scrolls++;
            }
            if (scrolls != owners.Length) throw new InvalidOperationException("Lobby chat scroll sequence missing.");
            // This insertion must not be bypassed or entered with a live evaluation stack.
            foreach (var instruction in code)
            {
                var target = instruction.Operand is ILLabel label ? label.Target : instruction.Operand as Instruction;
                if (target == code[start] &&
                    (instruction.OpCode == OpCodes.Leave || instruction.OpCode == OpCodes.Leave_S)) continue;
                if (target != null && code.IndexOf(target) >= start && code.IndexOf(target) < start + 15)
                    throw new InvalidOperationException("Unexpected branch into lobby chat scrolling.");
                if (instruction.Operand is Instruction[] targets)
                    foreach (var entry in targets)
                        if (code.IndexOf(entry) >= start && code.IndexOf(entry) < start + 15)
                            throw new InvalidOperationException("Unexpected switch into lobby chat scrolling.");
            }
            return start;
        }

        internal static void Patch(ILContext context, Action<object> onRebuilt)
        {
            var cursor = new ILCursor(context) { Index = FindInsertion(context) };
            var oldStart = cursor.Next;
            int index = cursor.Index;
            cursor.MoveAfterLabels(); // The foreach leave must execute the added callback too.
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(onRebuilt);
            // Keep the callback outside the enumerator's finally region.
            var newStart = context.Body.Instructions[index];
            foreach (var handler in context.Body.ExceptionHandlers)
            {
                if (handler.HandlerEnd == oldStart) handler.HandlerEnd = newStart;
                if (handler.TryEnd == oldStart) handler.TryEnd = newStart;
            }
        }
    }
}
