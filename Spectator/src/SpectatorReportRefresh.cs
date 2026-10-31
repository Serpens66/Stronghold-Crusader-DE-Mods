using System;
using System.Collections.Generic;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        internal static int CaptureReportFrame(ReportFrameClock clock) => clock.Capture(engineLock);
    }

    internal static partial class SpectatorPerspectiveRuntime
    {
        private readonly struct ReportValue
        {
            internal readonly FrameworkElement Owner;
            internal readonly DependencyObject Target;
            internal readonly DependencyProperty Property;
            internal ReportValue(FrameworkElement owner, DependencyObject target, DependencyProperty property)
            { Owner = owner; Target = target; Property = property; }
        }
        private static readonly ReportRefreshState reportRefresh = new ReportRefreshState();
        private static readonly ReportFrameClock reportClock = new ReportFrameClock();
        private static readonly List<ReportValue> reportValues = new List<ReportValue>();
        private static HUD_Buildings reportCacheOwner;
        private static ReportDisplayLease reportDisplay;

        private static void InitializeReportRefresh()
        {
            ReportFrameClock.Validate();
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, SpectatorPlugin.PluginGuid,
                "ReportRefresh", null, OnReportGuiCompleted, out string reason))
                throw new InvalidOperationException("Report GUI completion unavailable: " + reason);
        }

        internal static void ObserveReportSnapshot(EngineInterface.PlayState snapshot)
        {
            if (reportRefresh.Pending && reportClock.TryRead(snapshot, out int frame)) reportRefresh.Observe(snapshot, frame);
        }

        private static void OnReportGuiCompleted(PresentationPostEventArgs args)
        {
            if (!reportRefresh.Pending) return;
            try
            {
                if (!ReportContextCurrent()) { RestoreReportPanel(); return; }
                // Unbound widget heads are written imperatively by Vanilla. Capture their new
                // values after GUI completion, then keep the old complete report until release.
                reportDisplay?.Hold();
                if (!args.OriginalCompleted || !reportRefresh.HasFreshState(GameData.Instance?.lastGameState)) return;
                reportRefresh.Cancel();
                CacheSelectedName();
                RestoreReportPanel();
            }
            catch (Exception error)
            {
                RestoreReportPanel();
                Shared.DebugLogHelper.LogError(log, "SPECTATOR_REPORT_PRESENTATION_FAILED: " + error);
            }
        }

        private static bool ReportContextCurrent() => mapReady && MainViewModel.viewModelLoaded &&
            reportRefresh.IsCurrent(preparedSessionId, MainViewModel.Instance.HUDBuildingPanel?.RefBuildingPanel,
                selectedPlayer, GameData.Instance?.app_mode ?? 0, GameData.Instance?.app_sub_mode ?? 0);

        private static void CacheReportValues(HUD_Buildings hud)
        {
            if (ReferenceEquals(reportCacheOwner, hud)) return;
            reportValues.Clear(); reportCacheOwner = hud;
            if (hud == null) return;
            foreach (var root in new FrameworkElement[] { hud.RefReportsPanel, hud.RefReportsFoodPanel,
                hud.RefReportsPopularityPanel, hud.RefReportsPopEventsPanel, hud.RefReportsFearFactorPanel,
                hud.RefReportsPopulationPanel, hud.RefReportsArmy1Panel, hud.RefReportsArmy2Panel,
                hud.RefReportsArmy3Panel, hud.RefReportsArmy4Panel, hud.RefReportsStoresPanel,
                hud.RefReportsWeaponsPanel, hud.RefReportsReligionPanel })
                if (root != null) CacheReportValues(root);
        }

        private static void CacheReportValues(DependencyObject node)
        {
            if (node is FrameworkElement element)
            {
                if (element is TextBlock) AddReportValue(element, element, TextBlock.TextProperty);
                if (element is Image)
                {
                    AddReportValue(element, element, Image.SourceProperty);
                    AddReportValue(element, element, UIElement.VisibilityProperty);
                }
                if (element is Noesis.Path) AddReportValue(element, element, Noesis.Path.DataProperty);
                if (element is WGT_Popularity) AddReportValue(element, element, WGT_Popularity.popValueProperty);
                CacheReportTransform(element, element.RenderTransform);
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) CacheReportValues(VisualTreeHelper.GetChild(node, i));
        }

        private static void AddReportValue(FrameworkElement owner, DependencyObject target, DependencyProperty property) =>
            reportValues.Add(new ReportValue(owner, target, property));

        private static void CacheReportTransform(FrameworkElement owner, Transform transform)
        {
            if (transform is TranslateTransform && BindingOperations.GetBindingBase(transform, TranslateTransform.XProperty) != null)
                AddReportValue(owner, transform, TranslateTransform.XProperty);
            if (transform is TransformGroup group)
                for (int i = 0; i < group.Children.Count; i++) CacheReportTransform(owner, group.Children[i]);
        }

        private static void HoldReportValues()
        {
            CacheReportValues(MainViewModel.Instance.HUDBuildingPanel);
            if (reportDisplay != null) return;
            reportDisplay = new ReportDisplayLease();
            foreach (var value in reportValues)
                if (value.Owner.IsVisible) reportDisplay.Add(value.Target, value.Property);
        }

        private static void ClearReportCache()
        {
            RestoreReportPanel(); reportValues.Clear(); reportCacheOwner = null; reportClock.Clear();
        }
    }
}
