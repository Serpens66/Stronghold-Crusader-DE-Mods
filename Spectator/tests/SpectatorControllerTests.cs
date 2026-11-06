namespace Spectator {
 public static class ControllerTests {
  static void Check(bool value, string reason = "") { if(!value) throw new System.Exception("Controller invariant failed: " + reason); }
  public static void Main() {
   PerspectiveInitializationWaitTests.Run(Check);
   SpectatorDefeatRuntimeTests.Run(Check);
   MenuToolTipTests.Run();
   StatusInlineFormatTests.Run(Check);
   // Bound amount edits are frequent; they must not fan out into imperative HUD work.
   foreach(string property in new[]{"Amount","CanCredit","SelectedGood","SelectedGoodKey","ForceAcceptance"})
    Check(ToolbarRefreshPolicy.ForProperty(property)==ToolbarRefresh.None,"bound input updates without toolbar rebuild: "+property);
   Check(ToolbarRefreshPolicy.ForProperty("Status")==ToolbarRefresh.Status,"rich status updates independently of player cards and geometry");
   Check((ToolbarRefreshPolicy.ForProperty("ExpandedVisibility")&(ToolbarRefresh.Layout|ToolbarRefresh.State))==
    (ToolbarRefresh.Layout|ToolbarRefresh.State),"expanding tools wakes layout and build-menu reconciliation");
   Check(ToolbarRefreshPolicy.ForProperty("Powers")==ToolbarRefresh.State,"power metadata refresh wakes leased vanilla controls");
   Check(ToolbarRefreshPolicy.ForProperty(null)==(ToolbarRefresh.Status|ToolbarRefresh.Layout|ToolbarRefresh.State),"unspecified model change refreshes all imperative consumers");
   var refresh=new PresentationRefreshState();
   Check(refresh.ObservePerspective(true,2)&&refresh.TakeStatusCheck(0,1000),"initial perspective gets a complete reconcile");
   refresh.ContentPending=refresh.RosterPending=refresh.StatusPending=refresh.LayoutPending=false;
   Check(!refresh.ObservePerspective(true,2)&&!refresh.TakeStatusCheck(10,1000),"unchanged render identity does not force a status refresh");
   Check(refresh.ObservePerspective(true,3)&&refresh.ContentPending&&refresh.StatePending,
    "external view change between status intervals invalidates content and eligibility immediately");
   Check(refresh.TakeStatusCheck(10,1000),"external view change bypasses the status delay");
   refresh.ContentPending=refresh.RosterPending=refresh.StatusPending=refresh.LayoutPending=false;
   for(int now=20;now<110;now+=10){refresh.RosterPending=true;Check(!refresh.TakeStatusCheck(now,1000),"snapshot bursts keep the existing status deadline");}
   Check(refresh.TakeStatusCheck(110,1000)&&refresh.RosterPending,"latest snapshot survives until the due reconcile");
   refresh.ContentPending=refresh.RosterPending=refresh.StatusPending=refresh.LayoutPending=false;
   refresh.Failed(120,1000);
   Check(refresh.ContentPending&&refresh.RosterPending&&refresh.StatePending&&refresh.StatusPending&&refresh.LayoutPending,
    "failure restores work consumed by earlier consumers before a later UI exception");
   Check(!refresh.Ready(219)&&refresh.Ready(220)&&refresh.RetryDelay(120,1000)==System.TimeSpan.FromMilliseconds(100),
    "failed reconcile retries within the status bound without a per-frame exception loop");
   Check(refresh.ObservePerspective(false,-1),"identity guard still observes role loss during cleanup retry");
   refresh.Reset();Check(refresh.Ready(0)&&refresh.ContentPending&&refresh.TakeStatusCheck(0,1000),"new mission clears retry backoff and requests fresh content");
   for(int bits=0;bits<128;bits++) foreach(int occupants in new[]{0,1,8}) {
    bool eligible=(bits&1)!=0,ui=(bits&2)!=0,ingame=(bits&4)!=0,extras=(bits&8)!=0;
    bool briefing=(bits&16)!=0,statistics=(bits&32)!=0,gameMenu=(bits&64)!=0;
    Check(DockHudPolicy.CanShowToolbar(eligible,ui,ingame,extras,briefing,statistics,gameMenu,occupants)==
     (bits==15 && occupants>0),"Only an eligible world HUD with occupied slots may expose the toolbar");
   }
   Check(DockHudPolicy.CanShowToolbar(true,true,true,true,false,false,false,8),"Simulation pause alone does not hide the world HUD");

   foreach(bool tools in new[]{false,true}) foreach(bool roster in new[]{false,true})
   foreach(bool placement in new[]{false,true}) foreach(bool floating in new[]{false,true})
   foreach(string hud in new[]{"Build","OwnTroops","ForeignTroops","Building","Other"})
    Check(DockHudPolicy.CanShowControl(tools,roster)==(tools&&roster),
     "Tools/Stop follows the roster permission across HUD="+hud+", Place="+placement+", floating="+floating);

   var feedback = new ToolbarFeedback();
   Check(feedback.ForPage("Goods") == "" && feedback.ForPage("Orders") == "", "no initial placeholder or tooltip content");
   feedback.Set("Goods", "Credited 100"); feedback.Set("Orders", "Forced attack");
   Check(feedback.ForPage("Goods") == "Credited 100" && feedback.ForPage("Orders") == "Forced attack", "orders preserve goods feedback");
   feedback.Set("Goods", "Debit failed");
   Check(feedback.ForPage("Orders") == "Forced attack" && feedback.ForPage("Goods") == "Debit failed", "failure stays on its originating page");
   feedback.Set("Orders", null);
   Check(feedback.ForPage("Orders") == "" && feedback.ForPage("Goods") == "Debit failed", "clearing one page leaves the other intact");
   Check(DockHudPolicy.Evaluate(true,true,false,false)==DockHudDisplay.Tools,"free native centre");
   Check(DockHudPolicy.Evaluate(true,true,true,false)==DockHudDisplay.Hidden,"filled construction, reports and foreign troops own the centre");
   Check(DockHudPolicy.Evaluate(true,true,true,true)==DockHudDisplay.Hidden,"foreign centre still wins during docked placement");
   Check(DockHudPolicy.Evaluate(true,true,false,true)==DockHudDisplay.Placement,"placement header instead of tools");
   Check(DockHudPolicy.Evaluate(false,true,false,false)==DockHudDisplay.Hidden,"disabled/stale session");
   Check(DockHudPolicy.Evaluate(true,false,false,false)==DockHudDisplay.Hidden,"global hide");
   foreach(DockHudDisplay display in System.Enum.GetValues(typeof(DockHudDisplay)))
   {
    Check(DockHudPolicy.CanShowDetails(true,display),"floating tools ignore native centre ownership");
    Check(DockHudPolicy.CanShowDetails(false,display)==(display==DockHudDisplay.Tools),"docked tools follow actual native centre ownership");
   }
   for(int page=0;page<23;page++)
   {
    Check(DockHudPolicy.CenterIsOccupied(true,page,false,false)==(page!=22),"only the actual blank page permits category overdraw");
    Check(!DockHudPolicy.CenterIsOccupied(true,page,true,false),"own placement keeps its stop control available on every construction page");
    Check(DockHudPolicy.CenterIsOccupied(false,page,false,true),"foreign report/troup content owns the centre independently of main HUD flag");
   }
   var menu = new ToolbarMenuState();
   Check(!menu.Expanded && menu.View == "Goods", "new HUD starts compact with a useful initial page");
   Check(!menu.ToolsAvailable && !menu.CanExpand && !menu.CanInvoke("Collapse"), "new HUD cannot flash or open tools before its role is reconciled");
   menu.ToggleExpanded(); menu.ToggleView("Orders"); menu.SetPlacement(true);
   Check(!menu.Expanded && !menu.Placing && !menu.ShowDetails, "direct commands cannot open or place with disabled tools");
   Check(menu.CanInvoke("ToggleDesign"), "roster design remains useful without test tools");
   menu.SetToolsAvailable(true);
   menu.ToggleExpanded(); Check(menu.ShowDetails);
   menu.ToggleExpanded(false); Check(menu.Expanded,"options reopen tools temporarily hidden behind a Vanilla page with a single click");
   menu.ToggleView("Orders"); Check(menu.ShowDetails && menu.View == "Orders");
   menu.SetPlacement(true); Check(!menu.ShowDetails && menu.Expanded && menu.View == "Orders", "placement hides content without losing navigation");
   menu.SetPlacement(false); Check(menu.ShowDetails && menu.View == "Orders", "stop restores the previous page");
   menu.ToggleExpanded(); menu.SetPlacement(true); menu.SetPlacement(false);
   Check(!menu.Expanded, "placement from compact restores compact");
   menu.ToggleView("Goods"); menu.ToggleView("Goods"); Check(menu.View == "Goods" && menu.Expanded);
   menu.ToggleView("unknown"); Check(menu.View == "Goods", "unknown navigation leaves a usable page");
   Check(menu.ControlLabel("Tools","Zurück")=="Zurück" && menu.ControlFontSize==12,"an open docked menu shows the localized Back caption");
   Check(menu.ControlHelpKey(true)=="Tools.BackBuildHelp" && menu.ControlHelpKey(false)=="Tools.BackHelp",
    "Back explains the return to the player's own build menu only for the applicable docked role");
   for(int move=0;move<8;move++) {
    bool wasExpanded=menu.Expanded; string page=menu.View;
    Check(menu.SetFloating(true) && menu.Floating && menu.ControlFontSize==18,"floating control uses the larger symbol independently of colour theme");
    Check(menu.ControlLabel("Tools","Zurück")== (wasExpanded?"\u2212":"+"),"floating caption follows the existing expansion state");
    Check(menu.ControlHelpKey(true)==(wasExpanded?"Tools.BackHelp":"Tools.CollapseHelp"),"floating help never promises a native build-menu transition");
    Check(!menu.SetFloating(true) && menu.View==page && menu.Expanded==wasExpanded,"unchanged form is silent and preserves navigation");
    menu.SetPlacement(true); Check(menu.Placing && !menu.ShowDetails,"placement still uses the separate stop control");
    Check(menu.SetFloating(false) && menu.ControlLabel("Tools","Zurück")== (menu.Expanded?"Zurück":"Tools") && menu.ControlFontSize==12 && menu.Placing,
     "docking during placement preserves the separate Stop and the caption for the remembered menu state");
    menu.SetPlacement(false); menu.ToggleExpanded();
   }
   // The production state invokes the external HUD boundary before publishing an open
   // menu. Doubles model HUD transitions; they do not duplicate the expansion algorithm.
   foreach(string hud in new[]{"Build","OwnTroops","ForeignTroops","Building","Eliminated"})
   foreach(bool floating in new[]{false,true}) {
    var clickMenu=new ToolbarMenuState(); clickMenu.SetToolsAvailable(true); clickMenu.SetFloating(floating);
    bool nativeMain=hud=="Build"||hud=="Eliminated"; bool gateLease=hud=="Building";
    int transitions=0;
    System.Func<bool> prepare=()=>{Check(!clickMenu.Expanded,"HUD transition precedes first expansion");nativeMain=true;gateLease=false;transitions++;return true;};
    clickMenu.ToggleExpanded(nativeMain,prepare);
    Check(clickMenu.Expanded && transitions==(floating?0:1) && clickMenu.ControlLabel("Tools","Back")== (floating?"\u2212":"Back"),"one Tools click opens from "+hud+" with unchanged floating behavior");
    Check(floating || nativeMain&&!gateLease,"docked Tools retires the building lease and reaches the build HUD first");
    clickMenu.ToggleExpanded(true,()=>{throw new System.Exception("collapse must not change the native HUD");});
    Check(!clickMenu.Expanded && clickMenu.ControlLabel("Tools","Back")== (floating?"+":"Tools"),"Back collapses without a HUD transition and restores Tools");
    Check(clickMenu.ControlHelpKey(true)=="Tools.CollapseHelp","closed Tools restores its opening help after Back");
    clickMenu.SetPlacement(true);Check(!clickMenu.CanExpand&&!clickMenu.ShowDetails,"Place retains its separate Stop control");
    clickMenu.SetPlacement(false);Check(clickMenu.CanExpand,"Stop returns the Tools control");
   }
   var hiddenMenu=new ToolbarMenuState();hiddenMenu.SetToolsAvailable(true);hiddenMenu.ToggleExpanded();
   int hiddenTransition=0;hiddenMenu.ToggleExpanded(false,()=>{hiddenTransition++;Check(hiddenMenu.Expanded);return true;});
   Check(hiddenTransition==1&&hiddenMenu.Expanded,"Tools reclaims a hidden expanded dock in one click");
   var failedMenu=new ToolbarMenuState();failedMenu.SetToolsAvailable(true);
   try {failedMenu.ToggleExpanded(false,()=>{throw new System.InvalidOperationException("HUD unavailable");});}catch(System.InvalidOperationException){}
   Check(!failedMenu.Expanded,"a failed HUD transition cannot publish the first expansion");
   failedMenu.ToggleExpanded(false,()=>false);Check(!failedMenu.Expanded,"an unavailable native build HUD refuses expansion without an exception");
   failedMenu.SetToolsAvailable(false);failedMenu.ToggleExpanded(false,()=>{throw new System.Exception("disabled Tools must stay inert");});
   ToolbarLayoutTests.Run(Check); SpectatorRuntime.TestToolbarProperty(Check);
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
    Check(SpectatorFeatureActivation.ForceArmyReport(false, true, rights.Perspective) == !(client && perspective), "placement/tools must never grant reports");
    Check(SpectatorFeatureActivation.ForceArmyReport(true, true, rights.Perspective) == !(client && perspective), "enabled perspective owns report access independently of frozen controls");
    Check(SpectatorFeatureActivation.ForceArmyReport(true, false, rights.Perspective) && !SpectatorFeatureActivation.ForceArmyReport(false, false, rights.Perspective), "ordinary player keeps Vanilla navigation");
    // Control follows the portrait selection: it is opt-in and needs the local perspective feature.
    Check(!rights.Control, "spectator input control is off by default");
    Check(new SpectatorFeatureActivation(host, client, perspective, true, true, tools, control: true).Control == (client && perspective),
     "spectator input control requires client features and the perspective option");
   }
   Check(ToolSessionPolicy.CanMutate(true,true,true,true,false,true,false));
   Check(!ToolSessionPolicy.CanMutate(true,true,true,true,true,true,false));
   Check(!ToolSessionPolicy.CanMutate(true,true,true,true,false,true,true));
   Check(!ToolSessionPolicy.CanMutate(true,false,true,true,false,true,false));
   foreach(bool spectatorTools in new[]{false,true})
   foreach(bool activeTools in new[]{false,true})
   foreach(bool editor in new[]{false,true})
   foreach(bool spectator in new[]{false,true})
   foreach(bool active in new[]{false,true}) {
    bool enabled = ToolSessionPolicy.Enabled(spectatorTools,activeTools,editor,spectator,active);
    Check(enabled==(!editor&&(spectator?spectatorTools:active&&activeTools)),"test mode distinguishes active players from spectators; the toolbar-less editor has no tools");
    Check(!ToolSessionPolicy.CanMutate(enabled,true,true,true,true,true,false),"real network veto is independent of every local setting");
    Check(!ToolSessionPolicy.CanMutate(enabled,true,true,true,false,true,true),"director multiplayer veto remains fail-closed");
   }
   foreach(string role in new[]{"active","original spectator","eliminated spectator"})
   foreach(bool spectatorTools in new[]{false,true}) foreach(bool activeTools in new[]{false,true})
   foreach(bool editor in new[]{false,true}) foreach(bool multiplayer in new[]{false,true}) {
    bool observer=role!="active";
    bool enabled=ToolSessionPolicy.Enabled(spectatorTools,activeTools,editor,observer,role!="original spectator");
    bool available=ToolSessionPolicy.CanMutate(enabled,true,true,true,multiplayer,true,multiplayer);
    bool expected=!editor&&!multiplayer&&(observer?spectatorTools:activeTools);
    var roleMenu=new ToolbarMenuState(); roleMenu.SetToolsAvailable(available);
    Check(roleMenu.CanExpand==expected && roleMenu.CanInvoke("Collapse")==expected,
     "button/open command follow only the current role's setting: "+role);
    roleMenu.ToggleExpanded(); Check(roleMenu.ShowDetails==expected,"detail window follows the same role permission");
    roleMenu.ToggleView("Orders"); roleMenu.SetPlacement(true);
    Check(roleMenu.Placing==expected,"disabled entrypoints cannot start placement");
    roleMenu.SetFloating(true); roleMenu.SetToolsAvailable(false);
    Check(!roleMenu.Expanded&&!roleMenu.Placing&&!roleMenu.ShowDetails&&!roleMenu.CanExpand,
     "losing permission closes all tool surfaces, including placement stop state");
    Check(roleMenu.Floating&&roleMenu.CanInvoke("ToggleDesign"),"tool removal preserves floating roster/design state");
    foreach(string command in new[]{"Collapse","Goods","Orders","Placement","TogglePowers","Credit","Attack","Defend","Cancel"})
     Check(!roleMenu.CanInvoke(command),"hidden tools reject direct command: "+command);
    roleMenu.SetToolsAvailable(available);
    Check(!roleMenu.Expanded&&!roleMenu.Placing,"re-enabled tools never reopen an old window or placement");
   }
   foreach(bool host in new[]{false,true}) foreach(bool client in new[]{false,true})
   foreach(bool spectatorTools in new[]{false,true}) foreach(bool activeTools in new[]{false,true}) {
    var activation = new SpectatorFeatureActivation(host,client,true,true,true,spectatorTools,activeTools);
    Check(activation.SingleplayerTest==(client&&activeTools) && activation.TestTools==(client&&spectatorTools),"test options are independent and follow only local activation");
    Check(activation.Perspective==client && activation.Statistics==client && activation.Overview==client && activation.Defeat==host,"local views follow client activation; promotion follows host activation");
   }
   foreach(bool occupied in new[]{false,true}) foreach(bool ai in new[]{false,true}) foreach(bool self in new[]{false,true}) {
    Check(ToolSessionPolicy.CanSelect(false,true,occupied,ai,self)==(occupied&&(ai||self)),"active testing includes self and AI, no other human slots");
    Check(ToolSessionPolicy.CanSelect(true,false,occupied,ai,self)==occupied,"spectator roster remains unchanged");
    Check(!ToolSessionPolicy.CanSelect(false,false,occupied,ai,self),"no testing roster without a permitted session");
   }
   // Roster exclusion uses only a readable defeat of the current session; no snapshot/other session lists the slot.
   Check(SpectatorDefeatPolicy.IsRosterEliminated(5,5,true,true),"current readable defeat leaves the roster");
   Check(!SpectatorDefeatPolicy.IsRosterEliminated(5,4,true,true) && !SpectatorDefeatPolicy.IsRosterEliminated(0,0,true,true),"stale or missing session never hides a player");
   Check(!SpectatorDefeatPolicy.IsRosterEliminated(5,5,false,true) && !SpectatorDefeatPolicy.IsRosterEliminated(5,5,true,false),"unreadable or living players stay listed");
   var build = new BuildMenuLease(); var originalHud = new object(); var nextHud = new object();
   Check(BuildMenuLease.CanBorrow(false,true,true),"confirmed eliminated observer with spectator tools can open the dock on its retained build page");
   Check(!BuildMenuLease.CanBorrow(false,true,false),"elimination alone grants no tools");
   Check(!BuildMenuLease.CanBorrow(false,false,true),"tools option alone grants no active-player build-page lease");
   Check(BuildMenuLease.CanBorrow(true,false,false),"existing own-build-menu path is preserved");
   bool observerTools=BuildMenuLease.CanBorrow(false,true,true);
   Check(build.CanAcquire(observerTools,false,true,false,true,false),"eliminated observer opens docked tools without enabling input control");
   Check(!build.CanAcquire(observerTools,false,true,false,false,true),"troop HUD blocks the eliminated observer build-page lease");
   Check(!build.CanAcquire(observerTools,true,true,false,true,false),"floating observer tools do not replace the build page");
   foreach(bool active in new[]{false,true}) foreach(bool floating in new[]{false,true})
   foreach(bool expanded in new[]{false,true}) foreach(bool placing in new[]{false,true})
   foreach(bool main in new[]{false,true}) foreach(bool foreign in new[]{false,true})
    Check(build.CanAcquire(active,floating,expanded,placing,main,foreign)==(active&&!floating&&expanded&&!placing&&main&&!foreign),"only authorized docked tools may borrow a normal build page");
   bool?[] savedTabs = {true,false,null,false,false,false};
   build.Acquire(originalHud,7,3,savedTabs); savedTabs[0]=false;
   Check(build.Saved.Page==7 && build.Saved.SubMode==3 && build.Saved.Tabs[0]==true,"original page/submode and independent tab snapshot survive placement");
   build.ObserveNativePage(originalHud,4,true); Check(build.Saved!=null,"placement pages do not erase the original tool snapshot");
   build.ObserveNativePage(nextHud,4,false); Check(build.Saved!=null,"another HUD cannot claim the saved layout");
   Check(build.Release(true,false)==null && build.Saved!=null,"closing tools while reports are open defers restoration without losing the original page");
   var restored = build.Release(); Check(restored.Page==7 && ReferenceEquals(restored.Hud,originalHud) && build.Saved==null,"release transfers exactly one restoration snapshot");
   build.Acquire(originalHud,9,4,savedTabs); build.ObserveNativePage(originalHud,2,false);
   Check(build.Saved==null && build.Suppressed && !build.CanAcquire(true,false,true,false,true,false),"choosing a native category keeps it open instead of reclaiming it next frame");
   build.Request(); Check(build.CanAcquire(true,false,true,false,true,false),"an explicit tool navigation request can reopen the dock");
   build.Acquire(originalHud,5,0,savedTabs); build.Reset(); Check(build.Saved==null && !build.Suppressed,"HUD/session cleanup drops all ownership");
   var observerHud = new ObserverBuildHudState(); int clearedPages = 0;
   System.Action<object> clearPage = current => { Check(ReferenceEquals(current,originalHud),"clear uses the actual HUD"); clearedPages++; };
   Check(!observerHud.TryClear(originalHud,5,7,false,clearPage),"a living participant or genuine Place page stays intact");
   Check(observerHud.TryClear(originalHud,5,7,true,clearPage) && clearedPages==1,"promotion clears a retained build page immediately");
   Check(!observerHud.TryClear(originalHud,5,22,true,clearPage),"an already cleared observer page needs no repeated UI writes");
   build.Acquire(originalHud,22,48,savedTabs);
   Check(observerHud.TryClear(originalHud,5,4,true,current=>{
    Check(!observerHud.TryClear(current,5,0,true,clearPage),"nested setup callbacks do not recurse while clearing"); clearedPages++;
   }),"a later Vanilla category is cleared again");
   Check(build.Saved!=null && !build.Suppressed,"normalization before ObserveNativePage retains the docked Tools lease");
   Check(observerHud.TryClear(nextHud,5,22,true,current=>clearedPages++),"a replacement HUD clears its own icons even when the shared page ID is22");
   Check(observerHud.TryClear(nextHud,6,22,true,current=>clearedPages++),"a reused HUD is cleared in a replacement mission");
   bool clearFailed=false;
   try { observerHud.TryClear(originalHud,6,22,true,current=>{throw new System.InvalidOperationException("HUD not ready");}); }
   catch(System.InvalidOperationException){clearFailed=true;}
   Check(clearFailed && observerHud.TryClear(originalHud,6,22,true,clearPage),"a failed clear never consumes the replacement HUD's pending work");
   Check(!observerHud.TryClear(null,6,1,true,clearPage) && !observerHud.TryClear(originalHud,0,1,true,clearPage),"unavailable HUD or ended mission never receives UI writes");
   observerHud.Reset();
   Check(observerHud.TryClear(originalHud,6,22,true,clearPage),"HUD detach drops the old reference and a rebind clears again");
   Check(DockHudPolicy.CanShowControl(true,true),"blank observer page retains roster Tools");
   Check(DockHudPolicy.CanShowControl(true,true),"an eliminated observer retains roster Tools while another native HUD is shown");
   Check(SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,true,true,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(false,true,false,false,true,true,true,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,true,false,8));
   Check(!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(true,true,false,false,true,false,true,8));
   Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1,true)==2);
   Check(SpectatorDefeatPolicy.ResolvePresentedGameOverState(1,false)==1);
            Check(SpectatorDefeatPolicy.IsLocalElimination(12, 12, 3, 3, true, true),
                "a current confirmed local loss or lord death grants eligibility, including a loaded baseline");
            Check(!SpectatorDefeatPolicy.IsLocalElimination(12, 11, 3, 3, true, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(0, 0, 3, 3, true, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(12, 12, 3, 2, true, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(12, 12, 0, 0, true, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(12, 12, 9, 9, true, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(12, 12, 3, 3, false, true) &&
                    !SpectatorDefeatPolicy.IsLocalElimination(12, 12, 3, 3, true, false),
                "stale worlds, foreign players, unreadable states and missing elimination evidence grant no role");
            foreach (bool original in new[] { false, true })
            foreach (bool eliminated in new[] { false, true })
            foreach (bool perspective in new[] { false, true })
            foreach (bool tools in new[] { false, true })
            foreach (bool activeTest in new[] { false, true })
            {
                bool observer = SpectatorDefeatPolicy.IsSpectatorObserver(original, eliminated);
                Check(observer == (original || eliminated), "confirmed eliminated participants and original spectators share observer rights");
                bool enabled = ToolSessionPolicy.Enabled(tools, activeTest, false, observer, !original);
                Check(enabled == (observer ? tools : activeTest), "eliminated tools follow spectator tools independently from perspective and active-player test mode");
                Check(SpectatorFeatureActivation.ForceArmyReport(false, observer, perspective) == (observer && !perspective),
                    "observer reports require perspective permission, including while placement releases the freeze");
                Check(!ToolSessionPolicy.CanMutate(enabled, true, true, true, true, true, false),
                    "no observer tools in multiplayer even with every local option enabled");
            }
            foreach (bool vanillaFreeze in new[] { false, true })
            foreach (bool eliminated in new[] { false, true })
            foreach (bool authorizedPlacement in new[] { false, true })
            {
                bool run = SpectatorFeatureActivation.ResolveControlFreeze(vanillaFreeze, eliminated, authorizedPlacement, out bool effective);
                Check(run == (!authorizedPlacement || !vanillaFreeze) &&
                    effective == (authorizedPlacement ? vanillaFreeze : vanillaFreeze || eliminated),
                    "only authorized placement suppresses Vanilla freeze and permits an explicit unfreeze");
            }
            bool frozen = true;
            if (SpectatorFeatureActivation.ResolveControlFreeze(false, true, true, out bool unfreeze)) frozen = unfreeze;
            Check(!frozen, "the placement recovery call executes false once instead of refreezing every frame");
            Check(!SpectatorFeatureActivation.ResolveControlFreeze(true, true, true, out _),
                "Vanilla cannot refreeze an authorized placement session afterwards");
            Check(!SpectatorFeatureActivation.CanOpenReports(true, true) &&
                SpectatorFeatureActivation.CanOpenReports(true, false) &&
                SpectatorFeatureActivation.CanOpenReports(false, true),
                "uncertain observer views cannot reopen reports; ordinary Vanilla report access is unchanged");
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
