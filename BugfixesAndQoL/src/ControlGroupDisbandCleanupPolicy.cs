namespace BugfixesAndQoL
{
    internal static class ControlGroupDisbandCleanupPolicy
    {
        internal static bool ShouldClean(bool clientFeaturesEnabled, bool cleanupEnabled) =>
            clientFeaturesEnabled && cleanupEnabled;

        internal static int RemoveUnit(int[] records, int unitId)
        {
            if (records == null || records.Length % ControlGroupNativeDefinition.ControlGroupRecordIntCount != 0)
                return 0;

            int removed = 0;
            for (int offset = 0; offset < records.Length;
                 offset += ControlGroupNativeDefinition.ControlGroupRecordIntCount)
            {
                if (records[offset] != unitId)
                    continue;

                records[offset] = -1;
                removed++;
            }
            return removed;
        }
    }
}
