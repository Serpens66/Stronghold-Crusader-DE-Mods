// The manifest-selected Script Extender exposes GatehouseQueryEventArgs.UnitId as the normal
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

        // Vanilla closes only for a unit whose Chebyshev distance to the gate tile (fine
        // coordinates, tile * 8) is below 200 (or 140 with an extra flag); farther queries can
        // never close, whatever their answer (native baseline GATEHOUSE_QUERY_PASS.md).
        internal const int VanillaMaximumCloseDistance = 200;

        internal static bool IsWithinVanillaCloseRange(
            short gateTileX,
            short gateTileY,
            short unitWorldX,
            short unitWorldY)
        {
            int dx = gateTileX * 8 - unitWorldX;
            int dy = gateTileY * 8 - unitWorldY;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return (dx < dy ? dy : dx) < VanillaMaximumCloseDistance;
        }
    }
}
