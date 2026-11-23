using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Axis = System.Numerics.Vector2;

namespace ProjectileTrailsTest
{
    internal sealed class TrailMeshes
    {
        internal const string BundleName = "projectiletrails-shader";
        private const string ShaderPath = "Assets/ProjectileTrails.shader";
        private static readonly int EchoBufferId = Shader.PropertyToID("_SerpTrailEchoes");
        private static readonly int GeometryId = Shader.PropertyToID("_SerpTrailGeometry");
        private static readonly int TickId = Shader.PropertyToID("_SerpTrailTick");
        private static readonly int BeginId = Shader.PropertyToID("_SerpTrailBegin");
        private static readonly int CountId = Shader.PropertyToID("_SerpTrailCount");
        private static readonly int StrideId = Shader.PropertyToID("_SerpTrailVertexStride");
        private static readonly int BasisId = Shader.PropertyToID("_SerpTrailBasis");
        private static readonly int LifetimeId = Shader.PropertyToID("_SerpTrailLifetime");

        [StructLayout(LayoutKind.Sequential)]
        private struct Echo
        {
            internal Vector3 Position;
            internal uint Shape, Tick, VertexCount;
        }

        private sealed class Appearance
        {
            internal int Shape, VertexCount, TextureId;
            internal Vector2 Extent, WorldExtent;
            internal readonly float[] PlaneRadii = new float[6];
            internal Material Material;
        }

        private sealed class Group
        {
            internal readonly MeshFilter Filter;
            internal readonly MeshRenderer Renderer;
            internal int Count, Write, First, VertexStride;
            internal Vector3 Min, Max;
            internal Bounds RenderedBounds;
            internal int BoundFirst, BoundCount, BoundStride;
            internal long BoundRevision = -1;
            internal Mesh Mesh;
            internal Material Material;
            internal long Order;
            internal int Layer;

            internal Group(GameObject root)
            {
                var child = new GameObject("Projectile echo depth group");
                child.transform.SetParent(root.transform, false);
                Filter = child.AddComponent<MeshFilter>();
                Renderer = child.AddComponent<MeshRenderer>();
                Renderer.enabled = false;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.lightProbeUsage = LightProbeUsage.Off;
                Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                Renderer.allowOcclusionWhenDynamic = false;
            }
        }

        private readonly AssetBundle bundle;
        private readonly Shader shader;
        private readonly List<Vector4> geometry = new List<Vector4>();
        private readonly List<Appearance> appearances = new List<Appearance>();
        private readonly Dictionary<(int file, int frame), int> sprites = new Dictionary<(int, int), int>();
        private readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
        private readonly Dictionary<int, Mesh> carriers = new Dictionary<int, Mesh>();
        private readonly Dictionary<(long order, int texture, int layer), int> buckets =
            new Dictionary<(long, int, int), int>();
        private readonly List<Group> groups = new List<Group>();
        private readonly List<(int bucket, int historyIndex)> visible = new List<(int, int)>();
        private TrailBuffer publishedHistory;
        private readonly Plane[] planes = new Plane[6];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Echo[] echoData = Array.Empty<Echo>();
        private GraphicsBuffer echoes, shapes;
        private int uploadedShapes, activeGroups, extentCount, radiusCount, viewEpoch;
        private int boundLifetime;
        private long bindingRevision;
        private Axis boundX, boundY;
        private Camera viewCamera;
        private Matrix4x4 viewMatrix;
        private Axis previousX, previousY;
        private GameObject root;
#if SERP_DEVDIAG
        internal bool MatchLegacyBudget { get; set; }
        internal int BindingUpdates { get; private set; }
#endif
        internal int Accepted => visible.Count;
        internal int GroupCount => activeGroups;
        internal int UploadBytes { get; private set; }
        internal TrailBuffer.Sample GetAccepted(int index) => publishedHistory.Get(visible[index].historyIndex);

