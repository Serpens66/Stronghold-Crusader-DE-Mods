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
        private bool suppressNextRightUp;

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
                "Bugfixes and QoL placement-cancel input suppression installed at the Vanilla right-click Down/Up branches.");
        }

        private void PatchRightClickBranch(ILContext context)
        {
            IList<Instruction> instructions = context.Body.Instructions;
            var rightDownMatches = new List<int>();
            for (int index = 0; index <= instructions.Count - 5; index++)
            {
                if (MatchesField(instructions[index], OpCodes.Ldsfld, typeof(MainControls), "instance") &&
                    MatchesMethod(instructions[index + 1], OpCodes.Callvirt, typeof(MainControls), nameof(MainControls.StopAllPlacement), 0) &&
                    instructions[index + 2].OpCode == OpCodes.Ldarg_0 &&
                    instructions[index + 3].OpCode == OpCodes.Ldc_I4_1 &&
                    MatchesField(instructions[index + 4], OpCodes.Stfld, typeof(EditorDirector), "rightDownForEngine"))
                {
                    rightDownMatches.Add(index);
                }
            }

            var rightUpMatches = new List<int>();
            for (int index = 0; index <= instructions.Count - 6; index++)
            {
                if (instructions[index].OpCode == OpCodes.Ldc_I4_1 &&
                    MatchesMethod(instructions[index + 1], OpCodes.Call, typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonUp), 1) &&
                    IsFalseBranch(instructions[index + 2]) &&
                    instructions[index + 3].OpCode == OpCodes.Ldarg_0 &&
                    instructions[index + 4].OpCode == OpCodes.Ldc_I4_1 &&
                    MatchesField(instructions[index + 5], OpCodes.Stfld, typeof(EditorDirector), "rightUpForEngine"))
                {
                    rightUpMatches.Add(index);
                }
            }

            // Validate both gesture halves before mutating the shared IL body.
            if (rightDownMatches.Count != 1 || rightUpMatches.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one placement-cancel right-click Down and Up IL block, found " +
                    $"Down={rightDownMatches.Count}, Up={rightUpMatches.Count}.");
            }

            var forwardRightDown = new VariableDefinition(context.Method.Module.TypeSystem.Boolean);
            context.Body.Variables.Add(forwardRightDown);

            // Keep the first ldsfld instruction in place because Vanilla branches to it.
            // Its MainControls value becomes the argument of the injected click-only delegate.
            var cursor = new ILCursor(context) { Index = rightDownMatches[0] + 1 };
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

            // The Up block precedes the Down block, so the Down replacement does not
            // invalidate its validated instruction index. Replace only Vanilla's true.
            var rightUpCursor = new ILCursor(context) { Index = rightUpMatches[0] + 4 };
            rightUpCursor.Remove();
            rightUpCursor.EmitDelegate<Func<bool>>(GetRightUpForEngine);
        }

        private bool CancelPlacementAndGetRightDown(MainControls controls)
        {
            bool forwardRightDown = true;
            suppressNextRightUp = false;
            try
            {
                forwardRightDown = PlacementCancelRightClickPolicy.BeginRightClickGesture(
                    settings.EnableMod,
                    settings.EnableClientFeatures,
                    settings.PreventMoveOrderOnPlacementCancel,
                    controls.CurrentAction,
                    ConfigSettings.Settings_SH1RTSControls,
                    ref suppressNextRightUp);
                if (!forwardRightDown)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "Bugfixes and QoL suppressed placement-cancel right-click Down; matching Up is pending.");
                }
            }
            catch (Exception ex)
            {
                suppressNextRightUp = false;
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

        private bool GetRightUpForEngine()
        {
            bool forwardRightUp =
                PlacementCancelRightClickPolicy.CompleteRightClickGesture(ref suppressNextRightUp);
            if (!forwardRightUp)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL suppressed matching placement-cancel right-click Up.");
            }
            return forwardRightUp;
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
            string methodName,
            int parameterCount)
        {
            return instruction.OpCode == opcode &&
                instruction.Operand is MethodReference method &&
                method.Name == methodName &&
                method.Parameters.Count == parameterCount &&
                method.DeclaringType.FullName == declaringType.FullName;
        }

        private static bool IsFalseBranch(Instruction instruction)
        {
            return instruction.OpCode == OpCodes.Brfalse ||
                instruction.OpCode == OpCodes.Brfalse_S;
        }
    }
}
