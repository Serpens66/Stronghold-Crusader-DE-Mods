using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SerpsModsHost
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SerpsModsHostPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "SerpsMods_Serp";
        public const string PluginName = "Serps Mods";
        public const string PluginVersion = "1.0.3";
        public const bool CustomCustomTrailModSettingsOptOut = true;
        private const string ManifestFileName = "serps-modpack.json";

        private static SerpsModsHostPlugin instance;
        private static PackLogListener packLogListener;
        private static IDisposable lobbyJoinSubscription;
        private static LobbyModHashWarning lobbyModHashWarning;
        private readonly List<PackModRecord> activeMods = new List<PackModRecord>();
        private readonly HashSet<string> expectedLogSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private SerpsModsDiagnosticsViewModel diagnostics;
        private PackManifest manifest;
        private string packRoot;
        private int validatedCount;
        private int registeredCount;

        private void Awake()
        {
            instance = this;
            diagnostics = new SerpsModsDiagnosticsViewModel();
            diagnostics.SetRefreshAction(() => AuditLoadedPlugins(true));

            try
            {
                LoadValidateAndRegisterPack();
            }
            catch (Exception ex)
            {
                ReportError("H000", $"Host initialization failed: {ex}");
            }

            packLogListener = new PackLogListener(expectedLogSources, diagnostics);
            BepInEx.Logging.Logger.Listeners.Add(packLogListener);

            try
            {
                lobbyModHashWarning = new LobbyModHashWarning(Logger);
                lobbyJoinSubscription = Shared.LobbyLifecycle.SubscribeJoined(
                    Logger,
                    lobbyModHashWarning.CheckAfterJoin);
            }
            catch (Exception ex)
            {
                ReportError("H007", $"Lobby mod-hash monitoring could not be installed: {ex}");
            }

            // CrusaderLibrary invokes late subscribers immediately, which makes this safe
            // even though the Script Extender itself loads before this host.
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void LoadValidateAndRegisterPack()
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            packRoot = root;
            string manifestPath = Path.Combine(root, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException($"H001: Missing pack manifest: {manifestPath}");

            manifest = PackManifestJson.Read(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.SchemaVersion != 1)
                throw new InvalidDataException("H002: Unsupported or empty pack manifest.");
            if (!string.Equals(manifest.PackGuid, PluginGuid, StringComparison.Ordinal))
                throw new InvalidDataException($"H002: Pack GUID '{manifest.PackGuid}' does not match '{PluginGuid}'.");
            if (!string.Equals(manifest.HostVersion, PluginVersion, StringComparison.Ordinal))
                throw new InvalidDataException($"H002: Host version '{manifest.HostVersion}' does not match DLL version '{PluginVersion}'.");

            HashSet<string> guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PackModRecord mod in manifest.Mods ?? new List<PackModRecord>())
            {
                ValidateRecord(root, mod, guids, paths);
                if (string.Equals(mod.State, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    activeMods.Add(mod);
                    expectedLogSources.Add(mod.Guid);
                    expectedLogSources.Add(mod.Name);
                }
                validatedCount++;
            }

            AuditDuplicateInstallations();
            diagnostics.SetStatus(manifest.PackVersion, activeMods.Count, validatedCount, 0);
            foreach (PackModRecord mod in activeMods)
            {
                string directory = ResolveContainedPath(root, mod.RelativePath);
                try
                {
                    GameAssetModManager.Instance.RegisterAssetMod(directory);
                    bool registered = GameAssetModManager.Instance.GetRegisteredAssetDirectories()
                        .Any(entry => string.Equals(entry.Key.GUID, mod.Guid, StringComparison.OrdinalIgnoreCase));
                    if (!registered)
                        throw new InvalidOperationException("The Script Extender did not expose the registered GUID afterwards.");
                    registeredCount++;
                }
                catch (Exception ex)
                {
                    ReportError("H004", $"Asset registration failed for {mod.Guid}; partial registration is possible: {ex}");
                    break;
                }
            }

            diagnostics.SetStatus(manifest.PackVersion, activeMods.Count, validatedCount, registeredCount);
            Shared.DebugLogHelper.LogDebug(
                Logger,
                $"[{PluginName}] pack={manifest.PackVersion}, expected={activeMods.Count}, validated={validatedCount}, registered={registeredCount}.");
        }

        private static void ValidateRecord(
            string root,
            PackModRecord mod,
            HashSet<string> guids,
            HashSet<string> paths)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.Guid) || string.IsNullOrWhiteSpace(mod.RelativePath))
                throw new InvalidDataException("H002: A mod record is missing GUID or RelativePath.");
            if (!string.Equals(mod.State, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mod.State, "Retired", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"H002: Invalid state for {mod.Guid}: {mod.State}");
            if (!guids.Add(mod.Guid))
                throw new InvalidDataException($"H002: Duplicate mod GUID: {mod.Guid}");
            if (!paths.Add(mod.RelativePath))
                throw new InvalidDataException($"H002: Duplicate mod path: {mod.RelativePath}");

            string directory = ResolveContainedPath(root, mod.RelativePath);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"H003: Missing mod directory for {mod.Guid}: {directory}");

            foreach (PackFileRecord file in mod.Files ?? new List<PackFileRecord>())
            {
                string path = ResolveContainedPath(directory, file.Path);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"H003: Missing file for {mod.Guid}: {file.Path}", path);
                FileInfo info = new FileInfo(path);
                if (info.Length != file.Size)
                    throw new InvalidDataException($"H003: Size mismatch for {mod.Guid}/{file.Path}.");
                string hash = ComputeSha256(path);
                if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"H003: SHA-256 mismatch for {mod.Guid}/{file.Path}.");
            }

            string infoPath = Path.Combine(directory, "info.json");
            PackManifestJson.ReadStringProperties(
                File.ReadAllText(infoPath),
                "GUID",
                "Version",
                out string actualGuid,
                out string actualVersion);
            if (!string.Equals(actualGuid, mod.Guid, StringComparison.Ordinal) ||
                !string.Equals(actualVersion, mod.Version, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"H003: info.json identity mismatch for {mod.Guid}.");
            }
        }

        private void OnCrusaderLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    diagnostics,
                    "ScriptExtenderUI/SerpsModsStatus.xaml");
                AuditLoadedPlugins(false);
            }
            catch (Exception ex)
            {
                ReportError("H005", $"Diagnostics UI registration failed: {ex}");
            }
        }

        private void AuditLoadedPlugins(bool reportMissing)
        {
            if (reportMissing)
                AuditDuplicateInstallations();

            foreach (PackModRecord mod in activeMods)
            {
                if (!Chainloader.PluginInfos.TryGetValue(mod.Guid, out PluginInfo pluginInfo))
                {
                    if (reportMissing)
                        ReportError("H005", $"Expected child plugin is not loaded: {mod.Guid} v{mod.Version}.");
                    continue;
                }

                string actualVersion = pluginInfo.Metadata.Version?.ToString() ?? string.Empty;
                if (!string.Equals(actualVersion, mod.Version, StringComparison.Ordinal))
                    ReportError("H005", $"Loaded child version mismatch: {mod.Guid}, expected {mod.Version}, actual {actualVersion}.");

                string expectedDirectory = ResolveContainedPath(packRoot, mod.RelativePath);
                string loadedDirectory = Path.GetDirectoryName(pluginInfo.Location);
                if (!DuplicateInstallationDetector.PathsEqual(loadedDirectory, expectedDirectory))
                {
                    ReportError(
                        "H006",
                        $"Child plugin {mod.Guid} was loaded from a separate installation at '{loadedDirectory}' instead of the pack path '{expectedDirectory}'. Remove the separate Workshop/local installation.");
                }
            }
        }

        private void AuditDuplicateInstallations()
        {
            if (string.IsNullOrWhiteSpace(packRoot))
                return;

            foreach (PackModRecord mod in activeMods)
            {
                string expectedDirectory = ResolveContainedPath(packRoot, mod.RelativePath);
                try
                {
                    foreach (string duplicateDirectory in DuplicateInstallationDetector.FindSeparateManifestDirectories(
                        Paths.PluginPath,
                        expectedDirectory,
                        mod.Guid))
                    {
                        ReportError(
                            "H006",
                            $"Duplicate installation detected for {mod.Guid}: packed at '{expectedDirectory}' and separately installed at '{duplicateDirectory}'. Remove the separate Workshop/local installation.");
                    }
                }
                catch (Exception ex)
                {
                    ReportError("H006", $"Duplicate-installation scan failed for {mod.Guid}: {ex.Message}");
                }
            }
        }

        private void ReportError(string code, string message)
        {
            string full = $"[{PluginName}] ERROR {code}: {message}";
            diagnostics?.RecordError(full);
            Shared.DebugLogHelper.LogError(Logger, full);
        }

        private static string ResolveContainedPath(string parent, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException($"H002: Invalid relative path: {relative}");
            string fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(parent, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"H002: Path leaves its package root: {relative}");
            return full;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class PackLogListener : ILogListener
        {
            private readonly HashSet<string> sources;
            private readonly SerpsModsDiagnosticsViewModel viewModel;

            public PackLogListener(HashSet<string> sources, SerpsModsDiagnosticsViewModel viewModel)
            {
                this.sources = sources;
                this.viewModel = viewModel;
            }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if ((eventArgs.Level & (LogLevel.Error | LogLevel.Fatal)) == 0)
                    return;
                string source = eventArgs.Source?.SourceName ?? string.Empty;
                if (!sources.Contains(source))
                    return;
                viewModel.RecordError($"[{source}] {eventArgs.Level}: {eventArgs.Data}");
            }

            public void Dispose()
            {
            }
        }
    }
}
