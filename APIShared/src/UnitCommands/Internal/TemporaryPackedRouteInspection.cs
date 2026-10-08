// APIShared-owned implementation; independent of workspace Shared helpers.
// Initial provenance: a7888900e, Shared/TemporaryPackedRouteInspection.cs. No automatic synchronization.
// TEMP_GATE_ROUTE_ACCEPTANCE: read-only decoder for the audited low-nibble-first output.
using System;
namespace APIShared.Internal
{
    internal static class TemporaryPackedRouteInspection
    {
        internal static bool TryResolveUnit(long bufferOffset, int stride, int capacity, int contextUnit, out int unitId)
        {
            unitId = 0;
            if (stride <= 0 || bufferOffset <= 0 || bufferOffset % stride != 0 || bufferOffset / stride > capacity) return false;
            int candidate = (int)(bufferOffset / stride);
            if (contextUnit != 0 && candidate != contextUnit) return false;
            unitId = candidate; return true;
        }
        internal static string Decode(ReadOnlySpan<byte> packed, int length, int x, int y, int tx, int ty,
            int[] dx, int[] dy, out int[] directions)
        {
            directions = null;
            if (length <= 0 || packed.Length < (length + 1) / 2 || dx.Length != 8 || dy.Length != 8)
                return "invalid-length";
            var result = new int[length];
            for (int i = 0; i < length; i++)
            {
                int d = (packed[i >> 1] >> ((i & 1) * 4)) & 15;
                if (d > 7 || (uint)x >= 800 || (uint)y >= 800) return "invalid-direction-or-coordinate";
                x += dx[d]; y += dy[d];
                if ((uint)x >= 800 || (uint)y >= 800) return "invalid-coordinate";
                result[i] = d;
            }
            if (x != tx || y != ty) return "partial-endpoint";
            directions = result; return "decoded";
        }
    }
}
