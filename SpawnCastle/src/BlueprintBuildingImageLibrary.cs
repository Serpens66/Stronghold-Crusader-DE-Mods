using AIVParser.Core;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpawnCastle
{
    internal sealed class BlueprintBuildingImageLibrary
    {
        private const float PixelsPerUnit = 64f;

        private readonly ManualLogSource log;
        private readonly string libraryDirectory;
        private readonly string capturedDirectory;
        private readonly Dictionary<string, LoadedEntry> entries =
            new Dictionary<string, LoadedEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BlueprintDepthAtlasCaptureDefinition> depthCaptures =
            new Dictionary<string, BlueprintDepthAtlasCaptureDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, BlueprintFragmentCaptureEntry> developmentCaptures =
            new Dictionary<string, BlueprintFragmentCaptureEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, BlueprintDepthVisual> depthVisuals =
            new Dictionary<string, BlueprintDepthVisual>(StringComparer.Ordinal);
        private readonly Queue<string> pendingDepthLoads = new Queue<string>();
        private readonly HashSet<string> queuedDepthLoads =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> failedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedDepthCaptures =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<DepthReadResult>> depthReadTasks =
            new Dictionary<string, Task<DepthReadResult>>(StringComparer.Ordinal);
        private readonly SemaphoreSlim depthReadSemaphore = new SemaphoreSlim(2, 2);

        public BlueprintBuildingImageLibrary(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            string assemblyDirectory = Path.GetDirectoryName(
                typeof(BlueprintBuildingImageLibrary).Assembly.Location);
            libraryDirectory = Path.Combine(assemblyDirectory, "BlueprintImages");
            capturedDirectory = Path.Combine(libraryDirectory, "_Captured");
            Reload();
        }

        public event Action<string> DepthVisualLoaded;

        public string CapturedDirectory => capturedDirectory;
        public int PendingDepthLoadCount => pendingDepthLoads.Count;

        public void Reload()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            entries.Clear();
            depthCaptures.Clear();
            developmentCaptures.Clear();
            depthVisuals.Clear();
            pendingDepthLoads.Clear();
            queuedDepthLoads.Clear();
            failedFiles.Clear();
            failedDepthCaptures.Clear();
            depthReadTasks.Clear();
            LoadManifest(libraryDirectory);
            LoadDepthManifest(libraryDirectory);
            if (BlueprintBuildingPreviewCapture.EnableBlueprintImageGeneration)
            {
                // Development captures may override bundled composites and depth atlases.
                LoadManifest(capturedDirectory);
                LoadDepthManifest(capturedDirectory);
                LoadDevelopmentCaptureStatus(capturedDirectory);
            }
            ReportStatus();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint image manifests loaded: composites={entries.Count}, " +
                $"depthCaptures={depthCaptures.Count}, elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F1}.");
        }

        public bool TryResolveComposite(
            int mapperValue,
            string mapperName,
            bool islamicChurchSkin,
            int cameraQuarter,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection,
            bool stairFlipHorizontally,
            out Sprite sprite,
            out bool flipHorizontally,
            out RectInt alphaBounds)
        {
            BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                mapperName,
                islamicChurchSkin,
                cameraQuarter,
                drawbridgePosition,
                stairDirection,
                stairFlipHorizontally);
            sprite = null;
            flipHorizontally = request.FlipHorizontally;
            alphaBounds = default;
            if (!entries.TryGetValue(request.Key, out LoadedEntry loaded) ||
                (loaded.Entry.MapperValue != mapperValue &&
                 !IsMapperAlias(mapperName, loaded.Entry.MapperName)))
            {
                return false;
            }
            if (sprites.TryGetValue(loaded.FullPath, out sprite))
            {
                alphaBounds = GetAlphaBounds(loaded.Entry);
                return true;
            }
            if (failedFiles.Contains(loaded.FullPath))
                return false;

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (!File.Exists(loaded.FullPath))
                    throw new FileNotFoundException("Captured Blueprint PNG is missing.", loaded.FullPath);
                byte[] pngBytes = File.ReadAllBytes(loaded.FullPath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false)
                {
                    name = "SpawnCastle_Captured_" + Path.GetFileNameWithoutExtension(loaded.FullPath),
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                // Flat meshes use manifest bounds, so the decoded CPU pixels can be released immediately.
                if (!ImageConversion.LoadImage(texture, pngBytes, true))
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
                alphaBounds = GetAlphaBounds(loaded.Entry);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Loaded exact Blueprint composite: key={request.Key}, bytes={pngBytes.Length}, " +
                    $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F1}, file={loaded.FullPath}.");
                return true;
            }
            catch (Exception ex)
            {
                failedFiles.Add(loaded.FullPath);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Exact Blueprint composite is invalid: key={request.Key}, " +
                    $"file={loaded.FullPath}, error={ex.Message}");
                return false;
            }
        }

        private static RectInt GetAlphaBounds(BlueprintCaptureManifestEntry entry)
        {
            return new RectInt(
                entry.AlphaX,
                entry.AlphaY,
                entry.AlphaWidth,
                entry.AlphaHeight);
        }

        public BlueprintDepthResolveState ResolveDepth(
            int mapperValue,
            string mapperName,
            bool islamicChurchSkin,
            int cameraQuarter,
            BlueprintDrawbridgePosition drawbridgePosition,
            BlueprintStairDirection stairDirection,
            bool stairFlipHorizontally,
            out string key,
            out bool flipHorizontally,
            out BlueprintDepthVisual visual)
        {
            BlueprintCaptureRequest request = BlueprintBuildingCaptureCatalog.ResolveRequest(
                mapperName,
                islamicChurchSkin,
                cameraQuarter,
                drawbridgePosition,
                stairDirection,
                stairFlipHorizontally);
            key = request.Key;
            flipHorizontally = request.FlipHorizontally;
            visual = null;
            if (!depthCaptures.TryGetValue(key, out BlueprintDepthAtlasCaptureDefinition capture) ||
                (capture.MapperValue != mapperValue && !IsMapperAlias(mapperName, capture.MapperName)))
            {
                return BlueprintDepthResolveState.Missing;
            }
            if (depthVisuals.TryGetValue(key, out visual))
                return BlueprintDepthResolveState.Ready;
            if (failedDepthCaptures.Contains(key))
                return BlueprintDepthResolveState.Failed;
            QueueDepthLoad(key);
            return BlueprintDepthResolveState.Pending;
        }

        public void QueueDepthLoad(string key)
        {
            if (string.IsNullOrEmpty(key) || depthVisuals.ContainsKey(key) ||
                failedDepthCaptures.Contains(key) || !depthCaptures.ContainsKey(key) ||
                !queuedDepthLoads.Add(key))
            {
                return;
            }
            pendingDepthLoads.Enqueue(key);
            BlueprintDepthAtlasCaptureDefinition capture = depthCaptures[key];
            depthReadTasks[key] = Task.Run(async () =>
            {
                await depthReadSemaphore.WaitAsync().ConfigureAwait(false);
                Stopwatch timer = Stopwatch.StartNew();
                try
                {
                    var pageBytes = new Dictionary<int, byte[]>();
                    foreach (BlueprintDepthAtlasPageDefinition page in capture.Pages)
                    {
                        string fullPath = ResolveSafePath(capture.Directory, page.PngFile);
                        pageBytes.Add(page.PageIndex, File.ReadAllBytes(fullPath));
                    }
                    return new DepthReadResult(pageBytes, timer.Elapsed.TotalMilliseconds);
                }
                finally
                {
                    depthReadSemaphore.Release();
                }
            });
        }

        public bool TryGetLoadedDepthVisual(string key, out BlueprintDepthVisual visual)
        {
            return depthVisuals.TryGetValue(key, out visual);
        }

        public bool ProcessOnePendingDepthLoad()
        {
            if (pendingDepthLoads.Count == 0)
                return false;
            string key = null;
            int queued = pendingDepthLoads.Count;
            for (int index = 0; index < queued; index++)
            {
                string candidate = pendingDepthLoads.Dequeue();
                if (key == null && depthReadTasks.TryGetValue(candidate, out Task<DepthReadResult> task) &&
                    task.IsCompleted)
                {
                    key = candidate;
                }
                else
                {
                    pendingDepthLoads.Enqueue(candidate);
                }
            }
            if (key == null)
                return false;
            queuedDepthLoads.Remove(key);
            if (depthVisuals.ContainsKey(key) || failedDepthCaptures.Contains(key) ||
                !depthCaptures.TryGetValue(key, out BlueprintDepthAtlasCaptureDefinition capture))
            {
                return true;
            }

            Stopwatch total = Stopwatch.StartNew();
            try
            {
                DepthReadResult readResult = depthReadTasks[key].GetAwaiter().GetResult();
                depthReadTasks.Remove(key);
                var pages = new Dictionary<int, LoadedDepthPage>();
                double pngDecodeMilliseconds = 0d;
                foreach (BlueprintDepthAtlasPageDefinition page in capture.Pages)
                {
                    Stopwatch pageTimer = Stopwatch.StartNew();
                    byte[] bytes = readResult.PageBytes[page.PageIndex];
                    var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false)
                    {
                        name = "SpawnCastle_DepthAtlas_" + SanitizeName(key) + "_" + page.PageIndex,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    // Atlas pixels are never read again after the mesh UVs are created.
                    if (!ImageConversion.LoadImage(texture, bytes, true) ||
                        texture.width != page.Width || texture.height != page.Height)
                    {
                        Object.Destroy(texture);
                        throw new InvalidDataException($"Depth atlas dimensions are invalid: {page.PngFile}.");
                    }
                    double pageDecodeMilliseconds = pageTimer.Elapsed.TotalMilliseconds;
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                        Shader.Find("Sprites/Default");
                    if (shader == null)
                        throw new InvalidOperationException("No compatible unlit sprite shader is available.");
                    var material = new Material(shader)
                    {
                        name = texture.name + "_Material",
                        mainTexture = texture
                    };
                    Object.DontDestroyOnLoad(texture);
                    Object.DontDestroyOnLoad(material);
                    pages.Add(page.PageIndex, new LoadedDepthPage(page, texture, material));
                    pngDecodeMilliseconds += pageDecodeMilliseconds;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Decoded Blueprint depth atlas page: key={key}, page={page.PageIndex}, " +
                        $"bytes={bytes.Length}, size={page.Width}x{page.Height}, " +
                        $"pngDecodeMs={pageDecodeMilliseconds:F1}, " +
                        $"pagePrepareMs={pageTimer.Elapsed.TotalMilliseconds:F1}.");
                }

                Stopwatch meshTimer = Stopwatch.StartNew();
                List<BlueprintDepthLayer> layers = capture.Fragments
                    .GroupBy(value => new DepthLayerKey(
                        value.PageIndex,
                        value.RowOffset,
                        value.SortingOffset))
                    .OrderBy(value => value.Key.RowOffset)
                    .ThenBy(value => value.Key.SortingOffset)
                    .ThenBy(value => value.Key.PageIndex)
                    .Select(group => BuildDepthLayer(
                        group.Key,
                        group.OrderBy(value => value.Index),
                        pages[group.Key.PageIndex]))
                    .ToList();
                double meshBuildMilliseconds = meshTimer.Elapsed.TotalMilliseconds;
                var visual = new BlueprintDepthVisual(
                    key,
                    layers,
                    capture.MinimumRow,
                    capture.MaximumRow);
                depthVisuals.Add(key, visual);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Prepared Blueprint depth visual: key={key}, pages={pages.Count}, " +
                    $"fragments={capture.Fragments.Count}, layers={layers.Count}, " +
                    $"fileReadMs={readResult.ElapsedMilliseconds:F1}, " +
                    $"pngDecodeMs={pngDecodeMilliseconds:F1}, " +
                    $"meshBuildMs={meshBuildMilliseconds:F1}, " +
                    $"mainThreadMs={total.Elapsed.TotalMilliseconds:F1}.");
                DepthVisualLoaded?.Invoke(key);
            }
            catch (Exception ex)
            {
                depthReadTasks.Remove(key);
                failedDepthCaptures.Add(key);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint depth atlas failed; only colored ground markers remain: " +
                    $"key={key}, elapsedMs={total.Elapsed.TotalMilliseconds:F1}, error={ex.Message}");
                DepthVisualLoaded?.Invoke(key);
            }
            return true;
        }

        public bool Contains(BlueprintCaptureRequest request)
        {
            return entries.ContainsKey(request.Key);
        }

        public bool ContainsFragmentCapture(BlueprintCaptureRequest request)
        {
            return depthCaptures.ContainsKey(request.Key) || developmentCaptures.ContainsKey(request.Key);
        }

        public bool ContainsFragmentCapture(BlueprintCaptureRequest request, string requiredSource)
        {
            if (depthCaptures.TryGetValue(request.Key, out BlueprintDepthAtlasCaptureDefinition depth))
            {
                return SourceMatches(
                    request.MapperName,
                    requiredSource,
                    depth.CaptureSource,
                    depth.PlacedVisualVersion);
            }
            if (developmentCaptures.TryGetValue(request.Key, out BlueprintFragmentCaptureEntry development))
            {
                development.Metadata.TryGetValue("captureSource", out string source);
                development.Metadata.TryGetValue("placedVisualVersion", out string version);
                return SourceMatches(request.MapperName, requiredSource, source, version);
            }
            return false;
        }

        private static bool SourceMatches(
            string mapperName,
            string requiredSource,
            string source,
            string placedVisualVersion)
        {
            if (!string.IsNullOrEmpty(requiredSource) &&
                !string.Equals(source, requiredSource, StringComparison.Ordinal))
                return false;
            if (requiredSource == "placed" && RequiresCompletePlacedVisual(mapperName))
                return string.Equals(placedVisualVersion, "4", StringComparison.Ordinal);
            return true;
        }

        private static bool RequiresCompletePlacedVisual(string mapperName)
        {
            return mapperName == "MAPPER_WALL" || mapperName == "MAPPER_WOODWALL" ||
                mapperName == "MAPPER_CRENAL" || mapperName == "MAPPER_CRENAL2";
        }

        private BlueprintDepthLayer BuildDepthLayer(
            DepthLayerKey key,
            IEnumerable<BlueprintDepthAtlasFragmentDefinition> fragments,
            LoadedDepthPage page)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var colors = new List<Color32>();
            foreach (BlueprintDepthAtlasFragmentDefinition fragment in fragments)
            {
                int vertex = vertices.Count;
                float left = fragment.PositionOffsetX;
                float bottom = fragment.PositionOffsetY;
                float right = left + fragment.Width / PixelsPerUnit;
                float top = bottom + fragment.Height / PixelsPerUnit;
                float z = fragment.PositionOffsetZ;
                vertices.Add(new Vector3(left, bottom, z));
                vertices.Add(new Vector3(right, bottom, z));
                vertices.Add(new Vector3(left, top, z));
                vertices.Add(new Vector3(right, top, z));
                float u0 = fragment.X / (float)page.Definition.Width;
                float v0 = fragment.Y / (float)page.Definition.Height;
                float u1 = (fragment.X + fragment.Width) / (float)page.Definition.Width;
                float v1 = (fragment.Y + fragment.Height) / (float)page.Definition.Height;
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u0, v1));
                uvs.Add(new Vector2(u1, v1));
                triangles.Add(vertex);
                triangles.Add(vertex + 2);
                triangles.Add(vertex + 1);
                triangles.Add(vertex + 2);
                triangles.Add(vertex + 3);
                triangles.Add(vertex + 1);
                Color32 white = new Color32(255, 255, 255, 255);
                colors.Add(white);
                colors.Add(white);
                colors.Add(white);
                colors.Add(white);
            }
            var mesh = new Mesh
            {
                name = $"SpawnCastle_DepthLayer_{SanitizeName(page.Definition.CaptureKey)}_" +
                    $"{key.RowOffset}_{key.SortingOffset}"
            };
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.colors32 = colors.ToArray();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            Object.DontDestroyOnLoad(mesh);
            return new BlueprintDepthLayer(
                mesh,
                page.Material,
                key.PageIndex,
                key.RowOffset,
                key.SortingOffset);
        }

        private void LoadManifest(string directory)
        {
            string manifestPath = Path.Combine(directory, BlueprintBuildingCaptureCatalog.ManifestFileName);
            if (!File.Exists(manifestPath))
                return;
            try
            {
                IReadOnlyList<BlueprintCaptureManifestEntry> parsed =
                    BlueprintBuildingCaptureCatalog.ParseManifest(
                        File.ReadAllLines(manifestPath),
                        out IReadOnlyList<string> errors);
                foreach (string error in errors)
                    Shared.DebugLogHelper.LogWarning(log, $"Blueprint image entry ignored: {error}");
                foreach (BlueprintCaptureManifestEntry entry in parsed)
                {
                    string fullPath = ResolveSafePath(directory, entry.PngFile);
                    entries[entry.Key] = new LoadedEntry(entry, fullPath);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint image manifest could not be loaded: manifest={manifestPath}, error={ex.Message}");
            }
        }

        private void LoadDepthManifest(string directory)
        {
            string manifestPath = Path.Combine(directory, BlueprintDepthAtlasCatalog.ManifestFileName);
            if (!File.Exists(manifestPath))
                return;
            try
            {
                IReadOnlyList<BlueprintDepthAtlasCaptureDefinition> parsed =
                    BlueprintDepthAtlasCatalog.Parse(
                        directory,
                        File.ReadAllLines(manifestPath),
                        out IReadOnlyList<string> errors);
                foreach (string error in errors)
                    Shared.DebugLogHelper.LogWarning(log, $"Blueprint depth atlas entry ignored: {error}");
                foreach (BlueprintDepthAtlasCaptureDefinition capture in parsed)
                    depthCaptures[capture.Key] = capture;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Blueprint depth atlas manifest could not be loaded: " +
                    $"manifest={manifestPath}, error={ex.Message}");
            }
        }

        private void LoadDevelopmentCaptureStatus(string directory)
        {
            string path = Path.Combine(
                directory,
                BlueprintFragmentCaptureCatalog.CaptureManifestFileName);
            if (!File.Exists(path))
                return;
            IReadOnlyList<BlueprintFragmentCaptureEntry> captures =
                BlueprintFragmentCaptureCatalog.ParseCaptures(
                    File.ReadAllLines(path),
                    out IReadOnlyList<string> errors);
            foreach (string error in errors)
                Shared.DebugLogHelper.LogWarning(log, $"Development depth capture ignored: {error}");
            foreach (BlueprintFragmentCaptureEntry capture in captures)
                developmentCaptures[capture.Key] = capture;
        }

        private void ReportStatus()
        {
            int required = 0;
            var missing = new List<string>();
            var missingDepth = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int mapperValue = 0; mapperValue <= 500; mapperValue++)
            {
                AivMapperInfo mapper = AivMapperCatalog.Resolve(mapperValue);
                if (!mapper.IsKnown ||
                    BlueprintBuildingIconCatalog.ResolveDefinition(mapper.Name) == null ||
                    !BlueprintBuildingCaptureCatalog.RequiresCapturedImage(mapper.Name))
                    continue;
                foreach (BlueprintCaptureRequest request in GetRequiredRequests(mapper.Name))
                {
                    if (!seen.Add(request.Key))
                        continue;
                    required++;
                    if (!entries.ContainsKey(request.Key))
                        missing.Add(request.Key);
                    bool requiresPlaced = BlueprintBuildingCaptureCatalog.RequiresPlacedCapture(request.MapperName);
                    if (!ContainsFragmentCapture(
                            request,
                            requiresPlaced ? "placed" : string.Empty))
                        missingDepth.Add(request.Key);
                }
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint capture status: available={required - missing.Count}, required={required}, " +
                $"missing={missing.Count}, depthAtlases={required - missingDepth.Count}, " +
                $"missingDepth={missingDepth.Count}, captureDirectory={capturedDirectory}.");
            if (missing.Count > 0)
                Shared.DebugLogHelper.LogInfo(log, "Missing Blueprint captures: " + string.Join(", ", missing));
            if (missingDepth.Count > 0)
                Shared.DebugLogHelper.LogInfo(log, "Missing Blueprint depth atlases: " + string.Join(", ", missingDepth));
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
                report.Append($"fragmentMissing\t{missingDepth.Count}\r\n");
                report.Append("\r\nMissing mapper|skin|view variants:\r\n");
                foreach (string key in missing)
                    report.Append(key).Append("\r\n");
                report.Append("\r\nMissing depth-atlas variants:\r\n");
                foreach (string key in missingDepth)
                    report.Append(key).Append("\r\n");
                File.WriteAllText(
                    Path.Combine(capturedDirectory, "MissingBlueprintCaptures.txt"),
                    report.ToString(),
                    new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(log, $"Blueprint capture status report failed: {ex.Message}");
            }
        }

        private static IEnumerable<BlueprintCaptureRequest> GetRequiredRequests(string mapperName)
        {
            if (mapperName == "MAPPER_CHURCH1" || mapperName == "MAPPER_CHURCH2" ||
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
                    mapperName, false, 0, BlueprintDrawbridgePosition.NotApplicable,
                    BlueprintStairDirection.North);
                yield return BlueprintBuildingCaptureCatalog.ResolveRequest(
                    mapperName, false, 0, BlueprintDrawbridgePosition.NotApplicable,
                    BlueprintStairDirection.South);
                yield break;
            }
            if (mapperName == "MAPPER_ENGINEERS_GUILD" ||
                mapperName == "MAPPER_TUNNELERS_GUILD" || mapperName == "MAPPER_OIL_SMELTER")
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

        private static string ResolveSafePath(string directory, string relativePath)
        {
            string prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(directory, relativePath));
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe Blueprint image path: {relativePath}.");
            return fullPath;
        }

        private static bool IsMapperAlias(string requestedMapper, string capturedMapper)
        {
            return ((requestedMapper == "MAPPER_GATE_STONE1A" || requestedMapper == "MAPPER_GATE_STONE1B") &&
                    capturedMapper == "MAPPER_GATE_STONE1A") ||
                ((requestedMapper == "MAPPER_GATE_STONE2A" || requestedMapper == "MAPPER_GATE_STONE2B") &&
                    capturedMapper == "MAPPER_GATE_STONE2A") ||
                (BlueprintBuildingCaptureCatalog.IsStairMapper(requestedMapper) && capturedMapper == "MAPPER_STAIR");
        }

        private static string SanitizeName(string value)
        {
            return new string(value.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_').ToArray());
        }

        private readonly struct LoadedEntry
        {
            public LoadedEntry(BlueprintCaptureManifestEntry entry, string fullPath)
            {
                Entry = entry;
                FullPath = fullPath;
            }
            public BlueprintCaptureManifestEntry Entry { get; }
            public string FullPath { get; }
        }

        private sealed class LoadedDepthPage
        {
            public LoadedDepthPage(
                BlueprintDepthAtlasPageDefinition definition,
                Texture2D texture,
                Material material)
            {
                Definition = definition;
                Texture = texture;
                Material = material;
            }
            public BlueprintDepthAtlasPageDefinition Definition { get; }
            public Texture2D Texture { get; }
            public Material Material { get; }
        }

        private sealed class DepthReadResult
        {
            public DepthReadResult(
                IReadOnlyDictionary<int, byte[]> pageBytes,
                double elapsedMilliseconds)
            {
                PageBytes = pageBytes;
                ElapsedMilliseconds = elapsedMilliseconds;
            }
            public IReadOnlyDictionary<int, byte[]> PageBytes { get; }
            public double ElapsedMilliseconds { get; }
        }

        private readonly struct DepthLayerKey : IEquatable<DepthLayerKey>
        {
            public DepthLayerKey(int pageIndex, int rowOffset, int sortingOffset)
            {
                PageIndex = pageIndex;
                RowOffset = rowOffset;
                SortingOffset = sortingOffset;
            }
            public int PageIndex { get; }
            public int RowOffset { get; }
            public int SortingOffset { get; }
            public bool Equals(DepthLayerKey other) =>
                PageIndex == other.PageIndex && RowOffset == other.RowOffset &&
                SortingOffset == other.SortingOffset;
            public override bool Equals(object obj) => obj is DepthLayerKey other && Equals(other);
            public override int GetHashCode() =>
                ((PageIndex * 397) ^ RowOffset) * 397 ^ SortingOffset;
        }
    }

    internal enum BlueprintDepthResolveState
    {
        Missing,
        Pending,
        Ready,
        Failed
    }

    internal sealed class BlueprintDepthVisual
    {
        public BlueprintDepthVisual(
            string key,
            IReadOnlyList<BlueprintDepthLayer> layers,
            int captureMinimumRow,
            int captureMaximumRow)
        {
            Key = key;
            Layers = layers;
            CaptureMinimumRow = captureMinimumRow;
            CaptureMaximumRow = captureMaximumRow;
        }
        public string Key { get; }
        public IReadOnlyList<BlueprintDepthLayer> Layers { get; }
        public int CaptureMinimumRow { get; }
        public int CaptureMaximumRow { get; }
    }

    internal readonly struct BlueprintDepthLayer
    {
        public BlueprintDepthLayer(
            Mesh mesh,
            Material material,
            int pageIndex,
            int rowOffset,
            int sortingOffset)
        {
            Mesh = mesh;
            Material = material;
            PageIndex = pageIndex;
            RowOffset = rowOffset;
            SortingOffset = sortingOffset;
        }
        public Mesh Mesh { get; }
        public Material Material { get; }
        public int PageIndex { get; }
        public int RowOffset { get; }
        public int SortingOffset { get; }
    }
}
