using System;

namespace BugfixesAndQoL
{
    // Valid edits are visible immediately but only the final choice is applied on
    // menu close or idle. Invalid drafts suspend rather than commit a numeric prefix.
    internal sealed class LocalAutosavePendingChoice
    {
        private LocalAutosaveSchedule target;
        private int minutes;
        private long lastEdit;
        private bool valid;

        internal void Set(LocalAutosaveSchedule schedule, int value, long now)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (value < 0 || value > LocalAutosaveInputPolicy.MaximumMinutes)
                throw new ArgumentOutOfRangeException(nameof(value));
            target = schedule;
            minutes = value;
            lastEdit = now;
            valid = true;
        }

        internal void Invalidate(long now) { valid = false; lastEdit = now; }
        internal void Clear() { target = null; valid = false; }

        internal int EditorMinutes(LocalAutosaveSchedule current) =>
            valid && ReferenceEquals(target, current) ? minutes : current?.Minutes ?? 0;

        internal long RemainingTicks(long now, long frequency)
        {
            if (!valid || target == null) return -1;
            long elapsed = now >= lastEdit ? now - lastEdit : 0;
            long delay = checked(15L * frequency);
            return elapsed >= delay ? 0 : delay - elapsed;
        }

        internal bool TryTake(LocalAutosaveSchedule current, out int value)
        {
            value = minutes;
            bool accepted = valid && current != null && ReferenceEquals(target, current);
            Clear();
            return accepted;
        }
    }
}
