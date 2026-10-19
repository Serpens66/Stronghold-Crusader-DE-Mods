[CmdletBinding()]
param(
    [string]$Root = '',
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$MSBuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
)
# Behavior test for the DEVDIAG release protection: a fixture compiled with /devdiag must
# be rejected by the release gate; the same sources compiled normally must pass and contain
# no DevDiag call. It also checks the *.DevDiag.cs inclusion and the source inventory.
$ErrorActionPreference = 'Stop'
# PowerShell 5.1 has no $PSScriptRoot inside parameter defaults.
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\..' }
. (Join-Path $PSScriptRoot '..\Paths\WorkPaths.ps1')
. (Join-Path $PSScriptRoot 'DevDiagnostics.Common.ps1')
$Root = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
$cecil = Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'
$bepinex = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'
foreach ($required in @($MSBuild, $cecil, $bepinex)) { if (-not (Test-Path -LiteralPath $required)) { throw "Missing test prerequisite: $required" } }

function Write-CrLf([string]$Path, [string[]]$Lines) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, (($Lines + '') -join "`r`n"), [Text.UTF8Encoding]::new($false))
}

$work = New-ShortWorkDirectory -Root $Root -Kind t -Name 'devdiag-gate'
$helper = Join-Path $Root 'Shared\Runtime\Diagnostics\DevDiag.cs'
Write-CrLf (Join-Path $work 'Fixture.csproj') @(
    '<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">',
    '  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />',
    '  <PropertyGroup><Configuration>Debug</Configuration><Platform>AnyCPU</Platform><OutputType>Library</OutputType><AssemblyName>Fixture</AssemblyName><TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion><LangVersion>latest</LangVersion></PropertyGroup>',
    '  <ItemGroup>',
    '    <Reference Include="System" /><Reference Include="System.Core" />',
    '    <Compile Include="src\Feature.cs" />',
    ('    <Compile Include="' + $helper + '"><Link>Shared\Runtime\Diagnostics\DevDiag.cs</Link></Compile>'),
    ('    <Reference Include="BepInEx"><HintPath>' + $bepinex + '</HintPath><Private>false</Private></Reference>'),
    '  </ItemGroup>',
    '  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />',
    '</Project>')
Write-CrLf (Join-Path $work 'src\Feature.cs') @(
    'namespace Fixture',
    '{',
    '    public static class Feature',
    '    {',
    '        public static int Run(int value)',
    '        {',
    '            long started = 0;',
    '            Shared.DevDiag.Start(ref started);',
    '            Shared.DevDiag.Count("fixture.calls", Expensive(value));',
    '            Shared.DevDiag.Stop(ref started, "fixture.run");',
    '            return value + 1;',
    '        }',
    '        // Must never run in a normal build: [Conditional] removes the argument evaluation.',
    '        private static long Expensive(int value) { throw new System.InvalidOperationException("evaluated"); }',
    '    }',
    '}')
Write-CrLf (Join-Path $work 'src\Probe.DevDiag.cs') @(
    'namespace Fixture { internal static class DevelopmentOnlyProbe { internal const int Marker = 1; } }')

function Build-Fixture([string]$Name, [bool]$DevDiag) {
    $out = Join-Path $work ('out-' + $Name)
    $old = $env:SHCDE_DEVDIAG
    try {
        $env:SHCDE_DEVDIAG = if ($DevDiag) { '1' } else { $null }
        & $MSBuild (Join-Path $work 'Fixture.csproj') '/nologo' '/verbosity:minimal' "/p:OutputPath=$out/" "/p:BaseIntermediateOutputPath=$work\obj-$Name/" "/p:IntermediateOutputPath=$work\obj-$Name\bin/" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Fixture build '$Name' failed." }
    } finally { $env:SHCDE_DEVDIAG = $old }
    return $out
}

$failures = 0
function Check([bool]$Condition, [string]$Message) {
    if ($Condition) { Write-Host "PASS $Message" } else { Write-Host "FAIL $Message"; $script:failures++ }
}

$normal = Build-Fixture 'normal' $false
$diag = Build-Fixture 'devdiag' $true

$normalPassed = $true
try { Assert-NoDevDiagnosticAssemblies -Directory $normal -CecilPath $cecil } catch { $normalPassed = $false; Write-Host $_.Exception.Message }
Check $normalPassed 'normal build passes the release gate (no marker, no DevDiag call)'

$diagRejected = $false
try { Assert-NoDevDiagnosticAssemblies -Directory $diag -CecilPath $cecil } catch { $diagRejected = $_.Exception.Message -match 'SerpDevDiagnostics marker' -and $_.Exception.Message -match 'DevDiagRuntime' }
Check $diagRejected 'DEVDIAG build is rejected by marker and runtime type'

$normalAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $normal 'Fixture.dll')))
$diagAssembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $diag 'Fixture.dll')))
Check ($null -eq $normalAssembly.GetType('Fixture.DevelopmentOnlyProbe')) 'normal build excludes *.DevDiag.cs'
Check ($null -ne $diagAssembly.GetType('Fixture.DevelopmentOnlyProbe')) 'DEVDIAG build includes *.DevDiag.cs'

$found = @(Get-DevDiagnosticSources -Root $work -ModDirectory 'src')
Check ($found.Count -ge 4) "source inventory lists the fixture measurement points ($($found.Count))"
$sourceBlocked = $false
try { Assert-NoDevDiagnosticSources -Root $work -ModDirectory 'src' } catch { $sourceBlocked = $true }
Check $sourceBlocked 'main/release source gate blocks remaining measurement points'

if ($failures) { Write-Host "DEVDIAG gate test failed: $failures check(s). Evidence: $work"; exit 1 }
Write-Host "DEVDIAG gate test passed. Evidence: $work"
exit 0
