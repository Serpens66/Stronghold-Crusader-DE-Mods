#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private static IEnumerable<AutomationWait> CheckGpuCapacity(AutomationRun run, System.Guid runId)
        {
            bool registered = false;
            foreach (var entry in GameXAMLManagerAPI.Instance.RegisteredModSettings)
                if (ReferenceEquals(entry.ViewModel, settings) && entry.View != null) registered = true;
            Require(registered, "The Extender did not load the modsettings view.");

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
                    next.Tick = unchecked(tick - (uint)(TrailBuffer.Lifetime - 1 - i * TrailBuffer.Lifetime / copies));
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

                meshes.Publish(fixture, current, axisX, axisY);
                Require(meshes.BindingUpdates == 0, "An unchanged draw must reuse its renderer bindings.");
                meshes.Publish(fixture, current, axisX, axisY, lifetime: 5);
                Require(meshes.BindingUpdates == rows, "A new shader lifetime must reach every group.");
                const int maximum = 7000;
                int limited = meshes.Publish(fixture, current, axisX, axisY, maximum: maximum);
                Require(limited == maximum && meshes.UploadBytes == maximum * 24 &&
                    meshes.GetAccepted(0).Tick == tick && meshes.GetAccepted(maximum - 1).Tick == unchecked(tick - 1),
                    "The cap must reserve visible slots for the newest echoes.");
                Require(meshes.Publish(fixture, current, axisX, axisY, maximum: 0) == 0 && meshes.GroupCount == 0,
                    "A zero visible-copy budget must hide all groups.");
                var mirrored = -axisX;
                meshes.Publish(fixture, current, mirrored, axisY, maximum: 0);
                meshes.Publish(fixture, current, mirrored, axisY);
                Require(meshes.BindingUpdates == rows, "A basis changed while hidden must reach visible groups.");
                WriteInfo($"GPU_BUDGET registered={registered} limited={limited} zero={meshes.GroupCount}");
                foreach (var wait in CheckTowerDepth(run, runId, original)) yield return wait;
            }
            finally
            {
                meshes.Hide();
                TrailRenderingEnabled = true;
            }
        }

        private static IEnumerable<AutomationWait> CheckTowerDepth(
            AutomationRun run, System.Guid runId, TrailBuffer.Sample sample)
        {
            var mission = run.CurrentMission;
            var tiles = GameTileManagerAPI.Instance;
            var centre = run.InSimulation(mission, () =>
            {
                // Find an empty 5x5 patch inside the same camera catchment.
                for (int x = 388; x <= 422; x++)
                for (int y = 380; y <= 415; y++)
                {
                    bool clear = true;
                    for (int dx = -2; dx <= 2 && clear; dx++)
                    for (int dy = -2; dy <= 2 && clear; dy++)
                        clear = tiles.IsTileInsideMapBounds(x + dx, y + dy) &&
                            tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(x + dx, y + dy));
                    if (clear) return new Vector2Int(x, y);
                }
                throw new InvalidOperationException("No open tower fixture site on test_green.");
            });
            meshes.Hide();
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            foreach (var wait in PositionCamera(run, centre, Enums.Dircs.North)) yield return wait;
            for (int i = 0; i < 3 && Camera.main.orthographicSize > 9f; i++)
                foreach (var wait in run.ZoomStep(mission, true, _ => { })) yield return wait;
            var map = GameMap.instance;
            map.mapGameTileToTilemapCoord(centre.x, centre.y, out int tx, out int ty);
            var ground = map.getSpritePosVector(tx, ty);
            CameraControls2D.instance.setCameraPos(ground.x, ground.y + 1.5f);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            foreach (var wait in Capture(runId, "tower-ground")) yield return wait;
            int owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            run.InSimulation(mission, () =>
            {
                const eMappers tower = eMappers.MAPPER_TOWER1;
                return GameBuildingManagerAPI.Instance.CreatePrefab(owner, centre.x, centre.y, tower,
                    BuildingScales.GetScale(tower), 0, bIsFree: true, bypassPlacementRules: true);
            });
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 12);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            int towerId = run.InSimulation(mission, () => TowerOnTile(tiles, centre, owner));
            Require(towerId > 0, "The tower prefab did not create an occupied tower tile.");
            foreach (var wait in Capture(runId, "tower-empty")) yield return wait;

            var current = Camera.main;
            var pixel = current.WorldToScreenPoint(ground + Vector3.up * 1.5f);
            int row = map.getRow(tx, ty, ProjectileLayerDelay);
            var fixture = new TrailBuffer();
            uint tick = native.Tick;
            meshes.SetTick(tick);
            foreach (int rowOffset in new[] { -12, 12 })
            {
                fixture.Clear();
                sample.Tick = tick;
                sample.SortKey = TrailBuffer.Key(sortLayer, -20000 + (row + rowOffset) * 49 + 15);
                for (int x = -7; x <= 7; x++)
                for (int y = 0; y < 13; y++)
                {
                    sample.Visibility = 0;
                    var point = ground + new Vector3(x * 0.16f, y * 0.25f, 0f);
                    sample.Position = new System.Numerics.Vector3(point.x, point.y, point.z);
                    fixture.Append(sample);
                }
                Require(meshes.Publish(fixture, current, axisX, axisY) == fixture.Count,
                    "The tower fixture has copies outside the view.");
                foreach (var wait in Capture(runId, rowOffset < 0 ? "tower-behind" : "tower-front")) yield return wait;
            }
            WriteInfo($"TOWER_DEPTH id={towerId} tile={centre.x},{centre.y} pixel={(int)pixel.x},{(int)pixel.y} row={row}");
            meshes.Hide();
        }

        private static unsafe int TowerOnTile(GameTileManagerAPI tiles, Vector2Int tile, int owner)
        {
            int id = tiles.GetTileBuildingId(tiles.GetTileId(tile.x, tile.y));
            if (!BuildingAccess.TryGetById(GameBuildingManagerAPI.Instance, id, out var building, out _) ||
                building->r_PlayerIdOwner != owner) return 0;
            var type = building->r_BuildingType;
            return type == eStructs.STRUCT_TOWER || type == eStructs.STRUCT_TOWER1 ? id : 0;
        }
    }
}
#endif
