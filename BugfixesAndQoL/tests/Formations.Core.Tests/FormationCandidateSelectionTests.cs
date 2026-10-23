using BugfixesAndQoL.UnitCommands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BugfixesAndQoL.Formations.Tests;

// The bucket-grid nearest search in FormationPlacementModel.SelectCandidates must choose exactly the
// candidates of the former full scan (squared distance, then tile ID, then candidate index).
[TestClass]
public class FormationCandidateSelectionTests
{
    // Former implementation, kept here only as the independent reference.
    private static int[] ReferenceSelect(int anchorX, int anchorY, IReadOnlyList<FormationPoint> slots,
        IReadOnlyList<FormationPoint> candidates, IReadOnlyList<int> tileIds)
    {
        var result = new int[slots.Count];
        var used = new bool[candidates.Count];
        var exact = new Dictionary<long, int>();
        for (int index = 0; index < candidates.Count; index++)
            exact[((long)candidates[index].X << 32) | (uint)candidates[index].Y] = index;
        var assigned = new bool[slots.Count];
        for (int index = 0; index < slots.Count; index++)
        {
            long key = ((long)(anchorX + slots[index].X) << 32) | (uint)(anchorY + slots[index].Y);
            if (exact.TryGetValue(key, out int candidate) && !used[candidate])
            { used[candidate] = true; assigned[index] = true; result[index] = candidate; }
        }
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            if (assigned[slotIndex]) continue;
            int desiredX = anchorX + slots[slotIndex].X, desiredY = anchorY + slots[slotIndex].Y;
            long bestDistance = long.MaxValue;
            int best = -1;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (used[candidateIndex]) continue;
                long dx = candidates[candidateIndex].X - desiredX, dy = candidates[candidateIndex].Y - desiredY;
                long distance = dx * dx + dy * dy;
                if (distance < bestDistance ||
                    (distance == bestDistance && (best < 0 || tileIds[candidateIndex] < tileIds[best])))
                { bestDistance = distance; best = candidateIndex; }
            }
            if (best < 0) best = slotIndex % candidates.Count;
            used[best] = true;
            result[slotIndex] = best;
        }
        return result;
    }

    [TestMethod]
    public void BucketGridChoosesTheSameCandidatesAsTheFullScan()
    {
        var random = new Random(4711);
        int cases = 0;
        for (int trial = 0; trial < 1500; trial++)
        {
            int candidateCount = random.Next(1, trial % 10 == 0 ? 2500 : 300);
            int slotCount = random.Next(1, trial % 7 == 0 ? candidateCount + 40 : Math.Min(candidateCount, 400) + 1);
            // Mix of dense clusters (many equal distances), wide spreads and negative coordinates.
            int spread = trial % 3 == 0 ? 6 : trial % 3 == 1 ? 40 : 400;
            int originX = random.Next(-30, 800), originY = random.Next(-30, 800);
            var candidates = new FormationPoint[candidateCount];
            var tileIds = new int[candidateCount];
            for (int i = 0; i < candidateCount; i++)
            {
                candidates[i] = new FormationPoint(originX + random.Next(-spread, spread + 1),
                    originY + random.Next(-spread, spread + 1), 0, 0);
                // Duplicate tile IDs and positions exercise the index tie break.
                tileIds[i] = trial % 5 == 0 ? random.Next(0, 6) : random.Next(0, 1000000);
            }
            int anchorX = originX + random.Next(-spread, spread + 1), anchorY = originY + random.Next(-spread, spread + 1);
            var slots = new FormationPoint[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = new FormationPoint(random.Next(-spread - 20, spread + 21), random.Next(-spread - 20, spread + 21), 0, i);
            CollectionAssert.AreEqual(ReferenceSelect(anchorX, anchorY, slots, candidates, tileIds),
                FormationPlacementModel.SelectCandidates(anchorX, anchorY, slots, candidates, tileIds), "trial " + trial);
            cases++;
        }
        Assert.AreEqual(1500, cases);
    }

    [TestMethod]
    public void LargeSelectionMatchesTheFullScan()
    {
        // Production scale: 400 units, the 8192-candidate cap, a formation partly outside the region.
        var random = new Random(91);
        var candidates = new List<FormationPoint>();
        var tileIds = new List<int>();
        for (int y = 0; y < 128 && candidates.Count < 8192; y++)
            for (int x = 0; x < 128 && candidates.Count < 8192; x++)
                if (random.Next(4) != 0) { candidates.Add(new FormationPoint(300 + x, 300 + y, 0, 0)); tileIds.Add(y * 800 + x); }
        var slots = FormationModel.BuildRelativeSlots(FormationKind.Line, 400, 1, 2, 3);
        foreach ((int ax, int ay) in new[] { (360, 360), (300, 300), (250, 420) })
            CollectionAssert.AreEqual(ReferenceSelect(ax, ay, slots, candidates, tileIds),
                FormationPlacementModel.SelectCandidates(ax, ay, slots, candidates, tileIds), $"anchor {ax},{ay}");
    }
}
