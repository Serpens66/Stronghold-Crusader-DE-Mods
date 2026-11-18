# Prepares a safe-install game test (patcher config install=on executes it at the next start).
#   -Variant 1|2           stage that payload variant and request its installation into plugins
#   -KeepRevision <n>      reset revision for settings.json in the staged package's keep list
#   -AddPlayerFiles        first change the installed payload like a player would: settings.json,
#                          config\user.cfg (both listed in apishared-keep.txt) and junk.log (not listed)
#   -InterruptAt <step>    run the swap now and kill it at that step (journal|carried|old-moved|new-moved)
#                          instead of requesting it: the next start must recover before loading
#   -Patcher               stage the freshly built patcher DLL to test its self-replacement
param([ValidateSet(0, 1, 2)][int]$Variant = 0, [int]$KeepRevision = 0, [switch]$AddPlayerFiles, [string]$InterruptAt = '', [switch]$Patcher)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$game = Get-ProbeGame
$root = Join-Path $game 'BepInEx\cache\WLPI'
$folder = $ProbePayloadGuid
$target = Join-Path $game "BepInEx\plugins\$folder"
$harness = Join-Path $PSScriptRoot '..\tests\InstallHarness\bin\InstallHarness.exe'
& dotnet msbuild (Join-Path $PSScriptRoot '..\tests\InstallHarness\InstallHarness.csproj') /t:Build /p:Configuration=Release /verbosity:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed.' }

if ($Patcher) {
    $built = Join-Path $PSScriptRoot '..\BepInEx\patchers\WorkshopLoadProbe.Patcher.dll'
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'p'))
    Copy-Item -LiteralPath $built -Destination (Join-Path $root 'p\WorkshopLoadProbe.Patcher.dll') -Force
    Write-Output ("Staged patcher " + (Get-ProbeSha256 $built).Substring(0, 16))
}
if ($AddPlayerFiles) {
    if (-not (Test-Path -LiteralPath $target)) { throw 'Payload is not installed yet.' }
    [void][IO.Directory]::CreateDirectory((Join-Path $target 'config'))
    Write-ProbeText (Join-Path $target 'config\user.cfg') @('player config')
    Write-ProbeText (Join-Path $target 'settings.json') @('player settings')
    Write-ProbeText (Join-Path $target 'junk.log') @('not listed, must disappear')
    Write-Output 'Player files added to the installed payload.'
}
if ($Variant -gt 0) {
    $itemOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'New-TestItem.ps1') -Variant $Variant -KeepRevision $KeepRevision
    $simulated = (($itemOutput | Where-Object { $_ -like 'Simulated item:*' }) -replace '^Simulated item:\s*', '').Trim()
    $version = "v$Variant-" + (Get-Date).ToString('HHmmss')
    $out = & $harness stage-item $simulated "BepInEx/plugins/$folder" (Join-Path $root "s\$folder") $version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Staging failed: $out" }
    $out | ForEach-Object { Write-Output $_ }
    $staged = Join-Path $root "s\$folder\$version"
    if ($InterruptAt) {
        & $harness apply-crash $target $staged (Join-Path $root "b\$folder") (Join-Path $root "j\$folder.journal") $InterruptAt 2>&1 | ForEach-Object { Write-Output $_ }
        Write-Output ("Interrupted at $InterruptAt (exit $LASTEXITCODE). Installed folder exists: " + (Test-Path -LiteralPath $target) +
            "; journal: " + (Test-Path -LiteralPath (Join-Path $root "j\$folder.journal")))
    } else {
        [void][IO.Directory]::CreateDirectory((Join-Path $root 'r'))
        Write-ProbeText (Join-Path $root "r\$folder.request") @('WLP-INSTALL-1', $folder, $staged)
        Write-Output "Requested installation of $version into plugins\$folder"
    }
}
