using AIBuildDiagnoseTest;
using System;
using System.Text;
using System.Collections.Generic;
using SHCDESE.Interop.Enums;
namespace CanarariWoodTest
{
    // Copied snapshots and read-only replay; never writes game data or installs hooks.
    internal static class NearbyWoodEvidence
    {
        private const uint NativePlacementReservationFlag = 0x4;
        internal static bool IsActualWall(uint flags) =>
            (flags & (uint)TilePropertyFlag.IsWall) != 0 &&
            (flags & (uint)TilePropertyFlag.GoodsyardRelated) == 0;

        internal static readonly int[] NearbyDx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] NearbyDy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        internal sealed class NearbyFootprint
        {
            internal bool Complete;
            internal int AnchorNative = -1, AnchorApi = -1;
            internal int Bit4, Zero, Swamp, Wall, WallOwners, Trees, Buildings, Organisms, Occupied,
                Unavailable, ViewMismatches;
            internal string Tiles;
            internal string FirstObservedConstraint;
            internal bool ExcludeForCopyProbe => Complete && (AnchorNative == 0 || Bit4 != 0);
            internal bool NoMeasuredBlockers => Complete && AnchorNative > 0 && Zero == 0 && Bit4 == 0 &&
                Swamp == 0 && Wall == 0 && WallOwners == 0 && Trees == 0 && Buildings == 0 &&
                Organisms == 0 && Occupied == 0;
            internal string Describe() =>
                $"anchor={AnchorNative}/{AnchorApi}:bit4={Bit4}:pcl0={Zero}:" +
                $"swamp={Swamp}:wall={Wall}:wallOwner={WallOwners}:tree={Trees}:building={Buildings}:" +
                $"organism={Organisms}:occupied={Occupied}:unavailable={Unavailable}:" +
                $"viewMismatches={ViewMismatches}:" +
                $"firstObservedConstraint={FirstObservedConstraint}:nativePlacementUnobserved=true:" +
                $"tiles=[{Tiles}]";
        }

