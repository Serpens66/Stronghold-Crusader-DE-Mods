namespace BugfixesAndQoL
{
    // The same synchronized host setting controls every simulation participant.
    internal sealed class RaidActivationState
    {
        internal bool Available, Requested, SessionAllowed;
        internal bool Active => Available && Requested && SessionAllowed;
    }
}
