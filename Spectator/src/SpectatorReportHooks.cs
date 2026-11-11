using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CrusaderDE;
using HarmonyLib;

namespace Spectator
{
    internal static class SpectatorReportHooks
    {
        private static readonly Harmony harmony = new Harmony("Spectator_Serp.SpectatorReports");
        private static readonly FieldInfo freezeField = AccessTools.Field(typeof(MainViewModel), nameof(MainViewModel.FreezeMainControls));
        private static readonly MethodInfo forceArmyMethod = AccessTools.Method(typeof(SpectatorReportHooks), nameof(ShouldForceArmy));
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            MethodInfo foodAction = AccessTools.Method(typeof(MainViewModel), "ButtonChangeEdibleState", new[] { typeof(object) });
            MethodInfo reportsAction = AccessTools.Method(typeof(MainViewModel), "ButtonReports", new[] { typeof(object) });
            MethodInfo nameSetter = AccessTools.PropertySetter(typeof(MainViewModel), nameof(MainViewModel.PlayerNameText));
            if (foodAction == null || reportsAction == null || nameSetter == null || freezeField == null || forceArmyMethod == null)
                throw new MissingMemberException("Installed Assembly-CSharp report contract changed.");

            // An unsupported game version fails here; the transpiler itself never throws.
            int freezeReads = 0;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(reportsAction))
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, freezeField)) freezeReads++;
            if (freezeReads != 1)
                throw new InvalidOperationException("Vanilla ButtonReports freeze branch changed; expected one field read, found " + freezeReads + ".");
            // Install the read-only guard first. Published patches stay installed for the process lifetime.
            harmony.Patch(foodAction, prefix: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(AllowFoodChange)));
            harmony.Patch(nameSetter, prefix: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(UseSelectedReportName)));
            harmony.Patch(reportsAction, prefix: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(AllowReports)),
                transpiler: new HarmonyMethod(typeof(SpectatorReportHooks), nameof(ReportTranspiler)));
            installed = true;
        }

        private static bool AllowFoodChange()
        {
            return !SpectatorPerspectiveRuntime.IsSpectatorActionRestricted();
        }

        private static bool AllowReports() => SpectatorPerspectiveRuntime.CanOpenReports();

        private static void UseSelectedReportName(ref string value)
        {
            if (SpectatorPerspectiveRuntime.TryGetSelectedReportName(out string selectedName)) value = selectedName;
        }

        private static bool ShouldForceArmy(MainViewModel viewModel)
        {
            // Test placement can temporarily unlock build tabs, never information rights.
            return SpectatorFeatureActivation.ForceArmyReport(viewModel.FreezeMainControls,
                SpectatorPerspectiveRuntime.IsSpectatorObserver(), SpectatorPerspectiveRuntime.IsActiveSpectator());
        }

        // Harmony reruns this on every patch change of the method; a failure keeps the original IL and
        // every other patch of it. Spectators then keep Vanilla's report rights.
        private static IEnumerable<CodeInstruction> ReportTranspiler(IEnumerable<CodeInstruction> instructions) =>
            APIShared.Patching.SafeTranspiler.Run(SpectatorPlugin.PluginGuid, "Spectator.Reports", instructions, ReplaceFreezeRead, SpectatorRuntime.RawLog);

        private static IEnumerable<CodeInstruction> ReplaceFreezeRead(IEnumerable<CodeInstruction> instructions)
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
