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
# Execute the production installer and config binding with only the patch publication
# boundary replaced. Do not patch the user's game or duplicate the selection logic.
$runtime = [IO.File]::ReadAllText((Join-Path $probeRoot 'src\PerfProbePlugin.DevDiag.cs'))
$install = [regex]::Match($runtime, '(?ms)^        private static void InstallRunTiming\(\).*?^        }').Value
$setting = [regex]::Match($runtime, '(?m)^        internal static bool RunPhaseTiming = .*?;').Value
$binding = [regex]::Match($runtime, '(?s)PerfProbeRuntime\.RunPhaseTiming = config\.Bind\("Spikes", "RunPhaseTiming".*?\)\.Value;').Value
if (!$install -or !$setting -or !$binding) { throw 'Production run timing installer/config could not be extracted.' }
$fixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'RunTimingInstallTests.cs')).Replace('/* INSTALL */', $install).Replace('/* SETTING */', $setting).Replace('/* BINDING */', $binding)
Add-Type -TypeDefinition $fixture -ReferencedAssemblies $references
[PerfProbeInstallFixture.RunTimingInstallTests]::Run()
Add-Type -Path @(
    (Join-Path $probeRoot 'src\PerfProbeLockTiming.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeLockHistory.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeRunTiming.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeAnalysisBuffers.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeSampleHistory.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeCadence.DevDiag.cs'),
    (Join-Path $probeRoot 'src\PerfProbeTickGap.DevDiag.cs'),
    (Join-Path $PSScriptRoot 'CadenceTests.cs'),
    (Join-Path $PSScriptRoot 'TickGapTests.cs'),
    (Join-Path $PSScriptRoot 'DiagnosticHelperTests.cs')
    (Join-Path $PSScriptRoot 'RunIlFixture.cs')
) -ReferencedAssemblies $references -CompilerOptions '/nowarn:0649'
[PerfProbe.DiagnosticHelperTests]::Run()
[PerfProbe.CadenceTests]::Run()
[PerfProbe.TickGapTests]::Run()
