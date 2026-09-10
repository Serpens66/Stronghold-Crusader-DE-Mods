[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OldVersion,
    [Parameter(Mandatory)][string]$NewVersion,
    [Parameter(Mandatory)][string]$OldTag,
    [Parameter(Mandatory)][string]$NewTag,
    [Parameter(Mandatory)][string]$TargetCommit,
    [ValidateSet('Existing','Patch','Explicit')][string]$VersionMode = 'Existing',
    [string]$VersionsFile,
    [string]$CompatibilityPlanFile,
    [string]$Changelog = "Adjusted to Script Extender $NewVersion.",
    [string]$ExtenderDir,
    [switch]$SkipExtenderBuild,
    [switch]$SkipBaseline,
    [switch]$PrepareOnly,
    [switch]$Resume
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$inventoryPath = Join-Path $scriptRoot 'mods.json'
$extenderRoot = Join-Path $workspace 'shcde-script-extender'
$gameRoot = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$installedExtender = Join-Path $gameRoot 'BepInEx\plugins\000shcdese'
$runKey = "$OldVersion-$NewVersion"
$runRoot = Join-Path $workspace ".inspect\ScriptExtenderUpdates\$runKey"
$statePath = Join-Path $runRoot 'state.json'
. (Join-Path $scriptRoot 'ScriptExtenderUpdate.Common.ps1')

function Write-CrlfFile([string]$Path, [string]$Text) {
    $normalized = [regex]::Replace($Text, '\r?\n', [Environment]::NewLine)
    if (-not $normalized.EndsWith([Environment]::NewLine, [StringComparison]::Ordinal)) { $normalized += [Environment]::NewLine }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $normalized, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($Path) -cne $normalized) { throw "Ordinal write verification failed: $Path" }
}

