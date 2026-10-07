using System;
using System.Diagnostics;

namespace APIShared
{
    // A synchronous F4930 frame, never a command-global or cross-unit route cache.
    internal sealed class AssassinRouteHandoff
    {
        [ThreadStatic] private static AssassinRouteHandoff current;
        private readonly AssassinRouteHandoff previous;
        private readonly IntPtr context;
        private readonly int startX, startY, targetX, targetY, player;
        private readonly int controlPlayer, speedDelay;
        private readonly Func<bool> identityValid;
        private readonly Func<byte[], int, int> publish;
        private byte[] bytes;
        private int directions;
        private Func<bool> routeValid;
        private Action<long, bool> completion;
        internal static bool HasFrame => current != null;

        internal AssassinRouteHandoff(IntPtr context, int startX, int startY,
            int targetX, int targetY, int player, Func<bool> identityValid,
            Func<byte[], int, int> publish, int controlPlayer = -1, int speedDelay = -1)
        {
            previous = current;
            this.context = context; this.startX = startX; this.startY = startY;
            this.targetX = targetX; this.targetY = targetY; this.player = player;
            this.identityValid = identityValid; this.publish = publish;
            this.controlPlayer = controlPlayer; this.speedDelay = speedDelay;
            current = this;
        }

        internal static bool TryResolve(IntPtr context, int startX, int startY,
            int targetX, int targetY, out int controlPlayer, out int speedDelay)
        {
            controlPlayer = speedDelay = -1;
            AssassinRouteHandoff frame = current;
            if (frame == null || frame.identityValid == null || frame.publish == null ||
                frame.context != context || frame.startX != startX || frame.startY != startY ||
                frame.targetX != targetX || frame.targetY != targetY || frame.controlPlayer < 0 ||
                frame.speedDelay < 0 || !frame.identityValid()) return false;
            controlPlayer = frame.controlPlayer; speedDelay = frame.speedDelay;
            return true;
        }

        internal static bool Stage(IntPtr context, int startX, int startY,
            int targetX, int targetY, int player, byte[] bytes, int directions,
            Func<bool> routeValid, Action<long, bool> completion = null)
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
            frame.completion = completion;
            return true;
        }

        internal int Complete(int originalResult)
        {
            if (!ReferenceEquals(current, this) || bytes == null) return originalResult;
            long started = completion == null ? 0 : Stopwatch.GetTimestamp();
            bool published = false;
            try
            {
                if (!identityValid() || !routeValid() || !identityValid()) return originalResult;
                byte[] prepared = bytes;
                bytes = null;
                int result = publish(prepared, directions);
                published = true;
                return result;
            }
            finally
            {
                try { completion?.Invoke(Stopwatch.GetTimestamp() - started, published); }
                catch { /* Measurements cannot alter a native result or a published route. */ }
            }
        }

        internal void Leave()
        {
            if (!ReferenceEquals(current, this)) throw new InvalidOperationException("Assassin route frame mismatch.");
            current = previous;
            bytes = null; routeValid = null; completion = null;
        }
    }
}
