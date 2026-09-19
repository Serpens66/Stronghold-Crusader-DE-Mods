using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Noesis;
using SHCDESE.API;
using UnityEngine;

namespace FormationTest
{
    internal static class FormationPreviewOverlay
    {
        private const float PointSize = 11f;
        private const float ProtectedPointSize = 13f;
        private static readonly object Sync = new object();
        private static readonly List<Ellipse> PointPool = new List<Ellipse>();
        private static readonly Line[] ArrowLines = new Line[3];
        private static PreviewSnapshot snapshot = PreviewSnapshot.Empty;
        private static ManualLogSource log;
        private static Canvas canvas;
        private static int nextGeneration;
        private static int renderedGeneration;
        private static bool missingHostLogged;
        private static bool renderFailureLogged;
        private static SolidColorBrush outlineBrush;
        private static SolidColorBrush frontBrush;
        private static SolidColorBrush protectedBrush;
        private static SolidColorBrush neutralBrush;
        private static SolidColorBrush rearBrush;
        private static SolidColorBrush arrowBrush;

        internal static void Initialize(ManualLogSource logger) => log = logger;

        internal static void Publish(FormationPreviewPoint[] points, FormationDirectionIndicator direction)
        {
            lock (Sync)
                snapshot = new PreviewSnapshot(points ?? Array.Empty<FormationPreviewPoint>(), direction, unchecked(++nextGeneration));
            Refresh();
        }

        internal static void Clear()
        {
            lock (Sync)
                snapshot = PreviewSnapshot.Empty;
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(HideAll);
        }

        internal static void Refresh()
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(Render);
        }

        private static void Render()
        {
            PreviewSnapshot current;
            lock (Sync)
                current = snapshot;
            if (current.Points.Length == 0)
            {
                HideAll();
                return;
            }

            try
            {
                if (!TryResolveCanvas(out Canvas host))
                    return;
                Camera camera = ResolveCamera();
                if (camera == null || host.ActualWidth <= 0f || host.ActualHeight <= 0f)
                    return;

                EnsureBrushes();
                EnsurePointPool(host, current.Points.Length);
                int visible = 0;
                int skipped = 0;
                for (int index = 0; index < current.Points.Length; index++)
                {
                    Ellipse ellipse = PointPool[index];
                    FormationPreviewPoint point = current.Points[index];
                    if (!TryProject(camera, host, point.X, point.Y, out float x, out float y))
                    {
                        ellipse.Visibility = Visibility.Collapsed;
                        skipped++;
                        continue;
                    }
                    float size = point.Role == FormationRole.Protected ? ProtectedPointSize : PointSize;
                    ellipse.Width = size;
                    ellipse.Height = size;
                    ellipse.Fill = BrushFor(point.Role);
                    ellipse.Visibility = Visibility.Visible;
                    Canvas.SetLeft(ellipse, x - size * 0.5f);
                    Canvas.SetTop(ellipse, y - size * 0.5f);
                    visible++;
                }
                for (int index = current.Points.Length; index < PointPool.Count; index++)
                    PointPool[index].Visibility = Visibility.Collapsed;

                RenderArrow(camera, host, current.Direction);
                if (renderedGeneration != current.Generation)
                {
                    renderedGeneration = current.Generation;
                    Shared.DebugLogHelper.LogDebug(log,
                        $"FORMATION_OVERLAY_SUMMARY: generation={current.Generation}, visible={visible}, skipped={skipped}, arrow={current.Direction.Visible}.");
                }
            }
            catch (Exception exception)
            {
                HideAll();
                if (renderFailureLogged)
                    return;
                renderFailureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Formation Noesis overlay disabled after render error: {exception}");
            }
        }

        private static bool TryResolveCanvas(out Canvas host)
        {
            host = GameXAMLManagerAPI.Instance?.FindGlobalElement("FormationTestPreviewCanvas") as Canvas;
            if (host == null)
            {
                if (!missingHostLogged)
                {
                    missingHostLogged = true;
                    Shared.DebugLogHelper.LogWarning(log, "FORMATION_OVERLAY_SKIPPED: Noesis host FormationTestPreviewCanvas is unavailable.");
                }
                HideAll();
                return false;
            }
            missingHostLogged = false;
            if (ReferenceEquals(canvas, host))
                return true;

            canvas = host;
            PointPool.Clear();
            for (int index = 0; index < ArrowLines.Length; index++)
            {
                ArrowLines[index] = new Line { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
                Panel.SetZIndex(ArrowLines[index], 2);
                canvas.Children.Add(ArrowLines[index]);
            }
            return true;
        }

        private static void EnsurePointPool(Canvas host, int count)
        {
            while (PointPool.Count < count)
            {
                var ellipse = new Ellipse
                {
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false,
                    StrokeThickness = 2f,
                    Stroke = outlineBrush
                };
                Panel.SetZIndex(ellipse, 1);
                host.Children.Add(ellipse);
                PointPool.Add(ellipse);
            }
        }

        private static void RenderArrow(Camera camera, Canvas host, FormationDirectionIndicator direction)
        {
            if (!direction.Visible ||
                !TryProject(camera, host, direction.StartX, direction.StartY, out float sx, out float sy) ||
                !TryProject(camera, host, direction.EndX, direction.EndY, out float ex, out float ey))
            {
                HideArrow();
                return;
            }
            float dx = ex - sx;
            float dy = ey - sy;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 1f)
            {
                HideArrow();
                return;
            }
            dx /= length;
            dy /= length;
            float bx = ex - dx * 10f;
            float by = ey - dy * 10f;
            float px = -dy * 6f;
            float py = dx * 6f;
            SetLine(ArrowLines[0], sx, sy, ex, ey);
            SetLine(ArrowLines[1], ex, ey, bx + px, by + py);
            SetLine(ArrowLines[2], ex, ey, bx - px, by - py);
        }

