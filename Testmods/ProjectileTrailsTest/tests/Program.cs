using System;
using ProjectileTrailsTest;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

float[] expected = { 1f, 1f, 1f, 0.6875f, 0.6875f, 0.6875f, 0.34375f, 0.34375f, 0.34375f, 0.34375f, 0f };
for (uint age = 0; age < expected.Length; age++)
    Require(TrailBuffer.Opacity(age) == expected[age], "vanilla alpha and terminal slot at age " + age);

Require(TrailBuffer.Opacity(unchecked(2u - uint.MaxValue)) == 0.6875f, "age remains valid over tick wrap");
Require(TrailBuffer.Opacity(unchecked(10u - 11u)) == 0, "future entries are never drawn");
int needed = (TrailBuffer.WideProjectileLimit - 1) * (TrailBuffer.Lifetime + 1);
Require(TrailBuffer.CaptureCapacity >= needed && TrailBuffer.CaptureCapacity / 2 < needed &&
    (TrailBuffer.CaptureCapacity & (TrailBuffer.CaptureCapacity - 1)) == 0,
    "capture storage covers all live sweeps and a concurrent write sweep");

var history = new TrailBuffer();
int perTick = TrailBuffer.WideProjectileLimit - 1;
uint first = uint.MaxValue - 4;
void Add(uint tick)
{
    for (int slot = 1; slot <= perTick; slot++)
        history.Append(new TrailBuffer.Sample { Tick = tick, SourceSlot = slot });
}
for (int sweep = 0; sweep < TrailBuffer.Lifetime; sweep++)
    Add(unchecked(first + (uint)sweep));
Require(history.Count == perTick * TrailBuffer.Lifetime, "the projected history holds every live slot");
Require(!history.Expire(unchecked(first + (uint)TrailBuffer.Lifetime - 1)), "retain the terminal opacity");
long before = GC.GetAllocatedBytesForCurrentThread();
for (int step = TrailBuffer.Lifetime; step < 50; step++)
{
    uint tick = unchecked(first + (uint)step);
    Require(history.Expire(tick), "expire one complete sweep");
    Add(tick);
    Require(history.Count == perTick * TrailBuffer.Lifetime, "reuse the same history storage");
    Require(history.Get(0).SourceSlot == 1 && history.Get(history.Count - 1).Tick == tick, "preserve chronological order");
}
Require(GC.GetAllocatedBytesForCurrentThread() == before, "warmed history must not allocate");
history.Clear();
Add(0);
Require(history.Get(0).Tick == 0 && history.Count == perTick, "reset history at the mission boundary");
Console.WriteLine("PASS alpha, expiry, history wrapping, and maximum native flight workload");
