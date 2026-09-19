using System;
using BepInEx.Logging;
using UnityEngine;

namespace FormationTest
{
    internal sealed class FormationPreviewOverlay : MonoBehaviour
    {
        private static readonly object Sync = new object();
        private static PreviewSnapshot snapshot = PreviewSnapshot.Empty;
        private static ManualLogSource log;
        private static int nextGeneration;
        private static int diagnosedGeneration;
        private static int diagnosticUpdates;
        private static int diagnosticVisible;
        private static int diagnosticSkipped;
        private static int diagnosticErrors;
        private static Texture2D outlineTexture;
        private static Texture2D frontTexture;
        private static Texture2D protectedTexture;
        private static Texture2D neutralTexture;
        private static Texture2D rearTexture;

        internal static FormationPreviewOverlay CreateProcessLifetimeInstance(ManualLogSource logger)
        {
            log = logger;
            var root = new GameObject("FormationTest.PreviewOverlay");
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root.AddComponent<FormationPreviewOverlay>();
        }

        internal static void Publish(FormationPreviewPoint[] points)
        {
            lock (Sync)
                snapshot = new PreviewSnapshot(
                    points ?? Array.Empty<FormationPreviewPoint>(),
                    unchecked(++nextGeneration));
        }

        internal static void Clear()
        {
            int updates;
            int visible;
            int skipped;
            int errors;
            lock (Sync)
            {
                snapshot = PreviewSnapshot.Empty;
                updates = diagnosticUpdates;
                visible = diagnosticVisible;
                skipped = diagnosticSkipped;
                errors = diagnosticErrors;
                diagnosticUpdates = 0;
                diagnosticVisible = 0;
                diagnosticSkipped = 0;
                diagnosticErrors = 0;
            }
            if (updates <= 0)
                return;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
            {
                try
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"FORMATION_OVERLAY_SUMMARY: updates={updates}, " +
                        $"visible={visible}, skipped={skipped}, errors={errors}.");
                }
                catch
                {
                    // Overlay diagnostics must never affect rendering or command execution.
                }
            });
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
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            int visible = 0;
            int skipped = 0;
            int errors = 0;
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
                    if (float.IsNaN(screen.x) || float.IsNaN(screen.y) ||
                        float.IsInfinity(screen.x) || float.IsInfinity(screen.y) ||
                        screen.x < -16f || screen.x > Screen.width + 16f ||
                        screen.y < -16f || screen.y > Screen.height + 16f)
                    {
                        skipped++;
                        continue;
                    }
                    float size = point.Role == FormationRole.Protected ? 12f : 10f;
                    float outlineSize = size + 4f;
                    float guiY = Screen.height - screen.y;
                    GUI.DrawTexture(
                        new Rect(screen.x - outlineSize / 2f,
                            guiY - outlineSize / 2f, outlineSize, outlineSize),
                        outlineTexture);
                    GUI.DrawTexture(
                        new Rect(screen.x - size / 2f,
                            guiY - size / 2f, size, size),
                        TextureFor(point.Role));
                    visible++;
                }
                catch (Exception)
                {
                    errors++;
                }
            }
            GUI.depth = previousDepth;
            lock (Sync)
            {
                if (diagnosedGeneration != current.Generation &&
                    snapshot.Generation == current.Generation)
                {
                    diagnosedGeneration = current.Generation;
                    diagnosticUpdates++;
                    diagnosticVisible += visible;
                    diagnosticSkipped += skipped;
                    diagnosticErrors += errors;
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
            outlineTexture = CreateTexture(new Color(0.04f, 0.04f, 0.04f, 0.95f));
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
                new PreviewSnapshot(Array.Empty<FormationPreviewPoint>(), 0);

            internal PreviewSnapshot(FormationPreviewPoint[] points, int generation)
            {
                Points = points;
                Generation = generation;
            }

            internal FormationPreviewPoint[] Points { get; }
            internal int Generation { get; }
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
