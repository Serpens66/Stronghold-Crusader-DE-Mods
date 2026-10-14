param(
    [string]$SerpsModsMap = 'E:\ProgrammeE\Steam\steamapps\workshop\content\3024040\3788821961\SerpsMods.map',
    [string]$FixesMap = 'E:\ProgrammeE\Steam\steamapps\workshop\content\3024040\3791770511\fixes.map',
    [string]$AssemblyCSharp = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-ManagerCompatibility([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "SCDE Mod Manager compatibility test failed: $Message" }
}

function Open-AppendedZipArchive([string]$Path) {
    Add-Type -AssemblyName System.IO.Compression
    $bytes = [IO.File]::ReadAllBytes($Path)
    $minimum = [Math]::Max(0, $bytes.Length - 65557)
    $end = -1
    for ($index = $bytes.Length - 22; $index -ge $minimum; $index--) {
        if ($bytes[$index] -eq 0x50 -and $bytes[$index + 1] -eq 0x4b -and
            $bytes[$index + 2] -eq 0x05 -and $bytes[$index + 3] -eq 0x06) {
            $commentLength = [BitConverter]::ToUInt16($bytes, $index + 20)
            if ($index + 22 + $commentLength -eq $bytes.Length) { $end = $index; break }
        }
    }
    if ($end -lt 0) { throw "Appended ZIP end record was not found: $Path" }
    $centralSize = [BitConverter]::ToUInt32($bytes, $end + 12)
    $centralOffset = [BitConverter]::ToUInt32($bytes, $end + 16)
    $archiveOffset = [long]$end - [long]$centralSize - [long]$centralOffset
    if ($archiveOffset -lt 0) { throw "Invalid appended ZIP offset: $Path" }
    $stream = [IO.MemoryStream]::new($bytes, [int]$archiveOffset, $bytes.Length - [int]$archiveOffset, $false)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $false)
    return [PSCustomObject]@{ Archive = $archive; Stream = $stream; Offset = $archiveOffset }
}

function Read-ZipJson($Entry) {
    $reader = [IO.StreamReader]::new($Entry.Open(), [Text.UTF8Encoding]::new($false), $true)
    try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}

function Get-NestedScriptExtenderMinimum($Info) {
    $versions = [System.Collections.Generic.List[version]]::new()
    if ($null -ne $Info.PSObject.Properties['MinimumScriptExtenderVersion'] -and
        -not [string]::IsNullOrWhiteSpace([string]$Info.MinimumScriptExtenderVersion)) {
        $versions.Add([version]([string]$Info.MinimumScriptExtenderVersion))
    }
    if ($null -ne $Info.PSObject.Properties['Dependencies'] -and $null -ne $Info.Dependencies) {
        foreach ($dependency in @($Info.Dependencies)) {
            if ($null -ne $dependency -and $null -ne $dependency.PSObject.Properties['GUID'] -and
                [string]$dependency.GUID -ieq '000shcdese' -and
                $null -ne $dependency.PSObject.Properties['MinimumVersion'] -and
                -not [string]::IsNullOrWhiteSpace([string]$dependency.MinimumVersion)) {
                $versions.Add([version]([string]$dependency.MinimumVersion))
            }
        }
    }
    if ($versions.Count -eq 0) { return $null }
    return [string](@($versions | Sort-Object -Descending)[0])
}

