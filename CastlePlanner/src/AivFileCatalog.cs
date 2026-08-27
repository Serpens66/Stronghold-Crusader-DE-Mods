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
using SHCDESE.Interop.Enums;

namespace CastlePlanner
{
    internal sealed class AivFileCatalog
    {
        internal sealed class DiscoveryPlan
        {
            internal readonly List<PreparedOption> Options = new List<PreparedOption>();
            internal readonly List<RootSpec> Roots = new List<RootSpec>();
        }

        internal sealed class PreparedOption
        {
            internal PreparedOption(string option, string path)
            {
                Option = option;
                Path = path;
            }

            internal string Option { get; }
            internal string Path { get; }
        }

        internal sealed class RootSpec
        {
            internal RootSpec(string sourceName, string path)
            {
                SourceName = sourceName;
                Path = path;
            }

            internal string SourceName { get; }
            internal string Path { get; }
        }

        private static readonly Regex VanillaFileNamePattern = new Regex(
            @"^(?:Community_(?:Historical_)?)?(?<lord>.+?)(?<number>[1-8])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> pathByOption =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> discoveryOrder = new List<string>();
        private readonly Dictionary<string, CachedFingerprint> fingerprintByPath =
            new Dictionary<string, CachedFingerprint>(StringComparer.OrdinalIgnoreCase);

        public int IdenticalFileCount { get; private set; }

        public static DiscoveryPlan PrepareDiscovery(Action<string> warning = null)
        {
            var plan = new DiscoveryPlan();
            string pluginDirectory =
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                string.Empty;
            PrepareVanillaRoot(
                plan,
                Path.Combine(pluginDirectory, "VanillaAIV"),
                warning);
            plan.Roots.Add(new RootSpec("Mod", Path.Combine(pluginDirectory, "AIV")));
            AddLocalLordRoots(plan);
            AddWorkshopRoots(plan, warning);
            plan.Roots.Add(new RootSpec(
                "Editor",
                Path.Combine(
                    Paths.GameRootPath + " - Castle & CPU Lord Editor",
                    "CrusaderCastleEditorUnity_Data",
                    "StreamingAssets",
                    "Villages")));
            return plan;
        }

        public IReadOnlyList<string> Discover(
            DiscoveryPlan plan,
            Action<string> warning = null)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            pathByOption.Clear();
            discoveryOrder.Clear();
            IdenticalFileCount = 0;

            foreach (PreparedOption option in plan.Options)
                AddOption(option.Option, option.Path);
            foreach (RootSpec root in plan.Roots)
                AddRoot(root.SourceName, root.Path);

            RemoveIdenticalFiles(warning);

            return pathByOption.Keys
                .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void PrepareVanillaRoot(
            DiscoveryPlan plan,
            string root,
            Action<string> warning)
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

                plan.Options.Add(new PreparedOption(
                    $"[Vanilla] {fileName}",
                    effectivePath));
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

        private static void AddWorkshopRoots(
            DiscoveryPlan plan,
            Action<string> warning)
        {
            foreach (string itemRoot in Shared.WorkshopContentPaths
                .GetSubscribedItemRoots(warning)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string itemId = Path.GetFileName(
                    itemRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                plan.Roots.Add(new RootSpec("Steam Workshop " + itemId, itemRoot));
            }
        }

        public bool TryResolve(string option, out string fullPath)
        {
            return pathByOption.TryGetValue(option ?? string.Empty, out fullPath) &&
                   File.Exists(fullPath);
        }

        public bool TryResolveSelection(
            string option,
            out string fullPath,
            out ushort flagProjectileType,
            out string warning)
        {
            flagProjectileType = (ushort)ProjectileType.CrusaderFlag;
            warning = string.Empty;
            if (!TryResolve(option, out fullPath))
                return false;

            flagProjectileType = ResolveFlagProjectileType(
                fullPath,
                out _,
                out warning);
            return true;
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
                    hashes[entry.Key] = GetFingerprint(path, forceRefresh, warning);
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
                ushort flagProjectileType = ResolveFlagProjectileType(
                    path,
                    out _,
                    out _);
                hash = ComputeHash(path, flagProjectileType);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not fingerprint AIVJSON '{path}': {exception.Message}";
                return false;
            }
        }

        private static string ComputeHash(string path, ushort flagProjectileType)
        {
            short[] raw = AivRawDataEncoder.Encode(
                AivJsonReader.Parse(File.ReadAllText(path)));
            return FreeCastleProtocol.HashSelectionContent(
                raw,
                flagProjectileType);
        }

        private string GetFingerprint(
            string path,
            bool forceRefresh,
            Action<string> warning = null)
        {
            var info = new FileInfo(path);
            ushort flagProjectileType = ResolveFlagProjectileType(
                path,
                out string lordPath,
                out _);
            FileInfo lordInfo = !string.IsNullOrEmpty(lordPath) && File.Exists(lordPath)
                ? new FileInfo(lordPath)
                : null;
            if (!forceRefresh &&
                fingerprintByPath.TryGetValue(path, out CachedFingerprint cached) &&
                cached.Length == info.Length &&
                cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks &&
                cached.FlagProjectileType == flagProjectileType &&
                string.Equals(cached.LordPath, lordPath, StringComparison.OrdinalIgnoreCase) &&
                cached.LordLength == (lordInfo?.Length ?? -1) &&
                cached.LordLastWriteTimeUtcTicks == (lordInfo?.LastWriteTimeUtc.Ticks ?? -1))
            {
                return cached.Hash;
            }

            string hash = ComputeHash(path, flagProjectileType);
            fingerprintByPath[path] = new CachedFingerprint(
                info.Length,
                info.LastWriteTimeUtc.Ticks,
                flagProjectileType,
                lordPath,
                lordInfo?.Length ?? -1,
                lordInfo?.LastWriteTimeUtc.Ticks ?? -1,
                hash);
            return hash;
        }

        private void RemoveIdenticalFiles(Action<string> warning)
        {
            var firstOptionByHash = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            int duplicateCount = 0;
            foreach (string option in discoveryOrder.ToArray())
            {
                if (!pathByOption.TryGetValue(option, out string path))
                    continue;

                try
                {
                    string hash = GetFingerprint(path, forceRefresh: false, warning);
                    if (firstOptionByHash.ContainsKey(hash))
                    {
                        pathByOption.Remove(option);
                        duplicateCount++;
                    }
                    else
                    {
                        firstOptionByHash.Add(hash, option);
                    }
                }
                catch (Exception exception)
                {
                    warning?.Invoke(
                        $"Could not fingerprint AIVJSON '{path}' while removing identical files: {exception.Message}");
                }
            }

            IdenticalFileCount = duplicateCount;
        }

        private sealed class CachedFingerprint
        {
            public CachedFingerprint(
                long length,
                long lastWriteTimeUtcTicks,
                ushort flagProjectileType,
                string lordPath,
                long lordLength,
                long lordLastWriteTimeUtcTicks,
                string hash)
            {
                Length = length;
                LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
                FlagProjectileType = flagProjectileType;
                LordPath = lordPath ?? string.Empty;
                LordLength = lordLength;
                LordLastWriteTimeUtcTicks = lordLastWriteTimeUtcTicks;
                Hash = hash;
            }

            public long Length { get; }
            public long LastWriteTimeUtcTicks { get; }
            public ushort FlagProjectileType { get; }
            public string LordPath { get; }
            public long LordLength { get; }
            public long LordLastWriteTimeUtcTicks { get; }
            public string Hash { get; }
        }

        internal static ushort ResolveFlagProjectileType(
            string aivPath,
            out string lordPath,
            out string warning) =>
            AivLordJsonResolver.ResolveFlagProjectileType(
                aivPath,
                out lordPath,
                out warning);

        private static void AddLocalLordRoots(DiscoveryPlan plan)
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

            plan.Roots.Add(new RootSpec(
                "CustomLords",
                Path.Combine(gameDataRoot, "CustomLords")));
            plan.Roots.Add(new RootSpec(
                "ExtendedLords",
                Path.Combine(gameDataRoot, "ExtendedLords")));
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
                AddOption(option, file);
            }
        }

        private void AddOption(string option, string path)
        {
            if (!pathByOption.ContainsKey(option))
                discoveryOrder.Add(option);
            pathByOption[option] = path;
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
