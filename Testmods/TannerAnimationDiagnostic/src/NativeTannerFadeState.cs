using System;
using System.Collections.Generic;

namespace TannerAnimationDiagnostic
{
    internal readonly struct NativeFadeSample
    {
        internal readonly uint VanillaTransparency;
        internal readonly uint EffectiveTransparency;
        internal readonly int ElapsedTicks;
        internal readonly bool Started;
        internal readonly bool Ended;
        internal readonly bool Changed;

        internal NativeFadeSample(uint vanillaTransparency, uint effectiveTransparency,
            int elapsedTicks, bool started, bool ended, bool changed)
        {
            VanillaTransparency = vanillaTransparency;
            EffectiveTransparency = effectiveTransparency;
            ElapsedTicks = elapsedTicks;
            Started = started;
            Ended = ended;
            Changed = changed;
        }
    }

    // Packet values are selected before Vanilla queues them for GameMap.
    internal sealed class NativeTannerFadeState
    {
        internal const int FadeTicks = 30;
        internal readonly uint Generation;
        internal readonly long StartTick;
        internal uint LastEffectiveTransparency;
        internal uint LastVanillaTransparency;

        internal NativeTannerFadeState(uint generation, long startTick)
        {
            Generation = generation;
            StartTick = startTick;
            LastEffectiveTransparency = uint.MaxValue;
            LastVanillaTransparency = uint.MaxValue;
        }

        internal int ElapsedTicks(long currentTick) =>
            (int)Math.Min(FadeTicks, Math.Max(0L, currentTick - StartTick));

        internal uint EffectiveTransparency(long currentTick)
        {
            int elapsed = ElapsedTicks(currentTick);
            return (uint)(32 - Math.Min(32, (elapsed * 32 + FadeTicks / 2) / FadeTicks));
        }
    }

    internal sealed class NativeTannerFadeTracker
    {
        private readonly Dictionary<int, NativeTannerFadeState> active =
            new Dictionary<int, NativeTannerFadeState>();

        internal NativeFadeSample Observe(int buildingId, uint generation, int image,
            uint vanillaTransparency, long simulationTick)
        {
            bool ended = false;
            if (active.TryGetValue(buildingId, out NativeTannerFadeState state) &&
                state.Generation != generation)
            {
                active.Remove(buildingId);
                state = null;
                ended = true;
            }

            bool initialImage = image >= 1 && image <= 25;
            bool initialAlpha = vanillaTransparency == 30 || vanillaTransparency == 31;
            if (!initialImage || !initialAlpha)
            {
                if (state != null)
                {
                    active.Remove(buildingId);
                    ended = true;
                }
                return new NativeFadeSample(vanillaTransparency, vanillaTransparency,
                    state?.ElapsedTicks(simulationTick) ?? 0, false, ended, false);
            }

            bool started = false;
            if (state == null && ((image == 12 && vanillaTransparency == 31) ||
                vanillaTransparency == 30))
            {
                state = new NativeTannerFadeState(generation, simulationTick);
                active[buildingId] = state;
                started = true;
            }
            if (state == null)
                return new NativeFadeSample(vanillaTransparency, vanillaTransparency,
                    0, false, ended, false);

            uint effective = state.EffectiveTransparency(simulationTick);
            bool changed = started || effective != state.LastEffectiveTransparency ||
                vanillaTransparency != state.LastVanillaTransparency;
            state.LastEffectiveTransparency = effective;
            state.LastVanillaTransparency = vanillaTransparency;
            return new NativeFadeSample(vanillaTransparency, effective,
                state.ElapsedTicks(simulationTick), started, ended, changed);
        }

        internal void Clear() => active.Clear();
    }
}
