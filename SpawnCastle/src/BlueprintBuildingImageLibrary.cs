using AIVParser.Core;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpawnCastle
{
    internal sealed class BlueprintBuildingImageLibrary
    {
        private readonly ManualLogSource log;
        private readonly string libraryDirectory;
        private readonly string capturedDirectory;
        private readonly Dictionary<string, LoadedEntry> entries =
            new Dictionary<string, LoadedEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LoadedFragmentCapture> fragmentCaptures =
            new Dictionary<string, LoadedFragmentCapture>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> fragmentSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedFragmentCaptures =
            new HashSet<string>(StringComparer.Ordinal);

        public BlueprintBuildingImageLibrary(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            string assemblyDirectory = Path.GetDirectoryName(
                typeof(BlueprintBuildingImageLibrary).Assembly.Location);
            libraryDirectory = Path.Combine(assemblyDirectory, "BlueprintImages");
            capturedDirectory = Path.Combine(libraryDirectory, "_Captured");
            Reload();
        }

        public string CapturedDirectory => capturedDirectory;

        public void Reload()
        {
            entries.Clear();
            fragmentCaptures.Clear();
            failedFiles.Clear();
            failedFragmentCaptures.Clear();
            LoadManifest(libraryDirectory);
            LoadFragmentManifests(libraryDirectory);
            if (BlueprintBuildingPreviewCapture.EnableBlueprintImageGeneration)
            {
                // Development captures override bundled entries for immediate
                // testing before they are promoted into the shipped library.
                LoadManifest(capturedDirectory);
                LoadFragmentManifests(capturedDirectory);
            }
            ReportStatus();
        }

        public bool TryResolve(
            int mapperValue,
            string mapperName,
            bool islamicChurchSkin,
            int cameraQuarter,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection,
            bool stairFlipHorizontally,
            out Sprite sprite,
            out bool flipHorizontally,
            out BlueprintFragmentVisual fragmentVisual)
        {
            BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog
                .ResolveRequest(
                    mapperName,
                    islamicChurchSkin,
                    cameraQuarter,
                    drawbridgePosition,
                    stairDirection,
                    stairFlipHorizontally);
            sprite = null;
            fragmentVisual = null;
            flipHorizontally = request.FlipHorizontally;
            if (!entries.TryGetValue(request.Key, out LoadedEntry loaded))
                return false;

            if (loaded.Entry.MapperValue != mapperValue &&
                !IsMapperAlias(mapperName, loaded.Entry.MapperName))
            {
                return false;
            }

            if (!BlueprintBuildingCaptureCatalog.UsesCompositeOnlyIcon(request.MapperName))
                TryResolveFragmentVisual(request.Key, out fragmentVisual);
            if (sprites.TryGetValue(loaded.FullPath, out sprite))
                return true;
            if (failedFiles.Contains(loaded.FullPath))
                return false;

            try
            {
                if (!File.Exists(loaded.FullPath))
                    throw new FileNotFoundException("Captured Blueprint PNG is missing.", loaded.FullPath);

                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.name = "SpawnCastle_Captured_" +
                    Path.GetFileNameWithoutExtension(loaded.FullPath);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(loaded.FullPath), false))
                {
                    Object.Destroy(texture);
                    throw new InvalidDataException("Unity could not decode the captured PNG.");
                }

                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(loaded.Entry.PivotX, loaded.Entry.PivotY),
                    loaded.Entry.PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = texture.name;
                Object.DontDestroyOnLoad(texture);
                Object.DontDestroyOnLoad(sprite);
                sprites.Add(loaded.FullPath, sprite);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded exact Blueprint capture: mapper={mapperName} " +
                    $"({mapperValue}), skin={loaded.Entry.Skin}, " +
                    $"view={loaded.Entry.View}, pivot=({loaded.Entry.PivotX:F6}," +
                    $"{loaded.Entry.PivotY:F6}), ppu={loaded.Entry.PixelsPerUnit:F1}, " +
                    $"file={loaded.FullPath}.");
                return true;
            }
            catch (Exception ex)
            {
                failedFiles.Add(loaded.FullPath);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Exact Blueprint capture is invalid; falling back to the " +
                    $"Vanilla UI/Help icon: key={request.Key}, file={loaded.FullPath}, " +
                    $"error={ex.Message}");
                sprite = null;
                return false;
            }
        }

        public bool Contains(BlueprintCaptureRequest request)
        {
            return entries.ContainsKey(request.Key);
        }

        public bool ContainsFragmentCapture(BlueprintCaptureRequest request)
        {
            return fragmentCaptures.ContainsKey(request.Key);
        }

        public bool ContainsFragmentCapture(
            BlueprintCaptureRequest request,
            string requiredSource)
        {
            if (!fragmentCaptures.TryGetValue(
                    request.Key,
                    out LoadedFragmentCapture capture))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(requiredSource) &&
                (!capture.Capture.Metadata.TryGetValue(
                    "captureSource",
                    out string source) ||
                 !string.Equals(source, requiredSource, StringComparison.Ordinal)))
            {
                return false;
            }

            if (requiredSource == "placed" &&
                RequiresCompletePlacedVisual(request.MapperName))
            {
                return capture.Capture.Metadata.TryGetValue(
                        "placedVisualVersion",
                        out string version) &&
                    string.Equals(version, "4", StringComparison.Ordinal);
            }
            return true;
        }

        private static bool RequiresCompletePlacedVisual(string mapperName)
        {
            return mapperName == "MAPPER_WALL" ||
                mapperName == "MAPPER_WOODWALL" ||
                mapperName == "MAPPER_CRENAL" ||
                mapperName == "MAPPER_CRENAL2";
        }

        private bool TryResolveFragmentVisual(
            string key,
            out BlueprintFragmentVisual visual)
        {
            visual = null;
            if (!fragmentCaptures.TryGetValue(key, out LoadedFragmentCapture capture) ||
                failedFragmentCaptures.Contains(key))
            {
                return false;
            }

            try
            {
                var loaded = new List<BlueprintLoadedFragment>(
                    capture.Fragments.Count);
                foreach (BlueprintFragmentImageEntry entry in capture.Fragments)
                {
                    string fullPath = Path.GetFullPath(
                        Path.Combine(capture.Directory, entry.PngFile));
                    if (!fragmentSprites.TryGetValue(fullPath, out Sprite sprite))
                    {
                        if (!File.Exists(fullPath))
                            throw new FileNotFoundException(
                                "Blueprint fragment PNG is missing.", fullPath);
                        byte[] pngBytes = File.ReadAllBytes(fullPath);
                        string actualHash = CalculateSha256(pngBytes);
                        if (!string.Equals(actualHash, entry.Sha256,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                $"Blueprint fragment hash mismatch: {entry.PngFile}.");
                        }

                        var texture = new Texture2D(
                            2, 2, TextureFormat.ARGB32, false);
                        texture.name = "SpawnCastle_Fragment_" +
                            Path.GetFileNameWithoutExtension(entry.PngFile);
                        texture.filterMode = FilterMode.Point;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        if (!ImageConversion.LoadImage(texture, pngBytes, false) ||
                            texture.width != entry.Width ||
                            texture.height != entry.Height)
                        {
                            Object.Destroy(texture);
                            throw new InvalidDataException(
                                $"Blueprint fragment dimensions are invalid: {entry.PngFile}.");
                        }

                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(entry.PivotX, entry.PivotY),
                            entry.PixelsPerUnit,
                            0,
                            SpriteMeshType.FullRect);
                        sprite.name = texture.name;
                        Object.DontDestroyOnLoad(texture);
                        Object.DontDestroyOnLoad(sprite);
                        fragmentSprites.Add(fullPath, sprite);
                    }

                    int sortingOffset = 0;
                    if (entry.Metadata.TryGetValue(
                            "sortingOffset",
                            out string sortingOffsetText))
                    {
                        int.TryParse(
                            sortingOffsetText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out sortingOffset);
                    }
                    loaded.Add(new BlueprintLoadedFragment(
                        sprite,
                        entry.Index,
                        entry.RowOffset,
                        sortingOffset,
                        new Vector3(
                            entry.PositionOffsetX,
                            entry.PositionOffsetY,
                            entry.PositionOffsetZ)));
                }

                visual = new BlueprintFragmentVisual(
                    loaded,
                    capture.Capture.MinimumRow,
                    capture.Capture.MaximumRow);
                return true;
            }
            catch (Exception ex)
            {
                failedFragmentCaptures.Add(key);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint fragments are invalid; the composite PNG remains " +
                    $"active: key={key}, error={ex.Message}");
                return false;
            }
        }

        private void LoadManifest(string directory)
        {
            string manifestPath = Path.Combine(
                directory,
                BlueprintBuildingCaptureCatalog.ManifestFileName);
            if (!File.Exists(manifestPath))
                return;

            try
            {
                IReadOnlyList<BlueprintCaptureManifestEntry> parsed =
                    BlueprintBuildingCaptureCatalog.ParseManifest(
                        File.ReadAllLines(manifestPath),
                        out IReadOnlyList<string> errors);
                foreach (string error in errors)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Blueprint image manifest entry ignored: " +
                        $"manifest={manifestPath}, error={error}");
                }

                foreach (BlueprintCaptureManifestEntry entry in parsed)
                {
                    string fullPath = Path.GetFullPath(Path.Combine(directory, entry.PngFile));
                    string directoryPrefix = Path.GetFullPath(directory)
                        .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!fullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    entries[entry.Key] = new LoadedEntry(entry, fullPath);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint image manifest could not be loaded: " +
                    $"manifest={manifestPath}, error={ex.Message}");
            }
        }

        private void LoadFragmentManifests(string directory)
        {
            string capturePath = Path.Combine(
                directory,
                BlueprintFragmentCaptureCatalog.CaptureManifestFileName);
            string tilePath = Path.Combine(
                directory,
                BlueprintFragmentCaptureCatalog.TileManifestFileName);
            string fragmentPath = Path.Combine(
                directory,
                BlueprintFragmentCaptureCatalog.FragmentManifestFileName);
            if (!File.Exists(capturePath) ||
                !File.Exists(tilePath) ||
                !File.Exists(fragmentPath))
            {
                return;
            }

            try
            {
                IReadOnlyList<BlueprintFragmentCaptureEntry> captures =
                    BlueprintFragmentCaptureCatalog.ParseCaptures(
                        File.ReadAllLines(capturePath),
                        out IReadOnlyList<string> captureErrors);
                IReadOnlyList<BlueprintFragmentTileEntry> tiles =
                    BlueprintFragmentCaptureCatalog.ParseTiles(
                        File.ReadAllLines(tilePath),
                        out IReadOnlyList<string> tileErrors);
                IReadOnlyList<BlueprintFragmentImageEntry> fragments =
                    BlueprintFragmentCaptureCatalog.ParseFragments(
                        File.ReadAllLines(fragmentPath),
                        out IReadOnlyList<string> fragmentErrors);
                foreach (string error in captureErrors.Concat(tileErrors)
                    .Concat(fragmentErrors))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Blueprint fragment manifest entry ignored: " +
                        $"directory={directory}, error={error}");
                }

                foreach (BlueprintFragmentCaptureEntry capture in captures)
                {
                    List<BlueprintFragmentImageEntry> captureFragments = fragments
                        .Where(value => value.CaptureKey == capture.Key)
                        .OrderBy(value => value.Index)
                        .ToList();
                    List<BlueprintFragmentTileEntry> captureTiles = tiles
                        .Where(value => value.CaptureKey == capture.Key)
                        .OrderBy(value => value.Index)
                        .ToList();
                    if (captureFragments.Count != capture.FragmentCount ||
                        captureTiles.Count != capture.TileCount ||
                        !HasContiguousIndices(captureFragments.Select(value => value.Index)) ||
                        !HasContiguousIndices(captureTiles.Select(value => value.Index)) ||
                        captureFragments.Any(value =>
                            !BlueprintFragmentCaptureCatalog.IsValidRowOffset(
                                capture,
                                value)))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Incomplete Blueprint fragment capture ignored: " +
                            $"key={capture.Key}, expectedFragments={capture.FragmentCount}, " +
                            $"actualFragments={captureFragments.Count}, " +
                            $"expectedTiles={capture.TileCount}, actualTiles={captureTiles.Count}.");
                        continue;
                    }

                    fragmentCaptures[capture.Key] = new LoadedFragmentCapture(
                        capture,
                        captureFragments,
                        directory);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint fragment manifests could not be loaded: " +
                    $"directory={directory}, error={ex.Message}");
            }
        }

        private static bool HasContiguousIndices(IEnumerable<int> indices)
        {
            int expected = 0;
            foreach (int index in indices.OrderBy(value => value))
            {
                if (index != expected++)
                    return false;
            }
            return true;
        }

        private static string CalculateSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", string.Empty);
            }
        }

        private void ReportStatus()
        {
            int required = 0;
            var missing = new List<string>();
            var missingFragments = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int mapperValue = 0; mapperValue <= 500; mapperValue++)
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                if (!mapper.IsKnown ||
                    BlueprintBuildingIconCatalog.ResolveDefinition(mapper.Name) == null ||
                    !BlueprintBuildingCaptureCatalog.RequiresCapturedImage(mapper.Name))
                {
                    continue;
                }

                foreach (BlueprintCaptureRequest request in GetRequiredRequests(mapper.Name))
                {
                    if (!seen.Add(request.Key))
                        continue;
                    required++;
                    if (!entries.ContainsKey(request.Key))
                        missing.Add(request.Key);
                    bool requiresPlaced = BlueprintBuildingCaptureCatalog
                        .RequiresPlacedCapture(request.MapperName);
                    if (!BlueprintBuildingCaptureCatalog.UsesCompositeOnlyIcon(request.MapperName) &&
                        !ContainsFragmentCapture(
                            request,
                            requiresPlaced ? "placed" : string.Empty))
                        missingFragments.Add(request.Key);
                }
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint capture status: available={required - missing.Count}, " +
                $"required={required}, missing={missing.Count}, " +
                $"fragmentCaptures={required - missingFragments.Count}, " +
                $"missingFragments={missingFragments.Count}, " +
                $"captureDirectory={capturedDirectory}.");
            if (missing.Count > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Missing Blueprint captures: " + string.Join(", ", missing));
            }
            if (missingFragments.Count > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Missing Blueprint depth captures: " +
                    string.Join(", ", missingFragments));
            }

            if (!BlueprintBuildingPreviewCapture.EnableBlueprintImageGeneration)
                return;

            try
            {
                Directory.CreateDirectory(capturedDirectory);
                var report = new System.Text.StringBuilder();
                report.Append("Blueprint capture status\r\n");
                report.Append($"available\t{required - missing.Count}\r\n");
                report.Append($"required\t{required}\r\n");
                report.Append($"missing\t{missing.Count}\r\n");
                report.Append($"fragmentMissing\t{missingFragments.Count}\r\n");
                report.Append("\r\nMissing mapper|skin|view variants:\r\n");
                foreach (string key in missing)
                    report.Append(key).Append("\r\n");
                report.Append("\r\nMissing depth-fragment variants:\r\n");
                foreach (string key in missingFragments)
                    report.Append(key).Append("\r\n");
                File.WriteAllText(
                    Path.Combine(capturedDirectory, "MissingBlueprintCaptures.txt"),
                    report.ToString(),
                    new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint capture status report could not be written: {ex.Message}");
            }
        }

        private static IEnumerable<BlueprintCaptureRequest> GetRequiredRequests(string mapperName)
        {
            if (mapperName == "MAPPER_CHURCH1" ||
                mapperName == "MAPPER_CHURCH2" ||
                mapperName == "MAPPER_CHURCH3")
            {
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 0, BlueprintDrawbridgePosition.NotApplicable);
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, true, 0, BlueprintDrawbridgePosition.NotApplicable);
                yield break;
            }

            if (mapperName == "MAPPER_DRAWBRIDGE")
            {
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 0, BlueprintDrawbridgePosition.BottomLeft);
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 0, BlueprintDrawbridgePosition.TopLeft);
                yield break;
            }

            if (BlueprintBuildingCaptureCatalog.IsStairMapper(mapperName))
            {
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName,
                    false,
                    0,
                    BlueprintDrawbridgePosition.NotApplicable,
                    BlueprintStairDirection.North);
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName,
                    false,
                    0,
                    BlueprintDrawbridgePosition.NotApplicable,
                    BlueprintStairDirection.South);
                yield break;
            }

            if (mapperName == "MAPPER_ENGINEERS_GUILD" ||
                mapperName == "MAPPER_TUNNELERS_GUILD" ||
                mapperName == "MAPPER_OIL_SMELTER")
            {
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 0, BlueprintDrawbridgePosition.NotApplicable);
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 2, BlueprintDrawbridgePosition.NotApplicable);
                yield break;
            }

            yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                mapperName, false, 0, BlueprintDrawbridgePosition.NotApplicable);
        }

        private static bool IsMapperAlias(string requestedMapper, string capturedMapper)
        {
            return (requestedMapper == "MAPPER_GATE_STONE1A" || requestedMapper == "MAPPER_GATE_STONE1B") &&
                    capturedMapper == "MAPPER_GATE_STONE1A" ||
                (requestedMapper == "MAPPER_GATE_STONE2A" || requestedMapper == "MAPPER_GATE_STONE2B") &&
                    capturedMapper == "MAPPER_GATE_STONE2A" ||
                BlueprintBuildingCaptureCatalog.IsStairMapper(requestedMapper) &&
                    capturedMapper == "MAPPER_STAIR";
        }

        private readonly struct LoadedEntry
        {
            public LoadedEntry(
                BlueprintCaptureManifestEntry entry,
                string fullPath)
            {
                Entry = entry;
                FullPath = fullPath;
            }

            public BlueprintCaptureManifestEntry Entry { get; }

            public string FullPath { get; }
        }

        private sealed class LoadedFragmentCapture
        {
            public LoadedFragmentCapture(
                BlueprintFragmentCaptureEntry capture,
                IReadOnlyList<BlueprintFragmentImageEntry> fragments,
                string directory)
            {
                Capture = capture;
                Fragments = fragments;
                Directory = directory;
            }

            public BlueprintFragmentCaptureEntry Capture { get; }
            public IReadOnlyList<BlueprintFragmentImageEntry> Fragments { get; }
            public string Directory { get; }
        }
    }

    internal sealed class BlueprintFragmentVisual
    {
        public BlueprintFragmentVisual(
            IReadOnlyList<BlueprintLoadedFragment> fragments,
            int captureMinimumRow,
            int captureMaximumRow)
        {
            Fragments = fragments;
            CaptureMinimumRow = captureMinimumRow;
            CaptureMaximumRow = captureMaximumRow;
        }

        public IReadOnlyList<BlueprintLoadedFragment> Fragments { get; }
        public int CaptureMinimumRow { get; }
        public int CaptureMaximumRow { get; }
    }

    internal readonly struct BlueprintLoadedFragment
    {
        public BlueprintLoadedFragment(
            Sprite sprite,
            int index,
            int rowOffset,
            int sortingOffset,
            Vector3 positionOffset)
        {
            Sprite = sprite;
            Index = index;
            RowOffset = rowOffset;
            SortingOffset = sortingOffset;
            PositionOffset = positionOffset;
        }

        public Sprite Sprite { get; }
        public int Index { get; }
        public int RowOffset { get; }
        public int SortingOffset { get; }
        public Vector3 PositionOffset { get; }
    }
}
