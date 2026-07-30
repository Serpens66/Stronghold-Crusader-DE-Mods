using AIVParser.Core;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpawnCastle
{
    internal sealed class BlueprintRenderer
    {
        private readonly ManualLogSource log;
        private readonly Dictionary<int, Sprite> markerSprites =
            new Dictionary<int, Sprite>();
        private readonly Dictionary<string, Sprite> iconSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> missingIconMappers = new HashSet<int>();
        private static readonly FieldInfo NoesisTextureCacheField =
            typeof(NoesisTextureProvider).GetField(
                "_textures",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject overlayRoot;

        public BlueprintRenderer(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
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
            foreach (BlueprintIconPlacement placement in layout.Icons)
            {
                Sprite icon = GetBuildingIcon(placement.MapperValue);
                if (icon == null)
                    continue;
                if (TryCreateIcon(placement, icon))
                    renderedIcons++;
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
            Vector3 worldPosition = mapTile.tilemapRef.GetCellCenterWorld(
                new Vector3Int(tilePosition.x, tilePosition.y, 0));
            Vector3 sortingPosition = GameMap.instance.getSpritePosVector(
                tilePosition.x,
                tilePosition.y);
            // Cell center supplies the correct ground pivot; Vanilla's sprite
            // position still provides the proven depth value for row sorting.
            worldPosition.z = sortingPosition.z;
            if (!EngineInterface.FlattenedLandscape)
                worldPosition.y += mapTile.height;
            marker.transform.position = worldPosition;

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

        private Sprite GetBuildingIcon(int mapperValue)
        {
            if (missingIconMappers.Contains(mapperValue))
                return null;

            try
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                string resourceKey =
                    BlueprintBuildingIconCatalog.Resolve(mapper.Name);
                if (string.IsNullOrWhiteSpace(resourceKey))
                    throw new InvalidOperationException(
                        $"No normal build-menu icon is mapped for {mapper.Name}.");
                if (iconSprites.TryGetValue(resourceKey, out Sprite cached))
                    return cached;

                Noesis.BitmapSource source =
                    Noesis.GUI.GetApplicationResources()?[resourceKey]
                        as Noesis.BitmapSource;
                if (source == null)
                    throw new InvalidOperationException(
                        $"Vanilla resource '{resourceKey}' is unavailable.");

                Sprite sprite =
                    CreateSpriteFromVanillaAtlas(source, resourceKey);
                Object.DontDestroyOnLoad(sprite);
                iconSprites.Add(resourceKey, sprite);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Loaded Vanilla build-menu icon: mapper={mapper.Name}, " +
                    $"resource='{resourceKey}', size=" +
                    $"{sprite.rect.width}x{sprite.rect.height}.");
                return sprite;
            }
            catch (Exception ex)
            {
                missingIconMappers.Add(mapperValue);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint icon unavailable for mapper {mapperValue}; " +
                    $"the colored footprint remains visible: {ex.Message}");
                return null;
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
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "SpawnCastle_" +
                resourceKey.Replace(' ', '_');
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla build-menu atlas linked: resource='{resourceKey}', " +
                $"uri='{uri}', crop={crop}, unityRect={spriteRect}.");
            return sprite;
        }

        private bool TryCreateIcon(
            BlueprintIconPlacement placement,
            Sprite icon)
        {
            var corners = new[]
            {
                new BlueprintWorldTile(
                    placement.MinimumWorldX,
                    placement.MinimumWorldY),
                new BlueprintWorldTile(
                    placement.MinimumWorldX,
                    placement.MaximumWorldY),
                new BlueprintWorldTile(
                    placement.MaximumWorldX,
                    placement.MinimumWorldY),
                new BlueprintWorldTile(
                    placement.MaximumWorldX,
                    placement.MaximumWorldY)
            };

            Vector3 position = Vector3.zero;
            int validCorners = 0;
            int frontRow = 0;
            foreach (BlueprintWorldTile corner in corners)
            {
                if (!TryGetRenderedTile(
                        corner.X,
                        corner.Y,
                        out GameMapTile mapTile,
                        out Vector3Int tilePosition))
                {
                    continue;
                }

                Vector3 cornerPosition = GameMap.instance.getSpritePosVector(
                    tilePosition.x,
                    tilePosition.y);
                if (!EngineInterface.FlattenedLandscape)
                    cornerPosition.y += mapTile.height;
                position += cornerPosition;
                validCorners++;
                frontRow = Math.Max(frontRow, mapTile.row);
            }

            if (validCorners == 0)
                return false;

            position /= validCorners;
            var iconObject = new GameObject(
                $"BlueprintIcon_{placement.MapperValue}");
            iconObject.transform.SetParent(overlayRoot.transform, false);
            iconObject.transform.position = position;
            SpriteRenderer renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = icon;
            renderer.color = new Color(1f, 1f, 1f, 0.68f);
            renderer.sortingOrder = -20000 + frontRow * 49 + 4;

            float targetWidth = Math.Max(1.5f, placement.Size * 1.35f);
            float targetHeight = Math.Max(0.75f, placement.Size * 0.52f);
            float scale = Math.Min(
                targetWidth / Math.Max(0.01f, icon.bounds.size.x),
                targetHeight / Math.Max(0.01f, icon.bounds.size.y));
            iconObject.transform.localScale =
                new Vector3(scale, scale, 1f);
            return true;
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
