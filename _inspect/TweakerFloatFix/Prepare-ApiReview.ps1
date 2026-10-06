$ErrorActionPreference = 'Stop'
$review = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Edit([string]$relative, [scriptblock]$change) {
    $path = Join-Path $review $relative
    $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $next = & $change $text
    if ($next -ceq $text) { throw "No change: $relative" }
    $next = $next.Replace("`r`n", "`n").Replace("`n", "`r`n")
    [IO.File]::WriteAllText($path, $next, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $next) { throw "Write mismatch: $relative" }
}
Edit 'Config/PublicApi/ConfigurationApi.cs' {
    param($s)
    $s = $s.Replace('internal static Func<bool> NetworkPreparation;', "internal static Func<bool> NetworkPreparation;`n        internal static Action NetworkEnable;`n`n        /// <summary>Opt in after installing consumer-owned confirmation and start guards.</summary>`n        public static void EnableRestartManagedSynchronization()`n        {`n            lock (Gate) RequireReady();`n            if (NetworkEnable == null) throw new InvalidOperationException(`"Network synchronization is not initialized.`");`n            NetworkEnable();`n        }")
    $s = $s.Replace('if (stage) StageContextConfiguration(desired, contextId, ConfigurationFileTransaction.Revision(own));', 'if (!stage) return false;' + "`n                StageCore(desired, ConfigurationFileTransaction.Revision(own), contextId);")
    $s = $s.Replace("                return false;`n            }`n        }`n`n        public static ConfigurationCapabilities", "            }`n            Notify();`n            return false;`n        }`n`n        public static ConfigurationCapabilities")
    $s = $s.Replace('Stage(documents.Read(own), ConfigurationFileTransaction.Revision(own), "", preserveOwn: true);', 'StageCore(documents.Read(own), ConfigurationFileTransaction.Revision(own), "", preserveOwn: true);')
    $s = $s.Replace("            }`n        }`n`n        private static void Stage(IDictionary", "            }`n            Notify();`n        }`n`n        private static void Stage(IDictionary")
    $s = $s.Replace("            lock (Gate)`n            {`n                RequireReady();`n                var before = transaction.ReadOwn();", "            lock (Gate) StageCore(values, expectedOwnRevision, contextId, preserveOwn);`n            Notify();`n        }`n`n        // Caller holds Gate. Consumer callbacks are always dispatched after releasing it.`n        private static void StageCore(IDictionary<string, object> values, string expectedOwnRevision, string contextId, bool preserveOwn = false)`n        {`n                RequireReady();`n                if (contextId == null || contextId.Length > 1024) throw new ArgumentException(`"Invalid context identity.`", nameof(contextId));`n                var problems = documents.Validate(values);`n                if (problems.Length != 0) throw new InvalidDataException(string.Join(`"; `", problems.Select(x => x.ToString())));`n                var before = transaction.ReadOwn();")
    $s = $s.Replace("            }`n            Notify();`n        }`n        public static void DiscardPendingConfiguration", "        }`n        public static void DiscardPendingConfiguration")
    $s
}
Edit 'Config/Sync/ConfigSyncManager.cs' {
    param($s)
    $s = $s.Replace('private static bool _initialized;', "private static bool _initialized;`n        private static readonly Configuration.RestartSynchronizationPolicy RestartPolicy = new Configuration.RestartSynchronizationPolicy();")
    $s = $s.Replace('Configuration.ConfigurationApi.NetworkPreparation = PrepareNetworkRestart;', "Configuration.ConfigurationApi.NetworkPreparation = PrepareNetworkRestart;`n            Configuration.ConfigurationApi.NetworkEnable = RestartPolicy.Enable;")
    $s = $s.Replace("        private static bool PrepareNetworkRestart()`n        {", "        private static bool PrepareNetworkRestart()`n        {`n            RestartPolicy.RequireEnabled();")
    $s = $s.Replace('if (!CheckNetworkRestart(package, false))', 'if (RestartPolicy.IsEnabled && !CheckNetworkRestart(package, false))')
    $s = $s.Replace('                var written = WriteHostFiles(package);', "                // Record before any legacy side effects: even a failed partial application invalidates startup evidence.`n                RestartPolicy.BeforeHostApplication();`n                var written = WriteHostFiles(package);")
    $s
}
Edit 'Config/Toml/Core/Handlers/PropertyRegistry.cs' {
    param($s)
    $s.Replace('_handler.ValidateValue(typed);', '_handler.ValidateConfigurationValue(typed);')
}
Edit 'Config/Toml/Core/Handlers/PropertyHandler.cs' {
    param($s)
    $s.Replace('        public string Name { get; }', "        public string Name { get; }`n`n        // API validation may reject values that the legacy loader deliberately ignores.`n        internal virtual bool ValidateConfigurationValue(TValue value) => ValidateValue(value);")
}
Edit 'Config/Toml/Units/Properties/ResourceTypeProperties.cs' {
    param($s)
    $s = $s.Replace('return value != null && TryParseResource(value, out _);', 'return value != null;')
    $s.Replace('        internal override bool ValidateValue(string value)', "        internal override bool ValidateConfigurationValue(string value) =>`n            value != null && TryParseResource(value, out _);`n`n        internal override bool ValidateValue(string value)")
}
Edit 'Config/PublicApi/ConfigurationDocuments.cs' {
    param($s)
    $s = $s.Replace('        private readonly string cfgName;', "        private readonly string cfgName;`n        private readonly string[] orderedFiles;`n        private readonly Cell[] orderedCells;`n        private readonly Dictionary<string, Cell[]> cellsByFile;`n        private readonly Dictionary<string, Cell[]> validators;")
    $s = $s.Replace('return defaults.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();', 'return (string[])orderedFiles.Clone();')
    $s = $s.Replace('return cells.Values.Select(x => x.Option.Copy()).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();', 'return orderedCells.Select(x => x.Option.Copy()).ToArray();')
    $s = $s.Replace('            defaults.Add(cfgName, cfg.ToString());', "            defaults.Add(cfgName, cfg.ToString());`n            orderedFiles = defaults.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();`n            orderedCells = cells.Values.OrderBy(x => x.Option.Key, StringComparer.Ordinal).ToArray();`n            cellsByFile = orderedCells.GroupBy(x => x.Option.File, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);`n            validators = orderedCells.Where(x => x.Path.Length == 2).GroupBy(x => x.Option.File + `"\0`" + x.Path[1], StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);")
    $s = $s.Replace('cells.Values.Where(x => x.Option.File == file).ToArray()', 'cellsByFile[file]')
    $start = $s.IndexOf('            if (group == null)', $s.IndexOf('internal void BindValidator'))
    $end = $s.IndexOf('            cell.Type = type;', $start)
    $s = $s.Substring(0, $start) + @'
            if (!validators.TryGetValue(file + "\0" + name, out var candidates)) return;
            foreach (Cell candidate in candidates)
                if (group == null || candidate.Path[0] == group) BindValidator(candidate, type, validate);
        }

        private static void BindValidator(Cell cell, Type type, Func<object, bool> validate)
        {
'@ + "`n" + $s.Substring($end)
    $s
}
Edit 'CrusaderDETweaker.csproj' {
    param($s)
    $s.Replace('    <Compile Include="Config\PublicApi\ConfigurationApi.cs" />', '    <Compile Include="Config\PublicApi\ConfigurationApi.cs" />' + "`n" + '    <Compile Include="Config\PublicApi\RestartSynchronizationPolicy.cs" />')
}
Edit 'Tests/ConfigurationApi/ConfigurationApiTests.csproj' {
    param($s)
    $s.Replace('    <Compile Include="ContextApiTests.cs" />', '    <Compile Include="ContextApiTests.cs" />' + "`n" + '    <Compile Include="RestartSynchronizationTests.cs" />' + "`n" + '    <Compile Include="..\..\Config\PublicApi\RestartSynchronizationPolicy.cs" />')
}
Write-Output 'Applied bounded API review edits.'
function NewSource([string]$relative, [string]$content) {
    $path = Join-Path $review $relative
    if (Test-Path -LiteralPath $path) { throw "Already exists: $relative" }
    $content = $content.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $content) { throw "Write mismatch: $relative" }
}
NewSource 'Config/PublicApi/RestartSynchronizationPolicy.cs' @'
using System;

