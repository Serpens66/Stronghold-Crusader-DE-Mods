param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $toolDirectory)))
Set-Location -LiteralPath $workspace
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $toolDirectory 'Test-SwitchExport.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Controlled Ghidra switch export regression failed.' }
& $python -m unittest discover -s $toolDirectory -p 'test_*knowledge.py' -v
if ($LASTEXITCODE -ne 0) { throw 'Existing knowledge regressions failed.' }
foreach ($pattern in @('test_native_analysis.py','test_value_slices.py','test_memory_accesses.py','test_advanced_analysis.py','test_version_behaviour.py')) {
    & $python -m unittest discover -s $toolDirectory -p $pattern -v
    if ($LASTEXITCODE -ne 0) { throw "Analysis regressions failed: $pattern" }
}
$testArguments = @((Join-Path $toolDirectory 'validate_native_analysis.py'))
if ($Restore) { $testArguments += '--restore' }
& $python @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Offline analysis integration acceptance failed.' }
