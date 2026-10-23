using Noesis;
using System;

namespace Spectator
{
    internal static class GateAutomationBinding
    {
        internal static void Apply(FrameworkElement host, Button button, object model,
            Action<DependencyObject, DependencyProperty> capture)
        {
            // The Extender owns host.DataContext and synchronously restores it on any overwrite.
            capture(host, UIElement.VisibilityProperty);
            if (model == null || button == null) { host.Visibility = Visibility.Collapsed; return; }
            capture(button, FrameworkElement.DataContextProperty);
            button.DataContext = model;
            BindingOperations.SetBinding(host, UIElement.VisibilityProperty,
                new Binding("ButtonVisibility") { Source = model });
        }
    }
}
