using Shared;
using UnityEngine;

namespace PitchMoatLineTest
{
    // Owned by the live map scene. It is created only after a gesture reaches
    // the game's input callback and has no role in placement or hook lifetime.
    public sealed class PlacementPreview : MonoBehaviour
    {
        private static PlacementPreview view;
        private static readonly Color Valid = new Color(0.35f, 1f, 0.42f, 0.94f);
        private static readonly Color Invalid = new Color(1f, 0.26f, 0.26f, 0.94f);
        private static readonly Color Unaffordable = new Color(1f, 0.65f, 0.24f, 0.94f);
        private static readonly Color Existing = new Color(0.75f, 0.75f, 0.75f, 0.94f);
        private static readonly Color Capacity = new Color(1f, 0.91f, 0.29f, 0.94f);
#if SERP_DEVDIAG
        private int renderedGesture;
#endif

        internal static void EnsureOn(EditorDirector director)
        {
            if (view == null || view.gameObject != director.gameObject)
                view = director.gameObject.AddComponent<PlacementPreview>();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Application.isFocused)
                return;
            var snapshot = PlacementRuntime.GetPreview();
            if (snapshot == null)
                return;
            var map = GameMap.instance;
            var camera = Camera.main;
            if (map == null || camera == null || MainControls.instance == null)
                return;

            var originalColor = GUI.color;
            var originalMatrix = GUI.matrix;
#if SERP_DEVDIAG
            long firstDraw = 0;
            if (renderedGesture != snapshot.GestureNumber)
            {
                renderedGesture = snapshot.GestureNumber;
                DevDiag.Start(ref firstDraw);
            }
#endif
            try
            {
                for (int i = 0; i < snapshot.Tiles.Length; i++)
                {
                    var tile = snapshot.Tiles[i];
                    map.mapGameTileToTilemapCoord(tile.X, tile.Y, out int x, out int y);
                    var visible = map.getMapTile(x, y);
                    if (visible == null || visible.gameMapX != tile.X || visible.gameMapY != tile.Y)
                        continue;
                    Vector3 center = MainControls.instance.getCellCentre(x, y);
                    center.y += visible.height;
                    Vector3 point = camera.WorldToScreenPoint(center);
                    if (point.z < 0 || point.x < -100 || point.x > Screen.width + 100 ||
                        point.y < -100 || point.y > Screen.height + 100)
                        continue;
                    var corner = camera.WorldToScreenPoint(center + new Vector3(0.47f, 0.23f, 0f));
                    var p = new Vector2(point.x, Screen.height - point.y);
                    DrawTile(p, Mathf.Abs(corner.x - point.x), Mathf.Abs(corner.y - point.y),
                        snapshot.Available ? TileColor(snapshot.States[i]) : Invalid, originalMatrix);
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
#if SERP_DEVDIAG
                DevDiag.StopSpike(ref firstDraw, "pitch-moat.preview.first-draw", 0,
                    () => $"gesture={snapshot.GestureNumber} fields={snapshot.Tiles.Length}");
#endif
            }
        }

        private static Color TileColor(LineTileState state)
        {
            switch (state)
            {
                case LineTileState.Buildable: return Valid;
                case LineTileState.Existing: return Existing;
                case LineTileState.Material: return Unaffordable;
                case LineTileState.Capacity: return Capacity;
                default: return Invalid;
            }
        }

        private static void DrawTile(Vector2 center, float halfWidth, float halfHeight, Color color, Matrix4x4 matrix)
        {
            GUI.color = color;
            var top = center + new Vector2(0, -halfHeight);
            var right = center + new Vector2(halfWidth, 0);
            var bottom = center + new Vector2(0, halfHeight);
            var left = center + new Vector2(-halfWidth, 0);
            DrawEdge(top, right, matrix);
            DrawEdge(right, bottom, matrix);
            DrawEdge(bottom, left, matrix);
            DrawEdge(left, top, matrix);
        }

        private static void DrawEdge(Vector2 start, Vector2 end, Matrix4x4 matrix)
        {
            var delta = end - start;
            GUI.matrix = matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - 1f, delta.magnitude, 2f), Texture2D.whiteTexture);
        }

    }
}
