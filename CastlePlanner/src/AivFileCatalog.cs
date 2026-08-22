using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SHCDESE.API;

namespace CastlePlanner
{
    internal sealed class AivFileCatalog
    {
        private static readonly Regex VanillaFileNamePattern = new Regex(
            @"^(?:Community_(?:Historical_)?)?(?<lord>.+?)(?<number>[1-8])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> pathByOption =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CachedFingerprint> fingerprintByPath =
            new Dictionary<string, CachedFingerprint>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Discover(Action<string> warning = null)
        {
            pathByOption.Clear();

            string pluginDirectory =
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                string.Empty;

            AddRoot("Mod", Path.Combine(pluginDirectory, "AIV"));
            AddVanillaRoot(
                Path.Combine(pluginDirectory, "VanillaAIV"),
                warning);
            AddLocalLordRoots();
            AddWorkshopRoots(warning);
            AddRoot(
                "Editor",
                Path.Combine(
                    Paths.GameRootPath + " - Castle & CPU Lord Editor",
                    "CrusaderCastleEditorUnity_Data",
                    "StreamingAssets",
                    "Villages"));

            return pathByOption.Keys
                .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void AddVanillaRoot(string root, Action<string> warning)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.aivjson", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var effectivePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string bundledPath in files)
            {
                string fileName = Path.GetFileName(bundledPath);
                string effectivePath = bundledPath;
                bool overrideUnavailable = false;
                if (TryGetVanillaAsset(fileName, out string assetName) &&
                    TryResolveOverride(
                        assetName,
                        effectivePaths,
                        warning,
                        out string overridePath,
                        out overrideUnavailable))
                {
                    effectivePath = overridePath;
                }
                else if (overrideUnavailable)
                {
                    // Never expose an older bundled castle when an effective override
                    // was found but could not be made readable by the existing pipeline.
                    continue;
                }

                pathByOption[$"[Vanilla] {fileName}"] = effectivePath;
            }
        }

        private static bool TryResolveOverride(
            string assetName,
            IDictionary<string, string> effectivePaths,
            Action<string> warning,
            out string effectivePath,
            out bool overrideUnavailable)
        {
            effectivePath = null;
            overrideUnavailable = false;
            if (effectivePaths.TryGetValue(assetName, out string cachedPath))
            {
                effectivePath = cachedPath;
                overrideUnavailable = cachedPath == null;
                return !string.IsNullOrEmpty(effectivePath);
            }

            try
            {
                GameAssetManagerAPI manager = GameAssetManagerAPI.Instance;
                if (manager == null ||
                    !manager.GetModifiedFileTextContent(assetName, out string content) ||
                    content == null)
                {
                    effectivePaths[assetName] = string.Empty;
                    return false;
                }

                string cacheDirectory = Path.Combine(
                    Path.GetTempPath(),
                    CastlePlannerPlugin.PluginGuid,
                    "EffectiveVanillaAIV");
                Directory.CreateDirectory(cacheDirectory);
                string cachePath = Path.Combine(
                    cacheDirectory,
                    assetName.Substring("AIV/".Length));
                if (!File.Exists(cachePath) ||
                    !string.Equals(File.ReadAllText(cachePath), content, StringComparison.Ordinal))
                {
                    File.WriteAllText(cachePath, content, new UTF8Encoding(false));
                }

                effectivePaths[assetName] = cachePath;
                effectivePath = cachePath;
                return true;
            }
            catch (Exception ex)
            {
                effectivePaths[assetName] = null;
                overrideUnavailable = true;
                warning?.Invoke(
                    $"Could not materialize Script Extender AIV override '{assetName}': {ex.Message}");
                return false;
            }
        }

        private static bool TryGetVanillaAsset(string fileName, out string assetName)
        {
            assetName = string.Empty;
            string stem = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            bool historical = stem.StartsWith(
                "Community_Historical_",
                StringComparison.OrdinalIgnoreCase);
            Match match = VanillaFileNamePattern.Match(stem);
            if (!match.Success)
                return false;

            string lord = match.Groups["lord"].Value;
            int index = historical || !match.Groups["number"].Success
                ? 0
                : int.Parse(match.Groups["number"].Value) - 1;
            switch (lord.ToLowerInvariant())
            {
                case "philip": lord = "PHILLIP"; break;
                case "kahinah": lord = "KAHIN"; break;
                case "croc": lord = "CROCODILE"; break;
                case "surgeon": lord = "DLC4A"; break;
                case "baibars": lord = "DLC4B"; break;
                default: lord = lord.ToUpperInvariant(); break;
            }

            assetName = $"AIV/SK_{lord}_{index}.aivjson";
            return true;
        }

        private void AddWorkshopRoots(Action<string> warning)
        {
            foreach (string itemRoot in Shared.WorkshopContentPaths.GetSubscribedItemRoots(warning))
            {
                string itemId = Path.GetFileName(
                    itemRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                AddRoot("Steam Workshop " + itemId, itemRoot);
            }
        }

        public bool TryResolve(string option, out string fullPath)
        {
            return pathByOption.TryGetValue(option ?? string.Empty, out fullPath) &&
                   File.Exists(fullPath);
        }

        public IReadOnlyDictionary<string, string> CaptureHashes(
            Action<string> warning = null,
            bool forceRefresh = false)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var currentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> entry in pathByOption)
            {
                try
                {
                    string path = entry.Value;
                    currentPaths.Add(path);
                    var info = new FileInfo(path);
                    if (!forceRefresh &&
                        fingerprintByPath.TryGetValue(path, out CachedFingerprint cached) &&
                        cached.Length == info.Length &&
                        cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks)
                    {
                        hashes[entry.Key] = cached.Hash;
                        continue;
                    }

                    string hash = ComputeHash(path);
                    fingerprintByPath[path] = new CachedFingerprint(
                        info.Length,
                        info.LastWriteTimeUtc.Ticks,
                        hash);
                    hashes[entry.Key] = hash;
                }
                catch (Exception exception)
                {
                    warning?.Invoke(
                        $"Could not fingerprint AIVJSON '{entry.Value}': {exception.Message}");
                }
            }

            foreach (string stalePath in fingerprintByPath.Keys
                .Where(path => !currentPaths.Contains(path))
                .ToArray())
            {
                fingerprintByPath.Remove(stalePath);
            }

            return hashes;
        }

