namespace BugfixesAndQoL
{
    internal static class AiDefensePatrolPolicy
    {
        internal static bool NeedsCastleDefender(int role1Count, int defensiveTriggerLevel) =>
            role1Count < defensiveTriggerLevel;

        internal static uint SelectComparisonValue(bool needsCastleDefender) =>
            needsCastleDefender
                ? unchecked((uint)int.MaxValue)
                : unchecked((uint)int.MinValue);
    }
}
