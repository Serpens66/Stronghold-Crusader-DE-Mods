using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal readonly struct LargeMoveMarkerPoint
    {
        public LargeMoveMarkerPoint(MoveTargetCoordinate coordinate, bool active)
        {
            Coordinate = coordinate;
            Active = active;
        }

        public MoveTargetCoordinate Coordinate { get; }
        public bool Active { get; }
    }

    internal sealed class LargeMoveTargetMarkerRenderer
    {
        private readonly ManualLogSource log;
        private readonly object sync = new object();
        private readonly List<GameObject> rowObjects = new List<GameObject>();
        private readonly List<Mesh> rowMeshes = new List<Mesh>();
        private readonly List<RowRenderBatch> rowBatches = new List<RowRenderBatch>();
        private List<LargeMoveMarkerPoint> pending = new List<LargeMoveMarkerPoint>();
        private List<LargeMoveMarkerPoint> rendered = new List<LargeMoveMarkerPoint>();
        private GameObject root;
        private Texture2D markerTexture;
        private Material markerMaterial;
        private int pendingGeneration;
        private int renderedGeneration = -1;
        private int renderedRotation = int.MinValue;
        private bool installed;
        private bool failed;
        private bool failureLogged;

        public LargeMoveTargetMarkerRenderer(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool ReplacementAvailable => installed && !failed;

        public void Install()
        {
            if (installed)
                return;
            installed = true;
            Application.onBeforeRender += OnBeforeRender;
        }

        public void SetMarkers(IReadOnlyList<LargeMoveMarkerPoint> markers)
        {
            var copy = markers == null
                ? new List<LargeMoveMarkerPoint>()
                : new List<LargeMoveMarkerPoint>(markers);
            copy.Sort((left, right) => left.Coordinate.CompareTo(right.Coordinate));
            lock (sync)
            {
                pending = copy;
                pendingGeneration++;
            }
        }

        public void Shutdown()
        {
            if (!installed)
                return;
            installed = false;
            Application.onBeforeRender -= OnBeforeRender;
            ClearRowMeshes();
            if (root != null)
                UnityEngine.Object.Destroy(root);
            root = null;
        }

        private void OnBeforeRender()
        {
            if (!installed || failed)
                return;
            try
            {
                List<LargeMoveMarkerPoint> snapshot = null;
                int generation;
                lock (sync)
                {
                    generation = pendingGeneration;
                    if (generation != renderedGeneration)
                        snapshot = new List<LargeMoveMarkerPoint>(pending);
                }

                int rotation = GameMap.instance == null
                    ? int.MinValue
                    : (int)GameMap.instance.CurrentRotation();
                if (snapshot == null && rotation == renderedRotation)
                    return;
                if (snapshot == null)
                    snapshot = new List<LargeMoveMarkerPoint>(rendered);

                RenderSnapshot(snapshot, rotation);
                rendered = snapshot;
                renderedGeneration = generation;
                renderedRotation = rotation;
            }
            catch (Exception exception)
            {
                failed = true;
                ClearRowMeshes();
                if (!failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"MOVE_TARGET_MARKER_RENDER_FAIL_OPEN: Vanilla markers retained; {exception}");
                }
            }
        }

        private void RenderSnapshot(IReadOnlyList<LargeMoveMarkerPoint> markers, int rotation)
        {
            if (rotation == renderedRotation && HaveSameTopology(rendered, markers) && rowBatches.Count != 0)
            {
                UpdateMarkerColors(markers);
                return;
            }
            ClearRowMeshes();
            if (markers.Count == 0 || GameMap.instance == null)
                return;

            EnsureResources();
            var rows = new SortedDictionary<int, List<ProjectedMarker>>();
            for (int index = 0; index < markers.Count; index++)
            {
                LargeMoveMarkerPoint marker = markers[index];
                if (!TryProject(marker.Coordinate, out int row, out Vector3 position))
                    continue;
                if (!rows.TryGetValue(row, out List<ProjectedMarker> batch))
                {
                    batch = new List<ProjectedMarker>();
                    rows.Add(row, batch);
                }
                batch.Add(new ProjectedMarker(marker.Coordinate, position, marker.Active));
            }

            foreach (KeyValuePair<int, List<ProjectedMarker>> pair in rows)
                CreateRowMesh(pair.Key, pair.Value);
        }

        private void UpdateMarkerColors(IReadOnlyList<LargeMoveMarkerPoint> markers)
        {
            var activeByCoordinate = new Dictionary<MoveTargetCoordinate, bool>();
            for (int index = 0; index < markers.Count; index++)
                activeByCoordinate[markers[index].Coordinate] = markers[index].Active;
            foreach (RowRenderBatch batch in rowBatches)
            {
                var colors = new Color32[batch.Coordinates.Count * 4];
                for (int index = 0; index < batch.Coordinates.Count; index++)
                {
                    bool active = activeByCoordinate.TryGetValue(batch.Coordinates[index], out bool value) && value;
                    Color32 color = new Color32(255, 255, 255, active ? (byte)255 : (byte)0);
                    int vertex = index * 4;
                    colors[vertex] = color;
                    colors[vertex + 1] = color;
                    colors[vertex + 2] = color;
                    colors[vertex + 3] = color;
                }
                batch.Mesh.colors32 = colors;
            }
        }

        private static bool HaveSameTopology(
            IReadOnlyList<LargeMoveMarkerPoint> left,
            IReadOnlyList<LargeMoveMarkerPoint> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Coordinate.Equals(right[index].Coordinate))
                    return false;
            }
            return true;
        }

        private void EnsureResources()
        {
            if (root == null)
            {
                root = new GameObject("BugfixesAndQoL_LargeMoveTargets");
                UnityEngine.Object.DontDestroyOnLoad(root);
            }
            if (markerTexture == null)
            {
                const int width = 32;
                const int height = 16;
                markerTexture = new Texture2D(width, height, TextureFormat.ARGB32, false)
                {
                    name = "BugfixesAndQoL_LargeMoveTargetMarker",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float distance =
                            Math.Abs((x + 0.5f - width / 2f) / (width / 2f)) +
                            Math.Abs((y + 0.5f - height / 2f) / (height / 2f));
                        if (distance <= 1f)
                            pixels[y * width + x] = new Color(0.18f, 1f, 0.12f, distance > 0.72f ? 0.9f : 0.55f);
                    }
                }
                markerTexture.SetPixels(pixels);
                markerTexture.Apply(false, true);
                UnityEngine.Object.DontDestroyOnLoad(markerTexture);
            }
            if (markerMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                    Shader.Find("Sprites/Default");
                if (shader == null)
                    throw new InvalidOperationException("No compatible unlit sprite shader is available.");
                markerMaterial = new Material(shader)
                {
                    name = "BugfixesAndQoL_LargeMoveTargetMaterial",
                    mainTexture = markerTexture
                };
                UnityEngine.Object.DontDestroyOnLoad(markerMaterial);
            }
        }

        private void CreateRowMesh(int row, IReadOnlyList<ProjectedMarker> markers)
        {
            const float halfWidth = 0.34f;
            const float halfHeight = 0.17f;
            var vertices = new Vector3[markers.Count * 4];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[markers.Count * 6];
            for (int index = 0; index < markers.Count; index++)
            {
                ProjectedMarker marker = markers[index];
                int vertex = index * 4;
                int triangle = index * 6;
                Vector3 center = marker.Position;
                vertices[vertex] = center + new Vector3(-halfWidth, -halfHeight, 0f);
                vertices[vertex + 1] = center + new Vector3(halfWidth, -halfHeight, 0f);
                vertices[vertex + 2] = center + new Vector3(-halfWidth, halfHeight, 0f);
                vertices[vertex + 3] = center + new Vector3(halfWidth, halfHeight, 0f);
                uv[vertex] = new Vector2(0f, 0f);
                uv[vertex + 1] = new Vector2(1f, 0f);
                uv[vertex + 2] = new Vector2(0f, 1f);
                uv[vertex + 3] = new Vector2(1f, 1f);
                byte alpha = marker.Active ? (byte)255 : (byte)0;
                Color32 color = new Color32(255, 255, 255, alpha);
                colors[vertex] = color;
                colors[vertex + 1] = color;
                colors[vertex + 2] = color;
                colors[vertex + 3] = color;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            var mesh = new Mesh
            {
                name = $"BugfixesAndQoL_LargeMoveTargets_Row_{row}",
                vertices = vertices,
                uv = uv,
                colors32 = colors,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            rowMeshes.Add(mesh);
            var rowObject = new GameObject(mesh.name);
            rowObject.transform.SetParent(root.transform, false);
            rowObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = rowObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.sortingOrder = -20000 + row * 49 + 2;
            rowObjects.Add(rowObject);
            var coordinates = new List<MoveTargetCoordinate>(markers.Count);
            for (int index = 0; index < markers.Count; index++)
                coordinates.Add(markers[index].Coordinate);
            rowBatches.Add(new RowRenderBatch(mesh, coordinates));
        }

        private static bool TryProject(
            MoveTargetCoordinate coordinate,
            out int row,
            out Vector3 worldPosition)
        {
            row = 0;
            worldPosition = default;
            GameMap map = GameMap.instance;
            if (map == null)
                return false;
            map.mapGameTileToTilemapCoord(
                coordinate.X,
                coordinate.Y,
                out int tileMapX,
                out int tileMapY);
            GameMapTile mapTile = map.getMapTile(tileMapX, tileMapY);
            if (mapTile == null || mapTile.tilemapRef == null)
                return false;
            var tilePosition = new Vector3Int(tileMapX, tileMapY, 0);
            worldPosition = mapTile.tilemapRef.GetCellCenterWorld(tilePosition);
            Vector3 sortingPosition = map.getSpritePosVector(tileMapX, tileMapY);
            worldPosition.y += mapTile.height;
            worldPosition.z = sortingPosition.z;
            row = tileMapY;
            return true;
        }

        private void ClearRowMeshes()
        {
            for (int index = 0; index < rowObjects.Count; index++)
            {
                if (rowObjects[index] != null)
                    UnityEngine.Object.Destroy(rowObjects[index]);
            }
            rowObjects.Clear();
            for (int index = 0; index < rowMeshes.Count; index++)
            {
                if (rowMeshes[index] != null)
                    UnityEngine.Object.Destroy(rowMeshes[index]);
            }
            rowMeshes.Clear();
            rowBatches.Clear();
        }

        private readonly struct ProjectedMarker
        {
            public ProjectedMarker(MoveTargetCoordinate coordinate, Vector3 position, bool active)
            {
                Coordinate = coordinate;
                Position = position;
                Active = active;
            }

            public MoveTargetCoordinate Coordinate { get; }
            public Vector3 Position { get; }
            public bool Active { get; }
        }

        private sealed class RowRenderBatch
        {
            public RowRenderBatch(Mesh mesh, List<MoveTargetCoordinate> coordinates)
            {
                Mesh = mesh;
                Coordinates = coordinates;
            }

            public Mesh Mesh { get; }
            public List<MoveTargetCoordinate> Coordinates { get; }
        }
    }
}
