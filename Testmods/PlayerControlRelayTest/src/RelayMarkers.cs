using System;
using System.Collections.Generic;
using UnityEngine;
using Vectrosity;

namespace PlayerControlRelayTest
{
    internal readonly struct RelayMarker
    {
        internal RelayMarker(float x, float y) { X = x; Y = y; }
        internal float X { get; }
        internal float Y { get; }
    }

    // Vanilla marks only units of the local view, so allied units get a small diamond in
    // screen space, drawn with the same line library as Vanilla's selection rectangle.
    // Unity-thread only. A drawing failure must never affect orders: it disables the markers.
    internal sealed class RelayMarkers
    {
        internal const int MaximumMarkers = 1024;
        private const float Radius = 8f;
        private static readonly Color32 MarkerColor = new Color32(119, 255, 170, 255);
        private readonly Action<string> report;
        private readonly List<Vector2> points = new List<Vector2>(MaximumMarkers * 8);
        private VectorLine line;
        private bool failed;

        internal RelayMarkers(Action<string> report) { this.report = report; }

        internal void Show(RelayMarker[] markers)
        {
            if (failed) return;
            try
            {
                if (markers == null || markers.Length == 0) { Hide(); return; }
                if (line == null)
                {
                    line = new VectorLine("RelaySelection", points, 2f, LineType.Discrete);
                    line.color = MarkerColor;
                }
                points.Clear();
                foreach (var marker in markers)
                {
                    var left = new Vector2(marker.X - Radius, marker.Y);
                    var right = new Vector2(marker.X + Radius, marker.Y);
                    var top = new Vector2(marker.X, marker.Y + Radius);
                    var bottom = new Vector2(marker.X, marker.Y - Radius);
                    points.Add(left); points.Add(top); points.Add(top); points.Add(right);
                    points.Add(right); points.Add(bottom); points.Add(bottom); points.Add(left);
                }
                line.active = true;
                line.Draw();
            }
            catch (Exception error) { Disable(error); }
        }

        internal void Hide()
        {
            if (failed || line == null || !line.active) return;
            try { line.active = false; }
            catch (Exception error) { Disable(error); }
        }

        // The line's canvas object does not outlive a world reliably; the next selection recreates it.
        internal void Reset()
        {
            if (line == null) return;
            try { VectorLine.Destroy(ref line); }
            catch (Exception error) { line = null; Disable(error); }
        }

        private void Disable(Exception error)
        {
            failed = true;
            report("Selection markers disabled: " + error.Message);
        }
    }
}
