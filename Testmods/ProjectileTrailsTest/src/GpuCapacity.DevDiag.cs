#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using APIShared.Automation;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private static IEnumerable<AutomationWait> CheckGpuCapacity(AutomationRun run, System.Guid runId)
        {
            var mission = run.CurrentMission;
            var centre = new Vector2Int(400, 396);
            foreach (var wait in PositionCamera(run, centre, Enums.Dircs.North)) yield return wait;
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            var volley = run.InSimulation(mission, () => LaunchVolley(centre, 64, lowTrajectory: true));
            Require(volley.arrows > 0 && volley.bolts > 0, "No sprite sources for the GPU capacity fixture.");
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return AutomationWait.Until("game supplied visible afterimage geometry", () => LastSegmentCount > 0);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;

            var original = meshes.GetAccepted(0);
            var current = Camera.main;
            float distance = current.WorldToViewportPoint(new Vector3(original.Position.X, original.Position.Y, original.Position.Z)).z;
            Require(distance > 0, "The GPU capacity chart needs a visible depth.");
            TrailRenderingEnabled = false;
            try
            {
                uint tick = native.Tick;
                var fixture = new TrailBuffer();
                const int rows = 96;
                const int copies = 50000;
                var positions = new Vector3[rows * rows];
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = current.ViewportToWorldPoint(new Vector3(
                        0.2f + 0.6f * (i % rows) / rows, 0.2f + 0.6f * (i / rows) / rows, distance));
                // Only this fixture fabricates positions. The renderer uses the real atlas and shape.
                for (int i = 0; i < copies; i++)
                {
                    Vector3 pos = positions[i % positions.Length];
                    var next = original;
                    next.Tick = tick - (uint)(i % TrailBuffer.Lifetime);
                    next.Position = new System.Numerics.Vector3(pos.x, pos.y, pos.z);
                    next.SortKey = TrailBuffer.Key((int)(original.SortKey >> 32),
                        (int)original.SortKey + 49 * ((i % positions.Length) / rows - rows / 2));
                    fixture.Append(next);
                }
                meshes.SetTick(tick);
                int submitted = meshes.Publish(fixture, current, axisX, axisY);
                Require(submitted == copies && meshes.GroupCount == rows,
                    $"GPU capacity submitted {submitted}/{copies} echoes in {meshes.GroupCount}/{rows} rows.");
                WriteInfo($"GPU_CAPACITY submitted={submitted} groups={meshes.GroupCount} uploadBytes={meshes.UploadBytes}");
                foreach (var wait in Capture(runId, "gpu-capacity")) yield return wait;
            }
            finally
            {
                meshes.Hide();
                TrailRenderingEnabled = true;
            }
        }
    }
}
#endif
