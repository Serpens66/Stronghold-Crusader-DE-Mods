using Noesis;
using CrusaderDE;

namespace Spectator
{
    // UI-lifetime handlers only: paste is validated as a whole, never filtered or truncated.
    internal sealed class CreditAmountInput
    {
        private readonly TextBox control;
        private readonly TestToolsViewModel model;
        private string validText;
        private int validStart, validLength;
        private bool restoring;
        private volatile bool ownsKeyboard;
        private bool previousKeyboard;
        internal bool OwnsKeyboard => ownsKeyboard;

        internal void OutsidePointerDown(bool insideMenu)
        { if (!insideMenu && ownsKeyboard) EndEditing(); }

        internal CreditAmountInput(TextBox control, TestToolsViewModel model)
        {
            this.control = control;
            this.model = model;
            validText = model.Amount;
            control.Text = validText;
            control.PreviewTextInput += BeforeTextInput;
            control.TextChanged += TextChanged;
            control.SelectionChanged += SelectionChanged;
            control.LostFocus += LostFocus;
            control.GotKeyboardFocus += GotKeyboardFocus;
            control.LostKeyboardFocus += LostKeyboardFocus;
            control.IsVisibleChanged += VisibilityChanged;
        }

        private static bool IsTextInput(UIElement element) => element is TextBoxBase || element is PasswordBox;

        private void GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (!control.IsKeyboardFocused || ownsKeyboard || FatControler.instance == null) return;
            previousKeyboard = FatControler.instance.NoesisHasKeyboard && !IsTextInput(args.OldFocus);
            ownsKeyboard = true;
            MainViewModel.Instance.SetNoesisKeyboardState(true);
        }

        private void LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        { ReleaseKeyboard(args.NewFocus); }

        private void ReleaseKeyboard(UIElement next)
        {
            if (!ownsKeyboard) return;
            ownsKeyboard = false;
            if (FatControler.instance != null)
                FatControler.instance.SetNoesisKeyboardState(previousKeyboard || IsTextInput(next));
        }

        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        { if (!control.IsVisible) EndEditing(); }

        internal void EndEditing()
        {
            control.Text = CreditInput.Normalize(control.Text);
            Keyboard keyboard = control.Keyboard;
            if (control.IsKeyboardFocused) keyboard?.ClearFocus();
            ReleaseKeyboard(keyboard?.FocusedElement);
        }

        internal void ResetAmount()
        {
            EndEditing();
            control.Text = "0";
            model.Amount = validText = "0";
            control.SelectionStart = validStart = 0;
            control.SelectionLength = validLength = 0;
        }

        private void BeforeTextInput(object sender, TextCompositionEventArgs args)
        {
            if (!CreditInput.TryReplace(control.Text ?? "", control.SelectionStart,
                control.SelectionLength, args.Text, out _)) args.Handled = true;
        }

        private void TextChanged(object sender, RoutedEventArgs args)
        {
            if (restoring) return;
            string candidate = control.Text ?? "";
            if (CreditInput.IsEditable(candidate))
            {
                validText = candidate;
                model.Amount = candidate;
                RememberSelection();
                return;
            }
            restoring = true;
            try
            {
                control.Text = validText;
                control.SelectionStart = System.Math.Min(validStart, validText.Length);
                control.SelectionLength = System.Math.Min(validLength, validText.Length - control.SelectionStart);
            }
            finally { restoring = false; }
        }

        private void SelectionChanged(object sender, RoutedEventArgs args)
        { if (!restoring && control.Text == validText) RememberSelection(); }

        private void RememberSelection()
        { validStart = control.SelectionStart; validLength = control.SelectionLength; }

        private void LostFocus(object sender, RoutedEventArgs args)
        { control.Text = CreditInput.Normalize(control.Text); }

        internal void Detach()
        {
            EndEditing();
            control.GotKeyboardFocus -= GotKeyboardFocus;
            control.LostKeyboardFocus -= LostKeyboardFocus;
            control.IsVisibleChanged -= VisibilityChanged;
            control.PreviewTextInput -= BeforeTextInput;
            control.TextChanged -= TextChanged;
            control.SelectionChanged -= SelectionChanged;
            control.LostFocus -= LostFocus;
        }
    }
}
