using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private sealed class GateSelection
        {
            internal long Session, Generation;
            internal int Actor, Id;
            internal uint Global;
            internal eStructs Type;
        }
        private static GateSelection selectedGate, observedGate;
        private static GateSelection pendingGate;
        private static bool buildingOwnSetup;
        private static long toolsStateRevision;
        private static readonly GateHudLifecycle gateHudLifecycle = new GateHudLifecycle();
        private static MainViewModel gateHudOwner;
        private static DependencyObject gatePanelOwner;
        private static HUD_Main gateMainOwner;
        private static bool? gateDisplayedClosed;
        private static int gateHudDiagnostics;
        private static readonly List<ToolbarProperty> gateProperties = new List<ToolbarProperty>();
        private static OptionalGateAutomation gateAutomation;
        private static bool gateAutomationResolved;

        // The engine input callback only captures intent. Rendering waits for a completed PlayState update.
        private static void AfterToolsPlayState()
        {
            Interlocked.Increment(ref toolsStateRevision);
            SpectatorPerspectiveRuntime.ObserveReportSnapshot(GameData.Instance?.lastGameState);
        }

        private static void BeforeVanillaHudTransition()
        {
            if (buildingOwnSetup) return;
            gateHudLifecycle.Transition(); pendingGate = null;
            ReleaseGateHud(false);
        }

        private static void BeforeVanillaTroopTransition() => BeforeVanillaHudTransition();

        private static void CaptureGateProperty(DependencyObject target, DependencyProperty property) =>
            gateProperties.Add(new ToolbarProperty(target, property));

        private static void ReleaseGateHud(bool rebuild)
        {
            MainViewModel owner = gateHudOwner;
            DependencyObject panel = gatePanelOwner;
            HUD_Main main = gateMainOwner;
            gateHudOwner = null; gatePanelOwner = null; gateMainOwner = null;
            selectedGate = null; gateDisplayedClosed = null;
            if (!gateHudLifecycle.Release() || owner == null) return;
            foreach (ToolbarProperty property in gateProperties)
                try { property.Restore(); } catch (Exception error) { LogOnce("TEST_GATE_HUD_RESTORE: " + error); }
            gateProperties.Clear();
            if (rebuild && MainViewModel.viewModelLoaded && owner.Show_HUD_Building && !owner.Show_HUD_Main && !owner.Show_HUD_Troops &&
                ReferenceEquals(owner, MainViewModel.Instance) &&
                ReferenceEquals(panel, owner.HUDBuildingPanel) && ReferenceEquals(main, owner.HUDmain))
                RebuildCurrentHud(owner);
            LogGateHud("release rebuild=" + rebuild);
        }

        private static void RebuildCurrentHud(MainViewModel view)
        {
            var state = GameData.Instance?.lastGameState;
            if (!IsToolsSession() || state == null || !view.UIVisible || !view.Show_InGameUI || view.Show_HUD_Briefing || view.Show_HUD_MissionOver) return;
            switch (GateHudLifecycle.Route(state.app_mode, state.app_sub_mode, IsEditorToolsSession()))
            {
                case ToolsHudRoute.Building: view.InBuildingGameAction(); break;
                case ToolsHudRoute.Troops: view.TroopsSelectedGameAction(false); break;
                case ToolsHudRoute.Editor: view.DefaultMapEditorUIGameAction(); break;
                case ToolsHudRoute.Main: view.DefaultGameUIGameAction(); break;
            }
        }

        private static void EnterPlacementHud(MainViewModel view)
        {
            gateHudLifecycle.Transition(); pendingGate = null;
            ReleaseGateHud(false);
            if (IsEditorToolsSession()) view.DefaultMapEditorUIGameAction();
            else view.DefaultGameUIGameAction();
            LogGateHud("placement");
        }

        private static void LogGateHud(string reason)
        {
            if (gateHudDiagnostics++ >= 80 || !MainViewModel.viewModelLoaded) return;
            var view = MainViewModel.Instance;
            var state = GameData.Instance?.lastGameState;
            log.LogInfo("TEST_GATE_HUD_TRANSITION reason=" + reason + " generation=" + gateHudLifecycle.Generation +
                " revision=" + toolsStateRevision + " session=" + CurrentSessionId + " actor=" + humanSelectedOwner +
                " building=" + selectedGate?.Id + "/" + selectedGate?.Global + " app=" + state?.app_mode + "/" + state?.app_sub_mode +
                " main=" + view.Show_HUD_Main + " buildingHud=" + view.Show_HUD_Building + " troops=" + view.Show_HUD_Troops +
                " mask=" + view.HUDBuildingPanel?.RefHUDBuildingFullClickMask?.Visibility);
        }
        private static int observedGatePhysical = -1, observedGateRequest = -1;
        private static DateTime observeGateUntil;

        private static bool IsGateType(eStructs type) => type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_DRAWBRIDGE;

        private static unsafe bool CurrentGate(GateSelection selection, out GameBuilding* building)
        {
            building = null;
            return selection != null && IsToolsSession() && IsSelectableAi(selection.Actor) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(selection.Id, out building) && building != null &&
                ToolPolicy.SameBuilding(selection.Session, CurrentSessionId, selection.Actor, building->r_PlayerIdOwner,
                    selection.Id, selection.Id, selection.Global, building->r_GlobalId, building->r_AliveState == AliveState.IsAlive) &&
                building->r_BuildingType == selection.Type && IsGateType(selection.Type);
        }

        private static unsafe void SelectTestGate(int x, int y)
        {
            if (!IsToolsSession()) return;
            pendingGate = null;
            gateHudLifecycle.Request(Interlocked.Read(ref toolsStateRevision));
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return;
            int id = tiles.GetTileBuildingId(tiles.GetTileId(x, y));
            if (id <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive || building->r_PlayerIdOwner != humanSelectedOwner ||
                !IsGateType(building->r_BuildingType)) return;
            pendingGate = new GateSelection { Session = CurrentSessionId, Actor = humanSelectedOwner,
                Id = id, Global = building->r_GlobalId, Type = building->r_BuildingType, Generation = gateHudLifecycle.Generation };
        }

        private static void ClearGateSelection()
        {
            gateHudLifecycle.Transition(); pendingGate = null;
            ReleaseGateHud(true);
        }

        private static unsafe void BeforeGatePanel(ref int overridePanel, ref int overrideType)
        {
            // A direct setup call rebuilds panels only, unlike InBuildingGameAction's complete transition.
            if (!buildingOwnSetup) { BeforeVanillaHudTransition(); return; }
            if (!CurrentGate(selectedGate, out _)) return;
            overridePanel = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE ? 37 : 36;
            overrideType = (int)selectedGate.Type;
        }

        private static void ApplyGateIntent()
        {
            var view = MainViewModel.Instance;
            var readyPanel = view.HUDBuildingPanel;
            if (!gateHudLifecycle.TryConsume(Interlocked.Read(ref toolsStateRevision),
                readyPanel != null && view.HUDmain != null && humanBoundHud != null &&
                readyPanel.RefButtonCloseGate != null && readyPanel.RefButtonOpenGate != null &&
                readyPanel.RefButtonCloseBridge != null && readyPanel.RefButtonOpenBridge != null &&
                readyPanel.RefShowRepairPanel != null && readyPanel.RefShowWorkersPanel != null &&
                readyPanel.RefShowInfoPanel != null)) return;
            GateSelection next = pendingGate; pendingGate = null;
            if (next != null && selectedGate != null && next.Session == selectedGate.Session &&
                next.Generation == selectedGate.Generation && next.Actor == selectedGate.Actor &&
                next.Id == selectedGate.Id && next.Global == selectedGate.Global) return;
            ReleaseGateHud(next == null);
            if (next == null || next.Generation != gateHudLifecycle.Generation || next.Actor != humanSelectedOwner || !GateIsCurrent(next) ||
                !view.UIVisible || !view.Show_InGameUI || view.Show_HUD_Briefing || view.Show_HUD_MissionOver) return;
            selectedGate = next;
            gateHudOwner = view;
            gatePanelOwner = view.HUDBuildingPanel; gateMainOwner = view.HUDmain;
            gateHudLifecycle.Acquire();
            buildingOwnSetup = true;
            try
            {
                view.InBuildingGameAction();
                var panel = view.HUDBuildingPanel;
                // These unrelated actions must never operate on a previous local building selection.
                panel.RefShowRepairPanel.Visibility = Visibility.Collapsed;
                panel.RefShowWorkersPanel.Visibility = Visibility.Collapsed;
                panel.RefShowInfoPanel.Visibility = Visibility.Collapsed;
                // Vanilla owns all panel visibility and click masks. Only these sprites are ours.
                foreach (Button button in new[] { panel.RefButtonCloseGate, panel.RefButtonOpenGate,
                    panel.RefButtonCloseBridge, panel.RefButtonOpenBridge })
                {
                    CaptureGateProperty(button, PropEx.Sprite1Property);
                    CaptureGateProperty(button, PropEx.Sprite2Property);
                }
                BindGateAutomation(view);
                ShowTestGatePanel();
                LogGateHud("acquire");

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
                    try
                    {
                        gateAutomation = new OptionalGateAutomation(() => {
                            GateSelection gate = selectedGate;
                            if (gate != null && GateIsCurrent(gate) && gateAutomation.Ready)
                                QueueTestAction(new TestAction { Kind = TestActionKind.GateAutomation, Actor = gate.Actor,
                                    Gate = gate, Force = !gateAutomation.Automatic(gate.Id) });
                        });
                    }
                    catch (Exception error) { gateAutomation = null; LogOnce("TEST_GATE_AUTOMATION_UNAVAILABLE: " + error); }
                    // Continue binding with a null model: hide an unsupported provider's original button.

                }
                foreach (string name in new[] { "ExtraFeaturesGatehouseAutomationButtonHost", "ExtraFeaturesDrawbridgeAutomationButtonHost" })
                {
                    var host = view.HUDBuildingPanel.FindName(name) as FrameworkElement;
                    if (host == null) continue;
                    var button = view.HUDBuildingPanel.FindName(name.Replace("Host", "")) as Button;
                    GateAutomationBinding.Apply(host, button, gateAutomation?.Model, CaptureGateProperty);
                }
            }
            catch (Exception error) { gateAutomation = null; LogOnce("TEST_GATE_AUTOMATION_UNAVAILABLE: " + error); }
        }

        private static unsafe void ShowTestGatePanel()
        {
            if (selectedGate?.Actor != humanSelectedOwner || !CurrentGate(selectedGate, out GameBuilding* building)) return;
            var view = MainViewModel.Instance;
            if (!ReferenceEquals(gateHudOwner, view) || !ReferenceEquals(gatePanelOwner, view.HUDBuildingPanel) ||
                !ReferenceEquals(gateMainOwner, view.HUDmain) || !view.Show_HUD_Building) { ReleaseGateHud(false); return; }
            var panel = view.HUDBuildingPanel;
            if (panel == null) return;
            bool bridge = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE;
            try { gateAutomation?.Refresh(selectedGate.Id, bridge); }
            catch (Exception error) { LogOnce("TEST_GATE_AUTOMATION_REFRESH: " + error); }
            // Snapshot the real object's state into its own button sprites, without altering PlayState.
            int request = bridge ? building->N00008642_2 : building->r_GateState2;
            int physical = bridge ? building->N0000182B & 0xff : building->r_GateState;
            bool closed = request == 10 || (request != 11 && (physical == 1 || physical == 2));
            if (gateDisplayedClosed == closed) return;
            gateDisplayedClosed = closed;
            Button close = bridge ? panel.RefButtonCloseBridge : panel.RefButtonCloseGate;
            Button open = bridge ? panel.RefButtonOpenBridge : panel.RefButtonOpenGate;
            PropEx.SetSprite1(close, view.GameSprites[closed ? 85 : 86]);
            PropEx.SetSprite2(close, view.GameSprites[closed ? 85 : 86]);
            PropEx.SetSprite1(open, view.GameSprites[closed ? 84 : 83]);
            PropEx.SetSprite2(open, view.GameSprites[closed ? 84 : 83]);
        }

        private static bool BeforeTestGateCommand(object parameter) => QueueGateCommand(parameter, false);
        private static bool BeforeTestBridgeCommand(object parameter) => QueueGateCommand(parameter, true);
        private static unsafe bool QueueGateCommand(object parameter, bool bridge)
        {
            if (selectedGate == null) return true;
            if (!IsToolsSession() || selectedGate.Actor != humanSelectedOwner || !CurrentGate(selectedGate, out _) ||
                bridge != (selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE)) return false;
            if (!int.TryParse(parameter as string, out int button)) return false;
            // Exactly Vanilla's UI mappings, identical desired native states for both object types.
            int desired = bridge ? (button == 2 ? 11 : 10) : (button == 1 ? 10 : 11);
            QueueTestAction(new TestAction { Kind = TestActionKind.Gate, Actor = selectedGate.Actor,
                Gate = selectedGate, Amount = desired });
            // Skip Vanilla's local-player action AND its optimistic PlayState/button update.
            return false;
        }

        private static unsafe string ExecuteGate(TestAction action)
        {
            if (!CurrentGate(action.Gate, out GameBuilding* building)) return SerpLocalization.Get("Tools.InvalidTarget");
            bool bridge = building->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE;
            RequireActionEntry(bridge ? BridgeRva : GateRva, bridge ? bridgeEntry : gateEntry, false);
            (bridge ? nativeBridge : nativeGate)(action.Actor, action.Gate.Id, action.Amount, action.Gate.Global);
            observedGate = action.Gate; observedGatePhysical = observedGateRequest = -1;
            observeGateUntil = DateTime.UtcNow.AddSeconds(12);
            return SerpLocalization.Get("Tools.GateRequested") +
                (IsEditorToolsSession() ? " " + SerpLocalization.Get("Tools.EditorNotice") : "");
        }

        private static unsafe void RefreshGateState()
        {
            // QoL can publish its troop HUD through properties without a Vanilla method callback.
            // Observe this before a pending empty-map intent could request a default HUD rebuild.
            var view = MainViewModel.Instance;
            if (gateHudLifecycle.ObservePresentation(ReferenceEquals(gateHudOwner, view) &&
                ReferenceEquals(gatePanelOwner, view.HUDBuildingPanel) && ReferenceEquals(gateMainOwner, view.HUDmain),
                view.Show_HUD_Building, view.Show_HUD_Main, view.Show_HUD_Troops))
            { pendingGate = null; ReleaseGateHud(false); }
            ApplyGateIntent();
            if (selectedGate != null)
            {
                if (selectedGate.Actor != humanSelectedOwner || !CurrentGate(selectedGate, out _)) ClearGateSelection();
                else ShowTestGatePanel();
            }
            if (observedGate == null) return;
            if (DateTime.UtcNow > observeGateUntil || !CurrentGate(observedGate, out GameBuilding* building)) { observedGate = null; return; }
            bool bridge = observedGate.Type == eStructs.STRUCT_DRAWBRIDGE;
            int physical = bridge ? building->N0000182B & 0xff : building->r_GateState;
            int request = bridge ? building->N00008642_2 : building->r_GateState2;
            if (physical == observedGatePhysical && request == observedGateRequest) return;
            observedGatePhysical = physical; observedGateRequest = request;
            log.LogInfo("TEST_GATE_STATE session=" + observedGate.Session + " building=" + observedGate.Id +
                " global=" + observedGate.Global + " requested=" + request + " physical=" + physical + " bridge=" + bridge);
        }
    }
}
