using System;
using System.Globalization;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL.AutoSend
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
        private readonly string help, invalidAmountKey;
        private readonly int minimumValue, maximumValue, sliderStep;
        private readonly Func<int, string> valueHelp;
        private readonly AutoSendSliderGesture sliderGesture = new AutoSendSliderGesture();
        private AutoSendEditTarget target, frozen;
        private int modelValue, step = 1;
        private int helpValue = int.MinValue;
        private string valueHelpText;
        private bool allowed, updating, editing, pointer, changed, textEditing, canceling;
        private string reason;
        private bool hasRendered, paintDirty = true, hasRange, paintedEnabled;
        private int paintedMaximum, paintedStep, formattedNumber;
        private string formattedValue, sliderHelp, boxHelp;
        internal bool NeedsRender => paintDirty;

        internal AutoSendNumberEditor(string name, string label, Func<FrameworkElement, ToolTip> tooltip,
            Action<AutoSendEditTarget, int> commit, int minimumValue = 0, int maximumValue = int.MaxValue,
            int sliderStep = 1, string invalidAmountKey = "InvalidAmount", Func<int, string> valueHelp = null)
        {
            if (minimumValue < 0 || maximumValue < minimumValue || sliderStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumValue));
            this.commit = commit;
            this.minimumValue = minimumValue; this.maximumValue = maximumValue; this.sliderStep = sliderStep;
            this.invalidAmountKey = invalidAmountKey; this.valueHelp = valueHelp;
            var row = new Grid { Margin = new Thickness(8, 2, 8, 2), Height = 28 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(87) });
            var caption = new TextBlock { Text = label, Foreground = new SolidColorBrush(Colors.White), FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center };
            Slider = new Slider { Name = name + "Slider", Minimum = minimumValue,
                Maximum = maximumValue == int.MaxValue ? 100 : maximumValue, TickFrequency = sliderStep,
                IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Margin = new Thickness(0, 0, 8, 0), Height = 27 };
            Box = new TextBox { Name = name + "Value", Width = 87, FontSize = 16, Padding = new Thickness(5, 2, 5, 2),
                Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Color.FromArgb(255, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 242, 212, 138)), BorderThickness = new Thickness(1),
                MaxLength = 32, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(Slider, 1); Grid.SetColumn(Box, 2);
            row.Children.Add(caption); row.Children.Add(Slider); row.Children.Add(Box);
            Element = row;
            sliderTip = tooltip(Slider); boxTip = tooltip(Box); help = (string)boxTip.Content;
            sliderHelp = sliderTip.Content as string; boxHelp = boxTip.Content as string;
            SHCDESE.UI.KeyboardCaptureBinding.SetEnabled(Box, true);
            // The parent sees preview input before Slider's move-to-point/class
            // handling can change Value or consume the event.
            row.PreviewMouseDown += (_, args) => {
                if (args.ChangedButton != MouseButton.Left || !FromSlider(args.Source)) return;
                if (editing) { if (textEditing) FinishText(false); else FinishSlider(); }
                Begin(false, true);
                // A track click need not capture the mouse. Complete it after the
                // route; a captured thumb instead waits for release.
                ScheduleSliderFinish(true);
            };
            row.PreviewMouseUp += (_, args) => {
                if (args.ChangedButton == MouseButton.Left && pointer) ScheduleSliderFinish(false);
            };
            Slider.LostMouseCapture += (_, __) => { if (pointer) ScheduleSliderFinish(true); };
            Slider.LostFocus += (_, __) => { if (editing && !textEditing && !pointer) FinishSlider(); };
            row.PreviewKeyDown += (_, args) => {
                if (!FromSlider(args.Source)) return;
                if (args.Key == Key.Escape) { Cancel(); args.Handled = true; }
                else if (SliderKey(args.Key)) Begin(false, false);
            };
            row.PreviewKeyUp += (_, args) => {
                if (!FromSlider(args.Source) || !SliderKey(args.Key) || !editing || textEditing || pointer) return;
                // Home/End explicitly select a bound even if a larger textbox value
                // already displays its clamped thumb at that same bound.
                if (args.Key == Key.Home || args.Key == Key.End)
                    sliderGesture.ValueChanged();
                FinishSlider();
            };
            Slider.ValueChanged += (_, args) => {
                if (updating || !editing || textEditing) return;
                paintDirty = true;
                int value = RoundedSliderValue(args.NewValue);
                sliderGesture.ValueChanged(); SetText(value);
                if (valueHelp != null) SetHelp(HelpFor(value));
            };
            Box.GotFocus += (_, __) => {
                if (editing && !textEditing) FinishSlider();
                Begin(true, false); Box.SelectAll();
            };
            Box.TextChanged += (_, __) => {
                if (updating) return;
                paintDirty = true;
                if (!editing) Begin(true, false);
                if (editing && textEditing)
                {
                    changed = true;
                    if (valueHelp != null && TryReadValue(out int value)) SetHelp(HelpFor(value));
                }
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
            if (hasRendered && !paintDirty && target.Same(next) && allowed == canEdit && modelValue == value &&
                string.Equals(reason, unavailableReason, StringComparison.Ordinal)) return;
            paintDirty = true;
            if (editing && (!target.Same(next) || !canEdit)) Cancel();
            target = next; allowed = canEdit; modelValue = value; reason = unavailableReason;
            updating = true;
            try
            {
                // Quantity editors retain their independent display range and free text.
                // A finite input maximum instead fixes both bounds, regardless of goods.
                bool quantity = maximumValue == int.MaxValue;
                bool gold = quantity && next.Good == eGoods.STORED_GOLD;
                step = quantity ? gold ? 10 : 1 : sliderStep;
                int upper = quantity ? gold ? 10000 : 100 : maximumValue;
                if (!hasRange || paintedMaximum != upper) Slider.Maximum = upper;
                if (!hasRange || paintedStep != step)
                { Slider.TickFrequency = step; Slider.SmallChange = step; Slider.LargeChange = step * 10; }
                hasRange = true; paintedMaximum = upper; paintedStep = step;
                if (!hasRendered || paintedEnabled != canEdit)
                { Slider.IsEnabled = Box.IsEnabled = canEdit; paintedEnabled = canEdit; }
                if (!editing)
                {
                    float position = Position(value);
                    if (Slider.Value != position) Slider.Value = position;
                    SetText(value);
                    SetHelp(string.IsNullOrEmpty(reason) ? HelpFor(value) : reason);
                }
                hasRendered = true; paintDirty = false;
            }
            finally { updating = false; }
        }

        private void Begin(bool text, bool mouse)
        {
            if (updating || !allowed || editing) return;
            paintDirty = true;
            frozen = target; editing = true; textEditing = text; pointer = mouse; changed = false;
            if (!text) sliderGesture.Begin(RoundedSliderValue(Slider.Value));
            SetHelp(HelpFor(modelValue));
        }
        private void ScheduleSliderFinish(bool onlyWithoutCapture)
        {
            long generation = sliderGesture.Generation;
            UnityMainThreadDispatcher.Instance.EnqueueDeferred(() => {
                // Capture may move between children during the same input route.
                // Old callbacks must never finish a newer gesture or a new selection.
                if (onlyWithoutCapture && Slider.IsMouseCaptureWithin) return;
                FinishSlider(generation);
            });
        }
        private bool FromSlider(object source)
        {
            for (var node = source as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
                if (ReferenceEquals(node, Slider)) return true;
            return false;
        }
        private void FinishSlider(long? generation = null)
        {
            if (!editing || textEditing) return;
            int value = RoundedSliderValue(Slider.Value);
            if (!sliderGesture.TryComplete(generation ?? sliderGesture.Generation, value, out bool send)) return;
            paintDirty = true;
            var captured = frozen;
            editing = pointer = changed = false;
            if (send) commit(captured, value); else Restore();
        }
        private void FinishText(bool fromEnter)
        {
            if (!editing || !textEditing) return;
            paintDirty = true;
            if (!changed) { editing = textEditing = false; Restore(); return; }
            if (!TryReadValue(out int value))
            {
                SetHelp(AutoSendText.Get(invalidAmountKey));
                if (!fromEnter) { editing = textEditing = changed = false; Restore(); SetHelp(AutoSendText.Get(invalidAmountKey)); }
                return;
            }
            var captured = frozen;
            editing = textEditing = changed = false;
            SetText(value);
            if (valueHelp != null) SetHelp(HelpFor(value));
            commit(captured, value);
        }
        internal void Cancel()
        {
            if (canceling) return;
            paintDirty = true;
            canceling = true;
            bool previous = updating; updating = true;
            try
            {
                // Capture release synchronously raises LostMouseCapture/DragCompleted.
                // Clear the draft first so those callbacks cannot submit it.
                editing = textEditing = pointer = changed = false;
                sliderGesture.Cancel();
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
            try
            {
                float position = Position(modelValue);
                if (Slider.Value != position) Slider.Value = position;
                SetText(modelValue);
                if (valueHelp != null) SetHelp(string.IsNullOrEmpty(reason) ? HelpFor(modelValue) : reason);
            }
            finally { updating = previous; }
        }
        private float Position(int value) => Math.Max(Slider.Minimum, Math.Min(Slider.Maximum,
            (float)(Slider.Minimum + Math.Round(((double)value - Slider.Minimum) / step, MidpointRounding.AwayFromZero) * step)));
        private int RoundedSliderValue(float value) => (int)Math.Max(Slider.Minimum, Math.Min(Slider.Maximum,
            Slider.Minimum + (float)Math.Round((value - Slider.Minimum) / step, MidpointRounding.AwayFromZero) * step));
        private bool TryReadValue(out int value) => int.TryParse(Box.Text, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value) && value >= minimumValue && value <= maximumValue;
        private string HelpFor(int value)
        {
            if (valueHelp == null) return help;
            if (helpValue != value)
            { helpValue = value; valueHelpText = help + "\n" + valueHelp(value); }
            return valueHelpText;
        }
        private void SetText(int value)
        {
            if (formattedValue == null || formattedNumber != value)
            { formattedNumber = value; formattedValue = value.ToString(CultureInfo.InvariantCulture); }
            if (Box.Text == formattedValue) return;
            bool previous = updating; updating = true;
            try { Box.Text = formattedValue; }
            finally { updating = previous; }
        }
        private void SetHelp(string value)
        {
            if (!string.Equals(sliderHelp, value, StringComparison.Ordinal)) { sliderTip.Content = value; sliderHelp = value; }
            if (!string.Equals(boxHelp, value, StringComparison.Ordinal)) { boxTip.Content = value; boxHelp = value; }
        }
        private static bool SliderKey(Key key) => key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down ||
            key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown;
    }
}
