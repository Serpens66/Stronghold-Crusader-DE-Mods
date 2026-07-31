using AIVParser.Core;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<int, Sprite> markerSprites =
            new Dictionary<int, Sprite>();
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
        private static readonly FieldInfo NoesisTextureCacheField =
            typeof(NoesisTextureProvider).GetField(
                "_textures",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject overlayRoot;

        public BlueprintRenderer(
            ManualLogSource log,
            BlueprintBuildingSizeCalibration sizeCalibration)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.sizeCalibration = sizeCalibration ??
                throw new ArgumentNullException(nameof(sizeCalibration));
        }

        public BlueprintRenderResult Render(BlueprintLayout layout)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (GameMap.instance == null || TilemapManager.instance == null)
                throw new InvalidOperationException("The game tilemap is not ready.");

            Clear();
            overlayRoot = new GameObject("SpawnCastle_BlueprintOverlay");

            int clippedTiles = 0;
            int renderedTiles = 0;
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

                Sprite markerSprite = GetMarkerSprite(
                    placement.Category,
                    placement.VisualGroup);
                CreateVisibleGroundMarker(mapTile, position, markerSprite);
                renderedTiles++;
            }

            int renderedIcons = 0;
            bool flattenedLandscape = EngineInterface.FlattenedLandscape;
            foreach (BlueprintIconPlacement placement in layout.Icons)
            {
                BlueprintDrawbridgePosition drawbridgePosition =
                    ResolveDrawbridgePosition(placement);
                BlueprintIconVisual icon =
                    GetBlueprintIcon(
                        placement.MapperValue,
                        flattenedLandscape,
                        drawbridgePosition);
                if (icon.Sprite == null)
                    continue;
                if (TryCreateIcon(
                        placement,
                        icon,
                        flattenedLandscape))
                {
                    renderedIcons++;
                }
            }

            return new BlueprintRenderResult(
                renderedTiles,
                renderedIcons,
                clippedTiles);
        }

        public void Clear()
        {
            if (overlayRoot != null)
            {
                // Destroy is deferred until the end of the frame. Deactivate
                // first so a stale projection cannot flash for one more frame.
                overlayRoot.SetActive(false);
                Object.Destroy(overlayRoot);
                overlayRoot = null;
            }
        }

        private void CreateVisibleGroundMarker(
            GameMapTile mapTile,
            Vector3Int tilePosition,
            Sprite sprite)
        {
            if (overlayRoot == null || sprite == null)
                return;

            var marker = new GameObject("BlueprintGroundMarker");
            marker.transform.SetParent(overlayRoot.transform, false);
            marker.transform.position =
                GetGroundCellCenter(mapTile, tilePosition);

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = -20000 + mapTile.row * 49 + 1;
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

        private Sprite GetMarkerSprite(
            AivItemCategory category,
            AivVisualGroup visualGroup)
        {
            int key = ((int)category << 16) | (int)visualGroup;
            if (markerSprites.TryGetValue(key, out Sprite cached))
                return cached;

            Color baseColor = GetOverlayColor(category, visualGroup);
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.ARGB32,
                false);
            texture.name = $"SpawnCastle_BlueprintTile_{key}";
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

                    Color pixel = baseColor;
                    pixel.a = distance >= 0.84f ? 0.78f : 0.34f;
                    pixels[y * width + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                64f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;

            Object.DontDestroyOnLoad(texture);
            Object.DontDestroyOnLoad(sprite);
            markerSprites.Add(key, sprite);
            return sprite;
        }

        private BlueprintIconVisual GetBlueprintIcon(
            int mapperValue,
            bool flattenedLandscape,
            BlueprintDrawbridgePosition drawbridgePosition)
        {
            string iconKey = mapperValue + ":" + drawbridgePosition;
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
            bool flattenedLandscape)
        {
            Sprite sprite = icon.Sprite;
            AivMapperInfo mapper =
                AivMapperCatalog.Resolve(placement.MapperValue);
            bool isBuildingIcon =
                mapper.Category == AivItemCategory.Building ||
                icon.UsesHelpImage;
            float normalScale = 0f;
            if (isBuildingIcon &&
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
            int frontRow = 0;
            AccumulateIconFootprint(
                placement,
                ref position,
                ref validGroundCells,
                ref frontRow);

            if (validGroundCells == 0)
                return false;

            position /= validGroundCells;
            bool useOriginalBuildingScale =
                !flattenedLandscape &&
                isBuildingIcon;
            float scale = useOriginalBuildingScale
                ? normalScale
                : CalculateCompactIconScale(placement, sprite);
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
            SpriteRenderer renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, 0.68f);
            renderer.sortingOrder = -20000 + frontRow * 49 + 4;

            iconObject.transform.localScale =
                new Vector3(
                    icon.FlipHorizontally ? -scale : scale,
                    scale,
                    1f);
            return true;
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
            ref Vector3 position,
            ref int validGroundCells,
            ref int frontRow)
        {
            for (int worldY = placement.MinimumWorldY;
                worldY <= placement.MaximumWorldY;
                worldY++)
            {
                for (int worldX = placement.MinimumWorldX;
                    worldX <= placement.MaximumWorldX;
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
                    frontRow = Math.Max(frontRow, mapTile.row);
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
            if (!EngineInterface.FlattenedLandscape)
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

    }

    internal readonly struct BlueprintIconVisual
    {
        public BlueprintIconVisual(
            Sprite sprite,
            bool usesHelpImage,
            bool flipHorizontally,
            bool usesPlaceholderImage,
            BlueprintDrawbridgePosition drawbridgePosition,
            bool usesExactWorldScale)
        {
            Sprite = sprite;
            UsesHelpImage = usesHelpImage;
            FlipHorizontally = flipHorizontally;
            UsesPlaceholderImage = usesPlaceholderImage;
            DrawbridgePosition = drawbridgePosition;
            UsesExactWorldScale = usesExactWorldScale;
        }

        public Sprite Sprite { get; }

        public bool UsesHelpImage { get; }

        public bool FlipHorizontally { get; }

        public bool UsesPlaceholderImage { get; }

        public BlueprintDrawbridgePosition DrawbridgePosition { get; }

        public bool UsesExactWorldScale { get; }
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
