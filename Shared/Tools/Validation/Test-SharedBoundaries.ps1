[CmdletBinding()]
param([string]$Workspace, [string]$ModDirectory)
$ErrorActionPreference = 'Stop'
$preflightWatch = [Diagnostics.Stopwatch]::StartNew()
Write-Host '[Preflight] Discovering active projects and explicit source links...'
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
$Workspace = [IO.Path]::GetFullPath($Workspace)
$apiRoot = Join-Path $Workspace 'APIShared'
$sharedRoot = Join-Path $Workspace 'Shared'
$modRoot = $null
if ($ModDirectory) {
    $modRoot = (Resolve-Path -LiteralPath (Join-Path $Workspace $ModDirectory)).Path
    if (-not $modRoot.StartsWith($Workspace + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $modRoot -PathType Container)) {
        throw 'ModDirectory must be a directory inside the workspace.'
    }
}
$discoveryRoots = if ($modRoot) { @($modRoot, $apiRoot) } else { @($Workspace) }
$projects = @(Get-ChildItem -LiteralPath $discoveryRoots -Recurse -File -Filter '*.csproj' | Where-Object {
    $_.FullName -notmatch '[\\/](shcde-script-extender|bin|obj|BepInEx|\.tools|\.inspect|_inspect|\.native-analysis|\.release-output|before)[\\/]'
})
# Historical analysis directories contain intentionally incomplete prototypes.
# The maintained inventory explicitly includes the active analysis-hosted regressions.
$activeProjects = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'source-check-projects.txt') | Where-Object { $_ -and -not $_.StartsWith('#') }
if ($modRoot) {
    $activeProjects = @($activeProjects | Where-Object {
        $candidate = [IO.Path]::GetFullPath((Join-Path $Workspace $_))
        $candidate.StartsWith($modRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase)
    })
}
$projects = @($projects) + @($activeProjects | ForEach-Object { Get-Item -LiteralPath (Join-Path $Workspace $_) })
$projects = @($projects | Sort-Object FullName -Unique)
if ($ModDirectory) {

    $projects = @($projects | Where-Object {
        $_.FullName.StartsWith($modRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        ($_.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
         $_.FullName -notmatch '[\\/](examples|tests)[\\/]')
    })
    # Include real project dependencies, never every unrelated mod/testmod.
    for ($projectIndex = 0; $projectIndex -lt $projects.Count; $projectIndex++) {
        [xml]$referenceXml = [IO.File]::ReadAllText($projects[$projectIndex].FullName)
        foreach ($reference in $referenceXml.SelectNodes('//*[local-name()="ProjectReference"][@Include]')) {
            if ([string]$reference.Include -match '\$\(|[*?]') { continue }
            $dependency = Get-Item -LiteralPath ([IO.Path]::GetFullPath((Join-Path $projects[$projectIndex].Directory.FullName ([string]$reference.Include))))
            if ($projects.FullName -notcontains $dependency.FullName) { $projects += $dependency }
        }
    }
    Write-Host "[Preflight] Mod scope: $ModDirectory, APIShared and explicit project/source dependencies."
}
$linkedSources = @()
$links = 0
Write-Host "[Preflight] Checking $($projects.Count) projects and source ownership..."
foreach ($project in $projects) {
    [xml]$xml = [IO.File]::ReadAllText($project.FullName)
    $testProject = $project.FullName -match '[\\/](_inspect|tests|[^\\/]*\.Tests|Tests)[\\/]|\.PolicyTests\.csproj$'
    foreach ($entry in $xml.SelectNodes('//*[local-name()="Compile" or local-name()="ProjectReference"][@Include]')) {
        $include = [string]$entry.Include
        if ($include -match '\$\(|[*?]') { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $project.Directory.FullName $include))
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing source/project link: $($project.FullName) -> $target" }
        $links++
        if ($entry.LocalName -eq 'Compile') { $linkedSources += Get-Item -LiteralPath $target }
        if ($project.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
            -not $target.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "APIShared must not compile external workspace sources: $target"
        }
        if (-not $testProject -and -not $project.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
            $target.StartsWith((Join-Path $apiRoot 'src') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Production consumer compiles APIShared source instead of using its public assembly: $target"
        }
    }
}
foreach ($file in Get-ChildItem -LiteralPath $sharedRoot -File -Filter '*.cs') { throw "Unsorted root Shared runtime source: $($file.FullName)" }
$legacy = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'legacy-shared-paths.json') | ConvertFrom-Json
Write-Host '[Preflight] Checking active references and CRLF...'
$legacyPattern = '(?<![A-Za-z0-9_])(?:' + (($legacy.PSObject.Properties.Name | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')'
$legacyRegex = [regex]::new($legacyPattern, [Text.RegularExpressions.RegexOptions]::Compiled)
$activeDirectories = if ($modRoot) { @($modRoot, $apiRoot) + @($projects | Where-Object FullName -NotMatch '[\\/]_inspect[\\/]' | ForEach-Object { $_.Directory.FullName }) } else { @($apiRoot, $sharedRoot) + @($projects | Where-Object FullName -NotMatch '[\\/]_inspect[\\/]' | ForEach-Object { $_.Directory.FullName }) }
$activeFiles = @(foreach ($directory in $activeDirectories | Sort-Object -Unique) {
    Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|BepInEx|_inspect|\.inspect)[\\/]' -and
        $_.Extension -match '^\.(cs|csproj|ps1|bat|cmd|md|json|yml|props|targets)$' -and
        $_.Name -ne 'legacy-shared-paths.json' -and $_.Name -notmatch '^\d.*\.(compatibility|release-hooks)\.json$'
    }
}) + $(if ($modRoot) { $linkedSources } else { @() }) + @(Get-Item -LiteralPath (Join-Path $Workspace 'AGENTS.md'))
foreach ($file in $activeFiles | Sort-Object FullName -Unique) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Text must use CRLF: $($file.FullName)" }
    $withoutProvenance = [regex]::Replace($text, '(?m)^(?:// Initial provenance:|\s*Historical origin:)[^\r\n]*', '')
    if ($file.Extension -eq '.csproj') {
        # Link is a virtual IDE display name; Include is the actual source path.
        [xml]$referenceDocument = $withoutProvenance
        foreach ($virtualLink in @($referenceDocument.SelectNodes('//*[local-name()="Link"]'))) {
            [void]$virtualLink.ParentNode.RemoveChild($virtualLink)
        }
        $withoutProvenance = $referenceDocument.OuterXml
    }
    $normalized = [regex]::Replace($withoutProvenance.Replace('\', '/'), '/+', '/')
    $retiredPath = $legacyRegex.Match($normalized)
    if ($retiredPath.Success) { throw "Active reference to retired Shared path '$($retiredPath.Value)': $($file.FullName)" }
}
foreach ($file in Get-ChildItem -LiteralPath $apiRoot -Recurse -File -Filter '*.cs' | Where-Object FullName -NotMatch '[\\/](bin|obj|BepInEx)[\\/]') {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -cmatch '\busing\s+Shared\s*;|\bnamespace\s+Shared\b|(?<!API)\bShared\.(?!MissionEvents\.")') {
        throw "API implementation still depends on workspace Shared type identities: $($file.FullName)"
    }
}
. (Join-Path $Workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
Write-Host '[Preflight] Checking JSON, lifecycle and persistent runtime call paths...'
$inventory = Get-Content -Raw -LiteralPath (Join-Path $Workspace 'Shared\Tools\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
foreach ($mod in $inventory) {
    if ($modRoot -and $projects.FullName -notcontains [IO.Path]::GetFullPath((Join-Path $Workspace ([string]$mod.Project)))) { continue }
    Assert-SERuntimeModPreflight $mod $Workspace
}
foreach ($project in $projects | Where-Object { $_.FullName -match '[\\/]Testmods[\\/]' -and $_.FullName -notmatch '[\\/](tests|[^\\/]*\.Tests)[\\/]|\.PolicyTests\.csproj$' }) {
    $plugin = @(Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.cs' | Where-Object FullName -NotMatch '[\\/](bin|obj|tests|BepInEx)[\\/]' |
        Select-String -Pattern '\bclass\s+\w+\s*:\s*(?:BepInEx\.)?BaseUnityPlugin\b' | Select-Object -First 1)
    if ($plugin) {
        Assert-SERuntimeModPreflight ([pscustomobject]@{Name=$project.BaseName; Project=$project.FullName.Substring($Workspace.Length+1); Plugin=$plugin[0].Path}) $Workspace
    }
}
Write-Host '[Preflight] Checking XAML content roots...'
foreach ($project in $projects | Where-Object { $_.FullName -notmatch '[\\/](_inspect|tests|[^\\/]*\.Tests|Tests)[\\/]' }) {
    foreach ($file in Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.xaml' | Where-Object FullName -NotMatch '[\\/](bin|obj|BepInEx)[\\/]') {
        [xml]$patch = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $patch.SelectNodes('//*[local-name()="Content"]')) {
            if (@($content.ChildNodes | Where-Object NodeType -eq ([Xml.XmlNodeType]::Element)).Count -ne 1) {
                throw "XAML Content must have exactly one element: $($file.FullName)"
            }
        }
    }
}
& (Join-Path $PSScriptRoot 'Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Permanent-hook verification failed.' }
Write-Output "PASS: shared ownership boundaries; $($projects.Count) projects, $links valid explicit links; runtime JSON/lifecycle/scheduling, XAML and permanent hooks."

& (Join-Path $PSScriptRoot 'Test-DependencyMetadata.ps1') -Workspace $Workspace -ProjectFiles $(if ($modRoot) { $projects.FullName } else { @() })
$preflightWatch.Stop()
Write-Host ('[Preflight] Completed in {0:N1}s.' -f $preflightWatch.Elapsed.TotalSeconds)

& (Join-Path $PSScriptRoot 'Test-IngameTooltips.ps1') -Workspace $Workspace -ModDirectories $(if ($modRoot) { @($modRoot, $apiRoot) } else { @() })
