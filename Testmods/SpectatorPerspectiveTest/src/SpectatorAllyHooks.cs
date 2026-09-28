using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CrusaderDE;
using HarmonyLib;
using Noesis;

namespace SpectatorPerspectiveTest
{
    internal static class SpectatorAllyHooks
    {
        private static readonly Harmony harmony = new Harmony("SpectatorPerspectiveTest_Serp.Allies");
        private static readonly MethodInfo playerIdGetter = AccessTools.PropertyGetter(typeof(GameData), nameof(GameData.playerID));
        private static readonly MethodInfo viewPlayerGetter = AccessTools.Method(typeof(SpectatorAllyHooks), nameof(GetViewPlayerId));
        private static readonly MethodInfo panelOpen = AccessTools.Method(typeof(HUD_AlliesPanel), "Open", Type.EmptyTypes);
        private static readonly Dictionary<Button, bool> originalButtonStates = new Dictionary<Button, bool>();
        private static HUD_AlliesPanel guardedPanel;
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            MethodInfo allies = AccessTools.Method(typeof(HUD_AlliesPanel), nameof(HUD_AlliesPanel.GetAllyList));
            MethodInfo enemies = AccessTools.Method(typeof(HUD_AlliesPanel), nameof(HUD_AlliesPanel.GetEnemyList));
            MethodInfo update = AccessTools.Method(typeof(HUD_AlliesPanel), "UpdateAllies");
            MethodInfo open = AccessTools.Method(typeof(HUD_AlliesPanel), nameof(HUD_AlliesPanel.Open), new[] { typeof(bool) });
            MethodInfo gameAction = AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.GameAction),
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) });
            if (allies == null || enemies == null || update == null || open == null || gameAction == null ||
                panelOpen == null || playerIdGetter == null || viewPlayerGetter == null)
                throw new MissingMemberException("Installed Assembly-CSharp ally contract changed.");

            // Replace only the ally panel's view reads; GameData.playerID remains the real network identity.
            var viewPatch = new HarmonyMethod(typeof(SpectatorAllyHooks), nameof(AllyViewTranspiler));
            harmony.Patch(allies, transpiler: viewPatch);
            harmony.Patch(enemies, transpiler: viewPatch);
            harmony.Patch(update, transpiler: viewPatch);
            harmony.Patch(open, postfix: new HarmonyMethod(typeof(SpectatorAllyHooks), nameof(AfterOpen)));
            harmony.Patch(gameAction, prefix: new HarmonyMethod(typeof(SpectatorAllyHooks), nameof(AllowAllyAction)));
            installed = true;
        }

        private static int GetViewPlayerId(GameData data)
        {
            return SpectatorPerspectiveRuntime.GetAllyViewPlayerId(data);
        }

        private static IEnumerable<CodeInstruction> AllyViewTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    Equals(instruction.operand, playerIdGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = viewPlayerGetter;
                    replaced++;
                }
                yield return instruction;
            }
            int expected = original.Name == nameof(HUD_AlliesPanel.GetAllyList) ? 3 : 2;
            if (replaced != expected)
                throw new InvalidOperationException("Vanilla ally view reads changed in " + original.Name +
                    "; expected " + expected + ", found " + replaced + ".");
        }

        private static bool AllowAllyAction(Enums.GameActionCommand command, int structureID, int state, int value2,
            ref int __result)
        {
            if (command < Enums.GameActionCommand.Ally_Orders || command > Enums.GameActionCommand.Ally_CancelOrders)
                return true;
            if (SpectatorPerspectiveRuntime.IsNetworkSpectator())
            {
                __result = 0;
                return false;
            }
            if (!SpectatorPerspectiveRuntime.IsSpectatorActionRestricted()) return true;
            bool allowed = SpectatorPerspectiveRuntime.CanIssueAllyAction(command, structureID, state, value2);
            if (allowed) return true;
            __result = 0;
            return false;
        }

        private static void AfterOpen(bool state)
        {
            bool active = SpectatorPerspectiveRuntime.IsSpectatorActionRestricted() ||
                SpectatorPerspectiveRuntime.IsNetworkSpectator();
            if (state && active) RefreshControlState();
            else if (!active) RestoreControls();
        }

        internal static void RefreshOpenPanel()
        {
            if (!SpectatorPerspectiveRuntime.IsActiveSpectator() || MainViewModel.Instance?.AlliesPanelVisible != true) return;
            var panel = MainViewModel.Instance.HUDAlliesPanel;
            if (panel == null) return;
            panelOpen.Invoke(panel, null);
            RefreshControlState();
        }

        internal static void RefreshControlState()
        {
            var panel = MainViewModel.Instance?.HUDAlliesPanel;
            if (panel == null) return;
            if (!ReferenceEquals(panel, guardedPanel))
            {
                RestoreControls();
                guardedPanel = panel;
            }
            if (originalButtonStates.Count < 12) FindActionButtons(panel);
            bool enabled = SpectatorPerspectiveRuntime.CanInteractWithAllies();
            foreach (var entry in originalButtonStates)
            {
                bool shouldEnable = enabled && entry.Value;
                if (entry.Key.IsEnabled != shouldEnable) entry.Key.IsEnabled = shouldEnable;
            }
        }

        private static void FindActionButtons(DependencyObject root)
        {
            if (root is Button button && IsActionButton(button.CommandParameter as string) &&
                !originalButtonStates.ContainsKey(button))
                originalButtonStates.Add(button, button.IsEnabled);
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < count; index++)
                FindActionButtons(VisualTreeHelper.GetChild(root, index));
        }

        private static bool IsActionButton(string parameter)
        {
            return parameter == "Cancel" || parameter == "Defend" || parameter == "ConfirmOrders" ||
                   parameter == "CancelOrders" || parameter == "ConfirmGoods" || parameter == "CancelGoods" ||
                   (parameter != null && parameter.Length == 7 && parameter.StartsWith("Attack", StringComparison.Ordinal) &&
                    parameter[6] >= '0' && parameter[6] <= '5');
        }

        internal static void RestoreControls()
        {
            foreach (var entry in originalButtonStates)
                entry.Key.IsEnabled = entry.Value;
            originalButtonStates.Clear();
            guardedPanel = null;
        }
    }
}
