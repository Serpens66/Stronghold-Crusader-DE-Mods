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
    private sealed class World : IWallWorld
    {
        internal readonly Dictionary<int, WallCell> Cells = new Dictionary<int, WallCell>();
        public bool TryRead(int x, int y, out WallCell cell)
        {
            cell = null; if (x < 1 || x >= 799 || y < 1 || y >= 399) return false;
            int id = y * 800 + x;
            if (!Cells.TryGetValue(id, out cell)) cell = new WallCell { Id = id, X = x, Y = y };
            return true;
        }
        internal WallCell Put(int x, int y, int owner = 1, bool high = true)
        {
            TryRead(x, y, out WallCell cell); cell = cell.Copy();
            cell.Owner = owner; cell.IsWall = cell.IsCrenel = true; cell.High = high; Cells[cell.Id] = cell; return cell;
        }
        internal void Apply(WallPlan plan)
        {
            foreach (var pair in plan.Heights)
            { WallCell cell = plan.Cells[pair.Key].Copy(); cell.High = pair.Value; Cells[cell.Id] = cell; }
        }
        internal WallPlan Build(int x, int y, int endX, int endY, int owner = 1)
        {
            List<WallCell> predicted = WallPolicy.Predict(WallPolicy.Raster(this, x, y, endX, endY, 800), owner);
            WallPlan admission = WallPolicy.Plan(this, owner, predicted, true, Cells);
            if (admission.Accepted) { foreach (WallCell cell in predicted) Cells[cell.Id] = cell; Apply(admission); }
            return admission;
        }
        internal string State() => string.Join(";", Cells.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value.Owner + ":" + p.Value.High));
        internal void VanillaGraphics()
        {
            foreach (WallCell cell in Cells.Values)
            {
                bool Has(int x, int y) => TryRead(x, y, out WallCell c) && c.IsCrenel;
                bool n = Has(cell.X, cell.Y - 1), s = Has(cell.X, cell.Y + 1), e = Has(cell.X + 1, cell.Y), w = Has(cell.X - 1, cell.Y);
                bool lowEndpoint = !cell.High && ((n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0)) == 1;
                if (!lowEndpoint && !((n && s && !e && !w) || (e && w && !n && !s))) cell.High = true;
            }
        }
    }
    private static void Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]); CompileRuntime(root);
            StraightRows(); SpecialShapes(); OwnershipAndBounds(); Persistence(); Availability(); EndpointGraphics(); ExploitSequences();
            Console.WriteLine("PASS: " + checks + " assertions; production runtime compilation, policy, redraw stability, ownership and MessagePack persistence. Real game/peer execution is not inferred.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    private static void StraightRows()
    {
        foreach (var dir in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) for (int length = 1; length <= 80; length++)
        {
            var sequential = new World(); var dragged = new World();
            for (int i = 0; i < length; i++)
            {
                WallPlan plan = sequential.Build(200 + dir.Item1 * i, 200 + dir.Item2 * i, 200 + dir.Item1 * i, 200 + dir.Item2 * i);
                Check(plan.Accepted, "Single-place row rejected at " + length + "/" + i + ": " + plan.Rejection);
                string before = sequential.State(); sequential.VanillaGraphics(); Check(before == sequential.State(), "Vanilla re-promoted a normalized middle piece");
            }
            WallPlan all = dragged.Build(200, 200, 200 + dir.Item1 * (length - 1), 200 + dir.Item2 * (length - 1));
            Check(all.Accepted, "Dragged row rejected");
            Check(sequential.Cells.Count == length && dragged.Cells.Count == length, "Placement count changed");
            Check(all.EqualPairs == 0, "A straight row has an avoidable equal pair");
            foreach (World world in new[] { sequential, dragged })
            {
                bool? previous = null; int run = 0;
                for (int i = 0; i < length; i++)
                {
                    world.TryRead(200 + dir.Item1 * i, 200 + dir.Item2 * i, out WallCell cell);
                    run = previous == cell.High ? run + 1 : 1; previous = cell.High;
                    Check(run == 1, "Straight row failed strict alternation");
                }
            }
        }
        var reversedA = new World(); var reversedB = new World();
        Check(reversedA.Build(50, 50, 100, 50).Accepted && reversedB.Build(100, 50, 50, 50).Accepted, "Reverse drag failed");
        Check(reversedA.State() == reversedB.State(), "Reverse drag changed deterministic result");
        var connect = new World(); connect.Build(50, 100, 54, 100); connect.Build(58, 100, 62, 100);
        Check(connect.Build(55, 100, 57, 100).Accepted, "Joining rows failed");
        var limited = new World(); var raster = WallPolicy.Raster(limited, 20, 20, 30, 25, 3);
        Check(raster.Count == 3 && raster[2].X == 22 && raster[2].Y == 22, "Native simultaneous XY step/max prefix changed");
        Check(WallPolicy.Raster(limited, 20, 20, 30, 20, 0).Count == 0, "Rejected command has placements");
        limited.Put(20, 20); Check(WallPolicy.Predict(WallPolicy.Raster(limited, 20, 20, 20, 20, 5), 1).Count == 0, "Existing intact wall was rebuilt");
        var retained = new World(); retained.Build(40, 40, 47, 40);
        var preferred = retained.Cells.ToDictionary(p => p.Key, p => p.Value.Copy());
        WallCell end = retained.Cells.Values.Single(c => !c.High && (c.X == 40 || c.X == 47)); end.High = true;
        WallPlan restored = WallPolicy.Plan(retained, 1, new[] { end }, false, preferred);
        Check(restored.Accepted && restored.Heights.All(p => p.Value == preferred[p.Key].High), "A temporary Vanilla promotion displaced the saved assignment");
    }
    private static void SpecialShapes()
    {
        var single = new World(); single.Build(30, 30, 30, 30);
        Check(single.Cells.Values.Single().High, "Single piece must be high");
        single.Build(31, 30, 31, 30); Check(single.Cells[30 * 800 + 30].High && !single.Cells[30 * 800 + 31].High, "Preserve first endpoint high and lower its new neighbor");
        single.Build(32, 30, 32, 30); single.TryRead(31, 30, out WallCell mid); Check(!mid.High, "Sticky high middle was not cleared");
        var doubled = new World(); doubled.Build(40, 40, 45, 40); string old = doubled.State();
        WallPlan rejected = doubled.Build(40, 41, 45, 41); Check(!rejected.Accepted && rejected.Rejection.Contains("double-row"), "Double row accepted"); Check(old == doubled.State(), "Rejected row changed map");
        var within = new World(); within.Put(30, 31); within.Put(31, 31);
        Check(!within.Build(30, 30, 31, 30).Accepted, "Same-command predicted 2x2 not rejected");
        var shortL = new World(); shortL.Build(20, 20, 21, 20);
        Check(shortL.Build(21, 21, 21, 21).Accepted, "Short L with two variable ends rejected");
        Check(shortL.Cells[20 * 800 + 21].High && !shortL.Cells[20 * 800 + 20].High && !shortL.Cells[21 * 800 + 21].High, "Short L must be low/high/low");
        var longL = new World(); longL.Build(20, 20, 24, 20); Check(longL.Build(24, 21, 24, 24).Accepted, "Long normal corner rejected");
        var branch = new World(); branch.Build(50, 50, 60, 50); Check(branch.Build(55, 51, 55, 55).Accepted, "Long T junction rejected");
        var ring = new World(); ring.Build(70, 70, 76, 70); ring.Build(76, 71, 76, 76); ring.Build(75, 76, 70, 76);
        Check(ring.Build(70, 75, 70, 71).Accepted, "Ring rejected");
        var diagonal = new World(); Check(!diagonal.Build(30, 30, 34, 34).Accepted, "Three diagonal high special pieces accepted");
        var gate = new World(); for (int x = 50; x <= 56; x++) gate.Put(x, 60);
        gate.TryRead(53, 60, out WallCell tower); tower.Structure = true;
        WallPlan gatePlan = WallPolicy.Plan(gate, 1, new[] { gate.Cells[60 * 800 + 50] }, false);
        Check(!gatePlan.Heights.ContainsKey(tower.Id), "Building footprint was modified");
    }
    private static void OwnershipAndBounds()
    {
        var w = new World(); w.Build(30, 90, 34, 90); w.Build(90, 90, 94, 90);
        string distant = string.Join(",", w.Cells.Values.Where(c => c.X >= 90).Select(c => c.High));
        Check(w.Build(35, 90, 35, 90).Accepted, "Extension failed");
        Check(distant == string.Join(",", w.Cells.Values.Where(c => c.X >= 90).Select(c => c.High)), "Disconnected existing row changed");
        var foreign = new World(); foreign.Put(52, 100, 2, false); foreign.Put(53, 100, 2, true);
        foreign.Put(50, 100); foreign.Put(51, 100);
        WallPlan plan = WallPolicy.Plan(foreign, 1, new[] { foreign.Cells[100 * 800 + 50] }, false);
        Check(plan.Heights.Keys.All(id => foreign.Cells[id].Owner == 1), "Foreign owner included in writes");
        var damaged = new World(); for (int x = 30; x <= 36; x++) damaged.Put(x, 130);
        damaged.TryRead(33, 130, out WallCell broken); broken.Damaged = true;
        WallPlan damagePlan = WallPolicy.Plan(damaged, 1, new[] { damaged.Cells[130 * 800 + 30] }, false);
        Check(!damagePlan.Heights.ContainsKey(broken.Id), "Damaged tile was normalized");
        bool invalid = false; try { WallPolicy.Raster(w, -1, 30, 2, 30, 20); } catch (InvalidOperationException) { invalid = true; }
        Check(invalid, "Out-of-bounds raster not rejected");
        var host = new World(); var client = new World();
        foreach (var command in new[] { (100, 180, 120, 180), (115, 181, 115, 190), (101, 181, 119, 181), (121, 180, 121, 180) })
        {
            WallPlan a = host.Build(command.Item1, command.Item2, command.Item3, command.Item4), b = client.Build(command.Item1, command.Item2, command.Item3, command.Item4);
            Check(a.Accepted == b.Accepted && a.Rejection == b.Rejection && host.State() == client.State(), "Two-peer deterministic fixture diverged");
        }
    }
    private static void Persistence()
    {
        var w = new World(); w.Build(80, 220, 100, 220);
        int[][] data = SaveState.Capture(400, w.Cells.Values);
        byte[] bytes = MessagePackSerializer.Serialize(data);
        List<WallCell> restored = SaveState.Validate(MessagePackSerializer.Deserialize<int[][]>(bytes), 400, w);
        Check(restored.Count == w.Cells.Count && restored.All(c => c.High == w.Cells[c.Id].High), "Actual MessagePack round trip changed heights");
        int id = restored[3].Id; w.Cells[id].Owner = 2;
        Check(!SaveState.Validate(data, 400, w).Any(c => c.Id == id), "Transferred saved cell resurrected");
        var empty = SaveState.Capture(400, Array.Empty<WallCell>()); Check(SaveState.Validate(empty, 400, w).Count == 0, "Disabled/empty header failed");
        bool bad = false; try { SaveState.Validate(data, 500, w); } catch (InvalidOperationException) { bad = true; } Check(bad, "Wrong map size accepted");
        int[][] duplicate = data.Select(r => r.ToArray()).ToArray(); duplicate[2][0] = duplicate[1][0];
        bad = false; try { SaveState.Validate(duplicate, 400, w); } catch (InvalidOperationException) { bad = true; } Check(bad, "Duplicate saved id accepted");
        for (int player = 1; player <= 8; player++)
        {
            var editor = new World(); Check(editor.Build(100, 240, 109, 240, player).Accepted, "Editor owner fixture build failed");
            int[][] mapData = MessagePackSerializer.Deserialize<int[][]>(MessagePackSerializer.Serialize(SaveState.Capture(400, editor.Cells.Values)));
            List<WallCell> mapRestored = SaveState.Validate(mapData, 400, editor);
            Check(mapRestored.Count == 10 && mapRestored.All(c => c.Owner == player && c.High == editor.Cells[c.Id].High), "Manual editor metadata changed an owner/height");
            var ids = new HashSet<int>(mapRestored.Select(c => c.Id));
            WallPlan restoredPlan = WallPolicy.Plan(editor, player, mapRestored, false);
            Check(restoredPlan.Accepted && restoredPlan.Heights.Keys.All(ids.Contains), "Restoring editor map expanded its metadata scope");
        }
    }
    private static void CompileRuntime(string root)
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
                .Replace("$(ApiSharedDir)", game + "/BepInEx/plugins/APIShared_Serp");
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
        Console.WriteLine("PASS: all production sources type-checked against true Assembly-CSharp, installed SE and unchanged APIShared package; no runtime DLL emitted.");
    }
    private static void Availability()
    {
        var world = new World(); world.Build(100, 100, 101, 100);
        List<WallCell> raster = WallPolicy.Raster(world, 101, 101, 101, 108, 20);
        Check(WallPolicy.Predict(raster, 1, -1).Count == 0, "Negative native quota should stop before any placement");
        List<WallCell> prefix = WallPolicy.Predict(raster, 1, 0);
        Check(prefix.Count == 1, "Native strict-less-than quota semantics changed");
        Check(WallPolicy.Plan(world, 1, prefix, true).Accepted, "Affordable short corner with variable ends rejected");
        Check(WallPolicy.Plan(world, 1, WallPolicy.Predict(raster, 1, 20), true).Accepted, "Affordable long corner rejected");
        byte[] image = new byte[1024], pattern = { 0x31, 0xC0, 0xC3 };
        void Write(int at, int value, int size) { for (int i = 0; i < size; i++) image[at + i] = (byte)(value >> (8 * i)); }
        Write(0, 0x5A4D, 2); Write(0x3C, 64, 4); Write(64, 0x4550, 4); Write(70, 2, 2);
        Write(88 + 8, 256, 4); Write(88 + 12, 256, 4); Write(88 + 36, 0x20000000, 4);
        Write(128 + 8, 256, 4); Write(128 + 12, 512, 4); Write(128 + 36, 0x40000000, 4);
        Array.Copy(pattern, 0, image, 300, pattern.Length); Array.Copy(pattern, 0, image, 600, pattern.Length);
        Check(AvailabilityPattern.FindUniqueExecutable(image, pattern) == 300, "Data-section matches must be ignored");
        Array.Copy(pattern, 0, image, 350, pattern.Length);
        Check(AvailabilityPattern.FindUniqueExecutable(image, pattern) == -1, "Ambiguous executable pattern accepted");
        image[350] = 0; image[300] = 0;
        Check(AvailabilityPattern.FindUniqueExecutable(image, pattern) == -1, "Missing executable pattern accepted");
        Write(0x3C, int.MaxValue, 4);
        Check(AvailabilityPattern.FindUniqueExecutable(image, pattern) == -1, "Invalid PE bounds accepted");
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
    }
    private static void ExploitSequences()
    {
        // Coordinates from the 2026-10-15 log: side teeth, joins and reverse drags.
        var world = new World(); world.Build(545, 292, 545, 301);
        foreach (var c in new[] { (546,299,546,299), (546,297,546,297), (546,295,546,295), (546,292,546,292),
            (547,292,547,292), (547,295,547,295), (547,296,547,296), (547,297,547,297), (547,299,547,300),
            (546,302,545,302), (546,296,546,293), (543,295,543,297), (543,293,543,292), (543,294,543,294) })
        {
            string before = world.State(); WallPlan result = world.Build(c.Item1,c.Item2,c.Item3,c.Item4);
            if (!result.Accepted) Check(before == world.State(), "Rejected recorded exploit changed tiles");
            else { Check(WallPolicy.Plan(world, 1, world.Cells.Values.ToArray(), false).Accepted, "Accepted recorded side attachment violates triples");
                string normalized = world.State(); world.VanillaGraphics(); Check(normalized == world.State(), "Recorded side attachment promoted on redraw"); }
        }
        var random = new Random(20261015);
        for (int fixture = 0; fixture < 24; fixture++)
        {
            var map = new World();
            for (int step = 0; step < 100; step++)
            {
                int x = 200 + random.Next(12), y = 220 + random.Next(12);
                if (step % 7 == 0 && map.Cells.Count != 0)
                {
                    int id = map.Cells.Keys.OrderBy(k => k).ElementAt(random.Next(map.Cells.Count)); map.Cells.Remove(id);
                    WallPlan remaining = WallPolicy.Plan(map, 1, map.Cells.Values.ToArray(), false); if (remaining.Accepted) map.Apply(remaining);
                }
                string before = map.State(); WallPlan result = map.Build(x, y, x + random.Next(3), y);
                if (!result.Accepted) Check(before == map.State(), "Rejected random rebuild changed map");
                else
                {
                    Check(result.EqualPairs <= result.Cells.Count, "Invalid pair accounting");
                    Check(result.Accepted, "Accepted random build must have a valid solution");
                    var normalized = result.Cells.Keys.ToDictionary(id => id, id => map.Cells[id].High); map.VanillaGraphics();
                    Check(normalized.All(p => map.Cells[p.Key].High == p.Value), "Accepted random build promoted its corrected component after redraw");
                }
                foreach (WallCell cell in map.Cells.Values)
                    Check(!(map.Cells.ContainsKey(cell.Id + 1) && map.Cells.ContainsKey(cell.Id + 800) && map.Cells.ContainsKey(cell.Id + 801)), "Random construction created 2x2");
            }
        }
    }
}
