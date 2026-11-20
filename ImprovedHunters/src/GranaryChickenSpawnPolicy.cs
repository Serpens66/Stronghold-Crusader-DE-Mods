using System;

namespace ImprovedHunters
{
    internal sealed class GranaryChickenSpawnPolicy
    {
        public const int DefaultTargetPerPlayer = 10;
        public const int MinimumTargetPerPlayer = 0;
        public const int MaximumTargetPerPlayer = 100;
        public const int DefaultRespawnSeconds = 30;
        public const int MaximumRespawnSeconds = 300;

        private readonly long[] nextAttempt;
        private bool delayAfterLoad;

        public GranaryChickenSpawnPolicy(int playerCount) => nextAttempt = new long[playerCount];

        public static int ClampTarget(int value) =>
            Math.Max(MinimumTargetPerPlayer, Math.Min(MaximumTargetPerPlayer, value));

        public void Reset(bool fromSave)
        {
            Array.Clear(nextAttempt, 0, nextAttempt.Length);
            delayAfterLoad = fromSave;
        }

        // Holding the target at the current count suppresses creation without fading existing chickens.
        // An allowed attempt consumes the interval even if Vanilla cannot find a suitable building.
        public int TargetForDecision(int playerId, int target, int currentCount, long tick, int intervalTicks)
        {
            if (intervalTicks == 0 || (uint)(playerId - 1) >= (uint)nextAttempt.Length)
                return target;

            ref long due = ref nextAttempt[playerId - 1];
            if (due == 0 && delayAfterLoad)
                due = tick + intervalTicks;
            if (target <= currentCount)
                return target;
            if (tick < due)
                return Math.Max(0, currentCount);

            due = tick + intervalTicks;
            return target;
        }
    }
}
