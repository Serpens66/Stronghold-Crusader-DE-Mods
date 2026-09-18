param([switch]$NormalizeChangedText)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $workspace
. 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1'
$projects = @('APIShared\APIShared.csproj') + @(Get-Content '_inspect\MissionLifecycleProjects.json' -Raw | ConvertFrom-Json)
$inventory = Get-Content 'Shared\ScriptExtenderUpdate\mods.json' -Raw | ConvertFrom-Json
$release = Get-Content 'Shared\Release\release-projects.json' -Raw | ConvertFrom-Json
function Get-ApiSharedDependencyVersion([string]$PluginText) {
    $match = [regex]::Match($PluginText, 'BepInDependency\((?:"APIShared_Serp"|ApiSharedGuid),\s*"(?<version>[^"]+)"\)')
    if ($match.Success) { return $match.Groups['version'].Value }
    $constant = [regex]::Match($PluginText, 'const\s+string\s+ApiSharedVersion\s*=\s*"(?<version>[^"]+)"')
    if ($constant.Success -and $PluginText -match 'BepInDependency\((?:"APIShared_Serp"|ApiSharedGuid),\s*ApiSharedVersion\)') {
        return $constant.Groups['version'].Value
    }
    return $null
}
$sourceSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($relative in $projects) {
    $path = Join-Path $workspace $relative
    [xml]$project = [IO.File]::ReadAllText($path)
    $sources = @($project.SelectNodes('//*[local-name()="Compile"]') | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $path) $_.GetAttribute('Include')))
    })
    foreach ($source in $sources) { if (-not (Test-Path -LiteralPath $source)) { throw "Missing source: $source" }; [void]$sourceSet.Add($source) }
    $mod = @($inventory | Where-Object Project -eq $relative)
    if ($relative -eq 'ExtremePowers\ExtremePowers.API.csproj') {
        if (-not $project.SelectSingleNode('//*[local-name()="Reference" and @Include="APIShared"]')) { throw "Companion missing API reference" }
        continue
    }
    if ($mod.Count -ne 1) { throw "Inventory mismatch: $relative" }
    Assert-SERuntimeModPreflight $mod[0] $workspace
    if ($mod[0].Name -ne 'APIShared') {
        if (-not $project.SelectSingleNode('//*[local-name()="Reference" and @Include="APIShared"]')) { throw "Missing API reference: $relative" }
        $pluginText = [IO.File]::ReadAllText((Join-Path $workspace $mod[0].Plugin))
        $apiDependencyVersion = Get-ApiSharedDependencyVersion $pluginText
        if (-not $apiDependencyVersion) { throw "Missing hard API dependency: $relative" }
        if ($mod[0].DependsOn -notcontains 'APIShared' -or $mod[0].BuildOrder -le 10) { throw "Invalid API build order: $relative" }
        if ($release.ApiShared.Consumers.($mod[0].Name) -ne $apiDependencyVersion) { throw "API release dependency does not match plugin metadata: $relative" }
        $installed = Test-Path -LiteralPath ('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\' + $mod[0].Install)
        if (-not $installed -and [IO.File]::ReadAllText((Join-Path $workspace $mod[0].BuildDriver)) -notmatch '/noinstall') { throw "Missing /noinstall: $relative" }
    }
    Write-Output "RUNTIME PASS: $relative"
}
foreach ($path in $sourceSet) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility') { throw "Forbidden JSON: $path" }
    if ($path -notlike '*\APIShared\src\MissionLifecycleCapability.cs' -and $text -match 'MapLoaderR3EventHooks\.On(StartMap|LoadMap|LoadSave|UnloadMap)\.Observable') { throw "Parallel lifecycle subscription: $path" }
    if ($text -match 'IEditorMapLifecycleCapability|TryGetEditorMapLifecycle|onEditorEnded|EnsureEditorMapState|BeginEditorMapIfApplicable|implicit editor map-size probe|if \((true|false)\)') { throw "Obsolete migration residue: $path" }
}
$preserved = @('.github/workflows/release-status.yml', 'Shared/Release/Release.Common.ps1', 'Shared/Release/Test-ReleaseStatus.ps1')
$changed = @('AGENTS.md') + @(& git diff --name-only --diff-filter=ACMRT) + @(& git ls-files --others --exclude-standard)
$targets = @($changed | Where-Object {
    $_ -notin $preserved -and $_ -match '\.(cs|csproj|ps1|py|md|json|bat)$' -and
    $_ -notmatch '(^|/)(bin|obj|BepInEx)/' -and (Test-Path -LiteralPath $_ -PathType Leaf)
} | Sort-Object -Unique)
foreach ($relative in $targets) {
    $path = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (-not $path.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw $path }
    $text = [IO.File]::ReadAllText($path)
    if ($NormalizeChangedText) {
        $expected = [regex]::Replace($text, '\r?\n', "`r`n")
        if (-not [string]::Equals($text, $expected, [StringComparison]::Ordinal)) {
            [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
            $text = [IO.File]::ReadAllText($path)
            if (-not [string]::Equals($text, $expected, [StringComparison]::Ordinal)) { throw "Readback mismatch: $relative" }
        }
    }
    $bareLf = [regex]::Matches($text, '(?<!\r)\n').Count
    if ($bareLf) { throw "Bare LF: $relative" }
    Write-Output ("TEXT {0}: CRLF={1}, bareLF={2}, first={3}" -f $relative, [regex]::Matches($text, '\r\n').Count, $bareLf, ($text -split "`r`n")[0])
}
Write-Output "PASS: mission lifecycle preflight; $($projects.Count) projects, $($sourceSet.Count) distinct sources, $($targets.Count) changed text files."
