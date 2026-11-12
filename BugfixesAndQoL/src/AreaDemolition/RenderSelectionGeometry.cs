using System;

namespace BugfixesAndQoL.AreaDemolition
{
    // Receipt identities still change for every quote. Only the inputs used to resolve
    // tilemaps and sprite membership may be reused when their geometry is identical.
    internal static class RenderSelectionGeometry
    {
        internal static bool Equals(DemolitionTarget[] first, DemolitionTarget[] second)
        {
            if (ReferenceEquals(first, second)) return true;
            int count = first?.Length ?? 0;
            if (count != (second?.Length ?? 0)) return false;
            for (int i = 0; i < count; i++)
            {
                DemolitionTarget a = first[i], b = second[i];
                if (ReferenceEquals(a, b)) continue;
                if (a == null || b == null || a.Kind != b.Kind || a.Slot != b.Slot || a.Type != b.Type || a.X != b.X || a.Y != b.Y) return false;
                if (a.Kind == TargetKind.Decoration) continue;
                int cells = a.OccupiedTiles?.Length ?? 0;
                if (cells != (b.OccupiedTiles?.Length ?? 0)) return false;
                if (ReferenceEquals(a.OccupiedTiles, b.OccupiedTiles)) continue;
                for (int cell = 0; cell < cells; cell++) if (a.OccupiedTiles[cell] != b.OccupiedTiles[cell]) return false;
            }
            return true;
        }
    }
}
