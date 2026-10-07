using System;

namespace APIShared
{
    // A synchronous F4930 frame, never a command-global or cross-unit route cache.
    internal sealed class AssassinRouteHandoff
    {
        [ThreadStatic] private static AssassinRouteHandoff current;
        private readonly AssassinRouteHandoff previous;
        private readonly IntPtr context;
        private readonly int startX, startY, targetX, targetY, player;
        private readonly Func<bool> identityValid;
        private readonly Func<byte[], int, int> publish;
        private byte[] bytes;
        private int directions;
        private Func<bool> routeValid;
        internal static bool HasFrame => current != null;

        internal AssassinRouteHandoff(IntPtr context, int startX, int startY,
            int targetX, int targetY, int player, Func<bool> identityValid,
            Func<byte[], int, int> publish)
        {
            previous = current;
            this.context = context; this.startX = startX; this.startY = startY;
            this.targetX = targetX; this.targetY = targetY; this.player = player;
            this.identityValid = identityValid; this.publish = publish;
            current = this;
        }

        internal static bool Stage(IntPtr context, int startX, int startY,
            int targetX, int targetY, int player, byte[] bytes, int directions,
            Func<bool> routeValid)
        {
            AssassinRouteHandoff frame = current;
            if (frame == null || frame.identityValid == null || frame.publish == null ||
                frame.context != context || frame.startX != startX || frame.startY != startY ||
                frame.targetX != targetX || frame.targetY != targetY || frame.player != player ||
                bytes == null || directions < 1 || directions > 2000 ||
                bytes.Length != (directions + 1) / 2 || routeValid == null) return false;
            for (int i = 0; i < directions; i++)
                if (((bytes[i / 2] >> ((i & 1) * 4)) & 15) > 7) return false;
            if (!frame.identityValid() || !routeValid()) return false;
            frame.bytes = (byte[])bytes.Clone(); frame.directions = directions;
            frame.routeValid = routeValid;
            return true;
        }

        internal int Complete(int originalResult)
        {
            if (!ReferenceEquals(current, this) || bytes == null ||
                !identityValid() || !routeValid() || !identityValid()) return originalResult;
            byte[] prepared = bytes;
            bytes = null;
            return publish(prepared, directions);
        }

        internal void Leave()
        {
            if (!ReferenceEquals(current, this)) throw new InvalidOperationException("Assassin route frame mismatch.");
            current = previous;
            bytes = null; routeValid = null;
        }
    }
}
