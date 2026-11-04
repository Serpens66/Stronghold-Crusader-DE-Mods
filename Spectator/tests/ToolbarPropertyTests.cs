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
            var date = new FrameworkElement(); var otherMask = new FrameworkElement(); var replacement = new FrameworkElement();
            var lease = new ToolbarDateHitTestLease();
            lease.Update(date, hit, true); lease.Update(date, hit, true);
            check(Equals(date.GetValue(hit), false) && Equals(otherMask.GetValue(hit), true),
                "Only the leased date becomes click-through while the Spectator surface is visible.");
            lease.Update(date, hit, false);
            check(date.ReadLocalValue(hit) == DependencyProperty.UnsetValue && Equals(date.GetValue(hit), true),
                "Suspension restores the Vanilla default without adding a local override.");
            date.SetValue(hit, false); lease.Update(date, hit, true); lease.Update(null, hit, false);
            check(Equals(date.ReadLocalValue(hit), false), "An original explicit date value survives suspension.");
            var hitBinding = new BindingBase { Value = true }; BindingOperations.SetBinding(date, hit, hitBinding);
            lease.Update(date, hit, true); lease.Update(replacement, hit, true);
            hitBinding.Value = false;
            check(ReferenceEquals(BindingOperations.GetBindingBase(date, hit), hitBinding) && Equals(date.GetValue(hit), false),
                "HUD rebind restores the live original binding on the old date, rather than a sampled bool.");
            check(Equals(replacement.GetValue(hit), false), "The replacement HUD receives its own date lease.");
            lease.Update(null, hit, false); lease.Update(null, hit, false);
            check(replacement.ReadLocalValue(hit) == DependencyProperty.UnsetValue && Equals(otherMask.GetValue(hit), true),
                "Detach and repeated suspension restore the replacement and leave other click masks untouched.");
        }
    }
}
