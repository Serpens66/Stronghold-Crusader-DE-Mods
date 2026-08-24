// Feature: Pure one-shot timing rules for a previously observed missing AIV target.
using System;

namespace ExtraFeatures
{
    internal static class AIDefenseRebuildDelayPolicy
    {
        public const int TicksPerSecond = 40;

        public static int BeginMissing(int? existingMissingSinceTick, int nowTick) =>
            existingMissingSinceTick ?? nowTick;

        public static bool IsBlocked(int missingSinceTick, int nowTick, int delaySeconds)
        {
            if (delaySeconds <= 0)
                return false;
            return ElapsedTicks(nowTick, missingSinceTick) < checked(delaySeconds * TicksPerSecond);
        }

        public static int ElapsedTicks(int nowTick, int startTick) =>
            unchecked((int)Math.Min((uint)(nowTick - startTick), int.MaxValue));
    }
}
