using System;

namespace AreaDemolitionTest
{
    internal readonly struct ScreenPoint
    {
        internal readonly float X, Y;
        internal ScreenPoint(float x, float y) { X = x; Y = y; }
        internal bool IsFinite => ScreenRect.Finite(X) && ScreenRect.Finite(Y);
    }

    // Physical screen pixels, with Unity's bottom-left origin, as in Vanilla troop selection.
    internal readonly struct ScreenRect : IEquatable<ScreenRect>
    {
        internal readonly float MinX, MinY, MaxX, MaxY;
        internal ScreenRect(float x1, float y1, float x2, float y2)
        { MinX = Math.Min(x1, x2); MinY = Math.Min(y1, y2); MaxX = Math.Max(x1, x2); MaxY = Math.Max(y1, y2); }
        internal float Width => MaxX - MinX;
        internal float Height => MaxY - MinY;
        internal bool IsFinite => Finite(MinX) && Finite(MinY) && Finite(MaxX) && Finite(MaxY);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // A conservative broad phase for one native grid row. Height only broadens screen X;
        // the exact quad test below still decides whether the cell belongs to the rectangle.
        internal bool TryGetColumnRange(double rowBaseX, double columnStepX, double heightStepX,
            double minHeight, double maxHeight, double footprintHalfWidth, int minColumn, int maxColumn,
            out int first, out int last)
        {
            first = last = 0;
            if (!IsFinite || !Finite(rowBaseX) || !Finite(columnStepX) || !Finite(heightStepX) ||
                !Finite(minHeight) || !Finite(maxHeight) || !Finite(footprintHalfWidth) ||
                minHeight > maxHeight || footprintHalfWidth < 0 || minColumn > maxColumn) return false;
            double h0 = heightStepX * minHeight, h1 = heightStepX * maxHeight;
            double low = MinX - footprintHalfWidth - rowBaseX - Math.Max(h0, h1);
            double high = MaxX + footprintHalfWidth - rowBaseX - Math.Min(h0, h1);
            if (columnStepX == 0)
            {
                if (low > 0 || high < 0) return false;
                first = minColumn; last = maxColumn; return true;
            }
            double a = low / columnStepX, b = high / columnStepX;
            double start = Math.Floor(Math.Min(a, b)) - 1, end = Math.Ceiling(Math.Max(a, b)) + 1;
            if (end < minColumn || start > maxColumn) return false;
            first = (int)Math.Max(minColumn, start); last = (int)Math.Min(maxColumn, end); return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // The quad's perimeter must be ordered. Inclusive SAT also catches crossings where
        // neither polygon contains a vertex of the other, without selecting empty AABB corners.
        internal bool IntersectsQuad(ScreenPoint a, ScreenPoint b, ScreenPoint c, ScreenPoint d)
        {
            if (!IsFinite || !a.IsFinite || !b.IsFinite || !c.IsFinite || !d.IsFinite) return false;
            float minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            float maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            float minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            float maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            if (maxX < MinX || minX > MaxX || maxY < MinY || minY > MaxY) return false;
            // Large selections contain most footprint cells completely. Their AABB being
            // inside the rectangle proves intersection without testing the four edge axes.
            if (minX >= MinX && maxX <= MaxX && minY >= MinY && maxY <= MaxY) return true;
            return !Separates(a, b, a, b, c, d) && !Separates(b, c, a, b, c, d) &&
                !Separates(c, d, a, b, c, d) && !Separates(d, a, a, b, c, d);
        }

        private bool Separates(ScreenPoint first, ScreenPoint second, ScreenPoint a, ScreenPoint b, ScreenPoint c, ScreenPoint d)
        {
            double nx = -(double)second.Y + first.Y, ny = (double)second.X - first.X;
            if (nx == 0 && ny == 0) return false;
            double pa = nx * a.X + ny * a.Y, pb = nx * b.X + ny * b.Y;
            double pc = nx * c.X + ny * c.Y, pd = nx * d.X + ny * d.Y;
            double qMin = Math.Min(Math.Min(pa, pb), Math.Min(pc, pd)), qMax = Math.Max(Math.Max(pa, pb), Math.Max(pc, pd));
            double rMin = nx * (nx < 0 ? MaxX : MinX) + ny * (ny < 0 ? MaxY : MinY);
            double rMax = nx * (nx < 0 ? MinX : MaxX) + ny * (ny < 0 ? MinY : MaxY);
            return qMax < rMin || qMin > rMax;
        }

        public bool Equals(ScreenRect other) => MinX.Equals(other.MinX) && MinY.Equals(other.MinY) && MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);
        public override bool Equals(object other) => other is ScreenRect rectangle && Equals(rectangle);
        public override int GetHashCode() { unchecked { return ((MinX.GetHashCode() * 397 ^ MinY.GetHashCode()) * 397 ^ MaxX.GetHashCode()) * 397 ^ MaxY.GetHashCode(); } }
        public static bool operator ==(ScreenRect first, ScreenRect second) => first.Equals(second);
        public static bool operator !=(ScreenRect first, ScreenRect second) => !first.Equals(second);
    }
}
