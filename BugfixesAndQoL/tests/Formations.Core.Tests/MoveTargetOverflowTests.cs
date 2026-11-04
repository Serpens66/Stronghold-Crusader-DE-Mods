using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BugfixesAndQoL.Formations.Tests;

[TestClass]
public class MoveTargetOverflowTests
{
    [TestMethod]
    public void FailureStreakCountsIndependentAttemptsAndRecoversAfterSuccess()
    {
        var policy = new FormationFailurePolicy();
        var error = new InvalidOperationException("temporary preview failure");
        Assert.IsFalse(policy.RecordFailure("preview", error, 1));
        Assert.IsFalse(policy.RecordFailure("preview", error, 1));
        policy.RecordSuccess(1);
        Assert.AreEqual(1, policy.ConsecutiveFailures);
        policy.RecordSuccess(2);
        Assert.AreEqual(0, policy.ConsecutiveFailures);
        Assert.IsFalse(policy.RecordFailure("preview", error, 3));
        Assert.IsFalse(policy.RecordFailure("preview", error, 4));
        Assert.IsTrue(policy.RecordFailure("preview", error, 5));
        Assert.IsTrue(new FormationFailurePolicy().RecordFailure("native",
            new FormationNativeContractException("invalid hook contract"), 1));
        Assert.IsFalse(policy.RecordFailure("other-stage", error, 6));
    }

    [TestMethod]
    public void AddReportsTheActualRejectionAndAFullBufferStillAcceptsDuplicates()
    {
        var buffer = new LargeMoveTargetOverflowBuffer();
        Assert.AreEqual(MoveMarkerAddResult.InvalidTile, buffer.Add(107, 82, 12, 6, -1, 2));
        Assert.AreEqual(MoveMarkerAddResult.Added, buffer.Add(107, 82, 12, 6, 0, 2));
        for (int tile = 1; tile < LargeMoveTargetOverflowModel.MaximumOverflowMarkers; tile++)
            buffer.Add(107, 82, 12, 6, tile, 2);
        Assert.AreEqual(MoveMarkerAddResult.CapacityReached, buffer.Add(107, 82, 12, 6, 5000, 2));
        Assert.AreEqual(MoveMarkerAddResult.Duplicate, buffer.Add(107, 82, 12, 6, 0, 2));
        buffer.Clear();
        Assert.AreEqual(MoveMarkerAddResult.Added, buffer.Add(107, 82, 12, 6, 5000, 2));
    }

    [TestMethod]
    public void NativeDrawBoundaryLeavesTheReservedRecordUntouched()
    {
        Assert.IsFalse(LargeMoveTargetOverflowModel.IsRejectedByFullVanillaList(249));
        Assert.IsTrue(LargeMoveTargetOverflowModel.IsRejectedByFullVanillaList(250));
    }

