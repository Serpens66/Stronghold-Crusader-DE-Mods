namespace Shared
{
    internal static class SelectedChimpsSnapshotPolicy
    {
        internal const int MaximumCount = 10000;

        internal static bool IsPlausibleCount(int count)
        {
            return count >= 0 && count <= MaximumCount;
        }
    }
}
