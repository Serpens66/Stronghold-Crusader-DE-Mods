param([Parameter(Mandatory=$true)][string]$AssemblyPath, [ValidateSet('true','false')][string]$DiagnosticMode)
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$marker = @($assembly.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' -and $_.ConstructorArguments[1].Value -eq 'true' }).Count -eq 1
$worker = $null -ne $assembly.GetType('BetterPathfindingTest.DiagnosticRuntime', $false)
$capture = $null -ne $assembly.GetType('BetterPathfindingTest.SnapshotCapture', $false)
$expected = $DiagnosticMode -eq 'true'
if ($marker -ne $expected -or $worker -ne $expected -or $capture -ne $expected) { throw "Binary diagnostic boundary mismatch: marker=$marker worker=$worker capture=$capture expected=$expected" }
foreach ($name in @('ManagedEvidence','ManagedContracts','CaptureState')) { if (($null -ne $assembly.GetType('BetterPathfindingTest.' + $name, $false)) -ne $expected) { throw "Diagnostic helper boundary mismatch: $name" } }
if (($null -ne $assembly.GetType('BetterPathfindingTest.Service.ResidentNavigation', $false)) -ne $expected) { throw 'Resident service diagnostic boundary mismatch.' }
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'client/ClientFramework.csproj') /nologo /t:Rebuild /verbosity:minimal "/p:HostAssembly=$([IO.Path]::GetFullPath($AssemblyPath))"
if ($LASTEXITCODE) { throw 'Independent Framework consumer compilation failed.' }
$client = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'client/bin/framework/NavigationClientProbe.dll'))
$client.GetType('NavigationClientProbe', $true).GetMethod('CheckInactive').Invoke($null, $null)
Write-Host 'PASS: public API exists and remains explicitly inactive before diagnostic runtime startup.'
$manifest = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $AssemblyPath) 'info.json') -Raw | ConvertFrom-Json
$binaryVersion = $assembly.GetName().Version
if ([version]$manifest.Version -ne [version]("{0}.{1}.{2}" -f $binaryVersion.Major,$binaryVersion.Minor,$binaryVersion.Build)) { throw 'Manifest/assembly version mismatch.' }
if (@($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -in @('BugfixesAndQoL','MoatMove','Newtonsoft.Json','System.Text.Json') }).Count) { throw 'Forbidden runtime dependency.' }
Write-Host "PASS: binary diagnostic boundary; devdiag=$expected; no consumer/oracle assembly dependency."

$nativePath = Join-Path (Split-Path -Parent $AssemblyPath) 'BetterPathfindingNative.dll'
if ((Test-Path -LiteralPath $nativePath) -ne $expected) { throw 'Native diagnostic backend package boundary mismatch.' }
if ($expected) {
    $bytes = [IO.File]::ReadAllBytes($nativePath)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw 'Native backend must be x64.' }
    $escapedPath = $nativePath.Replace('"', '""')
    Add-Type -TypeDefinition ('using System.Runtime.InteropServices; public static class PrivateNativeBoundary { [DllImport(@"' + $escapedPath + '", CallingConvention=CallingConvention.Cdecl)] public static extern int bpf_contract(); }')
    if ([PrivateNativeBoundary]::bpf_contract() -ne 0x04003828) { throw 'Private native ABI contract mismatch.' }
}
Write-Host 'PASS: private native package boundary and x64 ABI.'
