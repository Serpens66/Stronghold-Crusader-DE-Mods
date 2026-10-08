from pathlib import Path
exec(Path('_inspect/FormationRows/implement.py').read_text().split("p='APIShared/src/UnitCommands/FormationModel.cs'")[0])
p='APIShared/src/UnitCommands/FormationModel.cs';s=read(p)
needle='        internal static int[] SelectCandidates('
idx=s.index(needle)
s=s[:idx]+'''        internal static IEnumerable<FormationPoint> EnumerateReachabilityProbes(
            IReadOnlyList<FormationPoint> slots)
        {
            // Adjacent alternatives must be searched even when an outer desired tile is blocked.
            var seen = new HashSet<long>();
            foreach (FormationPoint slot in slots)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = slot.X + dx, y = slot.Y + dy;
                        long key = ((long)x << 32) | (uint)y;
                        if (seen.Add(key)) yield return new FormationPoint(x, y, 0, 0);
                    }
        }

'''+s[idx:];write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p).replace('foreach (FormationPoint slot in desiredSlots)','foreach (FormationPoint slot in FormationPlacementModel.EnumerateReachabilityProbes(desiredSlots))');write(p,s)
p='BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
needle='        var blocked = new[]'
idx=s.index(needle)
s=s[:idx]+'''        var probes = FormationPlacementModel.EnumerateReachabilityProbes(line).ToArray();
        Check(probes.Select(p => Tuple.Create(p.X,p.Y)).Distinct().Count() == probes.Length &&
              probes.Any(p => p.X == line[0].X + 1 && p.Y == line[0].Y),
            "wide formation probes include adjacent fallback outside the anchor candidate pool");
        var farBlocked = new[] { new FormationPoint(60,0,0,0), new FormationPoint(56,0,0,1) };
        var farCandidates = new[] { new FormationPoint(0,0,0,0), new FormationPoint(56,0,0,0), new FormationPoint(60,1,0,0) };
        int[] farChosen = FormationPlacementModel.SelectCandidates(0,0,farBlocked,farCandidates,new[] { 1,2,3 });
        Check(farChosen[0] == 2 && farChosen[1] == 1,
            "blocked wide-line endpoint uses its adjacent alternative and reserves the other exact endpoint");
'''+s[idx:]
s=s.replace('reachable.Contains("pending.Remove(current.TileId)") &&','reachable.Contains("pending.Remove(current.TileId)") &&\n              reachable.Contains("FormationPlacementModel.EnumerateReachabilityProbes(desiredSlots)") &&')
write(p,s)
