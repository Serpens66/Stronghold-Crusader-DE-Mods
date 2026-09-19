using System;
using System.Collections.Generic;

namespace FormationTest
{
    internal enum FormationKind : byte
    {
        Vanilla = 0,
        Block = 1,
        Line = 2,
        Column = 3,
        Wedge = 4,
        Circle = 5
    }

    internal enum FormationRole : byte
    {
        Front = 0,
        Protected = 1,
        Neutral = 2,
        Rear = 3
    }

    internal enum RangedPlacementMode : byte
    {
        Off = 0,
        Rear = 1,
        Center = 2
    }

    internal readonly struct FormationPoint
    {
        internal FormationPoint(int x, int y, int rank, int file)
        {
            X = x;
            Y = y;
            Rank = rank;
            File = file;
        }

        internal int X { get; }
        internal int Y { get; }
        internal int Rank { get; }
        internal int File { get; }
    }

    internal readonly struct FormationUnit
    {
        internal FormationUnit(int unitId, int unitType, FormationRole role)
            : this(unitId, 0, unitType, role)
        {
        }

        internal FormationUnit(
            int unitId, uint globalId, int unitType, FormationRole role)
        {
            UnitId = unitId;
            GlobalId = globalId;
            UnitType = unitType;
            Role = role;
        }

        internal int UnitId { get; }
        internal uint GlobalId { get; }
        internal int UnitType { get; }
        internal FormationRole Role { get; }
    }

