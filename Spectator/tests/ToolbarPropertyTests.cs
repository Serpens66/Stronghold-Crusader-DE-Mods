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
            check(ReferenceEquals(host.ReadLocalValue(property), original), "Raster release restores the original transform object exactly and idempotently.");
            host.ClearValue(property); saved = new ToolbarProperty(host, property);
            host.SetValue(property, new object()); saved.Restore();
            check(host.ReadLocalValue(property) == DependencyProperty.UnsetValue, "An inherited/default raster transform must return to an unset local value.");
            var binding = new BindingBase { Value = original }; BindingOperations.SetBinding(host, property, binding);
            saved = new ToolbarProperty(host, property); host.SetValue(property, new object()); saved.Restore();
            var next = new object(); binding.Value = next;
            check(ReferenceEquals(BindingOperations.GetBindingBase(host, property), binding) && ReferenceEquals(host.GetValue(property), next),
                "Releasing the raster restores a live binding, never a sampled transform value.");
        }
    }
}
