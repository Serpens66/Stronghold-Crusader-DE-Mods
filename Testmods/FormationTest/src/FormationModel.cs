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
        Wedge = 4
    }

    internal enum FormationRole : byte
    {
        Front = 0,
        Protected = 1,
        Neutral = 2,
        Rear = 3
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
        {
            UnitId = unitId;
            UnitType = unitType;
            Role = role;
        }

        internal int UnitId { get; }
        internal int UnitType { get; }
        internal FormationRole Role { get; }
    }

    internal static class FormationModel
    {
        private static readonly int[] ForwardX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] ForwardY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        internal static FormationKind NormalizeKind(int value) =>
            value >= (int)FormationKind.Vanilla && value <= (int)FormationKind.Wedge
                ? (FormationKind)value
                : FormationKind.Vanilla;

        internal static int NormalizeDensity(int value) =>
            value < 1 || value > 4 ? 2 : value;

        internal static FormationKind Next(FormationKind kind) =>
            kind == FormationKind.Wedge
                ? FormationKind.Vanilla
                : (FormationKind)((byte)kind + 1);

        internal static int ChangeDensity(int density, int wheelDirection) =>
            Math.Max(1, Math.Min(4, NormalizeDensity(density) - Math.Sign(wheelDirection)));

        internal static int QuantizeDirection(int deltaX, int deltaY, int fallbackSector = 0)
        {
            if (deltaX == 0 && deltaY == 0)
                return fallbackSector & 7;
            double angle = Math.Atan2(deltaY, deltaX);
            int eastClockwise = (int)Math.Round(angle / (Math.PI / 4.0));
            int sector = (eastClockwise + 2) & 7;
            return sector;
        }

        internal static int ResolveAutomaticWidth(FormationKind kind, int count)
        {
            if (count <= 1)
                return Math.Max(1, count);
            double root = Math.Sqrt(count);
            switch (kind)
            {
                case FormationKind.Line:
                    return Math.Min(count, Math.Max(2, (int)Math.Ceiling(root * 2.0)));
                case FormationKind.Column:
                    return Math.Min(count, Math.Max(1, (int)Math.Ceiling(root * 0.5)));
                case FormationKind.Wedge:
                    return Math.Min(count, Math.Max(3, MakeOdd((int)Math.Ceiling(root * 1.5))));
                default:
                    return Math.Min(count, Math.Max(1, (int)Math.Ceiling(root)));
            }
        }

        internal static int ResolveDraggedWidth(int tileDistance, int density, int count)
        {
            if (count <= 0)
                return 0;
            int width = (int)Math.Round((double)Math.Max(1, tileDistance) /
                NormalizeDensity(density));
            return Math.Max(1, Math.Min(count, width));
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

            if (kind == FormationKind.Wedge)
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
            bool rearSorting)
        {
            int count = Math.Min(units?.Count ?? 0, slots?.Count ?? 0);
            var assignment = new int[count];
            for (int index = 0; index < count; index++)
                assignment[index] = index;
            if (!rearSorting || count < 2)
                return assignment;

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
}
