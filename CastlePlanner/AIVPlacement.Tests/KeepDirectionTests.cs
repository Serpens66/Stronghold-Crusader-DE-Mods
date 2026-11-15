using CastlePlanner;
using SHCDESE.Interop;

internal static class KeepDirectionTests
{
    internal static void Run()
    {
        var keep = new GameBuilding { N00012042_2 = 2, r_SpriteVariationIndex = 15 };
        if (KeepDirectionReader.Configure(false) || KeepDirectionReader.TryRead(in keep, out _))
            throw new Exception("An unverified native layout must not expose a Keep direction.");
        if (!KeepDirectionReader.Configure(true))
            throw new Exception("Installed GameBuilding no longer matches the audited Keep field layout.");
        foreach (ushort direction in new ushort[] { 0, 2, 4, 6, 15 })
        {
            keep.N00012042_2 = direction;
            // Vanilla's Keep allocator supplies 15 even when geometry faces East/North/West.
            foreach (ushort sprite in new ushort[] { 15, 0, 2, 4, 6, 99 })
            {
                keep.r_SpriteVariationIndex = sprite;
                int expected = direction == 15 ? 0 : direction;
                if (!KeepDirectionReader.TryRead(in keep, out int actual) || actual != expected)
                    throw new Exception($"Keep direction {direction} was confused with sprite {sprite}: {actual}.");
            }
        }
        foreach (ushort invalid in new ushort[] { 1, 3, 5, 7, 14, 16, ushort.MaxValue })
        {
            keep.N00012042_2 = invalid;
            keep.r_SpriteVariationIndex = 0;
            if (KeepDirectionReader.TryRead(in keep, out _))
                throw new Exception($"Unsupported Keep direction {invalid} was accepted through its sprite.");
        }
    }
}
