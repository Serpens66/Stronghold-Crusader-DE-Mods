using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;
using SHCDESE.Interop;

namespace CastlePlanner.AIVPlacement.Core
{
    // Captured on the main thread. Workers never read settings or native state.
    public sealed class KeepRangeSnapshot
    {
        public KeepRangeSnapshot(bool? bypass, IEnumerable<KeyValuePair<int, int>> ranges,
            IEnumerable<KeyValuePair<int, int>> teams = null)
        {
            Bypass = bypass;
            Ranges = new ReadOnlyDictionary<int, int>((ranges ?? Array.Empty<KeyValuePair<int, int>>())
                .ToDictionary(pair => pair.Key, pair => pair.Value));
            Teams = new ReadOnlyDictionary<int, int>((teams ?? Array.Empty<KeyValuePair<int, int>>())
                .ToDictionary(pair => pair.Key, pair => pair.Value));
        }
        public bool? Bypass { get; }
        public IReadOnlyDictionary<int, int> Ranges { get; }
        public IReadOnlyDictionary<int, int> Teams { get; }
        public int? Range(int size) => Ranges.TryGetValue(size, out int value) ? (int?)value : null;
        public string Fingerprint => (Bypass.HasValue ? (Bypass.Value ? "on" : "off") : "unknown") + ":" +
            string.Join(",", Ranges.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) + ":" +
            string.Join(",", Teams.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value));
        public static int VanillaRange(int size)
        {
            switch (size)
            {
                case 160: return 45;
                case 200: return 50;
                case 300: return 60;
                case 500: return 80;
                case 600: return 90;
                case 700: case 800: return 100;
                default: return 70;
            }
        }
        public static KeepRangeSnapshot Vanilla => new KeepRangeSnapshot(false,
            MapTileGeometry.SupportedWorldSizes.Select(size => new KeyValuePair<int, int>(size, VanillaRange(size))));
    }

    public sealed class KeepRangeAlly
    {
        public KeepRangeAlly(IEnumerable<MapCoordinate> possibleReferences, bool mayBeAbsent = false, bool unknown = false)
        {
            References = Array.AsReadOnly((possibleReferences ?? Array.Empty<MapCoordinate>()).Distinct().ToArray());
            MayBeAbsent = mayBeAbsent;
            Unknown = unknown;
        }
        public IReadOnlyList<MapCoordinate> References { get; }
        public bool MayBeAbsent { get; }
        public bool Unknown { get; }
    }

    public sealed class KeepRangeResult
    {
        internal readonly HashSet<int> Certain = new HashSet<int>();
        internal readonly HashSet<int> Possible = new HashSet<int>();
        public IReadOnlyCollection<int> CertainTiles => Certain;
        public IReadOnlyCollection<int> PossibleTiles => Possible;
        public int CertainBuildings { get; internal set; }
        public int PossibleBuildings { get; internal set; }
        public bool Unknown { get; internal set; }
        public int? Range { get; internal set; }
        public bool HasNotice => CertainBuildings > 0 || PossibleBuildings > 0 || Unknown;
    }

    public sealed class KeepRangeLayout
    {
        internal sealed class Building
        {
            internal MapCoordinate Anchor;
            internal int Size, Allowance;
        }
        internal readonly List<Building> Buildings = new List<Building>();
        public MapCoordinate? KeepReference { get; internal set; }
        public bool Known { get; internal set; } = true;
    }

    public static class KeepRangePolicy
    {
        // Current-hash 77E60 dispatch. Moat/wall AIV paths use different constructors.
        public static int? Allowance(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_KILLING_PIT:
                case eMappers.MAPPER_PITCH_DITCH:
                case eMappers.MAPPER_DRAWBRIDGE:
                case eMappers.MAPPER_DOG_CAGE: return 5;
                case eMappers.MAPPER_GATEHOUSE:
                case eMappers.MAPPER_GATE_MAIN:
                case eMappers.MAPPER_GATE_INNER:
                case eMappers.MAPPER_GATE_WOOD:
                case eMappers.MAPPER_GATE_POSTERN:
                case eMappers.MAPPER_TOWER1:
                case eMappers.MAPPER_TOWER2:
                case eMappers.MAPPER_TOWER3:
                case eMappers.MAPPER_TOWER4:
                case eMappers.MAPPER_TOWER5:
                case eMappers.MAPPER_GATE_WOOD1A:
                case eMappers.MAPPER_GATE_WOOD1B:
                case eMappers.MAPPER_GATE_WOOD1C:
                case eMappers.MAPPER_GATE_WOOD1D:
                case eMappers.MAPPER_GATE_STONE1A:
                case eMappers.MAPPER_GATE_STONE1B:
                case eMappers.MAPPER_GATE_STONE2A:
                case eMappers.MAPPER_GATE_STONE2B: return 0;
                default: return null;
            }
        }

        public static MapCoordinate Reference(MapCoordinate anchor, AivRotation rotation)
        {
            switch (rotation)
            {
                case AivRotation.Degrees0: return new MapCoordinate(anchor.X + 3, anchor.Y + 7);
                case AivRotation.Degrees90: return new MapCoordinate(anchor.X - 1, anchor.Y + 3);
                case AivRotation.Degrees180: return new MapCoordinate(anchor.X + 3, anchor.Y - 1);
                case AivRotation.Degrees270: return new MapCoordinate(anchor.X + 7, anchor.Y + 3);
                default: throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        public static KeepRangeLayout Prepare(AivProjectedCastle castle, ISet<MapCoordinate> rejected = null)
        {
            var result = new KeepRangeLayout();
            // 55320 paints in frame order; 53D00 scans the surviving raster in native Y/X order.
            // Reservations participate in overwrites but cannot become building anchors.
            var raster = new Dictionary<MapCoordinate, AivProjectedTile>();
            foreach (AivProjectedTile tile in castle.OccupiedTiles)
                raster[tile.MapCoordinate] = tile;
            var preparedFrames = new HashSet<int>();
            foreach (var pair in raster.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X))
            {
                AivProjectedTile tile = pair.Value;
                if (tile.Kind != AivProjectedTileKind.CoreFootprint || rejected?.Contains(pair.Key) == true)
                    continue;
                AivProjectedElement element = castle.Elements[tile.ElementIndex];
                var mapper = (eMappers)element.Mapper.Value;
                if (mapper == eMappers.MAPPER_KEEP2 && !result.KeepReference.HasValue)
                    result.KeepReference = Reference(pair.Key, castle.Rotation);
                int? allowance = Allowance(mapper);
                if (!allowance.HasValue) continue;
                // Pitch is a per-cell prepared path; ordinary buildings have one anchor per frame.
                if (mapper != eMappers.MAPPER_PITCH_DITCH && !preparedFrames.Add(element.BuildIndex)) continue;
                if (!element.Mapper.FootprintSize.HasValue) { result.Known = false; continue; }
                result.Buildings.Add(new KeepRangeLayout.Building
                {
                    Anchor = pair.Key, Size = element.Mapper.FootprintSize.Value, Allowance = allowance.Value
                });
            }
            if (!result.KeepReference.HasValue) result.Known = false;
            return result;
        }

        public static bool Within(MapCoordinate cell, MapCoordinate reference, int range) =>
            Math.Max(Math.Abs((long)cell.X - reference.X), Math.Abs((long)cell.Y - reference.Y)) <= range;

        public static KeepRangeResult Evaluate(KeepRangeLayout layout, KeepRangeSnapshot snapshot,
            MapTileGeometry geometry, ISet<int> evaluatedTiles, IEnumerable<KeepRangeAlly> allies = null)
        {
            int? range = snapshot.Range(geometry.WorldSize);
            var result = new KeepRangeResult { Range = range };
            if (snapshot.Bypass == true || range >= 100) return result;
            if (layout.Buildings.Count == 0 && layout.Known) return result;
            if (!layout.Known || !range.HasValue || !snapshot.Bypass.HasValue)
            {
                result.Unknown = true;
                return result;
            }
            var friends = (allies ?? Array.Empty<KeepRangeAlly>()).ToArray();
            foreach (KeepRangeLayout.Building building in layout.Buildings)
            {
                int limit = range.Value + building.Allowance;
                bool certainlyRejected = false, possiblyRejected = false;
                var footprint = new HashSet<int>();
                for (int y = 0; y < building.Size; y++)
                for (int x = 0; x < building.Size; x++)
                {
                    var cell = new MapCoordinate(building.Anchor.X + x, building.Anchor.Y + y);
                    if (!geometry.TryGetTileId(cell.X, cell.Y, out int tileId)) continue;
                    if (evaluatedTiles.Contains(tileId)) footprint.Add(tileId);
                    if (Within(cell, layout.KeepReference.Value, limit)) continue;
                    bool possibleRescue = friends.Any(ally => ally.Unknown ||
                        ally.References.Any(reference => Within(cell, reference, limit / 2)));
                    bool certainRescue = friends.Any(ally => !ally.Unknown && !ally.MayBeAbsent &&
                        ally.References.Count > 0 && ally.References.All(reference => Within(cell, reference, limit / 2)));
                    certainlyRejected |= !possibleRescue;
                    possiblyRejected |= !certainRescue;
                }
                if (certainlyRejected)
                {
                    result.CertainBuildings++;
                    result.Certain.UnionWith(footprint);
                }
                if (possiblyRejected)
                {
                    result.PossibleBuildings++;
                    result.Possible.UnionWith(footprint);
                }
            }
            return result;
        }
    }
}
