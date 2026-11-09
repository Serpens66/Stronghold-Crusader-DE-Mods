using System;
using System.Collections.Generic;
using APIShared.Presentation;
using Noesis;
using SHCDESE.Interop;

namespace AutoSendTest
{
    // One stable control tree per vanilla host. Rendering never submits numeric edits.
    internal sealed class AutoSendView
    {
        private readonly AutoSendRuntime runtime;
        private Border root;
        private AutoSendNumberEditor reserve, minimum, maximum;
        private Button enabled;
        private TextBlock toggleText, pendingText;
        private bool hasTogglePaint, paintedOn;
        private readonly Brush activeBackground = new SolidColorBrush(Color.FromArgb(255, 32, 69, 44));
        private readonly Brush activeBorder = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138));
        private ToolTip toggleTip;
        private Style tooltipStyle;
        private AutoSendKeyboardCapture keyboardCapture;
        private readonly Dictionary<eGoods, AutoSendAdornment> badges = new Dictionary<eGoods, AutoSendAdornment>();

        internal AutoSendView(AutoSendRuntime runtime) { this.runtime = runtime; }
        internal FrameworkElement Create(AllyGoodsSectionContext context)
        {
            AutoSendText.Load();
            tooltipStyle = CreateTooltipStyle();
            var grid = new Grid { Margin = new Thickness(4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root = new Border { Child = grid };
            reserve = Editor("Reserve", AutoSendEdit.Reserve);
            minimum = Editor("Minimum", AutoSendEdit.Minimum);
            maximum = Editor("Maximum", AutoSendEdit.Maximum);
            Place(grid, reserve.Element, 0, 0);
            Place(grid, minimum.Element, 1, 0);
            Place(grid, maximum.Element, 0, 1);
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
            var editors = new[] { reserve, minimum, maximum };
            keyboardCapture = new AutoSendKeyboardCapture(root, () => { foreach (var editor in editors) editor.Cancel(); },
                reserve.Slider, reserve.Box, minimum.Slider, minimum.Box, maximum.Slider, maximum.Box);
            Refresh(); return root;
        }
        private AutoSendNumberEditor Editor(string key, AutoSendEdit operation) => new AutoSendNumberEditor(
            "AutoSend" + key, AutoSendText.Get(key), element => AttachTooltip(element, key + "Help"),
            (target, amount) => runtime.EditCaptured(target.SessionId, operation, target.RecipientPlayerId,
                (int)target.Good, amount, false));
        private static void Place(Grid parent, FrameworkElement child, int row, int column)
        { Grid.SetRow(child, row); Grid.SetColumn(child, column); parent.Children.Add(child); }

        internal void Refresh()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context))
            {
                keyboardCapture?.Release();
                if (root != null) root.Visibility = Visibility.Collapsed;
                return;
            }
            RefreshBadges(context);
            if (root == null) return;
            var data = runtime.ReadDisplay(context);
            bool canEdit = context.IsOpen && context.IsSendMode && data.Ready;
            // End the input lease while its controls still belong to their view,
            // before changing their visibility or editability.
            if (!canEdit) keyboardCapture?.Release();
            root.Visibility = context.IsSendMode ? Visibility.Visible : Visibility.Collapsed;
            var target = new AutoSendEditTarget(data.SessionId, data.SenderPlayerId, context.RecipientPlayerId,
                context.Good.GetValueOrDefault());
            var rule = data.HasDesiredValues ? data.DesiredRule : data.Rule;
            string reason = data.TransferUnavailable ? AutoSendText.Get("TransferUnavailable") :
                !data.Ready ? AutoSendText.Get(data.IdentityMismatch ? "IdentityMismatch" : "NotReady") : data.Feedback;
            reserve.Render(target, canEdit, data.HasDesiredValues ? data.DesiredReserve : data.Reserve, reason);
            minimum.Render(target, canEdit, rule.Minimum, reason);
            maximum.Render(target, canEdit, rule.Maximum, reason);
            bool on = data.Rule.Enabled;
            enabled.IsEnabled = canEdit && !data.Pending && (on || rule.Maximum > 0);
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
            string help = !string.IsNullOrEmpty(reason) ? reason : data.Pending ? AutoSendText.Get("Pending") :
                !on && rule.Maximum == 0 ? AutoSendText.Get("StartNeedsMaximum") : AutoSendText.Get("EnabledHelp");
            if (!Equals(toggleTip.Content, help)) toggleTip.Content = help;
        }
        private void ToggleEnabled()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode || !context.Good.HasValue) return;
            var data = runtime.ReadDisplay(context);
            if (!data.Ready || data.Pending || !data.Rule.Enabled && data.Rule.Maximum == 0) return;
            runtime.EditCaptured(data.SessionId, AutoSendEdit.Enabled, context.RecipientPlayerId,
                (int)context.Good.Value, 0, !data.Rule.Enabled);
        }

        internal FrameworkElement CreateAdornment(AllyGoodsSectionContext context, eGoods good)
        {
            AutoSendText.Load();
            var element = new Border { Name = "AutoSendActiveGood" + (int)good, Width = 19, Height = 19,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false, Background = new SolidColorBrush(Color.FromArgb(235, 29, 23, 16)),
                CornerRadius = new CornerRadius(9), Child = new Noesis.Path {
                    Data = Geometry.Parse("M14,6 A6,6 0 1 0 14.8,10 M10.5,3 L14,6 L15,1.5"),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138)), StrokeThickness = 1.8f,
                    Width = 18, Height = 18, IsHitTestVisible = false } };
            var badge = new AutoSendAdornment(element, context.Selection.PanelGeneration);
            badges[good] = badge;
            RefreshBadge(good, badge, context.Selection);
            return element;
        }
        private void RefreshBadges(AllyGoodsPanelContext context)
        { foreach (var item in badges) RefreshBadge(item.Key, item.Value, context); }
        private void RefreshBadge(eGoods good, AutoSendAdornment badge, AllyGoodsPanelContext context)
        {
            bool visible = context.IsSendMode && context.PanelGeneration == badge.ContextGeneration &&
                runtime.ReadDisplayForGood(context, good).Rule.Enabled;
            badge.Element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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
        internal AutoSendAdornment(FrameworkElement element, long generation) { Element = element; ContextGeneration = generation; }
    }
}
