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
Require(TrailBuffer.Opacity(0, 0) == 0, "zero copies are transparent");
Require(TrailBuffer.Opacity(0, 1) == 0.6875f && TrailBuffer.Opacity(1, 1) == 0, "a single copy uses the middle Vanilla opacity");
for (int length = 2; length < TrailBuffer.Lifetime; length++)
{
    Require(TrailBuffer.Opacity(0, length) == 1f && TrailBuffer.Opacity((uint)(length - 1), length) == 0.34375f,
        "short trails use the same endpoints as Vanilla");
    Require(TrailBuffer.Opacity((uint)length, length) == 0, "the short fade ends at the selected history limit");
    for (uint age = 1; age + 1 < length; age++)
    {
        float opacity = TrailBuffer.Opacity(age, length);
        Require(TrailBuffer.Opacity(age - 1, length) - opacity < opacity - TrailBuffer.Opacity(age + 1, length),
            "short trails fade faster toward the tail");
    }
}
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
for (int length = 0; length <= TrailBuffer.Lifetime; length++)
{
    history.Clear();
    uint current = 2;
    for (int age = TrailBuffer.Lifetime; age >= 0; age--)
        history.Append(new TrailBuffer.Sample { Tick = unchecked(current - (uint)age) });
    history.Expire(current, length);
    Require(history.Count == length, $"the selected history length {length} applies across tick wrap");
    if (length > 0)
        Require(history.Get(length - 1).Tick == current, "the newest sample is always kept");
}
Console.WriteLine("PASS alpha, expiry, history wrapping, and maximum native flight workload");
