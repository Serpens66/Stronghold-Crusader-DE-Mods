// SE-GATEHOUSE-UNIT-ID-COMPAT: Normalize the zero-based gatehouse-query unit
// index emitted by Script Extender 1.42.0. Re-audit after every Script
// Extender update and remove this policy when GatehouseQueryEventArgs.UnitId is
// fixed upstream to honor the normal one-based public ID contract.
namespace Shared
{
    internal static class GatehouseQueryUnitIdPolicy
    {
        internal static bool TryConvertSpanIndexToGameId(
            int unitSpanIndex,
            int unitSpanLength,
            out int unitId)
        {
            unitId = 0;
            if (unitSpanIndex < 0 || unitSpanIndex >= unitSpanLength)
                return false;

            unitId = checked(unitSpanIndex + 1);
            return true;
        }

        internal static bool ResolveCandidateDecision(
            bool? existingDecision,
            bool vanillaCandidateCanClose)
        {
            return existingDecision ?? vanillaCandidateCanClose;
        }
    }
}
