param([Parameter(Mandatory)][string]$BuildRecord,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$mutex = [Threading.Mutex]::new($false, 'Local\SHCDE.InGameTest.3024040')
$locked = $false
try {
try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
if (!$locked) { throw 'Another game test owns the installation window.' }
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
. (Join-Path $workspace 'APIShared\tools\WorkshopPackage.ps1')
. (Join-Path $workspace 'APIShared\tools\DependencyBootstrapPackage.ps1')
. (Join-Path $PSScriptRoot 'InstallTransaction.ps1')
Assert-ShortWorkDirectory $EvidenceDirectory
if (Test-Path -LiteralPath (Join-Path $EvidenceDirectory 'installation.json')) { throw 'Installation evidence already exists; allocate a fresh run.' }
$game = [IO.Path]::GetFullPath($GameDir).TrimEnd('\','/')
$build = Get-Content -LiteralPath $BuildRecord -Raw | ConvertFrom-Json
# This consumer installer never replaces the independently maintained SDK.
Assert-ApiBootstrapPair $game
$sdkFile = Join-Path $game 'BepInEx\plugins\APIShared_Serp\APIShared.dll'
$sdkVersion = (Get-Content -LiteralPath (Join-Path $game 'BepInEx\plugins\APIShared_Serp\info.json') -Raw | ConvertFrom-Json).Version
if ((Get-ApiFileHash $sdkFile) -ne $build.SdkSha256) { throw 'Build against the currently installed SDK before installing.' }
$modPackage = Join-Path (Split-Path -Parent $build.Package) 'package'
foreach ($source in $build.Sources) {
    if ((Get-ApiFileHash $source.Path) -ne $source.Sha256) { throw "Mod sources changed after build: $($source.Path)" }
}
$payload = [Collections.Generic.List[object]]::new()
$modBase = [IO.Path]::GetFullPath($modPackage).TrimEnd('\','/') + '\'
foreach ($entry in $build.Files) {
    $full = [IO.Path]::GetFullPath($entry.Path)
    if (!$full.StartsWith($modBase,[StringComparison]::OrdinalIgnoreCase)) { throw 'Mod payload escaped its build package.' }
    $relative = $full.Substring($modBase.Length).Replace('/','\')
    if (!$relative.StartsWith('BepInEx\plugins\AutoSendTest_Serp\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected mod payload target.' }
    $payload.Add([pscustomobject]@{Relative=$relative;Source=$full;Sha256=$entry.Sha256})
}
if (!$payload.Count) { throw 'Empty payload.' }
foreach ($entry in $payload) {
    if ((Get-ApiFileHash $entry.Source) -ne $entry.Sha256) { throw "Payload hash changed: $($entry.Relative)" }
    $target = [IO.Path]::GetFullPath((Join-Path $game $entry.Relative))
    if (!$target.StartsWith($game + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installation target.' }
    $entry | Add-Member -NotePropertyName Target -NotePropertyValue $target
}
# Unknown or user-edited retired patches stop here, before evidence/backup/target writes.
$plan = New-AutoSendInstallPlan -GameDir $game -Payload $payload.ToArray()
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $modPackage 'BepInEx\plugins\AutoSendTest_Serp\AutoSendTest.dll'))
try {
    $marker = @($assembly.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and
        $_.ConstructorArguments.Count -eq 2 -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' -and $_.ConstructorArguments[1].Value -eq 'true' })
    $diagnosticRuntime = @($assembly.MainModule.Types | Where-Object { $_.FullName -eq 'Shared.DevDiagRuntime' })
    if ($build.DevDiag -or $marker.Count -ne 0 -or $diagnosticRuntime.Count -ne 0) { throw 'Only a normal artifact may be installed.' }
} finally { $assembly.Dispose() }
function Snapshot {
    @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx\plugins'),(Join-Path $game 'BepInEx\patchers') -Recurse -File |
        Sort-Object FullName | ForEach-Object { [pscustomobject]@{Path=$_.FullName;Sha256=(Get-ApiFileHash $_.FullName)} })
}
if (Get-Process | Where-Object { $_.ProcessName -match 'Stronghold|Crusader' }) { throw 'Close the game before installing.' }
$before = Snapshot
$before | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installed-before.json') -Encoding UTF8
try {
    $transaction = Invoke-AutoSendInstallTransaction -Plan $plan -EvidenceDirectory $EvidenceDirectory -BeforeWrite {
        if (Get-Process | Where-Object { $_.ProcessName -match 'Stronghold|Crusader' }) { throw 'The game started during preparation; nothing installed.' }
    } -ValidateAfter {
        Assert-ApiBootstrapPair $game
        $after = Snapshot
        $otherBefore = @($before | Where-Object { !$plan.Targets.Contains($_.Path) } | ForEach-Object { $_.Path + '|' + $_.Sha256 })
        $otherAfter = @($after | Where-Object { !$plan.Targets.Contains($_.Path) } | ForEach-Object { $_.Path + '|' + $_.Sha256 })
        if (Compare-Object $otherBefore $otherAfter) { throw 'Unrelated installed files changed; inspect snapshots.' }
        $after | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installed-after.json') -Encoding UTF8
        [ordered]@{InstalledUtc=[DateTime]::UtcNow.ToString('o');Installed=$true;Diagnostic=[bool]$build.DevDiag;GameDir=$game;
            BuildRecord=[IO.Path]::GetFullPath($BuildRecord);ApiSharedVersion=$sdkVersion;ApiSharedChanged=$false;Files=$payload;
            RemovedFiles=@($plan.RemovedFiles);UnrelatedFilesVerified=$otherBefore.Count;
            GameAcceptance='Prepared, not executed; no game launched.'} |
            ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'installation.json') -Encoding UTF8
        [pscustomobject]@{UnrelatedCount=$otherBefore.Count}
    }
} catch {
    # A failed final evidence write must not leave a success marker after rollback.
    $marker = Join-Path $EvidenceDirectory 'installation.json'
    if (Test-Path -LiteralPath $marker -PathType Leaf) { Remove-Item -LiteralPath $marker -Force }
    throw
}
Write-Host "PASS: $($payload.Count) files installed, $($transaction.RemovedFiles.Count) obsolete files removed; $($transaction.Validation.UnrelatedCount) unrelated installed files unchanged."
} finally { if ($locked) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
