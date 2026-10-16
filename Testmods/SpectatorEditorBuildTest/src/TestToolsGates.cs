using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private sealed class GateSelection
        {
            internal long Session;
            internal int Actor, Id;
            internal uint Global;
            internal eStructs Type;
        }
        private static GateSelection selectedGate, observedGate;
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
            ClearGateSelection();
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return;
            int id = tiles.GetTileBuildingId(tiles.GetTileId(x, y));
            if (id <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive || building->r_PlayerIdOwner != humanSelectedOwner ||
                !IsGateType(building->r_BuildingType)) return;
            selectedGate = new GateSelection { Session = CurrentSessionId, Actor = humanSelectedOwner,
                Id = id, Global = building->r_GlobalId, Type = building->r_BuildingType };
            MainViewModel.Instance.InBuildingGameAction();
            // This changes presentation only. Never set the native selected/controlled player or building.
            ShowTestGatePanel();
            log.LogInfo("TEST_GATE_SELECTED session=" + CurrentSessionId + " actor=" + humanSelectedOwner + " building=" + id + " global=" + building->r_GlobalId);
        }

        private static void ClearGateSelection()
        {
            if (selectedGate != null && MainViewModel.viewModelLoaded)
            {
                var view = MainViewModel.Instance;
                view.Show_HUD_Building = false; view.Show_HUD_Main = true; view.Show_HUD_Book = false;
            }
            selectedGate = null;
        }

        private static unsafe void BeforeGatePanel(ref int overridePanel, ref int overrideType)
        {
            if (selectedGate?.Actor != humanSelectedOwner || !CurrentGate(selectedGate, out _)) return;
            // Vanilla panel IDs 36/37; explicit presentation contract, not structure IDs.
            overridePanel = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE ? 37 : 36;
            overrideType = (int)selectedGate.Type;
        }

        private static unsafe void ShowTestGatePanel()
        {
            if (selectedGate?.Actor != humanSelectedOwner || !CurrentGate(selectedGate, out GameBuilding* building)) return;
            var view = MainViewModel.Instance;
            if (!view.Show_HUD_Building) { selectedGate = null; return; }
            var panel = view.HUDBuildingPanel;
            if (panel == null) return;
            bool bridge = selectedGate.Type == eStructs.STRUCT_DRAWBRIDGE;
            panel.RefBuildingPanel.Visibility = Visibility.Visible;
            panel.RefShowGatePanel.Visibility = bridge ? Visibility.Hidden : Visibility.Visible;
            panel.RefShowDrawbridgePanel.Visibility = bridge ? Visibility.Visible : Visibility.Hidden;
            // No unrelated repair/worker actions may act on Vanilla's previous local selection.
            panel.RefShowRepairPanel.Visibility = Visibility.Collapsed;
            panel.RefShowWorkersPanel.Visibility = Visibility.Collapsed;
            panel.RefShowInfoPanel.Visibility = Visibility.Collapsed;
            // Snapshot the real object's state into its own button sprites, without altering PlayState.
            int request = bridge ? building->N00008642_2 : building->r_GateState2;
            int physical = bridge ? building->N0000182B & 0xff : building->r_GateState;
            bool closed = request == 10 || (request != 11 && (physical == 1 || physical == 2));
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
