using System;
using System.Collections.Generic;
using Spectator;

namespace Noesis
{
    public class DependencyObject { }
    public class DependencyProperty { }
    public enum Visibility { Visible, Hidden, Collapsed }
    public class UIElement : DependencyObject
    {
        public static readonly DependencyProperty VisibilityProperty = new DependencyProperty();
        public Visibility Visibility;
    }
    public class FrameworkElement : UIElement
    {
        public static readonly DependencyProperty DataContextProperty = new DependencyProperty();
        private object context;
        public Action Changed;
        public object DataContext { get => context; set { context = value; Changed?.Invoke(); } }
    }
    public class Button : FrameworkElement { }
    public class Binding
    {
        public string Path; public object Source;
        public Binding(string path) { Path = path; }
    }
    public static class BindingOperations
    {
        public static Binding Last;
        public static void SetBinding(DependencyObject target, DependencyProperty property, Binding value) { Last = value; }
    }
}
namespace Fixture
{
    internal static class GateBindingFixture
    {
        internal static void Run()
        {
            var original = new object(); var selected = new object(); int overwrites = 0;
            var host = new Noesis.FrameworkElement { DataContext = original };
            host.Changed = () => { if (!ReferenceEquals(host.DataContext, original)) { overwrites++; host.DataContext = original; } };
            var button = new Noesis.Button();
            var captured = new List<Noesis.DependencyProperty>();
            GateAutomationBinding.Apply(host, button, selected, (o,p) => captured.Add(p));
            if (overwrites != 0 || !ReferenceEquals(host.DataContext, original) || !ReferenceEquals(button.DataContext, selected))
                throw new Exception("Extender-protected host must never be overwritten; only child uses test model.");
            if (!ReferenceEquals(Noesis.BindingOperations.Last.Source, selected) || Noesis.BindingOperations.Last.Path != "ButtonVisibility" ||
                captured.Count != 2 || captured[0] != Noesis.UIElement.VisibilityProperty || captured[1] != Noesis.FrameworkElement.DataContextProperty)
                throw new Exception("Visibility must use explicit source; restore inventory must be limited to owned properties.");
            GateAutomationBinding.Apply(host, button, null, (o,p) => { });
            if (host.Visibility != Noesis.Visibility.Collapsed || overwrites != 0) throw new Exception("Absent provider remains hidden.");
            Console.WriteLine("PASS: production gate binding respects Extender DataContext guard and explicit visibility source.");
        }
    }
}
