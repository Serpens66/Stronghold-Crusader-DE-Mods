using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static readonly BuildMenuLease dockBuildMenu = new BuildMenuLease();
        private static int buildPageMutation;
        private static BuildMenuSnapshot playerPlacementPage;
        private static HUD_Main placementLayoutHud;
        private static readonly Dictionary<string, FrameworkElement> placementElements = new Dictionary<string, FrameworkElement>();
        private static readonly Dictionary<FrameworkElement, ToolbarProperty> placementLayoutValues = new Dictionary<FrameworkElement, ToolbarProperty>();

        private static bool?[] CaptureBuildTabs(HUD_Main hud)
        {
            RadioButton[] tabs = NormalTabs(hud);
            var values = new bool?[tabs.Length];
            for (int i = 0; i < tabs.Length; i++) values[i] = tabs[i]?.IsChecked;
            return values;
        }

        private static void UpdateActivePlayerBuildMenu()
        {
            var view = MainViewModel.Instance;
            HUD_Main hud = view.HUDmain;
            bool enabled = IsActivePlayerTestSession();
            if (dockBuildMenu.Saved != null && !ReferenceEquals(hud, dockBuildMenu.Saved.Hud))
                dockBuildMenu.Release(); // a new HUD owns its page; never replay old UI state over it
            if (!humanPlacementMode && (!enabled || toolbarMoved || toolsModel?.Expanded != true))
            { ReleaseDockBuildMenu(true); return; }
            if (!dockBuildMenu.CanAcquire(enabled, toolbarMoved, toolsModel?.Expanded == true,
                humanPlacementMode, view.Show_HUD_Main, view.Show_HUD_Building || view.Show_HUD_Troops) || hud == null) return;
            int page = view.buildScreenID;
            if (!GateHudLifecycle.IsBuildScreen(page) && page != DockHudPolicy.BlankBuildScreen) return;
            dockBuildMenu.Acquire(hud, page, view.SubMode, CaptureBuildTabs(hud));
            buildPageMutation++;
            try { hud.NewBuildScreenBlank(); toolbarLayoutDirty = true; }
            catch { dockBuildMenu.Release(); throw; }
            finally { buildPageMutation--; }
        }

        private static void RestoreBuildPage(BuildMenuSnapshot saved)
        {
            if (saved == null || !IsCurrentSession(CurrentSessionId) || !MainViewModel.viewModelLoaded) return;
            var view = MainViewModel.Instance;
            if (!ReferenceEquals(view.HUDmain, saved.Hud) || !view.Show_HUD_Main) return;
            var hud = (HUD_Main)saved.Hud;
            buildPageMutation++;
            try
            {
                view.SubMode = saved.SubMode;
                hud.SetupNewBuildScreen(-1000);
                if (saved.Page == DockHudPolicy.BlankBuildScreen) hud.NewBuildScreenBlank();
                else hud.SetupNewBuildScreen(GateHudLifecycle.IsBuildScreen(saved.Page) ? saved.Page : 1);
                RadioButton[] tabs = NormalTabs(hud);
                if (saved.Tabs != null)
                    for (int i = 0; i < tabs.Length; i++) if (tabs[i] != null) tabs[i].IsChecked = saved.Tabs[i];
                hud.StartScrollSwish(); toolbarLayoutDirty = true;
            }
            finally { buildPageMutation--; }
        }

        private static void ReleaseDockBuildMenu(bool restore)
        {
            bool live = IsCurrentSession(CurrentSessionId) && MainViewModel.viewModelLoaded;
            var view = live ? MainViewModel.Instance : null;
            var saved = dockBuildMenu.Release(restore && live && ReferenceEquals(view.HUDmain, dockBuildMenu.Saved?.Hud),
                view?.Show_HUD_Main == true);
            if (restore && MainViewModel.viewModelLoaded && MainViewModel.Instance.buildScreenID == DockHudPolicy.BlankBuildScreen)
                RestoreBuildPage(saved);
        }

        private static void RememberPlacementLayout(FrameworkElement element)
        {
            if (playerPlacementPage != null && !placementLayoutValues.ContainsKey(element))
                placementLayoutValues.Add(element, new ToolbarProperty(element, UIElement.VisibilityProperty));
        }

        private static void RestorePlacementLayout()
        {
            foreach (var value in placementLayoutValues.Values)
                try { value.Restore(); } catch (Exception error) { LogOnce("TEST_PLACEMENT_LAYOUT_RESTORE: " + error); }
            placementLayoutValues.Clear(); placementElements.Clear(); placementLayoutHud = null;
        }

        private static void ReleaseActivePlayerBuildMenu()
        {
            RestorePlacementLayout();
            var placement = playerPlacementPage; playerPlacementPage = null;
            if (placement != null) humanPlacementMode = false;
            RestoreBuildPage(placement);
            ReleaseDockBuildMenu(true); dockBuildMenu.Reset();
        }

        private static void RetireUnavailablePlacement()
        {
            if (!humanPlacementMode || IsToolsSession()) return;
            humanPlacementMode = false;
            MainControls.instance?.StopAllPlacement();
            RestorePlacementLayout();
            var saved = playerPlacementPage; playerPlacementPage = null;
            // Promotion to spectator must not replay the former human's editable build page.
            bool stillHuman = APIShared.PlayerPerspectiveAPI.GetControlledPlayerId() > 0;
            if (stillHuman) RestoreBuildPage(saved);
            ReleaseDockBuildMenu(stillHuman);
            toolbarDirty = toolbarLayoutDirty = true;
        }
    }
}
