using System;

namespace CrenelatedWallTest
{
    internal static class AvailabilityPattern
    {
        internal static int FindUniqueExecutable(ReadOnlySpan<byte> image, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || image.Length < 0x40 || Read16(image, 0) != 0x5A4D) return -1;
            int pe = Read32(image, 0x3C);
            if (pe < 0 || pe > image.Length - 24 || Read32(image, pe) != 0x4550) return -1;
            int count = Read16(image, pe + 6), first = pe + 24 + Read16(image, pe + 20), found = -1;
            if (count > 96 || first < 0 || first > image.Length - count * 40) return -1;
            for (int section = 0; section < count; section++)
            {
                int row = first + section * 40;
                if ((Read32(image, row + 36) & 0x20000000) == 0) continue;
                int begin = Read32(image, row + 12), size = Read32(image, row + 8);
                if (begin < 0 || size < pattern.Length || begin > image.Length - size) continue;
                for (int offset = begin; offset <= begin + size - pattern.Length; offset++)
                {
                    if (image[offset] != pattern[0]) continue;
                    int i = 1; while (i < pattern.Length && image[offset + i] == pattern[i]) i++;
                    if (i != pattern.Length) continue;
                    if (found >= 0) return -1; found = offset;
                }
            }
            return found;
        }
        private static int Read16(ReadOnlySpan<byte> image, int p) => image[p] | image[p + 1] << 8;
        private static int Read32(ReadOnlySpan<byte> image, int p) => image[p] | image[p + 1] << 8 | image[p + 2] << 16 | image[p + 3] << 24;
    }
}
