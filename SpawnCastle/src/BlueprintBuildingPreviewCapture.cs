using AIVParser.Core;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
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
    internal sealed class BlueprintBuildingPreviewCapture
    {
        private const float SampleIntervalSeconds = 0.12f;
        private const int StableSamplesRequired = 4;
        private const int ScanRadius = 64;
        private const int TilemapSize = 800;
        private const float FlatHeightTolerance = 0.01f;
        private const int CaptureLayer = 31;
        private const int PlacedScanRadius = 10;
        private const int HighCrenelToolMapperValue = 26;
        private const int StairToolMapperValue = 27;
        private const int LowCrenelToolMapperValue = 35;

        private readonly ManualLogSource log;
        private readonly BlueprintBuildingImageLibrary library;
        private float nextSampleTime;
        private string candidateKey = string.Empty;
        private string candidateSignature = string.Empty;
        private int candidateStableSamples;
        private float nextFailureDiagnosticTime;

        public BlueprintBuildingPreviewCapture(
            ManualLogSource log,
            BlueprintBuildingImageLibrary library)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public bool Tick()
        {
            if (Time.unscaledTime < nextSampleTime)
                return false;

            nextSampleTime = Time.unscaledTime + SampleIntervalSeconds;
            if (TryTickPlacedStructureCapture(out bool placedCaptureCompleted))
                return placedCaptureCompleted;

            if (!TryGetCaptureContext(
                    out int mapperValue,
                    out AivMapperInfo mapper,
                    out BlueprintCaptureRequest request) ||
                library.Contains(request))
            {
                ResetCandidate();
                return false;
            }

            if (!TryCollectPreview(
                    mapper,
                    out List<PreviewFragment> fragments,
                    out Vector2 groundCenter,
                    out string failureReason))
            {
                if (Time.unscaledTime >= nextFailureDiagnosticTime)
                {
                    nextFailureDiagnosticTime = Time.unscaledTime + 2f;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Blueprint preview capture is waiting: mapper={mapper.Name} " +
                        $"({mapperValue}), key={request.Key}, reason={failureReason}");
                }
                ResetCandidate();
                return false;
            }

            string signature = BuildSignature(fragments);
            if (candidateKey == request.Key && candidateSignature == signature)
                candidateStableSamples++;
            else
            {
                candidateKey = request.Key;
                candidateSignature = signature;
                candidateStableSamples = 1;
            }

            if (candidateStableSamples < StableSamplesRequired)
                return false;

            ResetCandidate();
            try
            {
                SaveCapture(mapperValue, mapper.Name, request, fragments, groundCenter, signature);
                library.Reload();
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Vanilla Blueprint preview could not be captured: " +
                    $"mapper={mapper.Name} ({mapperValue}), key={request.Key}, " +
                    $"signature={signature}, error={ex}");
                return false;
            }
        }

        private bool TryTickPlacedStructureCapture(out bool captureCompleted)
        {
            captureCompleted = false;
            if (EngineInterface.FlattenedLandscape ||
                MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                GameMap.instance == null)
            {
                return false;
            }

            int toolMapperValue = MainControls.instance.CurrentSubAction;
            if (toolMapperValue != HighCrenelToolMapperValue &&
                toolMapperValue != LowCrenelToolMapperValue &&
                toolMapperValue != StairToolMapperValue)
            {
                return false;
            }

            if (!TryCollectPlacedCaptureTargets(
                    toolMapperValue,
                    out List<PlacedCaptureTarget> targets,
                    out string failureReason))
            {
                LogWaitingForPlacedCapture(toolMapperValue, failureReason);
                ResetCandidate();
                return true;
            }

            List<PlacedCaptureTarget> missingTargets = targets
                .Where(value => !library.Contains(value.Request))
                .GroupBy(value => value.Request.Key, StringComparer.Ordinal)
                .Select(value => value.First())
                .ToList();
            if (missingTargets.Count == 0)
            {
                ResetCandidate();
                return true;
            }

            string batchKey = string.Join(
                ";",
                missingTargets.Select(value => value.Request.Key));
            string batchSignature = string.Join(
                ";",
                missingTargets.Select(value => value.Signature));
            if (candidateKey == batchKey && candidateSignature == batchSignature)
                candidateStableSamples++;
            else
            {
                candidateKey = batchKey;
                candidateSignature = batchSignature;
                candidateStableSamples = 1;
            }

            if (candidateStableSamples < StableSamplesRequired)
                return true;

            ResetCandidate();
            try
            {
                foreach (PlacedCaptureTarget target in missingTargets)
                {
                    SaveCapture(
                        target.MapperValue,
                        target.MapperName,
                        target.Request,
                        new[] { target.Fragment },
                        target.GroundCenter,
                        target.Signature);
                }
                library.Reload();
                captureCompleted = true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Placed Blueprint visuals could not be captured: " +
                    $"toolMapper={toolMapperValue}, keys={batchKey}, error={ex}");
            }
            return true;
        }

        private static bool TryCollectPlacedCaptureTargets(
            int toolMapperValue,
            out List<PlacedCaptureTarget> targets,
            out string failureReason)
        {
            targets = new List<PlacedCaptureTarget>();
            failureReason = string.Empty;
            float mouseTileX = 0f;
            float mouseTileY = 0f;
            MainControls.instance.getMouseMapTilePosition(ref mouseTileX, ref mouseTileY);
            List<PlacedTile> placedTiles = CollectPlacedTiles(
                toolMapperValue,
                (int)mouseTileX,
                (int)mouseTileY);
            if (placedTiles.Count == 0)
            {
                failureReason = "no matching placed castle tile is under or near the cursor";
                return false;
            }

            PlacedTile nearest = placedTiles
                .OrderBy(value => SquaredDistance(value.TileX, value.TileY, mouseTileX, mouseTileY))
                .First();
            if (toolMapperValue != StairToolMapperValue)
            {
                int mapperValue = toolMapperValue == HighCrenelToolMapperValue
                    ? HighCrenelToolMapperValue
                    : LowCrenelToolMapperValue;
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapper.Name,
                    false,
                    GetCameraQuarter(),
                    BlueprintDrawbridgePosition.NotApplicable);
                PreviewFragment fragment = nearest.ToFragment();
                targets.Add(new PlacedCaptureTarget(
                    mapperValue,
                    mapper.Name,
                    request,
                    fragment,
                    nearest.Position,
                    BuildSignature(new[] { fragment })));
                return true;
            }

            List<PlacedTile> staircase = CollectConnectedStaircase(placedTiles, nearest);
            if (staircase.Count == 0)
            {
                failureReason = "the nearest stairs could not be grouped";
                return false;
            }

            // Vanilla stores the six ascending stair pieces as separate tiles.
            // buildingHeight gives their stable low-to-high mapper order.
            List<PlacedTile> ordered = staircase
                .OrderBy(value => value.BuildingHeight)
                .ThenBy(value => value.Sprite.rect.height)
                .ThenBy(value => value.TileY)
                .ThenBy(value => value.TileX)
                .Take(6)
                .ToList();
            BlueprintStairDirection direction = ResolvePlacedStairDirection(ordered);
            if (direction == BlueprintStairDirection.Unknown)
            {
                failureReason = "the staircase direction could not be determined from its low/high pieces";
                return false;
            }
            bool flipHorizontally = ordered[ordered.Count - 1].Position.x <
                ordered[0].Position.x;

            for (int index = 0; index < ordered.Count; index++)
            {
                int mapperValue = 181 + index;
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapper.Name,
                    false,
                    GetCameraQuarter(),
                    BlueprintDrawbridgePosition.NotApplicable,
                    direction,
                    flipHorizontally);
                PreviewFragment fragment = ordered[index].ToFragment();
                targets.Add(new PlacedCaptureTarget(
                    mapperValue,
                    mapper.Name,
                    request,
                    fragment,
                    ordered[index].Position,
                    BuildSignature(new[] { fragment })));
            }
            return true;
        }

        private static List<PlacedTile> CollectPlacedTiles(
            int toolMapperValue,
            int mouseTileX,
            int mouseTileY)
        {
            var result = new List<PlacedTile>();
            int minimumX = Math.Max(1, mouseTileX - PlacedScanRadius);
            int maximumX = Math.Min(TilemapSize - 1, mouseTileX + PlacedScanRadius);
            int minimumY = Math.Max(1, mouseTileY - PlacedScanRadius);
            int maximumY = Math.Min(TilemapSize - 1, mouseTileY + PlacedScanRadius);
            TilePropertyFlag wantedFlag = toolMapperValue == StairToolMapperValue
                ? TilePropertyFlag.IsStairs
                : TilePropertyFlag.CrenelationComponent;

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    GameMapTile tile = GameMap.instance.getMapTile(x, y);
                    if (tile == null || tile.tilemapRef == null)
                        continue;

                    int tileId = GameTileManagerAPI.Instance.GetTileId(tile.gameMapX, tile.gameMapY);
                    if (!GameTileManagerAPI.Instance.HasTilePropertyFlag(tileId, wantedFlag))
                        continue;

                    // While the placement cursor is active, constructionOrigImage
                    // preserves the already-built visual underneath its overlay.
                    Sprite sprite = tile.constructionOrigImage != null
                        ? tile.constructionOrigImage
                        : tile.tileImage;
                    if (!IsPlacedCastleSprite(sprite))
                        continue;

                    Vector3 position = tile.tilemapRef.GetCellCenterWorld(new Vector3Int(x, y, 0));
                    position.y += tile.height;
                    result.Add(new PlacedTile(
                        x,
                        y,
                        tile.row,
                        tile.buildingHeight,
                        position,
                        sprite));
                }
            }
            return result;
        }

        private static List<PlacedTile> CollectConnectedStaircase(
            IReadOnlyList<PlacedTile> candidates,
            PlacedTile seed)
        {
            var result = new List<PlacedTile>();
            var pending = new Queue<PlacedTile>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                PlacedTile current = pending.Dequeue();
                string key = current.TileX + "," + current.TileY;
                if (!visited.Add(key))
                    continue;

                result.Add(current);
                foreach (PlacedTile candidate in candidates)
                {
                    if (Math.Abs(candidate.TileX - current.TileX) <= 1 &&
                        Math.Abs(candidate.TileY - current.TileY) <= 1)
                    {
                        pending.Enqueue(candidate);
                    }
                }
            }
            return result;
        }

        private static BlueprintStairDirection ResolvePlacedStairDirection(
            IReadOnlyList<PlacedTile> ordered)
        {
            if (ordered.Count < 2)
                return BlueprintStairDirection.Unknown;
            return ordered[ordered.Count - 1].Position.y >= ordered[0].Position.y
                ? BlueprintStairDirection.North
                : BlueprintStairDirection.South;
        }

        private static bool IsPlacedCastleSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return false;
            return BlueprintBuildingCaptureCatalog.IsBuildingPreviewFragment(
                sprite.name,
                sprite.texture.name,
                sprite.rect.width,
                sprite.rect.height);
        }

        private static float SquaredDistance(
            int tileX,
            int tileY,
            float mouseTileX,
            float mouseTileY)
        {
            float differenceX = tileX - mouseTileX;
            float differenceY = tileY - mouseTileY;
            return differenceX * differenceX + differenceY * differenceY;
        }

        private void LogWaitingForPlacedCapture(int toolMapperValue, string failureReason)
        {
            if (Time.unscaledTime < nextFailureDiagnosticTime)
                return;
            nextFailureDiagnosticTime = Time.unscaledTime + 2f;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Placed Blueprint capture is waiting: toolMapper={toolMapperValue}, " +
                $"reason={failureReason}");
        }

        private static bool TryGetCaptureContext(
            out int mapperValue,
            out AivMapperInfo mapper,
            out BlueprintCaptureRequest request)
        {
            mapperValue = 0;
            mapper = default;
            request = default;
            if (EngineInterface.FlattenedLandscape ||
                MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                GameMap.instance == null)
            {
                return false;
            }

            mapperValue = MainControls.instance.CurrentSubAction;
            mapper = AivMapperCatalog.Resolve(mapperValue);
            if (!mapper.IsKnown ||
                BlueprintBuildingIconCatalog.ResolveDefinition(mapper.Name) == null ||
                !BlueprintBuildingCaptureCatalog.RequiresCapturedImage(mapper.Name))
            {
                return false;
            }

            int quarter = GetCameraQuarter();
            if (mapper.Name == "MAPPER_DRAWBRIDGE")
            {
                // Rotating the map exposes the two canonical bridge faces;
                // opposite directions are normalized into a mirrored base.
                bool rear = quarter >= 2;
                request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapper.Name,
                    false,
                    quarter,
                    rear
                        ? (quarter == 3
                            ? BlueprintDrawbridgePosition.TopRight
                            : BlueprintDrawbridgePosition.TopLeft)
                        : (quarter == 1
                            ? BlueprintDrawbridgePosition.BottomRight
                            : BlueprintDrawbridgePosition.BottomLeft));
            }
            else
            {
                request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapper.Name,
                    UsesIslamicChurchSkin(),
                    quarter,
                    BlueprintDrawbridgePosition.NotApplicable);
            }
            return true;
        }

        private static bool TryCollectPreview(
            AivMapperInfo mapper,
            out List<PreviewFragment> fragments,
            out Vector2 groundCenter,
            out string failureReason)
        {
            fragments = new List<PreviewFragment>();
            groundCenter = Vector2.zero;
            failureReason = string.Empty;
            var changedTiles = new List<ChangedTile>();
            float mouseTileX = 0f;
            float mouseTileY = 0f;
            MainControls.instance.getMouseMapTilePosition(ref mouseTileX, ref mouseTileY);
            int minimumX = Math.Max(1, (int)mouseTileX - ScanRadius);
            int maximumX = Math.Min(TilemapSize - 1, (int)mouseTileX + ScanRadius);
            int minimumY = Math.Max(1, (int)mouseTileY - ScanRadius);
            int maximumY = Math.Min(TilemapSize - 1, (int)mouseTileY + ScanRadius);

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    GameMapTile tile = GameMap.instance.getMapTile(x, y);
                    if (tile == null ||
                        tile.tilemapRef == null ||
                        tile.tileImage == null ||
                        tile.constructionOrigImage == null ||
                        tile.tileImage == tile.constructionOrigImage)
                    {
                        continue;
                    }

                    Vector3 position = tile.tilemapRef.GetCellCenterWorld(new Vector3Int(x, y, 0));
                    position.y += tile.height;
                    var changed = new ChangedTile(x, y, tile.row, tile.height, position, tile.tileImage);
                    changedTiles.Add(changed);
                }
            }

            // Editor previews can leave changed sprites at older cursor
            // positions. Only the connected preview nearest the current mouse
            // belongs to this capture.
            List<ChangedTile> selectedTiles = SelectChangedPreviewComponent(
                changedTiles,
                mouseTileX,
                mouseTileY);
            foreach (ChangedTile changed in selectedTiles)
            {
                Sprite sprite = changed.Sprite;
                if (BlueprintBuildingCaptureCatalog.IsBuildingPreviewFragment(
                        sprite.name,
                        sprite.texture != null ? sprite.texture.name : null,
                        sprite.rect.width,
                        sprite.rect.height))
                {
                    fragments.Add(new PreviewFragment(
                        changed.TileX,
                        changed.TileY,
                        changed.Row,
                        changed.Position,
                        sprite));
                }
            }

            if (fragments.Count == 0)
            {
                failureReason = $"no tile-atlas visual fragments among " +
                    $"{selectedTiles.Count} connected changed tiles; " +
                    DescribeChangedTiles(selectedTiles);
                return false;
            }

            // Exact preview images contain Vanilla's yard/field visuals. Their
            // pivot therefore follows every changed placement cell, matching
            // the complete colored reservation rendered by the Blueprint.
            IReadOnlyList<ChangedTile> anchorTiles = selectedTiles;

            float minimumHeight = anchorTiles.Min(value => value.Height);
            float maximumHeight = anchorTiles.Max(value => value.Height);
            if (maximumHeight - minimumHeight > FlatHeightTolerance)
            {
                failureReason = $"placement is not level: minHeight=" +
                    $"{minimumHeight:F4}, maxHeight={maximumHeight:F4}, " +
                    $"tiles={anchorTiles.Count}";
                return false;
            }

            groundCenter = new Vector2(
                anchorTiles.Average(value => value.Position.x),
                anchorTiles.Average(value => value.Position.y));
            return true;
        }

        private static List<ChangedTile> SelectChangedPreviewComponent(
            IReadOnlyList<ChangedTile> changedTiles,
            float mouseTileX,
            float mouseTileY)
        {
            if (changedTiles.Count == 0)
                return new List<ChangedTile>();

            ChangedTile seed = changedTiles
                .OrderBy(value => SquaredDistance(
                    value.TileX,
                    value.TileY,
                    mouseTileX,
                    mouseTileY))
                .First();
            var result = new List<ChangedTile>();
            var pending = new Queue<ChangedTile>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                ChangedTile current = pending.Dequeue();
                string key = current.TileX + "," + current.TileY;
                if (!visited.Add(key))
                    continue;

                result.Add(current);
                foreach (ChangedTile candidate in changedTiles)
                {
                    if (Math.Abs(candidate.TileX - current.TileX) <= 1 &&
                        Math.Abs(candidate.TileY - current.TileY) <= 1)
                    {
                        pending.Enqueue(candidate);
                    }
                }
            }
            return result;
        }

        private static string DescribeChangedTiles(
            IReadOnlyList<ChangedTile> changedTiles)
        {
            if (changedTiles.Count == 0)
                return "no changed tile images were visible";

            return "samples=" + string.Join(
                ", ",
                changedTiles
                    .Take(6)
                    .Select(value =>
                    {
                        Sprite sprite = value.Sprite;
                        return $"'{sprite.name}'/" +
                            $"'{(sprite.texture != null ? sprite.texture.name : "<none>")}'/" +
                            $"{sprite.rect.width:F0}x{sprite.rect.height:F0}";
                    }));
        }

        private void SaveCapture(
            int mapperValue,
            string mapperName,
            BlueprintCaptureRequest request,
            IReadOnlyList<PreviewFragment> fragments,
            Vector2 groundCenter,
            string signature)
        {
            Directory.CreateDirectory(library.CapturedDirectory);
            string baseName = SanitizeFileName(
                request.MapperName + "_" + request.Skin + "_" + request.View);
            string pngFile = baseName + ".png";
            string pngPath = Path.Combine(library.CapturedDirectory, pngFile);
            RenderedComposite composite = RenderTransparentComposite(
                fragments,
                groundCenter,
                request.FlipHorizontally);
            File.WriteAllBytes(pngPath, composite.PngBytes);

            string manifestPath = Path.Combine(
                library.CapturedDirectory,
                BlueprintBuildingCaptureCatalog.ManifestFileName);
            var entries = new List<BlueprintCaptureManifestEntry>();
            if (File.Exists(manifestPath))
            {
                entries.AddRange(BlueprintBuildingCaptureCatalog.ParseManifest(
                    File.ReadAllLines(manifestPath),
                    out _));
            }
            entries.RemoveAll(value => value.Key == request.Key);
            entries.Add(new BlueprintCaptureManifestEntry
            {
                FormatVersion = BlueprintBuildingCaptureCatalog.CurrentFormatVersion,
                MapperValue = mapperValue,
                MapperName = request.MapperName,
                Skin = request.Skin,
                View = request.View,
                PngFile = pngFile,
                PivotX = composite.Pivot.x,
                PivotY = composite.Pivot.y,
                PixelsPerUnit = BlueprintBuildingCaptureCatalog.VanillaPixelsPerUnit,
                FragmentSignature = signature
            });
            File.WriteAllText(
                manifestPath,
                BlueprintBuildingCaptureCatalog.SerializeManifest(entries),
                new UTF8Encoding(false));

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured exact Vanilla Blueprint image: mapper={mapperName} " +
                $"({mapperValue}), key={request.Key}, fragments={fragments.Count}, " +
                $"pivot=({composite.Pivot.x:F6},{composite.Pivot.y:F6}), " +
                $"size={composite.Width}x{composite.Height}, signature={signature}, " +
                $"png={pngPath}, manifest={manifestPath}.");
        }

        private static RenderedComposite RenderTransparentComposite(
            IReadOnlyList<PreviewFragment> fragments,
            Vector2 groundCenter,
            bool normalizeHorizontalFlip)
        {
            const float ppu = BlueprintBuildingCaptureCatalog.VanillaPixelsPerUnit;
            float visualLeft = float.PositiveInfinity;
            float visualRight = float.NegativeInfinity;
            float visualBottom = float.PositiveInfinity;
            float visualTop = float.NegativeInfinity;
            foreach (PreviewFragment fragment in fragments)
            {
                Bounds bounds = fragment.Sprite.bounds;
                visualLeft = Math.Min(visualLeft, fragment.Position.x + bounds.min.x);
                visualRight = Math.Max(visualRight, fragment.Position.x + bounds.max.x);
                visualBottom = Math.Min(visualBottom, fragment.Position.y + bounds.min.y);
                visualTop = Math.Max(visualTop, fragment.Position.y + bounds.max.y);
            }

            float left = Mathf.Floor(visualLeft * ppu) / ppu;
            float right = Mathf.Ceil(visualRight * ppu) / ppu;
            float bottom = Mathf.Floor(visualBottom * ppu) / ppu;
            float top = Mathf.Ceil(visualTop * ppu) / ppu;
            int width = Math.Max(1, Mathf.RoundToInt((right - left) * ppu));
            int height = Math.Max(1, Mathf.RoundToInt((top - bottom) * ppu));
            if (width > 4096 || height > 4096)
                throw new InvalidOperationException($"Preview composite is implausibly large: {width}x{height}.");

            float pivotX = BlueprintBuildingCaptureCatalog.CalculateNormalizedPivot(
                groundCenter.x, left, width, ppu);
            float pivotY = BlueprintBuildingCaptureCatalog.CalculateNormalizedPivot(
                groundCenter.y, bottom, height, ppu);
            if (normalizeHorizontalFlip)
                pivotX = 1f - pivotX;

            var temporaryObjects = new List<GameObject>();
            var cameraObject = new GameObject("SpawnCastle_BlueprintCaptureCamera");
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
            camera.transform.position = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, -10f);
            camera.orthographicSize = height / (2f * ppu);
            camera.aspect = width / (float)height;

            foreach (PreviewFragment fragment in fragments
                .OrderBy(value => value.Row)
                .ThenBy(value => value.TileY)
                .ThenBy(value => value.TileX))
            {
                var spriteObject = new GameObject("SpawnCastle_BlueprintCaptureFragment");
                spriteObject.hideFlags = HideFlags.HideAndDontSave;
                spriteObject.layer = CaptureLayer;
                spriteObject.transform.position = fragment.Position;
                SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
                renderer.sprite = fragment.Sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = fragment.Row * 64 + fragment.TileX;
                temporaryObjects.Add(spriteObject);
            }

            RenderTexture renderTexture = null;
            Texture2D readableTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                renderTexture.filterMode = FilterMode.Point;
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                readableTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                readableTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readableTexture.Apply(false, false);
                if (normalizeHorizontalFlip)
                {
                    Color32[] source = readableTexture.GetPixels32();
                    var flipped = new Color32[source.Length];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                            flipped[y * width + x] = source[y * width + (width - 1 - x)];
                    }
                    readableTexture.SetPixels32(flipped);
                    readableTexture.Apply(false, false);
                }
                return new RenderedComposite(
                    ImageConversion.EncodeToPNG(readableTexture),
                    width,
                    height,
                    new Vector2(pivotX, pivotY));
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

        private static string BuildSignature(IReadOnlyList<PreviewFragment> fragments)
        {
            int minimumX = fragments.Min(value => value.TileX);
            int minimumY = fragments.Min(value => value.TileY);
            var value = new StringBuilder();
            foreach (PreviewFragment fragment in fragments
                .OrderBy(item => item.TileY)
                .ThenBy(item => item.TileX))
            {
                Sprite sprite = fragment.Sprite;
                value.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1}:{2}:{3}:{4:F0},{5:F0},{6:F2},{7:F2};",
                    fragment.TileX - minimumX,
                    fragment.TileY - minimumY,
                    sprite.name,
                    sprite.texture != null ? sprite.texture.name : string.Empty,
                    sprite.rect.width,
                    sprite.rect.height,
                    sprite.pivot.x,
                    sprite.pivot.y);
            }
            return CalculateFnv1A64(value.ToString()).ToString("X16", CultureInfo.InvariantCulture);
        }

        private static bool UsesIslamicChurchSkin()
        {
            try
            {
                return GameData.Instance != null &&
                    GameData.Instance.lastGameState != null &&
                    BlueprintBuildingIconCatalog.IsIslamicLordType(
                        GameData.Instance.lastGameState.lord_Type);
            }
            catch
            {
                return false;
            }
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

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }

        private void ResetCandidate()
        {
            candidateKey = string.Empty;
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

        private readonly struct ChangedTile
        {
            public ChangedTile(
                int tileX,
                int tileY,
                int row,
                float height,
                Vector3 position,
                Sprite sprite)
            {
                TileX = tileX;
                TileY = tileY;
                Row = row;
                Height = height;
                Position = position;
                Sprite = sprite;
            }

            public int TileX { get; }
            public int TileY { get; }
            public int Row { get; }
            public float Height { get; }
            public Vector3 Position { get; }
            public Sprite Sprite { get; }
        }

        private readonly struct PreviewFragment
        {
            public PreviewFragment(int tileX, int tileY, int row, Vector3 position, Sprite sprite)
            {
                TileX = tileX;
                TileY = tileY;
                Row = row;
                Position = position;
                Sprite = sprite;
            }

            public int TileX { get; }
            public int TileY { get; }
            public int Row { get; }
            public Vector3 Position { get; }
            public Sprite Sprite { get; }
        }

        private readonly struct PlacedTile
        {
            public PlacedTile(
                int tileX,
                int tileY,
                int row,
                float buildingHeight,
                Vector3 position,
                Sprite sprite)
            {
                TileX = tileX;
                TileY = tileY;
                Row = row;
                BuildingHeight = buildingHeight;
                Position = position;
                Sprite = sprite;
            }

            public int TileX { get; }
            public int TileY { get; }
            public int Row { get; }
            public float BuildingHeight { get; }
            public Vector3 Position { get; }
            public Sprite Sprite { get; }

            public PreviewFragment ToFragment()
            {
                return new PreviewFragment(TileX, TileY, Row, Position, Sprite);
            }
        }

        private readonly struct PlacedCaptureTarget
        {
            public PlacedCaptureTarget(
                int mapperValue,
                string mapperName,
                BlueprintCaptureRequest request,
                PreviewFragment fragment,
                Vector2 groundCenter,
                string signature)
            {
                MapperValue = mapperValue;
                MapperName = mapperName;
                Request = request;
                Fragment = fragment;
                GroundCenter = groundCenter;
                Signature = signature;
            }

            public int MapperValue { get; }
            public string MapperName { get; }
            public BlueprintCaptureRequest Request { get; }
            public PreviewFragment Fragment { get; }
            public Vector2 GroundCenter { get; }
            public string Signature { get; }
        }

        private readonly struct RenderedComposite
        {
            public RenderedComposite(byte[] pngBytes, int width, int height, Vector2 pivot)
            {
                PngBytes = pngBytes;
                Width = width;
                Height = height;
                Pivot = pivot;
            }

            public byte[] PngBytes { get; }
            public int Width { get; }
            public int Height { get; }
            public Vector2 Pivot { get; }
        }
    }
}
