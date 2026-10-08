from pathlib import Path
exec(Path('_inspect/FormationRows/implement.py').read_text().split("p='APIShared/src/UnitCommands/FormationModel.cs'")[0])
p='BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml';s=read(p).replace('//n:UserControl.Resources/n:ResourceDictionary','//n:UserControl.Resources');write(p,s)
p='APIShared/src/UnitCommands/FormationRuntime.cs';s=read(p)
start=s.index('            var result = new NativeDestination[slots.Count];',s.index('        private static NativeDestination[] SnapSlots('));end=s.index('\n        private List<NativeDestination> CaptureReachableCandidates(',start)
old=s[start:end]; body=old[old.index('            var used ='):old.index('            return result;')]
body=body.replace('NativeDestination','FormationPoint').replace('result[index] = candidates[candidate];','result[index] = candidate;').replace('candidate.TileId < candidates[best].TileId','tileIds[candidateIndex] < tileIds[best]')
body=body.replace('                FormationPoint selected = candidates[best];\n                result[slotIndex] = new FormationPoint(\n                    selected.TileId, selected.X, selected.Y, FormationRole.Neutral);','                result[slotIndex] = best;')
s=s[:start]+'''            var points = new FormationPoint[candidates.Count];
            var tileIds = new int[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                points[index] = new FormationPoint(candidates[index].X, candidates[index].Y, 0, 0);
                tileIds[index] = candidates[index].TileId;
            }
            int[] selected = FormationPlacementModel.SelectCandidates(anchorX, anchorY, slots, points, tileIds);
            var result = new NativeDestination[slots.Count];
            for (int index = 0; index < slots.Count; index++) result[index] = candidates[selected[index]];
            return result;
        }
'''+s[end:];write(p,s)
p='APIShared/src/UnitCommands/FormationModel.cs';s=read(p);idx=s.index('    internal sealed class FormationGestureState')
s=s[:idx]+'''    internal static class FormationPlacementModel
    {
        internal static int[] SelectCandidates(int anchorX, int anchorY,
            IReadOnlyList<FormationPoint> slots, IReadOnlyList<FormationPoint> candidates,
            IReadOnlyList<int> tileIds)
        {
            if (candidates.Count == 0) throw new InvalidOperationException("No reachable destination.");
            var result = new int[slots.Count];
'''+body+'''            return result;
        }
    }

'''+s[idx:];write(p,s)
p='BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
s=s.replace('ProtocolVersion = 5','ProtocolVersion = 6').replace('Width = 200, UnitCount','Rows = 20, Width = 200, UnitCount').replace('restored.Width == packet.Width &&','restored.Rows == packet.Rows && restored.Width == packet.Width &&')
s=re.sub(r'        Check\(FormationModel.ChangeDensity.*?"density upper clamp"\);\n','',s,flags=re.S)
start=s.index('        Check(FormationModel.ResolveDraggedWidth(',s.index('private static void TestAutomaticWidths'));end=s.index('    private static void TestDirectionQuantization()',start)
s=s[:start]+'''        foreach (FormationKind kind in new[] { FormationKind.Block, FormationKind.Line,
            FormationKind.Column, FormationKind.Wedge, FormationKind.Circle, FormationKind.Vanilla })
        {
            for (int count = 1; count <= 100; count++)
            {
                var gesture = new FormationGestureState(kind, count, 4);
                int initial = gesture.Rows;
                gesture.UpdateDirection(20, -20, 2);
                Check(gesture.Rows == initial && gesture.Direction == 1, "drag changes direction only");
                gesture.UpdateDirection(0, 0, 2);
                Check(gesture.Direction == 1 && gesture.ExplicitDirection, "deadzone retains last direction");
                gesture.ApplyWheel(0.25f, 1);
                gesture.ApplyWheel(0.25f, 1);
                gesture.ApplyWheel(0.75f, 2);
                bool fixedShape = kind == FormationKind.Circle || kind == FormationKind.Vanilla;
                Check(gesture.Rows == (fixedShape ? initial : Math.Max(1, initial - 1)), "fractional wheel sampled once per frame");
                gesture.ApplyWheel(-3f, 3);
                Check(gesture.Rows == (fixedShape ? initial : Math.Min(count, Math.Max(1, initial - 1) + 3)) &&
                      gesture.Direction == 1, "multiple wheel steps change rows only");
                gesture.ApplyWheel(10000f, 4);
                Check(gesture.Rows == (fixedShape ? initial : 1), "minimum rows");
                gesture.ApplyWheel(-10000f, 5);
                Check(gesture.Rows == (fixedShape ? initial : count), "maximum rows");
                Check(new FormationGestureState(kind, count, 0).Rows == initial, "each gesture resets automatic rows");
            }
        }
        TestExactRowsAndPlacement();
    }

    private static void TestExactRowsAndPlacement()
    {
        foreach (int count in new[] { 1, 2, 31, 100, 4000 })
        foreach (int rows in new[] { 1, 2, 3, 21, 31, count }.Distinct().Where(r => r <= count))
        foreach (FormationKind kind in new[] { FormationKind.Block, FormationKind.Line, FormationKind.Column, FormationKind.Wedge })
        for (int sector = 0; sector < 8; sector++)
        for (int density = 1; density <= 4; density++)
        {
            var slots = FormationModel.BuildRelativeSlots(kind, count, rows, density, sector);
            FormationModel.GetForwardVector(sector, out int fx, out int fy);
            var ranks = slots.GroupBy(p => p.Rank).OrderBy(g => g.Key).ToArray();
            Check(slots.Count == count && ranks.Length == rows &&
                  slots.Select(p => Tuple.Create(p.X,p.Y)).Distinct().Count() == count, "exact ranks, complete unique positions");
            foreach (var rank in ranks)
                Check(rank.Select(p => p.X * fx + p.Y * fy).Distinct().Count() == 1,
                    "ranks are straight and parallel for every direction and density");
            int[] sizes = ranks.Select(g => g.Count()).ToArray();
            Check(sizes.Max() == FormationModel.ResolveWidthForRows(kind, count, rows), "packet width is maximum rank width");
            if (kind == FormationKind.Wedge && rows > 1)
                Check(sizes[0] == 1 && sizes.Zip(sizes.Skip(1), (a,b) => a <= b).All(x => x), "single apex and nondecreasing wedge ranks");
            else if (kind != FormationKind.Wedge)
                Check(sizes.Max() - sizes.Min() <= 1, "balanced rectangular ranks");
        }
        var line = FormationModel.BuildRelativeSlots(FormationKind.Line, 31, 1, 4, 1);
        var candidates = line.Select(p => new FormationPoint(200+p.X,200+p.Y,0,0)).Reverse().ToArray();
        int[] ids = Enumerable.Range(1, candidates.Length).ToArray();
        int[] chosen = FormationPlacementModel.SelectCandidates(200,200,line,candidates,ids);
        Check(chosen.Distinct().Count() == 31 && chosen.Select((c,i) =>
            candidates[c].X == 200+line[i].X && candidates[c].Y == 200+line[i].Y).All(x=>x),
            "wide diagonal line preserves every reachable exact destination");
        var blocked = new[] { new FormationPoint(0,0,0,0), new FormationPoint(1,0,1,0) };
        var reachable = new[] { new FormationPoint(1,0,0,0), new FormationPoint(-1,0,0,0) };
        chosen = FormationPlacementModel.SelectCandidates(0,0,blocked,reachable,new[] { 1,2 });
        Check(chosen[0] == 1 && chosen[1] == 0, "blocked slot cannot steal a later exact slot");
        chosen = FormationPlacementModel.SelectCandidates(0,0,new[] { blocked[0] },reachable,new[] { 9,3 });
        Check(chosen[0] == 1, "equal-distance fallback uses tile ID tie break");
        chosen = FormationPlacementModel.SelectCandidates(0,0,
            new[] { new FormationPoint(-2,0,0,0), new FormationPoint(0,0,1,0) },
            new[] { new FormationPoint(0,0,0,0), new FormationPoint(1,0,0,0) },new[] { 1,2 });
        Check(chosen[0] == 1 && chosen[1] == 0, "map-edge fallback reserves valid exact destinations");
        var first = FormationPreviewKey.Create(FormationKind.Line,2,RangedPlacementMode.Off,0,2,10,10,31,false,16);
        var second = FormationPreviewKey.Create(FormationKind.Line,2,RangedPlacementMode.Off,0,2,10,10,31,false,21);
        Check(!first.Equals(second), "rows distinguish previews with equal derived width");
    }

'''+s[end:]
s=s.replace('int vanillaDragged = FormationModel.ResolveDraggedWidth(\n            FormationKind.Vanilla, 10, 40);','int vanillaDragged = FormationModel.ResolveWidthForRows(FormationKind.Vanilla, 40, 1);')
s=s.replace('FormationModel.ResolveDraggedWidth(FormationKind.Block, 1, 20) == 4','new FormationGestureState(FormationKind.Block, 20, 0).Rows == 5')
# Existing role/shape fixtures formerly passed width. Retain their topology using explicit equivalent ranks.
s=re.sub(r'FormationModel.BuildRelativeSlots\(\s*FormationKind\.(Block|Line|Column), (\d+), (\d+),',lambda m: m[0].replace(', '+m[3]+',', ', '+str((int(m[2])+int(m[3])-1)//int(m[3]))+',',1),s)
s=s.replace('"short drag retains automatic Block depth"','"gesture starts with automatic Block depth"')
write(p,s)
