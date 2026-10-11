using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ArabianIdleSoundTest
{
    internal static class SoundIlContract
    {
        internal static readonly Action<SFXManager, int, float, float, bool, short[], int> Playback = Play;
        internal static Action<int, float, float> Reduced;

        private static void Play(SFXManager manager, int sound, float volume, float pan, bool unstoppable, short[] buffer, int index)
        {
            float scaled = SoundPolicy.Scale(buffer, index, sound, volume);
            // Exactly one original call, outside the independent diagnostic path.
            manager.playSound(sound, scaled, pan, unstoppable);
            if (scaled != volume)
            {
                try { Reduced?.Invoke(index, volume, scaled); }
                catch { /* Logging must not affect playback. */ }
            }
        }

        internal static void Validate(MethodInfo method)
        {
            using (var definition = new DynamicMethodDefinition(method))
            using (var context = new ILContext(definition.Definition)) Find(context, out _);
        }

        internal static Instruction[] Find(ILContext context, out VariableDefinition index)
        {
            index = null;
            var instructions = context.Body.Instructions;
            // Read the actual loop index from sourceMap[j * 21 + 11], not a
            // guessed compiler local number. Both call sites share this record.
            for (int i = 0; i + 6 < instructions.Count; i++)
            {
                if (instructions[i].MatchLdarg(1) && instructions[i + 1].MatchLdloc(out int local) &&
                    instructions[i + 2].MatchLdcI4(21) && instructions[i + 3].OpCode == OpCodes.Mul &&
                    instructions[i + 4].MatchLdcI4(11) && instructions[i + 5].OpCode == OpCodes.Add &&
                    instructions[i + 6].OpCode == OpCodes.Ldelem_I2)
                {
                    if (index != null && index != context.Body.Variables[local])
                        throw new InvalidOperationException("Ambiguous sound record loop index.");
                    index = context.Body.Variables[local];
                }
            }
            var calls = new List<Instruction>();
            foreach (var instruction in instructions)
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                    instruction.Operand is MethodReference method && method.DeclaringType.FullName == "SFXManager" &&
                    method.Name == "playSound" && method.ReturnType.FullName == "System.Void" && method.HasThis &&
                    method.Parameters.Count == 4 && method.Parameters[0].ParameterType.FullName == "System.Int32" &&
                    method.Parameters[1].ParameterType.FullName == "System.Single" &&
                    method.Parameters[2].ParameterType.FullName == "System.Single" &&
                    method.Parameters[3].ParameterType.FullName == "System.Boolean") calls.Add(instruction);
            if (index == null || calls.Count != 2) throw new InvalidOperationException("Vanilla sound IL contract changed.");
            return calls.ToArray();
        }

        internal static void Apply(ILContext context)
        {
            var calls = Find(context, out VariableDefinition index);
            foreach (var call in calls)
            {
                var cursor = new ILCursor(context);
                cursor.Goto(call, MoveType.Before);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.Emit(OpCodes.Ldloc, index);
                cursor.EmitDelegate(Playback);
                cursor.Remove();
            }
        }
    }
}
