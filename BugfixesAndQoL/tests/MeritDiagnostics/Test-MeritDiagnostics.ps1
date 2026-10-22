[CmdletBinding()]
param([switch]$Diagnostic)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run the in-memory cache fixture with PowerShell 7.' }
$modRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$harmonyPath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\core\0Harmony.dll'
Add-Type -Path $harmonyPath
$references = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File | ForEach-Object { $_.FullName }) + @($harmonyPath)
$sources = @((Join-Path $modRoot 'src\VanillaPerformance\MeritDataLockOptimization.cs'), (Join-Path $PSScriptRoot 'MeritFixture.cs'))
$compile = @{ ReferencedAssemblies = $references }
if ($Diagnostic) {
    $sources += Join-Path $modRoot 'src\VanillaPerformance\MeritDataLockOptimization.DevDiag.cs'
    $compile.CompilerOptions = @('/define:SERP_DEVDIAG')
}
# No mod artifact is emitted; the fixture replaces only external dependencies.
Add-Type -Path $sources @compile
[BugfixesAndQoL.VanillaPerformance.MeritFixtureRunner]::Run()
