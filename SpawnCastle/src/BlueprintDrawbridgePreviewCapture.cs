using BepInEx;
using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpawnCastle
{
    internal sealed class BlueprintDrawbridgePreviewCapture
    {
        // Keep this enabled while replacing the first incomplete captures.
        // It is idle unless mapper 105 is held at the mouse.
        internal static bool Enabled = false;

        private const int DrawbridgeMapperValue = 105;
        private const float SampleIntervalSeconds = 0.12f;
        private const int StableSamplesRequired = 3;
        private const int ScanRadius = 16;
        private const int TilemapSize = 800;
        private const float CapturePixelsPerWorldUnit = 64f;
        private const int CaptureLayer = 31;
        private const string DirectoryName =
            "SpawnCastle_Serp.DrawbridgePreviewCapture";

        private readonly ManualLogSource log;
        private readonly string outputDirectory;
        private readonly HashSet<string> capturedSignatures =
            new HashSet<string>(StringComparer.Ordinal);
        private float nextSampleTime;
        private string candidateSignature = string.Empty;
        private int candidateStableSamples;

        public BlueprintDrawbridgePreviewCapture(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            outputDirectory = Path.Combine(Paths.ConfigPath, DirectoryName);
        }

        public void Tick()
        {
            if (!Enabled || Time.unscaledTime < nextSampleTime)
                return;

            nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;
            if (!CanCapture() ||
                !TryCollectPreviewFragments(
                    out List<PreviewFragment> fragments))
            {
                ResetCandidate();
                return;
            }

            string signature = BuildSignature(fragments);
            if (string.Equals(
                    signature,
                    candidateSignature,
                    StringComparison.Ordinal))
            {
                candidateStableSamples++;
            }
            else
            {
                candidateSignature = signature;
                candidateStableSamples = 1;
            }

            if (candidateStableSamples < StableSamplesRequired)
                return;

            candidateStableSamples = 0;
            int rotation = GameMap.instance != null
                ? (int)GameMap.instance.CurrentRotation()
                : -1;
            string captureKey = rotation.ToString(
                    CultureInfo.InvariantCulture) +
                ":" + signature;
            if (!capturedSignatures.Add(captureKey))
                return;

            try
            {
                SaveCapture(fragments, rotation, signature);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Vanilla Drawbridge preview could not be captured: " +
                    $"rotation={rotation}, signature={signature}, error={ex}");
            }
        }

        private static bool CanCapture()
        {
            return !EngineInterface.FlattenedLandscape &&
                MainControls.instance != null &&
                MainControls.instance.CurrentAction == 5 &&
                MainControls.instance.CurrentSubAction == DrawbridgeMapperValue &&
                GameMap.instance != null;
        }

        private static bool TryCollectPreviewFragments(
            out List<PreviewFragment> fragments)
        {
            fragments = new List<PreviewFragment>();
            float mouseTileX = 0f;
            float mouseTileY = 0f;
            MainControls.instance.getMouseMapTilePosition(
                ref mouseTileX,
                ref mouseTileY);
            int minimumX = Math.Max(1, (int)mouseTileX - ScanRadius);
            int maximumX = Math.Min(
                TilemapSize - 1,
                (int)mouseTileX + ScanRadius);
            int minimumY = Math.Max(1, (int)mouseTileY - ScanRadius);
            int maximumY = Math.Min(
                TilemapSize - 1,
                (int)mouseTileY + ScanRadius);

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    GameMapTile tile = GameMap.instance.getMapTile(x, y);
                    if (tile == null ||
                        tile.tilemapRef == null ||
                        tile.tileImage == null ||
                        tile.constructionOrigImage == null ||
                        tile.tileImage == tile.constructionOrigImage ||
                        !IsVanillaDrawbridgeFragment(tile.tileImage))
                    {
                        continue;
                    }

                    Vector3 position = tile.tilemapRef.GetCellCenterWorld(
                        new Vector3Int(x, y, 0));
                    position.y += tile.height;
                    fragments.Add(new PreviewFragment(
                        x,
                        y,
                        position,
                        tile.tileImage));
                }
            }

            return fragments.Count > 0;
        }

        private static bool IsVanillaDrawbridgeFragment(Sprite sprite)
        {
            // The red/green placement ground also replaces 64x32 tiles. The
            // actual bridge pieces, including its missing 64x32 sections, all
            // come from tile_castle in AllTileSprites.
            return sprite != null &&
                sprite.texture != null &&
                sprite.name.StartsWith(
                    "tile_castle ",
                    StringComparison.Ordinal) &&
                string.Equals(
                    sprite.texture.name,
                    "AllTileSprites",
                    StringComparison.Ordinal);
        }

        private static string BuildSignature(
            IReadOnlyList<PreviewFragment> fragments)
        {
            int minimumX = fragments.Min(fragment => fragment.TileX);
            int minimumY = fragments.Min(fragment => fragment.TileY);
            var description = new StringBuilder();
            foreach (PreviewFragment fragment in fragments
                .OrderBy(value => value.TileY)
                .ThenBy(value => value.TileX))
            {
                Sprite sprite = fragment.Sprite;
                Rect rect = sprite.rect;
                Vector2 pivot = sprite.pivot;
                description.Append(fragment.TileX - minimumX);
                description.Append(',');
                description.Append(fragment.TileY - minimumY);
                description.Append(':');
                description.Append(sprite.name);
                description.Append(':');
                description.Append(sprite.texture != null
                    ? sprite.texture.name
                    : "<no-texture>");
                description.Append(':');
                description.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:F0},{1:F0},{2:F0},{3:F0}:{4:F2},{5:F2};",
                    rect.x,
                    rect.y,
                    rect.width,
                    rect.height,
                    pivot.x,
                    pivot.y);
            }

            return CalculateFnv1A64(description.ToString()).ToString(
                "X16",
                CultureInfo.InvariantCulture);
        }

        private void SaveCapture(
            IReadOnlyList<PreviewFragment> fragments,
            int rotation,
            string signature)
        {
            Directory.CreateDirectory(outputDirectory);
            string rotationName = GameMap.instance != null
                ? GameMap.instance.CurrentRotation().ToString()
                : rotation.ToString(CultureInfo.InvariantCulture);
            string baseName = $"Drawbridge_{rotationName}_{signature}";
            string pngPath = Path.Combine(
                outputDirectory,
                baseName + ".png");
            string detailsPath = Path.Combine(
                outputDirectory,
                baseName + ".tsv");

            if (!File.Exists(pngPath))
            {
                byte[] pngBytes = RenderTransparentComposite(fragments);
                File.WriteAllBytes(pngPath, pngBytes);
            }

            if (!File.Exists(detailsPath))
            {
                File.WriteAllText(
                    detailsPath,
                    BuildDetails(fragments, rotation, signature),
                    new UTF8Encoding(false));
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured clean Vanilla Drawbridge mouse preview: " +
                $"rotation={rotationName} ({rotation}), " +
                $"fragments={fragments.Count}, signature={signature}, " +
                $"png={pngPath}, details={detailsPath}.");
        }

        private static byte[] RenderTransparentComposite(
            IReadOnlyList<PreviewFragment> fragments)
        {
            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            int sortingOrder = 0;
            foreach (PreviewFragment fragment in fragments
                .OrderByDescending(value => value.Position.y)
                .ThenBy(value => value.TileX))
            {
                Bounds bounds = fragment.Sprite.bounds;
                left = Math.Min(left, fragment.Position.x + bounds.min.x);
                right = Math.Max(right, fragment.Position.x + bounds.max.x);
                bottom = Math.Min(bottom, fragment.Position.y + bounds.min.y);
                top = Math.Max(top, fragment.Position.y + bounds.max.y);
            }

            int width = Math.Max(
                1,
                Mathf.CeilToInt((right - left) * CapturePixelsPerWorldUnit));
            int height = Math.Max(
                1,
                Mathf.CeilToInt((top - bottom) * CapturePixelsPerWorldUnit));
            if (width > 4096 || height > 4096)
            {
                throw new InvalidOperationException(
                    $"Preview composite is implausibly large: {width}x{height}.");
            }

            var temporaryObjects = new List<GameObject>();
            var cameraObject = new GameObject(
                "SpawnCastle_DrawbridgeCaptureCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            temporaryObjects.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << CaptureLayer;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.position = new Vector3(
                (left + right) * 0.5f,
                (bottom + top) * 0.5f,
                -10f);
            camera.orthographicSize = height /
                (2f * CapturePixelsPerWorldUnit);
            camera.aspect = width / (float)height;

            foreach (PreviewFragment fragment in fragments)
            {
                var spriteObject = new GameObject(
                    "SpawnCastle_DrawbridgeCaptureFragment");
                spriteObject.hideFlags = HideFlags.HideAndDontSave;
                spriteObject.layer = CaptureLayer;
                spriteObject.transform.position = fragment.Position;
                SpriteRenderer renderer =
                    spriteObject.AddComponent<SpriteRenderer>();
                renderer.sprite = fragment.Sprite;
                renderer.color = Color.white;
                // Lower isometric tiles are in front, matching the Tilemap.
                renderer.sortingOrder = sortingOrder++;
                temporaryObjects.Add(spriteObject);
            }

            RenderTexture renderTexture = null;
            Texture2D readableTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                renderTexture.filterMode = FilterMode.Point;
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                readableTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.ARGB32,
                    false);
                readableTexture.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0,
                    false);
                readableTexture.Apply(false, false);
                return ImageConversion.EncodeToPNG(readableTexture);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);
                if (readableTexture != null)
                    Object.Destroy(readableTexture);
                foreach (GameObject temporaryObject in temporaryObjects)
                    Object.Destroy(temporaryObject);
            }
        }

        private static string BuildDetails(
            IReadOnlyList<PreviewFragment> fragments,
            int rotation,
            string signature)
        {
            var output = new StringBuilder();
            output.Append("# Drawbridge Vanilla mouse-preview capture\r\n");
            output.Append("# capturedUtc\t");
            output.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            output.Append("\r\n# rotation\t");
            output.Append(rotation.ToString(CultureInfo.InvariantCulture));
            output.Append("\r\n# signature\t");
            output.Append(signature);
            output.Append("\r\ntileX\ttileY\tworldX\tworldY\tsprite\ttexture\t" +
                "rectX\trectY\trectWidth\trectHeight\tpivotX\tpivotY\tppu\r\n");
            foreach (PreviewFragment fragment in fragments
                .OrderBy(value => value.TileY)
                .ThenBy(value => value.TileX))
            {
                Sprite sprite = fragment.Sprite;
                Rect rect = sprite.rect;
                Vector2 pivot = sprite.pivot;
                output.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2:F6}\t{3:F6}\t{4}\t{5}\t" +
                    "{6:F0}\t{7:F0}\t{8:F0}\t{9:F0}\t" +
                    "{10:F2}\t{11:F2}\t{12:F2}\r\n",
                    fragment.TileX,
                    fragment.TileY,
                    fragment.Position.x,
                    fragment.Position.y,
                    sprite.name,
                    sprite.texture != null ? sprite.texture.name : string.Empty,
                    rect.x,
                    rect.y,
                    rect.width,
                    rect.height,
                    pivot.x,
                    pivot.y,
                    sprite.pixelsPerUnit);
            }

            return output.ToString();
        }

        private void ResetCandidate()
        {
            candidateSignature = string.Empty;
            candidateStableSamples = 0;
        }

        private static ulong CalculateFnv1A64(string value)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }

        private readonly struct PreviewFragment
        {
            public PreviewFragment(
                int tileX,
                int tileY,
                Vector3 position,
                Sprite sprite)
            {
                TileX = tileX;
                TileY = tileY;
                Position = position;
                Sprite = sprite;
            }

            public int TileX { get; }

            public int TileY { get; }

            public Vector3 Position { get; }

            public Sprite Sprite { get; }
        }
    }
}
