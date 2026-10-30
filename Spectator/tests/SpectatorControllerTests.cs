namespace Spectator {
 public static class ControllerTests {
  static void Check(bool value, string reason = "") { if(!value) throw new System.Exception("Controller invariant failed: " + reason); }
  public static void Main() {
   MenuToolTipTests.Run();
   // Real presentation policy: the centre belongs to the new view only on Vanilla's blank page.
   Check(DockHudPolicy.Evaluate(true,true,true,false,false,false,22)==DockHudDisplay.Tools,"idle blank page");
   for(int screen=0;screen<23;screen++) if(screen!=22)
    Check(DockHudPolicy.Evaluate(true,true,true,false,false,false,screen)==DockHudDisplay.Hidden,"normal build pages retain ownership");
   Check(DockHudPolicy.Evaluate(true,true,true,true,false,false,22)==DockHudDisplay.Hidden,"foreign troops can coexist with main flag");
   Check(DockHudPolicy.Evaluate(true,true,true,true,false,true,1)==DockHudDisplay.Hidden,"foreign view wins even during placement");
   Check(DockHudPolicy.Evaluate(true,true,true,false,false,true,1)==DockHudDisplay.Placement,"placement header instead of tools");
   Check(DockHudPolicy.Evaluate(false,true,true,false,false,false,22)==DockHudDisplay.Hidden,"disabled or stale session");
   Check(DockHudPolicy.Evaluate(true,false,true,false,false,false,22)==DockHudDisplay.Hidden,"global hide");
   Check(DockHudPolicy.Evaluate(true,true,false,false,false,false,22)==DockHudDisplay.Hidden,"reports/building owner");
   Check(DockHudPolicy.Evaluate(true,true,true,false,true,false,22)==DockHudDisplay.Hidden,"editor terrain not empty");
   var menu = new ToolbarMenuState();
   Check(!menu.Expanded && menu.View == "Goods", "new HUD starts compact with a useful initial page");
   menu.ToggleExpanded(); Check(menu.ShowDetails);
   menu.ToggleView("Orders"); Check(menu.ShowDetails && menu.View == "Orders");
   menu.SetPlacement(true); Check(!menu.ShowDetails && menu.Expanded && menu.View == "Orders", "placement hides content without losing navigation");
   menu.SetPlacement(false); Check(menu.ShowDetails && menu.View == "Orders", "stop restores the previous page");
   menu.ToggleExpanded(); menu.SetPlacement(true); menu.SetPlacement(false);
   Check(!menu.Expanded, "placement from compact restores compact");
   menu.ToggleView("Goods"); menu.ToggleView("Goods"); Check(menu.View == "Goods" && menu.Expanded);
   menu.ToggleView("unknown"); Check(menu.View == "Goods", "unknown navigation leaves a usable page");
   ToolbarLayoutTests.Run(Check); SpectatorRuntime.TestToolbarProperty(Check);
   foreach(float menuScale in new[]{1f,1.333333f,2f,2.75f})
   foreach(float popupScale in new[]{1f,1.5f,2f}) {
    Check(ToolbarGeometry.TryTooltipScale(menuScale,menuScale,popupScale,popupScale,out float sx,out float sy));
    Check(System.Math.Abs(ToolbarGeometry.MenuFontSize*sy*popupScale-14*menuScale)<0.001,"popup and menu rendered font sizes match");
   }
   Check(!ToolbarGeometry.TryTooltipScale(0,1,1,1,out _,out _),"wait for valid layout");
   Check(!ToolbarGeometry.TryTooltipScale(1,1,1,0,out _,out _));
   Check(!ToolbarGeometry.TryTooltipScale(float.NaN,1,1,1,out _,out _));
   Check(!ToolbarGeometry.TryTooltipScale(float.PositiveInfinity,1,1,1,out _,out _));
   short[] modes = new short[9], targets = new short[9], requesters = new short[9];
   modes[2]=1; targets[2]=4; requesters[2]=7;
   var order = PresentedAiOrder.Read(2,modes,targets,requesters);
   Check(order.Mode==1 && order.Target==4,"attack uses target, not requester");
   modes[2]=2; order=PresentedAiOrder.Read(2,modes,targets,requesters);
   Check(order.Mode==2 && order.Target==7,"defend uses protected requester");
   modes[2]=0; Check(PresentedAiOrder.Read(2,modes,targets,requesters).Mode==0,"native cancellation removes pip");
   modes[2]=2; requesters[2]=99; Check(PresentedAiOrder.Read(2,modes,targets,requesters).Target==0,"invalid target never indexed");
   Check(PresentedAiOrder.Read(-1,modes,targets,requesters).Mode==0);
   Check(PresentedAiOrder.Read(8,new short[2],null,null).Mode==0);
   Check(!SpectatorFeatureActivation.HasToolbar(false,false,false));
   foreach(bool toolbarTools in new[]{false,true})
   foreach(bool toolbarPerspective in new[]{false,true})
   foreach(bool toolbarStatistics in new[]{false,true}) {
    Check(!SpectatorFeatureActivation.HasToolbar(toolbarTools,toolbarPerspective,toolbarStatistics,true),"editor never exposes spectator toolbar or its command context");
    Check(SpectatorFeatureActivation.HasToolbar(toolbarTools,toolbarPerspective,toolbarStatistics,false)==(toolbarTools||toolbarPerspective||toolbarStatistics),"normal toolbar activation stays unchanged");
   }
   foreach (bool host in new[] { false, true })
   foreach (bool client in new[] { false, true })
   foreach (bool perspective in new[] { false, true })
   foreach (bool tools in new[] { false, true }) {
    var rights = new SpectatorFeatureActivation(host, client, perspective, true, true, tools);
    Check(SpectatorFeatureActivation.ForceArmyReport(false, true, rights.Perspective) == !(host && perspective), "placement/tools must never grant reports");
    Check(SpectatorFeatureActivation.ForceArmyReport(true, true, rights.Perspective) == !(host && perspective), "enabled perspective owns report access independently of frozen controls");
    Check(SpectatorFeatureActivation.ForceArmyReport(true, false, rights.Perspective) && !SpectatorFeatureActivation.ForceArmyReport(false, false, rights.Perspective), "ordinary player keeps Vanilla navigation");
   }
   Check(ToolSessionPolicy.CanMutate(true,true,true,true,false,true,false));
   Check(!ToolSessionPolicy.CanMutate(true,true,true,true,true,true,false));
   Check(!ToolSessionPolicy.CanMutate(true,true,true,true,false,true,true));
   Check(!ToolSessionPolicy.CanMutate(true,false,true,true,false,true,false));
   Check(SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,true,true,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(false,true,false,false,true,true,true,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,true,false,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,false,true,8));
   Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1,true)==2);
   Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1,false)==1);
            Check(SpectatorDefeatPolicy.ShouldLatchPlayerLordDeath(3, 3) &&
                    !SpectatorDefeatPolicy.ShouldLatchPlayerLordDeath(3, 2) &&
                    !SpectatorDefeatPolicy.ShouldLatchPlayerLordDeath(0, 0) &&
                    !SpectatorDefeatPolicy.ShouldLatchPlayerLordDeath(9, 9),
                "only a valid local-player lord event sets the spectator latch");
            Check(SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(
                    true, true, false, false, true, true, true, 3) &&
                    !SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(
                        false, true, false, false, true, true, true, 3) &&
                    !SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(
                        true, true, false, false, true, true, false, 3) &&
                    !SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(
                        true, true, false, false, true, false, true, 3),
                "spectator promotion remains fail-closed and requires the defeat-event latch");
            Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1, true) == 2,
                "eliminated-player spectator promotion presents Vanilla victory as defeat");
            Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(2, true) == 2,
                "eliminated-player spectator promotion preserves an existing defeat");
            Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(0, true) == 0 &&
                    SpectatorDefeatPolicy.ResolvePresentedGameOverState(7, true) == 7,
                "spectator promotion preserves running and unknown game-over states");
            Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1, false) == 1,
                "ordinary victory and initial spectators retain Vanilla presentation");

   Check(SpectatorDefeatPolicy.IsStatisticsViewer(false,2,false));
   Check(!SpectatorDefeatPolicy.IsStatisticsViewer(false,2,true));
   Check(!SpectatorDefeatPolicy.IsStatisticsViewer(false,-1,false));
   Check(SpectatorDefeatPolicy.ConfirmIdentity(2,2,2));
   Check(!SpectatorDefeatPolicy.ConfirmIdentity(2,3,2));
   Check(!SpectatorDefeatPolicy.ConfirmIdentity(2,2,3));
   var p = new StatisticsPreviewController(); var first = new object(); var second = new object();
   Check(!p.Acquire(first)); p.Start(1); Check(p.Acquire(first)); long old=p.Generation;
   Check(p.Acquire(second)); Check(!p.Release(old,first) && p.Active);
   Check(!p.Release(p.Generation,first) && !p.Active);
   Check(p.Acquire(first)); p.EndVanilla(); Check(!p.Active && !p.Acquire(first));
   p.Start(2); Check(p.Acquire(second)); Check(!p.Release(old,second));
   Check(p.Release(p.Generation,second) && !p.Active);
  }
 }
}
