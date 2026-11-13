using System;
using System.Reflection;
using Noesis;
using SHCDESE.API;
using SHCDESE.UI;

namespace AutoSendTest
{
    // TextBox capture alone does not cover Slider keyboard gestures. Keep one lease
    // across this editor's controls and restore before a foreign control captures focus.
    internal sealed class AutoSendKeyboardCapture
    {
        private static readonly DependencyProperty previousTextCapture = PreviousTextCapture();
        private readonly FrameworkElement root;
        private readonly Action cancelDrafts;
        private readonly FrameworkElement[] inputs;
        private Keyboard keyboard;
        private bool active, original, restoring;
        private int generation;
        private UIElement expectedAfterExit;

        internal AutoSendKeyboardCapture(FrameworkElement root, Action cancelDrafts, params FrameworkElement[] inputs)
        {
            this.root = root; this.cancelDrafts = cancelDrafts; this.inputs = inputs;
            root.PreviewGotKeyboardFocus += OnEntering;
            root.PreviewLostKeyboardFocus += OnLeaving;
            root.GotFocus += (_, __) => {
                if (!active) return;
                Apply(true); ReconcileLater(generation);
            };
            // SE's TextBox LostFocus restore runs inline on Unity. Reconcile after
            // the complete focus route, with a fresh guard against foreign owners.
            root.LostFocus += (_, __) => { if (restoring) ReconcileLater(generation); };
            root.Unloaded += (_, __) => Release();
        }

        private void OnEntering(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (active || !Inside(args.NewFocus)) return;
            keyboard = root.Keyboard;
            if (keyboard == null) return;
            bool current = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
            var previousBox = AncestorTextBox(args.OldFocus);
            // A foreign SE textbox still owns global=true until its LostFocus restore.
            // Read its checked binding state, rather than accidentally retaining that true.
            original = current && previousBox != null && KeyboardCaptureBinding.GetEnabled(previousBox)
                ? (bool)previousBox.GetValue(previousTextCapture) : current;
            active = true; restoring = false; generation++;
        }
        private void OnLeaving(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (!active || Inside(args.NewFocus)) return;
            NormalizeOutgoingTextCapture(args.OldFocus);
            expectedAfterExit = args.NewFocus;
            active = false; restoring = true; generation++;
            Apply(original);
        }
        private void ReconcileLater(int version)
        {
            // Enqueue executes inline on Unity; this post-route check must really defer.
            UnityMainThreadDispatcher.Instance.EnqueueDeferred(() => {
                if (version != generation) return;
                UIElement focused = keyboard.FocusedElement;
                if (active && Inside(focused)) Apply(true);
                else if (!active && restoring)
                {
                    if (ReferenceEquals(focused, expectedAfterExit) && AncestorTextBox(focused) == null) Apply(original);
                    restoring = false;
                }
            });
        }
        // Explicitly end this lease before hiding/disabling its editor, independently
        // of whether the visual transition also publishes a keyboard-focus exit.
        internal void Release()
        {
            cancelDrafts();
            if (!active)
            {
                if (restoring) ReconcileLater(generation);
                return;
            }
            var focused = keyboard.FocusedElement;
            if (Inside(focused))
            {
                NormalizeOutgoingTextCapture(focused);
                Apply(original);
                keyboard.ClearFocus();
                focused = keyboard.FocusedElement;
            }
            active = false; restoring = focused == null; expectedAfterExit = focused; generation++;
            if (focused == null) { Apply(original); ReconcileLater(generation); }
        }
        private bool Inside(DependencyObject value)
        {
            for (var node = value; node != null; node = VisualTreeHelper.GetParent(node))
                foreach (var input in inputs) if (ReferenceEquals(node, input)) return true;
            return false;
        }
        private void NormalizeOutgoingTextCapture(DependencyObject focused)
        {
            var box = AncestorTextBox(focused);
            if (box == null || !KeyboardCaptureBinding.GetEnabled(box)) return;
            // Only explicitly registered inputs belong to this lease. Never rewrite
            // a foreign textbox's capture state, even if it shares a visual ancestor.
            foreach (var input in inputs)
                if (ReferenceEquals(box, input))
                { box.SetValue(previousTextCapture, original); return; }
        }
        private static TextBox AncestorTextBox(DependencyObject value)
        {
            for (var node = value; node != null; node = VisualTreeHelper.GetParent(node))
                if (node is TextBox box) return box;
            return null;
        }
        private static void Apply(bool value) => FatControler.instance.SetNoesisKeyboardState(value);
        private static DependencyProperty PreviousTextCapture()
        {
            var field = typeof(KeyboardCaptureBinding).GetField("PreviousKeyboardStateProperty", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || !field.IsPrivate || field.FieldType != typeof(DependencyProperty) ||
                !(field.GetValue(null) is DependencyProperty property) || property.PropertyType != typeof(bool) ||
                property.OwnerType != typeof(KeyboardCaptureBinding))
                throw new InvalidOperationException("The installed SE textbox keyboard-capture contract changed.");
            return property;
        }
    }
}
