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
    // production policy; VanillaGraphics models renderer 65830 with the endpoint hook.
    private sealed class World : IWallWorld
    {
        internal readonly Dictionary<int, WallCell> Cells = new Dictionary<int, WallCell>();
        internal readonly HashSet<int> Managed = new HashSet<int>();
        internal Func<int, bool> IsManaged => id => Managed.Contains(id);
        // Frozen session switches of WallRuntime: height adjustment and placement blockade.
        internal bool Heights = true, Block = true;
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
        internal WallVerdict Build(int x, int y, int endX, int endY, int owner = 1, int maximum = 800)
        {
            List<WallCell> raster = WallPolicy.Raster(this, x, y, endX, endY, maximum);
            WallVerdict verdict = Block ? WallAdmission.Evaluate(this, owner, raster, IsManaged, Heights) : new WallVerdict();
            if (!verdict.Accepted) return verdict;
            var ids = new List<int>();
            foreach (WallCell cell in WallPolicy.Predict(raster, owner))
            {
                Cells[cell.Id] = cell;
                if (!Heights) continue;
                Managed.Add(cell.Id); ids.Add(cell.Id);
                foreach (WallCell next in Neighbours(cell)) if (Managed.Contains(next.Id)) ids.Add(next.Id);
            }
            Normalize(ids);
            return verdict;
        }
        // Mirrors WallRuntime.Normalize: only managed, live, owned crenels; TargetHigh decides.
        internal int Normalize(IEnumerable<int> ids)
        {
            Func<int, int, WallCell> read = WallPolicy.Reader(this);
            var writes = new List<KeyValuePair<int, bool>>();
            foreach (int id in ids.Distinct().OrderBy(i => i).ToArray())
            {
                if (!Managed.Contains(id)) continue;
                if (!Cells.TryGetValue(id, out WallCell cell) || !WallPolicy.Crenel(cell)) { Managed.Remove(id); continue; }
                bool? target = WallPolicy.TargetHigh(read, cell);
                if (target.HasValue && target.Value != cell.High) writes.Add(new KeyValuePair<int, bool>(id, target.Value));
            }
            foreach (var write in writes) Cells[write.Key].High = write.Value;
            return writes.Count;
        }
        internal void Remove(int x, int y) { Cells.Remove(y * 800 + x); }
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
        internal void VanillaGraphics()
        {
            foreach (WallCell cell in Cells.Values.ToArray())
            {
                if (cell.Damaged || cell.Structure) continue;
                bool Has(int x, int y) => TryRead(x, y, out WallCell c) && WallPolicy.Crenel(c);
                bool n = Has(cell.X, cell.Y - 1), s = Has(cell.X, cell.Y + 1), e = Has(cell.X + 1, cell.Y), w = Has(cell.X - 1, cell.Y);
                int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);
                // StraightGraphicsHook: published = managed, intact, low, exactly one axis neighbour,
                // or no axis but a diagonal neighbour (diagonal row piece).
                bool diagonal = count == 0 && WallPolicy.DiagonalMask(WallPolicy.Reader(this), cell.X, cell.Y) != 0;
                bool publishedEndpoint = Managed.Contains(cell.Id) && !cell.High && (count == 1 || diagonal);
                if (!publishedEndpoint && !((n && s && !e && !w) || (e && w && !n && !s))) cell.High = true;
            }
        }
        // Render pass, tick normalization, render pass: the steady state the game converges to.
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
            StraightRows(); EnterableShapes(); ForbiddenShapes(); ScopeAndOwnership(); Persistence(); EndpointGraphics(); RandomConstruction(); PreviewAdmission();
            SplitSwitches();
            RepairOrigins();
            Console.WriteLine("PASS: " + checks + " assertions; production runtime compilation, parity policy, enterability rules, redraw stability, scope and MessagePack persistence. Real game/peer execution is not inferred.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    // The two parts are switched separately. Heights without blockade must alternate whatever
    // the build order; blockade without heights must predict pure Vanilla heights.
    private static void SplitSwitches()
    {
        // Heights on, blockade off: forbidden shapes are built, every managed line piece still
        // follows its target and the settled map does not depend on the build order.
        var doubled = new World { Block = false };
        Check(doubled.Build(40, 40, 45, 40).Accepted && doubled.Build(40, 41, 45, 41).Accepted, "Blockade off still rejected a double row");
        doubled.Settle(); CheckManagedParity(doubled, "Double row parity without blockade");
        var shapes = new List<(int, int)>();
        for (int x = 0; x < 9; x++) shapes.Add((300 + x, 300));             // straight run
        for (int y = 1; y < 6; y++) shapes.Add((308, 300 + y));             // corner and arm
        for (int x = 0; x < 4; x++) shapes.Add((303, 301 + x));             // T junction arm
        for (int i = 0; i < 5; i++) shapes.Add((320 + i, 310 + i));         // diagonal row
        for (int x = 0; x < 4; x++) shapes.Add((330 + x, 320)); shapes.Add((330, 321)); shapes.Add((331, 321)); // 2x2 block
        var reference = new World { Block = false };
        foreach (var c in shapes) reference.Build(c.Item1, c.Item2, c.Item1, c.Item2);
        reference.Settle();
        var random = new Random(20261124);
        for (int order = 0; order < 60; order++)
        {
            var world = new World { Block = false };
            foreach (var c in shapes.OrderBy(_ => random.Next()))
            {
                Check(world.Build(c.Item1, c.Item2, c.Item1, c.Item2).Accepted, "Blockade off rejected a piece");
                if (random.Next(3) == 0) world.Settle(); // renders between some placements (sticky high)
            }
            world.Settle();
            CheckManagedParity(world, "Order-independent parity");
            Check(world.State() == reference.State(), "Build order changed the settled heights");
            string settled = world.State(); world.Settle(); Check(settled == world.State(), "Redraw changed a settled map");
        }
        foreach (WallCell cell in reference.Cells.Values.Where(c => c.Y == 300 && c.X > 300 && c.X < 308 && c.X != 303))
            Check(cell.High == WallPolicy.Parity(cell.X, cell.Y), "Straight middle not alternating at " + cell.X);
        // Heights off: nothing is managed, Vanilla's renderer keeps endpoints and lone pieces high.
        var vanilla = new World { Heights = false, Block = false };
        for (int x = 60; x <= 66; x++) { Check(vanilla.Build(x, 60, x, 60).Accepted, "Heights off rejected a straight piece"); vanilla.Settle(); }
        Check(vanilla.Managed.Count == 0, "Heights off managed a tile");
        Check(vanilla.Cells.Values.All(c => c.High), "Heights off changed Vanilla sticky highs");
        // Blockade alone therefore stops a piece-by-piece row at the fourth sticky-high piece.
        var vanillaBlocked = new World { Heights = false };
        for (int x = 60; x <= 62; x++) { Check(vanillaBlocked.Build(x, 60, x, 60).Accepted, "Blockade alone rejected a short row"); vanillaBlocked.Settle(); }
        WallVerdict fourth = vanillaBlocked.Build(63, 60, 63, 60);
        Check(!fourth.Accepted && fourth.Rejection.Contains("four high"), "Blockade alone accepted four sticky highs");
        // Blockade with Vanilla prediction: endpoints/lone pieces count as high, middles as parity.
        var predict = new World(); predict.Put(70, 70, 1, false); predict.Put(71, 70, 1, false);
        Func<int, int, WallCell> read = WallPolicy.Reader(predict);
        var none = new HashSet<int>();
        Check(WallPolicy.PredictedHigh(read, predict.Cells[70 * 800 + 70], none, predict.IsManaged, false), "Vanilla prediction lowered an endpoint");
        predict.Put(72, 70, 1, false); read = WallPolicy.Reader(predict);
        Check(!WallPolicy.PredictedHigh(read, predict.Cells[70 * 800 + 71], none, predict.IsManaged, false), "Vanilla prediction raised a low straight middle");
        var newMiddle = new HashSet<int> { 70 * 800 + 71 };
        Check(WallPolicy.PredictedHigh(read, predict.Cells[70 * 800 + 71], newMiddle, predict.IsManaged, false) == WallPolicy.Parity(71, 70), "New Vanilla middle not at placement parity");
        // A command accepted with height adjustment can be rejected with Vanilla heights.
        bool differs = false;
        for (int shift = 0; shift <= 1 && !differs; shift++)
        {
            var a = new World(); var b = new World { Heights = false };
            foreach (World w in new[] { a, b }) { w.Build(100 + shift, 100, 100 + shift, 100); w.Build(100 + shift, 101, 100 + shift, 101); w.Settle(); }
            differs = a.Build(101 + shift, 101, 102 + shift, 101).Accepted && !b.Build(101 + shift, 101, 102 + shift, 101).Accepted;
        }
        Check(differs, "Vanilla height prediction never changed a blockade decision");
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
        var empty = new HashSet<int>();
        Check(!WallPolicy.PredictedHigh(WallPolicy.Reader(host), origin, empty, host.IsManaged) &&
            WallPolicy.PredictedHigh(WallPolicy.Reader(client), client.Cells[origin.Id], empty, client.IsManaged),
            "identical high walls can have different next normalization/admission heights when provenance differs");

        byte[] bytes = WallRepairRecord.Capture(400, host.Managed.Select(id => host.Cells[id]), host);
        WallCell Lookup(int id) => client.Cells.TryGetValue(id, out WallCell cell) ? cell : null;
        var replacement = WallRepairRecord.Prepare(bytes, 400, Lookup);
        Check(bytes.Length == WallRepairRecord.MaximumBytes && replacement.Count == 1 && replacement.ContainsKey(origin.Id),
            "bounded record rebuilds only the host's present, owned crenel provenance");
        client.Managed.Clear();
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

    // Longest cardinal simple path of high crenels (all high tiles are unwalkable natively).
    private static int LongestHighRun(World world)
    {
        int best = 0;
        void Walk(WallCell cell, HashSet<int> path)
        {
            best = Math.Max(best, path.Count); if (best >= 4) return;
            foreach (WallCell next in world.Cardinal(cell))
                if (next.High && !next.Structure && path.Add(next.Id)) { Walk(next, path); path.Remove(next.Id); }
        }
        foreach (WallCell cell in world.Cells.Values.Where(c => WallPolicy.Crenel(c) && c.High && !c.Structure).ToArray())
            Walk(cell, new HashSet<int> { cell.Id });
        return best;
    }
    private static bool Has2x2(World world) => world.Cells.Values.Any(c => WallPolicy.Crenel(c) &&
        new[] { (1, 0), (0, 1), (1, 1) }.All(d => world.TryRead(c.X + d.Item1, c.Y + d.Item2, out WallCell o) && WallPolicy.Crenel(o)));
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
                WallVerdict step = sequential.Build(x, y, x, y);
                Check(step.Accepted, "Single-place row rejected at " + length + "/" + i + ": " + step.Rejection);
                sequential.Settle();
            }
            Check(dragged.Build(start, 200, start + dir.Item1 * (length - 1), 200 + dir.Item2 * (length - 1)).Accepted, "Dragged row rejected");
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
        var single = new World(); single.Build(30, 30, 30, 30); single.Settle();
        Check(single.Cells.Values.Single().High, "An isolated crenel stays a high Vanilla special piece");
        single.Build(31, 30, 31, 30); single.Settle();
        Check(!single.Cells[30 * 800 + 30].High && single.Cells[30 * 800 + 31].High, "Sticky high first piece was not returned to parity");
        var reversedA = new World(); var reversedB = new World();
        Check(reversedA.Build(50, 50, 100, 50).Accepted && reversedB.Build(100, 50, 50, 50).Accepted, "Reverse drag failed");
        Check(reversedA.State() == reversedB.State(), "Reverse drag changed deterministic result");
        var connect = new World(); connect.Build(50, 100, 54, 100); connect.Build(58, 100, 62, 100);
        Check(connect.Build(55, 100, 57, 100).Accepted, "Joining rows failed"); connect.Settle();
        CheckManagedParity(connect, "Joined row parity");
        var limited = new World(); var raster = WallPolicy.Raster(limited, 20, 20, 30, 25, 3);
        Check(raster.Count == 3 && raster[2].X == 22 && raster[2].Y == 22, "Native simultaneous XY step/max prefix changed");
        Check(WallPolicy.Raster(limited, 20, 20, 30, 20, 0).Count == 0, "Rejected command has placements");
        limited.Put(20, 20); Check(WallPolicy.Predict(WallPolicy.Raster(limited, 20, 20, 20, 20, 5), 1).Count == 0, "Existing intact wall was rebuilt");
        bool invalid = false; try { WallPolicy.Raster(limited, -1, 30, 2, 30, 20); } catch (InvalidOperationException) { invalid = true; }
        Check(invalid, "Out-of-bounds raster not rejected");
    }
    private static void EnterableShapes()
    {
        // Normal castle shapes stay buildable at both parities; at most three highs in a row.
        for (int shift = 0; shift <= 1; shift++)
        {
            var longL = new World(); longL.Build(20 + shift, 20, 24 + shift, 20);
            Check(longL.Build(24 + shift, 21, 24 + shift, 24).Accepted, "Long corner rejected, shift " + shift);
            var branch = new World(); branch.Build(50 + shift, 50, 60 + shift, 50);
            Check(branch.Build(55 + shift, 51, 55 + shift, 55).Accepted, "Long T junction rejected, shift " + shift);
            var cross = new World(); cross.Build(80 + shift, 50, 90 + shift, 50); cross.Build(85 + shift, 51, 85 + shift, 55);
            Check(cross.Build(85 + shift, 49, 85 + shift, 45).Accepted, "Long crossing rejected, shift " + shift);
            var ring = new World(); ring.Build(70 + shift, 70, 76 + shift, 70); ring.Build(76 + shift, 71, 76 + shift, 76); ring.Build(75 + shift, 76, 70 + shift, 76);
            Check(ring.Build(70 + shift, 75, 70 + shift, 71).Accepted, "Ring rejected, shift " + shift);
            foreach (World world in new[] { longL, branch, cross, ring })
            {
                world.Settle();
                Check(LongestHighRun(world) <= 3, "Accepted normal shape has four highs in a row");
                CheckManagedParity(world, "Normal shape parity");
            }
        }
        var diagonalPair = new World(); Check(diagonalPair.Build(30, 30, 31, 31).Accepted, "Two diagonal crenels rejected");
        DiagonalRows();
        var gate = new World(); for (int x = 50; x <= 56; x++) gate.Build(x, 60, x, 60);
        gate.TryRead(53, 60, out WallCell tower); tower.Structure = true; bool before = tower.High;
        gate.Normalize(gate.Managed.ToArray()); Check(gate.Cells[tower.Id].High == before, "Building footprint was modified");
    }
    // Screen-horizontal walls are tile diagonals. Vanilla draws every piece as an isolated high
    // special; managed pieces alternate high/low along the diagonal, dragged or piece by piece.
    private static void DiagonalRows()
    {
        foreach (var dir in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }) for (int start = 200; start <= 201; start++) for (int length = 2; length <= 12; length++)
        {
            var dragged = new World(); var sequential = new World();
            int ex = start + dir.Item1 * (length - 1), ey = 200 + dir.Item2 * (length - 1);
            WallVerdict whole = dragged.Build(start, 200, ex, ey);
            Check(whole.Accepted, "Diagonal drag rejected: " + whole.Rejection);
            dragged.Settle();
            for (int i = 0; i < length; i++)
            {
                int x = start + dir.Item1 * i, y = 200 + dir.Item2 * i;
                Check(sequential.Build(x, y, x, y).Accepted, "Diagonal piece rejected");
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
        // Removing the diagonal neighbour leaves a lone piece to Vanilla (high).
        var lone = new World(); lone.Build(40, 40, 41, 41); lone.Settle(); lone.Remove(41, 41); lone.Settle();
        Check(lone.Cells[40 * 800 + 40].High, "A lone former diagonal piece did not return to Vanilla high");
        // A designer's unmanaged diagonal stays untouched when a managed piece joins it.
        var designer = new World(); designer.Put(60, 60); designer.Put(61, 61);
        Check(designer.Build(62, 62, 62, 62).Accepted, "Extending an unmanaged diagonal rejected"); designer.Settle();
        Check(designer.Cells[60 * 800 + 60].High && designer.Cells[61 * 800 + 61].High, "Unmanaged diagonal pieces changed");
        Check(designer.Cells[62 * 800 + 62].High == WallPolicy.DiagonalHigh(62), "New diagonal piece not alternating");
    }
    private static void ForbiddenShapes()
    {
        var doubled = new World(); doubled.Build(40, 40, 45, 40); doubled.Settle(); string old = doubled.State();
        WallVerdict rejected = doubled.Build(40, 41, 45, 41);
        Check(!rejected.Accepted && rejected.Rejection.Contains("double-row"), "Double row accepted"); Check(old == doubled.State(), "Rejected row changed map");
        var clicks = new World(); clicks.Build(40, 40, 41, 40); clicks.Build(40, 41, 40, 41);
        Check(!clicks.Build(41, 41, 41, 41).Accepted, "2x2 completed by single clicks");
        var within = new World(); within.Put(30, 31); within.Put(31, 31);
        Check(!within.Build(30, 30, 31, 30).Accepted, "Same-command 2x2 with an unmanaged wall not rejected");
        // Stairs with one-tile steps: every piece a corner.
        var stairs = new World();
        foreach (var c in new[] { (30, 30), (31, 30), (31, 31), (32, 31) }) Check(stairs.Build(c.Item1, c.Item2, c.Item1, c.Item2).Accepted, "Stair prefix rejected");
        WallVerdict third = stairs.Build(32, 32, 32, 32);
        Check(!third.Accepted && third.Rejection.Contains("special"), "Three special pieces accepted");
        // Stairs with two-tile steps pass the special rule but form runs of five highs.
        foreach (int shift in new[] { 0, 1 })
        {
            var wide = new World(); bool stopped = false;
            int x = 40 + shift, y = 40;
            for (int step = 0; step < 6 && !stopped; step++)
            {
                foreach (var c in new[] { (x, y), (x + 1, y), (x + 1, y + 1) })
                {
                    if (wide.Cells.ContainsKey(c.Item2 * 800 + c.Item1)) continue;
                    WallVerdict v = wide.Build(c.Item1, c.Item2, c.Item1, c.Item2);
                    if (!v.Accepted) { Check(v.Rejection.Contains("four high"), "Wide stairs rejected for an unexpected rule: " + v.Rejection); stopped = true; break; }
                    wide.Settle(); Check(LongestHighRun(wide) <= 3, "Accepted wide stair has four highs");
                }
                x += 2; y += 1;
            }
            Check(stopped, "Two-tile stairs were never stopped, shift " + shift);
        }
        // Short U, three tiles wide: allowed or rejected depending on map parity.
        var uLow = new World(); uLow.Build(12, 10, 12, 11); uLow.Build(13, 11, 14, 11);
        Check(uLow.Build(14, 10, 14, 10).Accepted, "Enterable short U rejected");
        var uHigh = new World(); uHigh.Build(11, 10, 11, 11); uHigh.Build(12, 11, 13, 11);
        WallVerdict u = uHigh.Build(13, 10, 13, 10);
        Check(!u.Accepted && u.Rejection.Contains("four high"), "Short U with five highs accepted");
    }
    private static void ScopeAndOwnership()
    {
        // Map-designer and pre-mod walls keep their loaded height; only new pieces are managed.
        var designed = new World(); for (int x = 100; x <= 105; x++) designed.Put(x, 100, 1, true);
        string designedBefore = string.Join(",", designed.Cells.Values.Select(c => c.High));
        Check(designed.Build(106, 100, 106, 100).Accepted, "Extending a designer wall rejected");
        designed.Settle();
        Check(designedBefore == string.Join(",", designed.Cells.Values.Where(c => c.X <= 105).Select(c => c.High)), "Unmanaged designer wall was rewritten");
        Check(designed.Managed.SetEquals(new[] { 100 * 800 + 106 }), "Only the new piece may become managed");
        var foreign = new World(); foreign.Put(52, 100, 2, false); foreign.Put(53, 100, 2, true);
        string foreignBefore = foreign.State();
        foreign.Build(50, 100, 51, 100); foreign.Settle();
        Check(foreign.Cells[100 * 800 + 52].Owner == 2 && !foreign.Managed.Contains(100 * 800 + 52) && !foreign.Managed.Contains(100 * 800 + 53), "Foreign walls became managed");
        var damaged = new World(); damaged.Build(30, 130, 36, 130); damaged.Settle();
        WallCell broken = damaged.Cells[130 * 800 + 33]; broken.Damaged = true; broken.High = !broken.High; bool brokenHigh = broken.High;
        damaged.Normalize(damaged.Managed.ToArray());
        Check(damaged.Cells[broken.Id].High == brokenHigh, "Damaged tile was normalized");
        // Demolition: survivors next to the gap must return to parity, never stay sticky high.
        var demolished = new World(); demolished.Build(50, 150, 60, 150); demolished.Settle();
        demolished.Remove(55, 150); demolished.Settle();
        CheckManagedParity(demolished, "Demolition survivor parity");
        Check(!demolished.Managed.Contains(150 * 800 + 55), "Demolished tile stayed managed");
        foreach (int x in new[] { 54, 56 }) Check(demolished.Cells[150 * 800 + x].High == WallPolicy.Parity(x, 150), "Survivor endpoint not at parity");
        // Removing an arm turns a managed corner into an endpoint, which must be lowered again.
        var corner = new World(); corner.Build(20, 20, 24, 20); corner.Build(24, 21, 24, 24); corner.Settle();
        for (int y = 21; y <= 24; y++) corner.Remove(24, y);
        corner.Settle(); CheckManagedParity(corner, "Former corner parity");
        var host = new World(); var client = new World();
        foreach (var command in new[] { (100, 180, 120, 180), (115, 181, 115, 190), (101, 181, 119, 181), (121, 180, 121, 180), (116, 190, 120, 190) })
        {
            WallVerdict a = host.Build(command.Item1, command.Item2, command.Item3, command.Item4), b = client.Build(command.Item1, command.Item2, command.Item3, command.Item4);
            host.Settle(); client.Settle();
            Check(a.Accepted == b.Accepted && a.Rejection == b.Rejection && host.State() == client.State(), "Two-peer deterministic fixture diverged");
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
            var editor = new World(); Check(editor.Build(100, 240, 109, 240, player).Accepted, "Editor owner fixture build failed");
            editor.Put(110, 240, player, true); // Designer-placed vanilla tile next to the managed run.
            int[][] mapData = MessagePackSerializer.Deserialize<int[][]>(MessagePackSerializer.Serialize(SaveState.Capture(400, editor.Managed.Select(i => editor.Cells[i]))));
            List<WallCell> mapRestored = SaveState.Validate(mapData, 400, editor);
            Check(mapRestored.Count == 10 && mapRestored.All(c => c.Owner == player), "Manual editor metadata changed an owner");
            Check(!mapRestored.Any(c => c.X == 110), "Unmanaged designer tile entered the managed set");
        }
    }
    private static void RandomConstruction()
    {
        // Build-only sequences: every accepted state keeps all walls enterable.
        var random = new Random(20261122);
        for (int fixture = 0; fixture < 40; fixture++)
        {
            var map = new World();
            for (int step = 0; step < 120; step++)
            {
                int x = 200 + random.Next(14), y = 220 + random.Next(14), length = random.Next(4);
                bool vertical = random.Next(2) == 0;
                string before = map.State();
                WallVerdict result = map.Build(x, y, vertical ? x : x + length, vertical ? y + length : y);
                if (!result.Accepted) { Check(before == map.State(), "Rejected random build changed map"); continue; }
                map.Settle();
                Check(!Has2x2(map), "Random construction created 2x2");
                Check(LongestHighRun(map) <= 3, "Random construction created four highs in a row");
                CheckManagedParity(map, "Random construction parity");
            }
        }
        // With demolition (not vetoed) the managed pieces still always return to parity.
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
                CheckManagedParity(map, "Demolition fuzz parity");
            }
        }
    }
    private static void PreviewAdmission()
    {
        foreach (var dir in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) })
        {
            var world = new World(); var cache = new PreviewAdmissionCache();
            for (int length = 1; length <= 8; length++) for (int maximum = 0; maximum < 12; maximum++)
            {
                int ex = 200 + dir.Item1 * length, ey = 200 + dir.Item2 * length;
                string before = world.State();
                WallVerdict preview = cache.Evaluate(world, 8, 200, 200, ex, ey, maximum, world.IsManaged);
                WallVerdict command = WallAdmission.Evaluate(world, 8, WallPolicy.Raster(world, 200, 200, ex, ey, maximum), world.IsManaged);
                Check(preview.Accepted == command.Accepted && preview.Rejection == command.Rejection && preview.Added == command.Added, "Preview differs from synchronized admission");
                Check(world.State() == before, "Preview modified map");
                Check(ReferenceEquals(preview, cache.Evaluate(world, 8, 200, 200, ex, ey, maximum, world.IsManaged)), "Stable full-read snapshot not reused");
            }
        }
        var map = new World(); map.Build(100, 100, 101, 100);
        var memo = new PreviewAdmissionCache();
        Check(!memo.Evaluate(map, 1, 100, 101, 101, 101, 10, map.IsManaged).Accepted, "Double row preview admitted");
        map.Remove(101, 100);
        Check(memo.Evaluate(map, 1, 100, 101, 101, 101, 10, map.IsManaged).Accepted, "Demolition left stale red preview");
        map.Put(101, 100);
        Check(!memo.Evaluate(map, 1, 100, 101, 101, 101, 10, map.IsManaged).Accepted, "Rebuild bypassed preview veto");
        var stable = new World(); stable.Build(120, 120, 124, 120);
        WallVerdict old = memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged);
        var watch = stable.Cells[120 * 800 + 123];
        foreach (Action<WallCell> mutate in new Action<WallCell>[] {
            c => c.Owner = 8, c => c.High = !c.High, c => c.Damaged = true,
            c => c.Structure = true, c => c.IsCrenel = false, c => c.IsWall = false })
        {
            WallCell original = watch.Copy(); mutate(watch);
            Check(!ReferenceEquals(old, memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged)), "Relevant cell change reused preview decision");
            stable.Cells[original.Id] = original; watch = original;
            old = memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged);
        }
        stable.Managed.Remove(watch.Id);
        Check(!ReferenceEquals(old, memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged)), "Managed-set change reused preview decision");
        stable.Managed.Add(watch.Id);
        memo.Clear();
        Check(!ReferenceEquals(old, memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged)), "Session reset retained preview result");
        old = memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged);
        Check(!ReferenceEquals(old, memo.Evaluate(stable, 2, 125, 120, 126, 120, 20, stable.IsManaged)), "Editor owner switch reused preview result");
        var bounds = new MutableBounds();
        old = memo.Evaluate(bounds, 1, 1, 100, 2, 100, 10, null);
        bounds.Open = true;
        Check(!ReferenceEquals(old, memo.Evaluate(bounds, 1, 1, 100, 2, 100, 10, null)), "Previously failed boundary read was not validated");
        old = memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            Check(ReferenceEquals(old, memo.Evaluate(stable, 1, 125, 120, 126, 120, 20, stable.IsManaged)), "Unchanged preview recomputed");
        Console.WriteLine("PASS: 1000 fully revalidated preview cache hits in " + timer.ElapsedMilliseconds + " ms on the small fixture; no in-game performance claim.");
        // A stale green/red decision is the cache's concrete failure class.
        var random = new Random(70123); var changing = new World(); var changingCache = new PreviewAdmissionCache();
        for (int i = 0; i < 2000; i++)
        {
            int rx = random.Next(98, 107), ry = random.Next(98, 104);
            int id = ry * 800 + rx;
            switch (random.Next(6))
            {
                case 0: changing.Cells.Remove(id); break;
                case 1: changing.Put(rx, ry, random.Next(1, 3), random.Next(2) != 0); break;
                case 2: var damaged = changing.Put(rx, ry); damaged.Damaged = true; break;
                case 3: var structure = changing.Put(rx, ry); structure.Structure = true; break;
                case 4: changing.Managed.Remove(id); break;
                case 5: changing.Managed.Add(id); break;
            }
            int player = 1 + i / 100 % 2, maximum = i / 50 % 8;
            WallVerdict cached = changingCache.Evaluate(changing, player, 100, 100, 105, 100, maximum, changing.IsManaged);
            WallVerdict fresh = WallAdmission.Evaluate(changing, player, WallPolicy.Raster(changing, 100, 100, 105, 100, maximum), changing.IsManaged);
            Check(cached.Rejection == fresh.Rejection && cached.Added == fresh.Added, "Mutated-world preview cache diverged at step " + i);
        }
    }
    private sealed class MutableBounds : IWallWorld
    {
        internal bool Open;
        private readonly World inner = new World();
        public bool TryRead(int x, int y, out WallCell cell)
        {
            if (x == 0 && y == 100 && Open) { cell = new WallCell { Id = 80000, X = x, Y = y }; return true; }
            return inner.TryRead(x, y, out cell);
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
        string brokenRegistry = Path.Combine(directory, "broken-registry.dll");
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(api, options))
        {
            var getter = assembly.MainModule.GetType("APIShared.NativeSites.NativeSiteRegistry").Methods.Single(m => m.Name == "get_IsImageBound");
            getter.IsPublic = false; getter.IsPrivate = true;
            assembly.Write(brokenRegistry);
        }
        Check(DiagnosticsFor(brokenRegistry).Any(d => d.GetMessage().Contains("IsImageBound")), "Loss of required native-site API was not detected");
        Console.WriteLine("PASS: synthetic SDK version/additive API accepted; missing required mission API rejected. Actual selected SDK remains untouched; fixtures=" + directory);
    }
    private static void EndpointGraphics()
    {
        var logic = new int[1000]; var deltas = new int[800 * 8]; var damage = new byte[1000];
        var structure = new ushort[1000]; var owner = new byte[1000];
        int tile = 500, row = 20;
        int[] steps = { -40, -39, 1, 41, 40, 39, -1, -41 };
        Array.Copy(steps, 0, deltas, row * 8, 8);
        foreach (int player in Enumerable.Range(1, 8)) foreach (int d in new[] { 0, 2, 4, 6 })
        {
            Array.Clear(logic); logic[tile] = 0x300; logic[tile + steps[d]] = 0x300; owner[tile] = (byte)(0xA0 | (player - 1));
            var assignment = new EndpointAssignment(player, row, tile + steps[d]);
            bool Matches(int direction) => EndpointGraphicsPolicy.Matches(assignment, tile, row, direction, logic, deltas, damage, structure, owner);
            Check(Matches(d) && Matches((d + 4) & 7), "Low endpoint axis or owner decoding failed");
            Check(!Matches((d + 2) & 7) && !Matches(1), "Transverse/invalid graphics axis accepted");
            logic[tile] |= 0x400000; Check(!Matches(d), "High endpoint graphics were replaced"); logic[tile] &= ~0x400000;
            damage[tile] = 1; Check(!Matches(d), "Damaged graphics were replaced"); damage[tile] = 0;
            structure[tile] = 3; Check(!Matches(d), "Building graphics were replaced"); structure[tile] = 0;
            owner[tile] ^= 1; Check(!Matches(d), "Transferred endpoint was rendered from stale metadata"); owner[tile] ^= 1;
            logic[tile + steps[(d + 2) & 7]] = 0x300; Check(!Matches(d), "A new corner reused old endpoint graphics");
            logic[tile + steps[(d + 2) & 7]] = 0;
            logic[tile + steps[d]] = 0; Check(!Matches(d), "An isolated tile reused old endpoint graphics");
            Check(!EndpointGraphicsPolicy.Matches(assignment, -1, row, d, logic, deltas, damage, structure, owner), "Negative id accepted");
            Check(!EndpointGraphicsPolicy.Matches(assignment, tile, -1, d, logic, deltas, damage, structure, owner), "Wrong row accepted");
        }
        Check(!EndpointGraphicsPolicy.Matches(null, tile, row, 0, logic, deltas, damage, structure, owner), "Untracked endpoint overridden");
        // Low diagonal piece: no axis neighbour, answered only for the renderer's first query (direction 0).
        Array.Clear(logic); logic[tile] = 0x300; logic[tile + steps[3]] = 0x300; owner[tile] = 0xA0;
        var piece = new EndpointAssignment(1, row, -1);
        Check(EndpointGraphicsPolicy.Matches(piece, tile, row, 0, logic, deltas, damage, structure, owner), "Low diagonal piece not drawn straight");
        Check(!EndpointGraphicsPolicy.Matches(piece, tile, row, 2, logic, deltas, damage, structure, owner), "Diagonal piece answered a second axis");
        logic[tile] |= 0x400000; Check(!EndpointGraphicsPolicy.Matches(piece, tile, row, 0, logic, deltas, damage, structure, owner), "High diagonal piece replaced");
        logic[tile] &= ~0x400000; logic[tile + steps[0]] = 0x300;
        Check(!EndpointGraphicsPolicy.Matches(piece, tile, row, 0, logic, deltas, damage, structure, owner), "Diagonal assignment reused after an axis neighbour appeared");
    }
}
