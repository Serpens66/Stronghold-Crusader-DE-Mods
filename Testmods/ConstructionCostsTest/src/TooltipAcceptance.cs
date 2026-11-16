using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Automation;
using APIShared.Buildings;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace ConstructionCostsTest
{
    // This drives the real HUD_Main rollover publisher and inspects its loaded Noesis anchor.
    // Reflection is confined to a separate testmod; no manufactured production callback or OS input.
    internal static class TooltipAcceptance
    {
        internal static IEnumerable<AutomationWait> Check(AutomationRun run, Action<string> log,
            HumanConstructionCostRegistration extras)
        {
            var view = MainViewModel.Instance;
            var hud = view.HUDmain;
            Require(hud != null, "A real loaded HUD_Main is required for tooltip acceptance.");
            var fields = new Dictionary<FieldInfo, object>();
            foreach (string name in new[] { "HoverStruct", "HoverString", "OtherVisible" })
            {
                FieldInfo field = typeof(HUD_Main).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Require(field != null, "Missing genuine HUD field: " + name);
                fields.Add(field, field.GetValue(hud));
            }
            FieldInfo hover = typeof(HUD_Main).GetField("HoverStruct", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo text = typeof(HUD_Main).GetField("HoverString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo other = typeof(HUD_Main).GetField("OtherVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object runtime = BepInEx.Bootstrap.Chainloader.PluginInfos["BuildingCosts_Serp"].Instance.GetType()
                .GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            MethodInfo provider = runtime.GetType().GetMethod("GetBuildingTooltip", BindingFlags.Instance | BindingFlags.NonPublic);
            int player = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            try
            {
                foreach (bool active in new[] { false, true })
                {
                    extras.SetActive(active);
                    foreach (var type in new[] { eStructs.STRUCT_BALLISTA, eStructs.STRUCT_MANGONEL })
                    {
                        for (int frame = 0; frame < 3; frame++)
                        {
                            hover.SetValue(hud, (int)type);
                            text.SetValue(hud, "Construction tooltip acceptance " + type);
                            other.SetValue(hud, false);
                            hud.UpdateRollover();
                            yield return AutomationWait.NextFrame;
                        }
                        // Publish once more after layout has run, then inspect the original base controls.
                        hud.UpdateRollover();
                        Require(view.RolloverBuilding_TooltipVis, "Vanilla detailed building tooltip is not active: " + type);
                        Require(view.RollOverText_AmountReq1.Trim() == "50", "Vanilla base gold must remain 50: " + type);
                        Require(view.RollOverText_GoodsImage1 != null, "Vanilla gold icon must be present.");
                        Require(string.IsNullOrWhiteSpace(view.RollOverText_AmountReq2) && view.RollOverText_GoodsImage2 == null,
                            "A spurious second native material cost is visible: " + type);
                        var context = new TooltipContext(TooltipTarget.BuildingDetailed, true, (int)type, null, player, 1, true);
                        var lines = (IReadOnlyList<TooltipLine>)provider.Invoke(runtime, new object[] { context });
                        Require(lines.Count == (active ? 1 : 0), "Unexpected BuildingCosts extension line count: " + type);
                        if (active)
                        {
                            Require(lines[0].Segments.Count == 3, "Only the configured gold surcharge may be added.");
                            Require(lines[0].Segments[0] is TooltipText amount && amount.Text.Trim() == "12", "Human surcharge must be 12 gold.");
                            Require(lines[0].Segments[1] is TooltipSymbol symbol && symbol.Source != null, "Human surcharge icon must render.");
                        }

                        object host = FindLoadedDetailedHost(hud, log, out TextBlock anchor);
                        Require(host != null, "No loaded APIShared detailed tooltip anchor in the actual HUD visual tree.");
                        Require(anchor.ActualWidth > 0 && anchor.ActualHeight > 0, "The detailed cost anchor has no laid-out visible area.");
                        var owned = (IList)host.GetType().GetField("owned", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
                        Require(owned.Count == (active ? 4 : 0), "Rendered Noesis cost suffix differs from the provider.");
                        int symbols = 0; bool hasTwelve = false;
                        foreach (Inline inline in owned)
                        {
                            Require(anchor.Inlines.Contains(inline), "A cost suffix inline was not attached to the visible anchor.");
                            if (inline is InlineUIContainer) symbols++;
                            if (inline is Run value && value.Text.Trim() == "12") hasTwelve = true;
                        }
                        Require(symbols == (active ? 1 : 0) && (!active || hasTwelve), "Rendered suffix contains phantom materials or lacks the gold surcharge.");
                        log("tooltip-visible " + type + " baseGold=50 extraGold=" + (active ? 12 : 0) +
                            " modelLines=" + lines.Count + " attachedInlines=" + owned.Count +
                            " anchor=" + anchor.ActualWidth + "x" + anchor.ActualHeight +
                            "; checked loaded Noesis tree and model, not screenshot pixels");
                    }
                }
            }
            finally
            {
                extras.SetActive(true);
                foreach (var field in fields) field.Key.SetValue(hud, field.Value);
                hud.UpdateRollover();
            }
        }

        private static object FindLoadedDetailedHost(DependencyObject hud, Action<string> log, out TextBlock anchor)
        {
            var subtree = new HashSet<DependencyObject>();
            var pending = new Stack<DependencyObject>(); pending.Push(hud);
            while (pending.Count != 0)
            {
                var node = pending.Pop();
                subtree.Add(node);
                for (int child = 0; child < VisualTreeHelper.GetChildrenCount(node); child++)
                    pending.Push(VisualTreeHelper.GetChild(node, child));
            }
            var live = (IEnumerable)typeof(TooltipAnchor).GetField("live", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            object selected = null; anchor = null; int hosts = 0, eligible = 0;
            foreach (object host in live)
            {
                hosts++;
                var text = (TextBlock)host.GetType().GetField("Anchor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
                string name = (string)host.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
                bool current = ReferenceEquals(text.DataContext, MainViewModel.Instance);
                if (hosts <= 16)
                    log("tooltip-anchor name=" + name + " loaded=" + text.IsLoaded + " visible=" + text.IsVisible +
                        " area=" + text.ActualWidth + "x" + text.ActualHeight + " currentMainContext=" + current +
                        " hudVisualDescendant=" + subtree.Contains(text) + " parent=" + text.Parent?.GetType().Name +
                        " context=" + text.DataContext?.GetType().FullName);
                // APIShared's loaded-anchor list is the renderer's authority. Popup/overlay layout
                // may reparent a live anchor outside HUD_Main's visual descendants, so use its
                // semantic identity, actual visibility and current MainViewModel binding instead.
                if (name == "BuildingDetailed" && text.IsLoaded && text.IsVisible && current)
                { eligible++; selected = host; anchor = text; }
            }
            log("tooltip-anchor-summary live=" + hosts + " eligibleDetailed=" + eligible + " hudVisualNodes=" + subtree.Count);
            Require(eligible <= 1, "Multiple visible current-context detailed cost anchors make visual acceptance ambiguous.");
            return selected;
        }

        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }
}
