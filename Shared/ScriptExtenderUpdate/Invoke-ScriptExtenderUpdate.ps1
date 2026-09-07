[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OldVersion,
    [Parameter(Mandatory)][string]$NewVersion,
    [Parameter(Mandatory)][string]$OldTag,
    [Parameter(Mandatory)][string]$NewTag,
    [Parameter(Mandatory)][string]$TargetCommit,
    [ValidateSet('Existing','Patch','Explicit')][string]$VersionMode = 'Existing',
    [string]$VersionsFile,
    [string]$Changelog = "Adjusted to Script Extender $NewVersion.",
    [string]$ExtenderDir,
    [switch]$SkipExtenderBuild,
    [switch]$SkipBaseline,
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

Set-Location -LiteralPath $workspace
if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) { throw "Inventory missing: $inventoryPath" }
$mods = @(Get-Content -Raw -LiteralPath $inventoryPath | ConvertFrom-Json)
if ($mods.Count -ne 28 -or @($mods | Where-Object Plugin).Count -ne 27) { throw 'Inventory must contain 28 runtime mods and 27 C# plugins.' }
$duplicateNames = @($mods | Group-Object Name | Where-Object Count -ne 1)
$duplicateGuids = @($mods | Group-Object Guid | Where-Object Count -ne 1)
if ($duplicateNames -or $duplicateGuids) { throw 'Inventory contains duplicate names or GUIDs.' }
foreach ($mod in $mods) {
    foreach ($property in @('Manifest','Package')) { if (-not (Test-Path -LiteralPath (Join-Path $workspace $mod.$property))) { throw "$($mod.Name): missing $property" } }
    if ($mod.Plugin) { foreach ($property in @('Plugin','Project','BuildDriver')) { if (-not (Test-Path -LiteralPath (Join-Path $workspace $mod.$property))) { throw "$($mod.Name): missing $property" } } }
    $sourceManifest = Get-Content -Raw -LiteralPath (Join-Path $workspace $mod.Manifest) | ConvertFrom-Json
    Assert-SEManifestExtenderRange $sourceManifest $NewVersion $mod.Name
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
$diffReport = [ordered]@{ oldTag=$OldTag; newTag=$NewTag; targetCommit=$TargetCommit; treeHash=$treeHash; changedFiles=$changedFiles; categories=$categories }
Write-CrlfFile (Join-Path $runRoot 'analysis.json') (($diffReport | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
& git -C $extenderRoot log --oneline --reverse "$OldTag..$NewTag" | Set-Content -LiteralPath (Join-Path $runRoot 'commits.txt') -Encoding utf8

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
if ($VersionMode -eq 'Explicit') {
    if (-not $VersionsFile) { throw '-VersionsFile is required for VersionMode Explicit.' }
    $map = Get-Content -Raw -LiteralPath $VersionsFile | ConvertFrom-Json
    foreach ($property in $map.PSObject.Properties) { $explicitVersions[$property.Name] = [string]$property.Value }
}
foreach ($mod in $mods) {
    $manifestPath = Join-Path $workspace $mod.Manifest
    $json = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $targetModVersion = [string]$json.Version
    $alreadyAdjusted = [string]$json.MinimumScriptExtenderVersion -eq $NewVersion -and [string]$json.SerpChangelog[0].Changes[0] -eq $Changelog
    if ($VersionMode -eq 'Patch' -and -not $alreadyAdjusted) { $v=[version]$targetModVersion; $targetModVersion="$($v.Major).$($v.Minor).$($v.Build+1)" }
    if ($VersionMode -eq 'Explicit') { if (-not $explicitVersions.ContainsKey($mod.Name)) { throw "No explicit version for $($mod.Name)." }; $targetModVersion=$explicitVersions[$mod.Name] }
    $needsUpdate = [string]$json.MinimumScriptExtenderVersion -ne $NewVersion -or [string]$json.Version -ne $targetModVersion -or [string]$json.SerpChangelog[0].Changes[0] -ne $Changelog
    if ($needsUpdate) {
        $json.Version = $targetModVersion; $json.MinimumScriptExtenderVersion = $NewVersion
        if (-not $json.SerpChangelog -or [string]$json.SerpChangelog[0].Version -ne $targetModVersion -or [string]$json.SerpChangelog[0].Changes[0] -ne $Changelog) {
            $entry=[pscustomobject]@{Version=$targetModVersion;Changes=@($Changelog)}; $json.SerpChangelog=@($entry)+@($json.SerpChangelog)
        }
        Write-CrlfFile $manifestPath (($json | ConvertTo-Json -Depth 30) + [Environment]::NewLine)
    }
    if ($mod.Plugin) {
        $pluginPath=Join-Path $workspace $mod.Plugin; $text=[IO.File]::ReadAllText($pluginPath)
        $text=[regex]::Replace($text,'(PluginVersion\s*=\s*")[^"]+("\s*;)',{ param($match) $match.Groups[1].Value + $targetModVersion + $match.Groups[2].Value },1)
        $text=[regex]::Replace($text,'(BepInDependency\([^\r\n]*,\s*")[^"]+("\s*\)\])',{ param($match) $match.Groups[1].Value + [string]$json.MinimumScriptExtenderVersion + $match.Groups[2].Value })
        Write-CrlfFile $pluginPath $text
    }
}

if (-not $SkipBaseline -and -not $state.BaselineValidated) {
    $baseline = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1'
    & $baseline UpdateForScriptExtender -ScriptExtenderCommit $TargetCommit -PreviousScriptExtenderCommit (& git -C $extenderRoot rev-list -n 1 $OldTag).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Semantic baseline update failed with exit code $LASTEXITCODE." }
    $state.BaselineValidated=$true; Save-State $state
}

$env:SHCDESE_EXTENDER_DIR = (Resolve-Path -LiteralPath $ExtenderDir).Path
try {
    Invoke-SECheckpointBuild $mods $workspace $runRoot $state ${function:Save-State} {
        Assert-TargetExtender $env:SHCDESE_EXTENDER_DIR | Out-Null
    }
}
finally { Remove-Item Env:SHCDESE_EXTENDER_DIR -ErrorAction SilentlyContinue }

$verification=@()
foreach ($mod in $mods) {
    $source = Join-Path $workspace $mod.Package; $installed = Join-Path (Join-Path $gameRoot 'BepInEx\plugins') $mod.Install
    if ($mod.Plugin -and -not (Test-Path -LiteralPath $installed -PathType Container)) { throw "$($mod.Name) is not installed: $installed" }
    $manifest=Get-Content -Raw -LiteralPath (Join-Path $workspace $mod.Manifest)|ConvertFrom-Json
    Assert-SEManifestExtenderRange $manifest $NewVersion $mod.Name
    if ([string]$manifest.MinimumScriptExtenderVersion -ne $NewVersion) { throw "$($mod.Name) minimum version mismatch." }
    if ($mod.Plugin) {
        $pluginText=[IO.File]::ReadAllText((Join-Path $workspace $mod.Plugin))
        $pluginVersionMatch=[regex]::Match($pluginText,'PluginVersion\s*=\s*"([^"]+)"\s*;')
        if(-not $pluginVersionMatch.Success -or $pluginVersionMatch.Groups[1].Value -ne [string]$manifest.Version){throw "$($mod.Name) PluginVersion does not match info.json."}
        $dependencyMatch=[regex]::Match($pluginText,'BepInDependency\([^,\r\n]+,\s*"([^"]+)"\s*\)\]')
        if(-not $dependencyMatch.Success -or $dependencyMatch.Groups[1].Value -ne [string]$manifest.MinimumScriptExtenderVersion){throw "$($mod.Name) BepInDependency does not match info.json minimum."}
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
Write-Host "PASS: Script Extender $NewVersion and $(@($mods|Where-Object Plugin).Count) C# runtime mods verified. Extender SHA-256: $($selectedExtender.Hash)"
