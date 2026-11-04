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
        private static TextBlock statusText, orderStatusText, statusShadow, orderStatusShadow;
        private static void OnToolsModelChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            ToolbarRefresh refresh = ToolbarRefreshPolicy.ForProperty(args.PropertyName);
            if ((refresh & ToolbarRefresh.Status) != 0) RequestToolbarStatus();
            if ((refresh & ToolbarRefresh.Layout) != 0) RequestToolbarLayout();
            if ((refresh & ToolbarRefresh.State) != 0) InvalidatePresentationState();
            if (args.PropertyName == nameof(TestToolsViewModel.ExpandedVisibility) && toolsModel?.Expanded != true)
            { ClosePlayerTooltips(); amountInput?.EndEditing(); }
        }

        private static Brush PlayerBrush(int player) => player > 0 && player < humanOwnerNumbers.Length
            ? humanOwnerNumbers[player]?.Foreground ?? neutralPlayerBrush : neutralPlayerBrush;

        private static void OnToolbarOutsideClick(object sender, MouseButtonEventArgs args)
        {
            // Preview runs before a different text input takes focus; leave the original click untouched.
            amountInput?.PointerDown(args);
        }

        private static void ReleaseAmountForMapClick()
        {
            var input = amountInput;
            if (input == null || !input.OwnsKeyboard) return;
            long session = CurrentSessionId;
            if (log.IsMainThread) input.OutsidePointerDown(false);
            else PostPresentation(() => {
                if (IsCurrentSession(session) && ReferenceEquals(amountInput, input)) input.OutsidePointerDown(false);
            });
        }

        private static void ClosePlayerTooltips() => MenuToolTipSizing.CloseAll();

        private static void RefreshColouredStatus()
        {
            RenderStatus(statusText, statusShadow);
            RenderStatus(orderStatusText, orderStatusShadow);
        }

        private static void RenderStatus(TextBlock text, TextBlock shadow)
        {
            if (text == null || toolsModel == null) return;
            string value = toolsModel.Status;
            FillStatusInlines(text, value, true, shadow);
            var previous = ToolTipService.GetToolTip(text) as ToolTip;
            if (string.IsNullOrEmpty(value))
            {
                if (previous != null) { previous.IsOpen = false; MenuToolTipSizing.SetEnabled(previous, false); }
                ToolTipService.SetToolTip(text, null);
                return;
            }
            var rich = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = neutralPlayerBrush };
            var richShadow = new TextBlock();
            StatusInlineFormat.BindShadow(richShadow, rich);
            FillStatusInlines(rich, value, false, richShadow);
            var tip = previous ?? new ToolTip { Style = humanPanel.TryFindResource("PlayerTooltip") as Style };
            // The shared scaler owns the ToolTip font; both rich layers follow it.
            BindingOperations.SetBinding(rich, TextBlock.FontSizeProperty,
                new Binding(nameof(ToolTip.FontSize)) { Source = tip });
            var layers = new Grid(); layers.Children.Add(richShadow); layers.Children.Add(rich);
            tip.Content = layers;
            ToolTipService.SetToolTip(text, tip);
            MenuToolTipSizing.SetEnabled(tip, true);
        }

        private static void FillStatusInlines(TextBlock text, string value, bool emphasize = false, TextBlock shadow = null)
        {
            text.Effect = null;
            text.Inlines.Clear();
            shadow?.Inlines.Clear();
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
                if (match < 0) { AddStatusFragment(text, shadow, value.Substring(offset), null, emphasize); break; }
                if (match > offset) AddStatusFragment(text, shadow, value.Substring(offset, match - offset), null, emphasize);
                AddStatusFragment(text, shadow, labelMatch, PlayerBrush(playerMatch), emphasize);
                offset = match + labelMatch.Length;
            }
        }

        private static void AddStatusFragment(TextBlock text, TextBlock shadow, string value, Brush colour, bool emphasize)
        {
            text.Inlines.Add(StatusInlineFormat.Create(text, value, colour, emphasize));
            shadow?.Inlines.Add(StatusInlineFormat.CreateShadow(text, value, colour, emphasize));
        }

        private static void RefreshOrderPresentation(bool force = false)
        {
            var state = GameData.Instance?.lastGameState;
            if (state == null || toolsModel?.Expanded != true || currentOrderText == null) return;
            var selected = PresentedAiOrder.Read(humanSelectedOwner, state.skirmish_order, state.skirmish_order_player, state.skirmish_order_from_player);
            string locale = ActiveLocale();
            if (!force && shownOrderActor == humanSelectedOwner && selected.Equals(shownCurrentOrder) && shownOrderLocale == locale) return;
            shownOrderActor = humanSelectedOwner; shownCurrentOrder = selected; shownOrderLocale = locale;
            currentOrderIcon.Source = OrderIcon(selected);
            currentOrderText.Text = SerpLocalization.Get("Tools.CurrentOrder") + ": " + SerpLocalization.Get(
                selected.Mode == 1 ? "Tools.Attack" : selected.Mode == 2 ? "Tools.Defend" : "Tools.NoOrder");
            currentOrderTarget.Text = selected.Mode == 0 ? "" : selected.Target > 0
                ? selected.Target.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
            if (ToolTipService.GetToolTip(currentOrderTarget) is ToolTip tip)
            {
                tip.Content = new TextBlock {
                    Text = selected.Target > 0 ? PlayerLabel(selected.Target) : SerpLocalization.Get("Tools.UnknownTarget"),
                    Foreground = neutralPlayerBrush, TextWrapping = TextWrapping.Wrap,
                    Effect = StatusInlineFormat.Shadow()
                };
                MenuToolTipSizing.SetEnabled(tip, true);
            }
            currentOrderTarget.Foreground = PlayerBrush(selected.Target);
        }

        // Vanilla's HUD_AlliesPanel uses these two getAlliesGoodsIcon keys for the live order pips.
        private static ImageSource OrderIcon(PresentedAiOrder order) => order.Mode == 1
            ? MainViewModel.Instance.getAlliesGoodsIcon(-3) : order.Mode == 2
            ? MainViewModel.Instance.getAlliesGoodsIcon(-4) : null;

        private static void MarkOwnerButton(Button button, bool selected)
        {
            button.Background = toolsModel?.ClassicTheme == true ?
                (selected ? toolsModel.MenuActive : toolsModel.MenuButton) : (selected ? ownerSelectedBrush : ownerNormalBrush);
            button.BorderBrush = selected ? selectedOwnerBorder : normalOwnerBorder;
            button.BorderThickness = new Thickness(selected ? 2 : 1);
        }
    }
}
