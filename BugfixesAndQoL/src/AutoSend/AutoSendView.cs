using System;
using System.Collections.Generic;
using System.Globalization;
using APIShared.Presentation;
using Noesis;
using SHCDESE.Interop;

namespace BugfixesAndQoL.AutoSend
{
    // One stable control tree per vanilla host. Rendering never submits numeric edits.
    internal sealed partial class AutoSendView
    {
        private readonly AutoSendRuntime runtime;
        private Border root;
        private AutoSendNumberEditor reserve, minimum, maximum, interval;
        private Button enabled;
        private CheckBox chatMessages;
        private TextBlock toggleText, pendingText;
        private bool hasTogglePaint, paintedOn, updatingChatMessages, confirmedChatMessages;
        private readonly Brush activeBackground = new SolidColorBrush(Color.FromArgb(255, 32, 69, 44));
        private readonly Brush activeBorder = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138));
        private ToolTip toggleTip;
        private Style tooltipStyle;
        private AutoSendKeyboardCapture keyboardCapture;
        private readonly Dictionary<eGoods, AutoSendAdornment> badges = new Dictionary<eGoods, AutoSendAdornment>();
        private readonly bool[] activeGoods = new bool[AutoSendGoods.Values.Length];
        private AllyGoodsPanelContext activeGoodsContext, dormantContext, paintedContext;
        private AutoSendDisplay paintedDisplay;
        private bool dormant, featureDormant, paintedExpanded, chatPaintDirty = true;

        internal AutoSendView(AutoSendRuntime runtime) { this.runtime = runtime; }
        internal FrameworkElement Create(AllyGoodsSectionContext context)
        {
            if (tooltipStyle == null) tooltipStyle = CreateTooltipStyle();
            var grid = new Grid { Margin = new Thickness(4, 0, 4, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root = new Border { Child = grid };
            reserve = Editor("Reserve", AutoSendEdit.Reserve);
            minimum = Editor("Minimum", AutoSendEdit.Minimum);
            maximum = Editor("Maximum", AutoSendEdit.Maximum);
            interval = Editor("Interval", AutoSendEdit.Interval,
                minimumValue: AutoSendState.MinimumIntervalSeconds, maximumValue: AutoSendState.MaximumIntervalSeconds,
                invalidAmountKey: "InvalidInterval", valueHelp: seconds => AutoSendText.Get("IntervalTicks")
                    .Replace("{Ticks}", (seconds * AutoSendState.TicksPerSecond).ToString(CultureInfo.InvariantCulture)));
            Place(grid, reserve.Element, 0, 0);
            Place(grid, minimum.Element, 1, 0);
            Place(grid, maximum.Element, 0, 1);
            Place(grid, interval.Element, 2, 0);
            chatMessages = new CheckBox { Name = "AutoSendChatMessages", Height = 28,
                Margin = new Thickness(8, 2, 8, 2), Content = new TextBlock {
                    Text = AutoSendText.Get("ChatMessages"), FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center } };
            chatMessages.Click += (_, __) => ToggleChatMessages();
            chatMessages.Checked += ChatVisualChanged;
            chatMessages.Unchecked += ChatVisualChanged;
            AttachTooltip(chatMessages, "ChatMessagesHelp");
            Place(grid, chatMessages, 2, 1);
            toggleText = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 16 };
            pendingText = new TextBlock { Width = 18, TextAlignment = TextAlignment.Center, FontSize = 16 };
            var toggleContent = new Grid();
            toggleContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toggleContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            Grid.SetColumn(pendingText, 1);
            toggleContent.Children.Add(toggleText); toggleContent.Children.Add(pendingText);
            enabled = new Button { Name = "AutoSendToggle", Content = toggleContent, Height = 28,
                Margin = new Thickness(8, 2, 8, 2), Padding = new Thickness(5, 1, 5, 1) };
            enabled.Click += (_, __) => ToggleEnabled();
            toggleTip = AttachTooltip(enabled, "EnabledHelp");
            Place(grid, enabled, 1, 1);
            hasTogglePaint = false;
            paintedDisplay = null;
            dormant = false;
            chatPaintDirty = true;
            var editors = new[] { reserve, minimum, maximum, interval };
            keyboardCapture = new AutoSendKeyboardCapture(root, () => { foreach (var editor in editors) editor.Cancel(); },
                reserve.Slider, reserve.Box, minimum.Slider, minimum.Box, maximum.Slider, maximum.Box, interval.Slider, interval.Box, chatMessages);
            Refresh(); return root;
        }
        private AutoSendNumberEditor Editor(string key, AutoSendEdit operation, int minimumValue = 0,
            int maximumValue = int.MaxValue, string invalidAmountKey = "InvalidAmount", Func<int, string> valueHelp = null) => new AutoSendNumberEditor(
            "AutoSend" + key, AutoSendText.Get(key), element => AttachTooltip(element, key + "Help"),
            (target, amount) => runtime.EditCaptured(target.SessionId, operation, target.RecipientPlayerId,
                (int)target.Good, amount, false), minimumValue, maximumValue, 1, invalidAmountKey, valueHelp);
        private static void Place(Grid parent, FrameworkElement child, int row, int column)
        { Grid.SetRow(child, row); Grid.SetColumn(child, column); parent.Children.Add(child); }

        internal void Refresh()
        {
            if (!runtime.LocalFeatureEnabled)
            {
                if (featureDormant) return;
                featureDormant = true;
                dormant = true; dormantContext = null; paintedDisplay = null; activeGoodsContext = null;
                keyboardCapture?.Release();
                disclosure.ObserveMenu(0, false, 0);
                pendingDisclosureClick.Cancel(); pendingDisclosurePanel = null;
                if (root != null && root.Visibility != Visibility.Collapsed) root.Visibility = Visibility.Collapsed;
                footerRegistration?.SetVisible(false);
                ApplyDisclosure(false);
                HideBadges();
                return;
            }
            featureDormant = false;
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context))
            {
                if (dormant && dormantContext == null) return;
                dormant = true; dormantContext = null; paintedDisplay = null; activeGoodsContext = null;
                keyboardCapture?.Release();
                if (root != null && root.Visibility != Visibility.Collapsed) root.Visibility = Visibility.Collapsed;
                footerRegistration?.SetVisible(false);
                ApplyDisclosure(false);
                HideBadges();
                return;
            }
            if (!context.IsSendMode && dormant && ReferenceEquals(dormantContext, context)) return;
            dormant = !context.IsSendMode; dormantContext = dormant ? context : null;
            var data = runtime.ReadDisplay(context, activeGoods);
            activeGoodsContext = context;
            bool expanded = RefreshDisclosure(context, data);
            RefreshBadges(context);
            if (!context.IsSendMode)
            {
                keyboardCapture?.Release();
                if (root != null && root.Visibility != Visibility.Collapsed) root.Visibility = Visibility.Collapsed;
                paintedDisplay = null;
                return;
            }
            if (root == null) return;
            if (SamePaint(context, data, expanded) && !chatPaintDirty && !reserve.NeedsRender && !minimum.NeedsRender && !maximum.NeedsRender && !interval.NeedsRender) return;
            bool canEdit = context.IsOpen && context.IsSendMode && data.Ready;
            bool canEditSender = context.IsOpen && context.IsSendMode && data.SenderReady;
            // End the input lease while its controls still belong to their view,
            // before changing their visibility or editability.
            if (!canEdit && !canEditSender) keyboardCapture?.Release();
            var visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            if (root.Visibility != visibility) root.Visibility = visibility;
            var target = new AutoSendEditTarget(data.SessionId, data.SenderPlayerId, context.RecipientPlayerId,
                context.Good.GetValueOrDefault());
            var rule = data.HasDesiredValues ? data.DesiredRule : data.Rule;
            string reason = data.TransferUnavailable ? AutoSendText.Get("TransferUnavailable") :
                !data.Ready ? AutoSendText.Get(data.IdentityMismatch ? "IdentityMismatch" :
                    !context.Good.HasValue ? "SelectGood" :
                    data.SenderPlayerId > 0 && context.ViewedPlayerId != data.SenderPlayerId ? "OwnView" : "NotReady") :
                string.IsNullOrEmpty(data.Feedback) ? string.Empty : AutoSendText.Get("ChangeFailed");
            reserve.Render(target, canEdit, data.HasDesiredValues ? data.DesiredReserve : data.Reserve, reason);
            minimum.Render(target, canEdit, rule.Minimum, reason);
            maximum.Render(target, canEdit, rule.Maximum, reason);
            int seconds = rule.IntervalSeconds;
            // An unavailable selection carries a default struct rather than a rule.
            if (seconds < AutoSendState.MinimumIntervalSeconds || seconds > AutoSendState.MaximumIntervalSeconds)
                seconds = AutoSendState.DefaultIntervalSeconds;
            interval.Render(target, canEdit, seconds, reason);
            ShowChatMessages(data.ChatMessages);
            bool chatEnabled = canEditSender && !data.Pending;
            if (chatMessages.IsEnabled != chatEnabled) chatMessages.IsEnabled = chatEnabled;
            bool on = data.Rule.Enabled;
            bool ruleEnabled = canEdit && !data.Pending && (on || rule.Maximum > 0);
            if (enabled.IsEnabled != ruleEnabled) enabled.IsEnabled = ruleEnabled;
            string label = AutoSendText.Get(on ? "EnabledOn" : "EnabledOff");
            if (toggleText.Text != label) toggleText.Text = label;
            string pending = data.Pending ? "…" : "";
            if (pendingText.Text != pending) pendingText.Text = pending;
            if (!hasTogglePaint || paintedOn != on)
            {
                if (on)
                { enabled.Background = activeBackground; enabled.BorderBrush = activeBorder; enabled.BorderThickness = new Thickness(2); }
                else
                {
                    enabled.ClearValue(Control.BackgroundProperty); enabled.ClearValue(Control.BorderBrushProperty);
                    enabled.ClearValue(Control.BorderThicknessProperty);
                }
                paintedOn = on; hasTogglePaint = true;
            }
            string help = !data.ChatMessages && data.ChatSuppressionUnavailable ? AutoSendText.Get("SuppressionUnavailable") :
                !string.IsNullOrEmpty(reason) ? reason : data.Pending ? AutoSendText.Get("Pending") :
                !on && rule.Maximum == 0 ? AutoSendText.Get("StartNeedsMaximum") : AutoSendText.Get("EnabledHelp");
            if (!Equals(toggleTip.Content, help)) toggleTip.Content = help;
            paintedContext = context; paintedDisplay = data; paintedExpanded = expanded;
        }
        private bool SamePaint(AllyGoodsPanelContext context, AutoSendDisplay data, bool expanded)
        {
            var old = paintedDisplay; var previous = paintedContext;
            if (old == null || previous == null || paintedExpanded != expanded ||
                previous.LocalPlayerId != context.LocalPlayerId || previous.ViewedPlayerId != context.ViewedPlayerId ||
                previous.RecipientPlayerId != context.RecipientPlayerId || previous.Good != context.Good ||
                previous.PanelGeneration != context.PanelGeneration || previous.IsOpen != context.IsOpen || previous.IsSendMode != context.IsSendMode) return false;
            return old.HasSamePaint(data);
        }
        private void ToggleEnabled()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode || !context.Good.HasValue) return;
            var data = runtime.ReadDisplay(context);
            if (!data.Ready || data.Pending || !data.Rule.Enabled && data.Rule.Maximum == 0) return;
            runtime.EditCaptured(data.SessionId, AutoSendEdit.Enabled, context.RecipientPlayerId,
                (int)context.Good.Value, 0, !data.Rule.Enabled);
        }

        private void ShowChatMessages(bool value)
        {
            confirmedChatMessages = value;
            updatingChatMessages = true;
            try { if (chatMessages.IsChecked != value) chatMessages.IsChecked = value; chatPaintDirty = false; }
            finally { updatingChatMessages = false; }
        }
        private void ChatVisualChanged(object sender, RoutedEventArgs args)
        { if (!updatingChatMessages) chatPaintDirty = true; }
        private void ToggleChatMessages()
        {
            if (updatingChatMessages) return;
            bool requested = chatMessages.IsChecked == true;
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context))
            { ShowChatMessages(confirmedChatMessages); return; }
            var data = runtime.ReadDisplay(context);
            // The control shows acknowledged state while the global edit is in flight.
            // IsChecked changes never dispatch this Click handler.
            ShowChatMessages(data.ChatMessages);
            if (!context.IsOpen || !context.IsSendMode || !data.SenderReady || data.Pending) return;
            runtime.EditCaptured(data.SessionId, AutoSendEdit.ChatMessages, 0, 0, 0, requested);
        }

        internal FrameworkElement CreateAdornment(AllyGoodsSectionContext context, eGoods good)
        {
            var element = new Border { Name = "AutoSendActiveGood" + (int)good, Width = 19, Height = 19,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false, Background = new SolidColorBrush(Color.FromArgb(235, 29, 23, 16)),
                CornerRadius = new CornerRadius(9), Child = new Noesis.Path {
                    Data = Geometry.Parse("M14,6 A6,6 0 1 0 14.8,10 M10.5,3 L14,6 L15,1.5"),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138)), StrokeThickness = 1.8f,
                    Width = 18, Height = 18, IsHitTestVisible = false } };
            int goodIndex = -1;
            for (int index = 0; index < AutoSendGoods.Values.Length; index++) if (AutoSendGoods.Values[index] == good) { goodIndex = index; break; }
            var badge = new AutoSendAdornment(element, context.Selection.PanelGeneration, goodIndex);
            badges[good] = badge;
            // Factories for all wares share one snapshot for this immutable context.
            if (!ReferenceEquals(activeGoodsContext, context.Selection))
            { runtime.ReadDisplay(context.Selection, activeGoods); activeGoodsContext = context.Selection; }
            RefreshBadge(badge, context.Selection);
            return element;
        }
        private void RefreshBadges(AllyGoodsPanelContext context)
        { foreach (var item in badges) RefreshBadge(item.Value, context); }
        private void RefreshBadge(AutoSendAdornment badge, AllyGoodsPanelContext context)
        {
            bool visible = runtime.LocalFeatureEnabled && context.IsSendMode && context.PanelGeneration == badge.ContextGeneration &&
                badge.GoodIndex >= 0 && activeGoods[badge.GoodIndex];
            PaintBadge(badge, visible);
        }
        private void HideBadges() { foreach (var item in badges) PaintBadge(item.Value, false); }
        private static void PaintBadge(AutoSendAdornment badge, bool visible)
        {
            if (badge.HasPaint && badge.Visible == visible) return;
            badge.Element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            badge.HasPaint = true; badge.Visible = visible;
        }
        private static Style CreateTooltipStyle() => GUI.ParseXaml(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToolTip}'>
