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
        // TEMP_GATE_ROUTE_ACCEPTANCE: guard outcome of the existing call, not an extra probe.
        [ThreadStatic] internal static string TemporaryRequestReason;
        private static bool Decline(string reason)
        { if (TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver) TemporaryRequestReason = reason; return false; }
        private static void Report(string result)
        {
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver)) return;
            UnitCommands.UnitCommandPathRuntime.ReportTemporaryAssassinStage("handoff", result,
                "source=single-unit,requestReason=" + TemporaryRequestReason);
        }
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
            if (frame == null) return Decline("missing-output-context");
            if (frame.identityValid == null || frame.publish == null) return Decline("incomplete-output-context");
            if (frame.context != context || frame.startX != startX || frame.startY != startY ||
                frame.targetX != targetX || frame.targetY != targetY) return Decline("request-mismatch");
            if (frame.controlPlayer < 0 || frame.speedDelay < 0) return Decline("invalid-control-or-speed");
            if (!frame.identityValid()) return Decline("invalid-identity");
            if (TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver) TemporaryRequestReason = "matched";
            controlPlayer = frame.controlPlayer; speedDelay = frame.speedDelay;
            return true;
        }

        internal static bool Stage(IntPtr context, int startX, int startY,
            int targetX, int targetY, int player, byte[] bytes, int directions,
            Func<bool> routeValid, Action<long, bool> completion = null)
        {
            AssassinRouteHandoff frame = current;
            if (frame == null) return Decline("missing-output-context");
            if (frame.identityValid == null || frame.publish == null) return Decline("incomplete-output-context");
            if (frame.context != context || frame.startX != startX || frame.startY != startY ||
                frame.targetX != targetX || frame.targetY != targetY || frame.player != player) return Decline("request-mismatch");
            if (bytes == null || directions < 1 || directions > 2000 ||
                bytes.Length != (directions + 1) / 2 || routeValid == null) return Decline("invalid-route-shape");
            for (int i = 0; i < directions; i++)
                if (((bytes[i / 2] >> ((i & 1) * 4)) & 15) > 7) return Decline("invalid-route-direction");
            if (!frame.identityValid()) return Decline("invalid-identity");
            if (!routeValid()) return Decline("route-validation-failed");
            frame.bytes = (byte[])bytes.Clone(); frame.directions = directions;
            frame.routeValid = routeValid;
            frame.completion = completion;
            Report("staged");
            return true;
        }

        internal int Complete(int originalResult)
        {
            if (!ReferenceEquals(current, this) || bytes == null) return originalResult;
            long started = completion == null ? 0 : Stopwatch.GetTimestamp();
            bool published = false;
            try
            {
                if (!identityValid()) { Report("discarded-identity"); return originalResult; }
                if (!routeValid()) { Report("discarded-route-validation"); return originalResult; }
                if (!identityValid()) { Report("discarded-identity-after-validation"); return originalResult; }
                byte[] prepared = bytes;
                bytes = null;
                int result = publish(prepared, directions);
                published = true;
                Report("published");
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
