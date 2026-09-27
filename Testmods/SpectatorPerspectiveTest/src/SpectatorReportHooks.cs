using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CrusaderDE;
using HarmonyLib;

namespace SpectatorPerspectiveTest
{
    internal static class SpectatorReportHooks
    {
        private static readonly Harmony harmony = new Harmony("SpectatorPerspectiveTest_Serp.Reports");
        private static readonly FieldInfo freezeField = AccessTools.Field(typeof(MainViewModel), nameof(MainViewModel.FreezeMainControls));
        private static readonly MethodInfo forceArmyMethod = AccessTools.Method(typeof(SpectatorReportHooks), nameof(ShouldForceArmy));
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            MethodInfo foodAction = AccessTools.Method(typeof(MainViewModel), "ButtonChangeEdibleState", new[] { typeof(object) });
            MethodInfo reportsAction = AccessTools.Method(typeof(MainViewModel), "ButtonReports", new[] { typeof(object) });
            if (foodAction == null || reportsAction == null || freezeField == null || forceArmyMethod == null)
                throw new MissingMemberException("Installed Assembly-CSharp report contract changed.");

            // Install the read-only guard first. Published patches stay installed for the process lifetime.
            harmony.Patch(foodAction, prefix: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(AllowFoodChange)));
            harmony.Patch(reportsAction, transpiler: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(ReportTranspiler)));
            installed = true;
        }

        private static bool AllowFoodChange()
        {
            return !SpectatorPerspectiveRuntime.IsActiveSpectator();
        }

        private static bool ShouldForceArmy(MainViewModel viewModel)
        {
            return viewModel.FreezeMainControls && !SpectatorPerspectiveRuntime.IsActiveSpectator();
        }

        private static IEnumerable<CodeInstruction> ReportTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, freezeField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = forceArmyMethod;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1)
                throw new InvalidOperationException("Vanilla ButtonReports freeze branch changed; expected one field read, found " + replaced + ".");
        }
    }
}
