using System;
using Spectator;

// Event harness for the production input adapter; no game or Noesis native runtime is loaded.
namespace Noesis
{
    public class RoutedEventArgs : EventArgs { }
    public class DependencyPropertyChangedEventArgs : EventArgs { }
    public class TextCompositionEventArgs : EventArgs { public string Text; public bool Handled; }
    public class KeyboardFocusChangedEventArgs : EventArgs { public UIElement OldFocus, NewFocus; }
    public class UIElement { }
    public class TextBoxBase : UIElement { }
    public class PasswordBox : UIElement { }
    public class Keyboard
    {
        public UIElement FocusedElement;
        public void ClearFocus() { Focus(null); }
        public void Focus(UIElement next)
        {
            var old = FocusedElement;
            FocusedElement = next;
            if (old is TextBox) ((TextBox)old).Blur(next);
            if (next is TextBox) ((TextBox)next).Gain(old);
        }
    }
    public class TextBox : TextBoxBase
    {
        public readonly Keyboard Keyboard;
        public TextBox(Keyboard keyboard) { Keyboard = keyboard; }
        private string text;
        public string Text { get { return text; } set { if (text == value) return; text = value; TextChanged?.Invoke(this, new RoutedEventArgs()); } }
        public int SelectionStart, SelectionLength;
        public bool IsKeyboardFocused { get { return Keyboard.FocusedElement == this; } }
        public bool IsVisible = true;
        public event EventHandler<TextCompositionEventArgs> PreviewTextInput;
        public event EventHandler<RoutedEventArgs> TextChanged, SelectionChanged, LostFocus;
        public event EventHandler<KeyboardFocusChangedEventArgs> GotKeyboardFocus, LostKeyboardFocus;
        public event EventHandler<DependencyPropertyChangedEventArgs> IsVisibleChanged;
        internal void Gain(UIElement old) { GotKeyboardFocus?.Invoke(this, new KeyboardFocusChangedEventArgs { OldFocus = old, NewFocus = this }); }
        internal void Blur(UIElement next) { LostKeyboardFocus?.Invoke(this, new KeyboardFocusChangedEventArgs { OldFocus = this, NewFocus = next }); LostFocus?.Invoke(this, new RoutedEventArgs()); }
        public void Hide() { IsVisible = false; IsVisibleChanged?.Invoke(this, new DependencyPropertyChangedEventArgs()); }
        public void Type(string value)
        {
            if (!FatControler.instance.NoesisHasKeyboard) return;
            var args = new TextCompositionEventArgs { Text = value }; PreviewTextInput?.Invoke(this, args);
            if (!args.Handled) Text = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, value);
        }
        public void Select(int start, int length) { SelectionStart = start; SelectionLength = length; SelectionChanged?.Invoke(this, new RoutedEventArgs()); }
    }
}
public class FatControler
{
    public static FatControler instance = new FatControler();
    public bool NoesisHasKeyboard;
    public void SetNoesisKeyboardState(bool state) { NoesisHasKeyboard = state; }
}
namespace CrusaderDE
{
    public class MainViewModel
    {
        public static MainViewModel Instance = new MainViewModel();
        public void SetNoesisKeyboardState(bool state) { FatControler.instance.SetNoesisKeyboardState(state); }
    }
}
namespace Spectator
{
    internal class TestToolsViewModel
    {
        private string amount = "0";
        internal event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        internal string Amount { get => amount; set { if (value == amount) return; amount = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Amount))); } }
    }
    public static class CreditFocusTests
    {
        static int checks;
        static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
        public static int Run()
        {
            var keyboard = new Noesis.Keyboard(); var control = new Noesis.TextBox(keyboard);
            var model = new TestToolsViewModel(); var input = new CreditAmountInput(control, model);
            var second = new Noesis.TextBox(keyboard);
            var secondInput = new CreditAmountInput(second, model);
            keyboard.Focus(control);
            Check(FatControler.instance.NoesisHasKeyboard, "focus enables keyboard and suppresses game keys");
            control.Select(0, 1); control.Type("4120");
            Check(control.Text == "4120" && model.Amount == "4120", "typing reaches amount model");
            input.OutsidePointerDown(true);
            Check(control.IsKeyboardFocused, "inside menu keeps typing focus");
            input.OutsidePointerDown(false);
            Check(!control.IsKeyboardFocused && !FatControler.instance.NoesisHasKeyboard && model.Amount == "4120", "outside map click restores hotkeys and retains amount");
            keyboard.Focus(control);
            control.Select(0, 4); control.Type("x"); Check(control.Text == "4120", "invalid typing rejected");
            control.Text = "12bad"; Check(control.Text == "4120", "invalid paste rolls back entire input");
            control.Text = "2147483648"; Check(control.Text == "4120", "overflow paste rolls back");
            control.Text = "2147483647"; Check(model.Amount == "2147483647", "max paste accepted");
            Check(second.Text == "2147483647", "second surface follows the first");
            second.Text = "bad"; Check(second.Text == "2147483647", "second rollback buffer follows shared changes");
            keyboard.Focus(second); second.Select(0, second.Text.Length); second.Type("153");
            Check(control.Text == "153" && model.Amount == "153", "second surface updates first");
            control.Text = "bad"; Check(control.Text == "153", "first rollback buffer follows second");
            input.EndEditing(); Check(FatControler.instance.NoesisHasKeyboard && second.IsKeyboardFocused, "hiding nonfocused surface preserves other input");
            second.Text = ""; input.EndEditing(); Check(model.Amount == "", "other surface cannot normalize an active empty edit");
            secondInput.ResetAmount(); Check(control.Text == "0" && second.Text == "0", "shared reset updates both fields");
            keyboard.Focus(control); control.Select(0, control.Text.Length); control.Type("-");
            Check(model.Amount == "-" && second.Text == "-", "minus editing shared without premature normalization");
            control.Select(1, 0); control.Type("2147483648"); Check(model.Amount == "-2147483648", "minimum signed amount typed");
            control.Text = "-2147483649"; Check(control.Text == "-2147483648", "negative overflow paste fully rolled back");
            second.Text = "--1"; Check(second.Text == "-2147483648", "peer rollback buffer retains signed value");
            control.Text = "-"; keyboard.ClearFocus(); Check(model.Amount == "0" && second.Text == "0", "lone minus normalized on blur");
            foreach (string value in new[] { "", "-", "-2147483648", "4120", "2147483647" })
            {
                control.Text = value;
                string capturedCredit = model.Amount;
                input.ResetAmount();
                Check(control.Text == "0" && model.Amount == "0", "reset synchronizes text/model " + value);
                Check(capturedCredit == value, "reset leaves captured credit unchanged " + value);
                control.Text = "bad";
                Check(control.Text == "0", "reset updates valid paste rollback buffer " + value);
                keyboard.Focus(control); control.Select(0, 1); control.Type("125");
                Check(model.Amount == "125", "editing works after reset " + value);
            }
            control.Text = ""; keyboard.ClearFocus();
            Check(control.Text == "0" && model.Amount == "0", "empty normalized on blur");
            Check(!FatControler.instance.NoesisHasKeyboard, "game keyboard restored");
            keyboard.Focus(control); keyboard.Focus(new Noesis.TextBox(keyboard));
            Check(FatControler.instance.NoesisHasKeyboard, "other text field retains keyboard");
            input.OutsidePointerDown(false);
            Check(FatControler.instance.NoesisHasKeyboard, "outside event cannot revoke another input's keyboard");
            keyboard.ClearFocus(); FatControler.instance.NoesisHasKeyboard = false;
            keyboard.Focus(control); control.Hide();
            Check(!control.IsKeyboardFocused && !FatControler.instance.NoesisHasKeyboard, "hidden input releases focus");
            control.IsVisible = true; keyboard.Focus(control); input.Detach();
            Check(!control.IsKeyboardFocused && !FatControler.instance.NoesisHasKeyboard, "HUD detach releases focus");
            keyboard.Focus(control); Check(!FatControler.instance.NoesisHasKeyboard, "detached input cannot reacquire keyboard");
            secondInput.Detach();
            return checks;
        }
    }
}
