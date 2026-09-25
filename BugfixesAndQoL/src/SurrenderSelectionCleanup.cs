using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal enum SurrenderSelectionCleanupStatus
    {
        IdentityChanged,
        AlreadyClear,
        Cleared,
        ClearedWithEmptyCount,
        UnexpectedSelector
    }

    internal struct SurrenderSelectionCleanupResult
    {
        internal SurrenderSelectionCleanupStatus Status;
        internal ushort SelectedBefore;
        internal uint CountBefore;
        internal uint CountAfter;
    }

    // Mirrors the selection-field and per-player count updates in Vanilla's
    // single-unit deselection. A dead Lord can remain in its unit slot until
    // after the next multiplayer checksum, so deletion cleanup is too late.
    internal static unsafe class SurrenderSelectionCleanup
    {
        internal static SurrenderSelectionCleanupResult Clear(
            GameUnit* unit, uint* selectedCount, int expectedGlobalId, int expectedPlayerId)
        {
            var result = new SurrenderSelectionCleanupResult
            {
                Status = SurrenderSelectionCleanupStatus.IdentityChanged
            };
            if (unit == null || selectedCount == null || expectedGlobalId <= 0 ||
                expectedPlayerId < 1 || expectedPlayerId > 8 ||
                unit->r_GlobalId != (uint)expectedGlobalId)
                return result;

            result.SelectedBefore = unit->r_UnitSelected;
            result.CountBefore = *selectedCount;
            result.CountAfter = result.CountBefore;
            if (result.SelectedBefore == 0)
            {
                result.Status = SurrenderSelectionCleanupStatus.AlreadyClear;
                return result;
            }
            if (result.SelectedBefore != expectedPlayerId)
            {
                result.Status = SurrenderSelectionCleanupStatus.UnexpectedSelector;
                return result;
            }

            unit->r_UnitSelected = 0;
            if (result.CountBefore == 0)
            {
                result.Status = SurrenderSelectionCleanupStatus.ClearedWithEmptyCount;
                return result;
            }

            result.CountAfter = result.CountBefore - 1;
            *selectedCount = result.CountAfter;
            result.Status = SurrenderSelectionCleanupStatus.Cleared;
            return result;
        }
    }
}
