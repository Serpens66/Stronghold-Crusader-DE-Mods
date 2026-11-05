param([Parameter(Mandatory)][string]$BuildRecord,
    [Parameter(Mandatory)][string]$ApiPackageRecord,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
. (Join-Path $workspace 'APIShared\tools\WorkshopPackage.ps1')
. (Join-Path $workspace 'APIShared\tools\DependencyBootstrapPackage.ps1')
Assert-ShortWorkDirectory $EvidenceDirectory
if (Test-Path -LiteralPath (Join-Path $EvidenceDirectory 'installation.json')) { throw 'Installation evidence already exists; allocate a fresh run.' }
$game = [IO.Path]::GetFullPath($GameDir).TrimEnd('\','/')
$build = Get-Content -LiteralPath $BuildRecord -Raw | ConvertFrom-Json
$frozen = Get-Content -LiteralPath $ApiPackageRecord -Raw | ConvertFrom-Json
$sdkPackage = [IO.Path]::GetFullPath($frozen.PackageDirectory)
Assert-ApiBootstrapPair $sdkPackage
if ((Get-Content -LiteralPath (Join-Path $sdkPackage 'BepInEx\plugins\APIShared_Serp\info.json') -Raw | ConvertFrom-Json).Version -ne $frozen.ApiSharedVersion) {
    throw 'SDK record and package versions differ.'
}
$modPackage = Join-Path (Split-Path -Parent $build.Package) 'package'
foreach ($source in $build.Sources) {
    if ((Get-ApiFileHash $source.Path) -ne $source.Sha256) { throw "Mod sources changed after build: $($source.Path)" }
}
$payload = [Collections.Generic.List[object]]::new()
foreach ($entry in $frozen.Files) {
    $relative = $entry.Path.Replace('/','\')
    if ([IO.Path]::IsPathRooted($relative) -or $relative.Split('\') -contains '..') { throw 'Unsafe SDK payload path.' }
    if ($relative.StartsWith('BepInEx\plugins\APIShared_Serp\', [StringComparison]::OrdinalIgnoreCase) -or
        $relative -eq 'BepInEx\patchers\APIShared.DependencyBootstrap.dll') {
        $payload.Add([pscustomobject]@{Relative=$relative;Source=(Join-Path $sdkPackage $relative);Sha256=$entry.Sha256})
    } else { throw 'Unexpected SDK payload target.' }
}
if (@($payload | Where-Object Relative -eq 'BepInEx\plugins\APIShared_Serp\APIShared.dll').Count -ne 1 -or
    @($payload | Where-Object Relative -eq 'BepInEx\patchers\APIShared.DependencyBootstrap.dll').Count -ne 1) {
    throw 'A complete matching APIShared plugin/bootstrap package is required.'
}
$modBase = [IO.Path]::GetFullPath($modPackage).TrimEnd('\','/') + '\'
foreach ($entry in $build.Files) {
    $full = [IO.Path]::GetFullPath($entry.Path)
    if (!$full.StartsWith($modBase,[StringComparison]::OrdinalIgnoreCase)) { throw 'Mod payload escaped its build package.' }
    $relative = $full.Substring($modBase.Length).Replace('/','\')
    if (!$relative.StartsWith('BepInEx\plugins\AutoSendTest_Serp\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected mod payload target.' }
    $payload.Add([pscustomobject]@{Relative=$relative;Source=$full;Sha256=$entry.Sha256})
}
if (!$payload.Count) { throw 'Empty payload.' }
if ((Get-ApiFileHash (Join-Path $sdkPackage 'BepInEx\plugins\APIShared_Serp\APIShared.dll')) -ne $build.SdkSha256) { throw 'The compiled mod used a different SDK.' }
foreach ($entry in $payload) {
    if ((Get-ApiFileHash $entry.Source) -ne $entry.Sha256) { throw "Payload hash changed: $($entry.Relative)" }
    $target = [IO.Path]::GetFullPath((Join-Path $game $entry.Relative))
    if (!$target.StartsWith($game + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installation target.' }
    $entry | Add-Member -NotePropertyName Target -NotePropertyValue $target
}
Add-Type -Path (Join-Path $game 'BepInEx\core\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $modPackage 'BepInEx\plugins\AutoSendTest_Serp\AutoSendTest.dll'))
try {
    $marker = @($assembly.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and
        $_.ConstructorArguments.Count -eq 2 -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' -and $_.ConstructorArguments[1].Value -eq 'true' })
    $diagnosticRuntime = @($assembly.MainModule.Types | Where-Object { $_.FullName -eq 'Shared.DevDiagRuntime' })
    if ($build.DevDiag) {
        if ($marker.Count -ne 1 -or $diagnosticRuntime.Count -ne 1) { throw 'Diagnostic compilation marker missing.' }
    } elseif ($marker.Count -ne 0 -or $diagnosticRuntime.Count -ne 0) { throw 'Diagnostic code found in normal artifact.' }
} finally { $assembly.Dispose() }
function Snapshot {
    @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx\plugins'),(Join-Path $game 'BepInEx\patchers') -Recurse -File |
        Sort-Object FullName | ForEach-Object { [pscustomobject]@{Path=$_.FullName;Sha256=(Get-ApiFileHash $_.FullName)} })
}
if (Get-Process | Where-Object { $_.ProcessName -match 'Stronghold|Crusader' }) { throw 'Close the game before installing.' }
$before = Snapshot
$before | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installed-before.json') -Encoding UTF8
$targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $payload) {
    if (!$targets.Add($entry.Target)) { throw 'Duplicate installation target.' }
    if (Test-Path -LiteralPath $entry.Target) {
        $backup = Join-Path $EvidenceDirectory ('backup\' + $entry.Relative)
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $backup))
        Copy-Item -LiteralPath $entry.Target -Destination $backup
    }
}
if (Get-Process | Where-Object { $_.ProcessName -match 'Stronghold|Crusader' }) { throw 'The game started during preparation; nothing installed.' }
$written = [Collections.Generic.List[object]]::new()
try {
    foreach ($entry in $payload) {
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $entry.Target))
        # Copy can fail after partially overwriting a file; register it first.
        $written.Add($entry)
        Copy-Item -LiteralPath $entry.Source -Destination $entry.Target -Force
        if ((Get-ApiFileHash $entry.Target) -ne $entry.Sha256) { throw "Installed file mismatch: $($entry.Target)" }
    }
    Assert-ApiBootstrapPair $game
} catch {
    # Restore only files this invocation actually touched; preserve unrelated files.
    foreach ($entry in $written) {
        $backup = Join-Path $EvidenceDirectory ('backup\' + $entry.Relative)
        if (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $entry.Target -Force }
        elseif (Test-Path -LiteralPath $entry.Target) { Remove-Item -LiteralPath $entry.Target -Force }
    }
    throw
}
$after = Snapshot
$after | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installed-after.json') -Encoding UTF8
$otherBefore = @($before | Where-Object { !$targets.Contains($_.Path) } | ForEach-Object { $_.Path + '|' + $_.Sha256 })
$otherAfter = @($after | Where-Object { !$targets.Contains($_.Path) } | ForEach-Object { $_.Path + '|' + $_.Sha256 })
if (Compare-Object $otherBefore $otherAfter) { throw 'Unrelated installed files changed; inspect snapshots.' }
[ordered]@{InstalledUtc=[DateTime]::UtcNow.ToString('o');Installed=$true;Diagnostic=[bool]$build.DevDiag;GameDir=$game;
    BuildRecord=[IO.Path]::GetFullPath($BuildRecord);ApiPackageRecord=[IO.Path]::GetFullPath($ApiPackageRecord);ApiSharedVersion=$frozen.ApiSharedVersion;Files=$payload;
    UnrelatedFilesVerified=$otherBefore.Count;GameAcceptance='Prepared, not executed; no game launched.'} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installation.json') -Encoding UTF8
Write-Host "PASS: $($payload.Count) files installed; $($otherBefore.Count) unrelated installed files unchanged."
