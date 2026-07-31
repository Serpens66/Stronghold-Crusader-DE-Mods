using AIVParser.Core;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly HashSet<string> failedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            failedFiles.Clear();
            LoadManifest(libraryDirectory);
            if (BlueprintBuildingPreviewCapture.EnableBlueprintImageGeneration)
            {
                // Development captures override bundled entries for immediate
                // testing before they are promoted into the shipped library.
                LoadManifest(capturedDirectory);
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
            out bool flipHorizontally)
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
            flipHorizontally = request.FlipHorizontally;
            if (!entries.TryGetValue(request.Key, out LoadedEntry loaded))
                return false;

            if (loaded.Entry.MapperValue != mapperValue &&
                !IsMapperAlias(mapperName, loaded.Entry.MapperName))
            {
                return false;
            }

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

        private void ReportStatus()
        {
            int required = 0;
            var missing = new List<string>();
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
                }
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint capture status: available={required - missing.Count}, " +
                $"required={required}, missing={missing.Count}, " +
                $"captureDirectory={capturedDirectory}.");
            if (missing.Count > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Missing Blueprint captures: " + string.Join(", ", missing));
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
                report.Append("\r\nMissing mapper|skin|view variants:\r\n");
                foreach (string key in missing)
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
                (requestedMapper == "MAPPER_CRENAL" || requestedMapper == "MAPPER_CRENAL2") &&
                    capturedMapper == "MAPPER_CRENAL" ||
                BlueprintBuildingCaptureCatalog.IsStairMapper(requestedMapper) &&
                    capturedMapper == "MAPPER_STAIR1";
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
    }
}
