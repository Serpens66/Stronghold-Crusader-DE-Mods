try {
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../BranchBuild/BranchBuild.Common.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$adapterInput = 'set "API_SHARED_DIR=old"' + "`r`n" + 'set "LOCAL_API_SHARED_DIR=old"' + "`r`n"
$adapterOutput = Convert-BranchBatch $adapterInput 'Fixture/build.bat' ''
if ($adapterOutput -match '(?<!\r)\n|\r(?!\n)') { throw 'SDK adaptation corrupted CRLF.' }
$fixture = New-ShortWorkDirectory -Root $workspace -Name working-source -Transient
# The selected package is independent of the SDK inspection source root. Exercise the BAT contract.
$probe = Join-Path $fixture 'sdk-adapter.cmd'
$selectedPackage = Join-Path $fixture 'selected-sdk'
$previousPackage = $env:SHCDE_BRANCH_API_PACKAGE
try {
    $env:SHCDE_BRANCH_API_PACKAGE = $selectedPackage
    Write-BranchText $probe ("@echo off`r`n" + $adapterOutput + "echo %API_SHARED_DIR%`r`necho %LOCAL_API_SHARED_DIR%`r`n")
    $actual = @(& $env:ComSpec /d /c ('"' + $probe + '"'))
    if ($LASTEXITCODE -ne 0 -or $actual.Count -ne 2 -or @($actual | Where-Object { $_ -cne $selectedPackage }).Count) {
        throw 'BAT adapter did not preserve the explicit SDK package selection.'
    }
} finally { $env:SHCDE_BRANCH_API_PACKAGE = $previousPackage }
$repo = Join-Path $fixture 'repo'
[void][IO.Directory]::CreateDirectory($repo)
function Git-Test([string[]]$Arguments) { Invoke-BranchGit $repo $Arguments | Out-Null }
Git-Test @('init','-b','develop')
Git-Test @('config','user.name','Path fixture')
Git-Test @('config','user.email','fixture@example.invalid')
Write-BranchText (Join-Path $repo '.gitignore') ".work/`r`n"
Write-BranchText (Join-Path $repo 'Fixture/build.bat') "@echo off`r`ntype `"%~dp0source.txt`"`r`nexit /b 0`r`n"
Write-BranchText (Join-Path $repo 'Fixture/source.txt') 'committed'
Write-BranchText (Join-Path $repo 'Fixture/deleted.txt') 'delete me'
Write-BranchText (Join-Path $repo 'Fixture/unchanged.txt') 'unchanged'
Git-Test @('add','.')
Git-Test @('-c','commit.gpgsign=false','commit','-m','synthetic fixture')
$head = (Invoke-BranchGit $repo @('rev-parse','HEAD')) -join ''
Write-BranchText (Join-Path $repo 'Fixture/source.txt') 'working'
[IO.File]::Delete((Join-Path $repo 'Fixture/deleted.txt'))
Write-BranchText (Join-Path $repo 'Fixture/untracked.txt') 'untracked'
$copy = New-WorkingSource $repo
if ([IO.File]::ReadAllText((Join-Path $copy.Root 'Fixture/unchanged.txt')) -cne 'unchanged' -or
    [IO.File]::ReadAllText((Join-Path $copy.Root 'Fixture/source.txt')) -cne 'working' -or
    [IO.File]::ReadAllText((Join-Path $copy.Root 'Fixture/untracked.txt')) -cne 'untracked' -or
    [IO.File]::Exists((Join-Path $copy.Root 'Fixture/deleted.txt'))) { throw 'Working input copy mismatch.' }
$dispatcher = Join-Path $PSScriptRoot '../BranchBuild/Invoke-BranchBuild.ps1'
# Working builds may install (Test-BranchBuild covers that); only contradictory selections fail.
foreach ($arguments in @(@('/source:working','/branch:develop','/noinstall','/nopause'),@('/source:unknown','/noinstall','/nopause'))) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $dispatcher -Repository $repo -Driver Fixture/build.bat @arguments
    if ($LASTEXITCODE -ne 1) { throw 'Invalid working build arguments were accepted.' }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $dispatcher -Repository ($repo + '/.') -Driver Fixture/build.bat /source:working /noinstall /nopause
if ($LASTEXITCODE) { throw 'Working build failed.' }
$records = @(Get-Item -LiteralPath (Join-Path $repo '.release-output/builds/Fixture/working.build.json'))
if ($records.Count -ne 1) { throw 'Working build record missing.' }
$record = [IO.File]::ReadAllText($records[0].FullName) | ConvertFrom-Json
# The commit covers unchanged files; the record hashes exactly the working differences.
$recorded = @($record.SourceFiles | ForEach-Object Path | Sort-Object)
if (-not $record.WorkingSource -or -not $record.NoInstall -or ($recorded -join ',') -cne 'Fixture/deleted.txt,Fixture/source.txt,Fixture/untracked.txt' -or
    -not @($record.SourceFiles | Where-Object { $_.Path -eq 'Fixture/deleted.txt' -and $_.Deleted }).Count) { throw 'Working provenance incomplete.' }
if (((Invoke-BranchGit $repo @('rev-parse','HEAD')) -join '') -cne $head) { throw 'Working build moved HEAD.' }
Write-Host "PASS: working/deleted/untracked sources, forbidden argument combinations, no-install, changed-file hashes and unchanged HEAD; $fixture"

} finally {
    if (Get-Command Complete-TransientWorkDirectories -ErrorAction SilentlyContinue) { Complete-TransientWorkDirectories -Owner $PSCommandPath }
}
