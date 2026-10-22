[CmdletBinding()]
param([string]$GameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
# PowerShell 7's Roslyn compiler supports the existing production C# language level.
# This emits only an in-memory helper fixture; no mod, build driver or installation.
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this isolated helper test with PowerShell 7 (pwsh).' }
$probeRoot = Split-Path -Parent $PSScriptRoot
$harmonyPath = Join-Path $GameDirectory 'BepInEx\core\0Harmony.dll'
Add-Type -Path $harmonyPath
$references = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File | ForEach-Object { $_.FullName }) + @($harmonyPath)
Add-Type -Path @(
    (Join-Path $probeRoot 'src\PerfProbeLockTiming.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeLockHistory.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeRunTiming.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeAnalysisBuffers.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeSampleHistory.DevDiag.cs'),
    (Join-Path $PSScriptRoot 'DiagnosticHelperTests.cs')
    (Join-Path $PSScriptRoot 'RunIlFixture.cs')
) -ReferencedAssemblies $references -CompilerOptions '/nowarn:0649'
[PerfProbe.DiagnosticHelperTests]::Run()
