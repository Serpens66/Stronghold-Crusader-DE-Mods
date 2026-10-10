$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
$path = Join-Path $repo 'Config\PublicApi\ConfigurationApi.cs'
$s = [IO.File]::ReadAllText($path).Replace("`r`n","`n")
function ReplaceApiText([string]$old,[string]$new) {
    if (!$script:s.Contains($old)) { throw "Missing anchor: $old" }
    $script:s = $script:s.Replace($old,$new)
}
ReplaceApiText 'private static ConfigurationFileTransaction transaction;' 'private static ConfigurationFileTransaction transaction;
        private static ConfigurationFileTransaction immediateTransaction;
        private static LobbyConfiguration lobby;
        private static string lobbyPath;
        private static Func<string> readActiveLobby;
        private static Action<string> applyActiveLobby;
        private static string activeContext = "";
        private static int gameThread;

        // Bound before startup recovery and before Extender persistence is loaded.
        internal static void ConfigureLobby(LobbyConfiguration document, string path,
            Func<string> readActive, Action<string> applyActive)
        {
            lobby = document;
            lobbyPath = path;
            readActiveLobby = readActive;
            applyActiveLobby = applyActive;
            gameThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        private static void RequireGameThread()
        {
            if (gameThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Lobby application must run on the game thread.");
        }

        internal static bool HasTemporaryLobby => activeContext.Length != 0;
        internal static string SelectedLobbyPath => HasTemporaryLobby
            ? Path.Combine(ConfigPaths.OwnConfigDir, "Session", LobbyConfiguration.FileName) : lobbyPath;'
ReplaceApiText 'Status = status' 'CanApplyLiveSubset = lobby != null && documents != null && loaded != null,
                Status = status'
ReplaceApiText 'return documents.Options;' 'return AllOptions();'
ReplaceApiText 'return documents.Validate(values);' 'return ValidateValues(values);'
ReplaceApiText 'private static void Stage(IDictionary<string, object> values,' '/// <summary>Returns startup-only values plus current live lobby overrides. Call on the game thread.</summary>
        public static ConfigurationSnapshot GetActiveConfiguration()
        {
            lock (Gate)
            {
                RequireReady();
                if (lobby != null) RequireGameThread();
                var snapshot = GetLoadedConfiguration();
                snapshot.Source = "ActiveConfiguration";
                snapshot.ContextId = activeContext;
                if (lobby != null)
                    foreach (var pair in lobby.ReadValues(readActiveLobby())) snapshot.Values[pair.Key] = pair.Value;
                return snapshot;
            }
        }

        /// <summary>Applies only live-capable differences, on the game thread. The complete snapshot is validated first.
        /// Startup-only differences and stale personal revisions are rejected before writes. A write interruption
        /// retains its recovery journal and blocks further application until restart; existing staged work is retained.</summary>
        public static void ApplyLiveConfiguration(IDictionary<string, object> values, string contextId, string expectedOwnRevision)
        {
            lock (Gate)
            {
                RequireGameThread();
                RequireReady();
                if (lobby == null) throw new NotSupportedException("Live lobby application is unavailable.");
                if (IsNetworkConfigurationClient()) throw new InvalidOperationException("Only the lobby owner can apply lobby overrides.");
                var problems = ValidateValues(values);
                if (problems.Length != 0) throw new InvalidDataException(string.Join("; ", problems.Select(x => x.ToString())));
                if (documents.Options.Any(option => !Equals(values[option.Key], loaded.Values[option.Key])))
                    throw new InvalidOperationException("This configuration changes startup-only settings. Stage it for restart.");
                StageCore(values, expectedOwnRevision, contextId, immediate: true);
            }
            Notify();
        }

        private static void Stage(IDictionary<string, object> values,'
ReplaceApiText 'string contextId, bool preserveOwn = false)' 'string contextId, bool preserveOwn = false, bool immediate = false)'
# Both Stage and StageCore signatures now accept the optional internal switch; the public staging path keeps false.
ReplaceApiText 'var problems = documents.Validate(values);' 'var problems = ValidateValues(values);'
ReplaceApiText 'var rendered = preserveOwn ? own : documents.Render(own, new Dictionary<string, object>(values, StringComparer.Ordinal));' 'var allProblems = ValidateValues(values);
            if (allProblems.Length != 0) throw new InvalidDataException(string.Join("; ", allProblems.Select(x => x.ToString())));
            var rendered = preserveOwn ? own : documents.Render(own, FileValues(values));
            if (!preserveOwn && lobby != null)
                rendered[LobbyConfiguration.FileName] = lobby.Render(own[LobbyConfiguration.FileName], values);'
ReplaceApiText 'transaction.Stage(after, ConfigurationFileTransaction.Revision(before));' 'if (immediate)
            {
                immediateTransaction.Stage(after, ConfigurationFileTransaction.Revision(before));
                immediateTransaction.ApplyBeforeLoading();
                activeContext = contextId;
                applyActiveLobby(LobbyConfiguration.ReadEncoded(rendered[LobbyConfiguration.FileName]));
                status = "Lobby overrides applied; startup settings unchanged.";
                return;
            }
            transaction.Stage(after, ConfigurationFileTransaction.Revision(before));'
ReplaceApiText 'StageCore(documents.Read(own),' 'StageCore(ReadValues(own),'
ReplaceApiText '"configuration-v2/" + PluginInfo.PLUGIN_VERSION);' '(lobby == null ? "configuration-v2/" : "configuration-v3/") + PluginInfo.PLUGIN_VERSION, lobbyPath);
            immediateTransaction = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, StateInventory(),
                "configuration-v3/" + PluginInfo.PLUGIN_VERSION, lobbyPath, immediate: true);'
ReplaceApiText 'ApplyLegacyPackageIfPresent();' 'immediateTransaction.ApplyBeforeLoading();
                ApplyLegacyPackageIfPresent();'
ReplaceApiText 'if (transaction.RecoveryRequired) throw;' 'if (transaction.RecoveryRequired || immediateTransaction.RecoveryRequired) throw;'
ReplaceApiText 'ConfigPaths.StartupContextId = ReadContext(startupFiles);' 'ConfigPaths.StartupContextId = ReadContext(startupFiles);
            activeContext = ConfigPaths.StartupContextId;'
ReplaceApiText 'Inventory().Where(x => x != CfgName)' 'Inventory().Where(x => x != CfgName && x != LobbyConfiguration.FileName)'
ReplaceApiText 'Values = documents.Read(files)' 'Values = ReadValues(files)'
ReplaceApiText 'if (documents == null || loaded == null) throw new InvalidOperationException(status);' 'if (documents == null || loaded == null) throw new InvalidOperationException(status);
            if (transaction.RecoveryRequired || immediateTransaction.RecoveryRequired)
                throw new InvalidOperationException("Configuration recovery is required. Restart before continuing.");'
ReplaceApiText 'yield return CfgName;' 'yield return CfgName;
            if (lobby != null) yield return LobbyConfiguration.FileName;'
ReplaceApiText '        private static void RequireReady()' '        private static Dictionary<string, object> FileValues(IDictionary<string, object> values) =>
            values.Where(pair => !LobbyConfiguration.IsOption(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        private static ConfigurationOption[] AllOptions() => lobby == null ? documents.Options
            : documents.Options.Concat(lobby.Options).ToArray();

        private static Dictionary<string, object> ReadValues(Dictionary<string, byte[]> files)
        {
            var values = documents.Read(files);
            if (lobby != null)
                foreach (var pair in lobby.Read(files[LobbyConfiguration.FileName])) values.Add(pair.Key, pair.Value);
            return values;
        }

        private static ConfigurationProblem[] ValidateValues(IDictionary<string, object> values)
        {
            if (values == null) return documents.Validate(null);
            if (lobby == null) return documents.Validate(values);
            return documents.Validate(FileValues(values)).Concat(lobby.Validate(values)).ToArray();
        }

        private static void RequireReady()'
# Received file sets omit lobby caps: these already arrive through the original authenticated MaxCounts property.
ReplaceApiText 'var desired = documents.Read(candidateFiles);' 'var desired = ReadValues(candidateFiles);
                if (lobby != null)
                    foreach (var pair in lobby.ReadValues(readActiveLobby())) desired[pair.Key] = pair.Value;'
ReplaceApiText 'var problems = documents.Validate(desired);' 'var problems = ValidateValues(desired);'
# Live overrides are authoritative in the original host transport; only startup settings need a restart comparison.
$s = $s.Replace("`n","`r`n")
[IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
if ([IO.File]::ReadAllText($path) -cne $s) { throw 'Readback mismatch' }
