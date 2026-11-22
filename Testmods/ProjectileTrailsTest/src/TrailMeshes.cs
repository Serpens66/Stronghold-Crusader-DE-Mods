using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectileTrailsTest
{
    internal sealed class TrailMeshes
    {
        private const float HalfWidth = 0.018f;
        private static readonly Color32 Transparent = new Color32(255, 239, 196, 0);
        private static readonly Color32 Lit = new Color32(255, 239, 196, 145);
        private const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontRecalculateBounds;
        private static readonly VertexAttributeDescriptor[] Layout = {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            internal Vector3 Position;
            internal Color32 Color;
            internal Vector2 Uv;
            internal Vertex(Vector3 position, Color32 color)
            { Position = position; Color = color; Uv = new Vector2(0.5f, 0.5f); }
        }

        private sealed class MeshGroup
        {
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal int Capacity;
        }

        private readonly BatchBudget budget = new BatchBudget();
        private readonly MeshGroup[] groups = new MeshGroup[BatchBudget.GroupLimit];
        private readonly int[] first = new int[BatchBudget.GroupLimit];
        private readonly int[] next = new int[BatchBudget.SegmentLimit];
        private readonly long[] keys = new long[BatchBudget.GroupLimit];
        private readonly TrailBuffer.Trail[] segments = new TrailBuffer.Trail[BatchBudget.SegmentLimit];
        private readonly Vertex[] vertices = new Vertex[BatchBudget.SegmentLimit * 4];
        private readonly ushort[] triangles = new ushort[BatchBudget.SegmentLimit * 6];
        private readonly Plane[] planes = new Plane[6];
        private GameObject root;
        private Material material;
        private int visibleGroupCount;

        internal TrailMeshes()
        {
            for (int i = 0; i < BatchBudget.SegmentLimit; i++)
            {
                int v = i * 4;
                int t = i * 6;
                triangles[t] = (ushort)v;
                triangles[t + 1] = (ushort)(v + 2);
                triangles[t + 2] = (ushort)(v + 1);
                triangles[t + 3] = (ushort)(v + 2);
                triangles[t + 4] = (ushort)(v + 3);
                triangles[t + 5] = (ushort)(v + 1);
            }
        }

        internal int Draw(TrailBuffer history, Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            budget.Clear();
            for (int i = 0; i < history.Count && budget.Count < BatchBudget.SegmentLimit; i++)
            {
                TrailBuffer.Trail trail = history.Get(i);
                if (!trail.HasSegment || !Visible(trail)) continue;
                int segment = budget.Count;
                int previousGroupCount = budget.GroupCount;
                if (!budget.TryAdd(trail.SortKey, out int group)) continue;
                if (group == previousGroupCount)
                {
                    first[group] = -1;
                    keys[group] = trail.SortKey;
                }
                trail.Identity = null;
                segments[segment] = trail;
                next[segment] = first[group];
                first[group] = segment;
            }
            for (int group = 0; group < budget.GroupCount; group++)
                Upload(group);
            for (int group = budget.GroupCount; group < visibleGroupCount; group++)
                groups[group].Renderer.enabled = false;
            visibleGroupCount = budget.GroupCount;
            return budget.Count;
        }

        internal void Hide()
        {
            // Sweep all created renderers, including groups enabled just before a failed upload.
            foreach (MeshGroup group in groups)
                if (group != null && group.Renderer != null) group.Renderer.enabled = false;
            visibleGroupCount = 0;
        }

        private bool Visible(TrailBuffer.Trail trail)
        {
            Vector3 from = Position(trail.Tail);
            Vector3 to = Position(trail.Head);
            Bounds bounds = new Bounds(from, Vector3.zero);
            bounds.Encapsulate(to);
            bounds.Expand(2 * HalfWidth);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private void Upload(int index)
        {
            MeshGroup group = groups[index] ?? (groups[index] = Create(index));
            int count = 0;
            Bounds bounds = new Bounds(Position(segments[first[index]].Head), Vector3.zero);
            for (int i = first[index]; i >= 0; i = next[i])
            {
                Vector3 tail = Position(segments[i].Tail);
                Vector3 head = Position(segments[i].Head);
                Vector3 delta = head - tail;
                float length = Mathf.Sqrt(delta.x * delta.x + delta.y * delta.y);
                if (length <= 0.0001f) continue;
                Vector3 side = new Vector3(-delta.y, delta.x, 0) * (HalfWidth / length);
                int v = count++ * 4;
                vertices[v] = new Vertex(tail - side, Transparent);
                vertices[v + 1] = new Vertex(tail + side, Transparent);
                vertices[v + 2] = new Vertex(head - side, Lit);
                vertices[v + 3] = new Vertex(head + side, Lit);
                bounds.Encapsulate(tail);
                bounds.Encapsulate(head);
            }
            if (count == 0)
            {
                group.Renderer.enabled = false;
                return;
            }
            bounds.Expand(2 * HalfWidth);
            if (group.Capacity < count)
            {
                group.Capacity = Mathf.NextPowerOfTwo(count);
                group.Mesh.SetVertexBufferParams(group.Capacity * 4, Layout);
                group.Mesh.SetIndexBufferParams(group.Capacity * 6, IndexFormat.UInt16);
                group.Mesh.SetIndexBufferData(triangles, 0, 0, group.Capacity * 6, UpdateFlags);
                group.Mesh.subMeshCount = 1;
            }
            group.Mesh.SetVertexBufferData(vertices, 0, 0, count * 4, 0, UpdateFlags);
            group.Mesh.SetSubMesh(0, new SubMeshDescriptor(0, count * 6, MeshTopology.Triangles)
                { bounds = bounds, vertexCount = count * 4 }, UpdateFlags);
            group.Mesh.bounds = bounds;
            group.Renderer.sortingLayerID = (int)(keys[index] >> 32);
            group.Renderer.sortingOrder = (int)keys[index];
            group.Renderer.enabled = true;
        }

        private MeshGroup Create(int index)
        {
            if (root == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Sprite shader unavailable.");
                material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
                root = new GameObject("ProjectileTrailsTest");
                UnityEngine.Object.DontDestroyOnLoad(root);
            }
            var child = new GameObject("Flight trail batch");
            child.transform.SetParent(root.transform, false);
            var mesh = new Mesh { name = "Projectile trail batch " + index };
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new MeshGroup { Mesh = mesh, Renderer = renderer };
        }

        private static Vector3 Position(System.Numerics.Vector3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
