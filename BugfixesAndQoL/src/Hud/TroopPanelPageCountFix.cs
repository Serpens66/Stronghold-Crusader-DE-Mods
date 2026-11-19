using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace BugfixesAndQoL
{
    // Vanilla's HUD_Troops.SetupSelectedTroops only raises its private page count (pages = 2/3/4 at the
    // 9th/18th/27th selected type) and never lowers it. After one large selection, every later selection
    // keeps the page arrows and TogglePages can open empty pages. Resetting the count to Vanilla's initial
    // value 1 before each run lets the original recompute it. Its later page starts are also one/two
    // entries late: eight slots need starts 9/17/25, not 9/18/27. Only those validated comparisons change.
    // APIShared subsequently replaces the layout and preserves its requested page across Vanilla's clamp.
    // Both patches stay installed; the setting and a shared fail-closed state switch them logically.
    internal static class TroopPanelPageCountFix
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.TroopPanelPageCount");
        private static AccessTools.FieldRef<HUD_Troops, int> pagesField;
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static int attempted;
        private static int enabled;
        private static int installed;
        private static int transpilerReady;
        private static int faulted;

        internal static void Install(ManualLogSource logger, BugfixesAndQoLViewModel viewModel)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            log = logger;
            try
            {
                settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                FieldInfo pages = AccessTools.DeclaredField(typeof(HUD_Troops), "pages");
                if (pages == null || pages.IsStatic || pages.FieldType != typeof(int))
                    throw new MissingFieldException("HUD_Troops.pages differs from the audit.");
                pagesField = AccessTools.FieldRefAccess<HUD_Troops, int>(pages);
                MethodInfo setup = AccessTools.DeclaredMethod(typeof(HUD_Troops), "SetupSelectedTroops", Type.EmptyTypes);
                if (setup == null || setup.IsStatic || setup.ReturnType != typeof(void))
                    throw new MissingMethodException("HUD_Troops.SetupSelectedTroops signature differs from the audit.");
                ValidatePageMarkers(PatchProcessor.GetOriginalInstructions(setup).ToList());
                harmony.Patch(setup,
                    prefix: new HarmonyMethod(typeof(TroopPanelPageCountFix), nameof(BeforeSetupSelectedTroops)),
                    transpiler: new HarmonyMethod(typeof(TroopPanelPageCountFix), nameof(TranspilePageMarkers)));
                if (Volatile.Read(ref transpilerReady) == 0 || Volatile.Read(ref faulted) != 0)
                    throw new InvalidOperationException("HUD_Troops page markers were not transformed.");
                Volatile.Write(ref installed, 1);
                settings.SettingChanged += OnSettingChanged;
                RefreshEnabled();
                Shared.DebugLogHelper.LogInfo(log, "TROOP_PANEL_PAGE_COUNT_FIX_INSTALLED: permanent HUD_Troops prefix; enabled=" +
                    (Volatile.Read(ref enabled) != 0));
            }
            catch (Exception exception)
            {
                Volatile.Write(ref faulted, 1);
                Volatile.Write(ref enabled, 0);
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/TroopPanelPageCountFix/install",
                    "TROOP_PANEL_PAGE_COUNT_FIX_DISABLED: Vanilla behavior retained; " + exception);
            }
        }

        private static void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableTroopPanelPageCountFix))
                RefreshEnabled();
        }

        private static void RefreshEnabled() => Volatile.Write(ref enabled,
            Volatile.Read(ref installed) != 0 && Volatile.Read(ref transpilerReady) != 0 &&
            Volatile.Read(ref faulted) == 0 && pagesField != null && settings != null &&
            settings.EnableMod && settings.EnableClientFeatures &&
            settings.EnableTroopPanelPageCountFix ? 1 : 0);

        private static IEnumerable<CodeInstruction> TranspilePageMarkers(IEnumerable<CodeInstruction> instructions) =>
            APIShared.Patching.SafeTranspiler.Run(BugfixesAndQoLPlugin.PluginGuid,
                "BugfixesAndQoL.TroopPanelPageCount", instructions, TransformPageMarkers, log, OnTranspilerOutcome);

        private static void OnTranspilerOutcome(bool applied)
        {
            Volatile.Write(ref transpilerReady, applied ? 1 : 0);
            // Harmony reruns transpilers when another mod patches the method. A failed rebuild must
            // disable the prefix too, and a settings toggle must not revive a partially valid fix.
            if (!applied) Volatile.Write(ref faulted, 1);
            RefreshEnabled();
        }

        private static int ThirdPageStart() => Volatile.Read(ref enabled) != 0 ? 17 : 18;
        private static int FourthPageStart() => Volatile.Read(ref enabled) != 0 ? 25 : 27;

        private static IEnumerable<CodeInstruction> TransformPageMarkers(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int[] markers = ValidatePageMarkers(code);
            // Modify the existing instructions so all incoming labels and exception metadata survive.
            code[markers[1]].opcode = OpCodes.Call;
            code[markers[1]].operand = AccessTools.DeclaredMethod(typeof(TroopPanelPageCountFix), nameof(ThirdPageStart));
            code[markers[2]].opcode = OpCodes.Call;
            code[markers[2]].operand = AccessTools.DeclaredMethod(typeof(TroopPanelPageCountFix), nameof(FourthPageStart));
            return code;
        }

        private static int[] ValidatePageMarkers(List<CodeInstruction> code)
        {
            FieldInfo pages = AccessTools.DeclaredField(typeof(HUD_Troops), "pages");
            int[] thresholds = { 9, 18, 27 };
            var matches = new int[3];
            int counter = -1, typeIndex = -1;
            var starts = new HashSet<int>();
            for (int marker = 0; marker < thresholds.Length; marker++)
            {
                int found = -1;
                for (int index = 1; index + 6 < code.Count; index++)
                {
                    if (!code[index].LoadsConstant(thresholds[marker]) || !code[index - 1].IsLdloc() ||
                        (code[index + 1].opcode != OpCodes.Bne_Un && code[index + 1].opcode != OpCodes.Bne_Un_S) ||
                        !code[index + 2].IsLdloc() || !code[index + 3].IsStloc() ||
                        code[index + 4].opcode != OpCodes.Ldarg_0 || !code[index + 5].LoadsConstant(marker + 2) ||
                        code[index + 6].opcode != OpCodes.Stfld || !Equals(code[index + 6].operand, pages)) continue;
                    if (found >= 0) throw new InvalidOperationException("HUD_Troops has duplicate page marker " + thresholds[marker] + ".");
                    found = index;
                }
                if (found < 0) throw new InvalidOperationException("HUD_Troops page marker " + thresholds[marker] + " differs from the audit.");
                int actualCounter = LocalIndex(code[found - 1]);
                int actualType = LocalIndex(code[found + 2]);
                int actualStart = LocalIndex(code[found + 3]);
                if (actualCounter < 0 || actualType < 0 || actualCounter == actualType ||
                    actualStart < 0 || actualStart == actualCounter || actualStart == actualType ||
                    (marker > 0 && (counter != actualCounter || typeIndex != actualType)) ||
                    !starts.Add(actualStart))
                    throw new InvalidOperationException("HUD_Troops page markers no longer share their count/type locals.");
                counter = actualCounter;
                typeIndex = actualType;
                matches[marker] = found;
            }
            if (!(matches[0] < matches[1] && matches[1] < matches[2]))
                throw new InvalidOperationException("HUD_Troops page marker order differs from the audit.");
            return matches;
        }

        private static int LocalIndex(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Stloc_0) return 0;
            if (instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Stloc_1) return 1;
            if (instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Stloc_2) return 2;
            if (instruction.opcode == OpCodes.Ldloc_3 || instruction.opcode == OpCodes.Stloc_3) return 3;
            if (instruction.operand is LocalBuilder builder) return builder.LocalIndex;
            if (instruction.operand is LocalVariableInfo local) return local.LocalIndex;
            return instruction.operand == null ? -1 : Convert.ToInt32(instruction.operand);
        }

        private static void BeforeSetupSelectedTroops(HUD_Troops __instance)
        {
            if (Volatile.Read(ref enabled) == 0) return;
            try { pagesField(__instance) = 1; }
            catch (Exception exception)
            {
                // Never let the fix break Vanilla's HUD refresh; the original still runs with its own count.
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/TroopPanelPageCountFix/prefix",
                    "TROOP_PANEL_PAGE_COUNT_FIX_FAILED: " + exception);
            }
        }
    }
}