<Setter Property='Background' Value='#FF1D1710'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderBrush' Value='#FFF2D48A'/><Setter Property='BorderThickness' Value='1'/><Setter Property='FontSize' Value='23'/><Setter Property='MaxWidth' Value='780'/>
<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' MaxWidth='{TemplateBinding MaxWidth}'><TextBlock Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}' FontSize='{TemplateBinding FontSize}' TextWrapping='Wrap'/></Border></ControlTemplate></Setter.Value></Setter></Style>") as Style;
        private ToolTip AttachTooltip(FrameworkElement element, string key)
        {
            var tip = new ToolTip { Style = tooltipStyle, Content = AutoSendText.Get(key), IsHitTestVisible = false };
            APIShared.ModSettings.SharedToolTipSizing.SetEnabled(tip, true);
            ToolTipService.SetToolTip(element, tip); ToolTipService.SetShowDuration(element, 60000);
            ToolTipService.SetShowOnDisabled(element, true);
            return tip;
        }
    }
    internal sealed class AutoSendAdornment
    {
        internal readonly FrameworkElement Element;
        internal readonly long ContextGeneration;
        internal readonly int GoodIndex;
        internal bool HasPaint, Visible;
        internal AutoSendAdornment(FrameworkElement element, long generation, int goodIndex)
        { Element = element; ContextGeneration = generation; GoodIndex = goodIndex; }
    }
}
