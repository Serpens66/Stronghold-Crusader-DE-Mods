using System;
using System.Diagnostics;
using System.Globalization;

namespace TannerAnimationDiagnostic
{
    internal readonly struct VisualSample
    {
        internal readonly int Image;
        internal readonly int RawTransparency;
        internal readonly float ActualAlpha;
        internal readonly bool Visible;
        internal readonly int AiState;
        internal readonly uint AnimationGroup;
        internal readonly uint AnimationFrame;

        internal VisualSample(int image, int rawTransparency, float actualAlpha, bool visible,
            int aiState, uint animationGroup, uint animationFrame)
        {
            Image = image;
            RawTransparency = rawTransparency;
            ActualAlpha = actualAlpha;
            Visible = visible;
            AiState = aiState;
            AnimationGroup = animationGroup;
            AnimationFrame = animationFrame;
        }

        internal int AlphaKey => float.IsNaN(ActualAlpha) ? int.MinValue :
            (int)Math.Round(ActualAlpha * 1024f, MidpointRounding.AwayFromZero);
    }

    // The frame and alpha clocks advance independently: changing frames must not end an alpha span.
    internal sealed class VisualTimeline
    {
        private readonly string identity;
        private readonly Action<string> emit;
        private bool started;
        private VisualSample last;
        private long frameSince;
        private long alphaSince;

        internal VisualTimeline(string identity, Action<string> emit)
        {
            this.identity = identity;
            this.emit = emit;
        }

        internal void Observe(long now, VisualSample sample)
        {
            if (!started)
            {
                started = true;
                last = sample;
                frameSince = now;
                alphaSince = now;
                emit($"{identity} START {Describe(sample)}");
                emit($"{identity} ALPHA_BEGIN raw={sample.RawTransparency} actual={Alpha(sample.ActualAlpha)} visible={sample.Visible}");
                return;
            }

            if (sample.RawTransparency != last.RawTransparency || sample.AlphaKey != last.AlphaKey)
            {
                emit($"{identity} ALPHA_END raw={last.RawTransparency} actual={Alpha(last.ActualAlpha)} duration_ms={Ms(now - alphaSince)}");
                alphaSince = now;
                emit($"{identity} ALPHA_BEGIN raw={sample.RawTransparency} actual={Alpha(sample.ActualAlpha)} visible={sample.Visible}");
            }
            if (sample.Image != last.Image)
            {
                emit($"{identity} FRAME old={last.Image} new={sample.Image} prior_duration_ms={Ms(now - frameSince)} {Describe(sample)}");
                frameSince = now;
            }
            if (sample.Visible != last.Visible)
                emit($"{identity} {(sample.Visible ? "VISIBLE" : "HIDDEN")} {Describe(sample)}");
            if (sample.AiState != last.AiState || sample.AnimationGroup != last.AnimationGroup)
                emit($"{identity} STATE ai={last.AiState}->{sample.AiState} group={last.AnimationGroup}->{sample.AnimationGroup} frame={sample.AnimationFrame}");
            last = sample;
        }

        internal void Close(long now, string reason)
        {
            if (!started)
                return;
            emit($"{identity} ALPHA_END raw={last.RawTransparency} actual={Alpha(last.ActualAlpha)} duration_ms={Ms(now - alphaSince)} reason={reason}");
            emit($"{identity} END last_image={last.Image} frame_duration_ms={Ms(now - frameSince)} reason={reason}");
            started = false;
        }

        private static string Describe(VisualSample sample) =>
            $"image={sample.Image} raw={sample.RawTransparency} actual={Alpha(sample.ActualAlpha)} visible={sample.Visible} ai={sample.AiState} group={sample.AnimationGroup} native_frame={sample.AnimationFrame}";

        private static string Alpha(float value) => float.IsNaN(value) ? "unavailable" :
            value.ToString("F6", CultureInfo.InvariantCulture);

        private static string Ms(long ticks) =>
            (ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);
    }
}
