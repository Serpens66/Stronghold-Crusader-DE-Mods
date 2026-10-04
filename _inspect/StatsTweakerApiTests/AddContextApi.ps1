$ErrorActionPreference = 'Stop'
$root = 'D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\crusader-de-tweaker'
function Edit([string]$relative, [scriptblock]$action) {
    $path = Join-Path $root $relative
    $text = & $action ([IO.File]::ReadAllText($path))
    $text = [regex]::Replace($text, "\r?\n", "`r`n")
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $text) { throw "Readback failed: $relative" }
}
Edit 'Config\Toml\ConfigPaths.cs' { param($t)
    $t.Replace('internal static string ActiveConfigDir => UseHostSyncFiles ? HostSyncDir : OwnConfigDir;', @'
internal static string StartupContextId { get; set; } = "";
        internal static string StartupConfigDir => string.IsNullOrEmpty(StartupContextId) ? OwnConfigDir : Path.Combine(OwnConfigDir, "Session");
        internal static string ActiveConfigDir => UseHostSyncFiles ? HostSyncDir : StartupConfigDir;
'@)
}
Edit 'Plugin.cs' { param($t) $t.Replace('Path.Combine(Paths.ConfigPath, "CrusaderDETweaker", "CrusaderDETweaker_GlobalMultipliers.cfg")', 'Path.Combine(CrusaderDETweaker.Config.Toml.ConfigPaths.StartupConfigDir, "CrusaderDETweaker_GlobalMultipliers.cfg")') }
Edit 'Config\Sync\ConfigSyncManager.cs' { param($t) $t.Replace('Path.Combine(ConfigPaths.OwnConfigDir, ConfigSyncCodec.RelativePathFor(id))','Path.Combine(ConfigPaths.StartupConfigDir, ConfigSyncCodec.RelativePathFor(id))') }
Edit 'Config\PublicApi\ConfigurationContracts.cs' { param($t)
    $t.Replace('public bool IsSupported { get; internal set; }','public bool RequiresRestart { get { return true; } }' + "`r`n        " + 'public bool IsSupported { get; internal set; }').Replace('public string Source { get; internal set; }', 'public string ContextId { get; internal set; }' + "`r`n        " + 'public string Source { get; internal set; }').Replace('public bool CanApplyWithoutRestart { get { return false; } }', 'public bool CanApplyWithoutRestart { get { return false; } }' + "`r`n        " + 'public bool CanStageContextConfigurations { get { return true; } }')
}
Edit 'Config\PublicApi\ConfigurationApi.cs' { param($t)
    $t=$t.Replace('private const string CfgName', 'private const string ContextFile = "Session/context.txt";' + "`r`n        " + 'private const string CfgName')
    $t=$t.Replace('Snapshot(transaction.ReadOwn(), "OwnFiles")', 'Snapshot(SelectFiles(transaction.ReadOwn(), false), "OwnFiles")')
    $t=$t.Replace('Revision = loaded.Revision, Source = loaded.Source,', 'Revision = loaded.Revision, Source = loaded.Source, ContextId = loaded.ContextId,')
    $t=$t.Replace('return pending == null ? null : Snapshot(pending, "PendingRestart");', 'return pending == null ? null : Snapshot(SelectFiles(pending, true), "PendingRestart", ReadContext(pending));')
    $start=$t.IndexOf('        public static void StageConfiguration(')
    $end=$t.IndexOf('        public static void DiscardPendingConfiguration()', $start)
    if($start -lt 0 -or $end -lt 0){throw 'API stage anchors missing'}
    $replacement=@'
        public static void StageConfiguration(IDictionary<string, object> values, string expectedOwnRevision)
        {
            Stage(values, expectedOwnRevision, "");
        }

        public static void StageContextConfiguration(IDictionary<string, object> values, string contextId, string expectedOwnRevision)
        {
            if (string.IsNullOrWhiteSpace(contextId) || contextId.Length > 1024)
                throw new ArgumentException("Invalid context identity.", nameof(contextId));
            Stage(values, expectedOwnRevision, contextId);
        }

        public static void StageReturnToOwnConfiguration()
        {
            lock (Gate)
            {
                RequireReady();
                var own = SelectFiles(transaction.ReadOwn(), false);
                Stage(documents.Read(own), ConfigurationFileTransaction.Revision(own), "");
            }
        }

        private static void Stage(IDictionary<string, object> values, string expectedOwnRevision, string contextId)
        {
            lock (Gate)
            {
                RequireReady();
                var before = transaction.ReadOwn();
                var own = SelectFiles(before, false);
                if (ConfigurationFileTransaction.Revision(own) != expectedOwnRevision)
                    throw new InvalidOperationException("Your files changed. Import them again before staging.");
                var rendered = documents.Render(own, new Dictionary<string, object>(values, StringComparer.Ordinal));
                var after = new Dictionary<string, byte[]>(before, StringComparer.Ordinal);
                foreach (string name in Inventory())
                {
                    // A single journal includes selection and both file sets. Temporary contexts
                    // retain the personal bytes exactly; no rollback ever depends on a consumer mod.
                    if (contextId.Length == 0) after[name] = rendered[name];
                    after[Path.Combine("Session", name)] = contextId.Length != 0 ? rendered[name] : before[Path.Combine("Session", name)] ?? own[name];
                }
                after[ContextFile] = Encoding.UTF8.GetBytes(contextId);
                transaction.Stage(after, ConfigurationFileTransaction.Revision(before));
                status = "Configuration staged. Restart the game to apply it.";
                Plugin.Logger.LogInfo("[ConfigurationApi] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Staged configuration " + ConfigurationFileTransaction.Revision(rendered) + " for next startup.");
            }
            Notify();
        }

'@
    $t=$t.Substring(0,$start)+$replacement+$t.Substring($end)
    $t=$t.Replace('transaction = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, Inventory(), "configuration-v1/" + PluginInfo.PLUGIN_VERSION);', @'
// Complete a package from the original API before upgrading the transaction inventory.
            string oldJournal = Path.Combine(ConfigPaths.OwnConfigDir, "ApplyingConfiguration.bin");
            string oldPending = Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin");
            string existing = File.Exists(oldJournal) ? oldJournal : oldPending;
            if (File.Exists(existing))
            {
                string identity;
                using (var reader = new BinaryReader(File.OpenRead(existing))) { reader.ReadInt32(); identity = reader.ReadString(); }
                if (identity == "configuration-v1/" + PluginInfo.PLUGIN_VERSION)
                    new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, Inventory(), identity).ApplyBeforeLoading();
            }
            transaction = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, StateInventory(), "configuration-v2/" + PluginInfo.PLUGIN_VERSION);
'@)
    $t=$t.Replace('status = transaction.ApplyBeforeLoading();', 'status = transaction.ApplyBeforeLoading();' + "`r`n                " + 'ConfigPaths.StartupContextId = ReadContext(transaction.ReadOwn());')
    $t=$t.Replace('Path.Combine(ConfigPaths.OwnConfigDir, relative)', 'Path.Combine(ConfigPaths.StartupConfigDir, relative)')
    $t=$t.Replace('loaded = Snapshot(transaction.ReadOwn(), "LoadedAtStartup");', 'loaded = Snapshot(SelectFiles(transaction.ReadOwn(), true), "LoadedAtStartup", ConfigPaths.StartupContextId);')
    $t=$t.Replace('Snapshot(Dictionary<string, byte[]> files, string source)', 'Snapshot(Dictionary<string, byte[]> files, string source, string contextId = "")')
    $t=$t.Replace('Source = source, Values = documents.Read(files)', 'Source = source, ContextId = contextId, Values = documents.Read(files)')
    $t=$t.Replace('        private static IEnumerable<string> Inventory()', @'
        private static string ReadContext(Dictionary<string, byte[]> files)
        {
            if (!files.TryGetValue(ContextFile, out var bytes) || bytes == null) return "";
            if (bytes.Length > 4096) throw new InvalidDataException("Invalid configuration context marker.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static Dictionary<string, byte[]> SelectFiles(Dictionary<string, byte[]> state, bool selected)
        {
            bool session = selected && ReadContext(state).Length != 0;
            return Inventory().ToDictionary(name => name, name => state[session ? Path.Combine("Session", name) : name], StringComparer.Ordinal);
        }

        private static IEnumerable<string> StateInventory()
        {
            foreach (string name in Inventory()) { yield return name; yield return Path.Combine("Session", name); }
            yield return ContextFile;
        }

        private static IEnumerable<string> Inventory()
'@)
    $t
}
