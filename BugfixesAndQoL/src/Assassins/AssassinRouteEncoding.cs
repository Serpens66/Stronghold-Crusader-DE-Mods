using System;

namespace BugfixesAndQoL
{
    internal static class AssassinRouteEncoding
    {
        private static readonly byte[] Directions = { 7, 0, 1, 6, 255, 2, 5, 4, 3 };
        internal static int GetDirection(int dx, int dy)
        {
            if ((uint)(dx + 1) > 2 || (uint)(dy + 1) > 2) return -1;
            int direction = Directions[(dy + 1) * 3 + dx + 1];
            return direction <= 7 ? direction : -1;
        }
        internal static byte[] EncodeTargetFirst(int[] nodes, int width)
        {
            if (nodes == null || nodes.Length < 2 || nodes.Length > 2001 || width < 1) return null;
            var bytes = new byte[nodes.Length / 2];
            for (int i = nodes.Length - 1, at = 0; i > 0; i--, at++)
            {
                if (nodes[i] < 0 || nodes[i - 1] < 0) return null;
                int x = nodes[i - 1] % width - nodes[i] % width;
                int y = nodes[i - 1] / width - nodes[i] / width;
                int direction = GetDirection(x, y);
                if (direction < 0) return null;
                bytes[at / 2] |= (byte)(direction << ((at & 1) * 4));
            }
            return bytes;
        }
    }
}
