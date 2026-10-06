$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Read-Source($name) { [IO.File]::ReadAllText((Join-Path $root $name)) }
function Write-Source($name, $text) {
    $text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
    $path = Join-Path $root $name
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $text) { throw "Write verification: $name" }
}
$name = 'Config/PublicApi/ConfigurationApi.cs'
$text = Read-Source $name
$start = $text.IndexOf('        internal static void ApplyPendingBeforeLoading()')
$end = $text.IndexOf('        internal static void CaptureGeneratedDefaults()', $start)
$replacement = @'
        internal static void ApplyPendingBeforeLoading()
        {
            // Keep a current-format transaction available even if pending-package detection fails.
            // Cancellation does not decode a package, so consumers can discard rejected data.
            transaction = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, StateInventory(),
                "configuration-v2/" + PluginInfo.PLUGIN_VERSION);
            try
            {
                ApplyLegacyPackageIfPresent();
                status = transaction.ApplyBeforeLoading();
            }
            catch (Exception ex)
            {
                status = "Startup configuration could not be applied: " + ex.GetBaseException().Message;
                Plugin.Logger.LogError("[ConfigurationApi] " + status);
                // The journal, including a legacy journal, means application has begun.
                // Never let loaders observe a potentially mixed file set.
                if (transaction.RecoveryRequired) throw;
            }

            // A rejected, unstarted package leaves the existing selection authoritative.
            // An invalid active context marker is not a pending-package error: stop loading.
            var startupFiles = transaction.ReadOwn();
            ConfigPaths.StartupContextId = ReadContext(startupFiles);
            Plugin.Logger.LogInfo("[ConfigurationApi] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                " " + status + " Startup file set " + ConfigurationFileTransaction.Revision(startupFiles) + ".");
        }

        private static void ApplyLegacyPackageIfPresent()
        {
            string journalPath = Path.Combine(ConfigPaths.OwnConfigDir, "ApplyingConfiguration.bin");
            string pendingPath = Path.Combine(ConfigPaths.OwnConfigDir, "PendingConfiguration.bin");
            string packagePath = File.Exists(journalPath) ? journalPath : pendingPath;
            if (!File.Exists(packagePath)) return;

            string identity;
            using (var stream = File.OpenRead(packagePath))
            {
                if (stream.Length > ConfigurationFileTransaction.MaximumPackageBytes)
                    throw new InvalidDataException("Configuration package is too large.");
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != 0x43545031)
                        throw new InvalidDataException("Invalid configuration package header.");
                    identity = reader.ReadString();
                }
            }

            string legacyIdentity = "configuration-v1/" + PluginInfo.PLUGIN_VERSION;
            if (identity == legacyIdentity)
            {
                var legacyTransaction = new ConfigurationFileTransaction(
                    ConfigPaths.OwnConfigDir, Inventory(), legacyIdentity);
                legacyTransaction.ApplyBeforeLoading();
            }
        }

'@
$text = $text.Substring(0,$start) + $replacement + "`r`n" + $text.Substring($end)
$text = $text.Replace('SelectFiles(transaction.ReadOwn(), false)', 'PersonalFiles(transaction.ReadOwn())').Replace('SelectFiles(before, false)', 'PersonalFiles(before)').Replace('SelectFiles(pending, true)', 'SelectedStartupFiles(pending)').Replace('SelectFiles(transaction.ReadOwn(), true)', 'SelectedStartupFiles(transaction.ReadOwn())')
$start = $text.IndexOf('        private static Dictionary<string, byte[]> SelectFiles(')
$end = $text.IndexOf('        private static IEnumerable<string> StateInventory()', $start)
$replacement = @'
        private static Dictionary<string, byte[]> PersonalFiles(Dictionary<string, byte[]> state)
        {
            return Inventory().ToDictionary(name => name, name => state[name], StringComparer.Ordinal);
        }

        private static Dictionary<string, byte[]> SelectedStartupFiles(Dictionary<string, byte[]> state)
        {
            if (ReadContext(state).Length == 0) return PersonalFiles(state);
            return Inventory().ToDictionary(name => name,
                name => state[Path.Combine("Session", name)], StringComparer.Ordinal);
        }

