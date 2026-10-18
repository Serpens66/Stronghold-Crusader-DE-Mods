[CmdletBinding()]
param([string]$Workspace, [string]$ModDirectory, [switch]$IndependentRuntime, [switch]$ConsumerOnly)
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
if ($ConsumerOnly -and -not $modRoot) { throw 'ConsumerOnly requires an explicit mod scope.' }
if ($IndependentRuntime -and -not $modRoot) { throw 'IndependentRuntime requires an explicit mod scope.' }
$scopedRoots = @($modRoot) + $(if (-not $IndependentRuntime -and -not $ConsumerOnly) { @($apiRoot) } else { @() })
$discoveryRoots = if ($modRoot) { $scopedRoots } else { @($Workspace) }
$projects = @(Get-ChildItem -LiteralPath $discoveryRoots -Recurse -File -Filter '*.csproj' | Where-Object {
     ('\' + $_.FullName.Substring($Workspace.Length).TrimStart('\')) -notmatch '[\\/](shcde-script-extender|bin|obj|BepInEx|\.tools|\.inspect|_inspect|\.native-analysis|\.work|\.release-output|before)[\\/]'
})
# Historical analysis directories contain intentionally incomplete prototypes.
# The maintained inventory explicitly includes the active analysis-hosted regressions.
$activeProjects = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'source-check-projects.txt') | Where-Object { $_ -and -not $_.StartsWith('#') }
if ($modRoot) {
    $activeProjects = @($activeProjects | Where-Object {
        $candidate = [IO.Path]::GetFullPath((Join-Path $Workspace $_))
        $candidate.StartsWith($modRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        (-not $IndependentRuntime -and -not $ConsumerOnly -and $candidate.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase))
    })
}
$projects = @($projects) + @($activeProjects | ForEach-Object { Get-Item -LiteralPath (Join-Path $Workspace $_) })
$projects = @($projects | Sort-Object FullName -Unique)
if ($ModDirectory) {

    $projects = @($projects | Where-Object {
        $_.FullName.StartsWith($modRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        ($_.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
          ('\' + $_.FullName.Substring($Workspace.Length).TrimStart('\')) -notmatch '[\\/](examples|tests)[\\/]')
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
    if ($IndependentRuntime) {
        foreach ($project in $projects) {
            [xml]$independentXml = [IO.File]::ReadAllText($project.FullName)
            foreach ($entry in $independentXml.SelectNodes('//*[local-name()="Reference" or local-name()="ProjectReference" or local-name()="Compile"]')) {
                if ($entry.OuterXml -match '(?i)APIShared') { throw 'IndependentRuntime cannot exclude a declared APIShared dependency.' }
            }
        }
    }
    Write-Host "[Preflight] Mod scope: $ModDirectory; IndependentRuntime=$IndependentRuntime; ConsumerOnly=$ConsumerOnly; explicit project/source dependencies included."
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
# Source ownership is checked from project inputs above; folder depth is not a
# runtime contract and must not reject an otherwise valid source arrangement.
$legacy = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'legacy-shared-paths.json') | ConvertFrom-Json
Write-Host '[Preflight] Checking active references and CRLF...'
$legacyPattern = '(?<![A-Za-z0-9_])(?:' + (($legacy.PSObject.Properties.Name | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')'
$legacyRegex = [regex]::new($legacyPattern, [Text.RegularExpressions.RegexOptions]::Compiled)
$activeDirectories = if ($modRoot) { $scopedRoots + @($projects | Where-Object FullName -NotMatch '[\\/]_inspect[\\/]' | ForEach-Object { $_.Directory.FullName }) } else { @($apiRoot, $sharedRoot) + @($projects | Where-Object FullName -NotMatch '[\\/]_inspect[\\/]' | ForEach-Object { $_.Directory.FullName }) }
$activeFiles = @(foreach ($directory in $activeDirectories | Sort-Object -Unique) {
    Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object {
         ('\' + $_.FullName.Substring($Workspace.Length).TrimStart('\')) -notmatch '[\\/](bin|obj|BepInEx|_inspect|\.inspect|\.work|\.release-output)[\\/]' -and
        # Consumer checks cover the dependency's runtime sources and project/manifest,
        # not its independent tests, documentation, examples or release tooling.
        (-not $modRoot -or -not $_.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
         ($_.FullName.StartsWith((Join-Path $apiRoot 'src') + '\', [StringComparison]::OrdinalIgnoreCase) -and $_.Extension -eq '.cs') -or
         $projects.FullName -contains $_.FullName -or $_.FullName -eq (Join-Path $apiRoot 'info.json')) -and
        $_.Extension -match '^\.(cs|csproj|ps1|bat|cmd|md|json|yml|props|targets)$' -and
        $_.Name -ne 'legacy-shared-paths.json' -and $_.Name -notmatch '^\d.*\.(compatibility|release-hooks)\.json$'
    }
}) + $(if ($modRoot) { $linkedSources } else { @() }) + @(Get-Item -LiteralPath (Join-Path $Workspace 'AGENTS.md'))
# Topic-specific agent instructions are split out of the root AGENTS.md and must follow the same rules.
$activeFiles += @(Get-ChildItem -LiteralPath (Join-Path $Workspace 'AGENTS') -File -Filter '*.md')
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
        foreach ($virtualLink in @($referenceDocument.SelectNodes('//@Link'))) {
            [void]$virtualLink.OwnerElement.RemoveAttributeNode($virtualLink)
        }
        $withoutProvenance = $referenceDocument.OuterXml
    }
    $normalized = [regex]::Replace($withoutProvenance.Replace('\', '/'), '/+', '/')
    $retiredPath = $legacyRegex.Match($normalized)
    if ($retiredPath.Success) { throw "Active reference to retired Shared path '$($retiredPath.Value)': $($file.FullName)" }
}
$apiOwnershipFiles = if ($modRoot) {
    @($activeFiles | Where-Object { $_.Extension -eq '.cs' -and $_.FullName.StartsWith($apiRoot + '\', [StringComparison]::OrdinalIgnoreCase) })
} else {
    @(Get-ChildItem -LiteralPath $apiRoot -Recurse -File -Filter '*.cs' | Where-Object FullName -NotMatch '[\\/](bin|obj|BepInEx)[\\/]')
}
foreach ($file in $apiOwnershipFiles) {
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
foreach ($project in $projects | Where-Object { $_.FullName -match '[\\/]Testmods[\\/]' -and  ('\' + $_.FullName.Substring($Workspace.Length).TrimStart('\')) -notmatch '[\\/](tests|[^\\/]*\.Tests)[\\/]|\.PolicyTests\.csproj$' }) {
    $plugin = @(Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.cs' | Where-Object FullName -NotMatch '[\\/](bin|obj|tests|BepInEx)[\\/]' |
        Select-String -Pattern '\bclass\s+\w+\s*:\s*(?:BepInEx\.)?BaseUnityPlugin\b' | Select-Object -First 1)
    if ($plugin) {
        Assert-SERuntimeModPreflight ([pscustomobject]@{Name=$project.BaseName; Project=$project.FullName.Substring($Workspace.Length+1); Plugin=$plugin[0].Path}) $Workspace
    }
}
Write-Host '[Preflight] Checking XAML content roots...'
foreach ($project in $projects | Where-Object {  ('\' + $_.FullName.Substring($Workspace.Length).TrimStart('\')) -notmatch '[\\/](_inspect|tests|[^\\/]*\.Tests|Tests)[\\/]' }) {
    foreach ($file in Get-ChildItem -LiteralPath $project.Directory.FullName -Recurse -File -Filter '*.xaml' | Where-Object FullName -NotMatch '[\\/](bin|obj|BepInEx)[\\/]') {
        [xml]$patch = [IO.File]::ReadAllText($file.FullName)
        foreach ($content in $patch.SelectNodes('//*[local-name()="Content"]')) {
            if (@($content.ChildNodes | Where-Object NodeType -eq ([Xml.XmlNodeType]::Element)).Count -ne 1) {
                throw "XAML Content must have exactly one element: $($file.FullName)"
            }
        }
    }
}
& (Join-Path $PSScriptRoot 'Test-PermanentNativeRuntimePatches.ps1') -ModDirectory $ModDirectory
if (-not $?) { throw 'Permanent-hook verification failed.' }
Write-Output "PASS: shared ownership boundaries; $($projects.Count) projects, $links valid explicit links; runtime JSON/lifecycle/scheduling, XAML and permanent hooks."

& (Join-Path $PSScriptRoot 'Test-DependencyMetadata.ps1') -Workspace $Workspace -ProjectFiles $(if ($modRoot) { $projects.FullName } else { @() })
Write-Host '[Preflight] Checking centralized UnitAccess contract...'
# An unchanged API DLL reference does not make its full implementation a consumer
# build prerequisite. ConsumerOnly still checks every explicit source link and all
# consumer runtime gates. Unit behavior is relevant only when runtime sources use it.
$needsUnitChecks = -not $ConsumerOnly
if ($ConsumerOnly) {
    foreach ($project in $projects | Where-Object { $_.FullName -notmatch '[\\/](tests|_inspect)[\\/]' }) {
        [xml]$runtimeXml = [IO.File]::ReadAllText($project.FullName)
        foreach ($entry in $runtimeXml.SelectNodes('//*[local-name()="Compile"][@Include]')) {
            $include = [string]$entry.Include
            if ($include -match '\$\(|[*?]') { $needsUnitChecks = $true; continue }
            $file = [IO.Path]::GetFullPath((Join-Path $project.Directory.FullName $include))
            if ([IO.File]::ReadAllText($file) -match '\b(UnitAccess|GameUnit|GameUnitManagerAPI|eChimps|UnitId)\b') { $needsUnitChecks = $true }
        }
    }
}
if ($needsUnitChecks) {
    & (Join-Path $PSScriptRoot 'Test-UnitAccess.ps1') -ModDirectory $ModDirectory -SkipPermanentHooks
    if (-not $?) { throw 'UnitAccess contract preflight failed.' }
} else { Write-Host '[Preflight] No unit access in the scoped runtime sources; unit behavior suite not applicable.' }
$preflightWatch.Stop()
Write-Host ('[Preflight] Completed in {0:N1}s.' -f $preflightWatch.Elapsed.TotalSeconds)

& (Join-Path $PSScriptRoot 'Test-IngameTooltips.ps1') -Workspace $Workspace -ModDirectories $(if ($modRoot) { @($modRoot) } else { @() })
