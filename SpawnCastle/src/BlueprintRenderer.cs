using AIVParser.Core;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpawnCastle
{
    internal sealed class BlueprintRenderer
    {
        // Match Vanilla's world-sprite import setting. Per-building UI
        // normalization is reversed separately from the AIV footprint.
        private const float BuildMenuPixelsPerWorldUnit = 64f;

        private readonly ManualLogSource log;
        private readonly BlueprintBuildingSizeCalibration sizeCalibration;
        private readonly BlueprintBuildingImageLibrary buildingImageLibrary;
        private Sprite markerSprite;
        private readonly Dictionary<string, Sprite> buildMenuSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> helpImageSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> missingIconKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> missingScaleMappers = new HashSet<int>();
        private readonly HashSet<int> missingGroundOffsetMappers =
            new HashSet<int>();
        private readonly HashSet<BlueprintDrawbridgePosition>
            reportedDrawbridgePlaceholders =
                new HashSet<BlueprintDrawbridgePosition>();
        private readonly HashSet<string> failedHelpImages =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PendingDepthIcon>> pendingDepthIcons =
            new Dictionary<string, List<PendingDepthIcon>>(StringComparer.Ordinal);
        private readonly HashSet<string> requestedDepthKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedDepthKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Renderer> alphaRenderers = new List<Renderer>();
        private readonly List<TrackedIconRoot> trackedIconRoots =
            new List<TrackedIconRoot>();
        private readonly List<Mesh> projectionMeshes = new List<Mesh>();
        private readonly Dictionary<string, Mesh> flattenedBuildingMeshes =
            new Dictionary<string, Mesh>(StringComparer.Ordinal);
        private readonly Dictionary<int, Material> meshMaterials =
            new Dictionary<int, Material>();
        private static readonly FieldInfo NoesisTextureCacheField =
            typeof(NoesisTextureProvider).GetField(
                "_textures",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject overlayRoot;
        private BlueprintLayout renderedLayout;
        private bool renderedFlattened;
        private int renderedRotation = int.MinValue;
        private float currentIconScale = 1f;
        private float currentIconAlpha = 0.3f;
        private int currentIconSortingBandOffset;
        private Stopwatch progressiveLoadTimer;
        private bool progressiveCompletionLogged;

        public BlueprintRenderer(
            ManualLogSource log,
            BlueprintBuildingSizeCalibration sizeCalibration,
            BlueprintBuildingImageLibrary buildingImageLibrary)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.sizeCalibration = sizeCalibration ??
                throw new ArgumentNullException(nameof(sizeCalibration));
            this.buildingImageLibrary = buildingImageLibrary ??
                throw new ArgumentNullException(nameof(buildingImageLibrary));
            this.buildingImageLibrary.DepthVisualLoaded += OnDepthVisualLoaded;
        }

        public int CompletedDepthCaptureCount => completedDepthKeys.Count;
        public int RequestedDepthCaptureCount => requestedDepthKeys.Count;
        public bool IsDepthLoading => completedDepthKeys.Count < requestedDepthKeys.Count;

        public void PreloadDepthCaptures(BlueprintLayout layout)
        {
            if (layout == null || GameMap.instance == null)
                return;
            foreach (BlueprintIconPlacement placement in layout.Icons)
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(placement.MapperValue);
                bool swapsAxes = GameMap.instance.CurrentRotation() == Enums.Dircs.East ||
                    GameMap.instance.CurrentRotation() == Enums.Dircs.West;
                string visualMapperName = BlueprintBuildingIconCatalog.ResolveGateVisualMapper(
                    mapper.Name,
                    swapsAxes);
                if (BlueprintBuildingCaptureCatalog.UsesCompositeOnlyIcon(visualMapperName))
                    continue;
                buildingImageLibrary.ResolveDepth(
                    placement.MapperValue,
                    visualMapperName,
                    UsesIslamicChurchSkin(),
                    GetCameraQuarter(),
                    ResolveDrawbridgePosition(placement),
                    ResolveStairDirection(placement),
                    ResolveStairFlipHorizontally(placement),
                    out _,
                    out _,
                    out _);
            }
        }

        public BlueprintRenderResult Render(
            BlueprintLayout layout,
            float iconScale,
            float iconAlpha)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (GameMap.instance == null || TilemapManager.instance == null)
                throw new InvalidOperationException("The game tilemap is not ready.");

            Clear();
            progressiveLoadTimer = Stopwatch.StartNew();
            progressiveCompletionLogged = false;
            overlayRoot = new GameObject("SpawnCastle_BlueprintOverlay");
            renderedLayout = layout;
            renderedFlattened = EngineInterface.FlattenedLandscape;
            renderedRotation = (int)GameMap.instance.CurrentRotation();
            currentIconScale = iconScale;
            currentIconAlpha = iconAlpha;

            int clippedTiles = 0;
            int renderedTiles = 0;
            int minimumGroundDepthRow = int.MaxValue;
            int maximumGroundDepthRow = int.MinValue;
            var markerBatches = new Dictionary<int, List<GroundMarkerInstance>>();
            foreach (BlueprintTilePlacement placement in layout.Tiles)
            {
                if (!TryGetRenderedTile(
                        placement.Tile.X,
                        placement.Tile.Y,
                        out GameMapTile mapTile,
                        out Vector3Int position))
                {
                    clippedTiles++;
                    continue;
                }

                if (!markerBatches.TryGetValue(
                        mapTile.row,
                        out List<GroundMarkerInstance> markerPositions))
                {
                    markerPositions = new List<GroundMarkerInstance>();
                    markerBatches.Add(mapTile.row, markerPositions);
                }
                markerPositions.Add(new GroundMarkerInstance(
                    GetGroundCellCenter(mapTile, position),
                    GetOverlayColor(placement.Category, placement.VisualGroup)));
                minimumGroundDepthRow = Math.Min(
                    minimumGroundDepthRow,
                    mapTile.row);
                maximumGroundDepthRow = Math.Max(
                    maximumGroundDepthRow,
                    mapTile.row);
                renderedTiles++;
            }
            CreateGroundMarkerBatches(markerBatches);

            // One shared offset keeps all icon-to-icon depth differences while
            // placing their complete band ahead of every colored ground cell.
            int iconSortingBandOffset = BlueprintFragmentCaptureCatalog
                .CalculateIconSortingBandOffset(
                    minimumGroundDepthRow,
                    maximumGroundDepthRow);
            int renderedIcons = 0;
            bool flattenedLandscape = renderedFlattened;
            currentIconSortingBandOffset = iconSortingBandOffset;
            foreach (BlueprintIconPlacement placement in layout.Icons)
            {
                BlueprintDrawbridgePosition drawbridgePosition =
                    ResolveDrawbridgePosition(placement);
                BlueprintStairDirection stairDirection =
                    ResolveStairDirection(placement);
                bool stairFlipHorizontally =
                    ResolveStairFlipHorizontally(placement);
                if (!flattenedLandscape && TryHandleDepthIcon(
                        placement,
                        drawbridgePosition,
                        stairDirection,
                        stairFlipHorizontally,
                        iconScale,
                        iconAlpha,
                        iconSortingBandOffset,
                        out bool depthRendered))
                {
                    if (depthRendered)
                        renderedIcons++;
                    continue;
                }

                BlueprintIconVisual icon =
                    GetBlueprintIcon(
                        placement,
                        flattenedLandscape,
                        drawbridgePosition,
                        stairDirection,
                        stairFlipHorizontally);
                if (icon.Sprite == null)
                    continue;
                if (TryCreateIcon(
                        placement,
                        icon,
                        flattenedLandscape,
                        iconScale,
                        iconAlpha,
                        iconSortingBandOffset))
                {
                    renderedIcons++;
                }
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint first visible output prepared: tiles={renderedTiles}, " +
                $"icons={renderedIcons}, depthReady={CompletedDepthCaptureCount}/" +
                $"{RequestedDepthCaptureCount}, elapsedMs={progressiveLoadTimer.Elapsed.TotalMilliseconds:F1}.");
            LogProgressiveCompletionIfReady();

            return new BlueprintRenderResult(
                renderedTiles,
                renderedIcons,
                clippedTiles);
        }

        public void Clear()
        {
            pendingDepthIcons.Clear();
            requestedDepthKeys.Clear();
            completedDepthKeys.Clear();
            alphaRenderers.Clear();
            trackedIconRoots.Clear();
            foreach (Mesh mesh in projectionMeshes)
                Object.Destroy(mesh);
            projectionMeshes.Clear();
            renderedLayout = null;
            renderedRotation = int.MinValue;
            progressiveLoadTimer = null;
            progressiveCompletionLogged = false;
            if (overlayRoot != null)
            {
                // Destroy is deferred until the end of the frame. Deactivate
                // first so a stale projection cannot flash for one more frame.
                overlayRoot.SetActive(false);
                Object.Destroy(overlayRoot);
                overlayRoot = null;
            }
        }

        public void Hide()
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        public bool TryShowExisting(BlueprintLayout layout)
        {
            if (overlayRoot == null || renderedLayout != layout || GameMap.instance == null ||
                renderedFlattened != EngineInterface.FlattenedLandscape ||
                renderedRotation != (int)GameMap.instance.CurrentRotation())
            {
                return false;
            }
            overlayRoot.SetActive(true);
            return true;
        }

        public void UpdateVisualSettings(float iconScale, float iconAlpha)
        {
            currentIconScale = iconScale;
            currentIconAlpha = iconAlpha;
            foreach (TrackedIconRoot tracked in trackedIconRoots)
            {
                tracked.Root.localScale = new Vector3(
                    tracked.FlipHorizontally ? -tracked.BaseScale * iconScale : tracked.BaseScale * iconScale,
                    tracked.BaseScale * iconScale,
                    1f);
                Vector2 offset = tracked.PositionOffsetPerScale * iconScale;
                tracked.Root.position = tracked.GroundPosition +
                    new Vector3(offset.x, offset.y, 0f);
            }
            foreach (Renderer renderer in alphaRenderers)
            {
                if (renderer is SpriteRenderer spriteRenderer)
                    spriteRenderer.color = new Color(1f, 1f, 1f, iconAlpha);
                else
                    ApplyAlpha(renderer, iconAlpha);
            }
        }

        private bool TryHandleDepthIcon(
            BlueprintIconPlacement placement,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection,
            bool stairFlipHorizontally,
            float iconScale,
            float iconAlpha,
            int sortingBandOffset,
            out bool rendered)
        {
            rendered = false;
            AivMapperInfo mapper = AivMapperCatalog.Resolve(placement.MapperValue);
            bool mapRotationSwapsAxes = GameMap.instance != null &&
                (GameMap.instance.CurrentRotation() == Enums.Dircs.East ||
                 GameMap.instance.CurrentRotation() == Enums.Dircs.West);
            string visualMapperName = BlueprintBuildingIconCatalog.ResolveGateVisualMapper(
                mapper.Name,
                mapRotationSwapsAxes);
            if (BlueprintBuildingCaptureCatalog.UsesCompositeOnlyIcon(visualMapperName))
                return false;

            BlueprintDepthResolveState state = buildingImageLibrary.ResolveDepth(
                placement.MapperValue,
                visualMapperName,
                UsesIslamicChurchSkin(),
                GetCameraQuarter(),
                drawbridgePosition,
                stairDirection,
                stairFlipHorizontally,
                out string key,
                out bool flipHorizontally,
                out BlueprintDepthVisual visual);
            if (state == BlueprintDepthResolveState.Missing)
            {
                // Captured buildings deliberately do not fall back to the old flat composite.
                return BlueprintBuildingCaptureCatalog.RequiresCapturedImage(visualMapperName);
            }
            if (state == BlueprintDepthResolveState.Failed)
                return true;

            requestedDepthKeys.Add(key);
            if (state == BlueprintDepthResolveState.Pending)
            {
                if (!pendingDepthIcons.TryGetValue(key, out List<PendingDepthIcon> pending))
                {
                    pending = new List<PendingDepthIcon>();
                    pendingDepthIcons.Add(key, pending);
                }
                pending.Add(new PendingDepthIcon(placement, flipHorizontally));
                return true;
            }

            completedDepthKeys.Add(key);
            rendered = TryCreateDepthIcon(
                placement,
                visual,
                flipHorizontally,
                iconScale,
                iconAlpha,
                sortingBandOffset);
            return true;
        }

        private void OnDepthVisualLoaded(string key)
        {
            if (overlayRoot == null || renderedFlattened ||
                !pendingDepthIcons.TryGetValue(key, out List<PendingDepthIcon> pending))
            {
                return;
            }
            pendingDepthIcons.Remove(key);
            completedDepthKeys.Add(key);
            if (!buildingImageLibrary.TryGetLoadedDepthVisual(key, out BlueprintDepthVisual visual))
            {
                LogProgressiveCompletionIfReady();
                return;
            }
            foreach (PendingDepthIcon icon in pending)
            {
                TryCreateDepthIcon(
                    icon.Placement,
                    visual,
                    icon.FlipHorizontally,
                    currentIconScale,
                    currentIconAlpha,
                    currentIconSortingBandOffset);
            }
            LogProgressiveCompletionIfReady();
        }

        private void LogProgressiveCompletionIfReady()
        {
            if (progressiveCompletionLogged || progressiveLoadTimer == null || IsDepthLoading)
                return;
            progressiveCompletionLogged = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint progressive rendering complete: depthCaptures=" +
                $"{CompletedDepthCaptureCount}/{RequestedDepthCaptureCount}, " +
                $"elapsedMs={progressiveLoadTimer.Elapsed.TotalMilliseconds:F1}.");
        }

        private bool TryCreateDepthIcon(
            BlueprintIconPlacement placement,
            BlueprintDepthVisual visual,
            bool flipHorizontally,
            float iconScale,
            float iconAlpha,
            int sortingBandOffset)
        {
            if (overlayRoot == null || visual?.Layers == null || visual.Layers.Count == 0)
                return false;
            AivMapperInfo mapper = AivMapperCatalog.Resolve(placement.MapperValue);
            Vector3 position = Vector3.zero;
            int validGroundCells = 0;
            int minimumDepthRow = int.MaxValue;
            int maximumDepthRow = int.MinValue;
            AccumulateIconFootprint(
                placement,
                BlueprintBuildingIconCatalog.HasReservedPlacementArea(mapper.Name),
                ref position,
                ref validGroundCells,
                ref minimumDepthRow,
                ref maximumDepthRow);
            if (validGroundCells == 0)
                return false;
            position /= validGroundCells;

            var root = new GameObject($"BlueprintIcon_{placement.MapperValue}_DepthAtlas");
            root.transform.SetParent(overlayRoot.transform, false);
            root.transform.position = position;
            root.transform.localScale = new Vector3(
                flipHorizontally ? -iconScale : iconScale,
                iconScale,
                1f);
            trackedIconRoots.Add(new TrackedIconRoot(
                root.transform,
                flipHorizontally,
                1f,
                position,
                Vector2.zero));

            foreach (BlueprintDepthLayer layer in visual.Layers)
            {
                var layerObject = new GameObject(
                    $"BlueprintDepthLayer_{layer.RowOffset}_{layer.SortingOffset}_{layer.PageIndex}");
                layerObject.transform.SetParent(root.transform, false);
                MeshFilter filter = layerObject.AddComponent<MeshFilter>();
                filter.sharedMesh = layer.Mesh;
                MeshRenderer renderer = layerObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = layer.Material;
                int targetRow = BlueprintFragmentCaptureCatalog.RemapDepthRow(
                    visual.CaptureMinimumRow,
                    visual.CaptureMaximumRow,
                    minimumDepthRow,
                    maximumDepthRow,
                    layer.RowOffset);
                renderer.sortingOrder =
                    -20000 + targetRow * 49 + 4 + layer.SortingOffset + sortingBandOffset;
                ApplyAlpha(renderer, iconAlpha);
                alphaRenderers.Add(renderer);
            }
            return true;
        }

        private static void ApplyAlpha(Renderer renderer, float alpha)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            properties.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
            renderer.SetPropertyBlock(properties);
        }

        private void CreateGroundMarkerBatches(
            Dictionary<int, List<GroundMarkerInstance>> batches)
        {
            Sprite sprite = GetMarkerSprite();
            foreach (KeyValuePair<int, List<GroundMarkerInstance>> batch in batches)
            {
                if (sprite == null || batch.Value.Count == 0)
                    continue;
                Vector3 minimum = sprite.bounds.min;
                Vector3 maximum = sprite.bounds.max;
                Rect textureRect = sprite.textureRect;
                float u0 = textureRect.xMin / sprite.texture.width;
                float v0 = textureRect.yMin / sprite.texture.height;
                float u1 = textureRect.xMax / sprite.texture.width;
                float v1 = textureRect.yMax / sprite.texture.height;
                var vertices = new Vector3[batch.Value.Count * 4];
                var uv = new Vector2[vertices.Length];
                var colors = new Color32[vertices.Length];
                var triangles = new int[batch.Value.Count * 6];
                for (int index = 0; index < batch.Value.Count; index++)
                {
                    int vertex = index * 4;
                    int triangle = index * 6;
                    GroundMarkerInstance instance = batch.Value[index];
                    Vector3 center = instance.Position;
                    vertices[vertex] = center + new Vector3(minimum.x, minimum.y, 0f);
                    vertices[vertex + 1] = center + new Vector3(maximum.x, minimum.y, 0f);
                    vertices[vertex + 2] = center + new Vector3(minimum.x, maximum.y, 0f);
                    vertices[vertex + 3] = center + new Vector3(maximum.x, maximum.y, 0f);
                    uv[vertex] = new Vector2(u0, v0);
                    uv[vertex + 1] = new Vector2(u1, v0);
                    uv[vertex + 2] = new Vector2(u0, v1);
                    uv[vertex + 3] = new Vector2(u1, v1);
                    Color32 color = instance.Color;
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
                    name = $"SpawnCastle_GroundMarkers_{batch.Key}",
                    vertices = vertices,
                    uv = uv,
                    colors32 = colors,
                    triangles = triangles
                };
                mesh.RecalculateBounds();
                projectionMeshes.Add(mesh);
                var markerObject = new GameObject(mesh.name);
                markerObject.transform.SetParent(overlayRoot.transform, false);
                markerObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = markerObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GetMeshMaterial(sprite.texture);
                renderer.sortingOrder = -20000 + batch.Key * 49 + 1;
            }
        }

        private bool TryGetRenderedTile(
            int worldX,
            int worldY,
            out GameMapTile mapTile,
            out Vector3Int position)
        {
            GameMap.instance.mapGameTileToTilemapCoord(
                worldX,
                worldY,
                out int tileMapX,
                out int tileMapY);
            mapTile = GameMap.instance.getMapTile(tileMapX, tileMapY);
            position = new Vector3Int(tileMapX, tileMapY, 0);
            return mapTile != null && mapTile.tilemapRef != null;
        }

        private Sprite GetMarkerSprite()
        {
            if (markerSprite != null)
                return markerSprite;
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.ARGB32,
                false);
            texture.name = "SpawnCastle_BlueprintTileMask";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float distance =
                        Math.Abs((x + 0.5f - width / 2f) / (width / 2f)) +
                        Math.Abs((y + 0.5f - height / 2f) / (height / 2f));
                    if (distance > 1f)
                        continue;

                    Color pixel = Color.white;
                    pixel.a = distance >= 0.84f ? 0.78f : 0.34f;
                    pixels[y * width + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            markerSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f,
                0,
                SpriteMeshType.FullRect);
            markerSprite.name = texture.name;

            Object.DontDestroyOnLoad(texture);
            Object.DontDestroyOnLoad(markerSprite);
            return markerSprite;
        }

        private BlueprintIconVisual GetBlueprintIcon(
            BlueprintIconPlacement placement,
            bool flattenedLandscape,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection,
            bool stairFlipHorizontally)
        {
            int mapperValue = placement.MapperValue;
            string iconKey = flattenedLandscape + ":" + mapperValue + ":" +
                drawbridgePosition + ":" + stairDirection + ":" +
                stairFlipHorizontally;
            try
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                bool mapRotationSwapsAxes = GameMap.instance != null &&
                    (GameMap.instance.CurrentRotation() == Enums.Dircs.East ||
                     GameMap.instance.CurrentRotation() == Enums.Dircs.West);
                string visualMapperName = BlueprintBuildingIconCatalog
                    .ResolveGateVisualMapper(
                        mapper.Name,
                        mapRotationSwapsAxes);
                iconKey += ":" + visualMapperName;
                if (missingIconKeys.Contains(iconKey))
                    return default;

                BlueprintBuildingIconDefinition definition =
                    BlueprintBuildingIconCatalog.ResolveDefinition(
                        visualMapperName);
                if (definition == null)
                    throw new InvalidOperationException(
                        $"No Blueprint icon is mapped for {visualMapperName}.");

                bool islamicSkin = UsesIslamicChurchSkin();
                bool mayUseCapturedImage =
                    (flattenedLandscape && mapper.Category == AivItemCategory.Building) ||
                    (!flattenedLandscape &&
                     BlueprintBuildingCaptureCatalog.UsesCompositeOnlyIcon(visualMapperName));
                if (mayUseCapturedImage &&
                    buildingImageLibrary.TryResolveComposite(
                        mapperValue,
                        visualMapperName,
                        islamicSkin,
                        GetCameraQuarter(),
                        drawbridgePosition,
                        stairDirection,
                        stairFlipHorizontally,
                        out Sprite capturedSprite,
                        out bool capturedFlip,
                        out RectInt alphaBounds))
                {
                    if (!flattenedLandscape)
                    {
                        return new BlueprintIconVisual(
                            capturedSprite,
                            true,
                            capturedFlip,
                            false,
                            drawbridgePosition,
                            true);
                    }

                    return new BlueprintIconVisual(
                        capturedSprite,
                        true,
                        capturedFlip,
                        false,
                        drawbridgePosition,
                        true,
                        true,
                        alphaBounds);
                }

                BlueprintDrawbridgeImageDefinition drawbridgeImage =
                    string.Equals(
                        mapper.Name,
                        "MAPPER_DRAWBRIDGE",
                        StringComparison.Ordinal)
                            ? BlueprintBuildingIconCatalog
                                .ResolveDrawbridgeImage(drawbridgePosition)
                            : null;
                string helpImage = flattenedLandscape
                    ? null
                    : drawbridgeImage?.HelpImageFileName ??
                        definition.ResolveHelpImage(islamicSkin);
                bool flipHorizontally =
                    drawbridgeImage?.FlipHorizontally ?? false;
                bool usesPlaceholderImage =
                    drawbridgeImage?.UsesPlaceholderImage ?? false;
                ReportDrawbridgePlaceholder(
                    drawbridgePosition,
                    usesPlaceholderImage);
                if (!flattenedLandscape &&
                    drawbridgeImage?.UsesBundledImage == true &&
                    TryGetBundledDrawbridgeSprite(
                        drawbridgeImage.HelpImageFileName,
                        drawbridgeImage.BundledPivotPixelsFromBottom,
                        out Sprite bundledDrawbridgeSprite))
                {
                    return new BlueprintIconVisual(
                        bundledDrawbridgeSprite,
                        true,
                        false,
                        false,
                        drawbridgePosition,
                        true);
                }

                // Keep the Vanilla Help image as a safe fallback if a bundled
                // directional asset is missing from an installation.
                if (drawbridgeImage?.UsesBundledImage == true)
                    helpImage = definition.ResolveHelpImage(islamicSkin);
                if (!string.IsNullOrWhiteSpace(helpImage) &&
                    TryGetHelpImageSprite(
                        helpImage,
                        definition.Cleanup,
                        out Sprite helpSprite))
                {
                    return new BlueprintIconVisual(
                        helpSprite,
                        true,
                        flipHorizontally,
                        usesPlaceholderImage,
                        drawbridgePosition,
                        false);
                }

                string resourceKey =
                    definition.ResolveBuildMenuResource(islamicSkin);
                Sprite buildMenuSprite =
                    GetBuildMenuSprite(mapper, resourceKey);
                return new BlueprintIconVisual(
                    buildMenuSprite,
                    false,
                    flipHorizontally,
                    usesPlaceholderImage,
                    drawbridgePosition,
                    false);
            }
            catch (Exception ex)
            {
                missingIconKeys.Add(iconKey);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint icon unavailable for mapper {mapperValue}; " +
                    $"the colored footprint remains visible: {ex.Message}");
                return default;
            }
        }

        private bool TryGetGroundBasis(
            int worldX,
            int worldY,
            Vector3 anchor,
            int deltaX,
            int deltaY,
            out Vector2 basis)
        {
            if (TryGetGroundPosition(
                    worldX + deltaX,
                    worldY + deltaY,
                    out Vector3 next))
            {
                basis = new Vector2(next.x - anchor.x, next.y - anchor.y);
                return basis.sqrMagnitude > 0.000001f;
            }
            if (TryGetGroundPosition(
                    worldX - deltaX,
                    worldY - deltaY,
                    out Vector3 previous))
            {
                basis = new Vector2(
                    anchor.x - previous.x,
                    anchor.y - previous.y);
                return basis.sqrMagnitude > 0.000001f;
            }

            basis = default;
            return false;
        }

        private bool TryGetBundledDrawbridgeSprite(
            string fileName,
            float pivotPixelsFromBottom,
            out Sprite sprite)
        {
            string cacheKey = "BundledDrawbridge:" + fileName;
            sprite = null;
            if (helpImageSprites.TryGetValue(cacheKey, out Sprite cached))
            {
                sprite = cached;
                return true;
            }
            if (failedHelpImages.Contains(cacheKey))
                return false;

            string assemblyDirectory = Path.GetDirectoryName(
                typeof(BlueprintRenderer).Assembly.Location);
            string fullPath = Path.Combine(
                assemblyDirectory,
                "BlueprintImages",
                fileName);
            try
            {
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException(
                        "Bundled directional Drawbridge image was not found.",
                        fullPath);

                var texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.ARGB32,
                    false);
                texture.name = "SpawnCastle_" +
                    Path.GetFileNameWithoutExtension(fileName);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(
                        texture,
                        File.ReadAllBytes(fullPath),
                        false))
                {
                    Object.Destroy(texture);
                    throw new InvalidDataException(
                        $"Unity could not decode '{fullPath}'.");
                }

                // The captures preserve the complete 5-tile Vanilla preview.
                // Keeping the full rect retains its exact 64-PPU dimensions;
                // the composite pivot was reconstructed from all 25 tiles.
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(
                        0.5f,
                        pivotPixelsFromBottom / texture.height),
                    BuildMenuPixelsPerWorldUnit,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = texture.name;
                Object.DontDestroyOnLoad(texture);
                Object.DontDestroyOnLoad(sprite);
                helpImageSprites.Add(cacheKey, sprite);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded bundled directional Drawbridge Blueprint: " +
                    $"file='{fileName}', size={texture.width}x{texture.height}, " +
                    $"pivotPixels={pivotPixelsFromBottom:F1}, " +
                    $"path={fullPath}.");
                return true;
            }
            catch (Exception ex)
            {
                failedHelpImages.Add(cacheKey);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bundled directional Drawbridge image '{fileName}' " +
                    $"is unavailable; falling back to ST49: {ex.Message}");
                return false;
            }
        }

        private BlueprintDrawbridgePosition ResolveDrawbridgePosition(
            BlueprintIconPlacement placement)
        {
            if (placement.MapperValue != 105)
                return BlueprintDrawbridgePosition.NotApplicable;
            if (!placement.AdjacentGateCenter.HasValue)
                return BlueprintDrawbridgePosition.Unknown;

            int bridgeCenterX =
                (placement.MinimumWorldX + placement.MaximumWorldX) / 2;
            int bridgeCenterY =
                (placement.MinimumWorldY + placement.MaximumWorldY) / 2;
            BlueprintWorldTile gateCenter =
                placement.AdjacentGateCenter.Value;
            if (!TryGetGroundPosition(
                    bridgeCenterX,
                    bridgeCenterY,
                    out Vector3 bridgePosition) ||
                !TryGetGroundPosition(
                    gateCenter.X,
                    gateCenter.Y,
                    out Vector3 gatePosition))
            {
                return BlueprintDrawbridgePosition.Unknown;
            }

            Vector3 delta = bridgePosition - gatePosition;
            return BlueprintBuildingIconCatalog.ResolveDrawbridgePosition(
                delta.x,
                delta.y);
        }

        private BlueprintStairDirection ResolveStairDirection(
            BlueprintIconPlacement placement)
        {
            if (placement.MapperValue < 181 || placement.MapperValue > 186)
                return BlueprintStairDirection.NotApplicable;
            if (!placement.StairLowEnd.HasValue ||
                !placement.StairHighEnd.HasValue ||
                !TryGetGroundPosition(
                    placement.StairLowEnd.Value.X,
                    placement.StairLowEnd.Value.Y,
                    out Vector3 lowPosition) ||
                !TryGetGroundPosition(
                    placement.StairHighEnd.Value.X,
                    placement.StairHighEnd.Value.Y,
                    out Vector3 highPosition))
            {
                return BlueprintStairDirection.Unknown;
            }

            // The selected capture follows the visible rise direction after
            // the current map rotation, just like directional Drawbridges.
            return highPosition.y >= lowPosition.y
                ? BlueprintStairDirection.North
                : BlueprintStairDirection.South;
        }

        private bool ResolveStairFlipHorizontally(
            BlueprintIconPlacement placement)
        {
            if (placement.MapperValue < 181 || placement.MapperValue > 186 ||
                !placement.StairLowEnd.HasValue ||
                !placement.StairHighEnd.HasValue ||
                !TryGetGroundPosition(
                    placement.StairLowEnd.Value.X,
                    placement.StairLowEnd.Value.Y,
                    out Vector3 lowPosition) ||
                !TryGetGroundPosition(
                    placement.StairHighEnd.Value.X,
                    placement.StairHighEnd.Value.Y,
                    out Vector3 highPosition))
            {
                return false;
            }

            // Both front/back stair captures are normalized with the high end
            // on the right; the other diagonal is the mirrored equivalent.
            return highPosition.x < lowPosition.x;
        }

        private bool TryGetGroundPosition(
            int worldX,
            int worldY,
            out Vector3 position)
        {
            if (!TryGetRenderedTile(
                    worldX,
                    worldY,
                    out GameMapTile mapTile,
                    out Vector3Int tilePosition))
            {
                position = default;
                return false;
            }

            position = GetGroundCellCenter(mapTile, tilePosition);
            return true;
        }

        private void ReportDrawbridgePlaceholder(
            BlueprintDrawbridgePosition position,
            bool usesPlaceholderImage)
        {
            if (!usesPlaceholderImage ||
                !reportedDrawbridgePlaceholders.Add(position))
            {
                return;
            }

            string reason = position == BlueprintDrawbridgePosition.Unknown
                ? "no unique adjacent directional gatehouse was found"
                : "a dedicated view from behind the gatehouse is still missing";
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Drawbridge Blueprint uses a temporary image: " +
                $"position={position}, reason={reason}. " +
                $"Replace this position's directional image when available.");
        }

        private Sprite GetBuildMenuSprite(
            AivMapperInfo mapper,
            string resourceKey)
        {
            if (buildMenuSprites.TryGetValue(
                    resourceKey,
                    out Sprite cached))
            {
                return cached;
            }

            Noesis.BitmapSource source =
                Noesis.GUI.GetApplicationResources()?[resourceKey]
                    as Noesis.BitmapSource;
            if (source == null)
                throw new InvalidOperationException(
                    $"Vanilla resource '{resourceKey}' is unavailable.");

            Sprite sprite =
                CreateSpriteFromVanillaAtlas(
                    source,
                    resourceKey);
            Object.DontDestroyOnLoad(sprite);
            buildMenuSprites.Add(resourceKey, sprite);
            float normalizedPivotY =
                sprite.pivot.y / Math.Max(1f, sprite.rect.height);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Loaded Vanilla build-menu icon: mapper={mapper.Name}, " +
                $"resource='{resourceKey}', size=" +
                $"{sprite.rect.width}x{sprite.rect.height}, " +
                $"pivot=(0.5, {normalizedPivotY:F4}).");
            return sprite;
        }

        private bool TryGetHelpImageSprite(
            string fileName,
            BlueprintHelpImageCleanup cleanup,
            out Sprite sprite)
        {
            sprite = null;
            if (helpImageSprites.TryGetValue(fileName, out Sprite cached))
            {
                sprite = cached;
                return true;
            }
            if (failedHelpImages.Contains(fileName))
                return false;

            string fullPath = Path.Combine(
                Application.streamingAssetsPath,
                "Help",
                "Images",
                fileName);
            try
            {
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException(
                        "Vanilla help image was not found.",
                        fullPath);

                byte[] pngBytes = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.ARGB32,
                    false);
                texture.name = "SpawnCastle_Help_" +
                    Path.GetFileNameWithoutExtension(fileName);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(
                        texture,
                        pngBytes,
                        false))
                {
                    Object.Destroy(texture);
                    throw new InvalidDataException(
                        $"Unity could not decode '{fullPath}'.");
                }

                Color32[] pixels = texture.GetPixels32();
                ApplyHelpImageCleanup(
                    pixels,
                    texture.width,
                    texture.height,
                    cleanup);
                if (!TryFindAlphaBounds(
                        pixels,
                        texture.width,
                        texture.height,
                        out RectInt alphaBounds))
                {
                    Object.Destroy(texture);
                    throw new InvalidDataException(
                        $"Vanilla help image '{fullPath}' has no visible pixels.");
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var spriteRect = new Rect(
                    alphaBounds.x,
                    alphaBounds.y,
                    alphaBounds.width,
                    alphaBounds.height);
                // Ground alignment is applied in world space per mapper. A
                // centred source pivot keeps image cropping out of that math.
                var pivot = new Vector2(0.5f, 0.5f);
                sprite = Sprite.Create(
                    texture,
                    spriteRect,
                    pivot,
                    BuildMenuPixelsPerWorldUnit,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = texture.name;
                Object.DontDestroyOnLoad(texture);
                Object.DontDestroyOnLoad(sprite);
                helpImageSprites.Add(fileName, sprite);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded clean Vanilla help image for Blueprint: " +
                    $"file='{fileName}', source={texture.width}x{texture.height}, " +
                    $"alphaBounds={alphaBounds}, cleanup={cleanup}.");
                return true;
            }
            catch (Exception ex)
            {
                failedHelpImages.Add(fileName);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Vanilla help image '{fileName}' is unavailable; " +
                    $"falling back to its build-menu icon: {ex.Message}");
                return false;
            }
        }

        private static void ApplyHelpImageCleanup(
            Color32[] pixels,
            int width,
            int height,
            BlueprintHelpImageCleanup cleanup)
        {
            if (cleanup == BlueprintHelpImageCleanup.None)
                return;

            // Four legacy Help PNGs contain the same opaque corrupt wedge in
            // their bottom 35 rows. Remove it before calculating alpha bounds.
            ClearRectangle(
                pixels,
                width,
                height,
                0,
                0,
                width,
                Math.Min(35, height));

            if (cleanup != BlueprintHelpImageCleanup.RemoveTannerArtifacts)
                return;

            // The Tanner source additionally contains stretched edge pixels.
            // These masks only cover the corrupt bands outside the workshop.
            ClearTopOriginRectangle(
                pixels,
                width,
                height,
                0,
                0,
                38,
                46);
            ClearTopOriginRectangle(
                pixels,
                width,
                height,
                Math.Max(0, width - 35),
                0,
                35,
                150);
            ClearTopOriginRectangle(
                pixels,
                width,
                height,
                0,
                105,
                30,
                Math.Max(0, height - 140));
            ClearTopOriginRectangle(
                pixels,
                width,
                height,
                Math.Max(0, width - 20),
                80,
                20,
                Math.Max(0, height - 115));
        }

        private static void ClearTopOriginRectangle(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int topY,
            int rectangleWidth,
            int rectangleHeight)
        {
            int unityY = height - topY - rectangleHeight;
            ClearRectangle(
                pixels,
                width,
                height,
                x,
                unityY,
                rectangleWidth,
                rectangleHeight);
        }

        private static void ClearRectangle(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            int rectangleWidth,
            int rectangleHeight)
        {
            int minimumX = Math.Max(0, x);
            int maximumX = Math.Min(width, x + rectangleWidth);
            int minimumY = Math.Max(0, y);
            int maximumY = Math.Min(height, y + rectangleHeight);
            for (int pixelY = minimumY; pixelY < maximumY; pixelY++)
            {
                int row = pixelY * width;
                for (int pixelX = minimumX;
                     pixelX < maximumX;
                     pixelX++)
                {
                    pixels[row + pixelX].a = 0;
                }
            }
        }

        private static bool TryFindAlphaBounds(
            Color32[] pixels,
            int width,
            int height,
            out RectInt bounds)
        {
            int minimumX = width;
            int maximumX = -1;
            int minimumY = height;
            int maximumY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 8)
                        continue;

                    minimumX = Math.Min(minimumX, x);
                    maximumX = Math.Max(maximumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumY = Math.Max(maximumY, y);
                }
            }

            if (maximumX < minimumX || maximumY < minimumY)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
            return true;
        }

        private static bool UsesIslamicChurchSkin()
        {
            return GameData.Instance != null &&
                GameData.Instance.lastGameState != null &&
                BlueprintBuildingIconCatalog.IsIslamicLordType(
                    GameData.Instance.lastGameState.lord_Type);
        }

        private static int GetCameraQuarter()
        {
            if (GameMap.instance == null)
                return 0;
            switch (GameMap.instance.CurrentRotation())
            {
                case Enums.Dircs.East:
                    return 1;
                case Enums.Dircs.South:
                    return 2;
                case Enums.Dircs.West:
                    return 3;
                default:
                    return 0;
            }
        }

        private Sprite CreateSpriteFromVanillaAtlas(
            Noesis.BitmapSource source,
            string resourceKey)
        {
            if (!(source is Noesis.CroppedBitmap cropped))
                throw new InvalidOperationException(
                    $"Vanilla resource '{resourceKey}' is not an atlas crop.");
            if (!(cropped.Source is Noesis.BitmapImage atlasSource) ||
                atlasSource.UriSource == null)
            {
                throw new InvalidOperationException(
                    $"Vanilla resource '{resourceKey}' has no atlas URI.");
            }
            if (NoesisTextureCacheField == null)
                throw new MissingFieldException(
                    typeof(NoesisTextureProvider).FullName,
                    "_textures");

            var cache = NoesisTextureCacheField.GetValue(
                    NoesisTextureProvider.instance)
                as Dictionary<string, NoesisTextureProvider.Value>;
            string uri = atlasSource.UriSource.OriginalString.TrimStart('/');
            if (cache == null ||
                !cache.TryGetValue(
                    uri,
                    out NoesisTextureProvider.Value atlasValue) ||
                !(atlasValue.texture is Texture2D atlasTexture))
            {
                throw new InvalidOperationException(
                    $"Unity atlas texture '{uri}' is not registered.");
            }

            Noesis.Int32Rect crop = cropped.SourceRect;
            Noesis.Int32Rect atlasRect = atlasValue.rect;
            // The per-mapper world offset aligns the visible building centre
            // to its footprint, so every cached source uses a neutral pivot.
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float unityY =
                atlasRect.Y + atlasRect.Height - crop.Y - crop.Height;
            var spriteRect = new Rect(
                atlasRect.X + crop.X,
                unityY,
                crop.Width,
                crop.Height);
            if (spriteRect.xMin < 0f ||
                spriteRect.yMin < 0f ||
                spriteRect.xMax > atlasTexture.width ||
                spriteRect.yMax > atlasTexture.height)
            {
                throw new InvalidOperationException(
                    $"Atlas crop '{resourceKey}' is outside texture bounds: " +
                    $"crop={crop}, atlasRect={atlasRect}, " +
                    $"texture={atlasTexture.width}x{atlasTexture.height}.");
            }

            Sprite sprite = Sprite.Create(
                atlasTexture,
                spriteRect,
                pivot,
                BuildMenuPixelsPerWorldUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "SpawnCastle_" +
                resourceKey.Replace(' ', '_');
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla build-menu atlas linked: resource='{resourceKey}', " +
                $"uri='{uri}', crop={crop}, unityRect={spriteRect}, " +
                $"pivot={pivot}.");
            return sprite;
        }

        private bool TryCreateIcon(
            BlueprintIconPlacement placement,
            BlueprintIconVisual icon,
            bool flattenedLandscape,
            float iconScale,
            float iconAlpha,
            int sortingBandOffset)
        {
            Sprite sprite = icon.Sprite;
            AivMapperInfo mapper =
                AivMapperCatalog.Resolve(placement.MapperValue);
            bool isBuildingIcon =
                mapper.Category == AivItemCategory.Building ||
                icon.UsesHelpImage;
            float normalScale = 0f;
            if (!flattenedLandscape &&
                isBuildingIcon &&
                !TryResolveBuildingScale(
                    placement,
                    icon,
                    mapper,
                    sprite,
                    out normalScale))
            {
                return false;
            }

            Vector3 position = Vector3.zero;
            int validGroundCells = 0;
            int minimumDepthRow = int.MaxValue;
            int maximumDepthRow = int.MinValue;
            AccumulateIconFootprint(
                placement,
                icon.UsesExactWorldScale &&
                    BlueprintBuildingIconCatalog.HasReservedPlacementArea(
                        mapper.Name),
                ref position,
                ref validGroundCells,
                ref minimumDepthRow,
                ref maximumDepthRow);

            if (validGroundCells == 0)
                return false;

            position /= validGroundCells;
            Vector3 groundPosition = position;
            bool useOriginalBuildingScale =
                !flattenedLandscape &&
                isBuildingIcon;
            float scale = flattenedLandscape && icon.UsesExactWorldScale
                ? 1f
                : useOriginalBuildingScale
                    ? normalScale
                    : CalculateCompactIconScale(placement, sprite);
            // Scale around each sprite's established pivot in both views, so
            // flat decals remain aligned while users can reduce visual clutter.
            scale *= iconScale;
            if (useOriginalBuildingScale &&
                !icon.UsesExactWorldScale)
            {
                Vector2 visualOffset = ResolveVisualCenterOffset(
                    placement,
                    mapper,
                    sprite,
                    scale);
                if (icon.FlipHorizontally)
                    visualOffset.x = -visualOffset.x;
                position.x += visualOffset.x;
                position.y += visualOffset.y;
            }

            var iconObject = new GameObject(
                icon.UsesPlaceholderImage
                    ? $"BlueprintIcon_{placement.MapperValue}_" +
                        $"{icon.DrawbridgePosition}_Placeholder"
                    : $"BlueprintIcon_{placement.MapperValue}");
            iconObject.transform.SetParent(overlayRoot.transform, false);
            iconObject.transform.position = position;
            bool transformFlip = icon.FlipHorizontally && !icon.UsesFlatMesh;
            float baseScale = scale / iconScale;
            iconObject.transform.localScale =
                new Vector3(
                    transformFlip ? -scale : scale,
                    scale,
                    1f);
            trackedIconRoots.Add(new TrackedIconRoot(
                iconObject.transform,
                transformFlip,
                baseScale,
                groundPosition,
                new Vector2(
                    (position.x - groundPosition.x) / iconScale,
                    (position.y - groundPosition.y) / iconScale)));
            if (flattenedLandscape && icon.UsesFlatMesh)
            {
                return TryCreateFlattenedMesh(
                    iconObject,
                    placement,
                    icon,
                    mapper,
                    iconAlpha,
                    minimumDepthRow,
                    maximumDepthRow,
                    sortingBandOffset);
            }

            SpriteRenderer renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, iconAlpha);
            alphaRenderers.Add(renderer);
            int middleDepthRow = BlueprintFragmentCaptureCatalog
                .GetMiddleDepthRow(minimumDepthRow, maximumDepthRow);
            renderer.sortingOrder =
                -20000 + middleDepthRow * 49 + 4 + sortingBandOffset;

            return true;
        }

        private bool TryCreateFlattenedMesh(
            GameObject root,
            BlueprintIconPlacement placement,
            BlueprintIconVisual icon,
            AivMapperInfo mapper,
            float iconAlpha,
            int minimumDepthRow,
            int maximumDepthRow,
            int sortingBandOffset)
        {
            Sprite source = icon.Sprite;
            if (source == null || source.texture == null)
                return false;
            bool useMarkerBounds = BlueprintBuildingIconCatalog.HasReservedPlacementArea(mapper.Name);
            int minimumWorldX = useMarkerBounds ? placement.MarkerMinimumWorldX : placement.MinimumWorldX;
            int maximumWorldX = useMarkerBounds ? placement.MarkerMaximumWorldX : placement.MaximumWorldX;
            int minimumWorldY = useMarkerBounds ? placement.MarkerMinimumWorldY : placement.MinimumWorldY;
            int maximumWorldY = useMarkerBounds ? placement.MarkerMaximumWorldY : placement.MaximumWorldY;
            int cellCountX = maximumWorldX - minimumWorldX + 1;
            int cellCountY = maximumWorldY - minimumWorldY + 1;
            if (!TryGetGroundPosition(minimumWorldX, minimumWorldY, out Vector3 anchor) ||
                !TryGetGroundBasis(minimumWorldX, minimumWorldY, anchor, 1, 0, out Vector2 basisX) ||
                !TryGetGroundBasis(minimumWorldX, minimumWorldY, anchor, 0, 1, out Vector2 basisY))
            {
                return false;
            }

            Vector2 origin = new Vector2(anchor.x, anchor.y) - basisX * 0.5f - basisY * 0.5f;
            Vector2 cornerX = origin + basisX * cellCountX;
            Vector2 cornerY = origin + basisY * cellCountY;
            Vector2 opposite = cornerX + basisY * cellCountY;
            float left = Math.Min(Math.Min(origin.x, cornerX.x), Math.Min(cornerY.x, opposite.x));
            float right = Math.Max(Math.Max(origin.x, cornerX.x), Math.Max(cornerY.x, opposite.x));
            float bottom = Math.Min(Math.Min(origin.y, cornerX.y), Math.Min(cornerY.y, opposite.y));
            float top = Math.Max(Math.Max(origin.y, cornerX.y), Math.Max(cornerY.y, opposite.y));
            if (right - left < 0.00001f || top - bottom < 0.00001f)
                return false;

            int spriteId = source.GetInstanceID();
            RectInt alphaBounds = icon.AlphaBounds;
            if (alphaBounds.width <= 0 || alphaBounds.height <= 0 ||
                alphaBounds.x < 0 || alphaBounds.y < 0 ||
                alphaBounds.xMax > source.texture.width ||
                alphaBounds.yMax > source.texture.height)
            {
                return false;
            }

            string cacheKey = spriteId + ":" + cellCountX + "x" + cellCountY + ":" +
                GetCameraQuarter() + ":" + icon.FlipHorizontally;
            if (!flattenedBuildingMeshes.TryGetValue(cacheKey, out Mesh mesh))
            {
                Vector2 rootPosition = new Vector2(root.transform.position.x, root.transform.position.y);
                var points = new[] { origin, cornerX, opposite, cornerY };
                var vertices = new Vector3[4];
                var uv = new Vector2[4];
                for (int index = 0; index < points.Length; index++)
                {
                    vertices[index] = new Vector3(
                        points[index].x - rootPosition.x,
                        points[index].y - rootPosition.y,
                        0f);
                    uv[index] = CalculateFlatUv(
                        points[index],
                        left,
                        right,
                        bottom,
                        top,
                        alphaBounds,
                        source.texture.width,
                        source.texture.height,
                        icon.FlipHorizontally);
                }
                float determinant = basisX.x * basisY.y - basisX.y * basisY.x;
                int[] triangles = determinant >= 0f
                    ? new[] { 0, 3, 1, 1, 3, 2 }
                    : new[] { 0, 1, 3, 1, 2, 3 };
                mesh = new Mesh { name = "SpawnCastle_FlatMesh_" + cacheKey.Replace(':', '_') };
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = triangles;
                mesh.colors32 = new[]
                {
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255),
                    new Color32(255, 255, 255, 255)
                };
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                Object.DontDestroyOnLoad(mesh);
                flattenedBuildingMeshes.Add(cacheKey, mesh);
            }

            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMeshMaterial(source.texture);
            ApplyAlpha(renderer, iconAlpha);
            alphaRenderers.Add(renderer);
            int middleDepthRow = BlueprintFragmentCaptureCatalog.GetMiddleDepthRow(
                minimumDepthRow,
                maximumDepthRow);
            renderer.sortingOrder = -20000 + middleDepthRow * 49 + 4 + sortingBandOffset;
            return true;
        }

        private Material GetMeshMaterial(Texture texture)
        {
            int key = texture.GetInstanceID();
            if (meshMaterials.TryGetValue(key, out Material material))
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("No compatible unlit sprite shader is available.");
            material = new Material(shader)
            {
                name = texture.name + "_BlueprintMeshMaterial",
                mainTexture = texture
            };
            Object.DontDestroyOnLoad(material);
            meshMaterials.Add(key, material);
            return material;
        }

        private static Vector2 CalculateFlatUv(
            Vector2 point,
            float left,
            float right,
            float bottom,
            float top,
            RectInt bounds,
            int textureWidth,
            int textureHeight,
            bool flipHorizontally)
        {
            float normalizedX = (point.x - left) / (right - left);
            float normalizedY = (point.y - bottom) / (top - bottom);
            if (flipHorizontally)
                normalizedX = 1f - normalizedX;
            float pixelX = bounds.x + normalizedX * Math.Max(0, bounds.width - 1) + 0.5f;
            float pixelY = bounds.y + normalizedY * Math.Max(0, bounds.height - 1) + 0.5f;
            return new Vector2(pixelX / textureWidth, pixelY / textureHeight);
        }

        private Vector2 ResolveVisualCenterOffset(
            BlueprintIconPlacement placement,
            AivMapperInfo mapper,
            Sprite sprite,
            float scale)
        {
            Vector3 spriteSize = sprite.bounds.size;
            var renderedWorldSize = new Vector2(
                spriteSize.x * Math.Abs(scale),
                spriteSize.y * Math.Abs(scale));
            if (sizeCalibration.TryGetVisualCenterOffset(
                    placement.MapperValue,
                    UsesIslamicChurchSkin(),
                    renderedWorldSize,
                    out Vector2 calibratedOffset))
            {
                missingGroundOffsetMappers.Remove(placement.MapperValue);
                return calibratedOffset;
            }

            float visualHeight = renderedWorldSize.y;
            float fallbackY = BlueprintBuildingIconCatalog
                .CalculateFootprintVisualCenterOffsetY(
                    placement.Size,
                    visualHeight);
            if (missingGroundOffsetMappers.Add(placement.MapperValue))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint icon uses a geometric ground-alignment " +
                    $"fallback: mapper={mapper.Name} " +
                    $"({placement.MapperValue}), footprint={placement.Size}, " +
                    $"visualHeight={visualHeight:F4}, offset=(0," +
                    $"{fallbackY:F4}). Hold its Vanilla construction " +
                    $"preview over level terrain for at least 0.4 seconds " +
                    $"to record the exact alignment.");
            }

            return new Vector2(0f, fallbackY);
        }

        private bool TryResolveBuildingScale(
            BlueprintIconPlacement placement,
            BlueprintIconVisual icon,
            AivMapperInfo mapper,
            Sprite sprite,
            out float scale)
        {
            Vector3 spriteSize = sprite.bounds.size;
            if (icon.UsesExactWorldScale)
            {
                // Directional Drawbridge captures already contain exactly five
                // 64-PPU world tiles; their differing heights are real views.
                scale = 1f;
                missingScaleMappers.Remove(placement.MapperValue);
                return true;
            }

            Vector2 calibratedWorldSize =
                sizeCalibration.TryGetWorldSize(
                    placement.MapperValue,
                    UsesIslamicChurchSkin(),
                    out Vector2 measuredSize)
                    ? measuredSize
                    : Vector2.zero;
            if (BlueprintBuildingIconCatalog.TryCalculateNormalWorldScale(
                    mapper.Name,
                    spriteSize.x,
                    spriteSize.y,
                    calibratedWorldSize.x,
                    calibratedWorldSize.y,
                    icon.UsesHelpImage,
                    out scale))
            {
                missingScaleMappers.Remove(placement.MapperValue);
                return true;
            }

            if (BlueprintBuildingIconCatalog
                .TryCalculateFootprintEstimatedScale(
                    placement.Size,
                    spriteSize.x,
                    out scale))
            {
                if (missingScaleMappers.Add(placement.MapperValue))
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Blueprint icon uses an estimated scale: " +
                        $"no measured or fixed scale is defined for " +
                        $"mapper={mapper.Name} " +
                        $"({placement.MapperValue}); the complete " +
                        $"icon width is fitted to footprint=" +
                        $"{placement.Size}. Hold its Vanilla build " +
                        $"preview over the map to calibrate it.");
                }
                return true;
            }

            if (missingScaleMappers.Add(placement.MapperValue))
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Blueprint icon skipped: no scale is " +
                    $"defined and no meaningful footprint " +
                    $"estimate is possible for mapper=" +
                    $"{mapper.Name} ({placement.MapperValue}), " +
                    $"footprint={placement.Size}, iconWidth=" +
                    $"{spriteSize.x:F4}. The colored " +
                    $"footprint and all other icons remain " +
                    $"visible.");
            }
            return false;
        }

        private void AccumulateIconFootprint(
            BlueprintIconPlacement placement,
            bool useMarkerBounds,
            ref Vector3 position,
            ref int validGroundCells,
            ref int minimumDepthRow,
            ref int maximumDepthRow)
        {
            int minimumWorldX = useMarkerBounds
                ? placement.MarkerMinimumWorldX
                : placement.MinimumWorldX;
            int maximumWorldX = useMarkerBounds
                ? placement.MarkerMaximumWorldX
                : placement.MaximumWorldX;
            int minimumWorldY = useMarkerBounds
                ? placement.MarkerMinimumWorldY
                : placement.MinimumWorldY;
            int maximumWorldY = useMarkerBounds
                ? placement.MarkerMaximumWorldY
                : placement.MaximumWorldY;
            for (int worldY = minimumWorldY;
                worldY <= maximumWorldY;
                worldY++)
            {
                for (int worldX = minimumWorldX;
                    worldX <= maximumWorldX;
                    worldX++)
                {
                    if (!TryGetRenderedTile(
                            worldX,
                            worldY,
                            out GameMapTile mapTile,
                            out Vector3Int tilePosition))
                    {
                        continue;
                    }

                    // Match the marker/calibration reference on uneven ground;
                    // corner heights alone can bias the building vertically.
                    position += GetGroundCellCenter(mapTile, tilePosition);
                    validGroundCells++;
                    minimumDepthRow = Math.Min(
                        minimumDepthRow,
                        mapTile.row);
                    maximumDepthRow = Math.Max(
                        maximumDepthRow,
                        mapTile.row);
                }
            }
        }

        private static float CalculateCompactIconScale(
            BlueprintIconPlacement placement,
            Sprite icon)
        {
            // Flat mode keeps the former footprint-constrained overview size;
            // non-building path icons also stay compact in the normal view.
            float targetWidth = placement.Size == 1
                ? 1f
                : Math.Max(1.5f, placement.Size * 1.35f);
            float targetHeight = placement.Size == 1
                ? 0.5f
                : Math.Max(0.75f, placement.Size * 0.52f);
            Vector3 iconSize = icon.bounds.size;
            return Math.Min(
                targetWidth / Math.Max(0.01f, iconSize.x),
                targetHeight / Math.Max(0.01f, iconSize.y));
        }

        private static Vector3 GetGroundCellCenter(
            GameMapTile mapTile,
            Vector3Int tilePosition)
        {
            Vector3 worldPosition = mapTile.tilemapRef.GetCellCenterWorld(
                new Vector3Int(tilePosition.x, tilePosition.y, 0));
            Vector3 sortingPosition = GameMap.instance.getSpritePosVector(
                tilePosition.x,
                tilePosition.y);
            // The cell center fixes the ground pivot while Vanilla's sprite
            // position retains its proven depth value for row sorting.
            worldPosition.z = sortingPosition.z;
            // Vanilla applies the native display height in both map views;
            // flat mode already supplies its own unified height value.
            worldPosition.y += mapTile.height;
            return worldPosition;
        }

        private static Color GetOverlayColor(
            AivItemCategory category,
            AivVisualGroup visualGroup)
        {
            switch (category)
            {
                case AivItemCategory.HighWallPath:
                    return new Color(0.35f, 0.68f, 1f);
                case AivItemCategory.LowWallPath:
                    return new Color(0.72f, 0.46f, 0.20f);
                case AivItemCategory.CrenelPath:
                    return new Color(0.30f, 0.95f, 0.95f);
                case AivItemCategory.Stair:
                    return new Color(1f, 0.65f, 0.18f);
                case AivItemCategory.PitchDitchPath:
                    return new Color(1f, 0.28f, 0.08f);
                case AivItemCategory.MoatPath:
                    return new Color(0.12f, 0.48f, 1f);
                case AivItemCategory.Trap:
                    return new Color(1f, 0.12f, 0.12f);
                case AivItemCategory.Unknown:
                    return new Color(1f, 0.08f, 0.95f);
            }

            switch (visualGroup)
            {
                case AivVisualGroup.Housing:
                    return new Color(0.55f, 0.85f, 0.35f);
                case AivVisualGroup.Food:
                    return new Color(0.95f, 0.78f, 0.22f);
                case AivVisualGroup.Industry:
                    return new Color(0.95f, 0.48f, 0.18f);
                case AivVisualGroup.Storage:
                    return new Color(0.72f, 0.55f, 0.28f);
                case AivVisualGroup.Military:
                case AivVisualGroup.Defense:
                    return new Color(0.38f, 0.62f, 1f);
                case AivVisualGroup.Civic:
                    return new Color(0.72f, 0.48f, 0.95f);
                case AivVisualGroup.PositiveFear:
                    return new Color(0.38f, 0.95f, 0.58f);
                case AivVisualGroup.NegativeFear:
                    return new Color(0.95f, 0.30f, 0.30f);
                case AivVisualGroup.Water:
                    return new Color(0.18f, 0.72f, 1f);
                default:
                    return new Color(0.42f, 0.82f, 1f);
            }
        }

        private readonly struct PendingDepthIcon
        {
            public PendingDepthIcon(
                BlueprintIconPlacement placement,
                bool flipHorizontally)
            {
                Placement = placement;
                FlipHorizontally = flipHorizontally;
            }
            public BlueprintIconPlacement Placement { get; }
            public bool FlipHorizontally { get; }
        }

        private readonly struct GroundMarkerInstance
        {
            public GroundMarkerInstance(Vector3 position, Color32 color)
            {
                Position = position;
                Color = color;
            }
            public Vector3 Position { get; }
            public Color32 Color { get; }
        }

        private readonly struct TrackedIconRoot
        {
            public TrackedIconRoot(
                Transform root,
                bool flipHorizontally,
                float baseScale,
                Vector3 groundPosition,
                Vector2 positionOffsetPerScale)
            {
                Root = root;
                FlipHorizontally = flipHorizontally;
                BaseScale = baseScale;
                GroundPosition = groundPosition;
                PositionOffsetPerScale = positionOffsetPerScale;
            }
            public Transform Root { get; }
            public bool FlipHorizontally { get; }
            public float BaseScale { get; }
            public Vector3 GroundPosition { get; }
            public Vector2 PositionOffsetPerScale { get; }
        }

    }

    internal readonly struct BlueprintIconVisual
    {
        public BlueprintIconVisual(
            Sprite sprite,
            bool usesHelpImage,
            bool flipHorizontally,
            bool usesPlaceholderImage,
            BlueprintDrawbridgePosition drawbridgePosition,
            bool usesExactWorldScale,
            bool usesFlatMesh = false,
            RectInt alphaBounds = default)
        {
            Sprite = sprite;
            UsesHelpImage = usesHelpImage;
            FlipHorizontally = flipHorizontally;
            UsesPlaceholderImage = usesPlaceholderImage;
            DrawbridgePosition = drawbridgePosition;
            UsesExactWorldScale = usesExactWorldScale;
            UsesFlatMesh = usesFlatMesh;
            AlphaBounds = alphaBounds;
        }

        public Sprite Sprite { get; }

        public bool UsesHelpImage { get; }

        public bool FlipHorizontally { get; }

        public bool UsesPlaceholderImage { get; }

        public BlueprintDrawbridgePosition DrawbridgePosition { get; }

        public bool UsesExactWorldScale { get; }

        public bool UsesFlatMesh { get; }

        public RectInt AlphaBounds { get; }
    }

    internal readonly struct BlueprintRenderResult
    {
        public BlueprintRenderResult(
            int renderedTiles,
            int renderedIcons,
            int clippedTiles)
        {
            RenderedTiles = renderedTiles;
            RenderedIcons = renderedIcons;
            ClippedTiles = clippedTiles;
        }

        public int RenderedTiles { get; }
        public int RenderedIcons { get; }
        public int ClippedTiles { get; }
    }
}
