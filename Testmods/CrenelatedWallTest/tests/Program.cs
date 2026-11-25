using CrenelatedWallTest;
using MessagePack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    // Model of the synchronized map plus the runtime's managed set. Build/Normalize call the
    // production policy; VanillaGraphics models renderer 65830 with the straight-drawing hook.
    private sealed class World : IWallWorld
    {
        internal readonly Dictionary<int, WallCell> Cells = new Dictionary<int, WallCell>();
        internal readonly HashSet<int> Managed = new HashSet<int>();
        // Published straight-drawing assignments: tile -> axis neighbour ids seen at normalization.
        internal readonly Dictionary<int, int[]> Assignments = new Dictionary<int, int[]>();
        // Frozen session switch of WallRuntime: height adjustment.
        internal bool Heights = true;
        public bool TryRead(int x, int y, out WallCell cell)
        {
            cell = null; if (x < 1 || x >= 799 || y < 1 || y >= 399) return false;
            int id = y * 800 + x;
            if (!Cells.TryGetValue(id, out cell)) cell = new WallCell { Id = id, X = x, Y = y };
            return true;
        }
        // Unmanaged wall (map designer, AI, pre-mod or foreign).
        internal WallCell Put(int x, int y, int owner = 1, bool high = true)
        {
            TryRead(x, y, out WallCell cell); cell = cell.Copy();
            cell.Owner = owner; cell.IsWall = cell.IsCrenel = true; cell.High = high; Cells[cell.Id] = cell; return cell;
        }
        // Vanilla 6F230 places every raster cell that is not an intact wall at placement parity.
        internal void Build(int x, int y, int endX, int endY, int owner = 1, int maximum = 800)
        {
            var ids = new List<int>();
            foreach (WallCell old in WallPolicy.Raster(this, x, y, endX, endY, maximum))
            {
                if (old.IsWall && !old.Damaged) continue;
                var cell = old.Copy(); cell.Owner = owner; cell.IsWall = cell.IsCrenel = true;
                cell.High = WallPolicy.Parity(cell.X, cell.Y); cell.Damaged = false; cell.Structure = false;
                Cells[cell.Id] = cell;
                if (!Heights) continue;
                Managed.Add(cell.Id); ids.Add(cell.Id);
                foreach (WallCell next in Neighbours(cell)) if (Managed.Contains(next.Id)) ids.Add(next.Id);
            }
            Normalize(ids);
        }
        // Mirrors WallRuntime.Normalize: only managed, live, owned crenels; TargetHigh decides, and
        // every low non-middle tile gets a straight-drawing assignment for its current neighbourhood.
        internal int Normalize(IEnumerable<int> ids)
        {
            Func<int, int, WallCell> read = WallPolicy.Reader(this);
            var writes = new List<KeyValuePair<int, bool>>();
            foreach (int id in new SortedSet<int>(ids))
            {
                if (!Managed.Contains(id)) continue;
                if (!Cells.TryGetValue(id, out WallCell cell) || !WallPolicy.Crenel(cell)) { Managed.Remove(id); Assignments.Remove(id); continue; }
                bool? target = WallPolicy.TargetHigh(read, cell);
                bool high = target ?? cell.High;
                int[] around = Cardinal(cell).Select(c => c.Id).OrderBy(i => i).ToArray();
                if (!high && !cell.Damaged && !cell.Structure && !WallPolicy.IsStraightMiddle(WallPolicy.CardinalMask(read, cell.X, cell.Y)))
                    Assignments[id] = around;
                else Assignments.Remove(id);
                if (target.HasValue && target.Value != cell.High) writes.Add(new KeyValuePair<int, bool>(id, target.Value));
            }
            foreach (var write in writes) Cells[write.Key].High = write.Value;
            return writes.Count;
        }
        internal void Remove(int x, int y) { Cells.Remove(y * 800 + x); }
        // Mirrors WallRuntime.AdoptExisting: every unmanaged crenel becomes managed, then normalized.
        internal int AdoptExisting()
        {
            if (!Heights) return 0;
            var ids = new List<int>();
            foreach (WallCell cell in Cells.Values.Where(c => WallPolicy.Crenel(c) && !Managed.Contains(c.Id)).ToArray())
            {
                Managed.Add(cell.Id); ids.Add(cell.Id);
                foreach (WallCell next in Neighbours(cell)) if (Managed.Contains(next.Id)) ids.Add(next.Id);
            }
            Normalize(ids);
            return ids.Count;
        }
        internal IEnumerable<WallCell> Cardinal(WallCell cell)
        {
            foreach (var d in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                if (TryRead(cell.X + d.Item1, cell.Y + d.Item2, out WallCell next) && WallPolicy.Crenel(next)) yield return next;
        }
        // Axis and diagonal crenel neighbours (WallRuntime normalizes both after a build).
        internal IEnumerable<WallCell> Neighbours(WallCell cell)
        {
            foreach (var d in new[] { (0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1) })
                if (TryRead(cell.X + d.Item1, cell.Y + d.Item2, out WallCell next) && WallPolicy.Crenel(next)) yield return next;
        }
        // Renderer: straight middles keep their bit; every other shape is ORed high unless the hook
        // draws it straight (managed, intact, low, assignment for exactly the current neighbours).
        internal void VanillaGraphics()
        {
            foreach (WallCell cell in Cells.Values.ToArray())
            {
                if (cell.Damaged || cell.Structure) continue;
                bool Has(int x, int y) => TryRead(x, y, out WallCell c) && WallPolicy.Crenel(c);
                bool n = Has(cell.X, cell.Y - 1), s = Has(cell.X, cell.Y + 1), e = Has(cell.X + 1, cell.Y), w = Has(cell.X - 1, cell.Y);
                if ((n && s && !e && !w) || (e && w && !n && !s)) continue;
                bool drawnStraight = Managed.Contains(cell.Id) && !cell.High && Assignments.TryGetValue(cell.Id, out int[] around) &&
                    around.SequenceEqual(Cardinal(cell).Select(c => c.Id).OrderBy(i => i));
                if (!drawnStraight) cell.High = true;
            }
        }
        // Render pass, normalization of the redrawn (reported) tiles, render pass: the steady state
        // the game converges to. This model redraws every tile, so every managed tile is reported.
        internal void Settle() { VanillaGraphics(); Normalize(Managed.ToArray()); VanillaGraphics(); }
        internal string State() => string.Join(";", Cells.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value.Owner + ":" + p.Value.High + ":" + Managed.Contains(p.Key)));
        internal bool HighAt(int x, int y) => TryRead(x, y, out WallCell c) && WallPolicy.Crenel(c) && c.High && !c.Structure;
    }
    private static void Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            string sdk = args.Length > 1 ? Path.GetFullPath(args[1]) :
                Environment.GetEnvironmentVariable("SHCDE_API_SHARED_DIR") ?? Path.Combine(root, "APIShared/BepInEx/plugins/APIShared_Serp");
            CSharpCompilation compiled = CompileRuntime(root, sdk);
            ApiCompatibility(compiled, sdk);
            StraightRows(); EnterableShapes(); CheckerboardShapes(); ScopeAndOwnership(); Persistence(); EndpointGraphics(); RandomConstruction();
            OrderIndependence(); ExistingWalls();
            RepairOrigins();
            Console.WriteLine("PASS: " + checks + " assertions; production runtime compilation, checkerboard policy, straight-drawing assignments, redraw stability, scope and MessagePack persistence. Real game/peer execution is not inferred.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    // The settled map depends only on the shapes, never on build order or render timing; without
    // height adjustment nothing is managed and Vanilla's sticky highs remain.
    private static void OrderIndependence()
    {
        var shapes = new List<(int, int)>();
        for (int x = 0; x < 9; x++) shapes.Add((300 + x, 300));             // straight run
        for (int y = 1; y < 6; y++) shapes.Add((308, 300 + y));             // corner and arm
        for (int x = 0; x < 4; x++) shapes.Add((303, 301 + x));             // T junction arm
        for (int i = 0; i < 5; i++) shapes.Add((320 + i, 310 + i));         // diagonal row
        for (int x = 0; x < 4; x++) shapes.Add((330 + x, 320)); shapes.Add((330, 321)); shapes.Add((331, 321)); // 2x2 block
        var reference = new World();
        foreach (var c in shapes) reference.Build(c.Item1, c.Item2, c.Item1, c.Item2);
        reference.Settle();
        CheckEnterable(reference, "Reference shapes");
        var random = new Random(20261124);
        for (int order = 0; order < 60; order++)
        {
            var world = new World();
            foreach (var c in shapes.OrderBy(_ => random.Next()))
            {
                world.Build(c.Item1, c.Item2, c.Item1, c.Item2);
                if (random.Next(3) == 0) world.Settle(); // renders between some placements (sticky high)
            }
            world.Settle();
            CheckEnterable(world, "Order-independent checkerboard");
            Check(world.State() == reference.State(), "Build order changed the settled heights");
            string settled = world.State(); world.Settle(); Check(settled == world.State(), "Redraw changed a settled map");
        }
        // Heights off: nothing is managed, Vanilla's renderer keeps endpoints and lone pieces high.
        var vanilla = new World { Heights = false };
        for (int x = 60; x <= 66; x++) { vanilla.Build(x, 60, x, 60); vanilla.Settle(); }
        Check(vanilla.Managed.Count == 0, "Heights off managed a tile");
        Check(vanilla.Cells.Values.All(c => c.High), "Heights off changed Vanilla sticky highs");
    }
    // Optional alignment of crenels that already exist at session start (designer, save, pre-placed).
    private static void ExistingWalls()
    {
        for (int shift = 0; shift <= 1; shift++)
        {
            var designed = new World();
            for (int x = 0; x < 8; x++) { designed.Put(100 + shift + x, 100); designed.Put(100 + shift + x, 104); }
            for (int y = 1; y < 4; y++) { designed.Put(100 + shift, 100 + y); designed.Put(107 + shift, 100 + y); }
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) designed.Put(120 + shift + x, 100 + y); // block
            for (int i = 0; i < 6; i++) designed.Put(140 + shift + i, 100 + i);                           // diagonal
            designed.Settle();
            Check(designed.Cells.Values.All(c => c.High), "Designer walls are not all high before alignment");
            designed.AdoptExisting(); designed.Settle();
            Check(designed.Managed.Count == designed.Cells.Count, "Not every existing crenel was adopted");
            CheckEnterable(designed, "Aligned existing walls, shift " + shift);
            string once = designed.State(); designed.AdoptExisting(); designed.Settle();
            Check(once == designed.State(), "A second alignment changed the map");
            // Already managed tiles keep their state; heights off adopts nothing.
            var off = new World { Heights = false }; off.Put(10, 10); off.Put(11, 10);
            Check(off.AdoptExisting() == 0 && off.Managed.Count == 0, "Alignment ran with height adjustment off");
        }
    }
    private static void RepairOrigins()
    {
        var host = new World();
        var client = new World();
        WallCell origin = host.Put(100, 100);
        host.Put(101, 100);
        client.Put(100, 100);
        WallCell foreign = client.Put(101, 100);
        host.Managed.Add(origin.Id);
        client.Managed.Add(foreign.Id);
        host.Settle(); client.Settle();
        Check(!host.HighAt(100, 100) && client.HighAt(100, 100),
            "identical walls get different normalized heights when provenance differs");

        byte[] bytes = WallRepairRecord.Capture(400, host.Managed.Select(id => host.Cells[id]), host);
        WallCell Lookup(int id) => client.Cells.TryGetValue(id, out WallCell cell) ? cell : null;
        var replacement = WallRepairRecord.Prepare(bytes, 400, Lookup);
        Check(bytes.Length == WallRepairRecord.MaximumBytes && replacement.Count == 1 && replacement.ContainsKey(origin.Id),
            "bounded record rebuilds only the host's present, owned crenel provenance");
        client.Managed.Clear(); client.Assignments.Clear();
        client.Managed.UnionWith(replacement.Keys);
        host.Settle(); client.Settle();
        Check(client.State() == host.State() && !client.HighAt(100, 100) && client.HighAt(101, 100),
            "restored provenance recovers the enterable endpoint while leaving the other wall alone");
        client.Put(100, 100, owner: 2);
        Check(WallRepairRecord.Prepare(bytes, 400, Lookup).Count == 0,
            "a replaced owner on the repaired tile does not inherit wall provenance");
        client.Remove(100, 100);
        Check(WallRepairRecord.Prepare(bytes, 400, Lookup).Count == 0,
            "a removed wall does not return from provenance alone");
        try { WallRepairRecord.Prepare(bytes, 600, Lookup); throw new Exception("mismatched repair map accepted"); }
        catch (InvalidDataException) { checks++; }
    }

    // Every managed intact crenel sits at its target height (checkerboard, diagonal pieces by x),
    // so no two managed crenels on the same axis are both high and every low one stays low after
    // a render pass (its straight drawing matches the current neighbourhood).
    private static void CheckEnterable(World world, string name)
    {
        CheckManagedParity(world, name);
        foreach (int id in world.Managed)
        {
            WallCell cell = world.Cells[id];
            if (cell.Damaged || cell.Structure) continue;
            foreach (WallCell next in world.Cardinal(cell))
                if (world.Managed.Contains(next.Id) && !next.Damaged && !next.Structure)
                    Check(!(cell.High && next.High), name + ": two adjacent managed highs at " + cell.X + "," + cell.Y);
        }
        string before = world.State(); world.VanillaGraphics(); Check(before == world.State(), name + ": render pass raised a managed low crenel");
    }
    private static void CheckManagedParity(World world, string name)
    {
        Func<int, int, WallCell> read = WallPolicy.Reader(world);
        foreach (int id in world.Managed)
        {
            WallCell cell = world.Cells[id]; bool? target = WallPolicy.TargetHigh(read, cell);
            Check(!target.HasValue || target.Value == cell.High, name + " at " + cell.X + "," + cell.Y);
        }
    }
    private static void StraightRows()
    {
        foreach (var dir in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) for (int start = 200; start <= 201; start++) for (int length = 2; length <= 40; length++)
        {
            var sequential = new World(); var dragged = new World();
            for (int i = 0; i < length; i++)
            {
                int x = start + dir.Item1 * i, y = 200 + dir.Item2 * i;
                sequential.Build(x, y, x, y);
                sequential.Settle();
            }
            dragged.Build(start, 200, start + dir.Item1 * (length - 1), 200 + dir.Item2 * (length - 1));
            dragged.Settle();
            foreach (World world in new[] { sequential, dragged })
            {
                Check(world.Cells.Count == length, "Placement count changed");
                // The exploit: piece-by-piece endpoints stayed high. Now every piece is checkerboard.
                foreach (WallCell cell in world.Cells.Values)
                    Check(cell.High == WallPolicy.Parity(cell.X, cell.Y), "Straight row is not Vanilla checkerboard at " + cell.X + "," + cell.Y);
                string before = world.State(); world.Settle(); Check(before == world.State(), "Redraw changed a settled row");
            }
            Check(sequential.State() == dragged.State(), "Single placement and dragging differ");
        }
        // An isolated crenel follows the checkerboard as well (drawn straight when low).
        foreach (int x in new[] { 30, 31 })
        {
            var single = new World(); single.Build(x, 30, x, 30); single.Settle();
            Check(single.Cells.Values.Single().High == WallPolicy.Parity(x, 30), "An isolated crenel is not at parity, x=" + x);
        }
        var pair = new World(); pair.Build(30, 30, 30, 30); pair.Settle(); pair.Build(31, 30, 31, 30); pair.Settle();
        Check(!pair.Cells[30 * 800 + 30].High && pair.Cells[30 * 800 + 31].High, "First piece did not stay at parity");
        var reversedA = new World(); var reversedB = new World();
        reversedA.Build(50, 50, 100, 50); reversedB.Build(100, 50, 50, 50);
        Check(reversedA.State() == reversedB.State(), "Reverse drag changed deterministic result");
        var connect = new World(); connect.Build(50, 100, 54, 100); connect.Build(58, 100, 62, 100);
        connect.Build(55, 100, 57, 100); connect.Settle();
        CheckEnterable(connect, "Joined row");
        var limited = new World(); var raster = WallPolicy.Raster(limited, 20, 20, 30, 25, 3);
        Check(raster.Count == 3 && raster[2].X == 22 && raster[2].Y == 22, "Native simultaneous XY step/max prefix changed");
        Check(WallPolicy.Raster(limited, 20, 20, 30, 20, 0).Count == 0, "Zero maximum has placements");
        bool invalid = false; try { WallPolicy.Raster(limited, -1, 30, 2, 30, 20); } catch (InvalidOperationException) { invalid = true; }
        Check(invalid, "Out-of-bounds raster not rejected");
    }
    // Corners, junctions, crossings, double rows, blocks and stairs all follow the map checkerboard,
    // in any build order, and their low tiles stay low through render passes.
    private static void CheckerboardShapes()
    {
        for (int shift = 0; shift <= 1; shift++) for (int width = 4; width <= 9; width++)
        {
            int x0 = 100 + shift, y0 = 100, x1 = x0 + width - 1, y1 = y0 + 2;
            var frame = new World();
            frame.Build(x0, y0, x1, y0); frame.Build(x0, y1, x1, y1);
            frame.Build(x0, y0 + 1, x0, y0 + 1); frame.Build(x1, y0 + 1, x1, y0 + 1);
            frame.Settle();
            CheckEnterable(frame, "Frame width " + width + " shift " + shift);
            foreach (WallCell cell in frame.Cells.Values)
                Check(cell.High == WallPolicy.Parity(cell.X, cell.Y), "Frame tile (incl. corner) not at parity: " + cell.X + "," + cell.Y);
            var shuffled = new World(); var random = new Random(width * 7 + shift);
            var cells = new List<(int, int)>();
            for (int x = x0; x <= x1; x++) { cells.Add((x, y0)); cells.Add((x, y1)); }
            cells.Add((x0, y0 + 1)); cells.Add((x1, y0 + 1));
            foreach (var c in cells.OrderBy(_ => random.Next())) { shuffled.Build(c.Item1, c.Item2, c.Item1, c.Item2); if (random.Next(2) == 0) shuffled.Settle(); }
            shuffled.Settle();
            Check(shuffled.State() == frame.State(), "Frame heights depend on build order, width " + width + " shift " + shift);
        }
        for (int shift = 0; shift <= 1; shift++)
        {
            var doubleRow = new World(); doubleRow.Build(40 + shift, 40, 45 + shift, 40); doubleRow.Build(40 + shift, 41, 45 + shift, 41);
            var block = new World(); for (int y = 60; y < 65; y++) block.Build(60 + shift, y, 64 + shift, y);
            var stairs = new World();
            foreach (var c in new[] { (30, 30), (31, 30), (31, 31), (32, 31), (32, 32), (33, 32) }) stairs.Build(c.Item1 + shift, c.Item2, c.Item1 + shift, c.Item2);
            var wide = new World();
            for (int step = 0, x = 80 + shift, y = 80; step < 6; step++, x += 2, y++) { wide.Build(x, y, x + 1, y); wide.Build(x + 1, y + 1, x + 1, y + 1); }
            var u = new World(); u.Build(12 + shift, 10, 12 + shift, 11); u.Build(13 + shift, 11, 14 + shift, 11); u.Build(14 + shift, 10, 14 + shift, 10);
            foreach (World world in new[] { doubleRow, block, stairs, wide, u })
            {
                world.Settle();
                CheckEnterable(world, "Former blockade shape, shift " + shift);
                foreach (WallCell cell in world.Cells.Values)
                    if (world.Cardinal(cell).Any()) Check(cell.High == WallPolicy.Parity(cell.X, cell.Y), "Shape tile not at parity: " + cell.X + "," + cell.Y);
            }
        }
        // A corner added to the end of a free row changes nothing along the row; removing it neither.
        var row = new World(); row.Build(200, 200, 211, 200); row.Settle(); string straight = row.State();
        row.Build(211, 201, 211, 201); row.Settle(); CheckEnterable(row, "Row with new corner");
        foreach (WallCell cell in row.Cells.Values) Check(cell.High == WallPolicy.Parity(cell.X, cell.Y), "Corner changed the row at " + cell.X);
        row.Remove(211, 201); row.Normalize(new[] { 200 * 800 + 211 }); row.Settle();
        Check(row.State() == straight, "Removing the corner did not restore the row");
    }
    private static void EnterableShapes()
    {
        for (int shift = 0; shift <= 1; shift++)
        {
            var longL = new World(); longL.Build(20 + shift, 20, 24 + shift, 20); longL.Build(24 + shift, 21, 24 + shift, 24);
            var branch = new World(); branch.Build(50 + shift, 50, 60 + shift, 50); branch.Build(55 + shift, 51, 55 + shift, 55);
            var cross = new World(); cross.Build(80 + shift, 50, 90 + shift, 50); cross.Build(85 + shift, 51, 85 + shift, 55); cross.Build(85 + shift, 49, 85 + shift, 45);
            var ring = new World(); ring.Build(70 + shift, 70, 76 + shift, 70); ring.Build(76 + shift, 71, 76 + shift, 76); ring.Build(75 + shift, 76, 70 + shift, 76);
            ring.Build(70 + shift, 75, 70 + shift, 71);
            foreach (World world in new[] { longL, branch, cross, ring }) { world.Settle(); CheckEnterable(world, "Normal shape, shift " + shift); }
        }
        DiagonalRows();
        var gate = new World(); for (int x = 50; x <= 56; x++) gate.Build(x, 60, x, 60);
        gate.TryRead(53, 60, out WallCell tower); tower.Structure = true; bool before = tower.High;
        gate.Normalize(gate.Managed.ToArray()); Check(gate.Cells[tower.Id].High == before, "Building footprint was modified");
    }
    // Screen-horizontal walls are tile diagonals. Map parity would give a whole diagonal one height;
    // managed pieces without axis neighbours alternate along x, dragged or piece by piece.
    private static void DiagonalRows()
    {
        foreach (var dir in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }) for (int start = 200; start <= 201; start++) for (int length = 2; length <= 12; length++)
        {
            var dragged = new World(); var sequential = new World();
            int ex = start + dir.Item1 * (length - 1), ey = 200 + dir.Item2 * (length - 1);
            dragged.Build(start, 200, ex, ey);
            dragged.Settle();
            for (int i = 0; i < length; i++)
            {
                int x = start + dir.Item1 * i, y = 200 + dir.Item2 * i;
                sequential.Build(x, y, x, y);
                sequential.Settle();
            }
            foreach (World world in new[] { dragged, sequential })
            {
                Check(world.Cells.Count == length, "Diagonal placement count changed");
                foreach (WallCell cell in world.Cells.Values)
                    Check(cell.High == WallPolicy.DiagonalHigh(cell.X), "Diagonal row does not alternate at " + cell.X + "," + cell.Y);
                string before = world.State(); world.Settle(); Check(before == world.State(), "Redraw changed a settled diagonal row");
            }
            Check(dragged.State() == sequential.State(), "Diagonal drag and single placement differ");
        }
        // X: centre and four diagonal pieces, none with an axis neighbour; the centre differs from them.
        var x5 = new World(); foreach (var c in new[] { (50, 50), (49, 49), (51, 49), (49, 51), (51, 51) }) x5.Build(c.Item1, c.Item2, c.Item1, c.Item2);
        x5.Settle(); CheckEnterable(x5, "X shape");
        Check(x5.HighAt(50, 50) != x5.HighAt(49, 49), "X centre has the height of its diagonal pieces");
        // Removing the diagonal neighbour leaves an isolated piece, which follows the checkerboard.
        var lone = new World(); lone.Build(40, 40, 41, 41); lone.Settle(); lone.Remove(41, 41); lone.Settle();
        Check(lone.Cells[40 * 800 + 40].High == WallPolicy.Parity(40, 40), "A lone former diagonal piece is not at parity");
        // A designer's unmanaged diagonal stays untouched when a managed piece joins it.
        var designer = new World(); designer.Put(60, 60); designer.Put(61, 61);
        designer.Build(62, 62, 62, 62); designer.Settle();
        Check(designer.Cells[60 * 800 + 60].High && designer.Cells[61 * 800 + 61].High, "Unmanaged diagonal pieces changed");
        Check(designer.Cells[62 * 800 + 62].High == WallPolicy.DiagonalHigh(62), "New diagonal piece not alternating");
    }
    private static void ScopeAndOwnership()
    {
        // Map-designer and pre-mod walls keep their loaded height; only new pieces are managed.
        var designed = new World(); for (int x = 100; x <= 105; x++) designed.Put(x, 100, 1, true);
        string designedBefore = string.Join(",", designed.Cells.Values.Select(c => c.High));
        designed.Build(106, 100, 106, 100);
        designed.Settle();
        Check(designedBefore == string.Join(",", designed.Cells.Values.Where(c => c.X <= 105).Select(c => c.High)), "Unmanaged designer wall was rewritten");
        Check(designed.Managed.SetEquals(new[] { 100 * 800 + 106 }), "Only the new piece may become managed");
        var foreign = new World(); foreign.Put(52, 100, 2, false); foreign.Put(53, 100, 2, true);
        foreign.Build(50, 100, 51, 100); foreign.Settle();
        Check(foreign.Cells[100 * 800 + 52].Owner == 2 && !foreign.Managed.Contains(100 * 800 + 52) && !foreign.Managed.Contains(100 * 800 + 53), "Foreign walls became managed");
        var damaged = new World(); damaged.Build(30, 130, 36, 130); damaged.Settle();
        WallCell broken = damaged.Cells[130 * 800 + 33]; broken.Damaged = true; broken.High = !broken.High; bool brokenHigh = broken.High;
        damaged.Normalize(damaged.Managed.ToArray());
        Check(damaged.Cells[broken.Id].High == brokenHigh, "Damaged tile was normalized");
        // Demolition: survivors next to the gap must return to parity, never stay sticky high.
        var demolished = new World(); demolished.Build(50, 150, 60, 150); demolished.Settle();
        demolished.Remove(55, 150); demolished.Settle();
        CheckEnterable(demolished, "Demolition survivors");
        Check(!demolished.Managed.Contains(150 * 800 + 55), "Demolished tile stayed managed");
        foreach (int x in new[] { 54, 56 }) Check(demolished.Cells[150 * 800 + x].High == WallPolicy.Parity(x, 150), "Survivor endpoint not at parity");
        // Removing an arm turns a managed corner into an endpoint; it keeps its checkerboard height.
        var corner = new World(); corner.Build(20, 20, 24, 20); corner.Build(24, 21, 24, 24); corner.Settle();
        for (int y = 21; y <= 24; y++) corner.Remove(24, y);
        corner.Settle(); CheckEnterable(corner, "Former corner");
        var host = new World(); var client = new World();
        foreach (var command in new[] { (100, 180, 120, 180), (115, 181, 115, 190), (101, 181, 119, 181), (121, 180, 121, 180), (116, 190, 120, 190) })
        {
            host.Build(command.Item1, command.Item2, command.Item3, command.Item4); client.Build(command.Item1, command.Item2, command.Item3, command.Item4);
            host.Settle(); client.Settle();
            Check(host.State() == client.State(), "Two-peer deterministic fixture diverged");
        }
    }
    private static void Persistence()
    {
        var w = new World(); w.Build(80, 220, 100, 220); w.Settle();
        int[][] data = SaveState.Capture(400, w.Managed.Select(id => w.Cells[id]));
        byte[] bytes = MessagePackSerializer.Serialize(data);
        List<WallCell> restored = SaveState.Validate(MessagePackSerializer.Deserialize<int[][]>(bytes), 400, w);
        Check(restored.Select(c => c.Id).OrderBy(i => i).SequenceEqual(w.Managed.OrderBy(i => i)), "Actual MessagePack round trip changed the managed set");
        Check(data.Skip(1).All(r => r.Length == 4), "Heights must not be persisted");
        int id = restored[3].Id; w.Cells[id].Owner = 2;
        Check(!SaveState.Validate(data, 400, w).Any(c => c.Id == id), "Transferred saved cell resurrected");
        w.Cells[id].Owner = 1; w.Remove(w.Cells[id].X, w.Cells[id].Y);
        Check(!SaveState.Validate(data, 400, w).Any(c => c.Id == id), "Removed saved cell resurrected");
        var empty = SaveState.Capture(400, Array.Empty<WallCell>()); Check(SaveState.Validate(empty, 400, w).Count == 0, "Disabled/empty header failed");
        bool bad = false; try { SaveState.Validate(data, 500, w); } catch (InvalidOperationException) { bad = true; } Check(bad, "Wrong map size accepted");
        int[][] duplicate = data.Select(r => r.ToArray()).ToArray(); duplicate[2][0] = duplicate[1][0];
        bad = false; try { SaveState.Validate(duplicate, 400, w); } catch (InvalidOperationException) { bad = true; } Check(bad, "Duplicate saved id accepted");
        int[][] schemaOne = data.Select(r => r.ToArray()).ToArray(); schemaOne[0][0] = 1;
        bad = false; try { SaveState.Validate(schemaOne, 400, w); } catch (InvalidOperationException) { bad = true; } Check(bad, "Old schema accepted");
        for (int player = 1; player <= 8; player++)
        {
            var editor = new World(); editor.Build(100, 240, 109, 240, player);
            editor.Put(110, 240, player, true); // Designer-placed vanilla tile next to the managed run.
            int[][] mapData = MessagePackSerializer.Deserialize<int[][]>(MessagePackSerializer.Serialize(SaveState.Capture(400, editor.Managed.Select(i => editor.Cells[i]))));
            List<WallCell> mapRestored = SaveState.Validate(mapData, 400, editor);
            Check(mapRestored.Count == 10 && mapRestored.All(c => c.Owner == player), "Manual editor metadata changed an owner");
            Check(!mapRestored.Any(c => c.X == 110), "Unmanaged designer tile entered the managed set");
        }
    }
    private static void RandomConstruction()
    {
        // Build-only sequences: every state keeps all managed walls at their target and enterable.
        var random = new Random(20261122);
        for (int fixture = 0; fixture < 40; fixture++)
        {
            var map = new World();
            for (int step = 0; step < 120; step++)
            {
                int x = 200 + random.Next(14), y = 220 + random.Next(14), length = random.Next(4);
                bool vertical = random.Next(2) == 0;
                map.Build(x, y, vertical ? x : x + length, vertical ? y + length : y);
                map.Settle();
                CheckEnterable(map, "Random construction");
            }
        }
        // With demolition the managed pieces still always return to their target.
        for (int fixture = 0; fixture < 20; fixture++)
        {
            var map = new World();
            for (int step = 0; step < 120; step++)
            {
                if (step % 5 == 0 && map.Cells.Count != 0)
                {
                    WallCell victim = map.Cells.Values.OrderBy(c => c.Id).ElementAt(random.Next(map.Cells.Count));
                    map.Remove(victim.X, victim.Y);
                }
                int x = 300 + random.Next(12), y = 220 + random.Next(12);
                map.Build(x, y, x + random.Next(3), y);
                map.Settle();
                CheckEnterable(map, "Demolition fuzz");
            }
        }
    }
    private static CSharpCompilation CompileRuntime(string root, string sdk)
    {
        // Type-check every production source against the actual installed dependencies and
        // .NET Framework reference assemblies without emitting/building the runtime DLL.
        string mod = Path.Combine(root, "Testmods", "CrenelatedWallTest");
        string game = "E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition";
        string framework = "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1";
        XDocument project = XDocument.Load(Path.Combine(mod, "CrenelatedWallTest.csproj")); XNamespace ns = project.Root.Name.Namespace;
        var trees = project.Descendants(ns + "Compile").Select(e => Path.GetFullPath(Path.Combine(mod, (string)e.Attribute("Include"))))
            .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)).ToList();
        var paths = new HashSet<string>(Directory.GetFiles(framework, "*.dll", SearchOption.AllDirectories).Where(p => {
            using var stream = File.OpenRead(p); using var pe = new PEReader(stream);
            return pe.HasMetadata && pe.GetMetadataReader().IsAssembly;
        }), StringComparer.OrdinalIgnoreCase);
        foreach (XElement reference in project.Descendants(ns + "Reference"))
        {
            string hint = (string)reference.Element(ns + "HintPath"); if (hint == null) continue;
            hint = hint.Replace("$(GameDir)", game).Replace("$(ExtenderDir)", game + "/BepInEx/plugins/000shcdese")
                .Replace("$(ApiSharedDir)", sdk);
            Check(File.Exists(hint), "Missing installed reference: " + hint); paths.Add(Path.GetFullPath(hint));
        }
        var compilation = CSharpCompilation.Create("CrenelatedWallTest.Preflight", trees,
            paths.Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        string[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
        Check(errors.Length == 0, "Production API/signature compilation failed:\n" + string.Join("\n", errors));
        var publicNames = new HashSet<string>();
        for (INamedTypeSymbol type = compilation.GetTypeByMetadataName("CrenelatedWallTest.WallSettings"); type != null; type = type.BaseType)
            foreach (ISymbol member in type.GetMembers()) if (member.DeclaredAccessibility == Accessibility.Public) publicNames.Add(member.Name);
        string xaml = File.ReadAllText(Path.Combine(mod, "Override/ScriptExtenderUI/CrenelatedWallTestSettings.xaml"));
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(xaml, @"\{Binding ([A-Za-z_][A-Za-z0-9_]*)"))
            Check(publicNames.Contains(match.Groups[1].Value), "XAML references missing public binding: " + match.Groups[1].Value);
        Console.WriteLine("PASS: all production sources and XAML bindings type-checked against true Assembly-CSharp, installed SE and selected APIShared SDK=" + sdk + "; no runtime DLL emitted.");
        return compilation;
    }
    private static void ApiCompatibility(CSharpCompilation production, string sdk)
    {
        // Regression: SDK identity/additive services must not invalidate a consumer;
        // loss of an actually used public member must. Only synthetic copies change.
        string api = Path.GetFullPath(Path.Combine(sdk, "APIShared.dll"));
        var reference = production.References.Single(r => string.Equals(r.Display, api, StringComparison.OrdinalIgnoreCase));
        string directory = TestWorkDirectory.New("crenel-api");
        using var resolver = new Mono.Cecil.DefaultAssemblyResolver();
        foreach (string path in new[] { sdk,
            "E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/core",
            "E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/000shcdese",
            "E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/Stronghold Crusader Definitive Edition_Data/Managed",
            "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1" }) resolver.AddSearchDirectory(path);
        var options = new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false };
        string compatible = Path.Combine(directory, "compatible.dll"), broken = Path.Combine(directory, "broken.dll");
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(api, options))
        {
            assembly.Name.Version = new Version(assembly.Name.Version.Major + 1, 0, 0, 0);
            var unrelated = new Mono.Cecil.TypeDefinition("CompatibilityFixture", "AdditionalService",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, assembly.MainModule.TypeSystem.Object);
            unrelated.Fields.Add(new Mono.Cecil.FieldDefinition("AdditionalOption", Mono.Cecil.FieldAttributes.Public, assembly.MainModule.TypeSystem.Int32));
            assembly.MainModule.Types.Add(unrelated);
            assembly.Write(compatible);
        }
        Diagnostic[] DiagnosticsFor(string path) => production.ReplaceReference(reference, MetadataReference.CreateFromFile(path))
            .GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Check(DiagnosticsFor(compatible).Length == 0, "An APIShared version change/additive unrelated service blocked unchanged required APIs");
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(api, options))
        {
            var method = assembly.MainModule.GetType("APIShared.ModApiClient").Methods.Single(m => m.Name == "TryGetMissionLifecycle" && m.IsPublic &&
                m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "APIShared.IMissionLifecycleCapability&" &&
                m.Parameters[1].ParameterType.FullName == "APIShared.NativeCapabilityDiagnostic&");
            method.IsPublic = false; method.IsPrivate = true;
            assembly.Write(broken);
        }
        Diagnostic[] rejected = DiagnosticsFor(broken);
        Check(rejected.Any(d => d.GetMessage().Contains("TryGetMissionLifecycle")), "Loss of the required public mission capability was not detected");
        Console.WriteLine("PASS: synthetic SDK version/additive API accepted; missing required mission API rejected. Actual selected SDK remains untouched; fixtures=" + directory);
    }
    private static void EndpointGraphics()
    {
        var logic = new int[1000]; var deltas = new int[800 * 8]; var damage = new byte[1000];
        var structure = new ushort[1000]; var owner = new byte[1000];
        int tile = 500, row = 20;
        int[] steps = { -40, -39, 1, 41, 40, 39, -1, -41 };
        Array.Copy(steps, 0, deltas, row * 8, 8);
        // Assignment for exactly the given native directions (0/2/4/6) as axis neighbours.
        EndpointAssignment Assign(int player, params int[] directions)
        {
            Array.Clear(logic); logic[tile] = 0x300; owner[tile] = (byte)(0xA0 | (player - 1));
            foreach (int d in directions) logic[tile + steps[d]] = 0x300;
            return new EndpointAssignment(player, row, directions.Select(d => tile + steps[d]).ToArray(), directions.Length);
        }
        bool Matches(EndpointAssignment a, int direction) => EndpointGraphicsPolicy.Matches(a, tile, row, direction, logic, deltas, damage, structure, owner);
        foreach (int player in Enumerable.Range(1, 8)) foreach (int d in new[] { 0, 2, 4, 6 })
        {
            // Endpoint: straight along its neighbour's axis, never across it.
            var assignment = Assign(player, d);
            Check(Matches(assignment, d) && Matches(assignment, (d + 4) & 7), "Low endpoint axis or owner decoding failed");
            Check(!Matches(assignment, (d + 2) & 7) && !Matches(assignment, 1), "Transverse/invalid graphics axis accepted");
            logic[tile] |= 0x400000; Check(!Matches(assignment, d), "High endpoint graphics were replaced"); logic[tile] &= ~0x400000;
            damage[tile] = 1; Check(!Matches(assignment, d), "Damaged graphics were replaced"); damage[tile] = 0;
            structure[tile] = 3; Check(!Matches(assignment, d), "Building graphics were replaced"); structure[tile] = 0;
            owner[tile] ^= 1; Check(!Matches(assignment, d), "Transferred endpoint was rendered from stale metadata"); owner[tile] ^= 1;
            logic[tile + steps[(d + 2) & 7]] = 0x300; Check(!Matches(assignment, d), "A new corner reused old endpoint graphics");
            logic[tile + steps[(d + 2) & 7]] = 0;
            logic[tile + steps[d]] = 0; Check(!Matches(assignment, d), "An isolated tile reused old endpoint graphics");
            Check(!EndpointGraphicsPolicy.Matches(assignment, -1, row, d, logic, deltas, damage, structure, owner), "Negative id accepted");
            Check(!EndpointGraphicsPolicy.Matches(assignment, tile, -1, d, logic, deltas, damage, structure, owner), "Wrong row accepted");
            // Corner (two perpendicular neighbours): tie, drawn along the axis of direction 0 only.
            var corner = Assign(player, d, (d + 2) & 7);
            Check(Matches(corner, 0) && Matches(corner, 4) && !Matches(corner, 2) && !Matches(corner, 6), "Corner not drawn along direction 0");
            logic[tile + steps[(d + 4) & 7]] = 0x300; Check(!Matches(corner, 0), "Corner assignment reused for a junction");
            // T junction (bar along d, stem across): straight along the bar.
            var junction = Assign(player, d, (d + 4) & 7, (d + 2) & 7);
            Check(Matches(junction, d) && !Matches(junction, (d + 2) & 7), "Junction not drawn along its bar");
            logic[tile + steps[(d + 2) & 7]] = 0; Check(!Matches(junction, d), "Junction assignment reused for a straight middle");
        }
        // Cross / block inside (four neighbours): tie, direction 0.
        var full = Assign(1, 0, 2, 4, 6);
        Check(Matches(full, 0) && !Matches(full, 2), "Cross not drawn along direction 0");
        Check(!EndpointGraphicsPolicy.Matches(null, tile, row, 0, logic, deltas, damage, structure, owner), "Untracked tile overridden");
        // Low diagonal or isolated piece: no axis neighbour, answered only for the renderer's first query.
        var piece = Assign(1);
        logic[tile + steps[3]] = 0x300; // diagonal neighbour does not count
        Check(Matches(piece, 0), "Low diagonal piece not drawn straight");
        Check(!Matches(piece, 2), "Diagonal piece answered a second axis");
        logic[tile] |= 0x400000; Check(!Matches(piece, 0), "High diagonal piece replaced");
        logic[tile] &= ~0x400000; logic[tile + steps[0]] = 0x300;
        Check(!Matches(piece, 0), "Diagonal assignment reused after an axis neighbour appeared");
        bool invalid = false; try { new EndpointAssignment(1, row, new int[2], 3); } catch (ArgumentOutOfRangeException) { invalid = true; }
        Check(invalid, "Assignment accepted more neighbours than given");
    }
}
