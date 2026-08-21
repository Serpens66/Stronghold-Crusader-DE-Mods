using System;

namespace BugfixesAndQoL
{
    public static class MultiplayerGameSpeedPolicy
    {
        public const int ProtocolVersion = 1;
        public const int IncreaseAction = 1;
        public const int DecreaseAction = 2;
        public const int SetAction = 3;
        public const int FastIncreaseAction = 4;
        public const int FastDecreaseAction = 5;
        public const int MinimumSpeed = 10;
        public const int MaximumSpeed = 5000;
        public const int SpeedStep = 5;
        public const int FastSpeedStep = 25;

        public static bool TryResolve(
            int currentSpeed,
            int action,
            int requestedTarget,
            out int resolvedSpeed) =>
            TryResolve(currentSpeed, action, requestedTarget, MaximumSpeed, out resolvedSpeed);

        public static bool TryResolve(
            int currentSpeed,
            int action,
            int requestedTarget,
            int maximumSpeed,
            out int resolvedSpeed)
        {
            maximumSpeed = NormalizeMaximumSpeed(maximumSpeed);
            resolvedSpeed = NormalizeObservedSpeed(currentSpeed, maximumSpeed);
            switch (action)
            {
                case IncreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Min(maximumSpeed, resolvedSpeed + SpeedStep);
                    return true;
                case DecreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Max(MinimumSpeed, resolvedSpeed - SpeedStep);
                    return true;
                case FastIncreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Min(maximumSpeed, resolvedSpeed + FastSpeedStep);
                    return true;
                case FastDecreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Max(MinimumSpeed, resolvedSpeed - FastSpeedStep);
                    return true;
                case SetAction:
                    if (!IsValidTarget(requestedTarget, maximumSpeed))
                        return false;
                    resolvedSpeed = requestedTarget;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryResolvePacket(
            int currentSpeed,
            int protocolVersion,
            int action,
            int requestedTarget,
            out int resolvedSpeed) =>
            TryResolvePacket(currentSpeed, protocolVersion, action, requestedTarget, MaximumSpeed, out resolvedSpeed);

        public static bool TryResolvePacket(
            int currentSpeed,
            int protocolVersion,
            int action,
            int requestedTarget,
            int maximumSpeed,
            out int resolvedSpeed)
        {
            if (protocolVersion != ProtocolVersion)
            {
                resolvedSpeed = NormalizeObservedSpeed(currentSpeed, maximumSpeed);
                return false;
            }

            return TryResolve(currentSpeed, action, requestedTarget, maximumSpeed, out resolvedSpeed);
        }

        public static bool IsValidTarget(int speed) =>
            IsValidTarget(speed, MaximumSpeed);

        public static bool IsValidTarget(int speed, int maximumSpeed) =>
            speed >= MinimumSpeed &&
            speed <= NormalizeMaximumSpeed(maximumSpeed) &&
            (speed % SpeedStep == 0 || speed == NormalizeMaximumSpeed(maximumSpeed));

        public static int NormalizeObservedSpeed(int speed) =>
            NormalizeObservedSpeed(speed, MaximumSpeed);

        public static int NormalizeObservedSpeed(int speed, int maximumSpeed)
        {
            maximumSpeed = NormalizeMaximumSpeed(maximumSpeed);
            int clamped = Math.Max(MinimumSpeed, Math.Min(maximumSpeed, speed));
            if (clamped == maximumSpeed)
                return maximumSpeed;

            int steps = (int)Math.Round(
                (double)clamped / SpeedStep,
                MidpointRounding.AwayFromZero);
            return Math.Min(maximumSpeed, steps * SpeedStep);
        }

        private static int NormalizeMaximumSpeed(int maximumSpeed) =>
            Math.Max(MinimumSpeed, maximumSpeed);
    }
}
