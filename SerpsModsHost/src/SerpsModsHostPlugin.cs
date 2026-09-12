using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using NoesisKey = Noesis.Key;
using NoesisKeyEventArgs = Noesis.KeyEventArgs;
using NoesisTextBox = Noesis.TextBox;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SerpsModsHost
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SerpsModsHostPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string InfoFileName = "info.json";
        public const string PluginGuid = "SerpsMods_Serp";
        public const string PluginName = "Serps Mods";
        public const string PluginVersion = "1.0.12";
        public const bool CustomCustomTrailModSettingsOptOut = true;
        private const string ManifestFileName = "serps-modpack.json";

        private static SerpsModsHostPlugin instance;
        private static PackLogListener packLogListener;
        private static IDisposable lobbyJoinSubscription;
        private static LobbyModHashWarning lobbyModHashWarning;
        private static LobbyModInventoryPublisher lobbyModInventoryPublisher;
        private readonly List<PackModRecord> activeMods = new List<PackModRecord>();
        private readonly HashSet<string> expectedLogSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private SerpsModsDiagnosticsViewModel diagnostics;
        private ModSettingsSearchViewModel modSettingsSearch;
        private PackManifest manifest;
        private string packRoot;
        private int validatedCount;
        private int registeredCount;

        private void Awake()
        {
            instance = this;
            diagnostics = new SerpsModsDiagnosticsViewModel();
            diagnostics.SetRefreshAction(() => AuditLoadedPlugins(true));
            modSettingsSearch = new ModSettingsSearchViewModel(Logger);
            diagnostics.SetSearch(modSettingsSearch);

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
                lobbyModInventoryPublisher = new LobbyModInventoryPublisher(Logger);
                lobbyModInventoryPublisher.Start();
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
            if (manifest == null || manifest.SchemaVersion != 2)
                throw new InvalidDataException("H002: Unsupported or empty pack manifest.");
            if (!string.Equals(manifest.PackGuid, PluginGuid, StringComparison.Ordinal))
                throw new InvalidDataException($"H002: Pack GUID '{manifest.PackGuid}' does not match '{PluginGuid}'.");
            if (!string.Equals(manifest.HostVersion, PluginVersion, StringComparison.Ordinal))
                throw new InvalidDataException($"H002: Host version '{manifest.HostVersion}' does not match DLL version '{PluginVersion}'.");

            HashSet<string> guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PackModRecord dependency in manifest.Infrastructure ?? new List<PackModRecord>())
            {
                ValidateRecord(root, dependency, guids, paths);
                if (!string.Equals(dependency.State, "Infrastructure", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"H002: Invalid infrastructure state for {dependency.Guid}: {dependency.State}");
                expectedLogSources.Add(dependency.Guid);
                expectedLogSources.Add(dependency.Name);
                validatedCount++;
            }
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

            CheckScriptExtenderCompatibility(root, manifest);

            AuditDuplicateInstallations();
            List<PackModRecord> assetMods = ScriptExtenderCompatibility.SelectRuntimePackRecords(manifest);
            diagnostics.SetStatus(manifest.PackVersion, assetMods.Count, validatedCount, 0);
            foreach (PackModRecord mod in assetMods)
            {
                string directory = ResolveContainedPath(root, mod.RelativePath);
                try
                {
                    GameAssetModManager.Instance.RegisterAssetMod(directory);
                    bool registered = GameAssetModManager.Instance.TryGetRegisteredDirectory(mod.Guid, out string registeredDirectory);
                    if (!RegisteredAssetDirectoryPolicy.TryValidate(directory, registered, registeredDirectory, out string failure))
                        throw new InvalidOperationException(failure);
                    registeredCount++;
                }
                catch (Exception ex)
                {
                    ReportError("H004", $"Asset registration failed for {mod.Guid}; partial registration is possible: {ex}");
                    break;
                }
            }

            diagnostics.SetStatus(manifest.PackVersion, assetMods.Count, validatedCount, registeredCount);
            Shared.DebugLogHelper.LogDebug(
                Logger,
                $"[{PluginName}] pack={manifest.PackVersion}, expected={assetMods.Count}, validated={validatedCount}, registered={registeredCount}.");
        }

        private void CheckScriptExtenderCompatibility(string root, PackManifest packManifest)
        {
            try
            {
                string scriptExtenderAssemblyPath = ResolveScriptExtenderAssemblyPath();
                ScriptExtenderVersionResolution versionResolution = ResolveScriptExtenderVersion(
                    scriptExtenderAssemblyPath);
                if (!versionResolution.IsResolved)
                    throw new InvalidDataException(versionResolution.Diagnostic);

                string installedVersion = versionResolution.Version;
                var requirements = new List<ScriptExtenderCompatibilityRequirement>();
                var issueLines = new List<string>();
                AddScriptExtenderRequirement(
                    requirements,
                    issueLines,
                    PluginName,
                    Path.Combine(root, InfoFileName));
                foreach (PackModRecord record in ScriptExtenderCompatibility.SelectRuntimePackRecords(packManifest))
                {
                    string name = string.IsNullOrWhiteSpace(record.Name) ? record.Guid : record.Name;
                    AddScriptExtenderRequirement(
                        requirements,
                        issueLines,
                        name,
                        Path.Combine(ResolveContainedPath(root, record.RelativePath), InfoFileName));
                }

                foreach (ScriptExtenderCompatibilityIssue issue in
                    ScriptExtenderCompatibility.EvaluateAll(installedVersion, requirements))
                {
                    issueLines.Add(FormatScriptExtenderCompatibilityIssue(issue));
                }

                if (issueLines.Count == 0)
                {
                    diagnostics.SetScriptExtenderCompatibilityWarning(string.Empty);
                    Shared.DebugLogHelper.LogDebug(
                        Logger,
                        $"[{PluginName}] Script Extender {installedVersion} satisfies all {requirements.Count} " +
                        $"runtime component requirements; {versionResolution.Diagnostic}.");
                    return;
                }

                string warning = SerpLocalization.Get(
                    SerpLocalization.SerpsModsScriptExtenderIssuesHeader,
                    "Installed", installedVersion) +
                    Environment.NewLine + string.Join(Environment.NewLine, issueLines.ToArray());
                diagnostics.SetScriptExtenderCompatibilityWarning(warning);
                ReportError("H008", warning);
            }
            catch (Exception ex)
            {
                string warning = SerpLocalization.Get(
                    SerpLocalization.SerpsModsScriptExtenderCheckFailed,
                    "Reason", ex.Message);
                diagnostics.SetScriptExtenderCompatibilityWarning(warning);
                ReportError("H008", warning);
            }
        }

        private static void AddScriptExtenderRequirement(
            List<ScriptExtenderCompatibilityRequirement> requirements,
            List<string> issueLines,
            string name,
            string infoPath)
        {
            try
            {
                if (!File.Exists(infoPath))
                    throw new FileNotFoundException("info.json is missing.", infoPath);

                PackManifestJson.ReadStringProperties(
                    File.ReadAllText(infoPath),
                    "MinimumScriptExtenderVersion",
                    "MaximumScriptExtenderVersion",
                    out string minimumVersion,
                    out string maximumVersion);
                requirements.Add(new ScriptExtenderCompatibilityRequirement
                {
                    Name = name,
                    MinimumVersion = minimumVersion,
                    MaximumVersion = maximumVersion
                });
            }
            catch (Exception ex)
            {
                issueLines.Add(SerpLocalization.Get(
                    SerpLocalization.SerpsModsScriptExtenderComponentCheckFailed,
                    "Name", name,
                    "Reason", ex.Message));
            }
        }

        private static string FormatScriptExtenderCompatibilityIssue(
            ScriptExtenderCompatibilityIssue issue)
        {
            ScriptExtenderCompatibilityResult result = issue.Result;
            string name = issue.Requirement.Name;
            switch (result.Status)
            {
                case ScriptExtenderCompatibilityStatus.BelowMinimum:
                case ScriptExtenderCompatibilityStatus.AboveMaximum:
                    if (!string.IsNullOrWhiteSpace(result.MinimumVersion) && result.HasMaximum)
                    {
                        return SerpLocalization.Get(
                            SerpLocalization.SerpsModsScriptExtenderComponentRange,
                            "Name", name,
                            "Minimum", result.MinimumVersion,
                            "Maximum", result.MaximumVersion);
                    }
                    if (result.Status == ScriptExtenderCompatibilityStatus.AboveMaximum)
                    {
                        return SerpLocalization.Get(
                            SerpLocalization.SerpsModsScriptExtenderComponentMaximum,
                            "Name", name,
                            "Maximum", result.MaximumVersion);
                    }
                    return SerpLocalization.Get(
                        SerpLocalization.SerpsModsScriptExtenderComponentMinimum,
                        "Name", name,
                        "Minimum", result.MinimumVersion);

                case ScriptExtenderCompatibilityStatus.InvalidMinimumVersion:
                    return SerpLocalization.Get(
                        SerpLocalization.SerpsModsScriptExtenderInvalidMinimum,
                        "Name", name,
                        "Minimum", result.MinimumVersion);

                case ScriptExtenderCompatibilityStatus.InvalidMaximumVersion:
                    return SerpLocalization.Get(
                        SerpLocalization.SerpsModsScriptExtenderInvalidMaximum,
                        "Name", name,
                        "Maximum", result.MaximumVersion);

                case ScriptExtenderCompatibilityStatus.InvalidRange:
                    return SerpLocalization.Get(
                        SerpLocalization.SerpsModsScriptExtenderInvalidRange,
                        "Name", name,
                        "Minimum", result.MinimumVersion,
                        "Maximum", result.MaximumVersion);

                default:
                    return SerpLocalization.Get(
                        SerpLocalization.SerpsModsScriptExtenderComponentCheckFailed,
                        "Name", name,
                        "Reason", result.Status.ToString());
            }
        }

        private static ScriptExtenderVersionResolution ResolveScriptExtenderVersion(string assemblyPath)
        {
            var evidence = new List<ScriptExtenderVersionEvidence>();
            string directory = Path.GetDirectoryName(assemblyPath);
            string infoPath = Path.Combine(directory, InfoFileName);
            try
            {
                evidence.Add(new ScriptExtenderVersionEvidence(
                    "info.json",
                    File.Exists(infoPath)
                        ? PackManifestJson.ReadStringProperty(File.ReadAllText(infoPath), "Version")
                        : string.Empty));
            }
            catch (Exception ex)
            {
                evidence.Add(new ScriptExtenderVersionEvidence("info.json", "invalid: " + ex.Message));
            }

            if (Chainloader.PluginInfos.TryGetValue(ScriptExtenderGuid, out PluginInfo pluginInfo))
            {
                evidence.Add(new ScriptExtenderVersionEvidence(
                    "BepInEx metadata",
                    pluginInfo.Metadata.Version?.ToString()));
            }

            try
            {
                evidence.Add(new ScriptExtenderVersionEvidence(
                    "assembly version",
                    AssemblyName.GetAssemblyName(assemblyPath).Version?.ToString()));
            }
            catch (Exception ex)
            {
                evidence.Add(new ScriptExtenderVersionEvidence("assembly version", "invalid: " + ex.Message));
            }

            try
            {
                FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(assemblyPath);
                evidence.Add(new ScriptExtenderVersionEvidence("file version", fileVersion.FileVersion));
                evidence.Add(new ScriptExtenderVersionEvidence("product version", fileVersion.ProductVersion));
            }
            catch (Exception ex)
            {
                evidence.Add(new ScriptExtenderVersionEvidence("file/product version", "invalid: " + ex.Message));
            }

            return ScriptExtenderVersionResolver.Resolve(evidence);
        }

        private static string ResolveScriptExtenderAssemblyPath()
        {
            if (Chainloader.PluginInfos.TryGetValue(ScriptExtenderGuid, out PluginInfo pluginInfo) &&
                !string.IsNullOrWhiteSpace(pluginInfo.Location) &&
                File.Exists(pluginInfo.Location))
                return pluginInfo.Location;

            string conventionalPath = Path.Combine(Paths.PluginPath, ScriptExtenderGuid, "SHCDESE.dll");
            if (File.Exists(conventionalPath))
                return conventionalPath;

            throw new FileNotFoundException("The installed Script Extender assembly could not be found.", conventionalPath);
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
                !string.Equals(mod.State, "Retired", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mod.State, "Infrastructure", StringComparison.OrdinalIgnoreCase))
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

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    diagnostics,
                    "ScriptExtenderUI/SerpsModsStatus.xaml");
                LobbyModSettingsEntry registration = GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .FirstOrDefault(entry => ReferenceEquals(entry.ViewModel, diagnostics));
                NoesisTextBox searchTextBox = registration?.View?.FindName("SerpsModSettingsSearchTextBox") as NoesisTextBox;
                if (searchTextBox != null)
                    searchTextBox.PreviewKeyDown += OnSearchTextBoxPreviewKeyDown;
                AuditLoadedPlugins(false);
            }
            catch (Exception ex)
            {
                ReportError("H005", $"Diagnostics UI registration failed: {ex}");
            }
        }

        private static void OnSearchTextBoxPreviewKeyDown(object sender, NoesisKeyEventArgs args)
        {
            if (args.Key == NoesisKey.Return)
                args.Handled = true;
        }

        private void AuditLoadedPlugins(bool reportMissing)
        {
            if (reportMissing)
                AuditDuplicateInstallations();

            bool apiSharedAvailable = AuditInfrastructurePlugins(reportMissing);
            foreach (PackModRecord mod in activeMods)
            {
                if (!Chainloader.PluginInfos.TryGetValue(mod.Guid, out PluginInfo pluginInfo))
                {
                    if (reportMissing)
                    {
                        string message = TryGetScriptExtenderMismatchMessage(mod, out string mismatchMessage)
                            ? mismatchMessage
                            : PackPluginDiagnosticMessages.MissingChild(mod, apiSharedAvailable);
                        ReportError("H005", message);
                    }
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

        private bool AuditInfrastructurePlugins(bool reportMissing)
        {
            bool apiSharedAvailable = true;
            foreach (PackModRecord dependency in manifest?.Infrastructure ?? new List<PackModRecord>())
            {
                bool isApiShared = PackPluginDiagnosticMessages.IsApiShared(dependency);
                if (!Chainloader.PluginInfos.TryGetValue(dependency.Guid, out PluginInfo pluginInfo))
                {
                    if (isApiShared)
                        apiSharedAvailable = false;
                    if (reportMissing)
                        ReportError("H009", PackPluginDiagnosticMessages.MissingInfrastructure(dependency));
                    continue;
                }

                string actualVersion = pluginInfo.Metadata.Version?.ToString() ?? string.Empty;
                if (!string.Equals(actualVersion, dependency.Version, StringComparison.Ordinal))
                {
                    if (isApiShared)
                        apiSharedAvailable = false;
                    ReportError(
                        "H009",
                        PackPluginDiagnosticMessages.InfrastructureVersionMismatch(dependency, actualVersion));
                }

                string expectedDirectory = ResolveContainedPath(packRoot, dependency.RelativePath);
                string loadedDirectory = Path.GetDirectoryName(pluginInfo.Location);
                if (!DuplicateInstallationDetector.PathsEqual(loadedDirectory, expectedDirectory))
                {
                    ReportError(
                        "H006",
                        $"Infrastructure plugin {dependency.Guid} was loaded from a separate installation at " +
                        $"'{loadedDirectory}' instead of the pack path '{expectedDirectory}'. Remove the separate " +
                        "Workshop/local installation.");
                }
            }

            return apiSharedAvailable;
        }

        private bool TryGetScriptExtenderMismatchMessage(PackModRecord mod, out string message)
        {
            message = string.Empty;
            try
            {
                string modDirectory = ResolveContainedPath(packRoot, mod.RelativePath);
                string infoPath = Path.Combine(modDirectory, InfoFileName);
                PackManifestJson.ReadStringProperties(
                    File.ReadAllText(infoPath),
                    "MinimumScriptExtenderVersion",
                    "MaximumScriptExtenderVersion",
                    out string minimumVersion,
                    out string maximumVersion);

                ScriptExtenderVersionResolution resolution = ResolveScriptExtenderVersion(
                    ResolveScriptExtenderAssemblyPath());
                if (!resolution.IsResolved)
                    return false;

                ScriptExtenderCompatibilityResult compatibility = ScriptExtenderCompatibility.Evaluate(
                    resolution.Version,
                    minimumVersion,
                    maximumVersion);
                if (compatibility.Status != ScriptExtenderCompatibilityStatus.BelowMinimum &&
                    compatibility.Status != ScriptExtenderCompatibilityStatus.AboveMaximum)
                {
                    return false;
                }

                message = PackPluginDiagnosticMessages.MissingChildForScriptExtender(
                    mod,
                    compatibility.InstalledVersion,
                    compatibility.MinimumVersion,
                    compatibility.HasMaximum ? compatibility.MaximumVersion : string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogDebug(
                    Logger,
                    $"[{PluginName}] Could not refine the load diagnostic for {mod.Guid}: {ex.Message}");
                return false;
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