        internal TrailMeshes(string path)
        {
            if (SystemInfo.graphicsShaderLevel < 45)
                throw new InvalidOperationException("Projectile trails need Shader Model 4.5 structured buffers.");
            if (Marshal.SizeOf<Echo>() != 24)
                throw new InvalidOperationException("Projectile echo buffer layout differs from the shader.");
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
                throw new InvalidOperationException("Could not load the projectile shader bundle at " + path);
            shader = bundle.LoadAsset<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                bundle.Unload(true);
                throw new InvalidOperationException("The projectile shader is unavailable on " + SystemInfo.graphicsDeviceType + ".");
            }
        }

        internal int GetAppearance(int file, int frame)
        {
            var key = (file, frame);
            if (sprites.TryGetValue(key, out int found)) return found;
            var sprite = SpriteMapping.getBodyImage(file, frame);
            if (sprite == null)
            {
                sprites.Add(key, -1);
                return -1;
            }

            Vector2[] positions = sprite.vertices;
            Vector2[] uv = sprite.uv;
            ushort[] indices = sprite.triangles;
            if (indices.Length == 0 || indices.Length % 3 != 0)
                throw new InvalidOperationException("A projectile sprite has no supported triangle geometry.");
            Texture texture = sprite.texture;
            if (!materials.TryGetValue(texture, out var material))
            {
                material = new Material(shader) { mainTexture = texture, hideFlags = HideFlags.DontSave };
                materials.Add(texture, material);
            }
            Bounds bounds = sprite.bounds;
            var appearance = new Appearance
            {
                Shape = geometry.Count,
                VertexCount = indices.Length,
                Extent = new Vector2(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.y) + bounds.extents.y),
                Material = material,
                TextureId = texture.GetInstanceID()
            };
            foreach (ushort index in indices)
                geometry.Add(new Vector4(positions[index].x, positions[index].y, uv[index].x, uv[index].y));
            int id = appearances.Count;
            appearances.Add(appearance);
            sprites.Add(key, id);
            return id;
        }

        internal void SetTick(uint tick) => Shader.SetGlobalInteger(TickId, unchecked((int)tick));

        internal int Publish(TrailBuffer history, Camera camera, Axis axisX, Axis axisY,
            int lifetime = TrailBuffer.Lifetime, int maximum = TrailBuffer.MaximumVisibleCopies)
        {
            Matrix4x4 matrix = camera.cullingMatrix;
            bool basisChanged = axisX != previousX || axisY != previousY;
            if (camera != viewCamera || !matrix.Equals(viewMatrix) || basisChanged || publishedHistory != history)
            {
                viewCamera = camera;
                viewMatrix = matrix;
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                radiusCount = 0;
                if (viewEpoch == int.MaxValue)
                {
                    for (int i = 0; i < history.Count; i++) history.Get(i).Visibility = 0;
                    viewEpoch = 0;
                }
                viewEpoch++;
            }
            if (basisChanged)
            {
                previousX = axisX;
                previousY = axisY;
                extentCount = 0;
            }
            for (; extentCount < appearances.Count; extentCount++)
            {
                var appearance = appearances[extentCount];
                appearance.WorldExtent = new Vector2(
                    Math.Abs(axisX.X) * appearance.Extent.x + Math.Abs(axisY.X) * appearance.Extent.y,
                    Math.Abs(axisX.Y) * appearance.Extent.x + Math.Abs(axisY.Y) * appearance.Extent.y);
            }
            for (; radiusCount < appearances.Count; radiusCount++)
            {
                var appearance = appearances[radiusCount];
                for (int i = 0; i < planes.Length; i++)
                {
                    Vector3 n = planes[i].normal;
                    appearance.PlaneRadii[i] = Math.Abs(n.x) * appearance.WorldExtent.x +
                        Math.Abs(n.y) * appearance.WorldExtent.y + Math.Abs(n.z) * 0.01f;
                }
            }
            buckets.Clear();
            visible.Clear();
            publishedHistory = history;
            int count = 0;
            UploadBytes = 0;
#if SERP_DEVDIAG
            BindingUpdates = 0;
            if (MatchLegacyBudget) maximum = Math.Min(maximum, 1024);
#endif
            // If capped, prefer current positions and spend remaining slots on older copies.
            bool newestFirst = history.Count > maximum;
            for (int index = 0; index < history.Count && visible.Count < maximum; index++)
            {
                int i = newestFirst ? history.Count - 1 - index : index;
                ref var sample = ref history.Get(i);
                if (sample.Visibility == -viewEpoch) continue;
                var appearance = appearances[sample.Appearance];
                Vector3 position = ToUnity(sample.Position);
                var extent = appearance.WorldExtent;
                if (sample.Visibility != viewEpoch)
                {
                    sample.Visibility = Visible(position, appearance.PlaneRadii) ? viewEpoch : -viewEpoch;
                    if (sample.Visibility < 0) continue;
                }
                var halfSize = new Vector3(extent.x, extent.y, 0.01f);
                Vector3 min = position - halfSize;
                Vector3 max = position + halfSize;
                var key = (sample.SortKey, appearance.TextureId, sample.ObjectLayer);
                if (!buckets.TryGetValue(key, out int bucket))
                {
#if SERP_DEVDIAG
                    if (MatchLegacyBudget && count == 64) continue;
#endif
                    bucket = count++;
                    buckets.Add(key, bucket);
                    Group group = GetGroup(bucket);
                    group.Count = 0;
                    group.VertexStride = 0;
                    group.Min = min;
                    group.Max = max;
                    if (group.Layer != sample.ObjectLayer)
                        group.Renderer.gameObject.layer = group.Layer = sample.ObjectLayer;
                    if (group.Material != appearance.Material || group.Order != sample.SortKey)
                    {
                        group.Order = sample.SortKey;
                        group.Material = appearance.Material;
                        group.Renderer.sortingLayerID = (int)(sample.SortKey >> 32);
                        group.Renderer.sortingOrder = (int)sample.SortKey;
                        group.Renderer.sharedMaterial = appearance.Material;
                    }
                }
                Group entry = groups[bucket];
                entry.Count++;
                entry.VertexStride = Math.Max(entry.VertexStride, appearance.VertexCount);
                entry.Min = Vector3.Min(entry.Min, min);
                entry.Max = Vector3.Max(entry.Max, max);
                visible.Add((bucket, i));
            }

            if (visible.Count != 0)
            {
                int offset = 0;
                for (int i = 0; i < count; i++)
                {
                    Group group = groups[i];
                    group.First = group.Write = offset;
                    offset += group.Count;
                }
                if (echoData.Length < visible.Count) echoData = new Echo[Mathf.NextPowerOfTwo(visible.Count)];
                for (int i = 0; i < visible.Count; i++)
                {
                    // Keep the original blend order within each depth group.
                    var item = visible[newestFirst ? visible.Count - 1 - i : i];
                    ref readonly var sample = ref history.Get(item.historyIndex);
                    var appearance = appearances[sample.Appearance];
                    echoData[groups[item.bucket].Write++] = new Echo
                    {
                        Position = ToUnity(sample.Position), Shape = (uint)appearance.Shape,
                        Tick = sample.Tick, VertexCount = (uint)appearance.VertexCount
                    };
                }

                bool echoesReplaced = EnsureBuffer(ref echoes, visible.Count, 24);
                echoes.SetData(echoData, 0, 0, visible.Count);
                UploadBytes = visible.Count * 24;
                bool shapesReplaced = EnsureBuffer(ref shapes, geometry.Count, 16);
                if (shapesReplaced) uploadedShapes = 0;
                if (uploadedShapes < geometry.Count)
                {
                    int append = geometry.Count - uploadedShapes;
                    shapes.SetData(geometry, uploadedShapes, uploadedShapes, append);
                    UploadBytes += append * 16;
                    uploadedShapes = geometry.Count;
                }
                if (echoesReplaced || shapesReplaced || boundLifetime != lifetime || axisX != boundX || axisY != boundY)
                {
                    bindingRevision++;
                    boundLifetime = lifetime;
                    boundX = axisX;
                    boundY = axisY;
                    properties.SetBuffer(EchoBufferId, echoes);
                    properties.SetBuffer(GeometryId, shapes);
                    properties.SetInteger(LifetimeId, lifetime);
                    properties.SetVector(BasisId, new Vector4(axisX.X, axisX.Y, axisY.X, axisY.Y));
                }
                for (int i = 0; i < count; i++)
                {
                    Group group = groups[i];
                    Mesh mesh = Carrier(checked(group.Count * (group.VertexStride / 3)));
                    if (group.Mesh != mesh) group.Filter.sharedMesh = group.Mesh = mesh;
                    var bounds = new Bounds();
                    bounds.SetMinMax(group.Min, group.Max);
                    if (group.BoundRevision < 0 || !group.RenderedBounds.Equals(bounds))
                        group.Renderer.bounds = group.RenderedBounds = bounds;
                    if (group.BoundRevision != bindingRevision || group.BoundFirst != group.First ||
                        group.BoundCount != group.Count || group.BoundStride != group.VertexStride)
                    {
                        properties.SetInteger(BeginId, group.BoundFirst = group.First);
                        properties.SetInteger(CountId, group.BoundCount = group.Count);
                        properties.SetInteger(StrideId, group.BoundStride = group.VertexStride);
                        group.Renderer.SetPropertyBlock(properties);
                        group.BoundRevision = bindingRevision;
#if SERP_DEVDIAG
                        BindingUpdates++;
#endif
                    }
                    if (i >= activeGroups) group.Renderer.enabled = true;
                }
            }
            DisableGroups(count);
            return visible.Count;
        }

        internal void Hide()
        {
            DisableGroups(0);
            visible.Clear();
            publishedHistory = null;
        }

        private bool Visible(Vector3 position, float[] radii)
        {
            for (int i = 0; i < planes.Length; i++)
                if (Vector3.Dot(planes[i].normal, position) + planes[i].distance < -radii[i]) return false;
            return true;
        }

        private void DisableGroups(int count)
        {
            for (int i = count; i < activeGroups; i++)
                groups[i].Renderer.enabled = false;
            activeGroups = count;
        }

        private Group GetGroup(int index)
        {
            if (index < groups.Count) return groups[index];
            if (root == null)
            {
                root = new GameObject("ProjectileTrailsTest");
                UnityEngine.Object.DontDestroyOnLoad(root);
            }
            var group = new Group(root);
            groups.Add(group);
            return group;
        }

        // One shared carrier for every required draw-length class. Its position stream is
        // ignored. SV_VertexID selects the instance and vertex in the real sprite geometry.
        private Mesh Carrier(int triangles)
        {
            int capacity = Mathf.NextPowerOfTwo(triangles);
            if (carriers.TryGetValue(capacity, out Mesh mesh)) return mesh;
            int vertices = checked(capacity * 3);
            mesh = new Mesh { name = "Projectile echo topology " + capacity, hideFlags = HideFlags.DontSave };
            mesh.SetVertexBufferParams(vertices, new VertexAttributeDescriptor(VertexAttribute.Position));
            mesh.SetIndexBufferParams(vertices, IndexFormat.UInt32);
            var identity = new int[vertices];
            for (int i = 0; i < vertices; i++) identity[i] = i;
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            mesh.SetIndexBufferData(identity, 0, 0, vertices, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertices, MeshTopology.Triangles)
                { vertexCount = vertices }, flags);
            mesh.UploadMeshData(true);
            carriers.Add(capacity, mesh);
            return mesh;
        }

        private static bool EnsureBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.count >= count) return false;
            var next = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(count), stride);
            buffer?.Dispose();
            buffer = next;
            return true;
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 p) => new Vector3(p.X, p.Y, p.Z);
    }
}
