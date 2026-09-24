using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct NativeBuildingIdentity : IEquatable<NativeBuildingIdentity>
    {
        internal readonly uint GlobalId;
        internal readonly uint OriginTileId;
        internal readonly ushort OwnerId;

        internal NativeBuildingIdentity(uint globalId, uint originTileId, ushort ownerId)
        {
            GlobalId = globalId;
            OriginTileId = originTileId;
            OwnerId = ownerId;
        }

        public bool Equals(NativeBuildingIdentity other) =>
            GlobalId == other.GlobalId && OriginTileId == other.OriginTileId &&
            OwnerId == other.OwnerId;
    }

    internal readonly struct NativeFadeSample
    {
        internal readonly uint VanillaTransparency;
        internal readonly uint EffectiveTransparency;
        internal readonly int ElapsedUpdates;
        internal readonly bool Started;
        internal readonly bool Ended;
        internal readonly bool Changed;
        internal readonly bool Active;

        internal NativeFadeSample(uint vanillaTransparency, uint effectiveTransparency,
            int elapsedUpdates, bool started, bool ended, bool changed, bool active)
        {
            VanillaTransparency = vanillaTransparency;
            EffectiveTransparency = effectiveTransparency;
            ElapsedUpdates = elapsedUpdates;
            Started = started;
            Ended = ended;
            Changed = changed;
            Active = active;
        }
    }

    internal sealed class NativeTannerFadeState
    {
        internal const int FadeUpdates = 64;
        internal readonly NativeBuildingIdentity Identity;
        internal int ElapsedUpdates;
        internal uint LastVanillaTransparency;
        internal uint LastEffectiveTransparency;

        internal NativeTannerFadeState(NativeBuildingIdentity identity, uint firstVanillaTransparency,
            bool freshStart)
        {
            Identity = identity;
            ElapsedUpdates = freshStart ? 0 :
                Math.Min(FadeUpdates, (32 - (int)firstVanillaTransparency) * 2);
            LastVanillaTransparency = firstVanillaTransparency;
            LastEffectiveTransparency = uint.MaxValue;
        }

        internal uint EffectiveTransparency =>
            (uint)(32 - Math.Min(32, ElapsedUpdates / 2));
    }

    internal sealed class NativeTannerFadeTracker
    {
        private readonly Dictionary<int, NativeTannerFadeState> active =
            new Dictionary<int, NativeTannerFadeState>();

        internal static bool ShouldBridgeExitFrame(bool restoredVanilla, int phaseBefore,
            int phaseAfter, int image, uint vanillaTransparency, NativeFadeSample sample) =>
            restoredVanilla && phaseBefore == 5 && phaseAfter == 6 && image == 25 &&
            vanillaTransparency == 30 && sample.Ended && !sample.Active &&
            sample.ElapsedUpdates == NativeTannerFadeState.FadeUpdates;

        internal bool TryGetVanillaTransparency(int buildingId, NativeBuildingIdentity identity,
            int phase, uint currentTransparency, out uint vanillaTransparency)
        {
            if (phase >= 0 && phase <= 5 && active.TryGetValue(buildingId,
                out NativeTannerFadeState state) && state.Identity.Equals(identity))
            {
                if (currentTransparency == state.LastEffectiveTransparency)
                {
                    vanillaTransparency = state.LastVanillaTransparency;
                    return true;
                }
                active.Remove(buildingId);
            }
            vanillaTransparency = 0;
            return false;
        }

        internal NativeFadeSample Observe(int buildingId, NativeBuildingIdentity identity,
            int phase, int image, uint vanillaTransparency)
        {
            bool ended = false;
            if (active.TryGetValue(buildingId, out NativeTannerFadeState state) &&
                !state.Identity.Equals(identity))
            {
                active.Remove(buildingId);
                state = null;
                ended = true;
            }

            bool initialPhase = phase >= 0 && phase <= 5;
            bool initialImage = image >= 1 && image <= 25;
            if (!initialPhase || !initialImage)
            {
                if (state != null)
                {
                    active.Remove(buildingId);
                    ended = true;
                }
                return new NativeFadeSample(vanillaTransparency, vanillaTransparency,
                    state?.ElapsedUpdates ?? 0, false, ended, false, false);
            }

            bool started = false;
            if (state == null && vanillaTransparency <= 32)
            {
                bool freshStart = image == 12 && vanillaTransparency == 31;
                state = new NativeTannerFadeState(identity, vanillaTransparency, freshStart);
                active[buildingId] = state;
                started = true;
            }
            if (state == null)
                return new NativeFadeSample(vanillaTransparency, vanillaTransparency,
                    0, false, ended, false, false);

            if (!started && state.ElapsedUpdates < NativeTannerFadeState.FadeUpdates)
                state.ElapsedUpdates++;
            uint effective = state.EffectiveTransparency;
            bool changed = started || effective != state.LastEffectiveTransparency;
            state.LastVanillaTransparency = vanillaTransparency;
            state.LastEffectiveTransparency = effective;
            return new NativeFadeSample(vanillaTransparency, effective,
                state.ElapsedUpdates, started, ended, changed, true);
        }

        internal bool Remove(int buildingId) => active.Remove(buildingId);
        internal void Clear() => active.Clear();
    }
}
