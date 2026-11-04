using BepInEx.Bootstrap;
using System;
using System.Reflection;

namespace Spectator
{
    // The optional provider owns persistence and the native resolver. Never register a second resolver.
    internal sealed class OptionalGateAutomation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly object runtime, model;
        private readonly MethodInfo isManual, apply, show, hide;
        private readonly FieldInfo automationReady, mapActive, loading;
        private readonly object settings;
        private readonly PropertyInfo enabledProperty;
        private readonly Func<bool, bool> isEnabled;
        private bool faulted;
        internal object Model => model;

        internal OptionalGateAutomation(Action toggle)
        {
            if (!Chainloader.PluginInfos.TryGetValue("ExtraFeatures_Serp", out var plugin)) return;
            Type root = plugin.Instance.GetType();
            // Bind the provider contract, independent of its release number.
            object owner = root.GetField("runtime", Private)?.GetValue(plugin.Instance);
            runtime = owner?.GetType().GetField("gatehouseAutomationRuntime", Private)?.GetValue(owner);
            if (runtime == null) throw new MissingMemberException("ExtraFeatures gate runtime is unavailable.");
            Type type = runtime.GetType();
            automationReady = RequireField(type, "automationReady", typeof(bool));
            mapActive = RequireField(type, "mapActive", typeof(bool));
            loading = RequireField(type, "loadedMapStatePending", typeof(bool));
            settings = type.GetField("settings", Private)?.GetValue(runtime) ??
                throw new MissingMemberException("ExtraFeatures settings contract changed.");
            enabledProperty = settings.GetType().GetProperty("EnableMod");
            if (enabledProperty?.PropertyType != typeof(bool) || enabledProperty.GetGetMethod() == null)
                throw new MissingMemberException("ExtraFeatures EnableMod contract changed.");
            Type gate = type.Assembly.GetType("Shared.GameplayModActivationGate", true);
            MethodInfo enabledMethod = gate.GetMethod("IsEnabled", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(bool) }, null);
            if (enabledMethod?.ReturnType != typeof(bool))
                throw new MissingMemberException("ExtraFeatures internal activation gate contract changed.");
            isEnabled = (Func<bool, bool>)Delegate.CreateDelegate(typeof(Func<bool, bool>), enabledMethod);
            isManual = type.GetMethod("IsManualOnlyBuilding", Private, null, new[] { typeof(int) }, null);
            apply = type.GetMethod("ApplyManualState", Private, null,
                new[] { typeof(int), typeof(int), typeof(bool), typeof(string) }, null);
            object original = type.GetProperty("ButtonViewModel")?.GetValue(runtime, null);
            if (isManual?.ReturnType != typeof(bool) || apply?.ReturnType != typeof(void) || original == null)
                throw new MissingMemberException("ExtraFeatures gate state contract changed.");
            Type vm = original.GetType();
            model = Activator.CreateInstance(vm, new object[] { toggle });
            show = vm.GetMethod("Show", new[] { typeof(bool), typeof(bool) });
            hide = vm.GetMethod("Hide", Type.EmptyTypes);
            PropertyInfo icon = vm.GetProperty("IconImageSource");
            MethodInfo setIcon = icon == null ? null : vm.GetMethod("SetIcon", new[] { icon.PropertyType });
            if (show?.ReturnType != typeof(void) || hide?.ReturnType != typeof(void) ||
                icon?.GetGetMethod() == null || setIcon?.ReturnType != typeof(void))
                throw new MissingMemberException("ExtraFeatures gate button contract changed.");
            setIcon.Invoke(model, new[] { icon.GetValue(original, null) });
        }

        internal bool Ready
        {
            get
            {
                if (faulted || runtime == null || model == null) return false;
                try
                {
                    return (bool)automationReady.GetValue(runtime) && (bool)mapActive.GetValue(runtime) &&
                        !(bool)loading.GetValue(runtime) && isEnabled((bool)enabledProperty.GetValue(settings, null));
                }
                catch { MarkFaulted(); throw; }
            }
        }
        internal bool Automatic(int building) => !(bool)isManual.Invoke(runtime, new object[] { building });
        internal void Refresh(int building, bool bridge)
        {
            if (faulted) return;
            try
            {
                if (Ready) show.Invoke(model, new object[] { Automatic(building), bridge });
                else hide?.Invoke(model, null);
            }
            catch { MarkFaulted(); throw; }
        }
        // A bounded log alone does not prevent per-frame exceptions and stack-trace formatting.
        internal void MarkFaulted()
        {
            if (faulted) return;
            faulted = true;
            try { hide?.Invoke(model, null); } catch { /* Do not retry a broken optional provider. */ }
        }
        private static FieldInfo RequireField(Type type, string name, Type expected)
        {
            FieldInfo field = type.GetField(name, Private);
            if (field?.FieldType != expected) throw new MissingMemberException("ExtraFeatures field contract changed: " + name);
            return field;
        }
        internal void Apply(int player, int building, uint global, bool automatic)
        {
            if (!Ready) throw new InvalidOperationException("ExtraFeatures gate automation is not ready.");
            apply.Invoke(runtime, new object[] { player, unchecked((int)global), automatic, "Spectator" });
            if (Automatic(building) != automatic) throw new InvalidOperationException("ExtraFeatures did not accept the gate automation state.");
        }
    }
}
