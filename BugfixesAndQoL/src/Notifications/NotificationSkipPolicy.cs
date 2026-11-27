// Feature: Feed the original unsigned age comparison without mutating its native message.
namespace BugfixesAndQoL
{
    internal static class NotificationSkipPolicy
    {
        internal static uint TimestampPastAgeGate(uint startedAt) =>
            unchecked(startedAt + NotificationQueueNativeContract.AgeThresholdMs + 1);
    }
}
