using System;

namespace BugfixesAndQoL
{
    internal static class AssassinRouteEncoding
    {
        internal static byte[] EncodeTargetFirst(int[] nodes, int width)
        {
            if (nodes == null || nodes.Length < 2 || nodes.Length > 2001 || width < 1) return null;
            var bytes = new byte[nodes.Length / 2];
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            for (int i = nodes.Length - 1, at = 0; i > 0; i--, at++)
            {
                if (nodes[i] < 0 || nodes[i - 1] < 0) return null;
                int x = nodes[i - 1] % width - nodes[i] % width;
                int y = nodes[i - 1] / width - nodes[i] / width;
                int direction = -1;
                for (int d = 0; d < 8; d++) if (x == dx[d] && y == dy[d]) direction = d;
                if (direction < 0) return null;
                bytes[at / 2] |= (byte)(direction << ((at & 1) * 4));
            }
            return bytes;
        }
    }
}
