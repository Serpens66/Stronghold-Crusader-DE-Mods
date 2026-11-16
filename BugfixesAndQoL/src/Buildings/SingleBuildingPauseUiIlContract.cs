using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal static class SingleBuildingPauseUiIlContract
    {
        internal static void Validate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.ReturnType != typeof(bool))
                throw new InvalidOperationException("Expected the instance bool addChimpActions method.");
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 4 || parameters[0].ParameterType != typeof(EngineInterface.PlayState) ||
                parameters[1].ParameterType != typeof(string).MakeByRefType() ||
                parameters[2].ParameterType != typeof(string).MakeByRefType() ||
                parameters[3].ParameterType != typeof(bool))
                throw new InvalidOperationException("Unexpected addChimpActions parameter contract.");

            using (var definition = new DynamicMethodDefinition(method))
            using (var context = new ILContext(definition.Definition))
                context.Invoke(il => FindUniqueRead(il));
        }

        internal static void ApplyPatch(
            ILContext context, Func<EngineInterface.PlayState, short, short> resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            int index = FindUniqueRead(context);
            // Keep incoming branches on the first replacement instruction. The shared snapshot is never changed.
            Instruction read = context.Body.Instructions[index];
            var field = (FieldReference)read.Operand;
            read.OpCode = OpCodes.Dup;
            read.Operand = null;
            var cursor = new ILCursor(context) { Index = index + 1 };
            cursor.Emit(OpCodes.Ldfld, field);
            cursor.EmitDelegate(resolver);
        }

        private static int FindUniqueRead(ILContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int match = -1;
            int count = 0;
            for (int index = 0; index < context.Body.Instructions.Count; index++)
            {
                Instruction instruction = context.Body.Instructions[index];
                if (instruction.OpCode == OpCodes.Ldfld && instruction.Operand is FieldReference field &&
                    field.DeclaringType.FullName == typeof(EngineInterface.PlayState).FullName.Replace('+', '/') &&
                    field.Name == nameof(EngineInterface.PlayState.building_type_sleeping) &&
                    field.FieldType.MetadataType == MetadataType.Int16)
                {
                    match = index;
                    count++;
                }
            }

            if (count != 1)
                throw new InvalidOperationException($"Expected one addChimpActions building_type_sleeping read, found {count}.");
            return match;
        }
    }
}
