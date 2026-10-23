using System;
using System.Collections.Generic;

namespace PopularityTest
{
    internal interface IOverrideValue
    {
        int Value { get; }
        int Depth { get; }
        void Push(int value);
        void Pop();
    }

    // ManagedValue has an anonymous stack. Retain our exact depth/value and never
    // reset its base or clear foreign overrides. Removal runs only at map boundaries.
    internal sealed class OwnedOverride
    {
        private readonly IOverrideValue target;
        private readonly int depth, value;
        internal OwnedOverride(IOverrideValue target, int value)
        {
            this.target = target; this.value = value;
            target.Push(value); depth = target.Depth;
        }
        internal bool Release()
        {
            if (target.Depth < depth) return false;
            int before = target.Depth;
            var above = new List<int>();
            bool removed = false;
            try
            {
                while (target.Depth > depth) { above.Add(target.Value); target.Pop(); }
                if (target.Value != value) return false; // Another owner replaced our layer.
                target.Pop();
                removed = true;
            }
            finally
            {
                for (int i = above.Count - 1; i >= 0; i--) target.Push(above[i]);
            }
            return removed && target.Depth == before - 1;
        }
        internal bool IsCurrent => target.Depth == depth && target.Value == value;
    }
}
