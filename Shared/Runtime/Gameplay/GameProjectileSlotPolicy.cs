using System;
using SHCDESE.API;
using SHCDESE.Interop;

namespace Shared
{
    public readonly struct GameProjectileSlotLayout
    {
        internal GameProjectileSlotLayout(
            int exposedSpanLength,
            int firstLiveSpanIndex,
            int exclusiveUpperBound)
        {
            ExposedSpanLength = exposedSpanLength;
            FirstLiveSpanIndex = firstLiveSpanIndex;
            ExclusiveUpperBound = exclusiveUpperBound;
        }

        public int ExposedSpanLength { get; }
        public int FirstLiveSpanIndex { get; }
        public int ExclusiveUpperBound { get; }
        public int LiveCount => ExclusiveUpperBound - 1;

        public bool IsAddressableId(int projectileId)
        {
            return TryGetSpanIndex(projectileId, out _);
        }

        public bool TryGetSpanIndex(int projectileId, out int spanIndex)
        {
            spanIndex = -1;
            if (projectileId <= 0 || projectileId >= ExclusiveUpperBound)
                return false;

            int candidate = FirstLiveSpanIndex + projectileId - 1;
            if ((uint)candidate >= (uint)ExposedSpanLength)
                return false;

            spanIndex = candidate;
            return true;
        }
    }

    // SHCDESE-WORKAROUND(2.7.1-projectile-slot-view): 2.7.1 exposes its native
    // manager header as span slot 0 and memory beyond the native projectile bound.
    // This adapter also accepts the corrected live-slot view, so it may remain after
    // the upstream fix. Remove it once all supported SE versions expose live ID 1 at
    // span index 0 and enforce the native bound, normally with a later planned release.
    public static unsafe class GameProjectileSlotPolicy
    {
        private const int LegacyExclusiveUpperBoundOffset = 0x04;

        public static bool TryResolve(
            Span<GameProjectile> exposedSpan,
            out GameProjectileSlotLayout layout)
        {
            layout = default;
            if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(
                    1,
                    out GameProjectile* projectileIdOne))
            {
                return false;
            }

            return TryResolve(exposedSpan, projectileIdOne, out layout);
        }

        public static bool TryResolve(
            Span<GameProjectile> exposedSpan,
            GameProjectile* projectileIdOne,
            out GameProjectileSlotLayout layout)
        {
            layout = default;
            if (exposedSpan.Length <= 0 ||
                exposedSpan.Length == int.MaxValue ||
                projectileIdOne == null)
            {
                return false;
            }

            fixed (GameProjectile* exposedStart = exposedSpan)
            {
                if (projectileIdOne == exposedStart)
                {
                    layout = new GameProjectileSlotLayout(
                        exposedSpan.Length,
                        firstLiveSpanIndex: 0,
                        exclusiveUpperBound: exposedSpan.Length + 1);
                    return true;
                }

                if (exposedSpan.Length < 2 || projectileIdOne != exposedStart + 1)
                    return false;

                uint nativeExclusiveUpperBound =
                    *(uint*)((byte*)exposedStart + LegacyExclusiveUpperBoundOffset);
                if (nativeExclusiveUpperBound < 2 ||
                    nativeExclusiveUpperBound > (uint)exposedSpan.Length ||
                    nativeExclusiveUpperBound > int.MaxValue)
                {
                    return false;
                }

                layout = new GameProjectileSlotLayout(
                    exposedSpan.Length,
                    firstLiveSpanIndex: 1,
                    exclusiveUpperBound: checked((int)nativeExclusiveUpperBound));
                return true;
            }
        }
    }
}
