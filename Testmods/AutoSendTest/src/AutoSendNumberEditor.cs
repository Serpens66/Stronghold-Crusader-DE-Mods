using System;
using System.Globalization;
using Noesis;
using SHCDESE.Interop;

namespace AutoSendTest
{
    internal readonly struct AutoSendEditTarget
    {
        internal readonly long SessionId;
        internal readonly int SenderPlayerId, RecipientPlayerId;
        internal readonly eGoods Good;
        internal AutoSendEditTarget(long session, int sender, int recipient, eGoods good)
        { SessionId = session; SenderPlayerId = sender; RecipientPlayerId = recipient; Good = good; }
        internal bool Same(AutoSendEditTarget other) => SessionId == other.SessionId && SenderPlayerId == other.SenderPlayerId &&
            RecipientPlayerId == other.RecipientPlayerId && Good == other.Good;
    }

    // Slider coordinates are a bounded convenience, never the saved numeric value.
    // Only user-completed input reaches Commit; Render and focus alone never write.
    internal sealed class AutoSendNumberEditor
    {
        internal readonly FrameworkElement Element;
        internal readonly Slider Slider;
        internal readonly TextBox Box;
        private readonly Action<AutoSendEditTarget, int> commit;
        private readonly ToolTip sliderTip, boxTip;
        private readonly string help;
        private AutoSendEditTarget target, frozen;
        private int modelValue, draft, step = 1;
        private bool allowed, updating, editing, pointer, changed, textEditing, canceling;
        private string reason;

        internal AutoSendNumberEditor(string name, string label, Func<FrameworkElement, ToolTip> tooltip,
            Action<AutoSendEditTarget, int> commit)
        {
            this.commit = commit;
            var row = new Grid { Margin = new Thickness(8, 2, 8, 2), Height = 28 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(87) });
            var caption = new TextBlock { Text = label, Foreground = new SolidColorBrush(Colors.White), FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center };
            Slider = new Slider { Name = name + "Slider", Minimum = 0, Maximum = 100, TickFrequency = 1,
                IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Margin = new Thickness(0, 0, 8, 0), Height = 27 };
            Box = new TextBox { Name = name + "Value", Width = 87, FontSize = 16, Padding = new Thickness(5, 2, 5, 2),
                Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Color.FromArgb(255, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138)), BorderThickness = new Thickness(1),
                MaxLength = 32, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(Slider, 1); Grid.SetColumn(Box, 2);
            row.Children.Add(caption); row.Children.Add(Slider); row.Children.Add(Box);
            Element = row;
            sliderTip = tooltip(Slider); boxTip = tooltip(Box); help = (string)boxTip.Content;
            SHCDESE.UI.KeyboardCaptureBinding.SetEnabled(Box, true);
            Slider.PreviewMouseDown += (_, args) => {
                if (args.ChangedButton != MouseButton.Left) return;
                if (editing && textEditing) FinishText(false);
                Begin(false, true);
            };
            Slider.PreviewMouseUp += (_, args) => { if (args.ChangedButton == MouseButton.Left && pointer) FinishSlider(); };
            Slider.LostMouseCapture += (_, __) => { if (pointer) FinishSlider(); };
            Slider.LostFocus += (_, __) => { if (editing && !textEditing && !pointer) FinishSlider(); };
            Slider.PreviewKeyDown += (_, args) => {
                if (args.Key == Key.Escape) { Cancel(); args.Handled = true; }
                else if (SliderKey(args.Key)) Begin(false, false);
            };
            Slider.PreviewKeyUp += (_, args) => {
                if (!SliderKey(args.Key) || !editing || textEditing) return;
                // Home/End explicitly select a bound even if a larger textbox value
                // already displays its clamped thumb at that same bound.
                if (args.Key == Key.Home || args.Key == Key.End)
                { draft = (int)Math.Round(Slider.Value / step, MidpointRounding.AwayFromZero) * step; changed = true; }
                FinishSlider();
            };
            Slider.ValueChanged += (_, args) => {
                if (updating || !editing || textEditing) return;
                draft = (int)Math.Round(args.NewValue / step, MidpointRounding.AwayFromZero) * step;
                changed = true; SetText(draft);
            };
            Box.GotFocus += (_, __) => { Begin(true, false); Box.SelectAll(); };
            Box.TextChanged += (_, __) => {
                if (updating) return;
                if (!editing) Begin(true, false);
                if (editing && textEditing) changed = true;
            };
            Box.LostFocus += (_, __) => { if (editing && textEditing) FinishText(false); };
            Box.PreviewKeyDown += (_, args) => {
                if (args.Key == Key.Escape) { Cancel(); args.Handled = true; }
                else if (args.Key == Key.Return || args.Key == Key.Enter) { FinishText(true); args.Handled = true; }
            };
            Element.Unloaded += (_, __) => Cancel();
        }