namespace CrusaderDETweaker.Configuration
{
    // Process-lifetime opt-in. Never reset after a legacy application, including failed attempts.
    internal sealed class RestartSynchronizationPolicy
    {
        private readonly object gate = new object();
        private bool enabled;
        private bool legacyApplicationStarted;

        internal bool IsEnabled { get { lock (gate) return enabled; } }

        internal void Enable()
        {
            lock (gate)
            {
                if (legacyApplicationStarted)
                    throw new InvalidOperationException("Host settings were already applied during this session. Restart the game before enabling restart-managed synchronization.");
                enabled = true;
            }
        }

        internal void RequireEnabled()
        {
            lock (gate)
                if (!enabled) throw new InvalidOperationException("A consumer must enable restart-managed synchronization before preparing host settings.");
        }

        internal void BeforeHostApplication()
        {
            lock (gate)
                if (!enabled) legacyApplicationStarted = true;
        }
    }
}
'@
NewSource 'Tests/ConfigurationApi/RestartSynchronizationTests.cs' @'
using System;
using CrusaderDETweaker.Configuration;

internal static class RestartSynchronizationTests
{
    internal static void Run()
    {
        var policy = new RestartSynchronizationPolicy();
        Check(!policy.IsEnabled, "Standalone synchronization unexpectedly changed");
        Reject(policy.RequireEnabled);
        policy.Enable(); policy.Enable();
        policy.RequireEnabled(); policy.BeforeHostApplication(); policy.Enable();
        Check(policy.IsEnabled, "Idempotent opt-in failed");
        var late = new RestartSynchronizationPolicy();
        late.BeforeHostApplication();
        Reject(late.Enable); Reject(late.Enable);
        Check(!late.IsEnabled, "Late activation published an invalid startup snapshot");
        Console.WriteLine("PASS: standalone synchronization policy, idempotent opt-in and permanent late-registration rejection");
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Expected restart synchronization rejection");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
'@
Edit 'Tests/ConfigurationApi/Program.cs' {
    param($s)
    $s.Replace('        ContextApiTests.Run(root);', "        RestartSynchronizationTests.Run();`n        ContextApiTests.Run(root);")
}