'@
$text = $text.Substring(0,$start) + $replacement + "`r`n" + $text.Substring($end)
$text = $text.Replace('                after[Path.Combine("Session", name)] = contextId.Length != 0 ? rendered[name] : before[Path.Combine("Session", name)] ?? own[name];', @'
                string sessionPath = Path.Combine("Session", name);
                byte[] sessionContents = contextId.Length != 0
                    ? rendered[name]
                    : before[sessionPath] ?? own[name];
                after[sessionPath] = sessionContents;
'@)
$text = $text.Replace('            foreach (string name in Inventory()) { yield return name; yield return Path.Combine("Session", name); }', @'
            foreach (string name in Inventory())
            {
                yield return name;
                yield return Path.Combine("Session", name);
            }
'@)
$summaries = @{
'GetCapabilities'='Returns readiness and supported operations without reading configuration files.'
'GetOptions'='Returns a defensive catalog copy. Throws if initialization has not completed.'
'ReadOwnConfiguration'='Reads personal files afresh; Revision is the concurrency token for staging.'
'GetLoadedConfiguration'='Returns the personal or temporary startup snapshot, not live game memory.'
'GetPendingConfiguration'='Returns the next-start snapshot or null; malformed pending data raises an exception.'
'ValidateConfiguration'='Checks a complete candidate without modifying files or game values.'
'StageConfiguration'='Validates and atomically stages personal settings. A stale personal revision is rejected.'
'StageContextConfiguration'='Stages a temporary startup set while preserving personal file contents.'
'StageReturnToOwnConfiguration'='Validates personal files and stages their selection without reformatting them.'
'DiscardPendingConfiguration'='Discards an unstarted package; refuses cancellation once recovery is required.'
'IsNetworkConfigurationClient'='Reports the current network role. Call on the game thread.'
'PrepareNetworkConfiguration'='On explicit consumer confirmation, checks or stages host settings. Call on the game thread after opt-in.'
}
foreach ($method in $summaries.Keys) {
    $pattern = '(?m)^(        public static [^\r\n]+\b' + $method + '\()'
    $text = [regex]::Replace($text, $pattern, '        /// <summary>' + $summaries[$method] + '</summary>' + "`r`n" + '$1')
}
Write-Source $name $text
$name = 'Config/Sync/ConfigSyncManager.cs'
$text = (Read-Source $name).Replace('rejoin this host and select Ready again.', 'rejoin this host and confirm the configuration again.').Replace('Select Ready to prepare them, then restart and rejoin this host.', 'Confirm preparation in the consuming mod, then restart and rejoin this host.')
Write-Source $name $text
$name = 'Tests/ConfigurationApi/ContextApiTests.cs'
$text = Read-Source $name
$fixture = $text.IndexOf('namespace CrusaderDETweaker {')
Write-Source 'Tests/ConfigurationApi/ContextApiFixtures.cs' ("using System;`r`nusing System.IO;`r`nusing CrusaderDETweaker.Configuration;`r`nusing CrusaderDETweaker.Config.Toml;`r`n`r`n" + $text.Substring($fixture))
$text = $text.Substring(0,$fixture).Replace('.Replace("\\r\\n", "\r\n")','')
Write-Source $name $text
$name = 'Tests/ConfigurationApi/ConfigurationApiTests.csproj'
$text = (Read-Source $name).Replace('<Compile Include="ContextApiTests.cs" />', '<Compile Include="ContextApiTests.cs" />' + "`r`n" + '    <Compile Include="ContextApiFixtures.cs" />' + "`r`n" + '    <Compile Include="StartupFailureTests.cs" />')
Write-Source $name $text
$name = 'Tests/ConfigurationApi/Program.cs'
Write-Source $name ((Read-Source $name).Replace('        ContextApiTests.Run(root);','        ContextApiTests.Run(root);' + "`r`n" + '        StartupFailureTests.Run(root);'))
Write-Source 'Tests/ConfigurationApi/StartupFailureTests.cs' @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CrusaderDETweaker.Configuration;
using CrusaderDETweaker.Config.Toml;

internal static class StartupFailureTests
{
    internal static void Run(string root)
    {
        RejectUnstartedPackage(root, "short-header", transaction =>
            File.WriteAllBytes(transaction.PendingPath, new byte[] { 1, 2 }));
        RejectUnstartedPackage(root, "bad-checksum", transaction =>
        {
            byte[] bytes = File.ReadAllBytes(transaction.PendingPath);
            bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(transaction.PendingPath, bytes);
        });
        RejectUnstartedPackage(root, "legacy-conflict", transaction =>
            File.WriteAllText(Path.Combine(ConfigPaths.OwnConfigDir, ConfigPaths.UnitsFileName), "external"), legacy: true);
        RecoverInterruptedMigration(root);
        Console.WriteLine("PASS: facade rejects unstarted packages without changing files; legacy recovery blocks mixed-state loading");
    }

    private static void RejectUnstartedPackage(string root, string scenario,
        Action<ConfigurationFileTransaction> damage, bool legacy = false)
    {
        var transaction = Create(root, scenario, legacy);
        damage(transaction);
        string before = ConfigurationFileTransaction.Revision(transaction.ReadOwn());
        ConfigurationApi.ApplyPendingBeforeLoading();
        Check(before == ConfigurationFileTransaction.Revision(transaction.ReadOwn()), scenario + " changed active files");
        Check(ConfigPaths.StartupContextId == "existing-context", scenario + " lost the active context");
        Check(ConfigurationApi.GetCapabilities().Status.Contains("could not be applied"), scenario + " hid the failure");
        // ContextApiTests has initialized the facade. Startup rejection must retain cancellation.
        ConfigurationApi.DiscardPendingConfiguration();
        Check(!File.Exists(transaction.PendingPath), scenario + " could not be discarded");
    }

    private static void RecoverInterruptedMigration(string root)
    {
        var transaction = Create(root, "legacy-interrupted", true);
        transaction.Checkpoint = point =>
        {
            if (point.StartsWith("written:", StringComparison.Ordinal)) throw new IOException("Interrupted legacy application");
        };
        try { transaction.ApplyBeforeLoading(); }
        catch (IOException) { }
        Check(transaction.RecoveryRequired, "Legacy checkpoint did not leave a recovery journal");
        string units = Path.Combine(ConfigPaths.OwnConfigDir, ConfigPaths.UnitsFileName);
        File.WriteAllText(units, "external-during-recovery");
        string before = ConfigurationFileTransaction.Revision(transaction.ReadOwn());
        bool blocked = false;
        try { ConfigurationApi.ApplyPendingBeforeLoading(); }
        catch (InvalidDataException) { blocked = true; }
        Check(blocked && transaction.RecoveryRequired, "Conflicted legacy recovery allowed loaders to continue");
        Check(before == ConfigurationFileTransaction.Revision(transaction.ReadOwn()), "Rejected recovery changed files");
        File.WriteAllText(units, "before");
        ConfigurationApi.ApplyPendingBeforeLoading();
        Check(!transaction.RecoveryRequired && transaction.ReadOwn().Values.All(bytes =>
            System.Text.Encoding.UTF8.GetString(bytes) == "after"), "Legacy recovery failed to complete");
    }

    private static ConfigurationFileTransaction Create(string root, string scenario, bool legacy)
    {
        ConfigPaths.OwnConfigDir = Path.Combine(root, scenario);
        Directory.CreateDirectory(ConfigPaths.OwnConfigDir);
        var names = new List<string> { "units.toml", "structures.toml", "gameplay.toml",
            "CrusaderDETweaker_GlobalMultipliers.cfg" };
        names.AddRange(new[] { "melee.csv", "ranged.csv", "aoe.csv", "ballista.csv", "fire.csv", "heal.csv", "building.csv" }
            .Select(name => Path.Combine("DamageMatrices", name)));
        string session = Path.Combine(ConfigPaths.OwnConfigDir, "Session");
        Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session, "context.txt"), "existing-context");
        if (!legacy)
        {
            names.AddRange(names.ToArray().Select(name => Path.Combine("Session", name)));
            names.Add("Session/context.txt");
        }
        foreach (string name in names.Where(name => name != "Session/context.txt"))
        {
            string path = Path.Combine(ConfigPaths.OwnConfigDir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "before");
        }
        var transaction = new ConfigurationFileTransaction(ConfigPaths.OwnConfigDir, names,
            (legacy ? "configuration-v1/" : "configuration-v2/") + "test");
        var before = transaction.ReadOwn();
        var after = before.ToDictionary(pair => pair.Key, pair => System.Text.Encoding.UTF8.GetBytes("after"));
        transaction.Stage(after, ConfigurationFileTransaction.Revision(before));
        return transaction;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
'@
$name = 'docs/CONFIGURATION_API.md'
$text = Read-Source $name
$text += @'

## Implementation walkthrough

Startup first recovers or applies the transaction, then the existing generators, CFG bindings
and loaders consume the selected file set. The API captures that startup snapshot and announces
readiness. A consumer may subsequently opt into restart-managed synchronization, after installing
its confirmation UI and persistent start guards. Only explicit confirmation stages another package.

`ConfigurationApi` coordinates these steps; `ConfigurationDocuments` owns catalog construction,
validation and document roundtrips; `ConfigurationFileTransaction` owns bounded package encoding,
atomic publication and recovery. `RestartSynchronizationPolicy` records process-lifetime opt-in.

The journal is necessary because replacing several files cannot be one filesystem operation.
It prevents loaders from observing a partly applied package after interruption. The separate
session files are necessary to retain personal settings while a mission or host uses another set.
A malformed unstarted package is reported and remains discardable; malformed or conflicting
recovery journals stop startup until recovery succeeds. Consumer removal does not bypass recovery.
'@
Write-Source $name $text
