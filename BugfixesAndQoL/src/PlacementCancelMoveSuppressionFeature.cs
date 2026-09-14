// Feature: Cancel building placement without forwarding the click to the engine.
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class PlacementCancelMoveSuppressionFeature
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private ILHook editorDirectorUpdateIlHook;
        private bool classificationFailureLogged;

        public PlacementCancelMoveSuppressionFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Install()
        {
            if (editorDirectorUpdateIlHook != null)
                return;

            MethodInfo updateMethod = typeof(EditorDirector).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (updateMethod == null)
                throw new MissingMethodException(typeof(EditorDirector).FullName, "Update");

            editorDirectorUpdateIlHook = new ILHook(updateMethod, PatchRightClickBranch);
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL placement-cancel input suppression installed at the Vanilla right-click branch.");
        }

        private void PatchRightClickBranch(ILContext context)
        {
            IList<Instruction> instructions = context.Body.Instructions;
            var matches = new List<int>();
            for (int index = 0; index <= instructions.Count - 5; index++)
            {
                if (MatchesField(instructions[index], OpCodes.Ldsfld, typeof(MainControls), "instance") &&
                    MatchesMethod(instructions[index + 1], OpCodes.Callvirt, typeof(MainControls), nameof(MainControls.StopAllPlacement)) &&
                    instructions[index + 2].OpCode == OpCodes.Ldarg_0 &&
                    instructions[index + 3].OpCode == OpCodes.Ldc_I4_1 &&
                    MatchesField(instructions[index + 4], OpCodes.Stfld, typeof(EditorDirector), "rightDownForEngine"))
                {
                    matches.Add(index);
                }
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one placement-cancel right-click IL block, found {matches.Count}.");
            }

            var forwardRightDown = new VariableDefinition(context.Method.Module.TypeSystem.Boolean);
            context.Body.Variables.Add(forwardRightDown);

            // Keep the first ldsfld instruction in place because Vanilla branches to it.
            // Its MainControls value becomes the argument of the injected click-only delegate.
            var cursor = new ILCursor(context) { Index = matches[0] + 1 };
            cursor.RemoveRange(4);
            cursor.EmitDelegate<Func<MainControls, bool>>(CancelPlacementAndGetRightDown);
            cursor.Emit(OpCodes.Stloc, forwardRightDown);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, forwardRightDown);
            cursor.Emit(
                OpCodes.Stfld,
                typeof(EditorDirector).GetField(
                    "rightDownForEngine",
                    BindingFlags.Instance | BindingFlags.NonPublic));
        }

        private bool CancelPlacementAndGetRightDown(MainControls controls)
        {
            bool forwardRightDown = true;
            try
            {
                forwardRightDown = PlacementCancelRightClickPolicy.ShouldForwardRightDown(
                    settings.EnableMod,
                    settings.EnableClientFeatures,
                    settings.PreventMoveOrderOnPlacementCancel,
                    controls.CurrentAction,
                    ConfigSettings.Settings_SH1RTSControls);
            }
            catch (Exception ex)
            {
                if (!classificationFailureLogged)
                {
                    classificationFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL could not classify a placement-cancel click; Vanilla input continues: {ex}");
                }
            }

            // Preserve Vanilla placement cleanup exactly once, even when classification fails.
            controls.StopAllPlacement();
            return forwardRightDown;
        }

        private static bool MatchesField(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string fieldName)
        {
            return instruction.OpCode == opcode &&
                instruction.Operand is FieldReference field &&
                field.Name == fieldName &&
                field.DeclaringType.FullName == declaringType.FullName;
        }

        private static bool MatchesMethod(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string methodName)
        {
            return instruction.OpCode == opcode &&
                instruction.Operand is MethodReference method &&
                method.Name == methodName &&
                method.Parameters.Count == 0 &&
                method.DeclaringType.FullName == declaringType.FullName;
        }
    }
}
