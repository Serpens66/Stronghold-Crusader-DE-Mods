using BugfixesAndQoL.UnitCommands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BugfixesAndQoL.Formations.Tests;

[TestClass]
public class FormationGeometryTests
{
    [TestMethod]
    public void DistantSlotsMatchNearestCandidateReference()
    {
        var random = new Random(3421);
        for (int trial = 0; trial < 30; trial++)
        {
            var candidates = Enumerable.Range(0, 60).Select(i => new FormationPoint(
                i % 10 * 3, i / 10 * 3, 0, 0)).ToArray();
            var tiles = candidates.Select((p, i) => 1000 - i).ToArray();
            var slots = Enumerable.Range(0, 80).Select(i => new FormationPoint(
                random.Next(-10000, 10000), random.Next(-10000, 10000), 0, 0)).ToArray();
            var expected = new int[slots.Length];
            var used = new HashSet<int>();
            for (int i = 0; i < slots.Length; i++)
            {
                int candidate = Enumerable.Range(0, candidates.Length).Where(c => !used.Contains(c))
                    .OrderBy(c => (long)(candidates[c].X - slots[i].X) * (candidates[c].X - slots[i].X) +
                        (long)(candidates[c].Y - slots[i].Y) * (candidates[c].Y - slots[i].Y))
                    .ThenBy(c => tiles[c]).ThenBy(c => c).DefaultIfEmpty(-1).First();
                expected[i] = candidate < 0 ? i % candidates.Length : candidate;
                used.Add(expected[i]);
            }
            CollectionAssert.AreEqual(expected, FormationPlacementModel.SelectCandidates(0, 0, slots, candidates, tiles));
        }
    }

    [TestMethod]
    public void AdmissionClampPreservesIntentAndWheelState()
    {
        foreach (FormationKind kind in Enum.GetValues<FormationKind>())
        {
            bool fixedShape = kind == FormationKind.Vanilla || kind == FormationKind.Circle;
            var gesture = new FormationGestureState(kind, 1300, 0, 37);
            gesture.UpdateDirection(20, 0, 2);
            gesture.ApplyWheel(-0.5f, 1);
            gesture = gesture.WithCount(3);
            Assert.AreEqual(fixedShape ? 0 : 37, gesture.RequestedRows);
            gesture = gesture.WithCount(650);
            Assert.AreEqual(fixedShape ? FormationModel.ResolveAutomaticRows(kind, 650) : 37, gesture.Rows);
            Assert.IsTrue(gesture.ExplicitDirection);
            gesture.ApplyWheel(-0.5f, 2);
            Assert.AreEqual(fixedShape ? 0 : 38, gesture.RequestedRows);
        }
    }

    [TestMethod]
    public void CircleFootprintAndSpacingAreIndependentOfFacing()
    {
        foreach (int count in new[] { 1, 2, 3, 30, 31, 41, 75, 76, 77, 100, 1000 })
        foreach (int density in new[] { 1, 2, 3, 4 })
        {
            var baseline = FormationModel.BuildRelativeSlots(FormationKind.Circle, count, 1, density, 0);
            var units = Enumerable.Range(1, count).Select(id => new FormationUnit(id, (uint)id, 1,
                (FormationRole)(id % 4))).ToArray();
            for (int direction = 0; direction < 8; direction++)
            {
                var slots = FormationModel.BuildRelativeSlots(FormationKind.Circle, count, count, density, direction);
                CollectionAssert.AreEqual(baseline.Select(p => (p.X, p.Y)).ToArray(),
                    slots.Select(p => (p.X, p.Y)).ToArray(), $"count={count}, density={density}, direction={direction}");
                Assert.AreEqual(count, slots.Select(p => (p.X, p.Y)).Distinct().Count());
                Assert.IsTrue(slots.All(p => p.Rank >= 0 && p.X % density == 0 && p.Y % density == 0));
                foreach (RangedPlacementMode mode in Enum.GetValues<RangedPlacementMode>())
                {
                    int[] assignment = FormationModel.AssignSlotsByRole(units, slots, mode, FormationKind.Circle);
                    Assert.AreEqual(count, assignment.Distinct().Count());
                    if (mode == RangedPlacementMode.Center || mode == RangedPlacementMode.Off)
                        CollectionAssert.AreEqual(FormationModel.AssignSlotsByRole(units, baseline, mode, FormationKind.Circle), assignment);
                }
                FormationModel.GetForwardVector(direction, out int fx, out int fy);
                var ordered = slots.OrderBy(p => p.Rank).ToArray();
                Assert.IsTrue(ordered.Zip(ordered.Skip(1)).All(pair =>
                    pair.First.X * fx + pair.First.Y * fy >= pair.Second.X * fx + pair.Second.Y * fy));
                var candidates = slots.Select(p => new FormationPoint(200 + p.X, 200 + p.Y, 0, 0)).Reverse().ToArray();
                int[] snapped = FormationPlacementModel.SelectCandidates(200, 200, slots, candidates,
                    Enumerable.Range(1, count).ToArray());
                CollectionAssert.AreEqual(slots.Select(p => (200 + p.X, 200 + p.Y)).ToArray(),
                    snapped.Select(i => (candidates[i].X, candidates[i].Y)).ToArray());
            }
        }
    }

