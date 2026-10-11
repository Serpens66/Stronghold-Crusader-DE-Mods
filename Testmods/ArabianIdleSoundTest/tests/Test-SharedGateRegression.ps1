[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$common = [IO.File]::ReadAllText((Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1'))
$start = $common.IndexOf("& (Join-Path `$PSScriptRoot 'Test-UnitAccess.ps1') -ModDirectory `$ModDirectory")
if ($start -lt 0) { throw 'Central UnitAccess gate absent' }
$end = $common.IndexOf("`n", $common.IndexOf("throw 'UnitAccess contract preflight failed.'", $start))
if ($end -lt 0) { throw 'Central failure propagation absent' }
# Execute the exact gate extracted from the production common preflight. Its
# child is replaced only to deterministically test success and nonzero exit.
$gate = [ScriptBlock]::Create("param(`$PSScriptRoot, `$ModDirectory)`r`n" + $common.Substring($start, $end - $start))
$fixtureDirectory = Join-Path $PSScriptRoot 'bin/SharedGateFixture'
[IO.Directory]::CreateDirectory($fixtureDirectory) | Out-Null
$child = @'
param([string]$ModDirectory)
if ($ModDirectory -eq 'fixture-bad') { exit 31 }
Write-Output $ModDirectory
'@
$child = $child.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
$childPath = Join-Path $fixtureDirectory 'Test-UnitAccess.ps1'
[IO.File]::WriteAllText($childPath, $child)
if ([IO.File]::ReadAllText($childPath) -cne $child) { throw 'Fixture readback mismatch' }
$scope = & $gate $fixtureDirectory 'fixture-good'
if ($scope -ne 'fixture-good') { throw 'Common gate did not forward mod scope' }
$rejected = $false
try { & $gate $fixtureDirectory 'fixture-bad' }
catch { $rejected = $_.Exception.Message -eq 'UnitAccess contract preflight failed.' }
if (-not $rejected) { throw 'Nonzero child exit did not stop common preflight' }
$paths = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'changed-paths.txt') | Where-Object { $_ -like '*build.bat' })
foreach ($relative in $paths) {
    $driver = [IO.File]::ReadAllText((Join-Path $workspace $relative))
    $entry = if ($relative.Replace('\','/') -eq 'Testmods/ArabianIdleSoundTest/build.bat') { 'verify.ps1' } else { 'Test-SharedBoundaries.ps1' }
    if ($driver -match 'Test-UnitAccess.ps1' -or -not $driver.Contains($entry)) { throw "Driver centralization regression: $relative" }
    $lines = $driver.Split([char]10)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i].Contains($entry) -and $lines[$i + 1].Trim() -ne 'if errorlevel 1 exit /b 1') {
            throw "Shared preflight failure does not abort driver: $relative"
        }
    }
}
Write-Host 'PASS: exact shared UnitAccess gate forwards scope and aborts on nonzero exit; centralized drivers preserve fail-closed build flow.'
