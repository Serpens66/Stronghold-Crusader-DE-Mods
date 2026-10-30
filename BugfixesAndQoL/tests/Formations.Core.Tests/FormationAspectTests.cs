using BugfixesAndQoL.UnitCommands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BugfixesAndQoL.Formations.Tests;

[TestClass]
public class FormationAspectTests
{
    private static readonly FormationKind[] Shapes = {
        FormationKind.Block, FormationKind.Line, FormationKind.Column, FormationKind.Wedge };

    [TestMethod]
    public void WidthFormulaMatchesActualGeneratedRanks()
    {
        foreach (var kind in Shapes)
        foreach (int count in Enumerable.Range(1, 80).Concat(new[] { 100, 500, 1000, 10000 }))
        foreach (int rows in count <= 80 ? Enumerable.Range(1, count) : new[] { 1, 2, 3, 37, count / 2, count - 1, count })
        {
            var points = FormationModel.BuildRelativeSlots(kind, count, rows, 1, 0);
            int referenceWidth = points.GroupBy(p => p.Rank).Max(rank => rank.Count());
            Assert.AreEqual(referenceWidth, FormationModel.ResolveWidthForRows(kind, count, rows), $"{kind}/{count}/{rows}");
            Assert.AreEqual(count, points.Count);
        }
    }

    [TestMethod]
    public void BinaryResolverMatchesFullReferenceSearch()
    {
        var ratios = new[] { (1, 1), (4, 1), (1, 4), (7, 3), (3, 7), (1, 10000), (10000, 1) };
        foreach (var kind in Shapes)
        foreach (int count in Enumerable.Range(1, 100).Concat(new[] { 500, 1000, 10000 }))
        foreach (var ratio in ratios)
        {
            var aspect = FormationAspectRatio.Create(ratio.Item1, ratio.Item2);
            int reference = 1;
            double best = double.PositiveInfinity;
            for (int rows = 1; rows <= count; rows++)
            {
                int width = FormationModel.ResolveWidthForRows(kind, count, rows);
                // Independent logarithmic reference gives symmetric relative error;
                // numerical ties retain the earlier (shallower) row count.
                double error = Math.Abs(Math.Log((double)width * aspect.Depth / ((double)rows * aspect.Width)));
                if (error < best - 1e-12) { best = error; reference = rows; }
            }
            Assert.AreEqual(reference, FormationModel.ResolveGeometryRows(kind, count, aspect), $"{kind}/{count}/{aspect}");
        }
    }

    [TestMethod]
    public void SquareResizesWithoutChangingTheDesiredProportions()
    {
        var aspect = FormationAspectRatio.Create(1, 1);
        var gesture = new FormationGestureState(FormationKind.Line, 100, 0, aspect);
        foreach (var example in new[] { (10, 3, 4), (100, 10, 10), (500, 22, 23), (1000, 32, 32) })
        {
            gesture = gesture.WithCount(example.Item1);
            Assert.AreEqual(example.Item2, gesture.Rows);
            Assert.AreEqual(example.Item3, FormationModel.ResolveWidthForRows(FormationKind.Line, example.Item1, gesture.Rows));
            Assert.AreEqual(aspect, gesture.Aspect);
        }
        var extreme = FormationAspectRatio.Create(1, 10000);
        Assert.AreEqual(extreme, new FormationGestureState(FormationKind.Line, 2, 0, extreme).WithCount(10000).Aspect);
        Assert.AreEqual(10000, new FormationGestureState(FormationKind.Line, 10000, 0, extreme).Rows);
    }

    [TestMethod]
    public void AutomaticAndFixedShapesRetainTheirExistingGeometry()
    {
        foreach (var kind in Enum.GetValues<FormationKind>())
        foreach (int count in new[] { 1, 2, 10, 100, 500, 1000, 10000 })
        {
            var gesture = new FormationGestureState(kind, count, 0);
            Assert.AreEqual(FormationModel.ResolveAutomaticRows(kind, count), gesture.Rows);
            if (kind != FormationKind.Circle && kind != FormationKind.Vanilla) continue;
            gesture = new FormationGestureState(kind, count, 0, FormationAspectRatio.Create(17, 3));
            gesture.ApplyWheel(-5, 1);
            Assert.IsTrue(gesture.Aspect.IsAutomatic);
            Assert.AreEqual(FormationModel.ResolveAutomaticRows(kind, count), gesture.Rows);
        }
    }

    [TestMethod]
    public void ParsingIsCanonicalBoundedAndCultureIndependent()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Assert.IsTrue(FormationAspectRatio.TryParse(" 12 : 8 ", out var aspect));
            Assert.AreEqual("3:2", aspect.ToString());
            Assert.IsTrue(FormationAspectRatio.TryParse("AUTO", out aspect));
            Assert.AreEqual("auto", aspect.ToString());
            foreach (string invalid in new[] { "", "NaN", "Infinity", "1,5:1", "0:0", "1:0", "-1:3", "1:10001", "1:2:3" })
            {
                Assert.IsFalse(FormationAspectRatio.TryParse(invalid, out aspect));
                Assert.IsTrue(aspect.IsAutomatic);
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void UnchangedGestureUsesCachedRowsWithoutAllocating()
    {
        var gesture = new FormationGestureState(FormationKind.Wedge, 10000, 0, FormationAspectRatio.Create(3, 2));
        // The loop lives in a warmed, fully optimized helper: a loop inside this test method
        // itself would be measured across tiered-JIT OSR/PGO transitions, which allocate.
        int sum = ReadRows(gesture, 100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        sum += ReadRows(gesture, 10000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
        Assert.IsTrue(sum > 0);
        var singleton = new FormationGestureState(FormationKind.Block, 1, 0, FormationAspectRatio.Create(9, 1));
        singleton.ApplyWheel(10, 1);
        Assert.AreEqual(FormationAspectRatio.Create(9, 1), singleton.Aspect);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static int ReadRows(FormationGestureState gesture, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++) sum += gesture.Rows;
        return sum;
    }
}
