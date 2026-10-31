using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    // Hold only report data visuals, never their frame, visibility/navigation roots or commands.
    internal sealed class ReportDisplayLease
    {
        private sealed class Value
        {
            internal DependencyObject Target;
            internal DependencyProperty Property;
            internal BindingBase Binding;
            internal object Local, Frozen, Latest;
        }
        private readonly List<Value> values = new List<Value>();
        internal void Add(DependencyObject target, DependencyProperty property)
        {
            var value = new Value { Target = target, Property = property,
                Binding = BindingOperations.GetBindingBase(target, property), Local = target.ReadLocalValue(property),
                Frozen = target.GetValue(property), Latest = target.GetValue(property) };
            values.Add(value); target.SetValue(property, value.Frozen);
        }
        internal void Hold()
        {
            foreach (var value in values)
            {
                if (BindingOperations.GetBindingBase(value.Target, value.Property) != null) continue;
                value.Latest = value.Target.GetValue(value.Property);
                value.Target.SetValue(value.Property, value.Frozen);
            }
        }
        internal Exception Restore()
        {
            Exception failure = null;
            foreach (var value in values)
            {
                try
                {
                    if (BindingOperations.GetBindingBase(value.Target, value.Property) != null) continue;
                    if (value.Binding != null) BindingOperations.SetBinding(value.Target, value.Property, value.Binding);
                    else if (!Equals(value.Latest, value.Frozen)) value.Target.SetValue(value.Property, value.Latest);
                    else if (value.Local == DependencyProperty.UnsetValue) value.Target.ClearValue(value.Property);
                    else value.Target.SetValue(value.Property, value.Local);
                }
                catch (Exception error) { if (failure == null) failure = error; }
            }
            values.Clear();
            return failure;
        }
    }
}
