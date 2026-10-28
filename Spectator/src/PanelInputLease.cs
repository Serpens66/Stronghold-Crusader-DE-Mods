using Noesis;
namespace Spectator
{
    // Never replace Visibility: the current Vanilla view must always retain authority to close.
    internal sealed class PanelInputLease
    {
        private readonly UIElement panel;
        private readonly BindingBase opacityBinding, hitBinding;
        private readonly object opacity, hit;
        internal PanelInputLease(UIElement element, bool obscure)
        {
            panel = element;
            opacityBinding = BindingOperations.GetBindingBase(panel, UIElement.OpacityProperty);
            hitBinding = BindingOperations.GetBindingBase(panel, UIElement.IsHitTestVisibleProperty);
            opacity = panel.ReadLocalValue(UIElement.OpacityProperty);
            hit = panel.ReadLocalValue(UIElement.IsHitTestVisibleProperty);
            if (obscure) panel.Opacity = 0;
            panel.IsHitTestVisible = false;
        }
        internal void Restore()
        {
            Restore(UIElement.OpacityProperty, opacityBinding, opacity, 0f);
            Restore(UIElement.IsHitTestVisibleProperty, hitBinding, hit, false);
        }
        private void Restore(DependencyProperty property, BindingBase binding, object local, object owned)
        {
            // A newer consumer may have rebound or changed this property while the refresh was pending.
            if (BindingOperations.GetBindingBase(panel, property) != null || !Equals(panel.ReadLocalValue(property), owned)) return;
            if (binding != null) BindingOperations.SetBinding(panel, property, binding);
            else if (local == DependencyProperty.UnsetValue) panel.ClearValue(property);
            else panel.SetValue(property, local);
        }
    }
}