        internal void Render(AutoSendEditTarget next, bool canEdit, int value, string unavailableReason)
        {
            if (editing && (!target.Same(next) || !canEdit)) Cancel();
            target = next; allowed = canEdit; modelValue = value; reason = unavailableReason;
            updating = true;
            try
            {
                bool gold = next.Good == eGoods.STORED_GOLD;
                step = gold ? 10 : 1;
                Slider.Maximum = gold ? 10000 : 100; Slider.TickFrequency = step;
                Slider.SmallChange = step; Slider.LargeChange = step * 10;
                Slider.IsEnabled = Box.IsEnabled = canEdit;
                if (!editing)
                {
                    Slider.Value = Position(value); SetText(value);
                    SetHelp(string.IsNullOrEmpty(reason) ? help : reason);
                }
            }
            finally { updating = false; }
        }

        private void Begin(bool text, bool mouse)
        {
            if (updating || !allowed || editing) return;
            frozen = target; editing = true; textEditing = text; pointer = mouse; changed = false; draft = modelValue;
            SetHelp(help);
        }
        private void FinishSlider()
        {
            if (!editing || textEditing) return;
            bool send = changed; int value = draft; var captured = frozen;
            editing = pointer = changed = false;
            if (send) commit(captured, value); else Restore();
        }
        private void FinishText(bool fromEnter)
        {
            if (!editing || !textEditing) return;
            if (!changed) { editing = textEditing = false; Restore(); return; }
            if (!int.TryParse(Box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
            {
                SetHelp(AutoSendText.Get("InvalidAmount"));
                if (!fromEnter) { editing = textEditing = changed = false; Restore(); SetHelp(AutoSendText.Get("InvalidAmount")); }
                return;
            }
            var captured = frozen;
            editing = textEditing = changed = false;
            SetText(value); commit(captured, value);
        }
        internal void Cancel()
        {
            if (canceling) return;
            canceling = true;
            bool previous = updating; updating = true;
            try
            {
                // Capture release synchronously raises LostMouseCapture/DragCompleted.
                // Clear the draft first so those callbacks cannot submit it.
                editing = textEditing = pointer = changed = false;
                var captured = Slider.Mouse?.Captured;
                for (DependencyObject node = captured; node != null; node = VisualTreeHelper.GetParent(node))
                    if (ReferenceEquals(node, Slider))
                    {
                        if (captured is Thumb thumb) thumb.CancelDrag();
                        if (ReferenceEquals(Slider.Mouse?.Captured, captured)) captured.ReleaseMouseCapture();
                        break;
                    }
                Restore();
            }
            finally { updating = previous; canceling = false; }
        }
        private void Restore()
        {
            bool previous = updating; updating = true;
            try { Slider.Value = Position(modelValue); SetText(modelValue); }
            finally { updating = previous; }
        }
        private float Position(int value) => Math.Max(0, Math.Min(Slider.Maximum,
            (float)(Math.Round((double)value / step, MidpointRounding.AwayFromZero) * step)));
        private void SetText(int value)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            if (Box.Text == text) return;
            bool previous = updating; updating = true;
            try { Box.Text = text; }
            finally { updating = previous; }
        }
        private void SetHelp(string value)
        { if (!Equals(sliderTip.Content, value)) sliderTip.Content = value; if (!Equals(boxTip.Content, value)) boxTip.Content = value; }
        private static bool SliderKey(Key key) => key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down ||
            key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown;
    }
}
