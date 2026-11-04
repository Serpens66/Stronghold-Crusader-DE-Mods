using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        // Only OST_Date is borrowed: preserve its binding/local state and never
        // broaden the lease to the construction HUD or its click masks.
        private sealed class ToolbarDateHitTestLease
        {
            private ToolbarProperty saved;
            internal void Update(DependencyObject target, DependencyProperty property, bool visible)
            {
                if (saved != null && (!visible || !ReferenceEquals(saved.Target, target)))
                { saved.Restore(); saved = null; }
                if (!visible || target == null || saved != null) return;
                saved = new ToolbarProperty(target, property);
                target.SetValue(property, false);
            }
        }

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
