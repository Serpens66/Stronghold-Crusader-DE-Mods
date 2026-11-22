using System;
using System.Numerics;
using ProjectileTrailsTest;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var history = new TrailBuffer();
var identity = new object();
var layer = BatchBudget.Key(-5, -1234);
Require(!TrailBuffer.IsBodySlot(0) && !TrailBuffer.IsBodySlot(10001) &&
    !TrailBuffer.IsBodySlot(6000) && TrailBuffer.IsBodySlot(5999), "body slot boundaries");
Require(!history.Observe(1, identity, 1, layer, Vector3.Zero), "first observation");
Require(history.Observe(1, identity, 1, layer, Vector3.UnitX), "continuous movement");
Require(Math.Abs(history.Get(0).Tail.X - (1f - TrailBuffer.TrailLength)) < 0.0001f, "length clamp");
Require(history.Remove(1) && !history.HasSegments && history.Count == 0, "retire visible segment");
Require(!history.Observe(1, identity, 1, layer, Vector3.UnitY), "delete followed by same slot and identity");
Require(!history.Observe(1, new object(), 1, layer, Vector3.Zero), "new identity in same slot");
history.Observe(1, identity, 1, layer, Vector3.Zero);
history.Observe(1, identity, 1, layer, Vector3.UnitX);
Require(history.Observe(1, identity, 2, layer, Vector3.UnitY) && !history.HasSegments, "type change");
Require(!history.Observe(1, identity, 2, layer, Vector3.One * 10) && !history.HasSegments, "position jump");
history.Observe(1, identity, 2, layer, Vector3.One * 10 + Vector3.UnitY);
Require(history.HasSegments, "track again after teleport");
history.Observe(2, identity, 2, layer, Vector3.Zero);
Require(history.Remove(1) && history.Count == 1, "remove from a dense active list");
Require(history.Observe(2, identity, 2, layer, Vector3.UnitX) && history.HasSegments, "moved dense index remains usable");
history.Clear();
Require(!history.Observe(2, identity, 2, layer, Vector3.Zero) && !history.HasSegments, "clear at view boundary");

var budget = new BatchBudget();
for (int i = 0; i < BatchBudget.GroupLimit; i++)
    Require(budget.TryAdd(BatchBudget.Key(-5, -1234 + i), out int group) && group == i, "exact sort buckets");
Require(!budget.TryAdd(BatchBudget.Key(-4, -1234), out _), "different layer exceeds the group budget");
for (int i = BatchBudget.GroupLimit; i < BatchBudget.SegmentLimit; i++)
    Require(budget.TryAdd(layer, out int group) && group == 0, "reuse existing group beyond group cap");
Require(!budget.TryAdd(layer, out _), "segment cap");
budget.Clear();
Require(budget.TryAdd(layer, out var next) && next == 0 && budget.Count == 1, "reset render budget");

static void Publish(TrailBuffer history, BatchBudget budget, object identity, long sort)
{
    budget.Clear();
    for (int id = 1; id <= BatchBudget.SegmentLimit; id++)
    {
        history.Observe(id, identity, 1, sort, Vector3.Zero);
        history.Observe(id, identity, 1, sort, Vector3.UnitX);
        budget.TryAdd(sort, out _);
        history.Remove(id);
    }
}
Publish(history, budget, identity, layer);
long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
Publish(history, budget, identity, layer);
Require(GC.GetAllocatedBytesForCurrentThread() == allocatedBefore, "warmed history and budget must not allocate");
Console.WriteLine("PASS projectile slot lifetime, discontinuities, and both render budgets");
