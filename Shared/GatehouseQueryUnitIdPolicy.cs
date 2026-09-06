// Script Extender 2.2.0 exposes GatehouseQueryEventArgs.UnitId as the normal
// one-based game ID. Keep validation here so both consuming mods share the same
// fail-closed boundary without applying the obsolete 1.42.0 +1 correction.
namespace Shared
{
    internal static class GatehouseQueryUnitIdPolicy
    {
        internal static bool TryValidateGameId(
            int candidateUnitId,
            int unitSpanLength,
            out int unitId)
        {
            unitId = 0;
            if (candidateUnitId <= 0 || candidateUnitId > unitSpanLength)
                return false;

            unitId = candidateUnitId;
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
