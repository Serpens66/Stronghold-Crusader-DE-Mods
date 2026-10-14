param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $toolDirectory)))
Set-Location -LiteralPath $workspace
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
& $python -m unittest discover -s $toolDirectory -p 'test_*knowledge.py' -v
if ($LASTEXITCODE -ne 0) { throw 'Existing knowledge regressions failed.' }
& $python -m unittest discover -s $toolDirectory -p 'test_native_analysis.py' -v
if ($LASTEXITCODE -ne 0) { throw 'Machine/flow regressions failed.' }
$testArguments = @((Join-Path $toolDirectory 'validate_native_analysis.py'))
if ($Restore) { $testArguments += '--restore' }
& $python @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Offline analysis integration acceptance failed.' }
