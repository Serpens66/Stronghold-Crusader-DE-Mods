using System;

namespace ExtendedData
{
    // Shared by Coop and Map setup drafts, not mission lifetime or settings
    // storage. Callbacks perform the existing coordinator operations; commit after success.
    internal sealed class CoopSettingsDraftState
    {
        private string identity;
        private bool hasSource, hasDraft, frozen;
        internal bool HasDraft => hasDraft;
        internal bool IsFrozen => frozen;

        internal void Prepare(string source, bool customize, bool editable, bool starting,
            Action applySource, Action freeze, Action reopen)
        {
            if (!hasSource || !string.Equals(identity, source, StringComparison.Ordinal))
            {
                applySource();
                identity = source;
                hasSource = true;
                hasDraft = customize && editable;
                frozen = false;
            }
            else if (customize && editable && !hasDraft)
            {
                applySource();
                hasDraft = true;
            }
            if (hasDraft && starting && !frozen)
            {
                freeze();
                frozen = true;
            }
            else if (hasDraft && editable && frozen)
            {
                reopen();
                frozen = false;
            }
        }

        internal void Recover(Action reopen)
        {
            if (!hasDraft || !frozen) return;
            reopen();
            frozen = false;
        }

        internal void Reset() { identity = null; hasSource = hasDraft = frozen = false; }
    }
}
