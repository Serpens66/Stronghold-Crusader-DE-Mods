from pathlib import Path
p=Path('Testmods/SpectatorEditorBuildTest/src/TestToolsGates.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('using System;','using System;\nusing System.Collections.Generic;\nusing System.Threading;')
s=s.replace('private static GateSelection selectedGate, observedGate;', '''private static GateSelection selectedGate, observedGate;
        private static GateSelection pendingGate;
        private static bool gateIntentPending, buildingOwnSetup;
        private static long toolsStateRevision, gateIntentRevision;
        private static MainViewModel gateHudOwner;
        private static bool savedMain, savedTroops, savedBuilding, savedBook;
        private static string savedRollover;
        private static readonly List<ToolbarProperty> gateProperties = new List<ToolbarProperty>();
        private static OptionalGateAutomation gateAutomation;
        private static bool gateAutomationResolved;

        // The engine input callback only captures intent. Rendering waits for a completed PlayState update.
        private static void AfterToolsPlayState() => Interlocked.Increment(ref toolsStateRevision);

        private static void BeforeVanillaHudTransition()
        {
            if (!buildingOwnSetup) ReleaseGateHud(false);
        }

        private static void SaveGateVisuals(DependencyObject element)
        {
            if (element is UIElement visual) gateProperties.Add(new ToolbarProperty(visual, UIElement.VisibilityProperty));
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) SaveGateVisuals(VisualTreeHelper.GetChild(element, i));
        }

        private static void ReleaseGateHud(bool restoreFlags)
        {
            MainViewModel owner = gateHudOwner;
            gateHudOwner = null;
            selectedGate = null;
            if (owner == null) return;
            foreach (ToolbarProperty property in gateProperties) property.Restore();
            gateProperties.Clear();
            if (restoreFlags && MainViewModel.viewModelLoaded && ReferenceEquals(owner, MainViewModel.Instance))
            {
                owner.Show_HUD_Main = savedMain; owner.Show_HUD_Troops = savedTroops;
                owner.Show_HUD_Building = savedBuilding; owner.Show_HUD_Book = savedBook;
                owner.RollOverText_Margin = savedRollover;
            }
            log.LogInfo("TEST_GATE_HUD_RELEASE restore=" + restoreFlags + " revision=" + toolsStateRevision);
        }''')
a=s.index('            MainViewModel.Instance.InBuildingGameAction();')
b=s.index('        private static unsafe void ShowTestGatePanel()',a)
s=s[:a]+'''            pendingGate = selectedGate;
            selectedGate = null;
            gateIntentRevision = Interlocked.Read(ref toolsStateRevision) + 1;
            gateIntentPending = true;
        }

        private static void ClearGateSelection()
        {
            gateIntentPending = false; pendingGate = null;
            ReleaseGateHud(true);
        }

        private static unsafe void BeforeGatePanel(ref int overridePanel, ref int overrideType)
        {
            if (!buildingOwnSetup) { ReleaseGateHud(false); return; }
            if (!CurrentGate(selectedGate, out _)) return;
            overridePanel = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE ? 37 : 36;
            overrideType = (int)selectedGate.Type;
        }

        private static void ApplyGateIntent()
        {
            if (!gateIntentPending || Interlocked.Read(ref toolsStateRevision) < gateIntentRevision) return;
            var view = MainViewModel.Instance;
            if (view.HUDBuildingPanel == null || humanBoundHud == null) return;
            gateIntentPending = false;
            ReleaseGateHud(true);
            GateSelection next = pendingGate; pendingGate = null;
            if (next == null || next.Actor != humanSelectedOwner || !GateIsCurrent(next) ||
                !view.UIVisible || !view.Show_InGameUI || view.Show_HUD_Briefing || view.Show_HUD_MissionOver) return;
            selectedGate = next;
            gateHudOwner = view;
            savedMain = view.Show_HUD_Main; savedTroops = view.Show_HUD_Troops;
            savedBuilding = view.Show_HUD_Building; savedBook = view.Show_HUD_Book;
            savedRollover = view.RollOverText_Margin;
            SaveGateVisuals(view.HUDBuildingPanel);
            buildingOwnSetup = true;
            try
            {
                view.InBuildingGameAction();
                ShowTestGatePanel();
                BindGateAutomation(view);
                log.LogInfo("TEST_GATE_HUD_ACQUIRE session=" + next.Session + " actor=" + next.Actor +
                    " building=" + next.Id + " global=" + next.Global + " revision=" + toolsStateRevision +
                    " app=" + GameData.Instance.lastGameState.app_mode + "/" + GameData.Instance.lastGameState.app_sub_mode);
            }
            catch { ReleaseGateHud(true); throw; }
            finally { buildingOwnSetup = false; }
        }

        private static unsafe bool GateIsCurrent(GateSelection value) => CurrentGate(value, out _);

        private static void BindGateAutomation(MainViewModel view)
        {
            try
            {
                if (!gateAutomationResolved)
                {
                    gateAutomationResolved = true;
                    gateAutomation = new OptionalGateAutomation(() => {
                        GateSelection gate = selectedGate;
                        if (gate != null && GateIsCurrent(gate) && gateAutomation.Ready)
                            QueueTestAction(new TestAction { Kind = TestActionKind.GateAutomation, Actor = gate.Actor,
                                Gate = gate, Force = !gateAutomation.Automatic(gate.Id) });
                    });
                }
                foreach (string name in new[] { "ExtraFeaturesGatehouseAutomationButtonHost", "ExtraFeaturesDrawbridgeAutomationButtonHost" })
                {
                    var host = view.HUDBuildingPanel.FindName(name) as FrameworkElement;
                    if (host == null) continue;
                    gateProperties.Add(new ToolbarProperty(host, FrameworkElement.DataContextProperty));
                    host.DataContext = gateAutomation.Model;
                }
            }
            catch (Exception error) { gateAutomation = null; LogOnce("TEST_GATE_AUTOMATION_UNAVAILABLE: " + error); }
        }

''' + s[b:]
# Intent capture must not clear/change live UI or active selection on the engine callback.
a=s.index('        private static unsafe void SelectTestGate(');b=s.index('        private static void ClearGateSelection()',a)
block=s[a:b].replace('            ClearGateSelection();','''            pendingGate = null;
            gateIntentRevision = Interlocked.Read(ref toolsStateRevision) + 1;
            gateIntentPending = true;''').replace('            selectedGate = new GateSelection','            pendingGate = new GateSelection').replace('            pendingGate = selectedGate;\n            selectedGate = null;\n','')
s=s[:a]+block+s[b:]
s=s.replace('if (!view.Show_HUD_Building) { selectedGate = null; return; }','if (!ReferenceEquals(gateHudOwner, view) || !view.Show_HUD_Building) { ReleaseGateHud(false); return; }')
s=s.replace('            var panel = view.HUDBuildingPanel;','            var panel = view.HUDBuildingPanel;')
s=s.replace('            bool bridge = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE;\n            panel.RefBuildingPanel', '''            bool bridge = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE;
            try { gateAutomation?.Refresh(selectedGate.Id, bridge); }
            catch (Exception error) { LogOnce("TEST_GATE_AUTOMATION_REFRESH: " + error); }
            panel.RefBuildingPanel''')
s=s.replace('        private static unsafe void RefreshGateState()\n        {','        private static unsafe void RefreshGateState()\n        {\n            ApplyGateIntent();')
p.write_text(s,encoding='utf-8')
p=Path('Testmods/SpectatorEditorBuildTest/src/SpectatorEditorBuildTestPlugin.Hud.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('bool show = eligible && (IsEditorToolsSession() || MainViewModel.Instance.Show_HUD_Main || selectedGate != null) &&\n                !MainViewModel.Instance.Show_HUD_Briefing;', 'bool show = eligible && MainViewModel.Instance.UIVisible && MainViewModel.Instance.Show_InGameUI &&\n                !MainViewModel.Instance.Show_HUD_Briefing && !MainViewModel.Instance.Show_HUD_MissionOver;');p.write_text(s,encoding='utf-8')
p=Path('Testmods/SpectatorEditorBuildTest/src/SpectatorEditorBuildTestPlugin.cs');s=p.read_text(encoding='utf-8-sig');anchor='                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.setUpInbuilding),';i=s.index(anchor);s=s[:i]+'''                Patch(candidate, typeof(GameData), nameof(GameData.setGameState), nameof(AfterToolsPlayState), true, typeof(EngineInterface.PlayState));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.InBuildingGameAction), nameof(BeforeVanillaHudTransition), false);
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.TroopsSelectedGameAction), nameof(BeforeVanillaHudTransition), false, typeof(bool));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.DefaultGameUIGameAction), nameof(BeforeVanillaHudTransition), false, typeof(bool));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.DefaultMapEditorUIGameAction), nameof(BeforeVanillaHudTransition), false);
''' +s[i:];p.write_text(s,encoding='utf-8')
p=Path('Testmods/SpectatorEditorBuildTest/src/TestActionPolicy.cs');s=p.read_text(encoding='utf-8-sig').replace('Power, Gate, PowerToggle','Power, Gate, GateAutomation, PowerToggle');p.write_text(s,encoding='utf-8')
p=Path('Testmods/SpectatorEditorBuildTest/src/TestToolsRuntime.cs');s=p.read_text(encoding='utf-8-sig').replace('Queue<TestAction> testActions = new Queue<TestAction>()','ConcurrentQueue<TestAction> testActions = new ConcurrentQueue<TestAction>()').replace('action.Operation = ++nextTestAction;', 'action.Operation = Interlocked.Increment(ref nextTestAction);').replace('while (testActions.Count != 0)\n            {\n                TestAction action = testActions.Dequeue();','while (testActions.TryDequeue(out TestAction action))\n            {').replace('testActions.Clear();','while (testActions.TryDequeue(out _)) { }').replace('case TestActionKind.Gate: result = ExecuteGate(action); break;', '''case TestActionKind.Gate: result = ExecuteGate(action); break;
                            case TestActionKind.GateAutomation:
                                if (!GateIsCurrent(action.Gate) || action.Actor != humanSelectedOwner || gateAutomation == null)
                                    result = SerpLocalization.Get("Tools.InvalidTarget");
                                else { gateAutomation.Apply(action.Actor, action.Gate.Global, action.Force);
                                    result = SerpLocalization.Get(action.Force ? "Tools.GateAutomatic" : "Tools.GateFixed"); }
                                break;''');p.write_text(s,encoding='utf-8')