        private static void SetLine(Line line, float x1, float y1, float x2, float y2)
        {
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Stroke = arrowBrush;
            line.StrokeThickness = 4f;
            line.Visibility = Visibility.Visible;
        }

        private static Camera ResolveCamera()
        {
            CameraControls2D controls = CameraControls2D.instance;
            Camera result = controls == null ? null : controls.GetComponent<Camera>();
            return result != null ? result : Camera.main;
        }

        private static bool TryProject(Camera camera, Canvas host, int tileX, int tileY, out float x, out float y)
        {
            System.Numerics.Vector2 world = SHCDESE.CoordinateConverter.ConvertLocalTileToCameraWorld(new System.Numerics.Vector2(tileX, tileY));
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(world.X, world.Y, 0f));
            x = screen.x * (host.ActualWidth / Screen.width);
            y = (Screen.height - screen.y) * (host.ActualHeight / Screen.height);
            return !float.IsNaN(x) && !float.IsNaN(y) && !float.IsInfinity(x) && !float.IsInfinity(y) &&
                x >= -24f && x <= host.ActualWidth + 24f && y >= -24f && y <= host.ActualHeight + 24f;
        }

        private static void EnsureBrushes()
        {
            if (frontBrush != null)
                return;
            outlineBrush = Brush(255, 10, 10, 10);
            frontBrush = Brush(240, 230, 55, 38);
            protectedBrush = Brush(240, 38, 217, 89);
            neutralBrush = Brush(240, 242, 217, 51);
            rearBrush = Brush(240, 51, 140, 255);
            arrowBrush = Brush(255, 38, 255, 64);
        }

        private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) =>
            new SolidColorBrush(Noesis.Color.FromArgb(a, r, g, b));

        private static Brush BrushFor(FormationRole role)
        {
            switch (role)
            {
                case FormationRole.Front: return frontBrush;
                case FormationRole.Protected: return protectedBrush;
                case FormationRole.Rear: return rearBrush;
                default: return neutralBrush;
            }
        }

        private static void HideAll()
        {
            for (int index = 0; index < PointPool.Count; index++)
                PointPool[index].Visibility = Visibility.Collapsed;
            HideArrow();
        }

        private static void HideArrow()
        {
            for (int index = 0; index < ArrowLines.Length; index++)
                if (ArrowLines[index] != null)
                    ArrowLines[index].Visibility = Visibility.Collapsed;
        }

        private sealed class PreviewSnapshot
        {
            internal static readonly PreviewSnapshot Empty = new PreviewSnapshot(Array.Empty<FormationPreviewPoint>(), FormationDirectionIndicator.Hidden, 0);
            internal PreviewSnapshot(FormationPreviewPoint[] points, FormationDirectionIndicator direction, int generation)
            {
                Points = points;
                Direction = direction;
                Generation = generation;
            }
            internal FormationPreviewPoint[] Points { get; }
            internal FormationDirectionIndicator Direction { get; }
            internal int Generation { get; }
        }
    }

    internal readonly struct FormationPreviewPoint
    {
        internal FormationPreviewPoint(int x, int y, FormationRole role) { X = x; Y = y; Role = role; }
        internal int X { get; }
        internal int Y { get; }
        internal FormationRole Role { get; }
    }

    internal readonly struct FormationDirectionIndicator
    {
        internal static readonly FormationDirectionIndicator Hidden = new FormationDirectionIndicator(false, 0, 0, 0, 0);
        internal FormationDirectionIndicator(bool visible, int startX, int startY, int endX, int endY)
        { Visible = visible; StartX = startX; StartY = startY; EndX = endX; EndY = endY; }
        internal bool Visible { get; }
        internal int StartX { get; }
        internal int StartY { get; }
        internal int EndX { get; }
        internal int EndY { get; }
    }
}
