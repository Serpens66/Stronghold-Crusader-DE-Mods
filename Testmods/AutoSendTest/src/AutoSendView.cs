using System;
using APIShared.Presentation;
using Noesis;

namespace AutoSendTest
{
    internal sealed partial class AutoSendView
    {
        private readonly AutoSendRuntime runtime;
        private Border root;
        private TextBlock title, status;
        private Button reserve, minimum, maximum, enabled, paused;
        private Style tooltipStyle;
        internal AutoSendView(AutoSendRuntime runtime) { this.runtime = runtime; }
        internal FrameworkElement Create(AllyGoodsSectionContext context)
        {
            AutoSendText.Load();
            tooltipStyle = GUI.ParseXaml(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToolTip}'>
<Setter Property='Background' Value='#FF1D1710'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderBrush' Value='#FFF2D48A'/><Setter Property='BorderThickness' Value='1'/><Setter Property='FontSize' Value='23'/><Setter Property='MaxWidth' Value='780'/>
<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' MaxWidth='{TemplateBinding MaxWidth}'><TextBlock Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}' FontSize='{TemplateBinding FontSize}' TextWrapping='Wrap'/></Border></ControlTemplate></Setter.Value></Setter></Style>") as Style;
            var body = new StackPanel { Margin = new Thickness(12), Orientation = Orientation.Vertical };
            root = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138)), BorderThickness = new Thickness(1), Child = body };
            title = Text(18); body.Children.Add(title);
            var values = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 5) };
            reserve = Action(values, "ReserveHelp", () => Adopt(AutoSendEdit.Reserve));
            minimum = Action(values, "MinimumHelp", () => Adopt(AutoSendEdit.Minimum));
            maximum = Action(values, "MaximumHelp", () => Adopt(AutoSendEdit.Maximum));
            body.Children.Add(values);
            var switches = new StackPanel { Orientation = Orientation.Horizontal };
            enabled = Action(switches, "EnabledHelp", ToggleEnabled);
            paused = Action(switches, "PausedHelp", TogglePaused);
            body.Children.Add(switches);
            status = Text(15); status.Margin = new Thickness(0, 7, 0, 0); body.Children.Add(status);
            AddDiagnostics(body);
            Refresh(); return root;
        }
        private static TextBlock Text(float size) => new TextBlock { Foreground = new SolidColorBrush(Colors.White), FontSize = size, TextWrapping = TextWrapping.Wrap };
        private Button Action(StackPanel panel, string help, Action execute)
        {
            var button = new Button { Width = 195, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(7), FontSize = 16 };
            button.Click += (_, __) => execute();
            var tip = new ToolTip { Style = tooltipStyle, Content = AutoSendText.Get(help), IsHitTestVisible = false };
            SHCDESE.UI.ToolTipResolutionScale.SetEnabled(tip, true);
            ToolTipService.SetToolTip(button, tip); ToolTipService.SetShowDuration(button, 60000);
            panel.Children.Add(button); return button;
        }
        internal void Refresh()
        {
            if (root == null || !AllyGoodsPanelExtensions.TryGetContext(out var context)) return;
            var data = runtime.ReadDisplay(context);
            root.Visibility = context.IsSendMode ? Visibility.Visible : Visibility.Collapsed;
            bool canUse = data.Ready && !data.Pending;
            bool amount = canUse && context.HasSelection;
            reserve.IsEnabled = minimum.IsEnabled = maximum.IsEnabled = amount;
            enabled.IsEnabled = canUse && data.Selected;
            paused.IsEnabled = canUse;
            Set(title, AutoSendText.Get("Title") + " — " + AutoSendText.Get("ActiveCount").Replace("{Count}", data.ActiveRules.ToString()));
            Label(reserve, "Reserve", data.Reserve); Label(minimum, "Minimum", data.Rule.Minimum); Label(maximum, "Maximum", data.Rule.Maximum);
            Content(enabled, AutoSendText.Get(data.Rule.Enabled ? "EnabledOn" : "EnabledOff"));
            Content(paused, AutoSendText.Get(data.Paused ? "Resume" : "Pause"));
            string state = data.TransferUnavailable ? AutoSendText.Get("TransferUnavailable") :
                !data.Ready ? AutoSendText.Get(data.IdentityMismatch ? "IdentityMismatch" : "NotReady") :
                data.Pending ? AutoSendText.Get("Pending") : data.Paused ? AutoSendText.Get("Paused") :
                !data.Rule.Enabled ? AutoSendText.Get("Inactive") : data.SourceStock <= data.Reserve ? AutoSendText.Get("ReserveWait") :
                data.RecipientStock >= data.Rule.Maximum ? AutoSendText.Get("TargetReached") : data.Capacity <= 0 ? AutoSendText.Get("Full") :
                data.RecipientStock < data.Rule.Minimum ? AutoSendText.Get("Emergency") : AutoSendText.Get("Supplying");
            Set(status, AutoSendText.Get("Stocks").Replace("{Own}", data.SourceStock.ToString()).Replace("{Ally}", data.RecipientStock.ToString()) +
                "  " + state + (string.IsNullOrEmpty(data.Feedback) ? "" : "\n" + data.Feedback));
            RefreshDiagnostics();
        }
        private static void Set(TextBlock block, string value) { if (block.Text != value) block.Text = value; }
        private static void Content(Button button, string value)
        {
            if (button.Content is TextBlock label) { if (label.Text != value) label.Text = value; }
            else button.Content = new TextBlock { Text = value, MaxWidth = 175, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        }
        private static void Label(Button button, string key, int value) => Content(button, AutoSendText.Get(key) + ": " + value);
        private void Adopt(AutoSendEdit operation)
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode || !context.HasSelection) return;
            var display = runtime.ReadDisplay(context);
            if (!display.Ready || display.Pending) return;
            runtime.Edit(operation, context.RecipientPlayerId, (int)context.Good.Value, context.Amount.Value, false);
        }
        private void ToggleEnabled()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode || !context.Good.HasValue) return;
            var data = runtime.ReadDisplay(context);
            if (data.Ready && !data.Pending) runtime.Edit(AutoSendEdit.Enabled, context.RecipientPlayerId, (int)context.Good.Value, 0, !data.Rule.Enabled);
        }
        private void TogglePaused()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode) return;
            var data = runtime.ReadDisplay(context);
            if (data.Ready && !data.Pending) runtime.Edit(AutoSendEdit.Paused, 0, 0, 0, !data.Paused);
        }
        partial void AddDiagnostics(StackPanel body);
        partial void RefreshDiagnostics();
    }
}