        public bool TryCaptureHash(
            string option,
            out string hash,
            out string error)
        {
            hash = string.Empty;
            error = string.Empty;
            if (!TryResolve(option, out string path))
            {
                error = $"AIVJSON is unavailable: '{option}'.";
                return false;
            }

            try
            {
                hash = ComputeHash(path);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not fingerprint AIVJSON '{path}': {exception.Message}";
                return false;
            }
        }

        private static string ComputeHash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        private sealed class CachedFingerprint
        {
            public CachedFingerprint(long length, long lastWriteTimeUtcTicks, string hash)
            {
                Length = length;
                LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
                Hash = hash;
            }

            public long Length { get; }
            public long LastWriteTimeUtcTicks { get; }
            public string Hash { get; }
        }

        private void AddLocalLordRoots()
        {
            string localAppData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DirectoryInfo appData = Directory.GetParent(localAppData);
            if (appData == null)
                return;

            string gameDataRoot = Path.Combine(
                appData.FullName,
                "LocalLow",
                "Firefly Studios",
                "Stronghold Crusader Definitive Edition");

            AddRoot("CustomLords", Path.Combine(gameDataRoot, "CustomLords"));
            AddRoot("ExtendedLords", Path.Combine(gameDataRoot, "ExtendedLords"));
        }

        private void AddRoot(string sourceName, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(root, "*.aivjson", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string relativePath = MakeRelativePath(root, file);
                string option = $"[{sourceName}] {relativePath}";
                pathByOption[option] = file;
            }
        }

        private static string MakeRelativePath(string root, string file)
        {
            string normalizedRoot =
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            Uri rootUri = new Uri(normalizedRoot, UriKind.Absolute);
            Uri fileUri = new Uri(Path.GetFullPath(file), UriKind.Absolute);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
