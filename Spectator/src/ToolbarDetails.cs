using System;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static readonly Brush neutralPlayerBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private static readonly Brush normalOwnerBorder = new SolidColorBrush(Color.FromArgb(176, 114, 36, 28));
        private static readonly Brush selectedOwnerBorder = new SolidColorBrush(Color.FromRgb(239, 198, 112));
        private static Image currentOrderIcon;
        private static TextBlock currentOrderText, currentOrderTarget;
        private static PresentedAiOrder shownCurrentOrder;
        private static int shownOrderActor = -1;
        private static string shownOrderLocale;
        private static TextBlock statusText;
        private static void OnToolsModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            toolbarDirty = true;
            toolbarLayoutDirty = true;
            if (args.PropertyName == nameof(TestToolsViewModel.ExpandedVisibility) && toolsModel?.Expanded != true)
            { ClosePlayerTooltips(); amountInput?.EndEditing(); }
        }

        private static Brush PlayerBrush(int player) => player > 0 && player < humanOwnerNumbers.Length
            ? humanOwnerNumbers[player]?.Foreground ?? neutralPlayerBrush : neutralPlayerBrush;

        private static bool HitPanel(FrameworkElement element, MouseButtonEventArgs args)
        {
            if (element == null || !element.IsVisible) return false;
            Point p = args.GetPosition(element);
            if (ReferenceEquals(element, humanPanel))
            {
                foreach (var piece in paintedPieces)
                    if (p.X >= piece.X && p.X < piece.Right && p.Y >= piece.Y && p.Y < piece.Bottom) return true;
                return false;
            }
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

        private static void ClosePlayerTooltips() => MenuToolTipSizing.CloseAll();

        private static void RefreshColouredStatus(TextBlock target = null)
        {
            var text = target ?? statusText;
            if (text == null || toolsModel == null) return;
            string value = toolsModel.StatusForPage(toolsModel.ActiveViewKey) ?? "";
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
                MenuToolTipSizing.SetEnabled(tip, true);
            }
        }

        private static void RefreshOrderPresentation(bool force = false)
        {
            var state = GameData.Instance?.lastGameState;
            if (state == null || toolsModel?.Expanded != true || currentOrderText == null) return;
            var selected = PresentedAiOrder.Read(humanSelectedOwner, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
            string locale = SerpLocalization.GetActiveLocale();
            if (!force && shownOrderActor == humanSelectedOwner && selected.Equals(shownCurrentOrder) && shownOrderLocale == locale) return;
            shownOrderActor = humanSelectedOwner; shownCurrentOrder = selected; shownOrderLocale = locale;
            currentOrderIcon.Source = OrderIcon(selected);
            currentOrderText.Text = SerpLocalization.Get("Tools.CurrentOrder") + ": " + SerpLocalization.Get(
                selected.Mode == 1 ? "Tools.Attack" : selected.Mode == 2 ? "Tools.Defend" : "Tools.NoOrder");
            currentOrderTarget.Text = selected.Mode == 0 ? "" : selected.Target > 0
                ? selected.Target.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
            if (ToolTipService.GetToolTip(currentOrderTarget) is ToolTip tip)
                tip.Content = selected.Target > 0 ? PlayerLabel(selected.Target) : SerpLocalization.Get("Tools.UnknownTarget");
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
