using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        // Preserve bindings themselves: restoring a sampled bool would permanently detach Vanilla updates.
        private sealed class ToolbarProperty
        {
            internal DependencyObject Target;
            internal DependencyProperty Property;
            internal BindingBase Binding;
            internal object Local;
            internal ToolbarProperty(DependencyObject target, DependencyProperty property)
            {
                Target = target; Property = property;
                Binding = BindingOperations.GetBindingBase(target, property);
                Local = target.ReadLocalValue(property);
            }
            internal void Restore()
            {
                if (Binding != null) BindingOperations.SetBinding(Target, Property, Binding);
                else if (Local == DependencyProperty.UnsetValue) Target.ClearValue(Property);
                else Target.SetValue(Property, Local);
            }
        }

    }
}
