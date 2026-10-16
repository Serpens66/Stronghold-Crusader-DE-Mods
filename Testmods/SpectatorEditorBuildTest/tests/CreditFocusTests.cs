using System;
using SpectatorEditorBuildTest;

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
namespace SpectatorEditorBuildTest
{
    internal class TestToolsViewModel { internal string Amount = "0"; }
    public static class CreditFocusTests
    {
        static int checks;
        static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
        public static int Run()
        {
            var keyboard = new Noesis.Keyboard(); var control = new Noesis.TextBox(keyboard);
            var model = new TestToolsViewModel(); var input = new CreditAmountInput(control, model);
            keyboard.Focus(control);
            Check(FatControler.instance.NoesisHasKeyboard, "focus enables keyboard and suppresses game keys");
            control.Select(0, 1); control.Type("4120");
            Check(control.Text == "4120" && model.Amount == "4120", "typing reaches amount model");
            control.Select(0, 4); control.Type("x"); Check(control.Text == "4120", "invalid typing rejected");
            control.Text = "12bad"; Check(control.Text == "4120", "invalid paste rolls back entire input");
            control.Text = "2147483648"; Check(control.Text == "4120", "overflow paste rolls back");
            control.Text = "2147483647"; Check(model.Amount == "2147483647", "max paste accepted");
            control.Text = ""; keyboard.ClearFocus();
            Check(control.Text == "0" && model.Amount == "0", "empty normalized on blur");
            Check(!FatControler.instance.NoesisHasKeyboard, "game keyboard restored");
            keyboard.Focus(control); keyboard.Focus(new Noesis.TextBox(keyboard));
            Check(FatControler.instance.NoesisHasKeyboard, "other text field retains keyboard");
            keyboard.ClearFocus(); FatControler.instance.NoesisHasKeyboard = false;
            keyboard.Focus(control); control.Hide();
            Check(!control.IsKeyboardFocused && !FatControler.instance.NoesisHasKeyboard, "hidden input releases focus");
            control.IsVisible = true; keyboard.Focus(control); input.Detach();
            Check(!control.IsKeyboardFocused && !FatControler.instance.NoesisHasKeyboard, "HUD detach releases focus");
            keyboard.Focus(control); Check(!FatControler.instance.NoesisHasKeyboard, "detached input cannot reacquire keyboard");
            return checks;
        }
    }
}