function Test-WorkshopMap([string]$Path, [string]$ExpectedVersion, [int]$ExpectedEntryCount, [string]$ExpectedMinimum) {
    Assert-ManagerCompatibility (Test-Path -LiteralPath $Path -PathType Leaf) "Workshop artifact is missing: $Path"
    $opened = Open-AppendedZipArchive -Path $Path
    try {
        $entries = @($opened.Archive.Entries)
        $files = @($entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
        Assert-ManagerCompatibility ($entries.Count -eq $ExpectedEntryCount) "$Path contains $($entries.Count) entries instead of $ExpectedEntryCount."
        $paths = @($files | ForEach-Object { $_.FullName.Replace('\', '/').TrimStart('/') })
        Assert-ManagerCompatibility (@($paths | Group-Object { $_.ToLowerInvariant() } | Where-Object Count -gt 1).Count -eq 0) "$Path contains duplicate archive paths."
        $violations = [System.Collections.Generic.List[string]]::new()
        foreach ($entryPath in $paths) {
            if ($entryPath -notmatch '^(?i)BepInEx/') { continue }
            $parts = $entryPath.Split('/')
            $pluginPath = $parts.Count -ge 4 -and $parts[1] -ieq 'plugins' -and
                $parts[2] -notin @('000shcdese', 'uuimgui', 'scdemultiplayercompatibility')
            $configPath = $parts.Count -ge 3 -and $parts[1] -ieq 'config' -and
                $entryPath -match '(?i)\.(cfg|toml|json|ini|xml|yaml|yml|txt)$'
            if (-not $pluginPath -and -not $configPath) { $violations.Add($entryPath) }
        }
        Assert-ManagerCompatibility ($violations.Count -eq 0) "$Path contains unsupported deployment paths: $($violations -join ', ')"
        $rootInfoEntry = @($files | Where-Object { $_.FullName.Replace('\', '/').TrimStart('/') -ieq 'info.json' })
        Assert-ManagerCompatibility ($rootInfoEntry.Count -eq 1) "$Path must contain exactly one root info.json."
        $rootInfo = Read-ZipJson $rootInfoEntry[0]
        Assert-ManagerCompatibility ([string]$rootInfo.Version -ceq $ExpectedVersion) "$Path has unexpected package version $([string]$rootInfo.Version)."
        $minimums = [System.Collections.Generic.List[version]]::new()
        foreach ($entry in @($files | Where-Object { $_.Name -ieq 'info.json' })) {
            $minimum = Get-NestedScriptExtenderMinimum -Info (Read-ZipJson $entry)
            if (-not [string]::IsNullOrWhiteSpace($minimum)) { $minimums.Add([version]$minimum) }
        }
        $effectiveMinimum = if ($minimums.Count -gt 0) { [string](@($minimums | Sort-Object -Descending)[0]) } else { '' }
        Assert-ManagerCompatibility ($effectiveMinimum -ceq $ExpectedMinimum) "$Path resolves nested Script Extender minimum $effectiveMinimum instead of $ExpectedMinimum."
        return [PSCustomObject]@{ Path = $Path; Version = [string]$rootInfo.Version; Entries = $entries.Count; ScriptExtenderMinimum = $effectiveMinimum; Offset = $opened.Offset }
    } finally {
        $opened.Archive.Dispose()
        $opened.Stream.Dispose()
    }
}

Assert-ManagerCompatibility (Test-Path -LiteralPath $AssemblyCSharp -PathType Leaf) "Managed game assembly is missing: $AssemblyCSharp"
$managedHash = (Get-FileHash -LiteralPath $AssemblyCSharp -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-ManagerCompatibility ($managedHash -ceq 'bc8b6a395f01d48557db413600c8dd8d1fdfd3abdf97bfbbb68a3c56b04fd789') "Assembly-CSharp.dll has unknown hash $managedHash."
$serps = Test-WorkshopMap -Path $SerpsModsMap -ExpectedVersion '1.0.14' -ExpectedEntryCount 840 -ExpectedMinimum '2.7.1'
$fixes = Test-WorkshopMap -Path $FixesMap -ExpectedVersion '1.18.1' -ExpectedEntryCount 16 -ExpectedMinimum '2.5.0'
$serps, $fixes | Format-Table Version,Entries,ScriptExtenderMinimum,Offset,Path -AutoSize
Write-Host 'SCDE Mod Manager Workshop compatibility tests succeeded.' -ForegroundColor Green
