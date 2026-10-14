param()
$ErrorActionPreference = 'Stop'
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $toolDirectory)))
Set-Location -LiteralPath $workspace
. (Join-Path $workspace 'Shared/Tools/Steam/SteamPackPolicy.ps1')
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$created = & $python (Join-Path $toolDirectory 'switch_fixture.py') (Join-Path $workspace '_inspect/NativeAnalysisFixtures')
if ($LASTEXITCODE -ne 0) { throw 'Source-owned fixture generation failed.' }
$fixture = ($created | Out-String) | ConvertFrom-Json
if ((Get-SteamPackFileSha256 $fixture.path) -ne $fixture.binaryHash) { throw 'Fixture hash mismatch.' }
$project = Join-Path $fixture.directory 'project'
[IO.Directory]::CreateDirectory($project) | Out-Null
$env:JAVA_HOME = Join-Path $workspace '.tools/temurin-jdk-21.0.12.1+1/jdk-21.0.12.1+1'
$headless = Join-Path $workspace '.tools/ghidra-12.1.3/ghidra_12.1.3_PUBLIC/support/analyzeHeadless.bat'
$arguments = @($project, 'SwitchFixture')
if (Test-Path -LiteralPath (Join-Path $project 'SwitchFixture.gpr')) { $arguments += '-process', 'switch.exe', '-noanalysis' }
else { $arguments += '-import', $fixture.path }
$exportProfile = Get-SteamPackFileSha256 (Join-Path $toolDirectory 'ExportNativeAnalysis.java')
$arguments += '-scriptPath', $toolDirectory, '-postScript', 'ConfigureSwitchFixture.java', $fixture.binaryHash
$arguments += '-postScript', 'ExportNativeAnalysis.java', (Join-Path $fixture.directory 'work'), $fixture.binaryHash, (Join-Path $fixture.directory 'selection.txt'), $exportProfile
$arguments += '-log', (Join-Path $fixture.directory 'ghidra-validation.log'), '-scriptlog', (Join-Path $fixture.directory 'export-validation.log')
& $headless @arguments
if ($LASTEXITCODE -ne 0) { throw 'Static Ghidra fixture analysis failed.' }
& $python (Join-Path $toolDirectory 'validate_switch_fixture.py') $fixture.directory $exportProfile
if ($LASTEXITCODE -ne 0) { throw 'Signed switch fixture contracts failed.' }
