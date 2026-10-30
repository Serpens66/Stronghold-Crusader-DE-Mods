using System;
using Noesis;
namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        internal static void TestToolbarProperty(Action<bool, string> check)
        {
            var property = DependencyProperty.RegisterAttached("Transform", typeof(object), typeof(FrameworkElement), new PropertyMetadata(null, null));
            var host = new FrameworkElement();
            var original = new object(); host.SetValue(property, original);
            var saved = new ToolbarProperty(host, property);
            host.SetValue(property, new object()); saved.Restore(); saved.Restore();
            check(ReferenceEquals(host.ReadLocalValue(property), original), "Borrowed HUD property release restores the original transform object exactly and idempotently.");
            host.ClearValue(property); saved = new ToolbarProperty(host, property);
            host.SetValue(property, new object()); saved.Restore();
            check(host.ReadLocalValue(property) == DependencyProperty.UnsetValue, "An inherited/default HUD property must return to an unset local value.");
            var binding = new BindingBase { Value = original }; BindingOperations.SetBinding(host, property, binding);
            saved = new ToolbarProperty(host, property); host.SetValue(property, new object()); saved.Restore();
            var next = new object(); binding.Value = next;
            check(ReferenceEquals(BindingOperations.GetBindingBase(host, property), binding) && ReferenceEquals(host.GetValue(property), next),
                "Releasing the HUD property restores a live binding, never a sampled transform value.");

            var hit = DependencyProperty.RegisterAttached("DateHitTest", typeof(bool), typeof(FrameworkElement), new PropertyMetadata(true, null));
            var date = new FrameworkElement(); var replacement = new FrameworkElement();
            var lease = new ToolbarHitTestLease();
            lease.Update(date, hit, true); lease.Update(date, hit, true);
            check(Equals(date.GetValue(hit), false), "Placement makes only the leased date click-through.");
            lease.Update(date, hit, false);
            check(date.ReadLocalValue(hit) == DependencyProperty.UnsetValue && Equals(date.GetValue(hit), true),
                "Ending placement restores the native default, including its unset local value.");
            date.SetValue(hit, false); lease.Update(date, hit, true); lease.Update(null, hit, false);
            check(Equals(date.ReadLocalValue(hit), false), "An originally click-through date remains click-through after release.");
            var hitBinding = new BindingBase { Value = true }; BindingOperations.SetBinding(date, hit, hitBinding);
            lease.Update(date, hit, true); lease.Update(replacement, hit, true);
            check(ReferenceEquals(BindingOperations.GetBindingBase(date, hit), hitBinding) && Equals(replacement.GetValue(hit), false),
                "HUD recreation releases the old instance and leases the new one.");
            lease.Update(null, hit, false); lease.Update(null, hit, false);
            check(replacement.ReadLocalValue(hit) == DependencyProperty.UnsetValue,
                "Foreign HUD/mission release is idempotent and restores the replacement instance.");
        }
    }
}
