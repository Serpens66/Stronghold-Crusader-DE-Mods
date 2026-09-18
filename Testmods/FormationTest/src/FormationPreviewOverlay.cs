using System;
using UnityEngine;

namespace FormationTest
{
    internal sealed class FormationPreviewOverlay : MonoBehaviour
    {
        private static readonly object Sync = new object();
        private static PreviewSnapshot snapshot = PreviewSnapshot.Empty;
        private static Texture2D frontTexture;
        private static Texture2D protectedTexture;
        private static Texture2D neutralTexture;
        private static Texture2D rearTexture;

        internal static FormationPreviewOverlay CreateProcessLifetimeInstance()
        {
            var root = new GameObject("FormationTest.PreviewOverlay");
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root.AddComponent<FormationPreviewOverlay>();
        }

        internal static void Publish(FormationPreviewPoint[] points)
        {
            lock (Sync)
                snapshot = new PreviewSnapshot(points ?? Array.Empty<FormationPreviewPoint>());
        }

        internal static void Clear()
        {
            lock (Sync)
                snapshot = PreviewSnapshot.Empty;
        }

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;
            PreviewSnapshot current;
            lock (Sync)
                current = snapshot;
            Camera camera = Camera.main;
            if (current.Points.Length == 0 || camera == null || GameMap.instance == null)
                return;

            EnsureTextures();
            for (int index = 0; index < current.Points.Length; index++)
            {
                FormationPreviewPoint point = current.Points[index];
                try
                {
                    System.Numerics.Vector2 world = SHCDESE.CoordinateConverter
                        .ConvertLocalTileToCameraWorld(
                            new System.Numerics.Vector2(point.X, point.Y));
                    Vector3 screen = camera.WorldToScreenPoint(
                        new Vector3(world.X, world.Y, 0f));
                    if (screen.z < 0f)
                        continue;
                    float size = point.Role == FormationRole.Protected ? 10f : 8f;
                    GUI.DrawTexture(
                        new Rect(screen.x - size / 2f,
                            Screen.height - screen.y - size / 2f, size, size),
                        TextureFor(point.Role));
                }
                catch
                {
                    // Preview drawing must never affect the simulation or command path.
                }
            }
        }

        private static Texture2D TextureFor(FormationRole role)
        {
            switch (role)
            {
                case FormationRole.Front: return frontTexture;
                case FormationRole.Protected: return protectedTexture;
                case FormationRole.Rear: return rearTexture;
                default: return neutralTexture;
            }
        }

        private static void EnsureTextures()
        {
            if (frontTexture != null)
                return;
            frontTexture = CreateTexture(new Color(0.9f, 0.22f, 0.15f, 0.9f));
            protectedTexture = CreateTexture(new Color(0.15f, 0.85f, 0.35f, 0.9f));
            neutralTexture = CreateTexture(new Color(0.95f, 0.85f, 0.2f, 0.9f));
            rearTexture = CreateTexture(new Color(0.2f, 0.55f, 1f, 0.9f));
        }

        private static Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private sealed class PreviewSnapshot
        {
            internal static readonly PreviewSnapshot Empty =
                new PreviewSnapshot(Array.Empty<FormationPreviewPoint>());

            internal PreviewSnapshot(FormationPreviewPoint[] points)
            {
                Points = points;
            }

            internal FormationPreviewPoint[] Points { get; }
        }
    }

    internal readonly struct FormationPreviewPoint
    {
        internal FormationPreviewPoint(int x, int y, FormationRole role)
        {
            X = x;
            Y = y;
            Role = role;
        }

        internal int X { get; }
        internal int Y { get; }
        internal FormationRole Role { get; }
    }
}