    internal readonly struct FormationPreviewKey : IEquatable<FormationPreviewKey>
    {
        private FormationPreviewKey(
            FormationKind kind,
            int density,
            RangedPlacementMode placementMode,
            int directionSector,
            int width,
            int targetX,
            int targetY,
            int unitCount)
        {
            Kind = kind;
            Density = density;
            PlacementMode = placementMode;
            DirectionSector = directionSector;
            Width = width;
            TargetX = targetX;
            TargetY = targetY;
            UnitCount = unitCount;
        }

        internal FormationKind Kind { get; }
        internal int Density { get; }
        internal RangedPlacementMode PlacementMode { get; }
        internal int DirectionSector { get; }
        internal int Width { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal int UnitCount { get; }

        internal static FormationPreviewKey Create(
            FormationKind kind,
            int density,
            RangedPlacementMode placementMode,
            int directionSector,
            int width,
            int targetX,
            int targetY,
            int unitCount)
        {
            FormationKind normalizedKind = FormationModel.NormalizeKind((int)kind);
            bool fixedShape = normalizedKind == FormationKind.Vanilla ||
                normalizedKind == FormationKind.Circle;
            return new FormationPreviewKey(
                normalizedKind,
                FormationModel.NormalizeDensity(density),
                FormationModel.NormalizePlacementMode((int)placementMode),
                directionSector & 7,
                fixedShape ? 1 : Math.Max(1, width),
                targetX,
                targetY,
                Math.Max(0, unitCount));
        }

        public bool Equals(FormationPreviewKey other) =>
            Kind == other.Kind && Density == other.Density &&
            PlacementMode == other.PlacementMode &&
            DirectionSector == other.DirectionSector && Width == other.Width &&
            TargetX == other.TargetX && TargetY == other.TargetY &&
            UnitCount == other.UnitCount;

        public override bool Equals(object obj) =>
            obj is FormationPreviewKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ Density;
                hash = hash * 397 ^ PlacementMode.GetHashCode();
                hash = hash * 397 ^ DirectionSector;
                hash = hash * 397 ^ Width;
                hash = hash * 397 ^ TargetX;
                hash = hash * 397 ^ TargetY;
                return hash * 397 ^ UnitCount;
            }
        }
    }

    internal readonly struct FormationDefaultsMigration
    {
        internal const int CurrentRevision = 1;

        internal FormationDefaultsMigration(
            FormationKind kind,
            int revision,
            bool kindChanged,
            bool revisionChanged)
        {
            Kind = kind;
            Revision = revision;
            KindChanged = kindChanged;
            RevisionChanged = revisionChanged;
        }

        internal FormationKind Kind { get; }
        internal int Revision { get; }
        internal bool KindChanged { get; }
        internal bool RevisionChanged { get; }

        internal static FormationDefaultsMigration Resolve(
            FormationKind currentKind,
            int currentRevision)
        {
            if (currentRevision >= CurrentRevision)
            {
                return new FormationDefaultsMigration(
                    currentKind,
                    currentRevision,
                    kindChanged: false,
                    revisionChanged: false);
            }

            bool migrateKind = currentKind == FormationKind.Vanilla;
            return new FormationDefaultsMigration(
                migrateKind ? FormationKind.Block : currentKind,
                CurrentRevision,
                migrateKind,
                revisionChanged: true);
        }
    }

    internal readonly struct PlacementDefaultsMigration
    {
        internal const int CurrentRevision = 2;

        internal PlacementDefaultsMigration(
            RangedPlacementMode mode,
            int revision,
            bool changed)
        {
            Mode = mode;
            Revision = revision;
            Changed = changed;
        }

        internal RangedPlacementMode Mode { get; }
        internal int Revision { get; }
        internal bool Changed { get; }

        internal static PlacementDefaultsMigration Resolve(
            int currentRevision,
            bool legacyRearSorting,
            RangedPlacementMode currentMode)
        {
            if (currentRevision >= CurrentRevision)
            {
                return new PlacementDefaultsMigration(
                    FormationModel.NormalizePlacementMode((int)currentMode),
                    currentRevision,
                    changed: false);
            }
            return new PlacementDefaultsMigration(
                legacyRearSorting
                    ? RangedPlacementMode.Rear
                    : RangedPlacementMode.Off,
                CurrentRevision,
                changed: true);
        }
    }

    internal static class FormationModel
    {
        private static readonly int[] ForwardX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] ForwardY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        internal static FormationKind NormalizeKind(int value) =>
            value >= (int)FormationKind.Vanilla && value <= (int)FormationKind.Circle
                ? (FormationKind)value
                : FormationKind.Vanilla;

        internal static int NormalizeDensity(int value) =>
            value < 1 || value > 4 ? 2 : value;

        internal static RangedPlacementMode NormalizePlacementMode(int value) =>
            value >= (int)RangedPlacementMode.Off && value <= (int)RangedPlacementMode.Center
                ? (RangedPlacementMode)value
                : RangedPlacementMode.Off;

        internal static FormationKind Next(FormationKind kind) =>
            kind == FormationKind.Circle
                ? FormationKind.Vanilla
                : (FormationKind)((byte)kind + 1);

        internal static int ChangeDensity(int density, int wheelDirection) =>
            Math.Max(1, Math.Min(4, NormalizeDensity(density) - Math.Sign(wheelDirection)));

        internal static bool IsNativeVanillaSlotCandidate(
            int relativeX,
            int relativeY,
            int pathDistance,
            int logicFlags,
            int density,
            bool assassinOnly) =>
            pathDistance > 0 && pathDistance < 4000 &&
            (Math.Abs(relativeX) + Math.Abs(relativeY)) % NormalizeDensity(density) == 0 &&
            (!assassinOnly || (logicFlags & 0x10000100) == 0);

        internal static int QuantizeDirection(int deltaX, int deltaY, int fallbackSector = 0)
        {
            if (deltaX == 0 && deltaY == 0)
                return fallbackSector & 7;
            double angle = Math.Atan2(deltaY, deltaX);
            int eastClockwise = (int)Math.Round(angle / (Math.PI / 4.0));
            int sector = (eastClockwise + 2) & 7;
            return sector;
        }

        internal static void GetForwardVector(
            int directionSector,
            out int forwardX,
            out int forwardY)
        {
            int sector = directionSector & 7;
            forwardX = ForwardX[sector];
            forwardY = ForwardY[sector];
        }

        internal static int ResolveAutomaticWidth(FormationKind kind, int count)
        {
            if (count <= 1)
                return Math.Max(1, count);
            if (kind == FormationKind.Wedge)
            {
                double wedgeRoot = Math.Sqrt(count);
                return Math.Min(count, Math.Max(3,
                    MakeOdd((int)Math.Ceiling(wedgeRoot * 1.5))));
            }
            if (kind == FormationKind.Circle)
                return ResolveCircleDiameter(count);
            int rows = ResolveAutomaticRows(kind, count);
            return Math.Max(1, Math.Min(count, (count + rows - 1) / rows));
        }

        internal static int ResolveAutomaticRows(FormationKind kind, int count)
        {
            if (count <= 1)
                return Math.Max(1, count);
            double root = Math.Sqrt(count);
            switch (kind)
            {
                case FormationKind.Line:
                    return Math.Min(count, Math.Max(2, (int)Math.Ceiling(root / 2.0)));
                case FormationKind.Column:
                    return Math.Min(count, Math.Max(2, (int)Math.Ceiling(root * 2.0)));
                case FormationKind.Wedge:
                    return CountWedgeRows(count, ResolveAutomaticWidth(kind, count));
                case FormationKind.Circle:
                    return ResolveCircleDiameter(count);
                default:
                    return Math.Min(count, Math.Max(1, (int)Math.Ceiling(root)));
            }
        }

        internal static int ResolveDraggedWidth(
            FormationKind kind,
            int tileDistance,
            int count)
        {
            if (count <= 0)
                return 0;
            int automaticWidth = ResolveAutomaticWidth(kind, count);
            if (kind == FormationKind.Vanilla || kind == FormationKind.Circle)
                return automaticWidth;
            int depthReduction = Math.Max(0, (Math.Max(2, tileDistance) - 2) / 2);
            if (depthReduction == 0)
                return automaticWidth;
            int targetRows = Math.Max(1,
                ResolveAutomaticRows(kind, count) - depthReduction);
            if (kind != FormationKind.Wedge)
                return Math.Max(1, Math.Min(count, (count + targetRows - 1) / targetRows));

            for (int width = MakeOdd(automaticWidth); width <= count; width += 2)
            {
                if (CountWedgeRows(count, width) <= targetRows)
                    return Math.Min(count, width);
            }
            return count;
        }

        internal static int ResolveActualRows(FormationKind kind, int count, int width)
        {
            if (count <= 0)
                return 0;
            int normalizedWidth = Math.Max(1, Math.Min(count, width));
            return kind == FormationKind.Wedge
                ? CountWedgeRows(count, normalizedWidth)
                : kind == FormationKind.Circle
                ? ResolveCircleDiameter(count)
                : (count + normalizedWidth - 1) / normalizedWidth;
        }

        internal static List<FormationPoint> BuildRelativeSlots(
            FormationKind kind,
            int count,
            int width,
            int density,
            int directionSector)
        {
            var result = new List<FormationPoint>(Math.Max(0, count));
            if (count <= 0)
                return result;

            int normalizedDensity = NormalizeDensity(density);
            int normalizedWidth = Math.Max(1, Math.Min(count, width));
            int sector = directionSector & 7;
            int forwardX = ForwardX[sector];
            int forwardY = ForwardY[sector];
            int rightX = -forwardY;
            int rightY = forwardX;

            if (kind == FormationKind.Vanilla)
                throw new InvalidOperationException(
                    "Vanilla slots require the native pathfinding candidate order.");
            if (kind == FormationKind.Circle)
                BuildCircle(count, normalizedDensity,
                    forwardX, forwardY, rightX, rightY, result);
            else if (kind == FormationKind.Wedge)
                BuildWedge(count, normalizedWidth, normalizedDensity,
                    forwardX, forwardY, rightX, rightY, result);
            else
                BuildRanks(count, normalizedWidth, normalizedDensity,
                    forwardX, forwardY, rightX, rightY, result);
            Center(result);
            return result;
        }

        internal static int[] AssignSlotsByRole(
            IReadOnlyList<FormationUnit> units,
            IReadOnlyList<FormationPoint> slots,
            RangedPlacementMode placementMode,
            FormationKind kind = FormationKind.Block)
        {
            int count = Math.Min(units?.Count ?? 0, slots?.Count ?? 0);
            var assignment = new int[count];
            for (int index = 0; index < count; index++)
                assignment[index] = index;
            RangedPlacementMode normalizedMode = NormalizePlacementMode((int)placementMode);
            if (normalizedMode == RangedPlacementMode.Off || count < 2)
                return assignment;

            if (normalizedMode == RangedPlacementMode.Center)
                return AssignSlotsToProtectedCenter(units, slots, count, kind);

            var unitIndices = new List<int>(count);
            for (int index = 0; index < count; index++)
                unitIndices.Add(index);
            unitIndices.Sort((left, right) =>
            {
                int role = RoleOrder(units[left].Role).CompareTo(RoleOrder(units[right].Role));
                return role != 0 ? role : units[left].UnitId.CompareTo(units[right].UnitId);
            });

            var slotIndices = new List<int>(count);
            for (int index = 0; index < count; index++)
                slotIndices.Add(index);
            slotIndices.Sort((left, right) =>
            {
                int rank = slots[left].Rank.CompareTo(slots[right].Rank);
                if (rank != 0)
                    return rank;
                int abs = Math.Abs(slots[left].File).CompareTo(Math.Abs(slots[right].File));
                return abs != 0 ? abs : slots[left].File.CompareTo(slots[right].File);
            });

            for (int order = 0; order < count; order++)
                assignment[unitIndices[order]] = slotIndices[order];
            return assignment;
        }

        private static int[] AssignSlotsToProtectedCenter(
            IReadOnlyList<FormationUnit> units,
            IReadOnlyList<FormationPoint> slots,
            int count,
            FormationKind kind)
        {
            var assignment = new int[count];
            var unitIndices = new List<int>(count);
            var slotIndices = new List<int>(count);
            int maximumRank = 0;
            for (int index = 0; index < count; index++)
            {
                assignment[index] = index;
                unitIndices.Add(index);
                slotIndices.Add(index);
                maximumRank = Math.Max(maximumRank, slots[index].Rank);
            }

            unitIndices.Sort((left, right) =>
            {
                int group = CenterRoleOrder(units[left].Role).CompareTo(
                    CenterRoleOrder(units[right].Role));
                return group != 0 ? group : units[left].UnitId.CompareTo(units[right].UnitId);
            });

            var rowPositions = new int[count];
            var rowCounts = new int[maximumRank + 1];
            for (int rank = 0; rank <= maximumRank; rank++)
            {
                var row = new List<int>();
                for (int index = 0; index < count; index++)
                    if (slots[index].Rank == rank)
                        row.Add(index);
                row.Sort((left, right) => slots[left].File.CompareTo(slots[right].File));
                rowCounts[rank] = row.Count;
                for (int position = 0; position < row.Count; position++)
                    rowPositions[row[position]] = position;
            }

            slotIndices.Sort((left, right) =>
            {
                if (kind == FormationKind.Circle || kind == FormationKind.Vanilla)
                {
                    long leftCircleRadius = (long)slots[left].X * slots[left].X +
                        (long)slots[left].Y * slots[left].Y;
                    long rightCircleRadius = (long)slots[right].X * slots[right].X +
                        (long)slots[right].Y * slots[right].Y;
                    int radiusOrder = rightCircleRadius.CompareTo(leftCircleRadius);
                    return radiusOrder != 0 ? radiusOrder : left.CompareTo(right);
                }
                int leftDepth = BoundaryDepth(slots[left], rowPositions[left],
                    rowCounts[slots[left].Rank], maximumRank);
                int rightDepth = BoundaryDepth(slots[right], rowPositions[right],
                    rowCounts[slots[right].Rank], maximumRank);
                int depth = leftDepth.CompareTo(rightDepth);
                if (depth != 0)
                    return depth;
                long leftRadius = (long)slots[left].X * slots[left].X +
                    (long)slots[left].Y * slots[left].Y;
                long rightRadius = (long)slots[right].X * slots[right].X +
                    (long)slots[right].Y * slots[right].Y;
                int radius = rightRadius.CompareTo(leftRadius);
                return radius != 0 ? radius : left.CompareTo(right);
            });

            for (int order = 0; order < count; order++)
                assignment[unitIndices[order]] = slotIndices[order];
            return assignment;
        }

        private static int CenterRoleOrder(FormationRole role)
        {
            switch (role)
            {
                case FormationRole.Front: return 0;
                case FormationRole.Neutral: return 1;
                default: return 2;
            }
        }

        private static int BoundaryDepth(
            FormationPoint point,
            int rowPosition,
            int rowCount,
            int maximumRank) => Math.Min(
                Math.Min(point.Rank, maximumRank - point.Rank),
                Math.Min(rowPosition, rowCount - 1 - rowPosition));

        private static int CountWedgeRows(int count, int maximumWidth)
        {
            int produced = 0;
            int rank = 0;
            while (produced < count)
            {
                produced += Math.Min(maximumWidth, 1 + rank * 2);
                rank++;
            }
            return rank;
        }

        private static int ResolveCircleDiameter(int count)
        {
            if (count <= 1)
                return Math.Max(1, count);
            int radius = 0;
            while (CountCirclePoints(radius) < count)
                radius++;
            return radius * 2 + 1;
        }

        private static int CountCirclePoints(int radius)
        {
            int count = 0;
            int squaredRadius = radius * radius;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= squaredRadius)
                        count++;
            return count;
        }

        private static void BuildCircle(
            int count,
            int spacing,
            int forwardX,
            int forwardY,
            int rightX,
            int rightY,
            List<FormationPoint> destination)
        {
            int radius = (ResolveCircleDiameter(count) - 1) / 2;
            var candidates = new List<CirclePoint>();
            int squaredRadius = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int squared = x * x + y * y;
                    if (squared <= squaredRadius)
                        candidates.Add(new CirclePoint(x, y, squared));
                }
            }
            candidates.Sort((left, right) =>
            {
                int distance = left.SquaredRadius.CompareTo(right.SquaredRadius);
                if (distance != 0)
                    return distance;
                int axis = Math.Abs(left.Y).CompareTo(Math.Abs(right.Y));
                if (axis != 0)
                    return axis;
                int y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.X.CompareTo(right.X);
            });

            var used = new HashSet<long>();
            if ((count & 1) == 0)
                used.Add(CircleKey(0, 0));
            for (int index = 0; index < candidates.Count && destination.Count < count; index++)
            {
                CirclePoint point = candidates[index];
                long key = CircleKey(point.X, point.Y);
                if (!used.Add(key))
                    continue;
                AddCirclePoint(point.X, point.Y, spacing,
                    forwardX, forwardY, rightX, rightY, radius, destination);
                if (destination.Count >= count || (point.X == 0 && point.Y == 0))
                    continue;
                long oppositeKey = CircleKey(-point.X, -point.Y);
                if (used.Add(oppositeKey))
                    AddCirclePoint(-point.X, -point.Y, spacing,
                        forwardX, forwardY, rightX, rightY, radius, destination);
            }
        }

        private static void AddCirclePoint(
            int localX,
            int localY,
            int spacing,
            int forwardX,
            int forwardY,
            int rightX,
            int rightY,
            int maximumProjection,
            List<FormationPoint> destination)
        {
            int baseX = rightX * localX + forwardX * localY;
            int baseY = rightY * localX + forwardY * localY;
            int x = baseX * spacing;
            int y = baseY * spacing;
            int projection = baseX * forwardX + baseY * forwardY;
            int rank = maximumProjection - projection;
            int file = baseX * rightX + baseY * rightY;
            destination.Add(new FormationPoint(x, y, rank, file));
        }

        private static long CircleKey(int x, int y) =>
            ((long)(uint)x << 32) | (uint)y;

        private readonly struct CirclePoint
        {
            internal CirclePoint(int x, int y, int squaredRadius)
            {
                X = x;
                Y = y;
                SquaredRadius = squaredRadius;
            }
            internal int X { get; }
            internal int Y { get; }
            internal int SquaredRadius { get; }
        }

        private static void BuildRanks(
            int count,
            int width,
            int spacing,
            int forwardX,
            int forwardY,
            int rightX,
            int rightY,
            List<FormationPoint> destination)
        {
            int ranks = (count + width - 1) / width;
            int produced = 0;
            for (int rank = 0; rank < ranks && produced < count; rank++)
            {
                int inRank = Math.Min(width, count - produced);
                for (int file = 0; file < inRank; file++)
                {
                    int lateral2 = file * 2 - (inRank - 1);
                    int x2 = rightX * lateral2 * spacing - forwardX * rank * 2 * spacing;
                    int y2 = rightY * lateral2 * spacing - forwardY * rank * 2 * spacing;
                    destination.Add(new FormationPoint(RoundHalf(x2), RoundHalf(y2), rank, lateral2));
                    produced++;
                }
            }
        }

        private static void BuildWedge(
            int count,
            int maximumWidth,
            int spacing,
            int forwardX,
            int forwardY,
            int rightX,
            int rightY,
            List<FormationPoint> destination)
        {
            int produced = 0;
            int rank = 0;
            while (produced < count)
            {
                int desired = Math.Min(maximumWidth, 1 + rank * 2);
                int inRank = Math.Min(desired, count - produced);
                for (int file = 0; file < inRank; file++)
                {
                    int lateral2 = file * 2 - (inRank - 1);
                    int x2 = rightX * lateral2 * spacing - forwardX * rank * 2 * spacing;
                    int y2 = rightY * lateral2 * spacing - forwardY * rank * 2 * spacing;
                    destination.Add(new FormationPoint(RoundHalf(x2), RoundHalf(y2), rank, lateral2));
                    produced++;
                }
                rank++;
            }
        }

        private static void Center(List<FormationPoint> points)
        {
            if (points.Count == 0)
                return;
            long sumX = 0;
            long sumY = 0;
            for (int index = 0; index < points.Count; index++)
            {
                sumX += points[index].X;
                sumY += points[index].Y;
            }
            int centerX = (int)Math.Round((double)sumX / points.Count);
            int centerY = (int)Math.Round((double)sumY / points.Count);
            for (int index = 0; index < points.Count; index++)
            {
                FormationPoint point = points[index];
                points[index] = new FormationPoint(
                    point.X - centerX,
                    point.Y - centerY,
                    point.Rank,
                    point.File);
            }
        }

        private static int RoleOrder(FormationRole role)
        {
            switch (role)
            {
                case FormationRole.Front: return 0;
                case FormationRole.Protected: return 1;
                case FormationRole.Neutral: return 2;
                default: return 3;
            }
        }

        private static int MakeOdd(int value) => (value & 1) == 0 ? value + 1 : value;

        private static int RoundHalf(int doubled) =>
            doubled >= 0 ? (doubled + 1) / 2 : (doubled - 1) / 2;
    }

    internal static class FormationPlanHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong Begin(int unitCount, RangedPlacementMode placementMode)
        {
            ulong hash = OffsetBasis;
            AddInt32(ref hash, unitCount);
            AddByte(ref hash, (byte)FormationModel.NormalizePlacementMode((int)placementMode));
            return hash;
        }

        internal static void AddEntry(
            ref ulong hash,
            int unitId,
            uint globalId,
            int x,
            int y,
            FormationRole role)
        {
            AddInt32(ref hash, unitId);
            AddUInt32(ref hash, globalId);
            AddInt32(ref hash, x);
            AddInt32(ref hash, y);
            AddByte(ref hash, (byte)role);
        }

        private static void AddInt32(ref ulong hash, int value) =>
            AddUInt32(ref hash, unchecked((uint)value));

        private static void AddUInt32(ref ulong hash, uint value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= Prime;
        }
    }

    internal static class FormationOrderMatchModel
    {
        internal static bool Matches(
            int expectedTribeId,
            int expectedX,
            int expectedY,
            int expectedNewOrder,
            int expectedMoveType,
            int actualTribeId,
            int actualX,
            int actualY,
            short actualPatrol,
            bool actualNewOrder,
            int actualMoveType) =>
            actualPatrol == 0 &&
            expectedTribeId == actualTribeId &&
            expectedX == actualX && expectedY == actualY &&
            expectedNewOrder == (actualNewOrder ? 1 : 0) &&
            expectedMoveType == actualMoveType;
    }
}
