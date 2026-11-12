using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.AreaDemolition
{
    internal static class VanillaHitGeometry
    {
        // GameMap.CalcMapTileFromMousePos: offsets -800..0 in two-pixel steps.
        // The continuous hull intentionally includes the spaces between those probes.
        internal static float MaximumRise(float testHeight, float buildingHeight, float spriteHeight, float zoom)
        {
            if (!ScreenRect.Finite(testHeight) || !ScreenRect.Finite(buildingHeight) || !ScreenRect.Finite(spriteHeight) ||
                !ScreenRect.Finite(zoom) || zoom <= 0) throw new ArgumentException("Vanilla hit heights and zoom must be finite.");
            float extra = buildingHeight != 0 ? buildingHeight * 64f : Math.Max(0, spriteHeight - 32f);
            float height = testHeight * 64f + extra;
            if (!ScreenRect.Finite(height)) throw new ArgumentException("Vanilla's combined hit height must be finite.");
            double maximum = Math.Max(0, Math.Min(800, (double)height * zoom));
            // Preserve the resolver's float division at an exact two-pixel boundary. It can
            // accept the next probe when ideal H*zoom falls just below it through rounding.
            int nextProbe = (int)Math.Ceiling(maximum / 2) * 2;
            if (nextProbe <= 800 && -nextProbe / zoom + height >= 0) maximum = Math.Max(maximum, nextProbe);
            return (float)maximum;
        }
    }

    internal readonly struct ScreenHitHull
    {
        private readonly ScreenPoint a, b, c, d;
        private readonly float rise;
        internal readonly ScreenRect Bounds;
        internal ScreenHitHull(ScreenPoint a, ScreenPoint b, ScreenPoint c, ScreenPoint d, float maximumRise)
        {
            if (!a.IsFinite || !b.IsFinite || !c.IsFinite || !d.IsFinite || !ScreenRect.Finite(maximumRise) || maximumRise < 0)
                throw new ArgumentException("A screen hit hull must be finite.");
            this.a = a; this.b = b; this.c = c; this.d = d; rise = maximumRise;
            Bounds = new ScreenRect(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
                Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + maximumRise);
        }
        internal bool Intersects(ScreenRect rectangle) => Bounds.Intersects(rectangle) && rectangle.IntersectsExtrudedQuad(a, b, c, d, rise);
    }

    // Owns the arrays supplied by the projection builder. Queries are pure managed work;
    // clipping a completed quote never re-enters Unity, native permission checks or deletion.
    internal sealed class ScreenHitGeometry
    {
        private readonly List<RootShapes> roots;
        private readonly Dictionary<RootKey, RootShapes> byIdentity = new Dictionary<RootKey, RootShapes>();
        internal ScreenHitGeometry(int capacity=0) { roots=new List<RootShapes>(capacity); }
        internal ScreenHitGeometry(DemolitionTarget[] targets, ScreenHitHull[][] shapes) : this(targets?.Length??0)
        {
            if (targets == null || shapes == null || targets.Length != shapes.Length) throw new ArgumentException("Root and hit-shape counts differ.");
            for (int i = 0; i < targets.Length; i++)
                AddRoot(targets[i],shapes[i]);
        }

        // The Unity builder adds one finished root per budgeted step. It publishes the
        // geometry only after construction, so readers never observe mutable records.
        internal void AddRoot(DemolitionTarget target,ScreenHitHull[] shapes)
        {
            if(target==null || shapes==null || shapes.Length==0)return;
            var key=new RootKey(target);
            if(byIdentity.TryGetValue(key,out RootShapes existing))existing.Append(shapes);
            else {var record=new RootShapes(target,shapes);byIdentity.Add(key,record);roots.Add(record);}
        }

        internal DemolitionTarget[] SelectRoots(ScreenRect rectangle)
        {
            if (!rectangle.IsFinite) return Array.Empty<DemolitionTarget>();
            var selected = new List<DemolitionTarget>();
            foreach (RootShapes root in roots) if (root.Intersects(rectangle)) selected.Add(root.Root);
            return selected.Count == 0 ? Array.Empty<DemolitionTarget>() : selected.ToArray();
        }

        internal bool Intersects(DemolitionTarget root, ScreenRect rectangle) => root != null && rectangle.IsFinite &&
            byIdentity.TryGetValue(new RootKey(root), out RootShapes shapes) && shapes.Intersects(rectangle);
        internal bool ContainsRoot(DemolitionTarget root) => root!=null && byIdentity.ContainsKey(new RootKey(root));

        private sealed class RootShapes
        {
            internal readonly DemolitionTarget Root;
            private ScreenHitHull[] hulls;
            private ScreenRect bounds;
            internal RootShapes(DemolitionTarget root, ScreenHitHull[] shapes) { Root = root; hulls = shapes; bounds = shapes[0].Bounds; Include(shapes); }
            private void Include(ScreenHitHull[] shapes)
            {
                foreach (ScreenHitHull shape in shapes)
                    bounds = new ScreenRect(Math.Min(bounds.MinX, shape.Bounds.MinX), Math.Min(bounds.MinY, shape.Bounds.MinY),
                        Math.Max(bounds.MaxX, shape.Bounds.MaxX), Math.Max(bounds.MaxY, shape.Bounds.MaxY));
            }
            internal void Append(ScreenHitHull[] shapes)
            {
                int old = hulls.Length; Array.Resize(ref hulls, old + shapes.Length); Array.Copy(shapes, 0, hulls, old, shapes.Length); Include(shapes);
            }
            internal bool Intersects(ScreenRect rectangle)
            {
                if (!bounds.Intersects(rectangle)) return false;
                foreach (ScreenHitHull hull in hulls) if (hull.Intersects(rectangle)) return true;
                return false;
            }
        }

        private readonly struct RootKey : IEquatable<RootKey>
        {
            private readonly TargetKind kind;
            private readonly int slot, tile, task;
            private readonly uint global;
            internal RootKey(DemolitionTarget root) { kind = root.Kind; slot = root.Slot; tile = root.TileId; task = root.TaskIndex; global = root.GlobalId; }
            public bool Equals(RootKey other) => kind == other.kind && slot == other.slot && tile == other.tile && task == other.task && global == other.global;
            public override bool Equals(object other) => other is RootKey key && Equals(key);
            public override int GetHashCode() { unchecked { return ((((int)kind * 397 ^ slot) * 397 ^ tile) * 397 ^ task) * 397 ^ (int)global; } }
        }
    }
}
