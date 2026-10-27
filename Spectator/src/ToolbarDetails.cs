using System;
using System.Collections.Generic;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static readonly Brush neutralPlayerBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private static readonly Brush normalOwnerBorder = new SolidColorBrush(Color.FromArgb(176, 114, 36, 28));
        private static readonly Brush selectedOwnerBorder = new SolidColorBrush(Color.FromRgb(239, 198, 112));
        private static readonly Image[] ownerOrderIcons = new Image[9];
        private static readonly PresentedAiOrder[] shownOrders = new PresentedAiOrder[9];
        private static Image currentOrderIcon;
        private static TextBlock currentOrderText, currentOrderTarget;
        private static PresentedAiOrder shownCurrentOrder;
        private static int shownOrderActor = -1;
        private static string shownOrderLocale;
        private static TextBlock statusText;
        private static readonly List<ToolTip> ownedTooltips = new List<ToolTip>();

        private static void CaptureTooltips(DependencyObject root)
        {
            if (root is FrameworkElement element && ToolTipService.GetToolTip(element) is ToolTip tip)
                ownedTooltips.Add(tip);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                CaptureTooltips(VisualTreeHelper.GetChild(root, i));
        }

        private static Brush PlayerBrush(int player) => player > 0 && player < humanOwnerNumbers.Length
            ? humanOwnerNumbers[player]?.Foreground ?? neutralPlayerBrush : neutralPlayerBrush;

        private static void PositionHumanDetails()
        {
            if (!humanPositioned || humanPanel == null || humanCanvas == null || humanDetails == null) return;
            float width = ToolbarGeometry.DetailWidth(humanCanvas.ActualWidth);
            float top = (float)Canvas.GetTop(humanPanel) + humanPanel.ActualHeight + 2;
            humanDetails.Width = width;
            humanDetails.MaxHeight = ToolbarGeometry.DetailHeight(humanCanvas.ActualHeight, top);
            Canvas.SetLeft(humanDetails, ToolbarGeometry.DetailLeft((float)Canvas.GetLeft(humanPanel),
                humanPanel.Width, width, humanCanvas.ActualWidth, detailsRight));
            Canvas.SetTop(humanDetails, top);
        }

        private static bool HitPanel(FrameworkElement element, MouseButtonEventArgs args)
        {
            if (element == null || !element.IsVisible) return false;
            Point p = args.GetPosition(element);
            return p.X >= 0 && p.Y >= 0 && p.X < element.ActualWidth && p.Y < element.ActualHeight;
        }

        private static void OnToolbarOutsideClick(object sender, MouseButtonEventArgs args)
        {
            // Preview runs before a different text input takes focus; leave the original click untouched.
            amountInput?.OutsidePointerDown(HitPanel(humanPanel, args) || HitPanel(humanDetails, args));
        }

        private static void ReleaseAmountForMapClick()
        {
            var input = amountInput;
            if (input == null || !input.OwnsKeyboard) return;
            long session = CurrentSessionId;
            if (log.IsMainThread) input.OutsidePointerDown(false);
            else presentationCallbacks.Enqueue(() => {
                if (IsCurrentSession(session) && ReferenceEquals(amountInput, input)) input.OutsidePointerDown(false);
            });
        }

        private static void ClosePlayerTooltips()
        {
            foreach (var tooltip in ownedTooltips) tooltip.IsOpen = false;
            foreach (var button in playerTooltipTexts.Keys)
                if (ToolTipService.GetToolTip(button) is ToolTip tip) tip.IsOpen = false;
        }

        private static void OnPlayerTooltipOpened(object sender, RoutedEventArgs args)
        {
            var tip = (ToolTip)sender;
            float font = APIShared.ModSettings.ToolTipPresentation.AutomaticFontSize;
            tip.FontSize = font;
            tip.MaxWidth = APIShared.ModSettings.ToolTipPresentation.AutomaticMaximumWidth;
            if (tip.Content is TextBlock text)
            {
                text.FontSize = font;
                text.MaxWidth = Math.Max(1, tip.MaxWidth - (float)tip.Padding.Left - (float)tip.Padding.Right - 12);
            }
        }

        private static void RefreshColouredStatus()
        {
            var text = statusText;
            if (text == null || toolsModel == null) return;
            string value = toolsModel.Status ?? "";
            text.Inlines.Clear();
            int offset = 0;
            while (offset < value.Length)
            {
                int match = -1, playerMatch = 0; string labelMatch = null;
                for (int player = 1; player <= 8; player++)
                {
                    if (!IsSelectableView(player)) continue;
                    string label = PlayerLabel(player);
                    int index = value.IndexOf(label, offset, StringComparison.Ordinal);
                    if (index >= 0 && (match < 0 || index < match || (index == match && label.Length > labelMatch.Length)))
                    { match = index; playerMatch = player; labelMatch = label; }
                }
                if (match < 0) { text.Inlines.Add(new Run { Text = value.Substring(offset) }); break; }
                if (match > offset) text.Inlines.Add(new Run { Text = value.Substring(offset, match - offset) });
                text.Inlines.Add(new Run { Text = labelMatch, Foreground = PlayerBrush(playerMatch) });
                offset = match + labelMatch.Length;
            }
            if (ToolTipService.GetToolTip(text) is ToolTip tip)
            {
                var rich = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = neutralPlayerBrush };
                foreach (Inline inline in text.Inlines)
                    if (inline is Run run) rich.Inlines.Add(new Run { Text = run.Text, Foreground = run.Foreground });
                tip.Style = humanPanel.TryFindResource("PlayerTooltip") as Style;
                tip.Content = rich;
                tip.Opened -= OnPlayerTooltipOpened;
                tip.Opened += OnPlayerTooltipOpened;
                OnPlayerTooltipOpened(tip, null);
            }
        }

        private static void RefreshOrderPresentation(bool force = false)
        {
            var state = GameData.Instance?.lastGameState;
            if (state == null || toolsModel?.Expanded != true || currentOrderText == null) return;
            for (int player = 1; player <= 8; player++)
            {
                var order = PresentedAiOrder.Read(player, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
                if (force || !order.Equals(shownOrders[player]))
                {
                    shownOrders[player] = order;
                    ownerOrderIcons[player].Source = OrderIcon(order);
                }
            }
            var selected = PresentedAiOrder.Read(humanSelectedOwner, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
            string locale = SerpLocalization.GetActiveLocale();
            if (!force && shownOrderActor == humanSelectedOwner && selected.Equals(shownCurrentOrder) && shownOrderLocale == locale) return;
            shownOrderActor = humanSelectedOwner; shownCurrentOrder = selected; shownOrderLocale = locale;
            currentOrderIcon.Source = OrderIcon(selected);
            currentOrderText.Text = SerpLocalization.Get("Tools.CurrentOrder") + ": " + SerpLocalization.Get(
                selected.Mode == 1 ? "Tools.Attack" : selected.Mode == 2 ? "Tools.Defend" : "Tools.NoOrder");
            currentOrderTarget.Text = selected.Mode == 0 ? "" : selected.Target > 0
                ? PlayerLabel(selected.Target) : SerpLocalization.Get("Tools.UnknownTarget");
            currentOrderTarget.Foreground = PlayerBrush(selected.Target);
        }

        // Vanilla's HUD_AlliesPanel uses these two getAlliesGoodsIcon keys for the live order pips.
        private static ImageSource OrderIcon(PresentedAiOrder order) => order.Mode == 1
            ? MainViewModel.Instance.getAlliesGoodsIcon(-3) : order.Mode == 2
            ? MainViewModel.Instance.getAlliesGoodsIcon(-4) : null;

        private static void MarkOwnerButton(Button button, bool selected)
        {
            button.Background = selected ? ownerSelectedBrush : ownerNormalBrush;
            button.BorderBrush = selected ? selectedOwnerBorder : normalOwnerBorder;
            button.BorderThickness = new Thickness(selected ? 2 : 1);
        }
    }
}