function Get-DllVersion([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required DLL is missing: $Path" }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $Path).Path)
    [pscustomobject]@{ FileVersion=$info.FileVersion; ProductVersion=$info.ProductVersion; Hash=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
}

function Assert-TargetExtender([string]$Directory) {
    $dll = Join-Path $Directory 'SHCDESE.dll'
    $version = Get-DllVersion $dll
    if ($version.ProductVersion -ne $NewVersion -or $version.FileVersion -notlike "$NewVersion.*") {
        throw "Selected SHCDESE.dll is $($version.ProductVersion) / $($version.FileVersion), expected ${NewVersion}: $dll"
    }
    $version
}

function Save-State([hashtable]$State) {
    Write-CrlfFile $statePath (($State | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
}

function Test-ModActive([object]$Mod) {
    $activeProperty = $Mod.PSObject.Properties['Active']
    return $null -eq $activeProperty -or [bool]$activeProperty.Value
}

function Get-ConstStringMap([string]$Text) {
    $constants = @{}
    foreach ($match in [regex]::Matches($Text, '(?m)\bconst\s+string\s+(?<name>[A-Za-z_]\w*)\s*=\s*"(?<value>[^"]*)"\s*;')) {
        $constants[$match.Groups['name'].Value] = $match.Groups['value'].Value
    }
    $constants
}

function Get-ScriptExtenderDependency([string]$Text) {
    $constants = Get-ConstStringMap $Text
    foreach ($match in [regex]::Matches($Text, '(?m)\[BepInDependency\(\s*(?<guid>[^,\r\n]+?)\s*,\s*(?<version>[^,\)\r\n]+?)\s*\)\]')) {
        $guidToken = $match.Groups['guid'].Value.Trim()
        $guid = if ($guidToken -match '^"(?<value>[^"]+)"$') { $Matches['value'] } elseif ($constants.ContainsKey($guidToken)) { $constants[$guidToken] } else { $null }
        if ($guid -ne '000shcdese') { continue }

        $versionToken = $match.Groups['version'].Value.Trim()
        $version = if ($versionToken -match '^"(?<value>[^"]+)"$') { $Matches['value'] } elseif ($constants.ContainsKey($versionToken)) { $constants[$versionToken] } else { $null }
        return [pscustomobject]@{ Match=$match; VersionGroup=$match.Groups['version']; VersionToken=$versionToken; Version=$version }
    }
    throw 'No resolvable BepInDependency for 000shcdese was found.'
}

function Set-PluginMetadata([string]$Text, [string]$PluginVersion, [string]$MinimumVersion) {
    $updated = [regex]::Replace(
        $Text,
        '(PluginVersion\s*=\s*")[^"]+("\s*;)',
        { param($match) $match.Groups[1].Value + $PluginVersion + $match.Groups[2].Value },
        1)
    $dependency = Get-ScriptExtenderDependency $updated
    if ($dependency.VersionToken -match '^"') {
        $group = $dependency.VersionGroup
        $updated = $updated.Substring(0, $group.Index) + '"' + $MinimumVersion + '"' + $updated.Substring($group.Index + $group.Length)
    }
    else {
        $constantName = [regex]::Escape($dependency.VersionToken)
        $pattern = '(?m)(\bconst\s+string\s+' + $constantName + '\s*=\s*")[^"]+("\s*;)'
        $updated = [regex]::Replace($updated, $pattern, { param($match) $match.Groups[1].Value + $MinimumVersion + $match.Groups[2].Value }, 1)
    }
    $updated
}

Set-Location -LiteralPath $workspace
if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) { throw "Inventory missing: $inventoryPath" }
$mods = @(Get-Content -Raw -LiteralPath $inventoryPath | ConvertFrom-Json)
$activeMods = @($mods | Where-Object { Test-ModActive $_ })
$inactiveMods = @($mods | Where-Object { -not (Test-ModActive $_) })
$duplicateNames = @($mods | Group-Object Name | Where-Object Count -ne 1)
$duplicateGuids = @($mods | Group-Object Guid | Where-Object Count -ne 1)
if ($duplicateNames -or $duplicateGuids) { throw 'Inventory contains duplicate names or GUIDs.' }
foreach ($mod in $mods) {
    foreach ($property in @('Manifest','Package')) { if (-not (Test-Path -LiteralPath (Join-Path $workspace $mod.$property))) { throw "$($mod.Name): missing $property" } }
    if ($mod.Plugin) { foreach ($property in @('Plugin','Project','BuildDriver')) { if (-not (Test-Path -LiteralPath (Join-Path $workspace $mod.$property))) { throw "$($mod.Name): missing $property" } } }
    if (Test-ModActive $mod) {
        $sourceManifest = Get-Content -Raw -LiteralPath (Join-Path $workspace $mod.Manifest) | ConvertFrom-Json
        Assert-SEManifestExtenderRange $sourceManifest $NewVersion $mod.Name
    }
}

$candidateSources = @(& git -C $workspace ls-files -- '*.cs') +
    @(& git -C $workspace ls-files --others --exclude-standard -- '*.cs')
$discoveredPlugins = @($candidateSources | ForEach-Object { $_.Replace('/', '\') } | Where-Object {
    $_ -notmatch '^(shcde-script-extender|_inspect|\.inspect|\.native-analysis)[\\/]' -and
    $_ -notmatch '[\\/](BepInEx[\\/]plugins|bin|obj)[\\/]' -and
    (Test-Path -LiteralPath (Join-Path $workspace $_) -PathType Leaf) -and
    [IO.File]::ReadAllText((Join-Path $workspace $_)).Contains('[BepInPlugin(')
})
$inventoriedPlugins = @($mods | Where-Object Plugin | ForEach-Object { [string]$_.Plugin })
$missingInventory = @($discoveredPlugins | Where-Object { $_ -notin $inventoriedPlugins })
$staleInventory = @($inventoriedPlugins | Where-Object { $_ -notin $discoveredPlugins })
if ($missingInventory -or $staleInventory) {
    throw "Plugin inventory mismatch. Missing: $($missingInventory -join ', '); stale: $($staleInventory -join ', ')"
}

$actualCommit = (& git -C $extenderRoot rev-parse HEAD).Trim()
$tagCommit = (& git -C $extenderRoot rev-list -n 1 $NewTag).Trim()
$treeHash = (& git -C $extenderRoot rev-parse 'HEAD^{tree}').Trim()
if ($actualCommit -ne $TargetCommit -or $tagCommit -ne $TargetCommit) { throw "Extender commit/tag mismatch: HEAD=$actualCommit tag=$tagCommit expected=$TargetCommit" }
& git -C $extenderRoot diff --quiet --ignore-submodules --
if ($LASTEXITCODE -ne 0) { throw 'Tracked Script Extender worktree is not clean.' }
& git -C $extenderRoot diff --cached --quiet --ignore-submodules --
if ($LASTEXITCODE -ne 0) { throw 'Script Extender index is not clean.' }

[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$changedFiles = @(& git -C $extenderRoot diff --name-only "$OldTag..$NewTag")
$categories = Get-SEChangeCategories $changedFiles
$diffReport = [ordered]@{
    oldTag=$OldTag
    newTag=$NewTag
    targetCommit=$TargetCommit
    treeHash=$treeHash
    changedFiles=$changedFiles
    categories=$categories
    activeInventory=@($activeMods | ForEach-Object Name)
    inactiveInventory=@($inactiveMods | ForEach-Object { [ordered]@{ Name=$_.Name; Reason=$_.InactiveReason } })
}
Write-CrlfFile (Join-Path $runRoot 'analysis.json') (($diffReport | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
$commitLog = (& git -C $extenderRoot log --oneline --reverse "$OldTag..$NewTag" | Out-String)
Write-CrlfFile (Join-Path $runRoot 'commits.txt') $commitLog

$nativePath = Join-Path $gameRoot 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$current = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') | ConvertFrom-Json
$nativeHash = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash
if ($nativeHash -ne $current.currentNativeHash) { throw 'Native DLL hash changed. A full new hash-bound baseline is required; the Script-Extender-only fast path is forbidden.' }

if (-not $ExtenderDir) { $ExtenderDir = $installedExtender }
if ($SkipExtenderBuild) { $ExtenderDir = Resolve-SEExtenderDirectory $ExtenderDir $installedExtender }

$state = @{ TargetCommit=$TargetCommit; TreeHash=$treeHash; CompletedBuilds=@(); ExtenderBuilt=$false; BaselineValidated=$false }
if ($Resume -and (Test-Path -LiteralPath $statePath)) {
    $oldState = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
    if ($oldState.TargetCommit -ne $TargetCommit) { throw 'Resume state belongs to another target commit.' }
    $state.ExtenderBuilt = [bool]$oldState.ExtenderBuilt
    $state.BaselineValidated = [bool]$oldState.BaselineValidated
    $state.CompletedBuilds = @($oldState.CompletedBuilds)
}

if (-not $SkipExtenderBuild -and -not $state.ExtenderBuilt) {
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Building Script Extender $NewVersion..."
    & (Join-Path $extenderRoot 'build.bat') /nopause
    if ($LASTEXITCODE -ne 0) { throw "Script Extender build failed with exit code $LASTEXITCODE." }
    $state.ExtenderBuilt = $true; Save-State $state
}
$selectedExtender = Assert-TargetExtender $ExtenderDir

$explicitVersions = @{}
$compatibilityPlan = $null
$plannedMods = @{}
if ($CompatibilityPlanFile) {
    $resolvedPlan = (Resolve-Path -LiteralPath $CompatibilityPlanFile).Path
    $compatibilityPlan = Get-Content -Raw -LiteralPath $resolvedPlan | ConvertFrom-Json
    if ([string]$compatibilityPlan.OldVersion -ne $OldVersion -or
        [string]$compatibilityPlan.NewVersion -ne $NewVersion -or
        [string]$compatibilityPlan.TargetCommit -ne $TargetCommit) {
        throw 'Compatibility plan identity does not match the requested update.'
    }
    foreach ($property in $compatibilityPlan.Mods.PSObject.Properties) {
        if (-not @($activeMods | Where-Object Name -eq $property.Name)) {
            throw "Compatibility plan references a missing or inactive mod: $($property.Name)."
        }
        $plannedMods[$property.Name] = $property.Value
    }
    Write-CrlfFile (Join-Path $runRoot 'compatibility-plan.json') (($compatibilityPlan | ConvertTo-Json -Depth 30) + [Environment]::NewLine)
}
elseif ($VersionMode -eq 'Explicit') {
    if (-not $VersionsFile) { throw '-VersionsFile is required for VersionMode Explicit.' }
    $map = Get-Content -Raw -LiteralPath $VersionsFile | ConvertFrom-Json
    foreach ($property in $map.PSObject.Properties) { $explicitVersions[$property.Name] = [string]$property.Value }
}
foreach ($mod in $activeMods) {
    $manifestPath = Join-Path $workspace $mod.Manifest
    $json = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $targetModVersion = [string]$json.Version
    $targetMinimum = [string]$json.MinimumScriptExtenderVersion
    $changes = @()
    $hasPlan = $plannedMods.ContainsKey($mod.Name)
    if ($hasPlan) {
        $entry = $plannedMods[$mod.Name]
        $targetModVersion = [string]$entry.Version
        $targetMinimum = [string]$entry.MinimumScriptExtenderVersion
        $changes = @($entry.Changes | ForEach-Object { [string]$_ })
        if (-not $targetModVersion -or -not $targetMinimum -or -not $changes.Count) {
            throw "$($mod.Name) has an incomplete compatibility-plan entry."
        }
    }
    elseif (-not $CompatibilityPlanFile) {
        $alreadyAdjusted = [string]$json.MinimumScriptExtenderVersion -eq $NewVersion -and [string]$json.SerpChangelog[0].Changes[0] -eq $Changelog
        if ($VersionMode -eq 'Patch' -and -not $alreadyAdjusted) { $v=[version]$targetModVersion; $targetModVersion="$($v.Major).$($v.Minor).$($v.Build+1)" }
        if ($VersionMode -eq 'Explicit') { if (-not $explicitVersions.ContainsKey($mod.Name)) { throw "No explicit version for $($mod.Name)." }; $targetModVersion=$explicitVersions[$mod.Name] }
        $targetMinimum = $NewVersion
        $changes = @($Changelog)
        $hasPlan = $true
    }
    $topChanges = @()
    if ($json.SerpChangelog -and @($json.SerpChangelog).Count -gt 0) {
        $topChanges = @($json.SerpChangelog[0].Changes | ForEach-Object { [string]$_ })
    }
    $needsUpdate = $hasPlan -and (
        [string]$json.MinimumScriptExtenderVersion -ne $targetMinimum -or
        [string]$json.Version -ne $targetModVersion -or
        [string]$json.SerpChangelog[0].Version -ne $targetModVersion -or
        ($topChanges -join "`n") -cne ($changes -join "`n"))
    if ($needsUpdate) {
        $json.Version = $targetModVersion; $json.MinimumScriptExtenderVersion = $targetMinimum
        if (-not $json.SerpChangelog -or [string]$json.SerpChangelog[0].Version -ne $targetModVersion -or ($topChanges -join "`n") -cne ($changes -join "`n")) {
            $changeEntry=[pscustomobject]@{Version=$targetModVersion;Changes=$changes}
            $existingChangelog = @($json.SerpChangelog | Where-Object { $null -ne $_ })
            $newChangelog=@($changeEntry)+$existingChangelog
            if ($json.PSObject.Properties['SerpChangelog']) { $json.SerpChangelog=$newChangelog }
            else { $json | Add-Member -NotePropertyName SerpChangelog -NotePropertyValue $newChangelog }
        }
        Write-CrlfFile $manifestPath (($json | ConvertTo-Json -Depth 30) + [Environment]::NewLine)
    }
    if ($mod.Plugin) {
        $pluginPath=Join-Path $workspace $mod.Plugin; $text=[IO.File]::ReadAllText($pluginPath)
        $updatedText = Set-PluginMetadata $text $targetModVersion $targetMinimum
        if ($updatedText -cne $text) { Write-CrlfFile $pluginPath $updatedText }
    }
}

if ($PrepareOnly) {
    Write-Host "PASS: Prepared Script Extender $OldVersion-$NewVersion compatibility metadata without building."
    return
}

if (-not $SkipBaseline -and -not $state.BaselineValidated) {
    $baseline = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1'
    & $baseline UpdateForScriptExtender -ScriptExtenderCommit $TargetCommit -PreviousScriptExtenderCommit (& git -C $extenderRoot rev-list -n 1 $OldTag).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Semantic baseline update failed with exit code $LASTEXITCODE." }
    $state.BaselineValidated=$true; Save-State $state
}

$env:SHCDESE_EXTENDER_DIR = (Resolve-Path -LiteralPath $ExtenderDir).Path
try {
    Invoke-SECheckpointBuild $activeMods $workspace $runRoot $state ${function:Save-State} {
        Assert-TargetExtender $env:SHCDESE_EXTENDER_DIR | Out-Null
    }
}
finally { Remove-Item Env:SHCDESE_EXTENDER_DIR -ErrorAction SilentlyContinue }

$verification=@()
foreach ($mod in $activeMods) {
    $source = Join-Path $workspace $mod.Package; $installed = Join-Path (Join-Path $gameRoot 'BepInEx\plugins') $mod.Install
    if ($mod.Plugin -and -not (Test-Path -LiteralPath $installed -PathType Container)) { throw "$($mod.Name) is not installed: $installed" }
    $manifest=Get-Content -Raw -LiteralPath (Join-Path $workspace $mod.Manifest)|ConvertFrom-Json
    if ($manifest.PSObject.Properties['SerpChangelog'] -and @($manifest.SerpChangelog | Where-Object { $null -eq $_ }).Count) {
        throw "$($mod.Name) contains a null changelog entry."
    }
    $packageManifest=Get-Content -Raw -LiteralPath (Join-Path $source 'info.json')|ConvertFrom-Json
    if (($manifest|ConvertTo-Json -Depth 30 -Compress) -cne ($packageManifest|ConvertTo-Json -Depth 30 -Compress)) {
        throw "$($mod.Name) source and package manifests differ."
    }
    Assert-SEManifestExtenderRange $manifest $NewVersion $mod.Name
    $expectedMinimum = if ($plannedMods.ContainsKey($mod.Name)) { [string]$plannedMods[$mod.Name].MinimumScriptExtenderVersion } else { [string]$manifest.MinimumScriptExtenderVersion }
    if ([string]$manifest.MinimumScriptExtenderVersion -ne $expectedMinimum) { throw "$($mod.Name) minimum version mismatch." }
    if ($mod.Plugin) {
        $pluginText=[IO.File]::ReadAllText((Join-Path $workspace $mod.Plugin))
        $pluginVersionMatch=[regex]::Match($pluginText,'PluginVersion\s*=\s*"([^"]+)"\s*;')
        if(-not $pluginVersionMatch.Success -or $pluginVersionMatch.Groups[1].Value -ne [string]$manifest.Version){throw "$($mod.Name) PluginVersion does not match info.json."}
        $dependency=Get-ScriptExtenderDependency $pluginText
        if($dependency.Version -ne [string]$manifest.MinimumScriptExtenderVersion){throw "$($mod.Name) BepInDependency does not match info.json minimum."}
        $localRelative=@(Get-ChildItem -LiteralPath $source -Recurse -File|ForEach-Object{$_.FullName.Substring($source.Length+1)})
        foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
            $rel=$file.FullName.Substring($source.Length+1);$target=Join-Path $installed $rel
            $sourceHash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $targetHash=if(Test-Path -LiteralPath $target -PathType Leaf){(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash}else{$null}
            if(-not $targetHash -or $sourceHash -ne $targetHash){throw "$($mod.Name) package mismatch: $rel"}
        }
        $unexpected=@(Get-ChildItem -LiteralPath $installed -Recurse -File|ForEach-Object{$_.FullName.Substring($installed.Length+1)}|Where-Object{$_ -notin $localRelative -and $_ -notmatch '^LobbyModSettings([\\/]|$)'})
        if($unexpected){throw "$($mod.Name) has unexpected installed files: $($unexpected -join ', ')"}
        $dll=Join-Path $source ($mod.Name+'.dll')
        if(-not(Test-Path -LiteralPath $dll -PathType Leaf)){throw "$($mod.Name) primary assembly missing: $dll"}
    }
    $verification += [pscustomobject]@{Name=$mod.Name;Version=$manifest.Version;Minimum=$manifest.MinimumScriptExtenderVersion;Maximum=$manifest.MaximumScriptExtenderVersion;Built=[bool]$mod.Plugin}
}
Write-CrlfFile (Join-Path $runRoot 'verification.json') (($verification|ConvertTo-Json -Depth 5)+[Environment]::NewLine)
Write-Host "PASS: Script Extender $NewVersion and $(@($activeMods|Where-Object Plugin).Count) active C# runtime mods verified; $($inactiveMods.Count) inactive inventory entries skipped. Extender SHA-256: $($selectedExtender.Hash)"
