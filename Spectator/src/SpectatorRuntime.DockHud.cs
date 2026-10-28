using System;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Grid dockLayer, dockMenu;
        private static StackPanel placementHeader;
        private static TextBlock dockPlayer, dockCurrentOrder, dockOrderDetails, dockStatus, dockTargetLabel;
        private static Image dockFace, dockOrderIcon;
        private static Button dockPlace, dockPowers;
        private static CreditAmountInput dockAmountInput;
        private static readonly Button[] dockOwners = new Button[9], placementOwners = new Button[9], dockTargets = new Button[9];
        private static readonly TextBlock[] dockNumbers = new TextBlock[9], placementNumbers = new TextBlock[9], dockTargetNumbers = new TextBlock[9];
        private static readonly RoutedEventHandler[] dockTargetHandlers = new RoutedEventHandler[9];
        private static bool dockDirty = true;
        private static int dockActor = -1, dockTarget = -1;
        private static long dockRoster;
        private static string dockLocale;
        private static PresentedAiOrder dockOrder;
        private static DockHudDisplay dockDisplay;
        private static bool dockFailed;

        private static void RefreshDockControlsSafely(bool eligible)
        {
            if (dockFailed) return;
            try { RefreshDockControls(eligible); }
            catch (Exception error)
            {
                dockFailed = true;
                if (dockLayer != null) SetDockVisibility(dockLayer, false);
                if (placementHeader != null) SetDockVisibility(placementHeader, false);
                dockAmountInput?.EndEditing();
                LogOnce("SPECTATOR_DOCK_UI_FAILED: " + error);
            }
        }

        private static void ReleaseDockAmountForMapClick()
        {
            var input = dockAmountInput;
            if (input == null || !input.OwnsKeyboard) return;
            long session = CurrentSessionId;
            if (log.IsMainThread) input.OutsidePointerDown(false);
            else presentationCallbacks.Enqueue(() => {
                if (IsCurrentSession(session) && ReferenceEquals(dockAmountInput, input)) input.OutsidePointerDown(false);
            });
        }

        private static bool EnsureDockControls()
        {
            if (dockLayer != null) return true;
            if (humanBoundHud == null || toolsModel == null) return false;
            if (!humanBoundHud.IsLoaded) return false;
            var layer = humanBoundHud.FindName("SpectatorDockLayer") as Grid;
            if (layer == null) throw new MissingMemberException("Loaded HUD_Main lacks SpectatorDockLayer.");
            T Find<T>(string name) where T : FrameworkElement => humanBoundHud.FindName(name) as T;
            var menu = Find<Grid>("SpectatorDockMenu");
            var header = Find<StackPanel>("SpectatorPlacementHeader");
            var amount = Find<TextBox>("DockAmount");
            var playerText = Find<TextBlock>("DockPlayer");
            var targetText = Find<TextBlock>("DockTargetLabel");
            var face = Find<Image>("DockFace");
            var orderText = Find<TextBlock>("DockCurrentOrder");
            var orderDetails = Find<TextBlock>("DockOrderDetails");
            var orderIcon = Find<Image>("DockCurrentOrderIcon");
            var status = Find<TextBlock>("DockStatus");
            var place = Find<Button>("DockPlace");
            var powers = Find<Button>("DockPowers");
            if (menu == null || header == null || amount == null || playerText == null || targetText == null || face == null ||
                orderText == null || orderDetails == null || orderIcon == null || status == null || place == null || powers == null)
                throw new MissingMemberException("Loaded Spectator dock is incomplete.");
            for (int player = 1; player <= 8; player++)
            {
                if (Find<Button>("DockOwner" + player) == null || Find<Button>("PlacementOwner" + player) == null ||
                    Find<Button>("DockTarget" + player) == null || Find<TextBlock>("DockOwnerText" + player) == null ||
                    Find<TextBlock>("PlacementOwnerText" + player) == null || Find<TextBlock>("DockTargetText" + player) == null)
                    throw new MissingMemberException("Loaded Spectator dock player controls are incomplete.");
            }
            // Publish only a fully resolved view. No Vanilla DataContext or clipping is overwritten.
            dockLayer = layer; dockMenu = menu; placementHeader = header;
            dockTargetLabel = targetText; dockPlayer = playerText; dockFace = face; dockCurrentOrder = orderText; dockOrderIcon = orderIcon;
            dockStatus = status;
            dockOrderDetails = orderDetails;
            dockPlace = place; dockPowers = powers;
            layer.DataContext = toolsModel;
            header.DataContext = toolsModel;
            dockAmountInput = new CreditAmountInput(amount, toolsModel);
            for (int player = 1; player <= 8; player++)
            {
                int id = player;
                dockOwners[id] = Find<Button>("DockOwner" + id);
                placementOwners[id] = Find<Button>("PlacementOwner" + id);
                dockTargets[id] = Find<Button>("DockTarget" + id);
                dockNumbers[id] = Find<TextBlock>("DockOwnerText" + id);
                placementNumbers[id] = Find<TextBlock>("PlacementOwnerText" + id);
                dockTargetNumbers[id] = Find<TextBlock>("DockTargetText" + id);
                dockOwners[id].Click += humanOwnerHandlers[id];
                placementOwners[id].Click += humanOwnerHandlers[id];
                dockOwners[id].PreviewMouseDown += humanJumpHandlers[id];
                placementOwners[id].PreviewMouseDown += humanJumpHandlers[id];
                dockTargetHandlers[id] = (sender, args) => {
                    if (!CanUseDock(DockHudDisplay.Tools)) return;
                    ExecuteToolsCommand("Target" + id);
                    dockDirty = true;
                };
                dockTargets[id].Click += dockTargetHandlers[id];
            }
            var attack = Find<Image>("DockAttackActionIcon");
            if (attack != null) attack.Source = MainViewModel.Instance.getAlliesGoodsIcon(-3);
            var ordersIcon = Find<Image>("DockOrdersIcon");
            if (ordersIcon != null) ordersIcon.Source = MainViewModel.Instance.getAlliesGoodsIcon(-3);
            dockDirty = true;
            return true;
        }

        private static DockHudDisplay CurrentDockDisplay(bool eligible)
        {
            if (!MainViewModel.viewModelLoaded || humanBoundHud == null) return DockHudDisplay.Hidden;
            var view = MainViewModel.Instance;
            if (!ReferenceEquals(view.HUDmain, humanBoundHud) || !ReferenceEquals(view.IngameUI, humanScreen)) return DockHudDisplay.Hidden;
            var state = GameData.Instance?.lastGameState;
            // Troops may retain Show_HUD_Main while clipping its centre (BFQ). Never steal that area.
            bool occupied = view.Show_HUD_Troops || view.Show_HUD_Building || view.Show_HUD_Briefing ||
                view.Show_HUD_MissionOver || view.AlliesPanelVisible || view.MeritPanelVisible || view.MPChatVisible ||
                state == null || GateHudLifecycle.Route(state.app_mode, state.app_sub_mode, false) != ToolsHudRoute.Main;
            return DockHudPolicy.Evaluate(eligible, view.UIVisible && view.Show_InGameUI, view.Show_HUD_Main,
                occupied, IsEditorToolsSession() && view.MEMode == 0, humanPlacementMode, view.buildScreenID);
        }

        private static bool CanUseDock(DockHudDisplay display) => IsToolbarSession() && CurrentDockDisplay(true) == display;

        private static void RefreshDockControls(bool eligible)
        {
            if (toolsModel == null || humanPanel == null) return;
            if (!EnsureDockControls()) return;
            var display = CurrentDockDisplay(eligible);
            if (display != dockDisplay)
            {
                dockAmountInput?.EndEditing();
                ClosePlayerTooltips();
                dockDisplay = display;
                dockDirty = true;
            }
            SetDockVisibility(dockLayer, display == DockHudDisplay.Tools);
            SetDockVisibility(dockMenu, display == DockHudDisplay.Tools);
            SetDockVisibility(placementHeader, display == DockHudDisplay.Placement);
            if (display == DockHudDisplay.Hidden) return;
            var state = GameData.Instance.lastGameState;
            var order = PresentedAiOrder.Read(humanSelectedOwner, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
            string locale = SerpLocalization.GetActiveLocale();
            if (!dockDirty && dockActor == humanSelectedOwner && dockTarget == toolsModel.Target &&
                dockRoster == rosterFingerprint && dockLocale == locale && order.Equals(dockOrder)) return;
            dockDirty = false; dockActor = humanSelectedOwner; dockTarget = toolsModel.Target;
            dockRoster = rosterFingerprint; dockLocale = locale; dockOrder = order;
            RefreshOrderAvailability();
            for (int player = 1; player <= 8; player++)
            {
                bool available = IsSelectableView(player);
                SetDockVisibility(dockOwners[player], available);
                SetDockVisibility(placementOwners[player], available);
                SetDockVisibility(dockTargets[player], IsToolsPlayer(player, false));
                if (available || IsToolsPlayer(player, false)) RefreshHumanOwnerColour(player);
                dockNumbers[player].Foreground = placementNumbers[player].Foreground = dockTargetNumbers[player].Foreground = PlayerBrush(player);
                MarkDockButton(dockOwners[player], player == humanSelectedOwner);
                MarkDockButton(placementOwners[player], player == humanSelectedOwner);
                MarkDockButton(dockTargets[player], player == toolsModel.Target);
                SetDockContrast(dockNumbers[player], player);
                SetDockContrast(placementNumbers[player], player);
                SetDockContrast(dockTargetNumbers[player], player);
                if (available)
                {
                    SetDockPlayerTooltip(dockOwners[player], player, false);
                    SetDockPlayerTooltip(placementOwners[player], player, false);
                }
                if (IsToolsPlayer(player, false)) SetDockPlayerTooltip(dockTargets[player], player, true);
            }
            dockPlayer.Text = IsSelectableView(humanSelectedOwner) ? PlayerLabel(humanSelectedOwner) : SerpLocalization.Get("Tools.InvalidTarget");
            dockPlayer.Foreground = PlayerBrush(humanSelectedOwner);
            SetDockContrast(dockPlayer, humanSelectedOwner);
            dockTargetLabel.Text = IsToolsPlayer(toolsModel.Target, false) ? PlayerLabel(toolsModel.Target) : SerpLocalization.Get("Tools.InvalidTarget");
            SetDockContrast(dockTargetLabel, toolsModel.Target);
            if (ToolTipService.GetToolTip(dockTargetLabel) is ToolTip targetTip) targetTip.Content = dockTargetLabel.Text;
            if (ToolTipService.GetToolTip(dockPlayer) is ToolTip playerTip) playerTip.Content = dockPlayer.Text;
            dockFace.Source = IsSelectableView(humanSelectedOwner) && state.is_skirmish_player(humanSelectedOwner)
                ? MainViewModel.Instance.getAIFace(state.computer_register[humanSelectedOwner], true) : null;
            dockPlace.IsEnabled = IsToolsSession() && IsSelectableAi(humanSelectedOwner);
            MarkDockButton(dockPowers, toolsModel.PowersEnabled);
            dockOrderIcon.Source = OrderIcon(order);
            dockCurrentOrder.Inlines.Clear();
            dockCurrentOrder.Inlines.Add(new Run { Text = SerpLocalization.Get(order.Mode == 1 ? "Tools.Attack" : order.Mode == 2 ? "Tools.Defend" : "Tools.NoOrder") });
            if (order.Mode != 0)
                dockCurrentOrder.Inlines.Add(new Run { Text = " · " + (order.Target > 0 ? PlayerLabel(order.Target) : SerpLocalization.Get("Tools.UnknownTarget")), Foreground = PlayerBrush(order.Target) });
            SetDockContrast(dockCurrentOrder, order.Target);
            SetDockContrast(dockOrderDetails, order.Target);
            string description = "";
            dockOrderDetails.Inlines.Clear();
            foreach (Inline inline in dockCurrentOrder.Inlines) if (inline is Run run)
            { description += run.Text; dockOrderDetails.Inlines.Add(new Run { Text = run.Text, Foreground = run.Foreground }); }
            if (ToolTipService.GetToolTip(dockCurrentOrder) is ToolTip orderTip) orderTip.Content = description;
            if (ToolTipService.GetToolTip(dockOrderDetails) is ToolTip detailsTip)
                detailsTip.Content = description + (string.IsNullOrEmpty(toolsModel.OrderAvailability) ? "" : " — " + toolsModel.OrderAvailability);
            RefreshColouredStatus(dockStatus);
        }

        private static readonly DropShadowEffect lightTextEdge = new DropShadowEffect { Color = Color.FromRgb(255, 239, 202), BlurRadius = 2, ShadowDepth = 0, Opacity = 0.95f };
        private static readonly DropShadowEffect darkTextEdge = new DropShadowEffect { Color = Color.FromRgb(35, 19, 9), BlurRadius = 2, ShadowDepth = 0, Opacity = 0.95f };
        private static void MarkDockButton(Button button, bool selected)
        {
            string state = selected ? "Selected" : "";
            if (!Equals(button.Tag, state)) button.Tag = state;
        }
        private static void SetDockContrast(TextBlock text, int player)
        {
            var brush = PlayerBrush(player) as SolidColorBrush;
            if (brush == null) return;
            var colour = brush.Color;
            // Colour remains the player's; only the edge adapts to contrast on parchment.
            var effect = (0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B) < 122 ? lightTextEdge : darkTextEdge;
            if (!ReferenceEquals(text.Effect, effect)) text.Effect = effect;
        }

        private static void SetDockVisibility(FrameworkElement element, bool visible)
        {
            var value = visible ? Visibility.Visible : Visibility.Collapsed;
            if (element.Visibility != value) element.Visibility = value;
            if (element.IsHitTestVisible != visible) element.IsHitTestVisible = visible;
        }

        private static void SetDockPlayerTooltip(Button button, int player, bool target)
        {
            string text = PlayerLabel(player) + " — " + SerpLocalization.Get(target ? "Tools.SelectTarget" : "Tools.SelectHelp");
            if (ToolTipService.GetToolTip(button) is ToolTip tip && !Equals(tip.Content, text)) tip.Content = text;
        }

        private static void StopDockPlacement()
        {
            if (!CanUseDock(DockHudDisplay.Placement) || !humanPlacementMode) return;
            ToggleHumanMode(null, null);
            dockDirty = true;
        }

        private static void DetachDockControls()
        {
            if (dockLayer == null) return;
            SetDockVisibility(dockLayer, false);
            if (placementHeader != null) { SetDockVisibility(placementHeader, false); placementHeader.DataContext = null; }
            dockAmountInput?.Detach(); dockAmountInput = null;
            dockLayer.DataContext = null;
            for (int player = 1; player <= 8; player++)
            {
                // Also safe after a failed, partially subscribed UI initialization.
                if (dockOwners[player] != null)
                {
                    dockOwners[player].Click -= humanOwnerHandlers[player];
                    dockOwners[player].PreviewMouseDown -= humanJumpHandlers[player];
                }
                if (placementOwners[player] != null)
                {
                    placementOwners[player].Click -= humanOwnerHandlers[player];
                    placementOwners[player].PreviewMouseDown -= humanJumpHandlers[player];
                }
                if (dockTargets[player] != null) dockTargets[player].Click -= dockTargetHandlers[player];
                dockOwners[player] = placementOwners[player] = dockTargets[player] = null;
                dockNumbers[player] = placementNumbers[player] = dockTargetNumbers[player] = null;
                dockTargetHandlers[player] = null;
            }
            dockLayer = dockMenu = null; placementHeader = null;
            dockPlayer = dockCurrentOrder = dockOrderDetails = dockStatus = dockTargetLabel = null; dockFace = dockOrderIcon = null;
            dockPlace = dockPowers = null; dockDisplay = DockHudDisplay.Hidden; dockDirty = true;
        }
    }
}
