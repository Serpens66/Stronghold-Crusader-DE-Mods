namespace AutoSendTest
{
    // Input-route completion can be deferred or arrive more than once. Keep it
    // bound to one gesture and use the control's final value, not a cached event.
    internal sealed class AutoSendSliderGesture
    {
        internal long Generation { get; private set; }
        private bool active, changed;
        private int initialPosition;

        internal void Begin(int position)
        {
            Generation = unchecked(Generation + 1);
            initialPosition = position; active = true; changed = false;
        }
        internal void ValueChanged() { if (active) changed = true; }
        internal bool TryComplete(long generation, int finalPosition, out bool submit)
        {
            submit = false;
            if (!active || generation != Generation) return false;
            active = false;
            // Even a missing/consumed ValueChanged callback must not lose a move.
            // An untouched clamped thumb must retain a larger free-text value.
            submit = changed || finalPosition != initialPosition;
            return true;
        }
        internal void Cancel()
        { active = false; Generation = unchecked(Generation + 1); }
    }
}