    [TestMethod]
    public void ShapesPreserveEveryUnitAndProduceDistinctDeterministicSlots()
    {
        foreach (FormationKind kind in new[] { FormationKind.Block, FormationKind.Line,
            FormationKind.Column, FormationKind.Wedge, FormationKind.Circle })
        foreach (int count in new[] { 0, 1, 2, 31, 100, 4000 })
        foreach (int density in new[] { 1, 2, 4 })
        for (int direction = 0; direction < 8; direction++)
        {
            int rows = FormationModel.ResolveAutomaticRows(kind, count);
            var slots = FormationModel.BuildRelativeSlots(kind, count, rows, density, direction);
            Assert.AreEqual(count, slots.Count, $"{kind}, count={count}, direction={direction}");
            Assert.AreEqual(count, slots.Select(p => (p.X, p.Y)).Distinct().Count());
            var repeated = FormationModel.BuildRelativeSlots(kind, count, rows, density, direction);
            CollectionAssert.AreEqual(slots.Select(p => (p.X, p.Y, p.Rank, p.File)).ToArray(),
                repeated.Select(p => (p.X, p.Y, p.Rank, p.File)).ToArray());
        }
    }

    [TestMethod]
    public void ExplicitRowsRemainStraightAtEveryOrientation()
    {
        foreach (FormationKind kind in new[] { FormationKind.Block, FormationKind.Line,
            FormationKind.Column, FormationKind.Wedge })
        foreach (int rows in new[] { 1, 2, 7, 31 })
        for (int direction = 0; direction < 8; direction++)
        {
            var slots = FormationModel.BuildRelativeSlots(kind, 31, rows, 4, direction);
            var ranks = slots.GroupBy(p => p.Rank).ToArray();
            Assert.AreEqual(rows, ranks.Length);
            FormationModel.GetForwardVector(direction, out int forwardX, out int forwardY);
            foreach (var rank in ranks)
                Assert.AreEqual(1, rank.Select(p => p.X * forwardX + p.Y * forwardY).Distinct().Count());
            Assert.AreEqual(FormationModel.ResolveWidthForRows(kind, 31, rows), ranks.Max(r => r.Count()));
        }
    }

    [TestMethod]
    public void ExactReachableDestinationsAreReservedBeforeFallbackAssignment()
    {
        var slots = new[] { new FormationPoint(0, 0, 0, 0), new FormationPoint(1, 0, 0, 1) };
        var candidates = new[] { new FormationPoint(1, 0, 0, 0), new FormationPoint(-1, 0, 0, 0) };
        CollectionAssert.AreEqual(new[] { 1, 0 },
            FormationPlacementModel.SelectCandidates(0, 0, slots, candidates, new[] { 1, 2 }));
        CollectionAssert.AreEqual(new[] { 1 }, FormationPlacementModel.SelectCandidates(0, 0,
            new[] { slots[0] }, candidates, new[] { 9, 3 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => FormationPlacementModel.SelectCandidates(
            0, 0, slots, Array.Empty<FormationPoint>(), Array.Empty<int>()));
    }

    [TestMethod]
    public void RolePlacementIsBijectiveAndDisabledPlacementPreservesOrder()
    {
        var slots = FormationModel.BuildRelativeSlots(FormationKind.Block, 25, 5, 2, 0);
        var units = Enumerable.Range(1, 25).Select(id => new FormationUnit(id, 0,
            id <= 16 ? FormationRole.Front : FormationRole.Rear)).ToArray();
        CollectionAssert.AreEqual(Enumerable.Range(0, 25).ToArray(),
            FormationModel.AssignSlotsByRole(units, slots, RangedPlacementMode.Off));
        foreach (var mode in new[] { RangedPlacementMode.Rear, RangedPlacementMode.Center })
        {
            int[] assignment = FormationModel.AssignSlotsByRole(units, slots, mode);
            Assert.AreEqual(25, assignment.Distinct().Count());
            Assert.IsTrue(assignment.All(index => index >= 0 && index < slots.Count));
            CollectionAssert.AreEqual(assignment, FormationModel.AssignSlotsByRole(units, slots, mode));
        }
        int[] centered = FormationModel.AssignSlotsByRole(units, slots, RangedPlacementMode.Center);
        double frontRadius = units.Select((unit, i) => (unit, slot: slots[centered[i]]))
            .Where(entry => entry.unit.Role == FormationRole.Front)
            .Average(entry => (long)entry.slot.X * entry.slot.X + (long)entry.slot.Y * entry.slot.Y);
        double rearRadius = units.Select((unit, i) => (unit, slot: slots[centered[i]]))
            .Where(entry => entry.unit.Role == FormationRole.Rear)
            .Average(entry => (long)entry.slot.X * entry.slot.X + (long)entry.slot.Y * entry.slot.Y);
        Assert.IsTrue(frontRadius > rearRadius);
    }

    [TestMethod]
    public void WheelIsSampledOncePerFrameAndAccumulatesFractionalSteps()
    {
        var gesture = new FormationGestureState(FormationKind.Block, 100, 0, rememberedRows: 5);
        gesture.ApplyWheel(0.25f, 1);
        gesture.ApplyWheel(0.25f, 1);
        gesture.ApplyWheel(0.75f, 2);
        Assert.AreEqual(4, gesture.Rows);
        gesture.ApplyWheel(float.NaN, 3);
        gesture.ApplyWheel(float.PositiveInfinity, 4);
        Assert.AreEqual(4, gesture.Rows);
        gesture.ApplyWheel(10000f, 5);
        Assert.AreEqual(1, gesture.Rows);
        gesture.ApplyWheel(-10000f, 6);
        Assert.AreEqual(100, gesture.Rows);
        gesture.UpdateDirection(20, -20, 2);
        gesture.UpdateDirection(0, 0, 2);
        Assert.AreEqual(1, gesture.Direction);
        Assert.IsTrue(gesture.ExplicitDirection);
    }
}
