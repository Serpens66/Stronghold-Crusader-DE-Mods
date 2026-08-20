using System;

namespace ExtraFeatures
{
    public static class MultiplayerGameSpeedPolicy
    {
        public const int ProtocolVersion = 1;
        public const int IncreaseAction = 1;
        public const int DecreaseAction = 2;
        public const int SetAction = 3;
        public const int MinimumSpeed = 10;
        public const int MaximumSpeed = 90;
        public const int SpeedStep = 5;

        public static bool TryResolve(
            int currentSpeed,
            int action,
            int requestedTarget,
            out int resolvedSpeed)
        {
            resolvedSpeed = NormalizeObservedSpeed(currentSpeed);
            switch (action)
            {
                case IncreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Min(MaximumSpeed, resolvedSpeed + SpeedStep);
                    return true;
                case DecreaseAction:
                    if (requestedTarget != 0)
                        return false;
                    resolvedSpeed = Math.Max(MinimumSpeed, resolvedSpeed - SpeedStep);
                    return true;
                case SetAction:
                    if (!IsValidTarget(requestedTarget))
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
            out int resolvedSpeed)
        {
            if (protocolVersion != ProtocolVersion)
            {
                resolvedSpeed = NormalizeObservedSpeed(currentSpeed);
                return false;
            }

            return TryResolve(currentSpeed, action, requestedTarget, out resolvedSpeed);
        }

        public static bool IsValidTarget(int speed) =>
            speed >= MinimumSpeed &&
            speed <= MaximumSpeed &&
            speed % SpeedStep == 0;

        public static int NormalizeObservedSpeed(int speed)
        {
            int clamped = Math.Max(MinimumSpeed, Math.Min(MaximumSpeed, speed));
            int steps = (int)Math.Round(
                (double)clamped / SpeedStep,
                MidpointRounding.AwayFromZero);
            return steps * SpeedStep;
        }
    }
}