    [TestMethod]
    public void MarkerRecognitionAcceptsOnlyVanillaMoveSpritesAndMaskedFlags()
    {
        for (int sprite = 0x52; sprite <= 0x59; sprite++)
        {
            Assert.IsTrue(LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0x6B, sprite, 0xC, 6, 2));
            Assert.IsTrue(LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0x6B, sprite, 0xC, 6, 0x40002));
        }
        foreach (var marker in new[] {
            (0x6B, 0x51, 0xC, 6, 2), (0x6B, 0x5A, 0xC, 6, 2),
            (0xAC, 0x142, 0x12, -1, 0xA0022), (0x6C, 0x52, 0xC, 6, 2),
            (0x6B, 0x52, 0xD, 6, 2), (0x6B, 0x52, 0xC, 7, 2), (0x6B, 0x52, 0xC, 6, 3) })
            Assert.IsFalse(LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(
                marker.Item1, marker.Item2, marker.Item3, marker.Item4, marker.Item5));
    }

    [TestMethod]
    [DataRow(248, 0)]
    [DataRow(249, 0)]
    [DataRow(250, 1)]
    [DataRow(251, 2)]
    [DataRow(1000, 751)]
    [DataRow(4000, 3751)]
    public void OnlyMarkersBeyondTheVanillaListEnterOverflow(int requested, int expected)
    {
        Assert.AreEqual(expected, LargeMoveTargetOverflowModel.GetOverflowCount(requested));
        var buffer = new LargeMoveTargetOverflowBuffer();
        for (int index = 0; index < expected; index++)
            Assert.IsTrue(buffer.TryAdd(0x6B, 0x52 + index % 8, 0xC, 6, index, 2));
        Assert.AreEqual(expected, buffer.Count);
    }

    [TestMethod]
    public void DuplicateTileCategoryAndSpriteRetainsTheFirstRecordAndReverseDrawOrder()
    {
        var buffer = new LargeMoveTargetOverflowBuffer();
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 0xC, 6, 42, 2));
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 9, 99, 42, 0x40002));
        Assert.AreEqual(1, buffer.Count);
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x53, 0xC, 6, 42, 2));
        Assert.AreEqual(2, buffer.Count);
        var head = buffer.GetRecord(buffer.GetHead(42));
        Assert.AreEqual(0x53, head.SpriteId);
        var first = buffer.GetRecord(head.Next);
        Assert.AreEqual(0x52, first.SpriteId);
        Assert.AreEqual(0xC, first.Layer);
        Assert.AreEqual(6, first.VerticalOffset);
        Assert.AreEqual(2, first.Flags);
        Assert.AreEqual(0, first.Next);
    }

    [TestMethod]
    public void ClearRetiresEveryTouchedTileAndAllowsFreshFrames()
    {
        var buffer = new LargeMoveTargetOverflowBuffer();
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 0xC, 6, 42, 2));
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x53, 0xC, 6, 42, 2));
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x54, 0xC, 6, 99, 2));
        buffer.Clear();
        Assert.AreEqual(0, buffer.Count);
        Assert.AreEqual(0, buffer.GetHead(42));
        Assert.AreEqual(0, buffer.GetHead(99));
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x54, 0xC, 6, 100, 2));
        Assert.AreEqual(1, buffer.Count);
        Assert.AreEqual(0, buffer.GetHead(42));
        Assert.AreEqual(0, buffer.GetHead(99));
        buffer.Clear();
        buffer.Clear();
        Assert.AreEqual(0, buffer.Count);
        Assert.AreEqual(0, buffer.GetHead(100));
    }

    [TestMethod]
    public void CapacityAndTileBoundsRejectNewRecordsWithoutDiscardingDuplicates()
    {
        var buffer = new LargeMoveTargetOverflowBuffer();
        int capacity = LargeMoveTargetOverflowModel.MaximumOverflowMarkers;
        Assert.AreEqual(LargeMoveTargetOverflowModel.NativeMode8IdentityCapacity,
            LargeMoveTargetOverflowModel.FirstSyntheticIdentity + capacity);
        for (int index = 0; index < capacity; index++)
            Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 0xC, 6, index, 2));
        Assert.IsFalse(buffer.TryAdd(0x6B, 0x52, 0xC, 6, capacity, 2));
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 0xC, 6, 0, 2));
        Assert.IsFalse(buffer.TryAdd(0x6B, 0x52, 0xC, 6, -1, 2));
        Assert.IsFalse(buffer.TryAdd(0x6B, 0x52, 0xC, 6, LargeMoveTargetOverflowModel.NativeTileCount, 2));
        Assert.AreEqual(0, buffer.GetHead(-1));
        Assert.AreEqual(0, buffer.GetHead(LargeMoveTargetOverflowModel.NativeTileCount));
        Assert.AreEqual(capacity, buffer.Count);
        buffer.Clear();
        Assert.IsTrue(buffer.TryAdd(0x6B, 0x52, 0xC, 6, LargeMoveTargetOverflowModel.NativeTileCount - 1, 2));
    }
}
