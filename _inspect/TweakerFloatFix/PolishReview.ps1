$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Fremde Mods\crusader-de-tweaker-api-review'))
function Read-Source($name) { [IO.File]::ReadAllText((Join-Path $root $name)) }
function Write-Source($name, $text) {
    $text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
    $path = Join-Path $root $name
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $text) { throw "Write verification: $name" }
}
$name = 'Tests/ConfigurationApi/ContextApiTests.cs'
$text = Read-Source $name
$networkStart = $text.IndexOf('        var hostFiles =')
$networkEnd = $text.IndexOf('        ConfigurationApi.Changed -= listener;', $networkStart)
$network = $text.Substring($networkStart, $networkEnd - $networkStart).Replace('!callbackBlocked','!callbackIsBlocked()')
$tailStart = $text.IndexOf('        own = ConfigurationApi.ReadOwnConfiguration();', $networkEnd)
$tailEnd = $text.IndexOf('    private static void Start()', $tailStart)
$tail = $text.Substring($tailStart, $tailEnd - $tailStart)
$tail = $tail.Replace('        own = ConfigurationApi.ReadOwnConfiguration();','        var own = ConfigurationApi.ReadOwnConfiguration();').Replace('        string pendingRevision', '        string units = Path.Combine(ConfigPaths.OwnConfigDir, ConfigPaths.UnitsFileName);' + "`r`n" + '        string pendingRevision')
$text = $text.Substring(0,$networkStart) + @'
        VerifyNetworkRoundtrip(() => callbackBlocked);
        ConfigurationApi.Changed -= listener;
        VerifyRejectedReturnAndMigration(values);
    }

    private static void VerifyNetworkRoundtrip(Func<bool> callbackIsBlocked)
    {
'@ + "`r`n" + $network + "    }`r`n`r`n" + @'
    private static void VerifyRejectedReturnAndMigration(Dictionary<string, object> values)
    {
'@ + "`r`n" + $tail + $text.Substring($tailEnd)
$text = $text.Replace('ConfigGenerator.GenerateDefaultConfigGlobals(); ConfigGenerator.GenerateDefaultConfigUnits(); ConfigGenerator.GenerateDefaultConfigStructures();', "ConfigGenerator.GenerateDefaultConfigGlobals();`r`n        ConfigGenerator.GenerateDefaultConfigUnits();`r`n        ConfigGenerator.GenerateDefaultConfigStructures();")
$text = $text.Replace('ConfigurationApi.StageReturnToOwnConfiguration(); Start();', "ConfigurationApi.StageReturnToOwnConfiguration();`r`n        Start();").Replace('cfg.Bind("Debug", "Enabled", false, "Local"); cfg.Save();', "cfg.Bind(`"Debug`", `"Enabled`", false, `"Local`");`r`n        cfg.Save();")
Write-Source $name $text
$name = 'Config/PublicApi/ConfigurationApi.cs'
$text = Read-Source $name
$text = $text.Replace('return new ConfigurationSnapshot { Revision = loaded.Revision, Source = loaded.Source, ContextId = loaded.ContextId, Values = new Dictionary<string, object>(loaded.Values, StringComparer.Ordinal) };', @'
return new ConfigurationSnapshot
                {
                    Revision = loaded.Revision,
                    Source = loaded.Source,
                    ContextId = loaded.ContextId,
                    Values = new Dictionary<string, object>(loaded.Values, StringComparer.Ordinal)
                };
'@).Replace('return new ConfigurationSnapshot { Revision = ConfigurationFileTransaction.Revision(files), Source = source, ContextId = contextId, Values = documents.Read(files) };', @'
return new ConfigurationSnapshot
            {
                Revision = ConfigurationFileTransaction.Revision(files),
                Source = source,
                ContextId = contextId,
                Values = documents.Read(files)
            };
'@)
$text = $text.Replace('            lock (Gate) { RequireReady(); return documents.Options; }', "            lock (Gate)`r`n            {`r`n                RequireReady();`r`n                return documents.Options;`r`n            }")
$text = $text.Replace('            lock (Gate) { RequireReady(); return Snapshot(PersonalFiles(transaction.ReadOwn()), "OwnFiles"); }', "            lock (Gate)`r`n            {`r`n                RequireReady();`r`n                return Snapshot(PersonalFiles(transaction.ReadOwn()), `"OwnFiles`");`r`n            }")
$text = $text.Replace('            lock (Gate) { RequireReady(); return documents.Validate(values); }', "            lock (Gate)`r`n            {`r`n                RequireReady();`r`n                return documents.Validate(values);`r`n            }")
$text = $text.Replace('    public static class ConfigurationApi', '    /// <summary>Thread-safe file configuration facade; network methods additionally require the game thread.</summary>' + "`r`n" + '    public static class ConfigurationApi')
$text = $text.Replace('        public static event Action Changed;', '        /// <summary>Raised on the calling thread after releasing the API lock. Consumers dispatch their own UI work.</summary>' + "`r`n" + '        public static event Action Changed;')
Write-Source $name $text
$name = 'Config/PublicApi/ConfigurationContracts.cs'
$text = Read-Source $name
$start = $text.IndexOf('    public sealed class ConfigurationSnapshot')
$end = $text.IndexOf('    public sealed class ConfigurationProblem', $start)
$text = $text.Substring(0,$start) + @'
    /// <summary>A detached file snapshot. Editing Values does not stage or apply any setting.</summary>
    public sealed class ConfigurationSnapshot
    {
        /// <summary>File-content fingerprint; use the personal snapshot revision when staging.</summary>
        public string Revision { get; internal set; }
        /// <summary>Opaque consumer context, or an empty string for personal configuration.</summary>
        public string ContextId { get; internal set; }
        /// <summary>OwnFiles, LoadedAtStartup or PendingRestart; not a claim of live native state.</summary>
        public string Source { get; internal set; }
        /// <summary>Complete catalog values using Boolean, String, Int64 and Double scalars.</summary>
        public Dictionary<string, object> Values { get; internal set; }
    }

'@ + "`r`n" + $text.Substring($end)
Write-Source $name $text
$name = 'Config/PublicApi/ConfigurationDocuments.cs'
$text = Read-Source $name
$text = $text.Replace('            cellsByFile = orderedCells.GroupBy(x => x.Option.File, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);', "            cellsByFile = orderedCells.GroupBy(x => x.Option.File, StringComparer.Ordinal)`r`n                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);")
$text = $text.Replace('            validators = orderedCells.Where(x => x.Path.Length == 2).GroupBy(x => x.Option.File + "\0" + x.Path[1], StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);', "            validators = orderedCells.Where(x => x.Path.Length == 2)`r`n                .GroupBy(x => x.Option.File + `"\0`" + x.Path[1], StringComparer.Ordinal)`r`n                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);")
Write-Source $name $text
