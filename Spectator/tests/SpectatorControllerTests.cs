namespace Spectator {
 public static class ControllerTests {
  static void Check(bool value, string reason = "") { if(!value) throw new System.Exception("Controller invariant failed"); }
  public static void Main() {
   var menu = new ToolbarMenuState();
   Check(!menu.Expanded && menu.View == "", "new HUD starts minimized");
   menu.ToggleExpanded(); Check(menu.Expanded && menu.View == "");
   menu.ToggleView("Goods"); Check(menu.Expanded && menu.View == "Goods");
   menu.ToggleView("Goods"); Check(menu.Expanded && menu.View == "");
   menu.ToggleView("Goods"); menu.ToggleView("Orders"); Check(menu.View == "Orders");
   menu.ToggleView("Orders"); Check(menu.View == "");
   menu.ToggleView("Orders"); menu.Placement(); Check(menu.View == "" && menu.Expanded);
   menu.ToggleExpanded(); Check(!menu.Expanded);
   foreach (int players in new[] {1,4,8}) {
    float barWidth = 46 + players * 34;
    foreach (float canvas in new[] {960f,1920f,2560f,3840f}) {
     float width = ToolbarGeometry.DetailWidth(canvas);
     Check(ToolbarGeometry.DetailLeft(10,barWidth,width,canvas,false)==10,"left anchor");
     float right = canvas - barWidth - 10;
     Check(ToolbarGeometry.DetailLeft(right,barWidth,width,canvas,true)==canvas-width-10,"right anchor");
     Check(ToolbarGeometry.DetailHeight(1080,1020)==50,"scroll instead of moving bar");
     Check(ToolbarGeometry.DetailHeight(1080,1090)==0,"no negative geometry");
    }
   }
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