        internal static NearbyFootprint CaptureNearbyFootprint(int coarseX, int coarseY)
        {
            var result = new NearbyFootprint();
            IReadOnlyList<AiPathTileSample> tiles = AiBuildDiagnostic.CaptureTiles(
                coarseX * 5, coarseY * 5, 3, 3);
            if (tiles.Count != 9)
            {
                result.Unavailable = 9;
                result.Tiles = "tile-count=" + tiles.Count;
                result.FirstObservedConstraint = "tile-count-unavailable";
                return result;
            }
            var raw = new StringBuilder();
            foreach (AiPathTileSample tile in tiles)
            {
                if (raw.Length != 0) raw.Append('|');
                raw.Append(tile.X).Append('/').Append(tile.Y).Append(':').Append(tile.Status);
                if (tile.Status != "ok") { result.Unavailable++; continue; }
                raw.Append(':').Append(tile.NativeComponent).Append('/')
                    .Append(tile.ApiComponent).Append(':').Append(tile.PropertyFlags.ToString("X8"))
                    .Append(':').Append(tile.BuildingId).Append(':').Append(tile.Organism)
                    .Append(':').Append(tile.Occupancy).Append(':').Append(tile.Height)
                    .Append(':').Append(tile.TileType).Append(':').Append(tile.WallOwner)
                    .Append(':').Append(tile.TileId);
                if ((tile.PropertyFlags & NativePlacementReservationFlag) != 0) result.Bit4++;
                if (tile.NativeComponent == 0) result.Zero++;
                if (tile.NativeComponent != tile.ApiComponent) result.ViewMismatches++;
                if ((tile.PropertyFlags & 0x20000000u) != 0) result.Swamp++;
                if (IsActualWall(tile.PropertyFlags)) result.Wall++;
                if (tile.WallOwner > 0) result.WallOwners++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) result.Trees++;
                if (tile.BuildingId > 0) result.Buildings++;
                if (tile.Organism > 0) result.Organisms++;
                if (tile.Occupancy > 0) result.Occupied++;
            }
            AiPathTileSample anchor = tiles[0];
            result.AnchorNative = anchor.NativeComponent;
            result.AnchorApi = anchor.ApiComponent;
            result.Tiles = raw.ToString();
            result.Complete = result.Unavailable == 0 && result.ViewMismatches == 0;
            result.FirstObservedConstraint = result.Unavailable != 0 ? "tile-unavailable" :
                result.ViewMismatches != 0 ? "native-api-pcl-view-mismatch" :
                anchor.NativeComponent == 0 ? "anchor-component-zero" :
                result.Bit4 != 0 ? "parcel-bit4-native-placement-blocker" :
                result.Wall != 0 ? "wall-flag" : result.Swamp != 0 ? "swamp-flag" :
                result.Buildings != 0 ? "building-layer" :
                result.Trees != 0 || result.Organisms != 0 || result.Occupied != 0 ?
                "other-measured-occupancy" : "none-of-measured-flags";
            return result;
        }

        internal sealed class NearbyWoodShadow
        {
            internal int X = -1, Y = -1, Visited;
            internal int TraceOmitted, FootprintOmitted;
            internal bool Valid = true;
            internal string Error;
            internal NearbyFootprint ExpectedFootprint;
            internal readonly List<NearbyMaskedCandidate> MaskedCandidates =
                new List<NearbyMaskedCandidate>();
            internal readonly List<string> CandidateTrace = new List<string>();
            internal readonly List<string> Trace = new List<string>();
        }

        internal sealed class NearbyMaskedCandidate
        {
            internal int Index;
            internal string Reason, FootprintTiles;
        }

        internal static NearbyWoodShadow ReplayNearbyWood(AiNearbyPathEvidence evidence, bool excludeInvalidFootprint,
            bool trace)
        {
            var result = new NearbyWoodShadow();
            if (evidence == null || evidence.Status != "ok" ||
                (uint)evidence.InputX >= 160 || (uint)evidence.InputY >= 160)
            { result.Valid = false; result.Error = "snapshot-or-input-unavailable"; return result; }
            int[] seed = ParseCoarseBytes(evidence.GetCapturedCoarseCell(evidence.InputX, evidence.InputY));
            if (seed == null || seed[12] != 0)
            { result.Valid = false; result.Error = "seed-outside-usable-map"; return result; }
            var seenCells = new bool[25600];
            var queue = new int[25600];
            int head = 0, tail = 1;
            int seedIndex = evidence.InputX * 160 + evidence.InputY;
            queue[0] = seedIndex;
            seenCells[seedIndex] = true;
            while (head < tail)
            {
                int current = queue[head++];
                int cx = current / 160, cy = current % 160;
                int[] parent = ParseCoarseBytes(evidence.GetCapturedCoarseCell(cx, cy));
                if (parent == null)
                { result.Valid = false; result.Error = "missing-parent-cell"; break; }
                for (int direction = 0; direction < 8; direction++)
                {
                    int x = cx + NearbyDx[direction], y = cy + NearbyDy[direction];
                    // Vanilla's outside-map branch is safe only for a blocked parent; never
                    // simulate a native out-of-bounds read or permit an overlay in that case.
                    if ((uint)x >= 160 || (uint)y >= 160)
                    {
                        if (parent[12] == 0)
                        { result.Valid = false; result.Error = "native-edge-branch"; }
                        if (!result.Valid) return result;
                        continue;
                    }
                    int index = x * 160 + y;
                    if (seenCells[index]) continue;
                    seenCells[index] = true;
                    int[] cell = ParseCoarseBytes(evidence.GetCapturedCoarseCell(x, y));
                    if (cell == null)
                    { result.Valid = false; result.Error = "missing-candidate-cell"; return result; }
                    result.Visited++;
                    string rejection = CoarseFailure(cell);
                    NearbyFootprint footprint = null;
                    if (trace)
                    {
                        // Bound detailed tile reads inside the existing nearby-search callback.
                        // Every coarse-eligible candidate is retained even beyond this limit.
                        bool includeFootprint = result.Visited <= 128 || rejection == "none-coarse-eligible";
                        if (!includeFootprint) result.FootprintOmitted++;
                        string placement = includeFootprint ?
                            (footprint = CaptureNearbyFootprint(x, y)).Describe() : "not-sampled-limit";
                        if (result.Trace.Count < 2048 || rejection == "none-coarse-eligible")
                            result.Trace.Add($"cell=({x},{y}), parent=({cx},{cy}), direction={direction}, " +
                                $"depth={unchecked((sbyte)parent[1]) + 1}, foreign={(sbyte)cell[0]}, " +
                                $"bytes={evidence.GetCapturedCoarseCell(x, y).Bytes}, firstRule={rejection}, " +
                                $"footprint={placement}");
                        else result.TraceOmitted++;
                    }
                    if ((sbyte)cell[0] >= 15) continue;
                    if (rejection == "none-coarse-eligible")
                    {
                        AiPathTileSample anchor = evidence.GetCapturedAnchor(x, y);
                        if (anchor == null || anchor.NativeComponent != anchor.ApiComponent)
                        { result.Valid = false; result.Error = $"anchor-view-mismatch-({x},{y})"; return result; }
                        if (excludeInvalidFootprint)
                        {
                            if (result.CandidateTrace.Count >= 256)
                            { result.Valid = false; result.Error = "candidate-audit-limit-256"; return result; }
                            footprint = footprint ?? CaptureNearbyFootprint(x, y);
                            if (!footprint.Complete || anchor.NativeComponent != footprint.AnchorNative)
                            { result.Valid = false; result.Error = $"footprint-view-unavailable-({x},{y})"; return result; }
                            string reason = anchor.NativeComponent == 0 && footprint.Bit4 != 0 ?
                                "anchor-component-zero+parcel-bit4-in-3x3" :
                                anchor.NativeComponent == 0 ? "anchor-component-zero" :
                                footprint.Bit4 != 0 ? "parcel-bit4-in-3x3" : "no-proven-exclusion";
                            result.CandidateTrace.Add($"cell=({x},{y}), coarse={evidence.GetCapturedCoarseCell(x, y).Bytes}, " +
                                $"decision={reason}, footprint={footprint.Describe()}");
                            if (footprint.ExcludeForCopyProbe)
                                result.MaskedCandidates.Add(new NearbyMaskedCandidate
                                { Index = index, Reason = reason, FootprintTiles = footprint.Tiles });
                            else
                            {
                                result.X = x; result.Y = y;
                                result.ExpectedFootprint = footprint;
                                return result;
                            }
                        }
                        else { result.X = x; result.Y = y; return result; }
                    }
                    if (tail >= queue.Length)
                    { result.Valid = false; result.Error = "queue-overflow"; return result; }
                    queue[tail++] = index;
                }
            }
            if (excludeInvalidFootprint && result.X < 0 && result.Valid)
            { result.Valid = false; result.Error = "no-unmasked-candidate"; }
            return result;
        }

        internal static int[] ParseCoarseBytes(AiCoarseCellSample cell)
        {
            if (cell == null || cell.Bytes == null) return null;
            string[] parts = cell.Bytes.Split('-');
            if (parts.Length < 16) return null;
            var result = new int[16];
            for (int i = 0; i < 16; i++)
                if (!int.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out result[i])) return null;
            return result;
        }

        internal static string CoarseFailure(int[] cell)
        {
            if ((sbyte)cell[0] >= 15) return "traversal-foreign-component-limit";
            if (cell[10] != 0) return "height-range";
            if (cell[11] != 0) return "structure-or-reservation";
            if (cell[12] != 0) return "outside-usable-map";
            if (cell[15] != 0) return "impassable-edge";
            if (cell[3] != 0) return "tree-weight";
            if ((sbyte)cell[4] >= 1) return "stone-count";
            if (cell[0] != 0) return "foreign-component-count";
            return "none-coarse-eligible";
        }

    }
}
